using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>The three simulation-fidelity zones, docs/18 SS5: cost
    /// scales with where the fighting is, not total map area.</summary>
    public enum EngagementZone
    {
        /// <summary>Full server-authoritative sim (docs/04 combat,
        /// individually synced entities) -- docs/09's existing ~60-entity
        /// budget, reinterpreted as a per-zone cap.</summary>
        Engagement,

        /// <summary>Lightweight: buildings static/undamaged unless a zone
        /// escalates around them; Citizens run client-side cosmetic/crowd
        /// AI, not server-synced (docs/19 SS5). Low or no server tick.</summary>
        LocalCity,

        /// <summary>Pure visual LOD/impostors. No simulation at all.</summary>
        DistantSkyline,
    }

    /// <summary>The two zone-boundary radii, as data -- docs/18 SS5:
    /// "flagged as a v0.1 proposal pending its own perf spike (Q14/Q15);
    /// the radii are the first thing to retune," so retuning is a number
    /// passed in here, not a code change.</summary>
    public sealed class EngagementZoneConfig
    {
        public double EngagementRadiusMeters { get; }
        public double LocalCityRadiusMeters { get; }

        public EngagementZoneConfig(double engagementRadiusMeters, double localCityRadiusMeters)
        {
            if (engagementRadiusMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(engagementRadiusMeters));
            if (localCityRadiusMeters <= engagementRadiusMeters)
                throw new ArgumentOutOfRangeException(nameof(localCityRadiusMeters), "must exceed the engagement radius");
            EngagementRadiusMeters = engagementRadiusMeters;
            LocalCityRadiusMeters = localCityRadiusMeters;
        }

        /// <summary>docs/18 SS5's v0.1 numbers: 175 m (the midpoint of the
        /// doc's stated 150-200 m engagement-zone range) and 1000 m.</summary>
        public static EngagementZoneConfig Default
        {
            get { return new EngagementZoneConfig(175.0, 1000.0); }
        }
    }

    /// <summary>
    /// Classifies positions into the docs/18 SS5 zones by distance to the
    /// nearest live engagement -- "dynamically re-centered on live
    /// engagements as armies move." Engagement centers are passed in
    /// fresh each call rather than owned as internal state: whatever
    /// tracks where fighting is currently happening (the match sim, which
    /// doesn't exist yet in this repo) is the source of truth, not this
    /// class.
    /// </summary>
    public static class EngagementZoneManager
    {
        /// <summary>No live engagements anywhere on the map means nothing
        /// needs full-fidelity sim -- everything is DistantSkyline.</summary>
        public static EngagementZone ClassifyHex(
            HexCoord position, IReadOnlyList<HexCoord> engagementCenters, EngagementZoneConfig config)
        {
            if (engagementCenters.Count == 0) return EngagementZone.DistantSkyline;
            return ZoneFor(NearestDistanceMeters(position, engagementCenters), config);
        }

        /// <summary>A building is classified by its CLOSEST footprint hex
        /// to the nearest engagement, not its centroid -- a large
        /// building with one corner inside the engagement radius should
        /// get full fidelity, not be excluded because its middle is
        /// slightly outside.</summary>
        public static EngagementZone ClassifyBuilding(
            Building building, IReadOnlyList<HexCoord> engagementCenters, EngagementZoneConfig config)
        {
            if (engagementCenters.Count == 0) return EngagementZone.DistantSkyline;

            var best = double.MaxValue;
            foreach (var hex in building.Footprint)
            {
                var d = NearestDistanceMeters(hex, engagementCenters);
                if (d < best) best = d;
            }
            return ZoneFor(best, config);
        }

        private static EngagementZone ZoneFor(double distanceMeters, EngagementZoneConfig config)
        {
            if (distanceMeters <= config.EngagementRadiusMeters) return EngagementZone.Engagement;
            if (distanceMeters <= config.LocalCityRadiusMeters) return EngagementZone.LocalCity;
            return EngagementZone.DistantSkyline;
        }

        private static double NearestDistanceMeters(HexCoord position, IReadOnlyList<HexCoord> centers)
        {
            var (px, pz) = position.ToWorld();
            var best = double.MaxValue;
            foreach (var c in centers)
            {
                var (cx, cz) = c.ToWorld();
                var dx = px - cx;
                var dz = pz - cz;
                var dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist < best) best = dist;
            }
            return best;
        }
    }
}
