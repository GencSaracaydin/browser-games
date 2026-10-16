using LongShadow.Core;
using LongShadow.Player.Movement;
using UnityEngine;

namespace LongShadow.Player.Footsteps
{
    /// <summary>
    /// Footsteps and movement foley (TECH_PLAN §7.3 FootstepEmitter, GDD §12, §14.2). Steps are timed by the head-bob
    /// footfalls (so sound, camera and the view-model bob share one stride clock); the surface comes from the ground
    /// collider (<see cref="SurfaceTag"/> or physic material name), the dominant terrain layer
    /// (<see cref="TerrainSurfaceCache"/>), else the acoustic zone default (<see cref="FootstepMath.ZoneDefaultSurface"/>);
    /// the cue is <see cref="AudioCues.Footstep"/>, falling back to the sand set while a P1 set (SALTCRUST) is missing.
    /// Every step raises an AI <see cref="NoiseEvent"/> (sprint 12 m, walk 4 m, crouch 1.5 m). Also: landing thump
    /// (impact-scaled), slide scrape, vault/climb/mantle grunt-and-gear, ladder rungs, jump push-off and the webbing
    /// rattle loop while sprinting. Allocation-free per step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [SerializeField] private PlayerMotor _motor;
        [SerializeField, Tooltip("Raise AI noise events (GDD §14.2).")]
        private bool _raiseNoise = true;
        [SerializeField, Tooltip("Landing impact (m/s) from which the gear thump plays.")]
        private float _landThumpSpeed = 5.5f;
        [SerializeField, Tooltip("Landing impact (m/s) from which a landing footstep plays.")]
        private float _landStepSpeed = 2.2f;
        [SerializeField, Tooltip("Fade of the gear rattle loop when the sprint ends (s).")]
        private float _gearFade = 0.35f;

        private ServiceRef<IAudioService> _audio;
        private Collider _surfaceCollider;
        private SurfaceType _surfaceOfCollider;
        private Terrain _terrain;
        private int _gearHandle;
        private bool _gearOn;
        private bool _subscribed;

        /// <summary>Surface of the last step.</summary>
        public SurfaceType LastSurface { get; private set; }
        /// <summary>Cue id of the last step.</summary>
        public string LastCue { get; private set; }
        /// <summary>AI hearing radius of the last step or foley noise (m).</summary>
        public float LastNoiseRadius { get; private set; }
        /// <summary>Steps played since the last reset (telemetry, tests).</summary>
        public int StepCount { get; private set; }
        /// <summary>True while the gear rattle loop runs.</summary>
        public bool GearLoopOn => _gearOn;

        /// <summary>Wires the motor (PlayerRigFactory).</summary>
        internal void Bind(PlayerMotor motor)
        {
            Unsubscribe();
            _motor = motor;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            StopGear(0.1f);
        }

        /// <summary>Per-frame update (PlayerRig LateUpdate, after the camera bob advanced).</summary>
        public void Tick(int footfalls, Stance stance, float planarSpeed, float adsBlend, bool active, float dt)
        {
            if (!active)
            {
                StopGear(_gearFade);
                return;
            }
            var stepping = stance == Stance.Stand || stance == Stance.Crouch || stance == Stance.Sprint;
            if (stepping && footfalls > 0 && planarSpeed > 0.5f)
            {
                var volume = FootstepMath.Volume(stance, adsBlend);
                for (var i = 0; i < footfalls && i < 2; i++) Step(stance, volume);
            }

            var wantGear = stance == Stance.Sprint || stance == Stance.Slide;
            if (wantGear && !_gearOn) StartGear();
            else if (!wantGear && _gearOn) StopGear(_gearFade);
        }

        /// <summary>Landing foley and noise, scaled by the impact speed (m/s).</summary>
        public void OnLanded(float impactSpeed)
        {
            if (impactSpeed < _landStepSpeed) return;
            var k = Mathf.InverseLerp(_landStepSpeed, 14f, impactSpeed);
            PlaySurface(Mathf.Lerp(0.55f, 1f, k));
            if (impactSpeed >= _landThumpSpeed) _audio.Value?.Play(PlayerCues.Land, transform.position, Mathf.Lerp(0.6f, 1f, k));
            Noise(Mathf.Lerp(FootstepMath.WalkNoiseRadius, FootstepMath.SprintNoiseRadius, k), NoiseKind.Footstep);
        }

        /// <summary>Clears counters and stops loops (respawn, teleport).</summary>
        public void ResetState()
        {
            StepCount = 0;
            StopGear(0.1f);
            _surfaceCollider = null;
            _terrain = null;
        }

        /// <summary>Surface under the feet now (SurfaceTag/physic material, terrain layer, zone default).</summary>
        public SurfaceType CurrentSurface()
        {
            var c = _motor != null ? _motor.GroundCollider : null;
            var s = SurfaceType.Default;
            if (c != null)
            {
                if (c != _surfaceCollider)
                {
                    _surfaceCollider = c;
                    _terrain = c is TerrainCollider && c.TryGetComponent(out Terrain t) ? t : null;
                    _surfaceOfCollider = _terrain != null ? SurfaceType.Default : SurfaceTag.Resolve(c);
                }
                s = _terrain != null ? TerrainSurfaceCache.Sample(_terrain, transform.position) : _surfaceOfCollider;
            }
            if (s == SurfaceType.Default)
            {
                var audio = _audio.Value;
                s = FootstepMath.ZoneDefaultSurface(audio != null ? audio.CurrentZone : ZoneType.OpenDesert);
            }
            return s;
        }

        private void Step(Stance stance, float volume)
        {
            StepCount++;
            PlaySurface(volume, stance == Stance.Sprint ? FootstepMath.SprintPitch : 1f);
            Noise(FootstepMath.NoiseRadius(stance), NoiseKind.Footstep);
        }

        private void PlaySurface(float volume, float pitch = 1f)
        {
            var surface = CurrentSurface();
            LastSurface = surface;
            var cue = AudioCues.Footstep(surface);
            var audio = _audio.Value;
            if (audio != null && !audio.HasCue(cue)) cue = AudioCues.FootstepFallback; // SALTCRUST is P1 (GDD §12)
            LastCue = cue;
            if (audio == null) return;
            if (pitch != 1f) audio.PlayPitched(cue, transform.position, volume, pitch);
            else audio.Play(cue, transform.position, volume);
        }

        private void Noise(float radius, NoiseKind kind)
        {
            LastNoiseRadius = radius;
            if (_raiseNoise) GameEvents.RaiseNoise(new NoiseEvent(transform.position, radius, Team.Player, kind));
        }

        private void OnSlideStarted()
        {
            _audio.Value?.Play(PlayerCues.Slide, transform.position, 0.9f);
            Noise(FootstepMath.SprintNoiseRadius, NoiseKind.Footstep);
        }

        private void OnTraversalStarted(LedgeKind kind)
        {
            if (kind == LedgeKind.LadderExit) return;
            _audio.Value?.Play(PlayerCues.Mantle, transform.position, kind == LedgeKind.Vault ? 0.8f : 1f);
            Noise(FootstepMath.WalkNoiseRadius, NoiseKind.Footstep);
        }

        private void OnLadderRung()
        {
            _audio.Value?.Play(PlayerCues.Ladder, transform.position, 0.75f);
            Noise(FootstepMath.WalkNoiseRadius, NoiseKind.Footstep);
        }

        private void OnJumped()
        {
            PlaySurface(0.7f);
            Noise(FootstepMath.WalkNoiseRadius, NoiseKind.Footstep);
        }

        private void StartGear()
        {
            var audio = _audio.Value;
            if (audio == null) return;
            _gearHandle = audio.PlayLoop(PlayerCues.FoleyGear, transform);
            _gearOn = true;
        }

        private void StopGear(float fade)
        {
            if (!_gearOn) return;
            _gearOn = false;
            _audio.Value?.StopLoop(_gearHandle, fade);
        }

        private void Subscribe()
        {
            if (_subscribed || _motor == null) return;
            _subscribed = true;
            _motor.SlideStarted += OnSlideStarted;
            _motor.TraversalStarted += OnTraversalStarted;
            _motor.LadderRung += OnLadderRung;
            _motor.Jumped += OnJumped;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _motor == null) return;
            _subscribed = false;
            _motor.SlideStarted -= OnSlideStarted;
            _motor.TraversalStarted -= OnTraversalStarted;
            _motor.LadderRung -= OnLadderRung;
            _motor.Jumped -= OnJumped;
        }
    }
}
