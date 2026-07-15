using System;
using MadDr.RosterClient;
using UnityEngine;

/// <summary>
/// The fight state shared by every battlefield unit -- monsters and
/// tanks alike carry one. Holds health, a WeaponProfile (from the genome
/// for monsters, a fixed archetype for tanks), faction, and the cadence
/// gate; owners drive movement/targeting and call TryFire when a foe is
/// in range. This IS the combatant currency: targets, the health-bar
/// HUD, and separation all traffic in UnitCombat, so there's one place
/// that knows how much a unit can take and dish out.
/// </summary>
public class UnitCombat : MonoBehaviour
{
    public string Faction = "monster";   // "monster" (player) | "human" (tanks)
    public float MaxHealth = 100f;
    public float Health = 100f;
    public float Radius = 1.5f;          // separation + body half-width
    public float AimHeight = 1.5f;       // aim point above the transform origin
    public WeaponProfile Weapon;

    private float _cooldown;
    private float _battleTimer;           // > 0 = "in battle" (fired or hit recently)
    private Action _onDied;
    private bool _dead;

    public bool Alive { get { return !_dead && Health > 0f; } }
    public bool InBattle { get { return _battleTimer > 0f; } }
    public float HealthFraction { get { return Mathf.Clamp01(Health / Mathf.Max(1f, MaxHealth)); } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * AimHeight; } }
    public UnitCombat LastAttacker { get; private set; }

    public void Configure(string faction, float maxHp, float radius, float aimHeight,
        WeaponProfile weapon, Action onDied)
    {
        Faction = faction;
        MaxHealth = maxHp;
        Health = maxHp;
        Radius = radius;
        AimHeight = aimHeight;
        Weapon = weapon;
        _onDied = onDied;
        _dead = false;
    }

    public bool ReadyToFire { get { return _cooldown <= 0f && Weapon != null && Weapon.CanAttack; } }

    public bool InRange(Vector3 point)
    {
        if (Weapon == null) return false;
        var d = point - transform.position;
        d.y = 0f;
        return d.magnitude <= (float)Weapon.Range;
    }

    /// <summary>Fire at an enemy if off cooldown and in range. FX and
    /// damage are handled by WeaponFx (instant for beams/flame/melee,
    /// on-arrival for projectiles). Returns true if a shot went out.</summary>
    public bool TryFire(UnitCombat target, Vector3 muzzle)
    {
        if (!ReadyToFire || target == null || !target.Alive) return false;
        if (!InRange(target.AimPoint)) return false;
        WeaponFx.Fire(this, target, muzzle);
        _cooldown = (float)Weapon.Cadence;
        _battleTimer = 4f;
        return true;
    }

    /// <summary>Fire at a fixed point (a building). The caller applies the
    /// structural damage; this only throws the FX and starts the
    /// cooldown, so cadence still gates it.</summary>
    public bool TryFireAtPoint(Vector3 point, Vector3 muzzle)
    {
        if (!ReadyToFire) return false;
        if (!InRange(point)) return false;
        WeaponFx.FireAtPoint(this, point, muzzle);
        _cooldown = (float)Weapon.Cadence;
        _battleTimer = 4f;
        return true;
    }

    public void TakeDamage(float amount, UnitCombat source)
    {
        if (_dead) return;
        Health -= amount;
        _battleTimer = 4f;
        if (source != null) LastAttacker = source;
        if (Health <= 0f)
        {
            Health = 0f;
            _dead = true;
            if (_onDied != null) _onDied();
        }
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        if (_cooldown > 0f) _cooldown -= dt;
        if (_battleTimer > 0f) _battleTimer -= dt;
    }
}
