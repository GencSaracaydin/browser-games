using LongShadow.Core;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// The authored sequences (TECH_PLAN §7.12, GDD §8, CINEMATIC_DIRECTION §2) as code data (D12). Lengths are the
    /// shot-table totals the timeline builders write; the EditMode tests compare them with the builders.
    /// </summary>
    public static class CinematicSequences
    {
        /// <summary>Booth shots 1-8 (1:33), the INSERTION cold open (55 s) and the shot 9 white-out landing (2 s).</summary>
        public const float IntroSeconds = 150f;
        /// <summary>GDD §8.3.</summary>
        public const float TitleCardSeconds = 8f;
        /// <summary>GDD §8.6.</summary>
        public const float MidBSeconds = 45f;
        /// <summary>GDD §8.7.</summary>
        public const float ClimaxSeconds = 12f;
        /// <summary>GDD §8.8.</summary>
        public const float OutroSeconds = 110f;

        /// <summary>Gameplay channels a cinematic locks (look is managed per shot).</summary>
        public const InputLockFlags Gameplay =
            InputLockFlags.Move | InputLockFlags.Fire | InputLockFlags.Weapons | InputLockFlags.Interact | InputLockFlags.Jump;

        private static readonly CinematicSequenceDef[] s_rows =
        {
            // Intro: skippable from the first viewing (CINEMATIC_DIRECTION §2 overrides GDD §8); always chains the title
            // card; "Intro.ControlToPlayer" fires when the title card's bars have retracted (GDD §8.3: control returns).
            new CinematicSequenceDef(SequenceIds.Intro, CinematicAssetIds.TimelineIntro, IntroSeconds, SkipRule.Always,
                Gameplay, false, true, true, CameraMode.Booth, EndScreen.Clear,
                next: SequenceIds.TitleCard, endEvent: SequenceIds.IntroControlToPlayer),
            // Title card: always plays, on the player camera (prone on the ridge, look only), HUD left to WP08's TitleCard.
            new CinematicSequenceDef(SequenceIds.TitleCard, CinematicAssetIds.TimelineTitleCard, TitleCardSeconds,
                SkipRule.Never, Gameplay, true, false, true, CameraMode.Player, EndScreen.Clear),
            // MID-B: player POV at the projector with ±20° look; the player's view is restored afterwards.
            new CinematicSequenceDef(SequenceIds.MidB, CinematicAssetIds.TimelineMidB, MidBSeconds, SkipRule.AfterFirstView,
                Gameplay, false, true, true, CameraMode.World, EndScreen.Clear, restorePlayerLook: true),
            // Climax: input locked, the player's own camera, ends on white (GDD §8.7 cut to white).
            new CinematicSequenceDef(SequenceIds.Climax, CinematicAssetIds.TimelineClimax, ClimaxSeconds,
                SkipRule.AfterFirstView, InputLockFlags.All, false, true, false, CameraMode.Player, EndScreen.HoldWhite),
            // Outro: the booth, ends on black under the debrief dossier (GDD §8.8 shot 11).
            new CinematicSequenceDef(SequenceIds.Outro, CinematicAssetIds.TimelineOutro, OutroSeconds,
                SkipRule.AfterFirstView, Gameplay, false, true, true, CameraMode.Booth, EndScreen.HoldBlack),
        };

        /// <summary>Every row.</summary>
        public static System.Collections.Generic.IReadOnlyList<CinematicSequenceDef> All => s_rows;

        /// <summary>The row of a sequence id, or null.</summary>
        public static CinematicSequenceDef Get(string id)
        {
            for (var i = 0; i < s_rows.Length; i++)
                if (s_rows[i].Id == id) return s_rows[i];
            return null;
        }
    }
}
