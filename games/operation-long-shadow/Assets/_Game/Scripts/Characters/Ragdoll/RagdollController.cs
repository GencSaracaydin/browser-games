using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Ragdoll
{
    /// <summary>
    /// Switches a body's bone colliders between kinematic hitboxes (alive) and a dynamic ragdoll (dead), GDD §14.3 "the
    /// death clip blends into a ragdoll after 0.2 s". The blend is continuous: while the death clip plays the controller
    /// samples every bone, and at the switch each rigidbody starts with the clip's own linear and angular velocity, so
    /// the fall carries on without a pop; the shot adds an impulse at the hit point (explosions push every bone). Once the
    /// body settles it freezes (kinematic, colliders off) to cost nothing. <see cref="Restore"/> rearms it for pooling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RagdollController : MonoBehaviour
    {
        [SerializeField] private RagdollBone[] _bones = new RagdollBone[0];
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("Hitbox layer while alive (Hitbox for enemies, SquadHitbox for the squad, TECH_PLAN §3.9).")]
        private int _aliveLayer = LayerIds.Hitbox;
        [SerializeField, Tooltip("Colliders on the body root disabled at death (the Character capsule).")]
        private Collider[] _aliveOnlyColliders = new Collider[0];
        [SerializeField, Tooltip("Seconds after which a ragdoll freezes even if it has not slept.")]
        private float _maxSimSeconds = 7f;

        private Vector3[] _prevPos;
        private Quaternion[] _prevRot;
        private float _prevTime;
        private bool _tracking;
        private bool _active;
        private bool _frozen;
        private float _activeTime;
        private float _sleepTime;

        /// <summary>Physical bones.</summary>
        public RagdollBone[] Bones => _bones;
        /// <summary>True while the ragdoll simulates or lies frozen.</summary>
        public bool IsActive => _active;
        /// <summary>True once the ragdoll settled and froze.</summary>
        public bool IsFrozen => _frozen;
        /// <summary>Alive hitbox layer.</summary>
        public int AliveLayer => _aliveLayer;
        /// <summary>The pelvis body (null if not built).</summary>
        public Rigidbody Pelvis => _bones.Length > 0 && _bones[0] != null ? _bones[0].Body : null;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            _prevPos = new Vector3[_bones.Length];
            _prevRot = new Quaternion[_bones.Length];
            SetAlive();
        }

        /// <summary>Alive state: kinematic hitboxes on the alive layer, capsule on, Animator on.</summary>
        public void SetAlive()
        {
            _active = false;
            _frozen = false;
            _tracking = false;
            foreach (var b in _bones)
            {
                if (b == null) continue;
                if (b.Body != null)
                {
                    b.Body.isKinematic = true;
                    b.Body.detectCollisions = true;
                    b.Body.interpolation = RigidbodyInterpolation.None;
                }
                if (b.Collider != null)
                {
                    b.Collider.enabled = true;
                    b.Collider.gameObject.layer = _aliveLayer;
                }
            }
            foreach (var c in _aliveOnlyColliders)
                if (c != null) c.enabled = true;
        }

        /// <summary>Starts sampling bone motion (call when the death clip starts).</summary>
        public void BeginTracking()
        {
            if (_prevPos == null || _prevPos.Length != _bones.Length)
            {
                _prevPos = new Vector3[_bones.Length];
                _prevRot = new Quaternion[_bones.Length];
            }
            Sample();
            _tracking = true;
            foreach (var c in _aliveOnlyColliders)
                if (c != null) c.enabled = false;
        }

        private void LateUpdate()
        {
            if (_tracking && !_active) Sample();
        }

        private void Sample()
        {
            for (var i = 0; i < _bones.Length; i++)
            {
                var t = _bones[i] != null ? _bones[i].Bone : null;
                if (t == null) continue;
                _prevPos[i] = t.position;
                _prevRot[i] = t.rotation;
            }
            _prevTime = Time.time;
        }

        /// <summary>
        /// Hands the body to physics. Bone velocities come from the last sampled frame; the damage adds an impulse
        /// (bullets on the nearest bone, explosions on every bone).
        /// </summary>
        public void Activate(in DamageInfo d)
        {
            if (_active) return;
            if (_bones.Length == 0)
            {
                LSLog.WarnOnce("ragdoll-empty:" + name, LogCategory.Characters, name + ": no ragdoll bones built; the death clip stays.", this);
                return;
            }
            var dt = Mathf.Max(0.005f, Time.time - _prevTime);
            var hasSample = _tracking;
            _tracking = false;
            if (_animator != null) _animator.enabled = false;
            foreach (var c in _aliveOnlyColliders)
                if (c != null) c.enabled = false;

            for (var i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                if (b == null || b.Body == null) continue;
                if (b.Collider != null)
                {
                    b.Collider.enabled = true;
                    b.Collider.gameObject.layer = LayerIds.Ragdoll;
                }
                var rb = b.Body;
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.solverIterations = 10;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                if (hasSample && b.Bone != null)
                {
                    var v = (b.Bone.position - _prevPos[i]) / dt;
                    rb.linearVelocity = Vector3.ClampMagnitude(v, 8f);
                    var dq = b.Bone.rotation * Quaternion.Inverse(_prevRot[i]);
                    dq.ToAngleAxis(out var angle, out var axis);
                    if (angle > 180f) angle -= 360f;
                    if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.5f)
                        rb.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / dt), 15f);
                }
            }
            ApplyDamageImpulse(d);
            _active = true;
            _frozen = false;
            _activeTime = 0f;
            _sleepTime = 0f;
        }

        /// <summary>Adds an extra impulse to a live ragdoll (shooting a corpse, a later explosion).</summary>
        public void ApplyDamageImpulse(in DamageInfo d)
        {
            var dir = d.Direction.sqrMagnitude > 1e-6f ? d.Direction.normalized : -transform.forward;
            if (d.Type == DamageType.Explosion)
            {
                // GDD §13.2: lethal ≤ 4 m, falloff to 8 m; ~4-5 m/s over the whole body at the lethal edge.
                foreach (var b in _bones)
                    if (b != null && b.Body != null && !b.Body.isKinematic)
                        b.Body.AddExplosionForce(38f, d.Point, 8f, 0.8f, ForceMode.Impulse);
                return;
            }
            var hit = NearestBody(d.Point);
            if (hit == null) return;
            // Bullet impulse grows with damage (AK 20-50 per hit → ~35-55 N·s on the struck bone); melee shoves harder.
            var impulse = d.Type == DamageType.Melee ? 60f : Mathf.Clamp(25f + d.Amount * 0.6f, 25f, 90f);
            hit.AddForceAtPosition(dir * impulse, d.Point, ForceMode.Impulse);
            var pelvis = Pelvis;
            if (pelvis != null && pelvis != hit && !pelvis.isKinematic) pelvis.AddForce(dir * (impulse * 0.35f), ForceMode.Impulse);
        }

        private void FixedUpdate()
        {
            if (!_active || _frozen) return;
            _activeTime += Time.fixedDeltaTime;
            var sleeping = true;
            foreach (var b in _bones)
            {
                if (b == null || b.Body == null) continue;
                if (b.Body.linearVelocity.sqrMagnitude > 0.0025f || b.Body.angularVelocity.sqrMagnitude > 0.01f)
                {
                    sleeping = false;
                    break;
                }
            }
            _sleepTime = sleeping ? _sleepTime + Time.fixedDeltaTime : 0f;
            if (_sleepTime > 0.6f || _activeTime > _maxSimSeconds) Freeze();
        }

        /// <summary>Stops simulating: bodies kinematic, colliders off (a settled corpse costs nothing).</summary>
        public void Freeze()
        {
            if (!_active || _frozen) return;
            _frozen = true;
            foreach (var b in _bones)
            {
                if (b == null) continue;
                if (b.Body != null)
                {
                    b.Body.isKinematic = true;
                    b.Body.interpolation = RigidbodyInterpolation.None;
                }
                if (b.Collider != null) b.Collider.enabled = false;
            }
        }

        /// <summary>Back to the alive state (pool reuse); the caller re-enables and rebinds the Animator.</summary>
        public void Restore()
        {
            SetAlive();
            if (_animator != null) _animator.enabled = true;
        }

        /// <summary>The rigidbody whose collider is closest to a world point.</summary>
        public Rigidbody NearestBody(Vector3 point)
        {
            Rigidbody best = null;
            var bestD = float.PositiveInfinity;
            foreach (var b in _bones)
            {
                if (b == null || b.Body == null || b.Body.isKinematic) continue;
                var p = b.Collider != null && b.Collider.enabled ? b.Collider.ClosestPoint(point) : b.Body.worldCenterOfMass;
                var dd = (p - point).sqrMagnitude;
                if (dd < bestD)
                {
                    bestD = dd;
                    best = b.Body;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        /// <summary>Builders: sets the bones (pelvis first), the alive layer and the root colliders to disable at death.</summary>
        public void EditorSetup(RagdollBone[] bones, int aliveLayer, Animator animator, Collider[] aliveOnly)
        {
            _bones = bones ?? new RagdollBone[0];
            _aliveLayer = aliveLayer;
            _animator = animator;
            _aliveOnlyColliders = aliveOnly ?? new Collider[0];
        }
#endif
    }
}
