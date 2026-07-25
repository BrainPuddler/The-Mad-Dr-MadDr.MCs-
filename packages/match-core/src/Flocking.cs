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
    /// Alignment and cohesion (docs/23 §5's other two flocking forces) are
    /// NOT implemented here. Both need an "order group" concept -- which
    /// units are moving together -- that match-core still doesn't have
    /// (docs/27 Phase B explicitly deferred the same concept for queued
    /// group moves, for the identical reason). Scoped to separation only,
    /// flagged here rather than guessed at.
    /// </summary>
    public static class Flocking
    {
        /// <summary>docs/23 §5 v0.1 weight table: separation is 1.0
        /// (full-strength; the other two weights, 0.35/0.15, belong to
        /// alignment/cohesion, not yet implemented).</summary>
        public const double SeparationWeight = 1.0;

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
    }
}
