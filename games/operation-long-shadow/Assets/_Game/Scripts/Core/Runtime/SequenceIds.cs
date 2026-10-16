namespace LongShadow.Core
{
    /// <summary>
    /// Ids of authored sequences shared by Mission (WP07), Cinematics (WP09), UI (WP08) and the dev console:
    /// cinematics for <see cref="ICinematicService.Play"/>, glitch inserts for <see cref="IInsertService.Play"/>,
    /// checkpoint ids (<see cref="ICheckpointService"/>, <see cref="CheckpointMarker"/>) and well-known mission events
    /// (<see cref="IMissionDirector.Raise"/>, <see cref="GameEvents.MissionEvent"/>). Beat scripts may raise more events;
    /// list every event another package listens to here (additive Core change, Docs/CORE_CHANGES.md).
    /// </summary>
    public static class SequenceIds
    {
        // ---- cinematics (TECH_PLAN §7.12; CINEMATIC_DIRECTION §2)
        /// <summary>TL_Intro: booth shots 1-8, the §8.2a INSERTION cold open (I-1..I-12), then shot 9 (about 2:38).</summary>
        public const string Intro = "Intro";
        /// <summary>TL_TitleCard, 8 s (always plays, even when the intro is skipped).</summary>
        public const string TitleCard = "TitleCard";
        /// <summary>TL_MidB_Reel, 45 s (Beat 8, GDD §8.6).</summary>
        public const string MidB = "MidB";
        /// <summary>TL_Climax, 12 s (Beat 11 end, GDD §8.8).</summary>
        public const string Climax = "Climax";
        /// <summary>TL_Outro, 1:50 (GDD §8.9 before the debrief).</summary>
        public const string Outro = "Outro";

        // ---- glitch inserts (GDD §5.5)
        public const string InsertI1 = "I1";
        public const string InsertI2 = "I2";
        public const string InsertI3 = "I3";
        public const string InsertI4 = "I4";
        /// <summary>Death insert (GDD §8.4); played with <see cref="IInsertService.PlayDeath"/>.</summary>
        public const string InsertDeath = "DEATH";

        // ---- checkpoints (GDD §17; Appendix A "CP")
        public const string CP1 = "CP1";
        public const string CP2 = "CP2";
        public const string CP3 = "CP3";
        /// <summary>Mid-Beat 3 checkpoint at the market crossing.</summary>
        public const string CP3b = "CP3b";
        public const string CP4 = "CP4";
        public const string CP5 = "CP5";
        public const string CP6 = "CP6";
        public const string CP7 = "CP7";
        public const string CP8 = "CP8";
        public const string CP9 = "CP9";
        public const string CP10 = "CP10";

        /// <summary>Every checkpoint in mission order.</summary>
        public static readonly string[] Checkpoints = { CP1, CP2, CP3, CP3b, CP4, CP5, CP6, CP7, CP8, CP9, CP10 };

        /// <summary>Order of a checkpoint in <see cref="Checkpoints"/> (-1 if unknown); used for "highest checkpoint".</summary>
        public static int CheckpointOrder(string cpId) => System.Array.IndexOf(Checkpoints, cpId);

        // ---- mission events with listeners outside Mission
        /// <summary>Signal track in TL_Intro: control passes to the player (Beat 1 starts).</summary>
        public const string IntroControlToPlayer = "Intro.ControlToPlayer";
        /// <summary>MID-A starts (GDD §8.5): the marksman ceases fire.</summary>
        public const string MidAStart = "MIDA.Start";
        /// <summary>MID-A ends: everything MID-A held is released.</summary>
        public const string MidAEnd = "MIDA.End";

        // ---- WP01 Platform boot (raised by Bootstrap)
        /// <summary>S_Persistent is loaded and active.</summary>
        public const string BootPersistentLoaded = "Boot.PersistentLoaded";
        /// <summary>The click gate accepts the click (S_Persistent and S0_Booth loaded, TECH_PLAN D20).</summary>
        public const string BootGateReady = "Boot.GateReady";
        /// <summary>The gate was clicked (or skipped) and the mission was started.</summary>
        public const string BootMissionStarted = "Boot.MissionStarted";
        /// <summary>The run is an automated smoke test (?autotest=1); raised right before <see cref="BootMissionStarted"/>.</summary>
        public const string BootAutotest = "Boot.Autotest";
        /// <summary>Boot failed (S_Persistent did not load).</summary>
        public const string BootFailed = "Boot.Failed";

        // ---- WP02 Player
        /// <summary>
        /// Raised by WP02 Binoculars once per GlassingPOI when the binocular view keeps it within its radius for its dwell
        /// time (GDD §7.1): GlassedPrefix + POI id, e.g. "Glassed.B1.POI1". The ticked set clears on CheckpointLoaded.
        /// </summary>
        public const string GlassedPrefix = "Glassed.";

        // ---- WP04 AI: commands the AI listens to
        /// <summary>
        /// "AI.Alert.&lt;encounter&gt;[.&lt;wave&gt;]": alerts an encounter or one wave; waves not spawned yet spawn alerted
        /// (Beat 2 alert branch: "AI.Alert.E2.Gate").
        /// </summary>
        public const string AIAlertPrefix = "AI.Alert.";
        /// <summary>"AI.Run.&lt;enemyId&gt;": a sentry runs to its wave's alert cover shouting BK_LEG_01 ("AI.Run.E2-01").</summary>
        public const string AIRunPrefix = "AI.Run.";
        /// <summary>Optional: the technical opens fire now; otherwise 2 s after D028 starts (GDD §7.4).</summary>
        public const string B4TechnicalOpen = "B4.TechnicalOpen";
        /// <summary>Beat 6: the breach detonated (E6 yard alert, gatehouse wave timer, blown wall).</summary>
        public const string B6Blast = "B6.Blast";
        // ---- WP04 AI: events the AI raises
        /// <summary>Beat 5: the marksman's first scope glint (cue for D035).</summary>
        public const string E5FirstGlint = "E5.FirstGlint";
        /// <summary>Beat 5: the marksman's first shot (cue for D036).</summary>
        public const string E5FirstShot = "E5.FirstShot";
        /// <summary>Raised 1.5 s after E4-01 dies: WP10 lights the fire prop at B4.FireProp.</summary>
        public const string B4TechnicalIgnited = "B4.TechnicalIgnited";
        /// <summary>Raised when E10-07 fires at Magpie: WP07 starts the wave-off.</summary>
        public const string B10RpgAtMagpie = "B10.RPGAtMagpie";
        /// <summary>Raised when B11 T3 launches at t 30: D081 and the climax.</summary>
        public const string B11RpgLaunch = "B11.RPGLaunch";
        /// <summary>"AI.Morale.&lt;encounter&gt;.&lt;wave&gt;", raised on a morale break (diagnostics, music).</summary>
        public const string AIMoralePrefix = "AI.Morale.";

        // ---- WP06 Audio
        /// <summary>Booth room tone instead of the world beds (CinematicStarted(Intro|Outro) does it by default).</summary>
        public const string AmbienceBoothOn = "Ambience.Booth.On";
        /// <summary>Back to the world beds.</summary>
        public const string AmbienceBoothOff = "Ambience.Booth.Off";
        /// <summary>Every ambience bed fades out.</summary>
        public const string AmbienceMute = "Ambience.Mute";
        /// <summary>Ends <see cref="AmbienceMute"/>.</summary>
        public const string AmbienceUnmute = "Ambience.Unmute";
        /// <summary>Starts the basement numbers broadcast (BeatStarted(9) and CP9 start it by default).</summary>
        public const string NumbersStart = "B9.Numbers.Start";
        /// <summary>Stops the basement numbers broadcast without the cut line.</summary>
        public const string NumbersStop = "B9.Numbers.Stop";
        /// <summary>Raised by WP06 when the fourth rack dies: the basement tube hum stops.</summary>
        public const string TransmittersDown = "B9.TransmittersDown";
        /// <summary>Curfew siren from the three PA horns.</summary>
        public const string PASiren = "PA.Siren";
        /// <summary>"PA.Play." + cue id: any cue from the three PA horns.</summary>
        public const string PAPlayPrefix = "PA.Play.";

        // ---- WP10 Look / Level
        /// <summary>Beat 4 cinema sky swap (s 1.00 to 1.35).</summary>
        public const string CinemaSkySwap = "B4.SkySwap";
        /// <summary>Beat 6: floods F1-F4 slam on.</summary>
        public const string FloodsOn = "B6.FloodsOn";
        /// <summary>Beat 9: the fourth rack died; emergency lightmap set and red stair lights (G11). Raised by WP07.</summary>
        public const string TransmitterDead = "B9.TransmitterDead";
        /// <summary>Beat 10 parachute flare 1 (t 12) at the marker of the same id.</summary>
        public const string Flare1 = "B10.FL1";
        /// <summary>Beat 10 parachute flare 2 (t 42).</summary>
        public const string Flare2 = "B10.FL2";
        /// <summary>Beat 10 parachute flare 3 (t 72, wave-off only).</summary>
        public const string Flare3 = "B10.FL3";
        /// <summary>Beat 3: the first market awning was torn by tracers (raised once by WP10 Level's AwningPanel; WP07 plays D023).</summary>
        public const string AwningTorn = "B3.AwningTorn";

        // ---- WP08 UI
        /// <summary>Raised by the HUD when the GDD §8.3 title card has retracted its bars; D007 follows (WP07).</summary>
        public const string TitleCardFinished = "UI.TitleCardFinished";
        /// <summary>The click gate became ready while shown (boot, QUIT TO SESSION, END SESSION); WP01 re-arms the browser gesture.</summary>
        public const string UIGateReady = "UI.Gate.Ready";
        /// <summary>The debrief finished typing and shows its buttons (GDD §8.9).</summary>
        public const string DebriefReady = "UI.Debrief.Ready";
        /// <summary>[REPLAY SESSION] was pressed.</summary>
        public const string DebriefReplay = "UI.Debrief.Replay";
        /// <summary>[END SESSION] was pressed.</summary>
        public const string DebriefEnd = "UI.Debrief.End";
        /// <summary>"UI.Debrief.Select." + checkpoint id: a beat was selected in the debrief.</summary>
        public const string DebriefSelectPrefix = "UI.Debrief.Select.";
        /// <summary>QUIT TO SESSION was confirmed in the pause menu.</summary>
        public const string PauseQuit = "UI.Pause.Quit";
        /// <summary>RESTART CHECKPOINT was confirmed in the pause menu.</summary>
        public const string PauseRestart = "UI.Pause.Restart";

        // ---- WP07 Mission (raised by the beat scripts)
        /// <summary>Beat 2: the chain is cut and the Weighbridge gate swings open, 3 s after D020 starts.</summary>
        public const string WeighbridgeOpen = "B2.GateOpen";
        /// <summary>Beat 7: the Hall door was kicked; 1.5 s slow-mo breach at 0.35.</summary>
        public const string HallBreach = "B7.Breach";
        /// <summary>Beat 9: D060, Sparrow's drill starts; E9 wave timers count from here.</summary>
        public const string DrillStarted = "B9.DrillStart";
        /// <summary>Beat 10 Magpie: final approach at hold t 60.</summary>
        public const string HeliApproach = "B10.HeliApproach";
        /// <summary>Beat 10 Magpie: wave-off after the RPG shot.</summary>
        public const string HeliWaveOff = "B10.HeliWaveOff";
        /// <summary>Beat 10 Magpie: wheels down at t 75/95.</summary>
        public const string HeliWheelsDown = "B10.WheelsDown";
        /// <summary>Beat 11: exfil spline t 0.</summary>
        public const string ExfilStart = "B11.ExfilStart";
        /// <summary>Beat 11: the Clock Square trucks at t 16.</summary>
        public const string ExfilTrucks = "B11.Trucks";
        /// <summary>Beat 11: the RPG launch / climax at t 30.</summary>
        public const string ClimaxStart = "B11.Climax";
        /// <summary>The debrief opened.</summary>
        public const string Debrief = "Mission.Debrief";
        /// <summary>QUIT TO SESSION: back to the click gate.</summary>
        public const string QuitToSession = "Mission.QuitToSession";
        /// <summary>MissionSmokeTest finished; its JSON is MissionSmokeTest.LastResultJson (WP11 forwards it to the page).</summary>
        public const string SmokeDone = "Smoke.Done";
    }
}
