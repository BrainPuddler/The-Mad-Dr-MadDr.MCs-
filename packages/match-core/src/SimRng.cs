using System;

namespace MadDr.MatchCore
{
    /// <summary>
    /// The match simulation's deterministic RNG. Bit-identical transition
    /// to citygen-core's <c>Rng</c> (sfc32 seeded via splitmix32) so it
    /// shares the repo's proven-deterministic stream -- but exposes the
    /// RAW uint32 draw (<see cref="NextUInt"/>) instead of a double.
    ///
    /// This matters for docs/23's determinism constitution (§0 float
    /// discipline): the simulation draws INTEGERS and does integer/
    /// fixed-point math, so RNG is never a source of cross-platform float
    /// divergence. The double convenience of citygen's Rng is deliberately
    /// absent here -- sim code that needs a fraction takes it as a
    /// fixed-point ratio of NextUInt, never an IEEE double it then does
    /// transcendental math on.
    ///
    /// The full 128-bit state (<see cref="StateA"/>..<see cref="StateD"/>)
    /// is part of MatchState and is hashed/serialized every tick, so two
    /// clients that have drawn the same number of values are provably in
    /// the same RNG position.
    /// </summary>
    public sealed class SimRng
    {
        private uint _a;
        private uint _b;
        private uint _c;
        private uint _d;

        public SimRng(uint seed)
        {
            // splitmix32 to fill sfc32 state from one 32-bit seed --
            // identical to citygen-core Rng so the streams match.
            var s = seed;
            uint Next()
            {
                s = s + 0x9e3779b9u;
                var z = s;
                z = (z ^ (z >> 16)) * 0x21f0aaadu;
                z = (z ^ (z >> 15)) * 0x735a2d97u;
                return z ^ (z >> 15);
            }
            _a = Next();
            _b = Next();
            _c = Next();
            _d = Next();
            for (var i = 0; i < 12; i++) NextUInt(); // warm up
        }

        /// <summary>Restore an RNG from a serialized 128-bit state (for
        /// MatchState deserialize / a reconnecting client resuming an
        /// exact position without replaying draws).</summary>
        public SimRng(uint a, uint b, uint c, uint d)
        {
            _a = a; _b = b; _c = c; _d = d;
        }

        public uint StateA => _a;
        public uint StateB => _b;
        public uint StateC => _c;
        public uint StateD => _d;

        /// <summary>The next 32-bit draw. sfc32's exact integer
        /// transition -- the same `t` citygen's Rng divides by 2^32, here
        /// returned raw.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                var t = _a + _b + _d;
                _d = _d + 1;
                _a = _b ^ (_b >> 9);
                _b = _c + (_c << 3);
                _c = (_c << 21) | (_c >> 11);
                _c = _c + t;
                return t;
            }
        }

        /// <summary>Uniform integer in [0, n). Rejection-free
        /// multiply-shift (Lemire) -- deterministic and unbiased enough
        /// for gameplay rolls, all integer math.</summary>
        public int IntRange(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
            var product = (ulong)NextUInt() * (ulong)(uint)n;
            return (int)(product >> 32);
        }
    }
}
