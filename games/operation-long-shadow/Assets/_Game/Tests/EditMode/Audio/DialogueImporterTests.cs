using System.Collections.Generic;
using LongShadow.Audio.Editor;
using LongShadow.Audio.VO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>DialogueImporter (TECH_PLAN §7.10) with fake manifest, clip and envelope lookups.</summary>
    public class DialogueImporterTests
    {
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, EnvelopeFile> _envs = new Dictionary<string, EnvelopeFile>();
        private readonly Dictionary<string, ManifestEntry> _manifest = new Dictionary<string, ManifestEntry>();

        [TearDown]
        public void TearDown()
        {
            AudioTestUtil.Cleanup();
            _clips.Clear();
            _envs.Clear();
            _manifest.Clear();
        }

        private static Dictionary<string, object> Row(string id, string speaker, string text, string context, bool radio = false, string subtitle = null)
        {
            var o = new Dictionary<string, object>
            {
                { "id", id }, { "speaker", speaker }, { "callsign", speaker }, { "voice_id", "vo_x" }, { "text", text }, { "context", context },
                { "radio_filter", radio },
            };
            if (subtitle != null) o["subtitle"] = subtitle;
            return o;
        }

        private void AddClip(string path, float seconds) => _clips[path] = AudioTestUtil.Clip(path, seconds);

        private List<DialogueLine> Build(List<object> rows, List<string> errors, List<string> warnings) =>
            DialogueImporter.BuildLines(rows, id => _manifest.TryGetValue(id, out var e) ? e : null,
                                        p => _clips.TryGetValue(p, out var c) ? c : null,
                                        id => _envs.TryGetValue(id, out var e) ? e : null, errors, warnings);

        [Test]
        public void BuildsSquadLineWithBothChainsAndEnvelopes()
        {
            _manifest["VO_D021"] = new ManifestEntry { id = "VO_D021", variants = new[] { "DRY", "RADIO" }, target_path = "Assets/_Game/Audio/VO/{CHAIN}/VO_D021_{CHAIN}.wav" };
            AddClip("Assets/_Game/Audio/VO/DRY/VO_D021_DRY.wav", 2f);
            AddClip("Assets/_Game/Audio/VO/RADIO/VO_D021_RADIO.wav", 2.1f);
            _envs["D021"] = EnvelopeFile.Parse(@"{""id"":""D021"",""rate_hz"":30,""clips"":{
                ""VO_D021_DRY"":{""chain"":""DRY"",""variant"":null,""duration_s"":2.0,""speech_offset_s"":0.0,""rms_max"":0.2,""rms"":[0.0,0.1,0.2]},
                ""VO_D021_RADIO"":{""chain"":""RADIO"",""variant"":null,""duration_s"":2.1,""speech_offset_s"":0.0,""rms_max"":0.25,""rms"":[0.01,0.25]}}}");
            var errors = new List<string>();
            var lines = Build(new List<object> { Row("D021", SpeakerIds.Hollis, "Off the middle of the street.", "Beat 3, market entry") }, errors, new List<string>());
            Assert.That(errors, Is.Empty);
            var l = lines[0];
            Assert.That(l.Beat, Is.EqualTo(3));
            Assert.That(l.Channel, Is.EqualTo(VOChannel.Net));
            Assert.That(l.Chains, Is.EqualTo(new[] { "DRY", "RADIO" }));
            Assert.That(l.HasChain("DRY") && l.HasChain("RADIO"), Is.True);
            Assert.That(l.Find("DRY", null).Envelope, Is.EqualTo(new byte[] { 0, 128, 255 }));
            Assert.That(l.Duration, Is.EqualTo(2f).Within(1e-3f));
            Assert.That(l.Subtitle, Is.EqualTo("Off the middle of the street."));
        }

        [Test]
        public void MissingClipIsAnErrorButTheLineStillExists()
        {
            _manifest["VO_D030"] = new ManifestEntry { id = "VO_D030", variants = new[] { "RADIO" }, target_path = "x" };
            var errors = new List<string>();
            var lines = Build(new List<object> { Row("D030", SpeakerIds.Hollis, "one two three four five", "Beat 4, nag") }, errors, new List<string>());
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("VO_D030_RADIO"));
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].HasAudio, Is.False);
            Assert.That(lines[0].Duration, Is.EqualTo(DialogueRules.EstimateSeconds("one two three four five")).Within(1e-4f), "silent lines keep a length");
        }

        [Test]
        public void LegionBarksHaveTwoVoicesPerChain()
        {
            _manifest["VO_BK_LEG_01"] = new ManifestEntry { id = "VO_BK_LEG_01", variants = new[] { "DRY", "FAR" }, target_path = "Assets/_Game/Audio/VO/{CHAIN}/VO_BK_LEG_01_{CHAIN}_{A|B}.wav" };
            foreach (var c in new[] { "DRY", "FAR" })
                foreach (var v in new[] { "A", "B" })
                    AddClip("Assets/_Game/Audio/VO/" + c + "/VO_BK_LEG_01_" + c + "_" + v + ".wav", 1f);
            var errors = new List<string>();
            var lines = Build(new List<object> { Row("BK_LEG_01", SpeakerIds.Legion, "Tovek! Tovek!", "Systemic enemy bark pool", false, "(shouting) There! Over there!") },
                              errors, new List<string>());
            Assert.That(errors, Is.Empty);
            var l = lines[0];
            Assert.That(l.IsBark, Is.True);
            Assert.That(l.Repeatable, Is.True);
            Assert.That(l.Clips.Count, Is.EqualTo(4));
            Assert.That(l.Find("FAR", "B").Clip, Is.Not.Null);
            Assert.That(l.Subtitle, Is.EqualTo("(shouting) There! Over there!"));
        }

        [Test]
        public void D039GetsItsSwitchTimeFromTheTokens()
        {
            _manifest["VO_D039"] = new ManifestEntry { id = "VO_D039", variants = new[] { "HF_AM" }, target_path = "x" };
            AddClip("Assets/_Game/Audio/VO/HF_AM/VO_D039_HF_AM.wav", 13.53f);
            _envs["D039"] = EnvelopeFile.Parse(@"{""id"":""D039"",""clips"":{""VO_D039_HF_AM"":{""chain"":""HF_AM"",""duration_s"":13.53,""rms_max"":0.3,""rms"":[0.3]}},
                ""token_onsets_s"":[{""token"":""attention"",""t"":0.03},{""token"":""seven"",""t"":1.9}]}");
            var lines = Build(new List<object> { Row("D039", SpeakerIds.Clockmaker, "Attention.", "MID-A, numbers on the squad net", true) }, new List<string>(), new List<string>());
            Assert.That(lines[0].RadioSwitchTime, Is.EqualTo(1.9f).Within(1e-4f));
            Assert.That(lines[0].Channel, Is.EqualTo(VOChannel.Broadcast));
            Assert.That(lines[0].RadioFilter, Is.True);
        }

        [Test]
        public void DuplicateIdsAreErrors()
        {
            _manifest["VO_D007"] = new ManifestEntry { id = "VO_D007", variants = new[] { "DRY" }, target_path = "x" };
            AddClip("Assets/_Game/Audio/VO/DRY/VO_D007_DRY.wav", 1f);
            var errors = new List<string>();
            var lines = Build(new List<object> { Row("D007", SpeakerIds.Hollis, "a", "Beat 1"), Row("D007", SpeakerIds.Hollis, "b", "Beat 1") }, errors, new List<string>());
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(errors.Count, Is.EqualTo(1));
        }
    }
}
