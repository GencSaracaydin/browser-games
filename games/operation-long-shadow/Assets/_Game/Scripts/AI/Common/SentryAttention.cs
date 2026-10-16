namespace LongShadow.AI.Common
{
    /// <summary>
    /// Attention of the Beat 2 pump-house sentries while they smoke and chat (GDD §7.2 silent opener, §14.2 "Suspicious
    /// state ... used only in Beat 2"). The GDD's 0.4-1.2 s detection time would spot the player at the firing position
    /// (16 m, inside their facing) long before Hollis's "Mark", so distracted sentries get narrower attention (WP04
    /// decision, Docs/DECISIONS.md): sight capped at 18 m, a 70° cone, detection ×3 slower, and a crouched player who
    /// stands still beyond 8 m adds nothing to the meter. Inside 8 m, sprinting or standing in the open fills it fast;
    /// noises (unsuppressed shots, sprint footsteps) still alert them at once. The HUD meter shows the result. Pure.
    /// </summary>
    public static class SentryAttention
    {
        /// <summary>Sight range cap while distracted (m).</summary>
        public const float RangeCap = 18f;
        /// <summary>Field of view while distracted (degrees).</summary>
        public const float Fov = 70f;
        /// <summary>Detection-time multiplier while distracted.</summary>
        public const float DetectionScale = 3f;
        /// <summary>Within this distance the sentries notice anyone (m).</summary>
        public const float CloseDistance = 8f;

        /// <summary>
        /// Detection-time scale for a distracted sentry: 1 inside <see cref="CloseDistance"/>, 0 (no detection) for a
        /// still, crouched target beyond it, else <see cref="DetectionScale"/>.
        /// </summary>
        public static float Scale(float distance, MotionClass motion, bool crouched)
        {
            if (distance <= CloseDistance) return 1f;
            if (crouched && motion == MotionClass.Still) return 0f;
            return DetectionScale;
        }
    }
}
