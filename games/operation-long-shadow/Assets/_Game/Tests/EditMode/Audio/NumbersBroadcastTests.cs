using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §7.9: the basement broadcast loops from the room speaker and cuts to D066 on the fourth rack.</summary>
    public class NumbersBroadcastTests
    {
        private FakeAudioService _audio;
        private FakeVOService _vo;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            AudioRuntime.ResetForTests();
            _audio = new FakeAudioService();
            _vo = new FakeVOService();
            GameServices.Register<IAudioService>(_audio);
            GameServices.Register<IVOService>(_vo);
            _go = new GameObject("Numbers");
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Unregister<IAudioService>(_audio);
            GameServices.Unregister<IVOService>(_vo);
            Object.DestroyImmediate(_go);
            AudioRuntime.ResetForTests();
        }

        [Test]
        public void LoopThenCutLineFromTheSpeaker()
        {
            var n = _go.AddComponent<NumbersBroadcast>();
            n.Begin();
            Assert.That(n.IsRunning, Is.True);
            Assert.That(_audio.Loops, Is.EqualTo(new[] { "NUM_BASEMENT_LOOP" }));
            AudioRuntime.Mix.Tick(0f, 0.01f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Music), Is.LessThan(-5f), "MUS_04 ducks under the broadcast");
            n.Begin();
            Assert.That(_audio.Loops.Count, Is.EqualTo(1), "idempotent");
            n.Cut();
            Assert.That(n.IsRunning, Is.False);
            Assert.That(_audio.Stopped.Count, Is.EqualTo(1));
            Assert.That(_vo.Played, Is.EqualTo(new[] { "D066" }));
            Assert.That(_vo.Options[0].Speaker, Is.Not.Null, "D066 comes from the room speaker");
            AudioRuntime.Mix.Tick(1f, 0.01f);
            Assert.That(AudioRuntime.Mix.TargetDb(MixCategory.Music), Is.EqualTo(0f).Within(0.01f));
        }
    }
}
