using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary><see cref="ICheckpointService"/> stand-in: Reload raises CheckpointLoaded so the HUD resets.</summary>
    public sealed class FakeCheckpointService : ICheckpointService
    {
        public string Last { get; set; } = SequenceIds.CP3b;
        public bool SavePending => false;

        /// <summary>Reload calls (tests).</summary>
        public int Reloads { get; private set; }

        public void Save(string cpId)
        {
            Last = cpId;
            GameEvents.RaiseCheckpointSaved(cpId);
        }

        public void Reload(bool deathInsert)
        {
            Reloads++;
            GameEvents.RaiseCheckpointLoaded(Last);
        }

        public void Register(ISaveable saveable) { }
        public void Unregister(ISaveable saveable) { }
    }
}
