using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Player.Movement;
using LongShadow.Player.Vitals;
using UnityEngine;

namespace LongShadow.Player.Dev
{
    /// <summary>
    /// Dev-only movement and health telemetry for the WP02 test course (TECH_PLAN §11.4 WP02 DoD: "logged speeds within
    /// ±5 %"). Measures steady ground speeds per stance (after 0.6 s settled, averaged over 0.75 s), jump apex, vault /
    /// climb / air-mantle / ladder-exit durations, slide entry/exit speeds and duration, ladder climb speed, fall damage
    /// against GDD §12, and regeneration delay/rate against the GDD §16 row in effect. Each result is logged as
    /// "[LS:Player] Telemetry Speed.Sprint 6.88 (target 6.90, -0.3 %) PASS" and kept in <see cref="Results"/>.
    /// Logging allocates (strings); add it only in dev scenes (the course builder does).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementTelemetry : MonoBehaviour
    {
        [SerializeField] private PlayerRig _rig;
        [SerializeField, Tooltip("WP02 DoD: within ±5 %.")] private float _tolerancePercent = 5f;
        [SerializeField] private float _settleSeconds = 0.6f;
        [SerializeField] private float _windowSeconds = 0.75f;
        [SerializeField] private bool _log = true;

        private readonly List<TelemetryResult> _results = new List<TelemetryResult>(64);
        private PlayerMotor _motor;
        private PlayerHealth _health;
        private bool _subscribed;

        private Stance _runStance;
        private float _runTime;
        private float _speedSum;
        private int _speedSamples;
        private float _windowTime;
        private bool _runReported;
        private float _runTarget;

        private bool _jumpTracking;
        private float _jumpStartY;
        private float _jumpMaxY;
        private bool _jumpInterrupted;

        private float _traversalStart = -1f;
        private LedgeKind _traversalKind;

        private float _slideStart = -1f;
        private bool _slideFirstFrame;

        private float _ladderTime;
        private float _ladderRise;
        private float _ladderLastY;

        private float _lastDamageTime = -1f;
        private float _regenStartTime = -1f;
        private float _regenStartHp;
        private float _lastHp;
        private bool _regenReported = true;

        /// <summary>Every result so far.</summary>
        public IReadOnlyList<TelemetryResult> Results => _results;

        /// <summary>Number of failed results.</summary>
        public int Failures
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _results.Count; i++) if (!_results[i].Pass) n++;
                return n;
            }
        }

        /// <summary>Records a result (also used by <see cref="PlayerDevHarness"/> for the timed speed gates).</summary>
        public TelemetryResult Report(string metric, float measured, float target, float tolerancePercent = -1f)
        {
            var tol = tolerancePercent >= 0f ? tolerancePercent : _tolerancePercent;
            var r = new TelemetryResult
            {
                Metric = metric,
                Measured = measured,
                Target = target,
                TolerancePercent = tol,
                Pass = target == 0f ? Mathf.Abs(measured) < 1e-3f : Mathf.Abs(measured - target) <= Mathf.Abs(target) * tol / 100f,
            };
            _results.Add(r);
            if (_log)
            {
                var line = "Telemetry " + r;
                if (r.Pass) LSLog.Info(LogCategory.Player, line, this);
                else LSLog.Warn(LogCategory.Player, line, this);
            }
            return r;
        }

        /// <summary>Clears the results.</summary>
        public void Clear() => _results.Clear();

        private void Awake()
        {
            if (_rig == null) _rig = GetComponent<PlayerRig>();
        }

        private void OnEnable()
        {
            if (_rig == null) return;
            _motor = _rig.Motor;
            _health = _rig.Health;
            if (_motor == null || _subscribed) return;
            _subscribed = true;
            _motor.Jumped += OnJumped;
            _motor.Landed += OnLanded;
            _motor.TraversalStarted += OnTraversalStarted;
            _motor.TraversalEnded += OnTraversalEnded;
            _motor.SlideStarted += OnSlideStarted;
            _motor.FallDamaged += OnFallDamaged;
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (!_subscribed || _motor == null) return;
            _subscribed = false;
            _motor.Jumped -= OnJumped;
            _motor.Landed -= OnLanded;
            _motor.TraversalStarted -= OnTraversalStarted;
            _motor.TraversalEnded -= OnTraversalEnded;
            _motor.SlideStarted -= OnSlideStarted;
            _motor.FallDamaged -= OnFallDamaged;
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void LateUpdate()
        {
            if (_motor == null) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            TrackSteadySpeed(dt);
            TrackJump();
            TrackSlide();
            TrackLadder(dt);
            TrackRegen();
        }

        private void TrackSteadySpeed(float dt)
        {
            var stance = _motor.Stance;
            var speed = _motor.PlanarSpeed;
            var steadyStance = (stance == Stance.Stand || stance == Stance.Crouch || stance == Stance.Sprint) && _motor.IsGrounded;
            if (stance != _runStance || !steadyStance || speed < 0.5f)
            {
                _runStance = stance;
                _runTime = 0f;
                _speedSum = 0f;
                _speedSamples = 0;
                _windowTime = 0f;
                _runReported = false;
                return;
            }
            _runTime += dt;
            if (_runReported || _runTime < _settleSeconds) return;

            var target = TargetSpeed(stance);
            if (_speedSamples == 0) _runTarget = target;
            if (Mathf.Abs(target - _runTarget) > 0.05f || speed < target * 0.5f)
            {
                // Input or ADS changed mid-window, or blocked by a wall: start the window again.
                _speedSum = 0f;
                _speedSamples = 0;
                _windowTime = 0f;
                return;
            }
            _speedSum += speed;
            _speedSamples++;
            _windowTime += dt;
            if (_windowTime < _windowSeconds) return;
            _runReported = true;
            Report("Speed." + stance + (_rig.AdsBlend > 0.5f ? ".ADS" : ""), _speedSum / _speedSamples, _runTarget);
        }

        private float TargetSpeed(Stance stance)
        {
            var s = _motor.Settings;
            var v = MovementMath.GroundSpeed(s, stance, _rig.AdsBlend, _motor.SpeedMultiplier, _motor.ForcedWalkSpeed);
            var local = transform.InverseTransformDirection(_motor.Velocity);
            var dir = new Vector2(local.x, local.z);
            return v * MovementMath.DirectionalFactor(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up, s.BackpedalFactor);
        }

        private void OnJumped()
        {
            _jumpTracking = true;
            _jumpInterrupted = false;
            _jumpStartY = transform.position.y;
            _jumpMaxY = _jumpStartY;
        }

        private void TrackJump()
        {
            if (!_jumpTracking) return;
            _jumpMaxY = Mathf.Max(_jumpMaxY, transform.position.y);
            if (_motor.IsTraversing || _motor.OnLadder) _jumpInterrupted = true;
        }

        private void OnLanded(float impact)
        {
            if (_jumpTracking && !_jumpInterrupted && _jumpMaxY - _jumpStartY > 0.2f)
                Report("Jump.Apex", _jumpMaxY - _jumpStartY, _motor.Settings.JumpApexHeight);
            _jumpTracking = false;
            var fall = _motor.LastFallHeight;
            if (fall >= 3f && fall <= _motor.Settings.SafeFallHeight)
                Report("Fall.Damage@" + fall.ToString("0.0") + "m", 0f, 0f, 0f);
        }

        private void OnFallDamaged(float amount, float height) =>
            Report("Fall.Damage@" + height.ToString("0.0") + "m", amount, MovementMath.FallDamage(height, _motor.Settings), 1f);

        private void OnTraversalStarted(LedgeKind kind)
        {
            _traversalStart = Time.time;
            _traversalKind = kind;
        }

        private void OnTraversalEnded()
        {
            if (_traversalStart < 0f) return;
            Report(_traversalKind + ".Duration", Time.time - _traversalStart, MovementMath.TraversalDuration(_traversalKind, _motor.Settings));
            _traversalStart = -1f;
        }

        private void OnSlideStarted()
        {
            _slideStart = Time.time;
            _slideFirstFrame = true;
        }

        private void TrackSlide()
        {
            if (_slideStart < 0f) return;
            var s = _motor.Settings;
            if (_motor.IsSliding)
            {
                if (!_slideFirstFrame) return;
                _slideFirstFrame = false;
                Report("Slide.StartSpeed", _motor.SimulatedPlanarVelocity.magnitude, s.SlideStartSpeed);
                return;
            }
            var duration = Time.time - _slideStart;
            _slideStart = -1f;
            if (duration < s.SlideDuration * 0.8f) return; // cut short by a jump or a wall
            Report("Slide.Duration", duration, s.SlideDuration);
        }

        private void TrackLadder(float dt)
        {
            if (!_motor.OnLadder)
            {
                if (_ladderTime > 0.5f) Report("Ladder.ClimbSpeed", _ladderRise / _ladderTime, 2.2f); // TECH_PLAN §7.3
                _ladderTime = 0f;
                _ladderRise = 0f;
                _ladderLastY = transform.position.y;
                return;
            }
            var y = transform.position.y;
            var dy = Mathf.Abs(y - _ladderLastY);
            _ladderLastY = y;
            if (dy / dt < 0.5f) return; // holding still on the rungs
            _ladderTime += dt;
            _ladderRise += dy;
        }

        private void OnDamaged(DamageInfo d)
        {
            _lastDamageTime = Time.time;
            _regenStartTime = -1f;
            _regenReported = false;
        }

        private void TrackRegen()
        {
            if (_health == null) return;
            var hp = _health.Current;
            if (!_regenReported && _health.IsAlive && _lastDamageTime >= 0f)
            {
                if (_regenStartTime < 0f && hp > _lastHp + 1e-3f)
                {
                    _regenStartTime = Time.time;
                    _regenStartHp = _lastHp;
                    Report("Regen.Delay." + _health.Profile.Level, _regenStartTime - _lastDamageTime, _health.Profile.RegenDelay);
                }
                else if (_regenStartTime >= 0f && hp >= _health.Max - 1e-3f)
                {
                    var t = Time.time - _regenStartTime + Time.deltaTime;
                    if (t > 0.25f) Report("Regen.Rate." + _health.Profile.Level, (_health.Max - _regenStartHp) / t, _health.Profile.RegenRate);
                    _regenReported = true;
                }
            }
            _lastHp = hp;
        }
    }
}
