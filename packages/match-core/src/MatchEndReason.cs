namespace MadDr.MatchCore
{
    /// <summary>docs/02 "Victory conditions": which of the three
    /// documented ways a match ended, or <see cref="None"/> while it's
    /// still running. Read alongside <see
    /// cref="MatchState.WinnerPlayerIndex"/> -- null there means a draw
    /// (mutual elimination on the same tick for <see
    /// cref="Elimination"/>, or a tied territory score for <see
    /// cref="TimeCap"/>; <see cref="Dominion"/> can never tie, see
    /// <see cref="MatchState.CheckMatchEnd"/>'s own doc comment for
    /// why).</summary>
    public enum MatchEndReason
    {
        None = 0,

        /// <summary>docs/02's "Vat destruction" condition, generalized for
        /// N-player FFA (docs/23 §11: 2-8 players) rather than assuming
        /// exactly two: a player is eliminated the instant their <see
        /// cref="BuildingKind.Hq"/> is destroyed, and the match ends the
        /// instant only one non-eliminated player remains (or zero, on a
        /// simultaneous mutual wipeout -- a draw). For the common 1v1
        /// case this collapses to exactly "destroy the enemy Vat, you
        /// win," matching the doc's own framing exactly.</summary>
        Elimination = 1,

        /// <summary>docs/02/docs/03: control &gt;=60% of the map's
        /// emitters CONTINUOUSLY for one full Lumen Cycle (<see
        /// cref="LumenClock.CycleTicks"/>, 4 minutes). "Continuously"
        /// means a streak that RESETS to zero the instant control drops
        /// below the threshold -- not the emitter capture channel's own
        /// "contested freezes, doesn't reset" rule (<see
        /// cref="SimEmitter.Tick"/>), a different mechanic this one is
        /// easy to conflate with.</summary>
        Dominion = 2,

        /// <summary>docs/02: at the 15-minute cap
        /// (<see cref="MatchState.TimeCapTicks"/>), the higher <see
        /// cref="MatchState.TerritoryScore"/> among still-active players
        /// wins; an exact tie is a draw.</summary>
        TimeCap = 3,
    }
}
