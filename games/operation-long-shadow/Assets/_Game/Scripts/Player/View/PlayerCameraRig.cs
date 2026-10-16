using LongShadow.Core;
using LongShadow.Player.Movement;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>
    /// The camera feel of the player (TECH_PLAN §7.3 PlayerCameraRig, CINEMATIC_DIRECTION §4): FOV stack (settings
    /// horizontal FOV → vertical, sprint +5° / slide +4° kicks, owner-based requests for ADS and binoculars), eye height
    /// spring (crouch 1.05, slide dip 0.6 m), head bob and sprint roll, landing dip and nod scaled by impact speed,
    /// traversal dips and nods, trauma shake, view punch (weapons, damage), strafe tilt, ADS breathing, binocular
    /// hand-held sway and the death fall. Runs in LateUpdate through <see cref="PlayerRig"/>; writes the Yaw node height,
    /// Bob and Shake nodes and the world camera FOV. Visual only: <c>IPlayerRig.ViewRay</c> comes from the Eye node,
    /// above Bob/Shake, so punch and shake never move the aim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private CameraSettings _settings = new CameraSettings();
        [SerializeField] private Transform _yawNode;
        [SerializeField] private Transform _eyeOffset;
        [SerializeField] private Transform _bob;
        [SerializeField] private Transform _shake;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private Camera _viewmodelCamera;

        private readonly FovStack _fov = new FovStack();
        private readonly HeadBob _headBob = new HeadBob();
        private TraumaShake _trauma;
        private SpringFloat _eye;
        private SpringFloat _landDip;
        private SpringFloat _landNod;
        private SpringQuaternion _punch;
        private float _kick;
        private float _strafeRoll;
        private float _deathTime = -1f;
        private float _overrideHeight;
        private float _overrideWeight;
        private float _overrideTarget;
        private float _overrideBlend = 0.5f;
        private float _vaultSide = 1f;
        private float _time;
        private float _hipVerticalFov = 58.7f;
        private float _currentFov = 58.7f;
        private bool _viewActive = true;
        private bool _initialised;

        /// <summary>Tunables.</summary>
        public CameraSettings Settings => _settings;
        /// <summary>Cam_World.</summary>
        public Camera WorldCamera => _worldCamera;
        /// <summary>Cam_Viewmodel.</summary>
        public Camera ViewmodelCamera => _viewmodelCamera;
        /// <summary>The Eye node (IPlayerRig.Eye).</summary>
        public Transform Eye => _eyeOffset;
        /// <summary>World camera vertical FOV this frame.</summary>
        public float CurrentVerticalFov => _currentFov;
        /// <summary>Vertical FOV of the settings at the hip (no kick, no request); the sensitivity reference.</summary>
        public float HipVerticalFov => _hipVerticalFov;
        /// <summary>Head-bob phase in radians.</summary>
        public float BobPhase => _headBob.Phase;
        /// <summary>Head-bob amount 0..1.</summary>
        public float BobAmount01 => _headBob.Amount01;
        /// <summary>Footfalls during the last tick (footsteps sync to the bob).</summary>
        public int FootfallsThisFrame => _headBob.Footfalls;
        /// <summary>Current eye height above the feet (m), including dips.</summary>
        public float EyeHeightNow { get; private set; }
        /// <summary>Current trauma 0..1.</summary>
        public float Trauma => _trauma.Trauma;
        /// <summary>True while the rig's cameras render.</summary>
        public bool ViewActive => _viewActive;
        /// <summary>True while a FOV request (ADS, binoculars, scripts) drives the FOV.</summary>
        public bool HasFovRequest => _fov.HasRequest;

        /// <summary>Wires the hierarchy (PlayerRigFactory).</summary>
        internal void Bind(Transform yawNode, Transform eyeOffset, Transform bob, Transform shake, Camera world, Camera viewmodel)
        {
            _yawNode = yawNode;
            _eyeOffset = eyeOffset;
            _bob = bob;
            _shake = shake;
            _worldCamera = world;
            _viewmodelCamera = viewmodel;
        }

        private void Awake() => Initialise(1.65f);

        /// <summary>Configures the cameras (TECH_PLAN §3.6) and puts the springs at rest (idempotent).</summary>
        public void Initialise(float eyeHeight)
        {
            if (_initialised) return;
            _initialised = true;
            PlayerCameraSetup.Configure(_worldCamera, _viewmodelCamera, _settings);
            _eye = new SpringFloat(_settings.EyeSpringFrequency, 1f, eyeHeight);
            _landDip = new SpringFloat(_settings.LandSpringFrequency, _settings.LandSpringDamping);
            _landNod = new SpringFloat(_settings.LandSpringFrequency, _settings.LandSpringDamping);
            _punch = new SpringQuaternion(_settings.PunchFrequency, _settings.PunchDamping);
            if (_eyeOffset != null) _eyeOffset.localPosition = _settings.EyeOffsetFromNeck;
            EyeHeightNow = eyeHeight;
            ApplyEyeHeight(eyeHeight, 0f);
            if (_worldCamera != null) _fov.Snap(_worldCamera.fieldOfView);
        }

        /// <summary>Advances every camera effect and writes the transforms and the FOV.</summary>
        public void Tick(in CameraFrame f, float dt)
        {
            Initialise(f.EyeHeight);
            if (dt < 0f) dt = 0f;
            _time += dt;

            // Eye height (stance), optional scripted override (prone/kneel), landing dip, traversal, death.
            _eye.Target = f.EyeHeight;
            _eye.Step(dt);
            if (_overrideBlend > 0f) _overrideWeight = Mathf.MoveTowards(_overrideWeight, _overrideTarget, dt / _overrideBlend);
            else _overrideWeight = _overrideTarget;
            var w = _overrideWeight * _overrideWeight * (3f - 2f * _overrideWeight);
            var eye = Mathf.Lerp(_eye.Value, _overrideHeight, w);
            _landDip.Step(dt);
            _landNod.Step(dt);

            var env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(f.Traversal01));
            float travDip = 0f, travRoll = 0f, travNod = 0f;
            switch (f.Traversal)
            {
                case LedgeKind.Vault:
                    travDip = -_settings.VaultDip * env;
                    travRoll = _settings.VaultRollDegrees * env * _vaultSide;
                    travNod = 2f * env;
                    break;
                case LedgeKind.Climb:
                    travDip = -0.04f * env;
                    travNod = _settings.ClimbNodDegrees * env;
                    break;
                case LedgeKind.AirMantle:
                    travNod = _settings.MantleNodDegrees * env;
                    break;
                case LedgeKind.LadderExit:
                    travNod = _settings.MantleNodDegrees * 0.6f * env;
                    break;
            }

            float deathDrop = 0f, deathRoll = 0f, deathPitch = 0f;
            if (f.Dead)
            {
                if (_deathTime < 0f) _deathTime = 0f;
                _deathTime += dt;
                var d = Mathf.Clamp01(_deathTime / Mathf.Max(0.05f, _settings.DeathSeconds));
                d *= d;
                deathDrop = (_settings.DeathEyeHeight - eye) * d;
                deathRoll = _settings.DeathRollDegrees * d;
                deathPitch = -_settings.DeathPitchDegrees * d;
            }
            else
            {
                _deathTime = -1f;
            }

            EyeHeightNow = eye + _landDip.Value + travDip + deathDrop;
            ApplyEyeHeight(EyeHeightNow, 0f);

            // Head bob, sprint roll, strafe tilt.
            var bobGrounded = f.Grounded && (f.Stance == Stance.Stand || f.Stance == Stance.Crouch || f.Stance == Stance.Sprint);
            _headBob.Step(dt, f.PlanarSpeed, bobGrounded, f.WalkSpeed, f.SprintSpeed, f.Glassing ? 1f : f.AdsBlend, f.Crouched);
            var walk = Mathf.Max(0.1f, f.WalkSpeed);
            var rollTarget = bobGrounded && !f.Dead ? -Mathf.Clamp(f.LocalVelocity.x / walk, -1.5f, 1.5f) * _settings.StrafeRollDegrees : 0f;
            _strafeRoll = MathX.Damp(_strafeRoll, rollTarget, 8f, dt);
            if (_bob != null)
            {
                _bob.localPosition = f.Dead ? Vector3.zero : _headBob.Offset;
                _bob.localRotation = Quaternion.Euler(0f, 0f, _headBob.Roll + _strafeRoll + travRoll + deathRoll);
            }

            // Shake, punch, breathing / hand-held sway, landing nod.
            _trauma.Tick(dt, _settings.TraumaDecayPerSecond);
            _punch.Step(dt);
            var shakeAngles = _trauma.Angles(_settings.ShakeMaxDegrees, _settings.ShakeFrequency);
            var shakeOffset = _trauma.Offset(_settings.ShakeMaxOffset, _settings.ShakeFrequency);
            var breath = f.Glassing ? _settings.GlassingSwayDegrees : _settings.AdsBreathDegrees * Mathf.Clamp01(f.AdsBlend);
            var breathPitch = breath * Mathf.Sin(_time * 2f * Mathf.PI * 0.22f);
            var breathYaw = breath * 0.7f * Mathf.Sin(_time * 2f * Mathf.PI * 0.17f + 1.3f);
            if (_shake != null)
            {
                _shake.localRotation = _punch.Value * Quaternion.Euler(
                    shakeAngles.x + breathPitch + _landNod.Value + travNod + deathPitch,
                    shakeAngles.y + breathYaw,
                    shakeAngles.z);
                _shake.localPosition = shakeOffset;
            }

            // FOV: settings horizontal → vertical, sprint/slide kicks, then owner requests (ADS, binoculars).
            var kickTarget = 0f;
            if (f.Stance == Stance.Sprint)
            {
                var span = Mathf.Max(0.1f, f.SprintSpeed - f.WalkSpeed);
                kickTarget = _settings.SprintKick * Mathf.Clamp01((f.PlanarSpeed - f.WalkSpeed * 0.8f) / span);
            }
            else if (f.Stance == Stance.Slide)
            {
                kickTarget = _settings.SlideKick;
            }
            _kick = MathX.Damp(_kick, kickTarget, kickTarget > _kick ? _settings.KickRiseLambda : _settings.KickFallLambda, dt);
            var aspect = Aspect();
            var horizontal = Mathf.Clamp(f.FovHorizontal, GameSettings.MinFov, GameSettings.MaxFov);
            _hipVerticalFov = MathX.HorizontalToVerticalFov(horizontal, aspect);
            var baseVertical = MathX.HorizontalToVerticalFov(horizontal + _kick, aspect);
            _currentFov = _fov.Evaluate(baseVertical, dt);
            if (_worldCamera != null) _worldCamera.fieldOfView = _currentFov;
        }

        /// <summary>Landing dip and nod scaled by the impact speed (m/s).</summary>
        public void OnLanded(float impactSpeed)
        {
            if (impactSpeed < _settings.LandMinSpeed) return;
            var over = impactSpeed - _settings.LandMinSpeed;
            var dip = Mathf.Min(_settings.MaxLandDip, 0.012f + over * _settings.LandDipPerMps);
            var nod = Mathf.Min(_settings.MaxLandNod, 0.5f + over * _settings.LandNodPerMps);
            var omega = 2f * Mathf.PI * _settings.LandSpringFrequency;
            _landDip.AddImpulse(-dip * omega * 1.8f);
            _landNod.AddImpulse(nod * omega * 1.8f);
            if (impactSpeed > 9f) _trauma.Add(Mathf.Clamp01((impactSpeed - 9f) / 12f) * 0.5f);
        }

        /// <summary>Alternates the vault roll side so repeated vaults do not all lean the same way.</summary>
        public void OnTraversalStarted(LedgeKind kind)
        {
            if (kind == LedgeKind.Vault) _vaultSide = -_vaultSide;
        }

        /// <summary>Adds camera trauma 0..1 (IPlayerRig.Shake).</summary>
        public void Shake(float trauma) => _trauma.Add(trauma);

        /// <summary>Visual-only view punch that springs back (IPlayerRig.SetViewPunch).</summary>
        public void Punch(Quaternion punch) => _punch.Punch(punch);

        /// <summary>Head snaps away from incoming damage (CINEMATIC_DIRECTION §4 "view punch on damage").</summary>
        public void DamagePunch(Vector3 worldDirection, float amount)
        {
            if (_yawNode == null || amount <= 0f) return;
            var local = worldDirection.sqrMagnitude > 1e-6f ? Quaternion.Inverse(_yawNode.rotation) * worldDirection.normalized : Vector3.back;
            var k = Mathf.Clamp(amount / 25f, 0.3f, 1.5f) * _settings.DamagePunchDegrees;
            var fromFront = Mathf.Clamp(-local.z, 0.3f, 1f);
            _punch.Punch(Quaternion.Euler(-k * fromFront, k * 0.6f * local.x, -k * 0.8f * local.x));
            _trauma.Add(amount * _settings.DamageTraumaPerHp);
        }

        /// <summary>Owner-based FOV request in vertical degrees (IPlayerRig.RequestFov).</summary>
        public void RequestFov(object owner, float verticalFov, float blendTime) => _fov.Request(owner, verticalFov, blendTime);

        /// <summary>Releases an FOV request (IPlayerRig.ReleaseFov).</summary>
        public void ReleaseFov(object owner) => _fov.Release(owner);

        /// <summary>Overrides the eye height (prone/kneel for scripted posture); null releases. Blend in seconds.</summary>
        public void SetEyeHeightOverride(float? height, float blendSeconds)
        {
            _overrideBlend = Mathf.Max(0f, blendSeconds);
            if (height.HasValue)
            {
                _overrideHeight = Mathf.Max(0.1f, height.Value);
                _overrideTarget = 1f;
            }
            else
            {
                _overrideTarget = 0f;
            }
        }

        /// <summary>Enables or disables both cameras (IPlayerRig.SetViewActive).</summary>
        public void SetViewActive(bool active)
        {
            _viewActive = active;
            if (_worldCamera != null) _worldCamera.enabled = active;
            if (_viewmodelCamera != null) _viewmodelCamera.enabled = active;
        }

        /// <summary>Puts every effect at rest at an eye height (respawn, teleport).</summary>
        public void ResetEffects(float eyeHeight)
        {
            Initialise(eyeHeight);
            _eye.Reset(eyeHeight);
            _landDip.Reset(0f);
            _landNod.Reset(0f);
            _punch.Reset(Quaternion.identity);
            _trauma.Trauma = 0f;
            _kick = 0f;
            _strafeRoll = 0f;
            _deathTime = -1f;
            _headBob.Reset();
            EyeHeightNow = eyeHeight;
            ApplyEyeHeight(eyeHeight, 0f);
            if (_bob != null)
            {
                _bob.localPosition = Vector3.zero;
                _bob.localRotation = Quaternion.identity;
            }
            if (_shake != null)
            {
                _shake.localPosition = Vector3.zero;
                _shake.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Local position of the Eye relative to the root for a standing eye (used to seat the eye on a mount).</summary>
        public Vector3 EyeLocalOffset(float eyeHeight) => new Vector3(0f, eyeHeight, 0f) + new Vector3(_settings.EyeOffsetFromNeck.x, 0f, _settings.EyeOffsetFromNeck.z);

        private void ApplyEyeHeight(float eyeHeight, float extra)
        {
            if (_yawNode == null) return;
            _yawNode.localPosition = new Vector3(0f, eyeHeight - _settings.EyeOffsetFromNeck.y + extra, 0f);
        }

        private float Aspect()
        {
            if (_worldCamera != null && _worldCamera.pixelHeight > 0 && _worldCamera.pixelWidth > 0) return _worldCamera.aspect;
            return 16f / 9f;
        }
    }
}
