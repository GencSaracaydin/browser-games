namespace LongShadow.AI.Encounters
{
    /// <summary>Kinds of <see cref="WaveCondition"/> (machine-readable GDD §7 wave triggers).</summary>
    public enum ConditionKind
    {
        /// <summary>True as soon as the encounter is active.</summary>
        Activated,
        /// <summary>A string event with this id happened after activation (MissionEvent, TriggerEntered, Interacted, DestructibleDestroyed, VO line start "VO:Dxxx").</summary>
        Event,
        /// <summary><see cref="WaveCondition.Value"/> seconds passed after the event <see cref="WaveCondition.Id"/>.</summary>
        SecondsAfterEvent,
        /// <summary>The player is inside the TriggerVolume <see cref="WaveCondition.Id"/> now.</summary>
        PlayerInside,
        /// <summary><see cref="WaveCondition.Value"/> seconds passed since activation.</summary>
        SecondsAfterActivate,
        /// <summary>Wave <see cref="WaveCondition.Id"/> has spawned and has at most <see cref="WaveCondition.Count"/> alive.</summary>
        WaveAliveAtMost,
        /// <summary>Every member of wave <see cref="WaveCondition.Id"/> is dead.</summary>
        WaveDead,
        /// <summary>At least <see cref="WaveCondition.Count"/> enemies of the encounter are dead.</summary>
        KilledAtLeast,
        /// <summary>Enemy <see cref="WaveCondition.Id"/> died at least <see cref="WaveCondition.Value"/> seconds ago.</summary>
        EnemyDead,
        /// <summary>Never true by itself (only <c>TriggerWave</c> starts the wave).</summary>
        Manual,
    }
}
