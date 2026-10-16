namespace LongShadow.AI.Encounters
{
    /// <summary>Life cycle of one encounter enemy (<see cref="EnemySlot"/>).</summary>
    public enum SlotState
    {
        /// <summary>Its wave has not started.</summary>
        Pending,
        /// <summary>Waiting for its delay, the max-alive cap, the hard cap or a clear closet.</summary>
        Queued,
        /// <summary>In the world and alive.</summary>
        Alive,
        /// <summary>Killed (also restored from a checkpoint: stays dead).</summary>
        Dead,
        /// <summary>Removed without dying (straggler, withdrawn marksman, failed spawn); counts as done.</summary>
        Despawned,
    }
}
