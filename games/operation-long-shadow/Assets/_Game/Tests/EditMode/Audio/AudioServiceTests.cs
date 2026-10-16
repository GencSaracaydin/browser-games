using LongShadow.Audio.Core;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.9, D9: pooled voices, the audible cap with stealing, loops, far takes, the listener, ducking.</summary>
    public class AudioServiceTests
    {
        private GameObject _go;
        private AudioService _svc;
        private AudioLibrary _lib;
        private float _now;
        private AudioClip _near;
        private AudioClip _far;

        [SetUp]
        public void SetUp()
        {
            AudioRuntime.ResetForTests();
            _now = 10f;
            AudioRuntime.SetClockForTests(() => _now);
            AudioRuntime.SetListener(Vector3.zero, Quaternion.identity);
            _near = AudioTestUtil.Clip("near", 1f);
            _far = AudioTestUtil.Clip("far", 1.5f);
            _lib = ScriptableObject.CreateInstance<AudioLibrary>();
            _lib.EditorSetEntries(new[]
            {
                new AudioCue("SFX_LOW", AudioCategories.Effects, new[] { AudioTestUtil.Clip("low", 2f) }).With3D(1f, 1f, 50f).WithLevels(1f, 0f, 0f, 10),
                new AudioCue("SFX_HIGH", AudioCategories.Effects, new[] { AudioTestUtil.Clip("high", 2f) }).With3D(1f, 1f, 50f).WithLevels(1f, 0f, 0f, 95),
                new AudioCue("SFX_SHOT", AudioCategories.Effects, new[] { _near }).With3D(1f, 6f, 250f).WithLevels(1f, 0f, 0f, 80)
                    .WithFar(new[] { _far }, new Vector2(40f, 120f), 30f, 1500f).WithLimits(true, 0, 0f),
                new AudioCue("SFX_LIMITED", AudioCategories.Effects, new[] { AudioTestUtil.Clip("lim", 2f) }).With3D(1f, 1f, 50f).WithLimits(false, 2, 0f),
                new AudioCue("SFX_LOOP", AudioCategories.Effects, new[] { AudioTestUtil.Clip("loop", 5f) }).With3D(1f, 1f, 30f)
                    .WithLoop(true, new[] { new Vector2(0.1f, 4.9f) }),
            });
            _go = new GameObject("AudioServiceTest");
            _svc = _go.AddComponent<AudioService>();
            _svc.SetLibrary(_lib);
            _svc.Initialize();
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

        private bool Has(string cue) => _svc.TryGetCue(cue, out var c) && c != null;

        [Test]
        public void OwnsTheOnlyListener()
        {
            Assert.That(_svc.ListenerTransform, Is.Not.Null);
            Assert.That(_go.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void KnownAndUnknownCues()
        {
            Assert.That(_svc.HasCue("SFX_LOW"), Is.True);
            Assert.That(_svc.HasCue("SFX_NOPE"), Is.False);
            Assert.DoesNotThrow(() => _svc.Play("SFX_NOPE", Vector3.zero));
            Assert.DoesNotThrow(() => _svc.Play2D(null));
            Assert.That(_svc.AudibleCount, Is.EqualTo(0));
        }

        [Test]
        public void VoiceCapStealsLowPriorityForHighPriority()
        {
            for (var i = 0; i < 40; i++) _svc.Play("SFX_LOW", new Vector3(2f, 0f, 0f));
            Assert.That(_svc.AudibleCount, Is.LessThanOrEqualTo(AudioConstants.AudibleVoices));
            Assert.That(Has("SFX_HIGH"), Is.True);
            _svc.TryGetCue("SFX_HIGH", out var high);
            var h = _svc.PlayCue(high, new Vector3(2f, 0f, 0f), null, false, 1f, 0);
            Assert.That(h, Is.Not.EqualTo(0), "an important sound always finds a voice");
            Assert.That(_svc.IsHandleActive(h), Is.True);
        }

        [Test]
        public void FarTakeReplacesTheNearOneWithDistance()
        {
            _svc.TryGetCue("SFX_SHOT", out var shot);
            var hNear = _svc.PlayCue(shot, new Vector3(10f, 0f, 0f), null, false, 1f, 0);
            Assert.That(_svc.FindVoice(hNear).Source.clip, Is.EqualTo(_near));
            var hFar = _svc.PlayCue(shot, new Vector3(300f, 0f, 0f), null, false, 1f, 0);
            Assert.That(_svc.FindVoice(hFar).Source.clip, Is.EqualTo(_far), "beyond 120 m only the baked far take plays");
            Assert.That(_svc.FindVoice(hFar).EndsAt - _now, Is.GreaterThan(300f / AudioConstants.SpeedOfSound), "it is heard after it is seen");
        }

        [Test]
        public void MaxInstancesStopsTheOldest()
        {
            _svc.TryGetCue("SFX_LIMITED", out var c);
            var a = _svc.PlayCue(c, Vector3.zero, null, false, 1f, 0);
            _now += 0.1f;
            var b = _svc.PlayCue(c, Vector3.zero, null, false, 1f, 0);
            _now += 0.1f;
            var d = _svc.PlayCue(c, Vector3.zero, null, false, 1f, 0);
            Assert.That(_svc.IsHandleActive(a), Is.False);
            Assert.That(_svc.IsHandleActive(b) && _svc.IsHandleActive(d), Is.True);
        }

        [Test]
        public void LoopsStartAndStop()
        {
            var h = _svc.PlayLoop("SFX_LOOP", null);
            Assert.That(h, Is.Not.EqualTo(0));
            Assert.That(_svc.IsHandleActive(h), Is.True);
            _svc.StopLoop(h, 0f);
            Assert.That(_svc.IsHandleActive(h), Is.False);
            Assert.That(_svc.PlayLoop("SFX_NOPE", null), Is.EqualTo(0));
        }

        [Test]
        public void ListenerOverrideMovesTheListener()
        {
            var cam = AudioTestUtil.At("cinecam", new Vector3(5f, 2f, 7f));
            _svc.SetListenerOverride(cam);
            Assert.That(_svc.ListenerTransform.position, Is.EqualTo(cam.position));
            Assert.That(AudioRuntime.ListenerPosition, Is.EqualTo(cam.position));
            _svc.SetListenerOverride(null);
        }

        [Test]
        public void TinnitusAndDucksGoThroughTheMix()
        {
            _svc.Tinnitus(2f);
            AudioRuntime.Mix.Tick(_now, 0.01f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Effects), Is.EqualTo(-18f).Within(0.01f));
            _now += 2.1f;
            AudioRuntime.Mix.Tick(_now, 0.01f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Effects), Is.EqualTo(0f).Within(0.01f));
            _svc.Duck(AudioCategories.Music, -8f, 1f);
            AudioRuntime.Mix.Tick(_now, 0.01f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Music), Is.EqualTo(-8f).Within(0.01f));
            _svc.SetCategoryGain(AudioCategories.Voice, 0.5f);
            Assert.That(AudioRuntime.Mix.ScriptGain(MixCategory.Voice), Is.EqualTo(0.5f));
        }
    }
}
