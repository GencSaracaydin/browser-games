using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// One application of damage. <see cref="Amount"/> is the FINAL amount to subtract: the caller (WP03
    /// DamageResolver, CombatService) has already applied falloff, zone multipliers (<see cref="IHitbox.Multiplier"/> or a
    /// weapon override such as the Hush Puppy head x3.0) and armour. Receivers only apply their own modifiers.
    /// Hostile <b>bullet</b> damage to the player arrives already difficulty-scaled (WP04 <c>ArchetypeTable.DamageToPlayer</c>:
    /// Regular x <c>EnemyDamageMultiplier</c>, the marksman's absolute <c>MarksmanDamagePerHit</c>); the player scales hostile
    /// <b>non-bullet</b> damage (explosions, fire, melee) by <c>EnemyDamageMultiplier</c>; falls, scripted and friendly
    /// damage are never scaled. Invulnerability and last-stand immunity stay with the receiver.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Hit points to subtract (already multiplied).</summary>
        public float Amount;
        /// <summary>Cause of the damage.</summary>
        public DamageType Type;
        /// <summary>Body zone that was hit (Torso for explosions and falls).</summary>
        public HitZone Zone;
        /// <summary>World-space hit point.</summary>
        public Vector3 Point;
        /// <summary>World-space direction the damage travelled (from instigator towards victim), normalised.</summary>
        public Vector3 Direction;
        /// <summary>The shooter / thrower root, if any. Used for the HUD damage indicator.</summary>
        public GameObject Instigator;
        /// <summary>Team of the instigator.</summary>
        public Team InstigatorTeam;
        /// <summary>Weapon id from WeaponTable ("COMMANDO", "AK", "M67", ...), or null.</summary>
        public string WeaponId;
        /// <summary>True for squad suppressive fire (visual hits that must not kill, TECH_PLAN §7.7 kill share).</summary>
        public bool Suppressive;

        /// <summary>Convenience constructor for the common fields.</summary>
        public DamageInfo(float amount, DamageType type, HitZone zone, Vector3 point, Vector3 direction,
                          GameObject instigator, Team instigatorTeam, string weaponId = null)
        {
            Amount = amount;
            Type = type;
            Zone = zone;
            Point = point;
            Direction = direction;
            Instigator = instigator;
            InstigatorTeam = instigatorTeam;
            WeaponId = weaponId;
            Suppressive = false;
        }

        /// <summary>Returns a copy with a different amount.</summary>
        public DamageInfo WithAmount(float amount)
        {
            var d = this;
            d.Amount = amount;
            return d;
        }

        /// <summary>True when the player caused this damage.</summary>
        public bool ByPlayer => InstigatorTeam == Team.Player;
    }
}
