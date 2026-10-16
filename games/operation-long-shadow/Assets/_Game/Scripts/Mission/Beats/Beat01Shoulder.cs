using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 1 THE SHOULDER (GDD §7.1): prone on the ridge, the 8 s title card with look-only input, a scripted kneel,
    /// binocular glassing of POI1-3 (ticks within 4° for 1.0 s; after 40 s the rest tick silently and D011 plays), FOLLOW
    /// HOLLISTER down the wadi with the slide/mantle/crouch prompts, Harrou's PA at the wadi midpoint (D012, then D013 +1.5 s),
    /// REACH THE GROVE. Exit: the grove volume (B1.T.GroveEnter) → Beat 2.
    /// </summary>
    public sealed class Beat01Shoulder : BeatScript
    {
        /// <summary>Prone eye height (GDD §7.1).</summary>
        public const float ProneEyeHeight = IntroFlow.ProneEyeHeight;
        /// <summary>Kneeling eye height after the title card (GDD §7.1 "a scripted kneel").</summary>
        public const float KneelEyeHeight = 1.05f;
        /// <summary>Blend of the kneel.</summary>
        public const float KneelSeconds = 0.8f;
        /// <summary>Observe timeout (GDD §7.1: "If 40 s pass first, the remaining POIs tick silently and D011 plays").</summary>
        public const float ObserveTimeout = 40f;
        /// <summary>D013 comes this long after the PA line (DIALOGUE D013 "PA line finished + 1.5 s").</summary>
        public const float D013Gap = 1.5f;

        private static readonly string[] s_pois = { MissionMarkers.B1POI1, MissionMarkers.B1POI2, MissionMarkers.B1POI3 };
        private static readonly string[] s_poiLines = { LineIds.D008, LineIds.D009, LineIds.D010 };

        /// <inheritdoc/>
        public override int Number => 1;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            // Enter: the white-out of intro shot 9 ends; CP1 saved at spawn (GDD §7.1). The ridge must be loaded first (D20).
            yield return WaitForSection(ctx);
            ctx.Director.TeleportToCheckpoint(SequenceIds.CP1); // prone at (42, 22, 30), heading 45° (also after the intro)
            SaveStartCheckpoint(ctx);
            ctx.Rig?.SetBinocularsAvailable(true);
            ctx.OnBeatEnd(() => ctx.Rig?.SetBinocularsAvailable(false)); // binoculars: this beat only
            ctx.Raise(MissionEvents.PASiren); // the curfew siren winds down from the three PA horns over the first 15 s (WP06)
            ctx.Nodes(MissionMarkers.B1SquadStart);

            if (entryCheckpoint == null)
            {
                // Title card (GDD §8.3): look-only for 8 s, then the bars retract, the scripted kneel, control returns.
                var lockHandle = ctx.Lock(InputLockFlags.Move | InputLockFlags.Fire | InputLockFlags.Weapons | InputLockFlags.Jump |
                                          InputLockFlags.Interact, "Title card");
                ctx.SetPosture(ProneEyeHeight, 0f);
                yield return ctx.Cinematic(SequenceIds.TitleCard, SequenceTimelines.TitleCardSeconds + 4f, () => TitleCardFallback(ctx));
                ctx.SetPosture(KneelEyeHeight, KneelSeconds);
                yield return ctx.Seconds(KneelSeconds);
                ctx.Unlock(lockHandle);
                ctx.Run(StandWhenMoving(ctx), "B1 stand up");
            }
            else
            {
                ctx.SetPosture(null, 0f);
            }

            // D007 on control; 1. OBSERVE ZAROUD 0/3 with hollow diamonds on the POIs.
            ctx.Line(LineIds.D007);
            ctx.Tutorial(Tutorials.BinocularsKey, Tutorials.BinocularsText);
            ctx.Objective(ObjectiveTable.B1Observe);
            yield return Observe(ctx);

            // 2. FOLLOW HOLLISTER from D011 until the wadi midpoint; the squad files down the wadi.
            ctx.Line(LineIds.D011);
            ctx.Objective(ObjectiveTable.B1Follow);
            ctx.Tutorial(Tutorials.ObjectivesKey, Tutorials.ObjectivesText);
            ctx.Follow();
            ctx.Run(TutorialAt(ctx, MissionTriggers.B1Scree, Tutorials.SlideKey, Tutorials.SlideText), "B1 slide prompt");
            ctx.Run(TutorialAt(ctx, MissionTriggers.B1Boulder, Tutorials.MantleKey, Tutorials.MantleText), "B1 mantle prompt");
            ctx.Run(TutorialAt(ctx, MissionTriggers.B1GroveEdge, Tutorials.CrouchKey, Tutorials.CrouchText), "B1 crouch prompt");
            yield return Wait.Any(ctx.Trigger(MissionTriggers.B1WadiMid), ctx.Trigger(MissionTriggers.B1GroveEdge),
                ctx.Trigger(MissionTriggers.B1GroveEnter));
            ctx.Complete(ObjectiveTable.B1Follow);

            // D012 PA from the three horns, D013 1.5 s after it; 3. REACH THE GROVE.
            ctx.Line(LineIds.D012);
            ctx.RunDetached(ctx.SayAfter(LineIds.D012, D013Gap, LineIds.D013), "B1 D013");
            ctx.Objective(ObjectiveTable.B1Grove);
            yield return ctx.Trigger(MissionTriggers.B1GroveEnter);
            ctx.Complete(ObjectiveTable.B1Grove);
        }

        private static IEnumerator Observe(MissionContext ctx)
        {
            var tracker = new GlassingTracker(s_pois);
            var silent = false;
            tracker.Glassed += id =>
            {
                var i = System.Array.IndexOf(s_pois, id);
                ctx.Objectives.HideTarget(ObjectiveTable.Get(ObjectiveTable.B1Observe), i);
                Count(ctx, ObjectiveTable.B1Observe, tracker.Count, s_pois.Length);
                if (!silent && i >= 0) ctx.Line(s_poiLines[i]);
            };
            var start = ctx.Now;
            var last = ctx.Now;
            yield return new Wait(WaitKind.Glassed, ObjectiveTable.B1Observe, () =>
            {
                tracker.Tick(ctx.Now - last, ctx.Log);
                last = ctx.Now;
                return tracker.Count >= s_pois.Length || ctx.Now - start >= ObserveTimeout;
            });
            silent = true;
            for (var i = 0; i < s_pois.Length; i++) tracker.Mark(s_pois[i]);
            ctx.Complete(ObjectiveTable.B1Observe);
        }

        private static IEnumerator TitleCardFallback(MissionContext ctx)
        {
            // No WP09 Timeline: WP08's HUD draws the card (8 s) and raises UI.TitleCardFinished when its bars retract.
            ctx.Hud?.TitleCard(SequenceTimelines.TitleCardSeconds);
            ctx.Music?.Stinger(MissionCues.StingerTitle);
            yield return Wait.Any(ctx.Event(MissionEvents.TitleCardFinished),
                ctx.Hud != null ? ctx.Seconds(SequenceTimelines.TitleCardSeconds + 1f) : ctx.Seconds(SequenceTimelines.TitleCardSeconds));
        }

        private static IEnumerator TutorialAt(MissionContext ctx, string triggerId, string key, string text)
        {
            yield return ctx.Trigger(triggerId);
            ctx.Tutorial(key, text);
        }

        private static IEnumerator StandWhenMoving(MissionContext ctx)
        {
            var end = ctx.Now + 6f;
            yield return Wait.Until(() => ctx.Now >= end || (ctx.Rig != null && ctx.Rig.Velocity.sqrMagnitude > 0.25f), "player moves");
            ctx.SetPosture(null, 0.4f);
        }
    }
}
