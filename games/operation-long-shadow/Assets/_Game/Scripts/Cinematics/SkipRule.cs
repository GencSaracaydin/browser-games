namespace LongShadow.Cinematics
{
    /// <summary>When a sequence may be skipped by holding Space for 1 s (GDD §8; CINEMATIC_DIRECTION §2).</summary>
    public enum SkipRule
    {
        /// <summary>Never (the title card always plays).</summary>
        Never,
        /// <summary>From the first viewing (the intro, CINEMATIC_DIRECTION §2 "web players are impatient").</summary>
        Always,
        /// <summary>Only after the sequence was seen once (GDD §8).</summary>
        AfterFirstView,
    }
}
