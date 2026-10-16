namespace LongShadow.Cinematics
{
    /// <summary>What the screen shows when a sequence ends (or is skipped).</summary>
    public enum EndScreen
    {
        /// <summary>Clear every cinematic overlay (normal hand-off to gameplay).</summary>
        Clear,
        /// <summary>Hold black (the outro ends on the debrief dossier, GDD §8.8 shot 11).</summary>
        HoldBlack,
        /// <summary>Hold white (the climax cuts to white before the shot, GDD §8.7; the outro opens from white).</summary>
        HoldWhite,
    }
}
