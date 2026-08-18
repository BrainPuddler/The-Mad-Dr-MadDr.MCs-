namespace MadDr.MatchCore
{
    /// <summary>2026-08 (creator direction: "Make sure we can scale the ai
    /// intelligence for Difficulty. So that in tutorial and early levels
    /// players can get a sense of achievement... this needs to be
    /// challenging enough without being too easy").
    ///
    /// A genuinely new axis, not a renamed personality trait: docs/30 §7
    /// explicitly flagged this gap when the AI-opponent epic shipped --
    /// "`CommanderPersonality` is a flavor/style dial (Berserker vs.
    /// Turtle), not a skill dial." Personality answers "what does this
    /// commander WANT to do" (aggressive vs. cautious, greedy vs.
    /// territorial); Difficulty answers "how WELL does it execute that"
    /// (how fast it reacts, how efficiently it spends, how large an army
    /// it commits to). The two compose freely -- a Tutorial-difficulty
    /// Berserker is still recognizably reckless, just slow and thin.
    ///
    /// Ordered low-to-high on purpose (Tutorial=0) so comparisons
    /// (`difficulty &lt; AiDifficulty.Normal`) read naturally, but nothing
    /// in this codebase relies on the numeric value or a `default`
    /// fallback -- every real call site (<see cref="PlayerSetup.Ai"/>)
    /// defaults the C# parameter to <see cref="Normal"/> explicitly.</summary>
    public enum AiDifficulty
    {
        /// <summary>Deliberately beatable, not inert. The brief's own
        /// bar: a new player should still feel real resistance -- this
        /// opponent reacts, trains, and fights back -- just late, thin,
        /// and inefficiently enough that a first-timer's early
        /// mistakes aren't instantly fatal and a clean win reads as a
        /// real accomplishment rather than a formality.</summary>
        Tutorial = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        Brutal = 4,
    }

    /// <summary>One difficulty level's multipliers over the SAME decision
    /// machinery every commander already uses -- <see
    /// cref="SkirmishCommander"/>'s scoring formulas and <see
    /// cref="ProductionAdvisor"/>'s training logic are completely
    /// unchanged; only how fast they run and how much they commit scales.
    /// Deliberately narrow, matching <see cref="ArmyGenerator"/>'s own
    /// "only Aggression/Caution drive unit choice, the rest are left
    /// unused rather than faked" discipline: Difficulty touches
    /// REACTION SPEED, ECONOMIC EFFICIENCY, and ARMY SIZE (both starting
    /// and target) -- never raw combat stats (<see cref="CombatStats"/>
    /// on <see cref="UnitRosterDef"/> stays identical at every
    /// difficulty, so a Tutorial opponent's units aren't secretly
    /// weaker in a fight, just fewer and slower to arrive) and never the
    /// scoring weights themselves (so a "Reckless" personality reads the
    /// same at every difficulty, just executed better or worse).
    ///
    /// v0.1 placeholder numbers, same standing policy as every other
    /// invented tuning table in this project (<see
    /// cref="UnitRosterDef"/>'s own header) -- picked to be legibly
    /// different, not claimed balanced from real playtesting.</summary>
    public readonly struct AiDifficultyProfile
    {
        public AiDifficulty Level { get; }

        /// <summary>Multiplies the discipline-derived decision interval
        /// (<see cref="SkirmishCommander.DecisionIntervalTicks"/>/<see
        /// cref="ProductionAdvisor.DecisionIntervalTicks"/>) -- &gt;1 is
        /// SLOWER to notice and react (more ticks between decisions),
        /// &lt;1 is faster. Only floor-clamped at each class's own
        /// `MinDecisionIntervalTicks`, deliberately NOT ceiling-clamped
        /// to the old personality-only `MaxDecisionIntervalTicks` --
        /// Tutorial's multiplier needs real headroom above that ceiling
        /// to read as meaningfully slow, not get silently capped back
        /// down to what a methodical-but-Normal commander already
        /// does.</summary>
        public double ReactionMultiplier { get; }

        /// <summary>Multiplies <see cref="ProductionAdvisor"/>'s
        /// per-decision wallet-commit fraction (Greed's own 0.2-0.8
        /// range) -- how much of the current wallet a single training
        /// decision spends. Clamped to [0,1] after multiplying (spending
        /// more than the whole wallet isn't meaningful). A low value
        /// reads as a commander that hoards resources it never quite
        /// gets around to spending -- a real, legible weakness, not an
        /// invisible stat nerf.</summary>
        public double EconomyMultiplier { get; }

        /// <summary>Multiplies <see cref="ProductionAdvisor"/>'s final
        /// target standing-army size (both the SupplyCap-fraction floor
        /// AND the player-relative balance target added 2026-08) before
        /// it's clamped to the player's own SupplyCap. This is the
        /// primary "sense of achievement" lever the creator asked for:
        /// at Tutorial, the AI deliberately undershoots even a small
        /// human army; at Brutal, it overshoots.</summary>
        public double ArmySizeMultiplier { get; }

        /// <summary>Multiplies every resource line in <see
        /// cref="RuntimeCityBuilder.OpponentStartingArmyBudget"/> --
        /// controls how big a force the AI opens the match with, before
        /// any in-match training happens at all. Unity-side only
        /// (match-core has no concept of a "starting" budget beyond what
        /// <see cref="ArmyGenerator.Generate"/> is handed); kept here
        /// anyway so all four difficulty numbers live in one place
        /// rather than a second table drifting out of sync.</summary>
        public double StartingArmyMultiplier { get; }

        public string Label { get; }

        private AiDifficultyProfile(AiDifficulty level, double reaction, double economy, double armySize, double startingArmy, string label)
        {
            Level = level;
            ReactionMultiplier = reaction;
            EconomyMultiplier = economy;
            ArmySizeMultiplier = armySize;
            StartingArmyMultiplier = startingArmy;
            Label = label;
        }

        private static readonly AiDifficultyProfile[] All =
        {
            // Tutorial: notices things ~2.5x slower, spends barely half
            // its wallet per decision, and both its starting force and
            // its standing-army target land well under half of Normal --
            // still a live, reacting opponent (never truly inert, unlike
            // the pre-2026-08 "AI opponent sits inert forever" bug docs/30
            // §0 documents), just one a new player can realistically
            // out-build and out-fight.
            new AiDifficultyProfile(AiDifficulty.Tutorial, reaction: 2.5, economy: 0.5, armySize: 0.55, startingArmy: 0.4, label: "Tutorial"),
            new AiDifficultyProfile(AiDifficulty.Easy, reaction: 1.6, economy: 0.7, armySize: 0.75, startingArmy: 0.65, label: "Easy"),
            // Normal is the identity -- every multiplier at 1.0, so a
            // difficulty-unaware call site (every existing test/scene,
            // via PlayerSetup.Ai's own default parameter) behaves exactly
            // as it did before this enum existed.
            new AiDifficultyProfile(AiDifficulty.Normal, reaction: 1.0, economy: 1.0, armySize: 1.0, startingArmy: 1.0, label: "Normal"),
            new AiDifficultyProfile(AiDifficulty.Hard, reaction: 0.7, economy: 1.25, armySize: 1.2, startingArmy: 1.25, label: "Hard"),
            new AiDifficultyProfile(AiDifficulty.Brutal, reaction: 0.45, economy: 1.5, armySize: 1.45, startingArmy: 1.6, label: "Brutal"),
        };

        public static AiDifficultyProfile Get(AiDifficulty level) => All[(int)level];

        public static System.Collections.Generic.IReadOnlyList<AiDifficultyProfile> AllLevels => All;
    }
}
