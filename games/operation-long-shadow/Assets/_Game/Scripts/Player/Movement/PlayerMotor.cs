using System;
using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>
    /// CharacterController locomotion of the player (TECH_PLAN §7.3, GDD §12): walk/ADS walk/crouch/sprint, slide,
    /// jump with coyote time and buffering, auto-vault, climb on Space, air mantle, ladders, forced walk, un-crouch
    /// clearance, ground snapping, fall damage. Ticked by <see cref="PlayerRig"/> (no Update of its own) so the order
    /// look → motor → weapons is fixed. Allocation-free per frame.
    /// <para>Traversals and ladders move the transform directly without calling <c>Move</c> (the path is validated by
    /// <see cref="LedgeProbe"/>). The controller stays enabled so trigger volumes keep seeing the player (disabling a
    /// collider can skip OnTriggerExit and re-send OnTriggerEnter); it is only disabled while mounted and for teleports.
    /// Physics auto-sync is off (TECH_PLAN §3.9), so the end of a traversal and every teleport call
    /// <see cref="Physics.SyncTransforms"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private enum Mode { Ground, Air, Slide, Traversal, Ladder, Mounted }

        [SerializeField] private MotorSettings _settings = new MotorSettings();
        [SerializeField] private CharacterController _controller;

        private Mode _mode = Mode.Ground;
        private bool _initialised;
        private bool _crouched;
        private bool _sprinting;
        private Vector3 _planar;
        private float _vertical;
        private float _airMaxSpeed;
        private bool _grounded = true;
        private Vector3 _groundNormal = Vector3.up;
        private Collider _groundCollider;
        private float _timeSinceGrounded;
        private bool _coyoteArmed;
        private float _timeSinceJump = 10f;
        private float _jumpBuffer;
        private float _airApexY;
        private float _slideTime;
        private Vector3 _slideDir;
        private float _slideCooldown;
        private float _timeSinceSprintEnd = 10f;
        private TraversalPath _path;
        private float _traversalTime;
        private LedgeKind _traversalKind;
        private bool _traversalEndCrouched;
        private Vector3 _traversalExit;
        private LadderVolume _ladder;
        private Vector3 _ladderOut;
        private Vector3 _ladderLine;
        private Vector3 _ladderEnterFrom;
        private float _ladderEnter01;
        private float _ladderY;
        private float _rungAccumulator;
        private readonly List<LadderVolume> _ladders = new List<LadderVolume>(2);
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private bool _holsterHeld;
        private ServiceRef<IWeaponService> _weapons;

        /// <summary>Raised on landing with the downward impact speed in m/s (camera dip, view model, foley).</summary>
        public event Action<float> Landed;
        /// <summary>Raised on a damaging landing with (HP, fall height in m), GDD §12.</summary>
        public event Action<float, float> FallDamaged;
        /// <summary>Raised when a vault, climb, air mantle or ladder step-off starts.</summary>
        public event Action<LedgeKind> TraversalStarted;
        /// <summary>Raised when a traversal ends.</summary>
        public event Action TraversalEnded;
        /// <summary>Raised on a jump.</summary>
        public event Action Jumped;
        /// <summary>Raised when a slide starts.</summary>
        public event Action SlideStarted;
        /// <summary>Raised per ladder rung climbed (foley).</summary>
        public event Action LadderRung;

        /// <summary>Tunables (GDD §12 defaults).</summary>
        public MotorSettings Settings => _settings;
        /// <summary>The character controller.</summary>
        public CharacterController Controller => _controller;
        /// <summary>Current locomotion state.</summary>
        public Stance Stance { get; private set; }
        /// <summary>True when standing on walkable ground (ground or slide).</summary>
        public bool IsGrounded => _grounded && (_mode == Mode.Ground || _mode == Mode.Slide);
        /// <summary>Crouched capsule (crouch, slide).</summary>
        public bool IsCrouched => _crouched;
        /// <summary>Sprint state (input held and moving).</summary>
        public bool IsSprinting => Stance == Stance.Sprint;
        /// <summary>Measured world velocity in m/s (updated by <see cref="MeasureVelocity"/>).</summary>
        public Vector3 Velocity => _velocity;
        /// <summary>Measured horizontal speed in m/s.</summary>
        public float PlanarSpeed => Mathf.Sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
        /// <summary>Simulated horizontal velocity (what the motor is trying to do).</summary>
        public Vector3 SimulatedPlanarVelocity => _planar;
        /// <summary>Vertical velocity of the simulation in m/s.</summary>
        public float VerticalVelocity => _vertical;
        /// <summary>True during a vault, climb, air mantle or ladder step-off.</summary>
        public bool IsTraversing => _mode == Mode.Traversal;
        /// <summary>Kind of the running traversal (None otherwise).</summary>
        public LedgeKind TraversalKind => _mode == Mode.Traversal ? _traversalKind : LedgeKind.None;
        /// <summary>Progress of the running traversal 0..1.</summary>
        public float Traversal01 => _mode == Mode.Traversal ? Mathf.Clamp01(_traversalTime / _path.Duration) : 0f;
        /// <summary>True while sliding.</summary>
        public bool IsSliding => _mode == Mode.Slide;
        /// <summary>Slide progress 0..1.</summary>
        public float Slide01 => _mode == Mode.Slide && _settings.SlideDuration > 0f ? Mathf.Clamp01(_slideTime / _settings.SlideDuration) : 0f;
        /// <summary>True while on a ladder.</summary>
        public bool OnLadder => _mode == Mode.Ladder;
        /// <summary>True while mounted (motor idle).</summary>
        public bool IsMounted => _mode == Mode.Mounted;
        /// <summary>Seconds since the sprint stance ended (0 while sprinting).</summary>
        public float TimeSinceSprintEnd => _timeSinceSprintEnd;
        /// <summary>Collider under the feet (footstep surface), or null.</summary>
        public Collider GroundCollider => _groundCollider;
        /// <summary>Normal of the ground under the feet.</summary>
        public Vector3 GroundNormal => _groundNormal;
        /// <summary>Height of the feet at the highest point of the current airtime (fall damage reference).</summary>
        public float AirApexY => _airApexY;
        /// <summary>Fall height of the last landing in metres (apex to landing; telemetry, tests).</summary>
        public float LastFallHeight { get; private set; }
        /// <summary>Target eye height for the stance (GDD §12: 1.65 / 1.05; slide dips 0.6 m).</summary>
        public float EyeHeight => MovementMath.EyeHeight(Stance, _mode == Mode.Traversal ? _traversalEndCrouched : _crouched, _settings);
        /// <summary>Weapon move multiplier (GDD §13.1), 1 = none.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Forced walking speed in m/s (MID-A 1.2), 0 = none. Disables sprint, slide, jump and vault.</summary>
        public float ForcedWalkSpeed { get; set; }
        /// <summary>Ignores input (dead, scripted).</summary>
        public bool InputBlocked { get; set; }

        private float ClampedMultiplier => Mathf.Clamp(SpeedMultiplier, 0.1f, 2f);

        private void Awake() => Init();

        private void OnDisable() => ReleaseHolster();

        /// <summary>Wires the controller (PlayerRigFactory) and applies the capsule settings.</summary>
        internal void Bind(CharacterController controller)
        {
            _controller = controller;
            _initialised = false;
            Init();
        }

        private void Init()
        {
            if (_initialised) return;
            _initialised = true;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            ApplyControllerSettings();
            _lastPosition = transform.position;
            _airApexY = transform.position.y;
        }

        /// <summary>Writes radius, step offset, slope limit, skin and the stance height to the controller.</summary>
        public void ApplyControllerSettings()
        {
            var c = _controller;
            if (c == null) return;
            c.radius = _settings.Radius;
            c.stepOffset = _settings.StepOffset;
            c.slopeLimit = _settings.SlopeLimit;
            c.skinWidth = _settings.SkinWidth;
            c.minMoveDistance = 0f;
            SetCrouched(_crouched);
        }

        /// <summary>Advances the motor by one frame.</summary>
        public void Tick(in MotorInput input, float dt)
        {
            Init();
            if (dt <= 0f || _controller == null) return;
            _slideCooldown = Mathf.Max(0f, _slideCooldown - dt);
            _timeSinceJump += dt;
            _jumpBuffer = input.JumpDown && !InputBlocked ? _settings.JumpBufferTime : Mathf.Max(0f, _jumpBuffer - dt);

            switch (_mode)
            {
                case Mode.Mounted:
                    break;
                case Mode.Traversal:
                    TickTraversal(dt);
                    break;
                case Mode.Ladder:
                    TickLadder(in input, dt);
                    break;
                case Mode.Slide:
                    TickSlide(in input, dt);
                    break;
                case Mode.Air:
                    TickAir(in input, dt);
                    break;
                default:
                    TickGround(in input, dt);
                    break;
            }

            if (_mode == Mode.Air) _timeSinceGrounded += dt;
            else _timeSinceGrounded = 0f;
            UpdateStance(dt);
            UpdateHolster();
        }

        /// <summary>Measures the world velocity from the frame's displacement (call once per frame, after moving).</summary>
        public void MeasureVelocity(float dt)
        {
            var p = transform.position;
            if (dt > 0f) _velocity = (p - _lastPosition) / dt;
            _lastPosition = p;
        }

        // ------------------------------------------------------------------ ground

        private void TickGround(in MotorInput input, float dt)
        {
            var blocked = InputBlocked;
            var forced = ForcedWalkSpeed > 0f;
            var move = blocked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);

            if (TryAttachLadder(in input, move)) return;

            if (!blocked && input.CrouchDown)
            {
                if (_sprinting && !forced && _slideCooldown <= 0f && MovementMath.CanStartSlide(Flat(_planar).magnitude, _settings))
                {
                    StartSlide(input.Heading);
                    MoveCharacter(dt, -_settings.GroundStickSpeed * dt, true);
                    return;
                }
                if (_crouched) TryStand();
                else SetCrouched(true);
                _sprinting = false;
            }

            if (_jumpBuffer > 0f && !blocked && _timeSinceJump > 0.2f && (_grounded || _timeSinceGrounded <= _settings.CoyoteTime))
            {
                _jumpBuffer = 0f;
                if (_crouched)
                {
                    TryStand(); // Space stands up first (CoD)
                }
                else if (!forced)
                {
                    if (TryGroundLedge(MovementMath.Forward(input.Heading), true)) return;
                    Jump();
                    MoveCharacter(dt, MovementMath.IntegrateVertical(ref _vertical, _settings.Gravity, _settings.TerminalVelocity, dt), false);
                    return;
                }
            }

            var wantSprint = !blocked && !forced && input.Sprint && !input.Ads && MovementMath.IsSprintInput(move);
            if (wantSprint && _crouched && !TryStand()) wantSprint = false;
            _sprinting = wantSprint;

            var stance = _sprinting ? Stance.Sprint : (_crouched ? Stance.Crouch : Stance.Stand);
            var speed = MovementMath.GroundSpeed(_settings, stance, input.AdsBlend, SpeedMultiplier, ForcedWalkSpeed) *
                        MovementMath.DirectionalFactor(move, _settings.BackpedalFactor);
            var wish = MovementMath.WishDirection(move, input.Heading);
            _planar = MovementMath.Accelerate(_planar, wish * speed, _settings, SpeedMultiplier, dt);

            // Auto-vault: moving into a 0.6–1.4 m ledge (GDD §12).
            if (!blocked && !forced && _grounded && move.y > 0.3f && _planar.sqrMagnitude > 0.5f)
            {
                var dir = wish.sqrMagnitude > 1e-4f ? wish.normalized : MovementMath.Forward(input.Heading);
                if (LedgeProbe.FaceAhead(transform.position, dir, 0.45f, _settings.GroundLedgeReach + 0.05f) && TryGroundLedge(dir, false))
                    return;
            }

            MoveCharacter(dt, -_settings.GroundStickSpeed * dt, true);
        }

        // ------------------------------------------------------------------ air

        private void TickAir(in MotorInput input, float dt)
        {
            var blocked = InputBlocked;
            var move = blocked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);

            // Coyote jump: Space shortly after walking (not jumping) off an edge still jumps.
            if (!blocked && _coyoteArmed && _jumpBuffer > 0f && _timeSinceGrounded <= _settings.CoyoteTime && ForcedWalkSpeed <= 0f &&
                !_crouched)
            {
                _jumpBuffer = 0f;
                Jump();
                MoveCharacter(dt, MovementMath.IntegrateVertical(ref _vertical, _settings.Gravity, _settings.TerminalVelocity, dt), false);
                return;
            }

            // Air mantle (GDD §12): ledge top 0–1.4 m above the feet, <= 0.8 m ahead.
            if (!blocked && ForcedWalkSpeed <= 0f && _timeSinceJump > 0.06f && (move.y > 0.3f || _jumpBuffer > 0f))
            {
                var facing = MovementMath.Forward(input.Heading);
                var feet = transform.position;
                var reach = _settings.AirMantleReach + 0.05f;
                if ((LedgeProbe.FaceAhead(feet, facing, 0.05f, reach) || LedgeProbe.FaceAhead(feet, facing, 0.9f, reach)) && TryAirMantle(facing))
                    return;
            }

            var wish = MovementMath.WishDirection(move, input.Heading);
            _planar = MovementMath.AirAccelerate(_planar, wish, _airMaxSpeed, _settings.AirAcceleration, dt);
            var dy = MovementMath.IntegrateVertical(ref _vertical, _settings.Gravity, _settings.TerminalVelocity, dt);
            MoveCharacter(dt, dy, false);
            if (_mode == Mode.Air) _airApexY = Mathf.Max(_airApexY, transform.position.y);
        }

        private void Jump()
        {
            _vertical = MovementMath.JumpVelocity(_settings.JumpApexHeight, _settings.Gravity);
            _mode = Mode.Air;
            _grounded = false;
            _coyoteArmed = false;
            _timeSinceJump = 0f;
            _airApexY = transform.position.y;
            _airMaxSpeed = Mathf.Max(Flat(_planar).magnitude, _settings.WalkSpeed * ClampedMultiplier);
            Jumped?.Invoke();
        }

        private void Land()
        {
            var impact = Mathf.Max(0f, -_vertical);
            var fall = _airApexY - transform.position.y;
            _mode = Mode.Ground;
            _coyoteArmed = false;
            LastFallHeight = fall;
            _vertical = -_settings.GroundStickSpeed;
            if (impact > 8f) _planar *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(8f, 16f, impact)); // hard landings bleed speed
            Landed?.Invoke(impact);
            var damage = MovementMath.FallDamage(fall, _settings);
            if (damage > 0f) FallDamaged?.Invoke(damage, fall);
        }

        // ------------------------------------------------------------------ slide

        private void StartSlide(float heading)
        {
            var planar = Flat(_planar);
            _slideDir = planar.sqrMagnitude > 0.01f ? planar.normalized : MovementMath.Forward(heading);
            _mode = Mode.Slide;
            _slideTime = 0f;
            _sprinting = false;
            SetCrouched(true);
            _planar = _slideDir * MovementMath.SlideSpeed(_settings, 0f);
            SlideStarted?.Invoke();
        }

        private void TickSlide(in MotorInput input, float dt)
        {
            var blocked = InputBlocked;
            var move = blocked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);
            _slideTime += dt;

            if (!blocked && _jumpBuffer > 0f && CanStand())
            {
                // Slide-jump keeps the slide's momentum.
                _jumpBuffer = 0f;
                SetCrouched(false);
                _slideCooldown = _settings.SlideCooldown;
                Jump();
                MoveCharacter(dt, MovementMath.IntegrateVertical(ref _vertical, _settings.Gravity, _settings.TerminalVelocity, dt), false);
                return;
            }

            var wish = MovementMath.WishDirection(move, input.Heading);
            if (wish.sqrMagnitude > 0.04f)
                _slideDir = Vector3.RotateTowards(_slideDir, wish.normalized, _settings.SlideSteerDegreesPerSecond * Mathf.Deg2Rad * dt, 0f);
            _planar = _slideDir * MovementMath.SlideSpeed(_settings, _slideTime);
            MoveCharacter(dt, -_settings.GroundStickSpeed * dt, true);
            if (_mode != Mode.Slide) return; // slid off an edge

            if ((_slideTime > 0.1f && Flat(_planar).magnitude < 1f) || _slideTime >= _settings.SlideDuration) EndSlide(in input);
        }

        private void EndSlide(in MotorInput input)
        {
            _mode = Mode.Ground;
            _slideCooldown = _settings.SlideCooldown;
            if (!InputBlocked && ForcedWalkSpeed <= 0f && input.Sprint && MovementMath.IsSprintInput(input.Move) && CanStand())
            {
                SetCrouched(false);
                _sprinting = true;
            }
        }

        // ------------------------------------------------------------------ vault / climb / mantle

        private LedgeQuery BaseQuery(Vector3 dir) => new LedgeQuery
        {
            Feet = transform.position,
            Forward = dir,
            Radius = _settings.Radius,
            StandHeight = _settings.StandHeight,
            CrouchHeight = _settings.CrouchHeight,
            SlopeLimit = _settings.SlopeLimit,
            OverVaultMaxThickness = _settings.OverVaultMaxThickness,
        };

        private bool TryGroundLedge(Vector3 dir, bool allowClimb)
        {
            var q = BaseQuery(dir);
            q.MinTop = _settings.VaultMinHeight;
            q.MaxTop = allowClimb ? _settings.ClimbMaxHeight : _settings.VaultMaxHeight;
            q.MaxFaceDistance = _settings.GroundLedgeReach;
            q.AllowOverVault = true;
            q.StepOffset = _settings.StepOffset;
            if (!LedgeProbe.Probe(in q, out var info)) return false;
            var kind = MovementMath.ClassifyGroundLedge(info.TopHeight, _settings);
            if (kind == LedgeKind.None || (kind == LedgeKind.Climb && info.OverVault)) return false;
            StartTraversal(kind, in info);
            return true;
        }

        private bool TryAirMantle(Vector3 dir)
        {
            var q = BaseQuery(dir);
            q.MinTop = _settings.AirMantleMinAboveFeet;
            q.MaxTop = _settings.AirMantleMaxAboveFeet;
            q.MaxFaceDistance = _settings.AirMantleReach;
            q.AllowOverVault = false;
            q.StepOffset = 0f;
            if (!LedgeProbe.Probe(in q, out var info)) return false;
            if (!MovementMath.IsAirMantle(info.TopHeight, info.FaceDistance, _settings)) return false;
            StartTraversal(LedgeKind.AirMantle, in info);
            return true;
        }

        private void StartTraversal(LedgeKind kind, in LedgeInfo info)
        {
            var duration = MovementMath.TraversalDuration(kind, _settings);
            _path = TraversalPath.Create(transform.position, info.Edge, info.End, info.Forward, _settings.Radius, duration,
                                         MovementMath.TraversalRiseFraction(kind));
            _traversalKind = kind;
            _traversalTime = 0f;
            _traversalEndCrouched = info.EndCrouched;
            var entry = Flat(_planar).magnitude;
            float exit;
            switch (kind)
            {
                case LedgeKind.Vault:
                    exit = info.OverVault ? Mathf.Max(entry * 0.85f, _settings.WalkSpeed * 0.5f) : Mathf.Min(entry, _settings.WalkSpeed) * 0.5f;
                    break;
                case LedgeKind.AirMantle:
                    exit = Mathf.Min(entry, 1.5f);
                    break;
                default:
                    exit = 0f;
                    break;
            }
            _traversalExit = info.Forward * exit;
            _mode = Mode.Traversal;
            _planar = Vector3.zero;
            _vertical = 0f;
            _sprinting = false;
            _grounded = false;
            TraversalStarted?.Invoke(kind);
        }

        private void TickTraversal(float dt)
        {
            _traversalTime += dt;
            var t = Mathf.Clamp01(_traversalTime / _path.Duration);
            transform.position = _path.Evaluate(t);
            if (t >= 1f) EndTraversal();
        }

        private void EndTraversal()
        {
            SetCrouched(_traversalEndCrouched);
            _controller.enabled = true;
            Physics.SyncTransforms();
            _mode = Mode.Ground;
            _planar = _traversalExit;
            _vertical = -_settings.GroundStickSpeed;
            _grounded = true;
            _airApexY = transform.position.y;
            TraversalEnded?.Invoke();
        }

        // ------------------------------------------------------------------ ladders

        private bool TryAttachLadder(in MotorInput input, Vector2 move)
        {
            if (_ladders.Count == 0 || InputBlocked || ForcedWalkSpeed > 0f) return false;
            var feet = transform.position;
            var facing = MovementMath.Forward(input.Heading);
            for (var i = _ladders.Count - 1; i >= 0; i--)
            {
                var l = _ladders[i];
                if (l == null || !l.isActiveAndEnabled || !l.Contains(feet + Vector3.up * 0.9f))
                {
                    _ladders.RemoveAt(i);
                    continue;
                }
                var outDir = LadderOut(l, feet);
                var atTop = feet.y >= l.Top.y - 0.35f;
                if (!atTop)
                {
                    if (move.y <= 0.3f || Vector3.Dot(facing, -outDir) < 0.5f) continue;
                }
                else
                {
                    var wish = MovementMath.WishDirection(move, input.Heading);
                    if (wish.sqrMagnitude < 0.09f || Vector3.Dot(wish.normalized, outDir) < 0.5f || input.Pitch > -25f) continue;
                }
                AttachLadder(l, outDir, atTop);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Horizontal direction from the ladder toward the climber. Convention for level builders (WP10): the
        /// LadderVolume's forward axis points away from the wall; if it is vertical, the direction from the bottom exit
        /// point to the player is used.
        /// </summary>
        private static Vector3 LadderOut(LadderVolume l, Vector3 feet)
        {
            var f = l.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) return f.normalized;
            var d = feet - l.Bottom;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
        }

        private void AttachLadder(LadderVolume ladder, Vector3 outDir, bool fromTop)
        {
            _ladder = ladder;
            _ladderOut = outDir;
            var bottom = ladder.Bottom;
            _ladderLine = new Vector3(bottom.x, 0f, bottom.z);
            _ladderEnterFrom = transform.position;
            _ladderY = fromTop ? Mathf.Max(bottom.y, ladder.Top.y - 0.6f) : Mathf.Clamp(transform.position.y, bottom.y, ladder.Top.y);
            _ladderEnter01 = 0f;
            _mode = Mode.Ladder;
            _planar = Vector3.zero;
            _vertical = 0f;
            _sprinting = false;
            _grounded = false;
            _rungAccumulator = 0f;
            if (_crouched) SetCrouched(false);
        }

        private void TickLadder(in MotorInput input, float dt)
        {
            var l = _ladder;
            if (l == null || !l.isActiveAndEnabled)
            {
                DetachLadder(Vector3.zero, 0f);
                return;
            }
            var blocked = InputBlocked;
            if (!blocked && _jumpBuffer > 0f)
            {
                _jumpBuffer = 0f;
                DetachLadder(_ladderOut * 2.5f, 2.5f); // push off backwards
                return;
            }

            var move = blocked ? 0f : Mathf.Clamp(input.Move.y, -1f, 1f);
            var sign = input.Pitch < _settings.LadderLookDownPitch ? -1f : 1f;
            var climb = move * sign * l.ClimbSpeed; // TECH_PLAN §7.3: 2.2 m/s
            var top = l.Top;
            var bottom = l.Bottom;
            var y = _ladderY + climb * dt;

            _rungAccumulator += Mathf.Abs(climb * dt);
            if (_rungAccumulator >= _settings.LadderRungSpacing)
            {
                _rungAccumulator -= _settings.LadderRungSpacing;
                LadderRung?.Invoke();
            }

            if (climb > 0f && y >= top.y - 0.45f)
            {
                // Hands at the top: step off onto the platform.
                var toPlatform = -_ladderOut;
                var edge = new Vector3(_ladderLine.x, top.y, _ladderLine.z) + toPlatform * 0.45f;
                var info = new LedgeInfo
                {
                    Start = transform.position,
                    Edge = edge,
                    End = top,
                    Forward = toPlatform,
                    TopHeight = top.y - transform.position.y,
                    EndCrouched = !LedgeProbe.Fits(top, _settings.StandHeight, _settings.Radius * 0.9f),
                };
                _ladder = null;
                StartTraversal(LedgeKind.LadderExit, in info);
                return;
            }
            if (climb < 0f && y <= bottom.y)
            {
                transform.position = new Vector3(_ladderLine.x, bottom.y, _ladderLine.z);
                DetachLadder(Vector3.zero, 0f);
                return;
            }

            _ladderY = Mathf.Clamp(y, bottom.y, top.y);
            var target = new Vector3(_ladderLine.x, _ladderY, _ladderLine.z);
            if (_ladderEnter01 < 1f)
            {
                _ladderEnter01 = Mathf.Min(1f, _ladderEnter01 + dt / 0.25f);
                var s = _ladderEnter01 * _ladderEnter01 * (3f - 2f * _ladderEnter01);
                target = Vector3.Lerp(_ladderEnterFrom, target, s);
            }
            transform.position = target;
        }

        private void DetachLadder(Vector3 push, float upward)
        {
            _ladder = null;
            _controller.enabled = true;
            Physics.SyncTransforms();
            _planar = push;
            _vertical = upward;
            _airApexY = transform.position.y;
            _airMaxSpeed = Mathf.Max(push.magnitude, _settings.WalkSpeed * ClampedMultiplier);
            if (upward > 0f)
            {
                _mode = Mode.Air;
                _grounded = false;
                _timeSinceJump = 0f;
            }
            else
            {
                _mode = Mode.Ground;
                _vertical = -_settings.GroundStickSpeed;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out LadderVolume ladder) && !_ladders.Contains(ladder)) _ladders.Add(ladder);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out LadderVolume ladder)) _ladders.Remove(ladder);
        }

        // ------------------------------------------------------------------ controller

        private void MoveCharacter(float dt, float verticalDisplacement, bool allowSnap)
        {
            var before = transform.position;
            var wasGrounded = _grounded;
            var step = _planar * dt;
            var flags = _controller.Move(new Vector3(step.x, verticalDisplacement, step.z));
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;

            var probe = ProbeGround(out var hit);
            _grounded = _vertical <= 0.01f && ((flags & CollisionFlags.Below) != 0 || probe);
            if (probe)
            {
                _groundNormal = hit.normal;
                _groundCollider = hit.collider;
            }
            if (!_grounded && wasGrounded && allowSnap && _vertical <= 0.01f) TrySnapDown();
            if (!_grounded) _groundCollider = null;

            // Keep the simulated velocity honest when something blocks the move (walls, AI bodies).
            var moved = transform.position - before;
            moved.y = 0f;
            var speed = Flat(_planar).magnitude;
            if (speed > 1e-3f && dt > 0f)
            {
                var actual = moved.magnitude / dt;
                if (actual < speed * 0.95f) _planar = actual > 1e-3f ? moved / dt : Vector3.zero;
            }

            if (_grounded)
            {
                if (_mode == Mode.Air) Land();
            }
            else if (_mode == Mode.Ground || _mode == Mode.Slide)
            {
                // Walked or slid off an edge.
                if (_mode == Mode.Slide) _slideCooldown = _settings.SlideCooldown;
                _coyoteArmed = true;
                _mode = Mode.Air;
                _airApexY = transform.position.y;
                _airMaxSpeed = Mathf.Max(speed, _settings.WalkSpeed * ClampedMultiplier);
                if (_vertical < 0f) _vertical = 0f;
            }
        }

        private bool ProbeGround(out RaycastHit hit)
        {
            var r = _controller.radius;
            var origin = transform.position + Vector3.up * (r + 0.05f);
            return Physics.SphereCast(origin, r * 0.9f, Vector3.down, out hit, 0.12f, PlayerLayers.Walkable, QueryTriggerInteraction.Ignore) &&
                   MovementMath.IsWalkable(hit.normal, _settings.SlopeLimit);
        }

        private void TrySnapDown()
        {
            var r = _controller.radius;
            var origin = transform.position + Vector3.up * (r + 0.05f);
            if (!Physics.SphereCast(origin, r * 0.95f, Vector3.down, out var hit, _settings.GroundSnapDistance + 0.05f, PlayerLayers.Walkable,
                    QueryTriggerInteraction.Ignore) || !MovementMath.IsWalkable(hit.normal, _settings.SlopeLimit))
                return;
            var drop = hit.distance - 0.03f;
            if (drop > 0.001f) _controller.Move(Vector3.down * drop);
            _grounded = true;
            _groundNormal = hit.normal;
            _groundCollider = hit.collider;
        }

        private void SetCrouched(bool crouched)
        {
            _crouched = crouched;
            if (_controller == null) return;
            var h = MovementMath.CapsuleHeight(crouched, _settings);
            _controller.height = h;
            _controller.center = new Vector3(0f, h * 0.5f, 0f);
        }

        /// <summary>True if the standing capsule fits here (un-crouch clearance, TECH_PLAN §7.3).</summary>
        public bool CanStand() => LedgeProbe.Fits(transform.position, _settings.StandHeight, _settings.Radius * 0.95f);

        private bool TryStand()
        {
            if (!_crouched) return true;
            if (!CanStand()) return false;
            SetCrouched(false);
            return true;
        }

        private void UpdateStance(float dt)
        {
            Stance s;
            switch (_mode)
            {
                case Mode.Mounted: s = Stance.Mounted; break;
                case Mode.Traversal: s = Stance.Mantle; break;
                case Mode.Ladder: s = Stance.Ladder; break;
                case Mode.Slide: s = Stance.Slide; break;
                case Mode.Air: s = Stance.Airborne; break;
                default:
                    s = _crouched ? Stance.Crouch : (_sprinting && Flat(_planar).sqrMagnitude > 1f ? Stance.Sprint : Stance.Stand);
                    break;
            }
            Stance = s;
            _timeSinceSprintEnd = s == Stance.Sprint ? 0f : _timeSinceSprintEnd + dt;
        }

        private void UpdateHolster()
        {
            // Ladders and the big climbs lower the weapon; the 0.45 s vault keeps it up (TECH_PLAN §7.5 SetHolstered).
            var want = _mode == Mode.Ladder || (_mode == Mode.Traversal && _traversalKind != LedgeKind.Vault);
            if (want == _holsterHeld) return;
            _holsterHeld = want;
            _weapons.Value?.SetHolstered(this, want);
        }

        private void ReleaseHolster()
        {
            if (!_holsterHeld) return;
            _holsterHeld = false;
            _weapons.Value?.SetHolstered(this, false);
        }

        // ------------------------------------------------------------------ external control

        /// <summary>Moves the player instantly and resets every movement state (checkpoints, dev).</summary>
        public void Teleport(Vector3 position)
        {
            Init();
            var wasMounted = _mode == Mode.Mounted;
            _ladder = null;
            _ladders.Clear();
            _controller.enabled = false;
            transform.position = position;
            if (!wasMounted) _controller.enabled = true;
            Physics.SyncTransforms();
            _planar = Vector3.zero;
            _vertical = 0f;
            _velocity = Vector3.zero;
            _lastPosition = position;
            _airApexY = position.y;
            _jumpBuffer = 0f;
            _coyoteArmed = false;
            _sprinting = false;
            if (!wasMounted)
            {
                _mode = Mode.Ground;
                _grounded = true;
                if (_crouched && CanStand()) SetCrouched(false);
            }
        }

        /// <summary>Enters or leaves the mounted state (PlayerMount): the controller is disabled while mounted.</summary>
        public void SetMounted(bool mounted)
        {
            Init();
            if (mounted == (_mode == Mode.Mounted)) return;
            _ladder = null;
            _planar = Vector3.zero;
            _vertical = 0f;
            _sprinting = false;
            _coyoteArmed = false;
            if (mounted)
            {
                _mode = Mode.Mounted;
                SetCrouched(false);
                _controller.enabled = false;
            }
            else
            {
                _controller.enabled = true;
                Physics.SyncTransforms();
                _mode = Mode.Ground;
                _grounded = true;
                _airApexY = transform.position.y;
                _lastPosition = transform.position;
            }
        }

        /// <summary>Clears transient state after a respawn (keeps the position).</summary>
        public void ResetState()
        {
            _planar = Vector3.zero;
            _vertical = 0f;
            _jumpBuffer = 0f;
            _sprinting = false;
            _airApexY = transform.position.y;
            _lastPosition = transform.position;
            _velocity = Vector3.zero;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
