namespace LongShadow.Audio.Editor
{
    /// <summary>One clip of an envelope file (<see cref="EnvelopeFile"/>).</summary>
    public sealed class EnvelopeClip
    {
        /// <summary>Clip name (<c>VO_D015_DRY</c>, <c>VO_BK_LEG_01_FAR_A</c>).</summary>
        public string Name;
        /// <summary>Chain.</summary>
        public string Chain;
        /// <summary>"" or the bark voice "A"/"B".</summary>
        public string Variant = "";
        /// <summary>Clip length in seconds.</summary>
        public float Duration;
        /// <summary>Seconds before the first word.</summary>
        public float SpeechOffset;
        /// <summary>Peak of <see cref="Rms"/>.</summary>
        public float RmsMax;
        /// <summary>Linear RMS at <see cref="EnvelopeFile.RateHz"/>.</summary>
        public float[] Rms = new float[0];
    }
}
