using System;

namespace MadDr.MatchCore
{
    /// <summary>
    /// Streaming FNV-1a (64-bit) hasher -- the canonical state digest for
    /// docs/23 §11's desync detection and §13-J's serialization contract.
    /// Everything is fed as raw bytes in a FIXED order; floats (when the
    /// sim eventually holds any at a hashed boundary) go in BITWISE via
    /// <see cref="AddBits"/>, never through ToString/culture-sensitive
    /// formatting. Integers are written little-endian so the digest is
    /// identical across architectures.
    /// </summary>
    public sealed class FnvHash
    {
        private const ulong Offset = 14695981039346656037ul;
        private const ulong Prime = 1099511628211ul;

        private ulong _h = Offset;

        public ulong Value => _h;

        public void Add(byte b)
        {
            unchecked
            {
                _h ^= b;
                _h *= Prime;
            }
        }

        public void Add(int v)
        {
            // little-endian, explicit -- never BitConverter.GetBytes, whose
            // byte order is host-endian and would diverge across platforms
            Add((byte)(v & 0xFF));
            Add((byte)((v >> 8) & 0xFF));
            Add((byte)((v >> 16) & 0xFF));
            Add((byte)((v >> 24) & 0xFF));
        }

        public void Add(uint v) => Add(unchecked((int)v));

        public void Add(long v)
        {
            Add(unchecked((int)(v & 0xFFFFFFFF)));
            Add(unchecked((int)((v >> 32) & 0xFFFFFFFF)));
        }

        /// <summary>Hash a float by its raw IEEE-754 bits, NEVER its
        /// decimal text (docs/23 §0 float discipline). Present so future
        /// phases that carry a hashed float boundary have the one correct
        /// path to use.</summary>
        public void AddBits(float f) => Add(BitConverter.SingleToInt32Bits(f));

        /// <summary>Hash a double by its raw bits, same rule.</summary>
        public void AddBits(double d) => Add(BitConverter.DoubleToInt64Bits(d));
    }
}
