using System;
using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.AI.Editor
{
    /// <summary>
    /// Shared helpers of the WP04 builders: finding WP05 body prefabs (by name hint, then by the manifest model folder
    /// they depend on), WP04's own FX materials (scope glint, NVG lens, chemlight), and Health replacement on unpacked
    /// bodies.
    /// </summary>
    public static class AIBuilderUtil
    {
        /// <summary>WP05 body prefab folder (TECH_PLAN §4.1).</summary>
        public const string BodiesFolder = "Assets/_Game/Prefabs/Bodies";
        /// <summary>WP04 enemy prefab folder.</summary>
        public const string EnemiesFolder = "Assets/_Game/Prefabs/Enemies";
        /// <summary>WP04 squad prefab folder.</summary>
        public const string SquadFolder = "Assets/_Game/Prefabs/Squad";
        /// <summary>WP04 materials (glint, lens, chemlight).</summary>
        public const string MaterialFolder = EnemiesFolder + "/Materials";

        private static readonly string[] s_excluded = { "Shadow", "_FP", "Booth" };

        /// <summary>
        /// Finds a WP05 body prefab: first one whose file name contains a hint, else one that depends on an asset in the
        /// manifest target folder of <paramref name="manifestId"/> (e.g. Art/Models/Characters/LegionA/). Shadow, FP and
        /// booth bodies are skipped; the root must carry an <see cref="ICharacterBody"/>. Null when WP05 has not landed.
        /// </summary>
        public static GameObject FindBody(IReadOnlyList<string> hints, string manifestId)
        {
            if (!AssetDatabase.IsValidFolder(BodiesFolder)) return null;
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { BodiesFolder })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            paths.Sort(StringComparer.Ordinal);
            if (hints != null)
            {
                foreach (var p in paths)
                {
                    var file = Path.GetFileNameWithoutExtension(p);
                    if (Excluded(file)) continue;
                    foreach (var h in hints)
                    {
                        if (string.IsNullOrEmpty(h) || file.IndexOf(h, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                        if (go != null && go.GetComponent<ICharacterBody>() != null) return go;
                    }
                }
            }
            var folder = !string.IsNullOrEmpty(manifestId) ? AssetManifest.ResolvePath(manifestId) : null;
            if (string.IsNullOrEmpty(folder)) return null;
            foreach (var p in paths)
            {
                if (Excluded(Path.GetFileNameWithoutExtension(p))) continue;
                foreach (var dep in AssetDatabase.GetDependencies(p, true))
                {
                    if (!dep.StartsWith(folder, StringComparison.Ordinal)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go != null && go.GetComponent<ICharacterBody>() != null) return go;
                    break;
                }
            }
            return null;
        }

        private static bool Excluded(string file)
        {
            foreach (var x in s_excluded)
                if (file.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>An HDR URP/Unlit material saved under <see cref="MaterialFolder"/> (created once, colour refreshed).</summary>
        public static Material UnlitMaterial(string name, Color hdrColor)
        {
            BuilderUtil.EnsureFolder(MaterialFolder);
            var path = MaterialFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdrColor);
            m.color = hdrColor;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Makes sure the root's Health is of type <typeparamref name="T"/>: a plain Core Health on a prefab instance is
        /// replaced (the instance is unpacked first, since prefab instances cannot lose components reliably), keeping its
        /// aim point and actor id.
        /// </summary>
        public static T EnsureHealth<T>(GameObject root) where T : Health
        {
            var existing = root.GetComponent<Health>();
            if (existing is T typed) return typed;
            Transform aim = null;
            if (existing != null)
            {
                // Only the outermost body prefab is unpacked; nested model prefabs (the FBX) stay linked.
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                aim = new SerializedObject(existing).FindProperty("_aimPoint")?.objectReferenceValue as Transform;
                UnityEngine.Object.DestroyImmediate(existing);
            }
            var h = root.AddComponent<T>();
            if (aim != null) SetObject(h, "_aimPoint", aim);
            return h;
        }

        /// <summary>Sets a serialized object reference field.</summary>
        public static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized bool field.</summary>
        public static void SetBool(UnityEngine.Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized string field.</summary>
        public static void SetString(UnityEngine.Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized enum field by index.</summary>
        public static void SetEnum(UnityEngine.Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) return;
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Finds a bone by name fragments (Spine2, Chest, Head ...) under a root; null when none.</summary>
        public static Transform FindBone(Transform root, params string[] fragments)
        {
            foreach (var f in fragments)
            {
                var t = FindDeep(root, f);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string fragment)
        {
            if (t.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), fragment);
                if (r != null) return r;
            }
            return null;
        }
    }
}
