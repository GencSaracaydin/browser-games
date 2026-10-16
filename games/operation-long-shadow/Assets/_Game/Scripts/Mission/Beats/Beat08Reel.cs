using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 8 THE REEL (GDD §7.8): Sparrow, Tinker and Hollis (in the doorway, back to the screen) at B8.NodeSet.Reel;
    /// D053 on entering the screening room; THREAD THE PROJECTOR ([E]) → MID-B (TL_MidB_Reel, 45 s: D054 on I3, D055 at
    /// the burn-out; WP09); RECOVER THE FILM (hold E, INTEL 2/3, D056, D057); optional O2 (D058); the west stair door and
    /// B8.T.BasementFoot open with INTEL 2/3; GET TO THE BASEMENT. Exit: the basement stair foot → Beat 9.
    /// </summary>
    public sealed class Beat08Reel : BeatScript
    {
        /// <inheritdoc/>
        public override int Number => 8;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            SaveStartCheckpoint(ctx); // CP8 on the landing (335, 4.8, 355)
            ctx.Nodes(MissionMarkers.B8NodeSetReel);
            yield return WaitForSection(ctx);
            ctx.SpawnIntel(IntelTable.FilmLabel);
            ctx.Objective(ObjectiveTable.B8O2);
            ctx.Run(EnterLine(ctx), "B8 D053");

            // 1. THREAD THE PROJECTOR → MID-B.
            ctx.Open(GateTable.UseProjector);
            ctx.Objective(ObjectiveTable.B8Thread);
            yield return ctx.Interacted(MissionMarkers.B8Projector);
            ctx.Close(GateTable.UseProjector);
            yield return ctx.Cinematic(SequenceIds.MidB, SequenceTimelines.MidBSeconds + 15f, () => MidBFallback(ctx));
            ctx.SetFlag(MissionFlags.B8MidBDone, true);
            ctx.Complete(ObjectiveTable.B8Thread);

            // 2. RECOVER THE FILM (INTEL 2/3): D056 on the pickup (director), D057 after it.
            ctx.SpawnIntel(IntelTable.Reels);
            ctx.Objective(ObjectiveTable.B8Film);
            yield return ctx.Intel(IntelTable.Reels);
            ctx.RunDetached(ctx.SayAfter(LineIds.D056, 0f, LineIds.D057), "B8 D057");

            // Gate (GDD §7.13): the west stair door and the basement foot trigger open with INTEL 2/3.
            ctx.Open(GateTable.WestStairDoor);
            ctx.Open(GateTable.ArmBasementFoot);
            ctx.Objective(ObjectiveTable.B8Basement);
            yield return ctx.TriggerFromNow(MissionTriggers.B8BasementFoot);
            ctx.Complete(ObjectiveTable.B8Basement);
        }

        private static IEnumerator EnterLine(MissionContext ctx)
        {
            yield return ctx.Trigger(MissionTriggers.B8Enter);
            ctx.Line(LineIds.D053);
        }

        private static IEnumerator MidBFallback(MissionContext ctx)
        {
            // GDD §8.6 without the Timeline: I3 with D054 at 0:36, D055 at the burn-out (0:40), control at 0:45.
            var t0 = ctx.Now;
            ctx.Sfx(MissionCues.ProjectorStart, ctx.PlayerPosition);
            yield return ctx.At(t0 + SequenceTimelines.MidBI3);
            ctx.Insert(SequenceIds.InsertI3, LineIds.D054);
            yield return ctx.At(t0 + 40f);
            ctx.Line(LineIds.D055);
            ctx.Rig?.Shake(0.3f); // Hollis's hand on the shoulder
            yield return ctx.At(t0 + SequenceTimelines.MidBSeconds);
        }
    }
}
