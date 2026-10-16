using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Camera operator motion (CINEMATIC_DIRECTION §1.1): Perlin "breathing" sway of 0.15-0.35° at 0.6-1.2 Hz on every
    /// non-locked camera, and decaying impulse shakes (0.5-1.5°, fast decay) for impacts, the rotor and lurches. Pure
    /// functions of time, so scrubbing a timeline gives the same picture every time.
    /// </summary>
    public static class HandheldNoise
    {
        /// <summary>Default booth POV breathing sway in degrees.</summary>
        public const float BreathingDeg = 0.18f;
        /// <summary>Default breathing frequency in Hz.</summary>
        public const float BreathingHz = 0.25f;

        /// <summary>
        /// Sway as Euler degrees (pitch, yaw, roll) at time <paramref name="t"/> seconds. Roll is half the amplitude;
        /// <paramref name="seed"/> decorrelates cameras.
        /// </summary>
        public static Vector3 Sway(float t, float amplitudeDeg, float frequencyHz, float seed)
        {
            if (amplitudeDeg <= 0f || frequencyHz <= 0f) return Vector3.zero;
            var x = t * frequencyHz;
            var p = (Mathf.PerlinNoise(x, seed) - 0.5f) * 2f;
            var y = (Mathf.PerlinNoise(seed + 17.3f, x) - 0.5f) * 2f;
            var r = (Mathf.PerlinNoise(x * 0.7f, seed + 41.9f) - 0.5f) * 2f;
            return new Vector3(p * amplitudeDeg, y * amplitudeDeg, r * amplitudeDeg * 0.5f);
        }

        /// <summary>
        /// Decaying impulse shake as Euler degrees at <paramref name="t"/> seconds after the impulse (0 before it).
        /// <paramref name="decay"/> is the exponential rate per second (8 = gone in about 0.5 s).
        /// </summary>
        public static Vector3 Impulse(float t, float amplitudeDeg, float decay = 8f, float frequencyHz = 11f, float seed = 0f)
        {
            if (t < 0f || amplitudeDeg <= 0f) return Vector3.zero;
            var env = amplitudeDeg * Mathf.Exp(-decay * t);
            if (env < 0.001f) return Vector3.zero;
            var w = 2f * Mathf.PI * frequencyHz * t;
            return new Vector3(Mathf.Sin(w + seed) * env, Mathf.Sin(w * 0.83f + 1.7f + seed) * env * 0.7f,
                               Mathf.Sin(w * 1.21f + 3.1f + seed) * env * 0.5f);
        }
    }
}
