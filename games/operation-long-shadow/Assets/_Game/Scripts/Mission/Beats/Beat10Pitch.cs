using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 10 THE PITCH (GDD §7.10): out of the postern (the fade from Beat 9 lifts), D068 → D069 → D070, GET TO THE LZ;
    /// the hold starts at the end of D070 wherever the player is: E10 activates, Hollis pops green smoke, DEFEND THE LZ
    /// shows Magpie's distance (2,000 m → 0 over 75 s; 900 m and 20 s more after a wave-off). Hold timeline
    /// (<see cref="PitchTimeline"/>): D071 truck 1 (t 8), FL1 + D072 + BK_GRY_09 (t 12), D073 + KILL THE RPG GUNNER when he
    /// spawns (t 40), FL2 (t 42), D074 Magpie's final approach (t 60), the wave-off if the RPG gunner is alive at t 63
    /// (D075, FL3 at t 72, the gunner a squad target and Hollis's kill 8 s later), wheels down at t 75 (or 95) with D076,
    /// then [E] Board. Exit: boarding → Beat 11.
    /// </summary>
    public sealed class Beat10Pitch : BeatScript
    {
        private float _holdStart = -1f;
        private float _waveOffAt = -1f;
        private float _holdT;

        /// <summary>Hold time in seconds (−1 before the hold).</summary>
        public float HoldTime => _holdStart < 0f ? -1f : _holdT;

        /// <summary>True once Magpie waved off.</summary>
        public bool WavedOff => _waveOffAt >= 0f;

        /// <inheritdoc/>
        public override int Number => 10;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            _holdStart = -1f;
            _waveOffAt = -1f;
            _holdT = 0f;
            SaveStartCheckpoint(ctx); // CP10 at the postern (346, 0, 383)
            ctx.ScreenFX?.FadeIn(Beat09Clockmaker.FadeHalf);
            ctx.Nodes(MissionMarkers.B10NodeSetP1);
            yield return WaitForSection(ctx);

            // D068 (player exits the postern) → D069 → D070; 1. GET TO THE LZ.
            ctx.Line(LineIds.D068);
            ctx.Objective(ObjectiveTable.B10LZ);
            ctx.Run(ReachLZ(ctx), "B10 LZ");
            yield return ctx.LineDone(LineIds.D068);
            yield return ctx.Say(LineIds.D069);
            yield return ctx.Say(LineIds.D070);

            // The hold starts at the end of D070.
            yield return Hold(ctx);

            // 4. BOARD THE HELICOPTER ([E] Board at the left door, enabled after D076).
            ctx.Open(GateTable.UseHeliDoor);
            ctx.Open(GateTable.HeliDoor);
            ctx.Objective(ObjectiveTable.B10Board);
            yield return ctx.Interacted(MissionMarkers.HeliDoor);
            ctx.Close(GateTable.UseHeliDoor);
            if (!ctx.IsComplete(ObjectiveTable.B10LZ)) ctx.Complete(ObjectiveTable.B10LZ);
            if (!ctx.IsComplete(ObjectiveTable.B10Rpg)) ctx.Complete(ObjectiveTable.B10Rpg);
            ctx.Complete(ObjectiveTable.B10Board);
        }

        private static IEnumerator ReachLZ(MissionContext ctx)
        {
            yield return ctx.Trigger(MissionTriggers.B10LZ);
            ctx.Complete(ObjectiveTable.B10LZ);
        }

        private IEnumerator Hold(MissionContext ctx)
        {
            _holdStart = ctx.Now;
            var t0 = _holdStart;
            ctx.Activate(EncounterIds.E10);
            ctx.Objective(ObjectiveTable.B10Defend);
            ctx.SquadActionNoWait(SquadIds.Hollis, ActionIds.PopSmokeGreen, MissionMarkers.B10GreenSmoke);
            var pad = ctx.MarkerOrAnchor(MissionMarkers.B10Pad);
            ctx.OnBeatEnd(() => ctx.Hud?.ClearRing(ObjectiveTable.B10Defend));

            ctx.Run(At(ctx, t0 + PitchTimeline.Truck1, () => ctx.Line(LineIds.D071)), "B10 D071");
            ctx.Run(At(ctx, t0 + PitchTimeline.Flare1, () =>
            {
                Flare(ctx, MissionMarkers.B10FL1);
                ctx.Line(LineIds.D072);
                ctx.RadioBark(LineIds.BarkGry09); // once, at FL1 (E10-04 is still in his closet)
            }), "B10 FL1");
            ctx.Run(RpgSpawn(ctx), "B10 D073");
            ctx.Run(At(ctx, t0 + PitchTimeline.Flare2, () => Flare(ctx, MissionMarkers.B10FL2)), "B10 FL2");
            ctx.Run(At(ctx, t0 + PitchTimeline.Approach - PitchTimeline.ApproachLead, () => Vehicle(ctx, MissionMarkers.B10HeliApproach)?.Play()),
                "B10 approach spline");
            ctx.Run(At(ctx, t0 + PitchTimeline.Approach, () =>
            {
                ctx.Line(LineIds.D074);
                ctx.Raise(MissionEvents.HeliApproach);
            }), "B10 D074");
            ctx.Run(WaveOff(ctx, t0), "B10 wave-off");

            // Wheels down at t 75, or t 95 after a wave-off; the ring shows Magpie's distance meanwhile (label rebuilt only
            // when the 5 m step changes: no per-frame string, R2).
            var shownMetres = -1;
            string label = null;
            yield return new Wait(WaitKind.Seconds, "wheels down", () =>
            {
                _holdT = ctx.Now - t0;
                var dist = PitchTimeline.MagpieDistance(_holdT, _waveOffAt);
                var metres = Mathf.RoundToInt(dist / 5f) * 5;
                if (metres != shownMetres)
                {
                    shownMetres = metres;
                    label = "MAGPIE " + metres + " m";
                }
                ctx.Hud?.SetRing(ObjectiveTable.B10Defend, pad, 1f - dist / PitchTimeline.StartDistance, label);
                var down = PitchTimeline.WheelsDown + (_waveOffAt >= 0f ? PitchTimeline.WaveOffExtra : 0f);
                return _holdT >= down;
            }, radius: PitchTimeline.WheelsDown);

            ctx.Hud?.ClearRing(ObjectiveTable.B10Defend);
            var approach = Vehicle(ctx, MissionMarkers.B10HeliApproach);
            if (approach != null && !approach.Arrived) approach.SetProgress(1f);
            ctx.Raise(MissionEvents.HeliWheelsDown);
            ctx.Complete(ObjectiveTable.B10Defend);
            ctx.Line(LineIds.D076);
            if (!ctx.IsKilled(EncounterIds.RpgGunner)) ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.RpgGunner);
        }

        private static IEnumerator RpgSpawn(MissionContext ctx)
        {
            yield return ctx.WaveSpawnedWait(EncounterIds.E10, EncounterIds.E10Truck2);
            if (ctx.IsKilled(EncounterIds.RpgGunner) && ctx.Encounters != null) yield break;
            ctx.Line(LineIds.D073);
            ctx.Objective(ObjectiveTable.B10Rpg);
            yield return ctx.Killed(EncounterIds.RpgGunner);
            ctx.Complete(ObjectiveTable.B10Rpg);
        }

        private IEnumerator WaveOff(MissionContext ctx, float t0)
        {
            // GDD §7.10: if the RPG gunner is alive at t 63 he fires at Magpie (WP04 raises B10.RPGAtMagpie) → wave-off.
            yield return Wait.Any(ctx.Event(MissionEvents.RpgFiredAtMagpie), ctx.At(t0 + PitchTimeline.RpgAtMagpie + PitchTimeline.RpgEventGrace));
            var fired = ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.RpgFiredAtMagpie);
            if (!fired && (ctx.Encounters == null || ctx.IsKilled(EncounterIds.RpgGunner) || ctx.Actor(EncounterIds.RpgGunner) == null)) yield break;
            _waveOffAt = ctx.Now - t0;
            ctx.SetFlag(MissionFlags.B10WaveOff, true);
            ctx.Line(LineIds.D075);
            ctx.Raise(MissionEvents.HeliWaveOff);
            Vehicle(ctx, MissionMarkers.B10HeliApproach)?.Stop();
            Vehicle(ctx, MissionMarkers.B10HeliWaveOff)?.Play();
            var rpg = ctx.Actor(EncounterIds.RpgGunner);
            if (rpg != null) rpg.SquadTarget = true;
            ctx.Run(At(ctx, t0 + PitchTimeline.Flare3, () => Flare(ctx, MissionMarkers.B10FL3)), "B10 FL3");
            ctx.Run(RejoinApproach(ctx, t0), "B10 rejoin");
            yield return ctx.Seconds(PitchTimeline.WaveOffKillDelay);
            if (!ctx.IsKilled(EncounterIds.RpgGunner)) ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.RpgGunner);
        }

        private IEnumerator RejoinApproach(MissionContext ctx, float t0)
        {
            // The wave-off spline (20 s) rejoins the approach at (600, 60, 420): the last 15 s of the approach to the pad.
            yield return ctx.At(t0 + PitchTimeline.WheelsDown + PitchTimeline.WaveOffExtra - PitchTimeline.ApproachLead);
            var approach = Vehicle(ctx, MissionMarkers.B10HeliApproach);
            if (approach == null) yield break;
            approach.SetProgress(0.5f);
            approach.Play();
        }

        private static void Flare(MissionContext ctx, string flareId)
        {
            ctx.Raise(flareId); // WP10 ParachuteFlare listens to "B10.FL1/FL2/FL3"
            ctx.SfxAt(MissionCues.Flare, flareId);
        }

        private static ISplineVehicle Vehicle(MissionContext ctx, string id)
        {
            var reg = ctx.Markers;
            if (reg == null || !reg.TryGet(id, out SceneMarker m) || m == null) return null;
            return m.GetComponent<ISplineVehicle>();
        }

        private static IEnumerator At(MissionContext ctx, float time, System.Action action)
        {
            yield return ctx.At(time);
            action?.Invoke();
        }
    }
}
