using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Generic hit points for enemies, squad and destructible actors (TECH_PLAN §6.4). Subtracts
    /// <see cref="DamageInfo.Amount"/> as given (callers apply weapon zone multipliers and <see cref="ArmourFactor"/>);
    /// raises <see cref="Damaged"/>/<see cref="Killed"/> and <see cref="GameEvents.ActorDamaged"/>/<see cref="GameEvents.ActorKilled"/>.
    /// Invulnerable actors (squad) and suppressive hits change nothing but still raise Damaged with amount 0 so hit
    /// reactions play. Implements <see cref="ISaveable"/> (key "Health.&lt;ActorId&gt;"); spawners register it if needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable, ISaveable
    {
        [SerializeField, Tooltip("Hit points (GDD §14.1: rifleman 100, gunner 130, Greycoat 150, marksman 120).")]
        private float _maxHp = 100f;
        [SerializeField] private Team _team = Team.Legion;
        [SerializeField, Tooltip("Stable actor id (encounter enemy id, squad id).")]
        private string _actorId;
        [SerializeField, Tooltip("Torso damage factor (Greycoat vest 0.8, GDD §14.1).")]
        private float _torsoArmour = 1f;
        [SerializeField, Tooltip("AI aim point (chest). Created at 1.3 m if empty.")]
        private Transform _aimPoint;
        [SerializeField] private bool _invulnerable;

        private float _current;
        private float _baseMax;
        private bool _alive = true;
        private bool _initialised;

        /// <summary>Raised after any damage (amount may be 0 for invulnerable/suppressive hits).</summary>
        public event Action<Health, DamageInfo> Damaged;
        /// <summary>Raised once on death.</summary>
        public event Action<Health, DamageInfo> Killed;

        /// <inheritdoc/>
        public Team Team => _team;
        /// <inheritdoc/>
        public bool IsAlive => _alive;
        /// <summary>Current hit points.</summary>
        public float Current { get { Init(); return _current; } }
        /// <summary>Maximum hit points (may be overridden, see <see cref="OverrideMax"/>).</summary>
        public float Max { get { Init(); return _maxHp; } }
        /// <summary>Current / Max.</summary>
        public float Normalized => Max > 0f ? Current / Max : 0f;
        /// <summary>Stable actor id.</summary>
        public string ActorId { get => _actorId; set => _actorId = value; }
        /// <summary>No damage while true (squad).</summary>
        public bool Invulnerable { get => _invulnerable; set => _invulnerable = value; }
        /// <summary>Torso armour factor.</summary>
        public float TorsoArmour => _torsoArmour;
        /// <inheritdoc/>
        public Transform AimPoint { get { EnsureAimPoint(); return _aimPoint; } }

        /// <summary>Armour factor for a zone (torso armour for Torso, else 1). Callers multiply damage by it.</summary>
        public float ArmourFactor(HitZone zone) => zone == HitZone.Torso ? _torsoArmour : 1f;

        /// <summary>Sets the team (pooled bodies reused across factions).</summary>
        public void SetTeam(Team team) => _team = team;

        protected virtual void Awake()
        {
            Init();
            EnsureAimPoint();
        }

        private void Init()
        {
            if (_initialised) return;
            _initialised = true;
            _baseMax = _maxHp;
            _current = _maxHp;
        }

        private void EnsureAimPoint()
        {
            if (_aimPoint != null) return;
            var t = new GameObject("AimPoint").transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3(0f, 1.3f, 0f);
            _aimPoint = t;
        }

        /// <inheritdoc/>
        public void ApplyDamage(in DamageInfo d)
        {
            Init();
            if (!_alive) return;
            if (_invulnerable || d.Suppressive || d.Amount <= 0f)
            {
                var zero = d.WithAmount(0f);
                Damaged?.Invoke(this, zero);
                GameEvents.RaiseActorDamaged(this, zero);
                return;
            }
            _current = Mathf.Max(0f, _current - d.Amount);
            Damaged?.Invoke(this, d);
            GameEvents.RaiseActorDamaged(this, d);
            if (_current <= 0f) Die(d);
        }

        /// <summary>Kills immediately (scripted kills); ignores invulnerability.</summary>
        public void Kill(in DamageInfo d)
        {
            Init();
            if (!_alive) return;
            _current = 0f;
            Die(d);
        }

        /// <summary>Restores hit points (clamped to Max).</summary>
        public void Heal(float amount)
        {
            Init();
            if (_alive && amount > 0f) _current = Mathf.Min(_maxHp, _current + amount);
        }

        /// <summary>Back to full health and alive (pooling, checkpoint restore).</summary>
        public void ResetFull()
        {
            Init();
            _alive = true;
            _current = _maxHp;
        }

        /// <summary>Overrides Max and current HP (sentries at 30 HP while unaware, GDD §14.1).</summary>
        public void OverrideMax(float hp)
        {
            Init();
            _maxHp = Mathf.Max(1f, hp);
            _current = Mathf.Min(_current, _maxHp);
            if (_alive && _current <= 0f) _current = _maxHp;
        }

        /// <summary>Restores the authored Max, keeping the damage already taken.</summary>
        public void RestoreMax()
        {
            Init();
            var damage = _maxHp - _current;
            _maxHp = _baseMax;
            if (_alive) _current = Mathf.Clamp(_maxHp - damage, 1f, _maxHp);
        }

        private void Die(in DamageInfo d)
        {
            _alive = false;
            Killed?.Invoke(this, d);
            GameEvents.RaiseActorKilled(new KillInfo
            {
                VictimId = _actorId,
                VictimTeam = _team,
                Damage = d,
                Headshot = d.Zone == HitZone.Head && (d.Type == DamageType.Bullet || d.Type == DamageType.Melee),
                ByPlayer = d.InstigatorTeam == Team.Player,
            });
        }

        /// <inheritdoc/>
        public string SaveKey => "Health." + (string.IsNullOrEmpty(_actorId) ? name : _actorId);

        /// <inheritdoc/>
        public void Capture(SaveWriter w)
        {
            Init();
            w.Write("hp", _current);
            w.Write("max", _maxHp);
            w.Write("alive", _alive);
        }

        /// <inheritdoc/>
        public void Restore(SaveReader r)
        {
            Init();
            _maxHp = r.ReadFloat("max", _baseMax);
            _current = r.ReadFloat("hp", _maxHp);
            _alive = r.ReadBool("alive", true);
        }
    }
}
