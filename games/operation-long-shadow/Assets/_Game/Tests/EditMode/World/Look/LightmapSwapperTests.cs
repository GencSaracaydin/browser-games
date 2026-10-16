using LongShadow.World.Look.Lightmaps;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>Lightmap swaps match by texture, not index (TECH_PLAN R13, D7, G11).</summary>
    public sealed class LightmapSwapperTests
    {
        private Texture2D[] _tex;
        private LightmapSet _golden, _sunset, _dusk;

        [SetUp]
        public void SetUp()
        {
            _tex = new Texture2D[7];
            for (var i = 0; i < _tex.Length; i++) _tex[i] = new Texture2D(4, 4) { name = "T" + i };
            _golden = Set("GOLDEN", _tex[0], _tex[1]);
            _sunset = Set("SUNSET", _tex[2], _tex[3]);
            _dusk = Set("DUSK", _tex[4], _tex[5]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var t in _tex) Object.DestroyImmediate(t);
            Object.DestroyImmediate(_golden);
            Object.DestroyImmediate(_sunset);
            Object.DestroyImmediate(_dusk);
        }

        private static LightmapSet Set(string name, Texture2D a, Texture2D b)
        {
            var s = ScriptableObject.CreateInstance<LightmapSet>();
            s.EditorSet(name, "S9_TownFar", new[] { a, b });
            return s;
        }

        [Test]
        public void RemapsOnlyThisSectionsEntriesBySlot()
        {
            // Another section's lightmap (T6) sits between ours in the global array.
            var maps = new[]
            {
                new LightmapData { lightmapColor = _tex[0] },
                new LightmapData { lightmapColor = _tex[6] },
                new LightmapData { lightmapColor = _tex[1] },
            };
            var known = new[] { _golden, _sunset, _dusk };
            Assert.AreEqual(2, LightmapSwapper.Remap(maps, known, _sunset));
            Assert.AreSame(_tex[2], maps[0].lightmapColor);
            Assert.AreSame(_tex[6], maps[1].lightmapColor);
            Assert.AreSame(_tex[3], maps[2].lightmapColor);

            Assert.AreEqual(2, LightmapSwapper.Remap(maps, known, _dusk));
            Assert.AreSame(_tex[4], maps[0].lightmapColor);
            Assert.AreSame(_tex[5], maps[2].lightmapColor);

            Assert.AreEqual(2, LightmapSwapper.Remap(maps, known, _golden));
            Assert.AreSame(_tex[0], maps[0].lightmapColor);
            Assert.AreEqual(0, LightmapSwapper.Remap(maps, known, _golden), "already applied");
        }

        [Test]
        public void StageThresholdsPickTheTownFarSet()
        {
            float[] th = { 1.35f, 1.97f };
            Assert.AreEqual(-1, LightmapSwapper.IndexForStage(0.5f, th, 2));
            Assert.AreEqual(-1, LightmapSwapper.IndexForStage(1.34f, th, 2));
            Assert.AreEqual(0, LightmapSwapper.IndexForStage(1.35f, th, 2));
            Assert.AreEqual(0, LightmapSwapper.IndexForStage(1.96f, th, 2));
            Assert.AreEqual(1, LightmapSwapper.IndexForStage(1.97f, th, 2));
            Assert.AreEqual(1, LightmapSwapper.IndexForStage(3.9f, th, 2));
            Assert.AreEqual(0, LightmapSwapper.IndexForStage(3.9f, th, 1), "only as many alternates as exist");
        }
    }
}
