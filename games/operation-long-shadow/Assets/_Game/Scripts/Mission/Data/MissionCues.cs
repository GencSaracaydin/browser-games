namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Music and SFX cue ids the mission scripts play (ASSET_MANIFEST ids, TECH_PLAN §7.9; GDD §7.0 music column,
    /// §10.3). Layered music cues are addressed without the layer suffix and a layer index (Docs/REQUESTS.md asks WP06's
    /// MusicDirector to map them): <c>MUS_02_MARKET</c> layer 0 = _LO, 1 = _HI; <c>MUS_04_STATION</c> layer 0 = _LOW,
    /// 1 = _FULL. A cue that has not landed is skipped by the audio services (logged once).
    /// </summary>
    public static class MissionCues
    {
        // ---- music (IMusicService)
        /// <summary>Intro and booth (GDD §8.2).</summary>
        public const string MusicSession = "MUS_01_SESSION";
        /// <summary>Beats 2-4, layers LO/HI.</summary>
        public const string MusicMarket = "MUS_02_MARKET";
        /// <summary>MID-A one-shot, swell when the sun touches the ridge.</summary>
        public const string MusicLastLight = "MUS_03_LASTLIGHT";
        /// <summary>Beats 6-9, layers LOW/FULL.</summary>
        public const string MusicStation = "MUS_04_STATION";
        /// <summary>Beats 10-11; hard cut on the RPG launch.</summary>
        public const string MusicExfil = "MUS_05_EXFIL";
        /// <summary>Outro (GDD §8.8).</summary>
        public const string MusicSixForty = "MUS_06_SIXFORTY";
        /// <summary>Greycoat arrival stinger (Beat 4).</summary>
        public const string StingerGreycoat = "STG_GREYCOAT";
        /// <summary>Title card stinger (GDD §8.3).</summary>
        public const string StingerTitle = "STG_TITLE";
        /// <summary>Layer index of the LO / LOW mixes.</summary>
        public const int LayerLow = 0;
        /// <summary>Layer index of the HI / FULL mixes.</summary>
        public const int LayerHigh = 1;

        // ---- music-category one-shots played through IAudioService
        /// <summary>Clockmaker interval signal: 8 ticks 0.5 s apart, then the chime (6.0 s, MID-A from Tinker's PRC-77).</summary>
        public const string IntervalSignal = "INTERVAL_SIGNAL";
        /// <summary>Basement live broadcast loop from the room speaker (Beat 9).</summary>
        public const string NumbersBasementLoop = "NUM_BASEMENT_LOOP";
        /// <summary>The three-note music box (climax shot 2).</summary>
        public const string MusicBox = "MOTIF_MUSICBOX";

        // ---- SFX (IAudioService)
        public const string CurfewSiren = "SFX_CURFEW_SIREN";
        public const string BoltCutter = "SFX_BOLTCUTTER_CHAIN";
        public const string GateCreak = "SFX_GATE_CREAK";
        public const string DoorCreak = "SFX_DOOR_CREAK";
        public const string DoorKick = "SFX_DOOR_KICK";
        public const string BreachExplosion = "SFX_EXP_BREACH";
        public const string SatchelPlant = "SFX_SATCHEL_PLANT";
        public const string FloodSlamOn = "SFX_FLOOD_SLAM_ON";
        public const string ChemlightCrack = "SFX_CHEMLIGHT_CRACK";
        public const string ProjectorStart = "SFX_PROJECTOR_START";
        public const string DrillLoop = "SFX_DRILL_LOOP";
        public const string DrillStartStop = "SFX_DRILL_START_STOP";
        public const string RadioKeyUp = "SFX_RADIO_KEYUP";
        public const string RadioStatic = "SFX_RADIO_STATIC_LOOP";
        public const string Flare = "SFX_FLARE";
        public const string HeliAlarm = "SFX_HELI_ALARM";
        public const string RpgFlyby = "SFX_RPG_FLYBY";
        public const string UiObjective = "SFX_UI_OBJECTIVE";
        public const string UiPickup = "SFX_UI_PICKUP";
        public const string UiIntelCard = "SFX_UI_INTEL_CARD";
        public const string FilmCan = "SFX_FILM_CAN";
        public const string Glitch = "STG_GLITCH";
    }
}
