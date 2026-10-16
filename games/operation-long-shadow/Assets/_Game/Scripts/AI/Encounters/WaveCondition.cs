namespace LongShadow.AI.Encounters
{
    /// <summary>One term of a <see cref="WaveTrigger"/>. Build with the static factories.</summary>
    public readonly struct WaveCondition
    {
        /// <summary>Kind.</summary>
        public readonly ConditionKind Kind;
        /// <summary>Event, trigger, wave or enemy id.</summary>
        public readonly string Id;
        /// <summary>Seconds (timers, delays).</summary>
        public readonly float Value;
        /// <summary>Count (alive at most, killed at least).</summary>
        public readonly int Count;

        private WaveCondition(ConditionKind kind, string id, float value, int count)
        {
            Kind = kind;
            Id = id;
            Value = value;
            Count = count;
        }

        /// <summary>True when active.</summary>
        public static WaveCondition Activated() => new WaveCondition(ConditionKind.Activated, null, 0f, 0);
        /// <summary>Event happened after activation.</summary>
        public static WaveCondition Event(string id) => new WaveCondition(ConditionKind.Event, id, 0f, 0);
        /// <summary>Seconds after an event.</summary>
        public static WaveCondition AfterEvent(string id, float seconds) => new WaveCondition(ConditionKind.SecondsAfterEvent, id, seconds, 0);
        /// <summary>Player inside a trigger volume.</summary>
        public static WaveCondition PlayerIn(string triggerId) => new WaveCondition(ConditionKind.PlayerInside, triggerId, 0f, 0);
        /// <summary>Seconds after activation.</summary>
        public static WaveCondition After(float seconds) => new WaveCondition(ConditionKind.SecondsAfterActivate, null, seconds, 0);
        /// <summary>A spawned wave has at most n alive.</summary>
        public static WaveCondition WaveAliveAtMost(string wave, int n) => new WaveCondition(ConditionKind.WaveAliveAtMost, wave, 0f, n);
        /// <summary>A wave is dead.</summary>
        public static WaveCondition WaveDead(string wave) => new WaveCondition(ConditionKind.WaveDead, wave, 0f, 0);
        /// <summary>At least n killed in the encounter.</summary>
        public static WaveCondition KilledAtLeast(int n) => new WaveCondition(ConditionKind.KilledAtLeast, null, 0f, n);
        /// <summary>An enemy died at least <paramref name="delay"/> seconds ago.</summary>
        public static WaveCondition EnemyDead(string enemyId, float delay = 0f) => new WaveCondition(ConditionKind.EnemyDead, enemyId, delay, 0);
        /// <summary>Only TriggerWave.</summary>
        public static WaveCondition Manual() => new WaveCondition(ConditionKind.Manual, null, 0f, 0);

        /// <inheritdoc/>
        public override string ToString() => Kind + (Id != null ? "(" + Id + ")" : "") + (Value != 0f ? " " + Value + "s" : "") + (Count != 0 ? " n=" + Count : "");
    }
}
