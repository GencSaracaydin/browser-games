using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Explosion numbers (GDD §13.2 frag: 220 at the centre, lethal within 4.0 m, linear falloff to 8.0 m; §14.1 RPG 4 m
    /// splash; TECH_PLAN §7.5: shake, tinnitus &lt; 6 m).
    /// </summary>
    public static class ExplosionMath
    {
        /// <summary>Full damage inside the lethal radius, linear to zero at the max radius, zero beyond.</summary>
        public static float DamageAt(float distance, float damage, float lethalRadius, float maxRadius)
        {
            if (distance <= lethalRadius) return damage;
            if (distance >= maxRadius || maxRadius <= lethalRadius) return 0f;
            return damage * (1f - (distance - lethalRadius) / (maxRadius - lethalRadius));
        }

        /// <summary>Camera trauma 0..1 for the player at <paramref name="distance"/> (felt out to 3× the damage radius).</summary>
        public static float Trauma(float distance, float maxRadius)
        {
            var reach = Mathf.Max(1f, maxRadius * 3f);
            var t = 1f - Mathf.Clamp01(distance / reach);
            return Mathf.Clamp01(t * t * 1.15f);
        }

        /// <summary>Tinnitus seconds (0 outside the radius): 2.5 s at the centre down to 0.8 s at the edge.</summary>
        public static float TinnitusSeconds(float distance, float radius)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            return Mathf.Lerp(2.5f, 0.8f, distance / radius);
        }

        /// <summary>Impulse (N·s) for ragdolls and debris at a distance (tuning).</summary>
        public static float Impulse(float distance, float maxRadius, float strength = 9f)
        {
            if (maxRadius <= 0f || distance >= maxRadius * 1.25f) return 0f;
            return strength * (1f - Mathf.Clamp01(distance / (maxRadius * 1.25f)));
        }
    }
}
