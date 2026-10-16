using System.Collections.Generic;
using LongShadow.World.Look.FX;
using LongShadow.World.Look.Practicals;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>Practical light rules: budget (GDD §9.5), sodium warm-up and ripple (GDD §8.5), flicker, flares, glare.</summary>
    public sealed class PracticalRulesTests
    {
        [Test]
        public void BudgetKeepsTwelveAdditionalLights()
        {
            Assert.AreEqual(6, PracticalBudgetMath.Slots(0)); // 12 - 4 muzzle = 8, capped at 6 per area
            Assert.AreEqual(6, PracticalBudgetMath.Slots(2));
            Assert.AreEqual(4, PracticalBudgetMath.Slots(4)); // floods F1-F4 on
            Assert.AreEqual(0, PracticalBudgetMath.Slots(8));
            Assert.AreEqual(0, PracticalBudgetMath.Slots(20));
        }

        [Test]
        public void BudgetPicksTheNearest()
        {
            var c = new List<PracticalBudgetMath.Candidate>
            {
                new PracticalBudgetMath.Candidate { Index = 0, Score = 30f },
                new PracticalBudgetMath.Candidate { Index = 1, Score = 5f },
                new PracticalBudgetMath.Candidate { Index = 2, Score = 12f },
                new PracticalBudgetMath.Candidate { Index = 3, Score = 8f },
            };
            var win = PracticalBudgetMath.SelectBest(c, 2);
            Assert.AreEqual(2, win);
            Assert.AreEqual(1, c[0].Index);
            Assert.AreEqual(3, c[1].Index);
            Assert.Less(PracticalBudgetMath.Score(10f, 1f, true, true), PracticalBudgetMath.Score(10f, 1f, true, false));
            Assert.Greater(PracticalBudgetMath.Score(10f, 1f, false, false), PracticalBudgetMath.Score(10f, 1f, true, false));
        }

        [Test]
        public void SodiumWarmsFromPinkRedToOrangeOverEightSeconds()
        {
            Assert.AreEqual(0f, SodiumWarmup.Level(-1f));
            Assert.AreEqual(SodiumWarmup.StartColor, SodiumWarmup.Hue(0f));
            Assert.AreEqual(SodiumWarmup.WarmColor, SodiumWarmup.Hue(SodiumWarmup.Duration));
            Assert.AreEqual(1f, SodiumWarmup.Level(SodiumWarmup.Duration + 1f), 1e-4f);
            Assert.Less(SodiumWarmup.Level(1f), SodiumWarmup.Level(4f));
            Assert.Less(SodiumWarmup.Level(4f), SodiumWarmup.Level(7.5f));
            Assert.Greater(SodiumWarmup.Hue(0f).b, SodiumWarmup.WarmColor.b); // pink → orange
        }

        [Test]
        public void RippleTravelsAtFifteenMetresPerSecondFromTheGenerator()
        {
            var o = LampRipple.DefaultOrigin;
            Assert.AreEqual(new Vector3(372f, 0f, 305f), o);
            Assert.AreEqual(10f, LampRipple.Delay(o + new Vector3(150f, 6f, 0f), o, LampRipple.DefaultSpeed), 1e-4f);
            Assert.AreEqual(0f, LampRipple.Delay(o + Vector3.up * 6f, o, LampRipple.DefaultSpeed), 1e-4f);
        }

        [Test]
        public void FlickerCurvesStayInRangeAndAreDeterministic()
        {
            for (var t = 0f; t < 60f; t += 0.037f)
            {
                var f = FlickerNoise.Fire(t, 3f);
                Assert.GreaterOrEqual(f, 0.549f);
                Assert.LessOrEqual(f, 1.151f);
                var fl = FlickerNoise.Fluorescent(t, 5f);
                Assert.GreaterOrEqual(fl, 0f);
                Assert.LessOrEqual(fl, 1f);
                var fa = FlickerNoise.Faulty(t, 2f);
                Assert.IsTrue(fa > 0.99f || fa < 0.1f);
                Assert.AreEqual(FlickerNoise.Flare(t, 9f), FlickerNoise.Flare(t, 9f));
            }
        }

        [Test]
        public void FlareBurnsThirtySecondsUnderAFourDegreePendulum()
        {
            Assert.AreEqual(0f, ParachuteFlare.Envelope(-1f));
            Assert.AreEqual(0f, ParachuteFlare.Envelope(0f), 1e-4f);
            Assert.AreEqual(1f, ParachuteFlare.Envelope(0.6f), 1e-4f);
            Assert.AreEqual(1f, ParachuteFlare.Envelope(15f));
            Assert.AreEqual(1f / 3f, ParachuteFlare.Envelope(29f), 1e-3f);
            Assert.AreEqual(0f, ParachuteFlare.Envelope(30.5f));
            var max = 0f;
            for (var t = 0f; t < 6f; t += 0.01f) max = Mathf.Max(max, Mathf.Abs(ParachuteFlare.PendulumAngle(t)));
            Assert.AreEqual(4f, max, 0.01f);
            Assert.AreEqual(1.3f, ParachuteFlare.DescentSpeed);
        }

        [Test]
        public void FloodSlamOvershootsThenSettles()
        {
            Assert.AreEqual(0f, FloodLight.SlamCurve(-0.1f, 1f));
            Assert.Greater(FloodLight.SlamCurve(0.049f, 1f), 1.2f); // arc flash before the sputter
            Assert.AreEqual(1f, FloodLight.SlamCurve(2f, 1f));
        }

        [Test]
        public void GlareNeedsTheViewAndTheBeam()
        {
            var cosOuter = Mathf.Cos(25f * Mathf.Deg2Rad);
            var cosInner = Mathf.Cos(3f * Mathf.Deg2Rad);
            var inBeamLooking = FloodGlare.Glare(1f, 1f, 50f, 15f, cosOuter, cosInner, 35f);
            var lookingAway = FloodGlare.Glare(0f, 1f, 50f, 15f, cosOuter, cosInner, 35f);
            var outsideBeam = FloodGlare.Glare(1f, 0.2f, 50f, 15f, cosOuter, cosInner, 35f);
            var far = FloodGlare.Glare(1f, 1f, 50f, 120f, cosOuter, cosInner, 35f);
            Assert.Greater(inBeamLooking, 0.6f);
            Assert.AreEqual(0f, lookingAway);
            Assert.AreEqual(0f, outsideBeam);
            Assert.Less(far, inBeamLooking);
        }

        [Test]
        public void BindRulesMatchExactOrPrefix()
        {
            Assert.IsTrue(PracticalBindRule.Matches("LAMP.", "LAMP.07"));
            Assert.IsFalse(PracticalBindRule.Matches("LAMP.", "LAMPFAR.01"));
            Assert.IsTrue(PracticalBindRule.Matches("B6.F*", "B6.F3"));
            Assert.IsTrue(PracticalBindRule.Matches("B4.FireProp", "B4.FireProp"));
            Assert.IsFalse(PracticalBindRule.Matches("B4.FireProp", "B4.FirePropX"));
            Assert.IsFalse(PracticalBindRule.Matches(null, "x"));
        }

        [Test]
        public void DownwashFadesWithHeight()
        {
            Assert.AreEqual(1f, RotorDownwash.StrengthFor(0f, 28f, 1f), 1e-4f);
            Assert.AreEqual(0f, RotorDownwash.StrengthFor(28f, 28f, 1f), 1e-4f);
            Assert.Greater(RotorDownwash.StrengthFor(5f, 28f, 1f), RotorDownwash.StrengthFor(15f, 28f, 1f));
            Assert.AreEqual(0f, RotorDownwash.StrengthFor(5f, 28f, 0f));
        }
    }
}
