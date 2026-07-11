using System;

namespace MadDr.CityGen
{
    /// <summary>
    /// Deterministic seeded RNG -- a bit-exact C# port of genome-core's
    /// canonical sfc32 (seeded via splitmix32),
    /// packages/genome-core/src/rng.ts. Same seed means same output
    /// sequence, in every language this repo uses -- docs/18's
    /// determinism contract ("city generation is a pure function of
    /// (seed, preset, size)... both clients generate an identical city
    /// from the seed alone") depends on this actually being true, not
    /// just "deterministic on its own." Verified against golden values
    /// captured straight from a running node process against the real
    /// rng.ts -- see RngTests.cs; do not hand-tune anything below without
    /// re-checking against that reference.
    ///
    /// System.Random is never used anywhere in this package, the same
    /// rule rng.ts states for Math.random.
    /// </summary>
    public sealed class Rng
    {
        private uint _a;
        private uint _b;
        private uint _c;
        private uint _d;
        private double? _spareGauss;

        public Rng(uint seed)
        {
            // splitmix32 stream to fill sfc32's state from one 32-bit seed
            var s = seed;
            Func<uint> next = () =>
            {
                s = s + 0x9e3779b9u;
                var z = s;
                z = (z ^ (z >> 16)) * 0x21f0aaadu;
                z = (z ^ (z >> 15)) * 0x735a2d97u;
                return z ^ (z >> 15);
            };
            _a = next();
            _b = next();
            _c = next();
            _d = next();
            for (var i = 0; i < 12; i++) Next(); // warm up
        }

        /// <summary>Convenience overload for a signed seed (e.g. a hash
        /// result already carried as int) -- reinterprets the same 32
        /// bits, matching rng.ts's "seed &gt;&gt;&gt; 0" coercion.</summary>
        public Rng(int seed) : this(unchecked((uint)seed))
        {
        }

        /// <summary>Derive a seed from a string (e.g. a preset/size key)
        /// -- FNV-1a.</summary>
        public static uint SeedFromString(string s)
        {
            var h = 0x811c9dc5u;
            foreach (var ch in s)
            {
                h ^= ch;
                h *= 0x01000193u;
            }
            return h;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double Next()
        {
            var t = _a + _b + _d;
            _d = _d + 1;
            _a = _b ^ (_b >> 9);
            _b = _c + (_c << 3);
            _c = (_c << 21) | (_c >> 11);
            _c = _c + t;
            return t / 4294967296.0;
        }

        public double Uniform(double lo, double hi)
        {
            return lo + Next() * (hi - lo);
        }

        /// <summary>Standard normal via Box-Muller (cached spare for determinism).</summary>
        public double Gauss(double mu = 0, double sigma = 1)
        {
            if (_spareGauss.HasValue)
            {
                var z = _spareGauss.Value;
                _spareGauss = null;
                return mu + sigma * z;
            }
            double u = 0;
            while (u == 0) u = Next(); // avoid log(0)
            var v = Next();
            var r = Math.Sqrt(-2 * Math.Log(u));
            _spareGauss = r * Math.Sin(2 * Math.PI * v);
            return mu + sigma * r * Math.Cos(2 * Math.PI * v);
        }

        /// <summary>Integer in [0, n). Named IntRange, not Int -- "int" is
        /// a reserved word in C# and can't be a method name.</summary>
        public int IntRange(int n)
        {
            return (int)Math.Floor(Next() * n);
        }

        public T Choice<T>(T[] arr)
        {
            if (arr.Length == 0) throw new ArgumentException("choice from empty array");
            return arr[IntRange(arr.Length)];
        }

        public bool Bool(double p = 0.5)
        {
            return Next() < p;
        }
    }
}
