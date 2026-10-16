namespace LongShadow.AI.Common
{
    /// <summary>
    /// Small deterministic xorshift32 generator. Every brain owns one (seeded from its actor id) so behaviour is
    /// reproducible in tests and two enemies spawned in the same frame do not share timings. Allocation-free.
    /// Mutable struct: keep it in a field and call through the field.
    /// </summary>
    public struct AIRandom
    {
        private uint _state;

        /// <summary>Creates a generator; seed 0 is replaced by a fixed non-zero constant.</summary>
        public AIRandom(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

        /// <summary>Seed from a string (FNV-1a of the id).</summary>
        public static AIRandom FromId(string id, uint salt = 0) => new AIRandom(LongShadow.Core.MathX.Fnv1a32(id) ^ (salt * 0x85EBCA6Bu));

        /// <summary>Next raw 32-bit value.</summary>
        public uint NextUInt()
        {
            if (_state == 0) _state = 0x9E3779B9u;
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Uniform int in [min, max] (inclusive).</summary>
        public int RangeInclusive(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min + 1));
        }

        /// <summary>True with probability <paramref name="p"/>.</summary>
        public bool Chance(float p) => p >= 1f || (p > 0f && Value < p);

        /// <summary>-1 or +1.</summary>
        public int Sign() => (NextUInt() & 1u) == 0 ? -1 : 1;
    }
}
