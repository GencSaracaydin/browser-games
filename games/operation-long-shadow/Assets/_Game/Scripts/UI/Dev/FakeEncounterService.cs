using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.UI.Dev
{
    /// <summary><see cref="IEncounterService"/> stand-in that only answers <see cref="Actor"/> and awareness (detection meters).</summary>
    public sealed class FakeEncounterService : IEncounterService
    {
        private readonly Dictionary<string, FakeTargetable> _actors = new Dictionary<string, FakeTargetable>();
        private readonly Dictionary<string, float> _awareness = new Dictionary<string, float>();

        /// <summary>Adds an actor.</summary>
        public void AddActor(FakeTargetable actor) => _actors[actor.ActorId] = actor;

        /// <summary>Sets the detection meter of an actor 0..1.</summary>
        public void SetAwareness(string id, float value) => _awareness[id] = value;

        /// <summary>Detection meter (what WP04's EncounterSpawner.Awareness01 returns).</summary>
        public float Awareness01(string id) => id != null && _awareness.TryGetValue(id, out var v) ? v : 0f;

        public ITargetable Actor(string enemyId) => enemyId != null && _actors.TryGetValue(enemyId, out var a) ? a : null;
        public void Activate(string encounterId) { }
        public void TriggerWave(string encounterId, string wave) { }
        public void Deactivate(string encounterId) { }
        public int Alive(string encounterId, string wave = null) => 0;
        public int Killed(string encounterId) => 0;
        public bool Cleared(string encounterId) => false;
        public int AliveTotal => _actors.Count;
    }
}
