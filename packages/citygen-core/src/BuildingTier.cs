using System;

namespace MadDr.CityGen
{
    /// <summary>Destructible-building tiers, docs/18 SS3: "a building is a
    /// stat block with Structure HP (== Vitality) and Armor, resolved
    /// through docs/04's existing damage formula unchanged."</summary>
    public enum BuildingTier
    {
        Small,    // house
        Medium,   // storefront
        Large,    // city block / tower
        Landmark, // town hall, cathedral, hospital...
    }

    /// <summary>The docs/18 SS3 tier table, verbatim. v0.1 numbers -- the
    /// tuning source of truth is the doc's table; change there first.
    ///
    /// 2026-08 (creator direction: "give buildings much larger hit
    /// points"): bumped from the original 300/600/1500/3000 baseline to
    /// land on the SAME absolute HP figures `MadDr.MatchCore.BuildingDef`
    /// uses for its own (separate) RTS-building roster after its own
    /// bump -- a procedural house/landmark a player attacks mid-match now
    /// feels comparably tough to a base of the same tier, instead of the
    /// two building systems drifting to different HP scales for "Small"/
    /// "Large"/etc. Armor unchanged, same "more hits to fell, no harder
    /// to actually damage per hit" reasoning.</summary>
    public static class BuildingStats
    {
        public static int StructureHp(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 1000;
                case BuildingTier.Medium: return 2000;
                case BuildingTier.Large: return 5000;
                case BuildingTier.Landmark: return 10000;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        public static int Armor(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 2;
                case BuildingTier.Medium: return 4;
                case BuildingTier.Large: return 6;
                case BuildingTier.Landmark: return 8;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }
    }
}
