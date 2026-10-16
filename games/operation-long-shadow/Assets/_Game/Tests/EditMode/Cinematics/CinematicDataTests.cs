using System.Text;
using LongShadow.Cinematics;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.Editor;
using LongShadow.Cinematics.Playables;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Cinematics
{
    /// <summary>Sequence table, typewriter, paths, bindings, booth layout vs markup_ids.csv, shell meshes.</summary>
    public sealed class CinematicDataTests
    {
        [TearDown]
        public void TearDown()
        {
            CinematicBindings.ClearForTests();
            SeenFlags.ClearMemoryForTests();
        }

        [Test]
        public void Sequences_CoverEveryCinematicId()
        {
            foreach (var id in new[] { SequenceIds.Intro, SequenceIds.TitleCard, SequenceIds.MidB, SequenceIds.Climax, SequenceIds.Outro })
                Assert.IsNotNull(CinematicSequences.Get(id), id);
            var intro = CinematicSequences.Get(SequenceIds.Intro);
            Assert.AreEqual(SkipRule.Always, intro.Skip, "CINEMATIC_DIRECTION §2: skippable from the first viewing");
            Assert.AreEqual(SequenceIds.TitleCard, intro.Next, "the title card always plays");
            Assert.AreEqual(SequenceIds.IntroControlToPlayer, intro.EndEvent);
            Assert.AreEqual(SkipRule.Never, CinematicSequences.Get(SequenceIds.TitleCard).Skip);
            Assert.IsFalse(CinematicSequences.Get(SequenceIds.MidB).CanSkip(false), "GDD §8: skip only after the first viewing");
            Assert.IsTrue(CinematicSequences.Get(SequenceIds.MidB).CanSkip(true));
            Assert.AreEqual(EndScreen.HoldWhite, CinematicSequences.Get(SequenceIds.Climax).End);
            Assert.AreEqual(EndScreen.HoldBlack, CinematicSequences.Get(SequenceIds.Outro).End);
        }

        [Test]
        public void Sequences_LengthsMatchShotTables()
        {
            Assert.That(CinematicSequences.IntroSeconds, Is.EqualTo((float)IntroTimeline.Length).Within(1e-4f));
            Assert.That(CinematicSequences.TitleCardSeconds, Is.EqualTo((float)TitleCardTimeline.Length).Within(1e-4f));
            Assert.That(CinematicSequences.MidBSeconds, Is.EqualTo((float)MidBTimeline.Length).Within(1e-4f));
            Assert.That(CinematicSequences.ClimaxSeconds, Is.EqualTo((float)ClimaxTimeline.Length).Within(1e-4f));
            Assert.That(CinematicSequences.OutroSeconds, Is.EqualTo((float)OutroTimeline.Length).Within(1e-4f));
            Assert.That(IntroTimeline.InsertionLength, Is.EqualTo(55.0).Within(1e-6), "CINEMATIC_DIRECTION §2: about 55 s");
            Assert.That(IntroTimeline.Length + TitleCardTimeline.Length, Is.EqualTo(158.0).Within(1.0), "intro grows to about 2:38");
        }

        [Test]
        public void Typewriter_NumberGroupsCorrectIntoText()
        {
            var model = new TypewriterModel(TitleCardTimeline.Text, 34f, true);
            for (var i = 0; i < model.LineCount; i++)
            {
                var numbers = model.NumbersOf(i);
                foreach (var c in numbers) Assert.IsTrue(c == ' ' || char.IsDigit(c), "number groups only");
            }
            var sb = new StringBuilder();
            model.Build(sb, model.Duration + 1f);
            Assert.AreEqual(TitleCardTimeline.Text, sb.ToString());
            model.Build(sb, model.LineStart(0) + 0.2f);
            Assert.IsTrue(char.IsDigit(sb[0]), "a line first prints as numbers");
            Assert.That(model.TypedCount(0.5f), Is.LessThanOrEqualTo(model.TypedCount(1.5f)));
        }

        [Test]
        public void Typewriter_TitleFlickerShows1840Briefly()
        {
            var model = new TypewriterModel(TitleCardTimeline.Text, 34f, true);
            model.SetFlicker(2, "17:58", "18:40", 0.05f, 0.1f);
            var sb = new StringBuilder();
            var lines = TitleCardTimeline.Text.Split('\n');
            var line3Done = model.LineStart(2) + lines[2].Length / 34f + 0.4f;
            model.Build(sb, line3Done + 0.1f);
            StringAssert.Contains("18:40", sb.ToString());
            model.Build(sb, line3Done + 0.5f);
            StringAssert.Contains("17:58", sb.ToString());
            StringAssert.DoesNotContain("18:40", sb.ToString());
        }

        [Test]
        public void PathCurve_EndsOnKnotsAndMovesAtConstantSpeed()
        {
            var curve = new PathCurve(LongShadow.Cinematics.Sets.InsertionLayout.Approach);
            Assert.That(Vector3.Distance(curve.Sample(0f), LongShadow.Cinematics.Sets.InsertionLayout.Approach[0]), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(curve.Sample(1f), LongShadow.Cinematics.Sets.InsertionLayout.LandingZone), Is.LessThan(0.01f));
            var step = curve.Length / 50f;
            var prev = curve.Sample(0f);
            for (var i = 1; i <= 50; i++)
            {
                var p = curve.Sample(i / 50f);
                Assert.That(Vector3.Distance(prev, p), Is.EqualTo(step).Within(step * 0.08f), "arc-length parameterised");
                prev = p;
            }
        }

        [Test]
        public void Bindings_ParseTrackNamesAndResolveRegisteredObjects()
        {
            Assert.AreEqual("Booth.CRT2", TrackBindingResolver.BindingIdOf("CRT2 @Booth.CRT2"));
            Assert.IsNull(TrackBindingResolver.BindingIdOf("Camera"));
            Assert.AreEqual("Tube @Booth.Tube", TrackBindingResolver.TrackName("Tube", BindingIds.Tube));
            var go = new GameObject("BindingTest");
            try
            {
                var light = go.AddComponent<Light>();
                CinematicBindings.Register("Test.Light", go);
                Assert.AreSame(light, CinematicBindings.Resolve<Light>("Test.Light"));
                Assert.AreSame(go.transform, CinematicBindings.ResolveTransform("Test.Light"));
                Assert.AreSame(light, CinematicBindings.ResolveAs("Test.Light", typeof(Light)));
                CinematicBindings.Unregister("Test.Light", go);
                Assert.IsFalse(CinematicBindings.IsRegistered("Test.Light"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SeenFlags_IntroUsesItsStorageKey()
        {
            Assert.AreEqual(StorageKeys.IntroSeen, SeenFlags.KeyOf(SequenceIds.Intro));
            Assert.AreEqual(SeenFlags.Prefix + SequenceIds.Outro, SeenFlags.KeyOf(SequenceIds.Outro));
            Assert.IsFalse(SeenFlags.HasSeen("UnitTestSequence"));
            SeenFlags.MarkSeen("UnitTestSequence");
            Assert.IsTrue(SeenFlags.HasSeen("UnitTestSequence"));
        }

        [Test]
        public void BoothLayout_MatchesMarkupCsvWhereNotMoved()
        {
            var table = MarkupIds.Load();
            Assume.That(table, Is.Not.Null, "markup_ids.csv not found");
            void Check(string id, Vector3 expected)
            {
                Assert.IsTrue(table.TryGet(id, out var row), id + " in markup_ids.csv");
                Assert.IsTrue(row.HasPosition, id + " has a position");
                Assert.That(Vector3.Distance(row.Position, expected), Is.LessThan(0.011f), id);
            }
            Check("BOOTH.C_POV", BoothLayout.Pov);
            Check("BOOTH.C_CLOCK", BoothLayout.ClockCamera);
            for (var i = 0; i < 6; i++) Check("BOOTH.CRT" + (i + 1), BoothLayout.Crt[i]);
            Check("BOOTH.Clock", BoothLayout.Clock);
            Check("BOOTH.Tube", BoothLayout.Tube);
            Check("BOOTH.Table", BoothLayout.Table);
            Check("BOOTH.Pen", BoothLayout.Pen);
            Check("BOOTH.Smoke", BoothLayout.Smoke);
            Check("BOOTH.FilmCam", BoothLayout.FilmCamLens);
            BoothLayout.Preset(BoothCameraPreset.Pov, out var p, out _, out var fov);
            Assert.AreEqual(BoothLayout.Pov, p);
            Assert.That(fov, Is.EqualTo(50f), "GDD §8.0: C_POV vertical FOV 50°");
        }

        [Test]
        public void BoothMeshes_PanelsFaceTheirNormalAndCutHoles()
        {
            var cases = new[]
            {
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.right, Vector3.forward, Vector3.down),
                (Vector3.right, Vector3.up, Vector3.back), (Vector3.right, Vector3.up, Vector3.forward),
                (Vector3.forward, Vector3.up, Vector3.right), (Vector3.forward, Vector3.up, Vector3.left),
            };
            foreach (var (u, v, n) in cases)
            {
                var mesh = BoothMeshes.Panel("T", Vector3.zero, u, v, n, 0f, 2f, 0f, 1f);
                mesh.RecalculateNormals();
                foreach (var normal in mesh.normals) Assert.That(Vector3.Dot(normal, n), Is.GreaterThan(0.99f), "winding for " + n);
                Object.DestroyImmediate(mesh);
            }
            var holed = BoothMeshes.Panel("H", Vector3.zero, Vector3.right, Vector3.up, Vector3.back, 0f, 3f, 0f, 3f,
                new[] { new Rect(1f, 1f, 1f, 1f) });
            Assert.AreEqual(8 * 6, holed.triangles.Length, "3x3 grid minus the hole = 8 quads");
            Object.DestroyImmediate(holed);
        }
    }
}
