using UnityEngine;

namespace LongShadow.World.Look.Practicals
{
    /// <summary>
    /// Deterministic light flicker curves (pure functions of time and seed, allocation-free): fire, fluorescent tubes,
    /// faulty bulbs (the cinema EXIT sign), magnesium flares and sodium strike sputter. Output is a brightness factor;
    /// 1 = nominal.
    /// </summary>
    public static class FlickerNoise
    {
        /// <summary>Open fire (burning technical, oil drums): 0.55..1.15, several octaves, no visible period.</summary>
        public static float Fire(float t, float seed)
        {
            var n = 0.55f * Noise(t * 2.1f, seed)
                    + 0.30f * Noise(t * 5.3f, seed + 13.1f)
                    + 0.15f * Noise(t * 11.7f, seed + 27.9f);
            return 0.55f + 0.6f * n;
        }

        /// <summary>
        /// Fluorescent tube: steady around 0.95–1.0 with rare short dips (a failing starter): roughly one dip every 4–8 s
        /// lasting 60–180 ms down to 0.35–0.75. Output 0..1.
        /// </summary>
        public static float Fluorescent(float t, float seed)
        {
            var steady = 0.97f + 0.03f * Noise(t * 9f, seed);
            const float slot = 6f;
            var k = Mathf.Floor(t / slot);
            var h = Hash(k, seed);
            var start = k * slot + h * (slot - 0.3f);
            var len = 0.06f + 0.12f * Hash(k + 0.5f, seed);
            if (t >= start && t <= start + len && h > 0.25f)
            {
                var depth = 0.35f + 0.4f * Hash(k + 0.25f, seed);
                var x = (t - start) / len;
                var chatter = Noise(t * 60f, seed) > 0.5f ? 1f : 0.6f;
                return Mathf.Lerp(steady, depth, Mathf.Sin(x * Mathf.PI)) * chatter;
            }
            return steady;
        }

        /// <summary>
        /// Failing bulb / EXIT sign: mostly on, with bursts of off-blinks (each burst 0.3–1.2 s of 30–90 ms gaps).
        /// Output 0 or 1 (with a short decay).
        /// </summary>
        public static float Faulty(float t, float seed)
        {
            const float slot = 3.5f;
            var k = Mathf.Floor(t / slot);
            var h = Hash(k, seed);
            if (h < 0.45f) return 1f;
            var start = k * slot + Hash(k + 0.3f, seed) * 2f;
            var len = 0.3f + 0.9f * Hash(k + 0.6f, seed);
            if (t < start || t > start + len) return 1f;
            return Noise(t * 22f, seed + 5f) > 0.55f ? 0.08f : 1f;
        }

        /// <summary>Magnesium parachute flare: bright with fast sputter, 0.82..1.06.</summary>
        public static float Flare(float t, float seed)
        {
            var n = 0.6f * Noise(t * 7f, seed) + 0.4f * Noise(t * 19f, seed + 3.3f);
            return 0.82f + 0.24f * n;
        }

        /// <summary>Arc-strike sputter for the first fraction of a second of a sodium or flood lamp: 0..1 blinks.</summary>
        public static float StrikeSputter(float sinceStrike, float seed)
        {
            if (sinceStrike < 0f) return 0f;
            if (sinceStrike > 0.45f) return 1f;
            return Noise(sinceStrike * 40f, seed) > 0.4f ? 1f : 0.15f;
        }

        /// <summary>Smooth 1D value noise in 0..1.</summary>
        public static float Noise(float x, float seed)
        {
            var i = Mathf.Floor(x);
            var f = x - i;
            var u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, seed), Hash(i + 1f, seed), u);
        }

        /// <summary>Hash of (x, seed) to 0..1.</summary>
        public static float Hash(float x, float seed)
        {
            var s = Mathf.Sin(x * 127.1f + seed * 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
