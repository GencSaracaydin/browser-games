using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Character materials (TECH_PLAN D4, §7.8): one URP/Lit material per texture set under Art/Textures/Characters
    /// (T_&lt;Set&gt;_{BC,N,M}, mask in Metallic + Occlusion, smoothness from the mask alpha) written to MaterialFactory's
    /// own path for the set (Art/Materials/Characters/M_&lt;Set&gt;.mat, so a folder-wide MaterialFactory pass updates the same
    /// asset instead of duplicating it) through Core's MaterialFactory, so a material is named exactly
    /// like the FBX slot the content pipeline exported (M_Calder_Body, M_CHP_ChestRig, ...). Hair-card sets get alpha
    /// clipping 0.35, double-sided rendering and no shadow-caster pass (content import notes). Then binds every body FBX slot to its material with
    /// Core's ModelMaterialBinder (an importer remap, so every prefab shows them). Fabric source tiles are not materials.
    /// </summary>
    public static class CharacterMaterialBuilder
    {
        /// <summary>Alpha-clip threshold of hair cards (content import notes).</summary>
        public const float HairAlphaClip = 0.35f;

        /// <summary>Texture folders that hold sources, not material sets.</summary>
        public static readonly string[] SkippedFolders = { "Fabrics" };

        /// <summary>Builds (or updates) every character material. Returns the materials.</summary>
        public static List<Material> BuildAll()
        {
            var result = new List<Material>();
            if (!AssetDatabase.IsValidFolder(CharacterPaths.TexturesRoot))
            {
                LSLog.MissingAsset(LogCategory.Characters, "character textures", CharacterPaths.TexturesRoot);
                return result;
            }
            foreach (var set in TextureSet.FindInFolder(CharacterPaths.TexturesRoot, true))
            {
                if (set.BaseColor == null || IsSkipped(set.Folder)) continue;
                var options = new MaterialOptions { MaterialPath = MaterialPathFor(set) };
                if (IsHair(set.Name))
                {
                    options.AlphaClip = HairAlphaClip;
                    options.DoubleSided = true;
                }
                var m = MaterialFactory.BuildFromTextureSet(set, options);
                if (m == null) continue;
                if (IsHair(set.Name) && m.GetShaderPassEnabled("ShadowCaster"))
                {
                    // Hair cards self-shadowing the scalp render as black streaks (WP05_ART §2).
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    EditorUtility.SetDirty(m);
                }
                result.Add(m);
            }
            AssetDatabase.SaveAssets();
            LSLog.Info(LogCategory.Characters, "CharacterMaterialBuilder: " + result.Count + " material(s) built.");
            return result;
        }

        /// <summary>Binds the material slots of every body model under Art/Models/Characters (props included).</summary>
        public static List<ModelMaterialBinder.Result> BindAll()
        {
            var results = new List<ModelMaterialBinder.Result>();
            if (!AssetDatabase.IsValidFolder(CharacterPaths.ModelsRoot)) return results;
            var index = MaterialIndex();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterPaths.ModelsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/_Mannequin/")) continue;
                var slots = ModelMaterialBinder.SlotNames(path);
                var map = new Dictionary<string, Material>();
                foreach (var slot in slots)
                {
                    var clean = ModelMaterialBinder.CleanSlot(slot);
                    if (index.TryGetValue(clean, out var m) || index.TryGetValue("M_" + clean, out m)) map[slot] = m;
                }
                results.Add(ModelMaterialBinder.Bind(path, map));
            }
            return results;
        }

        /// <summary>Material asset path for a set: MaterialFactory's default (Art/Materials/Characters/M_&lt;Set&gt;.mat).</summary>
        public static string MaterialPathFor(TextureSet set) => MaterialFactory.MaterialPathFor(set);

        /// <summary>Hair cards ("Hollis_Hair").</summary>
        public static bool IsHair(string setName) =>
            setName.EndsWith("_Hair", StringComparison.OrdinalIgnoreCase) || setName.IndexOf("Hair_", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsSkipped(string folder)
        {
            foreach (var s in SkippedFolders)
                if (folder.EndsWith("/" + s, StringComparison.OrdinalIgnoreCase) || folder.Contains("/" + s + "/")) return true;
            return false;
        }

        /// <summary>Every material of the project by name (character materials win over others with the same name).</summary>
        public static Dictionary<string, Material> MaterialIndex()
        {
            var index = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game/Art/Materials" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                var mine = p.StartsWith(CharacterPaths.MaterialsRoot, StringComparison.OrdinalIgnoreCase);
                if (!index.ContainsKey(m.name) || mine) index[m.name] = m;
            }
            return index;
        }

        /// <summary>A built character material by name ("M_CalderReel_Head"), or null.</summary>
        public static Material Find(string materialName)
        {
            foreach (var guid in AssetDatabase.FindAssets(materialName + " t:Material", new[] { CharacterPaths.MaterialsRoot }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == materialName) return m;
            }
            return null;
        }
    }
}
