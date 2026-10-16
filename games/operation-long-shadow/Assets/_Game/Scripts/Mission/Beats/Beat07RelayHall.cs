using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 7 THE RELAY HALL (GDD §7.7): the slow-mo breach (1.5 s at timeScale 0.35), BREACH (E7-01…03), the squad's
    /// corner clears (Hollis west, Tinker east, Sparrow into the office with the player), RECOVER THE LEDGERS (hold E
    /// 1.2 s, INTEL 1/3, D050, D051 +1.5 s — S8_Heli loads on it), optional O1 (D052), D049 when the stair wave spawns;
    /// the screening-room door and B7.T.Landing open once the ledgers are taken and the stair wave is dead; SEARCH
    /// UPSTAIRS. Exit: the landing → Beat 8.
    /// </summary>
    public sealed class Beat07RelayHall : BeatScript
    {
        /// <summary>Breach slow motion (GDD §7.7).</summary>
        public const float BreachSlowScale = 0.35f;
        /// <summary>Real seconds of the breach slow motion.</summary>
        public const float BreachSlowSeconds = 1.5f;
        /// <summary>D051 after D050 (DIALOGUE D051 "D050 finished + 1.5 s").</summary>
        public const float D051Gap = 1.5f;

        /// <inheritdoc/>
        public override int Number => 7;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            SaveStartCheckpoint(ctx); // CP7 at the door stack (338, 0, 327)
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E7);
            if (entryCheckpoint == null)
            {
                ctx.Raise(MissionEvents.HallBreach);
                ctx.Run(ctx.SlowMo(BreachSlowScale, BreachSlowSeconds, "Hall breach"), "B7 slow-mo");
            }
            ctx.Objective(ObjectiveTable.B7Breach);
            ctx.SpawnIntel(IntelTable.Ledgers);
            ctx.SpawnIntel(IntelTable.Schedule);
            ctx.Objective(ObjectiveTable.B7O1);
            ctx.Run(Corners(ctx), "B7 corners");
            ctx.Run(SparrowToOffice(ctx), "B7 Sparrow");
            ctx.Run(StairWaveLine(ctx), "B7 D049");

            yield return ctx.Killed(EncounterIds.E7BreachTeam);
            ctx.Complete(ObjectiveTable.B7Breach);

            ctx.Objective(ObjectiveTable.B7Ledgers);
            yield return ctx.Intel(IntelTable.Ledgers); // the director plays D050 and shows the intel card
            ctx.RunDetached(ctx.SayAfter(LineIds.D050, D051Gap, LineIds.D051), "B7 D051");

            // Gate (GDD §7.13): INTEL 1/3 and the stair wave dead.
            yield return Wait.Any(ctx.Killed("E7-04", "E7-05"), ctx.Cleared(EncounterIds.E7));
            ctx.Open(GateTable.ScreeningDoor);
            ctx.Open(GateTable.ArmLanding);
            ctx.Objective(ObjectiveTable.B7Upstairs);
            yield return ctx.TriggerFromNow(MissionTriggers.B7Landing);
            ctx.Complete(ObjectiveTable.B7Upstairs);
        }

        private static IEnumerator Corners(MissionContext ctx)
        {
            ctx.Run(ctx.SquadAction(SquadIds.Tinker, ActionIds.HandSignal, MissionMarkers.B7TinkerA), "B7 Tinker A");
            yield return ctx.SquadAction(SquadIds.Hollis, ActionIds.HandSignal, MissionMarkers.B7HollisA);
            yield return ctx.Killed(EncounterIds.E7BreachTeam);
            ctx.SquadActionNoWait(SquadIds.Hollis, MissionActionIds.Kneel, MissionMarkers.B7HollisB);
            ctx.SquadActionNoWait(SquadIds.Tinker, MissionActionIds.Kneel, MissionMarkers.B7TinkerB);
        }

        private static IEnumerator SparrowToOffice(MissionContext ctx)
        {
            yield return ctx.Trigger(MissionTriggers.B7OfficeDoor);
            ctx.SquadActionNoWait(SquadIds.Sparrow, MissionActionIds.Kneel, MissionMarkers.B7SparrowOffice);
        }

        private static IEnumerator StairWaveLine(MissionContext ctx)
        {
            yield return ctx.WaveSpawnedWait(EncounterIds.E7, EncounterIds.E7Stair);
            if (!ctx.IsCleared(EncounterIds.E7)) ctx.Line(LineIds.D049);
        }
    }
}
