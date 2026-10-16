namespace LongShadow.Core
{
    /// <summary>Category names for <see cref="IAudioService.SetCategoryGain"/> and Duck (TECH_PLAN D9).</summary>
    public static class AudioCategories
    {
        public const string Master = "Master";
        public const string Music = "Music";
        public const string Effects = "Effects";
        public const string Voice = "Voice";
        public const string UI = "UI";
        /// <summary>Booth inserts (plays while gameplay audio is paused).</summary>
        public const string Insert = "Insert";
        public const string Ambience = "Ambience";
    }
}
