using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.10: VO is decoded per beat window.</summary>
    public class BeatWindowTests
    {
        [Test]
        public void LoadsCurrentAndNextUnloadsPassedKeepsFuture()
        {
            Assert.That(BeatWindow.Decide(5, 5, 6), Is.EqualTo(BeatWindow.Action.Load));
            Assert.That(BeatWindow.Decide(6, 5, 6), Is.EqualTo(BeatWindow.Action.Load));
            Assert.That(BeatWindow.Decide(4, 5, 6), Is.EqualTo(BeatWindow.Action.Unload));
            Assert.That(BeatWindow.Decide(9, 5, 6), Is.EqualTo(BeatWindow.Action.Keep));
            Assert.That(BeatWindow.Decide(AudioConstants.AlwaysBeat, 5, 6), Is.EqualTo(BeatWindow.Action.Load));
            Assert.That(BeatWindow.Decide(5, 6, 5), Is.EqualTo(BeatWindow.Action.Load), "a reversed window is normalised");
        }
    }
}
