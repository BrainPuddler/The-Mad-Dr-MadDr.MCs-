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
///
/// 2026-08 follow-up (creator direction: "Expand Secondary Attack
/// Variety Across Races"): two EffectTypes are special-cased BEFORE the
/// normal per-target enemy loop, because they don't fit "apply
/// something to each caught enemy":
/// - `Hazard` applies nothing at cast time -- it spawns a
///   `HazardZoneEffect` that applies its own effect to whoever's
///   standing inside it, on its own schedule, for as long as it
///   persists. Handled here rather than in `ApplyEffect` because
///   `ApplyEffect` is a per-CAUGHT-TARGET callback, and a Hazard
///   catches nobody at the instant it lands.
/// - `Boost` targets the CASTER, not a caught enemy -- `ShouldCatchCombatant`
///   deliberately excludes same-faction targets (no friendly-fire
///   capture), so a self-buff could never reach `ApplyEffect`'s normal
///   per-target loop at all; it's applied directly here instead.
/// A full AoE mind-control (an actual "your monster fights for me now"
/// AI takeover) was considered for `Possess` and deliberately NOT
/// built -- see that case in `ApplyEffect` and docs/12's entry for this
/// pass for the full reasoning (real faction/ownership-swap logic is a
/// much bigger surface than this system otherwise touches, for an
/// effect the brief itself frames as minor -- "1-5% chance... 2-4
/// seconds... do not make this a guaranteed effect").
/// </summary>
public static class SpecialAttackResolver
{
    public static void ResolveInstant(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 originPoint)
    {
        if (builder == null || caster == null || definition == null) return;

        // 2026-08 ("Add Strong Visual Representation for Area Attacks and
        // Psionics"): same fallback shape as WebAttackAbility.ResolveImpact
        // -- a hand-authored prefab still wins if one's ever assigned.
        if (definition.ImpactVfxPrefab != null) Object.Instantiate(definition.ImpactVfxPrefab, originPoint, Quaternion.identity);
        else SpecialAttackVfx.PlayImpact(definition, originPoint);
        if (definition.ImpactSfx != null) AudioSource.PlayClipAtPoint(definition.ImpactSfx, originPoint);

        if (definition.EffectType == SpecialAttackEffectType.Hazard)
        {
            HazardZoneEffect.Spawn(builder, caster, definition, originPoint);
            return;
        }
        if (definition.EffectType == SpecialAttackEffectType.Boost)
        {
            caster.ApplyTempoModifier(definition.TempoMultiplier, definition.TempoDuration);
            return;
        }

        var radius = Mathf.Max(0.01f, definition.AreaOfEffect);

        var combatants = new List<UnitCombat>();
        builder.QueryCombatantsInRadius(originPoint, radius, combatants);
        foreach (var c in combatants)
        {
            if (!WebAttackAbility.ShouldCatchCombatant(c, caster, definition, originPoint)) continue;
            ApplyEffect(c, caster, definition);
        }

        // Citizens have no UnitCombat/Health/status-effect state -- none
        // of this resolver's effect types have anything to apply to one.
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
            case SpecialAttackEffectType.Fear:
                c.ApplyFear(definition.FearDuration);
                break;
            case SpecialAttackEffectType.Weaken:
                c.ApplyTempoModifier(definition.TempoMultiplier, definition.TempoDuration);
                break;
            case SpecialAttackEffectType.Possess:
                // 2026-08 ("1-5% chance of becoming temporarily
                // possessed... do not make this a guaranteed effect"):
                // this combat layer is entirely client-side (docs/26's
                // own header -- match-core has zero special-attack
                // concept), so it carries no replay-determinism
                // requirement the way match-core's own sfc32 stream
                // does; UnityEngine.Random is the right tool for a real
                // gameplay chance roll here -- this codebase's "avoid
                // UnityEngine.Random" convention is specifically about
                // not consuming a SHARED stream for cosmetic-only VFX
                // jitter (see DamageFx.cs/BrainJarBubbles.cs), not a
                // blanket ban on gameplay randomness.
                if (UnityEngine.Random.Range(0f, 100f) < definition.PossessChancePercent)
                    c.ApplyPossession(definition.PossessDuration);
                break;
            // PullAndConsume and SlowStatus are WebAttackAbility's own
            // effect types (projectile delivery) -- not resolved here.
            // Hazard and Boost are short-circuited in ResolveInstant
            // above, before this per-target loop ever runs.
        }
    }
}
