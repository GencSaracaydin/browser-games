using UnityEngine;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// AI hit chance per shot (GDD §14.3): base accuracy × range curve × player motion (still 1.0, walking 0.8,
    /// sprinting 0.6, sliding 0.5) × in-cover 0.6 × time-on-target ramp (0.5 → 1.0 over 2 s, reset when line of sight
    /// breaks) × flood 1.25 × difficulty; a shooter suppressed by squad fire shoots at half accuracy (§14.3
    /// Suppression). Misses are placed 0.5-3 m from the player by WP03 CombatService. Pure functions.
    /// </summary>
    public static class HitChance
    {
        /// <summary>Distance at which archetype accuracy is specified (GDD §14.1 "Accuracy at 20 m").</summary>
        public const float ReferenceDistance = 20f;
        /// <summary>Range factor cap at close range.</summary>
        public const float MaxRangeFactor = 1.8f;
        /// <summary>Range factor floor at extreme range.</summary>
        public const float MinRangeFactor = 0.1f;
        /// <summary>In-cover factor (GDD §14.3).</summary>
        public const float CoverFactor = 0.6f;
        /// <summary>Flood bonus (GDD §7.6, §14.3).</summary>
        public const float FloodFactor = 1.25f;
        /// <summary>Suppressed shooter accuracy factor (GDD §14.3: rate and accuracy ×0.5 for 2 s).</summary>
        public const float SuppressedFactor = 0.5f;
        /// <summary>Time-on-target ramp start (GDD §14.3).</summary>
        public const float RampStart = 0.5f;
        /// <summary>Seconds for the ramp to reach 1.0 (GDD §14.3).</summary>
        public const float RampSeconds = 2f;
        /// <summary>No shot is ever certain (keeps misses and whizzes in the mix).</summary>
        public const float MaxHitChance = 0.95f;

        /// <summary>The full formula, clamped to [0, <see cref="MaxHitChance"/>].</summary>
        public static float Compute(in HitChanceInputs i)
        {
            var p = Mathf.Max(0f, i.BaseAccuracy);
            p *= i.FlatRange ? 1f : RangeFactor(i.Distance, i.RangeMax);
            if (i.ApplyMotionFactor) p *= MotionFactor(i.TargetMotion);
            if (i.TargetInCover) p *= CoverFactor;
            p *= TimeOnTargetFactor(i.TimeOnTarget);
            if (i.TargetInFlood) p *= FloodFactor;
            p *= i.DifficultyMultiplier > 0f ? i.DifficultyMultiplier : 1f;
            if (i.ShooterSuppressed) p *= SuppressedFactor;
            return Mathf.Clamp(p, 0f, MaxHitChance);
        }

        /// <summary>
        /// Range curve: 1.0 at 20 m, sqrt(20 / d) around it (1.41 at 10 m, 0.71 at 40 m), capped at 1.8 close in, and an
        /// extra (rangeMax / d)² falloff beyond the archetype's range band. (GDD §14.1 gives only "accuracy at 20 m" and a
        /// range band; the curve shape is a WP04 decision, Docs/DECISIONS.md.)
        /// </summary>
        public static float RangeFactor(float distance, float rangeMax)
        {
            var d = Mathf.Max(1f, distance);
            var f = Mathf.Min(MaxRangeFactor, Mathf.Sqrt(ReferenceDistance / d));
            if (rangeMax > 0f && d > rangeMax)
            {
                var k = rangeMax / d;
                f *= k * k;
            }
            return Mathf.Max(MinRangeFactor, f);
        }

        /// <summary>Player-motion factor (GDD §14.3).</summary>
        public static float MotionFactor(MotionClass m)
        {
            switch (m)
            {
                case MotionClass.Walk: return 0.8f;
                case MotionClass.Sprint: return 0.6f;
                case MotionClass.Slide: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>Time-on-target ramp 0.5 → 1.0 over 2 s (GDD §14.3).</summary>
        public static float TimeOnTargetFactor(float seconds) =>
            Mathf.Lerp(RampStart, 1f, Mathf.Clamp01(seconds / RampSeconds));

        /// <summary>Greycoat Marksman accuracy by target motion (GDD §14.1: 0.8 still, 0.35 moving, 0.2 sprinting).</summary>
        public static float MarksmanAccuracy(MotionClass m)
        {
            switch (m)
            {
                case MotionClass.Still: return 0.8f;
                case MotionClass.Walk: return 0.35f;
                default: return 0.2f; // sprinting and sliding
            }
        }
    }
}
