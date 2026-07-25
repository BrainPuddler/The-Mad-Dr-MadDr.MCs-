using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §13 amendment B (Phase 3.5): a live emitter's
    /// capture/ownership state, seeded once from <see cref="CityModel.
    /// Landmarks"/> at <see cref="MatchState.Create"/> (a pre-existing map
    /// feature, not something a player builds -- same "generator places
    /// it, match-core only runs its runtime state" relationship
    /// <see cref="SimBuilding"/>'s HQ has to the generator's landmark
    /// site). Capture is automatic (docs/03: "captured by a monster
    /// standing on the emitter hex, uncontested, for 8 seconds") -- there
    /// is no capture COMMAND; <see cref="MatchState.Tick"/> reads live
    /// unit positions every tick and drives this directly.</summary>
    public sealed class SimEmitter
    {
        /// <summary>8s capture channel (docs/03), in ticks.</summary>
        public const int CaptureChannelTicks = 8 * MatchState.TicksPerSecond;

        public HexCoord Hex { get; }
        public EmitterPolarity Polarity { get; }

        /// <summary>Current controller, or null if never captured yet.
        /// Docs/03: "Captured emitters stay owned until re-captured" --
        /// this never reverts to null once set.</summary>
        public int? Owner { get; private set; }

        private int? _capturingPlayer;
        private int _captureProgressTicks;

        /// <summary>Ticks of uncontested progress the current claimant has
        /// -- 0 if nobody is currently attempting a capture. Exposed for
        /// a future capture-progress HUD bar (docs/03: "progress bar
        /// visible to both players") and for tests.</summary>
        public int CaptureProgressTicks => _captureProgressTicks;

        public int? CapturingPlayer => _capturingPlayer;

        internal SimEmitter(HexCoord hex, EmitterPolarity polarity)
        {
            Hex = hex;
            Polarity = polarity;
        }

        /// <summary>One tick of capture logic. <paramref name="soleOccupant"/>
        /// is the single player whose unit(s) stand on this emitter's exact
        /// hex right now, or null if the hex is empty (or contested by
        /// units from 2+ players standing on the SAME hex simultaneously --
        /// no valid single claimant, treated the same as empty).
        /// <paramref name="contested"/> is true when an enemy unit is
        /// anywhere within the aura radius (docs/03: "any enemy monster
        /// within the 3-hex aura freezes progress; progress does not
        /// reset") -- only meaningful when <paramref name="soleOccupant"/>
        /// is non-null.</summary>
        internal void Tick(int? soleOccupant, bool contested)
        {
            if (soleOccupant == null || soleOccupant == Owner)
            {
                // nobody (or only the current owner) is standing here --
                // nothing to capture; abandoning a capture attempt (as
                // opposed to being CONTESTED, which freezes instead)
                // forfeits whatever progress had built up.
                _capturingPlayer = null;
                _captureProgressTicks = 0;
                return;
            }

            if (_capturingPlayer != soleOccupant)
            {
                // a different player than whoever was last attempting this
                // capture -- a fresh attempt starts from zero.
                _capturingPlayer = soleOccupant;
                _captureProgressTicks = 0;
            }

            if (contested) return;   // frozen -- NOT reset (docs/03)

            _captureProgressTicks++;
            if (_captureProgressTicks >= CaptureChannelTicks)
            {
                Owner = soleOccupant;
                _capturingPlayer = null;
                _captureProgressTicks = 0;
            }
        }

        /// <summary>Append this emitter's canonical bytes, FIXED field
        /// order (docs/23 §13-J), matching every other entity's
        /// `WriteTo` convention.</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(Hex.Q);
            h.Add(Hex.R);
            h.Add((int)Polarity);
            h.Add(Owner ?? -1);
            h.Add(_capturingPlayer ?? -1);
            h.Add(_captureProgressTicks);
        }
    }
}
