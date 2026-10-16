namespace LongShadow.Cinematics.Booth
{
    /// <summary>Behaviour of the booth's fluorescent tube (TECH_PLAN §7.12 LightFlickerClip).</summary>
    public enum FlickerMode
    {
        /// <summary>Steady on (with a faint ballast ripple).</summary>
        On,
        /// <summary>Off.</summary>
        Off,
        /// <summary>Stutters on with a number of blinks (intro shot 2: four blinks).</summary>
        StutterOn,
        /// <summary>Dies with a few last flickers (outro shot 10).</summary>
        Dying,
        /// <summary>On with irregular dips (unsettled moments).</summary>
        Unstable,
    }
}
