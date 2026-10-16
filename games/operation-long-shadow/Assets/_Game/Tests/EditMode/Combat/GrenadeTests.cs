using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.2 frag: 18 m throw, cooking, the 4.0 s fuse, 220 damage lethal to 4 m with linear falloff to 8 m.</summary>
    public class GrenadeTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void FortyFiveDegreeThrowLandsAtEighteenMetres()
        {
            var g = 9.81f;
            var v = GrenadeBallistics.SpeedForMaxRange(18f, 1.65f, g);
            Assert.That(GrenadeBallistics.Range(v, 45f, 1.65f, g), Is.EqualTo(18f).Within(0.01f));
            Assert.That(GrenadeBallistics.Range(v, 12f, 1.65f, g), Is.LessThan(18f), "a level look (plus the bias) lands short");
        }

        [Test]
        public void ThrowVelocityAddsTheUpwardBias()
        {
            var v = GrenadeBallistics.ThrowVelocity(Quaternion.identity, 10f, 12f);
            var pitch = Mathf.Asin(v.normalized.y) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.EqualTo(12f).Within(0.01f));
            Assert.That(v.magnitude, Is.EqualTo(10f).Within(1e-3f));
            var steep = GrenadeBallistics.ThrowVelocity(Quaternion.Euler(-80f, 0f, 0f), 10f, 12f);
            Assert.That(Mathf.Asin(steep.normalized.y) * Mathf.Rad2Deg, Is.EqualTo(60f).Within(0.01f), "clamped to 60°");
        }

        [Test]
        public void AiArcHitsItsTarget()
        {
            var from = new Vector3(0f, 1.5f, 0f);
            var to = new Vector3(10f, 0f, 6f);
            var t = GrenadeBallistics.AiFlightTime(Vector3.Distance(from, to));
            var v = GrenadeBallistics.SolveArc(from, to, t, 9.81f);
            var p = from + v * t + 0.5f * new Vector3(0f, -9.81f, 0f) * t * t;
            Assert.That(Vector3.Distance(p, to), Is.LessThan(1e-3f));
        }

        [Test]
        public void ExplosionDamageFallsOffLinearly()
        {
            Assert.That(ExplosionMath.DamageAt(0f, 220f, 4f, 8f), Is.EqualTo(220f));
            Assert.That(ExplosionMath.DamageAt(4f, 220f, 4f, 8f), Is.EqualTo(220f), "lethal within 4.0 m");
            Assert.That(ExplosionMath.DamageAt(6f, 220f, 4f, 8f), Is.EqualTo(110f).Within(1e-3f));
            Assert.That(ExplosionMath.DamageAt(8f, 220f, 4f, 8f), Is.EqualTo(0f));
            Assert.That(ExplosionMath.DamageAt(12f, 220f, 4f, 8f), Is.EqualTo(0f));
            Assert.That(ExplosionMath.TinnitusSeconds(7f, 6f), Is.EqualTo(0f), "tinnitus only within 6 m");
            Assert.That(ExplosionMath.TinnitusSeconds(2f, 6f), Is.GreaterThan(0f));
            Assert.That(ExplosionMath.Trauma(1f, 8f), Is.GreaterThan(ExplosionMath.Trauma(15f, 8f)));
        }

        [Test]
        public void QuickThrowLeavesAboutThreeAndAHalfSeconds()
        {
            var t = new GrenadeThrower(WeaponTable.Frag);
            t.Begin();
            var thrownAt = -1f;
            var time = 0f;
            for (var i = 0; i < 120 && thrownAt < 0f; i++)
            {
                time += Dt;
                if (t.Tick(Dt, false, i == 0) == GrenadeThrower.Event.Throw) thrownAt = time;
            }
            var s = WeaponTable.Frag;
            Assert.That(thrownAt, Is.EqualTo(s.PinPullTime + s.ReleaseTime).Within(2f * Dt));
            Assert.That(t.ThrownFuse, Is.EqualTo(s.Fuse - thrownAt).Within(2f * Dt));
            Assert.That(t.Phase, Is.EqualTo(GrenadePhase.Recover));
        }

        [Test]
        public void CookingBurnsTheFuseAndTicks()
        {
            var t = new GrenadeThrower(WeaponTable.Frag);
            t.Begin();
            var ticks = 0;
            for (var i = 0; i < 90; i++) // 1.5 s
                if (t.Tick(Dt, true, false) == GrenadeThrower.Event.Tick) ticks++;
            Assert.That(ticks, Is.EqualTo(1), "a tick at 1 s");
            Assert.That(t.Phase, Is.EqualTo(GrenadePhase.Cooking));
            Assert.That(t.Cook01, Is.EqualTo(1.5f / 4f).Within(0.01f));
            GrenadeThrower.Event ev;
            var guard = 0;
            do ev = t.Tick(Dt, false, true);
            while (ev != GrenadeThrower.Event.Throw && ++guard < 100);
            Assert.That(ev, Is.EqualTo(GrenadeThrower.Event.Throw));
            Assert.That(t.ThrownFuse, Is.EqualTo(4f - 1.5f - WeaponTable.Frag.ReleaseTime).Within(0.05f));
        }

        [Test]
        public void HoldingTooLongExplodesInTheHand()
        {
            var t = new GrenadeThrower(WeaponTable.Frag);
            t.Begin();
            var time = 0f;
            var ev = GrenadeThrower.Event.None;
            while (time < 6f && ev != GrenadeThrower.Event.ExplodedInHand)
            {
                time += Dt;
                ev = t.Tick(Dt, true, false);
            }
            Assert.That(ev, Is.EqualTo(GrenadeThrower.Event.ExplodedInHand));
            Assert.That(time, Is.EqualTo(4.0f).Within(2f * Dt), "GDD §13.2: 4.0 s fuse from the pin pull");
        }

        [Test]
        public void ThrowSpeedReachesTheGddRange()
        {
            var t = new GrenadeThrower(WeaponTable.Frag);
            Assert.That(GrenadeBallistics.Range(t.ThrowSpeed, 45f, 1.65f, GrenadeBallistics.Gravity), Is.EqualTo(18f).Within(0.05f));
        }
    }
}
