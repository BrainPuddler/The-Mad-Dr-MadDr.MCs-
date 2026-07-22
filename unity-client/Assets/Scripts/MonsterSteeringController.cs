using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// docs/25 Phase B/C: the steering entry point `MonsterAgent.FollowPath`
/// calls, replacing the old two-call-site fight between `AvoidanceDir` (a
/// heading deflection, inside FollowPath) and `ApplySeparation` (a hard
/// position edit, once more every frame from `MonsterAgent.Update()`) --
/// docs/25 section 2's root cause #1: "Seek and Separation are applied in
/// sequence, not blended." `Combine()` folds seek, a softened
/// separation-as-force, and (Phase C) time-to-collision predictive
/// avoidance into ONE heading for a path-following unit, so all three
/// settle on the same answer this frame instead of separation yanking the
/// position back out of whatever heading avoidance just chose. It also now
/// returns a speed scale, so a unit fighting a strong deflection eases off
/// its own throttle instead of ramming full speed into whatever's blocking
/// it (docs/25 Phase C's "speed modulation" requirement).
///
/// `RuntimeCityBuilder.ApplySeparation` is UNTOUCHED as a public entry point
/// and UNCONDITIONAL behaviourally -- `Tank.cs` calls it too (docs/25
/// explicitly keeps tanks out of scope), and `MonsterAgent.Update()` still
/// calls it every frame regardless of order state, same as before this
/// plan. It remains the hard "never actually overlap" guarantee; `Combine`
/// below is an earlier-reacting heading NUDGE layered on top while
/// path-following, not a replacement for it (a Phase B harness confirmed a
/// soft blend alone isn't enough to prevent interpenetration on its own).
/// `ApplySeparation` now calls `SeparationForce` below for its per-pair
/// math instead of duplicating it -- a pure extract, same numbers, same
/// cumulative-push-per-neighbour order.
///
/// Stateless and dependency-free (matches SpatialGrid's style) so it
/// compiles and runs in the standalone console harness used to verify
/// docs/25 phases -- no MonoBehaviour, no engine calls beyond UnityEngine's
/// math types.
/// </summary>
public static class MonsterSteeringController
{
    /// <summary>docs/25 Phase C: how far ahead (seconds) predictive
    /// avoidance looks for a converging neighbour. Long enough to react to
    /// a head-on pair closing at a brisk run well before contact; short
    /// enough that a neighbour merely passing nearby on a diverging course
    /// never registers.</summary>
    public const float Horizon = 2.5f;

    /// <summary>Extra clearance (meters) predictive avoidance reacts
    /// within, on top of the two bodies' own combined radii -- a personal-
    /// space buffer distinct from the Inspector `groupSpacing` knob (which
    /// governs REST spacing once a group has stopped, not in-transit
    /// urgency).</summary>
    public const float AvoidancePadding = 1.5f;

    /// <summary>Floor on Combine's speed-scale output -- a heavily blocked
    /// unit eases off, but steering alone never fully stops it (that
    /// escalation is DeadlockManager's job, docs/25 Phase D, not this
    /// layer).</summary>
    public const float MinSpeedScale = 0.35f;

    /// <summary>Combine's output: a steering direction plus how much of the
    /// caller's intended speed to actually use this frame.</summary>
    public struct SteeringResult
    {
        public Vector3 Direction;
        public float SpeedScale;
    }

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

    /// <summary>docs/25 Phase C: time-to-collision (RVO-lite) avoidance,
    /// replacing Phase B's ahead-cone `AvoidanceBias` (removed -- nothing
    /// else called it). For each neighbour, predicts the closest approach
    /// between self and that neighbour assuming BOTH keep their current
    /// velocity -- self's assumed velocity is `fwd * selfSpeed` (what it's
    /// about to do if nothing steers it away), the neighbour's is its own
    /// published `LastVelocity` (see that field's doc comment for the
    /// stationary-tank default). A neighbour already moving apart, or not
    /// projected to close within `Horizon` seconds, contributes nothing --
    /// that's what makes this predictive rather than reactive: something
    /// far away but closing fast steers around NOW, not only once it's
    /// within a fixed spatial ring the way the old ahead-cone worked.
    /// Already-overlapping neighbours are deliberately skipped here --
    /// that's `SeparationForce`'s job (a collision that already happened,
    /// not one being predicted); mixing the two signals for the same pair
    /// would double up the response for no gain.</summary>
    public static Vector3 PredictiveAvoidance(UnitCombat self, Vector3 selfVel, Vector3 fwd, List<UnitCombat> neighbours)
    {
        var right = new Vector3(fwd.z, 0f, -fwd.x);   // fwd rotated -90 about up
        var pos = self.transform.position;
        var avoid = Vector3.zero;
        foreach (var c in neighbours)
        {
            if (c == null || c == self || !c.Alive) continue;
            var relPos = c.transform.position - pos;
            relPos.y = 0f;
            var bodyRadius = self.Radius + c.Radius;
            var combined = bodyRadius + AvoidancePadding;
            var dist = relPos.magnitude;
            // only skip once bodies are ACTUALLY overlapping -- that's
            // SeparationForce's job. Staying active all the way down to
            // bodyRadius (not the wider padded `combined`) matters: cutting
            // this off at `combined` left a dead zone where a closing
            // neighbour was too close for predictive avoidance to still
            // apply but not yet overlapping enough for separation to react
            // either -- a standalone harness caught this as a unit barely
            // slowing at all on approach to a blocker.
            if (dist < bodyRadius) continue;

            // relVel is the NEIGHBOUR's velocity relative to self, matching
            // relPos's same other-minus-self convention -- so relPos(t) =
            // relPos + relVel * t actually predicts the neighbour's future
            // position relative to self (getting the sign backwards here
            // makes every genuinely-closing pair compute a negative t and
            // get silently discarded as "already past," which is exactly
            // the bug a standalone harness caught: a unit walking straight
            // at a stationary blocker never triggered ANY predictive
            // response).
            var relVel = c.LastVelocity - selfVel;
            relVel.y = 0f;
            var relSpeedSq = relVel.sqrMagnitude;
            if (relSpeedSq < 1e-4f) continue;   // not closing -- nothing to predict

            var t = -Vector3.Dot(relPos, relVel) / relSpeedSq;
            if (t < 0f || t > Horizon) continue;   // closest approach already passed, or too far out to matter yet

            var closest = relPos + relVel * t;
            var closestDist = closest.magnitude;
            if (closestDist >= combined) continue;   // projected to clear -- no response needed

            // steer to the side away from the blocker's CURRENT position
            // (dead-ahead breaks to the left deterministically), same
            // tie-break the old ahead-cone used
            var onRight = Vector3.Dot(relPos, right);
            var side = onRight > 0f ? -1f : 1f;
            var urgency = (1f - t / Horizon) * (combined - closestDist) / combined;
            avoid += right * (side * urgency);
        }
        return avoid;
    }

    /// <summary>The FollowPath entry point (docs/25 Phase B, extended by
    /// Phase C): blends seek, separation-as-a-force, and predictive
    /// avoidance into one steering direction, plus a speed scale. A unit
    /// with a clear predicted path and no neighbour inside its separation
    /// envelope steers at full speed straight toward `desiredDir`;
    /// separation/avoidance only join the blend once they actually have
    /// something to say, and even then at softened weights -- see
    /// `PredictiveAvoidance` and `SeparationForce`'s own headers for why
    /// neither replaces `RuntimeCityBuilder.ApplySeparation`'s hard
    /// positional correction, which keeps running unconditionally
    /// regardless of this call.</summary>
    public static SteeringResult Combine(UnitCombat self, Vector3 desiredDir, float selfSpeed, List<UnitCombat> neighbours, float groupSpacing)
    {
        var fwd = new Vector3(desiredDir.x, 0f, desiredDir.z);
        if (fwd.sqrMagnitude < 1e-4f) return new SteeringResult { Direction = desiredDir, SpeedScale = 1f };
        fwd = fwd.normalized;

        var selfVel = fwd * selfSpeed;
        var avoid = PredictiveAvoidance(self, selfVel, fwd, neighbours);
        var sepPush = SeparationForce(self, neighbours, groupSpacing);
        var sepBias = sepPush.sqrMagnitude > 1e-6f
            ? sepPush.normalized * Mathf.Min(1f, sepPush.magnitude / Mathf.Max(0.01f, self.Radius))
            : Vector3.zero;

        var dir = avoid.sqrMagnitude < 1e-6f && sepBias.sqrMagnitude < 1e-6f
            ? fwd
            : (fwd + avoid * 1.2f + sepBias * 0.8f).normalized;

        // speed modulation (docs/25 Phase C): alignment between the chosen
        // heading and the original seek direction is a cheap, principled
        // proxy for "how much is avoidance/separation dominating this
        // frame" -- a unit arcing gently around something eases off a
        // little, one fighting a near-reversal eases off a lot. Floored,
        // never fully stops here (DeadlockManager, Phase D, owns that).
        var alignment = Vector3.Dot(dir, fwd);
        var speedScale = Mathf.Clamp(alignment, MinSpeedScale, 1f);

        return new SteeringResult { Direction = dir, SpeedScale = speedScale };
    }
}
