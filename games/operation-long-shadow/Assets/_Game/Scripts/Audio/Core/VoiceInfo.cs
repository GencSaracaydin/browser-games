namespace LongShadow.Audio.Core
{
    /// <summary>What <see cref="VoiceStealing"/> needs to know about one playing voice.</summary>
    public struct VoiceInfo
    {
        /// <summary>The voice is playing (or scheduled).</summary>
        public bool Active;
        /// <summary>Importance 0..100 (cue priority, raised for the player's own sounds).</summary>
        public int Priority;
        /// <summary>Estimated loudness at the listener 0..1.</summary>
        public float Audibility;
        /// <summary>Start time (older voices are stolen first among equals).</summary>
        public float StartedAt;
        /// <summary>Never steal (loops the game depends on, held VO).</summary>
        public bool Protected;
    }
}
