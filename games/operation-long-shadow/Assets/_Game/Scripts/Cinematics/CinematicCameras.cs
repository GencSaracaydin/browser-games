using System.Collections.Generic;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Decides which camera owns the screen (TECH_PLAN §7.12 "camera hand-off to and from the player rig"): the player rig,
    /// the cinematic world camera (created here, settings copied from Cam_World) or Cam_Booth. Owners push modes (a
    /// sequence, an insert on top of it) and release them. Every frame it composes the active cinematic camera's pose:
    /// shot pose (set by CameraShot clips or inserts) × limited look (GDD §8) × operator sway and impulse shake
    /// (CINEMATIC_DIRECTION §1.1), and drives a lens volume (Bokeh DoF from the shot's focal length, f-stop and focus;
    /// camera motion blur for whip pans). It also points WP06's AudioListener at the active camera.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics + 50)]
    [DisallowMultipleComponent]
    public sealed class CinematicCameras : MonoBehaviour
    {
        /// <summary>Priority of the cinematic lens volumes (above the look volumes, below the FX volumes).</summary>
        public const float LensPriority = 80f;

        private struct ModeRequest
        {
            public object Owner;
            public CameraMode Mode;
        }

        private readonly List<ModeRequest> _stack = new List<ModeRequest>(4);
        private readonly LimitedLook _look = new LimitedLook();

        private Camera _world;
        private bool _worldConfigured;
        private Volume _lensWorld;
        private Volume _lensBooth;
        private DepthOfField _dofWorld;
        private DepthOfField _dofBooth;
        private MotionBlur _blurWorld;
        private MotionBlur _blurBooth;

        private CameraMode _applied = CameraMode.Player;
        private bool _appliedOnce;
        private BoothCameraRig _boothRig;

        private Vector3 _worldPos;
        private Quaternion _worldRot = Quaternion.identity;
        private float _worldFov = 50f;
        private Vector3 _boothPos = BoothLayout.Pov;
        private Quaternion _boothRot = Quaternion.identity;
        private float _boothFov = BoothLayout.PovFov;

        private bool _dof;
        private float _focus = 10f;
        private float _aperture = 5.6f;
        private float _focal = 35f;
        private float _motionBlur;
        private float _swayDeg = HandheldNoise.BreathingDeg;
        private float _swayHz = HandheldNoise.BreathingHz;
        private float _impulseDeg;
        private float _impulseAge = -1f;
        private float _roll;
        private float _clock;

        private ServiceRef<IPlayerRig> _rig;
        private ServiceRef<IAudioService> _audio;
        private ServiceRef<IInputReader> _input;
        private ServiceRef<ISettingsService> _settings;

        /// <summary>Mode currently on screen.</summary>
        public CameraMode Active => _stack.Count > 0 ? _stack[_stack.Count - 1].Mode : CameraMode.Player;
        /// <summary>The cinematic world camera (created on demand).</summary>
        public Camera WorldCamera => EnsureWorldCamera();
        /// <summary>The limited look of the POV shots (enabled and limited by LookControl clips and inserts).</summary>
        public LimitedLook Look => _look;
        /// <summary>True when S0_Booth and its camera are loaded.</summary>
        public bool BoothAvailable => ResolveBooth() != null;
        /// <summary>Transform of the camera on screen (null in player mode).</summary>
        public Transform ActiveCameraTransform
        {
            get
            {
                switch (_applied)
                {
                    case CameraMode.World: return _world != null ? _world.transform : null;
                    case CameraMode.Booth: return _boothRig != null ? _boothRig.transform : null;
                    default: return null;
                }
            }
        }

        private void Awake()
        {
            CinematicRuntime.Set(this);
            var defaultLayer = LayerIds.Default;
            _lensWorld = VolumeFactory.Create(transform, "VP_CinematicLens_World", defaultLayer, LensPriority,
                CinematicLibrary.Lookup<VolumeProfile>(CinematicAssetIds.ProfileCinematic));
            _lensBooth = VolumeFactory.Create(transform, "VP_CinematicLens_Booth", LayerIds.Booth, LensPriority, null);
            _dofWorld = SetupDof(_lensWorld);
            _dofBooth = SetupDof(_lensBooth);
            _blurWorld = SetupBlur(_lensWorld);
            _blurBooth = SetupBlur(_lensBooth);
        }

        private void OnDestroy()
        {
            CinematicRuntime.Clear(this);
            VolumeFactory.Destroy(_lensWorld);
            VolumeFactory.Destroy(_lensBooth);
            if (_appliedOnce && _applied != CameraMode.Player)
            {
                _rig.Value?.SetViewActive(true);
                _audio.Value?.SetListenerOverride(null);
            }
        }

        // ------------------------------------------------------------------ ownership

        /// <summary>Requests a camera mode for an owner (the latest request wins until released).</summary>
        public void Push(object owner, CameraMode mode)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_stack[i].Owner, owner)) _stack.RemoveAt(i);
            _stack.Add(new ModeRequest { Owner = owner, Mode = mode });
            ApplyMode();
        }

        /// <summary>Releases an owner's request.</summary>
        public void Release(object owner)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_stack[i].Owner, owner)) _stack.RemoveAt(i);
            ApplyMode();
        }

        /// <summary>True if the owner currently holds a request.</summary>
        public bool Holds(object owner)
        {
            for (var i = 0; i < _stack.Count; i++)
                if (ReferenceEquals(_stack[i].Owner, owner)) return true;
            return false;
        }

        // ------------------------------------------------------------------ pose and lens

        /// <summary>Base pose of the cinematic world camera (world space) and its vertical FOV.</summary>
        public void SetWorldPose(Vector3 position, Quaternion rotation, float verticalFov)
        {
            _worldPos = position;
            _worldRot = rotation;
            _worldFov = verticalFov;
        }

        /// <summary>Base pose of Cam_Booth in the booth frame and its vertical FOV.</summary>
        public void SetBoothPose(Vector3 localPosition, Quaternion localRotation, float verticalFov)
        {
            _boothPos = localPosition;
            _boothRot = localRotation;
            _boothFov = verticalFov;
        }

        /// <summary>Booth pose from a preset (inserts).</summary>
        public void SetBoothPreset(BoothCameraPreset preset)
        {
            BoothLayout.Preset(preset, out var p, out var r, out var fov);
            SetBoothPose(p, r, fov);
        }

        /// <summary>Lens: Bokeh DoF on/off, focus distance (m), f-stop and focal length (mm).</summary>
        public void SetLens(bool depthOfField, float focusDistance, float aperture, float focalMm)
        {
            _dof = depthOfField;
            _focus = Mathf.Max(0.1f, focusDistance);
            _aperture = Mathf.Clamp(aperture, 1f, 32f);
            _focal = Mathf.Clamp(focalMm, 1f, 300f);
        }

        /// <summary>Camera motion blur 0..1 (whip pans; CINEMATIC_DIRECTION §1.1: 0.3-0.5).</summary>
        public void SetMotionBlur(float intensity) => _motionBlur = Mathf.Clamp01(intensity);

        /// <summary>Operator sway amplitude (degrees) and frequency (Hz).</summary>
        public void SetHandheld(float degrees, float hz)
        {
            _swayDeg = Mathf.Max(0f, degrees);
            _swayHz = Mathf.Max(0.01f, hz);
        }

        /// <summary>Kicks an impulse shake (degrees, decaying).</summary>
        public void Impulse(float degrees)
        {
            _impulseDeg = degrees;
            _impulseAge = 0f;
        }

        /// <summary>Dutch roll in degrees (the climax lurch).</summary>
        public void SetRoll(float degrees) => _roll = degrees;

        /// <summary>Resets look, sway, roll and lens to neutral (between sequences).</summary>
        public void ResetShotState()
        {
            _look.Reset();
            _look.Enabled = false;
            _roll = 0f;
            _impulseAge = -1f;
            _motionBlur = 0f;
            _dof = false;
            _swayDeg = HandheldNoise.BreathingDeg;
            _swayHz = HandheldNoise.BreathingHz;
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            _clock += dt;
            Shader.SetGlobalFloat(ShaderIds.UnscaledTime, Time.unscaledTime);
            ApplyMode();

            _look.Tick(dt, LookInputDegrees());
            if (_impulseAge >= 0f) _impulseAge += dt;

            var sway = Quaternion.Euler(HandheldNoise.Sway(_clock, _swayDeg, _swayHz, 3.7f));
            var shake = _impulseAge >= 0f ? Quaternion.Euler(HandheldNoise.Impulse(_impulseAge, _impulseDeg)) : Quaternion.identity;
            var roll = Mathf.Abs(_roll) > 0.001f ? Quaternion.Euler(0f, 0f, _roll) : Quaternion.identity;
            var offset = _look.Rotation * sway * shake * roll;

            switch (_applied)
            {
                case CameraMode.World:
                    if (_world != null)
                    {
                        _world.transform.SetPositionAndRotation(_worldPos, _worldRot * offset);
                        _world.fieldOfView = Mathf.Clamp(_worldFov, 1f, 120f);
                    }
                    break;
                case CameraMode.Booth:
                    var booth = ResolveBooth();
                    if (booth != null)
                        booth.SetLocalPose(BoothRoot.Current != null ? BoothRoot.Current.transform : null, _boothPos, _boothRot * offset, _boothFov);
                    break;
            }
            ApplyLens();
        }

        private Vector2 LookInputDegrees()
        {
            if (!_look.Enabled || _applied == CameraMode.Player) return Vector2.zero;
            var input = _input.Value;
            if (input == null) return Vector2.zero;
            var s = _settings.Value?.Current;
            var sens = s != null ? s.MouseSensitivity : 1f;
            var invert = s != null && s.InvertY;
            var k = sens * GameSettings.DegreesPerPixelPerSens;
            var d = input.Look;
            return new Vector2(d.x * k, (invert ? -d.y : d.y) * k);
        }

        private void ApplyLens()
        {
            var perf = _settings.Value?.Current?.PerformanceMode ?? false;
            var worldOn = _applied == CameraMode.World;
            var boothOn = _applied == CameraMode.Booth;
            Drive(_lensWorld, _dofWorld, _blurWorld, worldOn, perf);
            Drive(_lensBooth, _dofBooth, _blurBooth, boothOn, perf);
        }

        private void Drive(Volume v, DepthOfField dof, MotionBlur blur, bool on, bool performanceMode)
        {
            if (v == null) return;
            v.weight = on ? 1f : 0f;
            if (!on) return;
            if (dof != null)
            {
                dof.mode.value = _dof && !performanceMode ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Off;
                dof.focusDistance.value = _focus;
                dof.aperture.value = _aperture;
                dof.focalLength.value = _focal;
            }
            if (blur != null) blur.intensity.value = performanceMode ? 0f : _motionBlur;
        }

        private void ApplyMode()
        {
            var mode = Active;
            if (mode == CameraMode.Booth && ResolveBooth() == null)
            {
                LSLog.WarnOnce("cine-nobooth", LogCategory.Cinematics, "S0_Booth is not loaded; booth shots fall back to the cinematic world camera (black).", this);
                mode = CameraMode.World;
            }
            if (_appliedOnce && mode == _applied) return;
            _appliedOnce = true;
            _applied = mode;
            var rig = _rig.Value;
            var booth = ResolveBooth();
            switch (mode)
            {
                case CameraMode.Player:
                    if (_world != null) _world.enabled = false;
                    if (booth != null) booth.SetActive(false);
                    rig?.SetViewActive(true);
                    _audio.Value?.SetListenerOverride(null);
                    break;
                case CameraMode.World:
                    var cam = EnsureWorldCamera();
                    rig?.SetViewActive(false);
                    if (booth != null) booth.SetActive(false);
                    cam.enabled = true;
                    _audio.Value?.SetListenerOverride(cam.transform);
                    break;
                case CameraMode.Booth:
                    rig?.SetViewActive(false);
                    if (_world != null) _world.enabled = false;
                    booth.SetActive(true);
                    _audio.Value?.SetListenerOverride(booth.transform);
                    break;
            }
        }

        private BoothCameraRig ResolveBooth()
        {
            if (_boothRig != null) return _boothRig;
            _boothRig = BoothRoot.Current != null ? BoothRoot.Current.Camera : null;
            if (_boothRig == null) _boothRig = CinematicBindings.Resolve<BoothCameraRig>(BindingIds.BoothCamera);
            return _boothRig;
        }

        private Camera EnsureWorldCamera()
        {
            if (_world == null)
            {
                var go = new GameObject("Cam_Cinematic");
                go.transform.SetParent(transform, false);
                _world = go.AddComponent<Camera>();
                _world.enabled = false;
                _world.GetUniversalAdditionalCameraData();
                _worldConfigured = false;
            }
            if (!_worldConfigured)
            {
                var player = _rig.Value?.WorldCamera;
                if (player != null)
                {
                    _world.CopyFrom(player);
                    _world.targetTexture = null;
                    _worldConfigured = true;
                }
                else
                {
                    _world.nearClipPlane = 0.05f;
                    _world.farClipPlane = 3000f;
                    _world.clearFlags = CameraClearFlags.Skybox;
                }
                _world.cullingMask = FeedCameras.WorldCullingMask;
                _world.depth = 10f;
                var data = _world.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.SetRenderer(0);
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderShadows = true;
                data.volumeLayerMask = 1 << LayerIds.Default;
                data.stopNaN = false;
                data.dithering = true;
            }
            return _world;
        }

        private static DepthOfField SetupDof(Volume v)
        {
            var dof = VolumeFactory.Get<DepthOfField>(v);
            if (dof == null) return null;
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Off;
            dof.focusDistance.overrideState = true;
            dof.aperture.overrideState = true;
            dof.focalLength.overrideState = true;
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(0.8f);
            return dof;
        }

        private static MotionBlur SetupBlur(Volume v)
        {
            var mb = VolumeFactory.Get<MotionBlur>(v);
            if (mb == null) return null;
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.quality.Override(MotionBlurQuality.Low);
            mb.intensity.overrideState = true;
            mb.intensity.value = 0f;
            return mb;
        }
    }
}
