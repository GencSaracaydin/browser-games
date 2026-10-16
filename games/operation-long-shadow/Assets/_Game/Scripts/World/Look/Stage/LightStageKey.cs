using UnityEngine;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// One row of the authoritative LightStage keyframe table (GDD §9.2a). Rows are interpolated linearly by
    /// <see cref="LightStageTable.Sample"/>. Where the GDD leaves a cell empty ("—" or implied by the row above), the row
    /// repeats the previous value so the linear interpolation holds it.
    /// </summary>
    public readonly struct LightStageKey
    {
        /// <summary>Stage value of the row (0..4).</summary>
        public readonly float S;
        /// <summary>Sun elevation in degrees above the horizon (azimuth is fixed at 258°).</summary>
        public readonly float SunElevation;
        /// <summary>Sun (directional) intensity; 0 from s 2.0 (SUN off).</summary>
        public readonly float SunIntensity;
        /// <summary>Sun colour temperature in kelvin.</summary>
        public readonly float SunKelvin;
        /// <summary>Main-light shadow distance in metres (URP asset).</summary>
        public readonly float ShadowDistance;
        /// <summary>Main-light shadow strength 0..1 (0 = off).</summary>
        public readonly float ShadowStrength;
        /// <summary>Sky texture blend A (kiara_8_sunset) → B (kiara_9_dusk), Sky/PanoBlend <c>_Blend</c>.</summary>
        public readonly float SkyBlend;
        /// <summary>Sky tint (Sky/PanoBlend <c>_Tint</c>), sRGB.</summary>
        public readonly Color SkyTint;
        /// <summary>Sky exposure offset in EV (Sky/PanoBlend <c>_Exposure</c>).</summary>
        public readonly float SkyEv;
        /// <summary>Exponential-squared fog density.</summary>
        public readonly float FogDensity;
        /// <summary>Fog colour, sRGB.</summary>
        public readonly Color FogColor;
        /// <summary>Story clock in minutes after midnight (17:58 = 1078).</summary>
        public readonly float ClockMinutes;
        /// <summary>SKY_KEY directional intensity (shadowless, az 258°, el 12°); 0 before sunset.</summary>
        public readonly float SkyKeyIntensity;
        /// <summary>SKY_KEY colour temperature in kelvin.</summary>
        public readonly float SkyKeyKelvin;

        /// <summary>Creates a row.</summary>
        public LightStageKey(float s, float sunElevation, float sunIntensity, float sunKelvin, float shadowDistance,
            float shadowStrength, float skyBlend, Color skyTint, float skyEv, float fogDensity, Color fogColor,
            float clockMinutes, float skyKeyIntensity, float skyKeyKelvin)
        {
            S = s;
            SunElevation = sunElevation;
            SunIntensity = sunIntensity;
            SunKelvin = sunKelvin;
            ShadowDistance = shadowDistance;
            ShadowStrength = shadowStrength;
            SkyBlend = skyBlend;
            SkyTint = skyTint;
            SkyEv = skyEv;
            FogDensity = fogDensity;
            FogColor = fogColor;
            ClockMinutes = clockMinutes;
            SkyKeyIntensity = skyKeyIntensity;
            SkyKeyKelvin = skyKeyKelvin;
        }
    }
}
