namespace LongShadow.Cinematics
{
    /// <summary>
    /// Ids of the non-manifest entries of LIB_Cinematics (materials, profiles, fonts) and the timeline asset names
    /// (TECH_PLAN §4.2 naming: TL_, M_, VP_). Image and prefab entries use their manifest ids / prefab names.
    /// </summary>
    public static class CinematicAssetIds
    {
        // ---- timelines (Assets/_Game/Timelines)
        public const string TimelineIntro = "TL_Intro";
        public const string TimelineTitleCard = "TL_TitleCard";
        public const string TimelineMidB = "TL_MidB_Reel";
        public const string TimelineClimax = "TL_Climax";
        public const string TimelineOutro = "TL_Outro";

        // ---- materials (Assets/_Game/Art/Materials/FX/Cinematics)
        /// <summary>SG_CRTScreen base material (instanced per CRT).</summary>
        public const string MatCrt = "M_FX_CRTScreen";
        /// <summary>SG_GunCam material (outro CRT2).</summary>
        public const string MatGunCam = "M_FX_GunCam";
        /// <summary>SG_Film16mm material (MID-B screen).</summary>
        public const string MatFilm = "M_FX_Film16mm";
        /// <summary>SG_FilmBurn UI material (MID-B shot 8 burn-out).</summary>
        public const string MatFilmBurn = "M_FX_FilmBurn";
        /// <summary>SG_Glitch full-screen material (renderer feature "Glitch").</summary>
        public const string MatGlitch = "M_FX_Glitch";
        /// <summary>SG_GlitchOverlay UI material (static/tear overlay; renderer-feature fallback).</summary>
        public const string MatGlitchOverlay = "M_FX_GlitchOverlay";
        /// <summary>SG_FilmBeam additive projector beam.</summary>
        public const string MatFilmBeam = "M_FX_FilmBeam";
        /// <summary>Booth cigarette smoke particles.</summary>
        public const string MatSmoke = "M_FX_BoothSmoke";

        // ---- volume profiles (FX volumes owned by WP09, TECH_PLAN §3.7)
        public const string ProfileDeath = "VP_Death";
        public const string ProfileLowHealth = "VP_LowHealth";
        public const string ProfileSuppression = "VP_Suppression";
        public const string ProfileFlash = "VP_Flash";
        public const string ProfileCinematic = "VP_Cinematic";
        public const string ProfileBooth = "VP_Booth";

        // ---- fonts (TMP font assets from WP08's OFL fonts; LiberationSans fallback)
        public const string FontTypewriter = "FONT_Typewriter";
        public const string FontMono = "FONT_Mono";

        // ---- cast prefabs (WP05 bodies, WP10 vehicle)
        public const string PrefabHollis = "PFB_Hollis";
        public const string PrefabSparrow = "PFB_Sparrow";
        public const string PrefabTinker = "PFB_Tinker";
        public const string PrefabCalder = "PFB_Calder";
        public const string PrefabCalderShadow = "PFB_Calder_Shadow";
        public const string PrefabHelicopter = "VEH_HELICOPTER";
        public const string PrefabHeliCabin = "VEH_HELI_CABIN";
        public const string ModelHeliMainRotor = "SM_HeliMainRotor";
        public const string ModelHeliTailRotor = "SM_HeliTailRotor";
        /// <summary>WP10_LOOK rotor downwash dust (VFX_ROTOR_DOWNWASH), under the main rotor hub.</summary>
        public const string PrefabRotorDownwash = "PF_FX_RotorDownwash";
        /// <summary>WP10_LOOK shadows-only rotor blades (the cabin strobe), under the main rotor hub.</summary>
        public const string PrefabRotorShadow = "PF_FX_RotorShadow";

        // ---- images (manifest ids; WP09_ART file names)
        public const string ImgMap = "IMG_MAP_SARDAHN";
        public const string ImgRecon = "IMG_RECON_STATION";
        public const string ImgHarrou = "IMG_HARROU_STILL";
        public const string ImgSurveil = "IMG_SURVEIL_SPARROW";
        public const string ImgTeletype = "IMG_TELETYPE_FEED";
        public const string ImgTeamHollis = "IMG_TEAM_ID_PHOTOS/Hollis";
        public const string ImgTeamSparrow = "IMG_TEAM_ID_PHOTOS/Sparrow";
        public const string ImgTeamTinker = "IMG_TEAM_ID_PHOTOS/Tinker";
        public const string ImgRoster = "IMG_ROSTER_PAGE";
        public const string ImgCodebookPage = "IMG_CODEBOOK_PAGE";
        public const string ImgLeader5 = "IMG_FILM_LEADER/5";
        public const string ImgLeader4 = "IMG_FILM_LEADER/4";
        public const string ImgLeader3 = "IMG_FILM_LEADER/3";

        // ---- videos (StreamingAssets/video, played with VideoPlayer.url, TECH_PLAN D14)
        public const string VideoHarrou = "VID_Harrou8mm.mp4";
        public const string VideoGunCam = "VID_GunCam.mp4";
        public const string VideoLeader = "VID_FilmLeader.mp4";
    }
}
