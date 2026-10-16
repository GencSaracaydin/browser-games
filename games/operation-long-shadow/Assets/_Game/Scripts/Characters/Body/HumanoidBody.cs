using System;
using LongShadow.Characters.Animation;
using LongShadow.Characters.IK;
using LongShadow.Characters.Ragdoll;
using LongShadow.Characters.Shadow;
using LongShadow.Characters.Weapons;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.Characters.Body
{
    /// <summary>
    /// <see cref="ICharacterBody"/> of every animated humanoid (TECH_PLAN §7.8): NavMeshAgent movement with in-place,
    /// speed-matched clips and its own turn rate (updateRotation off), stances and cover poses, actions from
    /// <see cref="ActionTable"/> in two independent slots (a full-body action on the Base layer and an arms action on the
    /// Arms layer, so a seated soldier can key a handset), timed fallbacks so scripts never hang, the held weapon,
    /// aim/look/left-hand IK via <see cref="BodyIK"/>, hit reactions, death clip → ragdoll after 0.2 s (GDD §14.3), corpse
    /// cap and fade, animation LOD and the animation events of <see cref="AnimEventIds"/>. Travelling clips (vault, drop,
    /// skid drop, deaths) move the body with their root motion. Without an agent (booth, cinematics) <see cref="MoveTo"/>
    /// walks in a straight line. Survives pooling: re-enabling a dead body revives it.
    /// <para>Cross-assembly hooks without types: <c>SendMessage("PlayFireRecoil")</c>, <c>SendMessage("SetPerch", bool)</c>.</para>
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Squad + 10)]
    [DisallowMultipleComponent]
    public sealed class HumanoidBody : MonoBehaviour, ICharacterBody, ICorpse
    {
        [SerializeField] private BodyRole _role = BodyRole.Enemy;
        [SerializeField] private Animator _animator;
        [SerializeField] private BodyRig _rig;
        [SerializeField] private BodyIK _ik;
        [SerializeField] private BodyWeaponHolder _weapon;
        [SerializeField] private RagdollController _ragdoll;
        [SerializeField] private AnimEventRelay _events;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField, Tooltip("Weapon shown on spawn (WP04 replaces it with SetWeaponVisual).")]
        private string _defaultWeaponId;
        [Header("Death")]
        [SerializeField, Tooltip("GDD §14.3: the death clip blends into a ragdoll after 0.2 s.")]
        private float _ragdollDelay = 0.2f;
        [SerializeField, Tooltip("TECH_PLAN §7.8: corpses fade after 30 s.")]
        private float _corpseSeconds = 30f;
        [SerializeField] private float _sinkSeconds = 2.5f;
        [Header("Locomotion")]
        [SerializeField, Tooltip("Fastest clip speed in the locomotion tree (m/s), written by the controller builder.")]
        private float _maxNativeSpeed = 6.0f;
        [SerializeField, Tooltip("Acceleration of the agent-less straight-line mover (m/s²).")]
        private float _kinematicAcceleration = 6f;
        [SerializeField, Tooltip("Arms layer blend time (s).")]
        private float _armsBlend = 0.2f;
        [SerializeField, Tooltip("Hit reactions closer than this are merged (s).")]
        private float _hitCooldown = 0.25f;
        [SerializeField, Tooltip("Seconds without an animator state before an action's procedural gesture stands in for the clip.")]
        private float _gestureFallbackDelay = 0.25f;

        /// <inheritdoc/>
        public event Action<string> AnimEvent;

        private readonly LocomotionDriver _loco = new LocomotionDriver();
        private readonly FootstepClock _footsteps = new FootstepClock();
        private readonly ActionSlot _base = new ActionSlot();
        private readonly ActionSlot _arms = new ActionSlot();
        private readonly ActionSlot _proc = new ActionSlot();
        private Health _health;
        private int _baseLayer = -1, _armsLayer = -1, _hitsLayer = -1, _handsLayer = -1;
        private bool _unscaled;

        // movement
        private Vector3 _destination;
        private bool _hasDestination;
        private float _moveSpeed;
        private Vector3 _kinematicVelocity;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private Stance _stance = Stance.Stand;
        private Vector3? _aimTarget;
        private int _coverPose;
        private bool _perch;
        private Vector3? _faceDirection;
        private float _armsWeight;
        private bool _stowed;

        // death
        private bool _dead;
        private float _diedAt;
        private float _deathTime;
        private bool _ragdolled;
        private DamageInfo _deathInfo;
        private bool _evicted;
        private float _sinkTime;

        // misc
        private int _lod;
        private float _lastHitTime = -10f;
        private float _lastClipFootEvent = -10f;
        private bool _initialised;

        /// <summary>Body role.</summary>
        public BodyRole Role => _role;
        /// <summary>The rig.</summary>
        public BodyRig Rig => _rig;
        /// <summary>The IK pass.</summary>
        public BodyIK IK => _ik;
        /// <summary>The weapon holder.</summary>
        public BodyWeaponHolder WeaponHolder => _weapon;
        /// <summary>The Animator.</summary>
        public Animator Animator => _animator;
        /// <summary>True after <see cref="Die"/>.</summary>
        public bool IsDead => _dead;
        /// <summary>Current stance.</summary>
        public Stance Stance => _stance;
        /// <summary>World velocity (agent or straight-line mover).</summary>
        public Vector3 Velocity => _velocity;
        /// <summary>The full-body action playing (null when none).</summary>
        public string CurrentActionId => _base.Active ? _base.Def.Id : null;
        /// <summary>The arms action playing (null when none).</summary>
        public string CurrentArmsActionId => _arms.Active ? _arms.Def.Id : null;
        /// <summary>Fastest native locomotion clip speed (m/s).</summary>
        public float MaxNativeSpeed => _maxNativeSpeed;
        /// <inheritdoc/>
        public float DiedAt => _diedAt;

        // ------------------------------------------------------------------ lifecycle

        private void Awake() => Init();

        private void Init()
        {
            if (_initialised) return;
            _initialised = true;
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_rig == null) _rig = GetComponent<BodyRig>();
            if (_ik == null) _ik = GetComponent<BodyIK>();
            if (_weapon == null) _weapon = GetComponent<BodyWeaponHolder>();
            if (_ragdoll == null) _ragdoll = GetComponent<RagdollController>();
            if (_events == null) _events = GetComponent<AnimEventRelay>();
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_rig != null) _rig.EnsureResolved();
            if (_agent != null)
            {
                _agent.updateRotation = false; // TECH_PLAN §7.8: the body turns itself
                _agent.updateUpAxis = true;
            }
            if (_animator != null)
            {
                _unscaled = _animator.updateMode == AnimatorUpdateMode.UnscaledTime;
                _baseLayer = _animator.GetLayerIndex(CharacterAnimIds.LayerBase);
                _armsLayer = _animator.GetLayerIndex(CharacterAnimIds.LayerArms);
                _hitsLayer = _animator.GetLayerIndex(CharacterAnimIds.LayerHits);
                _handsLayer = _animator.GetLayerIndex(CharacterAnimIds.LayerHands);
                if (_baseLayer < 0) _baseLayer = 0;
            }
            if (_ik != null) _ik.UnscaledTime = _unscaled;
            _loco.MaxNativeSpeed = _maxNativeSpeed;
        }

        private void OnEnable()
        {
            Init();
            if (_dead) Revive();
            // Root motion is consumed by OnAnimatorMove (travelling actions and deaths only; in-place clips bake it).
            if (_animator != null) _animator.applyRootMotion = true;
            if (_events != null) _events.Raised += OnClipEvent;
            _health = GetComponent<Health>();
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Killed += OnKilled;
            }
            GameEvents.ShotFired += OnShotFired;
            _lastPosition = transform.position;
            _loco.Reset();
            _footsteps.Reset();
            if (_agent != null && _agent.enabled && !_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 1.5f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            if (_weapon != null && _weapon.WeaponId == null && !string.IsNullOrEmpty(_defaultWeaponId)) SetWeaponVisual(_defaultWeaponId);
            ApplyWeaponState();
        }

        private void OnDisable()
        {
            if (_events != null) _events.Raised -= OnClipEvent;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Killed -= OnKilled;
            }
            GameEvents.ShotFired -= OnShotFired;
            CorpseRegistry.Unregister(this);
            if (_animator != null) _animator.applyRootMotion = false;
            // A disabled body cannot finish its actions: complete them so no script waits forever.
            EndAll(true);
        }

        // ------------------------------------------------------------------ ICharacterBody

        /// <inheritdoc/>
        public NavMeshAgent Agent => _agent;

        /// <inheritdoc/>
        public void MoveTo(Vector3 destination, float speed)
        {
            EnsureCommandable();
            if (_dead) return;
            EndInterruptible();
            _destination = destination;
            _hasDestination = true;
            _moveSpeed = Mathf.Max(0f, speed);
            if (AgentUsable)
            {
                _agent.isStopped = false;
                _agent.speed = _moveSpeed;
                _agent.acceleration = Mathf.Max(_agent.acceleration, 8f);
                if (!_agent.SetDestination(destination))
                    LSLog.Verbose(LogCategory.Characters, name + ": SetDestination failed for " + destination, this);
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            _hasDestination = false;
            _kinematicVelocity = Vector3.zero;
            if (AgentUsable)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        /// <inheritdoc/>
        public bool HasArrived
        {
            get
            {
                if (!_hasDestination) return true;
                if (AgentUsable)
                {
                    if (_agent.pathPending) return false;
                    if (_agent.remainingDistance > _agent.stoppingDistance + 0.05f) return false;
                    return !_agent.hasPath || _agent.velocity.sqrMagnitude < 0.04f;
                }
                var d = _destination - transform.position;
                d.y = 0f;
                return d.sqrMagnitude < 0.01f;
            }
        }

        /// <inheritdoc/>
        public void SetStance(Stance stance)
        {
            EnsureCommandable();
            if (_dead) return;
            if (stance != _stance)
            {
                if (_base.Active) EndSlot(_base, true);
                if (_arms.Active && _arms.Def.Loop) EndSlot(_arms, true);
            }
            _stance = stance;
        }

        /// <inheritdoc/>
        public void SetAim(Vector3? worldTarget)
        {
            EnsureCommandable();
            _aimTarget = worldTarget;
            if (_ik != null) _ik.SetAim(_dead ? null : worldTarget);
        }

        /// <inheritdoc/>
        public void SetLookAt(Transform target, float weight)
        {
            EnsureCommandable();
            if (_ik != null) _ik.SetLookAt(target, weight);
        }

        /// <inheritdoc/>
        public void SetCoverPose(int pose)
        {
            _coverPose = Mathf.Clamp(pose, 0, 4);
            if (_ik != null) _ik.SetLean(_coverPose == 3 ? -1f : _coverPose == 4 ? 1f : 0f);
        }

        /// <summary>Perched idle (gunners, marksman; animator "Perch"). SendMessage target.</summary>
        public void SetPerch(bool perch) => _perch = perch;

        /// <summary>Explicit facing while not aiming (null = face the travel direction).</summary>
        public void SetFacing(Vector3? direction) => _faceDirection = direction;

        /// <inheritdoc/>
        public void PlayAction(string actionId, Action onDone = null)
        {
            EnsureCommandable();
            if (_dead)
            {
                SafeInvoke(onDone);
                return;
            }
            if (!ActionTable.TryGet(actionId, out var def))
            {
                LSLog.WarnOnce("action-unknown:" + actionId, LogCategory.Characters,
                    "Unknown action id '" + actionId + "' on " + name + "; completed immediately (ActionTable).", this);
                SafeInvoke(onDone);
                return;
            }
            switch (def.Layer)
            {
                case ActionLayer.Stance:
                    // Insertion crouch-run: crouched locomotion at run speed.
                    EndAll(true);
                    _stance = Stance.Crouch;
                    SafeInvoke(onDone);
                    return;
                case ActionLayer.Procedural:
                    PlayProcedural(def, onDone);
                    return;
                case ActionLayer.Arms:
                    EndSlot(_arms, true);
                    StartSlot(_arms, def, onDone);
                    return;
                default:
                    EndSlot(_arms, true);
                    EndSlot(_base, true);
                    StartSlot(_base, def, onDone);
                    return;
            }
        }

        /// <inheritdoc/>
        public bool IsActionPlaying =>
            (_base.Def != null && !_base.CallbackFired) || (_arms.Def != null && !_arms.CallbackFired) ||
            (_proc.Def != null && !_proc.CallbackFired);

        /// <inheritdoc/>
        public Transform Muzzle => _weapon != null ? _weapon.Muzzle : (_rig != null ? _rig.RightGrip : transform);

        /// <inheritdoc/>
        public Transform RightHand => _rig != null ? (_rig.RightGrip != null ? _rig.RightGrip : _rig.RightHand) : transform;

        /// <inheritdoc/>
        public Transform Head => _rig != null && _rig.Head != null ? _rig.Head : transform;

        /// <inheritdoc/>
        public void SetWeaponVisual(string weaponId)
        {
            EnsureCommandable();
            if (_weapon == null) return;
            _weapon.SetWeapon(weaponId);
            _stowed = false;
            ApplyWeaponState();
        }

        /// <inheritdoc/>
        public void Die(in DamageInfo d)
        {
            if (_dead) return;
            _dead = true;
            _diedAt = Time.time;
            _deathTime = 0f;
            _ragdolled = false;
            _evicted = false;
            _sinkTime = 0f;
            _deathInfo = d;
            EndAll(true);
            Stop();
            if (_agent != null && _agent.enabled) _agent.enabled = false;
            if (_ik != null)
            {
                _ik.SetAim(null);
                _ik.LeftHandOnWeapon = false;
                _ik.StopGesture();
                _ik.AddFlinch(d.Direction, d.Zone, 1.4f);
            }
            if (_animator != null && _animator.isActiveAndEnabled && HasController)
            {
                var index = CharacterAnimIds.DeathFor(d, transform.forward, UnityEngine.Random.value);
                _animator.SetInteger(AnimatorParams.DeathId, index);
                _animator.SetBool(AnimatorParams.AimId, false);
                if (_armsLayer >= 0) _animator.SetLayerWeight(_armsLayer, 0f);
            }
            if (_ragdoll != null) _ragdoll.BeginTracking();
            if (_role != BodyRole.Squad) CorpseRegistry.Register(this);
            if (_ragdollDelay <= 0f) GoRagdoll();
        }

        /// <inheritdoc/>
        public void SetAnimatorLod(int lod)
        {
            _lod = Mathf.Clamp(lod, 0, 2);
            if (_ik != null) _ik.SetForcedLod(_lod);
            if (_animator != null)
                _animator.cullingMode = _lod >= 2 ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.CullUpdateTransforms;
            if (_handsLayer >= 0 && _animator != null) _animator.SetLayerWeight(_handsLayer, _lod >= 1 ? 0f : 1f);
        }

        /// <summary>
        /// A shadow-proxy body (PFB_Calder_Shadow) is a disabled HumanoidBody until something drives it as an
        /// ICharacterBody (WP09 insertion): switch it to cinematic mode on the first command.
        /// </summary>
        private void EnsureCommandable()
        {
            if (_role != BodyRole.ShadowProxy || isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            var proxy = GetComponent<ShadowProxyBody>();
            if (proxy != null) proxy.SetCinematicMode(true);
        }

        // ------------------------------------------------------------------ extra API (same assembly / SendMessage)

        /// <summary>One shot fired: upper-body kick + Fire trigger. SendMessage target "PlayFireRecoil".</summary>
        public void PlayFireRecoil()
        {
            if (_dead) return;
            if (_ik != null) _ik.AddRecoil();
            if (_animator != null && _animator.isActiveAndEnabled && HasController) _animator.SetTrigger(AnimatorParams.FireId);
        }

        /// <summary>Plays a hit reaction from a damage record (also called from Health.Damaged).</summary>
        public void PlayHitReaction(in DamageInfo d)
        {
            if (_dead) return;
            var now = Time.time;
            if (now - _lastHitTime < _hitCooldown) return;
            _lastHitTime = now;
            if (_ik != null) _ik.AddFlinch(d.Direction, d.Zone, d.Suppressive || d.Amount <= 0f ? 0.4f : 1f);
            if (_animator != null && _animator.isActiveAndEnabled && _hitsLayer >= 0 && HasController)
            {
                _animator.SetInteger(AnimatorParams.HitDirId, CharacterAnimIds.HitDirFrom(transform.forward, d.Direction));
                _animator.SetTrigger(AnimatorParams.HitId);
            }
        }

        /// <summary>Teleports the body (agent warp) and clears smoothing.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (AgentUsable) _agent.Warp(position);
            else transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _lastPosition = position;
            _hasDestination = false;
            _kinematicVelocity = Vector3.zero;
            _loco.Reset();
        }

        /// <summary>Right-hand reach target for ReachToggle (booth scrambler/phone).</summary>
        public void SetReachTarget(Transform target)
        {
            if (_ik != null) _ik.SetReachTarget(target);
        }

        // ------------------------------------------------------------------ per frame

        private float Dt => _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

        private bool AgentUsable => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        private bool HasController => _animator != null && _animator.runtimeAnimatorController != null;

        private void Update()
        {
            var dt = Dt;
            if (dt <= 0f) return;
            if (_dead)
            {
                UpdateDeath(dt);
                return;
            }
            UpdateMovement(dt);
            UpdateAnimator(dt);
            UpdateSlot(_base, _baseLayer, AnimatorParams.ActionId, CharacterAnimIds.ActionHoldId, dt);
            UpdateSlot(_arms, _armsLayer, CharacterAnimIds.ArmsActionId, CharacterAnimIds.ArmsHoldId, dt);
            UpdateSlot(_proc, -1, 0, 0, dt);
            UpdateFootstepFallback();
        }

        private void UpdateMovement(float dt)
        {
            var pos = transform.position;
            if (!AgentUsable && _hasDestination && !RootMotionActive)
            {
                // Straight-line mover for agent-less bodies (booth, cinematics, before NavMesh exists).
                var to = _destination - pos;
                to.y = 0f;
                var dist = to.magnitude;
                var stopDist = Mathf.Max(0.05f, _kinematicVelocity.sqrMagnitude / (2f * _kinematicAcceleration));
                var desired = dist > stopDist ? to / Mathf.Max(dist, 1e-4f) * _moveSpeed : Vector3.zero;
                _kinematicVelocity = Vector3.MoveTowards(_kinematicVelocity, desired, _kinematicAcceleration * dt);
                var step = _kinematicVelocity * dt;
                if (step.magnitude > dist) step = to;
                transform.position = pos + step;
                if (dist < 0.02f && _kinematicVelocity.sqrMagnitude < 0.01f)
                {
                    transform.position = new Vector3(_destination.x, transform.position.y, _destination.z);
                    _kinematicVelocity = Vector3.zero;
                }
            }
            var now = transform.position;
            _velocity = AgentUsable && !RootMotionActive ? _agent.velocity : (now - _lastPosition) / dt;
            _lastPosition = now;

            Vector3? aimDir = null;
            if (_aimTarget.HasValue) aimDir = _aimTarget.Value - now;
            var fullBodyAction = _base.Active;
            var yaw = transform.eulerAngles.y;
            // During a full-body action the clip owns the facing (root rotation of travelling clips is applied in OnAnimatorMove).
            var planar = fullBodyAction ? Vector3.zero : _velocity;
            var newYaw = _loco.Step(dt, yaw, planar, fullBodyAction ? null : aimDir, fullBodyAction ? null : _faceDirection);
            if (!Mathf.Approximately(newYaw, yaw)) transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }

        private void UpdateAnimator(float dt)
        {
            var a = _animator;
            if (a == null || !a.isActiveAndEnabled || !HasController) return;
            var lv = _loco.LocalVelocity;
            a.SetFloat(AnimatorParams.VelXId, lv.x);
            a.SetFloat(AnimatorParams.VelZId, lv.y);
            a.SetFloat(AnimatorParams.SpeedId, _loco.Speed);
            a.SetFloat(CharacterAnimIds.TurnId, _loco.TurnParam);
            var crouch = _stance == Stance.Crouch || _coverPose == 1;
            a.SetBool(AnimatorParams.CrouchId, crouch);
            a.SetBool(AnimatorParams.AimId, _aimTarget.HasValue);
            a.SetBool(AnimatorParams.PerchId, _perch);
            a.SetInteger(AnimatorParams.CoverId, _coverPose);
            a.speed = _base.Active ? 1f : _loco.PlaybackScale(_loco.Speed);

            // Arms layer: on while a weapon is held or an arms action plays (GDD §14.3 rifle handling); a full-body action
            // owns the arms unless an arms action runs on top of it.
            var armed = _weapon != null && _weapon.Hold != WeaponHold.Unarmed;
            var targetArms = _arms.Active ? 1f : _base.Active ? 0f : armed ? 1f : 0f;
            _armsWeight = Mathf.MoveTowards(_armsWeight, targetArms, dt / Mathf.Max(0.01f, _armsBlend));
            if (_armsLayer >= 0) a.SetLayerWeight(_armsLayer, _armsWeight);
            if (_handsLayer >= 0) a.SetLayerWeight(_handsLayer, _lod >= 1 ? 0f : 1f);

            if (_ik != null)
            {
                bool keepsHand;
                if (_arms.Active) keepsHand = _arms.Def.LeftHandOnWeapon;
                else if (_base.Active) keepsHand = _base.Def.LeftHandOnWeapon;
                else keepsHand = true;
                _ik.LeftHandOnWeapon = armed && keepsHand && _stance != Stance.Sprint;
            }
        }

        // ------------------------------------------------------------------ actions

        private void StartSlot(ActionSlot slot, ActionDef def, Action onDone)
        {
            slot.Begin(def, onDone);
            if (slot == _base && _hasDestination && !def.RootMotion) Stop();

            var a = _animator;
            if (def.HasState && a != null && a.isActiveAndEnabled && HasController)
            {
                if (def.Layer == ActionLayer.Arms)
                {
                    a.SetInteger(CharacterAnimIds.ArmsActionId, def.Index);
                    a.SetBool(CharacterAnimIds.ArmsHoldId, def.Held);
                    a.SetTrigger(CharacterAnimIds.ArmsTriggerId);
                }
                else
                {
                    a.SetInteger(AnimatorParams.ActionId, def.Index);
                    a.SetBool(CharacterAnimIds.ActionHoldId, def.Held);
                    a.SetTrigger(AnimatorParams.ActionTriggerId);
                }
            }
            if (def.Gesture != Gesture.None && (def.GestureAlways || !HasController || !def.HasState)) StartGesture(slot);
            ApplyWeaponState();
            // Loops establish immediately for the caller (TECH_PLAN contract: never hang on PlayScripted).
            if (def.Loop) FireCallback(slot);
        }

        private void StartGesture(ActionSlot slot)
        {
            if (slot.GestureStarted || _ik == null || slot.Def == null || slot.Def.Gesture == Gesture.None) return;
            slot.GestureStarted = true;
            var def = slot.Def;
            _ik.PlayGesture(def.Gesture, def.Loop ? 1f : Mathf.Max(0.3f, slot.Length - slot.Time), def.Loop);
        }

        private void PlayProcedural(ActionDef def, Action onDone)
        {
            if (def.Id == ExtraActionIds.Fire)
            {
                PlayFireRecoil();
                SafeInvoke(onDone);
                return;
            }
            if (def.Id == ExtraActionIds.Stop)
            {
                EndAll(true);
                SafeInvoke(onDone);
                return;
            }
            EndSlot(_proc, true);
            if (_ik != null)
            {
                // MID-B reel: the seated man lifts his head (shot 5) or goes limp (shot 6); the seat action keeps playing.
                if (def.Id == ExtraActionIds.HeadLift) _ik.SetHeadPitch(0f, 0f);
                else if (def.Id == ExtraActionIds.GoLimp) _ik.SetHeadPitch(38f, 1f);
            }
            StartSlot(_proc, def, onDone);
        }

        private void UpdateSlot(ActionSlot slot, int layer, int indexParam, int holdParam, float dt)
        {
            var def = slot.Def;
            if (def == null || (slot.CallbackFired && !def.Loop)) return;
            slot.Time += dt;
            var a = _animator;
            if (!slot.Entered && def.HasState && layer >= 0 && a != null && a.isActiveAndEnabled && HasController)
            {
                var info = a.GetCurrentAnimatorStateInfo(layer);
                var next = a.GetNextAnimatorStateInfo(layer);
                var current = IsActionState(info) && a.GetInteger(indexParam) == def.Index;
                var entering = IsActionState(next);
                if (current || entering)
                {
                    slot.Entered = true;
                    var s = entering ? next : info;
                    // State length in real seconds (state speed is baked by the builder; animator speed is 1 during actions).
                    slot.Length = def.LengthFor(s.length / Mathf.Max(0.01f, a.speed));
                    if (s.tagHash == CharacterAnimIds.TagProceduralHash) StartGesture(slot);
                }
            }
            if (!slot.Entered && !slot.GestureStarted && slot.Time >= _gestureFallbackDelay) StartGesture(slot);

            var length = Mathf.Max(0.05f, slot.Length);
            var t = slot.Time / length;
            if (!slot.Event1Fired && t >= def.Event1At)
            {
                slot.Event1Fired = true;
                RaiseAnimEvent(def.Event1);
            }
            if (!slot.Event2Fired && t >= def.Event2At)
            {
                slot.Event2Fired = true;
                RaiseAnimEvent(def.Event2);
            }
            if (!slot.HitFired && t >= def.HitAt)
            {
                slot.HitFired = true;
                RaiseAnimEvent(AnimEventIds.ActionHit);
            }
            if (def.Loop || slot.Time < length) return;
            if (holdParam != 0 && a != null && a.isActiveAndEnabled && HasController) a.SetBool(holdParam, false);
            FinishSlot(slot);
        }

        private static bool IsActionState(AnimatorStateInfo s) =>
            s.tagHash == CharacterAnimIds.TagActionHash || s.tagHash == CharacterAnimIds.TagProceduralHash;

        private void FinishSlot(ActionSlot slot)
        {
            var def = slot.Def;
            if (def == null) return;
            if (!slot.HitFired)
            {
                slot.HitFired = true;
                RaiseAnimEvent(AnimEventIds.ActionHit);
            }
            RaiseAnimEvent(AnimEventIds.ActionEnd);
            var cb = slot.TakeCallback();
            slot.Def = null;
            CleanupAction(def);
            SafeInvoke(cb);
        }

        private void FireCallback(ActionSlot slot) => SafeInvoke(slot.TakeCallback());

        /// <summary>Ends looping and full-body actions when the body is told to move (arms one-shots and traversals keep playing).</summary>
        private void EndInterruptible()
        {
            if (_base.Def != null && !_base.Def.RootMotion) EndSlot(_base, true);
            if (_arms.Def != null && _arms.Def.Loop) EndSlot(_arms, true);
            if (_proc.Def != null) EndSlot(_proc, true);
        }

        private void EndAll(bool complete)
        {
            EndSlot(_arms, complete);
            EndSlot(_base, complete);
            EndSlot(_proc, complete);
        }

        /// <summary>Stops an action; <paramref name="complete"/> fires its callback (never leave a script waiting).</summary>
        private void EndSlot(ActionSlot slot, bool complete)
        {
            var def = slot.Def;
            if (def == null) return;
            var cb = complete ? slot.TakeCallback() : null;
            slot.Clear();
            if (def.HasState && _animator != null && _animator.isActiveAndEnabled && HasController)
            {
                if (def.Layer == ActionLayer.Arms)
                {
                    _animator.SetBool(CharacterAnimIds.ArmsHoldId, false);
                    _animator.ResetTrigger(CharacterAnimIds.ArmsTriggerId);
                    _animator.SetInteger(CharacterAnimIds.ArmsActionId, 0);
                }
                else
                {
                    _animator.SetBool(CharacterAnimIds.ActionHoldId, false);
                    _animator.ResetTrigger(AnimatorParams.ActionTriggerId);
                    _animator.SetInteger(AnimatorParams.ActionId, 0);
                }
            }
            CleanupAction(def);
            SafeInvoke(cb);
        }

        private void CleanupAction(ActionDef def)
        {
            // GoLimp keeps the head down until HeadLift (MID-B shot 6 → booth reset).
            if (_ik != null && def.Gesture != Gesture.None && _ik.CurrentGesture == def.Gesture) _ik.StopGesture();
            ApplyWeaponState();
        }

        private void SafeInvoke(Action cb)
        {
            if (cb == null) return;
            try
            {
                cb();
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Characters, e, this);
            }
        }

        // ------------------------------------------------------------------ root motion

        private bool RootMotionActive => (_dead && !_ragdolled) || (_base.Active && _base.Def.RootMotion);

        private void OnAnimatorMove()
        {
            var a = _animator;
            if (a == null || !RootMotionActive) return;
            var dp = a.deltaPosition;
            var dr = a.deltaRotation;
            if (AgentUsable)
            {
                // Off-mesh link traversal: WP04 moves the agent along the link; the clip only animates.
                if (_agent.isOnOffMeshLink) return;
                _agent.Move(dp);
            }
            else transform.position += dp;
            transform.rotation = dr * transform.rotation;
        }

        // ------------------------------------------------------------------ weapon / hands

        private void ApplyWeaponState()
        {
            if (_weapon == null) return;
            var stow = (_base.Active && _base.Def.Stow) || (_arms.Active && _arms.Def.Stow);
            if (stow != _stowed)
            {
                _stowed = stow;
                _weapon.SetStowed(stow);
            }
            var a = _animator;
            var hold = _weapon.Hold;
            var pose = _weapon.HandPose;
            if (_arms.Active && _arms.Def.HandPoseOverride >= 0) pose = (HandPose)_arms.Def.HandPoseOverride;
            else if (_base.Active && _base.Def.HandPoseOverride >= 0) pose = (HandPose)_base.Def.HandPoseOverride;
            if (a != null && a.isActiveAndEnabled && HasController)
            {
                a.SetInteger(CharacterAnimIds.HoldId, (int)hold);
                a.SetInteger(CharacterAnimIds.HandPoseId, (int)pose);
            }
            if (_ik != null) _ik.SetWeapon(_weapon.IsHolding ? _weapon.Muzzle : null, _weapon.LeftGrip);
        }

        // ------------------------------------------------------------------ events

        private void OnClipEvent(string id)
        {
            if (_dead) return;
            if (id == AnimEventIds.FootL || id == AnimEventIds.FootR) _lastClipFootEvent = Time.time;
            MarkClipEvent(_base, id);
            MarkClipEvent(_arms, id);
            RaiseAnimEvent(id);
        }

        private static void MarkClipEvent(ActionSlot slot, string id)
        {
            if (slot.Def == null) return;
            if (id == slot.Def.Event1) slot.Event1Fired = true;
            if (id == slot.Def.Event2) slot.Event2Fired = true;
        }

        private void RaiseAnimEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var h = AnimEvent;
            if (h == null) return;
            try
            {
                h(id);
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Characters, e, this);
            }
        }

        private void UpdateFootstepFallback()
        {
            // Clip foot events win; the phase clock covers clips without them (1 s grace).
            if (Time.time - _lastClipFootEvent < 1f || _animator == null || !_animator.isActiveAndEnabled || !HasController) return;
            var info = _animator.GetCurrentAnimatorStateInfo(_baseLayer);
            if (info.tagHash != CharacterAnimIds.TagLocomotionHash)
            {
                _footsteps.Reset();
                return;
            }
            var e = _footsteps.Step(info.normalizedTime, _loco.Speed);
            if (e != null) RaiseAnimEvent(e);
        }

        private void OnDamaged(Health h, DamageInfo d)
        {
            if (h.IsAlive) PlayHitReaction(d);
        }

        private void OnKilled(Health h, DamageInfo d) => Die(d);

        private void OnShotFired(ShotEvent s)
        {
            // AI shots come from ICombatService.FireAI with Origin = our muzzle: kick without any coupling to WP04.
            if (_dead || s.IsPlayer || _weapon == null || !_weapon.IsHolding) return;
            var m = _weapon.Muzzle;
            if (m != null && (s.Origin - m.position).sqrMagnitude < 0.36f) PlayFireRecoil();
        }

        // ------------------------------------------------------------------ death

        private void UpdateDeath(float dt)
        {
            _deathTime += dt;
            if (!_ragdolled && _deathTime >= _ragdollDelay) GoRagdoll();
            if (_role == BodyRole.Squad) return;
            if (!_evicted && Time.time - _diedAt >= _corpseSeconds) Evict();
            if (_evicted)
            {
                // Sink below the ground, then despawn (opaque URP materials cannot fade cheaply).
                _sinkTime += dt;
                var root = _rig != null && _rig.Hips != null ? _rig.Hips : transform;
                root.position += Vector3.down * (0.6f / Mathf.Max(0.1f, _sinkSeconds)) * dt;
                if (_sinkTime >= _sinkSeconds) Despawn();
            }
        }

        private void GoRagdoll()
        {
            _ragdolled = true;
            if (_weapon != null && _weapon.Instance != null)
            {
                var v = _ragdoll != null && _ragdoll.Pelvis != null ? _ragdoll.Pelvis.linearVelocity : Vector3.zero;
                _weapon.Drop(v + _deathInfo.Direction.normalized * 1.2f + Vector3.up * 0.5f, UnityEngine.Random.insideUnitSphere * 4f);
            }
            if (_ragdoll != null) _ragdoll.Activate(_deathInfo);
            else if (_animator != null) _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            ApplyWeaponState();
        }

        /// <inheritdoc/>
        public void Evict()
        {
            if (_evicted) return;
            _evicted = true;
            _sinkTime = 0f;
            if (_ragdoll != null) _ragdoll.Freeze();
            CorpseRegistry.Unregister(this);
        }

        private void Despawn()
        {
            CorpseRegistry.Unregister(this);
            Pools.Despawn(gameObject);
        }

        private void Revive()
        {
            _dead = false;
            _ragdolled = false;
            _evicted = false;
            CorpseRegistry.Unregister(this);
            if (_ragdoll != null) _ragdoll.Restore();
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Rebind();
                if (HasController) _animator.SetInteger(AnimatorParams.DeathId, 0);
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            if (_agent != null) _agent.enabled = true;
            _stance = Stance.Stand;
            _coverPose = 0;
            _aimTarget = null;
            _hasDestination = false;
            _stowed = false;
            if (_ik != null)
            {
                _ik.SetAim(null);
                _ik.SetHeadPitch(0f, 0f);
            }
            if (_weapon != null)
            {
                var id = _weapon.WeaponId ?? _defaultWeaponId;
                _weapon.Clear();
                if (!string.IsNullOrEmpty(id)) _weapon.SetWeapon(id);
            }
        }

#if UNITY_EDITOR
        /// <summary>Builders: wires the components and defaults.</summary>
        public void EditorSetup(BodyRole role, Animator animator, BodyRig rig, BodyIK ik, BodyWeaponHolder weapon,
                                RagdollController ragdoll, AnimEventRelay events, NavMeshAgent agent, string defaultWeaponId,
                                float maxNativeSpeed)
        {
            _role = role;
            _animator = animator;
            _rig = rig;
            _ik = ik;
            _weapon = weapon;
            _ragdoll = ragdoll;
            _events = events;
            _agent = agent;
            _defaultWeaponId = defaultWeaponId;
            _maxNativeSpeed = maxNativeSpeed > 0f ? maxNativeSpeed : 6f;
        }
#endif
    }
}
