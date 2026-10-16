using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.10: how each line is heard (dry/radio, PA horns, anchors, G9, barks).</summary>
    public class VOChainSelectorTests
    {
        [TearDown]
        public void TearDown() => AudioTestUtil.Cleanup();

        [Test]
        public void SquadLinesCrossfadeWithASpeaker()
        {
            var l = AudioTestUtil.Line("D021", SpeakerIds.Hollis, new[] { VOChains.Dry, VOChains.Radio });
            var p = VOChainSelector.Select(l, default, false, true, 10f, 1);
            Assert.That(p.Mode, Is.EqualTo(VOPlanMode.DryRadio));
            Assert.That(p.Dry.Chain, Is.EqualTo(VOChains.Dry));
            Assert.That(p.Radio.Chain, Is.EqualTo(VOChains.Radio));
            Assert.That(p.IsRadio, Is.False, "10 m is mostly dry");
            Assert.That(VOChainSelector.Select(l, default, false, true, 20f, 1).IsRadio, Is.True);
        }

        [Test]
        public void RadioWithoutSpeakerOrFilterOrForce()
        {
            var l = AudioTestUtil.Line("D021", SpeakerIds.Hollis, new[] { VOChains.Dry, VOChains.Radio });
            var p = VOChainSelector.Select(l, default, false, false, float.PositiveInfinity, 1);
            Assert.That(p.Mode, Is.EqualTo(VOPlanMode.TwoD));
            Assert.That(p.Main.Chain, Is.EqualTo(VOChains.Radio));
            var filtered = AudioTestUtil.Line("D037", SpeakerIds.Hollis, new[] { VOChains.Dry, VOChains.Radio }, radioFilter: true);
            Assert.That(VOChainSelector.Select(filtered, default, false, true, 2f, 1).Main.Chain, Is.EqualTo(VOChains.Radio));
            var forced = VOChainSelector.Select(l, new VOOptions { ForceDry = true }, false, true, 30f, 1);
            Assert.That(forced.Mode, Is.EqualTo(VOPlanMode.ThreeD));
            Assert.That(forced.Main.Chain, Is.EqualTo(VOChains.Dry));
        }

        [Test]
        public void PaHornsAndAnchors()
        {
            var pa = AudioTestUtil.Line("D012", SpeakerIds.Harrou, new[] { VOChains.PAHorn });
            var p = VOChainSelector.Select(pa, default, false, false, float.PositiveInfinity, 1);
            Assert.That(p.Mode, Is.EqualTo(VOPlanMode.Horns));
            Assert.That(p.MinDistance, Is.EqualTo(AudioConstants.PAHornMinDistance));
            Assert.That(p.MaxDistance, Is.EqualTo(AudioConstants.PAHornMaxDistance));
            var room = AudioTestUtil.Line("D066", SpeakerIds.Clockmaker, new[] { VOChains.RoomSpeaker });
            var r = VOChainSelector.Select(room, default, false, false, float.PositiveInfinity, 1);
            Assert.That(r.Mode, Is.EqualTo(VOPlanMode.ThreeD));
            Assert.That(r.AnchorId, Is.EqualTo(VOAnchors.RoomSpeaker));
            var tape = AudioTestUtil.Line("D002", SpeakerIds.Tallow, new[] { VOChains.Tape });
            var t = VOChainSelector.Select(tape, default, false, false, float.PositiveInfinity, 1);
            Assert.That(t.AnchorId, Is.EqualTo(VOAnchors.BoothReel));
            Assert.That(t.InBooth, Is.True);
        }

        [Test]
        public void PositionalSwitchForG9()
        {
            var d039 = AudioTestUtil.Line("D039", SpeakerIds.Clockmaker, new[] { VOChains.HfAm }, radioFilter: true);
            var p = VOChainSelector.Select(d039, new VOOptions { ForceDry = true, RadioSwitchTime = 1.83f }, false, true, 2f, 1);
            Assert.That(p.Mode, Is.EqualTo(VOPlanMode.PositionalSwitch));
            Assert.That(p.SwitchTime, Is.EqualTo(1.83f));
            var plain = VOChainSelector.Select(d039, default, false, true, 2f, 1);
            Assert.That(plain.Mode, Is.EqualTo(VOPlanMode.TwoD));
            Assert.That(plain.IsRadio, Is.True);
        }

        [Test]
        public void LegionBarksUseDryNearAndFarBeyond30m()
        {
            var bark = AudioTestUtil.Line("BK_LEG_01", SpeakerIds.Legion, new[] { VOChains.Dry, VOChains.Far }, voices: new[] { "A", "B" },
                                          channel: VOChannel.Bark, priority: VOPriority.Bark);
            var near = VOChainSelector.Select(bark, default, true, true, 12f, 2);
            Assert.That(near.Main.Chain, Is.EqualTo(VOChains.Dry));
            Assert.That(near.Main.Variant, Is.EqualTo(BarkRules.VoiceOf(2)));
            var far = VOChainSelector.Select(bark, default, true, true, 60f, 3);
            Assert.That(far.Main.Chain, Is.EqualTo(VOChains.Far));
            Assert.That(far.Main.Variant, Is.EqualTo(BarkRules.VoiceOf(3)));
            Assert.That(far.MaxDistance, Is.GreaterThan(near.MaxDistance));
        }

        [Test]
        public void GreycoatBarksAreTheirOwnRadios()
        {
            var g = AudioTestUtil.Line("BK_GRY_01", SpeakerIds.Greycoat, new[] { VOChains.GreycoatSquelch }, radioFilter: true,
                                       channel: VOChannel.Bark, priority: VOPriority.Bark);
            var p = VOChainSelector.Select(g, default, true, true, 15f, 1);
            Assert.That(p.Mode, Is.EqualTo(VOPlanMode.ThreeD));
            Assert.That(p.IsRadio, Is.True);
        }

        [Test]
        public void SilentWithoutAudio()
        {
            var s = AudioTestUtil.Silent("D021", SpeakerIds.Hollis, 2f);
            Assert.That(VOChainSelector.Select(s, default, false, true, 5f, 1).Mode, Is.EqualTo(VOPlanMode.Silent));
        }
    }
}
