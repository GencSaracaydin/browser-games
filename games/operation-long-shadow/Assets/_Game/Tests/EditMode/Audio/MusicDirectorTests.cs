using LongShadow.Audio.Core;
using LongShadow.Audio.Music;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.3 / TECH_PLAN §7.9: layered cues, bar-synced layer changes, cue changes, stingers, hard cut, checkpoint.</summary>
    public class MusicDirectorTests
    {
        private GameObject _go;
        private MusicDirector _music;
        private AudioLibrary _lib;
        private float _now;

        private static AudioCue Cue(string id, float seconds, bool loop)
        {
            var c = new AudioCue(id, AudioCategories.Music, new[] { AudioTestUtil.Clip(id, seconds, 2) });
            if (loop)
            {
                const float start = 2048f / 48000f;
                c.WithLoop(true, new[] { new Vector2(start, start + 33.3335f) }).WithMusicGrid(72f, 3.33335f, 10, false);
            }
            return c;
        }

        [SetUp]
        public void SetUp()
        {
            AudioRuntime.ResetForTests();
            _now = 50f;
            AudioRuntime.SetClockForTests(() => _now);
            _lib = ScriptableObject.CreateInstance<AudioLibrary>();
            _lib.EditorSetEntries(new[]
            {
                Cue("MUS_02_MARKET_LO", 34f, true), Cue("MUS_02_MARKET_HI", 34f, true), Cue("MUS_05_EXFIL", 34f, true),
                Cue("MUS_03_LASTLIGHT", 10f, false), Cue("STG_GREYCOAT", 3f, false),
            });
            _go = new GameObject("MusicTest");
            _music = _go.AddComponent<MusicDirector>();
            _music.Initialize();
            _music.SetLibrary(_lib);
        }

        [TearDown]
        public void TearDown()
        {
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
                _music.Tick(dt);
            }
        }

        [Test]
        public void LayeredCueStartsBothLayersWithOneAudible()
        {
            _music.PlayCue("MUS_02_MARKET", 0, 0.5f);
            Assert.That(_music.CurrentCue, Is.EqualTo("MUS_02_MARKET"));
            Assert.That(_music.BusyLayers, Is.EqualTo(2), "LO and HI start together on one DSP time");
            Run(1f);
            Assert.That(_music.LayerGain(0), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(_music.LayerGain(1), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void LayerChangesWaitForTheBarLine()
        {
            _music.PlayCue("MUS_02_MARKET", 0, 0.1f);
            Run(1f); // 1 s into the cue: the next bar line is ~2.4 s away
            _music.SetLayer(1, 0.5f);
            Assert.That(_music.CurrentLayer, Is.EqualTo(1));
            Run(1f);
            Assert.That(_music.LayerGain(0), Is.EqualTo(1f).Within(1e-3f), "nothing moves before the bar line");
            Run(3f);
            Assert.That(_music.LayerGain(0), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_music.LayerGain(1), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void CueChangeCrossfadesAndFreesTheOldLayers()
        {
            _music.PlayCue("MUS_02_MARKET", 1, 0.2f);
            Run(1f);
            _music.PlayCue("MUS_05_EXFIL", 0, 1f);
            Assert.That(_music.CurrentCue, Is.EqualTo("MUS_05_EXFIL"));
            Run(4f);
            Assert.That(_music.BusyLayers, Is.EqualTo(1));
            Assert.That(_music.LayerGain(0), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void HardCutAndStop()
        {
            _music.PlayCue("MUS_05_EXFIL", 0, 0.2f);
            Run(0.5f);
            _music.HardCut();
            Assert.That(_music.BusyLayers, Is.EqualTo(0), "the RPG launch cuts the score dead (GDD §8.7)");
            Assert.That(_music.CurrentCue, Is.Null);
            _music.PlayCue("MUS_05_EXFIL", 0, 0.2f);
            Run(0.5f);
            _music.Stop(1f);
            Assert.That(_music.CurrentCue, Is.Null);
            Run(1.5f);
            Assert.That(_music.BusyLayers, Is.EqualTo(0));
        }

        [Test]
        public void StingerDucksTheRunningCue()
        {
            _music.PlayCue("MUS_02_MARKET", 1, 0.2f);
            Run(0.5f);
            _music.Stinger("STG_GREYCOAT");
            Run(0.5f);
            Assert.That(_music.LayerDuck, Is.LessThan(0.6f), "−6 dB under STG_GREYCOAT");
        }

        [Test]
        public void CheckpointRestoresCueAndLayer()
        {
            _music.PlayCue("MUS_02_MARKET", 1, 0.2f);
            Run(0.5f);
            var record = new SaveRecord { Key = _music.SaveKey };
            _music.Capture(new SaveWriter(record));
            _music.PlayCue("MUS_05_EXFIL", 0, 0.2f);
            Run(1f);
            _music.Restore(new SaveReader(record));
            Assert.That(_music.CurrentCue, Is.EqualTo("MUS_02_MARKET"));
            Assert.That(_music.CurrentLayer, Is.EqualTo(1));
        }

        [Test]
        public void UnknownCueIsIgnored()
        {
            _music.PlayCue("MUS_99_NOPE", 0, 1f);
            Assert.That(_music.CurrentCue, Is.Null);
            Assert.That(_music.BusyLayers, Is.EqualTo(0));
        }
    }
}
