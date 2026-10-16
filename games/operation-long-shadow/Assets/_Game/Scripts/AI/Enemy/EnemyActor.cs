using System;
using LongShadow.AI.Common;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Identity and targeting surface of an enemy (<see cref="ITargetable"/>, <see cref="ISuppressible"/>): actor id from
    /// the encounter table, team, perch tag, kill-share bookkeeping (<see cref="LastEngagedByPlayer"/>,
    /// <see cref="SquadTarget"/>), suppression (squad fire halves rate and accuracy for 2 s, GDD §14.3), hit points from
    /// the archetype, registration in <see cref="ITargetRegistry"/>. The body is any <see cref="ICharacterBody"/> on
    /// this GameObject (WP05 HumanoidBody, or <see cref="DebugCapsuleBody"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour, ITargetable, ISuppressible
    {
        /// <summary>Seconds of suppression from a squad hit (GDD §14.3).</summary>
        public const float SquadSuppressionSeconds = 2f;

        [SerializeField, Tooltip("Prefab variant id (EnemyVariants).")] private string _variantId;
        [SerializeField] private ArchetypeId _archetype = ArchetypeId.LegionRifleman;
        [SerializeField, Tooltip("Actor id when placed by hand (the spawner overrides it).")] private string _actorId;

        private EnemyHealth _health;
        private ICharacterBody _body;
        private EnemyBrain _brain;
        private ServiceRef<ITargetRegistry> _targets;
        private PerchTag _perch = PerchTag.Ground;
        private bool _squadTarget;
        private float _lastEngaged = float.NegativeInfinity;
        private float _suppressedUntil = float.NegativeInfinity;
        private Vector3 _velocity;
        private Vector3 _lastPosition;
        private bool _registered;
        private bool _subscribed;

        /// <summary>Raised once when the enemy dies.</summary>
        public event Action<EnemyActor, DamageInfo> Died;

        /// <summary>Variant id.</summary>
        public string VariantId => _variantId;
        /// <summary>Archetype id.</summary>
        public ArchetypeId ArchetypeId => _archetype;
        /// <summary>Archetype data.</summary>
        public Archetype Archetype => ArchetypeTable.Get(_archetype);
        /// <summary>Health component.</summary>
        public EnemyHealth Health => _health != null ? _health : (_health = GetComponent<EnemyHealth>());
        /// <summary>Body.</summary>
        public ICharacterBody Body => _body ?? (_body = GetComponent<ICharacterBody>());
        /// <summary>Brain.</summary>
        public EnemyBrain Brain => _brain != null ? _brain : (_brain = GetComponent<EnemyBrain>());

        /// <inheritdoc/>
        public string ActorId => _actorId;
        /// <inheritdoc/>
        public Transform Root => transform;
        /// <inheritdoc/>
        public Vector3 Velocity => _velocity;
        /// <inheritdoc/>
        public PerchTag Perch => _perch;
        /// <inheritdoc/>
        public bool SquadTarget { get => _squadTarget; set => _squadTarget = value; }
        /// <inheritdoc/>
        public float LastEngagedByPlayer => _lastEngaged;
        /// <inheritdoc/>
        public Team Team => Health != null ? Health.Team : Archetype.Team;
        /// <inheritdoc/>
        public bool IsAlive => Health != null && Health.IsAlive && isActiveAndEnabled;
        /// <inheritdoc/>
        public Transform AimPoint => Health != null ? Health.AimPoint : transform;

        /// <summary>True while suppressed by squad fire.</summary>
        public bool IsSuppressed(float now) => now < _suppressedUntil;

        /// <summary>Sets variant and archetype (builders).</summary>
        public void Setup(string variantId, ArchetypeId archetype)
        {
            _variantId = variantId;
            _archetype = archetype;
        }

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _body = GetComponent<ICharacterBody>();
            _brain = GetComponent<EnemyBrain>();
            _lastPosition = transform.position;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterTarget();
        }

        private void Subscribe()
        {
            if (_subscribed || Health == null) return;
            Health.Damaged += OnDamaged;
            Health.Killed += OnKilled;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Killed -= OnKilled;
            _subscribed = false;
        }

        /// <summary>
        /// Prepares a (possibly pooled) enemy for a spawn: id, hit points, team, perch, position, NavMesh placement.
        /// Static enemies (perch, vehicle bed, static archetypes) get their agent disabled so it never snaps them to the
        /// ground (TECH_PLAN §7.6 perch-only rule).
        /// </summary>
        public void Prepare(string actorId, Vector3 position, Quaternion rotation, PerchTag perch, bool isStatic)
        {
            _actorId = actorId;
            _perch = perch;
            _squadTarget = false;
            _lastEngaged = float.NegativeInfinity;
            _suppressedUntil = float.NegativeInfinity;
            var a = Archetype;
            var h = Health;
            if (h != null)
            {
                h.ActorId = actorId;
                h.Invulnerable = false;
                h.ApplyArchetype(a.Hp, a.TorsoArmour, a.Team);                                                   // GDD §14.1
                h.SetShield(a.Id == ArchetypeId.TechnicalGunner, MathX.BearingOf(rotation * Vector3.forward), 60f); // GDD §14.1
            }
            (Body as DebugCapsuleBody)?.ResetBody();
            var agent = Body?.Agent;
            if (agent != null) agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            // Only on a NavMesh: an agent enabled off-mesh logs a warning; without one the body moves in straight lines.
            if (agent != null && !isStatic && NavMesh.SamplePosition(position, out var hit, 2.5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
                agent.updateRotation = false;
                LinkTraversal.Prepare(agent);
            }
            _lastPosition = transform.position;
            _velocity = Vector3.zero;
            Subscribe();
            RegisterTarget();
        }

        /// <summary>Velocity sampling (called by the brain each tick).</summary>
        public void SampleVelocity(float dt)
        {
            var agent = Body?.Agent;
            if (NavUtil.Usable(agent)) _velocity = agent.velocity;
            else if (dt > 0f) _velocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;
        }

        /// <summary>Sets the perch tag (moved onto a balcony or roof).</summary>
        public void SetPerch(PerchTag perch) => _perch = perch;

        /// <inheritdoc/>
        public void NotifyEngagedByPlayer() => _lastEngaged = Time.time;

        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            var h = Health;
            if (h != null) ((IDamageable)h).ApplyDamage(d);
        }

        /// <inheritdoc/>
        public void Suppress(float seconds, Team by)
        {
            if (!IsAlive || !Teams.AreHostile(Team, by)) return;
            _suppressedUntil = Mathf.Max(_suppressedUntil, Time.time + seconds);
            Brain?.OnSuppressed(by);
        }

        private void OnDamaged(LongShadow.Core.Health h, DamageInfo d)
        {
            if (d.ByPlayer) _lastEngaged = Time.time;
            if (d.InstigatorTeam == Team.Squad) Suppress(SquadSuppressionSeconds, Team.Squad);
            Brain?.OnDamaged(d);
        }

        private void OnKilled(LongShadow.Core.Health h, DamageInfo d)
        {
            UnregisterTarget();
            Body?.Die(d);
            Brain?.OnDeath(d);
            Died?.Invoke(this, d);
        }

        /// <summary>Removes the enemy from the target registry and AI (despawn).</summary>
        public void Retire()
        {
            UnregisterTarget();
            Brain?.Retire();
        }

        private void RegisterTarget()
        {
            if (_registered) return;
            var reg = _targets.Value ?? TargetRegistry.EnsureExists();
            if (reg == null) return;
            reg.Register(this);
            _registered = true;
        }

        private void UnregisterTarget()
        {
            if (!_registered) return;
            _targets.Value?.Unregister(this);
            _registered = false;
        }
    }
}
