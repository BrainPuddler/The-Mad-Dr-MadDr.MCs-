using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/25 Phase B: the steering entry point `MonsterAgent.FollowPath` calls,
/// replacing the old two-call-site fight between `AvoidanceDir` (a heading
/// deflection, inside FollowPath) and `ApplySeparation` (a hard position
/// edit, once more every frame from `MonsterAgent.Update()`) -- docs/25
/// section 2's root cause #1: "Seek and Separation are applied in sequence,
/// not blended." `Combine()` folds seek, a softened separation-as-force,
/// and the ported ahead-cone avoidance into ONE heading for a path-following
/// unit, so all three settle on the same answer this frame instead of
/// separation yanking the position back out of whatever heading avoidance
/// just chose.
///
/// `RuntimeCityBuilder.ApplySeparation` is UNTOUCHED as a public entry point
/// and UNCONDITIONAL behaviourally -- `Tank.cs` calls it too (docs/25
/// explicitly keeps tanks out of scope), and `MonsterAgent.Update()` still
/// calls it every frame regardless of order state, same as before this
/// plan. It remains the hard "never actually overlap" guarantee; `Combine`
/// below is an earlier-reacting heading NUDGE layered on top while
/// path-following, not a replacement for it (a standalone harness confirmed
/// the soft blend alone isn't enough to prevent interpenetration on its
/// own -- see `Combine`'s own header). `ApplySeparation` now calls
/// `SeparationForce` below for its per-pair math instead of duplicating it
/// -- a pure extract, same numbers, same cumulative-push-per-neighbour
/// order.
///
/// Stateless and dependency-free (matches SpatialGrid's style) so it
/// compiles and runs in the standalone console harness used to verify
/// docs/25 phases -- no MonoBehaviour, no engine calls beyond UnityEngine's
/// math types.
/// </summary>
public static class MonsterSteeringController
{
    /// <summary>Ported verbatim from `RuntimeCityBuilder.ApplySeparation`'s
    /// per-pair math (unchanged formula: once inside Radius+Radius+
    /// groupSpacing, push half the overlap toward daylight) -- including its
    /// cumulative order: each neighbour after the first is checked against
    /// the position already nudged by earlier neighbours in the same call,
    /// not the original position, exactly as the old inline loop did.
    /// Returns the NET displacement (final position minus start) instead of
    /// writing to `self.transform` directly, so the same computation backs
    /// both the hard positional correction (`ApplySeparation`) and the
    /// softened blend used while path-following (`Combine`).</summary>
    public static Vector3 SeparationForce(UnitCombat self, List<UnitCombat> neighbours, float groupSpacing)
    {
        var start = self.transform.position;
        var p = start;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            var d = p - c.transform.position;
            d.y = 0f;
            var minDist = self.Radius + c.Radius + groupSpacing;
            var dist = d.magnitude;
            if (dist < minDist && dist > 1e-3f)
                p += d / dist * ((minDist - dist) * 0.5f);
        }
        return p - start;
    }

    /// <summary>Ported verbatim from `RuntimeCityBuilder.AvoidanceDir`'s
    /// ahead-cone math -- same forward-cone gate, lookahead reach, and
    /// dead-ahead-breaks-left tie-break -- returning the raw sideways bias
    /// before it's folded into a heading, so `Combine` can blend it against
    /// separation instead of `AvoidanceDir`'s old standalone `fwd + avoid *
    /// 1.2` return.</summary>
    public static Vector3 AvoidanceBias(UnitCombat self, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var right = new Vector3(fwd.z, 0f, -fwd.x);   // fwd rotated -90 about up
        var pos = self.transform.position;
        var avoid = Vector3.zero;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            var to = c.transform.position - pos;
            to.y = 0f;
            var dist = to.magnitude;
            var reach = self.Radius + c.Radius + 4f;   // lookahead
            if (dist < 1e-3f || dist > reach) continue;
            var ahead = Vector3.Dot(to / dist, fwd);
            if (ahead < 0.35f) continue;                // only things ~in front

            // steer to the side away from the blocker (dead-ahead breaks to
            // the left deterministically)
            var onRight = Vector3.Dot(to, right);
            var side = onRight > 0f ? -1f : 1f;
            var strength = (reach - dist) / reach * ahead;
            avoid += right * (side * strength);
        }
        return avoid;
    }

    /// <summary>The new FollowPath entry point (docs/25 Phase B): blends
    /// seek, separation-as-a-force, and the ahead-cone avoidance into one
    /// steering direction. A unit that's already clear of every neighbour's
    /// groupSpacing envelope steers identically to today's AvoidanceDir --
    /// separation only joins the blend once a neighbour is actually inside
    /// that envelope, and even then at a softened weight: a steering NUDGE
    /// toward daylight this frame, reacting a beat earlier than a pure
    /// position correction would. This does NOT replace
    /// RuntimeCityBuilder.ApplySeparation's hard positional correction --
    /// that still runs unconditionally every frame regardless of order
    /// state (see MonsterAgent.Update) and remains the actual "never
    /// overlap" guarantee; a standalone harness confirmed the soft blend
    /// alone lets two units driving straight at a shared destination
    /// interpenetrate, since a heading bias has no floor on tolerated
    /// overlap the way a position correction does. Returns a NORMALIZED
    /// direction; equals `desiredDir` when nothing blocks (same contract as
    /// the old AvoidanceDir).</summary>
    public static Vector3 Combine(UnitCombat self, Vector3 desiredDir, List<UnitCombat> neighbours, float groupSpacing)
    {
        var fwd = new Vector3(desiredDir.x, 0f, desiredDir.z);
        if (fwd.sqrMagnitude < 1e-4f) return desiredDir;
        fwd = fwd.normalized;

        var avoid = AvoidanceBias(self, fwd, neighbours);
        var sepPush = SeparationForce(self, neighbours, groupSpacing);
        var sepBias = sepPush.sqrMagnitude > 1e-6f
            ? sepPush.normalized * Mathf.Min(1f, sepPush.magnitude / Mathf.Max(0.01f, self.Radius))
            : Vector3.zero;

        if (avoid.sqrMagnitude < 1e-6f && sepBias.sqrMagnitude < 1e-6f) return desiredDir;
        return (fwd + avoid * 1.2f + sepBias * 0.8f).normalized;
    }
}
