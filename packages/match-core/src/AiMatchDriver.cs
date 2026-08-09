using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/30 (selectable races + AI opponents): orchestrates
    /// every AI-controlled player's command sources
    /// (<see cref="SkirmishCommander"/> for combat, <see
    /// cref="ProductionAdvisor"/> for economy) into one call per tick. Pure
    /// match-core -- no <c>UnityEngine</c> dependency -- so it's
    /// `dotnet test`-able end to end (seed in, N ticks, assert on the
    /// resulting <see cref="MatchState"/>) with zero Unity/flightcheck
    /// involvement, and directly reusable by a future headless server or
    /// bot-fill feature.
    ///
    /// Closes a real, previously-open gap: before this class existed,
    /// <see cref="SkirmishCommander"/> was fully built and tested but never
    /// instantiated anywhere a live match actually ran -- an AI-controlled
    /// <see cref="PlayerState"/> got a faction, a base, and a one-time
    /// starting army, then sat inert forever. This is the missing per-tick
    /// caller.</summary>
    public sealed class AiMatchDriver
    {
        private readonly Dictionary<int, SkirmishCommander> _commanders = new Dictionary<int, SkirmishCommander>();
        private readonly Dictionary<int, ProductionAdvisor> _advisors = new Dictionary<int, ProductionAdvisor>();

        /// <summary>Builds one <see cref="SkirmishCommander"/> and one
        /// <see cref="ProductionAdvisor"/> per <see
        /// cref="PlayerState.IsAiControlled"/> slot in `match`, using each
        /// player's own <see cref="PlayerState.AiPersonality"/> (falling
        /// back to <see cref="CommanderPersonality.Balanced"/> only
        /// defensively -- every real caller sets one via <see
        /// cref="PlayerSetup.Ai"/>, which requires it). `seed` is folded
        /// per player index so each AI player's <see
        /// cref="ProductionAdvisor"/> draws from its own decorrelated
        /// stream (see that class's own constructor doc comment) rather
        /// than all AI players sharing one draw sequence.</summary>
        public AiMatchDriver(MatchState match, uint seed)
        {
            for (var i = 0; i < match.PlayerCount; i++)
            {
                var p = match.Player(i);
                if (!p.IsAiControlled) continue;
                var personality = p.AiPersonality ?? CommanderPersonality.Balanced();
                _commanders[i] = new SkirmishCommander(i, personality);
                _advisors[i] = new ProductionAdvisor(i, personality, unchecked(seed ^ (uint)(i * 0x9E3779B1)));
            }
        }

        /// <summary>True if this match has at least one AI-controlled
        /// player -- lets a caller skip building/collecting decisions
        /// entirely for an all-human match, same cost as today.</summary>
        public bool HasAnyAi => _commanders.Count > 0;

        /// <summary>Every AI player's commands for this tick, combined.
        /// Call BEFORE <see cref="MatchState.Tick"/> with the SAME `match`
        /// (reads <see cref="MatchState.Frame"/> as-is, matching <see
        /// cref="SkirmishCommander.Decide"/>'s and <see
        /// cref="ProductionAdvisor.DecideCommands"/>'s own pre-tick-frame
        /// contract) -- the caller is responsible for adding the result to
        /// its own pending command bundle, exactly as it would a human
        /// player's queued input.</summary>
        public IReadOnlyList<Command> DecideCommands(MatchState match)
        {
            var commands = new List<Command>();
            foreach (var commander in _commanders.Values)
                commands.AddRange(commander.DecideCommands(match));
            foreach (var advisor in _advisors.Values)
                commands.AddRange(advisor.DecideCommands(match));
            return commands;
        }
    }
}
