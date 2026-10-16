using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using UnityEngine;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 5 LAST LIGHT (GDD §7.5) and the owner of MID-A "LAST LIGHT" end to end (GDD §8.5; TECH_PLAN §7.11 "MID-A
    /// owner"): the roof traversal R1 → R2 → R3 under the marksman (D035 on his first glint, D036 on his first shot), the
    /// sun held at 1.5° (LightStage driver capped at 1.80), then MID-A when the player is inside R4 and the R3 nest guards
    /// are dead (after 20 s in R4 with them alive Hollis and Tinker kill them). MID-A takes <c>ILightStage.Override</c>,
    /// keys s (1.80 → 1.80 at 0:04 → 1.97 at 0:14 → 2.00 at 0:20), raises MIDA.Start (the marksman ceases fire, S7_Pitch
    /// loads), forces a 1.2 m/s walk, mirrors the player's look west on the squad, plays the watch check, makes sure the
    /// sodium practicals strike, runs the interval signal from Tinker's PRC-77, the D037-D042 line sequence with the
    /// positional-to-radio jump on "Seven", the per-digit vision pulses and I2 on "Six", then releases everything, opens the
    /// R4 stair and shows BREACH POINT. Exit: the alley (B6.T.Alley) → Beat 6.
    /// </summary>
    public sealed class Beat05LastLight : BeatScript
    {
        /// <summary>Forced walk speed during MID-A (GDD §8.5).</summary>
        public const float ForcedWalkSpeed = 1.2f;
        /// <summary>Seconds in R4 with the nest guards alive before the squad kills them (GDD §7.5).</summary>
        public const float NestKillAfter = 20f;
        /// <summary>Fade of MUS_03 to silence under the numbers (GDD §8.5 row 6).</summary>
        public const float MusicUnderNumbersFade = 4f;
        /// <summary>The squad moves to R2 when the player has crossed this x (the R1 → R2 plank at x 246-248).</summary>
        public const float R2AtX = 246f;
        /// <summary>The squad moves to R3 (nest clear) when the player has crossed this x (tank stand at 260).</summary>
        public const float R3AtX = 258f;

        private static readonly object s_midAOwner = new object();

        /// <summary>Mission time MID-A started (-1 before), for diagnostics and the timing test.</summary>
        public float MidAStartedAt { get; private set; } = -1f;

        /// <inheritdoc/>
        public override int Number => 5;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            MidAStartedAt = -1f;
            SaveStartCheckpoint(ctx); // CP5 at the ladder top (236, 9, 304)
            ctx.Nodes(MissionMarkers.B5NodeSetR1);
            ctx.LightRange(1.50f, MidATimeline.SStart); // sun held at 1.5° through the traversal (GDD §9.2a rows 1.50-1.80)
            yield return WaitForSection(ctx);
            ctx.Activate(EncounterIds.E5);
            ctx.Objective(ObjectiveTable.B5Wall);
            ctx.Run(MarksmanLines(ctx), "B5 marksman lines");
            ctx.Run(SquadAcrossRoofs(ctx), "B5 squad");
            ctx.Run(NestRule(ctx), "B5 nest rule");

            // MID-A trigger: inside R4 (x 277-292, z 312-332, y 11-15) and E5-02/03 dead.
            yield return new Wait(WaitKind.Trigger, MissionTriggers.B5R4,
                () => ctx.Inside(MissionTriggers.B5R4) && NestDead(ctx));
            yield return MidA(ctx);

            ctx.Objective(ObjectiveTable.B5Breach);
            yield return ctx.Trigger(MissionTriggers.B6Alley);
            ctx.Complete(ObjectiveTable.B5Breach);
        }

        private static bool NestDead(MissionContext ctx) =>
            ctx.IsKilled(EncounterIds.NestGuardA) && ctx.IsKilled(EncounterIds.NestGuardB);

        private static IEnumerator MarksmanLines(MissionContext ctx)
        {
            yield return ctx.Event(MissionEvents.MarksmanFirstGlint);
            if (!ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.MidAStart)) ctx.Line(LineIds.D035);
            yield return ctx.Event(MissionEvents.MarksmanFirstShot);
            if (!ctx.HappenedThisBeat(EventChannel.Mission, MissionEvents.MidAStart)) ctx.Line(LineIds.D036);
        }

        private static IEnumerator SquadAcrossRoofs(MissionContext ctx)
        {
            yield return Wait.Until(() => ctx.PlayerPosition.x >= R2AtX || ctx.Inside(MissionTriggers.B5R4), "player on R2");
            ctx.Nodes(MissionMarkers.B5NodeSetR2);
            yield return Wait.Until(() => NestDead(ctx) && (ctx.PlayerPosition.x >= R3AtX || ctx.Inside(MissionTriggers.B5R4)), "nest clear");
            ctx.Nodes(MissionMarkers.B5NodeSetR3);
            yield return ctx.Trigger(MissionTriggers.B5R4);
            ctx.Nodes(MissionMarkers.B5NodeSetMidA);
        }

        private static IEnumerator NestRule(MissionContext ctx)
        {
            // GDD §7.5: 20 s inside R4 with E5-02/03 alive → they become squad targets and Hollis and Tinker kill them (≤ 4 s).
            var inside = 0f;
            var last = ctx.Now;
            yield return Wait.Until(() =>
            {
                var dt = ctx.Now - last;
                last = ctx.Now;
                if (NestDead(ctx)) return true;
                inside = ctx.Inside(MissionTriggers.B5R4) ? inside + dt : inside;
                return inside >= NestKillAfter;
            }, "20 s in R4");
            if (NestDead(ctx)) yield break;
            ctx.ScriptedKill(SquadIds.Hollis, EncounterIds.NestGuardA);
            ctx.ScriptedKill(SquadIds.Tinker, EncounterIds.NestGuardB);
        }

        // ------------------------------------------------------------------ MID-A (GDD §8.5)

        private IEnumerator MidA(MissionContext ctx)
        {
            var t0 = ctx.Now;
            MidAStartedAt = t0;
            var rig = ctx.Rig;
            // 0:00 — MIDA.Start (marksman ceases fire; S7_Pitch quiet load), the image, Hollis's fist.
            ctx.Raise(MissionEvents.MidAStart);
            ctx.Complete(ObjectiveTable.B5Wall);
            ctx.LightOverride(s_midAOwner, true);
            ctx.LightKey(MidATimeline.SStart);
            rig?.SetForcedWalk(ForcedWalkSpeed);
            ctx.OnBeatEnd(() => ctx.Rig?.SetForcedWalk(null));
            ctx.OnBeatEnd(() => ctx.Squad?.SetLookTarget(null, 0f));
            ctx.Nodes(MissionMarkers.B5NodeSetMidA);
            ctx.SquadActionNoWait(SquadIds.Hollis, ActionIds.FistHold, null);

            ctx.Run(KeyLight(ctx, t0), "MID-A light");
            ctx.Run(LookWest(ctx, t0), "MID-A look west");
            ctx.Run(AtThen(ctx, t0 + MidATimeline.MusicSwell, () => ctx.PlayMusic(MissionCues.MusicLastLight, 0, 1f)), "MID-A music");
            ctx.Run(AtThen(ctx, t0 + MidATimeline.WatchCheck, () => ctx.FirstPersonBody?.PlayWatchCheck(MidATimeline.WatchCheckSeconds, null)),
                "MID-A watch");
            ctx.Run(AtThen(ctx, t0 + MidATimeline.StrikeFallback, () =>
            {
                // LightStage raises PracticalsStrike when s crosses 1.97 (WP10); without a LightStage the beat does.
                if (!ctx.Log.Happened(EventChannel.Mission, MissionEventLog.PracticalsStrikeId, ctx.BeatSeq)) GameEvents.RaisePracticalsStrike();
            }), "MID-A strike");

            // 0:20 — sun at 0°: D037 with the interval signal starting faintly from Tinker's PRC-77 (G9).
            yield return ctx.At(t0 + MidATimeline.D037At);
            ctx.Line(LineIds.D037);
            var tinker = ctx.Member(SquadIds.Tinker);
            ctx.Audio?.PlayAttached(MissionCues.IntervalSignal, tinker, MidATimeline.IntervalVolume);

            // ≈ 0:23 — D038 (after D037).
            yield return Wait.All(ctx.At(t0 + MidATimeline.D038At), ctx.LineDone(LineIds.D037));
            ctx.Line(LineIds.D038);

            // ≈ 0:27 — D039 positional from Tinker's pack, jumping to 2D radio at the first "Seven" (≈ 0:28.8).
            yield return ctx.At(t0 + MidATimeline.D039At);
            ctx.Line(LineIds.D039, new VOOptions
            {
                Speaker = tinker,
                ForceDry = true,
                RadioSwitchTime = MidATimeline.TokenOnset(MidATimeline.SevenToken),
            });
            var d039 = ctx.Now;
            ctx.Music?.Stop(MusicUnderNumbersFade); // "Music to silence under the numbers"
            ctx.Run(DigitPulses(ctx, d039), "MID-A digit pulses");
            // ≈ 0:38.7 — I2 on "Six".
            yield return ctx.At(d039 + MidATimeline.TokenOnset(MidATimeline.SixToken));
            ctx.Insert(SequenceIds.InsertI2);
            // ≈ 0:39.6 — D040 on the "Four" after "Six".
            yield return ctx.At(d039 + MidATimeline.TokenOnset(MidATimeline.FourToken));
            ctx.Line(LineIds.D040);
            // D041 when D040 finished; D042 starts on "Calder?" inside D041 and overlaps it.
            yield return ctx.LineDone(LineIds.D040);
            ctx.Line(LineIds.D041);
            yield return Wait.Until(() => ctx.Lines.HasStarted(LineIds.D041), "D041 started");
            var d041 = Mathf.Max(0f, ctx.Lines.StartTime(LineIds.D041));
            yield return ctx.At(d041 + MidATimeline.CalderOffsetInD041);
            ctx.Line(LineIds.D042);
            // ≈ 0:46 — Hollis switches frequency (hand to the handset) and the numbers cut.
            yield return ctx.LineDone(LineIds.D041);
            ctx.Lines.Stop(LineIds.D039);
            ctx.SquadActionNoWait(SquadIds.Hollis, ActionIds.HandsetToEar, null);
            // ≈ 0:47 — D042 ends: full speed returns, the R4 stair opens, BREACH POINT in pale green.
            yield return ctx.LineDone(LineIds.D042);

            rig = ctx.Rig;
            rig?.SetForcedWalk(null);
            ctx.Squad?.SetLookTarget(null, 0f);
            ctx.LightKey(MidATimeline.SEnd);
            ctx.LightOverride(s_midAOwner, false);
            ctx.Open(GateTable.R4Stair);
            ctx.SetFlag(MissionFlags.B5MidADone, true);
            ctx.Raise(MissionEvents.MidAEnd);
        }

        private static IEnumerator KeyLight(MissionContext ctx, float t0)
        {
            yield return Wait.Until(() =>
            {
                var t = ctx.Now - t0;
                ctx.LightKey(MidATimeline.SAt(t));
                return t >= MidATimeline.SunDownTime;
            }, "MID-A s keys");
        }

        private static IEnumerator LookWest(MissionContext ctx, float t0)
        {
            var sun = ctx.MarkerOrAnchor(MissionMarkers.B5SunLook);
            var looking = false;
            yield return Wait.Until(() =>
            {
                var rig = ctx.Rig;
                var west = false;
                if (rig != null)
                {
                    var bearing = MathX.BearingOf(rig.ViewRay.direction);
                    west = Mathf.Abs(MathX.WrapAngle180(bearing - MidATimeline.SunAzimuth)) <= MidATimeline.LookWestHalfAngle;
                }
                if (west != looking && sun != null)
                {
                    looking = west;
                    ctx.Squad?.SetLookTarget(west ? sun : null, west ? 1f : 0f);
                }
                return ctx.Now - t0 >= MidATimeline.LookWestUntil;
            }, "MID-A look west");
            if (looking) ctx.Squad?.SetLookTarget(null, 0f);
        }

        private static IEnumerator DigitPulses(MissionContext ctx, float d039Start)
        {
            for (var i = MidATimeline.FirstDigitToken; i < MidATimeline.TokenCount; i++)
            {
                yield return ctx.At(d039Start + MidATimeline.TokenOnset(i));
                ctx.ScreenFX?.PulseSuppression(MidATimeline.DigitPulse);
            }
        }

        private static IEnumerator AtThen(MissionContext ctx, float time, System.Action action)
        {
            yield return ctx.At(time);
            action?.Invoke();
        }
    }
}
