namespace LongShadow.Audio.Ambience
{
    /// <summary>Runtime state of one bed of the <see cref="AmbienceDirector"/>.</summary>
    internal sealed class BedState
    {
        /// <summary>Table rule (null for the booth room tone).</summary>
        public AmbienceBedRule Rule;
        /// <summary>Cue id.</summary>
        public string CueId;
        /// <summary>Target gain (last evaluation).</summary>
        public float Target;
        /// <summary>Smoothed gain.</summary>
        public float Gain;
        /// <summary>Seconds to follow the target.</summary>
        public float Tau = 1f;
        /// <summary>The take that is heard.</summary>
        public BedPlayer Current;
        /// <summary>The other variant fading in (rotation), or null.</summary>
        public BedPlayer Next;
        /// <summary>Unscaled time of the last moment the bed was audible (unloading).</summary>
        public float LastAudibleAt = -1000f;
        /// <summary>Clip data was requested.</summary>
        public bool LoadRequested;
        /// <summary>Unscaled time the data was requested (load timeout).</summary>
        public float LoadRequestedAt;
        /// <summary>The bed plays during a glitch insert (ignores the listener pause).</summary>
        public bool Insert;
    }
}
