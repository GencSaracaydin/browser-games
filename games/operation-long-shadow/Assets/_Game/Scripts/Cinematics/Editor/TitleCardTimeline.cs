using LongShadow.Cinematics.Playables;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// TL_TitleCard (GDD §8.3, 8 s, over the ridge on the player camera, look free): WP08's IHud.TitleCard draws the
    /// 2.35:1 bars and the amber teletype; this timeline cues it with STG_TITLE and carries a typed fallback (number
    /// groups corrected into text, 18:40 for 6 frames on line 3) plus bars that only show without a HUD.
    /// </summary>
    public static class TitleCardTimeline
    {
        /// <summary>Length (= CinematicSequences.TitleCardSeconds).</summary>
        public const double Length = 8.0;

        /// <summary>The four lines of GDD §8.3.</summary>
        public const string Text =
            "OPERATION LONG SHADOW\n" +
            "ZAROUD — REPUBLIC OF SARDAHN\n" +
            "21 OCTOBER 1984 — 17:58 LOCAL\n" +
            "SFC NATHAN CALDER — \"SUNDIAL-3\"";

        /// <summary>Writes the timeline.</summary>
        public static void Build(TimelineBuilder b)
        {
            b.Shot("Title card", 0.0, 7.5, CameraMode.Player, null, letterbox: true);
            b.Shot("Bars retract", 7.5, Length, CameraMode.Player, null, letterbox: false);
            b.Look(0.0, Length, true);
            b.Event(0.0, CinematicEventKind.HudTitleCard, value: (float)Length);
            b.Sfx(0.0, CinematicCues.StingerTitle, AudioCueClip.Mode.Stinger);
            var type = b.Type(0.55, 7.4, Text, TypewriterClip.Slot.Title, 34f, CinematicOverlay.Amber, numberGroups: true, onlyWithoutHud: true);
            type.SetFlicker(2, "17:58", "18:40", 0.1f);
        }
    }
}
