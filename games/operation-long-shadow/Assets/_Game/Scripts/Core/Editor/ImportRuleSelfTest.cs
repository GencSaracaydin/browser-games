using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Proves the import rules and MaterialFactory end to end (WP00 Phase B definition of done): copies real sample files
    /// from RawAssets/ (Poly Haven textures + HDRI, the Tripo rifle as FBX, a Rocketbox character and clip, WP10/WP03
    /// content-agent outputs, falmedia WAVs) into <see cref="ImportRulePaths.TestRoot"/> in the layout of Assets/_Game,
    /// imports them with the real rules, checks the resulting settings against independently written expectations
    /// (TECH_PLAN §3.10–3.12), builds materials from a texture set and from a spec file, binds model slots, writes
    /// Logs/import_rules_selftest.txt and deletes the test folder. Menu: LongShadow/Import/Run Rule Self-Test.
    /// For long runs through Unity_RunCommand call <see cref="Stage"/>, <see cref="Verify"/> and <see cref="Cleanup"/>
    /// separately.
    /// </summary>
    public static class ImportRuleSelfTest
    {
        /// <summary>Report file (project-relative).</summary>
        public const string ReportPath = "Logs/import_rules_selftest.txt";

        private const string T = ImportRulePaths.TestRoot;
        private const string Wp10 = "RawAssets/wp10/ready/Assets/_Game/";
        private const string SpecTempPath = "Temp/ls_import_rules_selftest_materials.json";

        private sealed class Sample
        {
            public string Source;   // project-relative source, or null for generated files
            public string Dest;     // test-root asset path
            public Action<string, List<string>> Check;
            public Func<string, string> Generate; // writes Dest itself (returns error or null)
        }

        /// <summary>Stage + Verify (+ Cleanup unless <paramref name="keepFiles"/>). True when every check passed.</summary>
        public static bool Run(bool keepFiles = false)
        {
            Stage();
            var ok = Verify();
            if (!keepFiles) Cleanup();
            return ok;
        }

        /// <summary>Copies and imports every sample (deletes an old test folder first). Returns the number staged.</summary>
        public static int Stage()
        {
            Cleanup();
            TextureSourceProbe.ClearCache();
            var staged = 0;
            foreach (var s in Samples())
            {
                if (s.Source == null) continue;
                var src = Path.Combine(BuilderUtil.ProjectRoot, s.Source);
                if (!File.Exists(src)) continue;
                var dst = Path.Combine(BuilderUtil.ProjectRoot, s.Dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
                File.Copy(src, dst, true);
                staged++;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var s in Samples())
            {
                if (s.Generate == null) continue;
                var err = s.Generate(s.Dest);
                if (err == null) staged++;
                else LSLog.Warn(LogCategory.Assets, "Self-test: could not generate " + s.Dest + ": " + err);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return staged;
        }

        /// <summary>Checks every staged sample, builds and binds materials, writes the report. True when all passed.</summary>
        public static bool Verify()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Import rule self-test " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            int pass = 0, fail = 0, skipped = 0;
            void Report(string what, List<string> errors, string info)
            {
                if (errors.Count == 0)
                {
                    pass++;
                    sb.AppendLine("PASS  " + what + (info != null ? "  [" + info + "]" : ""));
                }
                else
                {
                    fail++;
                    sb.AppendLine("FAIL  " + what + (info != null ? "  [" + info + "]" : ""));
                    foreach (var e in errors) sb.AppendLine("        " + e);
                }
            }

            sb.AppendLine().AppendLine("## Import settings");
            foreach (var s in Samples())
            {
                var importer = AssetImporter.GetAtPath(s.Dest);
                if (importer == null)
                {
                    skipped++;
                    sb.AppendLine("SKIP  " + s.Dest + "  (source missing: " + (s.Source ?? "generated") + ")");
                    continue;
                }
                var errors = new List<string>();
                try
                {
                    s.Check(s.Dest, errors);
                }
                catch (Exception e)
                {
                    errors.Add("exception: " + e.Message);
                }
                Report(s.Dest, errors, Describe(importer, s.Dest));
            }

            sb.AppendLine().AppendLine("## MaterialFactory");
            try
            {
                CheckMaterials(Report);
            }
            catch (Exception e)
            {
                Report("MaterialFactory", new List<string> { "exception: " + e }, null);
            }

            sb.AppendLine().AppendLine("## ModelMaterialBinder");
            try
            {
                CheckBinding(Report);
            }
            catch (Exception e)
            {
                Report("ModelMaterialBinder", new List<string> { "exception: " + e }, null);
            }

            var summary = pass + " passed, " + fail + " failed, " + skipped + " skipped";
            sb.AppendLine().AppendLine("# " + summary);
            var full = Path.Combine(BuilderUtil.ProjectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllText(full, sb.ToString());
            if (fail == 0) LSLog.Info(LogCategory.Assets, "Import rule self-test: " + summary + " (" + ReportPath + ")");
            else LSLog.Error(LogCategory.Assets, "Import rule self-test: " + summary + " (" + ReportPath + ")");
            return fail == 0;
        }

        /// <summary>Deletes the test folder and the temporary spec file.</summary>
        public static void Cleanup()
        {
            var folder = T.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            var full = Path.Combine(BuilderUtil.ProjectRoot, folder);
            if (Directory.Exists(full)) Directory.Delete(full, true);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
            var spec = Path.Combine(BuilderUtil.ProjectRoot, SpecTempPath);
            if (File.Exists(spec)) File.Delete(spec);
            TextureSourceProbe.ClearCache();
        }

        // ------------------------------------------------------------------ samples and expectations

        private static List<Sample> Samples()
        {
            const string ph = "RawAssets/cc0/textures/polyhaven/";
            const string rb = "RawAssets/cc0/characters/rocketbox/";
            const string fm = "RawAssets/falmedia/processed/";
            const string rifle = "RawAssets/fal3d/processed/rifle_tripo_fp_gltf/";
            var Tx = TextureImporterType.Default;
            var Nm = TextureImporterType.NormalMap;
            var D1 = TextureImporterFormat.DXT1Crunched;
            var D5 = TextureImporterFormat.DXT5Crunched;
            var list = new List<Sample>
            {
                // Env: hero ground set (manifest 2048), ground aniso 8; mask packed from the Poly Haven ARM.
                S(ph + "sand_01/sand_01_diff_2k.jpg", "Art/Textures/Env/sand_01/T_Sand01_BC.jpg", Tex(Tx, true, 2048, D1, 55, 8)),
                S(ph + "sand_01/sand_01_nor_gl_2k.jpg", "Art/Textures/Env/sand_01/T_Sand01_N.jpg", Tex(Nm, false, 2048, D5, 60, 8)),
                S(ph + "sand_01/sand_01_disp_2k.jpg", "Art/Textures/Env/sand_01/T_Sand01_H.jpg", Tex(Tx, false, 1024, D1, 55, 8)),
                G("Art/Textures/Env/sand_01/T_Sand01_M.png", d => MaskPacker.PackFromArm(ph + "sand_01/sand_01_arm_2k.jpg", d) == null ? "pack failed" : null,
                    Tex(Tx, false, 2048, D5, 60, 8)),
                // Env: normal set (manifest 1024), aniso 4.
                S(ph + "cracked_concrete_wall/cracked_concrete_wall_diff_2k.jpg", "Art/Textures/Env/cracked_concrete_wall/T_CrackedConcreteWall_BC.jpg", Tex(Tx, true, 1024, D1, 55, 4)),
                // Env/_Shared detail albedo: linear 512.
                S(Wp10 + "Art/Textures/Env/_Shared/T_MacroVariation_BC.png", "Art/Textures/Env/_Shared/T_MacroVariation_BC.png", Tex(Tx, false, 512, D1, 55, 4)),
                // 16-bit height source: editor-only R16 data.
                S(Wp10 + "Art/Textures/Env/Terrain003/T_Terrain003_H.png", "Art/Textures/Env/Terrain003/T_Terrain003_H.png",
                    Tex(Tx, false, 2048, TextureImporterFormat.R16, -1, 1, mips: false, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, readable: true, filter: FilterMode.Bilinear)),
                // HDRI: EditorOnly cube, sky panorama.
                S("RawAssets/cc0/hdri/goegap_2k.hdr", "Art/HDRI/EditorOnly/goegap_2k.hdr",
                    Tex(Tx, false, 2048, TextureImporterFormat.RGBAHalf, -1, 0, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, shape: TextureImporterShape.TextureCube)),
                S(Wp10 + "Art/HDRI/Runtime/T_Sky_kiara_1_dawn_LDR.png", "Art/HDRI/Runtime/T_Sky_RuleTest_LDR.png",
                    Tex(Tx, true, 2048, null, 70, 1, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear, mipFilter: TextureImporterMipFilter.KaiserFilter)),
                // Weapons (Tripo rifle maps): FP set BC/N 2048, mask 1024 packed from glTF metallic-roughness, magazine 512; q80.
                S(rifle + "baseColor_1.jpg", "Art/Textures/Weapons/AK/T_RuleTest_Rifle_BC.jpg", Tex(Tx, true, 2048, D1, 80, 4)),
                S(rifle + "normal_1.png", "Art/Textures/Weapons/AK/T_RuleTest_Rifle_N.png", Tex(Nm, false, 2048, D5, 80, 4)),
                G("Art/Textures/Weapons/AK/T_RuleTest_Rifle_M.png", d => MaskPacker.PackFromGltf(rifle + "metallicRoughness_1.jpg", d) == null ? "pack failed" : null,
                    Tex(Tx, false, 1024, D5, 80, 4)),
                S(rifle + "baseColor_1.jpg", "Art/Textures/Weapons/AK/T_RuleTest_Rifle_Mag_BC.jpg", Tex(Tx, true, 512, D1, 80, 4)),
                // Characters (Rocketbox TGAs): hero head 2048 q80, enemy normal 512 q70.
                S(rb + "Military_Male_01/Textures/sm002_head_color.tga", "Art/Textures/Characters/Hollis/T_RuleTest_Head_BC.tga", Tex(Tx, true, 2048, null, 80, 2)),
                S(rb + "Military_Male_01/Textures/sm002_body_normal.tga", "Art/Textures/Characters/LegionA/T_RuleTest_Body_N.tga", Tex(Nm, false, 512, D5, 70, 2)),
                // Props: hero (manifest 1024) and small (manifest 512).
                S(Wp10 + "Art/Textures/Props/teletype/T_Teletype_BC.png", "Art/Textures/Props/teletype/T_Teletype_BC.png", Tex(Tx, true, 1024, null, 55, 2)),
                S(Wp10 + "Art/Textures/Props/barrel_03/T_Barrel03_BC.png", "Art/Textures/Props/barrel_03/T_Barrel03_BC.png", Tex(Tx, true, 512, null, 55, 2)),
                S(Wp10 + "Art/Textures/Props/barrel_03/T_Barrel03_N.png", "Art/Textures/Props/barrel_03/T_Barrel03_N.png", Tex(Nm, false, 512, D5, 60, 2)),
                S(Wp10 + "Art/Textures/Props/barrel_03/T_Barrel03_M.png", "Art/Textures/Props/barrel_03/T_Barrel03_M.png", Tex(Tx, false, 512, D5, 60, 2)),
                // Decal: DXT5 clamp, alpha is transparency.
                S(Wp10 + "Art/Textures/Decals/tire_tracks/T_Decal_TireTracks_BC.png", "Art/Textures/Decals/tire_tracks/T_Decal_TireTracks_BC.png",
                    Tex(Tx, true, 1024, D5, 60, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, alphaIsTransparency: true)),
                S(Wp10 + "Art/Textures/Decals/tire_tracks/T_Decal_TireTracks_N.png", "Art/Textures/Decals/tire_tracks/T_Decal_TireTracks_N.png",
                    Tex(Nm, false, 1024, D5, 60, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp)),
                // Vegetation leaves: coverage-preserving mips.
                S(Wp10 + "Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_BC.png", "Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_BC.png",
                    Tex(Tx, true, 1024, D5, 55, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, coverage: true, alphaIsTransparency: true)),
                // Images: 1024, clamp, aniso 1.
                // 2400x1792 plate would scale to 1024x765 (not block-aligned): power-of-two resampling keeps it compressed.
                S("RawAssets/falmedia/07_image_crt_intel_nano-banana-2.1/00_XuSMFCbpNrZMp-KFIsbge_ViGv3fH3.png", "Art/Textures/Images/IMG_RuleTest.png",
                    Tex(Tx, true, 1024, null, 60, 1, mips: false, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear,
                        npot: TextureImporterNPOTScale.ToNearest)),
                // FX emissive atlas keeps the FX cap (not the surface-emission 512) at crunch q70.
                S(Wp10 + "Art/Textures/FX/T_VFX_MuzzleFlash_4x2_E.png", "Art/Textures/FX/T_VFX_MuzzleFlash_4x2_E.png",
                    Tex(Tx, true, 1024, D1, 70, 1, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear)),
                // Far impostor card: 256 cap, coverage mips, clamp; leaf-card normal clamps and stays at its 512 source size.
                S(Wp10 + "Art/Textures/Vegetation/olive_tree/T_OliveTree_A_Impostor_BC.png", "Art/Textures/Vegetation/olive_tree/T_OliveTree_A_Impostor_BC.png",
                    Tex(Tx, true, 256, D5, 55, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, coverage: true, alphaIsTransparency: true)),
                S(Wp10 + "Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_N.png", "Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_N.png",
                    Tex(Nm, false, 512, D5, 60, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp)),
                // Folder override from Tools/build/asset_budgets.json: the 1536x768 poster atlas keeps its resolution (2048).
                S(Wp10 + "Art/Textures/Decals/poster_art/T_Decal_PosterArt_BC.png", "Art/Textures/Decals/poster_art/T_Decal_PosterArt_BC.png",
                    Tex(Tx, true, 2048, D5, 60, 2, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, alphaIsTransparency: true)),
                // CRT/cinematic plate (WP09): no mips, 4:3 kept (NPOT None) -> imported 1024x768 DXT1 crunched.
                S("RawAssets/wp09/ready/Assets/_Game/Art/Textures/Images/T_AirportPlate_BC.png", "Art/Textures/Images/T_AirportPlate_BC.png",
                    Tex(Tx, true, 1024, D1, 60, 1, mips: false, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear,
                        npot: TextureImporterNPOTScale.None, importedSize: new Vector2Int(1024, 768))),
                // Intel card photo: Sprite for IHud.IntelCard, no mips.
                S("RawAssets/wp09/ready/Assets/_Game/Art/Textures/Images/T_IntelCodebook_BC.png", "Art/Textures/Images/T_IntelCodebook_BC.png",
                    Tex(TextureImporterType.Sprite, true, 1024, D1, 60, 1, mips: false, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear,
                        npot: TextureImporterNPOTScale.None)),
                // Editor-only look-dev reference: full size, plain DXT1 (no crunch artefacts), no mips.
                S(Wp10 + "Art/Textures/Images/concept_keyframes.png", "Art/Textures/Images/concept_keyframes.png",
                    Tex(Tx, true, 2048, TextureImporterFormat.DXT1, 100, 1, mips: false, wrapU: TextureWrapMode.Clamp, wrapV: TextureWrapMode.Clamp, filter: FilterMode.Bilinear)),
                // UI sprites: <= 128 px uncompressed, larger DXT5 crunched q70; no mips; Max Size = source size (below the cap).
                G("UI/Sprites/icons/T_RuleTest_Icon.png", d => WritePng(d, 64), Sprite(64, TextureImporterFormat.RGBA32)),
                G("UI/Sprites/click_gate/T_RuleTest_Gate.png", d => WritePng(d, 256), Sprite(256, D5)),

                // Models.
                S("RawAssets/wp00/samples/SM_RuleTest_Rifle_W.fbx", "Art/Models/Weapons/AK/SM_RuleTest_Rifle_W.fbx",
                    Model(ModelImporterAnimationType.None, ModelImporterMeshCompression.Medium, meshLods: true)),
                S("RawAssets/wp00/samples/SM_RuleTest_Rifle_FP.fbx", "Art/Models/Weapons/AK/SM_RuleTest_Rifle_FP.fbx",
                    Model(ModelImporterAnimationType.None, ModelImporterMeshCompression.Off, meshLods: false)),
                S(Wp10 + "Art/Models/Props/ammo_box/SM_AmmoBox.fbx", "Art/Models/Props/ammo_box/SM_AmmoBox.fbx",
                    Model(ModelImporterAnimationType.None, ModelImporterMeshCompression.Medium, meshLods: false, lodGroup: true)),
                S(Wp10 + "Art/Models/Props/barrel_03/SM_Barrel03.fbx", "Art/Models/Props/barrel_03/SM_Barrel03.fbx",
                    Model(ModelImporterAnimationType.None, ModelImporterMeshCompression.Medium, meshLods: true)),
                // Big lightmapped prop (asset_budgets.json model.lightmapped_props, D8): lightmap UVs, authored LODs.
                S(Wp10 + "Art/Models/Props/rock_face_01/SM_RockFace01.fbx", "Art/Models/Props/rock_face_01/SM_RockFace01.fbx",
                    Model(ModelImporterAnimationType.None, ModelImporterMeshCompression.Medium, meshLods: false, lodGroup: true, lightmapUVs: true)),
                S(rb + "Military_Male_01/Export/Military_Male_01.fbx", "Art/Models/Characters/RuleTest/SK_RuleTest.fbx",
                    Model(ModelImporterAnimationType.Human, ModelImporterMeshCompression.Off, meshLods: false, materials: ModelImporterMaterialImportMode.None, humanAvatar: true)),
                S(rb + "Animations/m_walk_neutral_01.max.fbx", "Art/Animation/Clips/Rocketbox/A_RuleTest_Walk.fbx",
                    Model(ModelImporterAnimationType.Human, ModelImporterMeshCompression.Off, meshLods: false, materials: ModelImporterMaterialImportMode.None, humanAvatar: true, humanClip: true)),

                // Audio.
                S(fm + "sfx_ak_shot_mirelo_a_punch.wav", "Audio/SFX/Weapons/SFX_RuleTest_SHOT_01.wav", Audio(true, AudioClipLoadType.DecompressOnLoad, true, false, 70, 0)),
                S(fm + "sfx_ak_shot_mirelo_b.wav", "Audio/SFX/Weapons/Far/SFX_RuleTest_SHOT_FAR_01.wav", Audio(true, AudioClipLoadType.DecompressOnLoad, true, false, 70, 0)),
                S(fm + "sfx_explosion_large_a_punch.wav", "Audio/SFX/SetPieces/SFX_RuleTest_SET_01.wav", Audio(true, AudioClipLoadType.CompressedInMemory, true, false, 60, 0)),
                S(fm + "sfx_ak_shot_elevenlabs.wav", "Audio/SFX/UI/SFX_UI_RuleTest_01.wav", Audio(true, AudioClipLoadType.DecompressOnLoad, true, false, 60, 22050)),
                S(fm + "vo_radio_bravo2_hold_el4_brian.wav", "Audio/VO/RADIO/VO_RuleTest_RADIO.wav", Audio(true, AudioClipLoadType.CompressedInMemory, false, true, 60, 22050)),
                S(fm + "vo_clean_bravo2_hold_el4_brian.wav", "Audio/VO/DRY/VO_RuleTest_DRY.wav", Audio(true, AudioClipLoadType.CompressedInMemory, false, true, 70, 24000)),
                S(fm + "mus_briefing_loop_6bars.wav", "Audio/Music/MUS_RuleTest.wav", Audio(false, AudioClipLoadType.CompressedInMemory, false, true, 60, 0)),
                S(fm + "mus_briefing_loop_6bars.wav", "Audio/Ambience/AMB_RuleTest.wav", Audio(false, AudioClipLoadType.CompressedInMemory, false, true, 50, 0)),
            };
            return list;
        }

        private static Sample S(string source, string rel, Action<string, List<string>> check) =>
            new Sample { Source = source, Dest = T + rel, Check = check };

        private static Sample G(string rel, Func<string, string> generate, Action<string, List<string>> check) =>
            new Sample { Dest = T + rel, Generate = generate, Check = check };

        private static void Expect<TV>(List<string> errors, string what, TV actual, TV expected)
        {
            if (!Equals(actual, expected)) errors.Add(what + ": " + actual + " (expected " + expected + ")");
        }

        /// <summary>Texture expectation; <paramref name="format"/> null = DXT1/DXT5 crunched by real alpha; quality -1 = not compressed.</summary>
        private static Action<string, List<string>> Tex(TextureImporterType type, bool srgb, int max, TextureImporterFormat? format, int quality, int aniso,
            bool mips = true, TextureWrapMode wrapU = TextureWrapMode.Repeat, TextureWrapMode wrapV = TextureWrapMode.Repeat, bool coverage = false,
            bool alphaIsTransparency = false, bool readable = false, TextureImporterShape shape = TextureImporterShape.Texture2D,
            FilterMode filter = FilterMode.Trilinear, TextureImporterMipFilter mipFilter = TextureImporterMipFilter.BoxFilter,
            TextureImporterNPOTScale? npot = null, Vector2Int? importedSize = null)
        {
            return (path, e) =>
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Expect(e, "textureType", ti.textureType, type);
                Expect(e, "textureShape", ti.textureShape, shape);
                if (type != TextureImporterType.NormalMap && shape == TextureImporterShape.Texture2D) Expect(e, "sRGB", ti.sRGBTexture, srgb);
                Expect(e, "mipmaps", ti.mipmapEnabled, mips);
                if (mips && shape == TextureImporterShape.Texture2D) Expect(e, "mipFilter", ti.mipmapFilter, mipFilter);
                Expect(e, "aniso", ti.anisoLevel, aniso);
                Expect(e, "filter", ti.filterMode, filter);
                Expect(e, "wrapU", ti.wrapModeU, wrapU);
                Expect(e, "wrapV", ti.wrapModeV, wrapV);
                Expect(e, "preserveCoverage", ti.mipMapsPreserveCoverage, coverage);
                Expect(e, "alphaIsTransparency", ti.alphaIsTransparency, alphaIsTransparency);
                Expect(e, "readable", ti.isReadable, readable);
                Expect(e, "streamingMipmaps", ti.streamingMipmaps, false);
                var w = ti.GetPlatformTextureSettings("WebGL");
                Expect(e, "WebGL override", w.overridden, true);
                Expect(e, "WebGL max", w.maxTextureSize, max);
                if (format.HasValue) Expect(e, "WebGL format", w.format, format.Value);
                else if (w.format != TextureImporterFormat.DXT1Crunched && w.format != TextureImporterFormat.DXT5Crunched)
                    e.Add("WebGL format: " + w.format + " (expected DXT1/DXT5 crunched)");
                else
                {
                    var alpha = TextureSourceProbe.Probe(path).AlphaUsed;
                    Expect(e, "WebGL format (alpha " + alpha + ")", w.format, alpha ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched);
                }
                if (quality >= 0) Expect(e, "WebGL quality", w.compressionQuality, quality);
                if (npot.HasValue) Expect(e, "npotScale", ti.npotScale, npot.Value);
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (tex == null)
                {
                    e.Add("texture did not import");
                    return;
                }
                if (importedSize.HasValue) Expect(e, "imported size", new Vector2Int(tex.width, tex.height), importedSize.Value);
                // With WebGL active the imported texture is the Web data: a compressed rule must not end up uncompressed
                // (Unity silently falls back to RGB(A) for e.g. NPOT + mips or non-multiple-of-4 sizes).
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && tex is Texture2D t2 &&
                    (w.format == TextureImporterFormat.DXT1Crunched || w.format == TextureImporterFormat.DXT5Crunched || w.format == TextureImporterFormat.DXT1))
                {
                    var f = t2.format;
                    if (f != TextureFormat.DXT1 && f != TextureFormat.DXT5 && f != TextureFormat.DXT1Crunched && f != TextureFormat.DXT5Crunched && f != TextureFormat.BC5)
                        e.Add("imported format " + f + " is not block-compressed (expected " + w.format + ")");
                }
            };
        }

        private static Action<string, List<string>> Sprite(int max, TextureImporterFormat format)
        {
            return (path, e) =>
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Expect(e, "textureType", ti.textureType, TextureImporterType.Sprite);
                Expect(e, "mipmaps", ti.mipmapEnabled, false);
                Expect(e, "alphaIsTransparency", ti.alphaIsTransparency, true);
                Expect(e, "wrapU", ti.wrapModeU, TextureWrapMode.Clamp);
                var w = ti.GetPlatformTextureSettings("WebGL");
                Expect(e, "WebGL max", w.maxTextureSize, max);
                Expect(e, "WebGL format", w.format, format);
                if (format == TextureImporterFormat.DXT5Crunched) Expect(e, "WebGL quality", w.compressionQuality, 70);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path) == null) e.Add("no Sprite sub-asset");
            };
        }

        private static Action<string, List<string>> Model(ModelImporterAnimationType anim, ModelImporterMeshCompression compression, bool meshLods,
            ModelImporterMaterialImportMode materials = ModelImporterMaterialImportMode.ImportViaMaterialDescription, bool lodGroup = false,
            bool humanAvatar = false, bool humanClip = false, bool lightmapUVs = false)
        {
            return (path, e) =>
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                Expect(e, "animationType", mi.animationType, anim);
                Expect(e, "meshCompression", mi.meshCompression, compression);
                Expect(e, "generateMeshLods", mi.generateMeshLods, meshLods);
                Expect(e, "materialImportMode", mi.materialImportMode, materials);
                Expect(e, "importNormals", mi.importNormals, ModelImporterNormals.Import);
                Expect(e, "importTangents", mi.importTangents, ModelImporterTangents.CalculateMikk);
                Expect(e, "generateSecondaryUV", mi.generateSecondaryUV, lightmapUVs);
                Expect(e, "isReadable", mi.isReadable, false);
                Expect(e, "bakeAxisConversion", mi.bakeAxisConversion, false);
                Expect(e, "importCameras", mi.importCameras, false);
                Expect(e, "importLights", mi.importLights, false);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    e.Add("model did not import");
                    return;
                }
                if (lodGroup && go.GetComponent<LODGroup>() == null) e.Add("no LODGroup for the authored *_LODn meshes");
                if (lightmapUVs)
                {
                    var hasUv2 = false;
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null && mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0) hasUv2 = true;
                    if (!hasUv2) e.Add("no lightmap UVs (uv2) in the imported meshes");
                }
                if (humanAvatar)
                {
                    Avatar avatar = null;
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (o is Avatar a) avatar = a;
                    if (avatar == null) e.Add("no avatar");
                    else if (!avatar.isHuman || !avatar.isValid) e.Add("avatar is not a valid humanoid");
                }
                if (humanClip)
                {
                    Expect(e, "animationCompression", mi.animationCompression, ModelImporterAnimationCompression.Optimal);
                    var found = false;
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.humanMotion) found = true;
                    if (!found) e.Add("no humanoid AnimationClip");
                }
            };
        }

        private static Action<string, List<string>> Audio(bool mono, AudioClipLoadType loadType, bool preload, bool background, int quality, int sampleRate)
        {
            return (path, e) =>
            {
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                Expect(e, "forceToMono", ai.forceToMono, mono);
                Expect(e, "loadInBackground", ai.loadInBackground, background);
                var norm = new SerializedObject(ai).FindProperty("m_Normalize");
                if (norm != null) Expect(e, "normalize", norm.boolValue, false);
                var s = ai.defaultSampleSettings;
                Expect(e, "loadType", s.loadType, loadType);
                Expect(e, "preload", s.preloadAudioData, preload);
                Expect(e, "quality", Mathf.RoundToInt(s.quality * 100f), quality);
                Expect(e, "sampleRateSetting", s.sampleRateSetting, sampleRate > 0 ? AudioSampleRateSetting.OverrideSampleRate : AudioSampleRateSetting.PreserveSampleRate);
                if (sampleRate > 0) Expect(e, "sampleRate", (int)s.sampleRateOverride, sampleRate);
                // Unity 6000.6 refuses WebGL sample-setting overrides; the Web output (AAC) follows the default settings.
                Expect(e, "WebGL override present", ai.ContainsSampleSettingsOverride("WebGL"), false);
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
                {
                    var o = AudioRuleApplier.ReadOutput(ai);
                    if (!o.Valid) e.Add("no import result for WebGL");
                    else
                    {
                        Expect(e, "imported codec (WebGL)", o.Format, AudioCompressionFormat.AAC);
                        Expect(e, "imported load type (WebGL)", o.LoadType, loadType);
                        Expect(e, "imported preload (WebGL)", o.Preload, preload);
                        Expect(e, "imported quality (WebGL)", Mathf.RoundToInt(o.Quality * 100f), quality);
                    }
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) e.Add("clip did not import");
                else if (mono) Expect(e, "channels", clip.channels, 1);
            };
        }

        private static string WritePng(string assetPath, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var px = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var r = Mathf.Sqrt((x - size / 2f) * (x - size / 2f) + (y - size / 2f) * (y - size / 2f)) / (size / 2f);
                    px[y * size + x] = new Color32(230, 220, 200, (byte)(r < 1f ? 255 * (1f - r) : 0));
                }
                tex.SetPixels32(px);
                tex.Apply();
                var full = Path.Combine(BuilderUtil.ProjectRoot, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
                File.WriteAllBytes(full, tex.EncodeToPNG());
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string Describe(AssetImporter importer, string path)
        {
            switch (importer)
            {
                case TextureImporter ti:
                    var r = TextureRules.Resolve(path, TextureSourceProbe.Probe(path, ti));
                    return r != null ? r.ToString() : "no rule";
                case ModelImporter _:
                    var m = ModelRules.Resolve(path, ModelSourceProbe.Probe(path));
                    return m != null ? m.ToString() : "no rule";
                case AudioImporter ai:
                    var a = AudioRules.Resolve(path);
                    return a != null ? a + " | imported: " + AudioRuleApplier.ReadOutput(ai) : "no rule";
                default:
                    return importer.GetType().Name;
            }
        }

        // ------------------------------------------------------------------ materials and binding

        private static void CheckMaterials(Action<string, List<string>, string> report)
        {
            // 1) Texture set with packed mask, height and world tiling.
            var sandSet = TextureSet.FromMember(T + "Art/Textures/Env/sand_01/T_Sand01_BC.jpg");
            var e = new List<string>();
            var sand = MaterialFactory.BuildFromTextureSet(sandSet, new MaterialOptions { TileMeters = 1.5f });
            if (sand == null) e.Add("not built");
            else
            {
                Expect(e, "path", AssetDatabase.GetAssetPath(sand), T + "Art/Materials/Env/M_Sand01.mat");
                Expect(e, "shader", sand.shader.name, MaterialFactory.LitShaderName);
                foreach (var k in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_PARALLAXMAP" })
                    if (!sand.IsKeywordEnabled(k)) e.Add("keyword " + k + " off");
                if (sand.GetTexture("_MetallicGlossMap") == null || sand.GetTexture("_MetallicGlossMap") != sand.GetTexture("_OcclusionMap"))
                    e.Add("D4: mask must be in _MetallicGlossMap and _OcclusionMap");
                Expect(e, "_SmoothnessTextureChannel", sand.GetFloat("_SmoothnessTextureChannel"), 0f);
                if (Mathf.Abs(sand.GetTextureScale("_BaseMap").x - 1f / 1.5f) > 1e-3f) e.Add("tiling " + sand.GetTextureScale("_BaseMap").x);
            }
            report("M_Sand01 from texture set (BC/N/M/H, tile 1.5 m)", e, sandSet?.ToString());

            // 2) FP weapon set with a detail normal.
            e = new List<string>();
            var rifleSet = TextureSet.FromMember(T + "Art/Textures/Weapons/AK/T_RuleTest_Rifle_BC.jpg");
            var rifle = MaterialFactory.BuildFromTextureSet(rifleSet, new MaterialOptions { DetailNormal = T + "Art/Textures/Env/sand_01/T_Sand01_N.jpg", DetailTiling = 6f });
            if (rifle == null) e.Add("not built");
            else
            {
                Expect(e, "path", AssetDatabase.GetAssetPath(rifle), T + "Art/Materials/Weapons/M_RuleTest_Rifle.mat");
                if (!rifle.IsKeywordEnabled("_DETAIL_MULX2")) e.Add("keyword _DETAIL_MULX2 off");
                if (Mathf.Abs(rifle.GetTextureScale("_DetailAlbedoMap").x - 6f) > 1e-3f) e.Add("detail tiling " + rifle.GetTextureScale("_DetailAlbedoMap").x);
            }
            report("M_RuleTest_Rifle from texture set (+detail normal)", e, rifleSet?.ToString());

            // 3) Props set (binding target) and decal set.
            e = new List<string>();
            var barrel = MaterialFactory.BuildFromBaseMap(T + "Art/Textures/Props/barrel_03/T_Barrel03_BC.png");
            if (barrel == null || barrel.GetTexture("_MetallicGlossMap") == null) e.Add("M_Barrel03 missing or without mask");
            report("M_Barrel03 from texture set", e, null);

            e = new List<string>();
            var decal = MaterialFactory.BuildFromBaseMap(T + "Art/Textures/Decals/tire_tracks/T_Decal_TireTracks_BC.png");
            if (decal == null) e.Add("not built");
            else if (Shader.Find(MaterialFactory.DecalShaderName) != null)
            {
                Expect(e, "shader", decal.shader.name, MaterialFactory.DecalShaderName);
                if (decal.GetTexture("Base_Map") == null) e.Add("Base_Map empty");
                if (!decal.enableInstancing) e.Add("instancing off");
            }
            report("M_Decal_TireTracks (URP decal)", e, null);

            // 4) Spec file in the content agents' schema: transparent glass, emissive lamp, ground with detail + TerrainLayer.
            var tx = T + "Art/Textures/";
            var json = "[" +
                       "{\"manifest_id\":\"TEST_GLASS\",\"material\":\"M_RuleTest_Glass\",\"target_path\":\"" + T + "Art/Materials/Props/M_RuleTest_Glass.mat\"," +
                       "\"shader\":\"Universal Render Pipeline/Lit\",\"textures\":{\"_BaseMap\":\"" + tx + "Props/teletype/T_Teletype_BC.png\"}," +
                       "\"floats\":{\"_Smoothness\":0.95},\"colors\":{\"_BaseColor\":[1,1,1,0.28]},\"surface\":\"Transparent\",\"blend\":\"Alpha\",\"render_face\":\"Both\",\"zwrite\":false}," +
                       "{\"manifest_id\":\"TEST_LAMP\",\"material\":\"M_RuleTest_Lamp\",\"target_path\":\"" + T + "Art/Materials/Props/M_RuleTest_Lamp.mat\"," +
                       "\"textures\":{\"_BaseMap\":\"" + tx + "Props/barrel_03/T_Barrel03_BC.png\",\"_MetallicGlossMap\":\"" + tx + "Props/barrel_03/T_Barrel03_M.png\"}," +
                       "\"emission\":{\"_EmissionMap\":\"" + tx + "Props/teletype/T_Teletype_BC.png\",\"_EmissionColor_linear\":[0,0,0]}}," +
                       "{\"manifest_id\":\"TEST_GROUND\",\"material\":\"M_RuleTest_Ground\",\"target_path\":\"" + T + "Art/Materials/Env/M_RuleTest_Ground.mat\"," +
                       "\"textures\":{\"_BaseMap\":\"" + tx + "Env/sand_01/T_Sand01_BC.jpg\",\"_BumpMap\":\"" + tx + "Env/sand_01/T_Sand01_N.jpg\",\"_MetallicGlossMap\":\"" + tx + "Env/sand_01/T_Sand01_M.png\"}," +
                       "\"tile_m\":1.5,\"world_uv_tiling\":0.6667,\"detail\":{\"albedo\":\"" + tx + "Env/_Shared/T_MacroVariation_BC.png\",\"tile_m\":25}," +
                       "\"terrain_layer\":{\"asset\":\"" + T + "Art/Materials/Env/TL_RuleTest_Ground.terrainlayer\",\"diffuse\":\"" + tx + "Env/sand_01/T_Sand01_BC.jpg\"," +
                       "\"normal\":\"" + tx + "Env/sand_01/T_Sand01_N.jpg\",\"mask\":\"" + tx + "Env/sand_01/T_Sand01_M.png\",\"tile_size_m\":1.5}}," +
                       "{\"manifest_id\":\"TEST_DECAL\",\"material\":\"M_RuleTest_Decal\",\"target_path\":\"" + T + "Art/Materials/Decals/M_RuleTest_Decal.mat\"," +
                       "\"shader\":\"Shader Graphs/Decal\",\"textures\":{\"Base_Map\":\"" + tx + "Decals/tire_tracks/T_Decal_TireTracks_BC.png\"}," +
                       "\"floats\":{\"Normal_Blend\":0.6,\"DrawOrder\":3},\"toggles\":{\"Affect BaseColor\":true},\"affect\":{\"MAOS\":false}}" +
                       "]";
            var specFull = Path.Combine(BuilderUtil.ProjectRoot, SpecTempPath);
            Directory.CreateDirectory(Path.GetDirectoryName(specFull) ?? ".");
            File.WriteAllText(specFull, json);
            var built = MaterialFactory.BuildFromSpecFile(SpecTempPath);
            e = new List<string>();
            Expect(e, "materials built", built.Count, 4);
            var glass = AssetDatabase.LoadAssetAtPath<Material>(T + "Art/Materials/Props/M_RuleTest_Glass.mat");
            if (glass == null || glass.renderQueue < 3000 || !glass.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) e.Add("glass is not transparent");
            else
            {
                Expect(e, "glass _Cull (render_face Both)", glass.GetFloat("_Cull"), 0f);
                Expect(e, "glass _ZWrite (transparent)", glass.GetFloat("_ZWrite"), 0f);
            }
            var specDecal = AssetDatabase.LoadAssetAtPath<Material>(T + "Art/Materials/Decals/M_RuleTest_Decal.mat");
            if (specDecal != null && specDecal.shader.name == MaterialFactory.DecalShaderName)
                Expect(e, "decal _DrawOrder (spec DrawOrder)", specDecal.GetFloat("_DrawOrder"), 3f);
            else if (Shader.Find(MaterialFactory.DecalShaderName) != null) e.Add("spec decal missing or not a decal");
            var lamp = AssetDatabase.LoadAssetAtPath<Material>(T + "Art/Materials/Props/M_RuleTest_Lamp.mat");
            if (lamp == null || !lamp.IsKeywordEnabled("_EMISSION")) e.Add("lamp lost _EMISSION");
            if (lamp != null && lamp.GetTexture("_OcclusionMap") != lamp.GetTexture("_MetallicGlossMap")) e.Add("lamp: D4 occlusion not filled from the mask");
            var ground = AssetDatabase.LoadAssetAtPath<Material>(T + "Art/Materials/Env/M_RuleTest_Ground.mat");
            if (ground == null || !ground.IsKeywordEnabled("_DETAIL_MULX2")) e.Add("ground: detail albedo keyword off");
            else if (Mathf.Abs(ground.GetTextureScale("_DetailAlbedoMap").x - 1.5f / 25f) > 1e-3f) e.Add("ground: detail tiling " + ground.GetTextureScale("_DetailAlbedoMap").x);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(T + "Art/Materials/Env/TL_RuleTest_Ground.terrainlayer");
            if (layer == null) e.Add("TerrainLayer missing");
            else if (layer.diffuseTexture == null || layer.maskMapTexture == null || Mathf.Abs(layer.tileSize.x - 1.5f) > 1e-3f)
                e.Add("TerrainLayer wrong: diffuse " + layer.diffuseTexture + ", mask " + layer.maskMapTexture + ", tile " + layer.tileSize);
            report("Spec file (transparent two-sided glass, emissive lamp, detail ground + TerrainLayer, decal)", e, built.Count + " built");
        }

        private static void CheckBinding(Action<string, List<string>, string> report)
        {
            foreach (var model in new[]
                     {
                         T + "Art/Models/Weapons/AK/SM_RuleTest_Rifle_W.fbx", T + "Art/Models/Weapons/AK/SM_RuleTest_Rifle_FP.fbx",
                         T + "Art/Models/Props/barrel_03/SM_Barrel03.fbx",
                     })
            {
                var e = new List<string>();
                if (AssetImporter.GetAtPath(model) == null)
                {
                    report(model, new List<string> { "model missing" }, null);
                    continue;
                }
                var r = ModelMaterialBinder.Bind(model);
                if (r.Bound.Count == 0) e.Add("nothing bound; slots: " + string.Join(", ", ModelMaterialBinder.SlotNames(model)));
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(model);
                foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in rend.sharedMaterials)
                    if (m == null || AssetDatabase.GetAssetPath(m) == model) e.Add(rend.name + " still uses the importer's own material");
                report("bind " + Path.GetFileName(model), e, r.ToString());
            }
        }
    }
}
