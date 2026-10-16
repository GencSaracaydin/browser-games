namespace LongShadow.Cinematics
{
    /// <summary>
    /// Audio cue ids (ASSET_MANIFEST ids, see Core <c>AudioCues</c>) used by the booth, the inserts and the timelines.
    /// A cue that has not landed is skipped by WP06's AudioService (logged once), so the sequences still time correctly.
    /// </summary>
    public static class CinematicCues
    {
        // ---- booth set pieces (GDD §8.2, §8.8)
        public const string ClockTickStuck = "SFX_CLOCK_TICK_STUCK";
        public const string FluorescentBuzz = "SFX_FLUORESCENT_BUZZ";
        public const string FluorescentFlicker = "SFX_FLUORESCENT_FLICKER";
        public const string CrtDegauss = "SFX_CRT_DEGAUSS";
        public const string CrtWhine = "SFX_CRT_WHINE";
        public const string CrtStatic = "SFX_CRT_STATIC_LOOP";
        public const string PenTap = "SFX_PEN_TAP";
        public const string Lighter = "SFX_LIGHTER";
        public const string LeatherCreak = "SFX_LEATHER_CREAK";
        public const string ChairCreak = "SFX_CHAIR_CREAK";
        public const string GloveStubble = "SFX_GLOVE_STUBBLE";
        public const string ToggleClick = "SFX_TOGGLE_CLICK";
        public const string Phone = "SFX_PHONE";
        public const string Breath = "SFX_BREATH_BOOTH";
        public const string Heartbeat = "SFX_HEARTBEAT";
        public const string Teletype = "SFX_TELETYPE";
        public const string TypeKey = "SFX_UI_TYPE_KEY";
        public const string TeletypeLine = "SFX_UI_TELETYPE_LINE";
        public const string CameraShutter = "SFX_CAMERA_SHUTTER";
        public const string TapeStop = "STG_LAYERS_TAPE";
        public const string RoomToneBooth = "AMB_ROOMTONE_BOOTH";

        // ---- insertion and climax
        public const string RotorApproach = "SFX_ROTOR_APPROACH";
        public const string RotorLoop = "SFX_ROTOR_LOOP";
        public const string RotorFlyby = "SFX_ROTOR_FLYBY";
        public const string HeliCabin = "SFX_HELI_CABIN_LOOP";
        public const string HeliAlarm = "SFX_HELI_ALARM";
        public const string RpgFlyby = "SFX_RPG_FLYBY";
        public const string MoveLand = "SFX_MOVE_LAND";
        public const string FoleyGear = "SFX_FOLEY_GEAR";
        public const string FootGravel = "SFX_FS_GRAVEL";
        public const string RadioKeyUp = "SFX_RADIO_KEYUP";
        public const string RadioClicks = "SFX_RADIO_CLICKS";
        public const string Tinnitus = "SFX_TINNITUS";
        public const string CurfewSiren = "SFX_CURFEW_SIREN";
        public const string DesertWind = "AMB_DESERT_WIND";
        public const string Cicadas = "AMB_CICADAS";
        public const string CommandoReload = "SFX_WPN_COMMANDO_RELOAD";

        // ---- MID-B projector
        public const string ProjectorStart = "SFX_PROJECTOR_START";
        public const string ProjectorLoop = "SFX_PROJECTOR_LOOP";
        public const string FilmFlap = "SFX_FILM_FLAP";
        public const string FilmCan = "SFX_FILM_CAN";
        public const string LeaderBeep = "SFX_LEADER_BEEP";
        public const string HandSlap = "SFX_HAND_SLAP";

        // ---- music and stingers (IMusicService)
        public const string MusicSession = "MUS_01_SESSION";
        public const string MusicSixForty = "MUS_06_SIXFORTY";
        public const string MusicBox = "MOTIF_MUSICBOX";
        public const string IntervalSignal = "INTERVAL_SIGNAL";
        public const string StingerGlitch = "STG_GLITCH";
        public const string StingerDeath = "STG_DEATH";
        public const string StingerTitle = "STG_TITLE";
    }
}
