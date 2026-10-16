using System;
using System.Collections.Generic;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Random variant choice without audible repetition (footstep slices "round-robin from 12; avoid repeating the last
    /// two", WP06_SFX import notes): with 2-3 variants the last one is never repeated, with 4 or more neither of the last
    /// two. State is kept per key (cue id).
    /// </summary>
    public sealed class VariantPicker
    {
        private struct Recent
        {
            public int Last;
            public int BeforeLast;
        }

        private readonly Dictionary<string, Recent> _recent = new Dictionary<string, Recent>(256, StringComparer.Ordinal);
        private readonly Random _random;

        /// <summary>Creates a picker (seed 0 = time-based).</summary>
        public VariantPicker(int seed = 0)
        {
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        /// <summary>A uniform random float in [0, 1).</summary>
        public float Value() => (float)_random.NextDouble();

        /// <summary>Picks an index in [0, <paramref name="count"/>) for <paramref name="key"/>.</summary>
        public int Next(string key, int count)
        {
            if (count <= 1) return 0;
            if (key == null) return _random.Next(count);
            if (!_recent.TryGetValue(key, out var r)) r = new Recent { Last = -1, BeforeLast = -1 };
            var avoid2 = count >= 4;
            int pick;
            var guard = 0;
            do
            {
                pick = _random.Next(count);
                guard++;
            } while (guard < 32 && (pick == r.Last || (avoid2 && pick == r.BeforeLast)));
            if (pick == r.Last || (avoid2 && pick == r.BeforeLast))
            {
                // Deterministic fallback (guard exhausted): first index that is neither.
                for (var i = 0; i < count; i++)
                {
                    if (i == r.Last || (avoid2 && i == r.BeforeLast)) continue;
                    pick = i;
                    break;
                }
            }
            _recent[key] = new Recent { Last = pick, BeforeLast = r.Last };
            return pick;
        }

        /// <summary>Forgets every key.</summary>
        public void Clear() => _recent.Clear();
    }
}
