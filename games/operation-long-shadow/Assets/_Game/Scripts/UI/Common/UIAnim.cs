using UnityEngine;

namespace LongShadow.UI.Common
{
    /// <summary>Easing and approach helpers for UI animation (allocation-free; driven by unscaled time, TECH_PLAN R3).</summary>
    public static class UIAnim
    {
        /// <summary>Cubic ease-out.</summary>
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * t + 1f;
        }

        /// <summary>Cubic ease-in-out.</summary>
        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Back ease-out (small overshoot): stamps and cards.</summary>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }

        /// <summary>Moves a value towards a target at a constant rate (units per second).</summary>
        public static float Approach(float current, float target, float ratePerSecond, float dt) =>
            Mathf.MoveTowards(current, target, ratePerSecond * dt);

        /// <summary>Exponential approach, frame-rate independent (lambda per second).</summary>
        public static float Damp(float current, float target, float lambda, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));

        /// <summary>
        /// Fade value for an element that fades in over <paramref name="fadeIn"/>, holds, then fades out over
        /// <paramref name="fadeOut"/> ending at <paramref name="total"/>.
        /// </summary>
        public static float Envelope(float t, float fadeIn, float total, float fadeOut)
        {
            if (t < 0f || t > total) return 0f;
            var a = fadeIn > 0f ? Mathf.Clamp01(t / fadeIn) : 1f;
            var b = fadeOut > 0f ? Mathf.Clamp01((total - t) / fadeOut) : 1f;
            return Mathf.Min(a, b);
        }

        /// <summary>Square-wave blink (on for the first half of each period).</summary>
        public static bool Blink(float time, float period) => period <= 0f || Mathf.Repeat(time, period) < period * 0.5f;

        /// <summary>
        /// Phosphor flicker: a 0..1 brightness drop made of two incommensurate sines and a rare dip, for CRT panels.
        /// </summary>
        public static float Flicker(float time, float amount)
        {
            if (amount <= 0f) return 1f;
            var s = Mathf.Sin(time * 61.3f) * 0.5f + Mathf.Sin(time * 7.7f + 1.3f) * 0.5f;
            var dip = Mathf.PerlinNoise(time * 3.1f, 0.37f) > 0.82f ? 0.6f : 0f;
            return 1f - amount * (0.035f * (s * 0.5f + 0.5f) + 0.08f * dip);
        }
    }
}
