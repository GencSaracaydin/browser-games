namespace LongShadow.Core
{
    /// <summary>
    /// Checkpoints (WP07 CheckpointService, TECH_PLAN §5.5, GDD §17): in-memory snapshots of every registered
    /// <see cref="ISaveable"/>; restore in place (no scene reload).
    /// </summary>
    public interface ICheckpointService
    {
        /// <summary>Requests a save (may be deferred up to 10 s by the GDD §17 rules).</summary>
        void Save(string cpId);
        /// <summary>Reloads the last checkpoint; <paramref name="deathInsert"/> plays the death insert first.</summary>
        void Reload(bool deathInsert);
        /// <summary>Registers a saveable (idempotent).</summary>
        void Register(ISaveable saveable);
        /// <summary>Unregisters a saveable.</summary>
        void Unregister(ISaveable saveable);
        /// <summary>Last committed checkpoint id, or null.</summary>
        string Last { get; }
        /// <summary>True while a deferred save is pending.</summary>
        bool SavePending { get; }
    }
}
