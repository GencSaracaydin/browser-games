using System.Collections;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;

namespace LongShadow.Mission.Beats
{
    /// <summary>
    /// Beat 12: the outro "SIX-FORTY" (GDD §8.8, TL_Outro 1:50, WP09) then the debrief dossier (GDD §8.9; the director
    /// shows it when this flow ends). Without a cinematic service the Mission fallback plays the outro lines on the GDD
    /// timings over black with MUS_06_SIXFORTY.
    /// </summary>
    public sealed class OutroFlow : BeatScript
    {
        /// <inheritdoc/>
        public override int Number => BeatTable.Outro;

        /// <inheritdoc/>
        public override IEnumerator Run(MissionContext ctx, string entryCheckpoint)
        {
            var w = ctx.Weapons;
            if (w != null && w.IsDoorGunMounted) w.DismountDoorGun();
            ctx.Rig?.SetDamageModifiers(1f, 0f);
            yield return ctx.Cinematic(SequenceIds.Outro, SequenceTimelines.OutroSeconds + 20f, () => Fallback(ctx));
        }

        private static IEnumerator Fallback(MissionContext ctx)
        {
            ctx.ScreenFX?.FadeToBlack(0f);
            ctx.PlayMusic(MissionCues.MusicSixForty, 0, 1f);
            var t0 = ctx.Now;
            ctx.Run(PlaySchedule(ctx, SequenceTimelines.Outro), "Outro lines");
            yield return ctx.At(t0 + SequenceTimelines.OutroSeconds);
        }
    }
}
