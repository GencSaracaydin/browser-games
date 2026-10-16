using LongShadow.Player.View;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Head bob of GDD §12 (1.2 cm vertical, 0.8 cm lateral at walking pace, scaling with speed) and its stride clock.</summary>
    public class HeadBobTests
    {
        private const float Walk = 4.4f;
        private const float Sprint = 6.9f;

        private static Vector2 Peaks(HeadBob bob, float speed, bool grounded = true, float ads = 0f, bool crouched = false)
        {
            const float dt = 1f / 120f;
            for (var i = 0; i < 360; i++) bob.Step(dt, speed, grounded, Walk, Sprint, ads, crouched); // settle 3 s
            var lateral = 0f;
            var vertical = 0f;
            for (var i = 0; i < 240; i++)
            {
                bob.Step(dt, speed, grounded, Walk, Sprint, ads, crouched);
                lateral = Mathf.Max(lateral, Mathf.Abs(bob.Offset.x));
                vertical = Mathf.Max(vertical, Mathf.Abs(bob.Offset.y));
            }
            return new Vector2(lateral, vertical);
        }

        [Test]
        public void WalkingPaceAmplitudesMatchGdd12()
        {
            var p = Peaks(new HeadBob(), Walk);
            Assert.That(p.y, Is.InRange(0.0115f, 0.0121f), "1.2 cm vertical");
            Assert.That(p.x, Is.InRange(0.0076f, 0.0081f), "0.8 cm lateral");
        }

        [Test]
        public void AmplitudeScalesWithSpeedAndShrinksWhileAiming()
        {
            var sprint = Peaks(new HeadBob(), Sprint);
            Assert.That(sprint.y, Is.EqualTo(0.012f * Sprint / Walk).Within(0.0006f), "scales with speed");
            var crouch = Peaks(new HeadBob(), 2.4f, crouched: true);
            Assert.That(crouch.y, Is.LessThan(0.006f));
            var ads = Peaks(new HeadBob(), 2.6f, ads: 1f);
            Assert.That(ads.y, Is.LessThan(0.002f), "aiming steadies the view");
        }

        [Test]
        public void StrideClockGivesRealisticCadence()
        {
            var bob = new HeadBob();
            const float dt = 1f / 60f;
            var steps = 0;
            for (var i = 0; i < 600; i++)
            {
                bob.Step(dt, Walk, true, Walk, Sprint, 0f, false);
                steps += bob.Footfalls;
            }
            Assert.That(steps, Is.InRange(26, 28), "walk 4.4 m/s ≈ 2.7 steps/s (1.6 m strides)");
            Assert.That(HeadBob.StepFrequency(Sprint), Is.EqualTo(3.47f).Within(0.01f), "sprint ≈ 3.5 steps/s");
            Assert.That(HeadBob.StepFrequency(0.1f), Is.EqualTo(0f), "standing still makes no steps");
        }

        [Test]
        public void AirborneHasNoFootfallsAndSettles()
        {
            var bob = new HeadBob();
            Peaks(bob, Walk);
            var steps = 0;
            for (var i = 0; i < 120; i++)
            {
                bob.Step(1f / 60f, Walk, false, Walk, Sprint, 0f, false);
                steps += bob.Footfalls;
            }
            Assert.That(steps, Is.EqualTo(0));
            Assert.That(Mathf.Abs(bob.Offset.y), Is.LessThan(0.001f));
        }

        [Test]
        public void SprintRollsSubtly()
        {
            var bob = new HeadBob();
            var maxRoll = 0f;
            for (var i = 0; i < 600; i++)
            {
                bob.Step(1f / 120f, Sprint, true, Walk, Sprint, 0f, false);
                maxRoll = Mathf.Max(maxRoll, Mathf.Abs(bob.Roll));
            }
            Assert.That(maxRoll, Is.InRange(0.3f, bob.SprintRollDegrees + 1e-3f), "CINEMATIC_DIRECTION §4: subtle sprint roll");
        }
    }
}
