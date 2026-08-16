using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/26 Phase 4: the Arachnid Web Attack special-attack resolver --
/// launch a non-homing web bolt at a snapshot of the target's position
/// (an area effect resolves at a LOCATION once it lands, not on whichever
/// single unit happens to still be standing there -- so it does NOT
/// track the target after launch, unlike a normal weapon shot), then on
/// arrival query everyone within the ability's AreaOfEffect and classify
/// each by mass.
///
/// PHASE 4 targeting + classification landed on its own first (logged
/// via Debug.Log, no effects) specifically so it could be proven correct
/// before any harder work touched it. PHASE 5 adds the heavy-target
/// branch's real effect (UnitCombat.ApplySlow); PHASE 6 the non-heavy
/// branch's capture/pull; PHASE 7 consume-on-arrival for citizens.
/// PHASE 8 (MonsterAgent.EvaluateBestAbility) scores candidate targets
/// using CountCatchable below, reusing this same query/classification
/// logic to decide WHETHER to cast at all.
///
/// Stateless -- one caster can have this resolver called for it any
/// number of times; all per-cast state lives on the spawned Projectile
/// and the caster's own SpecialAttackInstance cooldown.
/// </summary>
public static class WebAttackAbility
{
    /// <summary>Mass at or above which a caught target is "heavy" (slow
    /// only, per docs/26 -- never pulled or consumed) rather than
    /// "human-class" (capture-eligible). A v0.1 placeholder, like every
    /// other unbalanced number in this project -- Tank.cs sets Mass=10,
    /// everything else defaults to 1, so 3 cleanly separates them without
    /// being tuned against anything real yet.</summary>
    public const float HeavyMassThreshold = 3f;

    /// <summary>docs/26 Phase 5: how much a caught heavy target's move
    /// speed is cut, and for how long -- v0.1 placeholders, same
    /// unbalanced-on-purpose status as HeavyMassThreshold above.</summary>
    public const float HeavySlowMultiplier = 0.35f;
    public const float HeavySlowDuration = 3f;

    /// <summary>docs/26 Phase 6: how fast a caught non-heavy target is
    /// dragged toward its captor. Another v0.1 placeholder.</summary>
    public const float CapturePullSpeed = 6f;

    /// <summary>Spawns the web bolt and wires its arrival to
    /// <see cref="ResolveImpact"/>. `targetPoint` is a SNAPSHOT (the
    /// target's position at the moment of casting) -- the bolt does not
    /// home, matching "web travels toward target location" rather than
    /// "web homes to whichever unit it was aimed at."</summary>
    public static void Launch(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 targetPoint, Vector3 muzzle)
    {
        if (builder == null || caster == null || definition == null) return;

        var go = new GameObject("WebBolt");
        go.transform.position = muzzle;
        var projectile = go.AddComponent<Projectile>();
        // non-homing (target = null): an AoE effect resolves at the
        // impact LOCATION, so there is no single "the target" to track
        // -- WeaponFx's existing FireAtPoint projectiles use this same
        // null-target convention for the same reason (building attacks).
        var boltSpeed = Mathf.Max(10f, definition.Range * 0.6f);
        projectile.Init(caster, null, targetPoint, 0f, boltSpeed);
        projectile.OnArrive = (impactPoint) => ResolveImpact(builder, caster, definition, impactPoint);
        // 2026-08 ("Add Strong Visual Representation for Area Attacks and
        // Psionics") -- this bolt was otherwise fully invisible in
        // flight, see SpecialAttackVfx.AttachProjectileGlow's own doc
        // comment.
        SpecialAttackVfx.AttachProjectileGlow(go, definition);

        if (definition.CastSfx != null) AudioSource.PlayClipAtPoint(definition.CastSfx, muzzle);
    }

    private static void ResolveImpact(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 impactPoint)
    {
        // 2026-08 ("Add Strong Visual Representation for Area Attacks and
        // Psionics"): a hand-authored prefab (if a designer ever assigns
        // one) still wins outright, same as before -- the procedural
        // resolver only fills the gap for every definition that leaves
        // this null, which today is all of them.
        if (definition.ImpactVfxPrefab != null) Object.Instantiate(definition.ImpactVfxPrefab, impactPoint, Quaternion.identity);
        else SpecialAttackVfx.PlayImpact(definition, impactPoint);
        if (definition.ImpactSfx != null) AudioSource.PlayClipAtPoint(definition.ImpactSfx, impactPoint);

        var radius = Mathf.Max(0.01f, definition.AreaOfEffect);

        // combatants (monsters, tanks) -- the existing docs/25 neighbour
        // grid, shared with steering/separation, not a second grid
        var combatants = new List<UnitCombat>();
        builder.QueryCombatantsInRadius(impactPoint, radius, combatants);
        foreach (var c in combatants)
        {
            if (!ShouldCatchCombatant(c, caster, definition, impactPoint)) continue;
            var heavy = IsHeavy(c.Mass);
            if (heavy)
            {
                c.ApplySlow(HeavySlowMultiplier, HeavySlowDuration);
                Debug.Log("[WebAttack] caught " + c.name + " (mass=" + c.Mass + ") -> SLOW x"
                    + HeavySlowMultiplier + " for " + HeavySlowDuration + "s (heavy target, no pull/consume)");
            }
            else
            {
                c.Capture(caster, CapturePullSpeed);
                Debug.Log("[WebAttack] caught " + c.name + " (mass=" + c.Mass + ") -> CAPTURE+PULL toward "
                    + caster.name + " (no consume yet, Phase 7)");
            }
        }

        // citizens -- a SEPARATE pool with no combat component and no
        // spatial grid of their own (docs/26 research: Citizen carries no
        // UnitCombat at all), so this is a plain linear distance scan,
        // matching RuntimeCityBuilder.DistanceAhead's existing citizen-
        // scanning pattern rather than inventing a citizen grid for one
        // ability. Citizens are always human-class -- that IS the "human
        // target" case the task brief describes -- so no mass check.
        if (MatchesFilter(TargetFilter.Human, definition.ValidTargets))
        {
            foreach (var citizen in builder.Citizens)
            {
                if (citizen == null) continue;
                var d = citizen.transform.position - impactPoint;
                d.y = 0f;
                if (d.magnitude > radius) continue;
                citizen.Capture(caster, CapturePullSpeed);
                Debug.Log("[WebAttack] caught citizen " + citizen.name + " -> CAPTURE+PULL toward " + caster.name + " (no consume yet, Phase 7)");
            }
        }
    }

    /// <summary>docs/26 Phase 8: how many combatants + citizens would be
    /// caught if this ability landed at `impactPoint` right now -- the
    /// "weighted target count in AoE" the AI decision heuristic scores
    /// candidate anchors by. Runs the exact same query + the exact same
    /// `ShouldCatchCombatant`/`MatchesFilter` decisions `ResolveImpact`
    /// uses, so the heuristic can never disagree with what actually
    /// happens on impact -- just tallies instead of applying effects.</summary>
    public static int CountCatchable(RuntimeCityBuilder builder, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 impactPoint)
    {
        if (builder == null || caster == null || definition == null) return 0;
        var radius = Mathf.Max(0.01f, definition.AreaOfEffect);
        var count = 0;

        var combatants = new List<UnitCombat>();
        builder.QueryCombatantsInRadius(impactPoint, radius, combatants);
        foreach (var c in combatants)
            if (ShouldCatchCombatant(c, caster, definition, impactPoint)) count++;

        if (MatchesFilter(TargetFilter.Human, definition.ValidTargets))
        {
            foreach (var citizen in builder.Citizens)
            {
                if (citizen == null) continue;
                var d = citizen.transform.position - impactPoint;
                d.y = 0f;
                if (d.magnitude <= radius) count++;
            }
        }
        return count;
    }

    /// <summary>True at or above <see cref="HeavyMassThreshold"/>. Pure
    /// function, exposed so the mass-classification boundary is directly
    /// testable without a live scene.</summary>
    public static bool IsHeavy(float mass)
    {
        return mass >= HeavyMassThreshold;
    }

    /// <summary>True if `filter` allows affecting a target of `category`.
    /// Pure function -- (TargetFilter is a [Flags] enum, so this is a
    /// plain bitwise membership test).</summary>
    public static bool MatchesFilter(TargetFilter category, TargetFilter filter)
    {
        return (filter & category) != 0;
    }

    /// <summary>The full "would this ability's AoE catch this combatant"
    /// decision: alive, not the caster itself, not a same-faction ally
    /// (no friendly-fire capture -- UNLESS that ally is possessed; see
    /// docs/26 "Possessed units and friendly fire"), within the EXACT
    /// circular radius (the caller's grid query already narrowed to a
    /// bounding-square superset), and of a category this ability's
    /// ValidTargets actually wants. Pure and side-effect-free (unlike
    /// ResolveImpact, which also logs/plays FX) -- exposed so this
    /// decision is directly testable against real UnitCombat instances
    /// without needing a live RuntimeCityBuilder/SpatialGrid at all.</summary>
    public static bool ShouldCatchCombatant(UnitCombat c, UnitCombat caster,
        SpecialAttackDefinition definition, Vector3 impactPoint)
    {
        if (c == null || !c.Alive || c == caster) return false;
        // no friendly-fire capture -- except a possessed same-faction
        // unit isn't really an ally anymore (docs/26 Phase 5). No system
        // sets IsPossessed yet, so this exception is currently inert.
        if (c.Faction == caster.Faction && !c.IsPossessed) return false;
        var d = c.transform.position - impactPoint;
        d.y = 0f;
        if (d.magnitude > Mathf.Max(0.01f, definition.AreaOfEffect)) return false;
        // Faction is a free-form string ("monster" | "human"), not the
        // same thing as TargetFilter -- map it to the category THIS
        // combatant actually represents so the filter check is "does the
        // ability want to affect a unit of this kind," not "is at least
        // one flag set at all."
        var category = c.Faction == "human" ? TargetFilter.Human : TargetFilter.Monster;
        return MatchesFilter(category, definition.ValidTargets);
    }
}
