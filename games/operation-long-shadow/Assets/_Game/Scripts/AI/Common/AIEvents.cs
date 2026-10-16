using LongShadow.Core;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Mission event ids (<see cref="GameEvents.MissionEvent"/>) that the AI listens to or raises. Local adapter until the
    /// ids are added to <see cref="SequenceIds"/> (requested in Docs/CORE_CHANGES.md, WP04). Mission scripts (WP07) raise
    /// the "listened" ids with <c>IMissionDirector.Raise</c>; WP07/WP10 listen to the "raised" ids.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class AIEvents
    {
        // ---- commands the AI listens to ------------------------------------------------------------------------

        /// <summary>
        /// Prefix of an alert command: <c>AI.Alert.&lt;encounter&gt;</c> alerts every living enemy of an encounter,
        /// <c>AI.Alert.&lt;encounter&gt;.&lt;wave&gt;</c> one wave (waves not spawned yet spawn alerted). Example: the
        /// Beat 2 alert branch raises <c>AI.Alert.E2.Gate</c> (GDD §7.2).
        /// </summary>
        public const string AlertPrefix = SequenceIds.AIAlertPrefix;

        /// <summary>
        /// Prefix of a run command: <c>AI.Run.&lt;enemyId&gt;</c> makes a sentry run to its wave's alert cover shouting
        /// BK_LEG_01 (GDD §7.2 failure branch: <c>AI.Run.E2-01</c>).
        /// </summary>
        public const string RunPrefix = SequenceIds.AIRunPrefix;

        /// <summary>Beat 4: the technical opens scripted fire at the arch now (else 2 s after D028 starts, GDD §7.4).</summary>
        public const string TechnicalOpenFire = SequenceIds.B4TechnicalOpen;

        /// <summary>Beat 6: the breach charge detonated (E6 Greycoat wave: 25 s after the blast, GDD §7.6).</summary>
        public const string B6Blast = SequenceIds.B6Blast;

        // ---- events the AI raises --------------------------------------------------------------------------------

        /// <summary>Beat 5: the marksman's first scope glint (cue for D035, GDD §7.5).</summary>
        public const string MarksmanFirstGlint = SequenceIds.E5FirstGlint;

        /// <summary>Beat 5: the marksman's first shot (cue for D036, GDD §7.5).</summary>
        public const string MarksmanFirstShot = SequenceIds.E5FirstShot;

        /// <summary>Beat 4: the technical's fuel tank ignited 1.5 s after E4-01 died (WP10 enables the fire prop at B4.FireProp).</summary>
        public const string TechnicalIgnited = SequenceIds.B4TechnicalIgnited;

        /// <summary>Beat 10: the RPG gunner fired at Magpie (WP07 starts the wave-off along B10.HeliWaveOff, GDD §7.10).</summary>
        public const string RpgFiredAtMagpie = SequenceIds.B10RpgAtMagpie;

        /// <summary>Beat 11: T3 launched the RPG at the helicopter at t 30 (cue for D081 and the climax, GDD §7.11).</summary>
        public const string ExfilRpgLaunch = SequenceIds.B11RpgLaunch;

        /// <summary>A morale break happened: <c>AI.Morale.&lt;encounter&gt;.&lt;wave&gt;</c> (diagnostics, music hooks).</summary>
        public const string MoralePrefix = SequenceIds.AIMoralePrefix;

        /// <summary>Prefix of the condition-event id the spawner records for a VO line start (<c>VO:D028</c>).</summary>
        public const string VoPrefix = "VO:";

        /// <summary>Condition-event id of a VO line start (allocates; VO lines are rare).</summary>
        public static string Vo(string lineId) => VoPrefix + lineId;

        /// <summary>
        /// Parses an alert command. Returns true for <c>AI.Alert.E2</c> (wave null) and <c>AI.Alert.E2.Gate</c>.
        /// Allocates only when the id is an alert command.
        /// </summary>
        public static bool TryParseAlert(string eventId, out string encounterId, out string wave)
        {
            encounterId = null;
            wave = null;
            if (string.IsNullOrEmpty(eventId) || !eventId.StartsWith(AlertPrefix, System.StringComparison.Ordinal)) return false;
            var rest = eventId.Substring(AlertPrefix.Length);
            if (rest.Length == 0) return false;
            var dot = rest.IndexOf('.');
            if (dot < 0)
            {
                encounterId = rest;
                return true;
            }
            encounterId = rest.Substring(0, dot);
            wave = dot + 1 < rest.Length ? rest.Substring(dot + 1) : null;
            return encounterId.Length > 0;
        }

        /// <summary>Parses a run command (<c>AI.Run.E2-01</c>).</summary>
        public static bool TryParseRun(string eventId, out string enemyId)
        {
            enemyId = null;
            if (string.IsNullOrEmpty(eventId) || !eventId.StartsWith(RunPrefix, System.StringComparison.Ordinal)) return false;
            enemyId = eventId.Substring(RunPrefix.Length);
            return enemyId.Length > 0;
        }
    }
}
