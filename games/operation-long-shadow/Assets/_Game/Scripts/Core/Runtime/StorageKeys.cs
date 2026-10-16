namespace LongShadow.Core
{
    /// <summary>Keys for <see cref="ISettingsService.GetValue"/>/SetValue (localStorage, GDD §17 persistence).</summary>
    public static class StorageKeys
    {
        /// <summary>GameSettings JSON.</summary>
        public const string Settings = "ls.settings";
        /// <summary>Highest checkpoint id reached ("CP5").</summary>
        public const string HighestCheckpoint = "ls.highestCp";
        /// <summary>"1" after the intro was seen once (enables skip).</summary>
        public const string IntroSeen = "ls.introSeen";
        /// <summary>"1" after the first completion (unlocks SELECT BEAT).</summary>
        public const string CompletedOnce = "ls.completedOnce";
        /// <summary>Prefix for one-time tutorial prompts ("ls.tut.&lt;key&gt;").</summary>
        public const string TutorialPrefix = "ls.tut.";
        /// <summary>
        /// Prefix of the "seen once" flag of a sequence other than the intro ("ls.cine.Outro" = "1"); enables hold-to-skip
        /// after the first viewing (GDD §8).
        /// </summary>
        public const string CinematicSeenPrefix = "ls.cine.";
    }
}
