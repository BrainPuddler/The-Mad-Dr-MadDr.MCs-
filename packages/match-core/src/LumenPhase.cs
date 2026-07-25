namespace MadDr.MatchCore
{
    /// <summary>The global Lumen Cycle's four phases (docs/03). One full
    /// cycle is 240s (90 Day / 30 Dusk / 90 Night / 30 Dawn); matches
    /// always start at Dawn.</summary>
    public enum LumenPhase
    {
        Dawn,
        Day,
        Dusk,
        Night,
    }

    /// <summary>Pure, stateless Lumen Cycle math (docs/03) -- deliberately
    /// NOT a stateful clock object: the current phase is a pure function
    /// of the match's own <see cref="MatchState.Frame"/>, so there is
    /// nothing to keep in sync or drift out of sync. All durations are in
    /// TICKS (docs/23 §0 integer discipline), converted once here from
    /// docs/03's real-second durations at <see cref="MatchState.
    /// TicksPerSecond"/> -- 10 ticks/s.</summary>
    public static class LumenClock
    {
        public const int DaySeconds = 90;
        public const int DuskSeconds = 30;
        public const int NightSeconds = 90;
        public const int DawnSeconds = 30;
        public const int CycleSeconds = DaySeconds + DuskSeconds + NightSeconds + DawnSeconds;   // 240

        public const int DayTicks = DaySeconds * MatchState.TicksPerSecond;       // 900
        public const int DuskTicks = DuskSeconds * MatchState.TicksPerSecond;     // 300
        public const int NightTicks = NightSeconds * MatchState.TicksPerSecond;  // 900
        public const int DawnTicks = DawnSeconds * MatchState.TicksPerSecond;    // 300
        public const int CycleTicks = DayTicks + DuskTicks + NightTicks + DawnTicks;   // 2400

        /// <summary>The Lumen phase at a given frame -- matches always
        /// start at Dawn (docs/03: "Matches always start at Dawn
        /// (symmetric for both players)"), so frame 0 is mid-Dawn, not
        /// mid-Day. Cycle order thereafter: Dawn -> Day -> Dusk -> Night ->
        /// Dawn (docs/03's own table order is Day/Dusk/Night/Dawn, but its
        /// prose fixes the actual START point at Dawn -- this reconciles
        /// both without contradicting either).</summary>
        public static LumenPhase PhaseAt(int frame)
        {
            var t = frame % CycleTicks;
            if (t < DawnTicks) return LumenPhase.Dawn;
            t -= DawnTicks;
            if (t < DayTicks) return LumenPhase.Day;
            t -= DayTicks;
            if (t < DuskTicks) return LumenPhase.Dusk;
            return LumenPhase.Night;
        }
    }
}
