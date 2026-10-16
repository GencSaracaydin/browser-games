namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// What an <see cref="EncounterRuntime"/> needs from the world (implemented by <see cref="EncounterSpawner"/>;
    /// faked in EditMode tests): spawning and despawning, the global alive count for the hard cap, trigger occupancy and
    /// the notifications that become Core events, barks and fallbacks.
    /// </summary>
    public interface ISpawnHost
    {
        /// <summary>Living enemies across every encounter (GDD §14.3 hard cap 12).</summary>
        int AliveTotal { get; }
        /// <summary>Tries to put a queued enemy into the world.</summary>
        SpawnResult TrySpawn(EncounterRuntime encounter, EnemySlot slot, float now);
        /// <summary>Removes a living enemy without killing it.</summary>
        void Despawn(EnemySlot slot);
        /// <summary>True while the player is inside a TriggerVolume.</summary>
        bool PlayerInside(string triggerId);
        /// <summary>The first member of a wave spawned.</summary>
        void OnWaveSpawned(EncounterRuntime encounter, WaveDef wave);
        /// <summary>A living member was alerted by a wave alert rule or command.</summary>
        void OnAlerted(EncounterRuntime encounter, EnemySlot slot);
        /// <summary>A wave broke (60 % dead): its survivors fall back.</summary>
        void OnMoraleBreak(EncounterRuntime encounter, WaveDef wave);
        /// <summary>Every wave spawned and died.</summary>
        void OnCleared(EncounterRuntime encounter);
    }
}
