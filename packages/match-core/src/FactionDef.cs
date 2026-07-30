using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>The four factions a player can pick at match start
    /// (2026-07 amendment, docs/23 §1 2026-07 update -- superseding this
    /// enum's own former "never picked here" note). <see cref="Mixed"/>
    /// used to be earned mid-match ONLY via the Chimera Track (salvaging
    /// all three origins, amendment F, still real and still how a
    /// mono-faction player reaches hybrid parts in-match); it is now ALSO
    /// a starting pick, but gated behind a persistent account-level
    /// unlock (docs/12: "win a campaign") rather than open from turn one
    /// -- see <c>MixedFactionUnlock</c> in the Unity layer. Deliberately
    /// NOT a stat-boosted "best of all three" option: a Mixed player's own
    /// <see cref="FactionLumenTable"/> row is <see cref="FactionLumenModifier.None"/>
    /// (no faction-level bonus of its own); each unit fielded under Mixed
    /// resolves its OWN race's bonuses/handicaps individually (<see
    /// cref="SimUnit.RaceOverride"/>), so fielding one of each race nets
    /// exactly the sum of three mono-faction units, never a stacked
    /// advantage -- the reward is roster breadth/versatility, not raw
    /// power.</summary>
    public enum FactionId
    {
        MadDoctor = 0,
        HumanArmy = 1,
        AlienHive = 2,
        Mixed = 3,
    }

    /// <summary>Static per-faction data (docs/23 §1/§2): themed HQ name,
    /// origin bias, primary energy resource, and a stable display color as
    /// a packed 0xRRGGBB int (kept engine-agnostic -- the Unity view maps
    /// it to a UnityEngine.Color). This is DATA read by the sim, never
    /// simulation state, so it isn't part of the tick hash.
    ///
    /// <see cref="OriginBias"/>/<see cref="Energy"/> are nullable ONLY for
    /// <see cref="FactionId.Mixed"/> -- it has no single origin or energy
    /// currency of its own (each fielded unit keeps its OWN race's
    /// origin/energy, same devolve-to-the-unit design as the Lumen table
    /// above); every other faction keeps the real, non-null value it
    /// always had.</summary>
    public sealed class FactionDef
    {
        public FactionId Id { get; }
        public string DisplayName { get; }
        public string BaseName { get; }        // themed HQ, canon per docs/23 §2
        public Origin? OriginBias { get; }
        public ResourceKind? Energy { get; }
        public int ColorRgb { get; }

        public FactionDef(FactionId id, string displayName, string baseName,
            Origin? originBias, ResourceKind? energy, int colorRgb)
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
            // "The Patchwork Ward" -- a stitched-together compound
            // matching the mad-doctor surgery/salvage theme (CLAUDE.md:
            // "nothing is wasted"), not a fourth pure origin. No single
            // OriginBias/Energy: a Mixed player's buildings/units keep
            // whichever origin/energy their OWN race already has.
            new FactionDef(FactionId.Mixed, "The Mixed", "The Patchwork Ward",
                null, null, 0x6E5A8C),
        };

        public static IReadOnlyList<FactionDef> AllFactions => All;

        public static FactionDef Get(FactionId id) => All[(int)id];
    }
}
