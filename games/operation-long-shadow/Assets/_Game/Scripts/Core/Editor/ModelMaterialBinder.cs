using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Binds the material slots of imported models to MaterialFactory materials by name (TECH_PLAN §8.2.6 "MaterialFactory
    /// assigns by slot name"). For every slot the first match wins: an explicit map entry, a spec row with
    /// "assign_to_slot", a material named exactly like the slot ("M_AmmoBox"), "M_" + slot, and for single-slot models
    /// "M_" + the model name without SM_/SK_ and _FP/_W/_World/_LODn ("SM_WPN_AK_W" → "M_WPN_AK"). Blender duplicate
    /// suffixes (".001") are ignored. The binding is an importer remap, so every prefab built from the model shows the
    /// right materials. Slots without a material keep the importer's own material (logged once).
    /// </summary>
    public static class ModelMaterialBinder
    {
        /// <summary>Default root of shipped models.</summary>
        public const string ModelsRoot = "Assets/_Game/Art/Models";

        /// <summary>Result for one model.</summary>
        public sealed class Result
        {
            /// <summary>Model path.</summary>
            public string Model;
            /// <summary>Slot → bound material path.</summary>
            public readonly Dictionary<string, string> Bound = new Dictionary<string, string>();
            /// <summary>Slots without a material.</summary>
            public readonly List<string> Unbound = new List<string>();
            /// <summary>The importer changed (and was reimported).</summary>
            public bool Changed;
            /// <summary>The model importer (null when the path is not a model).</summary>
            public ModelImporter Importer;

            /// <inheritdoc />
            public override string ToString() =>
                Model + ": " + Bound.Count + " bound" + (Unbound.Count > 0 ? ", unbound: " + string.Join(", ", Unbound) : "") + (Changed ? " (reimported)" : "");
        }

        /// <summary>Binds one model. <paramref name="explicitMap"/> maps slot names to materials and wins over every rule.</summary>
        public static Result Bind(string modelPath, IDictionary<string, Material> explicitMap = null, bool reimport = true)
        {
            var result = Prepare(modelPath, explicitMap, null);
            if (result.Changed && reimport) result.Importer.SaveAndReimport();
            return result;
        }

        /// <summary>Binds every model under <paramref name="root"/> whose importer keeps material slots (one batched reimport).</summary>
        public static List<Result> BindAll(string root = ModelsRoot)
        {
            var results = new List<Result>();
            if (!AssetDatabase.IsValidFolder(root)) return results;
            Dictionary<string, Material> index = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                index = index ?? MaterialIndex(p);
                results.Add(Prepare(p, null, index));
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var r in results)
                    if (r.Changed) r.Importer.SaveAndReimport();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return results;
        }

        private static Result Prepare(string modelPath, IDictionary<string, Material> explicitMap, Dictionary<string, Material> index)
        {
            var result = new Result { Model = modelPath };
            var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            result.Importer = mi;
            if (mi == null || mi.materialImportMode == ModelImporterMaterialImportMode.None) return result;

            index = index ?? MaterialIndex(modelPath);
            var slots = SlotNames(modelPath);
            var existing = mi.GetExternalObjectMap();
            var stem = Path.GetFileNameWithoutExtension(modelPath);
            foreach (var slot in slots)
            {
                var mat = Resolve(slot, slots.Count, stem, explicitMap, index);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                if (mat == null)
                {
                    result.Unbound.Add(slot);
                    continue;
                }
                result.Bound[slot] = AssetDatabase.GetAssetPath(mat);
                if (existing.TryGetValue(id, out var cur) && cur == mat) continue;
                mi.AddRemap(id, mat);
                result.Changed = true;
            }
            if (result.Unbound.Count > 0)
                LSLog.WarnOnce("bind:" + modelPath, LogCategory.Assets, "ModelMaterialBinder: no material yet for slot(s) " +
                                                                        string.Join(", ", result.Unbound) + " of " + modelPath);
            return result;
        }

        /// <summary>Material slot names of an imported model (its embedded/remapped materials), without duplicates.</summary>
        public static List<string> SlotNames(string modelPath)
        {
            var names = new List<string>();
            var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (mi != null)
                foreach (var kv in mi.GetExternalObjectMap())
                    if (kv.Key.type == typeof(Material) && !names.Contains(kv.Key.name)) names.Add(kv.Key.name);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Material m && !names.Contains(m.name)) names.Add(m.name);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>The slot name without a Blender duplicate suffix ("M_X.001" → "M_X").</summary>
        public static string CleanSlot(string slot)
        {
            var dot = slot.LastIndexOf('.');
            if (dot > 0 && dot < slot.Length - 1)
            {
                var tail = slot.Substring(dot + 1);
                var digits = true;
                foreach (var c in tail) digits &= char.IsDigit(c);
                if (digits) return slot.Substring(0, dot);
            }
            return slot;
        }

        /// <summary>"SM_WPN_AK_W" → "WPN_AK"; "SM_AmmoBox_LOD0" → "AmmoBox".</summary>
        public static string BaseName(string modelStem)
        {
            var s = modelStem;
            if (s.StartsWith("SM_", StringComparison.Ordinal) || s.StartsWith("SK_", StringComparison.Ordinal)) s = s.Substring(3);
            var parts = new List<string>(s.Split('_'));
            while (parts.Count > 1)
            {
                var last = parts[parts.Count - 1];
                if (last == "FP" || last == "W" || last == "World" || last == "LM" || last.StartsWith("LOD", StringComparison.Ordinal)) parts.RemoveAt(parts.Count - 1);
                else break;
            }
            return string.Join("_", parts);
        }

        private static Material Resolve(string slot, int slotCount, string modelStem, IDictionary<string, Material> explicitMap,
            Dictionary<string, Material> index)
        {
            var clean = CleanSlot(slot);
            if (explicitMap != null && (explicitMap.TryGetValue(slot, out var e) || explicitMap.TryGetValue(clean, out e)) && e != null) return e;
            if (MaterialFactory.SlotDefaults().TryGetValue(clean, out var specPath))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(specPath);
                if (m != null) return m;
            }
            if (index.TryGetValue(clean, out var byName)) return byName;
            if (index.TryGetValue("M_" + clean, out var prefixed)) return prefixed;
            if (slotCount == 1 && index.TryGetValue("M_" + BaseName(modelStem), out var byModel)) return byModel;
            return null;
        }

        private static Dictionary<string, Material> MaterialIndex(string modelPath)
        {
            var root = ImportRulePaths.RootOf(modelPath) ?? ImportRulePaths.GameRoot;
            var folder = root + "Art/Materials";
            var index = new Dictionary<string, Material>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(folder)) return index;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null && !index.ContainsKey(m.name)) index.Add(m.name, m);
            }
            return index;
        }
    }
}
