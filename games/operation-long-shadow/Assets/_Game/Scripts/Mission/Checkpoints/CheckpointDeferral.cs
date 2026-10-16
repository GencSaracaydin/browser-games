namespace LongShadow.Mission.Checkpoints
{
    /// <summary>
    /// The save-deferral rule of GDD §17 (TECH_PLAN §5.5): a checkpoint is deferred while an enemy is within 10 m, a
    /// grenade is live, or the player is below 50 HP; at most 10 s; a deferred save always commits before the next beat's
    /// first spawn (the director calls CommitPending at every beat start). Pure (unit-tested).
    /// </summary>
    public static class CheckpointDeferral
    {
        /// <summary>Enemy proximity that defers a save (m).</summary>
        public const float EnemyRadius = 10f;
        /// <summary>Health below which a save is deferred.</summary>
        public const float MinHealth = 50f;
        /// <summary>Longest deferral (s).</summary>
        public const float MaxDeferSeconds = 10f;

        /// <summary>Why a save is deferred (None = commit now).</summary>
        public enum Reason
        {
            None = 0,
            EnemyNear = 1,
            GrenadeLive = 2,
            LowHealth = 3,
        }

        /// <summary>The reason that blocks a commit, or None.</summary>
        public static Reason Evaluate(int enemiesWithin10m, bool grenadeLive, float playerHealth)
        {
            if (enemiesWithin10m > 0) return Reason.EnemyNear;
            if (grenadeLive) return Reason.GrenadeLive;
            if (playerHealth < MinHealth) return Reason.LowHealth;
            return Reason.None;
        }

        /// <summary>True when a pending save must commit now (no blocking reason, or deferred for 10 s).</summary>
        public static bool ShouldCommit(Reason reason, float pendingSeconds) =>
            reason == Reason.None || pendingSeconds >= MaxDeferSeconds;
    }
}
