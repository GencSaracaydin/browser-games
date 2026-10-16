using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Recording <see cref="ICheckpointService"/> for rig tests.</summary>
    internal sealed class FakeCheckpointService : ICheckpointService
    {
        public readonly List<bool> Reloads = new List<bool>();
        public readonly List<ISaveable> Registered = new List<ISaveable>();

        public void Save(string cpId) => Last = cpId;
        public void Reload(bool deathInsert) => Reloads.Add(deathInsert);
        public void Register(ISaveable saveable) => Registered.Add(saveable);
        public void Unregister(ISaveable saveable) => Registered.Remove(saveable);
        public string Last { get; private set; }
        public bool SavePending => false;
    }
}
