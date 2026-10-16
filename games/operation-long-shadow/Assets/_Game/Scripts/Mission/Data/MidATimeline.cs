namespace LongShadow.Mission.Data
{
    /// <summary>
    /// MID-A "LAST LIGHT" (GDD §8.5; TECH_PLAN §7.11 "MID-A owner"): every event time in seconds from the MID-A trigger.
    /// Anchored events run on this clock; the lines whose GDD trigger is "previous line finished" (D041) or "inside a
    /// line" (D042 on "Calder?", I2 on "Six", D040 on "Four", the radio jump on "Seven") are chained to the real line
    /// timing (VO envelopes / D039 token onsets), so with the GDD's own line lengths every event lands on the §8.5 table
    /// (the EditMode test checks both cases, ±0.3 s).
    /// </summary>
    public static class MidATimeline
    {
        /// <summary>LightStage s at 0:00 (GDD §8.5, §9.2a "MID-A 0:00").</summary>
        public const float SStart = 1.80f;
        /// <summary>s is held at <see cref="SStart"/> until this time.</summary>
        public const float HoldUntil = 4f;
        /// <summary>s reaches <see cref="SStrike"/> (PracticalsStrike) at this time.</summary>
        public const float StrikeTime = 14f;
        /// <summary>s of the sodium strike (GDD §9.2a row 1.97).</summary>
        public const float SStrike = 1.97f;
        /// <summary>The sun is fully down (s 2.00) at this time.</summary>
        public const float SunDownTime = 20f;
        /// <summary>s at sun-down (SUN off, SKY_KEY on).</summary>
        public const float SEnd = 2.00f;

        /// <summary>The squad mirrors the player's look west until the sun is down (GDD §7.5 Squad, §8.5 row 2).</summary>
        public const float LookWestUntil = SunDownTime;
        /// <summary>Half-angle of "looking west" around the sun azimuth 258° (GDD §6.1).</summary>
        public const float LookWestHalfAngle = 60f;
        /// <summary>Sun azimuth (GDD §6.1).</summary>
        public const float SunAzimuth = 258f;

        /// <summary>MUS_03_LASTLIGHT swell starts (GDD §8.5 row 3).</summary>
        public const float MusicSwell = 5f;
        /// <summary>Automatic watch check (row 3, 1.2 s FP arm IK tween).</summary>
        public const float WatchCheck = 12f;
        /// <summary>Length of the watch check.</summary>
        public const float WatchCheckSeconds = 1.2f;
        /// <summary>If LightStage has not raised PracticalsStrike by this time, the beat script raises it (no LightStage).</summary>
        public const float StrikeFallback = 14.5f;
        /// <summary>D037 and the interval signal from Tinker's PRC-77 (row 5).</summary>
        public const float D037At = SunDownTime;
        /// <summary>D038 (row 5 "≈ 0:23").</summary>
        public const float D038At = 23f;
        /// <summary>D039 starts (row 6 "from ≈ 0:27").</summary>
        public const float D039At = 27f;
        /// <summary>Index of the first "Seven" token in D039: the broadcast jumps to 2D radio there (G9).</summary>
        public const int SevenToken = 2;
        /// <summary>Index of "Six" in D039: I2 fires on it (row 7, ≈ 0:38.7).</summary>
        public const int SixToken = 13;
        /// <summary>Index of the "Four" after "Six" in D039: D040 starts on it (≈ 0:39.6).</summary>
        public const int FourToken = 14;
        /// <summary>First D039 token that is a digit (the vision pulse runs per digit, row 6).</summary>
        public const int FirstDigitToken = 2;
        /// <summary>Vignette pulse per digit (row 6 "vignette pulse 0.15").</summary>
        public const float DigitPulse = 0.15f;
        /// <summary>"Calder?" inside D041: D042 starts there (GDD §8.5 D041 ≈ 0:41.6 → D042 ≈ 0:44).</summary>
        public const float CalderOffsetInD041 = 2.4f;
        /// <summary>Volume of the interval signal from the pack ("faintly", row 5).</summary>
        public const float IntervalVolume = 0.45f;

        /// <summary>The §8.5 table times, for the timing test (seconds from the trigger).</summary>
        public const float GddD039Seven = 28.8f;
        /// <summary>GDD §8.5 I2.</summary>
        public const float GddI2 = 38.7f;
        /// <summary>GDD §8.5 D040.</summary>
        public const float GddD040 = 39.6f;
        /// <summary>GDD §8.5 D041.</summary>
        public const float GddD041 = 41.6f;
        /// <summary>GDD §8.5 D042.</summary>
        public const float GddD042 = 44f;
        /// <summary>GDD §8.5 end ("When D042 ends (≈ 0:47)").</summary>
        public const float GddEnd = 47f;

        /// <summary>LightStage s at MID-A time t (piecewise linear, GDD §8.5).</summary>
        public static float SAt(float t)
        {
            if (t <= HoldUntil) return SStart;
            if (t <= StrikeTime) return SStart + (SStrike - SStart) * ((t - HoldUntil) / (StrikeTime - HoldUntil));
            if (t <= SunDownTime) return SStrike + (SEnd - SStrike) * ((t - StrikeTime) / (SunDownTime - StrikeTime));
            return SEnd;
        }

        /// <summary>Onset of a D039 token in seconds from the start of D039 (Clockmaker splice grid, 0.9 s).</summary>
        public static float TokenOnset(int index)
        {
            var on = LineDurations.D039TokenOnsets;
            if (on == null || on.Length == 0) return index * 0.9f;
            if (index < 0) index = 0;
            return index < on.Length ? on[index] : on[on.Length - 1] + (index - on.Length + 1) * 0.9f;
        }

        /// <summary>Number of D039 tokens.</summary>
        public static int TokenCount => LineDurations.D039TokenOnsets?.Length ?? 15;
    }
}
