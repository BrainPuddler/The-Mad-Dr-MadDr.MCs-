using System;

namespace MadDr.CityGen
{
    /// <summary>Road-network patterns, one per docs/18 SS1 preset row:
    /// Village = "organic/radial streets around a central plaza",
    /// Small Town = "one Main Street arterial + a perpendicular
    /// residential grid", Big City = "dense grid".</summary>
    public enum RoadPattern
    {
        Radial,
        MainStreet,
        Grid,
    }

    /// <summary>A style preset is DATA, not code (docs/18 SS2: "one
    /// generator, a small authored kit of style presets"). Geometry-
    /// affecting knobs live here; facade/prop kits are renderer-side
    /// strings the Unity layer resolves to art. All numbers v0.1.</summary>
    public sealed class CityPreset
    {
        public string Name { get; }
        public int WidthHexes { get; }
        public int HeightHexes { get; }
        public RoadPattern Pattern { get; }

        /// <summary>Hexes between parallel roads (grid pitch / ring
        /// spacing). Pitch 7 = 6 buildable hexes = 120 m blocks.</summary>
        public int BlockPitch { get; }

        /// <summary>Chance a buildable hex starts a building footprint.</summary>
        public double BuildDensity { get; }

        /// <summary>Tier mix for ordinary buildings: probability of
        /// Small / Medium / Large. Must sum to 1.</summary>
        public double SmallWeight { get; }
        public double MediumWeight { get; }
        public double LargeWeight { get; }

        /// <summary>Landmark archetypes hosting emitters (docs/18 SS2:
        /// "plaza, town hall, cathedral, rail depot -- preset-dependent").</summary>
        public string[] EmitterArchetypes { get; }

        /// <summary>Community Hub archetypes (docs/18 SS2: hospital,
        /// school, old-age home -- the same list for every preset).</summary>
        public string[] HubArchetypes { get; }

        /// <summary>River band width in hexes (20 m each); 0 = no river.
        /// The river is THE natural choke point: it severs the map into
        /// two banks joined only by bridges (docs/18 terrain).</summary>
        public int RiverWidthHexes { get; }

        /// <summary>Bridges across the river -- deliberately scarce;
        /// scarcity is what makes them choke points. Destructible
        /// (docs/18 SS3): a destroyed bridge reverts to water.</summary>
        public int BridgeCount { get; }

        /// <summary>Pond blobs: local impassable obstacles you walk
        /// around rather than bridge over.</summary>
        public int PondCount { get; }

        /// <summary>Hill blobs of ridge (high-ground) hexes -- the
        /// existing docs/04 +0.10 posMod terrain, now generated.</summary>
        public int HillCount { get; }

        public int HillRadiusHexes { get; }

        /// <summary>Nominal map area: (W x 20 m) x (H x 20 m). "Nominal"
        /// because it treats each hex column/row as a 20 m step rather
        /// than integrating true hex area -- the same simplification the
        /// docs' own "km^2 of built area" densities use.</summary>
        public double AreaKm2
        {
            get { return WidthHexes * 0.02 * (HeightHexes * 0.02); }
        }

        public CityPreset(
            string name,
            int widthHexes,
            int heightHexes,
            RoadPattern pattern,
            int blockPitch,
            double buildDensity,
            double smallWeight,
            double mediumWeight,
            double largeWeight,
            string[] emitterArchetypes,
            string[] hubArchetypes,
            int riverWidthHexes,
            int bridgeCount,
            int pondCount,
            int hillCount,
            int hillRadiusHexes)
        {
            if (blockPitch < 3) throw new ArgumentOutOfRangeException(nameof(blockPitch), "pitch < 3 leaves no buildable interior");
            if (Math.Abs(smallWeight + mediumWeight + largeWeight - 1.0) > 1e-9)
                throw new ArgumentException("tier weights must sum to 1");
            Name = name;
            WidthHexes = widthHexes;
            HeightHexes = heightHexes;
            Pattern = pattern;
            BlockPitch = blockPitch;
            BuildDensity = buildDensity;
            SmallWeight = smallWeight;
            MediumWeight = mediumWeight;
            LargeWeight = largeWeight;
            EmitterArchetypes = emitterArchetypes;
            HubArchetypes = hubArchetypes;
            RiverWidthHexes = riverWidthHexes;
            BridgeCount = bridgeCount;
            PondCount = pondCount;
            HillCount = hillCount;
            HillRadiusHexes = hillRadiusHexes;
        }

        private static readonly string[] Hubs = { "hospital", "school", "old_age_home" };

        /// <summary>~1 km x 1 km, radial around a central plaza (docs/18 SS1).
        /// A 1-hex stream with two footbridges; a couple of ponds and low hills.</summary>
        public static CityPreset Village()
        {
            return new CityPreset(
                "village", 50, 50, RoadPattern.Radial,
                blockPitch: 6, buildDensity: 0.35,
                smallWeight: 0.80, mediumWeight: 0.18, largeWeight: 0.02,
                emitterArchetypes: new[] { "plaza", "church" },
                hubArchetypes: Hubs,
                riverWidthHexes: 1, bridgeCount: 2,
                pondCount: 2, hillCount: 3, hillRadiusHexes: 2);
        }

        /// <summary>~2 km x 2 km, Main Street arterial + perpendicular
        /// residential grid (docs/18 SS1). A 2-hex river, two bridges.</summary>
        public static CityPreset SmallTown()
        {
            return new CityPreset(
                "small_town", 100, 100, RoadPattern.MainStreet,
                blockPitch: 8, buildDensity: 0.45,
                smallWeight: 0.60, mediumWeight: 0.30, largeWeight: 0.10,
                emitterArchetypes: new[] { "town_hall", "plaza", "rail_depot" },
                hubArchetypes: Hubs,
                riverWidthHexes: 2, bridgeCount: 2,
                pondCount: 3, hillCount: 4, hillRadiusHexes: 2);
        }

        /// <summary>Up to 5 km x 5 km, dense grid (docs/18 SS1 ceiling:
        /// 250 x 250 hexes at 20 m/hex). A 3-hex (60 m) river, three bridges.</summary>
        public static CityPreset BigCity()
        {
            return new CityPreset(
                "big_city", 250, 250, RoadPattern.Grid,
                blockPitch: 7, buildDensity: 0.65,
                smallWeight: 0.30, mediumWeight: 0.40, largeWeight: 0.30,
                emitterArchetypes: new[] { "plaza", "cathedral", "town_hall", "rail_depot" },
                hubArchetypes: Hubs,
                riverWidthHexes: 3, bridgeCount: 3,
                pondCount: 5, hillCount: 6, hillRadiusHexes: 3);
        }
    }
}
