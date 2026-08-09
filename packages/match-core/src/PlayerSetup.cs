using System;

namespace MadDr.MatchCore
{
    /// <summary>docs/30 (selectable races + AI opponents): one player
    /// slot's setup -- which faction, and whether it's driven by
    /// <see cref="AiMatchDriver"/> or external (human) input. Mirrors
    /// <see cref="CommanderPersonality"/>'s own "DATA, never simulation
    /// state" framing: constructing a match still ultimately produces a
    /// flat <see cref="FactionId"/> list plus per-player AI wiring on
    /// <see cref="PlayerState"/> -- this struct exists purely so
    /// <see cref="MatchState.Create(uint,System.Collections.Generic.IReadOnlyList{PlayerSetup},CityModel)"/>
    /// has one typed argument per player instead of three parallel lists.</summary>
    public readonly struct PlayerSetup
    {
        public readonly FactionId Faction;
        public readonly bool IsAiControlled;
        public readonly CommanderPersonality? Personality;

        public PlayerSetup(FactionId faction, bool isAiControlled = false, CommanderPersonality? personality = null)
        {
            if (isAiControlled && personality == null)
                throw new ArgumentException("an AI-controlled slot needs a CommanderPersonality", nameof(personality));
            Faction = faction;
            IsAiControlled = isAiControlled;
            Personality = personality;
        }

        /// <summary>A human-controlled slot -- no personality, since
        /// nothing ever reads one for a non-AI player.</summary>
        public static PlayerSetup Human(FactionId faction) => new PlayerSetup(faction, false, null);

        /// <summary>An AI-controlled slot. `personality` is REQUIRED (not
        /// defaulted to <see cref="CommanderPersonality.Balanced"/> here)
        /// so a caller can never silently field a bland opponent by
        /// forgetting to pick one -- <see cref="AiMatchDriver"/> itself
        /// still falls back to Balanced defensively, but the setup-time API
        /// makes the omission a compile-time-visible choice instead.</summary>
        public static PlayerSetup Ai(FactionId faction, CommanderPersonality personality) =>
            new PlayerSetup(faction, true, personality);
    }
}
