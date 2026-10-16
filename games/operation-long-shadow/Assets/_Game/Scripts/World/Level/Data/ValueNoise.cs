using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Deterministic 2D value noise and fBm for terrain and scatter (integer hashing, no tables, no Unity Random): the
    /// same inputs give the same heights on every machine, so the terrain is reproducible from code.
    /// </summary>
    public static class ValueNoise
    {
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Value noise in [0, 1] with a quintic fade.</summary>
        public static float Noise(float x, float y, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var tx = Smooth(x - xi);
            var ty = Smooth(y - yi);
            var a = Hash(xi, yi, seed);
            var b = Hash(xi + 1, yi, seed);
            var c = Hash(xi, yi + 1, seed);
            var d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Signed value noise in [-1, 1].</summary>
        public static float Signed(float x, float y, int seed) => Noise(x, y, seed) * 2f - 1f;

        /// <summary>Fractal sum of signed noise (octaves halve in amplitude, double in frequency); roughly in [-1, 1].</summary>
        public static float Fbm(float x, float y, int seed, int octaves = 4, float lacunarity = 2.03f, float gain = 0.5f)
        {
            var sum = 0f;
            var amp = 1f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Signed(x, y, seed + i * 131) * amp;
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ridged fBm in [0, 1] (sharp crests: dunes, rock strata).</summary>
        public static float Ridged(float x, float y, int seed, int octaves = 3)
        {
            var sum = 0f;
            var amp = 0.6f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                var n = 1f - Mathf.Abs(Signed(x, y, seed + i * 977));
                sum += n * n * amp;
                norm += amp;
                amp *= 0.5f;
                x *= 2.1f;
                y *= 2.1f;
            }
            return norm > 0f ? sum / norm : 0f;
        }
    }
}
