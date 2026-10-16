namespace LongShadow.AI.Encounters
{
    /// <summary>Outcome of <see cref="ISpawnHost.TrySpawn"/>.</summary>
    public enum SpawnResult
    {
        /// <summary>The enemy is in the world.</summary>
        Spawned,
        /// <summary>Not now (closet in view or too close, no ray budget left this frame); try again next tick.</summary>
        Wait,
        /// <summary>Impossible (no prefab and no fallback); the slot is given up.</summary>
        Failed,
    }
}
