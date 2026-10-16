using LongShadow.Core;
using UnityEngine;

namespace LongShadow.AI.Enemy
{
    /// <summary>
    /// Core <see cref="Health"/> with the enemy-only damage rules applied before the hit points change: the technical's
    /// gun shield (GDD §14.1: immune within ±60° of his facing). It re-implements <see cref="IDamageable"/> so every hit
    /// that arrives through the interface (Core <see cref="Hitbox"/>, WP03 CombatService, explosions) is filtered.
    /// Blocked hits still raise Damaged with amount 0 (sparks, hit reactions).
    /// </summary>
    public sealed class EnemyHealth : Health, IDamageable
    {
        [SerializeField, Tooltip("Gun shield on (technical gunner).")] private bool _shield;
        [SerializeField, Tooltip("Compass facing the shield protects (E4-01: 234°).")] private float _shieldFacing = 234f;
        [SerializeField, Tooltip("Half-arc of the shield in degrees (GDD §14.1: ±60°).")] private float _shieldHalfArc = 60f;

        /// <summary>True while the gun shield protects.</summary>
        public bool ShieldEnabled => _shield;

        /// <summary>Hits blocked by the shield since the last reset (diagnostics, tests).</summary>
        public int BlockedHits { get; private set; }

        /// <summary>
        /// Applies an archetype's hit points, torso armour (Greycoat vest ×0.8, read by WP03 through
        /// <see cref="Health.ArmourFactor"/>) and team, then returns to full health. The serialized base fields are written
        /// with <see cref="JsonUtility.FromJsonOverwrite(string, object)"/> (Unity serialization, no System.Reflection, R5).
        /// </summary>
        public void ApplyArchetype(float hp, float torsoArmour, Team team)
        {
            var json = "{\"_maxHp\":" + hp.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"_torsoArmour\":" + torsoArmour.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       ",\"_team\":" + ((int)team).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            JsonUtility.FromJsonOverwrite(json, this);
            SetTeam(team);
            OverrideMax(hp);
            ResetFull();
        }

        /// <summary>
        /// Changes the maximum hit points and keeps the damage already taken (sentries go from 30 HP while unaware back
        /// to their archetype HP when alerted, GDD §7.2).
        /// </summary>
        public void SetMaxKeepDamage(float hp)
        {
            if (!IsAlive) return;
            var damage = Mathf.Max(0f, Max - Current);
            OverrideMax(hp);
            var target = Mathf.Max(1f, Max - damage);
            if (target > Current) Heal(target - Current);
        }

        /// <summary>Configures the gun shield (facing in compass degrees, half-arc in degrees).</summary>
        public void SetShield(bool on, float facing, float halfArc)
        {
            _shield = on;
            _shieldFacing = facing;
            _shieldHalfArc = halfArc;
            BlockedHits = 0;
        }

        /// <summary>True if damage arriving with this info would be stopped by the shield.</summary>
        public bool IsShielded(in DamageInfo d)
        {
            if (!_shield || d.Type == DamageType.Script || d.Type == DamageType.Fall) return false;
            Vector3 toAttacker;
            if (d.Instigator != null) toAttacker = d.Instigator.transform.position - transform.position;
            else toAttacker = -d.Direction;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 1e-4f) return false;
            var bearing = MathX.BearingOf(toAttacker);
            return Mathf.Abs(MathX.WrapAngle180(bearing - _shieldFacing)) <= _shieldHalfArc;
        }

        void IDamageable.ApplyDamage(in DamageInfo d)
        {
            if (IsShielded(d))
            {
                BlockedHits++;
                ApplyDamage(d.WithAmount(0f));
                return;
            }
            ApplyDamage(d);
        }
    }
}
