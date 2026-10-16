using LongShadow.AI.Common;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>GDD §14.2 perception numbers, the Beat 2 sentry attention, the 24-ray budget and round-robin scheduling.</summary>
    public class PerceptionTests
    {
        [Test]
        public void SightRangeByStageAndLitZones()
        {
            Assert.That(PerceptionMath.StageRange(StageId.Golden), Is.EqualTo(70f));
            Assert.That(PerceptionMath.StageRange(StageId.Sunset), Is.EqualTo(60f));
            Assert.That(PerceptionMath.StageRange(StageId.Afterglow), Is.EqualTo(45f));
            Assert.That(PerceptionMath.StageRange(StageId.BlueHour), Is.EqualTo(35f));
            Assert.That(PerceptionMath.EffectiveRange(35f, true), Is.EqualTo(70f), "doubled in a flood/flare");
            Assert.That(PerceptionMath.EffectiveRange(45f, true), Is.EqualTo(70f), "capped at 70 m");
            Assert.That(PerceptionMath.EffectiveRange(70f, true), Is.EqualTo(70f));
            Assert.That(PerceptionMath.EffectiveRange(35f, false), Is.EqualTo(35f));
            Assert.That(PerceptionMath.EffectiveRange(35f, false, 160f), Is.EqualTo(160f), "marksman scope");
        }

        [Test]
        public void FieldOfViewWidensUpClose()
        {
            var fwd = Vector3.forward;
            var at70 = MathX.DirectionFromBearing(70f);
            Assert.IsFalse(PerceptionMath.InFov(fwd, at70 * 20f, 20f), "70° off at 20 m is outside 120°");
            Assert.IsTrue(PerceptionMath.InFov(fwd, at70 * 6f, 6f), "inside 160° within 8 m");
            var at59 = MathX.DirectionFromBearing(59f);
            Assert.IsTrue(PerceptionMath.InFov(fwd, at59 * 30f, 30f));
            Assert.IsFalse(PerceptionMath.InFov(fwd, -fwd * 3f, 3f), "never behind");
        }

        [Test]
        public void DetectionTimesAndStanceFactors()
        {
            Assert.That(PerceptionMath.DetectionSeconds(2f, 70f, Stance.Stand), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(PerceptionMath.DetectionSeconds(70f, 70f, Stance.Stand), Is.EqualTo(1.2f).Within(1e-4f));
            var mid = PerceptionMath.DetectionSeconds(30f, 70f, Stance.Stand);
            Assert.That(mid, Is.InRange(0.4f, 1.2f));
            Assert.That(PerceptionMath.DetectionSeconds(30f, 70f, Stance.Crouch), Is.EqualTo(mid * 1.5f).Within(1e-4f));
            Assert.That(PerceptionMath.DetectionSeconds(30f, 70f, Stance.Sprint), Is.EqualTo(mid * 0.6f).Within(1e-4f));
        }

        [Test]
        public void HearingRadiiAndScale()
        {
            Assert.That(PerceptionMath.HearShot, Is.EqualTo(60f));
            Assert.That(PerceptionMath.HearSuppressedShot, Is.EqualTo(6f));
            Assert.That(PerceptionMath.HearFrag, Is.EqualTo(80f));
            Assert.That(PerceptionMath.HearSquadGunfire, Is.EqualTo(40f));
            Assert.That(PerceptionMath.HearSprint, Is.EqualTo(12f));
            Assert.That(PerceptionMath.HearWalk, Is.EqualTo(4f));
            Assert.That(PerceptionMath.HearCrouch, Is.EqualTo(1.5f));
            var shot = new NoiseEvent(Vector3.zero, 60f, Team.Player, NoiseKind.Gunshot);
            Assert.IsTrue(PerceptionMath.CanHear(shot, new Vector3(59f, 0f, 0f), 1f));
            Assert.IsFalse(PerceptionMath.CanHear(shot, new Vector3(61f, 0f, 0f), 1f));
            Assert.IsFalse(PerceptionMath.CanHear(shot, new Vector3(50f, 0f, 0f), 0.8f), "HearingScale shrinks radii");
            var pistol = new NoiseEvent(Vector3.zero, 6f, Team.Player, NoiseKind.SuppressedShot);
            Assert.IsFalse(PerceptionMath.CanHear(pistol, new Vector3(8f, 0f, 0f), 1f), "Hush Puppy at 8 m is silent");
            Assert.IsTrue(PerceptionMath.IsAlarming(NoiseKind.Gunshot));
            Assert.IsFalse(PerceptionMath.IsAlarming(NoiseKind.Footstep));
        }

        [Test]
        public void SmokingSentriesAreDistracted()
        {
            Assert.That(SentryAttention.Scale(16f, MotionClass.Still, true), Is.EqualTo(0f), "still and crouched at the firing position");
            Assert.That(SentryAttention.Scale(16f, MotionClass.Walk, true), Is.EqualTo(SentryAttention.DetectionScale));
            Assert.That(SentryAttention.Scale(6f, MotionClass.Still, true), Is.EqualTo(1f), "inside 8 m nobody hides");
            Assert.That(SentryAttention.RangeCap, Is.LessThan(PerceptionMath.GoldenRange));
        }

        [Test]
        public void LosBudgetNeverExceeds24RaysPerFrame()
        {
            var b = new LosBudget();
            Assert.That(b.PerFrame, Is.EqualTo(24));
            Assert.IsTrue(b.TryUse(1, 20));
            Assert.IsFalse(b.TryUse(1, 5));
            Assert.IsTrue(b.TryUse(1, 4));
            Assert.IsFalse(b.TryUse(1, 1));
            Assert.IsTrue(b.TryUse(2, 24), "new frame, new budget");
            Assert.That(b.PeakPerFrame, Is.EqualTo(24));
        }

        private sealed class FakeAgent : IPerceptionAgent
        {
            public int Rays;
            public float Next;
            public int Runs;
            public bool PerceptionDue(float now) => now >= Next;
            public int PreparePerception(float now) => Rays;
            public void RunPerception(float now)
            {
                Runs++;
                Next = now + Perception.Interval;
            }
        }

        [Test]
        public void SchedulerIsFairAndWithinBudget()
        {
            var s = new PerceptionScheduler();
            var budget = new LosBudget();
            var agents = new FakeAgent[15];
            for (var i = 0; i < agents.Length; i++)
            {
                agents[i] = new FakeAgent { Rays = 3 };
                s.Add(agents[i]);
            }
            var now = 0f;
            for (var frame = 1; frame <= 600; frame++)
            {
                now += 1f / 60f;
                var used = s.Run(budget, frame, now);
                Assert.That(used, Is.LessThanOrEqualTo(24), "TECH_PLAN §7.6: ≤ 24 rays per frame");
            }
            foreach (var a in agents)
                Assert.That(a.Runs, Is.InRange(80, 101), "every agent gets ~10 Hz (10 s)");
            s.Remove(agents[3]);
            var before = agents[3].Runs;
            for (var frame = 601; frame <= 700; frame++)
            {
                now += 1f / 60f;
                s.Run(budget, frame, now);
            }
            Assert.That(agents[3].Runs, Is.EqualTo(before));
        }

        [Test]
        public void AlertAndRunCommandsParse()
        {
            Assert.IsTrue(AIEvents.TryParseAlert("AI.Alert.E2.Gate", out var e, out var w));
            Assert.That(e, Is.EqualTo("E2"));
            Assert.That(w, Is.EqualTo("Gate"));
            Assert.IsTrue(AIEvents.TryParseAlert("AI.Alert.E6", out e, out w));
            Assert.That(e, Is.EqualTo("E6"));
            Assert.IsNull(w);
            Assert.IsFalse(AIEvents.TryParseAlert("MIDA.Start", out _, out _));
            Assert.IsTrue(AIEvents.TryParseRun("AI.Run.E2-01", out var id));
            Assert.That(id, Is.EqualTo("E2-01"));
        }
    }
}
