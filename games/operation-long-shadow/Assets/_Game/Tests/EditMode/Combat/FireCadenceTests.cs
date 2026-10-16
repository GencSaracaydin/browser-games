using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>RPM cadence at any frame rate, the semi-auto cap, sprint-out, magazine limits and ADS timing.</summary>
    public class FireCadenceTests
    {
        private static int ShotsOverSeconds(WeaponRuntime rt, float seconds, float dt, WeaponInput input)
        {
            var shots = 0;
            for (var t = 0f; t < seconds - 1e-5f; t += dt)
            {
                var f = rt.Tick(dt, input);
                for (var k = 0; k < f.Shots; k++, shots++) rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f);
            }
            return shots;
        }

        [TestCase(60f)]
        [TestCase(30f)]
        [TestCase(144f)]
        public void AutomaticCadenceIsFrameRateIndependent(float fps)
        {
            var w = WeaponTable.RPK; // 600 RPM, 75 rounds
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var shots = ShotsOverSeconds(rt, 5f, 1f / fps, new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(shots, Is.InRange(50, 51), "600 RPM = 10 rounds/s");
        }

        [Test]
        public void MagazineLimitsTheBurst()
        {
            var w = WeaponTable.Commando;
            var rt = new WeaponRuntime(w, new WeaponAmmo(w.Id, 30, 0, 30, 0));
            var shots = ShotsOverSeconds(rt, 5f, 1f / 60f, new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(shots, Is.EqualTo(30));
        }

        [Test]
        public void SemiAutoIsCappedAt300Rpm()
        {
            var w = WeaponTable.HushPuppy;
            var rt = new WeaponRuntime(w, new WeaponAmmo(w.Id, 8, 48, 8, 48));
            var shots = 0;
            // Clicking every 3rd frame at 60 fps (20 clicks/s) for 1 s: capped at 5 rounds/s.
            for (var i = 0; i < 60; i++)
            {
                var press = i % 3 == 0;
                var f = rt.Tick(1f / 60f, new WeaponInput { TriggerDown = press, TriggerHeld = press, SinceSprintEnd = 10f, Grounded = true });
                for (var k = 0; k < f.Shots; k++, shots++) rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f);
            }
            Assert.That(shots, Is.InRange(5, 6));
        }

        [Test]
        public void SemiAutoNeedsAPressPerShot()
        {
            var w = WeaponTable.HushPuppy;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var shots = ShotsOverSeconds(rt, 1f, 1f / 60f, new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(shots, Is.EqualTo(0), "holding without a press never fires");
        }

        [TestCase(WeaponIds.Commando, 0.20f)]
        [TestCase(WeaponIds.RPK, 0.32f)]
        public void SprintOutDelaysTheFirstShot(string id, float sprintOut)
        {
            var w = WeaponTable.Get(id);
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            const float dt = 1f / 120f;
            var since = 0f;
            var firstShot = -1f;
            for (var i = 0; i < 120 && firstShot < 0f; i++)
            {
                since += dt;
                var f = rt.Tick(dt, new WeaponInput { TriggerHeld = true, SinceSprintEnd = since, Grounded = true });
                if (f.Shots > 0) firstShot = since;
            }
            Assert.That(firstShot, Is.EqualTo(sprintOut).Within(2f * dt));
        }

        [Test]
        public void NoFireWhileSprinting()
        {
            var w = WeaponTable.AK;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var shots = ShotsOverSeconds(rt, 1f, 1f / 60f, new WeaponInput { TriggerHeld = true, Sprinting = true, Grounded = true });
            Assert.That(shots, Is.EqualTo(0));
        }

        [TestCase(WeaponIds.Commando)]
        [TestCase(WeaponIds.RPK)]
        public void AdsBlendTakesTheAdsTime(string id)
        {
            var w = WeaponTable.Get(id);
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            const float dt = 1f / 240f;
            var t = 0f;
            var f = rt.Tick(dt, new WeaponInput { AimHeld = true, SinceSprintEnd = 10f, Grounded = true });
            Assert.That(f.AdsStarted, Is.True);
            t += dt;
            while (rt.AdsBlend < 1f && t < 2f)
            {
                rt.Tick(dt, new WeaponInput { AimHeld = true, SinceSprintEnd = 10f, Grounded = true });
                t += dt;
            }
            Assert.That(t, Is.EqualTo(w.AdsTime).Within(2f * dt));
        }

        [Test]
        public void AimingWhileSprintingReportsIntentButDoesNotBlend()
        {
            var w = WeaponTable.AK;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            rt.Tick(0.1f, new WeaponInput { AimHeld = true, Sprinting = true, Grounded = true });
            Assert.That(rt.AdsIntent, Is.True, "the motor ends the sprint on ADS intent");
            Assert.That(rt.AdsBlend, Is.EqualTo(0f));
        }

        [Test]
        public void PlayerTracersEveryFifthRound()
        {
            var w = WeaponTable.Commando;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var pattern = "";
            for (var i = 0; i < 10; i++) pattern += rt.TakeShot(0f, 0f, 0f, 0f).Tracer ? "T" : ".";
            Assert.That(pattern, Is.EqualTo("....T....T"));
        }

        [Test]
        public void LongFramesReleaseAtMostThreeShots()
        {
            var c = new FireCadence();
            c.Reset(0f);
            Assert.That(c.TickAuto(0f, 0.08f, true), Is.EqualTo(1));
            Assert.That(c.TickAuto(0.5f, 0.08f, true), Is.EqualTo(FireCadence.MaxShotsPerFrame));
        }
    }
}
