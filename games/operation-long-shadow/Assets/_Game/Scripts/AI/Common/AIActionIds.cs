using LongShadow.Core;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Action ids the AI plays that are not in <c>LongShadow.Core.ActionIds</c> yet (local adapter; requested in
    /// Docs/CORE_CHANGES.md). WP05 bodies map unknown ids to "log once and complete immediately", so these are safe
    /// before WP05 adds clips for them.
    /// <para>The requested Core constants landed (LAND pass, 2026-10-16): the shared ids here are now thin aliases of
    /// <c>LongShadow.Core.ActionIds</c>; new code should use the Core constants directly.</para>
    /// </summary>
    public static class AIActionIds
    {
        /// <summary>Beat 2 sentries: smoking idle loop (GDD §7.2 "Smoking idle").</summary>
        public const string SmokeIdle = ActionIds.SmokeIdle;

        /// <summary>
        /// Ends a looping action (smoking, drilling) and returns to locomotion. WP05 HumanoidBody maps it in
        /// <c>ExtraActionIds.Stop</c>; <see cref="DebugCapsuleBody"/> treats it as "stop the current action".
        /// </summary>
        public const string Stop = ActionIds.Stop;
    }
}
