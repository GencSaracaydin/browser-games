using System.Collections.Generic;
using LongShadow.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    /// <summary>Spec parsing, budget parsing and naming helpers of the material pipeline (no assets written).</summary>
    public class MaterialPipelineTests
    {
        [Test]
        public void MiniJsonParsesNestedData()
        {
            var o = (Dictionary<string, object>)MiniJson.Parse("{\"a\":[1,2.5,-3e2],\"b\":{\"c\":\"x\\\"y\\u0041\"},\"d\":null,\"e\":true}");
            var a = (List<object>)o["a"];
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(-300.0, (double)a[2], 1e-9);
            Assert.AreEqual("x\"yA", MiniJson.Str(MiniJson.Obj(o, "b"), "c"));
            Assert.IsNull(o["d"]);
            Assert.AreEqual(true, MiniJson.Bool(o, "e"));
            Assert.Throws<System.FormatException>(() => MiniJson.Parse("{\"a\":}"));
        }

        [Test]
        public void SpecRowInContentAgentSchema()
        {
            const string json = "[{\"manifest_id\":\"PH_CAGED_HANGING_LIGHT\",\"material\":\"M_Lamp\",\"target_path\":\"Assets/_Game/Art/Materials/Props/M_Lamp.mat\"," +
                                "\"shader\":\"Universal Render Pipeline/Lit\",\"textures\":{\"_BaseMap\":\"a.png\",\"_MetallicGlossMap\":\"m.png\"}," +
                                "\"floats\":{\"_Smoothness\":1.0},\"colors\":{\"_BaseColor\":[1,1,1,0.28]},\"surface\":\"Transparent\",\"blend\":\"Alpha\"," +
                                "\"tile_m\":2.0,\"world_uv_tiling\":0.5,\"alpha_clip\":null,\"emission\":{\"_EmissionMap\":\"e.png\",\"_EmissionColor_linear\":[1.0,0.9,0.6]}," +
                                "\"terrain_layer\":{\"asset\":\"Assets/x.terrainlayer\",\"diffuse\":\"a.png\",\"tile_size_m\":15.0},\"assign_to_slot\":\"M_Slot\"," +
                                "\"detail\":{\"normal\":\"dn.png\",\"normal_scale\":0.6,\"tile_m\":0.6}}]";
            var list = MaterialSpec.ListFromJson(json);
            Assert.AreEqual(1, list.Count);
            var s = list[0];
            Assert.AreEqual("M_Lamp", s.Material);
            Assert.AreEqual("m.png", s.Textures["_MetallicGlossMap"]);
            Assert.AreEqual(0.28f, s.Colors["_BaseColor"].a, 1e-5f);
            Assert.IsTrue(s.Transparent);
            Assert.AreEqual(2f, s.TileMeters.Value, 1e-5f);
            Assert.AreEqual(0.5f, s.Tiling.Value, 1e-5f);
            Assert.IsFalse(s.AlphaClip.HasValue);
            Assert.AreEqual("e.png", s.EmissionMap);
            Assert.AreEqual(0.9f, s.EmissionColorLinear.Value.g, 1e-5f);
            Assert.AreEqual(15f, s.TerrainLayer.TileSizeMeters, 1e-5f);
            Assert.AreEqual("M_Slot", s.AssignToSlot);
            Assert.AreEqual("dn.png", s.DetailNormal);
            Assert.AreEqual(0.6f, s.DetailTileMeters.Value, 1e-5f);
        }

        [Test]
        public void SpecRenderFaceZWriteAndShaderNotes()
        {
            const string json = "[{\"material\":\"M_A\",\"target_path\":\"Assets/x/M_A.mat\",\"render_face\":\"Both\",\"zwrite\":false,\"shader_alt\":\"SG_FoliageWind\"}," +
                                "{\"material\":\"M_B\",\"target_path\":\"Assets/x/M_B.mat\",\"render_face\":\"Back\",\"terrain_layer\":false," +
                                "\"shader\":\"LongShadow/SG_CRTScreen (WP09); placeholder URP/Lit\"}]";
            var list = MaterialSpec.ListFromJson(json);
            Assert.IsTrue(list[0].DoubleSided);
            Assert.AreEqual(false, list[0].ZWrite);
            Assert.AreEqual("SG_FoliageWind", list[0].ShaderAlt);
            Assert.AreEqual(MaterialFactory.LitShaderName, list[0].Shader);
            Assert.IsTrue(list[1].BackFaceOnly);
            Assert.IsNull(list[1].TerrainLayer);
            Assert.AreEqual("LongShadow/SG_CRTScreen", list[1].Shader);
            Assert.AreEqual("Shader Graphs/Decal", MaterialSpec.CleanShaderName("Shader Graphs/Decal"));
        }

        [Test]
        public void BudgetTextParsing()
        {
            Assert.AreEqual(512, TextureBudgets.ParseCap("6898 tris, 512 maps in game (1024 source; TECH_PLAN §8.9 cap)"));
            Assert.AreEqual(1024, TextureBudgets.ParseCap("1918 tris, 1024 maps (hero prop: 1024 in game)"));
            Assert.AreEqual(2048, TextureBudgets.ParseCap("2048 (hero), crunch q55"));
            Assert.AreEqual(1024, TextureBudgets.ParseCap("1024 atlas, alpha"));
            Assert.AreEqual(1024, TextureBudgets.ParseCap("1024x512, alpha"));
            Assert.AreEqual(256, TextureBudgets.ParseCap("128-256 px each, alpha"));
            Assert.AreEqual(512, TextureBudgets.ParseCap("vector or <=512 px"));
            Assert.AreEqual(512, TextureBudgets.ParseCap("3658 tris, 512 maps"));
            Assert.IsNull(TextureBudgets.ParseCap("fixed voice"));
            Assert.AreEqual(1024, TextureBudgets.ToMaxSize(1000));
        }

        [Test]
        public void SlotAndModelNames()
        {
            Assert.AreEqual("M_WPN_AK", ModelMaterialBinder.CleanSlot("M_WPN_AK.001"));
            Assert.AreEqual("WPN_AK", ModelMaterialBinder.BaseName("SM_WPN_AK_W"));
            Assert.AreEqual("AmmoBox", ModelMaterialBinder.BaseName("SM_AmmoBox_LOD0"));
            Assert.AreEqual("Calder", ModelMaterialBinder.BaseName("SK_Calder"));
            Assert.IsTrue(ModelRules.HasToken("SM_WPN_AK_FP", "FP"));
            Assert.IsFalse(ModelRules.HasToken("SM_FPGun", "FP"));
        }
    }
}
