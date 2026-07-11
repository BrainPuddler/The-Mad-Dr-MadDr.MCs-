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
    /// tuning source of truth is the doc's table; change there first.</summary>
    public static class BuildingStats
    {
        public static int StructureHp(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 300;
                case BuildingTier.Medium: return 600;
                case BuildingTier.Large: return 1500;
                case BuildingTier.Landmark: return 3000;
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
