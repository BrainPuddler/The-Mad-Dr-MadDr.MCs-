/// <summary>
/// The PER-UNIT runtime half of a special attack: which
/// <see cref="SpecialAttackDefinition"/> this instance wraps, plus the
/// actual live cooldown timer. Two units equipping the same shared
/// SpecialAttackDefinition asset each get their own SpecialAttackInstance
/// -- using one arachnid's web attack never touches another arachnid's
/// cooldown (docs/26: "per enemy instance," not shared-per-ability-type
/// or global).
///
/// Cooldown mechanics deliberately match UnitCombat._cooldown exactly: a
/// plain float, decremented by Time.deltaTime once per frame (ticked from
/// UnitCombat.Update(), unconditionally -- see UnitCombat.Abilities),
/// reloaded to Definition.Cooldown when the ability fires, gated by a
/// &lt;= 0f check. Ticking from UnitCombat.Update() rather than from
/// MonsterAgent's order-dispatch switch is what makes "cooldown persists
/// correctly through AI state changes" true by construction: it runs
/// every frame regardless of what OrderKind the owning MonsterAgent is
/// currently in, the same way the weapon's own _cooldown already does.
/// </summary>
[System.Serializable]
public sealed class SpecialAttackInstance
{
    public SpecialAttackDefinition Definition;

    private float _cooldownRemaining;

    public bool IsReady { get { return _cooldownRemaining <= 0f && Definition != null; } }
    public float CooldownRemaining { get { return _cooldownRemaining; } }

    public SpecialAttackInstance() { }
    public SpecialAttackInstance(SpecialAttackDefinition definition) { Definition = definition; }

    public void Tick(float dt)
    {
        if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;
    }

    /// <summary>Starts the cooldown -- called the moment the ability is
    /// actually used (docs/26: cooldown begins "after deployment," not at
    /// order-issue time, matching how UnitCombat.TryFire only reloads
    /// _cooldown once a shot actually goes out).</summary>
    public void TriggerCooldown()
    {
        _cooldownRemaining = Definition != null ? Definition.Cooldown : 0f;
    }
}
