namespace MadDr.MatchCore
{
    /// <summary>docs/23 §7's own faction x Lumen-phase modifier table,
    /// reused VERBATIM -- unlike docs/23 §6b's invented roster stat
    /// placeholders, these six numbers are real, first-party figures
    /// authored directly in the design doc (docs/23 §7's table), not a
    /// guess this package had to fill in.
    ///
    /// | Window | Doctor | Army | Hive |
    /// | Day | -10% regen | +15% weapon damage | -10% speed |
    /// | Dusk/Dawn | -- | -- | +15% Ichor income |
    /// | Night | +15% regen, +10% speed | -15% vision radius | -- |
    ///
    /// Three of these six cells are wired into real sim behavior this
    /// phase (Army's Day damage bonus feeds <see cref="CombatMath.
    /// ResolveDamage"/>'s new lumenModPercent term; Hive's Day speed
    /// penalty and Doctor's Night speed bonus feed <see cref="SimUnit.
    /// Tick"/>'s new speedMultiplier parameter; Doctor's regen swing feeds
    /// <see cref="MatchState.ApplyRegenerationQuirk"/>). The other three
    /// are carried here as DATA ONLY, deliberately not faked into a
    /// system that doesn't exist:
    ///
    /// - **Army's vision radius** needs a fog-of-war/vision system --
    ///   match-core has none at all (`BattlefieldState`'s blocked-hex set
    ///   governs PATHING, not visibility). This is purely a rendering/
    ///   minimap concern today (docs/23's own Phase 1 task list), so
    ///   there is nothing sim-side to modify yet.
    /// - **Hive's Ichor income** needs a generic per-faction resource
    ///   income tick -- docs/12's Phase 3 entry already logs this as
    ///   missing ("income ticks... gated on prerequisites that don't
    ///   exist yet"), unchanged since. Only emitter Mana income
    ///   (Phase 3.5) exists today; ordinary Blood/Fuel/Ichor/Bones income
    ///   does not.
    ///
    /// Both are stored here so the REAL number is on record and ready the
    /// moment their prerequisite system lands, rather than re-discovering
    /// "what does docs/23 actually say" a second time.</summary>
    public readonly struct FactionLumenModifier
    {
        /// <summary>Integer percent (100 = no change), same discipline as
        /// every other multiplier <see cref="CombatMath.ResolveDamage"/>
        /// accepts -- docs/04's stricter-than-usual "no double in the
        /// actual damage computation" bar.</summary>
        public readonly int DamagePercent;

        /// <summary>Multiplies <see cref="SimUnit.EffectiveSpeed"/> at the
        /// point <see cref="SimUnit.Tick"/> consumes it -- movement math
        /// elsewhere in this project already uses IEEE doubles (docs/23
        /// §0's allow-list), so this one stays a plain multiplier rather
        /// than an integer percent.</summary>
        public readonly double SpeedMultiplier;

        /// <summary>Multiplies the `regeneration` quirk's own real rate
        /// (docs/06: "1% max HP/s out of combat") -- see
        /// <see cref="MatchState.ApplyRegenerationQuirk"/>.</summary>
        public readonly double RegenMultiplier;

        /// <summary>DATA ONLY -- see this struct's own header. No vision/
        /// fog-of-war system exists in match-core to apply this
        /// to.</summary>
        public readonly double VisionMultiplier;

        /// <summary>DATA ONLY -- see this struct's own header. No generic
        /// per-faction resource income tick exists in match-core to apply
        /// this to.</summary>
        public readonly double IncomeMultiplier;

        public FactionLumenModifier(int damagePercent, double speedMultiplier,
            double regenMultiplier, double visionMultiplier, double incomeMultiplier)
        {
            DamagePercent = damagePercent;
            SpeedMultiplier = speedMultiplier;
            RegenMultiplier = regenMultiplier;
            VisionMultiplier = visionMultiplier;
            IncomeMultiplier = incomeMultiplier;
        }

        /// <summary>The "no effect this phase" default -- every cell docs/23
        /// §7 leaves blank ("--") uses this rather than a hand-copied
        /// all-1.0 literal at each call site.</summary>
        public static readonly FactionLumenModifier None = new FactionLumenModifier(100, 1.0, 1.0, 1.0, 1.0);
    }

    /// <summary>Static lookup table, one row per (docs/23 §7's own faction
    /// x phase table). DATA read by the sim, never simulation state (same
    /// convention as <see cref="FactionDef"/>/<see cref="BuildingDef"/>/
    /// <see cref="UnitRosterDef"/>) -- not part of the tick hash.
    ///
    /// <see cref="FactionId.Mixed"/>'s own row is left at
    /// <see cref="FactionLumenModifier.None"/> for every phase --
    /// deliberately, not an oversight. Mixed has no faction-level bonus of
    /// its own; <see cref="MatchState"/>'s Lumen lookups resolve each
    /// UNIT's effective faction (<see cref="SimUnit.RaceOverride"/> if
    /// set, else its owner's) before indexing this table, so a Mixed
    /// player's units already get their own race's real row. This row
    /// only matters as a fallback for a hypothetical unit with no race
    /// override under a Mixed owner, and staying neutral there is the
    /// "no stacked advantage" guarantee made explicit, not just implied by
    /// call-site behavior.</summary>
    public static class FactionLumenTable
    {
        // Indexed [FactionId, LumenPhase] -- 4 factions x 4 phases.
        private static readonly FactionLumenModifier[,] All = BuildTable();

        private static FactionLumenModifier[,] BuildTable()
        {
            var t = new FactionLumenModifier[4, 4];
            for (var f = 0; f < 4; f++)
                for (var p = 0; p < 4; p++)
                    t[f, p] = FactionLumenModifier.None;

            // Doctor: Day -10% regen; Night +15% regen, +10% speed.
            t[(int)FactionId.MadDoctor, (int)LumenPhase.Day] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 1.0, regenMultiplier: 0.90, visionMultiplier: 1.0, incomeMultiplier: 1.0);
            t[(int)FactionId.MadDoctor, (int)LumenPhase.Night] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 1.10, regenMultiplier: 1.15, visionMultiplier: 1.0, incomeMultiplier: 1.0);

            // Army: Day +15% weapon damage; Night -15% vision radius (data only).
            t[(int)FactionId.HumanArmy, (int)LumenPhase.Day] =
                new FactionLumenModifier(damagePercent: 115, speedMultiplier: 1.0, regenMultiplier: 1.0, visionMultiplier: 1.0, incomeMultiplier: 1.0);
            t[(int)FactionId.HumanArmy, (int)LumenPhase.Night] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 1.0, regenMultiplier: 1.0, visionMultiplier: 0.85, incomeMultiplier: 1.0);

            // Hive: Day -10% speed; Dusk/Dawn +15% Ichor income (data only).
            t[(int)FactionId.AlienHive, (int)LumenPhase.Day] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 0.90, regenMultiplier: 1.0, visionMultiplier: 1.0, incomeMultiplier: 1.0);
            t[(int)FactionId.AlienHive, (int)LumenPhase.Dusk] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 1.0, regenMultiplier: 1.0, visionMultiplier: 1.0, incomeMultiplier: 1.15);
            t[(int)FactionId.AlienHive, (int)LumenPhase.Dawn] =
                new FactionLumenModifier(damagePercent: 100, speedMultiplier: 1.0, regenMultiplier: 1.0, visionMultiplier: 1.0, incomeMultiplier: 1.15);

            return t;
        }

        public static FactionLumenModifier Get(FactionId faction, LumenPhase phase) => All[(int)faction, (int)phase];
    }
}
