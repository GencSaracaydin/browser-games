using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Playables;
using LongShadow.Cinematics.Sets;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// TL_MidB_Reel (GDD §8.6, 45 s; CINEMATIC_DIRECTION §3): the player's head at the projector (B8.CamReel) facing the
    /// screen, ±20° look; the 16 mm image is the booth's film camera feed through SG_Film16mm (Session 11 state for
    /// shots 4-6); I3 dives into the screen and dissolves into the real booth; the film jams and burns out to white;
    /// Hollis grips the shoulder and control returns.
    /// </summary>
    public static class MidBTimeline
    {
        /// <summary>Length (= CinematicSequences.MidBSeconds).</summary>
        public const double Length = 45.0;
        /// <summary>GDD §8.6 shot starts 1-8.</summary>
        public static readonly double[] ShotStarts = { 0, 8, 12, 16, 26, 31, 36, 40 };

        private const string Cam = "B8.CamReel";
        private const string Projector = "B8.Projector";

        /// <summary>Writes the timeline.</summary>
        public static void Build(TimelineBuilder b)
        {
            var screen = BindingIds.Projection;
            ShotPose Head(Vector3 euler, Vector3 offset = default) => ShotPose.At(ShotFrame.TargetOffset, offset, euler, Cam);
            ShotPose Screen(Vector3 offset = default) => ShotPose.Track(ShotFrame.TargetOffset, offset, screen, Vector3.zero, Cam);

            b.Event(0.0, CinematicEventKind.Projection, flag: true);
            b.Event(0.05, CinematicEventKind.ProjectionContent, "Off");
            b.Event(Length - 0.1, CinematicEventKind.Projection, flag: false, fireOnSkip: true);
            b.Event(Length - 0.1, CinematicEventKind.BoothResting, fireOnSkip: true);

            // 1. 0:00-0:08 the can on the table, threading the film (the FP hands are WP05's; see handoff gaps).
            b.Shot("1 threading the film", 0.0, 8.0, CameraMode.World, Head(new Vector3(38f, 0f, 0f)), Head(new Vector3(34f, -4f, 0f)),
                focalFrom: 35f, aperture: 2.8f, focusFrom: 0.75f, handheld: 0.2f, hz: 0.6f, ease: ShotEase.Smooth);
            b.Look(0.0, 12.0, false);
            b.Sfx(0.4, CinematicCues.FilmCan, AudioCueClip.Mode.OneShotAt, at: Projector);

            // 2. 0:08-0:12 the lever: the projector clatters up, the beam cuts the dust; tilt up to the screen.
            b.Shot("2 lever, beam", 8.0, 12.0, CameraMode.World, Head(new Vector3(34f, -4f, 0f)), Screen(),
                focalFrom: 35f, aperture: 2.8f, handheld: 0.2f, hz: 0.6f, impulse: 0.3f, ease: ShotEase.Smooth);
            b.Sfx(8.3, CinematicCues.ProjectorStart, AudioCueClip.Mode.OneShotAt, at: Projector);
            b.Event(8.6, CinematicEventKind.ProjectionContent, "Blank");

            // 3. 0:12-0:16 countdown leader 5..4..3 synced to the clatter.
            b.Shot("3 leader", 12.0, 16.0, CameraMode.World, Screen(), Screen(new Vector3(0f, 0f, 0.1f)),
                focalFrom: 35f, focalTo: 38f, aperture: 2.8f, handheld: 0.15f, hz: 0.5f, ease: ShotEase.Linear);
            b.Look(12.0, 36.0, true, 20f, 20f);
            b.Event(12.0, CinematicEventKind.ProjectionContent, "Leader");
            for (var i = 0; i < 3; i++) b.Sfx(12.0 + i * 1.333, CinematicCues.LeaderBeep, AudioCueClip.Mode.OneShotAt, at: screen);

            // 4. 0:16-0:26 the booth on film: Session 11, slate "S-3 / SESSION 11 / 03-84", 18 fps mono.
            b.Event(16.0, CinematicEventKind.BoothSession11, flag: true);
            b.Event(16.0, CinematicEventKind.ProjectionContent, "Film");
            b.Shot("4 Session 11 footage", 16.0, 26.0, CameraMode.World, Screen(new Vector3(0f, 0f, 0.1f)), Screen(new Vector3(0f, 0f, 0.4f)),
                focalFrom: 38f, focalTo: 42f, aperture: 2.8f, handheld: 0.15f, hz: 0.5f, ease: ShotEase.Linear);
            b.Act(16.4, 27.8, "Squad." + SquadIds.Sparrow, ActionIds.RaiseCamera);

            // 5. 0:26-0:31 a jerky zoom; the man lifts his head: it is Calder. Sparrow lowers her camera.
            b.Prop(26.0, BoothPropClip.Action.FilmZoomIn, crt: 16, seconds: 3.6f);
            b.Act(27.4, 29.6, BindingIds.BoothCalder, "HeadLift");
            b.Act(28.0, 28.2, "Squad." + SquadIds.Sparrow, CharacterActionClip.StopAction);
            b.Shot("5 jerky zoom", 26.0, 31.0, CameraMode.World, Screen(new Vector3(0f, 0f, 0.4f)), null,
                focalFrom: 42f, aperture: 2.8f, handheld: 0.15f, hz: 0.5f);

            // 6. 0:31-0:36 the lighter hand enters the frame; the lips move with the numbers; he goes limp.
            b.Prop(31.0, BoothPropClip.Action.LighterStrike);
            var jaw = b.Prop(31.8, BoothPropClip.Action.JawDigits, 3.4);
            jaw.Times = new[] { 0.2f, 1.1f, 2.0f, 2.9f };
            b.Act(35.0, 36.0, BindingIds.BoothCalder, "GoLimp");
            b.Shot("6 the lighter", 31.0, 36.0, CameraMode.World, Screen(new Vector3(0f, 0f, 0.4f)), Screen(new Vector3(0f, 0.02f, 0.55f)),
                focalFrom: 42f, focalTo: 45f, aperture: 2.8f, handheld: 0.15f, hz: 0.5f);

            // 7. 0:36-0:40 I3: the image grows to fill the frame and becomes the real booth (Session 12) from the same
            //    angle: push into the screen, then a cross-dissolve from the film image to the booth camera. D054 whispered.
            b.Event(36.0, CinematicEventKind.InsertBus, SequenceIds.InsertI3, flag: true);
            b.Event(36.0, CinematicEventKind.BoothSession11, flag: false);
            b.Event(36.0, CinematicEventKind.ProjectionContent, "Booth");
            b.Prop(36.0, BoothPropClip.Action.FilmZoomReset);
            b.Look(36.0, Length, false, recentre: 0.4f);
            b.Shot("7 I3 into the screen", 36.0, 37.6, CameraMode.World, Screen(new Vector3(0f, 0.02f, 0.55f)), Screen(new Vector3(0f, 0.08f, 3.2f)),
                focalFrom: 45f, focalTo: 75f, aperture: 2.8f, handheld: 0.1f, ease: ShotEase.EaseIn);
            BoothLayout.Preset(BoothCameraPreset.FilmCam, out var fp, out var fr, out var ffov);
            b.Shot("7 I3 the real room", 37.6, 40.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, fp, fr.eulerAngles),
                ShotPose.At(ShotFrame.Booth, fp + (BoothLayout.FilmCamTarget - fp).normalized * 0.25f, fr.eulerAngles),
                fov: ffov, aperture: 2.8f, focusFrom: Vector3.Distance(fp, BoothLayout.FilmCamTarget), handheld: 0.06f, ease: ShotEase.Linear);
            b.Fx(FxChannel.Feed, 36.4, 37.6, 0f, 1f, ShotEase.EaseIn, feedId: BindingIds.FeedFilm);
            b.Fx(FxChannel.Feed, 37.6, 39.4, 1f, 0f, ShotEase.EaseOut, feedId: BindingIds.FeedFilm);
            b.Vo(36.5, "D054", BindingIds.BoothInterrogator);
            b.Event(37.6, CinematicEventKind.MissionEvent, "Ambience.Booth.On");
            b.Event(40.0, CinematicEventKind.MissionEvent, "Ambience.Booth.Off", fireOnSkip: true);
            b.Event(40.0, CinematicEventKind.InsertBus, SequenceIds.InsertI3, flag: false);

            // 8. 0:40-0:45 the film jams and burns out from the centre to white; Hollis grips the shoulder; control returns.
            b.Event(40.0, CinematicEventKind.ProjectionContent, "Film");
            b.Shot("8 burn-out", 40.0, 43.5, CameraMode.World, Screen(new Vector3(0f, 0.02f, 0.4f)), Screen(new Vector3(0f, 0.02f, 0.9f)),
                focalFrom: 50f, focalTo: 58f, aperture: 2.8f, handheld: 0.2f, hz: 0.7f, impulse: 0.4f, ease: ShotEase.EaseIn);
            b.Sfx(40.2, CinematicCues.FilmFlap, AudioCueClip.Mode.OneShotAt, at: Projector);
            b.Fx(FxChannel.Burn, 40.6, 43.0, 0f, 1f, ShotEase.EaseIn);
            b.Fx(FxChannel.Burn, 43.0, 43.5, 1f, 1f);
            b.Vo(41.4, "D055", "Squad." + SquadIds.Hollis, forceDry: true);
            b.Shot("8 control returns", 43.5, Length, CameraMode.Player, null);
            b.Fx(FxChannel.Fade, 43.5, 44.4, 1f, 0f, ShotEase.EaseOut, Color.white);
            var grip = b.Rig(43.6, 43.7, PlayerRigClip.Action.Lurch);
            grip.Value = 2.5f;
            grip.Value2 = 0.3f;
            b.Sfx(43.6, CinematicCues.HandSlap, AudioCueClip.Mode.OneShot2D);
        }
    }
}
