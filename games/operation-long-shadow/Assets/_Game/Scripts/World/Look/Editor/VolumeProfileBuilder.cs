using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Builds the 1980s film-grade volume profiles of TECH_PLAN §3.7 / GDD §9.6 under <c>Assets/_Game/Settings/Volumes/</c>:
    /// <c>VP_Base</c> (ACES, bloom with T_LensDirt, contrast/saturation, split toning, lift-gamma-gain, Medium1 grain,
    /// vignette, chromatic aberration, camera motion blur; DoF off — <c>DofDirector</c> fades it in), the four stage
    /// profiles (white balance, exposure, saturation, bloom, split-toning balance, Purkinje SMH; every stage overrides the
    /// same parameter set so the LightStage cross-fade is exact), the interior exposure profiles, <c>VP_Booth</c>
    /// (complete, the booth camera only sees layer Booth) and <c>VP_Cinematic</c> (CINEMATIC_DIRECTION §1.3 helper for
    /// WP09). Idempotent: re-running updates the assets in place (GUIDs kept). The configure methods also work on
    /// in-memory profiles (tests).
    /// </summary>
    public static class VolumeProfileBuilder
    {
        // GDD §9.6 VP_Base.
        public const float BloomIntensity = 0.6f, BloomThreshold = 1.0f, BloomScatter = 0.65f, LensDirtIntensity = 0.8f;
        public const float Contrast = 12f, BaseSaturation = -8f, SplitBalance = -20f;
        public const float GrainIntensity = 0.25f, GrainResponse = 0.8f;
        public const float VignetteIntensity = 0.25f, VignetteSmoothness = 0.4f, ChromaticAberration = 0.10f, MotionBlurIntensity = 0.15f;
        public static readonly Color SplitShadows = LightStageTable.Hex(0x2E5C66);
        public static readonly Color SplitHighlights = LightStageTable.Hex(0xD9A066);
        public static readonly Vector4 Lift = new Vector4(0.98f, 0.99f, 1.02f, -0.02f);
        public static readonly Vector4 Gain = new Vector4(1.03f, 1.00f, 0.96f, 0f);
        /// <summary>Warm halation tint on bloom (CINEMATIC_DIRECTION §1.3 "bloom with a warm tint").</summary>
        public static readonly Color BloomTint = LightStageTable.Hex(0xFFE6CC);

        /// <summary>Per-stage overrides, Golden..Blue Hour (GDD §9.6 stage table). Unlisted cells repeat VP_Base.</summary>
        public static readonly float[] StageTemperature = { 12f, 18f, -5f, -18f };
        public static readonly float[] StageTint = { 2f, 4f, 6f, 0f };
        public static readonly float[] StageSaturation = { -6f, 0f, -10f, -22f };
        public static readonly float[] StageBloom = { 0.6f, 0.8f, 0.6f, 0.9f };
        public static readonly float[] StageSplitBalance = { -20f, -20f, -35f, -20f };
        /// <summary>Blue Hour Purkinje shift (SMH shadows).</summary>
        public static readonly Vector4 PurkinjeShadows = new Vector4(0.85f, 0.92f, 1.10f, 0f);

        /// <summary>Interior exposure offsets (GDD §9.2a interiors table).</summary>
        public const float CinemaEv = 1.2f, HallEv = 0.8f, ScreeningEv = 1.0f, BasementEv = 1.0f;

        [MenuItem("LongShadow/Look/Build Volume Profiles", priority = 110)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            BuildAll(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Builds or updates every profile asset.</summary>
        public static void BuildAll(StringBuilder log)
        {
            BuilderUtil.EnsureFolder(LookPaths.VolumesFolder);
            var dirt = AssetManifest.Find<Texture2D>(LookPaths.TexLensDirt, "T_LensDirt");
            if (dirt == null) log.Append("T_LensDirt_BC missing: bloom dirt off until it lands (re-run). ");

            ConfigureBase(Asset(LookPaths.VPBase), dirt, true);
            ConfigureStage(Asset(LookPaths.VPGolden), StageId.Golden, true);
            ConfigureStage(Asset(LookPaths.VPSunset), StageId.Sunset, true);
            ConfigureStage(Asset(LookPaths.VPAfterglow), StageId.Afterglow, true);
            ConfigureStage(Asset(LookPaths.VPBlueHour), StageId.BlueHour, true);
            ConfigureInterior(Asset(LookPaths.VPInteriorCinema), CinemaEv, true);
            ConfigureInterior(Asset(LookPaths.VPInteriorHall), HallEv, true);
            ConfigureInterior(Asset(LookPaths.VPInteriorScreening), ScreeningEv, true);
            ConfigureInterior(Asset(LookPaths.VPInteriorBasement), BasementEv, true);
            ConfigureBooth(Asset(LookPaths.VPBooth), dirt, true);
            ConfigureCinematic(Asset(LookPaths.VPCinematic), true);
            AssetDatabase.SaveAssets();
            log.Append("Volume profiles built (11) in " + LookPaths.VolumesFolder + ".");
        }

        /// <summary>VP_Base (GDD §9.6).</summary>
        public static void ConfigureBase(VolumeProfile p, Texture lensDirt, bool asset = false)
        {
            var tone = Ensure<Tonemapping>(p, asset);
            tone.mode.Override(TonemappingMode.ACES);

            ConfigureBloom(Ensure<Bloom>(p, asset), BloomIntensity, lensDirt);

            var ca = Ensure<ColorAdjustments>(p, asset);
            ca.postExposure.Override(0f);
            ca.contrast.Override(Contrast);
            ca.saturation.Override(BaseSaturation);

            ConfigureSplitToning(Ensure<SplitToning>(p, asset), SplitBalance);
            ConfigureLgg(Ensure<LiftGammaGain>(p, asset));

            var grain = Ensure<FilmGrain>(p, asset);
            grain.type.Override(FilmGrainLookup.Medium1); // PostProcessData_Web keeps Medium1 and Large01 only
            grain.intensity.Override(GrainIntensity);
            grain.response.Override(GrainResponse);

            var vig = Ensure<Vignette>(p, asset);
            vig.color.Override(Color.black);
            vig.intensity.Override(VignetteIntensity);
            vig.smoothness.Override(VignetteSmoothness);
            vig.rounded.Override(false);

            Ensure<ChromaticAberration>(p, asset).intensity.Override(ChromaticAberration);

            var mb = Ensure<MotionBlur>(p, asset);
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.quality.Override(MotionBlurQuality.Low);
            mb.intensity.Override(MotionBlurIntensity);

            var dof = Ensure<DepthOfField>(p, asset);
            dof.mode.Override(DepthOfFieldMode.Off);

            var wb = Ensure<WhiteBalance>(p, asset);
            wb.temperature.Override(0f);
            wb.tint.Override(0f);

            var smh = Ensure<ShadowsMidtonesHighlights>(p, asset);
            smh.shadows.Override(new Vector4(1f, 1f, 1f, 0f));
            Dirty(p, asset);
        }

        /// <summary>VP_Golden / VP_Sunset / VP_Afterglow / VP_BlueHour (GDD §9.6 stage table).</summary>
        public static void ConfigureStage(VolumeProfile p, StageId stage, bool asset = false)
        {
            var i = (int)stage;
            var wb = Ensure<WhiteBalance>(p, asset);
            wb.temperature.Override(StageTemperature[i]);
            wb.tint.Override(StageTint[i]);

            var ca = Ensure<ColorAdjustments>(p, asset);
            ca.postExposure.Override(LightStageTable.StageExposure[i]);
            ca.saturation.Override(StageSaturation[i]);

            var bloom = Ensure<Bloom>(p, asset);
            bloom.intensity.Override(StageBloom[i]);

            var st = Ensure<SplitToning>(p, asset);
            st.balance.Override(StageSplitBalance[i]);

            var smh = Ensure<ShadowsMidtonesHighlights>(p, asset);
            smh.shadows.Override(stage == StageId.BlueHour ? PurkinjeShadows : new Vector4(1f, 1f, 1f, 0f));
            Dirty(p, asset);
        }

        /// <summary>VP_Interior_* : post exposure only (EyeAdaptationDriver adds the stage exposure at runtime).</summary>
        public static void ConfigureInterior(VolumeProfile p, float ev, bool asset = false)
        {
            var ca = Ensure<ColorAdjustments>(p, asset);
            ca.postExposure.Override(ev);
            Dirty(p, asset);
        }

        /// <summary>VP_Booth: the full grade plus the booth look (GDD §9.6 VP_Booth row).</summary>
        public static void ConfigureBooth(VolumeProfile p, Texture lensDirt, bool asset = false)
        {
            Ensure<Tonemapping>(p, asset).mode.Override(TonemappingMode.ACES);
            ConfigureBloom(Ensure<Bloom>(p, asset), BloomIntensity, lensDirt);
            var ca = Ensure<ColorAdjustments>(p, asset);
            ca.postExposure.Override(0f);
            ca.contrast.Override(Contrast);
            ca.colorFilter.Override(LightStageTable.Hex(0xC8D6C0));
            ca.saturation.Override(-35f);
            ConfigureSplitToning(Ensure<SplitToning>(p, asset), SplitBalance);
            ConfigureLgg(Ensure<LiftGammaGain>(p, asset));
            var grain = Ensure<FilmGrain>(p, asset);
            grain.type.Override(FilmGrainLookup.Large01);
            grain.intensity.Override(0.55f);
            grain.response.Override(GrainResponse);
            Ensure<LensDistortion>(p, asset).intensity.Override(-0.12f);
            var vig = Ensure<Vignette>(p, asset);
            vig.color.Override(Color.black);
            vig.intensity.Override(0.35f);
            vig.smoothness.Override(VignetteSmoothness);
            Ensure<ChromaticAberration>(p, asset).intensity.Override(0.2f);
            var mb = Ensure<MotionBlur>(p, asset);
            mb.intensity.Override(0f);
            Dirty(p, asset);
        }

        /// <summary>VP_Cinematic: CINEMATIC_DIRECTION §1.3 grade for Timeline shots (WP09 blends it in by weight).</summary>
        public static void ConfigureCinematic(VolumeProfile p, bool asset = false)
        {
            var grain = Ensure<FilmGrain>(p, asset);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.75f);
            var mb = Ensure<MotionBlur>(p, asset);
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.quality.Override(MotionBlurQuality.Medium);
            mb.intensity.Override(0.4f);
            var vig = Ensure<Vignette>(p, asset);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            var bloom = Ensure<Bloom>(p, asset);
            bloom.tint.Override(LightStageTable.Hex(0xFFDDB8));
            Ensure<ChromaticAberration>(p, asset).intensity.Override(0.12f);
            Dirty(p, asset);
        }

        private static void ConfigureBloom(Bloom b, float intensity, Texture lensDirt)
        {
            b.intensity.Override(intensity);
            b.threshold.Override(BloomThreshold);
            b.scatter.Override(BloomScatter);
            b.tint.Override(BloomTint);
            b.highQualityFiltering.Override(false);
            if (lensDirt != null)
            {
                b.dirtTexture.Override(lensDirt);
                b.dirtIntensity.Override(LensDirtIntensity);
            }
            else b.dirtIntensity.Override(0f);
        }

        private static void ConfigureSplitToning(SplitToning st, float balance)
        {
            st.shadows.Override(SplitShadows);
            st.highlights.Override(SplitHighlights);
            st.balance.Override(balance);
        }

        private static void ConfigureLgg(LiftGammaGain lgg)
        {
            lgg.lift.Override(Lift);
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
            lgg.gain.Override(Gain);
        }

        private static VolumeProfile Asset(string path) => BuilderUtil.LoadOrCreateAsset<VolumeProfile>(path);

        /// <summary>Gets or adds a component; on assets the component is stored as a hidden sub-asset.</summary>
        public static T Ensure<T>(VolumeProfile p, bool asset) where T : VolumeComponent
        {
            if (p.TryGet(out T c))
            {
                c.active = true;
                return c;
            }
            c = p.Add<T>(false);
            c.name = typeof(T).Name;
            if (asset && AssetDatabase.Contains(p))
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            return c;
        }

        private static void Dirty(VolumeProfile p, bool asset)
        {
            if (!asset) return;
            EditorUtility.SetDirty(p);
            foreach (var c in p.components)
                if (c != null) EditorUtility.SetDirty(c);
        }
    }
}
