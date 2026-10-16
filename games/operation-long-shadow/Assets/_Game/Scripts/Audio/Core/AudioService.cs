using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// <see cref="IAudioService"/> (TECH_PLAN §7.9, D9): a pool of 32 one-shot sources with a cap of 24 audible voices
    /// and priority stealing, 12 gapless loop slots, category gains and ducking applied to <c>AudioSource.volume</c> (no
    /// AudioMixer: Web builds only do volume), baked far variants crossfaded by distance, the speed-of-sound delay for
    /// distant shots and explosions, a slow-motion pitch dip, the listener's acoustic zone from <see cref="ZoneTag"/>s,
    /// layered weapon shots (<see cref="WeaponShotPlayer"/>) and the grenade tinnitus muffle.
    /// <para>It owns <b>the game's only AudioListener</b> (a child object) which follows <see cref="IPlayerRig.Eye"/>, or the
    /// transform given to <see cref="SetListenerOverride"/> (cinematics). Other enabled AudioListeners are disabled with a
    /// warning when the service starts and whenever a section loads.</para>
    /// <para>Cues come from <c>LIB_Audio</c> (<see cref="AudioLibrary"/>); a missing cue or library is logged once and the
    /// call does nothing, so the game plays before every sound exists.</para>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio)]
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [SerializeField, Tooltip("LIB_Audio; when empty the registered library is used (Libraries.Get).")]
        private AudioLibrary _library;
        [SerializeField, Range(8, 64)] private int _poolSize = AudioConstants.SourcePool;
        [SerializeField, Range(4, 64)] private int _audibleCap = AudioConstants.AudibleVoices;
        [SerializeField, Range(2, 32)] private int _loopSlots = AudioConstants.LoopSlots;
        [SerializeField, Range(0.3f, 1f), Tooltip("Pitch of effects at time scale 0.25 (slow-motion dip).")]
        private float _slowMotionPitchFloor = 0.62f;
        [SerializeField] private float _combatAmbienceDuckDb = AudioConstants.CombatAmbienceDuckDb;
        [SerializeField] private float _zoneCheckInterval = 0.2f;
        [SerializeField] private bool _disableOtherListeners = true;
        [SerializeField, Tooltip("Tinnitus whine cue (GDD §13.2: < 6 m from a grenade).")]
        private string _tinnitusCue = "SFX_TINNITUS";
        [SerializeField, Tooltip("Pause the listener during glitch inserts so gameplay audio freezes (GDD §5.5); insert audio ignores it.")]
        private bool _pauseWorldDuringInserts = true;

        private OneShotVoice[] _voices = new OneShotVoice[0];
        private LoopVoice[] _loops = new LoopVoice[0];
        private VoiceInfo[] _scratch = new VoiceInfo[0];
        private AudioListener _listener;
        private Transform _listenerTransform;
        private Transform _override;
        private readonly VariantPicker _picker = new VariantPicker();
        private readonly Dictionary<string, float> _lastStart = new Dictionary<string, float>(128, System.StringComparer.Ordinal);
        private WeaponShotPlayer _weapons;
        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<ISettingsService> _settings;
        private float _nextLibraryLookup;
        private bool _libraryWarned;
        private int _nextHandle = 1;
        private float _nextZoneCheck;
        private int _combatKey;
        private float _muffleStart;
        private float _muffleEnd;
        private int _tinnitusHandle;
        private bool _initialized;
        private bool _pausedForInsert;

        /// <inheritdoc />
        public ZoneType CurrentZone => AudioRuntime.Zone;

        /// <summary>The listener transform (child of this object).</summary>
        public Transform ListenerTransform => _listenerTransform;

        /// <summary>The active audio library, or null (looked up lazily, retried every 2 s).</summary>
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

        /// <summary>0..1 muffle of the tinnitus (biases cues towards their baked far variants).</summary>
        internal float Muffle
        {
            get
            {
                var now = AudioRuntime.Now;
                if (now >= _muffleEnd || _muffleEnd <= _muffleStart) return 0f;
                return 1f - Mathf.InverseLerp(_muffleStart, _muffleEnd, now);
            }
        }

        private void Awake()
        {
            Initialize();
            GameServices.Register<IAudioService>(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IAudioService>(this);
            AudioRuntime.Mix.ClearHolds(_combatKey);
        }

        private void OnEnable()
        {
            GameEvents.SettingsChanged += ApplySettings;
            GameEvents.PauseChanged += OnPauseChanged;
            GameEvents.InsertStarted += OnInsertStarted;
            GameEvents.InsertEnded += OnInsertEnded;
            GameEvents.CombatStateChanged += OnCombatStateChanged;
            GameEvents.SectionLoaded += OnSectionLoaded;
            GameEvents.CheckpointLoaded += OnCheckpointLoaded;
        }

        private void OnDisable()
        {
            GameEvents.SettingsChanged -= ApplySettings;
            GameEvents.PauseChanged -= OnPauseChanged;
            GameEvents.InsertStarted -= OnInsertStarted;
            GameEvents.InsertEnded -= OnInsertEnded;
            GameEvents.CombatStateChanged -= OnCombatStateChanged;
            GameEvents.SectionLoaded -= OnSectionLoaded;
            GameEvents.CheckpointLoaded -= OnCheckpointLoaded;
        }

        private void Start()
        {
            ApplySettings();
            if (_disableOtherListeners) DisableOtherListeners();
        }

        /// <summary>Builds the listener and the pools (Awake; tests call it directly in edit mode).</summary>
        internal void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _combatKey = AudioRuntime.NewKey();
            _weapons = new WeaponShotPlayer(this);

            _listener = GetComponentInChildren<AudioListener>(true);
            if (_listener == null)
            {
                var lgo = new GameObject("AudioListener");
                lgo.transform.SetParent(transform, false);
                _listener = lgo.AddComponent<AudioListener>();
            }
            _listener.enabled = true;
            _listenerTransform = _listener.transform;

            var root = new GameObject("Voices").transform;
            root.SetParent(transform, false);
            _voices = new OneShotVoice[Mathf.Max(1, _poolSize)];
            for (var i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject("Voice_" + i.ToString("00"));
                go.transform.SetParent(root, false);
                var src = go.AddComponent<AudioSource>();
                PrepareSource(src);
                _voices[i] = new OneShotVoice { Source = src, Transform = go.transform };
            }
            _scratch = new VoiceInfo[_voices.Length];

            var lroot = new GameObject("Loops").transform;
            lroot.SetParent(transform, false);
            _loops = new LoopVoice[Mathf.Max(1, _loopSlots)];
            for (var i = 0; i < _loops.Length; i++)
            {
                var go = new GameObject("Loop_" + i.ToString("00"));
                go.transform.SetParent(lroot, false);
                var a = go.AddComponent<AudioSource>();
                var b = go.AddComponent<AudioSource>();
                PrepareSource(a);
                PrepareSource(b);
                _loops[i] = new LoopVoice { Looper = new ScheduledLooper(a, b), Transform = go.transform };
            }
        }

        /// <summary>Default settings every pooled source starts from.</summary>
        internal static void PrepareSource(AudioSource src)
        {
            src.playOnAwake = false;
            src.loop = false;
            src.dopplerLevel = 0f;
            src.spatialize = false;
            src.bypassReverbZones = true;
            src.reverbZoneMix = 0f;
            src.volume = 0f;
        }

        private void LateUpdate()
        {
            var now = AudioRuntime.Now;
            var dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
            UpdateListener();
            AudioRuntime.Mix.Tick(now, dt);
            if (now >= _nextZoneCheck)
            {
                _nextZoneCheck = now + _zoneCheckInterval;
                UpdateZone();
            }
            _weapons.Tick(now);
            UpdateVoices(now, dt);
            UpdateLoops(dt);
        }

        private void UpdateListener()
        {
            if (_listenerTransform == null) return;
            Transform target = _override;
            if (target == null)
            {
                var rig = _rig.Value;
                if (rig != null) target = rig.Eye;
            }
            if (target == null)
            {
                var cam = Camera.main;
                if (cam != null) target = cam.transform;
            }
            if (target != null) _listenerTransform.SetPositionAndRotation(target.position, target.rotation);
            AudioRuntime.SetListener(_listenerTransform.position, _listenerTransform.rotation);
        }

        private void UpdateZone()
        {
            var z = ZoneTag.Resolve(AudioRuntime.ListenerPosition);
            if (z == AudioRuntime.Zone) return;
            AudioRuntime.Zone = z;
            GameEvents.RaiseZoneChanged(z);
        }

        private void UpdateVoices(float now, float dt)
        {
            var slow = AudioMath.SlowMotionPitch(Time.timeScale, _slowMotionPitchFloor);
            var paused = AudioListener.pause;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Handle == 0) continue;
                if (paused && !v.Source.ignoreListenerPause)
                {
                    // Paused with the game: the clip does not advance, neither do its timers.
                    v.EndsAt += dt;
                    if (v.HardStopAt < float.MaxValue) v.HardStopAt += dt;
                    if (v.FadeOutAt < float.MaxValue) v.FadeOutAt += dt;
                    continue;
                }
                if (!v.Active(now))
                {
                    v.Release();
                    continue;
                }
                if (!v.StepFade(now, dt)) continue;
                if (v.Following)
                {
                    if (v.Follow == null) v.Following = false;
                    else v.Transform.position = v.Follow.position;
                }
                var src = v.Source;
                src.volume = v.BaseVolume * v.Fade * AudioRuntime.Gain(v.Category, v.Insert);
                src.pitch = MixCategories.FollowsSlowMotion(v.Category) ? v.BasePitch * slow : v.BasePitch;
            }
        }

        private void UpdateLoops(float dt)
        {
            for (var i = 0; i < _loops.Length; i++)
            {
                var l = _loops[i];
                if (l.Handle == 0) continue;
                l.Looper.Tick();
                if (!l.Looper.IsRunning)
                {
                    l.Release();
                    continue;
                }
                if (!l.StepFade(dt)) continue;
                if (l.Following)
                {
                    if (l.Follow == null)
                    {
                        // The object went away (section unloaded, actor despawned): its loop goes with it.
                        l.FadeOutAndStop(0.1f);
                        l.Following = false;
                    }
                    else l.Transform.position = l.Follow.position;
                }
                l.Looper.SetVolume(l.BaseVolume * l.Fade * AudioRuntime.Gain(l.Category, l.Insert));
            }
        }

        // ---------------------------------------------------------------- IAudioService

        /// <inheritdoc />
        public void Play(string cueId, Vector3 position, float volume = 1f)
        {
            if (TryGetCue(cueId, out var cue)) PlayCue(cue, position, null, false, volume, 0);
        }

        /// <summary>
        /// A 3D one-shot with a pitch multiplier on top of the cue's own jitter (WP02 sprint footsteps +5 %, GDD §10.2).
        /// Requested as an additive <c>IAudioService</c> member in Docs/CORE_CHANGES.md; callable through Integration until then.
        /// </summary>
        public void PlayPitched(string cueId, Vector3 position, float volume, float pitch)
        {
            if (TryGetCue(cueId, out var cue)) PlayCue(cue, position, null, false, volume, 0, pitch);
        }

        /// <inheritdoc />
        public void Play2D(string cueId, float volume = 1f)
        {
            if (TryGetCue(cueId, out var cue)) PlayCue(cue, AudioRuntime.ListenerPosition, null, true, volume, 0);
        }

        /// <inheritdoc />
        public int PlayAttached(string cueId, Transform follow, float volume = 1f)
        {
            if (!TryGetCue(cueId, out var cue)) return 0;
            var pos = follow != null ? follow.position : AudioRuntime.ListenerPosition;
            return PlayCue(cue, pos, follow, follow == null, volume, 0);
        }

        /// <inheritdoc />
        public int PlayLoop(string cueId, Transform follow)
        {
            if (!TryGetCue(cueId, out var cue) || !cue.HasClips) return 0;
            return StartLoop(cue, follow, 1f, 0.12f);
        }

        /// <inheritdoc />
        public void StopLoop(int handle, float fade)
        {
            if (handle == 0) return;
            for (var i = 0; i < _loops.Length; i++)
            {
                if (_loops[i].Handle != handle) continue;
                _loops[i].FadeOutAndStop(fade);
                return;
            }
            var v = FindVoice(handle);
            v?.FadeOutAndStop(fade);
        }

        /// <inheritdoc />
        public void SetListenerOverride(Transform target)
        {
            _override = target;
            UpdateListener();
        }

        /// <inheritdoc />
        public void PlayWeaponShot(string weaponId, Vector3 position, bool isPlayer)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            if (!_initialized) Initialize();
            _weapons.Play(weaponId, position, isPlayer, AudioRuntime.Now);
        }

        /// <inheritdoc />
        public void Tinnitus(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f) return;
            seconds = Mathf.Clamp(seconds, 0.2f, 20f);
            var now = AudioRuntime.Now;
            var mix = AudioRuntime.Mix;
            // A stepped recovery (deepest active duck wins), smoothed by the mix release (GDD §13.2 tinnitus muffle).
            mix.Duck(MixCategory.Effects, -18f, seconds * 0.35f, now);
            mix.Duck(MixCategory.Effects, -9f, seconds * 0.7f, now);
            mix.Duck(MixCategory.Effects, -4f, seconds, now);
            mix.Duck(MixCategory.Ambience, -18f, seconds * 0.35f, now);
            mix.Duck(MixCategory.Ambience, -8f, seconds * 0.8f, now);
            mix.Duck(MixCategory.Music, -12f, seconds * 0.5f, now);
            mix.Duck(MixCategory.Music, -5f, seconds, now);
            mix.Duck(MixCategory.Voice, -10f, seconds * 0.35f, now);
            mix.Duck(MixCategory.Voice, -4f, seconds * 0.7f, now);
            if (now + seconds > _muffleEnd)
            {
                _muffleStart = now;
                _muffleEnd = now + seconds;
            }

            var whine = FindVoice(_tinnitusHandle);
            if (whine != null)
            {
                whine.FadeTarget = 1f;
                whine.Fade = 1f;
                whine.StopAtSilence = false;
                whine.FadeOutAt = now + seconds * 0.6f;
                whine.FadeOutSeconds = seconds * 0.4f;
                whine.HardStopAt = now + seconds + 0.1f;
                return;
            }
            if (!TryGetCue(_tinnitusCue, out var cue) || !cue.HasClips) return;
            var clip = PickClip(cue.Id, cue.Clips);
            if (clip == null) return;
            var r = new PlayRequest
            {
                Clip = clip, CueId = cue.Id, Category = MixCategory.Tinnitus, Position = AudioRuntime.ListenerPosition,
                Volume = cue.Volume, Pitch = 1f, SpatialBlend = 0f, MinDistance = 1f, MaxDistance = 10f,
                Rolloff = AudioRolloffMode.Logarithmic, Priority = 100, LoopSource = clip.length < seconds, Audibility = 1f,
            };
            _tinnitusHandle = StartVoice(ref r);
            var v = FindVoice(_tinnitusHandle);
            if (v == null) return;
            v.FadeOutAt = now + seconds * 0.6f;
            v.FadeOutSeconds = seconds * 0.4f;
            v.HardStopAt = now + seconds + 0.1f;
        }

        /// <inheritdoc />
        public void SetCategoryGain(string category, float linear) => AudioRuntime.Mix.SetCategoryGain(category, linear);

        /// <inheritdoc />
        public void Duck(string category, float db, float seconds)
        {
            var now = AudioRuntime.Now;
            var mix = AudioRuntime.Mix;
            if (category == AudioCategories.Master)
            {
                mix.Duck(MixCategory.Music, db, seconds, now);
                mix.Duck(MixCategory.Effects, db, seconds, now);
                mix.Duck(MixCategory.Voice, db, seconds, now);
                mix.Duck(MixCategory.Ambience, db, seconds, now);
                return;
            }
            mix.Duck(MixCategories.Parse(category), db, seconds, now);
        }

        /// <inheritdoc />
        public bool HasCue(string cueId)
        {
            var lib = Library;
            return lib != null && cueId != null && lib.TryGet(cueId, out var cue) && cue != null && cue.HasClips;
        }

        // ---------------------------------------------------------------- package-internal API

        /// <summary>Looks a cue up; logs a missing cue once.</summary>
        internal bool TryGetCue(string cueId, out AudioCue cue)
        {
            cue = null;
            if (string.IsNullOrEmpty(cueId)) return false;
            if (!_initialized) Initialize();
            var lib = Library;
            if (lib == null) return false;
            if (lib.TryGet(cueId, out cue) && cue != null) return true;
            LSLog.MissingAsset(LogCategory.Audio, cueId, "LIB_Audio (Assets/_Game/Audio)", this);
            cue = null;
            return false;
        }

        /// <summary>A random float 0..1 from the shared picker.</summary>
        internal float Random01() => _picker.Value();

        /// <summary>Picks a non-null variant without audible repetition.</summary>
        internal AudioClip PickClip(string key, AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            var i = _picker.Next(key, clips.Length);
            for (var k = 0; k < clips.Length; k++)
            {
                var c = clips[(i + k) % clips.Length];
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>Plays a cue with variants, far crossfade, propagation delay and culling; returns a handle or 0.</summary>
        internal int PlayCue(AudioCue cue, Vector3 position, Transform follow, bool force2D, float volume, int priorityBoost, float pitchScale = 1f)
        {
            if (cue == null || (!cue.HasClips && !cue.HasFar)) return 0;
            if (!_initialized) Initialize();
            var now = AudioRuntime.Now;
            if (cue.MinInterval > 0f && _lastStart.TryGetValue(cue.Id, out var last) && now - last < cue.MinInterval) return 0;
            if (cue.MaxInstances > 0 && CountInstances(cue.Id, now) >= cue.MaxInstances) StopOldestInstance(cue.Id, now);

            var spatial = !force2D && cue.SpatialBlend > 0.001f;
            var pos = follow != null ? follow.position : position;
            var dist = spatial ? Vector3.Distance(pos, AudioRuntime.ListenerPosition) : 0f;
            var near = 1f;
            var far = 0f;
            if (spatial && cue.HasFar)
            {
                var t = AudioMath.FarShare(dist, cue.FarCrossfade.x, cue.FarCrossfade.y, out near, out far);
                var m = Muffle;
                if (m > 0f && m * 0.85f > t)
                {
                    var tm = m * 0.85f;
                    near = Mathf.Cos(tm * Mathf.PI * 0.5f);
                    far = Mathf.Sin(tm * Mathf.PI * 0.5f);
                }
                if (dist > cue.FarMaxDistance * 1.1f) return 0;
            }
            else if (spatial && dist > cue.MaxDistance * 1.1f) return 0;
            if (!cue.HasClips)
            {
                // Only distance variants: use them at full level.
                near = 0f;
                far = 1f;
            }

            var delay = spatial && follow == null && cue.SpeedOfSound ? AudioMath.PropagationDelay(dist) : 0f;
            var vol = cue.Volume * Mathf.Max(0f, volume) * AudioMath.JitterGain(cue.VolumeJitterDb, _picker.Value());
            var pitch = AudioMath.JitterPitch(cue.PitchJitter, _picker.Value()) * (pitchScale > 0f && !float.IsNaN(pitchScale) ? pitchScale : 1f);
            var cat = MixCategories.Parse(cue.Category);
            var prio = Mathf.Clamp(cue.Priority + priorityBoost, 0, 100);
            var gain = AudioRuntime.Mix.Gain(cat);

            var handle = 0;
            if (near > 0.001f)
            {
                var r = new PlayRequest
                {
                    Clip = PickClip(cue.Id, cue.Clips), CueId = cue.Id, Category = cat, Position = pos, Follow = follow,
                    Volume = vol * near, Pitch = pitch, SpatialBlend = spatial ? cue.SpatialBlend : 0f,
                    MinDistance = cue.MinDistance, MaxDistance = cue.MaxDistance, Rolloff = cue.Rolloff, Spread = cue.Spread,
                    Priority = prio, Delay = delay,
                    Audibility = vol * near * gain * (spatial ? AudioMath.RolloffGain(dist, cue.MinDistance, cue.MaxDistance, cue.Rolloff) : 1f),
                };
                handle = StartVoice(ref r);
            }
            if (far > 0.001f && cue.HasFar)
            {
                var r = new PlayRequest
                {
                    Clip = PickClip(null, cue.FarClips), CueId = cue.Id, Category = cat, Position = pos, Follow = follow,
                    Volume = vol * far, Pitch = pitch, SpatialBlend = spatial ? 1f : 0f,
                    MinDistance = cue.FarMinDistance, MaxDistance = cue.FarMaxDistance, Rolloff = AudioRolloffMode.Logarithmic,
                    Priority = prio, Delay = delay,
                    Audibility = vol * far * gain * (spatial ? AudioMath.RolloffGain(dist, cue.FarMinDistance, cue.FarMaxDistance, AudioRolloffMode.Logarithmic) : 1f),
                };
                var h2 = StartVoice(ref r);
                if (handle == 0) handle = h2;
            }
            if (handle != 0 && cue.MinInterval > 0f) _lastStart[cue.Id] = now;
            return handle;
        }

        /// <summary>Starts a pooled voice (stealing if needed); returns its handle or 0 when rejected.</summary>
        internal int StartVoice(ref PlayRequest r)
        {
            if (r.Clip == null) return 0;
            if (!_initialized) Initialize();
            var now = AudioRuntime.Now;
            var free = FindFreeVoice(now);
            if (free == null || CountAudible(now) >= _audibleCap)
            {
                var victim = PickVictim(r.Priority, r.Audibility, now);
                if (victim == null) return 0;
                victim.Release();
                if (free == null) free = victim;
            }
            var v = free;
            var src = v.Source;
            var insert = AudioRuntime.InsertActive && r.Category != MixCategory.Insert;
            src.clip = r.Clip;
            src.loop = r.LoopSource;
            src.pitch = Mathf.Clamp(r.Pitch, 0.1f, 3f);
            src.spatialBlend = r.SpatialBlend;
            src.minDistance = Mathf.Max(0.01f, r.MinDistance);
            src.maxDistance = Mathf.Max(src.minDistance + 0.01f, r.MaxDistance);
            src.rolloffMode = r.Rolloff == AudioRolloffMode.Custom ? AudioRolloffMode.Logarithmic : r.Rolloff;
            src.spread = r.Spread;
            src.priority = MixCategories.UnityPriority(r.Category, r.Priority);
            src.ignoreListenerPause = MixCategories.IgnoresListenerPause(r.Category) || insert;
            v.Transform.position = r.Follow != null ? r.Follow.position : r.Position;

            v.Handle = NextHandle();
            v.CueId = r.CueId;
            v.Category = r.Category;
            v.Insert = insert;
            v.BaseVolume = r.Volume;
            v.BasePitch = src.pitch;
            v.Priority = r.Priority;
            v.Audibility = r.Audibility;
            v.StartedAt = now;
            v.Follow = r.Follow;
            v.Following = r.Follow != null;
            v.Fade = 1f;
            v.FadeTarget = 1f;
            v.FadeSpeed = 0f;
            v.StopAtSilence = false;
            v.FadeOutAt = float.MaxValue;
            v.HardStopAt = float.MaxValue;

            src.volume = r.Volume * AudioRuntime.Gain(r.Category, insert);
            var offset = 0f;
            if (r.StartOffset > 0f && r.StartOffset < r.Clip.length - 0.02f)
            {
                offset = r.StartOffset;
                src.time = offset;
            }
            else src.timeSamples = 0;
            if (r.Delay > 0.001f) src.PlayDelayed(r.Delay);
            else src.Play();
            var length = r.LoopSource ? 1e6f : (r.Clip.length - offset) / Mathf.Max(0.1f, src.pitch);
            v.EndsAt = now + r.Delay + length + 0.05f;
            return v.Handle;
        }

        /// <summary>Starts a loop slot for a cue; returns its handle or 0.</summary>
        internal int StartLoop(AudioCue cue, Transform follow, float volume, float fadeIn)
        {
            if (!_initialized) Initialize();
            var slot = FindFreeLoop(cue.Priority);
            if (slot == null)
            {
                LSLog.WarnOnce("audio-loops-full", LogCategory.Audio, "AudioService: every loop slot is busy; '" + cue.Id + "' was not started.", this);
                return 0;
            }
            var count = cue.Clips.Length;
            var idx = _picker.Next(cue.Id, count);
            AudioClip clip = null;
            for (var k = 0; k < count && clip == null; k++)
            {
                var j = (idx + k) % count;
                if (cue.Clips[j] == null) continue;
                clip = cue.Clips[j];
                idx = j;
            }
            if (clip == null) return 0;
            var region = cue.LoopRegion(idx);
            var spatial = follow != null && cue.SpatialBlend > 0.001f;
            var cat = MixCategories.Parse(cue.Category);
            var insert = AudioRuntime.InsertActive && cat != MixCategory.Insert;
            ConfigureLoopSource(slot.Looper.A, cue, spatial, cat, insert);
            ConfigureLoopSource(slot.Looper.B, cue, spatial, cat, insert);
            slot.Transform.position = follow != null ? follow.position : AudioRuntime.ListenerPosition;

            // A second instance of the same loop starts somewhere else in its body so the two never phase.
            var startSample = 0;
            if (CountLoopInstances(cue.Id) > 0 && clip.frequency > 0)
            {
                var a = region.y > region.x ? region.x : 0f;
                var b = region.y > region.x ? region.y : clip.length;
                startSample = (int)((a + (b - a) * _picker.Value()) * clip.frequency);
            }

            slot.Handle = NextHandle();
            slot.CueId = cue.Id;
            slot.Category = cat;
            slot.Insert = insert;
            slot.BaseVolume = cue.Volume * Mathf.Max(0f, volume);
            slot.Priority = cue.Priority;
            slot.StartedAt = AudioRuntime.Now;
            slot.Follow = follow;
            slot.Following = follow != null;
            slot.Is3D = spatial;
            slot.Fade = fadeIn > 0.001f ? 0f : 1f;
            slot.FadeTarget = 1f;
            slot.FadeSpeed = fadeIn > 0.001f ? 1f / fadeIn : 0f;
            slot.StopAtSilence = false;
            slot.Looper.SetVolume(slot.BaseVolume * slot.Fade * AudioRuntime.Gain(cat, insert));
            slot.Looper.Play(clip, true, region, startSample, AudioSettings.dspTime);
            return slot.Handle;
        }

        private static void ConfigureLoopSource(AudioSource s, AudioCue cue, bool spatial, MixCategory cat, bool insert)
        {
            s.spatialBlend = spatial ? cue.SpatialBlend : 0f;
            s.minDistance = cue.MinDistance;
            s.maxDistance = Mathf.Max(cue.MinDistance + 0.01f, cue.MaxDistance);
            s.rolloffMode = cue.Rolloff == AudioRolloffMode.Custom ? AudioRolloffMode.Logarithmic : cue.Rolloff;
            s.spread = cue.Spread;
            s.pitch = 1f;
            s.priority = MixCategories.UnityPriority(cat, cue.Priority);
            s.ignoreListenerPause = MixCategories.IgnoresListenerPause(cat) || insert;
        }

        /// <summary>The voice of a handle, or null.</summary>
        internal OneShotVoice FindVoice(int handle)
        {
            if (handle == 0) return null;
            for (var i = 0; i < _voices.Length; i++)
                if (_voices[i].Handle == handle) return _voices[i];
            return null;
        }

        /// <summary>True while a one-shot or loop handle is playing.</summary>
        internal bool IsHandleActive(int handle)
        {
            if (handle == 0) return false;
            for (var i = 0; i < _loops.Length; i++)
                if (_loops[i].Handle == handle) return true;
            var v = FindVoice(handle);
            return v != null && v.Active(AudioRuntime.Now);
        }

        /// <summary>Replaces the library (tests, builders).</summary>
        internal void SetLibrary(AudioLibrary library) => _library = library;

        /// <summary>Number of one-shots and loops currently audible (tests, debug).</summary>
        internal int AudibleCount => CountAudible(AudioRuntime.Now);

        private int NextHandle()
        {
            var h = _nextHandle++;
            if (_nextHandle <= 0) _nextHandle = 1;
            return h;
        }

        private OneShotVoice FindFreeVoice(float now)
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Handle == 0) return v;
                if (!v.Active(now))
                {
                    v.Release();
                    return v;
                }
            }
            return null;
        }

        private LoopVoice FindFreeLoop(int priority)
        {
            LoopVoice weakest = null;
            for (var i = 0; i < _loops.Length; i++)
            {
                var l = _loops[i];
                if (l.Handle == 0) return l;
                if (weakest == null || l.Priority < weakest.Priority) weakest = l;
            }
            if (weakest != null && weakest.Priority < priority)
            {
                weakest.Release();
                return weakest;
            }
            return null;
        }

        private int CountAudible(float now)
        {
            var n = 0;
            for (var i = 0; i < _voices.Length; i++)
                if (_voices[i].Active(now)) n++;
            for (var i = 0; i < _loops.Length; i++)
                if (_loops[i].Handle != 0) n++;
            return n;
        }

        private int CountInstances(string cueId, float now)
        {
            var n = 0;
            for (var i = 0; i < _voices.Length; i++)
                if (_voices[i].Handle != 0 && _voices[i].CueId == cueId && _voices[i].Active(now)) n++;
            return n;
        }

        private int CountLoopInstances(string cueId)
        {
            var n = 0;
            for (var i = 0; i < _loops.Length; i++)
                if (_loops[i].Handle != 0 && _loops[i].CueId == cueId) n++;
            return n;
        }

        private void StopOldestInstance(string cueId, float now)
        {
            OneShotVoice oldest = null;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Handle == 0 || v.CueId != cueId || !v.Active(now)) continue;
                if (oldest == null || v.StartedAt < oldest.StartedAt) oldest = v;
            }
            oldest?.Release();
        }

        private OneShotVoice PickVictim(int priority, float audibility, float now)
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                _scratch[i] = new VoiceInfo
                {
                    Active = v.Active(now),
                    Priority = v.Priority,
                    Audibility = v.Audibility * v.Fade,
                    StartedAt = v.StartedAt,
                    Protected = v.Category == MixCategory.Tinnitus || v.Category == MixCategory.Insert,
                };
            }
            var idx = VoiceStealing.PickVictim(_scratch, _voices.Length, priority, audibility);
            return idx >= 0 ? _voices[idx] : null;
        }

        // ---------------------------------------------------------------- events

        private void ApplySettings()
        {
            var s = _settings.Value;
            AudioRuntime.Mix.SetUserVolumes(s != null ? s.Current : null);
        }

        private void OnPauseChanged(bool paused)
        {
            for (var i = 0; i < _loops.Length; i++)
            {
                var l = _loops[i];
                if (l.Handle == 0 || MixCategories.IgnoresListenerPause(l.Category) || l.Insert) continue;
                if (paused) l.Looper.OnPause();
                else l.Looper.OnResume();
            }
            AudioRuntime.RaiseListenerPaused(paused);
        }

        private void OnInsertStarted(string id)
        {
            AudioRuntime.InsertActive = true;
            if (!_pauseWorldDuringInserts || AudioListener.pause) return;
            AudioListener.pause = true;
            _pausedForInsert = true;
            OnPauseChanged(true);
        }

        private void OnInsertEnded(string id)
        {
            AudioRuntime.InsertActive = false;
            if (!_pausedForInsert) return;
            _pausedForInsert = false;
            // The pause menu may have opened meanwhile; it owns the pause then.
            if (GameServices.TryGet(out IPauseService pause) && pause.IsPaused) return;
            AudioListener.pause = false;
            OnPauseChanged(false);
        }

        private void OnCombatStateChanged(bool inCombat)
        {
            AudioRuntime.InCombat = inCombat;
            if (inCombat) AudioRuntime.Mix.SetHold(_combatKey, MixCategory.Ambience, _combatAmbienceDuckDb);
            else AudioRuntime.Mix.ClearHold(_combatKey, MixCategory.Ambience);
        }

        private void OnSectionLoaded(SectionId section)
        {
            if (_disableOtherListeners) DisableOtherListeners();
        }

        private void OnCheckpointLoaded(string cpId)
        {
            OnCombatStateChanged(false);
            _weapons.Clear();
            var now = AudioRuntime.Now;
            for (var i = 0; i < _voices.Length; i++)
            {
                var v = _voices[i];
                if (v.Handle == 0 || !v.Active(now)) continue;
                if (v.Category == MixCategory.UI || v.Category == MixCategory.Insert || v.Insert) continue;
                v.FadeOutAndStop(0.2f);
            }
        }

        private void DisableOtherListeners()
        {
            if (_listener == null) return;
            var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            for (var i = 0; i < all.Length; i++)
            {
                var l = all[i];
                if (l == _listener || !l.enabled) continue;
                l.enabled = false;
                LSLog.Warn(LogCategory.Audio, "Disabled extra AudioListener on '" + l.name + "': the AudioService owns the only listener (WP06).", l);
            }
        }
    }
}
