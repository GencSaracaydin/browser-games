using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    public class MathXTests
    {
        [TestCase(0f, 0f)]
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(540f, 180f)]
        [TestCase(-180f, 180f)]
        public void WrapAngle180(float input, float expected) => Assert.That(MathX.WrapAngle180(input), Is.EqualTo(expected).Within(1e-4f));

        [TestCase(-10f, 350f)]
        [TestCase(720f, 0f)]
        [TestCase(359.5f, 359.5f)]
        public void WrapAngle360(float input, float expected) => Assert.That(MathX.WrapAngle360(input), Is.EqualTo(expected).Within(1e-4f));

        [Test]
        public void BearingIsCompassClockwiseFromNorth()
        {
            Assert.That(MathX.BearingOf(Vector3.forward), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(MathX.BearingOf(Vector3.right), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(MathX.BearingOf(Vector3.back), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(MathX.BearingOf(Vector3.left), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(MathX.Bearing(new Vector3(1, 0, 1), new Vector3(2, 5, 2)), Is.EqualTo(45f).Within(1e-3f));
            var d = MathX.DirectionFromBearing(258f);
            Assert.That(MathX.BearingOf(d), Is.EqualTo(258f).Within(1e-3f));
        }

        [Test]
        public void FovConversionMatchesGdd()
        {
            // GDD §13: 90° horizontal is about 58.7° vertical at 16:9.
            var v = MathX.HorizontalToVerticalFov(90f, 16f / 9f);
            Assert.That(v, Is.EqualTo(58.72f).Within(0.05f));
            Assert.That(MathX.VerticalToHorizontalFov(v, 16f / 9f), Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void DampAndRemap()
        {
            Assert.That(MathX.Damp(0f, 10f, 5f, 0f), Is.EqualTo(0f));
            Assert.That(MathX.Damp(0f, 10f, 5f, 100f), Is.EqualTo(10f).Within(1e-3f));
            // Two half steps equal one full step (frame-rate independence).
            var half = MathX.Damp(MathX.Damp(0f, 10f, 3f, 0.05f), 10f, 3f, 0.05f);
            Assert.That(half, Is.EqualTo(MathX.Damp(0f, 10f, 3f, 0.1f)).Within(1e-4f));
            Assert.That(MathX.Remap(5f, 0f, 10f, 100f, 200f), Is.EqualTo(150f));
            Assert.That(MathX.Remap(20f, 0f, 10f, 100f, 200f), Is.EqualTo(200f));
            Assert.That(MathX.RemapUnclamped(20f, 0f, 10f, 100f, 200f), Is.EqualTo(300f));
        }

        [Test]
        public void DecibelRoundTrip()
        {
            Assert.That(MathX.DbToLinear(-6f), Is.EqualTo(0.501f).Within(0.001f));
            Assert.That(MathX.LinearToDb(MathX.DbToLinear(-4f)), Is.EqualTo(-4f).Within(1e-3f));
            Assert.That(MathX.LinearToDb(0f), Is.EqualTo(-80f));
        }

        [Test]
        public void PolylineProjection()
        {
            var pts = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
            Assert.That(MathX.PolylineLength(pts), Is.EqualTo(20f).Within(1e-4f));
            var c = MathX.ProjectOnPolyline(pts, new Vector3(4, 3, -2), out var along, out var total);
            Assert.That(c.x, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(along, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(total, Is.EqualTo(20f).Within(1e-4f));
            MathX.ProjectOnPolyline(pts, new Vector3(12, 0, 6), out along, out _);
            Assert.That(along, Is.EqualTo(16f).Within(1e-4f));
            Assert.That(MathX.PointAlongPolyline(pts, 15f), Is.EqualTo(new Vector3(10, 0, 5)));
            Assert.That(MathX.PointAlongPolyline(pts, 99f), Is.EqualTo(new Vector3(10, 0, 10)));
        }

        [Test]
        public void Fnv1aKnownVectors()
        {
            Assert.That(MathX.Fnv1a32(""), Is.EqualTo(0x811C9DC5u));
            Assert.That(MathX.Fnv1a32("a"), Is.EqualTo(0xE40C292Cu));
            Assert.That(MathX.Fnv1a32("foobar"), Is.EqualTo(0xBF9CF968u));
            var g = MathX.FlavourGroup("OBJ_B4_TECHNICAL");
            Assert.That(g, Is.InRange(0, 99999));
            Assert.That(MathX.FlavourGroup("OBJ_B4_TECHNICAL"), Is.EqualTo(g), "deterministic");
        }

        [Test]
        public void ConeContainment()
        {
            Assert.IsTrue(MathX.InCone(Vector3.zero, Vector3.forward, 30f, 10f, new Vector3(0, 0, 5)));
            Assert.IsFalse(MathX.InCone(Vector3.zero, Vector3.forward, 30f, 10f, new Vector3(5, 0, 1)));
            Assert.IsFalse(MathX.InCone(Vector3.zero, Vector3.forward, 30f, 10f, new Vector3(0, 0, 11)));
        }

        [Test]
        public void SignedAngleXZ()
        {
            Assert.That(MathX.SignedAngleXZ(Vector3.forward, Vector3.right), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(MathX.SignedAngleXZ(Vector3.forward, Vector3.left), Is.EqualTo(-90f).Within(1e-3f));
        }
    }
}
