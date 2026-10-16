using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Ambience
{
    /// <summary>
    /// The ambience beds (TECH_PLAN §7.9, GDD §10.2): every bed of <see cref="AmbienceTable"/> is evaluated five times a
    /// second against the LightStage <c>s</c> (<see cref="ILightStage"/>), the beat, the listener's acoustic zone
    /// (ZoneTag via the AudioService), the distance to the station and the transmitter racks, and follows its target gain
    /// with the bed's own fade (doors use a faster one). Beds are 2D stereo loops in the Ambience category played with
    /// sample-accurate scheduled loops (gapless AAC, TECH_PLAN §3.12); each bed rotates between its two variants every
    /// few loop bodies with an equal-power crossfade so the 20 s loops never read as loops. Silent beds stop and, after
    /// 30 s, release their clip data.
    /// <para>Booth: on <c>CinematicStarted(Intro|Outro)</c> or <see cref="AudioEvents.AmbienceBoothOn"/> the world beds
    /// fade out and the booth room tone fades in (until <c>Intro.ControlToPlayer</c>, <c>CinematicEnded(Intro)</c> or
    /// <see cref="AudioEvents.AmbienceBoothOff"/>); during glitch inserts the booth tone plays on the insert bus while the
    /// world is paused. <see cref="AudioEvents.AmbienceMute"/> fades everything out (silence as a tool).</para>
    /// Works without any other service: no LightStage means golden hour, no mission director means every beat window is
    /// open.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 8)]
    [DisallowMultipleComponent]
    public sealed class AmbienceDirector : MonoBehaviour
    {
        private static readonly string[] s_racks = { "B9.R1", "B9.R2", "B9.R3", "B9.R4" };

        [SerializeField, Tooltip("LIB_Audio; when empty the registered library is used (Libraries.Get).")]
        private AudioLibrary _library;
        [SerializeField, Range(4, 32), Tooltip("Looping takes available to all beds (each uses two AudioSources).")]
        private int _players = 14;
        [SerializeField] private float _evaluateInterval = 0.2f;
        [SerializeField, Tooltip("Loop bodies before a bed rotates to its other variant (WP06_SFX import notes).")]
        private int _cyclesPerVariant = 3;
        [SerializeField] private float _variantCrossfade = 4f;
        [SerializeField, Tooltip("Fade used for a second after the listener changes acoustic zone (walking through a door).")]
        private float _zoneChangeFade = 0.6f;
        [SerializeField] private float _boothFade = 2f;
        [SerializeField, Tooltip("Seconds a silent bed keeps its clip data loaded.")] private float _unloadAfter = 30f;
        [SerializeField] private float _loadTimeout = 3f;

        private BedState[] _beds = new BedState[0];
        private BedState _booth;
        private BedPlayer[] _pool = new BedPlayer[0];
        private ServiceRef<ILightStage> _light;
        private ServiceRef<IMissionDirector> _mission;
        private AmbienceContext _ctx = AmbienceContext.Default;
        private bool _hasForcedContext;
        private AmbienceContext _forced;
        private int _eventBeat = -1;
        private bool _transmittersDownEvent;
        private bool _boothMode;
        private bool _muted;
        private bool _insert;
        private ZoneType _lastZone = ZoneType.OpenDesert;
        private float _zoneFastUntil;
        private float _boothFastUntil;
        private float _insertEndedAt = -1000f;
        private float _nextEvaluate;
        private float _nextLibraryLookup;
        private bool _libraryWarned;
        private bool _initialized;
        private readonly VariantPicker _random = new VariantPicker();

        /// <summary>The context of the last evaluation (debug).</summary>
        public AmbienceContext Context => _ctx;
        /// <summary>True while the booth room tone replaces the world beds.</summary>
        public bool BoothMode => _boothMode;
        /// <summary>True while every bed is faded out.</summary>
        public bool Muted => _muted;

        /// <summary>The audio library, or null (looked up lazily, retried every 2 s).</summary>
        public AudioLibrary Library
        {
            get
            {
                if (_library != null) return _library;
                var now = AudioRuntime.Now;
                if (now < _nextLibraryLookup) return null;
                _nextLibraryLookup = now + 2f;
                if (Libraries.TryGet(out AudioLibrary lib))
                {
                    _library = lib;
                    return lib;
                }
                if (!_libraryWarned)
                {
                    _libraryWarned = true;
                    LSLog.MissingAsset(LogCategory.Audio, "LIB_Audio", Libraries.Folder + "/LIB_Audio.asset", this);
                }
                return null;
            }
        }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            GameEvents.BeatStarted += OnBeatStarted;
            GameEvents.MissionEvent += OnMissionEvent;
            GameEvents.CinematicStarted += OnCinematicStarted;
            GameEvents.CinematicEnded += OnCinematicEnded;
            GameEvents.InsertStarted += OnInsertStarted;
            GameEvents.InsertEnded += OnInsertEnded;
            AudioRuntime.ListenerPaused += OnPauseChanged;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.BeatStarted -= OnBeatStarted;
            GameEvents.MissionEvent -= OnMissionEvent;
            GameEvents.CinematicStarted -= OnCinematicStarted;
            GameEvents.CinematicEnded -= OnCinematicEnded;
            GameEvents.InsertStarted -= OnInsertStarted;
            GameEvents.InsertEnded -= OnInsertEnded;
            AudioRuntime.ListenerPaused -= OnPauseChanged;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
            for (var i = 0; i < _pool.Length; i++) _pool[i].Stop();
            for (var i = 0; i < _beds.Length; i++)
            {
                _beds[i].Current = null;
                _beds[i].Next = null;
                _beds[i].Gain = 0f;
            }
        }

        /// <summary>Builds the bed states and the player pool (Awake; tests call it in edit mode).</summary>
        internal void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            var rules = AmbienceTable.Beds;
            _beds = new BedState[rules.Length + 1];
            for (var i = 0; i < rules.Length; i++)
                _beds[i] = new BedState { Rule = rules[i], CueId = rules[i].CueId, Tau = rules[i].FadeSeconds };
            _booth = new BedState { Rule = null, CueId = AmbienceTable.BoothRoomTone, Tau = _boothFade };
            _beds[rules.Length] = _booth;

            var root = new GameObject("AmbienceBeds").transform;
            root.SetParent(transform, false);
            _pool = new BedPlayer[Mathf.Max(2, _players)];
            for (var i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("Bed_" + i.ToString("00"));
                go.transform.SetParent(root, false);
                var a = go.AddComponent<AudioSource>();
                var b = go.AddComponent<AudioSource>();
                Prepare(a);
                Prepare(b);
                _pool[i] = new BedPlayer { Looper = new ScheduledLooper(a, b) };
            }
        }

        private static void Prepare(AudioSource s)
        {
            AudioService.PrepareSource(s);
            s.spatialBlend = 0f;
            s.priority = MixCategories.UnityPriority(MixCategory.Ambience, 0);
        }

        private void Update() => Tick(AudioRuntime.Now, Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f));

        /// <summary>One frame (Update; tests drive it with their own clock).</summary>
        internal void Tick(float now, float dt)
        {
            if (!_initialized) Initialize();
            if (now >= _nextEvaluate)
            {
                _nextEvaluate = now + _evaluateInterval;
                Evaluate(now);
            }
            for (var i = 0; i < _beds.Length; i++) StepBed(_beds[i], now, dt);
        }

        // ---------------------------------------------------------------- public controls

        /// <summary>Booth room tone instead of the world beds (WP09 cinematics; also raised by mission events).</summary>
        public void SetBoothMode(bool on)
        {
            if (_boothMode == on) return;
            _boothMode = on;
            _boothFastUntil = AudioRuntime.Now + 0.5f;
            _nextEvaluate = 0f;
        }

        /// <summary>Fades every bed out (true) or back in (false).</summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;
            _nextEvaluate = 0f;
        }

        /// <summary>Tests and the dev console: evaluates against <paramref name="context"/> instead of the world.</summary>
        public void ForceContext(AmbienceContext context)
        {
            _hasForcedContext = true;
            _forced = context;
            _nextEvaluate = 0f;
        }

        /// <summary>Returns to evaluating the world.</summary>
        public void ClearForcedContext()
        {
            _hasForcedContext = false;
            _nextEvaluate = 0f;
        }

        /// <summary>Smoothed gain of a bed (tests, debug); 0 for unknown cues.</summary>
        public float GainOf(string cueId)
        {
            for (var i = 0; i < _beds.Length; i++)
                if (_beds[i].CueId == cueId) return _beds[i].Gain;
            return 0f;
        }

        /// <summary>Target gain of a bed after the last evaluation (tests, debug).</summary>
        public float TargetOf(string cueId)
        {
            for (var i = 0; i < _beds.Length; i++)
                if (_beds[i].CueId == cueId) return _beds[i].Target;
            return 0f;
        }

        /// <summary>Replaces the library (tests, builders).</summary>
        internal void SetLibrary(AudioLibrary library) => _library = library;

        /// <summary>Busy looping takes (tests, debug).</summary>
        internal int BusyPlayers
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _pool.Length; i++)
                    if (_pool[i].Busy) n++;
                return n;
            }
        }

        // ---------------------------------------------------------------- evaluation

        private void Evaluate(float now)
        {
            _ctx = _hasForcedContext ? _forced : BuildContext();
            if (_ctx.Zone != _lastZone)
            {
                _lastZone = _ctx.Zone;
                _zoneFastUntil = now + 1f;
            }
            var fastZone = now < _zoneFastUntil;
            var fastBooth = now < _boothFastUntil;
            var world = !_boothMode && !_muted;
            for (var i = 0; i < _beds.Length; i++)
            {
                var b = _beds[i];
                if (b.Rule == null) continue;
                b.Target = world ? b.Rule.Weight(_ctx) : 0f;
                var tau = b.Rule.FadeSeconds;
                if (fastZone) tau = Mathf.Min(tau, _zoneChangeFade);
                // Into the booth (or a mute) and back out: the world crossfades with the booth tone.
                if (!world || fastBooth) tau = Mathf.Min(tau, _boothFade);
                if (!b.Rule.ConditionHolds(_ctx)) tau = Mathf.Min(tau, b.Rule.CutFadeSeconds);
                b.Tau = tau;
            }
            _booth.Target = !_muted && (_boothMode || _insert) ? AmbienceTable.BoothLevel : 0f;
            // Inserts cut in and out (GDD §5.5); booth cinematics crossfade with the world beds.
            if (_insert) _booth.Tau = 0.03f;
            else if (!_boothMode && now - _insertEndedAt < 0.5f) _booth.Tau = 0.08f;
            else _booth.Tau = _boothFade;
        }

        private AmbienceContext BuildContext()
        {
            var c = new AmbienceContext
            {
                S = _ctx.S,
                Zone = AudioRuntime.Zone,
                Beat = _eventBeat,
                Listener = AudioRuntime.ListenerPosition,
                TransmittersOn = TransmittersOn(),
            };
            var light = _light.Value;
            if (light != null) c.S = light.S;
            var mission = _mission.Value;
            if (mission != null && mission.IsRunning) c.Beat = mission.Beat;
            return c;
        }

        private bool TransmittersOn()
        {
            if (_transmittersDownEvent) return false;
            var found = 0;
            var destroyed = 0;
            for (var i = 0; i < s_racks.Length; i++)
            {
                if (!Destructibles.TryGet(s_racks[i], out var d) || d == null) continue;
                found++;
                if (d.IsDestroyed) destroyed++;
            }
            return found < s_racks.Length || destroyed < found;
        }

        // ---------------------------------------------------------------- playback

        private void StepBed(BedState b, float now, float dt)
        {
            var k = dt > 0f ? 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, b.Tau)) : 0f;
            b.Gain = Mathf.Lerp(b.Gain, b.Target, k);
            if (Mathf.Abs(b.Gain - b.Target) < 0.0005f) b.Gain = b.Target;
            var audible = b.Gain > 0.0005f || b.Target > 0f;
            if (!audible)
            {
                ReleasePlayers(b);
                if (b.LoadRequested && now - b.LastAudibleAt > _unloadAfter) Unload(b);
                return;
            }
            b.LastAudibleAt = now;
            if (b.Current == null || !b.Current.Busy)
            {
                b.Current = null;
                if (!TryStart(b, now)) return;
            }
            Rotate(b, now, dt);
            var cue = b.Current.Cue;
            var gain = (cue != null ? cue.Volume : 1f) * b.Gain * AudioRuntime.Gain(MixCategory.Ambience, b.Insert);
            Apply(b.Current, gain);
            if (b.Next != null) Apply(b.Next, gain);
        }

        private static void Apply(BedPlayer p, float gain)
        {
            p.Looper.Tick();
            p.Looper.SetVolume(gain * Mathf.Sin(Mathf.Clamp01(p.Share) * Mathf.PI * 0.5f));
        }

        private bool TryStart(BedState b, float now)
        {
            var lib = Library;
            if (lib == null) return false;
            if (!lib.TryGet(b.CueId, out var cue) || cue == null || !cue.HasClips)
            {
                LSLog.MissingAsset(LogCategory.Audio, b.CueId, "Assets/_Game/Audio/Ambience", this);
                b.Target = 0f;
                b.Gain = 0f;
                return false;
            }
            if (!b.LoadRequested)
            {
                b.LoadRequested = true;
                b.LoadRequestedAt = now;
                for (var i = 0; i < cue.Clips.Length; i++)
                {
                    var c = cue.Clips[i];
                    if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
                }
            }
            var idx = PickClip(cue, -1);
            if (idx < 0) return false;
            var clip = cue.Clips[idx];
            var st = clip.loadState;
            if ((st == AudioDataLoadState.Loading || st == AudioDataLoadState.Unloaded) && now - b.LoadRequestedAt < _loadTimeout) return false;
            var p = Allocate();
            if (p == null)
            {
                LSLog.WarnOnce("amb-players-full", LogCategory.Audio, "AmbienceDirector: every bed player is busy; '" + b.CueId + "' waits.", this);
                return false;
            }
            b.Insert = b == _booth && _insert;
            if (!p.Start(cue, idx, _random.Value(), b.Insert, now)) return false;
            p.Share = 1f;
            b.Current = p;
            return true;
        }

        private void Rotate(BedState b, float now, float dt)
        {
            var cur = b.Current;
            if (b.Next == null)
            {
                if (_cyclesPerVariant <= 0 || cur.Cue == null || cur.Cue.Clips.Length < 2) return;
                if (now - cur.StartedAt < _cyclesPerVariant * cur.BodySeconds) return;
                var idx = PickClip(cur.Cue, cur.ClipIndex);
                if (idx < 0 || idx == cur.ClipIndex)
                {
                    cur.StartedAt = now;
                    return;
                }
                var clip = cur.Cue.Clips[idx];
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                    return;
                }
                var p = Allocate();
                if (p == null || !p.Start(cur.Cue, idx, _random.Value(), b.Insert, now))
                {
                    cur.StartedAt = now;
                    return;
                }
                p.Share = 0f;
                b.Next = p;
                return;
            }
            var nxt = b.Next;
            nxt.Share = Mathf.Min(1f, nxt.Share + dt / Mathf.Max(0.1f, _variantCrossfade));
            cur.Share = 1f - nxt.Share;
            if (nxt.Share < 1f) return;
            cur.Stop();
            b.Current = nxt;
            b.Next = null;
        }

        private int PickClip(AudioCue cue, int avoid)
        {
            var n = cue.Clips.Length;
            if (n == 0) return -1;
            var start = n > 1 ? _random.Next(cue.Id, n) : 0;
            for (var k = 0; k < n; k++)
            {
                var i = (start + k) % n;
                if (i == avoid || cue.Clips[i] == null) continue;
                return i;
            }
            return avoid >= 0 && avoid < n && cue.Clips[avoid] != null ? avoid : -1;
        }

        private BedPlayer Allocate()
        {
            for (var i = 0; i < _pool.Length; i++)
                if (!_pool[i].Busy) return _pool[i];
            return null;
        }

        private static void ReleasePlayers(BedState b)
        {
            if (b.Current != null) b.Current.Stop();
            if (b.Next != null) b.Next.Stop();
            b.Current = null;
            b.Next = null;
        }

        private void Unload(BedState b)
        {
            b.LoadRequested = false;
            var lib = Library;
            if (lib == null || !lib.TryGet(b.CueId, out var cue) || cue == null) return;
            for (var i = 0; i < cue.Clips.Length; i++)
            {
                var c = cue.Clips[i];
                if (c != null && !c.preloadAudioData && c.loadState == AudioDataLoadState.Loaded) c.UnloadAudioData();
            }
        }

        // ---------------------------------------------------------------- events

        private void OnBeatStarted(int beat) => _eventBeat = beat;

        private void OnMissionEvent(string id)
        {
            switch (id)
            {
                case AudioEvents.AmbienceBoothOn: SetBoothMode(true); break;
                case AudioEvents.AmbienceBoothOff: SetBoothMode(false); break;
                case AudioEvents.AmbienceMute: SetMuted(true); break;
                case AudioEvents.AmbienceUnmute: SetMuted(false); break;
                case AudioEvents.TransmittersDown:
                    _transmittersDownEvent = true;
                    _nextEvaluate = 0f;
                    break;
                case SequenceIds.IntroControlToPlayer: SetBoothMode(false); break;
            }
        }

        private void OnCinematicStarted(string id)
        {
            if (id == SequenceIds.Intro || id == SequenceIds.Outro) SetBoothMode(true);
        }

        private void OnCinematicEnded(string id)
        {
            if (id == SequenceIds.Intro) SetBoothMode(false);
        }

        private void OnInsertStarted(string id)
        {
            _insert = true;
            // A booth tone already playing in booth mode moves onto the insert bus.
            if (_booth.Current != null && _booth.Current.Busy && !_booth.Insert)
            {
                _booth.Insert = true;
                _booth.Current.Insert = true;
                _booth.Current.Looper.SetIgnorePause(true);
            }
            _nextEvaluate = 0f;
        }

        private void OnInsertEnded(string id)
        {
            _insert = false;
            _insertEndedAt = AudioRuntime.Now;
            if (_booth.Current != null && _booth.Insert)
            {
                _booth.Insert = false;
                _booth.Current.Insert = false;
                _booth.Current.Looper.SetIgnorePause(false);
            }
            _nextEvaluate = 0f;
        }

        private void OnPauseChanged(bool paused)
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                var p = _pool[i];
                if (!p.Busy || p.Insert) continue;
                if (paused) p.Looper.OnPause();
                else p.Looper.OnResume();
            }
        }

        private void OnCheckpointLoaded(string cpId)
        {
            _transmittersDownEvent = false;
            _muted = false;
            _nextEvaluate = 0f;
        }
    }
}
