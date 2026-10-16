using LongShadow.Core;
using LongShadow.Player.View;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Mouse-look formula of GDD §11 (sensitivity) and TECH_PLAN §7.3 (ADS × FOV ratio, ±85° pitch, limits).</summary>
    public class LookMathTests
    {
        [Test]
        public void SensitivityThreeGivesNineDegreesPerHundredCssPixels()
        {
            Assert.That(LookMath.DegreesPerPixel(3f), Is.EqualTo(0.09f).Within(1e-6f), "GDD §11: deg = px × sens × 0.03");
            var d = LookMath.LookDegrees(new Vector2(100f, 0f), 3f, false, 1f);
            Assert.That(d.x, Is.EqualTo(9f).Within(1e-4f));
            Assert.That(d.y, Is.EqualTo(0f));
            Assert.That(LookMath.LookDegrees(new Vector2(0f, 100f), 10f, false, 1f).y, Is.EqualTo(30f).Within(1e-4f), "max sensitivity");
            Assert.That(LookMath.LookDegrees(new Vector2(0f, 100f), 0.1f, false, 1f).y, Is.EqualTo(0.3f).Within(1e-5f), "min sensitivity");
        }

        [Test]
        public void InvertYFlipsPitchOnly()
        {
            var d = LookMath.LookDegrees(new Vector2(50f, 50f), 2f, true, 1f);
            Assert.That(d.x, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(d.y, Is.EqualTo(-3f).Within(1e-5f));
        }

        [Test]
        public void AdsScalesByAdsSensitivityAndFovRatio()
        {
            const float hip = 58.72f;
            const float ads = 30f;
            var ratio = Mathf.Tan(ads * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(hip * 0.5f * Mathf.Deg2Rad);
            Assert.That(LookMath.FovRatio(ads, hip), Is.EqualTo(ratio).Within(1e-5f));
            Assert.That(LookMath.Multiplier(true, 0.85f, ads, hip), Is.EqualTo(0.85f * ratio).Within(1e-5f), "GDD §11: ADS × 0.85 × FOV ratio");
            Assert.That(LookMath.Multiplier(false, 0.85f, hip, hip), Is.EqualTo(1f).Within(1e-6f), "hip fire is 1");
            Assert.That(LookMath.Multiplier(false, 0.85f, hip + 4f, hip), Is.EqualTo(1f), "the sprint FOV kick does not speed the look up");
            Assert.That(LookMath.Multiplier(true, 0.85f, 12f, hip), Is.LessThan(0.2f), "binoculars are slow and steady");
        }

        [Test]
        public void PitchClampsAtEightyFive()
        {
            Assert.That(LookMath.ClampPitch(100f), Is.EqualTo(85f));
            Assert.That(LookMath.ClampPitch(-100f), Is.EqualTo(-85f));
            Assert.That(LookMath.ClampPitch(12f), Is.EqualTo(12f));
        }

        [Test]
        public void LimitsClampAroundACentreAcrossNorth()
        {
            var yaw = 100f;
            var pitch = 30f;
            LookMath.ClampToLimits(ref yaw, ref pitch, 0f, 0f, 45f, 20f, 10f);
            Assert.That(yaw, Is.EqualTo(45f).Within(1e-4f));
            Assert.That(pitch, Is.EqualTo(20f).Within(1e-4f));

            yaw = 300f; // 60° left of a centre at 0°... measured across north
            pitch = -30f;
            LookMath.ClampToLimits(ref yaw, ref pitch, 10f, 0f, 45f, 20f, 10f);
            Assert.That(MathX.WrapAngle360(yaw), Is.EqualTo(325f).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(-10f).Within(1e-4f));
        }

        [Test]
        public void PitchOfDirection()
        {
            Assert.That(LookMath.PitchOf(Vector3.forward), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(LookMath.PitchOf(new Vector3(0f, 1f, 1f)), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(LookMath.PitchOf(Vector3.down), Is.EqualTo(-90f).Within(1e-3f));
        }
    }
}
