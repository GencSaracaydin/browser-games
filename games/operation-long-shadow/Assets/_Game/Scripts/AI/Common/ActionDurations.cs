using LongShadow.Core;

namespace LongShadow.AI.Common
{
    /// <summary>
    /// Nominal durations of body actions in seconds (TECH_PLAN §7.7, GDD §7: BoltCut 3 s, PlantSatchel 3 s, RPG raise
    /// 1.2 s). <see cref="DebugCapsuleBody"/> plays them for this long; brains and the squad use
    /// <see cref="Timeout"/> as a safety net in case a body never calls back.
    /// </summary>
    public static class ActionDurations
    {
        /// <summary>Loop actions run until another action or Stop.</summary>
        public const float Loop = -1f;

        /// <summary>Seconds of an action (unknown ids: 0, completes immediately like WP05's rule).</summary>
        public static float Of(string actionId)
        {
            switch (actionId)
            {
                case ActionIds.BoltCut: return 3f;          // GDD §7.2 "3 s animation"
                case ActionIds.PlantSatchel: return 3f;     // GDD §7.6 "in 3 s"
                case ActionIds.ThrowSmokeWhite: return 1.2f;
                case ActionIds.PopSmokeGreen: return 1.2f;
                case ActionIds.FistHold: return 3f;
                case ActionIds.Photograph: return 2.5f;
                case ActionIds.DrillLoop: return Loop;
                case ActionIds.CrackChemlight: return 1.2f;
                case ActionIds.KickDoorStack: return 1.0f;
                case ActionIds.HaulBag: return 4f;
                case ActionIds.Reload: return 2.4f;          // AK tactical reload, GDD §13.1
                case ActionIds.Throw: return 0.9f;
                case ActionIds.Vault: return 0.8f;
                case ActionIds.Climb: return 1.2f;
                case ActionIds.Drop: return 0.6f;
                case ActionIds.LadderClimb: return 3f;
                case ActionIds.HandSignal: return 1f;
                case ActionIds.RpgRaise: return 1.2f;        // GDD §14.1 telegraph
                case ActionIds.SitHeli: return Loop;
                case ActionIds.ProneIdle: return Loop;
                case ActionIds.CrouchRun: return Loop;
                case ActionIds.SkidDrop: return 1.0f;
                case ActionIds.HandsetToEar: return 2f;
                case ActionIds.RaiseCamera: return 2f;
                case AIActionIds.SmokeIdle: return Loop;
                default: return 0f;
            }
        }

        /// <summary>Time point (s) of the effect inside an action (ThrowRelease, ActionHit), or -1.</summary>
        public static float EffectPoint(string actionId)
        {
            switch (actionId)
            {
                case ActionIds.Throw: return 0.55f;
                case ActionIds.ThrowSmokeWhite: return 0.8f;
                case ActionIds.PopSmokeGreen: return 0.8f;
                case ActionIds.BoltCut: return 2.6f;
                case ActionIds.PlantSatchel: return 2.5f;
                case ActionIds.CrackChemlight: return 0.7f;
                case ActionIds.KickDoorStack: return 0.45f;
                default: return -1f;
            }
        }

        /// <summary>Safety timeout for a callback: duration + 2 s (loops: 0 = none).</summary>
        public static float Timeout(string actionId)
        {
            var d = Of(actionId);
            return d < 0f ? 0f : d + 2f;
        }
    }
}
