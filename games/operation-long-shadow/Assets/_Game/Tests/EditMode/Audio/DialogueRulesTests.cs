using System.Collections.Generic;
using System.IO;
using LongShadow.Audio.Core;
using LongShadow.Audio.Editor;
using LongShadow.Audio.VO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>How DialogueImporter classifies DIALOGUE.json lines (TECH_PLAN §7.10, GDD §17, §7.0).</summary>
    public class DialogueRulesTests
    {
        [Test]
        public void BeatsFromContext()
        {
            Assert.That(DialogueRules.BeatOf("D001", "Intro shot 4, booth"), Is.EqualTo(AudioConstants.IntroBeat));
            Assert.That(DialogueRules.BeatOf("D200", "Insertion cinematic I-2: helicopter"), Is.EqualTo(AudioConstants.IntroBeat));
            Assert.That(DialogueRules.BeatOf("D021", "Beat 3, market entry"), Is.EqualTo(3));
            Assert.That(DialogueRules.BeatOf("D068", "Beat 10, postern"), Is.EqualTo(10));
            Assert.That(DialogueRules.BeatOf("D017", "Glitch insert I1 (booth, 0.9 s freeze + line)"), Is.EqualTo(2));
            Assert.That(DialogueRules.BeatOf("D042", "Glitch insert I2 (6 frames of the stopped clock)"), Is.EqualTo(5));
            Assert.That(DialogueRules.BeatOf("D037", "MID-A Last Light 0:20"), Is.EqualTo(5));
            Assert.That(DialogueRules.BeatOf("D054", "MID-B The Reel, shot 7"), Is.EqualTo(8));
            Assert.That(DialogueRules.BeatOf("D082", "Exfil climax shot 3, pistol rises"), Is.EqualTo(11));
            Assert.That(DialogueRules.BeatOf("D083", "Outro shot 1, booth"), Is.EqualTo(AudioConstants.OutroBeat));
            Assert.That(DialogueRules.BeatOf("CO_HOL_01", "Systemic squad callout pool"), Is.EqualTo(AudioConstants.AlwaysBeat));
            Assert.That(DialogueRules.BeatOf("DI_03", "Death insert"), Is.EqualTo(AudioConstants.AlwaysBeat));
            Assert.That(DialogueRules.BeatOf("D093", "Any beat, out-of-bounds warning"), Is.EqualTo(AudioConstants.AlwaysBeat));
        }

        [Test]
        public void PrioritiesAndFlags()
        {
            Assert.That(DialogueRules.PriorityOf("BK_LEG_01"), Is.EqualTo(VOPriority.Bark));
            Assert.That(DialogueRules.PriorityOf("CO_SPA_02"), Is.EqualTo(VOPriority.Callout));
            Assert.That(DialogueRules.PriorityOf("D029"), Is.EqualTo(VOPriority.Objective));
            Assert.That(DialogueRules.PriorityOf("D021"), Is.EqualTo(VOPriority.Scripted));
            Assert.That(DialogueRules.IsRepeatable("DI_01") && DialogueRules.IsRepeatable("D093") && DialogueRules.IsRepeatable("CO_TIN_04"), Is.True);
            Assert.That(DialogueRules.IsRepeatable("D021"), Is.False);
            Assert.That(DialogueRules.ObjectiveCritical, Is.EquivalentTo(new[] { "D014", "D029", "D034", "D048", "D053", "D059", "D065", "D067" }));
            Assert.That(DialogueRules.ChannelOf("BK_GRY_01", SpeakerIds.Greycoat, VOChains.GreycoatSquelch), Is.EqualTo(VOChannel.Bark));
            Assert.That(DialogueRules.ChannelOf("D001", SpeakerIds.Interrogator, VOChains.Scrambler), Is.EqualTo(VOChannel.Booth));
            Assert.That(DialogueRules.ChannelOf("D012", SpeakerIds.Harrou, VOChains.PAHorn), Is.EqualTo(VOChannel.Broadcast));
            Assert.That(DialogueRules.ChannelOf("D021", SpeakerIds.Hollis, VOChains.Dry), Is.EqualTo(VOChannel.Net));
        }

        [Test]
        public void EstimatesFollowWordCount()
        {
            Assert.That(DialogueRules.EstimateSeconds(""), Is.EqualTo(0.8f));
            Assert.That(DialogueRules.EstimateSeconds("Off the middle of the street."), Is.EqualTo(6f / 2.6f + 0.4f).Within(1e-4f));
        }

        [Test]
        public void EveryDialogueLineGetsABeatAndAKnownChain()
        {
            var path = Path.Combine(BuilderUtil.ProjectRoot, "Docs", "DIALOGUE.json");
            Assume.That(File.Exists(path), "DIALOGUE.json present");
            var rows = (List<object>)MiniJson.Parse(File.ReadAllText(path));
            var unplaced = new List<string>();
            var noChain = new List<string>();
            foreach (var r in rows)
            {
                var o = (Dictionary<string, object>)r;
                var id = MiniJson.Str(o, "id");
                var beat = DialogueRules.BeatOf(id, MiniJson.Str(o, "context"));
                if (beat == AudioConstants.AlwaysBeat && !DialogueRules.IsRepeatable(id)) unplaced.Add(id);
                AssetManifest.TryGet("VO_" + id, out var e);
                if (DialogueImporter.ChainsOf(e, null).Count == 0) noChain.Add(id);
            }
            Assert.That(unplaced, Is.Empty, "story lines need a beat window: " + string.Join(", ", unplaced));
            Assert.That(noChain, Is.Empty, "manifest chains: " + string.Join(", ", noChain));
        }
    }
}
