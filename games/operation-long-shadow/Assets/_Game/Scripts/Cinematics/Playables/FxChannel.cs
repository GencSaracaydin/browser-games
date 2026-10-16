namespace LongShadow.Cinematics.Playables
{
    /// <summary>Full-screen layer a <see cref="ScreenFXClip"/> drives (one track per channel).</summary>
    public enum FxChannel
    {
        /// <summary>Fade to a colour (alpha from → to).</summary>
        Fade,
        /// <summary>Exposure offset in EV (from → to).</summary>
        Exposure,
        /// <summary>Desaturation 0..1.</summary>
        Desaturate,
        /// <summary>Glitch amount 0..1 (Value2 = white frame on entry).</summary>
        Glitch,
        /// <summary>Film burn-out 0..1 (full screen and the MID-B screen).</summary>
        Burn,
        /// <summary>A feed camera image over the picture (cross-dissolve; FeedId).</summary>
        Feed,
        /// <summary>Vignette tightening 0..1.</summary>
        Vignette,
        /// <summary>Booth grade saturation offset (−100..0; outro −30).</summary>
        BoothSaturation,
    }
}
