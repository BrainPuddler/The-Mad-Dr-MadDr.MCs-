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

    /// <summary>A destructible river crossing (docs/18 terrain). A bridge
    /// IS a road surface over water: its hexes appear in
    /// <see cref="CityModel.Roads"/> and are excluded from
    /// <see cref="CityModel.Water"/>. Destroying it removes those road
    /// hexes and reverts them to water -- severing the crossing for
    /// ground units (amphibious and airborne plans don't care, which is
    /// exactly the counterplay). Stats reuse the Large building tier
    /// (1500 Structure HP / 6 Armor, docs/18 SS3) -- cutting an army's
    /// path is meant to be a real investment, not a drive-by.</summary>
    public sealed class Bridge
    {
        public IReadOnlyList<HexCoord> Footprint { get; }

        public BuildingTier Tier
        {
            get { return BuildingTier.Large; }
        }

        public Bridge(IReadOnlyList<HexCoord> footprint)
        {
            Footprint = footprint;
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
        /// element-by-element, not just set-equal. Includes bridge decks.</summary>
        public IReadOnlyList<HexCoord> Roads { get; }

        /// <summary>Water hexes (river + ponds), sorted by (R, Q).
        /// Impassable to ground plans; amphibious plans (crab,
        /// serpentine -- genome-core catalog) cross freely; winged/
        /// floater pass over. Excludes standing bridge decks -- a
        /// destroyed bridge's hexes rejoin this set at runtime.</summary>
        public IReadOnlyList<HexCoord> Water { get; }

        /// <summary>Ridge (high-ground) hexes, sorted by (R, Q) -- the
        /// existing docs/02/04 ridge feature, now placed by generation:
        /// +0.10 posMod for an attacker on them, winged fly over.</summary>
        public IReadOnlyList<HexCoord> Ridges { get; }

        public IReadOnlyList<Building> Buildings { get; }
        public IReadOnlyList<Landmark> Landmarks { get; }
        public IReadOnlyList<Bridge> Bridges { get; }

        public CityModel(
            uint seed,
            string presetName,
            int widthHexes,
            int heightHexes,
            IReadOnlyList<HexCoord> roads,
            IReadOnlyList<HexCoord> water,
            IReadOnlyList<HexCoord> ridges,
            IReadOnlyList<Building> buildings,
            IReadOnlyList<Landmark> landmarks,
            IReadOnlyList<Bridge> bridges)
        {
            Seed = seed;
            PresetName = presetName;
            WidthHexes = widthHexes;
            HeightHexes = heightHexes;
            Roads = roads;
            Water = water;
            Ridges = ridges;
            Buildings = buildings;
            Landmarks = landmarks;
            Bridges = bridges;
        }

        /// <summary>Whether a hex lies on this map at all -- the inverse
        /// of the odd-r offset rectangle the generator enumerates. Every
        /// consumer that walks outward from a hex (spawn placement,
        /// wander targets, future pathfinding) needs this: "not in the
        /// blocked set" is NOT the same as "on the map", and treating
        /// off-map hexes as walkable is exactly how the first spawned
        /// monsters wandered out of the city.</summary>
        public bool Contains(HexCoord hex)
        {
            var row = hex.R;
            if (row < 0 || row >= HeightHexes) return false;
            var col = hex.Q + (row - (row & 1)) / 2;
            return col >= 0 && col < WidthHexes;
        }

        /// <summary>The map's central hex -- NOT axial (0,0), which is the
        /// offset rectangle's top-left corner. Spawning "at the middle of
        /// the city" means this hex.</summary>
        public HexCoord CenterHex
        {
            get { return HexCoord.FromOffset(WidthHexes / 2, HeightHexes / 2); }
        }
    }
}
