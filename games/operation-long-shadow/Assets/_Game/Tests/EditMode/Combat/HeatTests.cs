using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.3 door-gun heat: +1 per shot, overheat at 55 (about 6 s), 2.0 s recovery, 1.0 s idle cool-down.</summary>
    public class HeatTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void FiftyFifthShotOverheats()
        {
            var s = WeaponTable.DoorGunMount;
            var h = new HeatModel();
            for (var i = 1; i < 55; i++) Assert.That(h.OnShot(s), Is.False, "shot " + i);
            Assert.That(h.OnShot(s), Is.True);
            Assert.That(h.Overheated, Is.True);
            Assert.That(h.Heat01(s), Is.EqualTo(1f));
            Assert.That(h.OnShot(s), Is.False, "no shots while overheated");
        }

        [Test]
        public void OverheatRecoversAfterTwoSeconds()
        {
            var s = WeaponTable.DoorGunMount;
            var h = new HeatModel();
            for (var i = 0; i < 55; i++) h.OnShot(s);
            var t = 0f;
            while (h.Overheated && t < 5f)
            {
                h.Tick(s, Dt, true);
                t += Dt;
            }
            Assert.That(t, Is.EqualTo(2.0f).Within(Dt + 1e-4f));
            Assert.That(h.Heat, Is.EqualTo(0f));
        }

        [Test]
        public void IdleCoolsFromFullInOneSecond()
        {
            var s = WeaponTable.DoorGunMount;
            var h = new HeatModel();
            for (var i = 0; i < 54; i++) h.OnShot(s);
            var t = 0f;
            while (h.Heat > 0f && t < 5f)
            {
                h.Tick(s, Dt, false);
                t += Dt;
            }
            // 54/55 of the full cool-down after the idle delay.
            Assert.That(t, Is.EqualTo(s.IdleDelay + 54f / 55f * s.IdleRecovery).Within(2f * Dt));
        }

        [Test]
        public void SustainedFireOverheatsInAboutSixSeconds()
        {
            var w = WeaponTable.DoorGun;
            var rt = new WeaponRuntime(w, null, WeaponTable.DoorGunMount);
            var input = new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true };
            var t = 0f;
            var shots = 0;
            var overheatAt = -1f;
            for (var i = 0; i < 60 * 12; i++)
            {
                var f = rt.Tick(Dt, input);
                t += Dt;
                for (var k = 0; k < f.Shots; k++)
                {
                    var s = rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f);
                    shots++;
                    if (s.Overheated && overheatAt < 0f) overheatAt = t;
                }
                if (overheatAt > 0f) break;
            }
            Assert.That(shots, Is.EqualTo(55));
            Assert.That(overheatAt, Is.EqualTo(54f * 60f / 550f).Within(0.05f), "≈ 5.9 s at 550 RPM (GDD: about 6 s)");
            var firedWhileHot = 0;
            for (var i = 0; i < 60; i++) firedWhileHot += rt.Tick(Dt, input).Shots;
            Assert.That(firedWhileHot, Is.EqualTo(0));
            Assert.That(rt.Overheated, Is.True);
            var cooled = false;
            for (var i = 0; i < 90 && !cooled; i++) cooled |= rt.Tick(Dt, input).Cooled;
            Assert.That(cooled, Is.True, "usable again 2.0 s after the overheat");
        }

        [Test]
        public void DoorGunHasNoAmmunitionAndRedTracersEveryThird()
        {
            var rt = new WeaponRuntime(WeaponTable.DoorGun, null, WeaponTable.DoorGunMount);
            var tracers = 0;
            for (var i = 0; i < 30; i++) tracers += rt.TakeShot(0f, 0f, 0f, 0f).Tracer ? 1 : 0;
            Assert.That(tracers, Is.EqualTo(10));
        }
    }
}
