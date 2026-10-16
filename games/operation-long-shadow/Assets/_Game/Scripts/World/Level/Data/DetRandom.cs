using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Deterministic random numbers for the level generators (PCG32, O'Neill 2014). The same seed gives the same
    /// sequence on every platform and Unity version, which <c>System.Random</c> and <c>UnityEngine.Random</c> do not
    /// promise; every generated mesh, scatter and variation is therefore reproducible by re-running the builders.
    /// </summary>
    public sealed class DetRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <summary>Creates a generator from a seed and an optional stream id (different streams never overlap).</summary>
        public DetRandom(ulong seed, ulong stream = 1442695040888963407UL)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>Creates a generator seeded from a text key (FNV-1a 64 of the key).</summary>
        public DetRandom(string key, ulong stream = 1442695040888963407UL) : this(Hash(key), stream)
        {
        }

        /// <summary>FNV-1a 64-bit hash of a string (stable seeds from ids).</summary>
        public static ulong Hash(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var h = offset;
            if (string.IsNullOrEmpty(text)) return h;
            for (var i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= prime;
            }
            return h;
        }

        /// <summary>Combines a seed with an integer (child generators per element).</summary>
        public static ulong Mix(ulong seed, int value)
        {
            var z = seed + 0x9E3779B97F4A7C15UL * (ulong)(value + 1);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Next 32-bit unsigned value.</summary>
        public uint NextUInt()
        {
            var old = _state;
            _state = unchecked(old * Multiplier + _increment);
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value01() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value01();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        /// <summary>True with probability <paramref name="p"/>.</summary>
        public bool Chance(float p) => Value01() < p;

        /// <summary>Symmetric jitter in [-amount, amount).</summary>
        public float Jitter(float amount) => Range(-amount, amount);

        /// <summary>A random element (default when the list is empty).</summary>
        public T Pick<T>(IReadOnlyList<T> items) => items == null || items.Count == 0 ? default : items[Range(0, items.Count)];

        /// <summary>Random point in a unit disc scaled by <paramref name="radius"/> (XZ plane).</summary>
        public Vector3 InDisc(float radius)
        {
            var a = Range(0f, Mathf.PI * 2f);
            var r = Mathf.Sqrt(Value01()) * radius;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>Gaussian-ish value (sum of three uniforms), mean 0, roughly unit spread.</summary>
        public float Gauss() => (Value01() + Value01() + Value01() - 1.5f) * 2f;

        /// <summary>A child generator (independent, reproducible) for element <paramref name="index"/>.</summary>
        public DetRandom Child(int index) => new DetRandom(Mix(NextUInt(), index));
    }
}
