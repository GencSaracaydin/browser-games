namespace LongShadow.AI.Encounters
{
    /// <summary>What a <see cref="WaveTrigger"/> can ask about its encounter (implemented by the encounter runtime; faked in tests).</summary>
    public interface IConditionContext
    {
        /// <summary>Current time (scaled seconds).</summary>
        float Now { get; }
        /// <summary>Time the encounter was activated.</summary>
        float ActivatedAt { get; }
        /// <summary>Time an event last happened at or after activation; false if it has not.</summary>
        bool EventTime(string eventId, out float time);
        /// <summary>True while the player is inside a TriggerVolume.</summary>
        bool PlayerInside(string triggerId);
        /// <summary>True once a wave has started spawning.</summary>
        bool WaveStarted(string wave);
        /// <summary>True once every member of a wave has spawned (or was already dead).</summary>
        bool WaveAllSpawned(string wave);
        /// <summary>Living members of a wave.</summary>
        int WaveAlive(string wave);
        /// <summary>True when every member of a wave has spawned and died.</summary>
        bool WaveDead(string wave);
        /// <summary>Enemies of the encounter killed so far.</summary>
        int Killed { get; }
        /// <summary>Time an enemy died; false if alive or never spawned.</summary>
        bool EnemyDeathTime(string enemyId, out float time);
    }
}
