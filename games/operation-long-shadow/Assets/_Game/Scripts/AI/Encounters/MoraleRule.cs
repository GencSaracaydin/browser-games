namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// GDD §14.1 Legion morale: "when 60 % of a wave is dead the rest fall back one node". A wave breaks once, when its
    /// dead reach 60 % of its size while someone is still alive to fall back. Pure.
    /// </summary>
    public static class MoraleRule
    {
        /// <summary>Fraction of a wave that must be dead (GDD §14.1).</summary>
        public const float BreakFraction = 0.6f;

        /// <summary>True when a wave of <paramref name="waveSize"/> with <paramref name="dead"/> dead breaks now.</summary>
        public static bool ShouldBreak(int waveSize, int dead, bool alreadyBroken)
        {
            if (alreadyBroken || waveSize < 2 || dead >= waveSize) return false;
            return dead >= BreakFraction * waveSize - 1e-4f;
        }
    }
}
