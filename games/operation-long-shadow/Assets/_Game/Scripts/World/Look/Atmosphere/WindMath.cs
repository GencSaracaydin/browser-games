using UnityEngine;

namespace LongShadow.World.Look.Atmosphere
{
    /// <summary>
    /// Wind model of GDD §9.7: clear and dry, from the west at 3–5 m/s, with gusts. Pure functions of time so the
    /// controller, particles and tests agree. Compass convention: +Z north, +X east, so "from the west" blows toward +X.
    /// </summary>
    public static class WindMath
    {
        /// <summary>Calm speed (m/s).</summary>
        public const float MinSpeed = 3f;
        /// <summary>Gust speed (m/s).</summary>
        public const float MaxSpeed = 5f;
        /// <summary>Compass bearing the wind blows toward (east).</summary>
        public const float TowardBearing = 90f;
        /// <summary>Maximum wander of the direction around <see cref="TowardBearing"/>, degrees.</summary>
        public const float DirectionWander = 8f;

        /// <summary>Gust amount 0..1 at time <paramref name="t"/> (incommensurate sines: no visible period).</summary>
        public static float Gust(float t, float seed = 0f)
        {
            var x = t + seed * 17.13f;
            var g = 0.50f * Mathf.Sin(x * 0.173f)
                    + 0.30f * Mathf.Sin(x * 0.411f + 1.7f)
                    + 0.20f * Mathf.Sin(x * 1.093f + 4.1f);
            // g in [-1, 1]; bias toward calm with short gusts.
            var u = Mathf.Clamp01(0.5f + 0.5f * g);
            return u * u * (3f - 2f * u);
        }

        /// <summary>Wind speed in m/s at <paramref name="t"/>.</summary>
        public static float Speed(float t, float seed = 0f) => Mathf.Lerp(MinSpeed, MaxSpeed, Gust(t, seed));

        /// <summary>Compass bearing the wind blows toward at <paramref name="t"/>.</summary>
        public static float Bearing(float t, float seed = 0f) =>
            TowardBearing + DirectionWander * (0.7f * Mathf.Sin((t + seed) * 0.071f) + 0.3f * Mathf.Sin((t + seed) * 0.29f + 2.3f));

        /// <summary>Horizontal unit direction the wind blows toward.</summary>
        public static Vector3 Direction(float t, float seed = 0f)
        {
            var b = Bearing(t, seed) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b));
        }

        /// <summary>Turbulence 0..1 (how fast the gust is changing).</summary>
        public static float Turbulence(float t, float seed = 0f)
        {
            const float h = 0.25f;
            var d = Mathf.Abs(Gust(t + h, seed) - Gust(t - h, seed)) / (2f * h);
            return Mathf.Clamp01(0.25f + d * 4f);
        }
    }
}
