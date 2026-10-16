using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    /// <summary>Rule tables of TECH_PLAN §3.10–3.12 (pure resolution, no import).</summary>
    public class ImportRulesTests
    {
        private static TextureSourceInfo Opaque(int size = 2048) => new TextureSourceInfo { Known = true, Width = size, Height = size, BitsPerChannel = 8 };
        private static TextureSourceInfo WithAlpha(int size = 1024) => new TextureSourceInfo { Known = true, Width = size, Height = size, BitsPerChannel = 8, HasAlphaChannel = true, AlphaUsed = true };

        [Test]
        public void PathsOutsideTheGameRootAreNotManaged()
        {
            Assert.IsNull(TextureRules.Resolve("Assets/WebGLTest/foo_BC.png", Opaque()));
            Assert.IsNull(ModelRules.Resolve("Assets/Scenes/x.fbx", default));
            Assert.IsNull(AudioRules.Resolve("Assets/WebGLTest/a.wav"));
            Assert.AreEqual("Art/Textures/Env/a/T_A_BC.png", ImportRulePaths.ToRuleRelative("Assets/__ImportRuleTest/Art/Textures/Env/a/T_A_BC.png"));
        }

        [Test]
        public void SuffixParsing()
        {
            Assert.AreEqual(TextureSuffix.N, TextureNaming.ParseSuffix("T_WPN_AK_Mag_N.png", out var set));
            Assert.AreEqual("WPN_AK_Mag", set);
            Assert.AreEqual(TextureSuffix.BC, TextureNaming.ParseSuffix("T_VFX_DustImpact_8x8_BC"));
            Assert.AreEqual(TextureSuffix.None, TextureNaming.ParseSuffix("T_Sky_kiara_1_dawn_LDR.png"));
            Assert.AreEqual(TextureSuffix.None, TextureNaming.ParseSuffix("rock_a.png"));
        }

        [Test]
        public void EnvSetsFollowSuffixRows()
        {
            var bc = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/cracked_concrete_wall/T_CrackedConcreteWall_BC.png", Opaque());
            Assert.AreEqual(TextureImporterType.Default, bc.Type);
            Assert.IsTrue(bc.SRGB);
            Assert.AreEqual(1024, bc.MaxSize);
            Assert.AreEqual(TextureImporterFormat.DXT1Crunched, bc.WebGLFormat);
            Assert.AreEqual(55, bc.Quality);
            Assert.AreEqual(4, bc.Aniso);

            var n = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/cracked_concrete_wall/T_CrackedConcreteWall_N.png", Opaque());
            Assert.AreEqual(TextureImporterType.NormalMap, n.Type);
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, n.WebGLFormat);
            Assert.AreEqual(60, n.Quality);

            var m = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/cracked_concrete_wall/T_CrackedConcreteWall_M.png", Opaque());
            Assert.IsFalse(m.SRGB);
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, m.WebGLFormat);

            var withAlpha = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/cracked_concrete_wall/T_CrackedConcreteWall_BC.png", WithAlpha());
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, withAlpha.WebGLFormat);
        }

        [Test]
        public void HeroAndGroundSets()
        {
            var sand = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/sand_01/T_Sand01_BC.png", Opaque());
            Assert.AreEqual(2048, sand.MaxSize);
            Assert.AreEqual(8, sand.Aniso);
            var sandH = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/sand_01/T_Sand01_H.png", Opaque());
            Assert.AreEqual(1024, sandH.MaxSize);
            Assert.IsFalse(sandH.SRGB);
        }

        [Test]
        public void SixteenBitHeightIsEditorOnlyData()
        {
            var info = new TextureSourceInfo { Known = true, Width = 2048, Height = 2048, BitsPerChannel = 16 };
            var r = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/Terrain003/T_Terrain003_H.png", info);
            Assert.AreEqual(TextureImporterFormat.R16, r.WebGLFormat);
            Assert.IsTrue(r.Readable);
            Assert.IsFalse(r.Mipmaps);
            Assert.IsTrue(r.EditorOnly);
        }

        [Test]
        public void SharedDetailAlbedoIsLinear()
        {
            var r = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/_Shared/T_MacroVariation_BC.png", Opaque(512));
            Assert.IsFalse(r.SRGB);
            Assert.AreEqual(512, r.MaxSize);
        }

        [Test]
        public void WeaponCaps()
        {
            Assert.AreEqual(2048, TextureRules.Resolve("Assets/_Game/Art/Textures/Weapons/AK/T_WPN_AK_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(1024, TextureRules.Resolve("Assets/_Game/Art/Textures/Weapons/AK/T_WPN_AK_M.png", Opaque()).MaxSize);
            Assert.AreEqual(512, TextureRules.Resolve("Assets/_Game/Art/Textures/Weapons/AK/T_WPN_AK_Mag_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(1024, TextureRules.Resolve("Assets/_Game/Art/Textures/Weapons/SVD/T_WPN_SVD_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(80, TextureRules.Resolve("Assets/_Game/Art/Textures/Weapons/SVD/T_WPN_SVD_N.png", Opaque()).Quality);
        }

        [Test]
        public void CharacterCaps()
        {
            Assert.AreEqual(2048, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/Hollis/T_Hollis_Head_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(80, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/Hollis/T_Hollis_Head_BC.png", Opaque()).Quality);
            Assert.AreEqual(1024, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/Hollis/T_Hollis_Body_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(70, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/Hollis/T_Hollis_Body_BC.png", Opaque()).Quality);
            Assert.AreEqual(512, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/LegionA/T_LegionA_Body_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(512, TextureRules.Resolve("Assets/_Game/Art/Textures/Characters/Props/CHP_Boonie/T_CHP_Boonie_BC.png", Opaque()).MaxSize);
        }

        [Test]
        public void PropsUseHeroListOrSmallCap()
        {
            Assert.AreEqual(1024, TextureRules.Resolve("Assets/_Game/Art/Textures/Props/teletype/T_Teletype_BC.png", Opaque()).MaxSize);
            Assert.AreEqual(512, TextureRules.Resolve("Assets/_Game/Art/Textures/Props/some_new_prop/T_SomeNewProp_BC.png", Opaque()).MaxSize);
        }

        [Test]
        public void VehicleDataMapsCappedAt1024()
        {
            Assert.AreEqual(1024, TextureRules.Resolve("Assets/_Game/Art/Textures/Vehicles/helicopter/T_Helicopter_N.png", Opaque()).MaxSize);
        }

        [Test]
        public void FoliageKeepsCoverage()
        {
            var r = TextureRules.Resolve("Assets/_Game/Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_BC.png", WithAlpha());
            Assert.IsTrue(r.PreserveCoverage);
            Assert.IsTrue(r.AlphaIsTransparency);
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, r.WebGLFormat);
        }

        [Test]
        public void DecalsSkyHdriAndSprites()
        {
            var decal = TextureRules.Resolve("Assets/_Game/Art/Textures/Decals/scorch/T_Decal_Scorch_BC.png", Opaque());
            Assert.AreEqual(TextureImporterFormat.DXT5Crunched, decal.WebGLFormat);
            Assert.AreEqual(TextureWrapMode.Clamp, decal.WrapU);

            var sky = TextureRules.Resolve("Assets/_Game/Art/HDRI/Runtime/T_Sky_kiara_8_sunset_LDR.png", Opaque());
            Assert.AreEqual(TextureWrapMode.Repeat, sky.WrapU);
            Assert.AreEqual(TextureWrapMode.Clamp, sky.WrapV);
            Assert.AreEqual(70, sky.Quality);

            var hdri = TextureRules.Resolve("Assets/_Game/Art/HDRI/EditorOnly/kiara_8_sunset_nosun_2k.exr", new TextureSourceInfo { Known = true, IsHdr = true, Width = 2048, Height = 1024 });
            Assert.AreEqual(TextureImporterShape.TextureCube, hdri.Shape);
            Assert.AreEqual(TextureImporterFormat.RGBAHalf, hdri.WebGLFormat);
            Assert.IsTrue(hdri.EditorOnly);

            var icon = TextureRules.Resolve("Assets/_Game/UI/Sprites/icons/T_Icon_Frag.png", WithAlpha(64));
            Assert.AreEqual(TextureImporterType.Sprite, icon.Type);
            Assert.AreEqual(TextureImporterFormat.RGBA32, icon.WebGLFormat);
            Assert.IsFalse(icon.Mipmaps);
        }

        [Test]
        public void MaxSizeNeverExceedsTheSource()
        {
            var small = TextureRules.Resolve("Assets/_Game/Art/Textures/Env/hessian_230/T_Hessian230_A.png", Opaque(512));
            Assert.AreEqual(512, small.MaxSize);
            StringAssert.Contains("source 512x512", small.CapSource);
            // NPOT sources round up (Unity rescales them to the nearest power of two first).
            Assert.AreEqual(2048, TextureRules.Resolve("Assets/_Game/Art/Textures/Env/sand_01/T_Sand01_BC.png", new TextureSourceInfo { Known = true, Width = 1536, Height = 768, BitsPerChannel = 8 }).MaxSize);
            // Unknown sources keep the class cap.
            Assert.AreEqual(2048, TextureRules.Resolve("Assets/_Game/Art/Textures/Env/sand_01/T_Sand01_BC.png", default).MaxSize);
        }

        [Test]
        public void CardsImpostorsAndBlurDiscsClamp()
        {
            var imp = TextureRules.Resolve("Assets/_Game/Art/Textures/Vegetation/olive_tree/T_OliveTree_A_Impostor_BC.png", WithAlpha(512));
            Assert.AreEqual(256, imp.MaxSize);
            Assert.IsTrue(imp.PreserveCoverage);
            Assert.AreEqual(TextureWrapMode.Clamp, imp.WrapU);
            var leavesN = TextureRules.Resolve("Assets/_Game/Art/Textures/Vegetation/olive_tree/T_OliveTree_Leaves_N.png", Opaque(512));
            Assert.AreEqual(TextureWrapMode.Clamp, leavesN.WrapU);
            Assert.AreEqual(TextureImporterType.NormalMap, leavesN.Type);
            var bark = TextureRules.Resolve("Assets/_Game/Art/Textures/Vegetation/olive_tree/T_OliveTree_Bark_BC.png", Opaque(512));
            Assert.AreEqual(TextureWrapMode.Repeat, bark.WrapU);
            var blur = TextureRules.Resolve("Assets/_Game/Art/Textures/Vehicles/helicopter/T_HeliRotorBlur_BC.png", WithAlpha(512));
            Assert.AreEqual(TextureWrapMode.Clamp, blur.WrapU);
            Assert.IsTrue(blur.AlphaIsTransparency);
            Assert.IsFalse(blur.PreserveCoverage); // alpha-blended disc, not alpha-tested
            var grass = TextureRules.Resolve("Assets/_Game/Art/Textures/Props/dry_grass/T_DryGrass_BC.png", WithAlpha());
            Assert.AreEqual(TextureWrapMode.Clamp, grass.WrapU);
            Assert.IsTrue(grass.PreserveCoverage);
            // Env sets tile: never clamped, even with card-like names.
            Assert.AreEqual(TextureWrapMode.Repeat, TextureRules.Resolve("Assets/_Game/Art/Textures/Env/grass_dry/T_GrassDry_BC.png", WithAlpha()).WrapU);
        }

        [Test]
        public void FxEmissiveKeepsFxCapAndSurfaceEmissiveIs512()
        {
            var flash = TextureRules.Resolve("Assets/_Game/Art/Textures/FX/T_VFX_MuzzleFlash_4x2_E.png", new TextureSourceInfo { Known = true, Width = 1024, Height = 512, BitsPerChannel = 8 });
            Assert.AreEqual(1024, flash.MaxSize);
            Assert.AreEqual(TextureRules.QualityFxEmissive, flash.Quality);
            Assert.AreEqual(TextureWrapMode.Clamp, flash.WrapU);
            var lamp = TextureRules.Resolve("Assets/_Game/Art/Textures/Props/caged_hanging_light/T_CagedHangingLight_E.png", Opaque(1024));
            Assert.AreEqual(512, lamp.MaxSize);
            Assert.AreEqual(60, lamp.Quality);
        }

        [Test]
        public void EditorReferenceImagesAndFolderOverrides()
        {
            var concept = TextureRules.Resolve("Assets/_Game/Art/Textures/Images/concept_keyframes.png", new TextureSourceInfo { Known = true, Width = 2048, Height = 1848, BitsPerChannel = 8 });
            Assert.IsTrue(concept.EditorOnly);
            Assert.AreEqual(2048, concept.MaxSize);
            Assert.IsFalse(concept.Mipmaps);
            Assert.IsFalse(concept.Crunched);
            var intel = TextureRules.Resolve("Assets/_Game/Art/Textures/Images/T_IntelCodebook_BC.png", Opaque());
            Assert.AreEqual(1024, intel.MaxSize);
            Assert.AreEqual(TextureImporterType.Sprite, intel.Type); // IHud.IntelCard takes a Sprite
            Assert.IsFalse(intel.Mipmaps);
            var plate = TextureRules.Resolve("Assets/_Game/Art/Textures/Images/T_AirportPlate_BC.png", new TextureSourceInfo { Known = true, Width = 2048, Height = 1536, BitsPerChannel = 8 });
            Assert.AreEqual(TextureImporterType.Default, plate.Type);
            Assert.IsFalse(plate.Mipmaps); // CRT/cinematic plate: RT_CRT/UI only
            Assert.AreEqual(TextureImporterNPOTScale.None, plate.NpotScale); // keeps 4:3 (1024x768 stays block-aligned)
            Assert.AreEqual(1024, plate.MaxSize);
            var odd = TextureRules.Resolve("Assets/_Game/Art/Textures/Images/T_OddPlate_BC.png", new TextureSourceInfo { Known = true, Width = 1000, Height = 750, BitsPerChannel = 8 });
            Assert.AreEqual(TextureImporterNPOTScale.ToNearest, odd.NpotScale); // 1000x750 is not block-aligned
            var page = TextureRules.Resolve("Assets/_Game/Art/Textures/Images/T_LedgerPage_BC.png", new TextureSourceInfo { Known = true, Width = 1536, Height = 2048, BitsPerChannel = 8 });
            Assert.IsTrue(page.Mipmaps); // documents may sit on 3D quads
            Assert.AreEqual(TextureImporterNPOTScale.ToNearest, page.NpotScale); // NPOT None + mips would import uncompressed
            Assert.IsTrue(TextureRules.BlockAlignedAfterCap(new TextureSourceInfo { Known = true, Width = 2048, Height = 1848 }, 1024));
            Assert.IsFalse(TextureRules.BlockAlignedAfterCap(new TextureSourceInfo { Known = true, Width = 2400, Height = 1792 }, 1024)); // -> 1024x765
            Assert.AreEqual(TextureImporterNPOTScale.ToNearest, TextureRules.Resolve("Assets/_Game/Art/Textures/Images/IMG_X.png",
                new TextureSourceInfo { Known = true, Width = 2400, Height = 1792, BitsPerChannel = 8 }).NpotScale);
            if (ImportBudgetsFile.Present)
                Assert.AreEqual(2048, TextureRules.Resolve("Assets/_Game/Art/Textures/Decals/poster_art/T_Decal_PosterArt_BC.png",
                    new TextureSourceInfo { Known = true, Width = 1536, Height = 768, BitsPerChannel = 8, HasAlphaChannel = true, AlphaUsed = true }).MaxSize);
        }

        [Test]
        public void ModelTable()
        {
            var plain = new ModelSourceInfo { Known = true };
            var lods = new ModelSourceInfo { Known = true, HasAuthoredLods = true };

            var prop = ModelRules.Resolve("Assets/_Game/Art/Models/Props/barrel_03/SM_Barrel03.fbx", plain);
            Assert.AreEqual(ModelImporterAnimationType.None, prop.Animation);
            Assert.AreEqual(ModelImporterMeshCompression.Medium, prop.Compression);
            Assert.IsTrue(prop.MeshLods);
            Assert.IsFalse(prop.LightmapUVs);
            Assert.IsFalse(ModelRules.Resolve("Assets/_Game/Art/Models/Props/ammo_box/SM_AmmoBox.fbx", lods).MeshLods);
            Assert.IsTrue(ModelRules.Resolve("Assets/_Game/Art/Models/Props/gate/SM_Gate_LM.fbx", plain).LightmapUVs);

            var fp = ModelRules.Resolve("Assets/_Game/Art/Models/Weapons/AK/SM_WPN_AK_FP.fbx", plain);
            Assert.AreEqual(ModelImporterMeshCompression.Off, fp.Compression);
            Assert.IsFalse(fp.MeshLods);
            var mag = ModelRules.Resolve("Assets/_Game/Art/Models/Weapons/AK/SM_WPN_AK_Mag.fbx", plain);
            Assert.AreEqual(ModelImporterMeshCompression.Off, mag.Compression);
            var world = ModelRules.Resolve("Assets/_Game/Art/Models/Weapons/AK/SM_WPN_AK_W.fbx", plain);
            Assert.AreEqual(ModelImporterMeshCompression.Medium, world.Compression);
            Assert.IsTrue(world.MeshLods);

            var body = ModelRules.Resolve("Assets/_Game/Art/Models/Characters/Hollis/SK_Hollis.fbx", plain);
            Assert.AreEqual(ModelImporterAnimationType.Human, body.Animation);
            // CORE_CHANGES "WP05 character models keep material slots": slot names survive for ModelMaterialBinder.
            Assert.AreEqual(ModelImporterMaterialImportMode.ImportViaMaterialDescription, body.Materials);
            Assert.AreEqual(ModelImporterMeshCompression.Off, body.Compression);
            var hat = ModelRules.Resolve("Assets/_Game/Art/Models/Characters/Props/boonie/SM_CHP_Boonie.fbx", plain);
            Assert.AreEqual(ModelImporterAnimationType.None, hat.Animation);

            var clip = ModelRules.Resolve("Assets/_Game/Art/Animation/Clips/UAL1/A_UAL1_Walk.fbx", plain);
            Assert.AreEqual(ModelImporterAnimationType.Human, clip.Animation);
            Assert.IsTrue(clip.ImportAnimation);
            Assert.AreEqual(ModelImporterAnimationCompression.Optimal, clip.AnimCompression);

            Assert.IsTrue(ModelRules.Resolve("Assets/_Game/Art/Models/Backdrop/SM_TownFar_A.fbx", plain).LightmapUVs);
            Assert.IsTrue(ModelRules.Resolve("Assets/_Game/Art/Models/Props/rock_face_01/SM_RockFace01.fbx", lods).LightmapUVs);
            Assert.IsFalse(ModelRules.Resolve("Assets/_Game/Art/Models/Props/rock_face_01/SM_RockFace01.fbx", lods).MeshLods);
        }

        [Test]
        public void AudioTable()
        {
            var shot = AudioRules.Resolve("Assets/_Game/Audio/SFX/Weapons/Far/SFX_WPN_AK_SHOT_FAR_01.wav");
            Assert.IsTrue(shot.Mono);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, shot.LoadType);
            Assert.IsTrue(shot.Preload);
            Assert.AreEqual(70, shot.Quality);
            Assert.AreEqual(0, shot.SampleRate);

            var loop = AudioRules.Resolve("Assets/_Game/Audio/SFX/Vehicles/SFX_VEH_HELI_LOOP.wav");
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, loop.LoadType);
            Assert.AreEqual(60, loop.Quality);

            var ui = AudioRules.Resolve("Assets/_Game/Audio/SFX/UI/SFX_UI_CLICK_01.wav");
            Assert.AreEqual(22050, ui.SampleRate);

            var amb = AudioRules.Resolve("Assets/_Game/Audio/Ambience/AMB_MARKET.wav");
            Assert.IsFalse(amb.Mono);
            Assert.IsFalse(amb.Preload);
            Assert.AreEqual(50, amb.Quality);

            var radio = AudioRules.Resolve("Assets/_Game/Audio/VO/RADIO/VO_D015_RADIO.wav");
            Assert.AreEqual(22050, radio.SampleRate);
            Assert.AreEqual(60, radio.Quality);
            Assert.IsFalse(radio.Preload);

            var dry = AudioRules.Resolve("Assets/_Game/Audio/VO/DRY/VO_D015_DRY.wav");
            Assert.AreEqual(24000, dry.SampleRate);
            Assert.AreEqual(70, dry.Quality);

            var music = AudioRules.Resolve("Assets/_Game/Audio/Music/MUS_01_SESSION.wav");
            Assert.IsFalse(music.Mono);
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, music.LoadType);

            Assert.IsNull(AudioRules.Resolve("Assets/_Game/Audio/Envelopes/VO_D001.json"));
        }
    }
}
