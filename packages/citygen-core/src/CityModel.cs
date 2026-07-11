using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>A generated building: a footprint of hexes and a tier.
    /// Landmark-tier buildings carry the archetype string of the landmark
    /// they house (plaza, hospital...); ordinary buildings leave it empty
    /// -- their look comes from the style preset's skin pass, which is
    /// renderer-side data, not logic (docs/18 SS2).</summary>
    public sealed class Building
    {
        public IReadOnlyList<HexCoord> Footprint { get; }
        public BuildingTier Tier { get; }
        public string Archetype { get; }

        public Building(IReadOnlyList<HexCoord> footprint, BuildingTier tier, string archetype = "")
        {
            Footprint = footprint;
            Tier = tier;
            Archetype = archetype;
        }
    }

    public enum LandmarkKind
    {
        /// <summary>Hosts a mana emitter (docs/03): plaza, town hall,
        /// cathedral, rail depot -- preset-dependent.</summary>
        Emitter,

        /// <summary>A Community Hub (hospital / school / old-age home)
        /// hosting exactly one Collection Station (docs/18 SS2, docs/20).</summary>
        CommunityHub,
    }

    /// <summary>A landmark node from the generator's allocation pass
    /// (docs/18 SS2): each node is EITHER an emitter host OR a Community
    /// Hub, never both.</summary>
    public sealed class Landmark
    {
        /// <summary>An emitter's aura radius in hexes (docs/03).</summary>
        public const int EmitterAuraRadiusHexes = 3;

        /// <summary>A Collection Station's harvest radius in hexes
        /// (docs/18 SS2: "deliberately larger than the emitter's 3-hex
        /// aura, since a Community Hub footprint is a large campus
        /// building, not a point landmark").</summary>
        public const int CollectionStationRadiusHexes = 5;

        public LandmarkKind Kind { get; }
        public string Archetype { get; }
        public HexCoord Site { get; }

        public Landmark(LandmarkKind kind, string archetype, HexCoord site)
        {
            Kind = kind;
            Archetype = archetype;
            Site = site;
        }

        /// <summary>The radius this landmark's mechanic covers: emitter
        /// aura (3) or Collection Station harvest radius (5).</summary>
        public int RadiusHexes
        {
            get
            {
                return Kind == LandmarkKind.Emitter
                    ? EmitterAuraRadiusHexes
                    : CollectionStationRadiusHexes;
            }
        }
    }

    /// <summary>The generated city: a pure-data description a renderer
    /// walks and a match server indexes. Deterministic content for a
    /// given (seed, preset) -- docs/18 SS2's determinism requirement; the
    /// generator itself never touches wall-clock, hash order, or any
    /// RNG but the seeded one it is handed.</summary>
    public sealed class CityModel
    {
        public uint Seed { get; }
        public string PresetName { get; }
        public int WidthHexes { get; }
        public int HeightHexes { get; }

        /// <summary>Road hexes, sorted by (R, Q) -- stable order so two
        /// models generated from the same inputs compare identical
        /// element-by-element, not just set-equal.</summary>
        public IReadOnlyList<HexCoord> Roads { get; }

        public IReadOnlyList<Building> Buildings { get; }
        public IReadOnlyList<Landmark> Landmarks { get; }

        public CityModel(
            uint seed,
            string presetName,
            int widthHexes,
            int heightHexes,
            IReadOnlyList<HexCoord> roads,
            IReadOnlyList<Building> buildings,
            IReadOnlyList<Landmark> landmarks)
        {
            Seed = seed;
            PresetName = presetName;
            WidthHexes = widthHexes;
            HeightHexes = heightHexes;
            Roads = roads;
            Buildings = buildings;
            Landmarks = landmarks;
        }
    }
}
