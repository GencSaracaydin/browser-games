using LongShadow.Cinematics;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Cinematics.Inserts;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Cinematics
{
    /// <summary>Pure math of the WP09 runtime: lenses, look, inserts, envelopes, props (TECH_PLAN §7.12, GDD §8).</summary>
    public sealed class CinematicMathTests
    {
        [Test]
        public void LensMath_Super35_FocalAndFovRoundTrip()
        {
            // 50 mm on an 18.66 mm sensor height: 2·atan(9.33/50) ≈ 21.14°.
            Assert.That(LensMath.VerticalFov(50f), Is.EqualTo(21.14f).Within(0.05f));
            foreach (var f in new[] { 18f, 24f, 35f, 50f, 85f })
                Assert.That(LensMath.FocalFromVerticalFov(LensMath.VerticalFov(f)), Is.EqualTo(f).Within(0.01f));
            Assert.That(LensMath.ZoomFov(50f, 30f, 0f), Is.EqualTo(50f).Within(0.01f));
            Assert.That(LensMath.ZoomFov(50f, 30f, 1f), Is.EqualTo(30f).Within(0.01f));
            // A zoom is linear in focal length, so the halfway FOV is below the degree midpoint.
            Assert.That(LensMath.ZoomFov(50f, 30f, 0.5f), Is.LessThan(40f));
        }

        [Test]
        public void LimitedLook_ClampsToGddLimits()
        {
            var look = new LimitedLook { Enabled = true };
            for (var i = 0; i < 200; i++) look.Tick(0.016f, new Vector2(5f, 5f));
            Assert.That(look.Yaw, Is.EqualTo(LimitedLook.DefaultYaw).Within(0.001f), "GDD §8: ±40° yaw");
            Assert.That(look.Pitch, Is.EqualTo(LimitedLook.DefaultPitch).Within(0.001f), "GDD §8: ±20° pitch");
            for (var i = 0; i < 400; i++) look.Tick(0.016f, new Vector2(-5f, -5f));
            Assert.That(look.Yaw, Is.EqualTo(-LimitedLook.DefaultYaw).Within(0.001f));
            Assert.That(look.Pitch, Is.EqualTo(-LimitedLook.DefaultPitch).Within(0.001f));
        }

        [Test]
        public void LimitedLook_SpringsBackOnlyAfterTwoSecondsIdle()
        {
            var look = new LimitedLook { Enabled = true };
            look.Tick(0.016f, new Vector2(20f, 0f));
            var start = look.Yaw;
            for (var t = 0f; t < 1.9f; t += 0.02f) look.Tick(0.02f, Vector2.zero);
            Assert.That(look.Yaw, Is.EqualTo(start).Within(0.001f), "no spring before 2 s");
            for (var t = 0f; t < 4f; t += 0.02f) look.Tick(0.02f, Vector2.zero);
            Assert.That(Mathf.Abs(look.Yaw), Is.LessThan(0.5f), "springs back to centre after 2 s");
        }

        [Test]
        public void LimitedLook_RecentreIgnoresInputAndEndsAtZero()
        {
            var look = new LimitedLook { Enabled = true };
            look.Tick(0.016f, new Vector2(30f, 10f));
            look.Recentre(0.8f);
            // Input during the recentre is ignored; once it has finished, input applies again (so stop at its end).
            for (var i = 0; i < 100 && look.Recentring; i++) look.Tick(0.02f, new Vector2(10f, 10f));
            Assert.IsFalse(look.Recentring, "the recentre ends after 0.8 s");
            Assert.That(look.Yaw, Is.EqualTo(0f).Within(0.01f));
            Assert.That(look.Pitch, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void InsertTiming_FramesAt60AndTwoFrameMinimum()
        {
            Assert.That(InsertTiming.FramesToSeconds(6), Is.EqualTo(0.1f).Within(1e-5f), "GDD §8.4: 6 f = 0.100 s");
            Assert.IsFalse(InsertTiming.IsComplete(0.5f, 1, 0.1f), "minimum 2 rendered frames");
            Assert.IsTrue(InsertTiming.IsComplete(0.1f, 2, 0.1f));
            Assert.IsFalse(InsertTiming.IsComplete(0.05f, 6, 0.1f));
        }

        [Test]
        public void InsertTable_MatchesGdd55()
        {
            var i1 = InsertTable.Get(SequenceIds.InsertI1);
            Assert.That(i1.BoothSeconds, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.AreEqual("D017", i1.DefaultLine);
            Assert.IsTrue(i1.LineOutlivesInsert, "I1: VO continues over gameplay");
            Assert.That(InsertTable.Get(SequenceIds.InsertI2).BoothSeconds, Is.EqualTo(0.1f).Within(1e-4f), "I2: 6 frames");
            var i3 = InsertTable.Get(SequenceIds.InsertI3);
            Assert.That(i3.BoothSeconds, Is.EqualTo(3f).Within(1e-4f));
            Assert.AreEqual("D054", i3.DefaultLine);
            var i4 = InsertTable.Get(SequenceIds.InsertI4);
            Assert.AreEqual(4, i4.Shots.Length, "I4 cascades four inserts");
            Assert.That(i4.BoothSeconds, Is.EqualTo(23f / 60f).Within(1e-4f), "6 + 5 + 4 + 8 frames");
            Assert.IsTrue(i4.EndWhite, "cut to white before any shot");
            Assert.AreEqual(BoothCameraPreset.EyeEcu, i4.Shots[3].Preset);
            Assert.AreEqual(6, InsertTable.DeathLines.Length);
            Assert.That(InsertTable.DeathSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void FxEnvelope_FlashFallsFromPeakToZero()
        {
            Assert.That(FxEnvelope.Flash(6f, 2f, 0f), Is.EqualTo(6f));
            Assert.That(FxEnvelope.Flash(6f, 2f, 2f), Is.EqualTo(0f));
            var prev = 6f;
            for (var t = 0f; t <= 2f; t += 0.05f)
            {
                var v = FxEnvelope.Flash(6f, 2f, t);
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-5f));
                prev = v;
            }
            Assert.That(FxEnvelope.Glitch(1f, 1f, 0.075f), Is.EqualTo(0.5f).Within(0.01f), "0.15 s ramp in");
            Assert.That(FxEnvelope.Glitch(1f, 1f, 0.5f), Is.EqualTo(1f));
            Assert.That(FxEnvelope.Pulse(1f, FxEnvelope.SuppressionSeconds), Is.EqualTo(0f));
        }

        [Test]
        public void Props_MatchWp09ArtConventions()
        {
            // WP09_ART: 6:40 -> hour -200°, minute -240°.
            Assert.That(WallClock.HourAngle(6, 40), Is.EqualTo(-200f).Within(1e-4f));
            Assert.That(WallClock.MinuteAngle(40), Is.EqualTo(-240f).Within(1e-4f));
            // 7.5 ips on a full 7-inch pack (r 0.075 m): ≈ 145.5°/s.
            Assert.That(ReelDeck.ReelDegreesPerSecond(1f), Is.EqualTo(145.5f).Within(0.5f));
            Assert.That(ReelDeck.PackScale(0f), Is.EqualTo(ReelDeck.EmptyPack).Within(1e-4f));
            Assert.That(ReelDeck.PackScale(1f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void FlickerLight_StutterBlinksFourTimesThenStaysOn()
        {
            var transitions = 0;
            var last = FlickerLight.Evaluate(FlickerMode.StutterOn, 0f) > 0.5f;
            for (var t = 0f; t < 1.35f; t += 0.005f)
            {
                var on = FlickerLight.Evaluate(FlickerMode.StutterOn, t) > 0.5f;
                if (on && !last) transitions++;
                last = on;
            }
            Assert.That(transitions, Is.EqualTo(3), "starts on, so four blinks show three off→on edges inside the pattern");
            Assert.That(FlickerLight.Evaluate(FlickerMode.StutterOn, 4f), Is.GreaterThan(0.9f));
            Assert.That(FlickerLight.Evaluate(FlickerMode.Off, 1f), Is.EqualTo(0f));
            Assert.That(FlickerLight.Evaluate(FlickerMode.Dying, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void HandheldNoise_IsBoundedAndDecays()
        {
            for (var t = 0f; t < 10f; t += 0.37f)
            {
                var s = HandheldNoise.Sway(t, 0.3f, 1f, 3f);
                Assert.That(Mathf.Abs(s.x), Is.LessThanOrEqualTo(0.3f + 1e-4f));
                Assert.That(Mathf.Abs(s.y), Is.LessThanOrEqualTo(0.3f + 1e-4f));
            }
            Assert.That(HandheldNoise.Impulse(2f, 1.5f).magnitude, Is.LessThan(0.001f));
            Assert.That(HandheldNoise.Impulse(-0.1f, 1.5f), Is.EqualTo(Vector3.zero));
        }
    }
}
