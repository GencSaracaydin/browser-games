using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Switches foliage and cloth materials to <c>LongShadow/World/FoliageWind</c> (TECH_PLAN §7.14 SG_FoliageWind): every
    /// MaterialFactory spec row whose <c>shader_alt</c> names SG_FoliageWind (WP10_FAL leaves and grass, Level awnings)
    /// plus any <c>Art/Materials/Vegetation/*Leaves*|*Grass*</c> material. Textures and values carry over (same property
    /// names as URP/Lit); keywords for clipping, normal and mask maps are set from what is assigned. WP10_FAL meshes carry
    /// wind vertex colours (R sway, G phase, B flutter), so those materials use them. Run after MaterialFactory.
    /// </summary>
    public static class FoliageMaterialUpgrader
    {
        [MenuItem("LongShadow/Look/Convert Foliage Materials To Wind Shader", priority = 140)]
        public static void ConvertMenu()
        {
            var log = new StringBuilder();
            Convert(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Converts every foliage material found; returns the count.</summary>
        public static int Convert(StringBuilder log)
        {
            var shader = Shader.Find(LookPaths.ShaderFoliageWind);
            if (shader == null)
            {
                log.Append("FoliageWind shader missing: foliage stays URP/Lit. ");
                return 0;
            }
            var targets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); // path -> authored wind colours
            foreach (var file in MaterialFactory.SpecFiles())
            {
                List<MaterialSpec> specs;
                try
                {
                    specs = MaterialSpec.ListFromJson(File.ReadAllText(file));
                }
                catch (Exception e)
                {
                    LSLog.Warn(LogCategory.Look, "Spec file " + file + ": " + e.Message);
                    continue;
                }
                foreach (var s in specs)
                {
                    if (s == null || string.IsNullOrEmpty(s.TargetPath)) continue;
                    if (s.ShaderAlt == null || s.ShaderAlt.IndexOf("FoliageWind", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var p = s.TargetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? s.TargetPath : s.TargetPath + ".mat";
                    targets[p] = s.ManifestId != null && s.ManifestId.StartsWith("VEG_", StringComparison.Ordinal);
                }
            }
            const string veg = "Assets/_Game/Art/Materials/Vegetation";
            if (AssetDatabase.IsValidFolder(veg))
                foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { veg }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var n = Path.GetFileNameWithoutExtension(p);
                    if (n.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0)
                        if (!targets.ContainsKey(p)) targets[p] = true;
                }

            var converted = 0;
            foreach (var kv in targets)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(kv.Key);
                if (m == null) continue;
                Apply(m, shader, kv.Value);
                converted++;
            }
            AssetDatabase.SaveAssets();
            log.Append("Foliage materials on wind shader: " + converted + " (of " + targets.Count + " candidates). ");
            return converted;
        }

        /// <summary>Puts one material on the wind shader.</summary>
        public static void Apply(Material m, Shader shader, bool vertexColours)
        {
            var clip = m.HasProperty("_AlphaClip") ? m.GetFloat("_AlphaClip") > 0.5f : m.IsKeywordEnabled("_ALPHATEST_ON");
            var cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            var name = m.name;
            var grass = name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0;
            var cloth = name.IndexOf("Awning", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Laundry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Cloth", StringComparison.OrdinalIgnoreCase) >= 0;
            if (grass || name.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0) clip = true;

            if (m.shader != shader) m.shader = shader;
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_Cutoff", cutoff);
            LookMaterialBuilder.SetKeyword(m, "_ALPHATEST_ON", clip);
            var hasNormal = m.GetTexture("_BumpMap") != null;
            m.SetFloat("_UseNormal", hasNormal ? 1f : 0f);
            LookMaterialBuilder.SetKeyword(m, "_NORMALMAP", hasNormal);
            var hasMask = m.GetTexture("_MetallicGlossMap") != null;
            m.SetFloat("_UseMask", hasMask ? 1f : 0f);
            LookMaterialBuilder.SetKeyword(m, "_MASKMAP", hasMask);
            m.SetFloat("_WindVertexColor", vertexColours && !cloth ? 1f : 0f);
            LookMaterialBuilder.SetKeyword(m, "_WIND_VERTEXCOLOR", vertexColours && !cloth);
            m.SetFloat("_Cull", (float)CullMode.Off);
            if (grass)
            {
                m.SetFloat("_WindStrength", 1.6f);
                m.SetFloat("_WindHeight", 0.6f);
                m.SetFloat("_FlutterAmplitude", 0.03f);
                m.SetFloat("_Translucency", 0.8f);
            }
            else if (cloth)
            {
                m.SetFloat("_WindStrength", 0.8f);
                m.SetFloat("_WindHeight", 1.5f);
                m.SetFloat("_FlutterAmplitude", 0.05f);
                m.SetFloat("_Translucency", 0.35f);
            }
            else
            {
                m.SetFloat("_WindStrength", 1f);
                m.SetFloat("_WindHeight", 4f);
                m.SetFloat("_FlutterAmplitude", 0.025f);
                m.SetFloat("_Translucency", 0.6f);
            }
            m.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(m);
        }
    }
}
