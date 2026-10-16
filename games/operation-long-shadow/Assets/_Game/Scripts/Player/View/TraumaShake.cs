using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// Trauma-based camera shake (<c>IPlayerRig.Shake</c>): trauma 0..1 decays linearly; the shake is trauma² ×
    /// Perlin noise per axis, so small hits barely move the view and big blasts rattle it. Deterministic and
    /// allocation-free. Mutable struct: keep it in a field.
    /// </summary>
    public struct TraumaShake
    {
        /// <summary>Current trauma 0..1.</summary>
        public float Trauma;
        private float _time;

        /// <summary>Shake intensity (trauma²).</summary>
        public float Intensity => Trauma * Trauma;

        /// <summary>Adds trauma (clamped to 1).</summary>
        public void Add(float amount) => Trauma = Mathf.Clamp01(Trauma + Mathf.Max(0f, amount));

        /// <summary>Advances time and decays trauma.</summary>
        public void Tick(float dt, float decayPerSecond)
        {
            if (dt <= 0f) return;
            _time += dt;
            Trauma = Mathf.Max(0f, Trauma - decayPerSecond * dt);
        }

        /// <summary>Rotation offsets in degrees (pitch, yaw, roll).</summary>
        public Vector3 Angles(Vector3 maxDegrees, float frequency)
        {
            var s = Intensity;
            if (s <= 0f) return Vector3.zero;
            var t = _time * frequency;
            return new Vector3(maxDegrees.x * s * Noise(t, 0.13f), maxDegrees.y * s * Noise(t, 17.31f), maxDegrees.z * s * Noise(t, 41.77f));
        }

        /// <summary>Position offsets in metres.</summary>
        public Vector3 Offset(Vector3 maxMetres, float frequency)
        {
            var s = Intensity;
            if (s <= 0f) return Vector3.zero;
            var t = _time * frequency;
            return new Vector3(maxMetres.x * s * Noise(t, 63.1f), maxMetres.y * s * Noise(t, 87.9f), maxMetres.z * s * Noise(t, 101.3f));
        }

        /// <summary>Signed Perlin noise in [-1, 1].</summary>
        public static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }
}
