namespace LongShadow.Core
{
    /// <summary>Contributes state to a <see cref="CheckpointSnapshot"/> (TECH_PLAN D11). Keys must be unique and stable.</summary>
    public interface ISaveable
    {
        /// <summary>Unique, stable key ("Mission", "Objectives", "Health.E3-02").</summary>
        string SaveKey { get; }
        /// <summary>Writes state.</summary>
        void Capture(SaveWriter w);
        /// <summary>Restores state written by <see cref="Capture"/>; tolerate missing keys (older snapshots).</summary>
        void Restore(SaveReader r);
    }
}
