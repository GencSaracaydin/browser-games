using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 4 CLOCK SQUARE / CINEMA ZENITH (GDD §7.4): squad pinned at the arch (Q1), D028; the technical opens fire 2 s
    /// later with D029; FLANK THROUGH THE CINEMA ([E] Open, D031; D030 nag after 45 s); the cinema sky swap 1.00 → 1.35
    /// over 3 s in the lobby (ILightStage.Override; the sky never changes outside); NEUTRALIZE THE TECHNICAL (red,
    /// through walls) from the auditorium; D032 and Q2 when he dies; STG_GREYCOAT + D033 on the Greycoat pair; REGROUP;
    /// ladder L opens when every E4 enemy is dead; UP TOP with D034 within 10 m of the ladder (S6_StationInt loads).
    /// Exit: the ladder top (CP5).
    /// </summary>
    public sealed class Beat04ClockSquare : BeatScript
    {
        /// <summary>The technical opens fire this long after D028 (GDD §7.4).</summary>
        public const float TechnicalDelay = 2f;
        /// <summary>D030 nag delay after the technical opens fire.</summary>
        public const float NagDelay = 45f;
        /// <summary>Sky swap length (GDD §7.4, §9.1).</summary>
        public const float SkySwapSeconds = 3f;
        /// <summary>s after the sky swap (GDD §9.2a "cinema swap done").</summary>
        public const float SkySwapS = 1.35f;
        /// <summary>The ladder top counts as reached within this distance of CP5 and above <see cref="LadderTopMinY"/>.</summary>
        public const float LadderTopRadius = 4f;
        /// <summary>Minimum height of the ladder top (R1 at +9).</summary>
        public const float LadderTopMinY = 7.5f;
        /// <summary>D034 radius around the ladder (GDD §7.4 "player within 10 m of the ladder").</summary>
        public const float LadderLineRadius = 10f;

        private static readonly object s_skySwapOwner = new object();

        /// <inheritdoc/>
        public override int Number => 4;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            SaveStartCheckpoint(ctx); // CP4 at the arch (185, 0, 248)
            ctx.Nodes(MissionMarkers.B4NodeSetQ1);
            if (!ctx.Flag(MissionFlags.B4SkySwap)) ctx.LightRange(1.00f, 1.00f); // the sky only changes inside the cinema
            else ctx.LightRange(SkySwapS, 1.50f);
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E4);
            ctx.Open(GateTable.UseSideDoor);

            ctx.Line(LineIds.D028); // arch reveal
            yield return ctx.Seconds(TechnicalDelay);
            ctx.Raise(MissionEvents.TechnicalOpenFire);
            var technicalOpen = ctx.Now;
            ctx.Line(LineIds.D029);
            ctx.Objective(ObjectiveTable.B4Flank);
            ctx.Run(Nag(ctx, technicalOpen), "B4 D030");
            ctx.Run(SkySwap(ctx), "B4 sky swap");
            ctx.Run(GreycoatArrival(ctx), "B4 Greycoats");
            ctx.Run(LadderLine(ctx), "B4 D034");

            // 1. Side door opened ([E] Open).
            yield return Wait.Any(ctx.Interacted(MissionMarkers.B4SideDoor), ctx.Trigger(MissionTriggers.B4Lobby),
                ctx.Trigger(MissionTriggers.B4Auditorium));
            ctx.Open(GateTable.CinemaSideDoor);
            ctx.Close(GateTable.UseSideDoor);
            ctx.SfxAt(MissionCues.DoorCreak, MissionMarkers.B4SideDoor);
            ctx.Complete(ObjectiveTable.B4Flank);
            ctx.Line(LineIds.D031);

            // 2. On entering the auditorium: NEUTRALIZE THE TECHNICAL.
            yield return Wait.Any(ctx.Trigger(MissionTriggers.B4Auditorium), ctx.Trigger(MissionTriggers.B4Marquee),
                ctx.Killed(EncounterIds.TechnicalGunner));
            ctx.Objective(ObjectiveTable.B4Technical);
            yield return ctx.Killed(EncounterIds.TechnicalGunner);
            ctx.Complete(ObjectiveTable.B4Technical);
            ctx.Line(LineIds.D032);
            ctx.Nodes(MissionMarkers.B4NodeSetQ2);

            // 3. REGROUP WITH HOLLISTER until every E4 enemy is dead; the ladder gate opens then.
            ctx.Objective(ObjectiveTable.B4Regroup);
            yield return ctx.Cleared(EncounterIds.E4);
            ctx.Complete(ObjectiveTable.B4Regroup);
            ctx.Open(GateTable.LadderL);
            ctx.Follow();

            // 4. UP TOP: the ladder; the beat ends at the ladder top.
            ctx.Objective(ObjectiveTable.B4UpTop);
            yield return ctx.PlayerNear(SequenceIds.CP5, LadderTopRadius, LadderTopMinY);
            if (!ctx.Lines.WasRequested(LineIds.D034)) ctx.Line(LineIds.D034);
            ctx.Complete(ObjectiveTable.B4UpTop);
        }

        private static IEnumerator Nag(MissionContext ctx, float technicalOpen)
        {
            yield return ctx.At(technicalOpen + NagDelay);
            if (!ctx.IsComplete(ObjectiveTable.B4Flank)) ctx.Line(LineIds.D030);
        }

        private IEnumerator SkySwap(MissionContext ctx)
        {
            yield return Wait.Any(ctx.Trigger(MissionTriggers.B4Lobby), ctx.Trigger(MissionTriggers.B4Auditorium));
            if (ctx.Flag(MissionFlags.B4SkySwap)) yield break;
            ctx.LightOverride(s_skySwapOwner, true);
            ctx.Raise(MissionEvents.CinemaSkySwap);
            var from = Mathf.Min(ctx.LightS, SkySwapS);
            var t0 = ctx.Now;
            yield return Wait.Until(() =>
            {
                var t = Mathf.Clamp01((ctx.Now - t0) / SkySwapSeconds);
                ctx.LightKey(Mathf.Lerp(from, SkySwapS, t));
                return t >= 1f;
            }, "sky swap");
            ctx.LightOverride(s_skySwapOwner, false);
            ctx.SetFlag(MissionFlags.B4SkySwap, true);
            ctx.LightRange(SkySwapS, 1.50f);
        }

        private static IEnumerator GreycoatArrival(MissionContext ctx)
        {
            yield return ctx.WaveSpawnedWait(EncounterIds.E4, EncounterIds.E4Greycoat);
            if (ctx.IsCleared(EncounterIds.E4)) yield break;
            ctx.Music?.Stinger(MissionCues.StingerGreycoat);
            ctx.Line(LineIds.D033);
        }

        private static IEnumerator LadderLine(MissionContext ctx)
        {
            // D034: all Beat 4 enemies dead and the player within 10 m of the ladder (objective-critical; S6 loads on it).
            yield return ctx.Cleared(EncounterIds.E4);
            yield return Wait.Any(ctx.Trigger(MissionTriggers.B4Ladder), ctx.PlayerNear(MissionMarkers.B4Ladder, LadderLineRadius),
                ctx.PlayerNear(SequenceIds.CP5, LadderTopRadius, LadderTopMinY));
            if (!ctx.Lines.WasRequested(LineIds.D034)) ctx.Line(LineIds.D034);
        }
    }
}
