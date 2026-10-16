namespace LongShadow.Audio.VO
{
    /// <summary>
    /// Playback channel of a line. Each channel plays one line at a time from its own priority queue; channels overlap
    /// each other, which the cinematic choreography needs (GDD §8.5: the interrogator's <c>D042</c> overlaps Hollis's
    /// <c>D041</c>; the Clockmaker reads over the squad).
    /// </summary>
    public enum VOChannel
    {
        /// <summary>The squad net: Hollis, Sparrow, Tinker, Magpie and Calder in the field (TECH_PLAN §7.10 "net" channel).</summary>
        Net = 0,
        /// <summary>Broadcast voices: the Clockmaker (radio, room speaker) and Harrou's PA horns.</summary>
        Broadcast = 1,
        /// <summary>The booth: interrogator / TALLOW / Calder (SCRAMBLER, BOOTH, TAPE, CLEAN chains).</summary>
        Booth = 2,
        /// <summary>Enemy barks: no queue, may overlap.</summary>
        Bark = 3,
    }
}
