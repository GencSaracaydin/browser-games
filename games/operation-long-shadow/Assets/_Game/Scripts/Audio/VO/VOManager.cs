using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.VO
{
    /// <summary>
    /// <see cref="IVOService"/> (TECH_PLAN §7.10, GDD §10.1, §11, §17):
    /// <list type="bullet">
    /// <item>Three queued channels (<see cref="VOChannel"/>: the squad net, broadcast voices, the booth) that each play one
    /// line at a time by priority (Scripted &gt; Objective &gt; Callout &gt; Bark, <see cref="VOQueue"/>); a scripted or
    /// objective line cuts a callout; callouts that wait more than 1.5 s are dropped; requesting a line that is already
    /// queued or playing returns its handle instead of doubling it. Barks play at once and may overlap (4 voices).</item>
    /// <item>Rendering per <see cref="VOChainSelector"/>: dry/radio crossfade by speaker distance (dry ≤ 8 m, radio ≥ 15 m),
    /// G9 positional switch, the three PA horns with their propagation delays, anchors for the room speaker and the
    /// booth props; synced pairs start on the same DSP time.</item>
    /// <item>Subtitles through <see cref="GameEvents.SubtitleShow"/>/<see cref="GameEvents.SubtitleHide"/> (the HUD draws
    /// them): speaker colours, booth italics, barks only within 25 m with "Translate enemy chatter" on.</item>
    /// <item>Ducking while a line is heard: music −6 dB, ambience −4 dB, combat effects −6 dB in combat
    /// (TECH_PLAN §7.10, CINEMATIC_DIRECTION §4).</item>
    /// <item>Beat windows (<see cref="PrepareWindow"/>), envelopes (<see cref="Amplitude"/>, D10) and the played-line set
    /// as an <see cref="ISaveable"/> ("VO"): lines played before a checkpoint do not repeat after its reload unless they
    /// are objective-critical or systemic (<see cref="PlayedLineSet"/>).</item>
    /// </list>
    /// Lines whose audio has not landed play silently for their estimated length with their subtitle, so mission scripts
    /// never stall.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 5)]
    [DisallowMultipleComponent]
    public sealed class VOManager : MonoBehaviour, IVOService, ISaveable
    {
        private const int QueuedChannels = 3;
        private const int MaxPendingHides = 16;

        [SerializeField, Tooltip("LIB_Dialogue; when empty the registered library is used (Libraries.Get).")]
        private DialogueLibrary _library;
        [SerializeField, Range(1, 8)] private int _barkVoices = 4;
        [SerializeField] private float _musicDuckDb = AudioConstants.VOMusicDuckDb;
        [SerializeField] private float _ambienceDuckDb = AudioConstants.VOAmbienceDuckDb;
        [SerializeField] private float _combatDuckDb = AudioConstants.VOCombatDuckDb;
        [SerializeField, Tooltip("Silence between two lines on one channel (radio etiquette).")]
        private float _lineGap = 0.15f;
        [SerializeField, Tooltip("Longest wait for clip data before a line starts silently.")]
        private float _loadTimeout = 3f;
        [SerializeField] private float _interruptFade = 0.08f;
        [SerializeField, Tooltip("Seconds a subtitle stays after its line.")]
        private float _subtitleLinger = 0.35f;

        private static readonly string[] s_clickCues = { "SFX_RADIO_CLICKS_1", "SFX_RADIO_CLICKS_2", "SFX_RADIO_CLICKS_3" };

        private readonly VOQueue[] _queues = { new VOQueue(), new VOQueue(), new VOQueue() };
        private readonly VOQueue _barkQueue = new VOQueue();
        private readonly VOVoice[] _current = new VOVoice[QueuedChannels];
        private readonly float[] _channelFreeAt = new float[QueuedChannels];
        private readonly PlayedLineSet _played = new PlayedLineSet();
        private readonly List<string> _ids = new List<string>(256);
        private readonly int[] _hideHandles = new int[MaxPendingHides];
        private readonly float[] _hideAt = new float[MaxPendingHides];
        private VOVoice[] _voices = new VOVoice[0];
        private ServiceRef<ISquadService> _squad;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<IAudioService> _audio;
        private ICheckpointService _registeredWith;
        private int _nextHandle = 1;
        private long _sequence;
        private int _duckKey;
        private bool _ducking;
        private bool _duckingCombat;
        private float _nextLibraryLookup;
        private bool _libraryWarned;
        private bool _initialized;

        /// <inheritdoc />
        public string SaveKey => "VO";

        /// <summary>The dialogue library, or null (looked up lazily, retried every 2 s).</summary>
        public DialogueLibrary Library
        {
            get
            {
                if (_library != null) return _library;
                var now = AudioRuntime.Now;
                if (now < _nextLibraryLookup) return null;
                _nextLibraryLookup = now + 2f;
                if (Libraries.TryGet(out DialogueLibrary lib))
                {
                    _library = lib;
                    return lib;
                }
                if (!_libraryWarned)
                {
                    _libraryWarned = true;
                    LSLog.MissingAsset(LogCategory.VO, "LIB_Dialogue", Libraries.Folder + "/LIB_Dialogue.asset", this);
                }
                return null;
            }
        }

        private void Awake()
        {
            Initialize();
            GameServices.Register<IVOService>(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IVOService>(this);
            _registeredWith?.Unregister(this);
            _registeredWith = null;
            AudioRuntime.Mix.ClearHolds(_duckKey);
        }

        private void OnEnable()
        {
            GameEvents.InsertStarted += OnInsertStarted;
            GameEvents.InsertEnded += OnInsertEnded;
            GameEvents.BeatStarted += OnBeatStarted;
            GameEvents.CinematicStarted += OnCinematicStarted;
        }

        private void OnDisable()
        {
            GameEvents.InsertStarted -= OnInsertStarted;
            GameEvents.InsertEnded -= OnInsertEnded;
            GameEvents.BeatStarted -= OnBeatStarted;
            GameEvents.CinematicStarted -= OnCinematicStarted;
        }

        /// <summary>Builds the voice pool (Awake; tests call it in edit mode).</summary>
        internal void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _duckKey = AudioRuntime.NewKey();
            var count = QueuedChannels * 2 + Mathf.Max(1, _barkVoices);
            _voices = new VOVoice[count];
            var root = new GameObject("VOVoices").transform;
            root.SetParent(transform, false);
            for (var i = 0; i < count; i++)
            {
                var v = new VOVoice();
                var vgo = new GameObject("VO_" + i.ToString("00"));
                vgo.transform.SetParent(root, false);
                for (var s = 0; s < VOVoice.SourceCount; s++)
                {
                    var sgo = new GameObject(s == 0 ? "2D" : "3D_" + s);
                    sgo.transform.SetParent(vgo.transform, false);
                    var src = sgo.AddComponent<AudioSource>();
                    AudioService.PrepareSource(src);
                    v.Sources[s] = src;
                }
                _voices[i] = v;
            }
        }

        private void Update() => Tick(AudioRuntime.Now, Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f));

        /// <summary>One frame of the manager (Update; tests drive it with their own clock).</summary>
        internal void Tick(float now, float dt)
        {
            if (!_initialized) Initialize();
            RegisterWithCheckpoints();
            var paused = AudioListener.pause;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (!v.Busy) continue;
                if (!v.Tick(now, dt, paused, AudioRuntime.Gain(MixCategory.Voice, v.Insert))) Finish(v, now);
            }
            for (var ch = 0; ch < QueuedChannels; ch++)
            {
                _queues[ch].DropExpired(now, null);
                TryStartChannel(ch, now);
            }
            _barkQueue.DropExpired(now, null);
            for (var guard = 0; guard < 4; guard++)
            {
                var b = _barkQueue.PeekNext(now);
                if (b == null) break;
                _barkQueue.Remove(b);
                StartLine(b, now);
            }
            for (var i = 0; i < MaxPendingHides; i++)
            {
                if (_hideHandles[i] == 0 || now < _hideAt[i]) continue;
                GameEvents.RaiseSubtitleHide(_hideHandles[i]);
                _hideHandles[i] = 0;
            }
            UpdateDucks();
        }

        // ---------------------------------------------------------------- IVOService

        /// <inheritdoc />
        public int Play(string lineId, VOOptions o = default)
        {
            if (string.IsNullOrEmpty(lineId)) return 0;
            if (!_initialized) Initialize();
            var lib = Library;
            if (lib == null) return 0;
            if (!lib.TryGet(lineId, out var line) || line == null)
            {
                LSLog.WarnOnce("vo-unknown:" + lineId, LogCategory.VO, "Unknown dialogue line '" + lineId + "' (not in LIB_Dialogue).", this);
                return 0;
            }
            if (_played.ShouldSkip(line))
            {
                LSLog.Verbose(LogCategory.VO, "Skipping " + lineId + ": it played before the restored checkpoint.", this);
                return 0;
            }
            var existing = HandleOf(lineId);
            if (existing != 0) return existing;

            var now = AudioRuntime.Now;
            var priority = o.Priority ?? line.Priority;
            var channel = priority == VOPriority.Bark || line.Channel == VOChannel.Bark ? VOChannel.Bark : line.Channel;
            var req = new VORequest
            {
                Handle = NextHandle(), Line = line, Options = o, Priority = priority, Channel = channel, EnqueuedAt = now,
                EligibleAt = now + Mathf.Max(0f, o.Delay), Sequence = ++_sequence, Speaker = o.Speaker,
            };
            if (priority == VOPriority.Callout) req.ExpiresAt = req.EligibleAt + VOQueue.CalloutMaxWait;
            if (channel == VOChannel.Bark)
            {
                req.ExpiresAt = req.EligibleAt + 1f;
                if (o.Delay > 0f) _barkQueue.Enqueue(req);
                else StartLine(req, now);
                return req.Handle;
            }
            var ch = (int)channel;
            var cur = _current[ch];
            if (cur != null && o.Delay <= 0f && VOQueue.ShouldInterrupt(cur.Request.Priority, priority, o.Interrupt))
            {
                req.Urgent = true;
                cur.Stop(_interruptFade, now);
                _current[ch] = null;
                _channelFreeAt[ch] = 0f;
            }
            _queues[ch].Enqueue(req);
            TryStartChannel(ch, now);
            return req.Handle;
        }

        /// <inheritdoc />
        public bool IsPlaying(string lineId) => HandleOf(lineId) != 0;

        /// <inheritdoc />
        public bool IsNetBusy
        {
            get
            {
                var cur = _current[(int)VOChannel.Net];
                if (cur != null && cur.Busy && cur.Request.Priority >= VOPriority.Objective) return true;
                return _queues[(int)VOChannel.Net].HasAtLeast(VOPriority.Objective);
            }
        }

        /// <inheritdoc />
        public void Stop(int handle)
        {
            if (handle == 0) return;
            for (var ch = 0; ch < QueuedChannels; ch++)
                if (_queues[ch].Remove(handle)) return;
            if (_barkQueue.Remove(handle)) return;
            var now = AudioRuntime.Now;
            for (var i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].Handle != handle) continue;
                _voices[i].Stop(_interruptFade, now);
                return;
            }
        }

        /// <inheritdoc />
        public void StopAll(float fade)
        {
            for (var ch = 0; ch < QueuedChannels; ch++) _queues[ch].Clear();
            _barkQueue.Clear();
            var now = AudioRuntime.Now;
            for (var i = 0; i < _voices.Length; i++)
                if (_voices[i].Busy) _voices[i].Stop(fade, now);
        }

        /// <inheritdoc />
        public void PlayBark(string lineId, Transform speaker, Team team)
        {
            if (string.IsNullOrEmpty(lineId)) return;
            if (!_initialized) Initialize();
            var lib = Library;
            if (lib == null || !lib.TryGet(lineId, out var line) || line == null)
            {
                if (lib != null) LSLog.WarnOnce("vo-unknown:" + lineId, LogCategory.VO, "Unknown bark '" + lineId + "'.", this);
                return;
            }
            var now = AudioRuntime.Now;
            var req = new VORequest
            {
                Handle = NextHandle(), Line = line, Priority = VOPriority.Bark, Channel = VOChannel.Bark, EnqueuedAt = now,
                EligibleAt = now, ExpiresAt = now + 1f, Sequence = ++_sequence, Speaker = speaker, Team = team,
            };
            StartLine(req, now);
        }

        /// <inheritdoc />
        public void PlayClicks(int count, Transform speaker)
        {
            var audio = _audio.Value;
            if (audio == null) return;
            var cue = s_clickCues[Mathf.Clamp(count, 1, s_clickCues.Length) - 1];
            if (speaker != null) audio.Play(cue, speaker.position);
            else audio.Play2D(cue);
        }

        /// <inheritdoc />
        public float Amplitude(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return 0f;
            var now = AudioRuntime.Now;
            var best = 0f;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (!v.Busy || v.Line == null || v.Line.Speaker != speakerId) continue;
                var a = v.Amplitude(now);
                if (a > best) best = a;
            }
            return best;
        }

        /// <inheritdoc />
        public void PrepareWindow(int fromBeat, int toBeat)
        {
            var lib = Library;
            if (lib != null)
            {
                var entries = lib.Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    var line = entries[i];
                    if (line == null) continue;
                    var action = BeatWindow.Decide(line.Beat, fromBeat, toBeat);
                    if (action == BeatWindow.Action.Keep) continue;
                    if (action == BeatWindow.Action.Unload && HandleOf(line.Id) != 0) continue;
                    var clips = line.Clips;
                    for (var c = 0; c < clips.Count; c++)
                    {
                        var clip = clips[c] != null ? clips[c].Clip : null;
                        if (clip == null) continue;
                        if (action == BeatWindow.Action.Load)
                        {
                            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                        }
                        else if (clip.loadState == AudioDataLoadState.Loaded && !clip.preloadAudioData)
                        {
                            clip.UnloadAudioData();
                        }
                    }
                }
            }
            AudioRuntime.RaiseWindowPrepared(fromBeat, toBeat);
        }

        /// <inheritdoc />
        public bool HasPlayed(string lineId) => _played.HasPlayed(lineId);

        /// <inheritdoc />
        public bool HasLine(string lineId)
        {
            var lib = Library;
            return lib != null && lineId != null && lib.Contains(lineId);
        }

        /// <inheritdoc />
        public float Duration(string lineId)
        {
            var lib = Library;
            return lib != null && lineId != null && lib.TryGet(lineId, out var line) && line != null ? line.Duration : 0f;
        }

        // ---------------------------------------------------------------- ISaveable

        /// <inheritdoc />
        public void Capture(SaveWriter w)
        {
            _played.CopyTo(_ids);
            w.WriteStrings("played", _ids);
        }

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            _ids.Clear();
            r.ReadStrings("played", _ids);
            StopAll(0.15f);
            _played.Restore(_ids);
        }

        // ---------------------------------------------------------------- internals

        /// <summary>The played-line set (tests).</summary>
        internal PlayedLineSet Played => _played;

        /// <summary>Replaces the library (tests, builders).</summary>
        internal void SetLibrary(DialogueLibrary library) => _library = library;

        /// <summary>Busy voices (tests, debug).</summary>
        internal int BusyVoices
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _voices.Length; i++)
                    if (_voices[i].Busy) n++;
                return n;
            }
        }

        /// <summary>The line playing on a channel, or null (tests, debug).</summary>
        internal string CurrentLine(VOChannel channel)
        {
            var ch = (int)channel;
            if (ch < 0 || ch >= QueuedChannels) return null;
            var v = _current[ch];
            return v != null && v.Busy ? v.Line?.Id : null;
        }

        private int NextHandle()
        {
            var h = _nextHandle++;
            if (_nextHandle <= 0) _nextHandle = 1;
            return h;
        }

        private int HandleOf(string lineId)
        {
            if (lineId == null) return 0;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Busy && !v.Stopping && v.Line != null && v.Line.Id == lineId) return v.Handle;
            }
            for (var ch = 0; ch < QueuedChannels; ch++)
            {
                var r = _queues[ch].FindLine(lineId);
                if (r != null) return r.Handle;
            }
            var b = _barkQueue.FindLine(lineId);
            return b != null ? b.Handle : 0;
        }

        private void TryStartChannel(int ch, float now)
        {
            if (_current[ch] != null) return;
            var next = _queues[ch].PeekNext(now);
            if (next == null) return;
            if (!next.Urgent && now < _channelFreeAt[ch]) return;
            if (!EnsureLoaded(next, now)) return;
            _queues[ch].Remove(next);
            StartLine(next, now);
        }

        private bool EnsureLoaded(VORequest req, float now)
        {
            var clips = req.Line.Clips;
            var ready = true;
            for (var i = 0; i < clips.Count; i++)
            {
                var c = clips[i] != null ? clips[i].Clip : null;
                if (c == null) continue;
                var st = c.loadState;
                if (st == AudioDataLoadState.Unloaded)
                {
                    c.LoadAudioData();
                    ready = false;
                }
                else if (st == AudioDataLoadState.Loading) ready = false;
            }
            if (ready) return true;
            if (req.LoadRequestedAt < 0f) req.LoadRequestedAt = now;
            if (now - req.LoadRequestedAt <= _loadTimeout) return false;
            LSLog.WarnOnce("vo-load:" + req.LineId, LogCategory.VO, "Clip data of '" + req.LineId + "' did not load in " + _loadTimeout + " s; playing what is ready.", this);
            return true;
        }

        private void StartLine(VORequest req, float now)
        {
            var isBark = req.Channel == VOChannel.Bark;
            var voice = AllocateVoice(isBark, now);
            if (voice == null)
            {
                LSLog.Verbose(LogCategory.VO, "No free VO voice for " + req.LineId, this);
                return;
            }
            var line = req.Line;
            ResolveSpeaker(req, out var speaker, out var head);
            var hasSpeaker = speaker != null;
            var listener = AudioRuntime.ListenerPosition;
            var dist = hasSpeaker ? Vector3.Distance((head != null ? head : speaker).position, listener) : float.PositiveInfinity;
            var key = hasSpeaker ? speaker.GetHashCode() : req.Handle;
            var plan = VOChainSelector.Select(line, req.Options, isBark, hasSpeaker, dist, key);
            voice.Begin(req, plan, speaker, head, AudioRuntime.InsertActive, now);
            _played.MarkPlayed(line.Id);
            if (!isBark) _current[(int)req.Channel] = voice;
            GameEvents.RaiseVOLineStarted(line.Id, line.Speaker);
            if (!req.Options.NoSubtitle && ShouldSubtitle(isBark, dist))
            {
                var radio = plan.Mode == VOPlanMode.DryRadio ? RadioMix.RadioShare(dist) >= 0.5f : plan.IsRadio;
                var data = SubtitleBuilder.Build(line, req.Handle, radio, plan.InBooth, Mathf.Max(0.5f, voice.EndsAt - now) + _subtitleLinger);
                GameEvents.RaiseSubtitleShow(data);
                voice.SubtitleShown = true;
            }
        }

        private bool ShouldSubtitle(bool isBark, float distance)
        {
            var s = _settings.Value?.Current;
            var subtitles = s == null || s.Subtitles;
            if (!isBark) return subtitles;
            return BarkRules.ShouldSubtitle(distance, subtitles, s == null || s.TranslateEnemyChatter);
        }

        private void ResolveSpeaker(VORequest req, out Transform speaker, out Transform head)
        {
            speaker = req.Speaker;
            head = null;
            if (speaker == null && req.Line != null && req.Channel == VOChannel.Net)
            {
                var squadId = SpeakerIds.ToSquadId(req.Line.Speaker);
                if (squadId != null && squadId != SquadIds.Player)
                {
                    var squad = _squad.Value;
                    if (squad != null) speaker = squad.Member(squadId);
                }
            }
            if (speaker == null) return;
            var body = speaker.GetComponentInParent<ICharacterBody>();
            if (body != null) head = body.Head;
        }

        private VOVoice AllocateVoice(bool isBark, float now)
        {
            var barks = 0;
            VOVoice oldestBark = null;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (!v.Busy) continue;
                if (v.Request != null && v.Request.Channel == VOChannel.Bark)
                {
                    barks++;
                    if (oldestBark == null || v.StartedAt < oldestBark.StartedAt) oldestBark = v;
                }
            }
            if (isBark && barks >= _barkVoices)
            {
                // Replace the oldest bark if it has been heard for a while; otherwise drop the new one.
                if (oldestBark == null || now - oldestBark.StartedAt < 0.5f) return null;
                Finish(oldestBark, now);
                return oldestBark;
            }
            for (var i = 0; i < _voices.Length; i++)
                if (!_voices[i].Busy) return _voices[i];
            // Pool exhausted: take a fading voice, then the oldest bark.
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Stopping) continue;
                Finish(_voices[i], now);
                return _voices[i];
            }
            if (oldestBark != null)
            {
                Finish(oldestBark, now);
                return oldestBark;
            }
            return null;
        }

        private void Finish(VOVoice v, float now)
        {
            var line = v.Line;
            var handle = v.Handle;
            var shown = v.SubtitleShown;
            var interrupted = v.Stopping;
            for (var ch = 0; ch < QueuedChannels; ch++)
            {
                if (_current[ch] != v) continue;
                _current[ch] = null;
                _channelFreeAt[ch] = now + _lineGap;
            }
            v.Release();
            if (line != null) GameEvents.RaiseVOLineFinished(line.Id);
            if (!shown) return;
            if (interrupted)
            {
                GameEvents.RaiseSubtitleHide(handle);
                return;
            }
            QueueHide(handle, now + _subtitleLinger);
        }

        private void QueueHide(int handle, float at)
        {
            for (var i = 0; i < MaxPendingHides; i++)
            {
                if (_hideHandles[i] != 0) continue;
                _hideHandles[i] = handle;
                _hideAt[i] = at;
                return;
            }
            GameEvents.RaiseSubtitleHide(handle);
        }

        private void UpdateDucks()
        {
            var speaking = false;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (!v.Busy || v.Stopping || v.Request == null || v.Request.Channel == VOChannel.Bark) continue;
                if (v.Plan.Mode == VOPlanMode.Silent && !v.Line.HasAudio) continue;
                speaking = true;
                break;
            }
            var combat = speaking && AudioRuntime.InCombat;
            if (speaking == _ducking && combat == _duckingCombat) return;
            var mix = AudioRuntime.Mix;
            if (speaking)
            {
                mix.SetHold(_duckKey, MixCategory.Music, _musicDuckDb);
                mix.SetHold(_duckKey, MixCategory.Ambience, _ambienceDuckDb);
            }
            else
            {
                mix.ClearHold(_duckKey, MixCategory.Music);
                mix.ClearHold(_duckKey, MixCategory.Ambience);
            }
            if (combat) mix.SetHold(_duckKey, MixCategory.Effects, _combatDuckDb);
            else mix.ClearHold(_duckKey, MixCategory.Effects);
            _ducking = speaking;
            _duckingCombat = combat;
        }

        private void RegisterWithCheckpoints()
        {
            var cp = _checkpoints.Value;
            if (cp == _registeredWith) return;
            _registeredWith?.Unregister(this);
            _registeredWith = cp;
            cp?.Register(this);
        }

        /// <summary>
        /// Keeps the decoding window current even if the mission script does not call <see cref="PrepareWindow"/> itself:
        /// a new beat loads its lines and the next beat's, and unloads passed beats (TECH_PLAN §7.10).
        /// </summary>
        private void OnBeatStarted(int beat)
        {
            if (beat < 0) return;
            PrepareWindow(beat, Mathf.Min(beat + 1, AudioConstants.OutroBeat));
        }

        private void OnCinematicStarted(string id)
        {
            if (id == SequenceIds.Outro) PrepareWindow(AudioConstants.OutroBeat, AudioConstants.OutroBeat);
        }

        private static void OnInsertStarted(string id) => AudioRuntime.InsertActive = true;

        private static void OnInsertEnded(string id) => AudioRuntime.InsertActive = false;
    }
}
