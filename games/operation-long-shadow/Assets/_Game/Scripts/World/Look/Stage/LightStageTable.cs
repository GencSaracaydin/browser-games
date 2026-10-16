using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// The LightStage curves as code (TECH_PLAN D12, §7.14): the authoritative keyframes of GDD §9.2a, interpolated
    /// linearly between rows, and the per-stage constants of GDD §9.2, §9.6 and §14.2. Pure functions, no state: the
    /// controller, the editor scrubber, LookCapture and the tests all sample the same table.
    /// </summary>
    public static class LightStageTable
    {
        /// <summary>Lowest stage value.</summary>
        public const float MinS = 0f;
        /// <summary>Highest stage value.</summary>
        public const float MaxS = 4f;

        /// <summary>Compass azimuth of the sun and of the sky key, degrees (GDD §9.1).</summary>
        public const float SunAzimuth = 258f;
        /// <summary>Euler Y of the SUN/SKY_KEY transforms: rotation (elevation, 78°, 0) puts the light at azimuth 258° (GDD §9.1).</summary>
        public const float LightYaw = 78f;
        /// <summary>SKY_KEY elevation in degrees (GDD §9.2 AFTERGLOW).</summary>
        public const float SkyKeyElevation = 12f;
        /// <summary>Sodium practicals strike (MID-A 0:14, 18:40; GDD §8.5, §9.2a).</summary>
        public const float StrikeS = 1.97f;
        /// <summary>Sun at 0°: SUN off, SKY_KEY on, HUD palette switches (GDD §9.2a).</summary>
        public const float SunsetS = 2.0f;
        /// <summary>Half width of the stage-volume ramps either side of a boundary (GDD §9.2a).</summary>
        public const float StageRamp = 0.2f;
        /// <summary>Intensities below this count as "light off".</summary>
        public const float LightEpsilon = 1e-4f;

        /// <summary>Stage post exposure in EV, Golden..Blue Hour (VP_* profiles, GDD §9.6).</summary>
        public static readonly float[] StageExposure = { 0f, 0.4f, 1.0f, 1.5f };
        /// <summary>AI sight range in metres, Golden..Blue Hour (GDD §14.2).</summary>
        public static readonly float[] StageSightRange = { 70f, 60f, 45f, 35f };
        /// <summary>
        /// Default reflection intensity per stage. Decision (DECISIONS.md [WP10_LOOK]): sky-derived reflections are baked
        /// at GOLDEN in S_Persistent, so they are dimmed with the light instead of shipping a second HDR cubemap (§9.3).
        /// </summary>
        public static readonly float[] StageReflectionIntensity = { 1.0f, 0.85f, 0.5f, 0.35f };

        /// <summary>AI hearing scale per stage. GDD §14.2 gives fixed radii and no curve, so it stays 1 (decision logged).</summary>
        public static readonly float[] StageHearingScale = { 1f, 1f, 1f, 1f };

        /// <summary>HUD marker amber before sunset (TECH_PLAN §7.13, GDD §11).</summary>
        public static readonly Color HudAmber = Hex(0xE3A33B);
        /// <summary>HUD marker pale green from s 2.0.</summary>
        public static readonly Color HudGreen = Hex(0xA6E3B4);

        private static readonly Color White = Color.white;

        // GDD §9.2a, row by row. Empty cells repeat the row above. SKY_KEY is 0 until 1.97 and ramps to 0.15 at 2.00
        // while the sun falls from 0.45 to 0 (decision: a 0.03-s cross-fade instead of a pop; the GDD row at 2.00 holds).
        private static readonly LightStageKey[] s_keys =
        {
            //      s     el     int    K      dist str   blend tint              EV     fog      fog colour        clock          key    keyK
            new LightStageKey(0.00f, 8.0f, 2.00f, 3400f, 80f, 0.9f, 0.00f, White, 0f, 0.0022f, Hex(0xC9A27A), Clock(17, 58), 0f, 9000f),
            new LightStageKey(0.15f, 7.5f, 1.97f, 3330f, 80f, 0.9f, 0.00f, White, 0f, 0.0022f, Hex(0xC9A27A), Clock(18, 8), 0f, 9000f),
            new LightStageKey(0.45f, 6.0f, 1.86f, 3180f, 80f, 0.9f, 0.00f, White, 0f, 0.0022f, Hex(0xC9A27A), Clock(18, 17), 0f, 9000f),
            new LightStageKey(1.00f, 4.5f, 1.60f, 2800f, 80f, 0.9f, 0.00f, White, 0f, 0.0024f, Hex(0xC59474), Clock(18, 26), 0f, 9000f),
            new LightStageKey(1.35f, 3.0f, 1.40f, 2400f, 60f, 0.8f, 0.25f, Hex(0xFFE0C0), -0.3f, 0.0028f, Hex(0xB9805E), Clock(18, 30), 0f, 9000f),
            new LightStageKey(1.50f, 1.5f, 1.40f, 2250f, 60f, 0.8f, 0.25f, Hex(0xFFE0C0), -0.3f, 0.0028f, Hex(0xB9805E), Clock(18, 34), 0f, 9000f),
            new LightStageKey(1.80f, 1.5f, 1.40f, 2100f, 60f, 0.8f, 0.30f, Hex(0xFFE0C0), -0.3f, 0.0029f, Hex(0xB47F68), Clock(18, 38), 0f, 9000f),
            new LightStageKey(1.94f, 0.6f, 0.65f, 1980f, 60f, 0.8f, 0.70f, Hex(0xFFE0C0), -0.3f, 0.0031f, Hex(0xA9788A), Clock(18, 40), 0f, 9000f),
            new LightStageKey(1.97f, 0.4f, 0.45f, 1950f, 60f, 0.2f, 0.85f, Hex(0xFFE0C0), -0.3f, 0.0032f, Hex(0xA7788A), Clock(18, 40), 0f, 9000f),
            new LightStageKey(2.00f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, White, 0f, 0.0035f, Hex(0x6E6A8C), Clock(18, 41), 0.15f, 9000f),
            new LightStageKey(2.40f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, White, 0f, 0.0035f, Hex(0x6E6A8C), Clock(18, 50), 0.15f, 9000f),
            new LightStageKey(2.80f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, Hex(0xC8D2EC), -0.6f, 0.0037f, Hex(0x54587C), Clock(18, 58), 0.12f, 9500f),
            new LightStageKey(3.20f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, Hex(0x8FA6D8), -1.2f, 0.0040f, Hex(0x3A4A6E), Clock(19, 5), 0.08f, 10000f),
            new LightStageKey(3.70f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, Hex(0x8FA6D8), -1.2f, 0.0040f, Hex(0x3A4A6E), Clock(19, 10), 0.08f, 10000f),
            new LightStageKey(4.00f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, Hex(0x8FA6D8), -1.5f, 0.0042f, Hex(0x34425F), Clock(19, 12), 0.06f, 10000f),
        };

        // Per-light shadow bias for the low sun (TECH_PLAN §3.4, R15): starting values, scaled by LightStageTuning after the
        // vertical-slice look test on R4. Grazing light needs more normal bias and less depth bias (peter-panning).
        private static readonly float[] s_biasS = { 0f, 1.0f, 1.35f, 1.5f, 2.0f };
        private static readonly float[] s_depthBias = { 1.0f, 1.0f, 0.75f, 0.6f, 0.6f };
        private static readonly float[] s_normalBias = { 0.6f, 0.8f, 1.2f, 1.6f, 1.6f };

        /// <summary>The keyframes in order (GDD §9.2a).</summary>
        public static IReadOnlyList<LightStageKey> Keys => s_keys;

        /// <summary>Clamps to [0, 4].</summary>
        public static float Clamp(float s) => s < MinS ? MinS : s > MaxS ? MaxS : (float.IsNaN(s) ? MinS : s);

        /// <summary>Named stage of <paramref name="s"/>: Golden [0,1), Sunset [1,2), Afterglow [2,3), Blue Hour [3,4].</summary>
        public static StageId StageOf(float s)
        {
            s = Clamp(s);
            if (s < 1f) return StageId.Golden;
            if (s < 2f) return StageId.Sunset;
            if (s < 3f) return StageId.Afterglow;
            return StageId.BlueHour;
        }

        /// <summary>Stage-volume cross-fade at <paramref name="s"/> (ramps of ±0.2 around 1, 2 and 3).</summary>
        public static StageBlend BlendAt(float s)
        {
            s = Clamp(s);
            for (var b = 1; b <= 3; b++)
            {
                var lo = b - StageRamp;
                var hi = b + StageRamp;
                if (s > lo && s < hi) return new StageBlend((StageId)(b - 1), (StageId)b, (s - lo) / (hi - lo));
            }
            var st = StageOf(s);
            return new StageBlend(st, st, 0f);
        }

        /// <summary>Story clock in minutes after midnight at <paramref name="s"/> (GDD §9.2a).</summary>
        public static float ClockAt(float s)
        {
            Locate(s, out var a, out var b, out var t);
            return Mathf.Lerp(s_keys[a].ClockMinutes, s_keys[b].ClockMinutes, t);
        }

        /// <summary>Samples every curve at <paramref name="s"/>.</summary>
        public static LightStageSample Sample(float s)
        {
            s = Clamp(s);
            Locate(s, out var ia, out var ib, out var t);
            var a = s_keys[ia];
            var b = s_keys[ib];
            var blend = BlendAt(s);
            var stage = StageOf(s);

            var r = new LightStageSample
            {
                S = s,
                Stage = stage,
                Blend = blend,
                SunElevation = Mathf.Lerp(a.SunElevation, b.SunElevation, t),
                SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                SunKelvin = Mathf.Lerp(a.SunKelvin, b.SunKelvin, t),
                ShadowDistance = Mathf.Lerp(a.ShadowDistance, b.ShadowDistance, t),
                ShadowStrength = Mathf.Lerp(a.ShadowStrength, b.ShadowStrength, t),
                SkyBlend = Mathf.Lerp(a.SkyBlend, b.SkyBlend, t),
                SkyTint = LerpColor(a.SkyTint, b.SkyTint, t),
                SkyEv = Mathf.Lerp(a.SkyEv, b.SkyEv, t),
                FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                FogColor = LerpColor(a.FogColor, b.FogColor, t),
                ClockMinutes = Mathf.Lerp(a.ClockMinutes, b.ClockMinutes, t),
                SkyKeyIntensity = Mathf.Lerp(a.SkyKeyIntensity, b.SkyKeyIntensity, t),
                SkyKeyKelvin = Mathf.Lerp(a.SkyKeyKelvin, b.SkyKeyKelvin, t),
                StagePostExposure = blend.Blend(StageExposure[0], StageExposure[1], StageExposure[2], StageExposure[3]),
                ReflectionIntensity = blend.Blend(StageReflectionIntensity[0], StageReflectionIntensity[1],
                    StageReflectionIntensity[2], StageReflectionIntensity[3]),
                AISightTarget = StageSightRange[(int)stage],
                AfterSunset = s >= SunsetS,
                HudColor = s >= SunsetS ? HudGreen : HudAmber,
                SunDiscVisibility = s <= StrikeS ? 1f : s >= SunsetS ? 0f : 1f - (s - StrikeS) / (SunsetS - StrikeS),
            };
            ShadowBiasAt(s, out r.ShadowDepthBias, out r.ShadowNormalBias);
            return r;
        }

        /// <summary>Per-light shadow bias at <paramref name="s"/> (starting values; LightStageTuning scales them).</summary>
        public static void ShadowBiasAt(float s, out float depthBias, out float normalBias)
        {
            s = Clamp(s);
            var i = 0;
            while (i < s_biasS.Length - 2 && s > s_biasS[i + 1]) i++;
            var span = s_biasS[i + 1] - s_biasS[i];
            var t = span > 0f ? Mathf.Clamp01((s - s_biasS[i]) / span) : 0f;
            depthBias = Mathf.Lerp(s_depthBias[i], s_depthBias[i + 1], t);
            normalBias = Mathf.Lerp(s_normalBias[i], s_normalBias[i + 1], t);
        }

        /// <summary>Sight range of a named stage (GDD §14.2).</summary>
        public static float SightRangeOf(StageId stage) => StageSightRange[(int)stage];

        /// <summary>Hearing scale of a named stage.</summary>
        public static float HearingScaleOf(StageId stage) => StageHearingScale[(int)stage];

        /// <summary>Rotation of a directional light at <paramref name="elevation"/>: Euler (elevation, 78°, 0) (GDD §9.1).</summary>
        public static Quaternion LightRotation(float elevation) => Quaternion.Euler(elevation, LightYaw, 0f);

        /// <summary>World direction from the scene toward the sun at <paramref name="elevation"/> (azimuth 258°).</summary>
        public static Vector3 SunDirection(float elevation) => -(LightRotation(elevation) * Vector3.forward);

        /// <summary>"18:40" from minutes after midnight.</summary>
        public static string FormatClock(float minutes)
        {
            var m = Mathf.FloorToInt(minutes + 1e-3f);
            var h = (m / 60) % 24;
            return h.ToString("00") + ":" + (m % 60).ToString("00");
        }

        /// <summary>Finds the keyframe pair around <paramref name="s"/> and the fraction between them.</summary>
        public static void Locate(float s, out int a, out int b, out float t)
        {
            s = Clamp(s);
            var last = s_keys.Length - 1;
            if (s <= s_keys[0].S)
            {
                a = b = 0;
                t = 0f;
                return;
            }
            if (s >= s_keys[last].S)
            {
                a = b = last;
                t = 0f;
                return;
            }
            var i = 0;
            while (i < last - 1 && s >= s_keys[i + 1].S) i++;
            a = i;
            b = i + 1;
            var span = s_keys[b].S - s_keys[a].S;
            t = span > 0f ? (s - s_keys[a].S) / span : 0f;
        }

        /// <summary>sRGB colour from 0xRRGGBB.</summary>
        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>Interpolates two sRGB colours in linear light and returns sRGB.</summary>
        public static Color LerpColor(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.Lerp(a.linear, b.linear, t).gamma;
        }

        private static float Clock(int hours, int minutes) => hours * 60f + minutes;
    }
}
