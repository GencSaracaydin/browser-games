using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Action ids the beat scripts pass to <c>ISquadService.PlayScripted</c> that are not in Core <c>ActionIds</c> yet
    /// (requested by WP05 in Docs/CORE_CHANGES.md as <c>ExtraActionIds</c>; the strings are the same). An unknown action
    /// logs once and completes immediately (WP05), so a script never hangs on one.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.ActionIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class MissionActionIds
    {
        /// <summary>Kneel at the marker (Beat 2 sync: Hollis at the mark, Sparrow and Tinker at the hold).</summary>
        public const string Kneel = ActionIds.Kneel;
        /// <summary>Ends any looping action (the drill when the safe opens).</summary>
        public const string Stop = ActionIds.Stop;
        /// <summary>Glance at the watch.</summary>
        public const string CheckWatch = ActionIds.CheckWatch;
    }
}
