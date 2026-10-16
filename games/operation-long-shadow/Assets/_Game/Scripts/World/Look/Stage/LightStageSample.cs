using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// Everything the LightStage drives at one <c>s</c>: the GDD §9.2a keyframes interpolated linearly, plus the derived
    /// values (stage blend, post exposure, AI sight, HUD colour, sun disc visibility, shadow bias). Produced by
    /// <see cref="LightStageTable.Sample"/>; pure data, no Unity objects.
    /// </summary>
    public struct LightStageSample
    {
        /// <summary>Stage value 0..4.</summary>
        public float S;
        /// <summary>Named stage containing <see cref="S"/> (GDD §9.2).</summary>
        public StageId Stage;
        /// <summary>Stage volume cross-fade.</summary>
        public StageBlend Blend;

        /// <summary>Sun elevation in degrees.</summary>
        public float SunElevation;
        /// <summary>Sun intensity (0 = SUN disabled).</summary>
        public float SunIntensity;
        /// <summary>Sun colour temperature in kelvin.</summary>
        public float SunKelvin;
        /// <summary>Main-light shadow distance in metres.</summary>
        public float ShadowDistance;
        /// <summary>Main-light shadow strength 0..1.</summary>
        public float ShadowStrength;
        /// <summary>Per-light shadow depth bias (TECH_PLAN §3.4: tuned per stage for the 0.5°–1.5° sun on R4).</summary>
        public float ShadowDepthBias;
        /// <summary>Per-light shadow normal bias.</summary>
        public float ShadowNormalBias;
        /// <summary>SKY_KEY intensity (0 = disabled).</summary>
        public float SkyKeyIntensity;
        /// <summary>SKY_KEY colour temperature in kelvin.</summary>
        public float SkyKeyKelvin;
        /// <summary>Sun disc and lens flare visibility 0..1 (fades out over s 1.97–2.00, off after sunset).</summary>
        public float SunDiscVisibility;

        /// <summary>Sky/PanoBlend <c>_Blend</c>.</summary>
        public float SkyBlend;
        /// <summary>Sky/PanoBlend <c>_Tint</c> (sRGB).</summary>
        public Color SkyTint;
        /// <summary>Sky/PanoBlend <c>_Exposure</c> offset in EV.</summary>
        public float SkyEv;

        /// <summary>Fog density (exponential squared, before the tuning scale).</summary>
        public float FogDensity;
        /// <summary>Fog colour (sRGB).</summary>
        public Color FogColor;

        /// <summary>Story clock in minutes after midnight.</summary>
        public float ClockMinutes;
        /// <summary>Post exposure of the blended stage volumes in EV (0 / +0.4 / +1.0 / +1.5, GDD §9.6).</summary>
        public float StagePostExposure;
        /// <summary>Target AI sight range of the stage (70 / 60 / 45 / 35 m, GDD §14.2), before the 2 s smoothing.</summary>
        public float AISightTarget;
        /// <summary>True from s 2.0.</summary>
        public bool AfterSunset;
        /// <summary>HUD marker colour (amber before s 2.0, pale green after; GDD §9.2a, §11).</summary>
        public Color HudColor;
        /// <summary>Default reflection intensity (sky-derived reflections dim with the light).</summary>
        public float ReflectionIntensity;

        /// <summary>True while the SUN light is on.</summary>
        public bool SunEnabled => SunIntensity > LightStageTable.LightEpsilon;
        /// <summary>True while the SKY_KEY light is on.</summary>
        public bool SkyKeyEnabled => SkyKeyIntensity > LightStageTable.LightEpsilon;
    }
}
