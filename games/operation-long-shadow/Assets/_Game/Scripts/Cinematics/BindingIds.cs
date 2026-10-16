namespace LongShadow.Cinematics
{
    /// <summary>
    /// Binding ids used by the timeline tracks (track name suffix "@id", see <see cref="CinematicBindings"/>) and by the
    /// booth, insert and cast code. Booth ids are registered by <c>BoothRoot</c> in S0_Booth, cast ids by
    /// <c>InsertionCast</c>, feeds by <c>FeedCameras</c>. Any other id is tried as a marker id (<c>IMarkerRegistry</c>) and
    /// "Squad.HOL" style ids resolve through <c>ISquadService.Member</c>.
    /// </summary>
    public static class BindingIds
    {
        // ---- booth (S0_Booth, GDD §8.0)
        /// <summary>The booth root (local frame of GDD §8.0).</summary>
        public const string Booth = "Booth";
        /// <summary>Cam_Booth (C_POV and every booth preset).</summary>
        public const string BoothCamera = "Cam.Booth";
        /// <summary>Booth CRT prefix: "Booth.CRT1" .. "Booth.CRT6".</summary>
        public const string CrtPrefix = "Booth.CRT";
        /// <summary>Fluorescent tube.</summary>
        public const string Tube = "Booth.Tube";
        /// <summary>Scrambler box (VU needle, toggle).</summary>
        public const string Scrambler = "Booth.Scrambler";
        /// <summary>Reel-to-reel deck.</summary>
        public const string Reel = "Booth.Reel";
        /// <summary>Wall clock.</summary>
        public const string Clock = "Booth.Clock";
        /// <summary>The interrogator's pen (four-tap emitter).</summary>
        public const string Pen = "Booth.Pen";
        /// <summary>Desk phone.</summary>
        public const string Phone = "Booth.Phone";
        /// <summary>Steel lighter.</summary>
        public const string Lighter = "Booth.Lighter";
        /// <summary>Seated Calder (PFB_Calder_Booth).</summary>
        public const string BoothCalder = "Booth.Calder";
        /// <summary>The interrogator (PFB_Interrogator).</summary>
        public const string BoothInterrogator = "Booth.Interrogator";

        // ---- feed cameras (render textures for CRTs and the MID-B screen)
        /// <summary>Intro CRT2: the ridge at golden hour, live from CAM.IntroRidge in S1_Approach.</summary>
        public const string FeedRidge = "Feed.Ridge";
        /// <summary>Intro CRT3: the booth's own reel-to-reel deck.</summary>
        public const string FeedDeck = "Feed.Deck";
        /// <summary>MID-B: the 16 mm film camera in the booth (BOOTH.FilmCam).</summary>
        public const string FeedFilm = "Feed.Film";
        /// <summary>The stopped clock (C_CLOCK), shown on CRTs in shot 6 and the montage flashes.</summary>
        public const string FeedClock = "Feed.Clock";

        // ---- insertion cast (CINEMATIC_DIRECTION §2)
        /// <summary>Magpie's exterior (VEH_HELICOPTER) for the insertion.</summary>
        public const string CastHeli = "Cast.Heli";
        /// <summary>Cast Hollis.</summary>
        public const string CastHollis = "Cast.Hollis";
        /// <summary>Cast Sparrow.</summary>
        public const string CastSparrow = "Cast.Sparrow";
        /// <summary>Cast Tinker.</summary>
        public const string CastTinker = "Cast.Tinker";
        /// <summary>Cast Calder (third person).</summary>
        public const string CastCalder = "Cast.Calder";

        // ---- MID-B
        /// <summary>Centre of the screening-room screen (MID-B look-at target).</summary>
        public const string Projection = "MidB.Projection";

        // ---- player
        /// <summary>The player's eye (IPlayerRig.Eye): look-at target of Hollis in the climax, the intro hand-off.</summary>
        public const string PlayerEye = "Player.Eye";
        /// <summary>The player's root (IPlayerRig.Root).</summary>
        public const string PlayerRoot = "Player.Root";

        // ---- prefixes
        /// <summary>Prefix that resolves through ISquadService.Member ("Squad.HOL").</summary>
        public const string SquadPrefix = "Squad.";
        /// <summary>Prefix that forces a marker lookup ("Marker.B8.CamReel").</summary>
        public const string MarkerPrefix = "Marker.";

        /// <summary>"Booth.CRT{n}" for n in 1..6.</summary>
        public static string Crt(int n) => CrtPrefix + n;
    }
}
