namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Line schedules of the authored sequences (GDD §8.2 + CINEMATIC_DIRECTION §2 INSERTION, §8.6 MID-B, §8.7 climax,
    /// §8.8 outro) for the Mission fallback flows that run only while no <c>ICinematicService</c> is registered. With
    /// WP09's Timelines the lines are on the VOLineClip tracks and these tables are not used.
    /// </summary>
    public static class SequenceTimelines
    {
        /// <summary>Booth shots 1-8 end at 1:33; the INSERTION cold open (55 s) follows (CINEMATIC_DIRECTION §2).</summary>
        public const float InsertionStart = 93f;
        /// <summary>Length of the INSERTION.</summary>
        public const float InsertionSeconds = 55f;
        /// <summary>Shot 9 starts after the INSERTION; control passes to the player 2 s later (white-out ends, GDD §7.1).</summary>
        public const float IntroControl = InsertionStart + InsertionSeconds + 2f;
        /// <summary>Title card (GDD §8.3).</summary>
        public const float TitleCardSeconds = 8f;
        /// <summary>MID-B length (GDD §8.6).</summary>
        public const float MidBSeconds = 45f;
        /// <summary>MID-B I3 (shot 7).</summary>
        public const float MidBI3 = 36f;
        /// <summary>I3 length (GDD §5.5: 3 s).</summary>
        public const float MidBI3Seconds = 3f;
        /// <summary>Outro length (GDD §8.8).</summary>
        public const float OutroSeconds = 110f;

        /// <summary>GDD §8.2 shots 4-8 and CINEMATIC_DIRECTION §2 I-2…I-11.</summary>
        public static readonly TimedLine[] Intro =
        {
            new TimedLine(LineIds.D001, 25f),
            new TimedLine(LineIds.D002, 44f),
            new TimedLine(LineIds.D003, 51f),
            new TimedLine(LineIds.D004, 59f),
            new TimedLine(LineIds.D005, 68f),
            new TimedLine(LineIds.D006, 79f),
            new TimedLine(LineIds.D200, InsertionStart + 5f),
            new TimedLine(LineIds.D201, InsertionStart + 9f),
            new TimedLine(LineIds.D202, InsertionStart + 18f),
            new TimedLine(LineIds.D203, 0.3f, true),
            new TimedLine(LineIds.D204, InsertionStart + 29f),
            new TimedLine(LineIds.D205, InsertionStart + 36f),
            new TimedLine(LineIds.D206, InsertionStart + 44f),
            new TimedLine(LineIds.D207, 0.6f, true),
        };

        /// <summary>GDD §8.6 rows 7-8.</summary>
        public static readonly TimedLine[] MidB =
        {
            new TimedLine(LineIds.D054, MidBI3),
            new TimedLine(LineIds.D055, 40f),
        };

        /// <summary>GDD §8.8 rows 1-10.</summary>
        public static readonly TimedLine[] Outro =
        {
            new TimedLine(LineIds.D083, 4f),
            new TimedLine(LineIds.D084, 1.2f, true),
            new TimedLine(LineIds.D085, 15f),
            new TimedLine(LineIds.D086, 27f),
            new TimedLine(LineIds.D087, 40f),
            new TimedLine(LineIds.D088, 50f),
            new TimedLine(LineIds.D089, 1f, true),
            new TimedLine(LineIds.D090, 61f),
            new TimedLine(LineIds.D091, 72f),
            new TimedLine(LineIds.D092, 100f),
        };
    }
}
