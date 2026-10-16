using System;
using System.Collections;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace LongShadow.Platform
{
    /// <summary>
    /// The boot sequence in S_Boot (TECH_PLAN §5.2, D1, D20; GDD §8.1):
    /// <list type="number">
    /// <item>services register (Awake of the BootRoot components); the HTML loader already showed the engine download;</item>
    /// <item>Addressables initialises, S_Persistent loads and becomes the active scene, S0_Booth loads, the VO window for
    /// beats 0-1 is prepared; progress feeds the click gate's number groups;</item>
    /// <item>the gate accepts the click; S9_TownFar and S1_Approach start loading behind it (the intro holds at shot 8
    /// until they are in);</item>
    /// <item>on the click: audio resumes, pointer lock (unadjustedMovement first) and optional fullscreen are requested,
    /// pausing is allowed, and the mission starts (<c>StartMission</c>, or <c>ResumeFrom(cp)</c> with <c>?cp=</c>).</item>
    /// </list>
    /// <c>?autotest=1</c> skips the click (WP11's smoke test drives the run); <see cref="DevStart"/> can skip it in the
    /// editor. Systems that are missing (no MissionDirector yet) are logged and skipped, never fatal. The boot camera
    /// (black, culls nothing) renders only while no other camera is active.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform + 50)]
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private SectionStreamer _streamer;
        [SerializeField] private PointerLockController _pointerLock;
        [SerializeField] private PauseService _pause;
        [SerializeField, Tooltip("Used when no other IClickGate (WP08 ClickGateView) is registered.")]
        private HtmlClickGate _fallbackGate;
        [SerializeField] private DevStart _devStart;
        [SerializeField, Tooltip("Black camera that renders until S_Persistent's cameras exist.")]
        private Camera _bootCamera;
        [SerializeField, Tooltip("Loaded before the click is accepted (TECH_PLAN D20).")]
        private SectionId[] _beforeGate = { SectionId.Booth };
        [SerializeField, Tooltip("Requested when the gate becomes ready; keep loading behind the gate and the intro (D20).")]
        private SectionId[] _behindGate = { SectionId.TownFar, SectionId.Approach };
        [SerializeField, Tooltip("VO window prepared before the gate (TECH_PLAN §5.2: beats 0-1).")]
        private int _voWindowFrom = 0;
        [SerializeField] private int _voWindowTo = 1;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the gate progress for the Addressables catalog.")]
        private float _catalogShare = 0.05f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the gate progress for S_Persistent (the rest is the before-gate sections).")]
        private float _persistentShare = 0.55f;
        [SerializeField, Tooltip("A boot phase that takes longer than this (seconds) is reported as stalled.")]
        private float _phaseTimeout = 240f;

        private static BootPhase s_phase;

        /// <summary>Raised when the boot phase changes.</summary>
        public static event Action<BootPhase> PhaseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_phase = BootPhase.Starting;
            PhaseChanged = null;
        }

        /// <summary>Current boot phase of the session.</summary>
        public static BootPhase CurrentPhase => s_phase;

        private ServiceRef<IMissionDirector> _director;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IVOService> _vo;
        private IClickGate _gate;
        private bool _gateIsFallback;
        private bool _gateSubscribed;
        private float _phaseStartedAt;
        private bool _stallReported;
        private float _nextCameraCheck;
        private Camera[] _cameraBuffer = new Camera[16];
        private Action<string> _onMissionEvent;

        /// <summary>Phase of this boot.</summary>
        public BootPhase Phase => s_phase;
        /// <summary>Gate progress 0..1.</summary>
        public float Progress { get; private set; }
        /// <summary>The gate in use (WP08's view or the HTML fallback).</summary>
        public IClickGate Gate => _gate;

        private void Awake()
        {
            if (_streamer == null) _streamer = GetComponent<SectionStreamer>();
            if (_pointerLock == null) _pointerLock = GetComponent<PointerLockController>();
            if (_pause == null) _pause = GetComponent<PauseService>();
            if (_fallbackGate == null) _fallbackGate = GetComponent<HtmlClickGate>();
            if (_devStart == null) _devStart = GetComponent<DevStart>();
            LaunchOptions.Current = LaunchOptions.FromEnvironment(_devStart);
            AddressablesRemap.Install();
            LSLog.Info(LogCategory.Platform, "Boot " + Application.version + " (" + Application.platform + "), launch " + LaunchOptions.Current +
                                             (AddressablesRemap.Active ? ", published names" : string.Empty));
            SetPhase(BootPhase.Starting);
            _onMissionEvent = OnMissionEvent;
            GameEvents.MissionEvent += _onMissionEvent;
        }

        private void Start()
        {
            if (_streamer == null)
            {
                LSLog.Error(LogCategory.Platform, "Bootstrap has no SectionStreamer; rebuild S_Boot (LongShadow/Platform/Build Boot Scene).", this);
                return;
            }
            StartCoroutine(Boot());
        }

        private void OnDestroy()
        {
            if (_gate != null && _gateSubscribed) _gate.Clicked -= OnGateClicked;
            if (_onMissionEvent != null) GameEvents.MissionEvent -= _onMissionEvent;
        }

        /// <summary>
        /// WP08 shows the gate again after QUIT TO SESSION / END SESSION / REPLAY and raises UI.Gate.Ready: re-arm the page
        /// gesture so the next gate click also locks the pointer and resumes audio inside the browser's pointerdown.
        /// </summary>
        private void OnMissionEvent(string id)
        {
            if (id == SequenceIds.UIGateReady && s_phase == BootPhase.Running) ConfigureGestures();
        }

        private IEnumerator Boot()
        {
            ResolveGate();
            if (_pause != null) _pause.CanPause = false;
            _gate.SetVisible(true);
            _gate.SetReady(false);
            Report(0f, "ESTABLISHING LINK");

            // 1. Addressables catalog.
            SetPhase(BootPhase.Addressables);
            AsyncOperationHandle<IResourceLocator> init = default;
            try
            {
                init = Addressables.InitializeAsync(false);
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Streaming, e, this);
            }
            while (init.IsValid() && !init.IsDone)
            {
                Report(_catalogShare * init.PercentComplete, "RECEIVING CATALOG");
                yield return null;
            }
            if (init.IsValid())
            {
                if (init.Status != AsyncOperationStatus.Succeeded)
                    LSLog.Error(LogCategory.Streaming, "Addressables failed to initialise: " + (init.OperationException?.Message ?? "unknown") +
                                                       "; scenes fall back to their paths in the editor.", this);
                Addressables.Release(init);
            }

            // 2. S_Persistent (systems, player, HUD; TECH_PLAN D1).
            SetPhase(BootPhase.Persistent);
            var persistentStart = _catalogShare;
            var persistentShare = Mathf.Clamp01(_persistentShare);
            var probe = StartCoroutine(_streamer.LoadPersistent());
            while (!_streamer.PersistentLoaded)
            {
                Report(persistentStart + persistentShare * _streamer.PersistentProgress, "RECEIVING SESSION");
                yield return null;
            }
            if (probe != null) yield return probe;
            GameEvents.RaiseMissionEvent(BootEvents.PersistentLoaded);
            UpdateBootCamera();

            // 3. Sections needed before the click (S0_Booth, D20).
            SetPhase(BootPhase.BootSections);
            foreach (var s in _beforeGate) _streamer.Request(s, true);
            var sectionsStart = persistentStart + persistentShare;
            var sectionsShare = Mathf.Max(0f, 1f - sectionsStart);
            while (!AllLoaded(_beforeGate, out var fraction, out var loading))
            {
                Report(sectionsStart + sectionsShare * fraction, loading != null ? "RECEIVING " + loading.ToUpperInvariant() : "RECEIVING");
                yield return null;
            }

            // 4. VO for the first beats.
            var vo = _vo.Value;
            if (vo != null) vo.PrepareWindow(_voWindowFrom, _voWindowTo);
            else LSLog.WarnOnce("boot-no-vo", LogCategory.Platform, "No IVOService registered; the VO window was not prepared.");

            // 5. Gate ready; the rest keeps loading behind it.
            foreach (var s in _behindGate) _streamer.Request(s, true);
            Report(1f, string.Empty);
            SetPhase(BootPhase.GateReady);
            ConfigureGestures();
            _gate.SetReady(true);
            GameEvents.RaiseMissionEvent(BootEvents.GateReady);

            var skip = LaunchOptions.Current.Autotest;
#if UNITY_EDITOR
            if (_devStart != null && _devStart.SkipClickGate) skip = true;
#endif
            if (skip)
            {
                if (LaunchOptions.Current.Autotest) GameEvents.RaiseMissionEvent(BootEvents.Autotest);
                OnGateClicked();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextCameraCheck)
            {
                _nextCameraCheck = Time.unscaledTime + 0.5f;
                UpdateBootCamera();
            }
            if (_stallReported) return;
            var p = s_phase;
            if ((p == BootPhase.Addressables || p == BootPhase.Persistent || p == BootPhase.BootSections) &&
                Time.unscaledTime - _phaseStartedAt > _phaseTimeout)
            {
                _stallReported = true;
                var msg = "Boot stalled in phase " + p + " for " + _phaseTimeout.ToString("0") + " s.";
                LSLog.Error(LogCategory.Platform, msg, this);
                GameEvents.RaiseMissionEvent(BootEvents.Failed);
                if (_gateIsFallback && _fallbackGate != null) _fallbackGate.ShowError("LINK FAILURE: " + msg);
                SetPhase(BootPhase.Failed);
            }
        }

        private void ResolveGate()
        {
            if (GameServices.TryGet(out IClickGate gate) && !ReferenceEquals(gate, _fallbackGate))
            {
                _gate = gate;
                _gateIsFallback = false;
                WebNative.LoaderVisible(false); // WP08's in-engine gate replaces the HTML loader
                LSLog.Info(LogCategory.Platform, "Click gate: " + gate.GetType().Name);
            }
            else
            {
                if (_fallbackGate == null) _fallbackGate = gameObject.AddComponent<HtmlClickGate>();
                _gate = _fallbackGate;
                _gateIsFallback = true;
                if (!GameServices.Has<IClickGate>()) GameServices.Register<IClickGate>(_fallbackGate);
                LSLog.Info(LogCategory.Platform, "Click gate: HTML fallback (WP08 ClickGateView not present)");
            }
            if (!_gateSubscribed)
            {
                _gate.Clicked += OnGateClicked;
                _gateSubscribed = true;
            }
        }

        private void ConfigureGestures()
        {
            var settings = _settings.Value?.Current;
            var fullscreen = settings != null && settings.Fullscreen;
            var wantLock = !LaunchOptions.Current.NoLock && !LaunchOptions.Current.Autotest;
            if (_gateIsFallback)
            {
                _fallbackGate.ConfigureClick(fullscreen, wantLock);
            }
            else if (WebNative.Available)
            {
                // In-engine gate: let the page lock/resume inside the browser's own pointerdown (TECH_PLAN §5.2.3).
                var flags = WebNative.GestureAudio | (wantLock ? WebNative.GestureLock : 0) | (fullscreen ? WebNative.GestureFullscreen : 0);
                WebNative.ArmGesture(flags, true);
            }
        }

        private void OnGateClicked()
        {
            if (s_phase != BootPhase.GateReady && s_phase != BootPhase.Running) return;
            var director = _director.Value;
            if (director != null && director.IsRunning) return;

            SetPhase(BootPhase.Running);
            _gate.SetReady(false);
            _gate.SetVisible(false);
            WebBridge.ResumeAudio();
            var launch = LaunchOptions.Current;
            if (!launch.NoLock && !launch.Autotest && _pointerLock != null)
            {
                // On the web the page already requested the lock inside the browser gesture (HTML gate, or the gesture
                // armed in ConfigureGestures for WP08's gate); a second request now would be refused. Editor: lock now.
                if (WebNative.Available) _pointerLock.ExpectLock();
                else _pointerLock.RequestLock();
            }
            var settings = _settings.Value?.Current;
            if (settings != null && settings.Fullscreen && !launch.Autotest && !WebBridge.IsFullscreen) WebBridge.RequestFullscreen();
            if (_pause != null) _pause.CanPause = true;
            GameEvents.RaiseMissionEvent(BootEvents.MissionStarted);

            if (director == null)
            {
                LSLog.Warn(LogCategory.Platform, "No IMissionDirector registered (placeholder S_Persistent?): nothing to start.", this);
                return;
            }
            if (!string.IsNullOrEmpty(launch.Checkpoint))
            {
                LSLog.Info(LogCategory.Platform, "Starting at checkpoint " + launch.Checkpoint + " (URL)");
                director.ResumeFrom(launch.Checkpoint);
            }
            else
            {
                director.StartMission();
            }
        }

        private bool AllLoaded(SectionId[] sections, out float fraction, out string loading)
        {
            fraction = 1f;
            loading = null;
            if (sections == null || sections.Length == 0) return true;
            var sum = 0f;
            var all = true;
            foreach (var s in sections)
            {
                var p = _streamer.SectionProgress(s);
                sum += p;
                if (_streamer.IsLoaded(s)) continue;
                all = false;
                if (loading == null) loading = Sections.SceneName(s);
            }
            fraction = sum / sections.Length;
            return all;
        }

        private void Report(float progress01, string label)
        {
            Progress = Mathf.Clamp01(progress01);
            if (_gateIsFallback && _fallbackGate != null) _fallbackGate.SetLabel(label);
            _gate?.SetProgress(Progress);
        }

        private void UpdateBootCamera()
        {
            if (_bootCamera == null) return;
            var count = Camera.allCamerasCount;
            if (count > _cameraBuffer.Length) _cameraBuffer = new Camera[count + 8];
            count = Camera.GetAllCameras(_cameraBuffer);
            var other = false;
            for (var i = 0; i < count; i++)
            {
                var c = _cameraBuffer[i];
                if (c != null && c != _bootCamera && c.targetTexture == null) other = true;
                _cameraBuffer[i] = null;
            }
            // allCameras lists enabled cameras only, so the boot camera itself must stay counted while enabled.
            if (_bootCamera.enabled == other) _bootCamera.enabled = !other;
        }

        private void SetPhase(BootPhase phase)
        {
            if (s_phase == phase && _phaseStartedAt > 0f) return;
            s_phase = phase;
            _phaseStartedAt = Time.unscaledTime;
            LSLog.Verbose(LogCategory.Platform, "Boot phase " + phase);
            PhaseChanged?.Invoke(phase);
        }
    }
}
