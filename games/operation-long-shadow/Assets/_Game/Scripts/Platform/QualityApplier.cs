using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Platform
{
    /// <summary>
    /// Applies the graphics options at runtime (TECH_PLAN §7.1, §3.3, D21): render scale (automatic default from the
    /// canvas size unless the player picked one), performance mode (SSAO off, 2 cascades, shadows capped at 50 m, render
    /// scale capped at 0.8; GDD §9.8), motion blur, film-grain amount and brightness (runtime option volumes, see
    /// <see cref="OptionsVolumes"/>). Re-applies when options change and when the canvas is resized. In the editor the
    /// URP asset and renderer features are restored when play mode ends, so nothing leaks into the assets.
    /// The shadow distance is shared with WP10's LightStage (80/60 m per stage): this component only clamps it to 50 m
    /// while performance mode is on. The Options "Fullscreen" switch is applied when it changes (WP08's options menu
    /// calls <c>ISettingsService.Apply</c> from a click, so the browser still sees a user gesture); at boot the click
    /// gate requests fullscreen inside the click instead (TECH_PLAN §5.2.3).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Platform + 10)]
    [DisallowMultipleComponent]
    public sealed class QualityApplier : MonoBehaviour
    {
        [SerializeField, Tooltip("Internal-resolution budget for the automatic render scale (TECH_PLAN D21: 2.1 MP).")]
        private float _maxAutoPixels = RenderScalePolicy.MaxAutoPixels;
        [SerializeField, Tooltip("Shadow cascades in performance mode (TECH_PLAN §3.3).")]
        private int _performanceCascades = 2;
        [SerializeField, Tooltip("Shadow distance cap in performance mode, metres (TECH_PLAN §3.3).")]
        private float _performanceShadowDistance = 50f;
        [SerializeField, Tooltip("One set of option volumes per layer: Default (world cameras) and Booth (Cam_Booth's volume mask, TECH_PLAN §3.6).")]
        private int[] _volumeLayers = { LayerIds.Default, LayerIds.Booth };
        [SerializeField] private float _volumePriority = 1000f;
        [SerializeField, Tooltip("Seconds between canvas-size checks.")]
        private float _resizeCheckInterval = 0.5f;

        private ServiceRef<ISettingsService> _settings;
        private UniversalRenderPipelineAsset _urp;
        private readonly List<OptionsVolumes> _volumes = new List<OptionsVolumes>(2);
        private readonly List<ScriptableRendererFeature> _ssao = new List<ScriptableRendererFeature>(2);
        private readonly List<bool> _ssaoOriginal = new List<bool>(2);
        private float _originalScale = 1f;
        private float _originalShadowDistance = 80f;
        private int _originalCascades = 4;
        private bool _performance;
        private float _distanceBeforePerformance = -1f;
        private Vector2Int _lastCanvas;
        private float _nextResizeCheck;
        private bool _subscribed;
        private bool _fullscreenKnown;
        private bool _lastFullscreen;

        /// <summary>Render scale currently applied.</summary>
        public float AppliedRenderScale { get; private set; } = 1f;
        /// <summary>True while performance mode is applied.</summary>
        public bool PerformanceModeActive => _performance;

        private void Awake()
        {
            Application.targetFrameRate = -1; // the browser paces frames (TECH_PLAN §3.3)
            QualitySettings.vSyncCount = 0;
            _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_urp == null)
            {
                LSLog.Warn(LogCategory.Platform, "QualityApplier: the active render pipeline is not URP; graphics options will not apply.", this);
            }
            else
            {
                _originalScale = _urp.renderScale;
                _originalShadowDistance = _urp.shadowDistance;
                _originalCascades = _urp.shadowCascadeCount;
                foreach (var data in _urp.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                    {
                        if (!(f is ScreenSpaceAmbientOcclusion)) continue;
                        _ssao.Add(f);
                        _ssaoOriginal.Add(f.isActive);
                    }
                }
            }
            foreach (var layer in _volumeLayers)
                if (layer >= 0 && layer < 32) _volumes.Add(new OptionsVolumes(transform, layer, _volumePriority));
        }

        private void OnEnable() => TrySubscribe();

        private void Start()
        {
            TrySubscribe();
            ApplyNow();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEvents.SettingsChanged -= ApplyNow;
        }

        private void OnDestroy()
        {
            RestoreAssets();
            foreach (var v in _volumes) v.Dispose();
            _volumes.Clear();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.SettingsChanged += ApplyNow;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextResizeCheck)
            {
                _nextResizeCheck = Time.unscaledTime + _resizeCheckInterval;
                var c = new Vector2Int(Screen.width, Screen.height);
                if (c != _lastCanvas) ApplyRenderScale();
            }
        }

        private void LateUpdate()
        {
            // LightStage (WP10) writes the shadow distance per stage; keep the performance cap on top of it.
            if (_performance && _urp != null && _urp.shadowDistance > _performanceShadowDistance)
                _urp.shadowDistance = _performanceShadowDistance;
        }

        /// <summary>Re-applies every option now.</summary>
        public void ApplyNow()
        {
            var s = _settings.Value?.Current ?? new GameSettings();
            ApplyPerformance(s.PerformanceMode);
            ApplyRenderScale();
            foreach (var v in _volumes) v.Apply(s.Brightness, s.FilmGrain, s.MotionBlur && !s.PerformanceMode);
            ApplyFullscreen(s.Fullscreen);
        }

        private void ApplyFullscreen(bool wanted)
        {
            if (!_fullscreenKnown)
            {
                // Boot: no user gesture yet; the click gate requests fullscreen inside the click (Bootstrap/template).
                _fullscreenKnown = true;
                _lastFullscreen = wanted;
                return;
            }
            if (wanted == _lastFullscreen) return;
            _lastFullscreen = wanted;
            if (wanted)
            {
                if (!WebBridge.IsFullscreen) WebBridge.RequestFullscreen();
            }
            else if (WebBridge.IsFullscreen)
            {
                WebBridge.ExitFullscreen();
            }
        }

        private void ApplyRenderScale()
        {
            var s = _settings.Value?.Current ?? new GameSettings();
            _lastCanvas = new Vector2Int(Screen.width, Screen.height);
            var scale = RenderScalePolicy.Effective(s, _lastCanvas.x, _lastCanvas.y, _maxAutoPixels);
            if (_urp != null && !Mathf.Approximately(_urp.renderScale, scale))
            {
                _urp.renderScale = scale;
                LSLog.Info(LogCategory.Platform, "Render scale " + scale.ToString("0.00") + " for canvas " + _lastCanvas.x + "x" + _lastCanvas.y +
                                                 " (" + (s.RenderScaleAuto ? "auto" : "player") + (s.PerformanceMode ? ", performance mode" : string.Empty) + ")");
            }
            AppliedRenderScale = scale;
        }

        private void ApplyPerformance(bool on)
        {
            if (on == _performance) return;
            _performance = on;
            if (_urp != null)
            {
                if (on)
                {
                    _distanceBeforePerformance = _urp.shadowDistance;
                    _urp.shadowDistance = Mathf.Min(_urp.shadowDistance, _performanceShadowDistance);
                    _urp.shadowCascadeCount = Mathf.Clamp(_performanceCascades, 1, 4);
                }
                else
                {
                    // Restore the distance only if nobody (LightStage) changed it while we held the cap.
                    if (_distanceBeforePerformance > 0f && Mathf.Approximately(_urp.shadowDistance, Mathf.Min(_distanceBeforePerformance, _performanceShadowDistance)))
                        _urp.shadowDistance = _distanceBeforePerformance;
                    _urp.shadowCascadeCount = Mathf.Clamp(_originalCascades, 1, 4);
                }
            }
            for (var i = 0; i < _ssao.Count; i++)
                if (_ssao[i] != null) _ssao[i].SetActive(!on && _ssaoOriginal[i]);
            LSLog.Info(LogCategory.Platform, "Performance mode " + (on ? "on" : "off"));
        }

        private void RestoreAssets()
        {
            // Runtime edits of the URP asset and renderer data would be saved into the assets in the editor
            // (harmless in players, where the asset is an in-memory copy).
            if (_urp != null)
            {
                _urp.renderScale = _originalScale;
                _urp.shadowDistance = _originalShadowDistance;
                _urp.shadowCascadeCount = Mathf.Clamp(_originalCascades, 1, 4);
            }
            for (var i = 0; i < _ssao.Count; i++)
                if (_ssao[i] != null) _ssao[i].SetActive(_ssaoOriginal[i]);
        }
    }
}
