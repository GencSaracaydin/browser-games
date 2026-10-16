using System;
using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Dev
{
    /// <summary>
    /// The automated smoke test of TECH_PLAN §10.3 (driven by WP11's Integration SmokeTest, or ?autotest=1): for every
    /// checkpoint CP1 … CP10 + CP3b it jumps there (<c>ResumeFrom</c>), waits for the sections, turns on god mode, asserts
    /// (objective set, markers resolvable, the beat's marker ids present, squad at the node set, encounter caps, music cue,
    /// LightStage within the beat's range), dwells 8 s with the AI fighting, takes a screenshot, then moves on. It records
    /// exceptions, frame time p50/p95, GC and memory, and writes <c>Logs/smoke/smoke_&lt;ts&gt;.json</c> (editor/desktop) or
    /// keeps the JSON in <see cref="LastResultJson"/> and raises <see cref="DoneEvent"/> (web: Integration forwards it to
    /// <c>window.LS_SMOKE_RESULT</c>). Pass = zero exceptions, all asserts true, every beat reached.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionSmokeTest : MonoBehaviour
    {
        /// <summary>Mission event raised when a run ends (the JSON is in <see cref="LastResultJson"/>).</summary>
        public const string DoneEvent = SequenceIds.SmokeDone;

        private static string s_lastJson;
        private readonly List<float> _frames = new List<float>(4096);
        private readonly List<string> _exceptions = new List<string>();
        private bool _running;

        /// <summary>JSON of the last finished run (also after the component is gone).</summary>
        public static string LastResultJson => s_lastJson;

        /// <summary>True while a run is in progress.</summary>
        public bool IsRunning => _running;

        /// <summary>The last result object.</summary>
        public SmokeResult Result { get; private set; }

        /// <summary>Raised with the JSON when a run ends.</summary>
        public event Action<string> Completed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_lastJson = null;

        /// <summary>Starts a run on the MissionSystems object (creates the component). Returns null without MissionSystems.</summary>
        public static MissionSmokeTest Run(SmokeOptions options)
        {
            var host = MissionSystems.Instance;
            if (host == null)
            {
                LSLog.Error(LogCategory.Mission, "Smoke test: no MissionSystems in the scene.");
                return null;
            }
            var t = host.GetComponent<MissionSmokeTest>();
            if (t == null) t = host.gameObject.AddComponent<MissionSmokeTest>();
            t.Begin(options ?? new SmokeOptions());
            return t;
        }

        /// <summary>Starts a run (ignored while one is running).</summary>
        public void Begin(SmokeOptions options)
        {
            if (_running) return;
            StartCoroutine(RunRoutine(options ?? new SmokeOptions()));
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!_running || (type != LogType.Exception && type != LogType.Assert)) return;
            if (_exceptions.Count < 200) _exceptions.Add(condition);
        }

        private IEnumerator RunRoutine(SmokeOptions o)
        {
            _running = true;
            _frames.Clear();
            _exceptions.Clear();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var result = new SmokeResult
            {
                startedAt = stamp,
                monoUsedStartBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
            };
            var gc0 = GC.CollectionCount(0);
            var director = MissionSystems.Instance != null ? MissionSystems.Instance.Director : null;
            if (director == null)
            {
                _running = false;
                yield break;
            }
            LSLog.Info(LogCategory.Mission, "Smoke test started (" + o.Checkpoints.Length + " checkpoints)");
            var beatsReached = new HashSet<int>();
            foreach (var cp in o.Checkpoints)
            {
                var r = new SmokeCheckpointResult { checkpoint = cp, beat = BeatTable.BeatOf(cp) };
                result.checkpoints.Add(r);
                yield return Visit(director, cp, o, r, stamp);
                if (r.reached) beatsReached.Add(r.beat);
            }
            result.everyBeatReached = true;
            foreach (var cp in o.Checkpoints)
                if (!beatsReached.Contains(BeatTable.BeatOf(cp))) result.everyBeatReached = false;
            if (GameServices.TryGet(out IPlayerHealth health)) health.GodMode = false;
            result.exceptions.AddRange(_exceptions);
            result.exceptionCount = _exceptions.Count;
            Percentiles(_frames, out result.frameMsP50, out result.frameMsP95);
            result.monoUsedEndBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            result.totalAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            result.gcCollections = GC.CollectionCount(0) - gc0;
            result.pass = result.exceptionCount == 0 && result.everyBeatReached;
            foreach (var c in result.checkpoints) result.pass &= c.pass;
            Result = result;
            s_lastJson = JsonUtility.ToJson(result, true);
            WriteFile(stamp, s_lastJson);
            LSLog.Info(LogCategory.Mission, "Smoke test " + (result.pass ? "PASSED" : "FAILED") + " (" + result.exceptionCount + " exceptions)");
            _running = false;
            Completed?.Invoke(s_lastJson);
            GameEvents.RaiseMissionEvent(DoneEvent);
        }

        private IEnumerator Visit(MissionDirector director, string cp, SmokeOptions o, SmokeCheckpointResult r, string stamp)
        {
            var t0 = Time.realtimeSinceStartup;
            director.ResumeFrom(cp);
            var sections = MissionMarkup.Sections(r.beat);
            GameServices.TryGet(out ISectionStreamer streamer);
            while (Time.realtimeSinceStartup - t0 < o.SectionTimeout)
            {
                var loaded = director.Beat == r.beat && !director.IsReloading;
                if (loaded && streamer != null)
                    foreach (var s in sections)
                        loaded &= streamer.IsLoaded(s);
                if (loaded) break;
                yield return null;
            }
            r.loadSeconds = Time.realtimeSinceStartup - t0;
            r.reached = director.Beat == r.beat;
            if (!r.reached) r.notes.Add("beat " + r.beat + " not reached (at " + director.Beat + ")");
            if (o.GodMode && GameServices.TryGet(out IPlayerHealth health)) health.GodMode = true;
            var settle = Time.realtimeSinceStartup + o.SettleSeconds;
            while (Time.realtimeSinceStartup < settle) yield return null;

            Assert(director, cp, r);

            var dwell = Time.realtimeSinceStartup + o.DwellSeconds;
            GameServices.TryGet(out IEncounterService enc);
            while (Time.realtimeSinceStartup < dwell)
            {
                _frames.Add(Time.unscaledDeltaTime * 1000f);
                if (enc != null) r.maxAlive = Mathf.Max(r.maxAlive, enc.AliveTotal);
                yield return null;
            }
            r.encounterCaps = r.maxAlive <= MissionMarkup.HardCap && r.maxAlive <= MissionMarkup.PeakAlive(r.beat);
            if (!r.encounterCaps) r.notes.Add("alive peak " + r.maxAlive + " > " + MissionMarkup.PeakAlive(r.beat));
            if (o.Screenshots) r.screenshot = Capture(stamp, cp);
            r.pass = r.reached && r.objectiveSet && r.markersResolvable && r.missingMarkers.Count == 0 && r.squadAtNodes &&
                     r.encounterCaps && r.musicOk && r.lightInRange;
        }

        private static void Assert(MissionDirector director, string cp, SmokeCheckpointResult r)
        {
            var def = BeatTable.Get(r.beat);
            var objectives = director.Objectives;
            r.objectiveSet = objectives.Active.Count > 0;
            if (!r.objectiveSet) r.notes.Add("no active objective");
            r.markersResolvable = true;
            foreach (var m in objectives.Markers)
            {
                if (m.Target != null || m.Offset != Vector3.zero) continue;
                r.markersResolvable = false;
                r.notes.Add("marker " + m.Id + " has no target and no position");
            }
            if (GameServices.TryGet(out IMarkerRegistry reg))
                foreach (var id in MissionMarkup.Required(r.beat))
                    if (!reg.Has(id)) r.missingMarkers.Add(id);
            var cpDef = BeatTable.Checkpoint(cp);
            r.squadAtNodes = true;
            if (cpDef != null && !string.IsNullOrEmpty(cpDef.SquadNodeSet) && GameServices.TryGet(out ISquadService squad))
            {
                r.squadAtNodes = squad.AllArrived(cpDef.SquadNodeSet);
                if (!r.squadAtNodes) r.notes.Add("squad not at " + cpDef.SquadNodeSet);
            }
            r.musicOk = true;
            if (GameServices.TryGet(out IMusicService music))
            {
                r.musicCue = music.CurrentCue;
                if (!string.IsNullOrEmpty(def.MusicCue)) r.musicOk = music.CurrentCue == def.MusicCue;
                if (!r.musicOk) r.notes.Add("music " + music.CurrentCue + " != " + def.MusicCue);
            }
            r.lightS = director.CurrentS;
            r.lightInRange = r.lightS >= def.SStart - 0.02f && r.lightS <= def.SEnd + 0.02f;
            if (!r.lightInRange) r.notes.Add("s " + r.lightS.ToString("0.00") + " outside " + def.SStart + "–" + def.SEnd);
        }

        private static string Capture(string stamp, string cp)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            try
            {
                var dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Builds", "captures", "smoke_" + stamp);
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, cp + ".png");
                ScreenCapture.CaptureScreenshot(path);
                return path;
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Mission, e);
            }
#endif
            return null;
        }

        private static void WriteFile(string stamp, string json)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            try
            {
                var dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "smoke");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "smoke_" + stamp + ".json"), json);
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Mission, e);
            }
#endif
        }

        private static void Percentiles(List<float> samples, out float p50, out float p95)
        {
            p50 = p95 = 0f;
            if (samples.Count == 0) return;
            var copy = new List<float>(samples);
            copy.Sort();
            p50 = copy[Mathf.Clamp(Mathf.RoundToInt(0.50f * (copy.Count - 1)), 0, copy.Count - 1)];
            p95 = copy[Mathf.Clamp(Mathf.RoundToInt(0.95f * (copy.Count - 1)), 0, copy.Count - 1)];
        }
    }
}
