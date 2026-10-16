using System.Collections.Generic;
using System.Text;
using LongShadow.Audio.Core;
using LongShadow.Audio.Editor;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>AudioLibraryBuilder groups clip files into cues (TECH_PLAN §7.9, §4.2).</summary>
    public class AudioLibraryBuilderTests
    {
        [TearDown]
        public void TearDown() => AudioTestUtil.Cleanup();

        private static void Add(List<CueFile> files, List<AudioClip> clips, string path, float seconds = 1f)
        {
            Assert.That(CueNaming.TryParse(path, out var f), Is.True, path);
            files.Add(f);
            clips.Add(AudioTestUtil.Clip(System.IO.Path.GetFileNameWithoutExtension(path), seconds));
        }

        [Test]
        public void GroupsVariantsFarTakesAggregatesAndLoops()
        {
            var files = new List<CueFile>();
            var clips = new List<AudioClip>();
            Add(files, clips, "Assets/_Game/Audio/SFX/Weapons/SFX_WPN_AK_SHOT_02.wav");
            Add(files, clips, "Assets/_Game/Audio/SFX/Weapons/SFX_WPN_AK_SHOT_01.wav");
            Add(files, clips, "Assets/_Game/Audio/SFX/Weapons/Far/SFX_WPN_AK_SHOT_FAR_01.wav");
            Add(files, clips, "Assets/_Game/Audio/SFX/Weapons/SFX_WPN_SHELLS_SAND_01.wav");
            Add(files, clips, "Assets/_Game/Audio/SFX/Weapons/SFX_WPN_SHELLS_STONE_01.wav");
            Add(files, clips, "Assets/_Game/Audio/Ambience/AMB_CICADAS_01.wav", 20.2f);
            Add(files, clips, "Assets/_Game/Audio/Music/MUS_02_MARKET_LO.wav", 34f);
            var loops = LoopPointsFile.Parse(@"{
                ""Assets/_Game/Audio/Ambience/AMB_CICADAS_01.wav"": {""sr"": 48000, ""loop_start_sample"": 2048, ""loop_end_sample"": 962048},
                ""Assets/_Game/Audio/Music/MUS_02_MARKET_LO.wav"": {""sr"": 48000, ""loop_start_sample"": 2048, ""loop_end_sample"": 1602048, ""bpm_grid"": 72.0, ""bar_seconds"": 3.3335, ""loop_bars"": 10}}");
            var cues = AudioLibraryBuilder.BuildCues(files, clips, loops, new StringBuilder());
            var byId = new Dictionary<string, AudioCue>();
            foreach (var c in cues) byId[c.Id] = c;

            var ak = byId["SFX_WPN_AK_SHOT"];
            Assert.That(ak.Clips.Length, Is.EqualTo(2));
            Assert.That(ak.Clips[0].name, Is.EqualTo("SFX_WPN_AK_SHOT_01"), "variants in order");
            Assert.That(ak.HasFar, Is.True);
            Assert.That(ak.SpeedOfSound, Is.True);

            Assert.That(byId.ContainsKey("SFX_WPN_SHELLS"), Is.True, "WP03 asks for the aggregate");
            Assert.That(byId["SFX_WPN_SHELLS"].Clips.Length, Is.EqualTo(2));

            var cic = byId["AMB_CICADAS"];
            Assert.That(cic.Loop, Is.True);
            Assert.That(cic.Category, Is.EqualTo(AudioCategories.Ambience));
            Assert.That(cic.LoopRegion(0).y - cic.LoopRegion(0).x, Is.EqualTo(20f).Within(1e-4f));

            var mus = byId["MUS_02_MARKET_LO"];
            Assert.That(mus.BarSeconds, Is.EqualTo(3.3335f).Within(1e-5f));
            Assert.That(mus.LoopBars, Is.EqualTo(10));
            Assert.That(mus.Category, Is.EqualTo(AudioCategories.Music));
        }
    }
}
