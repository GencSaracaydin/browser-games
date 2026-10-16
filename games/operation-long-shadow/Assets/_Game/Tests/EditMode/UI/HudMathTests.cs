using LongShadow.UI.Common;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>GDD §11 numbers behind the HUD: distance steps, compass projection, ring angles, clamping, scaling.</summary>
    public sealed class HudMathTests
    {
        [TestCase(0.4f, 0)]
        [TestCase(4.6f, 5)]
        [TestCase(47.4f, 47)]
        [TestCase(99.4f, 99)]
        [TestCase(99.6f, 100)]
        [TestCase(102.4f, 100)]
        [TestCase(103f, 105)]
        [TestCase(147f, 145)]
        [TestCase(148f, 150)]
        [TestCase(1234f, 1235)]
        public void DistanceStepsAreOneMetreBelow100AndFiveBeyond(float metres, int expected)
        {
            Assert.AreEqual(expected, HudMath.RoundDistance(metres));
        }

        [Test]
        public void CompassMapsTheFrontHalfOntoTheStrip()
        {
            Assert.IsTrue(HudMath.CompassOffset(258f, 258f, 90f, 320f, out var x));
            Assert.AreEqual(0f, x, 1e-3f);
            Assert.IsTrue(HudMath.CompassOffset(303f, 258f, 90f, 320f, out x));
            Assert.AreEqual(160f, x, 1e-3f);
            Assert.IsTrue(HudMath.CompassOffset(350f, 10f, 90f, 320f, out x), "wraps through north");
            Assert.AreEqual(-71.11f, x, 0.01f);
            Assert.IsFalse(HudMath.CompassOffset(180f, 0f, 90f, 320f, out x), "behind is outside");
            Assert.AreEqual(320f, Mathf.Abs(x), 1e-3f, "outside clamps to the edge");
        }

        [Test]
        public void RingAnglePointsAtTheSource()
        {
            var forward = Vector3.forward;
            Assert.AreEqual(0f, HudMath.RingAngle(forward, new Vector3(0f, 0f, 10f)), 1e-3f);
            Assert.AreEqual(-90f, HudMath.RingAngle(forward, new Vector3(10f, 0f, 0f)), 1e-3f, "right = clockwise = negative z");
            Assert.AreEqual(90f, HudMath.RingAngle(forward, new Vector3(-10f, 0f, 0f)), 1e-3f);
            Assert.AreEqual(180f, Mathf.Abs(HudMath.RingAngle(forward, new Vector3(0f, 0f, -10f))), 1e-3f);
            // Turned 90 degrees right, a source to the north is now on the left.
            Assert.AreEqual(90f, HudMath.RingAngle(Vector3.right, new Vector3(0f, 0f, 10f)), 1e-3f);
        }

        [Test]
        public void ClampKeepsInsidePointsAndProjectsOutsidePointsOntoTheEdge()
        {
            var center = new Vector2(960f, 540f);
            var half = new Vector2(888f, 468f);
            var p = new Vector2(1000f, 600f);
            Assert.IsFalse(HudMath.ClampToRect(ref p, center, half, out _));
            Assert.AreEqual(new Vector2(1000f, 600f), p);

            p = new Vector2(5000f, 540f);
            Assert.IsTrue(HudMath.ClampToRect(ref p, center, half, out var angle));
            Assert.AreEqual(960f + 888f, p.x, 1e-2f);
            Assert.AreEqual(540f, p.y, 1e-2f);
            Assert.AreEqual(0f, angle, 1e-3f);

            p = new Vector2(960f, -4000f);
            Assert.IsTrue(HudMath.ClampToRect(ref p, center, half, out angle));
            Assert.AreEqual(540f - 468f, p.y, 1e-2f);
            Assert.AreEqual(-90f, angle, 1e-3f);
        }

        [Test]
        public void LetterboxIs239ToOne()
        {
            var h = HudMath.LetterboxBarHeight(1920f, 1080f, 2.39f);
            Assert.AreEqual((1080f - 1920f / 2.39f) * 0.5f, h, 1e-3f);
            // 2560x1080 ("21:9") is only 2.37:1 and still gets 4.4 px bars; 2640x1080 (2.44:1) is wider than the scope ratio.
            Assert.AreEqual(4.435f, HudMath.LetterboxBarHeight(2560f, 1080f, 2.39f), 0.01f, "2.37:1 is narrower than 2.39:1");
            Assert.AreEqual(0f, HudMath.LetterboxBarHeight(2640f, 1080f, 2.39f), "already wider than 2.39:1");
        }

        [Test]
        public void CanvasScalerMatchesUnityFormula()
        {
            Assert.AreEqual(1f, HudMath.CanvasScale(new Vector2(1920f, 1080f), HudMath.Reference, 0.5f), 1e-4f);
            Assert.AreEqual(2f / 3f, HudMath.CanvasScale(new Vector2(1280f, 720f), HudMath.Reference, 0.5f), 1e-4f);
            var size = HudMath.CanvasSize(new Vector2(1280f, 720f));
            Assert.AreEqual(1920f, size.x, 0.01f);
            Assert.AreEqual(1080f, size.y, 0.01f);
        }

        [Test]
        public void MarkersFadeTo30PercentWhileAiming()
        {
            Assert.AreEqual(1f, HudMath.MarkerAlpha(0f, false, false), 1e-4f);
            Assert.AreEqual(0.3f, HudMath.MarkerAlpha(1f, false, false), 1e-4f);
            Assert.Less(HudMath.MarkerAlpha(0f, true, false), 1f, "occluded markers dim");
            Assert.AreEqual(1f, HudMath.MarkerAlpha(0f, true, true), 1e-4f, "through-walls markers do not dim");
        }

        [Test]
        public void VignetteStartsBelowHalfHealth()
        {
            Assert.AreEqual(0f, HudMath.VignetteAlpha(1f));
            Assert.AreEqual(0f, HudMath.VignetteAlpha(0.5f));
            Assert.AreEqual(0.5f, HudMath.VignetteAlpha(0.25f), 1e-4f);
            Assert.AreEqual(1f, HudMath.VignetteAlpha(0f));
        }
    }
}
