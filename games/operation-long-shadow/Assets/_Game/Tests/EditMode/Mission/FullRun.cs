using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// One complete autopiloted run (StartMission → intro → B1 … B11 → outro → debrief) in the fake world, recorded once per
    /// test session and shared by the flow, gate, light and id tests.
    /// </summary>
    internal sealed class FullRun
    {
        private static FullRun s_run;

        public bool Finished;
        public float SimSeconds;
        public readonly List<int> BeatsStarted = new List<int>();
        public readonly List<int> BeatsCompleted = new List<int>();
        public readonly List<string> LinesRequested = new List<string>();
        public readonly List<string> MarkerLookups = new List<string>();
        public readonly List<GateChange> GateChanges = new List<GateChange>();
        public readonly List<KeyValuePair<float, float>> LightHistory = new List<KeyValuePair<float, float>>();
        public readonly List<KeyValuePair<float, int>> BeatTimes = new List<KeyValuePair<float, int>>();
        public readonly List<string> MissionEvents = new List<string>();
        public readonly HashSet<string> Completed = new HashSet<string>();
        public readonly List<string> Pilot = new List<string>();
        public MissionStats Debrief;
        public int IntelCount;
        public int OptionalCount;
        public int LiveTimeScales;
        public int LiveInputLocks;
        public string HighestCheckpoint;
        public string Failure;

        public static FullRun Get()
        {
            if (s_run != null) return s_run;
            var r = new FullRun();
            using (var w = new FakeWorld())
            {
                GameEvents.BeatStarted += b => r.BeatTimes.Add(new KeyValuePair<float, int>(w.Now, b));
                w.AutopilotOn = true;
                w.Director.StartMission();
                r.Finished = w.RunUntil(() => w.Director.Finished, 2400f);
                r.SimSeconds = w.RealNow;
                foreach (var b in w.BeatsStarted) r.BeatsStarted.Add(b.Key);
                r.BeatsCompleted.AddRange(w.BeatsCompleted);
                r.LinesRequested.AddRange(w.Director.Lines.Requested);
                r.GateChanges.AddRange(w.GateChanges);
                r.LightHistory.AddRange(w.Light.History);
                r.MissionEvents.AddRange(w.MissionEvents);
                foreach (var id in w.Director.Objectives.Completed) r.Completed.Add(id);
                r.Pilot.AddRange(w.Pilot.Log);
                r.Debrief = w.Hud.Debrief;
                r.IntelCount = w.Director.Objectives.IntelCount;
                r.OptionalCount = w.Director.Objectives.OptionalCount;
                r.LiveTimeScales = w.TimeScale.Live;
                r.LiveInputLocks = w.InputLock.Live;
                r.HighestCheckpoint = w.Settings.GetValue(StorageKeys.HighestCheckpoint);
                if (!r.Finished) r.Failure = "stuck in beat " + w.Director.Beat + ": " + Describe(w);
            }
            return s_run = r;
        }

        /// <summary>What the director is waiting for (failure messages).</summary>
        public static string Describe(FakeWorld w)
        {
            var parts = new List<string>();
            foreach (var t in w.Director.Runner.Tasks) parts.Add(t.ToString());
            return string.Join(" | ", parts);
        }

        public static void AssertFinished()
        {
            var r = Get();
            Assert.IsTrue(r.Finished, r.Failure + "\nautopilot: " + string.Join("\n", r.Pilot));
        }
    }
}
