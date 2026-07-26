using System;

namespace MadDr.CityGen
{
    /// <summary>Road-network patterns, one per docs/18 SS1 preset row:
    /// Village and Small Town both = "one Main Street arterial + a
    /// perpendicular residential grid" (creator direction, 2026-07:
    /// Village must NOT lay out as hex-connected/radial roads -- base it
    /// on a real North American town, 1950s, population 8,000-30,000,
    /// which means a Main-Street grid downtown, not a wagon-wheel plaza),
    /// Big City = "dense grid". The two MainStreet presets differ only in
    /// DATA (size, pitch, density) -- same pattern, smaller town.</summary>
    public enum RoadPattern
    {
        MainStreet,
        Grid,

        /// <summary>docs/23 §8 (Phase 8): "the cardinal grid plus 2
        /// diagonal avenues meeting at a grand roundabout" -- Paris's own
        /// pattern. The cardinal net is identical to <see cref="Grid"/>'s
        /// own (streets both ways at the block pitch; docs/23 §8's prose
        /// names it "the cardinal grid," a documented reading of that
        /// phrase as Grid's dense net rather than MainStreet's single-
        /// arterial-plus-sparse-residential one). The two diagonal
        /// avenues and the grand ("l'Étoile") roundabout at their
        /// crossing are traced separately -- see
        /// <see cref="CityGenerator"/>'s own Boulevard-specific
        /// code.</summary>
        Boulevard,
    }

    /// <summary>docs/23 §8 Phase 8: which real-world 1950s flagship city
    /// (if any) a preset represents -- lets Unity's per-region dressing
    /// branch (BuildingDresser/RoadDresser palette + prop switches, NOT
    /// implemented by this citygen-core slice) key off ONE field instead
    /// of string-matching <see cref="CityPreset.Name"/>. Every
    /// pre-Phase-8 preset (Village/SmallTown/BigCity) is
    /// <see cref="Generic"/> -- a generic 1950s North American town/city,
    /// not modeled on one specific real place.</summary>
    public enum CityRegion
    {
        Generic,
        NewYork,
        Paris,
        Montreal,
    }

    /// <summary>A style preset is DATA, not code (docs/18 SS2: "one
    /// generator, a small authored kit of style presets"). Geometry-
    /// affecting knobs live here; facade/prop kits are renderer-side
    /// strings the Unity layer resolves to art. All numbers v0.1.</summary>
    public sealed class CityPreset
    {
        public string Name { get; }
        public CityRegion Region { get; }
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
            int hillRadiusHexes,
            CityRegion region = CityRegion.Generic)
        {
            if (blockPitch < 3) throw new ArgumentOutOfRangeException(nameof(blockPitch), "pitch < 3 leaves no buildable interior");
            if (Math.Abs(smallWeight + mediumWeight + largeWeight - 1.0) > 1e-9)
                throw new ArgumentException("tier weights must sum to 1");
            Name = name;
            Region = region;
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

        /// <summary>~1.4 km x 1.4 km, a Main Street grid (docs/18 SS1;
        /// creator direction, 2026-07: modeled on a real North American
        /// town, 1950s, population 8,000-30,000 -- NOT hex-connected/
        /// radial roads around a plaza). Smaller and sparser than Small
        /// Town's own Main-Street grid, not a different pattern: a
        /// modest downtown strip along the arterial, houses filling out
        /// the residential blocks around it. A 1-hex stream with two
        /// footbridges; a couple of ponds and low hills.</summary>
        public static CityPreset Village()
        {
            return new CityPreset(
                "village", 70, 70, RoadPattern.MainStreet,
                blockPitch: 6, buildDensity: 0.42,
                smallWeight: 0.75, mediumWeight: 0.22, largeWeight: 0.03,
                emitterArchetypes: new[] { "town_hall", "plaza", "church" },
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

        /// <summary>docs/23 §8: "the Big City preset, personified" --
        /// same scale/pattern/density knobs as <see cref="BigCity"/>
        /// verbatim (the docs' own words for this region), re-skinned
        /// with New York's own named landmarks ("Liberty Statuette
        /// plaza, Grand Terminal") as real emitter archetype strings.
        /// Everything else docs/23 §8 names for New York (brownstone/
        /// skyscraper massing, Checker-cab traffic palette, elevated
        /// rail, Times Square neon, fire escapes) is Unity-side dressing,
        /// out of scope for this citygen-core preset -- see docs/23
        /// Phase 8's own status note.</summary>
        public static CityPreset NewYork()
        {
            return new CityPreset(
                "new_york", 250, 250, RoadPattern.Grid,
                blockPitch: 7, buildDensity: 0.65,
                smallWeight: 0.30, mediumWeight: 0.40, largeWeight: 0.30,
                emitterArchetypes: new[] { "liberty_statuette_plaza", "grand_terminal", "cathedral" },
                hubArchetypes: Hubs,
                riverWidthHexes: 3, bridgeCount: 3,
                pondCount: 5, hillCount: 6, hillRadiusHexes: 3,
                region: CityRegion.NewYork);
        }

        /// <summary>docs/23 §8: "the roundabout showcase" -- introduces
        /// <see cref="RoadPattern.Boulevard"/> (see that enum member's own
        /// doc comment for the cardinal-grid reading and
        /// <see cref="CityGenerator"/>'s Boulevard-specific code for the
        /// diagonal-avenue/l'Étoile geometry). Size is a v0.1 placeholder
        /// -- unlike Village/SmallTown/BigCity, docs/23 §8 gives Paris no
        /// explicit km^2 figure, so this reuses SmallTown's own scale as
        /// the closest documented analog rather than inventing a new
        /// number from nothing. Landmarks use the real archetype strings
        /// docs/23 §8 names ("Iron Tower," "Sacré-Cœur hill"). Haussmann
        /// massing, Métro props, café awnings, and plane trees are
        /// Unity-side dressing, out of scope here.</summary>
        public static CityPreset Paris()
        {
            return new CityPreset(
                "paris", 100, 100, RoadPattern.Boulevard,
                blockPitch: 8, buildDensity: 0.55,
                smallWeight: 0.20, mediumWeight: 0.65, largeWeight: 0.15,
                emitterArchetypes: new[] { "iron_tower", "cathedral", "plaza" },
                hubArchetypes: Hubs,
                riverWidthHexes: 2, bridgeCount: 2,
                pondCount: 2, hillCount: 3, hillRadiusHexes: 3,
                region: CityRegion.Paris);
        }

        /// <summary>docs/23 §8: "the character piece" -- mid-density
        /// MainStreet, same pattern SmallTown already uses. Size is a
        /// v0.1 placeholder for the same reason as <see cref="Paris"/>'s
        /// (docs/23 §8 gives no explicit figure for Montreal either). A
        /// bigger, wider hill cluster than any other preset stands in for
        /// Mount Royal ("the big ridge cluster"); the Old Port crane row
        /// docs/23 §8 names needs no new generator code at all ("which
        /// the generator already carves" -- its own words, referring to
        /// the existing river/bridge system). Duplex spiral staircases,
        /// bilingual signage, and the illuminated cross are Unity-side
        /// dressing, out of scope here.</summary>
        public static CityPreset Montreal()
        {
            return new CityPreset(
                "montreal", 120, 120, RoadPattern.MainStreet,
                blockPitch: 7, buildDensity: 0.50,
                smallWeight: 0.55, mediumWeight: 0.35, largeWeight: 0.10,
                emitterArchetypes: new[] { "marche_tower", "forum_arena", "town_hall" },
                hubArchetypes: Hubs,
                riverWidthHexes: 2, bridgeCount: 3,
                pondCount: 2, hillCount: 1, hillRadiusHexes: 6,
                region: CityRegion.Montreal);
        }
    }
}
