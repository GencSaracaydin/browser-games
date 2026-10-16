using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Creates or updates a TerrainLayer asset from a <see cref="TerrainLayerSpec"/> (diffuse, normal, D4 mask with height
    /// in B, tile size). The asset is updated in place so terrains that reference it keep working.
    /// </summary>
    public static class TerrainLayerBuilder
    {
        /// <summary>Builds the layer; returns null when the spec has no asset path.</summary>
        public static TerrainLayer Build(TerrainLayerSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.AssetPath)) return null;
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(spec.AssetPath);
            var created = layer == null;
            if (created) layer = new TerrainLayer { name = Path.GetFileNameWithoutExtension(spec.AssetPath) };
            layer.diffuseTexture = Load(spec.Diffuse);
            layer.normalMapTexture = Load(spec.Normal);
            layer.maskMapTexture = Load(spec.Mask);
            layer.normalScale = spec.NormalScale;
            layer.tileSize = new Vector2(spec.TileSizeMeters, spec.TileSizeMeters);
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = 0.5f; // used only without a mask
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            if (created)
            {
                // Values are set before the asset is written, so a reimport right after creation cannot lose them.
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(spec.AssetPath));
                AssetDatabase.CreateAsset(layer, spec.AssetPath);
            }
            else EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
            return layer;
        }

        private static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) LSLog.MissingAsset(LogCategory.Assets, "TerrainLayer:" + Path.GetFileName(path), path);
            return t;
        }
    }
}
