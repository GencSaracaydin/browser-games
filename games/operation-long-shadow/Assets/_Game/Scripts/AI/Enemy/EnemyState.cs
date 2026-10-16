namespace LongShadow.AI.Enemy
{
    /// <summary>Top-level enemy states (GDD §14.3: Idle/Patrol → (Suspicious) → Combat → Death).</summary>
    public enum EnemyState
    {
        /// <summary>Not configured or despawned.</summary>
        Inactive,
        /// <summary>Unaware (idle, smoking, ambush hold).</summary>
        Idle,
        /// <summary>Beat 2 sentries only: the detection meter is filling.</summary>
        Suspicious,
        /// <summary>Aware of the player or the squad.</summary>
        Combat,
        /// <summary>Dead.</summary>
        Dead,
    }
}
