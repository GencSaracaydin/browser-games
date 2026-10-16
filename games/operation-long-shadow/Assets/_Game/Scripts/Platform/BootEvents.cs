using LongShadow.Core;

namespace LongShadow.Platform
{
    /// <summary>
    /// Mission events raised by <see cref="Bootstrap"/> through <c>GameEvents.RaiseMissionEvent</c>, so systems that cannot
    /// reference Platform (WP07 MissionDirector, WP11 smoke test, WP08 gate) can follow the boot.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.SequenceIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class BootEvents
    {
        /// <summary>S_Persistent is loaded and active.</summary>
        public const string PersistentLoaded = SequenceIds.BootPersistentLoaded;
        /// <summary>The click gate accepts the click (S_Persistent and S0_Booth loaded, TECH_PLAN D20).</summary>
        public const string GateReady = SequenceIds.BootGateReady;
        /// <summary>The gate was clicked (or skipped) and the mission was started.</summary>
        public const string MissionStarted = SequenceIds.BootMissionStarted;
        /// <summary>The run is an automated smoke test (?autotest=1); raised right before <see cref="MissionStarted"/>.</summary>
        public const string Autotest = SequenceIds.BootAutotest;
        /// <summary>Boot failed (S_Persistent did not load).</summary>
        public const string Failed = SequenceIds.BootFailed;
    }
}
