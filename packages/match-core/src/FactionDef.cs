using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>The three playable factions at match start (docs/23 §1).
    /// The 4th category (Hybrids/Chimera) is UNLOCKED via the Chimera
    /// Track, never picked here (docs/23 §13 amendment F).</summary>
    public enum FactionId
    {
        MadDoctor = 0,
        HumanArmy = 1,
        AlienHive = 2,
    }

    /// <summary>Static per-faction data (docs/23 §1/§2): themed HQ name,
    /// origin bias, primary energy resource, and a stable display color as
    /// a packed 0xRRGGBB int (kept engine-agnostic -- the Unity view maps
    /// it to a UnityEngine.Color). This is DATA read by the sim, never
    /// simulation state, so it isn't part of the tick hash.</summary>
    public sealed class FactionDef
    {
        public FactionId Id { get; }
        public string DisplayName { get; }
        public string BaseName { get; }        // themed HQ, canon per docs/23 §2
        public Origin OriginBias { get; }
        public ResourceKind Energy { get; }
        public int ColorRgb { get; }

        public FactionDef(FactionId id, string displayName, string baseName,
            Origin originBias, ResourceKind energy, int colorRgb)
        {
            Id = id;
            DisplayName = displayName;
            BaseName = baseName;
            OriginBias = originBias;
            Energy = energy;
            ColorRgb = colorRgb;
        }

        private static readonly FactionDef[] All =
        {
            new FactionDef(FactionId.MadDoctor, "The Mad Doctor", "The Sanatorium",
                Origin.Organic, ResourceKind.Blood, 0x9C2A2A),
            new FactionDef(FactionId.HumanArmy, "The Human Army", "Fort Vigilance",
                Origin.Tech, ResourceKind.Fuel, 0x3A6E3A),
            new FactionDef(FactionId.AlienHive, "The Alien Hive", "The Brood Nest",
                Origin.Biotech, ResourceKind.Ichor, 0x2E8B57),
        };

        public static IReadOnlyList<FactionDef> AllFactions => All;

        public static FactionDef Get(FactionId id) => All[(int)id];
    }
}
