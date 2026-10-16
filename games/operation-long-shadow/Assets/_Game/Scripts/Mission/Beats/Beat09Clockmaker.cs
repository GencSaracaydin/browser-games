using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 9 THE CLOCKMAKER (GDD §7.9): squad at B9.NodeSet.Defend, D059, Sparrow's drill (D060: E9 activates, its wave
    /// timers count from here); DEFEND CASTELLANOS: a ring on the safe, 90 s of active drilling that pause while an enemy
    /// is within 5 m of Sparrow; D061 wave 2, D062 first wave-2 Greycoat at the east stair foot, D063 wave 3 through the
    /// hatch; D064 at 100 % with the codebook (INTEL 3/3, automatic); D065 makes the racks vulnerable; DESTROY THE
    /// TRANSMITTER n/4 (red markers); the 4th rack → D066 (the broadcast cuts; WP06), the tunnel door opens, D067 +1 s;
    /// EXFIL: THE PITCH; a 0.5 s fade at the top of the ladder. The basement broadcast itself is WP06's NumbersBroadcast.
    /// Exit: the postern (B10.T.Postern) → Beat 10.
    /// </summary>
    public sealed class Beat09Clockmaker : BeatScript
    {
        /// <summary>Seconds of active drilling (GDD §7.9).</summary>
        public const float DrillSeconds = 90f;
        /// <summary>Drilling pauses while an enemy is within this distance of Sparrow.</summary>
        public const float DrillPauseRadius = 5f;
        /// <summary>D067 after D066 (DIALOGUE D067 "D066 finished + 1.0 s").</summary>
        public const float D067Gap = 1f;
        /// <summary>D062: a wave-2 Greycoat within this distance of the east stair foot.</summary>
        public const float EastStairRadius = 3f;
        /// <summary>Half of the 0.5 s fade at the ladder top (GDD §7.9).</summary>
        public const float FadeHalf = 0.25f;

        private static readonly string[] s_racks = { MissionMarkers.B9R1, MissionMarkers.B9R2, MissionMarkers.B9R3, MissionMarkers.B9R4 };

        /// <summary>Drill progress 0..1 (diagnostics).</summary>
        public float DrillProgress { get; private set; }

        /// <inheritdoc/>
        public override int Number => 9;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            DrillProgress = 0f;
            SaveStartCheckpoint(ctx); // CP9 at the west stair foot (320.5, -4.2, 343)
            ctx.Close(GateTable.Racks); // racks take hits but no damage until D065
            ctx.Nodes(MissionMarkers.B9NodeSetDefend);
            yield return WaitForSection(ctx);

            // D059 when the squad reaches the basement nodes; Sparrow sets the drill; D060 drill start.
            yield return ctx.SquadAt(MissionMarkers.B9NodeSetDefend);
            yield return ctx.Say(LineIds.D059);
            ctx.SquadActionNoWait(SquadIds.Sparrow, ActionIds.DrillLoop, MissionMarkers.B9Safe);
            ctx.Line(LineIds.D060);
            ctx.Raise(MissionEvents.DrillStarted);
            ctx.Activate(EncounterIds.E9);
            ctx.SfxAt(MissionCues.DrillStartStop, MissionMarkers.B9Safe);
            var drillLoop = ctx.Audio?.PlayLoop(MissionCues.DrillLoop, ctx.MarkerOrAnchor(MissionMarkers.B9Safe)) ?? 0;
            ctx.OnBeatEnd(() =>
            {
                if (drillLoop != 0) ctx.Audio?.StopLoop(drillLoop, 0.3f);
                ctx.Hud?.ClearRing(ObjectiveTable.B9Defend);
            });
            ctx.Run(WaveLines(ctx), "B9 wave lines");

            // 1. DEFEND CASTELLANOS: 90 s of active drilling.
            ctx.Objective(ObjectiveTable.B9Defend);
            var safe = ctx.MarkerOrAnchor(MissionMarkers.B9Safe);
            var last = ctx.Now;
            var shownPercent = -1;
            string label = null;
            yield return new Wait(WaitKind.Until, "drill 100 %", () =>
            {
                var dt = ctx.Now - last;
                last = ctx.Now;
                if (!EnemyNearSparrow(ctx)) DrillProgress = Mathf.Min(1f, DrillProgress + dt / DrillSeconds);
                var percent = Mathf.FloorToInt(DrillProgress * 100f);
                if (percent != shownPercent)
                {
                    shownPercent = percent; // the label string is rebuilt once per percent, not per frame (R2)
                    label = percent + " %";
                }
                ctx.Hud?.SetRing(ObjectiveTable.B9Defend, safe, DrillProgress, label);
                return DrillProgress >= 1f;
            });
            ctx.Hud?.ClearRing(ObjectiveTable.B9Defend);
            if (drillLoop != 0) ctx.Audio?.StopLoop(drillLoop, 0.3f);
            drillLoop = 0;
            ctx.SfxAt(MissionCues.DrillStartStop, MissionMarkers.B9Safe);
            ctx.SquadActionNoWait(SquadIds.Sparrow, MissionActionIds.Stop, null);
            ctx.SetFlag(MissionFlags.B9Drilled, true);

            // D064; 2. the codebook, automatically (INTEL 3/3); D065 releases the racks.
            ctx.Line(LineIds.D064);
            ctx.Complete(ObjectiveTable.B9Defend);
            ctx.CollectIntel(IntelTable.Codebook);
            yield return ctx.LineDone(LineIds.D064);
            ctx.Line(LineIds.D065);
            ctx.Open(GateTable.Racks);

            // 3. DESTROY THE TRANSMITTER 0/4.
            ctx.Objective(ObjectiveTable.B9Transmitter);
            ctx.Run(RackCounter(ctx), "B9 rack counter");
            yield return Wait.All(ctx.Destroyed(s_racks[0]), ctx.Destroyed(s_racks[1]), ctx.Destroyed(s_racks[2]), ctx.Destroyed(s_racks[3]));
            ctx.Complete(ObjectiveTable.B9Transmitter);
            ctx.Line(LineIds.D066); // the same request as WP06's NumbersBroadcast cut (merged)
            ctx.Raise(MissionEvents.TransmitterDead);
            ctx.Open(GateTable.TunnelDoor);
            ctx.RunDetached(ctx.SayAfter(LineIds.D066, D067Gap, LineIds.D067), "B9 D067");

            // 4. EXFIL: THE PITCH.
            ctx.Objective(ObjectiveTable.B9Exfil);
            yield return Wait.Any(ctx.Trigger(MissionTriggers.B9Tunnel), ctx.Trigger(MissionTriggers.B10Postern));
            ctx.Complete(ObjectiveTable.B9Exfil);
            yield return ctx.Trigger(MissionTriggers.B10Postern);
            ctx.ScreenFX?.FadeToBlack(FadeHalf); // hides the S6 unload / S7 activation; Beat 10 fades back in
            yield return ctx.RealSeconds(FadeHalf);
        }

        private static bool EnemyNearSparrow(MissionContext ctx)
        {
            var reg = ctx.Targets;
            if (reg == null) return false;
            var s = ctx.Member(SquadIds.Sparrow);
            Vector3 p;
            if (s != null) p = s.position;
            else if (!ctx.TryPosition(MissionMarkers.B9Safe, out p)) return false;
            return reg.CountWithin(Team.Legion, p, DrillPauseRadius) + reg.CountWithin(Team.Greycoat, p, DrillPauseRadius) > 0;
        }

        private static IEnumerator RackCounter(MissionContext ctx)
        {
            var shown = -1;
            var spec = ObjectiveTable.Get(ObjectiveTable.B9Transmitter);
            yield return Wait.Until(() =>
            {
                var n = 0;
                for (var i = 0; i < s_racks.Length; i++)
                {
                    if (!ctx.IsDestroyed(s_racks[i])) continue;
                    n++;
                    if (ctx.Objectives.HasMarker(spec.MarkerId(i))) ctx.Objectives.HideTarget(spec, i);
                }
                if (n != shown)
                {
                    shown = n;
                    Count(ctx, ObjectiveTable.B9Transmitter, n, s_racks.Length);
                }
                return n >= s_racks.Length;
            }, "racks");
        }

        private static IEnumerator WaveLines(MissionContext ctx)
        {
            ctx.Run(LineOnWave(ctx, EncounterIds.E9W2, LineIds.D061), "B9 D061");
            ctx.Run(LineOnWave(ctx, EncounterIds.E9W3, LineIds.D063), "B9 D063");
            yield return ctx.WaveSpawnedWait(EncounterIds.E9, EncounterIds.E9W2);
            yield return Wait.Until(() => ctx.IsCleared(EncounterIds.E9) || Wave2AtEastStair(ctx), "wave 2 at the east stair foot");
            if (!ctx.IsCleared(EncounterIds.E9)) ctx.Line(LineIds.D062);
        }

        private static IEnumerator LineOnWave(MissionContext ctx, string wave, string line)
        {
            yield return ctx.WaveSpawnedWait(EncounterIds.E9, wave);
            if (!ctx.IsCleared(EncounterIds.E9)) ctx.Line(line);
        }

        private static bool Wave2AtEastStair(MissionContext ctx)
        {
            if (!ctx.TryPosition(MissionMarkers.B9EastStairFoot, out var foot)) return false;
            var ids = EncounterIds.E9Wave2;
            for (var i = 0; i < ids.Length; i++)
            {
                var root = ctx.Actor(ids[i])?.Root;
                if (root != null && (root.position - foot).sqrMagnitude <= EastStairRadius * EastStairRadius) return true;
            }
            return false;
        }
    }
}
