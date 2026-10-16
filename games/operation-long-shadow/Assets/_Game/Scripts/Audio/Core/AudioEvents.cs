using LongShadow.Core;

namespace LongShadow.Audio.Core
{
    /// <summary>
    /// Mission-event ids (<c>GameEvents.RaiseMissionEvent</c>) the audio package listens to. Other packages cannot
    /// reference LongShadow.Audio, so they raise the same string literals (a Core change in Docs/CORE_CHANGES.md asks to
    /// mirror them in <c>SequenceIds</c>). Every audio behaviour also has a sensible default without these events.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class AudioEvents
    {
        /// <summary>The listener is in the interrogation booth: outdoor beds stop, the booth room tone plays.</summary>
        public const string AmbienceBoothOn = SequenceIds.AmbienceBoothOn;
        /// <summary>Back to the world beds.</summary>
        public const string AmbienceBoothOff = SequenceIds.AmbienceBoothOff;
        /// <summary>Every ambience bed fades out (silence as a tool, CINEMATIC_DIRECTION §1).</summary>
        public const string AmbienceMute = SequenceIds.AmbienceMute;
        /// <summary>Ends <see cref="AmbienceMute"/>.</summary>
        public const string AmbienceUnmute = SequenceIds.AmbienceUnmute;
        /// <summary>Starts the basement numbers broadcast (GDD §7.9); BeatStarted(9) and CP9 also start it.</summary>
        public const string NumbersStart = SequenceIds.NumbersStart;
        /// <summary>Stops the basement numbers broadcast without the cut line.</summary>
        public const string NumbersStop = SequenceIds.NumbersStop;
        /// <summary>The curfew siren from the three PA horns at once (GDD §7.1 "the curfew siren winds down").</summary>
        public const string PASiren = SequenceIds.PASiren;
        /// <summary>Prefix: <c>"PA.Play." + cueId</c> plays any cue from the three PA horns.</summary>
        public const string PAPlayPrefix = SequenceIds.PAPlayPrefix;
        /// <summary>Raised by the audio package when the fourth transmitter rack dies (the station's tubes lose power).</summary>
        public const string TransmittersDown = SequenceIds.TransmittersDown;
    }
}
