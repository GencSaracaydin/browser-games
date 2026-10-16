using LongShadow.Core;
using LongShadow.Player.Movement;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>GDD §12 movement numbers through <see cref="MovementMath"/> (TECH_PLAN §11.4 WP02 tests).</summary>
    public class MovementMathTests
    {
        private MotorSettings _s;

        [SetUp]
        public void SetUp() => _s = new MotorSettings();

        [Test]
        public void GroundSpeedsMatchGdd12()
        {
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Stand, 0f, 1f, 0f), Is.EqualTo(4.4f).Within(1e-4f), "walk");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Stand, 1f, 1f, 0f), Is.EqualTo(2.6f).Within(1e-4f), "ADS walk");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Stand, 0.5f, 1f, 0f), Is.EqualTo(3.5f).Within(1e-4f), "half ADS blends");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Crouch, 0f, 1f, 0f), Is.EqualTo(2.4f).Within(1e-4f), "crouch");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Crouch, 1f, 1f, 0f), Is.EqualTo(2.4f).Within(1e-4f), "crouch + ADS stays 2.4");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Sprint, 0f, 1f, 0f), Is.EqualTo(6.9f).Within(1e-4f), "sprint");
        }

        [Test]
        public void WeaponMultiplierAndForcedWalkApply()
        {
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Stand, 0f, 0.9f, 0f), Is.EqualTo(3.96f).Within(1e-4f));
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Sprint, 0f, 1f, 1.2f), Is.EqualTo(1.2f).Within(1e-4f), "MID-A forced walk caps sprint");
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Crouch, 0f, 1f, 1.2f), Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(MovementMath.GroundSpeed(_s, Stance.Stand, 0f, 50f, 0f), Is.EqualTo(8.8f).Within(1e-4f), "multiplier clamped to 2");
        }

        [Test]
        public void BackpedalIsSlowerStrafeIsNot()
        {
            Assert.That(MovementMath.DirectionalFactor(new Vector2(0f, 1f), 0.8f), Is.EqualTo(1f));
            Assert.That(MovementMath.DirectionalFactor(new Vector2(1f, 0f), 0.8f), Is.EqualTo(1f));
            Assert.That(MovementMath.DirectionalFactor(new Vector2(0f, -1f), 0.8f), Is.EqualTo(0.8f).Within(1e-5f));
            var diag = new Vector2(1f, -1f).normalized;
            Assert.That(MovementMath.DirectionalFactor(diag, 0.8f), Is.EqualTo(Mathf.Lerp(1f, 0.8f, 0.70710677f)).Within(1e-4f));
        }

        [Test]
        public void WishDirectionFollowsCompassHeading()
        {
            AssertVec(MovementMath.WishDirection(new Vector2(0f, 1f), 0f), new Vector3(0f, 0f, 1f));
            AssertVec(MovementMath.WishDirection(new Vector2(0f, 1f), 90f), new Vector3(1f, 0f, 0f));
            AssertVec(MovementMath.WishDirection(new Vector2(1f, 0f), 90f), new Vector3(0f, 0f, -1f));
            AssertVec(MovementMath.WishDirection(new Vector2(3f, 4f), 0f), new Vector3(0.6f, 0f, 0.8f));
        }

        [Test]
        public void SprintNeedsForwardInput()
        {
            Assert.IsTrue(MovementMath.IsSprintInput(new Vector2(0f, 1f)));
            Assert.IsTrue(MovementMath.IsSprintInput(new Vector2(1f, 1f).normalized));
            Assert.IsFalse(MovementMath.IsSprintInput(new Vector2(1f, 0f)));
            Assert.IsFalse(MovementMath.IsSprintInput(new Vector2(0f, -1f)));
            Assert.IsFalse(MovementMath.IsSprintInput(new Vector2(0.9f, 0.44f)));
        }

        [Test]
        public void AccelerationIsSnappyThenWeighty()
        {
            // 0 -> walk in about 0.15 s, walk -> sprint in about 0.25 s (feel values, DECISIONS WP02).
            var v = Vector3.zero;
            var target = new Vector3(0f, 0f, 4.4f);
            var t = 0f;
            const float dt = 1f / 60f;
            while (v.magnitude < 4.4f * 0.95f && t < 2f)
            {
                v = MovementMath.Accelerate(v, target, _s, 1f, dt);
                t += dt;
            }
            Assert.That(t, Is.LessThanOrEqualTo(0.16f), "0 to 95 % walk");
            target = new Vector3(0f, 0f, 6.9f);
            t = 0f;
            while (v.magnitude < 6.9f - 1e-3f && t < 2f)
            {
                v = MovementMath.Accelerate(v, target, _s, 1f, dt);
                t += dt;
            }
            Assert.That(t, Is.InRange(0.2f, 0.3f), "walk to sprint");
        }

        [Test]
        public void AirKeepsMomentumWithoutInput()
        {
            var v = new Vector3(0f, 0f, 6.9f);
            Assert.That(MovementMath.AirAccelerate(v, Vector3.zero, 6.9f, 4f, 0.1f), Is.EqualTo(v));
            var steered = MovementMath.AirAccelerate(v, Vector3.right, 6.9f, 4f, 0.1f);
            Assert.That(steered.x, Is.GreaterThan(0f));
            Assert.That(steered.x, Is.LessThanOrEqualTo(0.41f), "air control is limited");
        }

        [Test]
        public void JumpApexIsOneMetre()
        {
            var v = MovementMath.JumpVelocity(_s.JumpApexHeight, _s.Gravity);
            Assert.That(v, Is.EqualTo(Mathf.Sqrt(2f * _s.Gravity * 1f)).Within(1e-4f));
            Assert.That(MovementMath.ApexHeight(v, _s.Gravity), Is.EqualTo(1f).Within(1e-4f), "GDD §12: apex 1.0 m");
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        [TestCase(0.05f)]
        public void IntegratedJumpPeaksAtOneMetreAtAnyFrameRate(float dt)
        {
            var v = MovementMath.JumpVelocity(1f, _s.Gravity);
            var y = 0f;
            var max = 0f;
            for (var i = 0; i < 200 && (i == 0 || y > 0f); i++)
            {
                y += MovementMath.IntegrateVertical(ref v, _s.Gravity, _s.TerminalVelocity, dt);
                max = Mathf.Max(max, y);
            }
            Assert.That(max, Is.LessThanOrEqualTo(1f + 1e-4f), "never above the apex");
            Assert.That(max, Is.GreaterThanOrEqualTo(1f - _s.Gravity * dt * dt / 8f - 1e-4f), "sampled apex error is at most g dt^2 / 8");
        }

        [Test]
        public void TerminalVelocityCapsFalls()
        {
            var v = 0f;
            for (var i = 0; i < 600; i++) MovementMath.IntegrateVertical(ref v, _s.Gravity, _s.TerminalVelocity, 1f / 60f);
            Assert.That(v, Is.EqualTo(-_s.TerminalVelocity).Within(1e-4f));
        }

        [Test]
        public void SlideCurveRunsEightToFourOverPoint75Seconds()
        {
            Assert.That(MovementMath.SlideSpeed(_s, 0f), Is.EqualTo(8f).Within(1e-4f), "GDD §12: 8.0 at entry");
            Assert.That(MovementMath.SlideSpeed(_s, 0.75f), Is.EqualTo(4f).Within(1e-4f), "GDD §12: 4.0 at the end");
            Assert.That(MovementMath.SlideSpeed(_s, 2f), Is.EqualTo(4f).Within(1e-4f), "clamped after the end");
            var prev = float.MaxValue;
            var distance = 0f;
            const float dt = 1f / 240f;
            for (var t = 0f; t <= 0.75f + 1e-5f; t += dt)
            {
                var v = MovementMath.SlideSpeed(_s, t);
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-5f), "monotonic");
                prev = v;
                distance += v * dt;
            }
            Assert.That(MovementMath.SlideSpeed(_s, 0.375f), Is.LessThan(6f), "ease-out: friction bites hardest early");
            Assert.That(distance, Is.InRange(4.0f, 4.3f), "about 4.15 m per slide");
        }

        [Test]
        public void SlideStartsFromSprintSpeedOnly()
        {
            Assert.IsTrue(MovementMath.CanStartSlide(5.5f, _s), "GDD §12: >= 5.5 m/s");
            Assert.IsTrue(MovementMath.CanStartSlide(6.9f, _s));
            Assert.IsFalse(MovementMath.CanStartSlide(5.49f, _s));
            Assert.IsFalse(MovementMath.CanStartSlide(4.4f, _s), "walking cannot slide");
        }

        [TestCase(0f, 0f)]
        [TestCase(4.4f, 0f)]
        [TestCase(4.5f, 0f)]
        [TestCase(5.5f, 30f)]
        [TestCase(6f, 45f)]
        [TestCase(7.5f, 90f)]
        public void FallDamageAboveFourPointFiveMetres(float height, float hp)
        {
            Assert.That(MovementMath.FallDamage(height, _s), Is.EqualTo(hp).Within(1e-3f), "GDD §12: none below 4.5 m, then 30 HP per metre");
        }

        [TestCase(0.35f, LedgeKind.None)]
        [TestCase(0.59f, LedgeKind.None)]
        [TestCase(0.6f, LedgeKind.Vault)]
        [TestCase(1.0f, LedgeKind.Vault)]
        [TestCase(1.4f, LedgeKind.Vault)]
        [TestCase(1.41f, LedgeKind.Climb)]
        [TestCase(1.8f, LedgeKind.Climb)]
        [TestCase(2.1f, LedgeKind.Climb)]
        [TestCase(2.2f, LedgeKind.None)]
        public void LedgeRangesMatchGdd12(float top, LedgeKind expected)
        {
            Assert.That(MovementMath.ClassifyGroundLedge(top, _s), Is.EqualTo(expected));
        }

        [Test]
        public void AirMantleReachAndHeight()
        {
            Assert.IsTrue(MovementMath.IsAirMantle(1.0f, 0.5f, _s));
            Assert.IsTrue(MovementMath.IsAirMantle(1.4f, 0.8f, _s), "GDD §12: 0-1.4 m above the feet, <= 0.8 m ahead");
            Assert.IsFalse(MovementMath.IsAirMantle(1.5f, 0.5f, _s));
            Assert.IsFalse(MovementMath.IsAirMantle(1.0f, 0.9f, _s));
            Assert.IsFalse(MovementMath.IsAirMantle(0.05f, 0.5f, _s), "a top at the feet is a landing, not a mantle");
        }

        [Test]
        public void TraversalDurations()
        {
            Assert.That(MovementMath.TraversalDuration(LedgeKind.Vault, _s), Is.EqualTo(0.45f));
            Assert.That(MovementMath.TraversalDuration(LedgeKind.Climb, _s), Is.EqualTo(0.8f));
            Assert.That(MovementMath.TraversalDuration(LedgeKind.AirMantle, _s), Is.EqualTo(0.5f));
            Assert.That(MovementMath.TraversalDuration(LedgeKind.None, _s), Is.EqualTo(0f));
        }

        [Test]
        public void EyeAndCapsuleHeights()
        {
            Assert.That(MovementMath.EyeHeight(Stance.Stand, false, _s), Is.EqualTo(1.65f), "GDD §12");
            Assert.That(MovementMath.EyeHeight(Stance.Crouch, true, _s), Is.EqualTo(1.05f), "GDD §12");
            Assert.That(MovementMath.EyeHeight(Stance.Slide, true, _s), Is.EqualTo(1.05f).Within(1e-5f), "GDD §12: 0.6 m dip");
            Assert.That(MovementMath.EyeHeight(Stance.Airborne, true, _s), Is.EqualTo(1.05f));
            Assert.That(MovementMath.CapsuleHeight(false, _s), Is.EqualTo(1.8f));
            Assert.That(MovementMath.CapsuleHeight(true, _s), Is.EqualTo(1.1f));
        }

        [Test]
        public void SlopeLimitIsFortyFiveDegrees()
        {
            Assert.IsTrue(MovementMath.IsWalkable(Quaternion.Euler(44f, 0f, 0f) * Vector3.up, 45f));
            Assert.IsTrue(MovementMath.IsWalkable(Quaternion.Euler(45f, 0f, 0f) * Vector3.up, 45f));
            Assert.IsFalse(MovementMath.IsWalkable(Quaternion.Euler(50f, 0f, 0f) * Vector3.up, 45f));
        }

        private static void AssertVec(Vector3 actual, Vector3 expected) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(1e-4f), actual + " != " + expected);
    }
}
