namespace LongShadow.Core
{
    /// <summary>Priorities for <see cref="ITimeScaleService.Push"/> (TECH_PLAN §5.4).</summary>
    public static class TimePriority
    {
        public const int Pause = 100;
        /// <summary>Glitch inserts (scale 0).</summary>
        public const int Insert = 90;
        public const int Cinematic = 50;
        /// <summary>Death slow-down 0.3 over 0.6 s.</summary>
        public const int Death = 40;
        /// <summary>Breach 0.35, blast 0.3.</summary>
        public const int SlowMo = 30;
        public const int Default = 0;
    }
}
