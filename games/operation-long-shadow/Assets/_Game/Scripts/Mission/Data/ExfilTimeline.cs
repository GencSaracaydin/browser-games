namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Beat 11 timeline (GDD §7.11), seconds of the exfil spline (B11.Exfil, 50 s). The target windows mirror WP04's
    /// <c>ExfilSchedule</c>; the script uses them for the HUD markers and the line cues.
    /// </summary>
    public static class ExfilTimeline
    {
        /// <summary>Spline length in seconds (Appendix A segment times 8/8/10/10/8/6).</summary>
        public const float Total = 50f;
        /// <summary>D077.</summary>
        public const float D077At = 0f;
        /// <summary>T1 opens fire (≈ t 8, D078); the script also waits for the first Legion shot after <see cref="T1Start"/>.</summary>
        public const float T1Start = 8f;
        public const float T1End = 16f;
        public const float T2Start = 15f;
        public const float T2End = 23f;
        public const float T4Start = 26f;
        public const float T4End = 33f;
        /// <summary>T3, the RPG man, stands up (hidden before).</summary>
        public const float T3Start = 28f;
        /// <summary>T3 fires the RPG whatever happens (climax, D081).</summary>
        public const float T3Fire = 30f;
        /// <summary>The two Legion trucks drive into Clock Square.</summary>
        public const float TrucksStart = 16f;
        public const float TrucksEnd = 27f;
        /// <summary>D079.</summary>
        public const float D079At = 25f;
        /// <summary>D080 comes this long after D079 finishes.</summary>
        public const float D080Gap = 0.5f;
        /// <summary>Grace for the AI's B11.RPGLaunch before the script starts the climax itself.</summary>
        public const float RpgEventGrace = 0.3f;
        /// <summary>Climax length (TL_Climax, GDD §8.7).</summary>
        public const float ClimaxSeconds = 12f;
        /// <summary>Climax shot 2: the door gun disengages.</summary>
        public const float ClimaxDismount = 3f;
        /// <summary>Climax shot 3: the pistol rises over 3 s.</summary>
        public const float ClimaxPistol = 6f;
        /// <summary>Length of the pistol rise.</summary>
        public const float PistolSeconds = 3f;
        /// <summary>D082 "Pistol reaches Hollis's chest height".</summary>
        public const float ClimaxD082 = 7.5f;
        /// <summary>Climax shot 4: the I4 cascade.</summary>
        public const float ClimaxI4 = 10f;
        /// <summary>Incoming damage scale and health floor on the door gun (GDD §7.11).</summary>
        public const float DamageScale = 0.25f;
        /// <summary>Health floor on the door gun.</summary>
        public const float HealthFloor = 1f;
    }
}
