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

        /// <summary>2026-08 (creator direction: "scale the ai intelligence
        /// for Difficulty"): the skill dial, orthogonal to <see
        /// cref="Personality"/> -- see <see cref="AiDifficulty"/>'s own
        /// header for the personality-vs-difficulty distinction. Unlike
        /// Personality, this is never null and never required: it's
        /// meaningless for a human slot (simply unread) and defaults to
        /// <see cref="AiDifficulty.Normal"/> for an AI slot that doesn't
        /// specify one, which reproduces every pre-2026-08 AI opponent's
        /// exact behavior (Normal's multipliers are all 1.0).</summary>
        public readonly AiDifficulty Difficulty;

        public PlayerSetup(FactionId faction, bool isAiControlled = false, CommanderPersonality? personality = null, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            if (isAiControlled && personality == null)
                throw new ArgumentException("an AI-controlled slot needs a CommanderPersonality", nameof(personality));
            Faction = faction;
            IsAiControlled = isAiControlled;
            Personality = personality;
            Difficulty = difficulty;
        }

        /// <summary>A human-controlled slot -- no personality, since
        /// nothing ever reads one for a non-AI player.</summary>
        public static PlayerSetup Human(FactionId faction) => new PlayerSetup(faction, false, null);

        /// <summary>An AI-controlled slot. `personality` is REQUIRED (not
        /// defaulted to <see cref="CommanderPersonality.Balanced"/> here)
        /// so a caller can never silently field a bland opponent by
        /// forgetting to pick one -- <see cref="AiMatchDriver"/> itself
        /// still falls back to Balanced defensively, but the setup-time API
        /// makes the omission a compile-time-visible choice instead.
        /// `difficulty` defaults to <see cref="AiDifficulty.Normal"/> --
        /// unlike personality, silently defaulting here is fine (Normal is
        /// a genuinely reasonable default, not a "not recommended" one the
        /// way <see cref="CommanderPersonality.Balanced"/> is for
        /// personality).</summary>
        public static PlayerSetup Ai(FactionId faction, CommanderPersonality personality, AiDifficulty difficulty = AiDifficulty.Normal) =>
            new PlayerSetup(faction, true, personality, difficulty);
    }
}
