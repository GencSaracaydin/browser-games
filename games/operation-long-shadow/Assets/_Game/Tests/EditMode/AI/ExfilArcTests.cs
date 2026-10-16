using System.Collections.Generic;
using LongShadow.AI.Encounters;
using LongShadow.AI.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>
    /// TECH_PLAN §11.4 WP04 DoD: sample the B11.Exfil spline and assert that every E11 rooftop target and both Clock
    /// Square trucks stay inside the left door-gun arc (yaw ±70° about the door normal, pitch +15° / −60°, GDD §13.3)
    /// for their whole active window (GDD §7.11). Checked against every interpretation of "auto (Catmull-Rom) tangents,
    /// segment times 8/8/10/10/8/6 s": uniform Catmull-Rom, and a Unity Spline with AutoSmooth knots sampled time-linear
    /// and at constant speed per segment.
    /// </summary>
    public class ExfilArcTests
    {
        private const float Step = 0.05f;

        private delegate void Sampler(float t, out Vector3 position, out Vector3 tangent);

        private static IEnumerable<KeyValuePair<string, Sampler>> Samplers()
        {
            yield return new KeyValuePair<string, Sampler>("catmull-rom", ExfilFlightPath.SampleCatmullRom);
            var spline = ExfilFlightPath.BuildSpline();
            yield return new KeyValuePair<string, Sampler>("unity-spline", (float t, out Vector3 p, out Vector3 d) =>
                ExfilFlightPath.SampleSpline(spline, ExfilSchedule.SegmentSeconds, t, false, out p, out d));
            yield return new KeyValuePair<string, Sampler>("unity-spline-constant-speed", (float t, out Vector3 p, out Vector3 d) =>
                ExfilFlightPath.SampleSpline(spline, ExfilSchedule.SegmentSeconds, t, true, out p, out d));
        }

        private static void AssertInArc(string what, Vector3 target, float start, float end)
        {
            foreach (var s in Samplers())
            {
                for (var t = start; t <= end + 1e-4f; t += Step)
                {
                    s.Value(t, out var pos, out var tan);
                    var yaw = ExfilFlightPath.YawOf(tan);
                    var inside = DoorGunArc.Contains(pos, yaw, ExfilSchedule.DoorGunPivotLocal, target, out var y, out var p);
                    Assert.IsTrue(inside, $"{what} leaves the door-gun arc at t {t:0.00} ({s.Key}): yaw {y:0.0}°, pitch {p:0.0}°");
                }
            }
        }

        [Test]
        public void EveryRooftopTargetIsInsideTheArcForItsWindow()
        {
            var e11 = EncounterTable.Get(EncounterTable.E11);
            Assert.IsNotNull(e11);
            Assert.That(e11.Enemies.Count, Is.EqualTo(4));
            foreach (var d in e11.Enemies)
            {
                Assert.That(d.Archetype, Is.EqualTo(ArchetypeId.RooftopTarget), d.Id);
                Assert.That(d.WindowEnd, Is.GreaterThanOrEqualTo(d.WindowStart), d.Id);
                // T3 is exposed from t 28 (stand) to t 30 (launch).
                var start = d.HiddenUntil > 0f ? d.HiddenUntil : d.WindowStart;
                AssertInArc(d.Id, d.Position + Vector3.up * 1.3f, start, d.WindowEnd);
            }
        }

        [Test]
        public void TheClockSquareTrucksAreInsideTheArcFrom16To27()
        {
            for (var i = 0; i < ExfilSchedule.TruckCount; i++)
            {
                var index = i;
                foreach (var s in Samplers())
                {
                    for (var t = ExfilSchedule.TruckWindowStart; t <= ExfilSchedule.TruckWindowEnd + 1e-4f; t += Step)
                    {
                        s.Value(t, out var pos, out var tan);
                        var truck = ExfilSchedule.TruckPosition(index, t) + Vector3.up * ExfilSchedule.TruckAimHeight;
                        var inside = DoorGunArc.Contains(pos, ExfilFlightPath.YawOf(tan), ExfilSchedule.DoorGunPivotLocal, truck, out var y, out var p);
                        Assert.IsTrue(inside, $"truck {index} leaves the arc at t {t:0.00} ({s.Key}): yaw {y:0.0}°, pitch {p:0.0}°");
                    }
                }
            }
        }

        [Test]
        public void WindowsMatchTheGdd()
        {
            var e11 = EncounterTable.Get(EncounterTable.E11);
            var t1 = e11.GetEnemy("E11-T1");
            var t2 = e11.GetEnemy("E11-T2");
            var t3 = e11.GetEnemy("E11-T3");
            var t4 = e11.GetEnemy("E11-T4");
            Assert.That(new[] { t1.WindowStart, t1.WindowEnd }, Is.EqualTo(new[] { 8f, 16f }));    // GDD §7.11
            Assert.That(new[] { t2.WindowStart, t2.WindowEnd }, Is.EqualTo(new[] { 15f, 23f }));
            Assert.That(new[] { t4.WindowStart, t4.WindowEnd }, Is.EqualTo(new[] { 26f, 33f }));
            Assert.That(t3.HiddenUntil, Is.EqualTo(28f));
            Assert.That(t3.FireAt, Is.EqualTo(30f));
            Assert.That(ExfilSchedule.KnotTime(6), Is.EqualTo(ExfilSchedule.TotalSeconds));
        }

        [Test]
        public void SamplersPassThroughTheKnotsAtTheirTimes()
        {
            foreach (var s in Samplers())
            {
                for (var k = 0; k < ExfilSchedule.Knots.Length; k++)
                {
                    s.Value(ExfilSchedule.KnotTime(k), out var p, out _);
                    Assert.That(Vector3.Distance(p, ExfilSchedule.Knots[k]), Is.LessThan(0.05f), s.Key + " N" + k);
                }
            }
        }

        [Test]
        public void YawMatchesTheCompass()
        {
            Assert.That((DoorGunArc.Yaw(90f, Vector3.forward) - Vector3.right).magnitude, Is.LessThan(1e-5f));
            Assert.That((DoorGunArc.Yaw(90f, Vector3.left) - Vector3.forward).magnitude, Is.LessThan(1e-5f), "facing east the left door looks north");
            Assert.That((DoorGunArc.Yaw(180f, Vector3.forward) - Vector3.back).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void DoorArcGeometry()
        {
            // Cabin at the origin heading north: the left door looks west. A target due west at the same height is centred.
            Assert.IsTrue(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(-50f, 0f, 0f), out var yaw, out var pitch));
            Assert.That(yaw, Is.EqualTo(0f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(0f).Within(0.01f));
            Assert.IsFalse(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(50f, 0f, 0f), out _, out _), "right side");
            Assert.IsFalse(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(-10f, -30f, 0f), out _, out _), "below -60°");
            Assert.IsFalse(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(-50f, 20f, 0f), out _, out _), "above +15°");
            Assert.IsTrue(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(-50f, 0f, 120f), out _, out _), "68° forward");
            Assert.IsFalse(DoorGunArc.Contains(Vector3.zero, 0f, Vector3.zero, new Vector3(-20f, 0f, 120f), out _, out _), "81° forward");
        }
    }
}
