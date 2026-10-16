using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Sky;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Stage
{
    /// <summary>
    /// The one-parameter time of day (GDD G6, §9.1–9.2a; TECH_PLAN §7.14), implementing <see cref="ILightStage"/>. Lives
    /// in S_Persistent on <c>PF_LookRig</c>. From <see cref="S"/> it drives: the realtime <c>SUN</c> (elevation at
    /// azimuth 258°, intensity, colour temperature, shadow strength and bias), the shadowless <c>SKY_KEY</c> from s 2.0,
    /// the URP shadow distance, the <c>Sky/PanoBlend</c> skybox, the sun disc and lens flare, fog colour and density,
    /// the ambient SH (four captured stage probes), the default reflection intensity, the four stage volume weights,
    /// <c>PracticalsStrike</c> at s 1.97, AI sight/hearing, the HUD colour and the story clock. Shader globals
    /// <c>_LS_SunColor</c>, <c>_LS_SunDirection</c>, <c>_LS_StageParams</c> are written for Look's and Level's shaders.
    /// It is also the <c>ISaveable</c> "LightStage" of the checkpoint snapshot (TECH_PLAN §5.5).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.LightStage)]
    [DisallowMultipleComponent]
    public sealed class LightStageController : MonoBehaviour, ILightStage, ISaveable
    {
        /// <summary>Checkpoint save key.</summary>
        public const string SaveKeyName = "LightStage";
        /// <summary>Shadow distance cap while the Performance Mode option is on (GDD §9.8; matches WP01 QualityApplier).</summary>
        public const float PerformanceShadowDistance = 50f;

        [Header("Lights (S_Persistent, TECH_PLAN §3.8)")]
        [SerializeField, Tooltip("SUN: directional, Realtime, soft shadows, rendering layer World.")]
        private Light _sun;
        [SerializeField, Tooltip("SKY_KEY: directional, Realtime, no shadows, az 258°, el 12°; used from AFTERGLOW on.")]
        private Light _skyKey;
        [SerializeField, Tooltip("Lens flare on the sun (LensFlareComponentSRP).")]
        private LensFlareComponentSRP _sunFlare;
        [SerializeField] private SunDisc _sunDisc;

        [Header("Sky")]
        [SerializeField, Tooltip("M_Sky_PanoBlend. A runtime copy becomes RenderSettings.skybox.")]
        private Material _skyMaterial;

        [Header("Volumes (global, layer Default; priorities Golden < Sunset < Afterglow < Blue Hour)")]
        [SerializeField] private Volume _golden;
        [SerializeField] private Volume _sunset;
        [SerializeField] private Volume _afterglow;
        [SerializeField] private Volume _blueHour;

        [Header("Data")]
        [SerializeField] private LightStageTuning _tuning;
        [SerializeField] private AmbientSHSet _ambient;

        [Header("Behaviour")]
        [SerializeField, Range(0f, 4f), Tooltip("Stage value at start (Beat 1: 0).")]
        private float _initialS;
        [SerializeField, Tooltip("Write RenderSettings fog (exp², colour, density).")]
        private bool _driveFog = true;
        [SerializeField, Tooltip("Write RenderSettings.ambientProbe and reflection intensity.")]
        private bool _driveAmbient = true;
        [SerializeField, Tooltip("Write UniversalRenderPipelineAsset.shadowDistance per stage (restored on destroy).")]
        private bool _driveShadowDistance = true;
        [SerializeField, Tooltip("Assign the sky material as RenderSettings.skybox.")]
        private bool _driveSkybox = true;

        private readonly HashSet<object> _overrides = new HashSet<object>();
        private readonly Volume[] _volumes = new Volume[4];
        private readonly SphericalHarmonicsL2[] _fallbackAmbient = new SphericalHarmonicsL2[4];

        private ServiceRef<ICheckpointService> _checkpoints;
        private ServiceRef<ISettingsService> _settings;
        private LightStageSample _sample;
        private Material _skyInstance;
        private UniversalRenderPipelineAsset _urp;
        private float _originalShadowDistance = -1f;
        private float _lastShadowDistance = -1f;
        private bool _registeredCheckpoint;
        private bool _registeredService;
        private bool _initialised;
        private float _s;
        private float _target;
        private float _rate;
        private float _lastRaisedS = float.NaN;
        private StageId _lastStage;
        private bool _stageRaised;
        private float _sightRange = 70f;
        private float _sightStepPerSecond;
        private Color _sunColorLinear;

        private static LightStageController s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active = null;

        /// <summary>The live controller (null when none). Look components in this assembly use it for extras beyond <see cref="ILightStage"/>.</summary>
        public static LightStageController Active => s_active;

        // ---------------------------------------------------------------- ILightStage
        /// <inheritdoc />
        public float S => _s;
        /// <inheritdoc />
        public StageId Stage => _sample.Stage;
        /// <inheritdoc />
        public bool IsOverridden => _overrides.Count > 0;
        /// <inheritdoc />
        public float AISightRange => _sightRange;
        /// <inheritdoc />
        public float HearingScale => LightStageTable.HearingScaleOf(_sample.Stage);
        /// <inheritdoc />
        public bool AfterSunset => _sample.AfterSunset;
        /// <inheritdoc />
        public Color HudColor => _sample.HudColor;
        /// <inheritdoc />
        public float StoryClockMinutes => _sample.ClockMinutes;

        // ---------------------------------------------------------------- extras (same assembly, tools)
        /// <summary>Target the smoothing moves toward.</summary>
        public float Target => _target;
        /// <summary>The sample applied last.</summary>
        public LightStageSample CurrentSample => _sample;
        /// <summary>Post exposure of the blended stage volumes (EV); interior eye-adaptation volumes add to it.</summary>
        public float StagePostExposure => _sample.StagePostExposure;
        /// <summary>The SUN light.</summary>
        public Light SunLight => _sun;
        /// <summary>The SKY_KEY light.</summary>
        public Light SkyKeyLight => _skyKey;
        /// <summary>Current key light (SUN until s 2.0, then SKY_KEY).</summary>
        public Light KeyLight => _sample.SunEnabled ? _sun : _skyKey;
        /// <summary>Linear colour x intensity of the SUN (black after sunset).</summary>
        public Color SunColorLinear => _sunColorLinear;
        /// <summary>Tuning in use.</summary>
        public LightStageTuning Tuning => _tuning;
        /// <summary>Raised after every applied change of s (any size), with the new sample.</summary>
        public event Action<LightStageSample> Applied;

        // ---------------------------------------------------------------- ISaveable
        /// <inheritdoc />
        public string SaveKey => SaveKeyName;

        /// <inheritdoc />
        public void Capture(SaveWriter w)
        {
            w.Write("s", _s);
            w.Write("target", _target);
        }

        /// <inheritdoc />
        public void Restore(SaveReader r)
        {
            _overrides.Clear();
            SetImmediate(r.ReadFloat("s", _s));
        }

        // ---------------------------------------------------------------- lifecycle
        private void Awake()
        {
            if (_tuning == null) _tuning = LightStageTuning.CreateDefault();
            _volumes[0] = _golden;
            _volumes[1] = _sunset;
            _volumes[2] = _afterglow;
            _volumes[3] = _blueHour;
            BuildFallbackAmbient();
            PrepareLights();

            _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_urp != null) _originalShadowDistance = _urp.shadowDistance;

            if (_skyMaterial != null && _driveSkybox)
            {
                _skyInstance = new Material(_skyMaterial) { name = _skyMaterial.name + " (LightStage)" };
                RenderSettings.skybox = _skyInstance;
            }
            else if (_skyMaterial == null)
                LSLog.MissingAsset(LogCategory.Look, "M_Sky_PanoBlend", "Assets/_Game/Art/Materials/Sky/M_Sky_PanoBlend.mat", this);

            if (_driveFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }

            if (s_active != null && s_active != this)
                LSLog.Warn(LogCategory.Look, "Two LightStageControllers are alive; the newest wins.", this);
            s_active = this;
            GameServices.Register<ILightStage>(this);
            _registeredService = true;

            _s = _target = LightStageTable.Clamp(_initialS);
            _sample = LightStageTable.Sample(_s);
            _sightRange = _sample.AISightTarget;
            _lastStage = _sample.Stage;
        }

        private void OnEnable()
        {
            GameEvents.SettingsChanged += OnSettingsChanged;
        }

        private void Start()
        {
            ApplyNow(raiseEvents: true, immediate: true);
            _initialised = true;
            TryRegisterCheckpoint();
        }

        private void OnDisable()
        {
            GameEvents.SettingsChanged -= OnSettingsChanged;
        }

        private void OnDestroy()
        {
            if (_registeredCheckpoint) _checkpoints.Value?.Unregister(this);
            _registeredCheckpoint = false;
            if (_registeredService) GameServices.Unregister<ILightStage>(this);
            _registeredService = false;
            if (s_active == this) s_active = null;
            if (_urp != null && _originalShadowDistance > 0f) _urp.shadowDistance = _originalShadowDistance;
            if (_skyInstance != null)
            {
                if (RenderSettings.skybox == _skyInstance) RenderSettings.skybox = _skyMaterial;
                Destroy(_skyInstance);
            }
        }

        private void Update()
        {
            if (!_registeredCheckpoint) TryRegisterCheckpoint();

            var dt = Time.deltaTime;
            if (!Mathf.Approximately(_s, _target))
            {
                var next = _rate <= 0f ? _target : MathX.Damp(_s, _target, _rate, dt);
                if (Mathf.Abs(next - _target) < 1e-4f) next = _target;
                Move(next, immediate: false);
            }

            // AI sight steps per stage over the smoothing time (GDD §9.2a "AI sight range steps per stage with 2 s smoothing").
            var goal = _sample.AISightTarget;
            if (!Mathf.Approximately(_sightRange, goal))
            {
                var step = _sightStepPerSecond > 0f ? _sightStepPerSecond * dt : float.MaxValue;
                _sightRange = Mathf.MoveTowards(_sightRange, goal, step);
            }
        }

        // ---------------------------------------------------------------- control
        /// <summary>
        /// Moves toward <paramref name="s"/>. <paramref name="smoothing"/> is the settle time in seconds (≈95 % of a jump
        /// within it; GDD §9.1 "a jump is smoothed over 2 s"); 0 jumps this frame but still raises <c>PracticalsStrike</c>
        /// when 1.97 is crossed upward (scripted MID-A drive).
        /// </summary>
        public void SetTarget(float s, float smoothing = 2f)
        {
            _target = LightStageTable.Clamp(s);
            _rate = smoothing > 1e-3f ? 3f / smoothing : 0f;
            if (_rate <= 0f) Move(_target, immediate: false);
        }

        /// <summary>Jumps to <paramref name="s"/> now (checkpoints, debug): lamps snap, no strike ripple.</summary>
        public void SetImmediate(float s)
        {
            _target = LightStageTable.Clamp(s);
            _rate = 0f;
            Move(_target, immediate: true);
            _sightRange = _sample.AISightTarget;
        }

        /// <inheritdoc />
        public void Override(object owner, bool on)
        {
            if (owner == null) return;
            if (on) _overrides.Add(owner);
            else _overrides.Remove(owner);
        }

        /// <summary>
        /// Editor preview and LookCapture: applies the look of <paramref name="s"/> without raising game events or touching
        /// the checkpoint state. Works in edit mode (call <see cref="EditorReleasePreview"/> to restore the URP asset).
        /// </summary>
        public void PreviewApply(float s)
        {
            if (_tuning == null) _tuning = LightStageTuning.CreateDefault();
            if (_volumes[0] == null)
            {
                _volumes[0] = _golden;
                _volumes[1] = _sunset;
                _volumes[2] = _afterglow;
                _volumes[3] = _blueHour;
            }
            if (_urp == null)
            {
                _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (_urp != null && _originalShadowDistance < 0f) _originalShadowDistance = _urp.shadowDistance;
            }
            BuildFallbackAmbient();
            _s = _target = LightStageTable.Clamp(s);
            _sample = LightStageTable.Sample(_s);
            _sightRange = _sample.AISightTarget;
            ApplySample(_sample, Application.isPlaying ? _skyInstance : _skyMaterial);
        }

        /// <summary>Restores the URP shadow distance changed by <see cref="PreviewApply"/> (edit mode).</summary>
        public void EditorReleasePreview()
        {
            if (_urp != null && _originalShadowDistance > 0f) _urp.shadowDistance = _originalShadowDistance;
            _lastShadowDistance = -1f;
        }

        // ---------------------------------------------------------------- internals
        private void Move(float s, bool immediate)
        {
            var prev = _s;
            _s = LightStageTable.Clamp(s);
            if (!immediate && prev < LightStageTable.StrikeS && _s >= LightStageTable.StrikeS)
            {
                LSLog.Info(LogCategory.Look, "PracticalsStrike at s " + _s.ToString("0.000") + " (" +
                                             LightStageTable.FormatClock(LightStageTable.ClockAt(_s)) + ")", this);
                GameEvents.RaisePracticalsStrike();
            }
            ApplyNow(raiseEvents: true, immediate: immediate);
        }

        private void ApplyNow(bool raiseEvents, bool immediate)
        {
            _sample = LightStageTable.Sample(_s);
            var stageSpan = Mathf.Abs(_sightRange - _sample.AISightTarget);
            var seconds = _tuning != null ? _tuning.SightSmoothingSeconds : 2f;
            _sightStepPerSecond = seconds > 0f ? Mathf.Max(stageSpan / seconds, 1f) : 0f;

            ApplySample(_sample, _skyInstance != null ? _skyInstance : (_driveSkybox ? null : _skyMaterial));
            if (!raiseEvents) return;

            if (!_stageRaised || _sample.Stage != _lastStage)
            {
                _lastStage = _sample.Stage;
                _stageRaised = true;
                GameEvents.RaiseStageChanged(_sample.Stage);
            }
            // LightStageChanged in ≥ 0.01 steps (Core contract): once per crossed hundredth (so 1.97 and 2.00 are always
            // reported), and on every immediate jump.
            if (immediate || float.IsNaN(_lastRaisedS) || Cell(_s) != Cell(_lastRaisedS))
            {
                if (float.IsNaN(_lastRaisedS) || !Mathf.Approximately(_lastRaisedS, _s))
                {
                    _lastRaisedS = _s;
                    GameEvents.RaiseLightStageChanged(_s);
                }
            }
            Applied?.Invoke(_sample);
        }

        private static int Cell(float s) => Mathf.FloorToInt(s * 100f + 1e-3f);

        private void ApplySample(in LightStageSample s, Material sky)
        {
            var t = _tuning != null ? _tuning : (_tuning = LightStageTuning.CreateDefault());

            // Sun.
            _sunColorLinear = Color.black;
            if (_sun != null)
            {
                var on = s.SunEnabled;
                if (_sun.enabled != on) _sun.enabled = on;
                if (on)
                {
                    _sun.transform.rotation = LightStageTable.LightRotation(s.SunElevation);
                    var c = Mathf.CorrelatedColorTemperatureToRGB(s.SunKelvin);
                    _sun.color = c;
                    _sun.intensity = s.SunIntensity * t.SunIntensityScale;
                    _sun.shadowStrength = Mathf.Clamp01(s.ShadowStrength);
                    _sun.shadows = s.ShadowStrength > 0.001f ? LightShadows.Soft : LightShadows.None;
                    _sun.shadowBias = s.ShadowDepthBias * t.ShadowDepthBiasScale;
                    _sun.shadowNormalBias = s.ShadowNormalBias * t.ShadowNormalBiasScale;
                    _sunColorLinear = c.linear * _sun.intensity;
                }
            }

            // Sky key.
            if (_skyKey != null)
            {
                var on = s.SkyKeyEnabled;
                if (_skyKey.enabled != on) _skyKey.enabled = on;
                if (on)
                {
                    _skyKey.transform.rotation = LightStageTable.LightRotation(LightStageTable.SkyKeyElevation);
                    _skyKey.color = Mathf.CorrelatedColorTemperatureToRGB(s.SkyKeyKelvin);
                    _skyKey.intensity = s.SkyKeyIntensity * t.SkyKeyIntensityScale;
                    _skyKey.shadows = LightShadows.None;
                }
            }
            var key = s.SunEnabled ? _sun : _skyKey;
            if (key != null) RenderSettings.sun = key;

            // Shadow distance (URP asset; Performance Mode caps it, GDD §9.8).
            if (_driveShadowDistance && _urp != null)
            {
                var d = s.ShadowDistance;
                var perf = _settings.Value?.Current?.PerformanceMode ?? false;
                if (perf) d = Mathf.Min(d, PerformanceShadowDistance);
                if (Mathf.Abs(d - _lastShadowDistance) > 0.25f || !Mathf.Approximately(_urp.shadowDistance, d))
                {
                    _urp.shadowDistance = d;
                    _lastShadowDistance = d;
                }
            }

            // Sky.
            if (sky != null) PanoBlendSky.Apply(sky, s, t);

            // Sun disc and flare.
            var discColor = Color.black;
            if (s.SunEnabled && s.SunDiscVisibility > 0f)
            {
                var c = Mathf.CorrelatedColorTemperatureToRGB(s.SunKelvin).linear;
                discColor = c * (t.SunDiscLuminance * s.SunIntensity * t.SunIntensityScale * s.SunDiscVisibility);
            }
            var toSun = LightStageTable.SunDirection(s.SunElevation);
            if (_sunDisc != null) _sunDisc.SetState(toSun, discColor, t.SunDiscDiameter, t.SunDiscDistance);
            if (_sunFlare != null)
            {
                var fi = s.SunEnabled ? t.FlareIntensity * s.SunIntensity * s.SunDiscVisibility : 0f;
                _sunFlare.intensity = fi;
                if (_sunFlare.enabled != fi > 1e-4f) _sunFlare.enabled = fi > 1e-4f;
            }

            // Fog.
            if (_driveFog)
            {
                RenderSettings.fogColor = s.FogColor;
                RenderSettings.fogDensity = s.FogDensity * t.FogDensityScale;
            }

            // Ambient.
            if (_driveAmbient)
            {
                RenderSettings.ambientProbe = BlendAmbient(s) * t.AmbientScale;
                RenderSettings.reflectionIntensity = s.ReflectionIntensity;
            }

            // Stage volumes.
            for (var i = 0; i < 4; i++)
            {
                var v = _volumes[i];
                if (v == null) continue;
                var w = s.Blend.VolumeWeightOf((StageId)i);
                v.weight = w;
                var en = w > 0f;
                if (v.enabled != en) v.enabled = en;
            }

            // Shader globals.
            var keyColor = _sunColorLinear;
            if (!s.SunEnabled && _skyKey != null && _skyKey.enabled) keyColor = _skyKey.color.linear * _skyKey.intensity;
            Shader.SetGlobalVector(LookShaderIds.SunColor, new Vector4(keyColor.r, keyColor.g, keyColor.b, s.SunDiscVisibility));
            var keyDir = s.SunEnabled ? toSun : LightStageTable.SunDirection(LightStageTable.SkyKeyElevation);
            Shader.SetGlobalVector(LookShaderIds.SunDirection, new Vector4(keyDir.x, keyDir.y, keyDir.z, s.SunEnabled ? 1f : 0f));
            Shader.SetGlobalVector(LookShaderIds.StageParams, new Vector4(s.S, s.StagePostExposure, s.AfterSunset ? 1f : 0f, s.ClockMinutes));
        }

        private SphericalHarmonicsL2 BlendAmbient(in LightStageSample s)
        {
            var total = new SphericalHarmonicsL2();
            total.Clear();
            for (var i = 0; i < 4; i++)
            {
                var stage = (StageId)i;
                var w = s.Blend.WeightOf(stage);
                if (w <= 0f) continue;
                // Each probe was captured from its stage's own sky texture (A for Golden/Sunset, B after sunset) at
                // RepresentativeS; rescale it by that texture's live exposure so ambient follows the visible sky.
                var current = StageSkyExposure(s, stage);
                if (_ambient != null && _ambient.TryGet(stage, out var sh, out var capture))
                    total += sh * (w * (capture > 1e-6f ? current / capture : 1f));
                else total += _fallbackAmbient[i] * (w * current / Mathf.Max(1e-4f, StageSkyExposure(LightStageTable.Sample(RepresentativeS(stage)), stage)));
            }
            return total;
        }

        /// <summary>Linear exposure of the sky texture a stage's ambient probe comes from (A before sunset, B after).</summary>
        public float StageSkyExposure(in LightStageSample s, StageId stage) =>
            PanoBlendSky.LinearExposure(s, _tuning, stage >= StageId.Afterglow);

        /// <summary>The stage value each ambient probe stands for (capture point of AmbientSHCapture).</summary>
        public static float RepresentativeS(StageId stage)
        {
            switch (stage)
            {
                case StageId.Golden: return 0.5f;
                case StageId.Sunset: return 1.5f;
                case StageId.Afterglow: return 2.4f;
                default: return 3.45f;
            }
        }

        private void BuildFallbackAmbient()
        {
            // Sky/horizon/ground gradient per stage from the fog colours (used until AmbientSHCapture has run), scaled to
            // the stage's sky exposure so the ambient keeps the visible sky's brightness.
            for (var i = 0; i < 4; i++)
            {
                var rs = LightStageTable.Sample(RepresentativeS((StageId)i));
                var exposure = StageSkyExposure(rs, (StageId)i) * 2.5f;
                var horizon = rs.FogColor.linear * exposure;
                var sky = Color.Lerp(horizon, rs.SkyTint.linear * 0.6f * exposure, 0.5f);
                var ground = horizon * 0.25f;
                _fallbackAmbient[i] = AmbientSHMath.Gradient(sky, horizon, ground);
            }
        }

        private void PrepareLights()
        {
            if (_sun != null)
            {
                _sun.type = LightType.Directional;
                var data = _sun.GetUniversalAdditionalLightData();
                if (data != null) data.usePipelineSettings = false; // per-stage bias (TECH_PLAN §3.4)
            }
            if (_skyKey != null)
            {
                _skyKey.type = LightType.Directional;
                _skyKey.shadows = LightShadows.None;
                var data = _skyKey.GetUniversalAdditionalLightData();
                if (data != null && _tuning != null) data.renderingLayers = _tuning.SkyKeyRenderingLayers;
            }
            if (_sun == null) LSLog.Warn(LogCategory.Look, "LightStageController has no SUN light; run LongShadow/Look/Build Look Rig.", this);
        }

        private void TryRegisterCheckpoint()
        {
            var cp = _checkpoints.Value;
            if (cp == null) return;
            cp.Register(this);
            _registeredCheckpoint = true;
        }

        private void OnSettingsChanged()
        {
            _lastShadowDistance = -1f;
            if (_initialised) ApplySample(_sample, _skyInstance);
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: wires the rig.</summary>
        public void EditorWire(Light sun, Light skyKey, LensFlareComponentSRP flare, SunDisc disc, Material sky,
            Volume golden, Volume sunset, Volume afterglow, Volume blueHour, LightStageTuning tuning, AmbientSHSet ambient)
        {
            _sun = sun;
            _skyKey = skyKey;
            _sunFlare = flare;
            _sunDisc = disc;
            _skyMaterial = sky;
            _golden = golden;
            _sunset = sunset;
            _afterglow = afterglow;
            _blueHour = blueHour;
            _tuning = tuning;
            _ambient = ambient;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor: the sky material asset.</summary>
        public Material EditorSkyMaterial => _skyMaterial;
#endif
    }
}
