using LongShadow.Combat.Data;
using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>Bullet damage over distance (GDD §13.1 "Damage, close → far" over the "Falloff range"): flat, linear, flat.</summary>
    public static class DamageFalloff
    {
        /// <summary>
        /// Damage at <paramref name="distance"/>: <paramref name="close"/> up to <paramref name="start"/>, linear to
        /// <paramref name="far"/> at <paramref name="end"/>, <paramref name="far"/> beyond. A zero-length range is a step at start.
        /// </summary>
        public static float Evaluate(float close, float far, float start, float end, float distance)
        {
            if (distance <= start) return close;
            if (distance >= end || end <= start) return far;
            return Mathf.Lerp(close, far, (distance - start) / (end - start));
        }

        /// <summary>Base (torso, unarmoured) damage of a weapon at a distance.</summary>
        public static float Evaluate(WeaponSpec w, float distance) =>
            Evaluate(w.DamageClose, w.DamageFar, w.FalloffStart, w.FalloffEnd, distance);
    }
}
