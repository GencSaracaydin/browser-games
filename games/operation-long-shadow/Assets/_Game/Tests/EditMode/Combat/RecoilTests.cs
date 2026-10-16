using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Combat.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §13.1 recoil: fixed first-N patterns reproduce exactly, randomized shots stay in range, recovery.</summary>
    public class RecoilTests
    {
        [Test]
        public void FixedPatternsIgnoreRandomness()
        {
            foreach (var w in WeaponTable.PlayerWeapons)
            {
                var r = w.Recoil;
                for (var i = 0; i < r.PatternLength; i++)
                {
                    var a = RecoilPattern.Kick(r, i, 0f, 0f);
                    var b = RecoilPattern.Kick(r, i, 0.999f, 0.999f);
                    Assert.That(a, Is.EqualTo(r.Pattern[i]), w.Id + " shot " + i);
                    Assert.That(b, Is.EqualTo(r.Pattern[i]), w.Id + " shot " + i);
                }
            }
        }

        [Test]
        public void CommandoPatternGoesUpThenRightThenHooksLeft()
        {
            var p = WeaponTable.Commando.Recoil.Pattern;
            Assert.That(p.Length, Is.EqualTo(8));
            Assert.That(p[0].x, Is.EqualTo(0.8f).Within(1e-5f), "first shot 0.8°");
            Assert.That(p[0].y, Is.EqualTo(0f));
            for (var i = 1; i < 5; i++) Assert.That(p[i].y, Is.GreaterThan(0f), "right at shot " + (i + 1));
            for (var i = 5; i < 8; i++) Assert.That(p[i].y, Is.LessThan(0f), "hooking left at shot " + (i + 1));
            for (var i = 1; i < 8; i++) Assert.That(p[i].x, Is.EqualTo(0.55f).Within(1e-5f), "0.55° vertical");
        }

        [Test]
        public void AkPatternIsUpLeftRightLeft()
        {
            var p = WeaponTable.AK.Recoil.Pattern;
            Assert.That(p.Length, Is.EqualTo(6));
            Assert.That(p[0].y, Is.EqualTo(0f), "up");
            Assert.That(p[1].y, Is.LessThan(0f), "left");
            Assert.That(p[2].y, Is.LessThan(0f), "left");
            Assert.That(p[3].y, Is.GreaterThan(0f), "right");
            Assert.That(p[4].y, Is.LessThan(0f), "left");
        }

        [Test]
        public void RpkClimbsSteeplyThenFlattensAndSettles()
        {
            var r = WeaponTable.RPK.Recoil;
            for (var i = 1; i < r.PatternLength; i++) Assert.That(r.Pattern[i].x, Is.LessThan(r.Pattern[i - 1].x), "flattening at " + i);
            Assert.That(r.Pattern[r.PatternLength - 1].x, Is.EqualTo(0.45f).Within(1e-5f));
            for (var k = 0; k < 50; k++)
            {
                var kick = RecoilPattern.Kick(r, 8 + k, Random.value, Random.value);
                Assert.That(kick.x, Is.InRange(0.45f - r.PitchRandom, 0.45f + r.PitchRandom), "settles to 0.45° after 8 shots");
                Assert.That(kick.y, Is.InRange(-0.25f, 0.25f), "±0.25");
            }
        }

        [Test]
        public void CommandoRandomPhaseDriftsRight()
        {
            var r = WeaponTable.Commando.Recoil;
            float sum = 0f;
            for (var k = 0; k < 200; k++)
            {
                var kick = RecoilPattern.Kick(r, 8 + k, (k * 0.37f) % 1f, (k * 0.61f) % 1f);
                Assert.That(kick.y, Is.InRange(0.12f - 0.18f, 0.12f + 0.18f));
                Assert.That(kick.x, Is.InRange(0.55f - r.PitchRandom, 0.55f + r.PitchRandom));
                sum += kick.y;
            }
            Assert.That(sum / 200f, Is.EqualTo(0.12f).Within(0.03f), "mean drift +0.12° right");
        }

        [Test]
        public void HushPuppySingleKick()
        {
            var r = WeaponTable.HushPuppy.Recoil;
            Assert.That(r.PatternLength, Is.EqualTo(0));
            var k = RecoilPattern.Kick(r, 0, 0.5f, 0f);
            Assert.That(k.x, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(k.y, Is.EqualTo(-0.3f).Within(1e-4f), "±0.3 at u = 0");
        }

        [Test]
        public void RecoveryWaitsForTheDelayThenReturnsAtTheRate()
        {
            var rec = new RecoilRecovery();
            rec.OnKick(5f, 0f);
            var c = rec.Step(0.05f, 0.08f, 8f);
            Assert.That(c, Is.EqualTo(Vector2.zero), "nothing before 0.08 s");
            var total = 0f;
            for (var i = 0; i < 30; i++) total += -rec.Step(1f / 60f, 0.08f, 8f).x; // 0.5 s
            // 0.05 + 0.5 s elapsed, 0.08 s delay: 0.47 s of recovery at 8°/s = 3.76°
            Assert.That(total, Is.EqualTo(3.76f).Within(0.02f));
            for (var i = 0; i < 120; i++) rec.Step(1f / 60f, 0.08f, 8f);
            Assert.That(rec.DebtPitch, Is.EqualTo(0f).Within(1e-4f), "never overshoots below the start");
        }

        [Test]
        public void PullingDownPaysTheDebt()
        {
            var rec = new RecoilRecovery();
            rec.OnKick(4f, 1f);
            rec.OnPlayerLook(-3f, 0f);
            Assert.That(rec.DebtPitch, Is.EqualTo(1f).Within(1e-5f));
            rec.OnPlayerLook(-5f, -2f);
            Assert.That(rec.DebtPitch, Is.EqualTo(0f));
            Assert.That(rec.DebtYaw, Is.EqualTo(0f));
            rec.OnPlayerLook(3f, 0f);
            Assert.That(rec.DebtPitch, Is.EqualTo(0f), "looking with the recoil changes nothing");
        }

        [Test]
        public void HeldTriggerReplaysThePatternThroughTheRuntime()
        {
            var w = WeaponTable.Commando;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var input = new WeaponInput { TriggerHeld = true, SinceSprintEnd = 10f, Grounded = true, AimHeld = true };
            var kicks = new System.Collections.Generic.List<Vector2>();
            for (var f = 0; f < 60 && kicks.Count < 10; f++)
            {
                var frame = rt.Tick(1f / 60f, input);
                for (var s = 0; s < frame.Shots; s++) kicks.Add(rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f).Kick);
            }
            Assert.That(kicks.Count, Is.GreaterThanOrEqualTo(8));
            for (var i = 0; i < 8; i++) Assert.That(kicks[i], Is.EqualTo(w.Recoil.Pattern[i]), "shot " + (i + 1));
        }

        [Test]
        public void ReleasingTheTriggerRestartsThePattern()
        {
            var w = WeaponTable.AK;
            var rt = new WeaponRuntime(w, WeaponAmmo.Full(w));
            var hold = new WeaponInput { TriggerHeld = true, TriggerDown = true, SinceSprintEnd = 10f, Grounded = true };
            var idle = new WeaponInput { SinceSprintEnd = 10f, Grounded = true };
            var shots = 0;
            for (var f = 0; f < 30; f++)
            {
                var fr = rt.Tick(1f / 60f, hold);
                for (var s = 0; s < fr.Shots; s++, shots++) rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f);
            }
            Assert.That(rt.BurstShots, Is.GreaterThan(3));
            for (var f = 0; f < 30; f++) rt.Tick(1f / 60f, idle); // 0.5 s > burst reset
            var first = default(ShotSolution);
            for (var f = 0; f < 5; f++)
            {
                var fr = rt.Tick(1f / 60f, hold);
                if (fr.Shots > 0)
                {
                    first = rt.TakeShot(0.5f, 0.5f, 0.5f, 0.5f);
                    break;
                }
            }
            Assert.That(first.BurstIndex, Is.EqualTo(0));
            Assert.That(first.Kick, Is.EqualTo(w.Recoil.Pattern[0]));
        }
    }
}
