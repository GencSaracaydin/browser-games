using System.Collections.Generic;
using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>
    /// WP06 DoD VOManager tests (TECH_PLAN §7.10, GDD §10.1, §11, §17): priority order, interruption, stale callouts,
    /// no repeat after a checkpoint, the bark subtitle rule, subtitles, overlapping channels, envelopes and ducking.
    /// Driven in edit mode with a test clock.
    /// </summary>
    public class VOManagerTests
    {
        private GameObject _go;
        private VOManager _vo;
        private DialogueLibrary _lib;
        private float _now;
        private readonly List<string> _started = new List<string>();
        private readonly List<string> _finished = new List<string>();
        private readonly List<SubtitleData> _shown = new List<SubtitleData>();
        private readonly List<int> _hidden = new List<int>();

        private void OnStarted(string line, string speaker) => _started.Add(line);
        private void OnFinished(string line) => _finished.Add(line);
        private void OnShow(SubtitleData d) => _shown.Add(d);
        private void OnHide(int h) => _hidden.Add(h);

        [SetUp]
        public void SetUp()
        {
            AudioRuntime.ResetForTests();
            _now = 100f;
            AudioRuntime.SetClockForTests(() => _now);
            AudioRuntime.SetListener(Vector3.zero, Quaternion.identity);
            _go = new GameObject("VOManagerTest");
            _vo = _go.AddComponent<VOManager>();
            _vo.Initialize();
            _lib = ScriptableObject.CreateInstance<DialogueLibrary>();
            _lib.EditorSetEntries(new[]
            {
                AudioTestUtil.Silent("X1", SpeakerIds.Hollis, 1f),
                AudioTestUtil.Silent("S2", SpeakerIds.Sparrow, 1f),
                AudioTestUtil.Silent("O1", SpeakerIds.Hollis, 1f, priority: VOPriority.Objective, critical: true),
                AudioTestUtil.Silent("C1", SpeakerIds.Tinker, 2f, priority: VOPriority.Callout, repeatable: true),
                AudioTestUtil.Silent("LONG", SpeakerIds.Hollis, 3f),
                AudioTestUtil.Silent("BOOTH1", SpeakerIds.Interrogator, 2f, channel: VOChannel.Booth),
                AudioTestUtil.Silent("BK1", SpeakerIds.Legion, 1f, channel: VOChannel.Bark, priority: VOPriority.Bark, repeatable: true,
                                     subtitle: "(shouting) Push! Push!"),
                AudioTestUtil.Line("AUD", SpeakerIds.Hollis, new[] { VOChains.Dry, VOChains.Radio }, seconds: 1f),
            });
            _vo.SetLibrary(_lib);
            GameEvents.VOLineStarted += OnStarted;
            GameEvents.VOLineFinished += OnFinished;
            GameEvents.SubtitleShow += OnShow;
            GameEvents.SubtitleHide += OnHide;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.VOLineStarted -= OnStarted;
            GameEvents.VOLineFinished -= OnFinished;
            GameEvents.SubtitleShow -= OnShow;
            GameEvents.SubtitleHide -= OnHide;
            _started.Clear();
            _finished.Clear();
            _shown.Clear();
            _hidden.Clear();
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_lib);
            AudioTestUtil.Cleanup();
            AudioRuntime.SetClockForTests(null);
            AudioRuntime.ResetForTests();
        }

        private void Run(float seconds)
        {
            const float dt = 0.05f;
            for (var t = 0f; t < seconds; t += dt)
            {
                _now += dt;
                _vo.Tick(_now, dt);
            }
        }

        [Test]
        public void ScriptedBeforeObjectiveOnTheNet()
        {
            _vo.Play("X1");
            _vo.Play("O1");
            _vo.Play("S2");
            Run(4f);
            Assert.That(_started, Is.EqualTo(new[] { "X1", "S2", "O1" }));
            Assert.That(_finished, Is.EqualTo(new[] { "X1", "S2", "O1" }));
        }

        [Test]
        public void ScriptedLineCutsACallout()
        {
            _vo.Play("C1");
            Run(0.2f);
            Assert.That(_vo.CurrentLine(VOChannel.Net), Is.EqualTo("C1"));
            _vo.Play("X1");
            Run(0.3f);
            Assert.That(_finished, Does.Contain("C1"));
            Assert.That(_vo.CurrentLine(VOChannel.Net), Is.EqualTo("X1"));
        }

        [Test]
        public void StaleCalloutsAreDropped()
        {
            _vo.Play("LONG");
            _vo.Play("C1");
            Run(5f);
            Assert.That(_started, Is.EqualTo(new[] { "LONG" }), "a callout that waited > 1.5 s is wrong by the time it plays");
        }

        [Test]
        public void RequestsOfAPlayingLineMerge()
        {
            var a = _vo.Play("LONG");
            var b = _vo.Play("LONG");
            Assert.That(a, Is.Not.EqualTo(0));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(_vo.IsPlaying("LONG"), Is.True);
            Assert.That(_vo.IsNetBusy, Is.True);
            Run(3.5f);
            Assert.That(_vo.IsPlaying("LONG"), Is.False);
            Assert.That(_vo.Play("NOPE"), Is.EqualTo(0));
        }

        [Test]
        public void NoRepeatAfterCheckpointButCriticalRepeats()
        {
            _vo.Play("X1");
            _vo.Play("O1");
            Run(3f);
            var record = new SaveRecord { Key = _vo.SaveKey };
            _vo.Capture(new SaveWriter(record));
            _vo.Play("S2"); // after the checkpoint
            Run(1.5f);
            _vo.Restore(new SaveReader(record));
            Run(0.5f);
            Assert.That(_vo.HasPlayed("X1"), Is.True);
            Assert.That(_vo.HasPlayed("S2"), Is.False);
            Assert.That(_vo.Play("X1"), Is.EqualTo(0), "spent before the checkpoint");
            Assert.That(_vo.Play("O1"), Is.Not.EqualTo(0), "objective-critical lines repeat (GDD §17)");
            Assert.That(_vo.Play("S2"), Is.Not.EqualTo(0), "lines after the checkpoint play again");
        }

        [Test]
        public void BarkSubtitlesOnlyWithin25m()
        {
            var near = AudioTestUtil.At("near", new Vector3(10f, 0f, 0f));
            var far = AudioTestUtil.At("far", new Vector3(40f, 0f, 0f));
            _vo.PlayBark("BK1", near, Team.Legion);
            Assert.That(_shown.Count, Is.EqualTo(1));
            Assert.That(_shown[0].IsBark, Is.True);
            Assert.That(_shown[0].Text, Is.EqualTo("(shouting) Push! Push!"));
            Run(1.5f);
            _vo.PlayBark("BK1", far, Team.Legion);
            Assert.That(_shown.Count, Is.EqualTo(1), "no subtitle beyond 25 m");
            Assert.That(_started.Count, Is.EqualTo(2), "but the bark plays");
        }

        [Test]
        public void SubtitlesShowAndHide()
        {
            var h = _vo.Play("X1");
            Assert.That(_shown.Count, Is.EqualTo(1));
            Assert.That(_shown[0].Handle, Is.EqualTo(h));
            Assert.That(_shown[0].Speaker, Is.EqualTo("HOLLIS"));
            Run(2f);
            Assert.That(_hidden, Does.Contain(h));
        }

        [Test]
        public void BoothAndNetOverlap()
        {
            _vo.Play("LONG");
            _vo.Play("BOOTH1");
            Run(0.2f);
            Assert.That(_vo.CurrentLine(VOChannel.Net), Is.EqualTo("LONG"));
            Assert.That(_vo.CurrentLine(VOChannel.Booth), Is.EqualTo("BOOTH1"));
        }

        [Test]
        public void EnvelopeDrivesAmplitudeAndVoiceDucksMusic()
        {
            _vo.Play("AUD");
            Run(0.05f);
            Assert.That(_vo.Amplitude(SpeakerIds.Hollis), Is.GreaterThan(0.5f));
            Assert.That(_vo.Amplitude(SpeakerIds.Sparrow), Is.EqualTo(0f));
            AudioRuntime.Mix.Tick(_now, 0.05f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Music), Is.EqualTo(AudioConstants.VOMusicDuckDb).Within(0.01f));
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Ambience), Is.EqualTo(AudioConstants.VOAmbienceDuckDb).Within(0.01f));
            Run(1.5f);
            AudioRuntime.Mix.Tick(_now, 0.05f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Music), Is.EqualTo(0f).Within(0.01f), "released after the line");
        }

        [Test]
        public void DurationAndHasLine()
        {
            Assert.That(_vo.HasLine("LONG"), Is.True);
            Assert.That(_vo.HasLine("NOPE"), Is.False);
            Assert.That(_vo.Duration("LONG"), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(_vo.Duration("AUD"), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void StopAllClearsEverything()
        {
            _vo.Play("LONG");
            _vo.Play("X1");
            _vo.StopAll(0f);
            Run(0.2f);
            Assert.That(_vo.BusyVoices, Is.EqualTo(0));
            Assert.That(_vo.IsPlaying("X1"), Is.False);
        }
    }
}
