using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Final damage of a player bullet (TECH_PLAN §7.4 DamageResolver): falloff × zone multiplier (the hitbox's, or the
    /// weapon's override such as the Hush Puppy head ×3.0) × armour (Greycoat vest: torso ×0.8). The result is the final
    /// <see cref="DamageInfo.Amount"/>; receivers only apply their own modifiers.
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>Zone multiplier: weapon override if it has one for this zone, else the hitbox's own multiplier.</summary>
        public static float ZoneMultiplier(WeaponSpec w, HitZone zone, float hitboxMultiplier)
        {
            if (w != null)
            {
                if (zone == HitZone.Head && w.HeadMultiplierOverride > 0f) return w.HeadMultiplierOverride;
                if (zone == HitZone.Limb && w.LimbMultiplierOverride > 0f) return w.LimbMultiplierOverride;
            }
            return hitboxMultiplier > 0f ? hitboxMultiplier : Hitbox.DefaultMultiplier(zone);
        }

        /// <summary>Final damage of one bullet.</summary>
        public static float Resolve(WeaponSpec w, float distance, HitZone zone, float hitboxMultiplier, float armourFactor, float penetrationScale = 1f)
        {
            var baseDamage = DamageFalloff.Evaluate(w, distance);
            return baseDamage * ZoneMultiplier(w, zone, hitboxMultiplier) * Mathf.Max(0f, armourFactor) * Mathf.Max(0f, penetrationScale);
        }

        /// <summary>Bullets of <paramref name="w"/> needed to bring <paramref name="hp"/> to zero with hits in one zone.</summary>
        public static int HitsToKill(WeaponSpec w, float distance, float hp, HitZone zone = HitZone.Torso, float armourFactor = 1f)
        {
            var d = Resolve(w, distance, zone, Hitbox.DefaultMultiplier(zone), armourFactor);
            if (d <= 0f) return int.MaxValue;
            // Tolerate float noise (3 × 33.333 must count as 100).
            return Mathf.CeilToInt(hp / d - 1e-4f);
        }

        /// <summary>Armour factor of a damageable for a zone (Core Health's torso armour; 1 for anything else).</summary>
        public static float ArmourOf(IDamageable target, HitZone zone) => target is Health h ? h.ArmourFactor(zone) : 1f;
    }
}
