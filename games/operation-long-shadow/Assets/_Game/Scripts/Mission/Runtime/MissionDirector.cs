using System;
using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Beats;
using LongShadow.Mission.Checkpoints;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// <see cref="IMissionDirector"/> (TECH_PLAN §7.11): the beat state machine Intro → B1 … B11 → Outro → Debrief. It runs
    /// the beat scripts on a deterministic <see cref="ScriptRunner"/>, owns the <see cref="ObjectiveService"/>, the
    /// <see cref="CheckpointService"/> and the <see cref="Gates"/>, drives LightStage progress (GDD §9.1), keeps flags and
    /// debrief stats, runs the death flow (TECH_PLAN §7.4: slow-down + death insert + reload) and the in-place checkpoint
    /// reload (§5.5, D11), and handles cold jumps (?cp=, SELECT BEAT, the smoke test).
    /// <para>Plain C#. <c>MissionSystems</c> (MonoBehaviour in S_Persistent) creates it, registers the services and ticks it;
    /// the EditMode tests do the same against a fake world.</para>
    /// </summary>
    public sealed class MissionDirector : IMissionDirector, ISaveable
    {
        /// <summary>Save key.</summary>
        public const string Key = "Mission";
        /// <summary>Death slow-down (TECH_PLAN §5.4: 0.3 over 0.6 s; GDD §8.4).</summary>
        public const float DeathSlowScale = 0.3f;
        /// <summary>Length of the death slow-down in real seconds.</summary>
        public const float DeathSlowSeconds = 0.6f;
        /// <summary>Longest wait for the death insert to report back.</summary>
        public const float DeathInsertTimeout = 4.5f;
        /// <summary>Teletype line on black while the checkpoint reloads (GDD §8.4).</summary>
        public const string ResumingText = "SESSION 12 — RESUMING";
        /// <summary>Seconds between trigger fallback polls.</summary>
        public const float TriggerPollInterval = 0.1f;
        /// <summary>Story clock during the intro: the INSERTION happens at 17:52 (CINEMATIC_DIRECTION §2).</summary>
        public const float InsertionClockMinutes = 17 * 60 + 52;

        private readonly BeatScript[] _beats;
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _watchedTriggers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _scratch = new List<string>(32);
        private readonly List<Action> _cleanups = new List<Action>(16);
        private readonly HashSet<object> _lightOwners = new HashSet<object>();
        private readonly Dictionary<string, GameObject> _crates = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly float[] _pathProgress = new float[BeatTable.PlayableBeats + 1];

        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IPlayerHealth> _health;
        private ServiceRef<ILightStage> _light;
        private ServiceRef<ICriticalPath> _path;
        private ServiceRef<ISectionStreamer> _streamer;
        private ServiceRef<IVOService> _vo;
        private ServiceRef<IMusicService> _music;
        private ServiceRef<ISquadService> _squad;
        private ServiceRef<IHud> _hud;
        private ServiceRef<IInsertService> _insert;
        private ServiceRef<IScreenFX> _fx;
        private ServiceRef<ITimeScaleService> _time;
        private ServiceRef<IInputLock> _input;
        private ServiceRef<IMarkerRegistry> _markers;
        private ServiceRef<ICombatService> _combat;
        private ServiceRef<IWeaponService> _weapons;
        private ServiceRef<ITargetRegistry> _targets;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IClickGate> _gate;
        private ServiceRef<IAudioService> _audio;

        private ScriptTask _flowTask;
        private ScriptTask _beatTask;
        private ScriptTask _reloadTask;
        private bool _attached;
        private bool _registered;
        private int _beat;
        private float _fallbackS;
        private float _nextTriggerPoll;
        private Vector3 _lastPlayerPos;
        private int _deathTimeHandle;

        /// <summary>Creates the director with the default beat scripts (Intro, B1 … B11, Outro).</summary>
        public MissionDirector(IMissionSpawner spawner = null) : this(spawner, BeatScripts.CreateAll()) { }

        /// <summary>Creates the director with explicit beat scripts (index = beat number 0..12).</summary>
        public MissionDirector(IMissionSpawner spawner, BeatScript[] beats)
        {
            if (beats == null || beats.Length != BeatTable.Outro + 1) throw new ArgumentException("13 beat scripts expected (0..12)", nameof(beats));
            _beats = beats;
            Spawner = spawner;
            Runner = new ScriptRunner();
            Log = new MissionEventLog(() => Runner.Time);
            Lines = new LineTracker(Log, () => Runner.Time);
            Objectives = new ObjectiveService(() => PlayerPosition, id => Log.Happened(EventChannel.Killed, id));
            Checkpoints = new CheckpointService(() => Runner.Time, EvaluateDeferral, deathInsert => BeginReload(deathInsert, null));
            Gates = new Gates();
            LightDriver = new LightProgressDriver();
            StatsTracker = new MissionStatsTracker();
            Context = new MissionContext(this);
            Objectives.IntelAdded += _ => StatsTracker.SetIntel(Objectives.IntelCount, Objectives.OptionalCount);
        }

        // ------------------------------------------------------------------ owned parts

        /// <summary>Script runner (beat scripts, side tasks, death and reload flows).</summary>
        public ScriptRunner Runner { get; }
        /// <summary>Event log.</summary>
        public MissionEventLog Log { get; }
        /// <summary>Line tracker.</summary>
        public LineTracker Lines { get; }
        /// <summary>Objectives (also IObjectiveService).</summary>
        public ObjectiveService Objectives { get; }
        /// <summary>Checkpoints (also ICheckpointService).</summary>
        public CheckpointService Checkpoints { get; }
        /// <summary>Gates and blockers.</summary>
        public Gates Gates { get; }
        /// <summary>LightStage progress driver.</summary>
        public LightProgressDriver LightDriver { get; }
        /// <summary>Debrief statistics.</summary>
        public MissionStatsTracker StatsTracker { get; }
        /// <summary>The beat scripts' helper surface.</summary>
        public MissionContext Context { get; }
        /// <summary>Scene object factory (intel pickups), or null.</summary>
        public IMissionSpawner Spawner { get; set; }

        /// <summary>Overrides the player position (tests, dev scenes without a rig).</summary>
        public Func<Vector3> PlayerPositionOverride { get; set; }
        /// <summary>Overrides critical-path progress 0..1 for the current beat (Beat 11: exfil time / 50 s).</summary>
        public Func<float> PathProgressOverride { get; set; }
        /// <summary>Sequence of the event log when the current beat started.</summary>
        public int BeatStartSeq { get; private set; }
        /// <summary>The running beat script task, or null.</summary>
        public ScriptTask CurrentBeatTask => _beatTask;
        /// <summary>True while the death or reload flow runs.</summary>
        public bool IsReloading => _reloadTask != null && !_reloadTask.IsDone;
        /// <summary>The checkpoint id the current beat was entered at (null on a normal entry).</summary>
        public string EntryCheckpoint { get; private set; }
        /// <summary>True once the debrief opened.</summary>
        public bool Finished { get; private set; }

        /// <summary>The player's position (override, rig, or the last teleport target).</summary>
        public Vector3 PlayerPosition
        {
            get
            {
                if (PlayerPositionOverride != null) return PlayerPositionOverride();
                var root = _rig.Value?.Root;
                if (root != null) _lastPlayerPos = root.position;
                return _lastPlayerPos;
            }
        }

        /// <summary>Current LightStage s (the service, or the director's own value without one).</summary>
        public float CurrentS => _light.Value?.S ?? _fallbackS;

        // ------------------------------------------------------------------ IMissionDirector

        /// <inheritdoc/>
        public int Beat => _beat;
        /// <inheritdoc/>
        public bool IsRunning { get; private set; }
        /// <inheritdoc/>
        public MissionStats Stats => StatsTracker.Stats;

        /// <inheritdoc/>
        public float StoryClockMinutes
        {
            get
            {
                if (_beat <= BeatTable.Intro) return InsertionClockMinutes;
                if (_beat >= BeatTable.Outro) return StoryClock.BoothOutroMinutes;
                var light = _light.Value;
                return light != null ? light.StoryClockMinutes : StoryClock.FromS(_fallbackS);
            }
        }

        /// <inheritdoc/>
        public void StartMission()
        {
            LSLog.Info(LogCategory.Mission, "StartMission");
            StopEverything();
            ResetMissionState(BeatTable.Intro);
            IsRunning = true;
            _flowTask = Runner.Start(MissionFlow(BeatTable.Intro, null), "Mission");
        }

        /// <inheritdoc/>
        public void ResumeFrom(string cpId)
        {
            var def = BeatTable.Checkpoint(cpId);
            if (def == null)
            {
                LSLog.Error(LogCategory.Mission, "ResumeFrom: unknown checkpoint '" + cpId + "'");
                return;
            }
            if (IsRunning && Checkpoints.HasSnapshot(cpId))
            {
                BeginReload(false, cpId);
                return;
            }
            ColdJump(cpId);
        }

        /// <inheritdoc/>
        public void QuitToSession()
        {
            LSLog.Info(LogCategory.Mission, "QuitToSession");
            StopEverything();
            IsRunning = false;
            StatsTracker.Counting = false;
            Objectives.Clear();
            Spawner?.Clear();
            Checkpoints.CancelPending();
            _music.Value?.Stop(1.5f);
            _vo.Value?.StopAll(0.3f);
            Raise(MissionEvents.QuitToSession);
            _fx.Value?.FadeToBlack(0.5f);
            var gate = _gate.Value;
            if (gate != null)
            {
                gate.SetVisible(true);
                gate.SetReady(true);
            }
        }

        /// <inheritdoc/>
        public void Raise(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            GameEvents.RaiseMissionEvent(eventId);
            QuietLoadFor(eventId);
        }

        /// <inheritdoc/>
        public bool Flag(string id) => id != null && _flags.Contains(id);

        /// <inheritdoc/>
        public void SetFlag(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (value) _flags.Add(id);
            else _flags.Remove(id);
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Subscribes to GameEvents (host OnEnable).</summary>
        public void Enable()
        {
            if (_attached) return;
            _attached = true;
            Log.Attach();
            StatsTracker.Attach();
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.SectionLoaded += OnSectionLoaded;
        }

        /// <summary>Unsubscribes (host OnDisable).</summary>
        public void Disable()
        {
            if (!_attached) return;
            _attached = false;
            Log.Detach();
            StatsTracker.Detach();
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.SectionLoaded -= OnSectionLoaded;
        }

        /// <summary>Registers IMissionDirector, IObjectiveService and ICheckpointService, and the mission's own saveables.</summary>
        public void RegisterServices()
        {
            if (_registered) return;
            _registered = true;
            GameServices.Register<IMissionDirector>(this);
            GameServices.Register<IObjectiveService>(Objectives);
            GameServices.Register<ICheckpointService>(Checkpoints);
            Checkpoints.Register(this);
            Checkpoints.Register(Objectives);
            Checkpoints.Register(Gates);
        }

        /// <summary>Removes the services (host OnDestroy).</summary>
        public void UnregisterServices()
        {
            if (!_registered) return;
            _registered = false;
            GameServices.Unregister<IMissionDirector>(this);
            GameServices.Unregister<IObjectiveService>(Objectives);
            GameServices.Unregister<ICheckpointService>(Checkpoints);
        }

        /// <summary>Advances the mission (host Update: scaled and unscaled delta time).</summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            Runner.Tick(deltaTime, unscaledDeltaTime);
            if (!IsRunning) return;
            StatsTracker.Tick(unscaledDeltaTime);
            Checkpoints.Tick();
            Objectives.Tick(Runner.Time);
            if (Runner.UnscaledTime >= _nextTriggerPoll)
            {
                _nextTriggerPoll = Runner.UnscaledTime + TriggerPollInterval;
                PollTriggerFallbacks();
            }
            DriveLight();
        }

        // ------------------------------------------------------------------ flow

        private IEnumerator MissionFlow(int startBeat, string entryCp)
        {
            for (var b = startBeat; b <= BeatTable.Outro; b++)
            {
                EnterBeat(b, b == startBeat ? entryCp : null);
                var script = _beats[b];
                _beatTask = Runner.Start(script.Run(Context, EntryCheckpoint), "Beat " + b + " " + BeatTable.Get(b).Name, _flowTask);
                yield return _beatTask;
                var faulted = _beatTask.Error != null;
                Runner.Stop(_beatTask);
                RunCleanups();
                if (faulted) LSLog.Error(LogCategory.Mission, "Beat " + b + " script faulted; continuing with the next beat.");
                GameEvents.RaiseBeatCompleted(b);
            }
            _beatTask = null;
            Finish();
        }

        private void EnterBeat(int beat, string entryCp)
        {
            Checkpoints.CommitPending(); // GDD §17: a deferred save commits before the next beat's first spawn
            RunCleanups();
            PathProgressOverride = null;
            _beat = beat;
            EntryCheckpoint = entryCp;
            BeatStartSeq = Log.Seq;
            var def = BeatTable.Get(beat);
            LSLog.Info(LogCategory.Mission, "Beat " + beat + " " + def.Name + (entryCp != null ? " (entry " + entryCp + ")" : ""));
            StatsTracker.Counting = def.IsPlayable;
            Objectives.DropBefore(beat);
            foreach (var s in def.UnloadOnEnter) RequestSection(s, false);
            foreach (var s in def.LoadOnEnter) RequestSection(s, true);
            // Quiet-point loads of earlier beats that a fast player outran (their line never played): catch up now.
            foreach (var s in BeatTable.SectionsAtStart(beat)) RequestSection(s, true);
            foreach (var g in def.CloseOnEnter) Gates.Set(g, false);
            if (def.MusicCue != null)
            {
                var m = _music.Value;
                if (m != null)
                {
                    if (def.MusicCue.Length == 0) m.Stop(def.MusicFade);
                    else m.PlayCue(def.MusicCue, def.MusicLayer, def.MusicFade);
                }
            }
            _vo.Value?.PrepareWindow(beat, beat + 1);
            LightDriver.BeginBeat(beat, def.SStart, def.SEnd, CurrentS);
            LightDriver.Enabled = def.IsPlayable;
            if (beat >= 1 && beat < _pathProgress.Length) _pathProgress[beat] = 0f;
            foreach (var c in def.Crates) SpawnCrate(c);
            _watchedTriggers.Clear();
            GameEvents.RaiseBeatStarted(beat);
        }

        private void Finish()
        {
            LSLog.Info(LogCategory.Mission, "Mission complete: debrief");
            StatsTracker.Counting = false;
            StatsTracker.SetIntel(Objectives.IntelCount, Objectives.OptionalCount);
            Finished = true;
            foreach (var s in BeatTable.UnloadAfterOutro) RequestSection(s, false);
            _settings.Value?.SetValue(StorageKeys.CompletedOnce, "1");
            _settings.Value?.Save();
            Raise(MissionEvents.Debrief);
            _hud.Value?.ShowDebrief(Stats.Clone());
            foreach (var line in DebriefModel.Build(Stats, Objectives.HasIntel)) LSLog.Info(LogCategory.Mission, line);
            IsRunning = false;
        }

        private void ResetMissionState(int beat)
        {
            Finished = false;
            _flags.Clear();
            StatsTracker.Clear();
            Objectives.Clear();
            Objectives.ResetProgress();
            Checkpoints.ClearSnapshots();
            Gates.ResetToBeat(beat);
            Lines.Clear();
            Log.Clear();
            Spawner?.Clear();
            _crates.Clear();
            Array.Clear(_pathProgress, 0, _pathProgress.Length);
            _beat = beat;
        }

        private void StopEverything()
        {
            if (_reloadTask != null) Runner.Stop(_reloadTask);
            _reloadTask = null;
            Runner.StopAll();
            _flowTask = null;
            _beatTask = null;
            RunCleanups();
            ReleaseDeathTimeScale();
            Checkpoints.Suspended = false;
        }

        // ------------------------------------------------------------------ cold jumps (?cp=, SELECT BEAT, smoke test)

        private void ColdJump(string cpId)
        {
            var def = BeatTable.Checkpoint(cpId);
            LSLog.Info(LogCategory.Mission, "Cold jump to " + cpId + " (beat " + def.Beat + ")");
            StopEverything();
            ResetMissionState(def.Beat);
            IsRunning = true;
            ApplyColdState(cpId);
            _flowTask = Runner.Start(MissionFlow(def.Beat, cpId), "Mission");
        }

        /// <summary>
        /// The world state at a checkpoint in a normal run, from data: earlier objectives complete, main intel of earlier
        /// beats collected, gates at their beat-start state, flags of finished set pieces, sections, light, loadout, player
        /// and squad at the checkpoint (TECH_PLAN §5.5 "cold" variant; used when no snapshot exists).
        /// </summary>
        private void ApplyColdState(string cpId)
        {
            var def = BeatTable.Checkpoint(cpId);
            var beat = def.Beat;
            foreach (var spec in ObjectiveTable.All)
                if (spec.Beat < beat && !spec.Optional) Objectives.MarkComplete(spec.Id);
            foreach (var intel in IntelTable.All)
                if (!intel.Optional && intel.Beat < beat) Objectives.MarkIntel(intel.Id);
            StatsTracker.SetIntel(Objectives.IntelCount, Objectives.OptionalCount);
            Gates.ResetToBeat(beat);
            if (beat > 2) SetFlag(MissionFlags.B2GateOpen, true);
            if (beat > 4) SetFlag(MissionFlags.B4SkySwap, true);
            if (beat > 5) SetFlag(MissionFlags.B5MidADone, true);
            if (beat > 6) SetFlag(MissionFlags.B6Blown, true);
            if (beat > 8) SetFlag(MissionFlags.B8MidBDone, true);
            if (beat > 9) SetFlag(MissionFlags.B9Drilled, true);
            var wanted = BeatTable.SectionsAtStart(beat);
            for (var i = 0; i < Sections.Count; i++)
            {
                var s = (SectionId)i;
                if (s == SectionId.Booth || s == SectionId.TownFar) continue;
                if (!wanted.Contains(s)) RequestSection(s, false);
            }
            foreach (var s in wanted) RequestSection(s, true);
            SetLightImmediate(def.EntryS);
            var w = _weapons.Value;
            if (w != null)
            {
                w.ResetLoadout();
                w.Resupply();
            }
            _health.Value?.ResetFull();
            TeleportToCheckpoint(cpId);
        }

        // ------------------------------------------------------------------ death and reload (TECH_PLAN §5.5, §7.4)

        private void OnPlayerDied(DamageInfo d)
        {
            if (!IsRunning || IsReloading || _beat < 1 || _beat >= BeatTable.PlayableBeats) return;
            _reloadTask = Runner.Start(DeathFlow(), "Death", null, true);
        }

        private IEnumerator DeathFlow()
        {
            var ts = _time.Value;
            ReleaseDeathTimeScale();
            _deathTimeHandle = ts?.Push(DeathSlowScale, TimePriority.Death, "Death") ?? 0; // GDD §8.4 0.6 s slow-down
            var start = Runner.UnscaledTime;
            while (Runner.UnscaledTime - start < DeathSlowSeconds)
            {
                _fx.Value?.Death(Mathf.Clamp01((Runner.UnscaledTime - start) / DeathSlowSeconds));
                yield return null;
            }
            _fx.Value?.Death(1f);
            yield return ReloadFlow(true, null);
        }

        /// <summary>Starts the reload flow (Restart Checkpoint, OOB, death, ResumeFrom with a snapshot).</summary>
        public void BeginReload(bool deathInsert, string cpId)
        {
            if (!IsRunning)
            {
                if (cpId != null) ColdJump(cpId);
                return;
            }
            if (IsReloading) return;
            _reloadTask = Runner.Start(ReloadFlow(deathInsert, cpId), "Reload", null, true);
        }

        private IEnumerator ReloadFlow(bool deathInsert, string cpId)
        {
            Checkpoints.Suspended = true;
            var input = _input.Value;
            var lockHandle = input?.Lock(InputLockFlags.All, "Checkpoint reload") ?? 0;
            try
            {
                if (deathInsert)
                {
                    var insert = _insert.Value;
                    var done = insert == null;
                    insert?.PlayDeath(() => done = true);
                    var end = Runner.UnscaledTime + (insert == null ? 1f : DeathInsertTimeout);
                    while (!done && Runner.UnscaledTime < end) yield return null;
                }
                else
                {
                    _fx.Value?.FadeToBlack(0.35f);
                    var end = Runner.UnscaledTime + 0.4f;
                    while (Runner.UnscaledTime < end) yield return null;
                }
                _fx.Value?.FadeToBlack(0f);
                if (deathInsert) _hud.Value?.Banner(ResumingText, 1.6f);
                ReleaseDeathTimeScale();

                var cp = cpId ?? Checkpoints.Last ?? BeatTable.StartCheckpoint(Math.Max(1, Math.Min(_beat, BeatTable.PlayableBeats)));
                var def = BeatTable.Checkpoint(cp);
                if (def == null)
                {
                    LSLog.Error(LogCategory.Mission, "Reload: no checkpoint to reload (" + cp + ")");
                    yield break;
                }
                Runner.Stop(_flowTask);
                _flowTask = null;
                _beatTask = null;
                RunCleanups();
                Lines.Clear();
                _vo.Value?.StopAll(0.15f);
                var restored = Checkpoints.Restore(cp) > 0;
                if (!restored)
                {
                    LSLog.Warn(LogCategory.Mission, "No snapshot for " + cp + "; rebuilding its state from data.");
                    ResetMissionState(def.Beat);
                    ApplyColdState(cp);
                }
                else
                {
                    _health.Value?.ResetFull();
                    TeleportToCheckpoint(cp);
                }
                GameEvents.RaiseCheckpointLoaded(cp);
                var settle = Runner.UnscaledTime + 0.25f;
                while (Runner.UnscaledTime < settle) yield return null;
                _flowTask = Runner.Start(MissionFlow(def.Beat, cp), "Mission");
                settle = Runner.UnscaledTime + 0.2f;
                while (Runner.UnscaledTime < settle) yield return null;
                _fx.Value?.FadeIn(0.8f);
            }
            finally
            {
                if (lockHandle != 0) input?.Unlock(lockHandle);
                ReleaseDeathTimeScale();
                Checkpoints.Suspended = false;
            }
        }

        private void ReleaseDeathTimeScale()
        {
            if (_deathTimeHandle == 0) return;
            _time.Value?.Pop(_deathTimeHandle);
            _deathTimeHandle = 0;
        }

        private CheckpointDeferral.Reason EvaluateDeferral()
        {
            var pos = PlayerPosition;
            var reg = _targets.Value;
            var near = reg == null
                ? 0
                : reg.CountWithin(Team.Legion, pos, CheckpointDeferral.EnemyRadius) + reg.CountWithin(Team.Greycoat, pos, CheckpointDeferral.EnemyRadius);
            var hp = _health.Value?.Current ?? 100f;
            return CheckpointDeferral.Evaluate(near, Log.GrenadeLive, hp);
        }

        /// <summary>Teleports the player (and the squad) to a checkpoint marker, or its Appendix A fallback.</summary>
        public void TeleportToCheckpoint(string cpId)
        {
            var def = BeatTable.Checkpoint(cpId);
            if (def == null) return;
            var pos = def.Position;
            var yaw = def.Yaw;
            var nodes = def.SquadNodeSet;
            var reg = _markers.Value;
            if (reg != null && reg.TryGet(cpId, out CheckpointMarker marker) && marker != null)
            {
                pos = marker.Position;
                yaw = marker.PlayerYaw;
                if (!string.IsNullOrEmpty(marker.SquadNodeSetId)) nodes = marker.SquadNodeSetId;
            }
            _lastPlayerPos = pos;
            _rig.Value?.Teleport(pos, yaw);
            var squad = _squad.Value;
            if (squad != null)
            {
                if (!string.IsNullOrEmpty(nodes)) squad.TeleportToNodeSet(nodes);
                else squad.Follow();
            }
            Log.Clear();
        }

        // ------------------------------------------------------------------ helpers used by MissionContext

        internal void AddCleanup(Action cleanup)
        {
            if (cleanup != null) _cleanups.Add(cleanup);
        }

        private void RunCleanups()
        {
            for (var i = _cleanups.Count - 1; i >= 0; i--)
            {
                try
                {
                    _cleanups[i]?.Invoke();
                }
                catch (Exception e)
                {
                    LSLog.Exception(LogCategory.Mission, e);
                }
            }
            _cleanups.Clear();
            if (_lightOwners.Count > 0)
            {
                var light = _light.Value;
                foreach (var o in _lightOwners) light?.Override(o, false);
                _lightOwners.Clear();
            }
        }

        internal void WatchTrigger(string id)
        {
            if (!string.IsNullOrEmpty(id)) _watchedTriggers.Add(id);
        }

        internal void SetLightOverride(object owner, bool on)
        {
            if (owner == null) return;
            if (on) _lightOwners.Add(owner);
            else _lightOwners.Remove(owner);
            _light.Value?.Override(owner, on);
        }

        internal void SetLightKey(float s)
        {
            _fallbackS = s;
            _light.Value?.SetTarget(s, 0f);
            LightDriver.RaiseFloor(s);
        }

        internal void SetLightImmediate(float s)
        {
            _fallbackS = s;
            _light.Value?.SetImmediate(s);
            LightDriver.RaiseFloor(s);
        }

        /// <summary>Requests a section load or unload (logged; without a streamer nothing happens).</summary>
        public void RequestSection(SectionId section, bool load)
        {
            var s = _streamer.Value;
            if (s == null) return;
            if (load == s.IsLoaded(section) && !s.IsBusy) return;
            s.Request(section, load);
        }

        internal void SpawnCrate(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return;
            if (_crates.TryGetValue(markerId, out var existing) && existing != null) return;
            var combat = _combat.Value;
            if (combat == null) return;
            if (!Context.TryPosition(markerId, out var pos)) return;
            var t = Context.MarkerTransform(markerId);
            var rot = t != null ? t.rotation : Quaternion.identity;
            _crates[markerId] = combat.SpawnAmmoCrate(pos, rot);
        }

        internal void SpawnIntel(string intelId)
        {
            var def = IntelTable.Get(intelId);
            if (def == null || def.MarkerId == null || Objectives.HasIntel(intelId) || Spawner == null) return;
            if (!Context.TryPosition(def.MarkerId, out var pos)) return;
            var t = Context.MarkerTransform(def.MarkerId);
            Spawner.SpawnIntel(def, pos, t != null ? t.rotation : Quaternion.identity, t);
        }

        /// <summary>Collects an intel item: counts it, completes its objective, shows the intel card, plays its line.</summary>
        public bool CollectIntel(string intelId)
        {
            var def = IntelTable.Get(intelId);
            if (def == null || !Objectives.CollectIntel(intelId)) return false;
            Spawner?.DespawnIntel(intelId);
            StatsTracker.SetIntel(Objectives.IntelCount, Objectives.OptionalCount);
            var count = def.Optional ? Objectives.OptionalCount : Objectives.IntelCount;
            _hud.Value?.IntelCard(def.Title, Spawner?.IntelPhoto(def), def.Counter(count));
            _audio.Value?.Play2D(MissionCues.UiIntelCard);
            var line = IntelTable.LineFor(intelId);
            if (line != null) Lines.Play(line);
            return true;
        }

        /// <summary>Stores "intro seen" (enables the skip on the next run; CINEMATIC_DIRECTION §2 allows it from the first run).</summary>
        public void MarkIntroSeen()
        {
            var settings = _settings.Value;
            if (settings == null) return;
            settings.SetValue(StorageKeys.IntroSeen, "1");
        }

        internal void OnLineRequested(string lineId) => QuietLoadFor(lineId);

        private void QuietLoadFor(string at)
        {
            // Quiet points are unique line/event ids (GDD §6.7); a chained line may play just after its beat ended.
            foreach (var b in BeatTable.Beats)
                if (b.TryQuietLoad(at, out var section)) RequestSection(section, true);
        }

        /// <summary>The running mission flow task (owner of line chains that may outlive their beat).</summary>
        internal ScriptTask FlowTask => _flowTask;

        private void OnSectionLoaded(SectionId section)
        {
            Gates.ApplyAll();
            if (!IsRunning) return;
            // Pickups and crates of the current beat whose section streamed in after the beat started.
            foreach (var intel in IntelTable.All)
                if ((intel.Beat == _beat || (intel.Optional && intel.Beat < _beat && _beat < intel.ExpiresAtBeat)) && !Objectives.HasIntel(intel.Id) &&
                    Objectives.IsActive(intel.ObjectiveId))
                    SpawnIntel(intel.Id);
        }

        private void PollTriggerFallbacks()
        {
            if (_watchedTriggers.Count == 0) return;
            var reg = _markers.Value;
            var pos = PlayerPosition;
            Collider playerCollider = null;
            var root = _rig.Value?.Root;
            if (root != null) playerCollider = root.GetComponent<Collider>(); // the CharacterController: WP04's wave conditions need it
            _scratch.Clear();
            _scratch.AddRange(_watchedTriggers);
            for (var i = 0; i < _scratch.Count; i++)
            {
                var id = _scratch[i];
                if (reg != null && reg.TryGet(id, out TriggerVolume tv) && tv != null) continue;
                if (!MissionTriggers.TryGetShape(id, out var shape)) continue;
                var inside = shape.Contains(pos);
                if (inside == Log.IsInside(id)) continue;
                LSLog.WarnOnce("trigger-fallback:" + id, LogCategory.Mission,
                    "TriggerVolume '" + id + "' is not in the scene; using its Appendix A geometry.");
                if (inside) GameEvents.RaiseTriggerEntered(id, playerCollider);
                else GameEvents.RaiseTriggerExited(id, playerCollider);
            }
        }

        private void DriveLight()
        {
            if (!LightDriver.Enabled || _beat < 1 || _beat > BeatTable.PlayableBeats || IsReloading) return;
            var light = _light.Value;
            var completion = Objectives.CompletionOfBeat(_beat);
            float path;
            if (PathProgressOverride != null) path = PathProgressOverride();
            else if (_path.Value != null) path = _path.Value.Progress01(PlayerPosition, _beat);
            else path = CriticalPathKnots.Project01(_beat, PlayerPosition);
            if (_beat < _pathProgress.Length)
            {
                if (path > _pathProgress[_beat]) _pathProgress[_beat] = path;
                path = _pathProgress[_beat];
            }
            var overridden = light != null && light.IsOverridden;
            if (!LightDriver.Update(completion, path, overridden, CurrentS)) return;
            if (light != null) light.SetTarget(LightDriver.Target, LightProgressDriver.Smoothing);
            else _fallbackS = LightDriver.Target;
        }

        // ------------------------------------------------------------------ ISaveable

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("beat", _beat);
            w.Write("s", CurrentS);
            w.Write("flags", SaveCodec.Join(_flags));
            w.Write("killed", SaveCodec.Join(Log.Ids(EventChannel.Killed)));
            w.Write("destroyed", SaveCodec.Join(Log.Ids(EventChannel.Destroyed)));
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            _beat = r.ReadInt("beat", _beat);
            var s = r.ReadFloat("s", CurrentS);
            _flags.Clear();
            SaveCodec.Split(r.ReadString("flags", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) _flags.Add(_scratch[i]);
            Log.Clear();
            SaveCodec.Split(r.ReadString("killed", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) Log.Record(EventChannel.Killed, _scratch[i]);
            SaveCodec.Split(r.ReadString("destroyed", string.Empty), _scratch);
            for (var i = 0; i < _scratch.Count; i++) Log.Record(EventChannel.Destroyed, _scratch[i]);
            SetLightImmediate(s);
        }
    }
}
