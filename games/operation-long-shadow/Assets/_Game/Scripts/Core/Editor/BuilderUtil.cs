using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Helpers for the "builder" editor scripts every package writes (prefabs, scenes, libraries generated from data so
    /// an editor agent can rebuild everything with a few menu calls). All methods are idempotent where it makes sense.
    /// </summary>
    public static class BuilderUtil
    {
        /// <summary>Folder for placeholder material assets.</summary>
        public const string PlaceholderMaterialFolder = "Assets/_Game/Art/Materials/FX/Placeholders";

        /// <summary>Absolute project root.</summary>
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>Creates a project folder and its parents (e.g. "Assets/_Game/Prefabs/Weapons").</summary>
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Saves a scene object as a prefab (replacing an existing one, keeping its GUID) and destroys the object.</summary>
        public static GameObject SavePrefab(GameObject root, string assetPath, bool destroySceneObject = true)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath, out var ok);
            if (!ok) LSLog.Error(LogCategory.Assets, "Saving prefab failed: " + assetPath);
            if (destroySceneObject) Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Loads a ScriptableObject asset or creates it.</summary>
        public static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (a != null) return a;
            EnsureFolder(Path.GetDirectoryName(assetPath));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, assetPath);
            return a;
        }

        /// <summary>Writes an asset to a path, overwriting the content of an existing asset in place (keeps its GUID).</summary>
        public static T CreateOrReplaceAsset<T>(T asset, string assetPath) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(assetPath));
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>A URP/Lit placeholder material saved as an asset (safe to reference from prefabs).</summary>
        public static Material PlaceholderMaterial(Color color)
        {
            var path = PlaceholderMaterialFolder + "/M_Placeholder_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            EnsureFolder(PlaceholderMaterialFolder);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Primitive with a placeholder material asset (collider kept or removed).</summary>
        public static GameObject PlaceholderPrimitive(string name, PrimitiveType type, Vector3 size, Color color, Transform parent = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "PH_" + name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterial(color);
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Sets the layer of a hierarchy.</summary>
        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform c in root.transform) SetLayerRecursively(c.gameObject, layer);
        }

        /// <summary>Finds or creates a child by name.</summary>
        public static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}
