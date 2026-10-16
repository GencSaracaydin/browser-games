using System;
using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Audio.Music
{
    /// <summary>
    /// <see cref="IMusicService"/> (TECH_PLAN §7.9, GDD §10.3):
    /// <list type="bullet">
    /// <item>Cues and layers: <c>PlayCue("MUS_02_MARKET", 1)</c> starts LO and HI together on one DSP time (identical loop
    /// grids, WP06_SFX) with only the requested layer audible; <see cref="SetLayer"/> crossfades on the next bar line.</item>
    /// <item>Gapless loops of each clip's loop region with two sources (<see cref="ScheduledLooper"/>); MUS_01 plays its
    /// intro then loops its last 9 bars; one-shot cues (MUS_03, MUS_06) end by themselves.</item>
    /// <item>Cue changes start on the next beat of the playing cue and crossfade; <see cref="HardCut"/> cuts everything.</item>
    /// <item>Stingers (<c>STG_*</c>) on their own sources; during a glitch insert they ignore the listener pause.</item>
    /// <item>Music ducks under VO through the shared mix (the VOManager holds −6 dB on Music).</item>
    /// <item>Clips are decoded with the VO beat windows (<see cref="MusicWindows"/>) and on demand; a cue whose data is
    /// still loading starts when ready (≤ 4 s).</item>
    /// <item><see cref="ISaveable"/> "Music": the cue and layer return with a checkpoint (GDD §17).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Audio + 6)]
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour, IMusicService, ISaveable
    {
        [SerializeField, Tooltip("LIB_Audio; when empty the registered library is used.")]
        private AudioLibrary _library;
        [SerializeField, Range(2, 8)] private int _layerVoices = 6;
        [SerializeField] private float _loadTimeout = 4f;
        [SerializeField, Tooltip("Restore fade after a checkpoint reload.")] private float _restoreFade = 1.5f;
        [SerializeField, Tooltip("The running cue ducks under a stinger (WP06_SFX: STG_GREYCOAT -6 dB).")]
        private float _stingerDuckDb = -6f;

        private MusicLayer[] _layers = new MusicLayer[0];
        private readonly MusicLayer[] _active = new MusicLayer[2];
        private int _activeCount;
        private AudioSource[] _stingers = new AudioSource[0];
        private readonly AudioCue[] _stingerCues = new AudioCue[2];
        private readonly bool[] _stingerInsert = new bool[2];
        private int _nextStinger;
        private string _currentCue;
        private int _currentLayer;
        private MusicClock _clock;

        private string _pendingBase;
        private AudioCue[] _pendingCues;
        private int _pendingLayer;
        private float _pendingFade;
        private float _pendingSince;

        private readonly HashSet<string> _wanted = new HashSet<string>(StringComparer.Ordinal);
        private Func<string, bool> _exists;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ICheckpointService _registeredWith;
        private bool _initialized;
        private float _nextLibraryLookup;
        private bool _libraryWarned;
        private float _layerDuck = 1f;

        /// <inheritdoc />
        public string CurrentCue => _currentCue;
        /// <inheritdoc />
        public int CurrentLayer => _currentLayer;
        /// <inheritdoc />
        public string SaveKey => "Music";

        /// <summary>DSP time (the unscaled clock in edit mode, so tests can drive fades).</summary>
        internal static double Dsp => Application.isPlaying ? AudioSettings.dspTime : AudioRuntime.Now;

        /// <summary>The audio library, or null.</summary>
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
                    LSLog.MissingAsset(LogCategory.Music, "LIB_Audio", Libraries.Folder + "/LIB_Audio.asset", this);
                }
                return null;
            }
        }

        private void Awake()
        {
            Initialize();
            GameServices.Register<IMusicService>(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister<IMusicService>(this);
            _registeredWith?.Unregister(this);
            _registeredWith = null;
        }

        private void OnEnable()
        {
            AudioRuntime.WindowPrepared += PrepareWindow;
            AudioRuntime.ListenerPaused += OnPauseChanged;
        }

        private void OnDisable()
        {
            AudioRuntime.WindowPrepared -= PrepareWindow;
            AudioRuntime.ListenerPaused -= OnPauseChanged;
        }

        /// <summary>Builds the sources (Awake; tests call it in edit mode).</summary>
        internal void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _exists = Exists;
            var root = new GameObject("MusicLayers").transform;
            root.SetParent(transform, false);
            _layers = new MusicLayer[Mathf.Max(2, _layerVoices)];
            for (var i = 0; i < _layers.Length; i++)
            {
                var go = new GameObject("Layer_" + i);
                go.transform.SetParent(root, false);
                var a = go.AddComponent<AudioSource>();
                var b = go.AddComponent<AudioSource>();
                Prepare(a);
                Prepare(b);
                _layers[i] = new MusicLayer { Looper = new ScheduledLooper(a, b) };
            }
            _stingers = new AudioSource[2];
            for (var i = 0; i < _stingers.Length; i++)
            {
                var go = new GameObject("Stinger_" + i);
                go.transform.SetParent(root, false);
                var s = go.AddComponent<AudioSource>();
                Prepare(s);
                _stingers[i] = s;
            }
        }

        private static void Prepare(AudioSource s)
        {
            AudioService.PrepareSource(s);
            s.spatialBlend = 0f;
            s.priority = MixCategories.UnityPriority(MixCategory.Music, 100);
        }

        private void Update() => Tick(Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f));

        /// <summary>One frame (Update; tests).</summary>
        internal void Tick(float dt)
        {
            if (!_initialized) Initialize();
            RegisterWithCheckpoints();
            var dsp = Dsp;
            if (_pendingBase != null && (CuesLoaded(_pendingCues) || AudioRuntime.Now - _pendingSince > _loadTimeout))
            {
                var b = _pendingBase;
                _pendingBase = null;
                StartCue(b, _pendingCues, _pendingLayer, _pendingFade);
            }
            var music = AudioRuntime.Mix.Gain(MixCategory.Music);
            var musicInsert = AudioRuntime.Mix.GainDuringInsert(MixCategory.Music);
            var stinger = false;
            for (var i = 0; i < _stingerCues.Length; i++)
                if (_stingerCues[i] != null) stinger = true;
            var duckTarget = stinger ? MathX.DbToLinear(-Mathf.Abs(_stingerDuckDb)) : 1f;
            // Fast attack under the stinger's hit, slow release after it.
            var tau = duckTarget < _layerDuck ? 0.12f : 1.5f;
            _layerDuck = dt > 0f ? Mathf.Lerp(_layerDuck, duckTarget, 1f - Mathf.Exp(-dt / tau)) : _layerDuck;
            var layerGain = music * _layerDuck;
            for (var i = 0; i < _layers.Length; i++)
            {
                var l = _layers[i];
                if (l.Busy) l.Tick(dsp, dt, layerGain);
            }
            // A one-shot cue that ended clears the current cue.
            if (_activeCount > 0 && _pendingBase == null)
            {
                var any = false;
                for (var i = 0; i < _activeCount; i++)
                    if (_active[i] != null && _active[i].Busy) any = true;
                if (!any)
                {
                    _activeCount = 0;
                    _currentCue = null;
                }
            }
            for (var i = 0; i < _stingers.Length; i++)
            {
                var s = _stingers[i];
                var cue = _stingerCues[i];
                if (cue == null) continue;
                if (!s.isPlaying && Application.isPlaying)
                {
                    _stingerCues[i] = null;
                    continue;
                }
                s.volume = cue.Volume * (_stingerInsert[i] ? musicInsert : music);
            }
        }

        // ---------------------------------------------------------------- IMusicService

        /// <inheritdoc />
        public void PlayCue(string cueId, int layer = 0, float fade = 1f)
        {
            if (!_initialized) Initialize();
            if (Library == null) return;
            if (!MusicCueResolver.Resolve(cueId, _exists, out var baseId, out var layerIds, out var hint))
            {
                LSLog.MissingAsset(LogCategory.Music, cueId ?? "(null)", "Assets/_Game/Audio/Music", this);
                return;
            }
            if (layer == 0 && hint > 0) layer = hint;
            layer = Mathf.Clamp(layer, 0, layerIds.Length - 1);
            if (baseId == _currentCue && _activeCount > 0 && _pendingBase == null)
            {
                SetLayer(layer, fade);
                return;
            }
            var cues = new AudioCue[layerIds.Length];
            for (var i = 0; i < layerIds.Length; i++)
            {
                Library.TryGet(layerIds[i], out cues[i]);
                Load(cues[i]);
            }
            if (!CuesLoaded(cues))
            {
                _pendingBase = baseId;
                _pendingCues = cues;
                _pendingLayer = layer;
                _pendingFade = fade;
                _pendingSince = AudioRuntime.Now;
                return;
            }
            _pendingBase = null;
            StartCue(baseId, cues, layer, fade);
        }

        /// <inheritdoc />
        public void SetLayer(int layer, float fade)
        {
            if (_activeCount == 0)
            {
                if (_pendingBase != null) _pendingLayer = layer;
                return;
            }
            layer = Mathf.Clamp(layer, 0, _activeCount - 1);
            if (layer == _currentLayer) return;
            var dsp = Dsp;
            var at = _clock.HasGrid ? _clock.NextBar(dsp, 0.05) : dsp;
            for (var i = 0; i < _activeCount; i++)
                _active[i]?.FadeTo(i == layer ? 1f : 0f, Mathf.Max(0.05f, fade), at);
            _currentLayer = layer;
        }

        /// <inheritdoc />
        public void Stinger(string id)
        {
            if (!_initialized) Initialize();
            var lib = Library;
            if (lib == null || string.IsNullOrEmpty(id)) return;
            if (!lib.TryGet(id, out var cue) || cue == null || !cue.HasClips)
            {
                LSLog.MissingAsset(LogCategory.Music, id, "Assets/_Game/Audio/Music", this);
                return;
            }
            var clip = cue.Clips[0];
            for (var i = 1; i < cue.Clips.Length && clip == null; i++) clip = cue.Clips[i];
            if (clip == null) return;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            var idx = _nextStinger;
            _nextStinger = (_nextStinger + 1) % _stingers.Length;
            var s = _stingers[idx];
            var insert = AudioRuntime.InsertActive;
            s.Stop();
            s.clip = clip;
            s.ignoreListenerPause = insert;
            s.volume = cue.Volume * (insert ? AudioRuntime.Mix.GainDuringInsert(MixCategory.Music) : AudioRuntime.Mix.Gain(MixCategory.Music));
            s.Play();
            _stingerCues[idx] = cue;
            _stingerInsert[idx] = insert;
        }

        /// <inheritdoc />
        public void Stop(float fade)
        {
            _pendingBase = null;
            var dsp = Dsp;
            for (var i = 0; i < _layers.Length; i++)
            {
                var l = _layers[i];
                if (!l.Busy) continue;
                l.FadeTo(0f, Mathf.Max(0.01f, fade), dsp);
                l.StopWhenSilent = true;
            }
            _activeCount = 0;
            _currentCue = null;
        }

        /// <inheritdoc />
        public void HardCut()
        {
            _pendingBase = null;
            for (var i = 0; i < _layers.Length; i++)
                if (_layers[i].Busy) _layers[i].Stop();
            for (var i = 0; i < _stingers.Length; i++)
            {
                _stingers[i].Stop();
                _stingerCues[i] = null;
            }
            _activeCount = 0;
            _currentCue = null;
        }

        // ---------------------------------------------------------------- ISaveable

        /// <inheritdoc />
        public void Capture(SaveWriter w)
        {
            w.Write("cue", _pendingBase ?? _currentCue ?? "");
            w.Write("layer", _pendingBase != null ? _pendingLayer : _currentLayer);
        }

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            var cue = r.ReadString("cue", "");
            var layer = r.ReadInt("layer", 0);
            if (string.IsNullOrEmpty(cue)) Stop(_restoreFade);
            else PlayCue(cue, layer, _restoreFade);
        }

        // ---------------------------------------------------------------- internals

        /// <summary>Replaces the library (tests, builders).</summary>
        internal void SetLibrary(AudioLibrary library) => _library = library;

        /// <summary>Current duck of the layers under a stinger (linear, tests and debug).</summary>
        internal float LayerDuck => _layerDuck;

        /// <summary>Gain of the active layer <paramref name="index"/> (tests, debug).</summary>
        internal float LayerGain(int index) => index >= 0 && index < _activeCount && _active[index] != null ? _active[index].Gain : 0f;

        /// <summary>Number of busy layer players (tests, debug).</summary>
        internal int BusyLayers
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _layers.Length; i++)
                    if (_layers[i].Busy) n++;
                return n;
            }
        }

        private bool Exists(string id)
        {
            var lib = Library;
            return lib != null && lib.TryGet(id, out var c) && c != null && c.HasClips;
        }

        private void StartCue(string baseId, AudioCue[] cues, int layer, float fade)
        {
            var dsp = Dsp;
            var startDsp = _activeCount > 0 && _clock.HasGrid && fade > 0.05f ? _clock.NextBeat(dsp, 0.05) : dsp + 0.05;
            var outFade = Mathf.Max(0.05f, fade);
            for (var i = 0; i < _activeCount; i++)
            {
                var old = _active[i];
                if (old == null || !old.Busy) continue;
                old.FadeTo(0f, outFade, startDsp);
                old.StopWhenSilent = true;
            }
            _activeCount = 0;
            var insert = AudioRuntime.InsertActive;
            AudioCue first = null;
            for (var i = 0; i < cues.Length && _activeCount < _active.Length; i++)
            {
                var cue = cues[i];
                if (cue == null) continue;
                var l = Allocate();
                if (l == null)
                {
                    LSLog.WarnOnce("music-layers-full", LogCategory.Music, "MusicDirector: no free layer player for " + cue.Id, this);
                    break;
                }
                if (!l.Start(cue, startDsp, insert)) continue;
                l.FadeTo(i == layer ? 1f : 0f, fade, startDsp);
                _active[_activeCount++] = l;
                if (first == null) first = cue;
            }
            _currentCue = baseId;
            _currentLayer = Mathf.Clamp(layer, 0, Mathf.Max(0, _activeCount - 1));
            if (first != null)
            {
                var region = first.LoopRegion(0);
                _clock = new MusicClock(startDsp, region.x, region.y, first.BarSeconds);
            }
            else _clock = default;
        }

        private MusicLayer Allocate()
        {
            for (var i = 0; i < _layers.Length; i++)
                if (!_layers[i].Busy) return _layers[i];
            // Take the quietest fading layer.
            MusicLayer best = null;
            for (var i = 0; i < _layers.Length; i++)
            {
                var l = _layers[i];
                if (!l.StopWhenSilent) continue;
                if (best == null || l.Gain < best.Gain) best = l;
            }
            best?.Stop();
            return best;
        }

        private static void Load(AudioCue cue)
        {
            if (cue == null) return;
            for (var i = 0; i < cue.Clips.Length; i++)
            {
                var c = cue.Clips[i];
                if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            }
        }

        private static bool CuesLoaded(AudioCue[] cues)
        {
            if (cues == null) return true;
            for (var i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (cue == null || cue.Clips.Length == 0 || cue.Clips[0] == null) continue;
                var st = cue.Clips[0].loadState;
                if (st == AudioDataLoadState.Loading || st == AudioDataLoadState.Unloaded) return false;
            }
            return true;
        }

        private void PrepareWindow(int from, int to)
        {
            var lib = Library;
            if (lib == null) return;
            if (to < from)
            {
                var t = to;
                to = from;
                from = t;
            }
            _wanted.Clear();
            for (var i = 0; i < MusicWindows.Always.Length; i++) AddWanted(MusicWindows.Always[i]);
            for (var b = from; b <= to; b++)
            {
                var cues = MusicWindows.CuesOf(b);
                for (var i = 0; i < cues.Length; i++) AddWanted(cues[i]);
            }
            var entries = lib.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var cue = entries[i];
                if (cue == null || MixCategories.Parse(cue.Category) != MixCategory.Music) continue;
                if (_wanted.Contains(cue.Id))
                {
                    Load(cue);
                    continue;
                }
                if (IsPlaying(cue)) continue;
                for (var c = 0; c < cue.Clips.Length; c++)
                {
                    var clip = cue.Clips[c];
                    if (clip != null && !clip.preloadAudioData && clip.loadState == AudioDataLoadState.Loaded) clip.UnloadAudioData();
                }
            }
        }

        private void AddWanted(string id)
        {
            if (MusicCueResolver.Resolve(id, _exists, out _, out var layers, out _))
                for (var i = 0; i < layers.Length; i++) _wanted.Add(layers[i]);
        }

        private bool IsPlaying(AudioCue cue)
        {
            for (var i = 0; i < _layers.Length; i++)
                if (_layers[i].Busy && _layers[i].Cue == cue) return true;
            for (var i = 0; i < _stingerCues.Length; i++)
                if (_stingerCues[i] == cue) return true;
            return false;
        }

        private void OnPauseChanged(bool paused)
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                var l = _layers[i];
                if (!l.Busy) continue;
                if (paused) l.Looper.OnPause();
                else l.Looper.OnResume();
            }
        }

        private void RegisterWithCheckpoints()
        {
            var cp = _checkpoints.Value;
            if (cp == _registeredWith) return;
            _registeredWith?.Unregister(this);
            _registeredWith = cp;
            cp?.Register(this);
        }
    }
}
