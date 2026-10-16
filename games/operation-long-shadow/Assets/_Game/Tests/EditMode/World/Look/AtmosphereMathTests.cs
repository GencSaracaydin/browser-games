using LongShadow.World.Look.Atmosphere;
using LongShadow.World.Look.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>Wind (GDD §9.7) and the ambient SH projection (GDD §9.1).</summary>
    public sealed class AtmosphereMathTests
    {
        [Test]
        public void WindBlowsWestToEastAtThreeToFiveMetresPerSecond()
        {
            for (var t = 0f; t < 600f; t += 0.73f)
            {
                var v = WindMath.Speed(t, 3.7f);
                Assert.GreaterOrEqual(v, WindMath.MinSpeed - 1e-4f);
                Assert.LessOrEqual(v, WindMath.MaxSpeed + 1e-4f);
                var d = WindMath.Direction(t, 3.7f);
                Assert.Greater(d.x, 0.98f, "blows toward +X (east)");
                Assert.AreEqual(1f, d.magnitude, 1e-4f);
                var g = WindMath.Gust(t, 3.7f);
                Assert.GreaterOrEqual(g, 0f);
                Assert.LessOrEqual(g, 1f);
                var tu = WindMath.Turbulence(t, 3.7f);
                Assert.GreaterOrEqual(tu, 0f);
                Assert.LessOrEqual(tu, 1f);
            }
        }

        [Test]
        public void UniformSkyEvaluatesToItsRadiance()
        {
            const int w = 64, h = 32;
            var px = new Color[w * h];
            for (var i = 0; i < px.Length; i++) px[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            var sh = AmbientSHMath.ProjectEquirect(px, w, h, 1, Color.white);
            foreach (var d in new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.forward, new Vector3(1f, 1f, 0f) })
                Assert.AreEqual(0.5f, AmbientSHMath.Evaluate(sh, d).r, 0.03f, d.ToString());
        }

        [Test]
        public void BrightSkyAboveLightsUpwardNormalsMore()
        {
            var sh = AmbientSHMath.Gradient(new Color(0.6f, 0.7f, 0.9f), new Color(0.5f, 0.5f, 0.5f), new Color(0.1f, 0.08f, 0.05f));
            Assert.Greater(AmbientSHMath.Evaluate(sh, Vector3.up).b, AmbientSHMath.Evaluate(sh, Vector3.down).b * 2f);
        }

        [Test]
        public void GlowRotationMovesTheBrightSide()
        {
            // A bright patch at azimuth 0 (+Z), rotated by +90° must light +X-facing normals.
            const int w = 64, h = 32;
            var px = new Color[w * h];
            for (var r = 0; r < h; r++)
                for (var c = 0; c < w; c++)
                {
                    var d = AmbientSHMath.EquirectDirection(c, r, w, h);
                    var bright = Vector3.Dot(d, Vector3.forward) > 0.8f;
                    px[r * w + c] = bright ? new Color(4f, 4f, 4f) : new Color(0.1f, 0.1f, 0.1f);
                }
            var sh = AmbientSHMath.ProjectEquirect(px, w, h, 1, Color.white, 90f);
            Assert.Greater(AmbientSHMath.Evaluate(sh, Vector3.right).r, AmbientSHMath.Evaluate(sh, Vector3.left).r * 1.5f);
        }

        [Test]
        public void ShRoundTripsThroughFloats()
        {
            var sh = AmbientSHMath.Gradient(Color.cyan, Color.gray, Color.black);
            var back = AmbientSHMath.FromArray(AmbientSHMath.ToArray(sh));
            for (var c = 0; c < 3; c++)
                for (var i = 0; i < 9; i++)
                    Assert.AreEqual(sh[c, i], back[c, i], 1e-6f);
            Assert.IsTrue(AmbientSHMath.IsValid(AmbientSHMath.ToArray(sh)));
            Assert.IsFalse(AmbientSHMath.IsValid(new float[27]));
            Assert.AreEqual(1f, Mathf.Abs(AmbientSHMath.DirectionSign()));
        }

        [Test]
        public void EquirectDirectionsFollowThePanoramicConvention()
        {
            // u = 0.5 - atan2(z, x) / 2π: the texel column at u ≈ 0.9667 faces azimuth 258° (WP10_CC0 sky json).
            const int w = 2048, h = 1024;
            var col = Mathf.RoundToInt(0.96667f * w - 0.5f);
            var d = AmbientSHMath.EquirectDirection(col, h / 2, w, h);
            Assert.AreEqual(258f, LongShadow.Core.MathX.BearingOf(d), 0.5f);
            Assert.AreEqual(0f, d.y, 0.01f);
            Assert.Greater(AmbientSHMath.EquirectDirection(0, h - 1, w, h).y, 0.99f, "top row = zenith");
        }
    }
}
