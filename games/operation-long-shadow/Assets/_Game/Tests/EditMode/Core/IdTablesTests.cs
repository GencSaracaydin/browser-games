using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    /// <summary>Shared id tables stay in sync with the docs (DIALOGUE.json, ASSET_MANIFEST.json, GDD §6.7 / §11 / §17).</summary>
    public class IdTablesTests
    {
        [System.Serializable]
        private class DialogueLine
        {
            public string id;
            public string speaker;
        }

        [System.Serializable]
        private class DialogueWrapper
        {
            public List<DialogueLine> items = new List<DialogueLine>();
        }

        private static List<DialogueLine> LoadDialogue()
        {
            var path = Path.Combine(BuilderUtil.ProjectRoot, "Docs", "DIALOGUE.json");
            Assert.IsTrue(File.Exists(path), path);
            return JsonUtility.FromJson<DialogueWrapper>("{\"items\":" + File.ReadAllText(path) + "}").items;
        }

        [Test]
        public void EveryDialogueSpeakerHasAStyle()
        {
            var lines = LoadDialogue();
            Assert.That(lines.Count, Is.GreaterThan(100));
            var seen = new HashSet<string>();
            foreach (var l in lines)
            {
                seen.Add(l.speaker);
                Assert.That(SpeakerIds.All, Does.Contain(l.speaker), l.id + " speaker " + l.speaker);
                Assert.That(SpeakerIds.StyleOf(l.speaker).Id, Is.EqualTo(l.speaker));
            }
            foreach (var s in SpeakerIds.All) Assert.IsTrue(seen.Contains(s), "SpeakerIds." + s + " is not used in DIALOGUE.json");
        }

        [Test]
        public void SpeakerStylesMatchGdd11()
        {
            Assert.That(ColorUtility.ToHtmlStringRGB(SpeakerIds.StyleOf(SpeakerIds.Hollis).Color), Is.EqualTo("E8C77A"));
            Assert.That(ColorUtility.ToHtmlStringRGB(SpeakerIds.StyleOf(SpeakerIds.Clockmaker).Color), Is.EqualTo("7FE07F"));
            Assert.IsTrue(SpeakerIds.StyleOf(SpeakerIds.Clockmaker).Monospace);
            Assert.IsTrue(SpeakerIds.StyleOf(SpeakerIds.Interrogator).ItalicInBooth);
            Assert.That(SpeakerIds.ToSquadId(SpeakerIds.Sparrow), Is.EqualTo(SquadIds.Sparrow));
            Assert.That(SpeakerIds.FromSquadId(SquadIds.Tinker), Is.EqualTo(SpeakerIds.Tinker));
            Assert.IsNull(SpeakerIds.ToSquadId(SpeakerIds.Magpie));
            Assert.IsTrue(SpeakerIds.IsEnemy(SpeakerIds.Legion));
            Assert.That(SpeakerIds.StyleOf("NOBODY").Color, Is.EqualTo(Color.white));
        }

        [Test]
        public void WeaponIdsExistInTheManifest()
        {
            AssetManifest.Reload();
            foreach (var id in new[] { WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK, WeaponIds.AKMS, WeaponIds.SVD,
                                       WeaponIds.RPG7, WeaponIds.DShK, WeaponIds.DoorGun, WeaponIds.Frag, WeaponIds.Knife, WeaponIds.Smoke })
                Assert.IsTrue(AssetManifest.TryGet(WeaponIds.ManifestId(id), out _), "manifest has no " + WeaponIds.ManifestId(id));
            Assert.IsTrue(WeaponIds.IsPrimary(WeaponIds.RPK));
            Assert.IsFalse(WeaponIds.IsPrimary(WeaponIds.HushPuppy));
        }

        [Test]
        public void DerivedAudioCuesExistInTheManifest()
        {
            AssetManifest.Reload();
            foreach (SurfaceType st in System.Enum.GetValues(typeof(SurfaceType)))
            {
                Assert.IsTrue(AssetManifest.TryGet(AudioCues.Footstep(st), out _), "footstep cue for " + st + ": " + AudioCues.Footstep(st));
                Assert.IsTrue(AssetManifest.TryGet(AudioCues.Impact(st), out _), "impact cue for " + st + ": " + AudioCues.Impact(st));
            }
            foreach (ZoneType z in System.Enum.GetValues(typeof(ZoneType)))
                Assert.IsTrue(AssetManifest.TryGet(AudioCues.WeaponTail(z), out _), "tail for " + z);
            foreach (var w in new[] { WeaponIds.Commando, WeaponIds.HushPuppy, WeaponIds.AK, WeaponIds.RPK, WeaponIds.AKMS, WeaponIds.SVD,
                                      WeaponIds.DShK, WeaponIds.DoorGun, WeaponIds.RPG7 })
                Assert.IsTrue(AssetManifest.TryGet(AudioCues.WeaponShot(w), out _), "shot cue for " + w);
            foreach (var c in new[] { AudioCues.BulletCrack, AudioCues.BulletWhiz, AudioCues.UiHitMarker, AudioCues.UiHeadshot, AudioCues.UiKill })
                Assert.IsTrue(AssetManifest.TryGet(c, out _), c);
        }

        [Test]
        public void SectionNamesAndParsing()
        {
            Assert.That(Sections.SceneName(SectionId.Market), Is.EqualTo("S2_Market"));
            Assert.That(Sections.ScenePath(SectionId.TownFar), Is.EqualTo("Assets/_Game/Scenes/Sections/S9_TownFar.unity"));
            Assert.That(Sections.AddressKey(SectionId.Booth), Is.EqualTo("S0_Booth"));
            for (var i = 0; i < Sections.Count; i++)
            {
                var id = (SectionId)i;
                Assert.IsTrue(Sections.TryParse(Sections.SceneName(id), out var back));
                Assert.That(back, Is.EqualTo(id));
                Assert.IsTrue(Sections.TryParse(id.ToString().ToLowerInvariant(), out back));
                Assert.That(back, Is.EqualTo(id));
            }
            Assert.IsTrue(Sections.TryParse("s6", out var s6));
            Assert.That(s6, Is.EqualTo(SectionId.StationInt));
            Assert.IsFalse(Sections.TryParse("S11", out _));
        }

        [Test]
        public void CheckpointIdsMatchTheMarkupCsv()
        {
            var t = MarkupIds.Load();
            foreach (var cp in SequenceIds.Checkpoints)
            {
                Assert.IsTrue(t.TryGet(cp, out var row), "markup_ids.csv has no " + cp);
                Assert.That(row.Component, Is.EqualTo("CheckpointMarker"));
            }
            Assert.That(SequenceIds.CheckpointOrder(SequenceIds.CP3b), Is.EqualTo(3));
            Assert.That(SequenceIds.CheckpointOrder("CP99"), Is.EqualTo(-1));
        }
    }
}
