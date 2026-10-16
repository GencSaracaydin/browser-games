using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Playables;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// TL_Outro "SIX-FORTY" (GDD §8.8, 1:50; CINEMATIC_DIRECTION §3): from the climax's white back into the chair;
    /// fists against the cuffs; the gun camera on CRT2; the push into CRT5's roster to SUNDIAL-3; one pen tap per digit;
    /// the hands unclench and the grade loses 30 % saturation; the interrogator walks past the chair, flips the scrambler
    /// off and lifts the phone (the briefing-tape voice); the surveillance shot of Sparrow; the tube dies, the interval
    /// signal, D092, black for the debrief.
    /// </summary>
    public static class OutroTimeline
    {
        /// <summary>Length (= CinematicSequences.OutroSeconds).</summary>
        public const double Length = 110.0;
        /// <summary>GDD §8.8 shot starts 1-11.</summary>
        public static readonly double[] ShotStarts = { 0, 8, 14, 26, 40, 50, 60, 72, 82, 94, 102 };
        /// <summary>D087 digit onsets inside the line when no envelope is available (s).</summary>
        public static readonly float[] DefaultDigitOnsets = { 0.12f, 1.12f, 2.1f, 3.05f };

        /// <summary>Writes the timeline.</summary>
        public static void Build(TimelineBuilder b)
        {
            var pov = BoothLayout.Pov;
            var povFocal = LensMath.FocalFromVerticalFov(BoothLayout.PovFov);
            var cuffs = new Vector3(0f, 0.66f, 0.22f);
            var crt2 = BoothLayout.Crt[1] + new Vector3(0f, 0.01f, -0.2f);
            var crt5 = BoothLayout.Crt[4] + new Vector3(0f, 0.01f, -0.16f);
            var interrogator = BindingIds.BoothInterrogator;

            // State.
            b.Event(0.0, CinematicEventKind.FadeIn, value: 1.2f);
            b.Event(0.0, CinematicEventKind.BoothResting);
            b.Tube(0.0, 0.2, FlickerMode.Off);
            b.Tube(0.2, 8.0, FlickerMode.StutterOn, 2);
            b.Tube(8.0, 94.0, FlickerMode.Unstable);
            b.Tube(94.0, Length, FlickerMode.Dying);
            for (var n = 1; n <= 6; n++)
                if (n != 2) b.Crt(n, 0.0, 26.0, CrtContent.Static, staticAmount: 0.7f);
            b.Crt(2, 0.0, 14.0, CrtContent.Static, staticAmount: 0.7f);
            // Room tone: WP06 switches the ambience to the booth on CinematicStarted(Outro).
            b.Sfx(0.0, CinematicCues.Breath, AudioCueClip.Mode.Loop, 46.0, fade: 3f);
            b.Prop(0.0, BoothPropClip.Action.ClockTicking);
            b.Prop(8.0, BoothPropClip.Action.ClockSilent);
            b.Look(0.0, 8.0, false);

            // 1. 0:00-0:08 white gives way to the flickering tube; the chair; the clock at 6:40.
            b.Shot("1 white to the booth", 0.0, 8.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(6f, 0f, 0f)),
                null, focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f);
            b.Vo(4.0, "D083", interrogator);

            // 2. 0:08-0:14 fists clench against the cuffs.
            b.Shot("2 fists against the cuffs", 8.0, 9.5, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(6f, 0f, 0f)),
                ShotPose.Aim(ShotFrame.Booth, pov, cuffs), focalFrom: povFocal, aperture: 2f, focusFrom: 1.3f, focusTo: 0.55f);
            b.Shot("2 fists hold", 9.5, 14.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, cuffs), null,
                focalFrom: povFocal, aperture: 2f, focusFrom: 0.55f);
            b.Look(8.0, 60.0, true);
            b.Prop(8.6, BoothPropClip.Action.FistsClench);
            b.Prop(9.0, BoothPropClip.Action.CuffStrain);
            b.Prop(11.6, BoothPropClip.Action.CuffStrain);
            b.Vo(9.0, "D084");

            // 3. 0:14-0:26 CRT2: the gun camera (green phosphor), the RPG trail under the skid, one muzzle flash.
            b.Shot("3 gun camera", 14.0, 15.2, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, cuffs), ShotPose.Aim(ShotFrame.Booth, pov, crt2),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 0.55f, focusTo: 1.1f);
            b.Shot("3 gun camera hold", 15.2, 26.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, crt2), null,
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.1f);
            var gun = b.Crt(2, 14.0, 26.0, CrtContent.Video, CinematicAssetIds.VideoGunCam, 0.05f);
            gun.GunCam = true;
            gun.LoopVideo = false;
            b.Sfx(14.0, CinematicCues.CrtWhine, AudioCueClip.Mode.Loop, 12.0, BindingIds.Crt(2), fade: 0.5f, volume: 0.4f);
            b.Vo(15.0, "D085", interrogator);

            // 4. 0:26-0:40 the push into CRT5: Sparrow's photographs of the codebook, the roster, SUNDIAL-3 ... 0640.
            b.Shot("4 push into CRT5", 26.0, 40.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, crt5),
                ShotPose.Aim(ShotFrame.Booth, crt5 + new Vector3(-0.01f, -0.02f, -0.34f), crt5), focalFrom: 35f, focalTo: 60f,
                aperture: 2.8f, focusFrom: 1.15f, focusTo: 0.34f, handheld: 0.1f, ease: ShotEase.EaseInOutCubic);
            var roster = b.Crt(5, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgRoster, 0.04f);
            roster.UvFrom = new Vector4(0f, 0.22f, 1f, 0.5625f);
            roster.UvTo = new Vector4(0.03f, 0.294f - 0.1266f, 0.45f, 0.253f);
            roster.UvEase = ShotEase.EaseInOutCubic;
            b.Crt(1, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgCodebookPage, 0.05f);
            b.Crt(2, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgCodebookPage, 0.05f);
            b.Crt(3, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgRoster, 0.06f);
            b.Crt(4, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgCodebookPage, 0.05f);
            b.Crt(6, 26.0, 82.0, CrtContent.Still, CinematicAssetIds.ImgRoster, 0.06f);
            foreach (var t in new[] { 26.2, 28.0, 30.5, 33.0 }) b.Sfx(t, CinematicCues.CameraShutter, AudioCueClip.Mode.OneShot2D, volume: 0.55f);
            b.Vo(27.0, "D086", interrogator);

            // 5. 0:40-0:50 one pen tap per digit of D087; the music box; the hands unclench; the grade loses 30 %.
            b.Shot("5 back to POV", 40.0, 41.5, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, crt5 + new Vector3(-0.01f, -0.02f, -0.34f), crt5),
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(3f, 0f, 0f)), focalFrom: 60f, focalTo: povFocal, aperture: 2.8f,
                focusFrom: 0.34f, focusTo: 1.3f, ease: ShotEase.EaseInOutCubic);
            b.Shot("5 POV", 41.5, 50.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(3f, 0f, 0f)), null,
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f);
            const double d087 = 41.0;
            b.Vo(d087, "D087", interrogator);
            var onsets = b.Dialogue.Onsets("D087", 4) ?? DefaultDigitOnsets;
            foreach (var o in onsets) b.Prop(d087 + o, BoothPropClip.Action.PenTapOnce);
            var chime = d087 + onsets[onsets.Length - 1] + 1.0;
            b.Sfx(chime, CinematicCues.MusicSixForty, AudioCueClip.Mode.Music, fade: 0.01f);
            b.Prop(chime + 0.4, BoothPropClip.Action.FistsRelax);
            b.Fx(FxChannel.BoothSaturation, chime + 0.3, chime + 2.8, 0f, -30f, ShotEase.Smooth);
            b.Fx(FxChannel.BoothSaturation, chime + 2.8, Length, -30f, -30f);

            // 6. 0:50-1:00 stillness.
            b.Shot("6 stillness", 50.0, 60.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(3f, 0f, 0f)), null,
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f, handheld: 0.12f);
            b.Vo(50.5, "D088", interrogator);
            b.Vo(54.5, "D089");

            // 7. 1:00-1:12 the interrogator walks past the chair, seen from behind, backlit by the CRTs; rack focus from the
            //    cuffs to his silhouette; the toggle flips and the needle drops; D090 clean for the first time.
            b.Look(60.0, Length, false, recentre: 0.6f);
            b.Prop(60.0, BoothPropClip.Action.SmokeOn);
            b.Shot("7 the walk past", 60.0, 72.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, cuffs),
                ShotPose.Track(ShotFrame.Booth, pov, interrogator, new Vector3(0f, 1.45f, 0f)),
                focalFrom: povFocal, focalTo: 40f, aperture: 2f, focusFrom: 0.55f, focusTo: 0f, handheld: 0.1f,
                ease: ShotEase.EaseInOutCubic);
            b.Act(60.5, 64.4, interrogator, null, "booth", BoothLayout.InterrogatorAtScrambler, 0.95f);
            b.Act(64.4, 66.4, interrogator, "ReachToggle");
            b.Prop(66.0, BoothPropClip.Action.ScramblerOff);
            b.Vo(67.0, "D090", interrogator);

            // 8. 1:12-1:22 he lifts the desk phone: the briefing-tape voice.
            b.Shot("8 the phone", 72.0, 82.0, CameraMode.Booth, ShotPose.Track(ShotFrame.Booth, pov, interrogator, new Vector3(0f, 1.45f, 0f)),
                null, focalFrom: 40f, aperture: 2f, handheld: 0.1f);
            b.Act(72.0, 74.0, interrogator, null, "booth", BoothLayout.InterrogatorAtPhone, 0.95f);
            b.Act(74.0, 82.0, interrogator, "PhoneTalk");
            b.Prop(74.3, BoothPropClip.Action.PhoneLift);
            b.Vo(75.0, "D091", interrogator);

            // 9. 1:22-1:34 the CRTs switch to the long-lens surveillance shot of Sparrow; live burn-in 26.10.84 18:31.
            b.Shot("9 surveillance", 82.0, 83.5, CameraMode.Booth, ShotPose.Track(ShotFrame.Booth, pov, interrogator, new Vector3(0f, 1.45f, 0f)),
                ShotPose.Aim(ShotFrame.Booth, pov, crt2), focalFrom: 40f, focalTo: povFocal, aperture: 2.8f, focusTo: 1.1f,
                ease: ShotEase.EaseInOutCubic);
            b.Shot("9 surveillance hold", 83.5, 94.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, crt2), null,
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.1f);
            for (var n = 1; n <= 6; n++)
                b.Crt(n, 82.0 + (n - 1) * 0.08, 94.5, CrtContent.Still, CinematicAssetIds.ImgSurveil, 0.05f,
                    overlay: n == 2 ? "26.10.84 18:31" : null);

            // 10. 1:34-1:42 the tube dies; black; the interval signal (6.0 s); D092 at 1:40.
            b.Shot("10 dark", 94.0, Length, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, crt2), null,
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.1f);
            for (var n = 1; n <= 6; n++) b.Crt(n, 94.5, Length, CrtContent.Off);
            b.Fx(FxChannel.Fade, 94.6, 95.0, 0f, 1f, ShotEase.EaseIn);
            b.Fx(FxChannel.Fade, 95.0, Length, 1f, 1f);
            b.Sfx(94.0, CinematicCues.IntervalSignal, AudioCueClip.Mode.Stinger);
            b.Vo(100.0, "D092");
            // 11. 1:42-1:50 hold black; the debrief types in (WP08, after the sequence ends: EndScreen.HoldBlack).
        }
    }
}
