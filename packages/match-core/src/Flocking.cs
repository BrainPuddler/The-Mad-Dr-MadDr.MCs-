using System;
using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §5 / docs/27 Phase C: pure separation math for
    /// sim-driven units -- the deterministic sim-side twin of Unity's
    /// <c>MonsterSteeringController.SeparationForce</c> (identical formula:
    /// once two bodies' combined radii + spacing overlap, push each one
    /// half the overlap toward daylight, cumulative in caller-supplied
    /// order). This closes the gap docs/27 §5 flagged and deliberately
    /// accepted for Phase A: a sim-driven unit had NO separation at all,
    /// since the existing hard correction
    /// (<c>RuntimeCityBuilder.ApplySeparation</c>) writes
    /// <c>transform.position</c> directly and would fight the interpolated
    /// render position every frame.
    ///
    /// This file never touches Unity, <c>MonsterSteeringController</c>, or
    /// <c>RuntimeCityBuilder.ApplySeparation</c>'s own code -- those keep
    /// running, completely unchanged, for every unit that isn't sim-driven
    /// (which today means every unit in every real scene: the opt-in
    /// toggle is still off by default). The only Unity-side change this
    /// phase makes is gating that ONE existing call site to skip units
    /// that are now sim-driven, since match-core is the sole authoritative
    /// writer of their position once they are.
    ///
    /// docs/23 Phase 5 adds the other two forces, <see cref="Alignment"/>
    /// and <see cref="Cohesion"/>, as PURE math only -- exactly what
    /// docs/23 §5's own match-core task line asks for ("Flocking.cs (pure
    /// math, unit positions in, steering out)"), no more. Neither is
    /// wired into <see cref="MatchState"/>'s own tick loop the way
    /// <see cref="Separate"/> is: docs/23 §5 frames alignment/cohesion as
    /// forces "for units sharing an order group," a concept match-core
    /// still doesn't have (docs/27 Phase B deferred it for queued group
    /// moves, for the identical reason) -- and unlike separation's hard
    /// "never overlap" correction (a real, already-accepted gap this
    /// class closed for sim-driven units in Phase C), docs/23 itself puts
    /// the actual STEERING integration under the Unity task line ("wire
    /// into MonsterAgent.FollowPath"), not match-core's. So this phase's
    /// match-core deliverable is the pure math plus the numeric harness
    /// docs/23 §5 explicitly asks for; the live integration lives in
    /// Unity's `MonsterSteeringController.Alignment`/`Cohesion`, wired
    /// additively into `Combine`.
    /// </summary>
    public static class Flocking
    {
        /// <summary>docs/23 §5 v0.1 weight table.</summary>
        public const double SeparationWeight = 1.0;

        /// <summary>docs/23 §5 v0.1 weight table: "align 0.35."</summary>
        public const double AlignmentWeight = 0.35;

        /// <summary>docs/23 §5 v0.1 weight table: "coh 0.15."</summary>
        public const double CohesionWeight = 0.15;

        /// <summary>One neighbour's position + body radius, as seen by
        /// <see cref="Separate"/> -- a plain snapshot rather than a
        /// <see cref="SimUnit"/> reference, so this class stays engine/
        /// entity-agnostic and independently testable with synthetic
        /// positions (matches <c>MonsterSteeringController</c>'s own
        /// dependency-free style).</summary>
        public readonly struct Neighbor
        {
            public readonly double X;
            public readonly double Z;
            public readonly double Radius;

            public Neighbor(double x, double z, double radius)
            {
                X = x;
                Z = z;
                Radius = radius;
            }
        }

        /// <summary>Net (dx, dz) displacement to push a body clear of every
        /// overlapping neighbour, cumulative in list order -- each
        /// neighbour after the first is checked against the position
        /// already nudged by earlier ones in this SAME call, exactly the
        /// idiom <c>MonsterSteeringController.SeparationForce</c> uses.
        /// Callers must supply neighbours in a fixed, deterministic order
        /// (match-core: entity-ID order) -- iterating a different order
        /// each run would make the exact per-tick nudge non-reproducible
        /// even though the eventual rest state converges the same, and
        /// docs/23 §0 forbids that class of nondeterminism outright.
        /// Restricted to the same IEEE-exact float-discipline allow-list
        /// as the rest of the sim tick path (+, -, *, /, sqrt).</summary>
        public static (double dx, double dz) Separate(double selfX, double selfZ, double selfRadius, IReadOnlyList<Neighbor> neighbors, double spacing)
        {
            var px = selfX;
            var pz = selfZ;
            for (var i = 0; i < neighbors.Count; i++)
            {
                var n = neighbors[i];
                var dx = px - n.X;
                var dz = pz - n.Z;
                var dist = Math.Sqrt(dx * dx + dz * dz);
                var minDist = selfRadius + n.Radius + spacing;
                if (dist < minDist && dist > 1e-9)
                {
                    var push = (minDist - dist) * 0.5 * SeparationWeight;
                    px += dx / dist * push;
                    pz += dz / dist * push;
                }
            }
            return (px - selfX, pz - selfZ);
        }

        /// <summary>docs/23 §5: "match average heading of groupmates
        /// within 12 m." Each entry in `neighborHeadings` is a groupmate's
        /// current movement-direction vector (not necessarily normalized,
        /// and not necessarily even nonzero -- a stationary groupmate has
        /// no heading to align to and is skipped, matching how a unit
        /// with zero velocity contributes nothing to
        /// <c>MonsterSteeringController.PredictiveAvoidance</c> either).
        /// The 12m proximity filter itself is the CALLER's job (match-core
        /// entity-ID order, Unity's spatial grid query) -- this function
        /// only ever sees whatever list it's handed, matching
        /// <see cref="Separate"/>'s own "caller supplies neighbours"
        /// contract. Returns a normalized direction, or (0,0) if there are
        /// no headings to align to, or they cancel out to (near) zero
        /// (e.g. two groupmates moving directly apart).</summary>
        public static (double dx, double dz) Alignment(IReadOnlyList<(double Hx, double Hz)> neighborHeadings)
        {
            double sumX = 0, sumZ = 0;
            var counted = 0;
            for (var i = 0; i < neighborHeadings.Count; i++)
            {
                var h = neighborHeadings[i];
                var mag = Math.Sqrt(h.Hx * h.Hx + h.Hz * h.Hz);
                if (mag < 1e-9) continue;
                sumX += h.Hx / mag;
                sumZ += h.Hz / mag;
                counted++;
            }
            if (counted == 0) return (0.0, 0.0);
            var avgX = sumX / counted;
            var avgZ = sumZ / counted;
            var avgMag = Math.Sqrt(avgX * avgX + avgZ * avgZ);
            return avgMag < 1e-9 ? (0.0, 0.0) : (avgX / avgMag, avgZ / avgMag);
        }

        /// <summary>docs/23 §5: "gentle pull toward group centroid, capped
        /// so it never fights the path." Returns a normalized direction
        /// toward the centroid of `neighborPositions` (the CAP is the
        /// caller's blend weight, <see cref="CohesionWeight"/>, not a
        /// magnitude limit here -- the same "return a unit vector, let the
        /// weighted blend bound it" shape <see cref="Alignment"/> and
        /// Unity's `Combine` both already use for their bias terms).
        /// Returns (0,0) if there are no groupmates, or self is already
        /// exactly at the centroid.</summary>
        public static (double dx, double dz) Cohesion(double selfX, double selfZ, IReadOnlyList<(double X, double Z)> neighborPositions)
        {
            if (neighborPositions.Count == 0) return (0.0, 0.0);
            double sumX = 0, sumZ = 0;
            for (var i = 0; i < neighborPositions.Count; i++)
            {
                sumX += neighborPositions[i].X;
                sumZ += neighborPositions[i].Z;
            }
            var centroidX = sumX / neighborPositions.Count;
            var centroidZ = sumZ / neighborPositions.Count;
            var dx = centroidX - selfX;
            var dz = centroidZ - selfZ;
            var mag = Math.Sqrt(dx * dx + dz * dz);
            return mag < 1e-9 ? (0.0, 0.0) : (dx / mag, dz / mag);
        }
    }
}
