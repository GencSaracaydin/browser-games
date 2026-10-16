using LongShadow.Core;
using LongShadow.World.Look.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Tests.EditMode.World.Look
{
    /// <summary>The film grade (GDD §9.6) and the baking plan (GDD §9.4, D16) as built by the editor tools.</summary>
    public sealed class LookDataTests
    {
        [Test]
        public void BaseProfileIsTheEightiesGrade()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            try
            {
                VolumeProfileBuilder.ConfigureBase(p, null);
                Assert.IsTrue(p.TryGet(out Tonemapping tone));
                Assert.AreEqual(TonemappingMode.ACES, tone.mode.value);
                Assert.IsTrue(p.TryGet(out Bloom bloom));
                Assert.AreEqual(0.6f, bloom.intensity.value, 1e-5f);
                Assert.AreEqual(1.0f, bloom.threshold.value, 1e-5f);
                Assert.AreEqual(0.65f, bloom.scatter.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out ColorAdjustments ca));
                Assert.AreEqual(12f, ca.contrast.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out SplitToning st));
                Assert.AreEqual(-20f, st.balance.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out LiftGammaGain lgg));
                Assert.AreEqual(new Vector4(0.98f, 0.99f, 1.02f, -0.02f), lgg.lift.value);
                Assert.IsTrue(p.TryGet(out FilmGrain grain));
                Assert.AreEqual(FilmGrainLookup.Medium1, grain.type.value);
                Assert.AreEqual(0.25f, grain.intensity.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out Vignette vig));
                Assert.AreEqual(0.25f, vig.intensity.value, 1e-5f);
                Assert.AreEqual(0.4f, vig.smoothness.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out ChromaticAberration chroma));
                Assert.AreEqual(0.10f, chroma.intensity.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out MotionBlur mb));
                Assert.AreEqual(0.15f, mb.intensity.value, 1e-5f);
                Assert.IsTrue(p.TryGet(out DepthOfField dof));
                Assert.AreEqual(DepthOfFieldMode.Off, dof.mode.value);
            }
            finally
            {
                Destroy(p);
            }
        }

        [Test]
        public void StageProfilesOverrideTheSameParameters()
        {
            for (var i = 0; i < 4; i++)
            {
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                try
                {
                    VolumeProfileBuilder.ConfigureStage(p, (StageId)i);
                    Assert.IsTrue(p.TryGet(out WhiteBalance wb) && wb.temperature.overrideState && wb.tint.overrideState);
                    Assert.IsTrue(p.TryGet(out ColorAdjustments ca) && ca.postExposure.overrideState && ca.saturation.overrideState);
                    Assert.IsFalse(ca.contrast.overrideState, "contrast stays with VP_Base");
                    Assert.IsTrue(p.TryGet(out Bloom b) && b.intensity.overrideState);
                    Assert.IsTrue(p.TryGet(out SplitToning st) && st.balance.overrideState);
                    Assert.IsTrue(p.TryGet(out ShadowsMidtonesHighlights smh) && smh.shadows.overrideState);
                }
                finally
                {
                    Destroy(p);
                }
            }
        }

        [Test]
        public void BlueHourCarriesThePurkinjeShift()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            try
            {
                VolumeProfileBuilder.ConfigureStage(p, StageId.BlueHour);
                p.TryGet(out ColorAdjustments ca);
                p.TryGet(out ShadowsMidtonesHighlights smh);
                p.TryGet(out WhiteBalance wb);
                p.TryGet(out Bloom bloom);
                Assert.AreEqual(1.5f, ca.postExposure.value, 1e-5f);
                Assert.AreEqual(-22f, ca.saturation.value, 1e-5f);
                Assert.AreEqual(-18f, wb.temperature.value, 1e-5f);
                Assert.AreEqual(0.9f, bloom.intensity.value, 1e-5f);
                Assert.AreEqual(new Vector4(0.85f, 0.92f, 1.10f, 0f), smh.shadows.value);
            }
            finally
            {
                Destroy(p);
            }
        }

        [Test]
        public void BoothProfileIsComplete()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            try
            {
                VolumeProfileBuilder.ConfigureBooth(p, null);
                Assert.IsTrue(p.TryGet(out Tonemapping _));
                p.TryGet(out ColorAdjustments ca);
                Assert.AreEqual(-35f, ca.saturation.value, 1e-5f);
                p.TryGet(out FilmGrain grain);
                Assert.AreEqual(FilmGrainLookup.Large01, grain.type.value);
                Assert.AreEqual(0.55f, grain.intensity.value, 1e-5f);
                p.TryGet(out LensDistortion ld);
                Assert.AreEqual(-0.12f, ld.intensity.value, 1e-5f);
            }
            finally
            {
                Destroy(p);
            }
        }

        [Test]
        public void BakePlanFitsTheLightmapBudget()
        {
            var total = 0f;
            foreach (SectionId id in System.Enum.GetValues(typeof(SectionId)))
                foreach (var spec in SectionBakeTable.For(id))
                    total += spec.CapEq;
            Assert.LessOrEqual(total, SectionBakeTable.TotalCapEq + 1e-4f, "D16: 27 x 1024² equivalents");
            Assert.AreEqual(3, SectionBakeTable.For(SectionId.TownFar).Count);
            Assert.AreEqual("GOLDEN", SectionBakeTable.For(SectionId.TownFar)[2].SetName, "the scene keeps GOLDEN");
            Assert.AreEqual(LightmapBakeType.Baked, SectionBakeTable.For(SectionId.TownFar)[0].SunBakeType);
            Assert.AreEqual(0f, SectionBakeTable.For(SectionId.Roofs)[0].IndirectMultiplier, "S4 sun bounce off");
            Assert.AreEqual("NORMAL", SectionBakeTable.For(SectionId.StationInt)[1].SetName);
            Assert.AreEqual(new Vector3(42f, 22f, 30f), SectionBakeTable.Knots(1)[0]);
            Assert.AreEqual(new Vector3(350f, -1f, 404f), SectionBakeTable.Knots(10)[3]);
        }

        private static void Destroy(VolumeProfile p)
        {
            foreach (var c in p.components)
                if (c != null) Object.DestroyImmediate(c);
            Object.DestroyImmediate(p);
        }
    }
}
