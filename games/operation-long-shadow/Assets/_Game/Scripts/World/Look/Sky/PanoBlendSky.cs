using LongShadow.World.Look.Common;
using LongShadow.World.Look.Stage;
using UnityEngine;

namespace LongShadow.World.Look.Sky
{
    /// <summary>
    /// Writes the LightStage sky state into a <c>Sky/PanoBlend</c> material (GDD §9.3): texture A/B blend, tint, exposure
    /// in EV, the per-texture base exposures and the horizon haze. Rotation stays 0 because the shipped LDR skies are
    /// pre-rotated so the sun/glow sits at azimuth 258° (WP10_CC0 <c>hdri_prep.py</c>).
    /// </summary>
    public static class PanoBlendSky
    {
        /// <summary>Shader name of the runtime sky.</summary>
        public const string ShaderName = "Sky/PanoBlend";

        /// <summary>Applies the stage state to the material.</summary>
        public static void Apply(Material sky, in LightStageSample s, LightStageTuning tuning)
        {
            if (sky == null) return;
            sky.SetFloat(LookShaderIds.SkyBlend, Mathf.Clamp01(s.SkyBlend));
            sky.SetColor(LookShaderIds.SkyTint, s.SkyTint);
            sky.SetFloat(LookShaderIds.SkyExposure, s.SkyEv);
            if (tuning == null) return;
            sky.SetFloat(LookShaderIds.SkyExposureA, tuning.SkyExposureA);
            sky.SetFloat(LookShaderIds.SkyExposureB, tuning.SkyExposureB);
            sky.SetFloat(LookShaderIds.SkyGlowBoost, tuning.SkyGlowBoost);
            sky.SetFloat(LookShaderIds.SkyHorizonFog, tuning.HorizonFog);
            sky.SetFloat(LookShaderIds.SkyHorizonFogHeight, tuning.HorizonFogHeight);
        }

        /// <summary>
        /// Linear sky radiance multiplier of one texture at this sample: base exposure x tint luminance x 2^EV. Used to
        /// rescale the captured ambient probes so ambient and visible sky stay consistent.
        /// </summary>
        public static float LinearExposure(in LightStageSample s, LightStageTuning tuning, bool textureB)
        {
            var baseExp = tuning != null ? (textureB ? tuning.SkyExposureB : tuning.SkyExposureA) : 1f;
            var tint = s.SkyTint.linear;
            var lum = 0.2126f * tint.r + 0.7152f * tint.g + 0.0722f * tint.b;
            return baseExp * lum * Mathf.Pow(2f, s.SkyEv);
        }

        /// <summary>Linear sky radiance multiplier of the blended sky (A → B by <c>_Blend</c>).</summary>
        public static float LinearExposureBlended(in LightStageSample s, LightStageTuning tuning)
        {
            var a = LinearExposure(s, tuning, false);
            var b = LinearExposure(s, tuning, true);
            return a + (b - a) * Mathf.Clamp01(s.SkyBlend);
        }
    }
}
