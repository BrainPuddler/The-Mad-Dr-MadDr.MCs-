using System;
using System.Collections.Generic;
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

    /// <summary>docs/26 (Special Attacks System): how heavy this unit
    /// reads to a mass-classifying effect (e.g. a web attack's human-vs-
    /// heavy branch) -- a continuous stat, not a per-species tag, so a
    /// new enemy type is classified correctly for free from whatever
    /// already sets its Radius/MaxHealth, with no new per-type lookup
    /// table to maintain. Default 1 (citizen/light-monster scale); Tank
    /// sets a heavier value explicitly (see Tank.cs).</summary>
    public float Mass = 1f;

    /// <summary>docs/26: this unit's equipped special attacks, each with
    /// its OWN independent runtime cooldown (SpecialAttackInstance) --
    /// ticked unconditionally in Update() below, exactly like _cooldown,
    /// so a cooldown keeps counting down through any AI state change on
    /// the owning MonsterAgent, not just while that ability's own order
    /// is active.</summary>
    public readonly List<SpecialAttackInstance> Abilities = new List<SpecialAttackInstance>();

    /// <summary>docs/25 Phase C: this unit's XZ velocity as of its owner's
    /// last Update() -- the "neighbours' last-known velocity" the
    /// migration plan's predictive-avoidance layer needs, published here so
    /// MonsterSteeringController can read a neighbour's velocity through
    /// the same UnitCombat handle separation/targeting already use, no new
    /// per-neighbour lookup. MonsterAgent writes it every frame (including
    /// zero when idle); Tank.cs never sets it (docs/25 keeps tanks out of
    /// scope for this plan) so a tank predictively reads as momentarily
    /// stationary -- a safe, conservative default, not a bug.</summary>
    public Vector3 LastVelocity;

    /// <summary>docs/25 Phase D: the "per-unit priority/yield flag the
    /// steering controller honours" the migration plan's approved
    /// architecture calls for. DeadlockManager sets these on a BLOCKER
    /// (never on the stalled unit itself -- see that class's header) when
    /// it's near a unit that's wanted to move and made no real progress for
    /// a while; `RuntimeCityBuilder.SteerFollowPath` reads them and, while
    /// `YieldUntil` hasn't passed, steers this unit toward `YieldTarget`
    /// instead of wherever its own order was taking it, briefly. Null/0 the
    /// rest of the time (the overwhelmingly common case) -- nothing reads
    /// these unless DeadlockManager has actually granted a yield.</summary>
    public Vector3? YieldTarget;
    public float YieldUntil;

    /// <summary>docs/26 Phase 5: true for a unit under (future) mind-
    /// control -- nothing sets this yet (no possession mechanic exists),
    /// but the field exists now so friendly-fire exclusions have
    /// somewhere to check "is this actually still an ally?" without a
    /// later breaking change. Default false is fully behavior-inert.
    /// See docs/26 "Possessed units and friendly fire."</summary>
    public bool IsPossessed;

    private float _cooldown;
    private float _battleTimer;           // > 0 = "in battle" (fired or hit recently)
    private Action _onDied;
    private bool _dead;

    /// <summary>docs/26 Phase 5: generic slow-status timer, deliberately
    /// on UnitCombat (not MonsterAgent or Tank) so it applies to every
    /// unit type uniformly -- a bred monster, a Tank, or anything added
    /// later -- with each mover reading SpeedMultiplier from its own
    /// movement-speed calculation. Same Time.deltaTime-decrement idiom
    /// as _cooldown/_battleTimer above.</summary>
    private float _slowRemaining;
    private float _slowMultiplier = 1f;

    /// <summary>docs/26 Phase 6: this unit's "being pulled toward a
    /// captor" state, or null when not caught. Lives on UnitCombat (not
    /// MonsterAgent/Tank) for the same reason Mass/SpeedMultiplier do --
    /// so any combatant type gets it for free.</summary>
    private CaptureState _capture;

    /// <summary>docs/26 Phase 9 (Ground Stomp): a frozen-solid timer,
    /// deliberately separate from the slow-status pair above rather than
    /// reusing SpeedMultiplier=0 as a "slow" -- stun is binary (no
    /// magnitude to weigh stronger/weaker on reapplication, unlike
    /// ApplySlow) and also halts FIRING, not just movement, which a slow
    /// never does.</summary>
    private float _stunRemaining;

    public bool Alive { get { return !_dead && Health > 0f; } }
    public bool InBattle { get { return _battleTimer > 0f; } }
    public float HealthFraction { get { return Mathf.Clamp01(Health / Mathf.Max(1f, MaxHealth)); } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * AimHeight; } }
    public UnitCombat LastAttacker { get; private set; }

    /// <summary>1 = normal speed; less than 1 while a slow status is
    /// active; exactly 0 while stunned (stun overrides slow entirely --
    /// a frozen unit doesn't "slowly" move). Every mover
    /// (MonsterAgent.RunOrWalkSpeed, Tank.Update) multiplies its own
    /// speed by this -- so the effect applies to any unit that reads it,
    /// not just whichever caster first used it.</summary>
    public float SpeedMultiplier
    {
        get
        {
            if (_stunRemaining > 0f) return 0f;
            return _slowRemaining > 0f ? _slowMultiplier : 1f;
        }
    }

    /// <summary>True while frozen by a stun effect (e.g. Ground Stomp) --
    /// can't move (see SpeedMultiplier) or fire (see ReadyToFire).</summary>
    public bool IsStunned { get { return _stunRemaining > 0f; } }

    /// <summary>Apply (or refresh) a stun. Binary, so reapplication just
    /// takes the longer remaining duration -- there's no "weaker stun"
    /// to protect against the way ApplySlow protects a stronger slow.</summary>
    public void ApplyStun(float duration)
    {
        _stunRemaining = Mathf.Max(_stunRemaining, duration);
    }

    /// <summary>Apply (or refresh) a slow status. Never lets a weaker/
    /// shorter reapplication water down a stronger one already running:
    /// takes the lower multiplier and the longer remaining duration.</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp01(multiplier);
        if (_slowRemaining > 0f)
        {
            _slowMultiplier = Mathf.Min(_slowMultiplier, multiplier);
            _slowRemaining = Mathf.Max(_slowRemaining, duration);
        }
        else
        {
            _slowMultiplier = multiplier;
            _slowRemaining = duration;
        }
    }

    /// <summary>True while this unit is being dragged toward a captor
    /// (e.g. a web attack's non-heavy branch). The owning mover
    /// (MonsterAgent/Tank) checks this at the top of its own Update() and
    /// calls TickCapture INSTEAD of its own AI/movement while true --
    /// mirroring how Citizen overrides its own Update() the same way.
    /// Self-healing: reads live off the captor's Alive state, so a dead
    /// captor's victim is released the very next check with no separate
    /// cleanup call needed.</summary>
    public bool IsCaptured { get { return _capture != null && _capture.Active; } }
    public UnitCombat Captor { get { return _capture != null ? _capture.Captor : null; } }

    /// <summary>Begin being pulled toward `captor`. Re-capturing an
    /// already-captured unit (a second web lands on it) simply retargets
    /// to the newest captor -- last web wins, no stacking, v0.1.</summary>
    public void Capture(UnitCombat captor, float speed)
    {
        if (_capture == null) _capture = new CaptureState();
        _capture.Begin(captor, speed);
    }

    /// <summary>Advances the pull one frame; returns true once arrived
    /// at the captor. No-op (returns false) once the captor has died
    /// (and releases the capture so IsCaptured reads false from then
    /// on). docs/26 Phase 7: no generic "consume a UnitCombat" path
    /// exists yet, so callers don't act on the returned value today --
    /// see docs/26's "non-Citizen consume path" design note.</summary>
    public bool TickCapture(float dt)
    {
        if (_capture == null) return false;
        if (!_capture.Active) { _capture = null; return false; }
        return _capture.TickPull(transform, dt);
    }

    public void Configure(string faction, float maxHp, float radius, float aimHeight,
        WeaponProfile weapon, Action onDied, float mass = 1f)
    {
        Faction = faction;
        MaxHealth = maxHp;
        Health = maxHp;
        Radius = radius;
        AimHeight = aimHeight;
        Weapon = weapon;
        Mass = mass;
        _onDied = onDied;
        _dead = false;
    }

    public bool ReadyToFire { get { return _cooldown <= 0f && !IsStunned && Weapon != null && Weapon.CanAttack; } }

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
        if (_slowRemaining > 0f) { _slowRemaining -= dt; if (_slowRemaining <= 0f) _slowMultiplier = 1f; }
        if (_stunRemaining > 0f) _stunRemaining -= dt;
        for (var i = 0; i < Abilities.Count; i++) Abilities[i].Tick(dt);
    }
}
