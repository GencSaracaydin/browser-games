namespace LongShadow.Audio.Editor
{
    /// <summary>One row of <c>Assets/_Game/Audio/Music/LOOP_POINTS.json</c> (WP06_SFX): the loop body of a clip in samples
    /// of the 48 kHz source, and the musical grid of score cues.</summary>
    public struct LoopPoint
    {
        /// <summary>Source sample rate.</summary>
        public int SampleRate;
        /// <summary>First sample of the loop body.</summary>
        public long LoopStartSample;
        /// <summary>Sample after the loop body.</summary>
        public long LoopEndSample;
        /// <summary>Tempo of the bar grid (0 = none).</summary>
        public float Bpm;
        /// <summary>Seconds per bar (0 = derive from <see cref="Bpm"/> in 4/4, or none).</summary>
        public float BarSeconds;
        /// <summary>Bars in the loop body.</summary>
        public int LoopBars;
        /// <summary>The clip plays an intro before the loop body (MUS_01_SESSION).</summary>
        public bool HasIntro;

        /// <summary>Loop body start in seconds.</summary>
        public float StartSeconds => SampleRate > 0 ? (float)(LoopStartSample / (double)SampleRate) : 0f;
        /// <summary>Loop body end in seconds.</summary>
        public float EndSeconds => SampleRate > 0 ? (float)(LoopEndSample / (double)SampleRate) : 0f;
        /// <summary>True for a usable loop body.</summary>
        public bool Valid => SampleRate > 0 && LoopEndSample > LoopStartSample;
        /// <summary>Seconds per bar from the data (BarSeconds, else 4 beats at Bpm), 0 when unknown.</summary>
        public float Bar => BarSeconds > 0f ? BarSeconds : Bpm > 0f ? 240f / Bpm : 0f;
    }
}
