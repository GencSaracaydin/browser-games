using System.Collections.Generic;
using LongShadow.Cinematics;
using LongShadow.Cinematics.Editor;
using LongShadow.Cinematics.Playables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

namespace LongShadow.Tests.EditMode.Cinematics
{
    /// <summary>
    /// Builds every TL_* shot table in memory and checks it against GDD §8 / CINEMATIC_DIRECTION §2: cut times (±0.2 s),
    /// a camera on screen for the whole length, no overlapping clips on state tracks, every line in DIALOGUE.json,
    /// key line times, known binding ids.
    /// </summary>
    public sealed class ShotTableTests
    {
        private static DialogueData s_dialogue;
        private readonly List<Object> _created = new List<Object>();

        private static DialogueData Dialogue => s_dialogue ?? (s_dialogue = new DialogueData());

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private TimelineAsset Build(double seconds, System.Action<TimelineBuilder> build)
        {
            var b = TimelineBuilder.InMemory(seconds, Dialogue);
            build(b);
            foreach (var t in b.Timeline.GetOutputTracks())
            {
                foreach (var c in t.GetClips())
                    if (c.asset != null) _created.Add(c.asset);
                _created.Add(t);
            }
            _created.Add(b.Timeline);
            return b.Timeline;
        }

        private static List<TimelineClip> CameraClips(TimelineAsset tl)
        {
            var list = new List<TimelineClip>();
            foreach (var t in tl.GetOutputTracks())
                if (t is CameraShotTrack)
                    list.AddRange(t.GetClips());
            list.Sort((a, b) => a.start.CompareTo(b.start));
            return list;
        }

        private static void AssertCameraCovers(TimelineAsset tl, double length)
        {
            var clips = CameraClips(tl);
            Assert.That(clips.Count, Is.GreaterThan(0));
            Assert.That(clips[0].start, Is.EqualTo(0d).Within(1e-6));
            var end = 0d;
            foreach (var c in clips)
            {
                Assert.That(c.start, Is.LessThanOrEqualTo(end + 0.02), "camera gap before " + c.displayName);
                end = System.Math.Max(end, c.end);
            }
            Assert.That(end, Is.EqualTo(length).Within(0.02), "camera until the end");
        }

        private static void AssertCutAt(TimelineAsset tl, double t, string what)
        {
            foreach (var c in CameraClips(tl))
                if (System.Math.Abs(c.start - t) <= 0.2) return;
            Assert.Fail("no cut at " + t.ToString("0.00") + " s (" + what + ")");
        }

        private static double LineStart(TimelineAsset tl, string lineId)
        {
            foreach (var t in tl.GetOutputTracks())
                foreach (var c in t.GetClips())
                    if (c.asset is VOLineClip vo && vo.LineId == lineId) return c.start;
            return -1;
        }

        private static void AssertCommonRules(TimelineAsset tl)
        {
            foreach (var track in tl.GetOutputTracks())
            {
                var id = TrackBindingResolver.BindingIdOf(track.name);
                if (id != null)
                    Assert.IsTrue(id.StartsWith("Booth") || id.StartsWith("Cam.") || id.StartsWith("Feed.") || id.StartsWith("Cast.") ||
                                  id.StartsWith("MidB.") || id.StartsWith("Squad.") || id.StartsWith("Marker.") || id.StartsWith("Player."),
                        "unknown binding id " + id);
                var clips = new List<TimelineClip>(track.GetClips());
                clips.Sort((a, b) => a.start.CompareTo(b.start));
                for (var i = 1; i < clips.Count; i++)
                    Assert.That(clips[i].start, Is.GreaterThanOrEqualTo(clips[i - 1].end - 1e-6),
                        track.name + ": " + clips[i - 1].displayName + " overlaps " + clips[i].displayName);
                foreach (var c in clips)
                    if (c.asset is VOLineClip vo)
                        Assert.IsTrue(Dialogue.Has(vo.LineId), vo.LineId + " in DIALOGUE.json");
            }
        }

        [Test]
        public void Intro_BoothShotsInsertionAndLandingFollowTheSpec()
        {
            var tl = Build(IntroTimeline.Length, IntroTimeline.Build);
            AssertCommonRules(tl);
            AssertCameraCovers(tl, IntroTimeline.Length);
            for (var i = 0; i < IntroTimeline.BoothShotStarts.Length; i++) AssertCutAt(tl, IntroTimeline.BoothShotStarts[i], "GDD §8.2 shot " + (i + 1));
            for (var i = 0; i < IntroTimeline.InsertionShotOffsets.Length; i++)
                AssertCutAt(tl, IntroTimeline.InsertionStart + IntroTimeline.InsertionShotOffsets[i], "I-" + (i + 1));
            AssertCutAt(tl, IntroTimeline.Shot9Start, "shot 9");
            Assert.That(LineStart(tl, "D001"), Is.EqualTo(25.0).Within(0.2), "GDD: D001 at 0:25");
            Assert.That(LineStart(tl, "D002"), Is.EqualTo(44.0).Within(0.2), "GDD: D002 at 0:44");
            Assert.That(LineStart(tl, "D005"), Is.EqualTo(68.0).Within(0.3), "GDD: D005 at 1:08");
            Assert.That(LineStart(tl, "D006"), Is.EqualTo(79.0).Within(0.2), "GDD: D006 at 1:19");
            Assert.That(LineStart(tl, "D206"), Is.EqualTo(IntroTimeline.InsertionStart + 44.0).Within(0.2), "D206 at I+0:44");
            Assert.That(LineStart(tl, "D207"), Is.EqualTo(IntroTimeline.InsertionStart + 49.0).Within(0.2), "D207 at I+0:49");
            // D003 waits for the tape line D002 to end.
            Assert.That(LineStart(tl, "D003"), Is.GreaterThanOrEqualTo(LineStart(tl, "D002") + Dialogue.DurationOf("D002")));
            foreach (var id in new[] { "D200", "D201", "D202", "D203", "D204", "D205" }) Assert.That(LineStart(tl, id), Is.GreaterThan(IntroTimeline.InsertionStart));
        }

        [Test]
        public void TitleCard_CuesTheHudAndHasAFallback()
        {
            var tl = Build(TitleCardTimeline.Length, TitleCardTimeline.Build);
            AssertCommonRules(tl);
            AssertCameraCovers(tl, TitleCardTimeline.Length);
            var hud = false;
            var type = false;
            foreach (var t in tl.GetOutputTracks())
                foreach (var c in t.GetClips())
                {
                    if (c.asset is CinematicEventClip e && e.Kind == CinematicEventKind.HudTitleCard) hud = true;
                    if (c.asset is TypewriterClip w && w.OnlyWithoutHud && w.NumberGroups) type = true;
                }
            Assert.IsTrue(hud, "IHud.TitleCard cue");
            Assert.IsTrue(type, "typed number-group fallback");
        }

        [Test]
        public void MidB_ShotsAndLinesFollowGdd86()
        {
            var tl = Build(MidBTimeline.Length, MidBTimeline.Build);
            AssertCommonRules(tl);
            AssertCameraCovers(tl, MidBTimeline.Length);
            foreach (var t in MidBTimeline.ShotStarts) AssertCutAt(tl, t, "GDD §8.6");
            Assert.That(LineStart(tl, "D054"), Is.EqualTo(36.5).Within(0.6), "D054 in I3 (0:36-0:40)");
            Assert.That(LineStart(tl, "D055"), Is.InRange(40.0, 45.0), "D055 in shot 8");
        }

        [Test]
        public void Climax_EndsInI4AndWhite()
        {
            var tl = Build(ClimaxTimeline.Length, ClimaxTimeline.Build);
            AssertCommonRules(tl);
            AssertCameraCovers(tl, ClimaxTimeline.Length);
            foreach (var t in ClimaxTimeline.ShotStarts) AssertCutAt(tl, t, "GDD §8.7");
            var i4 = false;
            foreach (var t in tl.GetOutputTracks())
                foreach (var c in t.GetClips())
                    if (c.asset is CinematicEventClip e && e.Kind == CinematicEventKind.PlayInsert && e.Id == LongShadow.Core.SequenceIds.InsertI4)
                    {
                        Assert.That(c.start, Is.EqualTo(10.0).Within(0.2), "I4 at 0:10");
                        i4 = true;
                    }
            Assert.IsTrue(i4);
            Assert.That(LineStart(tl, "D081"), Is.LessThan(3.0));
            Assert.That(LineStart(tl, "D082"), Is.InRange(6.0, 10.0));
        }

        [Test]
        public void Outro_LinesLandOnTheirShots()
        {
            var tl = Build(OutroTimeline.Length, OutroTimeline.Build);
            AssertCommonRules(tl);
            AssertCameraCovers(tl, OutroTimeline.Length);
            foreach (var t in OutroTimeline.ShotStarts) if (t < 102) AssertCutAt(tl, t, "GDD §8.8");
            Assert.That(LineStart(tl, "D083"), Is.EqualTo(4.0).Within(0.2));
            Assert.That(LineStart(tl, "D085"), Is.EqualTo(15.0).Within(0.2));
            Assert.That(LineStart(tl, "D086"), Is.EqualTo(27.0).Within(0.2));
            Assert.That(LineStart(tl, "D092"), Is.EqualTo(100.0).Within(0.2), "GDD: D092 at 1:40");
            var taps = 0;
            foreach (var t in tl.GetOutputTracks())
                foreach (var c in t.GetClips())
                    if (c.asset is BoothPropClip p && p.Kind == BoothPropClip.Action.PenTapOnce)
                    {
                        taps++;
                        Assert.That(c.start, Is.InRange(LineStart(tl, "D087"), LineStart(tl, "D087") + Dialogue.DurationOf("D087")));
                    }
            Assert.AreEqual(4, taps, "one pen tap per digit (0-6-4-0)");
        }
    }
}
