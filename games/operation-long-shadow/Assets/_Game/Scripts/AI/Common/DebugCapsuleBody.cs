using System;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Greybox <see cref="ICharacterBody"/> (TECH_PLAN §6.2: "WP04 ships DebugCapsuleBody for tests"): a 1.8 m capsule
    /// soldier with a visor, faction tell (orange Legion armband, Greycoat helmet, squad green), a placeholder rifle with
    /// a muzzle, three hitboxes (head/torso/legs) and a NavMeshAgent. Used by the AIDevArena, by EnemyPrefabBuilder while
    /// WP05's HumanoidBody prefabs have not landed, and by tests. Without a NavMesh it moves in straight lines.
    /// Actions play for <see cref="ActionDurations.Of"/> seconds; unknown action ids log once and complete at once
    /// (WP05 rule), so scripts never hang.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugCapsuleBody : MonoBehaviour, ICharacterBody
    {
        /// <summary>Capsule height (TECH_PLAN §7.15 agent height 1.8 m).</summary>
        public const float Height = 1.8f;
        /// <summary>Capsule radius (TECH_PLAN §7.15 agent radius 0.35 m).</summary>
        public const float Radius = 0.35f;
        /// <summary>Visual height factor while crouched.</summary>
        public const float CrouchScale = 0.62f;

        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private Transform _visual;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _weapon;
        [SerializeField, Tooltip("Degrees per second.")] private float _turnRate = 540f;
        [SerializeField, Tooltip("Squad body: trigger capsule, SquadHitbox colliders.")] private bool _squad;

        private Vector3 _destination;
        private bool _hasDestination;
        private float _speed;
        private Vector3? _aim;
        private Transform _lookTarget;
        private float _lookWeight;
        private Stance _stance = Stance.Stand;
        private int _coverPose;
        private float _crouchBlend;
        private string _action;
        private float _actionTime;
        private float _actionDuration;
        private float _actionEffect;
        private bool _actionEffectRaised;
        private Action _actionDone;
        private bool _dead;
        private float _deathTime;
        private int _lod;
        private Vector3 _lastPosition;
        private Vector3 _velocity;

        /// <inheritdoc/>
        public event Action<string> AnimEvent;

        /// <inheritdoc/>
        public NavMeshAgent Agent => _agent;
        /// <inheritdoc/>
        public bool HasArrived
        {
            get
            {
                if (!_hasDestination) return true;
                if (NavUtil.Usable(_agent))
                {
                    if (_agent.pathPending) return false;
                    if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
                    return _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;
                }
                var d = _destination - transform.position;
                d.y = 0f;
                return d.sqrMagnitude <= 0.04f;
            }
        }
        /// <inheritdoc/>
        public bool IsActionPlaying => _action != null;
        /// <inheritdoc/>
        public Transform Muzzle => _muzzle != null ? _muzzle : transform;
        /// <inheritdoc/>
        public Transform RightHand => _rightHand != null ? _rightHand : transform;
        /// <inheritdoc/>
        public Transform Head => _head != null ? _head : transform;
        /// <summary>Current stance.</summary>
        public Stance Stance => _stance;
        /// <summary>Current cover pose (AnimatorParams.Cover).</summary>
        public int CoverPose => _coverPose;
        /// <summary>Action being played, or null.</summary>
        public string CurrentAction => _action;
        /// <summary>World velocity (agent velocity or position delta).</summary>
        public Vector3 Velocity => NavUtil.Usable(_agent) ? _agent.velocity : _velocity;
        /// <summary>Animator LOD set by the brain.</summary>
        public int AnimatorLod => _lod;
        /// <summary>True after <see cref="Die"/>.</summary>
        public bool IsDead => _dead;

        private void Awake()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) _agent.updateRotation = false;
            _lastPosition = transform.position;
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>Advances movement, turning, crouch blend and actions (called from Update; tests call it directly).</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            if (!_dead)
            {
                if (_hasDestination && !NavUtil.Usable(_agent))
                {
                    var to = _destination - transform.position;
                    to.y = 0f;
                    var step = _speed * dt;
                    if (to.magnitude <= step) transform.position = new Vector3(_destination.x, transform.position.y, _destination.z);
                    else transform.position += to.normalized * step;
                }
                _velocity = (transform.position - _lastPosition) / dt;
                _lastPosition = transform.position;
                Turn(dt);
                var crouchTarget = _stance == Stance.Crouch || _coverPose == 1 ? 1f : 0f;
                _crouchBlend = Mathf.MoveTowards(_crouchBlend, crouchTarget, dt * 4f);
                if (_visual != null)
                {
                    var s = Mathf.Lerp(1f, CrouchScale, _crouchBlend);
                    var lean = _coverPose == 3 ? 12f : _coverPose == 4 ? -12f : 0f;
                    _visual.localScale = new Vector3(1f, s, 1f);
                    _visual.localRotation = Quaternion.Euler(0f, 0f, lean);
                }
            }
            else if (_visual != null)
            {
                _deathTime += dt;
                var t = Mathf.Clamp01(_deathTime / 0.45f);
                _visual.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -88f, t * t), 0f, 0f);
            }
            TickAction(dt);
        }

        private void Turn(float dt)
        {
            Vector3 dir;
            if (_aim.HasValue) dir = _aim.Value - transform.position;
            else
            {
                dir = Velocity;
                if (dir.sqrMagnitude < 0.04f) return;
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnRate * dt);
            if (_head != null && _lookTarget != null && _lookWeight > 0f)
            {
                var l = _lookTarget.position - _head.position;
                if (l.sqrMagnitude > 1e-4f)
                {
                    var local = transform.InverseTransformDirection(l.normalized);
                    var yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f) * Mathf.Clamp01(_lookWeight);
                    _head.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
            else if (_head != null) _head.localRotation = Quaternion.identity;
        }

        /// <inheritdoc/>
        public void MoveTo(Vector3 destination, float speed)
        {
            if (_dead) return;
            _destination = destination;
            _hasDestination = true;
            _speed = Mathf.Max(0.1f, speed);
            if (NavUtil.Usable(_agent))
            {
                _agent.speed = _speed;
                _agent.isStopped = false;
                _agent.SetDestination(destination);
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            _hasDestination = false;
            if (NavUtil.Usable(_agent))
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        /// <inheritdoc/>
        public void SetStance(Stance stance) => _stance = stance;
        /// <inheritdoc/>
        public void SetAim(Vector3? worldTarget) => _aim = worldTarget;
        /// <inheritdoc/>
        public void SetLookAt(Transform target, float weight)
        {
            _lookTarget = target;
            _lookWeight = target != null ? Mathf.Clamp01(weight) : 0f;
        }
        /// <inheritdoc/>
        public void SetCoverPose(int pose) => _coverPose = pose;

        /// <inheritdoc/>
        public void PlayAction(string actionId, Action onDone = null)
        {
            // A new action replaces the current one; the replaced action still reports completion.
            FinishAction(false);
            if (_dead)
            {
                onDone?.Invoke();
                return;
            }
            var d = ActionDurations.Of(actionId);
            if (string.IsNullOrEmpty(actionId) || actionId == AIActionIds.Stop || d == 0f)
            {
                if (!string.IsNullOrEmpty(actionId) && actionId != AIActionIds.Stop)
                    LSLog.WarnOnce("capsule-action:" + actionId, LogCategory.AI, "DebugCapsuleBody has no action '" + actionId + "'; completing at once.");
                onDone?.Invoke();
                return;
            }
            _action = actionId;
            _actionTime = 0f;
            _actionDuration = d;
            _actionEffect = ActionDurations.EffectPoint(actionId);
            _actionEffectRaised = false;
            if (d < 0f)
            {
                // Loops establish immediately for the caller (same contract as WP05 HumanoidBody: never hang).
                _actionDone = null;
                onDone?.Invoke();
                return;
            }
            _actionDone = onDone;
        }

        private void TickAction(float dt)
        {
            if (_action == null) return;
            _actionTime += dt;
            if (!_actionEffectRaised && _actionEffect >= 0f && _actionTime >= _actionEffect)
            {
                _actionEffectRaised = true;
                AnimEvent?.Invoke(_action == ActionIds.Throw ? AnimEventIds.ThrowRelease : AnimEventIds.ActionHit);
            }
            if (_action == ActionIds.Reload)
            {
                if (_actionTime >= 0.6f && _actionTime - dt < 0.6f) AnimEvent?.Invoke(AnimEventIds.MagOut);
                if (_actionTime >= 1.6f && _actionTime - dt < 1.6f) AnimEvent?.Invoke(AnimEventIds.MagIn);
            }
            if (_actionDuration > 0f && _actionTime >= _actionDuration) FinishAction(true);
        }

        private void FinishAction(bool raiseEnd)
        {
            if (_action == null) return;
            var done = _actionDone;
            var hadEffect = _actionEffect >= 0f && !_actionEffectRaised;
            var id = _action;
            _action = null;
            _actionDone = null;
            if (hadEffect) AnimEvent?.Invoke(id == ActionIds.Throw ? AnimEventIds.ThrowRelease : AnimEventIds.ActionHit);
            if (raiseEnd) AnimEvent?.Invoke(AnimEventIds.ActionEnd);
            done?.Invoke();
        }

        /// <inheritdoc/>
        public void SetWeaponVisual(string weaponId)
        {
            if (_weapon == null) return;
            _weapon.gameObject.SetActive(!string.IsNullOrEmpty(weaponId));
            var length = 0.9f;
            if (weaponId == WeaponIds.RPK || weaponId == WeaponIds.SVD) length = 1.15f;
            else if (weaponId == WeaponIds.RPG7) length = 1.0f;
            else if (weaponId == WeaponIds.AKMS || weaponId == WeaponIds.Commando) length = 0.75f;
            _weapon.localScale = new Vector3(0.06f, weaponId == WeaponIds.RPG7 ? 0.11f : 0.08f, length);
            if (_muzzle != null) _muzzle.localPosition = _weapon.localPosition + new Vector3(0f, 0f, length * 0.5f);
        }

        /// <inheritdoc/>
        public void Die(in DamageInfo d)
        {
            if (_dead) return;
            _dead = true;
            _deathTime = 0f;
            Stop();
            FinishAction(false);
            if (_agent != null) _agent.enabled = false;
            var root = GetComponent<Collider>();
            if (root != null) root.enabled = false;
            foreach (var hb in GetComponentsInChildren<Hitbox>(true)) hb.gameObject.layer = LayerIds.Ragdoll;
            if (d.Direction.sqrMagnitude > 1e-4f)
            {
                var away = d.Direction;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(-away.normalized, Vector3.up);
            }
        }

        /// <summary>Back to a living, standing body (pool reuse, checkpoint restore).</summary>
        public void ResetBody()
        {
            _dead = false;
            _deathTime = 0f;
            _stance = Stance.Stand;
            _coverPose = 0;
            _crouchBlend = 0f;
            _aim = null;
            _lookTarget = null;
            _hasDestination = false;
            _action = null;
            _actionDone = null;
            if (_visual != null)
            {
                _visual.localRotation = Quaternion.identity;
                _visual.localScale = Vector3.one;
            }
            // The agent stays as the caller left it (EnemyActor.Prepare enables it once the body stands on its spawn point).
            var root = GetComponent<Collider>();
            if (root != null) root.enabled = true;
            var hitLayer = _squad ? LayerIds.SquadHitbox : LayerIds.Hitbox;
            foreach (var hb in GetComponentsInChildren<Hitbox>(true)) hb.gameObject.layer = hitLayer;
        }

        /// <inheritdoc/>
        public void SetAnimatorLod(int lod) => _lod = lod;

        // ---------------------------------------------------------------------------------------------------------
        // Construction (shared by the runtime fallback and the editor builders)

        /// <summary>
        /// Builds the greybox body on <paramref name="root"/>: agent, kinematic rigidbody, root capsule, visuals and
        /// hitboxes. <paramref name="material"/> supplies materials (runtime: Placeholders.Material; editor builders: a
        /// saved material asset). Squad bodies use a trigger capsule (the player pushes through, GDD §15) and SquadHitbox
        /// colliders (player shots pass, TECH_PLAN §3.9).
        /// </summary>
        public static DebugCapsuleBody Build(GameObject root, Team team, Func<Color, Material> material, bool squad = false)
        {
            if (material == null) material = Placeholders.Material;
            root.layer = LayerIds.Character;

            var agent = root.GetComponent<NavMeshAgent>();
            if (agent == null) agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Radius;
            agent.height = Height;
            agent.baseOffset = 0f;
            agent.speed = 3.5f;
            agent.angularSpeed = 0f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.25f;
            agent.autoBraking = true;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = squad ? ObstacleAvoidanceType.MedQualityObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var cap = root.GetComponent<CapsuleCollider>();
            if (cap == null) cap = root.AddComponent<CapsuleCollider>();
            cap.radius = Radius;
            cap.height = Height;
            cap.center = new Vector3(0f, Height * 0.5f, 0f);
            cap.isTrigger = squad;

            var body = root.GetComponent<DebugCapsuleBody>();
            if (body == null) body = root.AddComponent<DebugCapsuleBody>();
            body._agent = agent;
            body._squad = squad;

            var visual = Child(root.transform, "Visual");
            body._visual = visual;
            var cloth = squad ? new Color(0.62f, 0.55f, 0.42f) : team == Team.Greycoat ? new Color(0.40f, 0.44f, 0.40f) : new Color(0.60f, 0.53f, 0.38f);
            Prim("Torso", PrimitiveType.Capsule, visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.85f, 0.42f), cloth, material);
            var head = Child(visual, "Head");
            head.localPosition = new Vector3(0f, 1.62f, 0f);
            body._head = head;
            Prim("Skull", PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.24f, 0.26f, 0.26f), new Color(0.55f, 0.42f, 0.33f), material);
            Prim("Visor", PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.11f), new Vector3(0.2f, 0.05f, 0.06f), Color.black, material);
            if (squad) Prim("Tell", PrimitiveType.Cube, visual, new Vector3(0f, 1.42f, -0.2f), new Vector3(0.3f, 0.35f, 0.15f), Placeholders.Friendly, material);
            else if (team == Team.Greycoat) Prim("Helmet", PrimitiveType.Sphere, head, new Vector3(0f, 0.07f, 0f), new Vector3(0.3f, 0.2f, 0.32f), new Color(0.32f, 0.36f, 0.32f), material);
            else Prim("Armband", PrimitiveType.Cube, visual, new Vector3(-0.27f, 1.25f, 0f), new Vector3(0.06f, 0.12f, 0.16f), new Color(1f, 0.45f, 0.05f), material);

            var hand = Child(visual, "RightHand");
            hand.localPosition = new Vector3(0.18f, 1.25f, 0.25f);
            body._rightHand = hand;
            var weapon = Prim("Weapon", PrimitiveType.Cube, visual, new Vector3(0.12f, 1.32f, 0.45f), new Vector3(0.06f, 0.08f, 0.9f), new Color(0.12f, 0.1f, 0.08f), material).transform;
            body._weapon = weapon;
            var muzzle = Child(visual, "Muzzle");
            muzzle.localPosition = weapon.localPosition + new Vector3(0f, 0f, 0.45f);
            body._muzzle = muzzle;

            var hitLayer = squad ? LayerIds.SquadHitbox : LayerIds.Hitbox;
            var hbHead = Child(head, "HB_Head");
            SetCollider<SphereCollider>(hbHead.gameObject, c => c.radius = 0.14f);
            Hitbox(hbHead.gameObject, HitZone.Head, hitLayer);
            var hbTorso = Child(visual, "HB_Torso");
            hbTorso.localPosition = new Vector3(0f, 1.15f, 0f);
            SetCollider<CapsuleCollider>(hbTorso.gameObject, c => { c.radius = 0.24f; c.height = 0.75f; });
            Hitbox(hbTorso.gameObject, HitZone.Torso, hitLayer);
            var hbLegs = Child(visual, "HB_Legs");
            hbLegs.localPosition = new Vector3(0f, 0.45f, 0f);
            SetCollider<CapsuleCollider>(hbLegs.gameObject, c => { c.radius = 0.2f; c.height = 0.9f; });
            Hitbox(hbLegs.gameObject, HitZone.Limb, hitLayer);
            return body;
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color,
                                       Func<Color, Material> material)
        {
            var existing = parent.Find("PH_" + name);
            if (existing != null) return existing.gameObject;
            var go = GameObject.CreatePrimitive(type);
            go.name = "PH_" + name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = material(color);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            go.layer = parent.gameObject.layer;
            return go;
        }

        private static void SetCollider<T>(GameObject go, Action<T> configure) where T : Collider
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            c.isTrigger = false;
            configure(c);
        }

        private static void Hitbox(GameObject go, HitZone zone, int layer)
        {
            go.layer = layer;
            var hb = go.GetComponent<Hitbox>();
            if (hb == null) hb = go.AddComponent<Hitbox>();
            hb.Configure(zone);
        }
    }
}
