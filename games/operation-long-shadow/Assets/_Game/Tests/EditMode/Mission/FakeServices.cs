using System;
using System.Collections;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>Recorded call with a time stamp (fake world clock).</summary>
    internal struct Call
    {
        public float Time;
        public string Name;
        public string Arg;
        public float Value;

        public override string ToString() => Time.ToString("0.00") + " " + Name + " " + Arg + " " + Value;
    }

    /// <summary>Base of the recording fakes.</summary>
    internal abstract class FakeBase
    {
        protected readonly FakeWorld World;
        public readonly List<Call> Calls = new List<Call>();

        protected FakeBase(FakeWorld world) => World = world;

        protected void Rec(string name, string arg = null, float value = 0f) =>
            Calls.Add(new Call { Time = World.Now, Name = name, Arg = arg, Value = value });

        public bool Has(string name, string arg = null)
        {
            foreach (var c in Calls)
                if (c.Name == name && (arg == null || c.Arg == arg)) return true;
            return false;
        }

        public float FirstTime(string name, string arg = null)
        {
            foreach (var c in Calls)
                if (c.Name == name && (arg == null || c.Arg == arg)) return c.Time;
            return -1f;
        }
    }

    /// <summary>VO: plays every line at once on request (+ delay) for its length; raises VOLineStarted/Finished.</summary>
    internal sealed class FakeVO : FakeBase, IVOService
    {
        private sealed class Playing
        {
            public string Id;
            public int Handle;
            public float Start;
            public float End;
            public bool Started;
        }

        private readonly List<Playing> _playing = new List<Playing>();
        private readonly HashSet<string> _played = new HashSet<string>();
        private int _handle = 1;
        public readonly Dictionary<string, float> Lengths = new Dictionary<string, float>();

        public FakeVO(FakeWorld w) : base(w) { }

        public float LengthOf(string id) => Lengths.TryGetValue(id, out var s) ? s : LineDurations.Of(id);

        public int Play(string lineId, VOOptions o = default)
        {
            foreach (var p in _playing)
                if (p.Id == lineId) return p.Handle;
            var start = World.Now + Math.Max(0f, o.Delay);
            var pl = new Playing { Id = lineId, Handle = _handle++, Start = start, End = start + LengthOf(lineId) };
            _playing.Add(pl);
            Rec("Play", lineId, o.RadioSwitchTime);
            Tick();
            return pl.Handle;
        }

        public void Tick()
        {
            for (var i = _playing.Count - 1; i >= 0; i--)
            {
                var p = _playing[i];
                if (!p.Started && World.Now >= p.Start)
                {
                    p.Started = true;
                    Rec("Started", p.Id);
                    _played.Add(p.Id);
                    GameEvents.RaiseVOLineStarted(p.Id, "X");
                }
                if (p.Started && World.Now >= p.End)
                {
                    _playing.RemoveAt(i);
                    Rec("Finished", p.Id);
                    GameEvents.RaiseVOLineFinished(p.Id);
                }
            }
        }

        public bool IsPlaying(string lineId)
        {
            foreach (var p in _playing)
                if (p.Id == lineId) return true;
            return false;
        }

        public bool IsNetBusy => _playing.Count > 0;

        public void Stop(int handle)
        {
            for (var i = _playing.Count - 1; i >= 0; i--)
            {
                if (_playing[i].Handle != handle) continue;
                Rec("Stop", _playing[i].Id);
                GameEvents.RaiseVOLineFinished(_playing[i].Id);
                _playing.RemoveAt(i);
            }
        }

        public void StopAll(float fade)
        {
            Rec("StopAll");
            _playing.Clear();
        }

        public void PlayBark(string lineId, Transform speaker, Team team) => Rec("Bark", lineId);
        public void PlayClicks(int count, Transform speaker) => Rec("Clicks", null, count);
        public float Amplitude(string speakerId) => 0f;
        public void PrepareWindow(int fromBeat, int toBeat) => Rec("Window", null, fromBeat);
        public bool HasPlayed(string lineId) => _played.Contains(lineId);
        public bool HasLine(string lineId) => true;
        public float Duration(string lineId) => LengthOf(lineId);
    }

    /// <summary>A living fake enemy.</summary>
    internal sealed class FakeActor : ITargetable
    {
        public FakeActor(string id) => ActorId = id;
        public string ActorId { get; }
        public Team Team => Team.Legion;
        public bool IsAlive { get; set; } = true;
        public Transform AimPoint => null;
        public void ApplyDamage(in DamageInfo d) { }
        public Transform Root => null;
        public Vector3 Velocity => Vector3.zero;
        public PerchTag Perch => PerchTag.Ground;
        public bool SquadTarget { get; set; }
        public float LastEngagedByPlayer => 0f;
        public void NotifyEngagedByPlayer() { }
    }

    /// <summary>Encounters E2-E11 with their GDD §7 enemies; every wave spawns on activation; enemies die on demand.</summary>
    internal sealed class FakeEncounters : FakeBase, IEncounterService
    {
        public static readonly Dictionary<string, Dictionary<string, string[]>> Table = new Dictionary<string, Dictionary<string, string[]>>
        {
            { "E2", new Dictionary<string, string[]> { { "Sentries", N("E2", 1, 2) }, { "Gate", N("E2", 3, 6) } } },
            { "E3", new Dictionary<string, string[]> { { "A", N("E3", 1, 4) }, { "Balcony", N("E3", 5, 6) }, { "B", N("E3", 7, 9) } } },
            { "E4", new Dictionary<string, string[]> { { "Square", N("E4", 1, 4) }, { "Lobby", N("E4", 5, 6) }, { "Greycoat", N("E4", 7, 8) } } },
            { "E5", new Dictionary<string, string[]> { { "Main", N("E5", 1, 3) } } },
            { "E6", new Dictionary<string, string[]> { { "Yard", N("E6", 1, 5) }, { "Gatehouse", N("E6", 6, 8) } } },
            { "E7", new Dictionary<string, string[]> { { "Breach", N("E7", 1, 3) }, { "Stair", N("E7", 4, 5) } } },
            { "E9", new Dictionary<string, string[]> { { "W1", N("E9", 1, 3) }, { "W2", N("E9", 4, 6) }, { "W3", N("E9", 7, 9) } } },
            { "E10", new Dictionary<string, string[]> { { "Truck1", N("E10", 1, 3) }, { "NVG", N("E10", 4, 4) }, { "Truck2", N("E10", 5, 7) } } },
            { "E11", new Dictionary<string, string[]> { { "Targets", new[] { "E11-T1", "E11-T2", "E11-T3", "E11-T4" } } } },
        };

        private static string[] N(string enc, int a, int b)
        {
            var r = new string[b - a + 1];
            for (var i = a; i <= b; i++) r[i - a] = enc + "-" + i.ToString("00");
            return r;
        }

        private readonly HashSet<string> _active = new HashSet<string>();
        private readonly HashSet<string> _killed = new HashSet<string>();
        private readonly Dictionary<string, FakeActor> _actors = new Dictionary<string, FakeActor>();
        /// <summary>When false, waves wait for TriggerWave (tests of the beat's own wave commands).</summary>
        public bool SpawnAllOnActivate = true;

        public FakeEncounters(FakeWorld w) : base(w) { }

        public IEnumerable<string> Active => _active;

        public void Activate(string encounterId)
        {
            Rec("Activate", encounterId);
            if (!Table.ContainsKey(encounterId) || !_active.Add(encounterId)) return;
            GameEvents.RaiseEncounterActivated(encounterId);
            foreach (var wave in Table[encounterId])
            {
                foreach (var id in wave.Value)
                    if (!_killed.Contains(id)) _actors[id] = new FakeActor(id);
                GameEvents.RaiseWaveSpawned(encounterId, wave.Key);
            }
        }

        public void TriggerWave(string encounterId, string wave) => Rec("TriggerWave", encounterId + "/" + wave);
        public void Deactivate(string encounterId) => _active.Remove(encounterId);

        public int Alive(string encounterId, string wave = null)
        {
            if (!_active.Contains(encounterId) || !Table.TryGetValue(encounterId, out var waves)) return 0;
            var n = 0;
            foreach (var w in waves)
            {
                if (wave != null && w.Key != wave) continue;
                foreach (var id in w.Value)
                    if (!_killed.Contains(id)) n++;
            }
            return n;
        }

        public int Killed(string encounterId)
        {
            var n = 0;
            if (!Table.TryGetValue(encounterId, out var waves)) return 0;
            foreach (var w in waves)
                foreach (var id in w.Value)
                    if (_killed.Contains(id)) n++;
            return n;
        }

        public bool Cleared(string encounterId) => _active.Contains(encounterId) && Alive(encounterId) == 0;

        public ITargetable Actor(string enemyId) =>
            _actors.TryGetValue(enemyId, out var a) && a.IsAlive && !_killed.Contains(enemyId) ? a : null;

        public int AliveTotal
        {
            get
            {
                var n = 0;
                foreach (var e in _active) n += Alive(e);
                return n;
            }
        }

        public float Awareness01(string enemyId) => 0f;

        public bool IsKilled(string id) => _killed.Contains(id);

        public void Kill(string enemyId, bool byPlayer = true)
        {
            if (_killed.Contains(enemyId)) return;
            var known = false;
            foreach (var e in _active)
                foreach (var w in Table[e])
                    if (Array.IndexOf(w.Value, enemyId) >= 0) known = true;
            if (!known) return;
            _killed.Add(enemyId);
            if (_actors.TryGetValue(enemyId, out var a)) a.IsAlive = false;
            Rec("Kill", enemyId);
            GameEvents.RaiseActorKilled(new KillInfo { VictimId = enemyId, VictimTeam = Team.Legion, ByPlayer = byPlayer });
            var active = new List<string>(_active);
            foreach (var e in active)
                if (Contains(e, enemyId) && Alive(e) == 0) GameEvents.RaiseEncounterCleared(e);
        }

        private static bool Contains(string enc, string id)
        {
            foreach (var w in Table[enc])
                if (Array.IndexOf(w.Value, id) >= 0) return true;
            return false;
        }

        public void KillAll(string encounterId, string wave = null, int leave = 0)
        {
            if (!_active.Contains(encounterId)) return;
            foreach (var w in Table[encounterId])
            {
                if (wave != null && w.Key != wave) continue;
                var alive = 0;
                foreach (var id in w.Value)
                    if (!_killed.Contains(id)) alive++;
                foreach (var id in w.Value)
                {
                    if (alive <= leave) break;
                    if (_killed.Contains(id)) continue;
                    Kill(id);
                    alive--;
                }
            }
        }

        /// <summary>Checkpoint restore: forget runtimes, keep the killed set given.</summary>
        public void ResetTo(IEnumerable<string> killed)
        {
            _active.Clear();
            _actors.Clear();
            _killed.Clear();
            foreach (var k in killed) _killed.Add(k);
        }

        public IEnumerable<string> KilledIds => _killed;
    }

    /// <summary>ISaveable of the fake encounters (killed ids), like WP04's EncounterSpawner.</summary>
    internal sealed class FakeEncounterSave : ISaveable
    {
        private readonly FakeEncounters _e;
        public FakeEncounterSave(FakeEncounters e) => _e = e;
        public string SaveKey => "Encounters";
        public void Capture(SaveWriter w) => w.Write("killed", SaveCodec.Join(_e.KilledIds));

        public void Restore(SaveReader r)
        {
            var list = new List<string>();
            SaveCodec.Split(r.ReadString("killed", ""), list);
            _e.ResetTo(list);
        }
    }

    /// <summary>Squad: arrives 1 s after SetNodeSet; scripted actions finish after 1 s; scripted kills land after 0.3 s.</summary>
    internal sealed class FakeSquad : FakeBase, ISquadService
    {
        private string _set;
        private float _since;
        private readonly List<KeyValuePair<float, Action>> _pending = new List<KeyValuePair<float, Action>>();
        public float ArriveSeconds = 1f;
        public bool NeverArrive;

        public FakeSquad(FakeWorld w) : base(w) { }

        public string CurrentSet => _set;

        public void SetNodeSet(string setId)
        {
            Rec("Nodes", setId);
            if (_set == setId) return;
            _set = setId;
            _since = World.Now;
        }

        public void Follow()
        {
            Rec("Follow");
            _set = null;
        }

        public void TeleportToNodeSet(string setId)
        {
            Rec("Teleport", setId);
            _set = setId;
            _since = World.Now - 100f;
        }

        public bool AllArrived(string setId) => !NeverArrive && _set == setId && World.Now - _since >= ArriveSeconds;

        public void PlayScripted(string member, string actionId, string markerId, Action done)
        {
            Rec("Scripted", member + ":" + actionId + "@" + markerId);
            _pending.Add(new KeyValuePair<float, Action>(World.Now + 1f, done));
        }

        public void ScriptedKill(string member, ITargetable target)
        {
            if (target == null) return;
            Rec("ScriptedKill", member + ":" + target.ActorId);
            var id = target.ActorId;
            _pending.Add(new KeyValuePair<float, Action>(World.Now + 0.3f, () => World.Encounters.Kill(id, false)));
        }

        public void SetChemlights(bool on) => Rec("Chemlights", on ? "on" : "off");
        public void SetLookTarget(Transform target, float weight) => Rec("Look", target == null ? "null" : "target", weight);
        public Transform Member(string id) => null;

        public void Tick()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (World.Now < _pending[i].Key) continue;
                var a = _pending[i].Value;
                _pending.RemoveAt(i);
                a?.Invoke();
            }
        }
    }

    /// <summary>LightStage: SetTarget jumps (smoothing ignored), overrides counted, PracticalsStrike raised when crossing 1.97.</summary>
    internal sealed class FakeLight : FakeBase, ILightStage
    {
        private readonly HashSet<object> _owners = new HashSet<object>();
        private float _s;
        public readonly List<KeyValuePair<float, float>> History = new List<KeyValuePair<float, float>>();

        public FakeLight(FakeWorld w) : base(w) { }

        public float S => _s;
        public StageId Stage => _s < 1f ? StageId.Golden : _s < 2f ? StageId.Sunset : _s < 3f ? StageId.Afterglow : StageId.BlueHour;

        public void SetTarget(float s, float smoothing = 2f)
        {
            Rec("SetTarget", null, s);
            Set(s);
        }

        public void SetImmediate(float s)
        {
            Rec("SetImmediate", null, s);
            Set(s);
        }

        private void Set(float s)
        {
            if (_s < 1.97f && s >= 1.97f) GameEvents.RaisePracticalsStrike();
            _s = s;
            History.Add(new KeyValuePair<float, float>(World.Now, s));
        }

        public void Override(object owner, bool on)
        {
            Rec("Override", on ? "on" : "off");
            if (on) _owners.Add(owner);
            else _owners.Remove(owner);
        }

        public bool IsOverridden => _owners.Count > 0;
        public float AISightRange => 70f;
        public float HearingScale => 1f;
        public bool AfterSunset => _s >= 2f;
        public Color HudColor => Color.white;
        public float StoryClockMinutes => StoryClock.FromS(_s);
    }

    /// <summary>Sections load at once.</summary>
    internal sealed class FakeStreamer : FakeBase, ISectionStreamer
    {
        private readonly HashSet<SectionId> _loaded = new HashSet<SectionId>();
        public FakeStreamer(FakeWorld w) : base(w) { }

        public void Request(SectionId section, bool load)
        {
            Rec(load ? "Load" : "Unload", section.ToString());
            if (load)
            {
                if (_loaded.Add(section)) GameEvents.RaiseSectionLoaded(section);
            }
            else if (_loaded.Remove(section)) GameEvents.RaiseSectionUnloaded(section);
        }

        public bool IsLoaded(SectionId section) => _loaded.Contains(section);
        public float Progress => 1f;
        public IEnumerator WaitLoaded(SectionId section) { yield break; }
        public bool IsBusy => false;
    }

    /// <summary>HUD recorder.</summary>
    internal sealed class FakeHud : FakeBase, IHud
    {
        public MissionStats Debrief;
        public FakeHud(FakeWorld w) : base(w) { }
        public void ShowPrompt(PromptData prompt) => Rec("Prompt", prompt.Id);
        public void HidePrompt(string id) => Rec("HidePrompt", id);
        public void Tutorial(string key, string text) => Rec("Tutorial", key);
        public void IntelCard(string title, Sprite photo, string counter) => Rec("IntelCard", title + "|" + counter);
        public void FailCard(string text) => Rec("FailCard", text);
        public void Banner(string text, float seconds) => Rec("Banner", text, seconds);
        public void SetOverlay(HudOverlay overlay) => Rec("Overlay", overlay.ToString());
        public void SetRing(string id, Transform target, float value01, string label) => Rec("Ring", id + "|" + label, value01);
        public void ClearRing(string id) => Rec("ClearRing", id);
        public void Letterbox(bool on, float seconds) => Rec("Letterbox", on ? "on" : "off");
        public void SetVisible(bool visible) => Rec("Visible", visible ? "on" : "off");
        public float TitleCardEnd = -1f;

        public void TitleCard(float seconds)
        {
            Rec("TitleCard", null, seconds);
            TitleCardEnd = World.RealNow + seconds;
        }

        /// <summary>Like WP08's TitleCardView: raises UI.TitleCardFinished when the bars have retracted.</summary>
        public void Tick()
        {
            if (TitleCardEnd < 0f || World.RealNow < TitleCardEnd) return;
            TitleCardEnd = -1f;
            GameEvents.RaiseMissionEvent(LongShadow.Mission.Data.MissionEvents.TitleCardFinished);
        }

        public void ShowDebrief(MissionStats stats)
        {
            Rec("Debrief");
            Debrief = stats;
        }
    }

    /// <summary>Inserts: recorded; the death insert reports back after 2 s.</summary>
    internal sealed class FakeInserts : FakeBase, IInsertService
    {
        private readonly List<KeyValuePair<float, Action>> _pending = new List<KeyValuePair<float, Action>>();
        public FakeInserts(FakeWorld w) : base(w) { }

        public void Play(string insertId, string lineId = null)
        {
            Rec("Insert", insertId + (lineId != null ? "+" + lineId : ""));
            if (lineId != null) World.VO.Play(lineId);
        }

        public void PlayDeath(Action onDone)
        {
            Rec("Death");
            _pending.Add(new KeyValuePair<float, Action>(World.RealNow + 2f, onDone));
        }

        public bool IsPlaying => _pending.Count > 0;

        public void Tick()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (World.RealNow < _pending[i].Key) continue;
                var a = _pending[i].Value;
                _pending.RemoveAt(i);
                a?.Invoke();
            }
        }
    }

    /// <summary>Cinematics: each lasts <see cref="Length"/> s; the intro raises Intro.ControlToPlayer at its end.</summary>
    internal sealed class FakeCinematics : FakeBase, ICinematicService
    {
        private readonly List<KeyValuePair<float, Action>> _pending = new List<KeyValuePair<float, Action>>();
        public float Length = 1f;
        public string CurrentId;
        public FakeCinematics(FakeWorld w) : base(w) { }

        public void Play(string id, Action onDone)
        {
            Rec("Cinematic", id);
            CurrentId = id;
            GameEvents.RaiseCinematicStarted(id);
            _pending.Add(new KeyValuePair<float, Action>(World.RealNow + Length, () =>
            {
                if (id == SequenceIds.Intro) GameEvents.RaiseMissionEvent(SequenceIds.IntroControlToPlayer);
                GameEvents.RaiseCinematicEnded(id);
                CurrentId = null;
                onDone?.Invoke();
            }));
        }

        public bool IsPlaying => _pending.Count > 0;
        public string Current => CurrentId;
        public void Skip() { }

        public void Tick()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (World.RealNow < _pending[i].Key) continue;
                var a = _pending[i].Value;
                _pending.RemoveAt(i);
                a?.Invoke();
            }
        }
    }

    internal sealed class FakeScreenFX : FakeBase, IScreenFX
    {
        public FakeScreenFX(FakeWorld w) : base(w) { }
        public void SetLowHealth(float amount01) { }
        public void PulseSuppression(float amount01) => Rec("Pulse", null, amount01);
        public void Death(float t01) => Rec("Death", null, t01);
        public void Flash(float ev, float seconds) => Rec("Flash", null, ev);
        public void Glitch(float intensity, float seconds) => Rec("Glitch");
        public void Glare(float amount01) { }
        public void FadeToBlack(float seconds) => Rec("FadeToBlack", null, seconds);
        public void FadeIn(float seconds) => Rec("FadeIn", null, seconds);
        public void FadeToWhite(float seconds) => Rec("FadeToWhite", null, seconds);
        public bool IsBlack => false;
    }

    internal sealed class FakeMusic : FakeBase, IMusicService
    {
        public FakeMusic(FakeWorld w) : base(w) { }
        public void PlayCue(string cueId, int layer = 0, float fade = 1f)
        {
            Rec("Cue", cueId, layer);
            CurrentCue = cueId;
            CurrentLayer = layer;
        }

        public void SetLayer(int layer, float fade) => CurrentLayer = layer;
        public void Stinger(string id) => Rec("Stinger", id);

        public void Stop(float fade)
        {
            Rec("Stop", null, fade);
            CurrentCue = null;
        }

        public void HardCut()
        {
            Rec("HardCut");
            CurrentCue = null;
        }

        public string CurrentCue { get; private set; }
        public int CurrentLayer { get; private set; }
    }

    internal sealed class FakeAudio : FakeBase, IAudioService
    {
        private int _h = 1;
        public FakeAudio(FakeWorld w) : base(w) { }
        public void Play(string cueId, Vector3 position, float volume = 1f) => Rec("Play", cueId);
        public void Play2D(string cueId, float volume = 1f) => Rec("Play2D", cueId);
        public void PlayPitched(string cueId, Vector3 position, float volume, float pitch) => Rec("Play", cueId);
        public int PlayLoop(string cueId, Transform follow) { Rec("Loop", cueId); return _h++; }
        public void StopLoop(int handle, float fade) => Rec("StopLoop");
        public int PlayAttached(string cueId, Transform follow, float volume = 1f) { Rec("Attached", cueId, volume); return _h++; }
        public void SetListenerOverride(Transform target) { }
        public void PlayWeaponShot(string weaponId, Vector3 position, bool isPlayer) { }
        public ZoneType CurrentZone => ZoneType.OpenDesert;
        public void Tinnitus(float seconds) => Rec("Tinnitus", null, seconds);
        public void SetCategoryGain(string category, float linear) { }
        public void Duck(string category, float db, float seconds) { }
        public bool HasCue(string cueId) => true;
    }

    internal sealed class FakeTimeScale : FakeBase, ITimeScaleService
    {
        private readonly Dictionary<int, KeyValuePair<int, float>> _req = new Dictionary<int, KeyValuePair<int, float>>();
        private int _h = 1;
        public FakeTimeScale(FakeWorld w) : base(w) { }

        public int Push(float scale, int priority, string reason)
        {
            Rec("Push", reason, scale);
            _req[_h] = new KeyValuePair<int, float>(priority, scale);
            return _h++;
        }

        public void Pop(int handle)
        {
            Rec("Pop", null, handle);
            _req.Remove(handle);
        }

        public bool SetScale(int handle, float scale)
        {
            if (!_req.TryGetValue(handle, out var r)) return false;
            Rec("SetScale", null, scale);
            _req[handle] = new KeyValuePair<int, float>(r.Key, scale);
            return true;
        }

        public float Current
        {
            get
            {
                var best = -1;
                var scale = 1f;
                foreach (var kv in _req)
                {
                    if (kv.Value.Key <= best) continue;
                    best = kv.Value.Key;
                    scale = kv.Value.Value;
                }
                return scale;
            }
        }

        public int Live => _req.Count;
    }

    internal sealed class FakeInputLock : FakeBase, IInputLock
    {
        private readonly Dictionary<int, InputLockFlags> _locks = new Dictionary<int, InputLockFlags>();
        private int _h = 1;
        public FakeInputLock(FakeWorld w) : base(w) { }

        public int Lock(InputLockFlags flags, string reason)
        {
            Rec("Lock", reason);
            _locks[_h] = flags;
            return _h++;
        }

        public void Unlock(int handle) => _locks.Remove(handle);

        public bool IsLocked(InputLockFlags flags)
        {
            foreach (var kv in _locks)
                if ((kv.Value & flags) != 0) return true;
            return false;
        }

        public int Live => _locks.Count;
    }

    /// <summary>Player rig: position from the fake world; records the mission calls.</summary>
    internal sealed class FakeRig : FakeBase, IPlayerRig
    {
        public FakeRig(FakeWorld w) : base(w) { }
        public Transform Root => null;
        public Transform Eye => null;
        public Camera WorldCamera => null;
        public Camera ViewmodelCamera => null;
        public Transform ViewmodelRoot => null;
        public Vector3 Velocity => Vector3.zero;
        public Stance Stance => Stance.Stand;
        public bool IsGrounded => true;
        public bool IsADS => false;
        public float AdsBlend => 0f;
        public float SpeedMultiplier { set { } }
        public float TimeSinceSprintEnd => 10f;
        public float BobPhase => 0f;
        public float BobAmount01 => 0f;
        public Ray ViewRay => new Ray(World.PlayerPos + Vector3.up * 1.6f, Direction);
        public Vector3 Direction = Vector3.forward;
        public bool IsGlassing => false;
        public float Pitch => 0f;
        public float Yaw => 0f;
        public float? ForcedWalk;
        public event Action<float> Landed { add { } remove { } }
        public void AddAimDelta(float pitch, float yaw) { }
        public void SetViewPunch(Quaternion punch) { }
        public void SetAdsState(bool isAds, float blend01) { }
        public void RequestFov(object owner, float vFov, float blendTime) { }
        public void ReleaseFov(object owner) { }
        public void Shake(float trauma) => Rec("Shake", null, trauma);

        public void Teleport(Vector3 position, float yaw)
        {
            Rec("Teleport", null, yaw);
            World.PlayerPos = position;
        }

        public void SetLookAngles(float pitch, float yaw) { }
        public void SetViewActive(bool active) { }

        public void SetForcedWalk(float? speed)
        {
            ForcedWalk = speed;
            Rec("ForcedWalk", speed.HasValue ? "on" : "off", speed ?? 0f);
        }

        public void SetLookLimits(Quaternion center, float yaw, float up, float down) { }
        public void ClearLookLimits() { }
        public void SetDamageModifiers(float scale, float floorHp) => Rec("DamageModifiers", null, scale);
        public void Mount(Transform seat) { }
        public void Dismount() { }
        public void SetBinocularsAvailable(bool available) => Rec("Binoculars", available ? "on" : "off");
        public void SetBodyVisibility(bool viewmodel, bool shadowProxy) { }
        public float? EyeHeightOverride;

        public void SetEyeHeightOverride(float? eyeHeight, float blendSeconds)
        {
            EyeHeightOverride = eyeHeight;
            Rec("EyeHeight", eyeHeight.HasValue ? "on" : "off", eyeHeight ?? 0f);
        }
    }

    internal sealed class FakeHealth : FakeBase, IPlayerHealth
    {
        public FakeHealth(FakeWorld w) : base(w) { }
        public Team Team => Team.Player;
        public bool IsAlive => Current > 0f;
        public Transform AimPoint => null;
        public void ApplyDamage(in DamageInfo d) { }
        public float Current { get; set; } = 100f;
        public float Max => 100f;
        public float TimeSinceDamage => 10f;
        public bool GodMode { get; set; }

        public void ResetFull()
        {
            Rec("ResetFull");
            Current = 100f;
        }
    }

    internal sealed class FakeWeapons : FakeBase, IWeaponService
    {
        public FakeWeapons(FakeWorld w) : base(w) { }
        public string CurrentWeaponId => WeaponIds.HushPuppy;
        public int CurrentSlot => 2;
        public string WeaponInSlot(int slot) => slot == 1 ? WeaponIds.Commando : WeaponIds.HushPuppy;
        public bool HasWeapon(string weaponId) => true;
        public int Frags => 3;
        public void SetHolstered(object owner, bool holstered) { }
        public bool IsHolstered => false;
        public void SelectSlot(int slot) { }
        public void Give(string weaponId, int reserveRounds = -1) { }
        public void Resupply() => Rec("Resupply");
        public void ResetLoadout() => Rec("ResetLoadout");

        public void MountDoorGun(Transform pivot)
        {
            Rec("Mount");
            IsDoorGunMounted = true;
        }

        public void DismountDoorGun()
        {
            Rec("Dismount");
            IsDoorGunMounted = false;
        }

        public bool IsDoorGunMounted { get; private set; }
    }

    internal sealed class FakeFpBody : FakeBase, IFirstPersonBody
    {
        public FakeFpBody(FakeWorld w) : base(w) { }

        public void PlayWatchCheck(float seconds, Action onDone)
        {
            Rec("Watch", null, seconds);
            onDone?.Invoke();
        }

        public void PlayPistolRise(float seconds, Action onDone)
        {
            Rec("Pistol", null, seconds);
            onDone?.Invoke();
        }

        public void SetFists(bool clenched) { }
        public bool IsPlaying => false;
    }

    internal sealed class FakeCombat : FakeBase, ICombatService
    {
        public FakeCombat(FakeWorld w) : base(w) { }
        public void FireAI(in AIShotRequest r) { }
        public void ThrowGrenadeAI(Vector3 from, Vector3 to, Team team) { }
        public void FireRocket(Vector3 from, Vector3 to, Team team, Action<Vector3> onImpact) { }
        public void Explode(Vector3 position, float damage, float lethalRadius, float maxRadius, GameObject instigator, Team team) => Rec("Explode", null, damage);
        public void SpawnSmoke(Vector3 position, Color color, float seconds) { }
        public void SpawnPickup(string weaponId, Vector3 position) { }

        public GameObject SpawnAmmoCrate(Vector3 position, Quaternion rotation)
        {
            Rec("Crate", position.ToString());
            return null;
        }
    }

    internal sealed class FakeTargets : FakeBase, ITargetRegistry
    {
        public int Near;
        public FakeTargets(FakeWorld w) : base(w) { }
        public void Register(ITargetable t) { }
        public void Unregister(ITargetable t) { }
        public void GetHostilesOf(Team team, List<ITargetable> results) => results.Clear();
        public int CountWithin(Team team, Vector3 position, float radius) => team == Team.Legion ? Near : 0;
        public ITargetable Find(string actorId) => null;
        public ITargetable Player => null;
        public void GetAll(List<ITargetable> results) => results.Clear();
    }

    internal sealed class FakeSettings : FakeBase, ISettingsService
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public FakeSettings(FakeWorld w) : base(w) { }
        public GameSettings Current { get; } = new GameSettings();
        public DifficultyProfile Difficulty => DifficultyProfile.For(LongShadow.Core.Difficulty.Regular);
        public event Action Changed { add { } remove { } }
        public void Apply(GameSettings settings) { }
        public void Save() => Rec("Save");
        public string GetValue(string key) => Values.TryGetValue(key, out var v) ? v : null;
        public void SetValue(string key, string value) => Values[key] = value;
    }

    /// <summary>A shootable target registered in Core Destructibles.</summary>
    internal sealed class FakeDestructible : IDestructible
    {
        public FakeDestructible(string id) => Id = id;
        public string Id { get; }
        public bool IsDestroyed { get; private set; }
        public float Health01 => IsDestroyed ? 0f : 1f;
        public bool Invulnerable { get; set; }
        public Transform Root => null;
        public void ResetIntact() => IsDestroyed = false;

        public void ForceDestroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            GameEvents.RaiseDestructibleDestroyed(Id);
        }
    }

    /// <summary>Records intel spawns (no GameObjects).</summary>
    internal sealed class FakeSpawner : IMissionSpawner
    {
        public readonly HashSet<string> Spawned = new HashSet<string>();
        public void SpawnIntel(IntelDef intel, Vector3 position, Quaternion rotation, Transform anchor) => Spawned.Add(intel.Id);
        public void DespawnIntel(string intelId) => Spawned.Remove(intelId);
        public void Clear() => Spawned.Clear();
        public Sprite IntelPhoto(IntelDef intel) => null;
        public Transform Anchor(string id, Vector3 position) => null;
    }
}
