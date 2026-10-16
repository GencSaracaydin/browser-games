using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Mission event ids (<see cref="GameEvents.MissionEvent"/>, <see cref="IMissionDirector.Raise"/>) that the mission
    /// scripts raise or listen to. Ids shared with other packages mirror their local tables until Core lists them in
    /// <see cref="SequenceIds"/> (requested in Docs/CORE_CHANGES.md): the AI ids match WP04's <c>AIEvents</c>, the
    /// glassing prefix matches WP02's <c>PlayerEvents.GlassedPrefix</c>, the boot ids match WP01's <c>BootEvents</c>.
    /// Mission never references those assemblies, so the strings are compared.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class MissionEvents
    {
        // ---- Core (SequenceIds)
        /// <summary>TL_Intro signal track: control passes to the player (Beat 1 starts).</summary>
        public const string IntroControlToPlayer = SequenceIds.IntroControlToPlayer;
        /// <summary>MID-A starts (GDD §8.5): the marksman ceases fire.</summary>
        public const string MidAStart = SequenceIds.MidAStart;
        /// <summary>MID-A ends: everything MID-A held is released.</summary>
        public const string MidAEnd = SequenceIds.MidAEnd;

        // ---- WP02 Player (PlayerEvents)
        /// <summary>Raised by the binoculars per GlassingPOI: "Glassed.B1.POI1" (WP02; CORE_CHANGES SequenceIds.GlassedPrefix).</summary>
        public const string GlassedPrefix = SequenceIds.GlassedPrefix;

        // ---- WP04 AI (AIEvents): commands the AI listens to
        /// <summary>Alert an encounter or wave: "AI.Alert.E2.Gate" (GDD §7.2 alert branch).</summary>
        public const string AlertPrefix = SequenceIds.AIAlertPrefix;
        /// <summary>Make a sentry run to its alert cover shouting BK_LEG_01: "AI.Run.E2-01" (GDD §7.2 failure branch).</summary>
        public const string RunPrefix = SequenceIds.AIRunPrefix;
        /// <summary>Beat 4: the technical opens scripted fire at the arch now (2 s after D028, GDD §7.4).</summary>
        public const string TechnicalOpenFire = SequenceIds.B4TechnicalOpen;
        /// <summary>Beat 6: the breach charge detonated (E6 alert, Greycoat wave timer, GDD §7.6).</summary>
        public const string B6Blast = SequenceIds.B6Blast;

        // ---- WP04 AI (AIEvents): events the AI raises
        /// <summary>Beat 5: the marksman's first scope glint (cue for D035).</summary>
        public const string MarksmanFirstGlint = SequenceIds.E5FirstGlint;
        /// <summary>Beat 5: the marksman's first shot (cue for D036).</summary>
        public const string MarksmanFirstShot = SequenceIds.E5FirstShot;
        /// <summary>Beat 4: the technical's fuel tank ignited 1.5 s after E4-01 died.</summary>
        public const string TechnicalIgnited = SequenceIds.B4TechnicalIgnited;
        /// <summary>Beat 10: the RPG gunner fired at Magpie (wave-off, GDD §7.10).</summary>
        public const string RpgFiredAtMagpie = SequenceIds.B10RpgAtMagpie;
        /// <summary>Beat 11: T3 launched the RPG at the helicopter (t 30; D081 and the climax).</summary>
        public const string ExfilRpgLaunch = SequenceIds.B11RpgLaunch;

        // ---- WP01 Platform (BootEvents)
        /// <summary>Raised by Bootstrap when ?autotest=1 skipped the click gate (starts MissionSmokeTest).</summary>
        public const string BootAutotest = SequenceIds.BootAutotest;

        // ---- WP06 Audio (AudioEvents): commands the audio package listens to
        /// <summary>The curfew siren winding down from all three PA horns (GDD §7.1; WP06 AudioEvents.PASiren).</summary>
        public const string PASiren = SequenceIds.PASiren;

        // ---- WP08 UI (UiEvents)
        /// <summary>The HUD title card retracted its bars (GDD §8.3: D007 follows; WP08 UiEvents.TitleCardFinished).</summary>
        public const string TitleCardFinished = SequenceIds.TitleCardFinished;

        // ---- raised by Mission for World (WP10), Combat (WP03), Audio (WP06) and Cinematics (WP09) listeners
        /// <summary>Beat 2: the Weighbridge chain is cut and the gate creaks open (3 s after D020 starts).</summary>
        public const string WeighbridgeOpen = SequenceIds.WeighbridgeOpen;
        /// <summary>Beat 3: an awning tore (WP10 AwningPanel may raise it if it is not a Destructible).</summary>
        public const string AwningTorn = SequenceIds.AwningTorn;
        /// <summary>Beat 4: cinema sky swap started (1.00 → 1.35 over 3 s).</summary>
        public const string CinemaSkySwap = SequenceIds.CinemaSkySwap;
        /// <summary>Beat 6: floodlights F1-F4 slam on (4 s after the blast; WP10 lights + IlluminationZones).</summary>
        public const string FloodsOn = SequenceIds.FloodsOn;
        /// <summary>Beat 7: the Hall door was kicked; slow-mo breach (1.5 s at 0.35).</summary>
        public const string HallBreach = SequenceIds.HallBreach;
        /// <summary>Beat 9: drill started (D060; E9 wave timers count from here).</summary>
        public const string DrillStarted = SequenceIds.DrillStarted;
        /// <summary>Beat 9: the fourth rack died; the broadcast cuts and the emergency lightmap set swaps in (G11).</summary>
        public const string TransmitterDead = SequenceIds.TransmitterDead;
        /// <summary>Beat 10 flares: "B10.FL1" (t 12), "B10.FL2" (t 42), "B10.FL3" (t 72 after a wave-off).</summary>
        public const string FlarePrefix = "B10.";
        /// <summary>Beat 10: Magpie's final approach starts (t 60).</summary>
        public const string HeliApproach = SequenceIds.HeliApproach;
        /// <summary>Beat 10: Magpie waves off along B10.HeliWaveOff (+20 s).</summary>
        public const string HeliWaveOff = SequenceIds.HeliWaveOff;
        /// <summary>Beat 10: wheels down at the pad (D076).</summary>
        public const string HeliWheelsDown = SequenceIds.HeliWheelsDown;
        /// <summary>Beat 11: the exfil spline starts (t 0).</summary>
        public const string ExfilStart = SequenceIds.ExfilStart;
        /// <summary>Beat 11: the two Legion trucks drive into Clock Square (t 16).</summary>
        public const string ExfilTrucks = SequenceIds.ExfilTrucks;
        /// <summary>Beat 11: the climax starts (t 30, input locked, GDD §8.7).</summary>
        public const string ClimaxStart = SequenceIds.ClimaxStart;
        /// <summary>The debrief dossier opened (GDD §8.9).</summary>
        public const string Debrief = SequenceIds.Debrief;
        /// <summary>QUIT TO SESSION: back to the click gate.</summary>
        public const string QuitToSession = SequenceIds.QuitToSession;

        /// <summary>Alert command for an encounter wave ("AI.Alert.E2.Gate").</summary>
        public static string Alert(string encounterId, string wave = null) =>
            string.IsNullOrEmpty(wave) ? AlertPrefix + encounterId : AlertPrefix + encounterId + "." + wave;

        /// <summary>Run command for an enemy ("AI.Run.E2-01").</summary>
        public static string Run(string enemyId) => RunPrefix + enemyId;

        /// <summary>Glassed event of a POI ("Glassed.B1.POI1").</summary>
        public static string Glassed(string poiId) => GlassedPrefix + poiId;
    }
}
