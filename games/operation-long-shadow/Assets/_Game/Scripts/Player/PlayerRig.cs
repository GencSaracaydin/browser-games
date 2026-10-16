using System;
using LongShadow.Core;
using LongShadow.Player.Body;
using LongShadow.Player.Bounds;
using LongShadow.Player.Footsteps;
using LongShadow.Player.Interaction;
using LongShadow.Player.Movement;
using LongShadow.Player.View;
using LongShadow.Player.Vitals;
using UnityEngine;

namespace LongShadow.Player
{
    /// <summary>
    /// Composition root of the player (TECH_PLAN §7.3): implements <see cref="IPlayerRig"/> (registered in
    /// <see cref="GameServices"/>), <see cref="ITargetable"/> and <see cref="ISuppressible"/> for AI and combat (damage is
    /// forwarded to <see cref="PlayerHealth"/>), and <see cref="ISaveable"/> ("Player.Rig": checkpoint restores clear
    /// transient player state). It owns the frame order so every consumer sees a consistent state:
    /// <list type="number">
    /// <item>Update (<see cref="ExecOrder.Player"/>, after Input, before Weapons): sample <see cref="IInputReader"/> →
    /// mouse look (<see cref="LookMath"/>: CSS px × sens × 0.03, ADS × FOV ratio) → motor → measured velocity →
    /// binoculars → interaction.</item>
    /// <item>LateUpdate (before the view model and FP arms, which run at Weapons + 50): camera feel (FOV stack, bob,
    /// dips, shake) → footsteps synced to the bob → shadow proxy.</item>
    /// </list>
    /// Every service is optional: without an input reader the player stands still, without settings the GDD defaults
    /// apply, without HUD/audio/FX the feedback is skipped. No AudioListener lives on the rig (WP06 owns the only one).
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Player)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerRig : MonoBehaviour, IPlayerRig, ITargetable, ISuppressible, ISaveable
    {
        /// <summary>Save key in checkpoint snapshots.</summary>
        public const string Key = "Player.Rig";

        [Header("Parts (wired by PlayerRigFactory)")]
        [SerializeField] private CharacterController _controller;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerCameraRig _camera;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private Binoculars _binoculars;
        [SerializeField] private FootstepEmitter _footsteps;
        [SerializeField] private OutOfBoundsWatcher _bounds;
        [SerializeField] private PlayerShadowProxy _shadow;
        [SerializeField] private PlayerMount _mount;
        [SerializeField] private Transform _viewmodelRoot;
        [SerializeField] private Transform _fpBodySlot;

        [Header("Behaviour")]
        [SerializeField] private bool _registerService = true;
        [SerializeField, Tooltip("View flinch on a near miss in degrees (CoD feel; 0 disables).")]
        private float _nearMissFlinchDegrees = 0.5f;
        [SerializeField, Tooltip("Frames without SetAdsState after which ADS is considered released (weapon holstered).")]
        private int _adsReportTimeoutFrames = 2;

        private readonly GameSettings _defaultSettings = new GameSettings();
        private ServiceRef<IInputReader> _input;
        private ServiceRef<ISettingsService> _settings;
        private ServiceRef<IScreenFX> _screenFx;
        private ServiceRef<ITargetRegistry> _targets;
        private ServiceRef<ICheckpointService> _checkpoints;
        private ITargetRegistry _registeredTargets;
        private ICheckpointService _registeredCheckpoints;

        private bool _resolved;
        private bool _wired;
        private bool _registered;
        private int _frame;
        private bool _isAds;
        private float _adsBlend;
        private int _adsFrame = -100;
        private bool _binocularsToggled;
        private bool _viewActive = true;
        private bool _showViewmodel = true;
        private bool _showShadow = true;
        private float? _forcedWalk;
        private bool _squadTarget;
        private float _lastEngagedByPlayer = -1000f;
        private PerchTag _zonePerch = PerchTag.Ground;
        private float? _eyeHeightOverride;

        /// <inheritdoc/>
        public event Action<float> Landed;

        // ------------------------------------------------------------------ parts

        /// <summary>The motor (movement state, tunables).</summary>
        public PlayerMotor Motor => _motor;
        /// <summary>Yaw/pitch.</summary>
        public PlayerLook Look => _look;
        /// <summary>Camera feel and the two cameras.</summary>
        public PlayerCameraRig CameraRig => _camera;
        /// <summary>Health (also the <see cref="IPlayerHealth"/> service).</summary>
        public PlayerHealth Health => _health;
        /// <summary>Use key.</summary>
        public PlayerInteractor Interactor => _interactor;
        /// <summary>Beat 1 field glasses.</summary>
        public Binoculars BinocularsComponent => _binoculars;
        /// <summary>Footsteps and movement foley.</summary>
        public FootstepEmitter Footsteps => _footsteps;
        /// <summary>Out-of-bounds countdown.</summary>
        public OutOfBoundsWatcher Bounds => _bounds;
        /// <summary>Shadow-only body driver (D19).</summary>
        public PlayerShadowProxy ShadowProxy => _shadow;
        /// <summary>Seat handling.</summary>
        public PlayerMount MountComponent => _mount;
        /// <summary>Slot of PFB_Calder_FP under the view-model root.</summary>
        public Transform FirstPersonBodySlot => _fpBodySlot;
        /// <summary>True while the rig's cameras render (<see cref="SetViewActive"/>).</summary>
        public bool ViewActive => _viewActive;
        /// <summary>Forced walking speed in m/s, or null.</summary>
        public float? ForcedWalk => _forcedWalk;

        // ------------------------------------------------------------------ IPlayerRig

        /// <inheritdoc/>
        public Transform Root => transform;
        /// <inheritdoc/>
        public Transform Eye => _camera != null && _camera.Eye != null ? _camera.Eye : transform;
        /// <inheritdoc/>
        public Camera WorldCamera => _camera != null ? _camera.WorldCamera : null;
        /// <inheritdoc/>
        public Camera ViewmodelCamera => _camera != null ? _camera.ViewmodelCamera : null;
        /// <inheritdoc/>
        public Transform ViewmodelRoot => _viewmodelRoot;
        /// <inheritdoc/>
        public Vector3 Velocity => _motor != null ? _motor.Velocity : Vector3.zero;
        /// <inheritdoc/>
        public Stance Stance => _motor != null ? _motor.Stance : Stance.Stand;
        /// <inheritdoc/>
        public bool IsGrounded => _motor != null && _motor.IsGrounded;
        /// <inheritdoc/>
        public bool IsADS => _isAds;
        /// <inheritdoc/>
        public float AdsBlend => _adsBlend;
        /// <inheritdoc/>
        public float SpeedMultiplier
        {
            set { if (_motor != null) _motor.SpeedMultiplier = value; }
        }
        /// <inheritdoc/>
        public float TimeSinceSprintEnd => _motor != null ? _motor.TimeSinceSprintEnd : 10f;
        /// <inheritdoc/>
        public float BobPhase => _camera != null ? _camera.BobPhase : 0f;
        /// <inheritdoc/>
        public float BobAmount01 => _camera != null ? _camera.BobAmount01 : 0f;
        /// <inheritdoc/>
        public Ray ViewRay
        {
            get
            {
                var e = Eye;
                return new Ray(e.position, e.forward);
            }
        }
        /// <inheritdoc/>
        public bool IsGlassing => _binoculars != null && _binoculars.IsGlassing;
        /// <inheritdoc/>
        public float Pitch => _look != null ? _look.Pitch : 0f;
        /// <inheritdoc/>
        public float Yaw => _look != null ? _look.Heading : MathX.BearingOf(transform.forward);

        // ------------------------------------------------------------------ ITargetable / IDamageable

        /// <inheritdoc/>
        public string ActorId => SquadIds.Player;
        /// <inheritdoc/>
        public Team Team => Team.Player;
        /// <inheritdoc/>
        public bool IsAlive => _health == null || _health.IsAlive;
        /// <inheritdoc/>
        public Transform AimPoint => _health != null ? _health.AimPoint : transform;
        /// <inheritdoc/>
        public PerchTag Perch => _mount != null && _mount.IsMounted ? PerchTag.Vehicle : _zonePerch;
        /// <inheritdoc/>
        public bool SquadTarget { get => _squadTarget; set => _squadTarget = value; }
        /// <inheritdoc/>
        public float LastEngagedByPlayer => _lastEngagedByPlayer;
        /// <inheritdoc/>
        public void NotifyEngagedByPlayer() => _lastEngagedByPlayer = Time.time;
        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            if (_health != null) _health.ApplyDamage(in d);
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Wires the parts (PlayerRigFactory); missing ones are looked up on the object.</summary>
        internal void Bind(CharacterController controller, PlayerMotor motor, PlayerLook look, PlayerCameraRig cameraRig, PlayerHealth health,
                           PlayerInteractor interactor, Binoculars binoculars, FootstepEmitter footsteps, OutOfBoundsWatcher bounds,
                           PlayerShadowProxy shadow, PlayerMount mount, Transform viewmodelRoot, Transform fpBodySlot)
        {
            _controller = controller;
            _motor = motor;
            _look = look;
            _camera = cameraRig;
            _health = health;
            _interactor = interactor;
            _binoculars = binoculars;
            _footsteps = footsteps;
            _bounds = bounds;
            _shadow = shadow;
            _mount = mount;
            _viewmodelRoot = viewmodelRoot;
            _fpBodySlot = fpBodySlot;
            _resolved = false;
        }

        private void Awake()
        {
            Resolve();
            if (_registerService)
            {
                GameServices.Register<IPlayerRig>(this);
                _registered = true;
            }
        }

        private void OnEnable()
        {
            Resolve();
            Wire();
        }

        private void Start() => RegisterWithServices();

        private void OnDisable() => Unwire();

        /// <summary>Subscribes to the parts' and the global events (OnEnable; EditMode tests call it directly). Idempotent.</summary>
        internal void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (_motor != null)
            {
                _motor.Landed += OnMotorLanded;
                _motor.FallDamaged += OnFallDamaged;
                _motor.TraversalStarted += OnTraversalStarted;
            }
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
                _health.Respawned += OnRespawned;
            }
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.ZoneChanged += OnZoneChanged;
            GameServices.Changed += OnServiceChanged;
        }

        /// <summary>Undoes <see cref="Wire"/>.</summary>
        internal void Unwire()
        {
            if (!_wired) return;
            _wired = false;
            if (_motor != null)
            {
                _motor.Landed -= OnMotorLanded;
                _motor.FallDamaged -= OnFallDamaged;
                _motor.TraversalStarted -= OnTraversalStarted;
            }
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
                _health.Respawned -= OnRespawned;
            }
            GameEvents.NearMiss -= OnNearMiss;
            GameEvents.ZoneChanged -= OnZoneChanged;
            GameServices.Changed -= OnServiceChanged;
        }

        private void OnDestroy()
        {
            if (_registered) GameServices.Unregister<IPlayerRig>(this);
            _registered = false;
            if (_registeredTargets != null && !(_registeredTargets is UnityEngine.Object to && to == null)) _registeredTargets.Unregister(this);
            _registeredTargets = null;
            if (_registeredCheckpoints != null && !(_registeredCheckpoints is UnityEngine.Object co && co == null)) _registeredCheckpoints.Unregister(this);
            _registeredCheckpoints = null;
        }

        private void Update() => TickUpdate(Time.deltaTime);

        private void LateUpdate() => TickLate(Time.deltaTime);

        /// <summary>Runs one whole frame with an explicit time step (EditMode tests, deterministic simulation).</summary>
        internal void SimulateFrame(float dt)
        {
            TickUpdate(dt);
            TickLate(dt);
        }

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_look == null) _look = GetComponent<PlayerLook>();
            if (_camera == null) _camera = GetComponent<PlayerCameraRig>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_interactor == null) _interactor = GetComponent<PlayerInteractor>();
            if (_binoculars == null) _binoculars = GetComponent<Binoculars>();
            if (_footsteps == null) _footsteps = GetComponent<FootstepEmitter>();
            if (_bounds == null) _bounds = GetComponent<OutOfBoundsWatcher>();
            if (_shadow == null) _shadow = GetComponent<PlayerShadowProxy>();
            if (_mount == null) _mount = GetComponent<PlayerMount>();
            if (_viewmodelRoot == null) _viewmodelRoot = FindDeep(transform, PlayerRigNames.ViewmodelRoot);
            if (_fpBodySlot == null && _viewmodelRoot != null) _fpBodySlot = _viewmodelRoot.Find(PlayerRigNames.FirstPersonBodySlot);
            if (_motor == null || _look == null || _camera == null)
                LSLog.Error(LogCategory.Player, "PlayerRig is missing its motor, look or camera rig; rebuild PF_Player (LongShadow/Player/Build Player Prefab).", this);
        }

        // ------------------------------------------------------------------ frame

        private GameSettings Settings
        {
            get
            {
                var s = _settings.Value;
                var c = s != null ? s.Current : null;
                return c ?? _defaultSettings;
            }
        }

        private void TickUpdate(float dt)
        {
            if (_motor == null || _look == null || _camera == null) return;
            if (dt < 0f) dt = 0f;
            _frame++;
            if (_frame - _adsFrame > _adsReportTimeoutFrames)
            {
                _isAds = false;
                _adsBlend = Mathf.MoveTowards(_adsBlend, 0f, dt * 5f);
            }

            var alive = IsAlive;
            var reader = _input.Value;
            var hasInput = reader != null && alive;
            var s = Settings;
            var mounted = _mount != null && _mount.IsMounted;

            // Binoculars are raised with the aim button (hold, or toggle when "ADS toggle" is on, GDD §11/§12).
            var binocularsAvailable = _binoculars != null && _binoculars.Available;
            if (!binocularsAvailable || !hasInput) _binocularsToggled = false;
            else if (s.AdsToggle && reader.AimDown) _binocularsToggled = !_binocularsToggled;
            var binocularsHeld = binocularsAvailable && hasInput && (s.AdsToggle ? _binocularsToggled : reader.Aim);
            var binocularsRaise = _binoculars != null ? _binoculars.Raise01 : 0f;
            var aiming = _isAds || binocularsRaise > 0.5f;

            // Look (GDD §11: degrees = CSS px × sens × 0.03; ADS × ADS sens × FOV ratio).
            var lookDelta = Vector2.zero;
            if (hasInput)
            {
                var multiplier = LookMath.Multiplier(aiming, s.AdsSensitivity, _camera.CurrentVerticalFov, _camera.HipVerticalFov);
                lookDelta = LookMath.LookDegrees(reader.Look, s.MouseSensitivity, s.InvertY, multiplier);
            }
            _look.Tick(lookDelta, dt);

            // Motor.
            var input = new MotorInput
            {
                Move = hasInput ? reader.Move : Vector2.zero,
                Sprint = hasInput && reader.Sprint,
                JumpDown = hasInput && reader.JumpDown,
                CrouchDown = hasInput && reader.CrouchDown,
                Heading = _look.Heading,
                Pitch = _look.Pitch,
                Ads = aiming,
                AdsBlend = Mathf.Max(_adsBlend, binocularsRaise),
            };
            _motor.InputBlocked = !alive;
            _motor.ForcedWalkSpeed = _forcedWalk ?? 0f;
            _motor.Tick(in input, dt);
            _motor.MeasureVelocity(dt);

            // Binoculars and interaction.
            var stance = _motor.Stance;
            var handsFree = alive && _viewActive && !mounted && stance != Stance.Ladder && stance != Stance.Mantle;
            if (_binoculars != null) _binoculars.Tick(binocularsHeld, handsFree && stance != Stance.Slide, ViewRay, _camera, dt);
            if (_interactor != null)
            {
                var canUse = handsFree && !IsGlassing;
                _interactor.Tick(Eye, this, canUse, hasInput && reader.InteractDown, hasInput && reader.Interact, dt);
            }
        }

        private void TickLate(float dt)
        {
            if (_motor == null || _camera == null) return;
            if (dt < 0f) dt = 0f;
            var alive = IsAlive;
            var s = Settings;
            var ms = _motor.Settings;
            var multiplier = Mathf.Clamp(SpeedMultiplierValue, 0.1f, 2f);
            var frame = new CameraFrame
            {
                Stance = _motor.Stance,
                PlanarSpeed = _motor.PlanarSpeed,
                LocalVelocity = transform.InverseTransformDirection(_motor.Velocity),
                Grounded = _motor.IsGrounded,
                Crouched = _motor.IsCrouched,
                EyeHeight = _motor.EyeHeight,
                AdsBlend = _adsBlend,
                Glassing = IsGlassing,
                Dead = !alive,
                Traversal = _motor.TraversalKind,
                Traversal01 = _motor.Traversal01,
                FovHorizontal = s.FovHorizontal,
                WalkSpeed = ms.WalkSpeed * multiplier,
                SprintSpeed = ms.SprintSpeed * multiplier,
            };
            _camera.Tick(in frame, dt);

            if (_footsteps != null) _footsteps.Tick(_camera.FootfallsThisFrame, _motor.Stance, _motor.PlanarSpeed, _adsBlend, alive && _viewActive, dt);
            if (_shadow != null)
            {
                var mounted = _mount != null && _mount.IsMounted;
                var visible = PlayerShadowProxy.ShouldShow(_showShadow, _viewActive, alive, mounted, _camera.EyeHeightNow);
                _shadow.Tick(visible, _motor.Stance, _motor.IsCrouched, _motor.PlanarSpeed, _isAds, ms, dt);
            }
        }

        private float SpeedMultiplierValue => _motor != null ? _motor.SpeedMultiplier : 1f;

        // ------------------------------------------------------------------ IPlayerRig commands

        /// <inheritdoc/>
        public void AddAimDelta(float pitch, float yaw)
        {
            if (_look != null) _look.AddDelta(pitch, yaw);
        }

        /// <inheritdoc/>
        public void SetViewPunch(Quaternion punch)
        {
            if (_camera != null) _camera.Punch(punch);
        }

        /// <inheritdoc/>
        public void SetAdsState(bool isAds, float blend01)
        {
            _isAds = isAds;
            _adsBlend = Mathf.Clamp01(blend01);
            _adsFrame = _frame;
        }

        /// <inheritdoc/>
        public void RequestFov(object owner, float vFov, float blendTime)
        {
            if (_camera != null) _camera.RequestFov(owner, vFov, blendTime);
        }

        /// <inheritdoc/>
        public void ReleaseFov(object owner)
        {
            if (_camera != null) _camera.ReleaseFov(owner);
        }

        /// <inheritdoc/>
        public void Shake(float trauma)
        {
            if (_camera != null) _camera.Shake(trauma);
        }

        /// <inheritdoc/>
        public void Teleport(Vector3 position, float yaw)
        {
            Resolve();
            if (_mount != null && _mount.IsMounted) _mount.Dismount();
            if (_motor != null) _motor.Teleport(position);
            if (_look != null)
            {
                _look.SetAngles(0f, yaw);
                _look.ResetEffects();
            }
            if (_camera != null && _motor != null) _camera.ResetEffects(_motor.EyeHeight);
            if (_footsteps != null) _footsteps.ResetState();
        }

        /// <inheritdoc/>
        public void SetLookAngles(float pitch, float yaw)
        {
            if (_look != null) _look.SetAngles(pitch, yaw);
        }

        /// <inheritdoc/>
        public void SetViewActive(bool active)
        {
            _viewActive = active;
            ApplyCameraState();
        }

        /// <inheritdoc/>
        public void SetForcedWalk(float? speed) => _forcedWalk = speed.HasValue && speed.Value > 0f ? speed : null;

        /// <inheritdoc/>
        public void SetLookLimits(Quaternion center, float yaw, float up, float down)
        {
            if (_look != null) _look.SetLimits(center, yaw, up, down);
        }

        /// <inheritdoc/>
        public void ClearLookLimits()
        {
            if (_look != null) _look.ClearLimits();
        }

        /// <inheritdoc/>
        public void SetDamageModifiers(float scale, float floorHp)
        {
            if (_health != null) _health.SetDamageModifiers(scale, floorHp);
        }

        /// <inheritdoc/>
        public void Mount(Transform seat)
        {
            Resolve();
            if (_mount == null)
            {
                LSLog.Error(LogCategory.Player, "PlayerRig.Mount: no PlayerMount component on the rig.", this);
                return;
            }
            if (_interactor != null) _interactor.Clear();
            _mount.Mount(seat);
        }

        /// <inheritdoc/>
        public void Dismount()
        {
            if (_mount != null) _mount.Dismount();
        }

        /// <inheritdoc/>
        public void SetBinocularsAvailable(bool available)
        {
            _binocularsToggled = false;
            if (_binoculars != null) _binoculars.SetAvailable(available, _camera);
        }

        /// <inheritdoc/>
        public void SetBodyVisibility(bool viewmodel, bool shadowProxy)
        {
            _showViewmodel = viewmodel;
            _showShadow = shadowProxy;
            ApplyCameraState();
        }

        /// <summary>
        /// Scripted posture: overrides the eye height (GDD §7.1 start prone at about 0.35 m, then the scripted kneel at
        /// 1.05 m) blended over <paramref name="blendSeconds"/>; null returns to the stance height. The shadow proxy hides
        /// while the eye is below <see cref="PlayerShadowProxy.ProneEyeHeight"/> (no prone clip in AC_ShadowProxy).
        /// Local adapter for the requested Core member <c>IPlayerRig.SetEyeHeightOverride</c> (Docs/CORE_CHANGES.md, WP02).
        /// </summary>
        public void SetEyeHeightOverride(float? eyeHeight, float blendSeconds)
        {
            _eyeHeightOverride = eyeHeight;
            if (_camera != null) _camera.SetEyeHeightOverride(eyeHeight, blendSeconds);
        }

        /// <summary>Current scripted eye height override, or null.</summary>
        public float? EyeHeightOverride => _eyeHeightOverride;

        private void ApplyCameraState()
        {
            if (_camera == null) return;
            _camera.SetViewActive(_viewActive);
            var vm = _camera.ViewmodelCamera;
            // Hide the view model by culling, never by disabling Cam_Viewmodel: it runs the stack's grade (ViewmodelPostMode).
            if (vm != null) vm.cullingMask = _showViewmodel ? PlayerLayers.ViewmodelCameraCulling : 0;
        }

        // ------------------------------------------------------------------ ISuppressible

        /// <inheritdoc/>
        public void Suppress(float seconds, Team by)
        {
            if (seconds <= 0f || !IsAlive || !Teams.AreHostile(by, Team.Player)) return;
            if (_look != null) _look.Suppress(seconds);
            _screenFx.Value?.PulseSuppression(Mathf.Clamp01(seconds));
        }

        // ------------------------------------------------------------------ ISaveable

        /// <inheritdoc/>
        public string SaveKey => Key;

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            w.Write("pos", transform.position);
            w.Write("yaw", Yaw);
            w.Write("binoculars", _binoculars != null && _binoculars.Available);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            // GDD §17 / TECH_PLAN §5.5: the checkpoint service teleports the player to the CP marker; here only the
            // transient state that a beat script re-applies after ResumeFrom is cleared.
            if (_mount != null && _mount.IsMounted) _mount.Dismount();
            _forcedWalk = null;
            if (_look != null)
            {
                _look.ClearLimits();
                _look.ResetEffects();
            }
            _isAds = false;
            _adsBlend = 0f;
            _binocularsToggled = false;
            if (_binoculars != null) _binoculars.SetAvailable(r.ReadBool("binoculars", false), _camera);
            if (_motor != null) _motor.ResetState();
            if (_camera != null && _motor != null) _camera.ResetEffects(_motor.EyeHeight);
            if (_footsteps != null) _footsteps.ResetState();
            if (_bounds != null) _bounds.ResetState();
        }

        // ------------------------------------------------------------------ events

        private void OnMotorLanded(float impactSpeed)
        {
            if (_camera != null) _camera.OnLanded(impactSpeed);
            if (_footsteps != null) _footsteps.OnLanded(impactSpeed);
            Landed?.Invoke(impactSpeed);
        }

        private void OnFallDamaged(float amount, float height)
        {
            if (_health == null) return;
            LSLog.Verbose(LogCategory.Player, "Fall " + height.ToString("0.00") + " m -> " + amount.ToString("0") + " HP", this);
            _health.ApplyDamage(PlayerDamageMath.Fall(amount, transform.position));
        }

        private void OnTraversalStarted(LedgeKind kind)
        {
            if (_camera != null) _camera.OnTraversalStarted(kind);
        }

        private void OnDamaged(DamageInfo d)
        {
            if (_camera == null) return;
            var amount = d.Amount > 0f ? d.Amount : 4f; // absorbed by last-stand immunity: still flinch
            if (d.Type == DamageType.Fall) _camera.Shake(Mathf.Clamp01(d.Amount / 60f));
            else _camera.DamagePunch(d.Direction, amount);
        }

        private void OnDied(DamageInfo d)
        {
            if (_interactor != null) _interactor.Clear();
            _binocularsToggled = false;
            _isAds = false;
            if (_bounds != null) _bounds.Cancel();
        }

        private void OnRespawned()
        {
            if (_motor != null) _motor.ResetState();
            if (_look != null) _look.ResetEffects();
            if (_camera != null && _motor != null) _camera.ResetEffects(_motor.EyeHeight);
            if (_footsteps != null) _footsteps.ResetState();
        }

        private void OnNearMiss(NearMissEvent e)
        {
            if (_camera == null || _nearMissFlinchDegrees <= 0f || !IsAlive) return;
            // Flinch away from the bullet's side, stronger for closer passes (GDD §14.3 near misses within 3 m).
            var local = transform.InverseTransformPoint(e.Point);
            var side = local.x >= 0f ? -1f : 1f;
            var k = _nearMissFlinchDegrees * Mathf.Clamp01(1f - e.Distance / 3f + 0.25f);
            _camera.Punch(Quaternion.Euler(-k * 0.5f, side * k, side * k * 0.6f));
        }

        private void OnZoneChanged(ZoneType zone) => _zonePerch = zone == ZoneType.Roof ? PerchTag.Rooftop : PerchTag.Ground;

        private void OnServiceChanged(Type type, object instance)
        {
            if (type == typeof(ITargetRegistry) || type == typeof(ICheckpointService)) RegisterWithServices();
        }

        private void RegisterWithServices()
        {
            var targets = _targets.Value;
            if (targets != null && !ReferenceEquals(targets, _registeredTargets))
            {
                targets.Register(this);
                _registeredTargets = targets;
            }
            var cp = _checkpoints.Value;
            if (cp != null && !ReferenceEquals(cp, _registeredCheckpoints))
            {
                cp.Register(this);
                _registeredCheckpoints = cp;
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var d = FindDeep(c, name);
                if (d != null) return d;
            }
            return null;
        }
    }
}
