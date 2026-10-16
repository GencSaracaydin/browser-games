namespace LongShadow.Audio.Music
{
    /// <summary>
    /// Which music cues each beat may need (GDD §7.0 pacing table, §10.3), so the MusicDirector decodes them with the VO
    /// window (music imports with Preload Audio Data off, TECH_PLAN §3.12) instead of on the first <c>PlayCue</c>.
    /// Stingers for inserts and deaths are always kept. Beat 0 = intro, 12 = outro.
    /// </summary>
    public static class MusicWindows
    {
        /// <summary>Always loaded (glitch inserts, death).</summary>
        public static readonly string[] Always = { "STG_GLITCH", "STG_DEATH" };

        private static readonly string[] Intro = { "MUS_01_SESSION", "STG_TITLE" };
        private static readonly string[] Market = { "MUS_02_MARKET" };
        private static readonly string[] Square = { "MUS_02_MARKET", "STG_GREYCOAT" };
        private static readonly string[] LastLight = { "MUS_03_LASTLIGHT" };
        private static readonly string[] Station = { "MUS_04_STATION" };
        private static readonly string[] Exfil = { "MUS_05_EXFIL" };
        private static readonly string[] Outro = { "MUS_06_SIXFORTY" };
        private static readonly string[] None = new string[0];

        /// <summary>Cue ids (base ids for layered cues) of a beat.</summary>
        public static string[] CuesOf(int beat)
        {
            switch (beat)
            {
                case 0: return Intro;
                case 2:
                case 3: return Market;
                case 4: return Square;
                case 5: return LastLight;
                case 6:
                case 7:
                case 9: return Station;
                case 10:
                case 11: return Exfil;
                case 12: return Outro;
                default: return None; // Beat 1 (ambience only), Beat 8 (projector only)
            }
        }
    }
}
