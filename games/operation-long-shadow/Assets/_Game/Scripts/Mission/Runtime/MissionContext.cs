using System;
using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Checkpoints;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// The helper surface the beat scripts are written against (TECH_PLAN §7.11: WaitTrigger, WaitLine, WaitCleared,
    /// WaitAlive, WaitObjective, WaitSeconds, Line, Nodes, Wave, Objective, Marker, Checkpoint, Insert, Cinematic, Event).
    /// Every service is looked up lazily through <see cref="GameServices"/> and every helper degrades when its service is
    /// missing (no VO → lines last their real length; no AI → encounters count as cleared; no squad → arrived; no
    /// streamer → loaded; no cinematic service → the Mission fallback runs), so a beat never hangs in a test scene.
    /// Waits re-evaluate every tick; nothing here allocates per frame (waits allocate once when created).
    /// </summary>
    public sealed class MissionContext
    {
        private readonly MissionDirector _d;
        private ServiceRef<IVOService> _vo;
        private ServiceRef<IEncounterService> _encounters;
        private ServiceRef<ISquadService> _squad;
        private ServiceRef<ILightStage> _light;
        private ServiceRef<ISectionStreamer> _streamer;
        private ServiceRef<IHud> _hud;
        private ServiceRef<IInsertService> _insert;
        private ServiceRef<ICinematicService> _cinematic;
        private ServiceRef<IScreenFX> _fx;
        private ServiceRef<IMusicService> _music;
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<ITimeScaleService> _time;
        private ServiceRef<IInputLock> _input;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IPlayerHealth> _health;
        private ServiceRef<IWeaponService> _weapons;
        private ServiceRef<IFirstPersonBody> _fpBody;
        private ServiceRef<ICombatService> _combat;
        private ServiceRef<ITargetRegistry> _targets;
        private ServiceRef<IMarkerRegistry> _markers;

        internal MissionContext(MissionDirector director) => _d = director;

        // ------------------------------------------------------------------ state

        /// <summary>The director.</summary>
        public MissionDirector Director => _d;
        /// <summary>The script runner.</summary>
        public ScriptRunner Runner => _d.Runner;
        /// <summary>The event log.</summary>
        public MissionEventLog Log => _d.Log;
        /// <summary>Objectives and markers.</summary>
        public ObjectiveService Objectives => _d.Objectives;
        /// <summary>Checkpoints.</summary>
        public CheckpointService Checkpoints => _d.Checkpoints;
        /// <summary>Gates and blockers.</summary>
        public Gates Gates => _d.Gates;
        /// <summary>Requested lines.</summary>
        public LineTracker Lines => _d.Lines;
        /// <summary>Gameplay time (scaled).</summary>
        public float Now => _d.Runner.Time;
        /// <summary>Real time.</summary>
        public float RealNow => _d.Runner.UnscaledTime;
        /// <summary>Current beat.</summary>
        public int Beat => _d.Beat;
        /// <summary>Event-log sequence at the start of the current beat.</summary>
        public int BeatSeq => _d.BeatStartSeq;
        /// <summary>The player's position.</summary>
        public Vector3 PlayerPosition => _d.PlayerPosition;

        // ------------------------------------------------------------------ services (null when missing)

        public IVOService VO => _vo.Value;
        public IEncounterService Encounters => _encounters.Value;
        public ISquadService Squad => _squad.Value;
        public ILightStage LightStage => _light.Value;
        public ISectionStreamer Streamer => _streamer.Value;
        public IHud Hud => _hud.Value;
        public IInsertService Inserts => _insert.Value;
        public ICinematicService Cinematics => _cinematic.Value;
        public IScreenFX ScreenFX => _fx.Value;
        public IMusicService Music => _music.Value;
        public IAudioService Audio => _audio.Value;
        public ITimeScaleService TimeScale => _time.Value;
        public IInputLock InputLock => _input.Value;
        public IPlayerRig Rig => _rig.Value;
        public IPlayerHealth Health => _health.Value;
        public IWeaponService Weapons => _weapons.Value;
        public IFirstPersonBody FirstPersonBody => _fpBody.Value;
        public ICombatService Combat => _combat.Value;
        public ITargetRegistry Targets => _targets.Value;
        public IMarkerRegistry Markers => _markers.Value;

        // ------------------------------------------------------------------ time and tasks

        /// <summary>Waits gameplay seconds (frozen by pause and inserts).</summary>
        public Wait Seconds(float seconds)
        {
            var end = Now + seconds;
            return new Wait(WaitKind.Seconds, null, () => Now >= end, radius: seconds);
        }

        /// <summary>Waits real seconds.</summary>
        public Wait RealSeconds(float seconds)
        {
            var end = RealNow + seconds;
            return new Wait(WaitKind.Seconds, "real", () => RealNow >= end, radius: seconds);
        }

        /// <summary>Waits until a gameplay time.</summary>
        public Wait At(float time) => new Wait(WaitKind.Seconds, "at", () => Now >= time, radius: Math.Max(0f, time - Now));

        /// <summary>Starts a side task owned by the current beat (stopped with it).</summary>
        public ScriptTask Run(IEnumerator routine, string name) => Runner.Start(routine, name, _d.CurrentBeatTask);

        /// <summary>
        /// Starts a task owned by the mission flow instead of the beat: it survives the beat's end (line chains such as
        /// "D051 1.5 s after D050" when the player leaves the beat quickly) and stops only on a reload or a jump.
        /// </summary>
        public ScriptTask RunDetached(IEnumerator routine, string name) => Runner.Start(routine, name, _d.FlowTask ?? _d.CurrentBeatTask);

        /// <summary>Runs <paramref name="action"/> after gameplay seconds, as a side task of the beat.</summary>
        public ScriptTask After(float seconds, Action action, string name = "after")
        {
            return Run(AfterRoutine(seconds, action), name);
        }

        private IEnumerator AfterRoutine(float seconds, Action action)
        {
            yield return Seconds(seconds);
            action?.Invoke();
        }

        /// <summary>Registers an action that runs when the current beat ends or is stopped (reload, cold jump).</summary>
        public void OnBeatEnd(Action cleanup) => _d.AddCleanup(cleanup);

        // ------------------------------------------------------------------ triggers and positions

        /// <summary>
        /// The player reached the trigger during this beat or is inside it now (Appendix A geometry is polled while the
        /// TriggerVolume is missing from the scene).
        /// </summary>
        public Wait Trigger(string id)
        {
            _d.WatchTrigger(id);
            var since = BeatSeq;
            return new Wait(WaitKind.Trigger, id, () => Log.Happened(EventChannel.TriggerEnter, id, since) || Inside(id));
        }

        /// <summary>The player enters the trigger after this wait was created (or is inside it).</summary>
        public Wait TriggerFromNow(string id)
        {
            _d.WatchTrigger(id);
            var since = Log.Seq;
            return new Wait(WaitKind.Trigger, id, () => Log.Happened(EventChannel.TriggerEnter, id, since) || Inside(id));
        }

        /// <summary>True if the player reached the trigger during this beat.</summary>
        public bool Reached(string id)
        {
            _d.WatchTrigger(id);
            return Log.Happened(EventChannel.TriggerEnter, id, BeatSeq) || Inside(id);
        }

        /// <summary>True while the player is inside the trigger.</summary>
        public bool Inside(string id)
        {
            _d.WatchTrigger(id);
            if (Log.IsInside(id)) return true;
            var reg = Markers;
            return reg != null && reg.TryGet(id, out TriggerVolume tv) && tv != null && tv.PlayerInside;
        }

        /// <summary>World position of a marker: the scene marker, else the Appendix A fallback.</summary>
        public bool TryPosition(string markerId, out Vector3 position)
        {
            var t = MarkerTransform(markerId);
            if (t != null)
            {
                position = t.position;
                return true;
            }
            return MissionMarkers.TryGetFallback(markerId, out position);
        }

        /// <summary>The marker's transform in the scene, or null.</summary>
        public Transform MarkerTransform(string markerId) => markerId == null ? null : Markers?.Get(markerId);

        /// <summary>
        /// The marker's transform, or a helper transform at its Appendix A position (look targets, positional audio),
        /// or null where neither exists.
        /// </summary>
        public Transform MarkerOrAnchor(string markerId)
        {
            var t = MarkerTransform(markerId);
            if (t != null) return t;
            return MissionMarkers.TryGetFallback(markerId, out var p) ? _d.Spawner?.Anchor(markerId, p) : null;
        }

        /// <summary>True when the player is within <paramref name="radius"/> m of a marker (and above <paramref name="minY"/>).</summary>
        public bool IsNear(string markerId, float radius, float minY = float.NegativeInfinity)
        {
            if (!TryPosition(markerId, out var p)) return false;
            var pos = PlayerPosition;
            return pos.y >= minY && (pos - p).sqrMagnitude <= radius * radius;
        }

        /// <summary>Waits until the player is near a marker.</summary>
        public Wait PlayerNear(string markerId, float radius, float minY = float.NegativeInfinity) =>
            new Wait(WaitKind.PlayerNear, markerId, () => IsNear(markerId, radius, minY), radius: radius);

        // ------------------------------------------------------------------ lines

        /// <summary>Plays a line (fire and forget). Returns the VO handle.</summary>
        public int Line(string lineId, VOOptions options = default)
        {
            var h = Lines.Play(lineId, options);
            _d.OnLineRequested(lineId);
            return h;
        }

        /// <summary>Waits until a line the mission requested has finished.</summary>
        public Wait LineDone(string lineId) => new Wait(WaitKind.Line, lineId, () => Lines.IsDone(lineId));

        /// <summary>Plays a line and waits for it to finish.</summary>
        public IEnumerator Say(string lineId, VOOptions options = default)
        {
            Line(lineId, options);
            yield return LineDone(lineId);
        }

        /// <summary>Plays a line after the previous one finished plus a gap (GDD "D019 finished", "+1.5 s").</summary>
        public IEnumerator SayAfter(string previous, float gap, string lineId, VOOptions options = default)
        {
            yield return LineDone(previous);
            if (gap > 0f) yield return Seconds(gap);
            yield return Say(lineId, options);
        }

        /// <summary>Plays an enemy bark over the radio (BK_LEG_08 at the floods, BK_GRY_09 at FL1).</summary>
        public void RadioBark(string lineId) =>
            Line(lineId, new VOOptions { ForceRadio = true, Priority = VOPriority.Bark });

        // ------------------------------------------------------------------ events

        /// <summary>Raises a mission event (also records it).</summary>
        public void Raise(string eventId) => _d.Raise(eventId);

        /// <summary>A mission event raised during this beat.</summary>
        public Wait Event(string eventId)
        {
            var since = BeatSeq;
            return new Wait(WaitKind.Event, eventId, () => Log.Happened(EventChannel.Mission, eventId, since));
        }

        /// <summary>True if a mission event was raised during this beat.</summary>
        public bool HappenedThisBeat(EventChannel channel, string id) => Log.Happened(channel, id, BeatSeq);

        /// <summary>An InteractionPoint was used during this beat.</summary>
        public Wait Interacted(string id)
        {
            var since = BeatSeq;
            return new Wait(WaitKind.Interaction, id, () => Log.Happened(EventChannel.Interacted, id, since));
        }

        /// <summary>A destructible was destroyed (now or earlier in the mission).</summary>
        public Wait Destroyed(string id) => new Wait(WaitKind.Destroyed, id, () => IsDestroyed(id));

        /// <summary>True if the destructible is destroyed (event or its state).</summary>
        public bool IsDestroyed(string id) =>
            Log.Happened(EventChannel.Destroyed, id) || (Destructibles.TryGet(id, out var d) && d.IsDestroyed);

        // ------------------------------------------------------------------ objectives

        /// <summary>Shows an objective of GDD §7.12 (optionally with replaced text).</summary>
        public void Objective(string objectiveId, string text = null) => Objectives.Show(objectiveId, text);

        /// <summary>Completes an objective.</summary>
        public void Complete(string objectiveId) => Objectives.Complete(objectiveId);

        /// <summary>True once completed.</summary>
        public bool IsComplete(string objectiveId) => Objectives.IsComplete(objectiveId);

        /// <summary>Waits until an objective is completed.</summary>
        public Wait ObjectiveDone(string objectiveId) =>
            new Wait(WaitKind.Objective, objectiveId, () => Objectives.IsComplete(objectiveId));

        // ------------------------------------------------------------------ encounters (WP04)

        /// <summary>Activates an encounter (placed enemies spawn, waves arm; killed ones stay dead).</summary>
        public void Activate(string encounterId)
        {
            var e = Encounters;
            if (e == null)
            {
                LSLog.WarnOnce("mission-noenc", LogCategory.Mission, "No IEncounterService: encounters are treated as cleared.");
                return;
            }
            e.Activate(encounterId);
        }

        /// <summary>Starts a wave now (idempotent with WP04's own trigger).</summary>
        public void Wave(string encounterId, string wave) => Encounters?.TriggerWave(encounterId, wave);

        /// <summary>True when every wave is spawned and dead (true without an encounter service).</summary>
        public bool IsCleared(string encounterId)
        {
            var e = Encounters;
            return e == null || e.Cleared(encounterId) || Log.Happened(EventChannel.EncounterCleared, encounterId, BeatSeq);
        }

        /// <summary>Waits until an encounter is cleared.</summary>
        public Wait Cleared(string encounterId) => new Wait(WaitKind.Encounter, encounterId, () => IsCleared(encounterId));

        /// <summary>True when a wave has spawned (this beat).</summary>
        public bool WaveSpawned(string encounterId, string wave) =>
            Log.Happened(EventChannel.WaveSpawned, MissionEventLog.WaveKey(encounterId, wave), BeatSeq);

        /// <summary>Waits until a wave has spawned.</summary>
        public Wait WaveSpawnedWait(string encounterId, string wave) =>
            new Wait(WaitKind.Event, MissionEventLog.WaveKey(encounterId, wave), () => WaveSpawned(encounterId, wave) || IsCleared(encounterId));

        /// <summary>Waits until a spawned wave has at most <paramref name="count"/> alive (or the encounter is cleared).</summary>
        public Wait AliveAtMost(string encounterId, string wave, int count) =>
            new Wait(WaitKind.Encounter, encounterId, () =>
            {
                var e = Encounters;
                if (e == null || IsCleared(encounterId)) return true;
                if (wave != null && !WaveSpawned(encounterId, wave)) return false;
                return e.Alive(encounterId, wave) <= count;
            }, wave, count);

        /// <summary>Living enemies of an encounter (0 without a service).</summary>
        public int Alive(string encounterId, string wave = null) => Encounters?.Alive(encounterId, wave) ?? 0;

        /// <summary>True once the enemy died (ActorKilled), or always without an encounter service.</summary>
        public bool IsKilled(string enemyId) => Encounters == null || Log.Happened(EventChannel.Killed, enemyId);

        /// <summary>Waits until every listed enemy is dead.</summary>
        public Wait Killed(params string[] enemyIds) =>
            new Wait(WaitKind.Killed, enemyIds.Length > 0 ? enemyIds[0] : null, () =>
            {
                for (var i = 0; i < enemyIds.Length; i++)
                    if (!IsKilled(enemyIds[i])) return false;
                return true;
            }, enemyIds.Length > 1 ? string.Join(",", enemyIds) : null);

        /// <summary>A living enemy, or null.</summary>
        public ITargetable Actor(string enemyId) => Encounters?.Actor(enemyId);

        // ------------------------------------------------------------------ squad (WP04)

        /// <summary>Sends the squad to a node set.</summary>
        public void Nodes(string setId) => Squad?.SetNodeSet(setId);

        /// <summary>Follow mode.</summary>
        public void Follow() => Squad?.Follow();

        /// <summary>
        /// Waits until the squad is at a node set; after <paramref name="timeout"/> s the squad is teleported there (logged)
        /// so a stuck squadmate can never soft-lock a beat (GDD §15 catch-up).
        /// </summary>
        public Wait SquadAt(string setId, float timeout = 30f)
        {
            var end = Now + timeout;
            var forced = false;
            return new Wait(WaitKind.Squad, setId, () =>
            {
                var s = Squad;
                if (s == null || forced || s.AllArrived(setId)) return true;
                if (Now < end) return false;
                LSLog.Warn(LogCategory.Mission, "Squad did not reach " + setId + " in " + timeout + " s; teleporting it.");
                s.TeleportToNodeSet(setId);
                forced = true;
                return true;
            }, radius: timeout);
        }

        /// <summary>Plays a squad scripted action and waits for it (at most <paramref name="timeout"/> s).</summary>
        public IEnumerator SquadAction(string member, string actionId, string markerId, float timeout = 20f)
        {
            var s = Squad;
            if (s == null) yield break;
            var done = false;
            s.PlayScripted(member, actionId, markerId, () => done = true);
            var end = Now + timeout;
            yield return new Wait(WaitKind.Squad, member + ":" + actionId, () => done || Now >= end);
        }

        /// <summary>Plays a squad scripted action without waiting.</summary>
        public void SquadActionNoWait(string member, string actionId, string markerId) =>
            Squad?.PlayScripted(member, actionId, markerId, null);

        /// <summary>A squad member's scripted lethal shot on an enemy (also flags him as a squad target).</summary>
        public void ScriptedKill(string member, string enemyId)
        {
            var a = Actor(enemyId);
            if (a == null || !a.IsAlive) return;
            a.SquadTarget = true;
            Squad?.ScriptedKill(member, a);
        }

        /// <summary>A squad member's root, or null.</summary>
        public Transform Member(string memberId) => Squad?.Member(memberId);

        // ------------------------------------------------------------------ gates, sections, crates, intel

        /// <summary>Opens a gate / arms a trigger / enables an interaction / releases a destructible group.</summary>
        public void Open(string gateId) => Gates.Set(gateId, true);

        /// <summary>Closes it.</summary>
        public void Close(string gateId) => Gates.Set(gateId, false);

        /// <summary>Requests a section load.</summary>
        public void Load(SectionId section) => _d.RequestSection(section, true);

        /// <summary>Requests a section unload.</summary>
        public void Unload(SectionId section) => _d.RequestSection(section, false);

        /// <summary>Requests the section the beat table names for a quiet point (line or event id), if any.</summary>
        public void QuietPoint(string at)
        {
            if (BeatTable.Get(Beat).TryQuietLoad(at, out var section)) Load(section);
        }

        /// <summary>Waits until a section is loaded (true without a streamer).</summary>
        public Wait SectionReady(SectionId section) =>
            new Wait(WaitKind.Section, Sections.SceneName(section), () => Streamer == null || Streamer.IsLoaded(section));

        /// <summary>Spawns the resupply crate at a CRATE.* marker once (GDD §13.2).</summary>
        public void Crate(string markerId) => _d.SpawnCrate(markerId);

        /// <summary>Spawns an intel pickup (unless collected).</summary>
        public void SpawnIntel(string intelId) => _d.SpawnIntel(intelId);

        /// <summary>Waits until an intel item is collected.</summary>
        public Wait Intel(string intelId) => new Wait(WaitKind.Intel, intelId, () => Objectives.HasIntel(intelId));

        /// <summary>Collects an intel item for the player (Sparrow's codebook).</summary>
        public bool CollectIntel(string intelId) => _d.CollectIntel(intelId);

        // ------------------------------------------------------------------ light (WP10)

        /// <summary>Narrows the LightStage driver range of this beat.</summary>
        public void LightRange(float min, float max) => _d.LightDriver.SetRange(min, max);

        /// <summary>Takes or releases <c>ILightStage.Override</c> for an owner (released automatically when the beat ends).</summary>
        public void LightOverride(object owner, bool on) => _d.SetLightOverride(owner, on);

        /// <summary>Sets s now without events (checkpoint jumps, cold starts: SetImmediate never strikes the practicals).</summary>
        public void LightImmediate(float s) => _d.SetLightImmediate(s);

        /// <summary>
        /// Keys s for a scripted sequence that holds the override (MID-A, the cinema swap): <c>SetTarget(s, 0)</c> every
        /// frame, so crossing 1.97 raises PracticalsStrike (WP10_LOOK contract).
        /// </summary>
        public void LightKey(float s) => _d.SetLightKey(s);

        /// <summary>Current LightStage s.</summary>
        public float LightS => _d.CurrentS;

        // ------------------------------------------------------------------ presentation (WP06, WP08, WP09)

        /// <summary>Plays a music cue (null/empty stops).</summary>
        public void PlayMusic(string cueId, int layer = 0, float fade = 1f)
        {
            var m = Music;
            if (m == null) return;
            if (string.IsNullOrEmpty(cueId)) m.Stop(fade);
            else m.PlayCue(cueId, layer, fade);
        }

        /// <summary>Plays a 3D sound at a marker (or its fallback position).</summary>
        public void SfxAt(string cueId, string markerId)
        {
            if (TryPosition(markerId, out var p)) Audio?.Play(cueId, p);
        }

        /// <summary>Plays a 3D sound at a position.</summary>
        public void Sfx(string cueId, Vector3 position) => Audio?.Play(cueId, position);

        /// <summary>Shows a one-time tutorial prompt (once per session; WP08 keeps the per-browser "once").</summary>
        public void Tutorial(string key, string text)
        {
            var flag = MissionFlags.TutorialPrefix + key;
            if (_d.Flag(flag)) return;
            _d.SetFlag(flag, true);
            Hud?.Tutorial(key, text);
        }

        /// <summary>Locks input channels until <see cref="Unlock"/> or the end of the beat.</summary>
        public int Lock(InputLockFlags flags, string reason)
        {
            var l = InputLock;
            if (l == null) return 0;
            var h = l.Lock(flags, reason);
            if (h != 0) OnBeatEnd(() => l.Unlock(h));
            return h;
        }

        /// <summary>Releases an input lock.</summary>
        public void Unlock(int handle)
        {
            if (handle != 0) InputLock?.Unlock(handle);
        }

        /// <summary>Slow motion for real seconds (GDD §7.6 blast 0.3, §7.7 breach 0.35; TECH_PLAN §5.4 priority SlowMo).</summary>
        public IEnumerator SlowMo(float scale, float realSeconds, string reason)
        {
            var ts = TimeScale;
            var h = ts?.Push(scale, TimePriority.SlowMo, reason) ?? 0;
            var popped = false;
            void Pop()
            {
                if (popped || h == 0) return;
                popped = true;
                ts.Pop(h);
            }
            OnBeatEnd(Pop);
            yield return RealSeconds(realSeconds);
            Pop();
        }

        /// <summary>Plays a glitch insert (GDD §5.5); without an insert service its line plays as a normal line.</summary>
        public void Insert(string insertId, string lineId = null)
        {
            var i = Inserts;
            if (i == null)
            {
                if (lineId != null) Line(lineId);
                return;
            }
            Lines.NoteRequested(lineId);
            i.Play(insertId, lineId);
        }

        /// <summary>
        /// Plays a WP09 cinematic and waits for it (onDone, CinematicEnded or <paramref name="maxRealSeconds"/>). Without a
        /// cinematic service the Mission <paramref name="fallback"/> runs instead.
        /// </summary>
        public IEnumerator Cinematic(string id, float maxRealSeconds, Func<IEnumerator> fallback)
        {
            var c = Cinematics;
            if (c == null)
            {
                LSLog.WarnOnce("mission-nocine:" + id, LogCategory.Mission, "No ICinematicService: running the Mission fallback for " + id + ".");
                if (fallback != null) yield return fallback();
                yield break;
            }
            var done = false;
            var seq = Log.Seq;
            var end = RealNow + maxRealSeconds;
            c.Play(id, () => done = true);
            yield return new Wait(WaitKind.Sequence, id, () => done || Log.Happened(EventChannel.CinematicEnded, id, seq) || RealNow >= end);
            if (!done && RealNow >= end) LSLog.Warn(LogCategory.Mission, "Cinematic " + id + " did not report back in " + maxRealSeconds + " s.");
        }

        /// <summary>Requests a checkpoint save (deferred by the GDD §17 rules).</summary>
        public void Checkpoint(string cpId) => Checkpoints.Save(cpId);

        /// <summary>Reads a mission flag.</summary>
        public bool Flag(string id) => _d.Flag(id);

        /// <summary>Sets a mission flag.</summary>
        public void SetFlag(string id, bool value) => _d.SetFlag(id, value);

        /// <summary>Sets the player's eye-height override (<see cref="IPlayerRig.SetEyeHeightOverride"/>, Beat 1 prone/kneel).</summary>
        public void SetPosture(float? eyeHeight, float blendSeconds)
        {
            var rig = Rig;
            if (rig == null)
            {
                LSLog.WarnOnce("mission-noposture", LogCategory.Mission,
                    "No IPlayerRig: the Beat 1 prone start and kneel are skipped.");
                return;
            }
            rig.SetEyeHeightOverride(eyeHeight, blendSeconds);
        }
    }
}
