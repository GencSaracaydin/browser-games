using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Beats;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: MID-A runs to the GDD §8.5 timeline (±0.3 s per event) in a fake-world test. Two cases: the line lengths
    /// the §8.5 table was written against (every event lands on the table), and the real VO lengths (anchored events on the
    /// table; D041 chained to D040's end, D042 on "Calder?" inside D041, the release when D042 ends).
    /// </summary>
    public class MidATimingTests
    {
        private const float Tol = 0.3f;

        private sealed class Timeline
        {
            public float T0;
            public readonly Dictionary<string, float> Line = new Dictionary<string, float>();
            public readonly Dictionary<string, float> LineEnd = new Dictionary<string, float>();
            public float Music = -1f, Watch = -1f, Interval = -1f, I2 = -1f, End = -1f, Strike = -1f;
            public float RadioSwitch;
            public FakeWorld World;
        }

        [TearDown]
        public void TearDown() => GameServices.Clear();

        private static Timeline Run(Dictionary<string, float> lengths)
        {
            var beats = BeatScripts.CreateAll();
            var b5 = (Beat05LastLight)beats[5];
            var t = new Timeline();
            var w = new FakeWorld(true, true, beats);
            t.World = w;
            if (lengths != null)
                foreach (var kv in lengths) w.VO.Lengths[kv.Key] = kv.Value;
            w.AutopilotOn = true;
            w.Director.ResumeFrom(SequenceIds.CP5);
            Assert.IsTrue(w.RunUntil(() => b5.MidAStartedAt >= 0f, 120f), "MID-A started");
            t.T0 = b5.MidAStartedAt;
            Assert.IsTrue(w.RunUntil(() => w.MissionEvents.Contains(SequenceIds.MidAEnd), 120f), "MID-A ended: " + FullRun.Describe(w));
            foreach (var c in w.VO.Calls)
            {
                if (c.Name == "Started" && !t.Line.ContainsKey(c.Arg)) t.Line[c.Arg] = c.Time - t.T0;
                if ((c.Name == "Finished" || c.Name == "Stop") && !t.LineEnd.ContainsKey(c.Arg)) t.LineEnd[c.Arg] = c.Time - t.T0;
                if (c.Name == "Play" && c.Arg == LineIds.D039) t.RadioSwitch = c.Value;
            }
            t.Music = Rel(t, w.Music.FirstTime("Cue", MissionCues.MusicLastLight));
            t.Watch = Rel(t, w.FpBody.FirstTime("Watch"));
            t.Interval = Rel(t, w.Audio.FirstTime("Attached", MissionCues.IntervalSignal));
            t.I2 = Rel(t, w.Inserts.FirstTime("Insert", SequenceIds.InsertI2));
            foreach (var kv in w.EventTimes)
                if (kv.Value == SequenceIds.MidAEnd) t.End = kv.Key - t.T0;
            var strike = w.Director.Log.LastTime(EventChannel.Mission, MissionEventLog.PracticalsStrikeId);
            t.Strike = strike >= 0f ? strike - t.T0 : -1f;
            return t;
        }

        private static float Rel(Timeline t, float time) => time < 0f ? -1f : time - t.T0;

        private static void Near(float actual, float expected, string what) =>
            Assert.That(actual, Is.EqualTo(expected).Within(Tol), what + " at " + actual.ToString("0.00") + " s (expected " + expected.ToString("0.00") + ")");

        private static void AssertAnchoredEvents(Timeline t)
        {
            var w = t.World;
            CollectionAssert.Contains(w.MissionEvents, SequenceIds.MidAStart);
            Near(t.Music, MidATimeline.MusicSwell, "MUS_03 swell (0:05)");
            Near(t.Watch, MidATimeline.WatchCheck, "watch check (0:12)");
            Near(t.Strike, MidATimeline.StrikeTime, "PracticalsStrike (0:14)");
            Near(t.Line[LineIds.D037], MidATimeline.D037At, "D037 (0:20)");
            Near(t.Interval, MidATimeline.D037At, "interval signal from the PRC-77 (0:20)");
            Near(t.Line[LineIds.D038], MidATimeline.D038At, "D038 (≈ 0:23)");
            Near(t.Line[LineIds.D039], MidATimeline.D039At, "D039 (≈ 0:27)");
            Near(t.Line[LineIds.D039] + t.RadioSwitch, MidATimeline.GddD039Seven, "radio jump on 'Seven' (≈ 0:28.8)");
            Near(t.I2, MidATimeline.GddI2, "I2 on 'Six' (≈ 0:38.7)");
            Near(t.Line[LineIds.D040], MidATimeline.GddD040, "D040 on 'Four' (≈ 0:39.6)");
            // LightStage keys: 1.80 held to 0:04, 1.97 at 0:14, 2.00 at 0:20 (GDD §8.5, §9.2a).
            foreach (var kv in w.Light.History)
            {
                var rel = kv.Key - t.T0;
                if (rel < 0f || rel > MidATimeline.SunDownTime + 1f) continue;
                Assert.That(kv.Value, Is.EqualTo(MidATimeline.SAt(rel)).Within(0.01f), "s at " + rel.ToString("0.00"));
            }
            Assert.That(MidATimeline.SAt(4f), Is.EqualTo(1.80f).Within(1e-4f));
            Assert.That(MidATimeline.SAt(14f), Is.EqualTo(1.97f).Within(1e-4f));
            Assert.That(MidATimeline.SAt(20f), Is.EqualTo(2.00f).Within(1e-4f));
            Assert.That(w.Light.S, Is.EqualTo(2.00f).Within(1e-4f), "sun down after MID-A");
            Assert.IsFalse(w.Light.IsOverridden, "LightStage override released");
            Assert.IsTrue(w.Rig.Has("ForcedWalk", "on"), "forced walk 1.2 m/s");
            Assert.IsNull(w.Rig.ForcedWalk, "forced walk released");
            Assert.IsTrue(w.Director.Gates.IsOpen(GateTable.R4Stair), "the R4 stair opens when MID-A ends");
            Assert.IsTrue(w.Director.Objectives.IsActive(ObjectiveTable.B5Breach), "BREACH POINT shown");
            Assert.IsTrue(w.Director.Objectives.IsComplete(ObjectiveTable.B5Wall), "REACH THE STATION WALL completes when MID-A starts");
            var pulses = 0;
            foreach (var c in w.ScreenFX.Calls)
                if (c.Name == "Pulse") pulses++;
            Assert.That(pulses, Is.EqualTo(MidATimeline.TokenCount - MidATimeline.FirstDigitToken), "one vision pulse per digit");
        }

        [Test]
        public void WithTheGddLineLengthsEveryEventLandsOnTheTable()
        {
            // The §8.5 table assumes D040 ends at 0:41.6 (D041) and D042 (from 0:44) at 0:47.
            var lengths = new Dictionary<string, float>
            {
                { LineIds.D037, 2.0f }, { LineIds.D038, 3.5f }, { LineIds.D040, MidATimeline.GddD041 - MidATimeline.GddD040 },
                { LineIds.D041, 4.2f }, { LineIds.D042, MidATimeline.GddEnd - MidATimeline.GddD042 },
            };
            var t = Run(lengths);
            try
            {
                AssertAnchoredEvents(t);
                Near(t.Line[LineIds.D041], MidATimeline.GddD041, "D041 (≈ 0:41.6)");
                Near(t.Line[LineIds.D042], MidATimeline.GddD042, "D042 on 'Calder?' (≈ 0:44)");
                Near(t.End, MidATimeline.GddEnd, "release when D042 ends (≈ 0:47)");
            }
            finally
            {
                t.World.Dispose();
            }
        }

        [Test]
        public void WithTheRealLineLengthsChainedLinesFollowTheirTriggers()
        {
            var t = Run(null);
            try
            {
                AssertAnchoredEvents(t);
                Near(t.Line[LineIds.D041], t.LineEnd[LineIds.D040], "D041 when D040 finished");
                Near(t.Line[LineIds.D042], t.Line[LineIds.D041] + MidATimeline.CalderOffsetInD041, "D042 on 'Calder?' inside D041");
                Near(t.End, t.LineEnd[LineIds.D042], "release when D042 ends");
                Assert.That(t.End, Is.LessThan(MidATimeline.GddEnd + 2f), "MID-A stays about 47 s");
            }
            finally
            {
                t.World.Dispose();
            }
        }

        [Test]
        public void TheMarksmanCeasesFireAndTheSquadIsNeverShotDuringTheForcedWalk()
        {
            var t = Run(null);
            try
            {
                // MIDA.Start is raised before the forced walk begins (WP04's marksman stops on it).
                var w = t.World;
                var start = -1f;
                foreach (var kv in w.EventTimes)
                    if (kv.Value == SequenceIds.MidAStart) start = kv.Key;
                Assert.That(start, Is.EqualTo(t.T0).Within(0.01f));
                Assert.That(w.Rig.FirstTime("ForcedWalk", "on"), Is.GreaterThanOrEqualTo(start));
            }
            finally
            {
                t.World.Dispose();
            }
        }

        [Test]
        public void MidAStartsOnlyWithTheNestDeadAndTheSquadKillsItAfter20Seconds()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP5);
                w.Run(1f);
                w.Enter(MissionTriggers.B5R4);
                w.Run(10f);
                CollectionAssert.DoesNotContain(w.MissionEvents, SequenceIds.MidAStart, "nest alive: no MID-A yet");
                w.Run(11f);
                Assert.IsTrue(w.Squad.Has("ScriptedKill", SquadIds.Hollis + ":" + EncounterIds.NestGuardA), "Hollis kills E5-02");
                Assert.IsTrue(w.Squad.Has("ScriptedKill", SquadIds.Tinker + ":" + EncounterIds.NestGuardB), "Tinker kills E5-03");
                Assert.IsTrue(w.RunUntil(() => w.MissionEvents.Contains(SequenceIds.MidAStart), 5f), "MID-A starts once both are dead");
            }
        }
    }
}
