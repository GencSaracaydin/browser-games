using System.IO;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Saves generated meshes as <c>.asset</c> files (TECH_PLAN §3.11: "generated meshes are .asset, not imported") with
    /// lightmap UVs from <see cref="Unwrapping.GenerateSecondaryUVSet(Mesh, UnwrapParam)"/>. An existing asset is overwritten
    /// in place so scenes and prefabs keep their references (GUID) across rebuilds.
    /// </summary>
    public static class MeshAssets
    {
        /// <summary>Saves (or replaces) a mesh asset; generates UV1 for lightmapping when asked.</summary>
        public static Mesh Save(Mesh mesh, string path, bool lightmapUVs)
        {
            if (mesh == null) return null;
            if (lightmapUVs && mesh.vertexCount > 0)
            {
                UnwrapParam.SetDefaults(out var p);
                p.hardAngle = 88f;
                p.angleError = 0.08f;
                p.areaError = 0.15f;
                p.packMargin = 1f / 256f;
                Unwrapping.GenerateSecondaryUVSet(mesh, p);
            }
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>A file-system-safe asset name.</summary>
        public static string SafeName(string s)
        {
            var chars = s.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
