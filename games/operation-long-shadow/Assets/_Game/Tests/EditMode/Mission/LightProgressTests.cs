using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: LightStage progress is monotonic and stays within the beat's range (GDD §9.1: progress = max(previous,
    /// 0.5 × objectives + 0.5 × critical path), mapped into the beat's s range, 2 s smoothing; scripted overrides).
    /// </summary>
    public class LightProgressTests
    {
        [Test]
        public void ProgressFormulaAndMonotonicity()
        {
            var d = new LightProgressDriver();
            d.BeginBeat(3, 0.45f, 1.00f, 0.45f);
            Assert.IsTrue(d.Update(0f, 0.5f, false, 0.45f));
            Assert.That(d.Target, Is.EqualTo(0.45f + 0.55f * 0.25f).Within(1e-4f));
            Assert.IsTrue(d.Update(1f, 0.5f, false, 0.6f));
            Assert.That(d.Target, Is.EqualTo(0.45f + 0.55f * 0.75f).Within(1e-4f));
            Assert.IsFalse(d.Update(0f, 0f, false, 0.86f), "progress never decreases");
            Assert.That(d.Target, Is.EqualTo(0.45f + 0.55f * 0.75f).Within(1e-4f));
            d.Update(1f, 1f, false, 0.9f);
            Assert.That(d.Target, Is.EqualTo(1.00f).Within(1e-4f), "objectives done + path done = s_end");
        }

        [Test]
        public void OverridesSuspendTheDriverAndRaiseItsFloor()
        {
            var d = new LightProgressDriver();
            d.BeginBeat(4, 1.00f, 1.50f, 1.00f);
            d.SetRange(1.00f, 1.00f);
            d.Update(0.5f, 0.5f, false, 1.0f);
            Assert.That(d.Target, Is.EqualTo(1.00f), "Beat 4 holds 1.00 until the cinema swap");
            Assert.IsFalse(d.Update(0.5f, 0.6f, true, 1.2f), "overridden: nothing sent");
            d.SetRange(1.35f, 1.50f);
            d.Update(0.5f, 0.6f, false, 1.35f);
            Assert.That(d.Target, Is.GreaterThanOrEqualTo(1.35f));
            Assert.That(d.Target, Is.LessThanOrEqualTo(1.50f));
        }

        [Test]
        public void FullRunIsMonotonicAndWithinEachBeatsRange()
        {
            FullRun.AssertFinished();
            var r = FullRun.Get();
            var last = -1f;
            foreach (var kv in r.LightHistory)
            {
                Assert.That(kv.Value, Is.GreaterThanOrEqualTo(last - 1e-4f), "s went back from " + last + " to " + kv.Value + " at " + kv.Key);
                last = kv.Value;
                var beat = BeatAt(r, kv.Key);
                if (beat < 1 || beat > BeatTable.PlayableBeats) continue;
                var def = BeatTable.Get(beat);
                Assert.That(kv.Value, Is.InRange(def.SStart - 0.01f, def.SEnd + 0.01f), "beat " + beat + " s " + kv.Value);
            }
            // Beat 11: progress = 0.5 × objectives + 0.5 × exfil time / 50 s (Appendix A); the climax ends the beat near t 31-42.
            Assert.That(last, Is.InRange(3.90f, 4.00f), "the exfil ends at the end of civil twilight");
        }

        [Test]
        public void StoryClockFollowsGdd92a()
        {
            Assert.That(StoryClock.Format(StoryClock.FromS(0f)), Is.EqualTo("17:58"));
            Assert.That(StoryClock.Format(StoryClock.FromS(0.45f)), Is.EqualTo("18:17"));
            Assert.That(StoryClock.Format(StoryClock.FromS(1.94f)), Is.EqualTo("18:40"), "the Seiko reads 18:40 at the watch check");
            Assert.That(StoryClock.Format(StoryClock.FromS(3.7f)), Is.EqualTo("19:10"));
            Assert.That(StoryClock.Format(StoryClock.FromS(4f)), Is.EqualTo("19:12"));
            for (var b = 1; b <= BeatTable.PlayableBeats; b++)
                Assert.That(StoryClock.Format(StoryClock.FromS(BeatTable.Get(b).SStart)), Is.EqualTo(BeatTable.Get(b).StoryClock), "beat " + b);
        }

        [Test]
        public void CriticalPathFallbackProjects()
        {
            Assert.That(CriticalPathKnots.Project01(3, new UnityEngine.Vector3(185f, 0f, 170f)), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(CriticalPathKnots.Project01(3, new UnityEngine.Vector3(185f, 0f, 252f)), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(CriticalPathKnots.Project01(3, new UnityEngine.Vector3(180f, 0f, 211f)), Is.EqualTo(0.5f).Within(0.01f));
        }

        private static int BeatAt(FullRun r, float time)
        {
            var beat = 0;
            foreach (var kv in r.BeatTimes)
                if (kv.Key <= time) beat = kv.Value;
            return beat;
        }
    }
}
