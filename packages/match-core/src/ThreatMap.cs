using System;
using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §6's other named Phase 6c ingredient (alongside
    /// build orders): "threat maps." A SNAPSHOT of how dangerous each
    /// point on the field is to one particular player, taken once per
    /// decision and then queried many times while scoring candidate
    /// actions.
    ///
    /// Deliberately not a materialized grid. A `BigCity` preset is 140x140
    /// hexes -- ~20k cells to fill every decision, almost all of them
    /// never read -- while a match fields tens of units (docs/23 §13-E's
    /// own supply cap targets 20-40 per player). Scanning the small list
    /// on demand is both cheaper and exact, with no cell-resolution
    /// artifacts. The "map" is the falloff FUNCTION, not a table.
    ///
    /// Threat falls off linearly to exactly zero at
    /// <see cref="ThreatRadiusMeters"/>: bounded, cheap, and inside the
    /// same +,-,*,/ arithmetic the rest of this package restricts itself
    /// to (docs/23 §0 -- no Exp/Sin in reach of sim math, even though this
    /// class sits outside the tick; consistency beats a marginally
    /// prettier curve).</summary>
    public sealed class ThreatMap
    {
        /// <summary>Distance at which an enemy contributes no threat at
        /// all. A v0.1 placeholder -- docs/23 names threat maps but gives
        /// no radius -- chosen as a small multiple of `HexCoord.HexMeters`
        /// so it spans a few hexes of engagement range rather than the
        /// whole map.</summary>
        public const double ThreatRadiusMeters = 60.0;

        private readonly struct Source
        {
            public readonly double X;
            public readonly double Z;
            public readonly double Power;

            public Source(double x, double z, double power)
            {
                X = x;
                Z = z;
                Power = power;
            }
        }

        private readonly List<Source> _sources = new List<Source>();

        /// <summary>Total <see cref="SimUnit.EffectivePower"/> of every
        /// living enemy counted into this snapshot -- a cheap "how big is
        /// the army I'm facing at all" number, independent of
        /// position.</summary>
        public double TotalEnemyPower { get; private set; }

        public int SourceCount => _sources.Count;

        private ThreatMap() { }

        /// <summary>Snapshot every unit hostile to
        /// <paramref name="forPlayerIndex"/>. match-core has no team/ally
        /// concept yet (docs/23 §11's 4v4 teams are a later phase), so
        /// "hostile" means simply "a different player index" -- correct
        /// for 1v1 and FFA, and the honest thing to do rather than
        /// inventing a team model this package doesn't have. Dead units
        /// contribute nothing (a corpse threatens no one; it's
        /// <em>loot</em> -- see <see cref="SkirmishCommander"/>'s salvage
        /// scoring).</summary>
        public static ThreatMap From(MatchState state, int forPlayerIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var map = new ThreatMap();
            for (var i = 0; i < state.UnitCount; i++)
            {
                var u = state.UnitAt(i);
                if (u.PlayerIndex == forPlayerIndex || !u.IsAlive || u.Combat == null) continue;
                var power = u.EffectivePower;
                map._sources.Add(new Source(u.X, u.Z, power));
                map.TotalEnemyPower += power;
            }
            return map;
        }

        /// <summary>Threat at a world position: every in-range enemy's
        /// effective power, scaled by how close it is. Zero on an empty
        /// or far-from-everything field -- never negative.</summary>
        public double ThreatAt(double x, double z)
        {
            var total = 0.0;
            for (var i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                var dx = s.X - x;
                var dz = s.Z - z;
                var dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist >= ThreatRadiusMeters) continue;
                total += s.Power * (1.0 - dist / ThreatRadiusMeters);
            }
            return total;
        }

        /// <summary>Threat normalized against this snapshot's own worst
        /// case (every enemy stacked on one point), so a commander's
        /// caution weighting means the same thing in a skirmish and in a
        /// full-army brawl. Always in [0,1]; 0 when there are no enemies
        /// at all.</summary>
        public double NormalizedThreatAt(double x, double z)
        {
            if (TotalEnemyPower <= 0.0) return 0.0;
            var t = ThreatAt(x, z) / TotalEnemyPower;
            return t > 1.0 ? 1.0 : t;
        }
    }
}
