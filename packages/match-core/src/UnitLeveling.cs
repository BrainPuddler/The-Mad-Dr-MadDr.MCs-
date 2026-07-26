namespace MadDr.MatchCore
{
    /// <summary>docs/23 §4 "RPG layer": XP, levels, and the per-level stat
    /// bonus curve, kept as pure/testable math -- same discipline as
    /// <see cref="CombatMath"/>. "Every unit is a character," but only a
    /// combat-capable unit (<see cref="SimUnit.Combat"/> non-null) can
    /// gain XP at all -- a pure-movement unit has no `UnitRuntime` concept
    /// of levels, matching docs/23's own framing ("Carried by UnitRuntime
    /// in match-core, not the genome").
    ///
    /// Only KILL xp is implemented here. Assist xp ("half" a kill) and
    /// building-destruction xp (tier index x 30) are explicitly NOT --
    /// docs/23 never specifies the assist-tracking window (how recent a
    /// hit counts, how many assisters can share credit), and match-core
    /// has no `AttackBuilding` command/order kind yet to know WHO dealt
    /// building damage in the first place (`SimBuilding.ApplyBuildingDamage`
    /// is a generic hook, not tied to an attacker). Both are real content/
    /// design gaps, not silently dropped -- see docs/12's Phase 4 RPG
    /// entry.</summary>
    public static class UnitLeveling
    {
        public const int MaxLevel = 10;

        /// <summary>docs/23 §4: "kill = 40 XP flat + 4×victim level."</summary>
        public static int KillXp(int victimLevel) => 40 + 4 * victimLevel;

        /// <summary>docs/23 §4's v0.1 cumulative XP table -- index i is the
        /// total XP needed to be AT level i+2 (index 0 -> level 2, ...,
        /// index 8 -> level 10). The 10th entry (3300) is the table as
        /// docs/23 literally lists it (10 numbers) but is never actually
        /// reachable as a threshold -- level is capped at
        /// <see cref="MaxLevel"/> (10), so nothing after index 8 ever
        /// triggers a level-up; kept here rather than silently dropped so
        /// the constant matches the source doc exactly.</summary>
        private static readonly int[] CumulativeThresholds = { 60, 150, 280, 460, 700, 1000, 1400, 1900, 2500, 3300 };

        /// <summary>The level a unit with `totalXp` (career-cumulative, never
        /// decreasing) has reached, capped at <see cref="MaxLevel"/>.</summary>
        public static int LevelForXp(int totalXp)
        {
            var level = 1;
            for (var i = 0; i < CumulativeThresholds.Length && level < MaxLevel; i++)
            {
                if (totalXp < CumulativeThresholds[i]) break;
                level++;
            }
            return level;
        }

        /// <summary>docs/23 §4: "Per level: +8% MaxHP, +4% damage, +2%
        /// speed (multiplicative with genome stats, never replacing
        /// them)." Read as LINEAR per-level scaling (each level adds
        /// another flat +8%/+4%/+2%, not compounding) -- the doc's own
        /// phrasing ("+8% MaxHP" per level, not "x1.08 per level") reads
        /// as additive percentage points, and linear avoids runaway
        /// compounding by level 10 (which would be 1.08^9 ≈ 2x under
        /// compounding vs. a much gentler 1.72x linear) -- a documented
        /// interpretation choice, not decided definitively by the
        /// source doc.</summary>
        public static double StatMultiplier(int level, double perLevelBonus) => 1.0 + perLevelBonus * (level - 1);

        public const double MaxVitalityPerLevel = 0.08;
        public const double PowerPerLevel = 0.04;
        public const double SpeedPerLevel = 0.02;
    }
}
