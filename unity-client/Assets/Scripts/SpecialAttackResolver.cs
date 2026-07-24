using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/26 Phase 9: resolves an INSTANT area effect -- no projectile
/// travel, unlike WebAttackAbility's arcing bolt -- centered on
/// `originPoint` the moment it's cast. This is the delivery shape for a
/// stomp (originPoint = the caster's own feet) or a burst (originPoint =
/// the target's position, reached the same way TickSpecialAttack already
/// approaches a target before triggering PullAndConsume).
///
/// Reuses WebAttackAbility's ShouldCatchCombatant/MatchesFilter/
/// CountCatchable rather than duplicating the catch/classify logic --
/// those are already generic (parameterized by `definition`, not
/// hardcoded to "web"), so the only new code here is the per-effect
/// application switch. Adding a new instant-effect kind later means one
/// enum value (SpecialAttackDefinition.cs) + one case in ApplyEffect
/// below -- no new resolver class.
/// </summary>
public static class SpecialAttackResolver
{
    public static void ResolveInstant(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 originPoint)
    {
        if (builder == null || caster == null || definition == null) return;

        if (definition.ImpactVfxPrefab != null) Object.Instantiate(definition.ImpactVfxPrefab, originPoint, Quaternion.identity);
        if (definition.ImpactSfx != null) AudioSource.PlayClipAtPoint(definition.ImpactSfx, originPoint);

        var radius = Mathf.Max(0.01f, definition.AreaOfEffect);

        var combatants = new List<UnitCombat>();
        builder.QueryCombatantsInRadius(originPoint, radius, combatants);
        foreach (var c in combatants)
        {
            if (!WebAttackAbility.ShouldCatchCombatant(c, caster, definition, originPoint)) continue;
            ApplyEffect(c, caster, definition);
        }

        // Citizens have no UnitCombat/Health/status-effect state -- Damage
        // and Stun (the only effect types this resolver handles today)
        // have nothing to apply to one. A future instant-effect kind that
        // DOES affect citizens (e.g. a fear/panic pulse) branches here,
        // mirroring WebAttackAbility.ResolveImpact's own citizen scan.
    }

    private static void ApplyEffect(UnitCombat c, UnitCombat caster, SpecialAttackDefinition definition)
    {
        switch (definition.EffectType)
        {
            case SpecialAttackEffectType.Damage:
                c.TakeDamage(definition.DamageAmount, caster);
                break;
            case SpecialAttackEffectType.Stun:
                c.ApplyStun(definition.StunDuration);
                break;
            // PullAndConsume and SlowStatus are WebAttackAbility's own
            // effect types (projectile delivery) -- not resolved here.
        }
    }
}
