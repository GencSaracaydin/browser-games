using System.IO;
using LongShadow.Audio.Ambience;
using LongShadow.Audio.Core;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.2 / §9.2a: ambience beds follow the LightStage, the zone, the beat and the station.</summary>
    public class AmbienceTableTests
    {
        private static AmbienceContext Ctx(float s, ZoneType zone, int beat, Vector3 listener = default, bool transmitters = true) =>
            new AmbienceContext { S = s, Zone = zone, Beat = beat, Listener = listener, TransmittersOn = transmitters };

        private static float W(string cue, AmbienceContext c) => AmbienceTable.Find(cue).Weight(c);

        [Test]
        public void CicadasStopAndCricketsStartAt197()
        {
            Assert.That(W("AMB_CICADAS", Ctx(1.96f, ZoneType.Roof, 5)), Is.GreaterThan(0f));
            Assert.That(W("AMB_CRICKETS", Ctx(1.96f, ZoneType.Roof, 5)), Is.EqualTo(0f));
            Assert.That(W("AMB_CICADAS", Ctx(AudioConstants.CricketsStageS, ZoneType.Roof, 5)), Is.EqualTo(0f));
            Assert.That(W("AMB_CRICKETS", Ctx(AudioConstants.CricketsStageS, ZoneType.Roof, 5)), Is.GreaterThan(0f));
        }

        [Test]
        public void OutdoorBedsLeakIntoInteriorsQuietly()
        {
            var outside = W("AMB_TOWN_CURFEW", Ctx(0.5f, ZoneType.Street, 3));
            var inside = W("AMB_TOWN_CURFEW", Ctx(0.5f, ZoneType.Interior, 3));
            Assert.That(inside, Is.GreaterThan(0f));
            Assert.That(MathX.LinearToDb(inside / outside), Is.LessThan(-12f), "walls take at least 12 dB");
            Assert.That(W("AMB_DESERT_WIND", Ctx(0.5f, ZoneType.Basement, 9)), Is.EqualTo(0f));
            Assert.That(W("AMB_ROOMTONE_HALL", Ctx(2.5f, ZoneType.Street, 7)), Is.EqualTo(0f), "interior beds only inside");
            Assert.That(W("AMB_ROOMTONE_HALL", Ctx(2.5f, ZoneType.Interior, 7)), Is.GreaterThan(0f));
        }

        [Test]
        public void GeneratorHumByDistanceToTheStation()
        {
            var at = AmbienceTable.StationCentre;
            var near = W("AMB_GENERATOR_HUM", Ctx(2.2f, ZoneType.Street, 6, at + new Vector3(10f, 0f, 0f)));
            var mid = W("AMB_GENERATOR_HUM", Ctx(2.2f, ZoneType.Street, 6, at + new Vector3(75f, 0f, 0f)));
            var far = W("AMB_GENERATOR_HUM", Ctx(2.2f, ZoneType.Street, 6, at + new Vector3(200f, 0f, 0f)));
            Assert.That(near, Is.GreaterThan(mid));
            Assert.That(mid, Is.GreaterThan(0f));
            Assert.That(far, Is.EqualTo(0f));
            Assert.That(W("AMB_GENERATOR_HUM", Ctx(0.5f, ZoneType.Street, 3, at)), Is.EqualTo(0f), "Beats 6–10 only");
        }

        [Test]
        public void BasementTubesDieWithTheRacks()
        {
            Assert.That(W("AMB_BASEMENT_TUBES", Ctx(3f, ZoneType.Basement, 9)), Is.GreaterThan(0.5f));
            Assert.That(W("AMB_BASEMENT_TUBES", Ctx(3f, ZoneType.Basement, 9, default, false)), Is.EqualTo(0f));
            Assert.That(AmbienceTable.Find("AMB_BASEMENT_TUBES").CutFadeSeconds, Is.LessThan(0.5f), "a power cut is abrupt");
        }

        [Test]
        public void BlueHourBeds()
        {
            Assert.That(W("AMB_SALTPAN_WIND", Ctx(1.5f, ZoneType.OpenDesert, 5)), Is.EqualTo(0f));
            Assert.That(W("AMB_SALTPAN_WIND", Ctx(3.5f, ZoneType.OpenDesert, 10)), Is.GreaterThan(0.5f));
            Assert.That(W("AMB_DISTANT_SHOUTING", Ctx(3.3f, ZoneType.Street, 10)), Is.GreaterThan(0f));
            Assert.That(W("AMB_DESERT_WIND", Ctx(3.5f, ZoneType.OpenDesert, 10)), Is.EqualTo(0f), "the dry wind hands over");
        }

        [Test]
        public void UnknownBeatOpensEveryWindow()
        {
            Assert.That(W("AMB_CINEMA_INTERIOR", Ctx(1.2f, ZoneType.Interior, -1)), Is.GreaterThan(0f));
            Assert.That(W("AMB_CINEMA_INTERIOR", Ctx(1.2f, ZoneType.Interior, 7)), Is.EqualTo(0f));
        }

        [Test]
        public void SoftEdgesRamp()
        {
            var r = new AmbienceBedRule("X", 1f, 1f, 2f, 0.4f, -1, -1, new float[] { 1, 1, 1, 1, 1, 1, 1 }, 1f);
            Assert.That(r.StageWeight(0.7f), Is.EqualTo(0f));
            Assert.That(r.StageWeight(1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(r.StageWeight(1.5f), Is.EqualTo(1f));
            Assert.That(r.StageWeight(2f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(r.StageWeight(2.3f), Is.EqualTo(0f));
        }

        [Test]
        public void EveryBedIsAManifestCue()
        {
            var manifest = Path.Combine(BuilderUtil.ProjectRoot, AssetManifest.RelativePath);
            Assume.That(File.Exists(manifest), "ASSET_MANIFEST.json present");
            foreach (var b in AmbienceTable.Beds)
                Assert.That(AssetManifest.TryGet(b.CueId, out _), Is.True, b.CueId + " is not in ASSET_MANIFEST.json");
            Assert.That(AssetManifest.TryGet(AmbienceTable.BoothRoomTone, out _), Is.True);
        }
    }
}
