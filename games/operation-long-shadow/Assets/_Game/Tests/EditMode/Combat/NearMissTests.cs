using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>GDD §14.3: AI misses land 0.5-3 m from the player's head and crack past within 3 m.</summary>
    public class NearMissTests
    {
        [Test]
        public void MissPointsStayInTheGddBand()
        {
            var head = new Vector3(5f, 1.65f, 20f);
            var rng = new System.Random(11);
            for (var i = 0; i < 1000; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.4f - 0.2f, 1f).normalized;
                var p = NearMissMath.MissPoint(head, dir, WeaponTable.AiMissMin, WeaponTable.AiMissMax, (float)rng.NextDouble(), (float)rng.NextDouble());
                var d = Vector3.Distance(p, head);
                Assert.That(d, Is.InRange(0.5f - 1e-4f, 3f + 1e-4f));
                var flat = new Vector3(dir.x, 0f, dir.z).normalized; // level shots: the offset is exactly across the shot
                var q = NearMissMath.MissPoint(head, flat, 0.5f, 3f, (float)rng.NextDouble(), (float)rng.NextDouble());
                Assert.That(Mathf.Abs(Vector3.Dot((q - head).normalized, flat)), Is.LessThan(1e-3f), "perpendicular to the shot");
            }
        }

        [Test]
        public void PassingBulletIsANearMiss()
        {
            var head = new Vector3(0f, 1.65f, 0f);
            Assert.That(NearMissMath.IsNearMiss(new Vector3(-50f, 1.65f, 1.5f), new Vector3(50f, 1.65f, 1.5f), head, 3f, out _, out var d), Is.True);
            Assert.That(d, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(NearMissMath.IsNearMiss(new Vector3(-50f, 1.65f, 4f), new Vector3(50f, 1.65f, 4f), head, 3f, out _, out _), Is.False);
            // A round that stops in a wall in front of the shooter never reaches the player.
            Assert.That(NearMissMath.IsNearMiss(new Vector3(0f, 1.65f, 40f), new Vector3(0f, 1.65f, 30f), head, 3f, out _, out _), Is.False);
        }

        [Test]
        public void ClosestApproachOnTheSegment()
        {
            var d = NearMissMath.ClosestApproach(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(4f, 2f, 0f), out var c, out var t);
            Assert.That(d, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(t, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(c, Is.EqualTo(new Vector3(4f, 0f, 0f)));
        }
    }
}
