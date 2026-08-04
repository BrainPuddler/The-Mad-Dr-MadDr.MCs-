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

        /// <summary>2026-08 (creator report: "I don't see people fleeing
        /// from the wreckage of the building"): occupant/garrison count
        /// for a PROCEDURAL civilian building -- houses/shops/landmarks,
        /// the vast majority of a generated city and (per `MonsterAgent.
        /// TickAttack`) fully attackable/destructible today. Disgorge-on-
        /// destroy (`RuntimeCityBuilder.SpawnFleeingOccupant`, "when they
        /// are destroyed they disgorge their human occupants that flee")
        /// was wired ONLY to `MadDr.MatchCore.BuildingDef.Occupants`, the
        /// SEPARATE RTS-building roster (HQ/Factory/storage) -- a house or
        /// shop dying only ever produced rubble FX, nobody fled, since
        /// procedural `Building` had no occupant concept of its own at
        /// all. Same "small flat garrison scaled loosely by tier" v0.1
        /// placeholder policy `BuildingDef.Occupants`'s own doc comment
        /// already states for the RTS roster, applied here for the
        /// procedural one -- not sourced from any design doc.</summary>
        public static int Occupants(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 3;
                case BuildingTier.Medium: return 5;
                case BuildingTier.Large: return 10;
                case BuildingTier.Landmark: return 15;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>2026-08 (creator direction: "it should start with 1
        /// but then others popup in different places based on the
        /// building size up to 8"): how many simultaneous fire points a
        /// Damaged building of this tier eventually grows to -- one fire
        /// lands the instant it's Damaged regardless of tier, then more
        /// stagger in over the next several seconds up to this total for
        /// anything bigger.
        ///
        /// 2026-08 follow-up (creator direction: "YOU may add more fire
        /// as the building is attacked. 2-4 depending on the size of the
        /// building"): the 1-8 range above is REPLACED with 2-4 -- every
        /// building now starts with at least 2 fire points instead of a
        /// single one, and the ceiling drops from 8 down to 4. Same v0.1
        /// "small flat count scaled loosely by tier" placeholder policy
        /// as `Occupants`' own doc comment already states.</summary>
        public static int FireCount(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 2;
                case BuildingTier.Medium: return 3;
                case BuildingTier.Large: return 4;
                case BuildingTier.Landmark: return 4;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>2026-08 (creator report: "I've never seen the smoke
        /// either"): traced to the smoke puff itself being sized for
        /// nothing bigger than a doorway -- a Small building's own 1x
        /// puff read fine at 6m tall, but the SAME fixed puff size on a
        /// 40m Landmark was lost against the roofline the moment it
        /// spawned. This is the size multiplier a Damaged building's
        /// smoke plume scales its puffs by, same "small flat number
        /// scaled loosely by tier" placeholder policy as <see
        /// cref="FireCount"/> and <see cref="Occupants"/> -- 1.0 leaves
        /// Small exactly as it always rendered; bigger tiers scale up so
        /// the plume stays proportionate (and visible) against their own
        /// taller silhouette.</summary>
        public static float SmokeScale(BuildingTier tier)
        {
            switch (tier)
            {
                case BuildingTier.Small: return 1.0f;
                case BuildingTier.Medium: return 1.5f;
                case BuildingTier.Large: return 2.2f;
                case BuildingTier.Landmark: return 3.0f;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }
    }
}
