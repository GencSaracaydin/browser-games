using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform
{
    /// <summary>
    /// Render-scale rules (TECH_PLAN D21, GDD §9.8). The automatic default is the largest of {1.0, 0.9, 0.8, 0.75} with
    /// canvas pixels x scale^2 &lt;= 2.1 MP; a value the player picked wins over it; performance mode caps it at 0.8
    /// (TECH_PLAN §3.3).
    /// </summary>
    public static class RenderScalePolicy
    {
        /// <summary>Internal-resolution budget for 60 fps (GDD §9.8: ≤ 2.1 MP).</summary>
        public const float MaxAutoPixels = 2.1e6f;

        /// <summary>Render scale in performance mode (TECH_PLAN §3.3).</summary>
        public const float PerformanceModeScale = 0.8f;

        private static readonly float[] s_steps = { 1f, 0.9f, 0.8f, 0.75f };

        /// <summary>Candidate scales for the automatic default, largest first (D21).</summary>
        public static float[] Steps => (float[])s_steps.Clone();

        /// <summary>
        /// Largest step whose internal resolution fits <paramref name="maxPixels"/>; the smallest step when none fits.
        /// A non-positive canvas size returns 1.
        /// </summary>
        public static float AutoScale(int canvasWidth, int canvasHeight, float maxPixels = MaxAutoPixels)
        {
            if (canvasWidth <= 0 || canvasHeight <= 0) return 1f;
            var px = (double)canvasWidth * canvasHeight;
            foreach (var s in s_steps)
                if (px * s * s <= maxPixels) return s;
            return s_steps[s_steps.Length - 1];
        }

        /// <summary>Scale actually applied for <paramref name="settings"/> on a canvas of the given device-pixel size.</summary>
        public static float Effective(GameSettings settings, int canvasWidth, int canvasHeight, float maxPixels = MaxAutoPixels)
        {
            if (settings == null) return AutoScale(canvasWidth, canvasHeight, maxPixels);
            var scale = settings.RenderScaleAuto
                ? AutoScale(canvasWidth, canvasHeight, maxPixels)
                : Mathf.Clamp(settings.RenderScale, GameSettings.MinRenderScale, GameSettings.MaxRenderScale);
            if (settings.PerformanceMode) scale = Mathf.Min(scale, PerformanceModeScale);
            return scale;
        }

        /// <summary>Linear gain for a brightness offset in EV (GDD §11: -0.5..+0.5 EV).</summary>
        public static float BrightnessGain(float ev)
        {
            ev = Mathf.Clamp(ev, GameSettings.MinBrightness, GameSettings.MaxBrightness);
            return Mathf.Pow(2f, ev);
        }
    }
}
