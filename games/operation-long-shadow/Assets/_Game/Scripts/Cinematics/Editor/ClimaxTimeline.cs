using LongShadow.Cinematics.Playables;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// TL_Climax (GDD §8.7, 12 s, input locked, the player's own camera in the cabin; CINEMATIC_DIRECTION §3): the RPG
    /// passes under the skid and the helicopter lurches (8° roll), the door gun disengages and the view is drawn 160°
    /// across the cabin to Hollis, Calder's pistol comes up over 3 s, then I4 cascades and everything cuts to white.
    /// WP04 fires the RPG (B11 T3 at t 30) and WP07 plays this sequence on "B11.RPGLaunch".
    /// </summary>
    public static class ClimaxTimeline
    {
        /// <summary>Length (= CinematicSequences.ClimaxSeconds).</summary>
        public const double Length = 12.0;
        /// <summary>GDD §8.7 shot starts 1-4.</summary>
        public static readonly double[] ShotStarts = { 0, 3, 6, 10 };

        /// <summary>Writes the timeline.</summary>
        public static void Build(TimelineBuilder b)
        {
            var hollis = "Squad." + SquadIds.Hollis;

            // 1. 0:00-0:03 the RPG streaks under the skid; lurch; alarm; D081.
            b.Shot("1 RPG under the skid", 0.0, 3.0, CameraMode.Player, null);
            b.Rig(0.0, Length, PlayerRigClip.Action.LockLook);
            var lurch = b.Rig(0.05, 0.15, PlayerRigClip.Action.Lurch);
            lurch.Value = 8f;
            lurch.Value2 = 0.75f;
            b.Sfx(0.0, CinematicCues.RpgFlyby, AudioCueClip.Mode.OneShot2D);
            b.Sfx(0.2, CinematicCues.HeliAlarm, AudioCueClip.Mode.Loop, 9.8, fade: 0.05f);
            b.Vo(0.3, "D081", null, forceRadio: true);

            // 2. 0:03-0:06 the door gun disengages; the view is drawn across the cabin to Hollis hauling the bag;
            //    the music box once, dry and centred.
            b.Shot("2 drawn across the cabin", 3.0, 6.0, CameraMode.Player, null);
            b.Rig(3.0, 3.1, PlayerRigClip.Action.DismountDoorGun);
            var turn = b.Rig(3.1, 4.6, PlayerRigClip.Action.LookTurn);
            turn.Target = hollis;
            turn.TargetOffset = new Vector3(0f, 1.2f, 0f);
            turn.Ease = ShotEase.EaseInOutCubic;
            b.Act(3.0, 6.0, hollis, ActionIds.HaulBag);
            b.Sfx(3.4, CinematicCues.MusicBox, AudioCueClip.Mode.Stinger);

            // 3. 0:06-0:10 Calder's pistol rises (3 s); Hollis turns to the camera; D082 with the jaw flap.
            b.Shot("3 pistol rise", 6.0, 10.0, CameraMode.Player, null);
            b.Rig(6.0, 9.0, PlayerRigClip.Action.PistolRise);
            var hold = b.Rig(6.0, 10.0, PlayerRigClip.Action.LookTurn);
            hold.Target = hollis;
            hold.TargetOffset = new Vector3(0f, 1.45f, 0f);
            hold.Ease = ShotEase.Smooth;
            b.Act(6.2, 10.0, hollis, null, lookAt: BindingIds.PlayerEye, lookWeight: 1f);
            b.Sfx(6.0, CinematicCues.Heartbeat, AudioCueClip.Mode.OneShot2D);
            b.Vo(6.5, "D082", hollis, forceDry: true);

            // 4. 0:10-0:12 I4: clock 6 f, pen 5 f, CRT static 4 f, Calder's own eye 8 f; tinnitus; cut to white.
            b.Shot("4 I4 cascade", 10.0, Length, CameraMode.Player, null);
            b.Event(10.0, CinematicEventKind.PlayInsert, SequenceIds.InsertI4);
            b.Event(10.0, CinematicEventKind.Tinnitus, value: 4f);
            b.Sfx(10.0, null, AudioCueClip.Mode.MusicStop, fade: 0.05f);
            b.Fx(FxChannel.Fade, 10.7, Length, 1f, 1f, color: Color.white);
        }
    }
}
