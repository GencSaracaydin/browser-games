namespace LongShadow.Player
{
    /// <summary>
    /// Audio cue ids (ASSET_MANIFEST ids, WP06 builds LIB_Audio from them) of the player's own foley, played through
    /// <c>IAudioService</c>. Only the player plays these, so they stay local to Player; footsteps use
    /// <c>AudioCues.Footstep(surface)</c> from Core. A cue that has not landed is skipped by the AudioService.
    /// </summary>
    public static class PlayerCues
    {
        /// <summary>Landing thump with gear rattle.</summary>
        public const string Land = "SFX_MOVE_LAND";
        /// <summary>Slide start (gravel/sand scrape).</summary>
        public const string Slide = "SFX_MOVE_SLIDE";
        /// <summary>Vault, climb and air mantle.</summary>
        public const string Mantle = "SFX_MOVE_MANTLE";
        /// <summary>One ladder rung.</summary>
        public const string Ladder = "SFX_MOVE_LADDER";
        /// <summary>Webbing and cloth rustle loop while moving.</summary>
        public const string FoleyGear = "SFX_FOLEY_GEAR";
        /// <summary>Heartbeat loop below 25 % HP (GDD §11 Health).</summary>
        public const string Heartbeat = "SFX_HEARTBEAT";
        /// <summary>Out-of-bounds warning line (GDD §17, DIALOGUE.json).</summary>
        public const string OutOfBoundsLine = "D093";
    }
}
