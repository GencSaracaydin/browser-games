using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Playables;
using LongShadow.Cinematics.Sets;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// TL_Intro shot table: booth shots 1-8 of GDD §8.2 (0:00-1:33), the §8.2a INSERTION cold open of
    /// CINEMATIC_DIRECTION §2 (I-1..I-12, 1:33-2:28) and the shot 9 white-out landing on the gameplay view (2:28-2:30).
    /// The title card is TL_TitleCard, chained by CinematicService. Line starts follow the GDD except where the produced
    /// VO is longer than the gap (D003 waits for D002 to end, D004 for D003; DECISIONS.md [WP09]).
    /// </summary>
    public static class IntroTimeline
    {
        /// <summary>GDD §8.2 shot starts 1-8 (seconds).</summary>
        public static readonly double[] BoothShotStarts = { 0, 6, 12, 22, 30, 58, 68, 78 };
        /// <summary>Start of the insertion (end of booth shot 8).</summary>
        public const double InsertionStart = 93.0;
        /// <summary>CINEMATIC_DIRECTION §2 shot starts I-1..I-12, relative to the insertion start.</summary>
        public static readonly double[] InsertionShotOffsets = { 0, 5, 9, 12, 15, 18, 22, 27, 31, 36, 42, 51 };
        /// <summary>Insertion length (CINEMATIC_DIRECTION §2: about 55 s).</summary>
        public const double InsertionLength = 55.0;
        /// <summary>Shot 9: the white-out lands on the gameplay view.</summary>
        public const double Shot9Start = InsertionStart + InsertionLength;
        /// <summary>Total length (= CinematicSequences.IntroSeconds).</summary>
        public const double Length = Shot9Start + 2.0;

        /// <summary>Helicopter approach: from the start of I-1 to touchdown during I-7 (seconds).</summary>
        public const double ApproachStart = InsertionStart;
        public const double Touchdown = InsertionStart + 26.5;
        /// <summary>Lift-off and departure (I-10).</summary>
        public const double DepartStart = InsertionStart + 36.0;
        public const double DepartEnd = Length;

        private static readonly Color Phosphor = CinematicOverlay.Phosphor;

        /// <summary>Writes the timeline.</summary>
        public static void Build(TimelineBuilder b)
        {
            BuildBooth(b);
            BuildInsertion(b);
            BuildLanding(b);
        }

        // ================================================================== booth, GDD §8.2 shots 1-8

        private static void BuildBooth(TimelineBuilder b)
        {
            var pov = BoothLayout.Pov;
            var povFocal = LensMath.FocalFromVerticalFov(BoothLayout.PovFov);

            // Set state: dark booth, CRTs off, room tone, stuck clock (shot 1).
            b.Event(0.0, CinematicEventKind.BoothResting);
            b.Tube(0.0, 6.3, FlickerMode.Off);
            for (var n = 1; n <= 6; n++) b.Crt(n, 0.0, 22.0, CrtContent.Off);
            // Room tone: WP06 switches the ambience to the booth on CinematicStarted(Intro); "Ambience.Booth.Off" at 1:28.
            b.Prop(0.0, BoothPropClip.Action.ClockTicking);
            b.Prop(12.0, BoothPropClip.Action.ClockSilent);
            b.Sfx(3.0, CinematicCues.MusicSession, AudioCueClip.Mode.Music, 0.25, fade: 0.01f);

            // 1. 0:00-0:06 black, typed slate.
            b.Fx(FxChannel.Fade, 0.0, 6.0, 1f, 1f, color: Color.black);
            b.Type(0.4, 5.9, "02 NOVEMBER 1984 — 03:12 — LOCATION: [REDACTED]", TypewriterClip.Slot.Center, 18f, Phosphor);
            b.Shot("1 black (C_POV)", 0.0, 6.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(35f, 0f, 0f)), handheld: 0f);
            b.Look(0.0, 14.0, false);

            // 2. 0:06-0:12 C_CLOCK ECU pushing in 0.1 m; the tube stutters on (4 blinks).
            var clockCam = BoothLayout.ClockCamera;
            b.Shot("2 ECU clock", 6.0, 12.0, CameraMode.Booth,
                ShotPose.Aim(ShotFrame.Booth, clockCam, BoothLayout.Clock),
                ShotPose.Aim(ShotFrame.Booth, clockCam + new Vector3(0f, 0.004f, 0.1f), BoothLayout.Clock),
                focalFrom: 50f, focalTo: 55f, aperture: 2.8f, handheld: 0.05f, ease: ShotEase.Linear);
            b.Tube(6.3, 22.0, FlickerMode.StutterOn, 4);

            // 3. 0:12-0:22 C_POV tilting up from the strapped wrists to the dark CRT cart; rack focus 0.4 → 1.3 m (f/2);
            //    smoke from behind the left shoulder; limited look unlocks at 0:14.
            b.Shot("3 POV tilt up", 12.0, 17.5, CameraMode.Booth,
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(35f, 0f, 0f)),
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(4f, 0f, 0f)),
                focalFrom: povFocal, aperture: 2f, focusFrom: 0.45f, focusTo: 1.3f, ease: ShotEase.Smooth);
            b.Shot("3 POV hold", 17.5, 22.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(4f, 0f, 0f)),
                focalFrom: povFocal, aperture: 2f, focusFrom: 1.3f);
            b.Look(14.0, 68.0, true);
            b.Sfx(12.4, CinematicCues.LeatherCreak, AudioCueClip.Mode.OneShotAt, boothPoint: new Vector3(0f, 0.65f, 0.15f));
            b.Sfx(12.0, CinematicCues.Breath, AudioCueClip.Mode.Loop, 46.0, fade: 2f, volume: 0.8f);
            b.Prop(15.0, BoothPropClip.Action.SmokeOn);

            // 4. 0:22-0:30 the CRTs snap on 0.6 s apart onto static; the VU needle answers D001.
            var order = new[] { 2, 1, 3, 5, 4, 6 };
            for (var i = 0; i < order.Length; i++)
            {
                var t = 22.0 + i * 0.6;
                var end = order[i] == 4 ? 30.0 : order[i] == 2 ? 32.6 : order[i] == 5 ? 35.2 : order[i] == 1 ? 37.8 : order[i] == 6 ? 40.4 : 43.0;
                b.Crt(order[i], t, end, CrtContent.Static, staticAmount: 0.9f, snapOn: true);
            }
            b.Shot("4 POV, needle at the left edge", 22.0, 30.0, CameraMode.Booth,
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(4f, 0f, 0f)),
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(3f, -8f, 0f)),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.25f, ease: ShotEase.Smooth);
            b.Vo(25.0, "D001", BindingIds.BoothInterrogator);

            // 5. 0:30-0:58 intel montage, one clip per CRT every 2.6 s; tape on the deck; clock frames flash 3 times.
            b.Shot("5 POV montage", 30.0, 34.0, CameraMode.Booth,
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(3f, -8f, 0f)),
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(2f, 0f, 0f)),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f, ease: ShotEase.Smooth);
            b.Shot("5 POV montage hold", 34.0, 58.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(2f, 0f, 0f)),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f);
            b.Crt(4, 30.0, 58.0, CrtContent.Still, CinematicAssetIds.ImgMap);
            b.Crt(2, 32.6, 58.0, CrtContent.Still, CinematicAssetIds.ImgRecon, overlay: "ZAROUD RELAY / 14 OCT 1984 1012Z / FRAME 0640");
            var harrou = b.Crt(5, 35.2, 58.0, CrtContent.Video, CinematicAssetIds.VideoHarrou);
            harrou.FallbackStillId = CinematicAssetIds.ImgHarrou;
            var teletype = b.Crt(1, 37.8, 58.0, CrtContent.Still, CinematicAssetIds.ImgTeletype);
            teletype.Scroll = new Vector2(0f, 0.03f);
            var ids = b.Crt(6, 40.4, 58.0, CrtContent.Slides);
            ids.SlideIds = new[] { CinematicAssetIds.ImgTeamHollis, CinematicAssetIds.ImgTeamSparrow, CinematicAssetIds.ImgTeamTinker };
            ids.SlideSeconds = 1.4f;
            b.Crt(3, 43.0, 58.0, CrtContent.Feed, BindingIds.FeedDeck, 0.05f);
            b.Prop(43.6, BoothPropClip.Action.ReelPlay);
            b.Prop(37.4, BoothPropClip.Action.CrtFlashClock, crt: 5, seconds: 0.05f);
            b.Prop(47.7, BoothPropClip.Action.CrtFlashClock, crt: 1, seconds: 0.05f);
            b.Prop(54.1, BoothPropClip.Action.CrtFlashClock, crt: 4, seconds: 0.05f);
            b.Vo(44.0, "D002", BindingIds.Reel);
            var d3 = System.Math.Max(51.0, b.EndOf(44.0, "D002") + 0.15);
            b.Vo(d3, "D003", BindingIds.Reel);

            // 6. 0:58-1:08 every CRT holds on the stopped clock; four pen taps on steel behind left at 1:04-1:06.
            for (var n = 1; n <= 6; n++) b.Crt(n, 58.0 + (n - 1) * 0.07, n == 2 ? 68.6 : 78.0, CrtContent.Feed, BindingIds.FeedClock, 0.08f);
            var d4 = System.Math.Max(59.0, b.EndOf(d3, "D003") + 0.2);
            b.Prop(d4 - 0.1, BoothPropClip.Action.ReelStop);
            b.Vo(d4, "D004");
            b.Prop(64.0, BoothPropClip.Action.PenTapGroup);
            b.Shot("6 POV clock wall", 58.0, 68.0, CameraMode.Booth, ShotPose.At(ShotFrame.Booth, pov, new Vector3(2f, 0f, 0f)),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.3f);

            // 7. 1:08-1:18 a gloved hand turns Calder's head to CRT2 (0.8 s ease); CRT2 shows the ridge, live.
            var crt2Glass = BoothLayout.Crt[1] + new Vector3(0f, 0.01f, -0.2f);
            b.Look(68.0, 78.0, false, recentre: 0.8f);
            b.Shot("7 forced turn", 68.0, 68.8, CameraMode.Booth,
                ShotPose.At(ShotFrame.Booth, pov, new Vector3(2f, 0f, 0f)),
                ShotPose.Aim(ShotFrame.Booth, pov, crt2Glass),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.1f, impulse: 0.7f, ease: ShotEase.EaseInOutCubic);
            b.Shot("7 CRT2 ridge live", 68.8, 78.0, CameraMode.Booth, ShotPose.Aim(ShotFrame.Booth, pov, crt2Glass),
                focalFrom: povFocal, aperture: 2.8f, focusFrom: 1.1f);
            b.Sfx(67.9, CinematicCues.GloveStubble, AudioCueClip.Mode.OneShotAt, boothPoint: new Vector3(-0.12f, 1.2f, -0.05f));
            b.Sfx(68.1, CinematicCues.ChairCreak, AudioCueClip.Mode.OneShotAt, boothPoint: new Vector3(0f, 0.4f, -0.1f));
            b.Act(66.6, 68.4, BindingIds.BoothInterrogator, null, "booth", new Vector3(-0.32f, 0f, -0.42f), 1.0f);
            b.Act(72.0, 74.0, BindingIds.BoothInterrogator, null, "booth", BoothLayout.InterrogatorMark, 0.9f);
            b.Crt(2, 68.6, 78.0, CrtContent.Feed, BindingIds.FeedRidge, 0.04f);
            b.Vo(68.2, "D005", BindingIds.BoothInterrogator);

            // 8. 1:18-1:33 hold until S1_Approach/S9_TownFar are loaded (D20), then dolly 1.0 m into CRT2, FOV 50 → 30;
            //    the scanlines thicken into the horizon bands; hum crossfades to wind and cicadas over the last 5 s.
            b.Event(78.0, CinematicEventKind.Hold, value: (float)HoldCondition.IntroSections);
            var ridge = b.Crt(2, 78.0, 93.0, CrtContent.Feed, BindingIds.FeedRidge, 0.03f);
            ridge.ScanFrom = 0f;
            ridge.ScanTo = 1f;
            ridge.UvFrom = new Vector4(0f, 0f, 1f, 1f);
            ridge.UvTo = new Vector4(0.2f, 0.36f, 0.6f, 0.3f);
            ridge.UvEase = ShotEase.EaseIn;
            b.Shot("8 dolly into CRT2", 78.0, 93.0, CameraMode.Booth,
                ShotPose.Aim(ShotFrame.Booth, pov, crt2Glass),
                ShotPose.Aim(ShotFrame.Booth, pov + new Vector3(0f, -0.12f, 1.0f), crt2Glass + new Vector3(0f, 0f, 0.2f)),
                fov: BoothLayout.PovFov, fovTo: 30f, aperture: 2.8f, focusFrom: 1.1f, focusTo: 0.1f, handheld: 0.08f,
                ease: ShotEase.EaseIn);
            b.Vo(79.0, "D006", BindingIds.BoothInterrogator);
            b.Event(88.0, CinematicEventKind.MissionEvent, "Ambience.Booth.Off", fireOnSkip: true);
            b.Sfx(88.0, CinematicCues.DesertWind, AudioCueClip.Mode.Loop, 62.0, fade: 3f);
            b.Sfx(88.0, CinematicCues.Cicadas, AudioCueClip.Mode.Loop, 47.0, fade: 4f, volume: 0.7f);
            b.Event(85.0, CinematicEventKind.InsertionCast, flag: true);
            // The insertion plays at the Beat 1 light (s 0: sun 8° at 258°), WP10_LOOK request.
            b.Event(85.0, CinematicEventKind.LightStage, value: 0f, flag: true);

            // Leaving the booth: CRTs back to static so the ridge and clock feed cameras stop rendering.
            for (var n = 1; n <= 6; n++) b.Crt(n, InsertionStart, Length, CrtContent.Static, staticAmount: 0.85f);
        }

        // ================================================================== insertion, CINEMATIC_DIRECTION §2

        private static void BuildInsertion(TimelineBuilder b)
        {
            var approach = new PathCurve(InsertionLayout.Approach);
            var s = InsertionStart;
            Vector3 HeliAt(double t) => approach.Sample(Easing.Evaluate(ShotEase.EaseOut, (float)((t - ApproachStart) / (Touchdown - ApproachStart))));

            // Helicopter: approach with banking and the nose-up flare, rotor sound attached (J-cut 2 s before I-1).
            b.PathMove(BindingIds.CastHeli, ApproachStart, Touchdown, InsertionLayout.Approach, ease: ShotEase.EaseOut,
                orient: TransformPathClip.Orient.Aircraft, clearance: InsertionLayout.FlightClearance, flare: true);
            b.PathMove(BindingIds.CastHeli, DepartStart, DepartEnd, InsertionLayout.Depart, ease: ShotEase.EaseIn,
                orient: TransformPathClip.Orient.Aircraft, clearance: InsertionLayout.FlightClearance);
            b.Sfx(s - 2.0, CinematicCues.RotorApproach, AudioCueClip.Mode.Attached, 31.0, BindingIds.CastHeli, fade: 1.5f);
            b.Sfx(Touchdown - 2.0, CinematicCues.RotorLoop, AudioCueClip.Mode.Loop, DepartStart - Touchdown + 6.0, BindingIds.CastHeli, fade: 2f);
            b.Sfx(DepartStart, CinematicCues.RotorFlyby, AudioCueClip.Mode.Attached, 14.0, BindingIds.CastHeli, fade: 3f);

            // Cast in the cabin.
            b.Act(s, s + 0.2, BindingIds.CastHollis, ActionIds.SitHeli, weapon: WeaponIds.Commando);
            b.Act(s, s + 0.2, BindingIds.CastSparrow, ActionIds.SitHeli);
            b.Act(s, s + 0.2, BindingIds.CastTinker, ActionIds.SitHeli);
            b.Act(s, s + 0.2, BindingIds.CastCalder, ActionIds.SitHeli, weapon: WeaponIds.Commando);

            // I-1 0:00-0:05 EWS aerial: 24 mm f/8, drone tracking at about 120 m, the sun low on the right.
            var p0 = HeliAt(s);
            var p1 = HeliAt(s + 5.0);
            b.Shot("I-1 EWS aerial", s, s + 5.0, CameraMode.World,
                ShotPose.Track(ShotFrame.World, new Vector3(p0.x - 30f, 26f, p0.z + 112f), BindingIds.CastHeli, Vector3.zero),
                ShotPose.Track(ShotFrame.World, new Vector3(p1.x - 6f, 27f, p1.z + 106f), BindingIds.CastHeli, Vector3.zero),
                focalFrom: 24f, aperture: 8f, handheld: 0.12f, hz: 0.4f, letterbox: true, ease: ShotEase.Linear);

            // I-2 0:05-0:09 MS tracking at door height, 50 mm f/2.8 hand-held, 8 m off the open left door.
            var door = InsertionLayout.CabinFloor + new Vector3(-1.1f, 0.9f, 0.4f);
            b.Shot("I-2 MS door tracking", s + 5.0, s + 9.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.Target, door + new Vector3(-8f, 0.4f, 1.5f), door, BindingIds.CastHeli),
                ShotPose.Aim(ShotFrame.Target, door + new Vector3(-7.6f, 0.6f, 0.4f), door, BindingIds.CastHeli),
                focalFrom: 50f, aperture: 2.8f, focusFrom: 8f, handheld: 0.35f, hz: 1.1f, letterbox: true, ease: ShotEase.Linear);
            b.Vo(s + 5.4, "D200", null, forceRadio: true);

            // I-3 0:09-0:12 CU cabin interior, 35 mm f/2: Hollis backlit raises one finger.
            var hol = InsertionLayout.CabinFloor + InsertionLayout.SeatHollis;
            b.Shot("I-3 CU Hollis", s + 9.0, s + 12.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.Target, hol + new Vector3(0.95f, 1.05f, 0.7f), hol + new Vector3(0f, 1.05f, 0f), BindingIds.CastHeli),
                null, focalFrom: 35f, aperture: 2f, handheld: 0.3f, hz: 1.0f, impulse: 0.4f, letterbox: true);
            b.Act(s + 9.6, s + 11.5, BindingIds.CastHollis, "OneFinger");
            b.Vo(s + 10.0, "D201", BindingIds.CastHollis, forceDry: true);
            b.Sfx(s + 9.0, CinematicCues.HeliCabin, AudioCueClip.Mode.OneShot2D, 9.0, volume: 0.9f);

            // I-4 0:12-0:15 ECU hands, 85 mm f/1.8: magazine seated (slap), charging handle; rack to Hollis behind.
            var cal = InsertionLayout.CabinFloor + InsertionLayout.SeatCalder;
            var hands = cal + Quaternion.Euler(0f, InsertionLayout.SeatCalderYaw, 0f) * new Vector3(0f, 0.95f, 0.35f);
            b.Shot("I-4 ECU hands", s + 12.0, s + 15.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.Target, hands + new Vector3(0.38f, 0.12f, 0.3f), hands, BindingIds.CastHeli),
                null, focalFrom: 85f, aperture: 1.8f, focusFrom: 0.5f, focusTo: 1.5f, handheld: 0.25f, hz: 1.1f, letterbox: true,
                ease: ShotEase.EaseInOutCubic);
            b.Act(s + 12.1, s + 14.8, BindingIds.CastCalder, ActionIds.Reload, weapon: WeaponIds.Commando);
            b.Sfx(s + 12.3, CinematicCues.CommandoReload, AudioCueClip.Mode.OneShotAt, at: BindingIds.CastCalder);

            // I-5 0:15-0:18 OTS Sparrow, 50 mm f/2.8: the Minolta comes up at the ridge; shutter.
            var spa = InsertionLayout.CabinFloor + InsertionLayout.SeatSparrow;
            b.Shot("I-5 OTS Sparrow", s + 15.0, s + 18.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.Target, spa + new Vector3(0.5f, 1.25f, 0.35f), spa + new Vector3(-3f, 1.0f, -0.4f), BindingIds.CastHeli),
                null, focalFrom: 50f, aperture: 2.8f, focusFrom: 0.6f, handheld: 0.3f, hz: 1.0f, letterbox: true);
            b.Act(s + 15.2, s + 17.8, BindingIds.CastSparrow, ActionIds.RaiseCamera);
            b.Sfx(s + 17.0, CinematicCues.CameraShutter, AudioCueClip.Mode.OneShotAt, at: BindingIds.CastSparrow);

            // I-6 0:18-0:22 CU profile, 50 mm f/2.8 push-in: Tinker keys the PRC-77 handset.
            var tin = InsertionLayout.CabinFloor + InsertionLayout.SeatTinker;
            b.Shot("I-6 CU Tinker profile", s + 18.0, s + 22.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.Target, tin + new Vector3(0.05f, 1.05f, 0.95f), tin + new Vector3(0f, 0.95f, 0f), BindingIds.CastHeli),
                ShotPose.Aim(ShotFrame.Target, tin + new Vector3(0.05f, 1.02f, 0.72f), tin + new Vector3(0f, 0.95f, 0f), BindingIds.CastHeli),
                focalFrom: 50f, aperture: 2.8f, handheld: 0.28f, hz: 0.9f, letterbox: true, ease: ShotEase.Linear);
            b.Act(s + 18.1, s + 21.8, BindingIds.CastTinker, ActionIds.HandsetToEar);
            b.Sfx(s + 18.4, CinematicCues.RadioKeyUp, AudioCueClip.Mode.OneShotAt, at: BindingIds.CastTinker);
            b.Vo(s + 18.6, "D202", BindingIds.CastTinker);
            b.Vo(System.Math.Max(s + 21.2, b.EndOf(s + 18.6, "D202") + 0.15), "D203", null, forceRadio: true);

            // I-7 0:22-0:27 low WS on the sand at (−30, 12.6, −15), 18 mm f/5.6 into the sun: flare and touchdown;
            //     speed ramp from 0:25.
            b.Shot("I-7 low WS landing", s + 22.0, s + 27.0, CameraMode.World,
                ShotPose.Track(ShotFrame.World, new Vector3(-30f, 12.95f, -15f), BindingIds.CastHeli, new Vector3(0f, 1.5f, 0f)),
                null, focalFrom: 18f, aperture: 5.6f, handheld: 0f, impulse: 0.5f, letterbox: true);
            b.Ramp(s + 25.0, s + 27.0, 1f, 0.45f);

            // I-8 0:27-0:31 ECU slow motion at boot height beside the skid: boots hit the sand; ramp back to 1.0 on the
            //     first stride.
            var lzFwd = approach.Tangent(1f);
            lzFwd.y = 0f;
            lzFwd.Normalize();
            var lzLeft = Vector3.Cross(lzFwd, Vector3.up);
            var lz = InsertionLayout.LandingZone;
            var drop = lz + lzLeft * 2.0f + lzFwd * 0.4f;
            var doorWorld = lz + lzLeft * 1.15f + lzFwd * 0.4f + Vector3.up * 1.2f;
            b.Ramp(s + 27.0, s + 29.6, 0.45f, 0.45f);
            b.Ramp(s + 29.6, s + 30.6, 0.45f, 1f);
            b.Shot("I-8 ECU boots slow-mo", s + 27.0, s + 31.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.World, drop + lzLeft * 0.9f - lzFwd * 0.7f + Vector3.up * 0.28f, drop + Vector3.up * 0.15f),
                null, focalFrom: 35f, aperture: 2f, focusFrom: 1.1f, handheld: 0.2f, hz: 0.8f, letterbox: true);
            b.PathMove(BindingIds.CastTinker, Touchdown + 0.2, s + 28.0, new[] { doorWorld, Vector3.Lerp(doorWorld, drop, 0.6f) + Vector3.up * 0.2f, drop },
                ease: ShotEase.EaseIn, groundSnap: false);
            b.Act(Touchdown + 0.2, s + 29.5, BindingIds.CastTinker, ActionIds.SkidDrop);
            b.Sfx(s + 27.4, CinematicCues.MoveLand, AudioCueClip.Mode.OneShotAt, at: BindingIds.CastTinker);
            b.Vo(s + 28.2, "D204", BindingIds.CastHollis, forceDry: true);

            // Crouch-run up the reverse slope (INS.Squad.Path), each a little offset, to the crest slots of B1.Squad.Start.
            var squad = new PathCurve(InsertionLayout.SquadPath);
            Run(b, squad, BindingIds.CastHollis, InsertionLayout.OffsetHollis, s + 29.6, 0.00, InsertionLayout.CrestHollis);
            Run(b, squad, BindingIds.CastTinker, InsertionLayout.OffsetTinker, s + 29.9, 0.35, InsertionLayout.CrestTinker);
            Run(b, squad, BindingIds.CastSparrow, InsertionLayout.OffsetSparrow, s + 30.2, 0.6, InsertionLayout.CrestSparrow);
            Run(b, squad, BindingIds.CastCalder, InsertionLayout.OffsetCalder, s + 30.5, 0.9, InsertionLayout.CrestCalder);

            // I-9 0:31-0:36 MS frontal backing up the slope, 35 mm f/4 hand-held; Hollis signals "move".
            var runDir = squad.Tangent(0.25f);
            runDir.y = 0f;
            runDir.Normalize();
            b.Shot("I-9 MS frontal backing up", s + 31.0, s + 36.0, CameraMode.World,
                ShotPose.Track(ShotFrame.TargetOffset, runDir * 6.5f + Vector3.up * 1.2f, BindingIds.CastHollis, new Vector3(0f, 1.0f, 0f), BindingIds.CastHollis),
                null, focalFrom: 35f, aperture: 4f, handheld: 0.35f, hz: 1.2f, letterbox: true, motionBlur: 0.3f);
            b.Act(s + 32.5, s + 34.0, BindingIds.CastHollis, ActionIds.HandSignal);
            b.Sfx(s + 31.0, CinematicCues.FoleyGear, AudioCueClip.Mode.OneShotAt, at: BindingIds.CastHollis);

            // I-10 0:36-0:42 WS, 35 mm f/8 slow pan: Magpie lifts and banks away; the squad climbs, small.
            b.Shot("I-10 WS departure pan", s + 36.0, s + 42.0, CameraMode.World,
                ShotPose.Track(ShotFrame.World, new Vector3(-92f, 17.5f, -6f), BindingIds.CastHeli, new Vector3(0f, 1.5f, 0f)),
                null, focalFrom: 35f, aperture: 8f, handheld: 0.1f, hz: 0.5f, letterbox: true);
            b.Vo(s + 36.6, "D205", null, forceRadio: true);

            // I-11 0:42-0:51 hero reveal: crane up behind the squad as they drop prone; 28 → 40 mm, f/4; Zaroud below.
            var crest = InsertionLayout.CrestCalder;
            var town = new Vector3(232f, 6f, 262f);
            b.Shot("I-11 hero reveal crane", s + 42.0, s + 51.0, CameraMode.World,
                ShotPose.Aim(ShotFrame.World, crest + new Vector3(-6.5f, 1.1f, -8.5f), crest + new Vector3(0f, 0.4f, 0f)),
                ShotPose.Aim(ShotFrame.World, crest + new Vector3(-4.6f, 5.4f, -5.8f), town),
                focalFrom: 28f, focalTo: 40f, aperture: 4f, focusFrom: 8f, focusTo: 300f, handheld: 0.12f, hz: 0.5f,
                letterbox: true, ease: ShotEase.Smooth);
            b.Sfx(s + 42.0, CinematicCues.MusicSession, AudioCueClip.Mode.MusicLayer, 0.25, fade: 3f, layer: 1);
            b.Sfx(s + 43.0, CinematicCues.CurfewSiren, AudioCueClip.Mode.OneShot2D, volume: 0.55f);
            b.Vo(s + 44.0, "D206");
            b.Vo(s + 49.0, "D207", BindingIds.BoothInterrogator);

            // I-12 0:51-0:55 push-in over Calder's shoulder into his eye line; ends on the player camera; +6 EV blow-out.
            b.Event(s + 50.9, CinematicEventKind.PlayerLook, value: -2f, value2: InsertionLayout.LandingHeading, fireOnSkip: true);
            var shoulder = crest + new Vector3(-0.85f, 0.75f, -1.05f);
            var i12 = b.Shot("I-12 push-in to POV", s + 51.0, Shot9Start, CameraMode.World,
                ShotPose.Aim(ShotFrame.World, shoulder, crest + MathX.DirectionFromBearing(45f) * 40f + Vector3.up * 1.0f),
                ShotPose.At(ShotFrame.PlayerEye, Vector3.zero, Vector3.zero),
                focalFrom: 40f, aperture: 4f, focusFrom: 25f, handheld: 0.08f, ease: ShotEase.EaseInOutCubic);
            i12.MatchPlayerFov = true;
            b.Fx(FxChannel.Exposure, s + 53.0, Shot9Start, 0f, 6f, ShotEase.EaseIn);
            b.Sfx(Shot9Start - 0.06, CinematicCues.FluorescentBuzz, AudioCueClip.Mode.Loop, 0.06, fade: 0.02f);
        }

        private static void Run(TimelineBuilder b, PathCurve squad, string id, float lateral, double start, double delay, Vector3 slot)
        {
            const float until = 0.86f;
            var runStart = start + delay;
            var runEnd = InsertionStart + 41.0 + delay * 0.5;
            b.Act(runStart, runEnd + 1.5, id, ActionIds.CrouchRun);
            b.PathMove(id, runStart, runEnd, InsertionLayout.SquadPath, 0.04f, until, ShotEase.Linear, TransformPathClip.Orient.Yaw,
                lateral, groundSnap: true);
            var at = squad.Sample(until);
            var fwd = squad.Tangent(until);
            var right = Vector3.Cross(Vector3.up, new Vector3(fwd.x, 0f, fwd.z).normalized);
            var p = at + right * lateral;
            b.PathMove(id, runEnd, InsertionStart + 42.0 + delay * 0.4, new[] { p, Vector3.Lerp(p, slot, 0.6f), slot },
                ease: ShotEase.EaseOut, groundSnap: true);
            b.Act(InsertionStart + 42.0 + delay * 0.4, InsertionStart + 42.3 + delay * 0.4, id, ActionIds.ProneIdle);
        }

        // ================================================================== shot 9

        private static void BuildLanding(TimelineBuilder b)
        {
            // 9. 2:28-2:30 the player camera, prone on the ridge: +6 EV falling to 0 over 2 s (GDD §8.2 shot 9).
            b.Shot("9 player view (white-out)", Shot9Start, Length, CameraMode.Player, null);
            b.Event(Shot9Start, CinematicEventKind.Flash, value: 6f, value2: 2f, fireOnSkip: true);
            b.Look(Shot9Start, Length, true);
            b.Event(Length - 0.15, CinematicEventKind.InsertionCast, flag: false, fireOnSkip: true);
            b.Event(Length - 0.12, CinematicEventKind.LightStage, flag: false, fireOnSkip: true);
            b.Event(Length - 0.1, CinematicEventKind.BoothResting, fireOnSkip: true);
        }
    }
}
