namespace LongShadow.Audio.VO
{
    /// <summary>How a line is rendered (<see cref="VOChainSelector"/>).</summary>
    public enum VOPlanMode
    {
        /// <summary>No audio yet: subtitle and timing only.</summary>
        Silent,
        /// <summary>One clip in 2D (radio, intercom, booth, voice-over).</summary>
        TwoD,
        /// <summary>One clip in 3D at the speaker or an anchor marker.</summary>
        ThreeD,
        /// <summary>Synced DRY (3D) and RADIO (2D), crossfaded by distance (GDD §10.1).</summary>
        DryRadio,
        /// <summary>One clip on a 3D and a 2D source, switching at a time (G9).</summary>
        PositionalSwitch,
        /// <summary>One clip from the three PA horns, each delayed by its distance (GDD §7.1).</summary>
        Horns,
    }
}
