using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Dev
{
    /// <summary>
    /// A stand-in player for the AI dev arena when the WP02 rig is not used: an <see cref="ITargetable"/> of team Player
    /// that patrols waypoints (with pauses and crouches), makes footstep noise, and optionally "fires" at the nearest
    /// visible enemy (60 m gunshot noise, ShotFired, engagement for the kill-share rule, a rolled hit). Takes AI fire on
    /// an invulnerable <see cref="Health"/> and counts the hits, so tokens and hit chances can be watched in the overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIDevTarget : MonoBehaviour, ITargetable
    {
        [SerializeField] private Vector3[] _patrol = new Vector3[0];
        [SerializeField] private float _speed = 3f;
        [SerializeField] private float _pause = 3f;
        [SerializeField, Tooltip("Shoot back at enemies (noise, engagement, damage).")] private bool _fireBack = true;
        [SerializeField] private float _fireInterval = 0.9f;
        [SerializeField] private float _hitChance = 0.35f;
        [SerializeField] private float _damage = 34f;
        [SerializeField] private bool _crouchAtPauses = true;

        private Health _health;
        private int _index;
        private float _pauseUntil;
        private float _nextStep;
        private float _nextShot;
        private Vector3 _velocity;
        private Vector3 _last;
        private bool _crouched;
        private ServiceRef<ITargetRegistry> _targets;
        private static readonly System.Collections.Generic.List<ITargetable> s_hostiles = new System.Collections.Generic.List<ITargetable>(16);

        /// <summary>Hits taken (invulnerable).</summary>
        public int HitsTaken { get; private set; }
        /// <summary>True while crouched (detection is slower).</summary>
        public bool Crouched => _crouched;

        /// <summary>Configures the patrol (builder).</summary>
        public void Configure(Vector3[] patrol, bool fireBack)
        {
            _patrol = patrol ?? new Vector3[0];
            _fireBack = fireBack;
        }

        /// <inheritdoc/>
        public string ActorId => SquadIds.Player;
        /// <inheritdoc/>
        public Transform Root => transform;
        /// <inheritdoc/>
        public Vector3 Velocity => _velocity;
        /// <inheritdoc/>
        public PerchTag Perch => PerchTag.Ground;
        /// <inheritdoc/>
        public bool SquadTarget { get => false; set { } }
        /// <inheritdoc/>
        public float LastEngagedByPlayer => float.NegativeInfinity;
        /// <inheritdoc/>
        public void NotifyEngagedByPlayer() { }
        /// <inheritdoc/>
        public Team Team => Team.Player;
        /// <inheritdoc/>
        public bool IsAlive => isActiveAndEnabled;
        /// <inheritdoc/>
        public Transform AimPoint => _health != null ? _health.AimPoint : transform;
        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            if (_health != null) _health.ApplyDamage(d);
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_health == null) _health = gameObject.AddComponent<Health>();
            _health.SetTeam(Team.Player);
            _health.ActorId = SquadIds.Player;
            _health.Invulnerable = true;
            _last = transform.position;
        }

        private void OnEnable()
        {
            (_targets.Value ?? TargetRegistry.EnsureExists())?.Register(this);
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            _targets.Value?.Unregister(this);
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(Health h, DamageInfo d)
        {
            if (Teams.AreHostile(Team.Player, d.InstigatorTeam)) HitsTaken++;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var now = Time.time;
            if (dt <= 0f) return;
            Move(now, dt);
            _velocity = (transform.position - _last) / dt;
            _last = transform.position;
            if (_velocity.sqrMagnitude > 1f && now >= _nextStep)
            {
                _nextStep = now + 0.5f;
                GameEvents.RaiseNoise(new NoiseEvent(transform.position, _crouched ? PerceptionMath.HearCrouch : PerceptionMath.HearWalk, Team.Player, NoiseKind.Footstep));
            }
            if (_fireBack && now >= _nextShot) Fire(now);
        }

        private void Move(float now, float dt)
        {
            if (_patrol == null || _patrol.Length == 0 || now < _pauseUntil) return;
            _crouched = false;
            var target = _patrol[_index % _patrol.Length];
            var to = target - transform.position;
            to.y = 0f;
            if (to.magnitude <= _speed * dt + 0.05f)
            {
                transform.position = new Vector3(target.x, transform.position.y, target.z);
                _index = (_index + 1) % _patrol.Length;
                _pauseUntil = now + _pause;
                _crouched = _crouchAtPauses;
                return;
            }
            transform.position += to.normalized * (_speed * dt);
            transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        private void Fire(float now)
        {
            _nextShot = now + _fireInterval;
            var reg = _targets.Value;
            if (reg == null) return;
            reg.GetHostilesOf(Team.Player, s_hostiles);
            ITargetable best = null;
            var bestD = 50f * 50f;
            var eye = transform.position + Vector3.up * (_crouched ? 1.0f : 1.6f);
            for (var i = 0; i < s_hostiles.Count; i++)
            {
                var t = s_hostiles[i];
                if (t?.AimPoint == null || !t.IsAlive) continue;
                var d = (t.AimPoint.position - eye).sqrMagnitude;
                if (d >= bestD) continue;
                if (Physics.Linecast(eye, t.AimPoint.position, LayerIds.AISight, QueryTriggerInteraction.Ignore)) continue;
                bestD = d;
                best = t;
            }
            s_hostiles.Clear();
            if (best == null) return;
            var aim = best.AimPoint.position;
            var dir = (aim - eye).normalized;
            best.NotifyEngagedByPlayer();
            GameEvents.RaiseShotFired(new ShotEvent { Origin = eye, Direction = dir, Team = Team.Player, WeaponId = WeaponIds.Commando, NoiseRadius = PerceptionMath.HearShot, IsPlayer = true });
            GameEvents.RaiseNoise(new NoiseEvent(eye, PerceptionMath.HearShot, Team.Player, NoiseKind.Gunshot));
            GameEvents.RaiseTracerPath(eye, aim, Team.Player);
            if (Random.value < _hitChance)
                best.ApplyDamage(new DamageInfo(_damage, DamageType.Bullet, HitZone.Torso, aim, dir, gameObject, Team.Player, WeaponIds.Commando));
        }
    }
}
