using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>Evaluates <see cref="ShotEase"/> curves.</summary>
    public static class Easing
    {
        /// <summary>Eased 0..1 for a linear 0..1 input (clamped).</summary>
        public static float Evaluate(ShotEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case ShotEase.Smooth: return t * t * (3f - 2f * t);
                case ShotEase.EaseIn: return t * t;
                case ShotEase.EaseOut: return 1f - (1f - t) * (1f - t);
                case ShotEase.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                default: return t;
            }
        }
    }
}
