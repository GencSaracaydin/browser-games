using LongShadow.Player.Movement;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Feet paths of vault / climb / air mantle (<see cref="TraversalPath"/>).</summary>
    public class TraversalPathTests
    {
        private static void AssertPathSane(TraversalPath p, Vector3 start, Vector3 end)
        {
            Assert.That((p.Evaluate(0f) - start).magnitude, Is.LessThan(1e-4f), "starts at the feet");
            Assert.That((p.Evaluate(1f) - end).magnitude, Is.LessThan(1e-4f), "ends at the end point");
            var floor = Mathf.Min(start.y, end.y) - 1e-4f;
            var prev = p.Evaluate(0f);
            for (var i = 1; i <= 400; i++)
            {
                var q = p.Evaluate(i / 400f);
                Assert.That(q.y, Is.GreaterThanOrEqualTo(floor), "never dips below the lower end");
                Assert.That((q - prev).magnitude, Is.LessThan(0.05f), "continuous");
                prev = q;
            }
        }

        [Test]
        public void VaultOntoOneMetreLedge()
        {
            var start = Vector3.zero;
            var edge = new Vector3(0f, 1f, 0.8f);
            var end = new Vector3(0f, 1f, 1.25f);
            var p = TraversalPath.Create(start, edge, end, Vector3.forward, 0.3f, 0.45f, MovementMath.TraversalRiseFraction(LedgeKind.Vault));
            AssertPathSane(p, start, end);
            Assert.That(p.PeakY, Is.GreaterThanOrEqualTo(1f), "clears the top");
            Assert.That(p.Duration, Is.EqualTo(0.45f));
            // The feet are above the top before they move over it.
            var atFace = p.Evaluate(p.RiseFraction);
            Assert.That(atFace.y, Is.GreaterThanOrEqualTo(1f));
            Assert.That(atFace.z, Is.LessThanOrEqualTo(0.8f - 0.3f + 1e-3f), "capsule front at the face, not inside it");
        }

        [Test]
        public void OverVaultDropsOnTheFarSide()
        {
            var start = Vector3.zero;
            var edge = new Vector3(0f, 1f, 0.6f);
            var end = new Vector3(0f, 0f, 1.3f);
            var p = TraversalPath.Create(start, edge, end, Vector3.forward, 0.3f, 0.45f, 0.45f);
            AssertPathSane(p, start, end);
            Assert.That(p.HorizontalLength, Is.EqualTo(1.3f).Within(1e-3f));
        }

        [Test]
        public void AlreadyAtTheFaceRisesStraightUp()
        {
            var start = new Vector3(0f, 0f, 0.6f);
            var edge = new Vector3(0f, 1.8f, 0.85f);
            var end = new Vector3(0f, 1.8f, 1.2f);
            var p = TraversalPath.Create(start, edge, end, Vector3.forward, 0.3f, 0.8f, MovementMath.TraversalRiseFraction(LedgeKind.Climb));
            AssertPathSane(p, start, end);
            var crest = p.Evaluate(p.RiseFraction);
            Assert.That(crest.z, Is.EqualTo(0.6f).Within(1e-4f), "no backwards move when already against the wall");
        }
    }
}
