using System.Collections.Generic;
using LongShadow.Audio.Music;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.3: layered cues are addressed by their base id and a layer index (WP07 MissionCues).</summary>
    public class MusicCueResolverTests
    {
        private static readonly HashSet<string> s_have = new HashSet<string>
        {
            "MUS_01_SESSION", "MUS_02_MARKET_LO", "MUS_02_MARKET_HI", "MUS_04_STATION_LOW", "MUS_04_STATION_FULL", "MUS_05_EXFIL",
        };

        [Test]
        public void BaseIdsResolveToLayers()
        {
            Assert.That(MusicCueResolver.Resolve("MUS_02_MARKET", s_have.Contains, out var b, out var layers, out var hint), Is.True);
            Assert.That(b, Is.EqualTo("MUS_02_MARKET"));
            Assert.That(layers, Is.EqualTo(new[] { "MUS_02_MARKET_LO", "MUS_02_MARKET_HI" }));
            Assert.That(hint, Is.EqualTo(-1));
            Assert.That(MusicCueResolver.Resolve("MUS_04_STATION", s_have.Contains, out _, out layers, out _), Is.True);
            Assert.That(layers, Is.EqualTo(new[] { "MUS_04_STATION_LOW", "MUS_04_STATION_FULL" }));
        }

        [Test]
        public void LayerIdsResolveToTheirBase()
        {
            Assert.That(MusicCueResolver.Resolve("MUS_02_MARKET_HI", s_have.Contains, out var b, out var layers, out var hint), Is.True);
            Assert.That(b, Is.EqualTo("MUS_02_MARKET"));
            Assert.That(layers.Length, Is.EqualTo(2));
            Assert.That(hint, Is.EqualTo(1));
        }

        [Test]
        public void SingleCuesAndMisses()
        {
            Assert.That(MusicCueResolver.Resolve("MUS_05_EXFIL", s_have.Contains, out _, out var layers, out _), Is.True);
            Assert.That(layers, Is.EqualTo(new[] { "MUS_05_EXFIL" }));
            Assert.That(MusicCueResolver.Resolve("MUS_99", s_have.Contains, out _, out _, out _), Is.False);
            Assert.That(MusicCueResolver.Resolve(null, s_have.Contains, out _, out _, out _), Is.False);
        }

        [Test]
        public void EveryBeatOfThePacingTableHasItsCue()
        {
            Assert.That(MusicWindows.CuesOf(0), Does.Contain("MUS_01_SESSION"));
            Assert.That(MusicWindows.CuesOf(3), Does.Contain("MUS_02_MARKET"));
            Assert.That(MusicWindows.CuesOf(5), Does.Contain("MUS_03_LASTLIGHT"));
            Assert.That(MusicWindows.CuesOf(9), Does.Contain("MUS_04_STATION"));
            Assert.That(MusicWindows.CuesOf(11), Does.Contain("MUS_05_EXFIL"));
            Assert.That(MusicWindows.CuesOf(12), Does.Contain("MUS_06_SIXFORTY"));
            Assert.That(MusicWindows.CuesOf(1), Is.Empty, "Beat 1 is ambience only (GDD §7.0)");
            Assert.That(MusicWindows.Always, Does.Contain("STG_DEATH"));
        }
    }
}
