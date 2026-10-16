using LongShadow.Core;
using LongShadow.World.Look.Stage;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>The LightStage curves against GDD §9.2a (keyframes, linear interpolation, stages, events-relevant points).</summary>
    public sealed class LightStageTableTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void KeyframesAreExactAtTheirRows()
        {
            // GDD §9.2a rows: s, sun el, sun int, K, shadow dist, strength, blend, EV, fog density, clock.
            AssertRow(0.00f, 8.0f, 2.00f, 3400f, 80f, 0.9f, 0.00f, 0f, 0.0022f, 17 * 60 + 58);
            AssertRow(0.15f, 7.5f, 1.97f, 3330f, 80f, 0.9f, 0.00f, 0f, 0.0022f, 18 * 60 + 8);
            AssertRow(0.45f, 6.0f, 1.86f, 3180f, 80f, 0.9f, 0.00f, 0f, 0.0022f, 18 * 60 + 17);
            AssertRow(1.00f, 4.5f, 1.60f, 2800f, 80f, 0.9f, 0.00f, 0f, 0.0024f, 18 * 60 + 26);
            AssertRow(1.35f, 3.0f, 1.40f, 2400f, 60f, 0.8f, 0.25f, -0.3f, 0.0028f, 18 * 60 + 30);
            AssertRow(1.50f, 1.5f, 1.40f, 2250f, 60f, 0.8f, 0.25f, -0.3f, 0.0028f, 18 * 60 + 34);
            AssertRow(1.80f, 1.5f, 1.40f, 2100f, 60f, 0.8f, 0.30f, -0.3f, 0.0029f, 18 * 60 + 38);
            AssertRow(1.94f, 0.6f, 0.65f, 1980f, 60f, 0.8f, 0.70f, -0.3f, 0.0031f, 18 * 60 + 40);
            AssertRow(1.97f, 0.4f, 0.45f, 1950f, 60f, 0.2f, 0.85f, -0.3f, 0.0032f, 18 * 60 + 40);
            AssertRow(2.00f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, 0f, 0.0035f, 18 * 60 + 41);
            AssertRow(2.80f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, -0.6f, 0.0037f, 18 * 60 + 58);
            AssertRow(3.20f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, -1.2f, 0.0040f, 19 * 60 + 5);
            AssertRow(4.00f, 0.0f, 0.00f, 1950f, 60f, 0.0f, 1.00f, -1.5f, 0.0042f, 19 * 60 + 12);
        }

        [Test]
        public void SkyKeyMatchesTheGdd()
        {
            Assert.AreEqual(0.15f, LightStageTable.Sample(2.0f).SkyKeyIntensity, Eps);
            Assert.AreEqual(0.15f, LightStageTable.Sample(2.4f).SkyKeyIntensity, Eps);
            Assert.AreEqual(0.12f, LightStageTable.Sample(2.8f).SkyKeyIntensity, Eps);
            Assert.AreEqual(9500f, LightStageTable.Sample(2.8f).SkyKeyKelvin, 0.01f);
            Assert.AreEqual(0.08f, LightStageTable.Sample(3.2f).SkyKeyIntensity, Eps);
            Assert.AreEqual(0.06f, LightStageTable.Sample(4.0f).SkyKeyIntensity, Eps);
            Assert.AreEqual(0f, LightStageTable.Sample(1.9f).SkyKeyIntensity, Eps);
        }

        [Test]
        public void ValuesAreLinearBetweenRows()
        {
            var s = LightStageTable.Sample(0.075f);
            Assert.AreEqual(7.75f, s.SunElevation, Eps);
            Assert.AreEqual(1.985f, s.SunIntensity, Eps);
            Assert.AreEqual(3365f, s.SunKelvin, 0.01f);
            var m = LightStageTable.Sample(1.955f); // half way 1.94 → 1.97
            Assert.AreEqual(0.5f, m.SunElevation, Eps);
            Assert.AreEqual(0.5f, m.ShadowStrength, Eps);
        }

        [Test]
        public void StagesFollowTheBoundaries()
        {
            Assert.AreEqual(StageId.Golden, LightStageTable.StageOf(0f));
            Assert.AreEqual(StageId.Golden, LightStageTable.StageOf(0.999f));
            Assert.AreEqual(StageId.Sunset, LightStageTable.StageOf(1f));
            Assert.AreEqual(StageId.Sunset, LightStageTable.StageOf(1.999f));
            Assert.AreEqual(StageId.Afterglow, LightStageTable.StageOf(2f));
            Assert.AreEqual(StageId.BlueHour, LightStageTable.StageOf(3f));
            Assert.AreEqual(StageId.BlueHour, LightStageTable.StageOf(4f));
            Assert.AreEqual(StageId.Golden, LightStageTable.StageOf(-3f));
            Assert.AreEqual(StageId.BlueHour, LightStageTable.StageOf(9f));
            Assert.AreEqual(0f, LightStageTable.Clamp(float.NaN));
        }

        [Test]
        public void SunGoesOffAndSkyKeyComesOnAtSunset()
        {
            Assert.IsTrue(LightStageTable.Sample(1.96f).SunEnabled);
            Assert.IsFalse(LightStageTable.Sample(1.96f).SkyKeyEnabled);
            Assert.IsFalse(LightStageTable.Sample(2.0f).SunEnabled);
            Assert.IsTrue(LightStageTable.Sample(2.0f).SkyKeyEnabled);
            Assert.IsFalse(LightStageTable.Sample(3.5f).SunEnabled);
        }

        [Test]
        public void SunDiscFadesBehindTheRidge()
        {
            Assert.AreEqual(1f, LightStageTable.Sample(1.97f).SunDiscVisibility, Eps);
            Assert.AreEqual(0.5f, LightStageTable.Sample(1.985f).SunDiscVisibility, 1e-3f);
            Assert.AreEqual(0f, LightStageTable.Sample(2.0f).SunDiscVisibility, Eps);
        }

        [Test]
        public void AiSightAndHudPaletteFollowTheStage()
        {
            Assert.AreEqual(70f, LightStageTable.Sample(0.5f).AISightTarget);
            Assert.AreEqual(60f, LightStageTable.Sample(1.5f).AISightTarget);
            Assert.AreEqual(45f, LightStageTable.Sample(2.5f).AISightTarget);
            Assert.AreEqual(35f, LightStageTable.Sample(3.5f).AISightTarget);
            Assert.AreEqual(LightStageTable.HudAmber, LightStageTable.Sample(1.99f).HudColor);
            Assert.AreEqual(LightStageTable.HudGreen, LightStageTable.Sample(2.0f).HudColor);
            Assert.IsTrue(LightStageTable.Sample(2.0f).AfterSunset);
            Assert.IsFalse(LightStageTable.Sample(1.999f).AfterSunset);
            Assert.AreEqual(1f, LightStageTable.HearingScaleOf(StageId.BlueHour));
        }

        [Test]
        public void StoryClockReadsTheWatchCheckTime()
        {
            Assert.AreEqual("17:58", LightStageTable.FormatClock(LightStageTable.ClockAt(0f)));
            Assert.AreEqual("18:40", LightStageTable.FormatClock(LightStageTable.ClockAt(1.94f)));
            Assert.AreEqual("18:40", LightStageTable.FormatClock(LightStageTable.ClockAt(1.97f)));
            Assert.AreEqual("19:12", LightStageTable.FormatClock(LightStageTable.ClockAt(4f)));
        }

        [Test]
        public void StageWeightsSumToOneAndRampAroundBoundaries()
        {
            for (var s = 0f; s <= 4f; s += 0.01f)
            {
                var b = LightStageTable.BlendAt(s);
                var sum = 0f;
                for (var i = 0; i < 4; i++) sum += b.WeightOf((StageId)i);
                Assert.AreEqual(1f, sum, 1e-4f, "s " + s);
                Assert.AreEqual(1f, b.VolumeWeightOf(b.Lower), Eps);
            }
            var mid = LightStageTable.BlendAt(1.0f);
            Assert.AreEqual(StageId.Golden, mid.Lower);
            Assert.AreEqual(StageId.Sunset, mid.Upper);
            Assert.AreEqual(0.5f, mid.Fraction, Eps);
            Assert.AreEqual(0.75f, LightStageTable.BlendAt(1.1f).Fraction, Eps);
            Assert.AreEqual(LightStageTable.BlendAt(0.8f).Lower, LightStageTable.BlendAt(0.8f).Upper);
            Assert.AreEqual(LightStageTable.BlendAt(2.5f).Lower, StageId.Afterglow);
        }

        [Test]
        public void StagePostExposureBlends()
        {
            Assert.AreEqual(0f, LightStageTable.Sample(0.5f).StagePostExposure, Eps);
            Assert.AreEqual(0.4f, LightStageTable.Sample(1.5f).StagePostExposure, Eps);
            Assert.AreEqual(1.0f, LightStageTable.Sample(2.5f).StagePostExposure, Eps);
            Assert.AreEqual(1.5f, LightStageTable.Sample(3.5f).StagePostExposure, Eps);
            Assert.AreEqual(0.7f, LightStageTable.Sample(2.0f).StagePostExposure, Eps);
        }

        [Test]
        public void SunSitsAtAzimuth258()
        {
            var d = LightStageTable.SunDirection(5f);
            Assert.AreEqual(258f, MathX.BearingOf(d), 0.01f);
            Assert.AreEqual(Mathf.Sin(5f * Mathf.Deg2Rad), d.y, 1e-4f);
            var fwd = LightStageTable.LightRotation(5f) * Vector3.forward; // light travels toward azimuth 78°
            Assert.AreEqual(78f, MathX.BearingOf(fwd), 0.01f);
            Assert.Less(fwd.y, 0f);
        }

        [Test]
        public void FogThickensThroughTheMission()
        {
            var prev = 0f;
            for (var s = 0f; s <= 4f; s += 0.05f)
            {
                var d = LightStageTable.Sample(s).FogDensity;
                Assert.GreaterOrEqual(d + 1e-7f, prev, "s " + s);
                prev = d;
            }
        }

        [Test]
        public void ShadowBiasGrowsForTheGrazingSun()
        {
            LightStageTable.ShadowBiasAt(0f, out var d0, out var n0);
            LightStageTable.ShadowBiasAt(1.8f, out var d1, out var n1);
            Assert.Greater(n1, n0);
            Assert.Less(d1, d0);
        }

        private static void AssertRow(float s, float el, float intensity, float k, float dist, float strength, float blend, float ev, float fog, float clock)
        {
            var x = LightStageTable.Sample(s);
            Assert.AreEqual(el, x.SunElevation, Eps, "el @" + s);
            Assert.AreEqual(intensity, x.SunIntensity, Eps, "int @" + s);
            Assert.AreEqual(k, x.SunKelvin, 0.01f, "K @" + s);
            Assert.AreEqual(dist, x.ShadowDistance, Eps, "dist @" + s);
            Assert.AreEqual(strength, x.ShadowStrength, Eps, "strength @" + s);
            Assert.AreEqual(blend, x.SkyBlend, Eps, "blend @" + s);
            Assert.AreEqual(ev, x.SkyEv, Eps, "EV @" + s);
            Assert.AreEqual(fog, x.FogDensity, 1e-6f, "fog @" + s);
            Assert.AreEqual(clock, x.ClockMinutes, 1e-3f, "clock @" + s);
        }
    }
}
