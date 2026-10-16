using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.1 hip/ADS spread and the spread model modifiers.</summary>
    public class SpreadTests
    {
        [Test]
        public void StillSpreadIsTheGddValue()
        {
            foreach (var w in WeaponTable.PlayerWeapons)
            {
                var m = new SpreadModel();
                Assert.That(m.Current(w.Spread, 0f, 0f, true, false), Is.EqualTo(w.Spread.Hip).Within(1e-5f), w.Id + " hip");
                Assert.That(m.Current(w.Spread, 1f, 0f, true, false), Is.EqualTo(w.Spread.Ads).Within(1e-5f), w.Id + " ADS");
            }
        }

        [Test]
        public void MovementAirAndCrouchModifyTheCone()
        {
            var s = WeaponTable.AK.Spread;
            var m = new SpreadModel();
            var still = m.Current(s, 0f, 0f, true, false);
            Assert.That(m.Current(s, 0f, 1f, true, false), Is.GreaterThan(still), "moving widens");
            Assert.That(m.Current(s, 0f, 0f, false, false), Is.EqualTo(still * s.AirborneMul).Within(1e-4f), "airborne");
            Assert.That(m.Current(s, 0f, 0f, true, true), Is.EqualTo(still * s.CrouchMul).Within(1e-4f), "crouched and still");
            Assert.That(m.Current(s, 1f, 1f, true, false), Is.LessThan(m.Current(s, 0f, 1f, true, false)), "ADS always tighter");
        }

        [Test]
        public void BloomAccumulatesToItsCapAndDecays()
        {
            var s = WeaponTable.Commando.Spread;
            var m = new SpreadModel();
            for (var i = 0; i < 50; i++) m.OnShot(s);
            Assert.That(m.Bloom, Is.EqualTo(s.BloomMax).Within(1e-5f));
            m.Tick(s, 10f);
            Assert.That(m.Bloom, Is.EqualTo(0f));
        }

        [Test]
        public void DeviationStaysInsideTheCone()
        {
            var aim = Quaternion.Euler(-10f, 35f, 0f);
            var fwd = aim * Vector3.forward;
            var rng = new System.Random(5);
            var maxSeen = 0f;
            for (var i = 0; i < 2000; i++)
            {
                var d = SpreadModel.Deviate(aim, 3f, (float)rng.NextDouble(), (float)rng.NextDouble());
                var a = Vector3.Angle(fwd, d);
                Assert.That(a, Is.LessThanOrEqualTo(3.0f + 0.05f));
                maxSeen = Mathf.Max(maxSeen, a);
            }
            Assert.That(maxSeen, Is.GreaterThan(2.7f), "uses the whole cone");
            Assert.That(Vector3.Angle(fwd, SpreadModel.Deviate(aim, 0f, 0.3f, 0.7f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void CrosshairIsNormalised()
        {
            var s = WeaponTable.Commando.Spread;
            Assert.That(SpreadModel.Crosshair01(s, 0f), Is.EqualTo(0f));
            Assert.That(SpreadModel.Crosshair01(s, s.CrosshairMax * 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SpreadModel.Crosshair01(s, 100f), Is.EqualTo(1f));
        }
    }
}
