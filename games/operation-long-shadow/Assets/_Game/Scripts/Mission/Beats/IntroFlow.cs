using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 0: click gate → "SESSION 12" booth shots 1-8 → the INSERTION cold open → shot 9 (GDD §8.1-8.2,
    /// CINEMATIC_DIRECTION §2). TL_Intro (WP09) plays everything, holds at shot 8 until S1_Approach and S9_TownFar are
    /// loaded, and raises <see cref="SequenceIds.IntroControlToPlayer"/> when the white-out of shot 9 settles; Beat 1
    /// starts there. The player is placed prone at CP1 before the timeline starts so shot 9 lands on the gameplay view.
    /// Without a cinematic service the Mission fallback plays the intro lines on the GDD timings over black.
    /// </summary>
    public sealed class IntroFlow : BeatScript
    {
        /// <summary>Prone eye height on the ridge (GDD §7.1 "Prone at (42, 22.0, 30)").</summary>
        public const float ProneEyeHeight = 0.35f;

        /// <inheritdoc/>
        public override int Number => BeatTable.Intro;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            // Place the player prone at CP1 once S1_Approach is in (D20: it may still be streaming behind the intro), so the
            // rig never stands over an empty tile and shot 9 lands on the gameplay view.
            ctx.Run(PlaceOnRidge(ctx), "Intro place player");
            ctx.SetPosture(ProneEyeHeight, 0f);
            ctx.Rig?.SetBinocularsAvailable(false);
            ctx.LightImmediate(0f); // the INSERTION plays at the Beat 1 light (CINEMATIC_DIRECTION §2)
            var intro = ctx.Run(ctx.Cinematic(SequenceIds.Intro, 300f, () => Fallback(ctx)), "TL_Intro");
            yield return Wait.Any(ctx.Event(SequenceIds.IntroControlToPlayer), Wait.Until(() => intro.IsDone, "TL_Intro ended"));
            ctx.Director.MarkIntroSeen();
        }

        private static IEnumerator PlaceOnRidge(MissionContext ctx)
        {
            yield return ctx.SectionReady(SectionId.Approach);
            ctx.Director.TeleportToCheckpoint(SequenceIds.CP1);
        }

        private static IEnumerator Fallback(MissionContext ctx)
        {
            ctx.ScreenFX?.FadeToBlack(0f);
            ctx.PlayMusic(MissionCues.MusicSession, 0, 0.5f);
            ctx.Run(PlaySchedule(ctx, SequenceTimelines.Intro), "Intro lines");
            yield return ctx.Seconds(SequenceTimelines.IntroControl);
            ctx.ScreenFX?.Flash(6f, 2f); // shot 9: +6 EV falling to 0 over 2 s
            ctx.ScreenFX?.FadeIn(0.5f);
        }
    }
}
