using LongShadow.Characters.Body;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class LocomotionTests
    {
        [Test]
        public void FacesTheTravelDirectionWhenNotAiming()
        {
            var d = new LocomotionDriver();
            var yaw = 0f;
            for (var i = 0; i < 120; i++) yaw = d.Step(1f / 60f, yaw, new Vector3(3f, 0f, 0f), null, null);
            Assert.That(Mathf.DeltaAngle(yaw, 90f), Is.EqualTo(0f).Within(0.5f));
            Assert.That(d.LocalVelocity.y, Is.EqualTo(3f).Within(0.05f), "moving forward in the body frame");
            Assert.That(d.LocalVelocity.x, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void StrafesWhileAiming()
        {
            var d = new LocomotionDriver();
            var yaw = 0f;
            // Aim north, move east: the body keeps facing the aim and strafes right.
            for (var i = 0; i < 120; i++) yaw = d.Step(1f / 60f, yaw, new Vector3(1.3f, 0f, 0f), Vector3.forward * 10f, null);
            Assert.That(Mathf.DeltaAngle(yaw, 0f), Is.EqualTo(0f).Within(0.5f));
            Assert.That(d.LocalVelocity.x, Is.EqualTo(1.3f).Within(0.05f));
            Assert.That(d.LocalVelocity.y, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void SmallAimErrorsAreLeftToTheSpineThenTheFeetTurn()
        {
            var d = new LocomotionDriver();
            var yaw = d.Step(1f / 60f, 0f, Vector3.zero, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward, null);
            Assert.That(yaw, Is.EqualTo(0f).Within(1e-3f), "30° is inside the turn-start hysteresis");
            Assert.That(d.TurningInPlace, Is.False);
            yaw = d.Step(1f / 60f, 0f, Vector3.zero, Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, null);
            Assert.That(d.TurningInPlace, Is.True);
            Assert.That(yaw, Is.GreaterThan(0f));
            for (var i = 0; i < 120; i++) yaw = d.Step(1f / 60f, yaw, Vector3.zero, Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, null);
            Assert.That(Mathf.DeltaAngle(yaw, 90f), Is.EqualTo(0f).Within(d.TurnStopAngle + 0.1f));
            Assert.That(d.TurningInPlace, Is.False);
        }

        [Test]
        public void PlaybackSpeedsUpOnlyBeyondTheFastestClip()
        {
            var d = new LocomotionDriver { MaxNativeSpeed = 6f, MaxPlaybackScale = 1.35f };
            Assert.That(d.PlaybackScale(3f), Is.EqualTo(1f));
            Assert.That(d.PlaybackScale(6.9f), Is.EqualTo(1.15f).Within(1e-4f));
            Assert.That(d.PlaybackScale(20f), Is.EqualTo(1.35f));
        }

        [Test]
        public void FootstepClockFiresOncePerPlant()
        {
            var c = new FootstepClock();
            Assert.That(c.Step(0.9f, 2f), Is.Null, "first call primes the phase");
            Assert.That(c.Step(0.05f, 2f), Is.EqualTo(LongShadow.Core.AnimEventIds.FootL), "wrapping past 0 plants the left foot");
            Assert.That(c.Step(0.3f, 2f), Is.Null);
            Assert.That(c.Step(0.55f, 2f), Is.EqualTo(LongShadow.Core.AnimEventIds.FootR));
            Assert.That(c.Step(0.6f, 0.1f), Is.Null, "no steps when standing");
            Assert.That(FootstepClock.Crossed(0.9f, 0.1f, 0.95f), Is.True);
            Assert.That(FootstepClock.Crossed(0.2f, 0.2f, 0.2f), Is.False);
        }
    }
}
