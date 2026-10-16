using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Packs the D4 mask texture (TECH_PLAN D4): R metallic, G ambient occlusion, B height (terrain sets) or 0,
    /// A smoothness = 1 − roughness, from Poly Haven ARM, glTF metallic-roughness or separate maps. Values are copied as
    /// raw data (no colour conversion); inputs of different sizes are resampled bilinearly to the output size. The result
    /// is written as PNG and, when it lies under Assets/, imported (the <c>_M</c> suffix makes it linear DXT5).
    /// Content tools normally pack offline; this is the in-editor path for sets that arrive with loose maps.
    /// </summary>
    public static class MaskPacker
    {
        private sealed class Img
        {
            public Color32[] Px;
            public int W, H;

            public float Sample(float u, float v, int channel)
            {
                var x = Mathf.Clamp(u * W - 0.5f, 0, W - 1);
                var y = Mathf.Clamp(v * H - 0.5f, 0, H - 1);
                int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, W - 1), y1 = Math.Min(y0 + 1, H - 1);
                float fx = x - x0, fy = y - y0;
                float a = C(x0, y0, channel), b = C(x1, y0, channel), c = C(x0, y1, channel), d = C(x1, y1, channel);
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) / 255f;
            }

            private float C(int x, int y, int ch)
            {
                var p = Px[y * W + x];
                return ch == 0 ? p.r : ch == 1 ? p.g : ch == 2 ? p.b : p.a;
            }
        }

        /// <summary>
        /// Packs <paramref name="src"/> into <paramref name="outputPath"/> (project-relative or absolute .png). Size: the
        /// largest input (or <paramref name="size"/> when &gt; 0). Returns the written path, or null on failure (logged).
        /// </summary>
        public static string Pack(MaskSources src, string outputPath, int size = 0)
        {
            if (src == null || !src.Any)
            {
                LSLog.Error(LogCategory.Assets, "MaskPacker: no input maps for " + outputPath);
                return null;
            }
            try
            {
                var arm = Load(src.Arm);
                var mr = Load(src.MetallicRoughness);
                var met = Load(src.Metallic);
                var ao = Load(src.Occlusion);
                var rough = Load(src.Roughness);
                var smooth = Load(src.Smoothness);
                var height = Load(src.Height);
                int w = size, h = size;
                if (size <= 0)
                {
                    w = h = 0;
                    foreach (var i in new[] { arm, mr, met, ao, rough, smooth, height })
                    {
                        if (i == null) continue;
                        w = Math.Max(w, i.W);
                        h = Math.Max(h, i.H);
                    }
                }
                if (w <= 0 || h <= 0) throw new InvalidOperationException("no readable input");

                var outPx = new Color32[w * h];
                for (var y = 0; y < h; y++)
                {
                    var v = (y + 0.5f) / h;
                    for (var x = 0; x < w; x++)
                    {
                        var u = (x + 0.5f) / w;
                        var m = met != null ? met.Sample(u, v, 0) : mr != null ? mr.Sample(u, v, 2) : arm != null ? arm.Sample(u, v, 2) : src.MetallicValue;
                        var o = ao != null ? ao.Sample(u, v, 0) : arm != null ? arm.Sample(u, v, 0) : src.OcclusionValue;
                        float s;
                        if (smooth != null) s = smooth.Sample(u, v, 0);
                        else if (rough != null) s = 1f - rough.Sample(u, v, 0);
                        else if (mr != null) s = 1f - mr.Sample(u, v, 1);
                        else if (arm != null) s = 1f - arm.Sample(u, v, 1);
                        else s = src.SmoothnessValue;
                        var hgt = height != null ? height.Sample(u, v, 0) : src.HeightValue;
                        outPx[y * w + x] = new Color32(B(m), B(o), B(hgt), B(s));
                    }
                }
                return Write(outPx, w, h, outputPath);
            }
            catch (Exception e)
            {
                LSLog.Error(LogCategory.Assets, "MaskPacker failed for " + outputPath + ": " + e.Message);
                return null;
            }
        }

        /// <summary>Packs a Poly Haven ARM map (optionally with a height map for B).</summary>
        public static string PackFromArm(string armPath, string outputPath, string heightPath = null) =>
            Pack(new MaskSources { Arm = armPath, Height = heightPath }, outputPath);

        /// <summary>Packs a glTF metallic-roughness map with an optional separate AO map.</summary>
        public static string PackFromGltf(string metallicRoughnessPath, string outputPath, string occlusionPath = null) =>
            Pack(new MaskSources { MetallicRoughness = metallicRoughnessPath, Occlusion = occlusionPath }, outputPath);

        /// <summary>Sources for a <see cref="TextureSet"/>'s loose maps, or null when it has none.</summary>
        public static MaskSources FromLooseMaps(TextureSet set)
        {
            if (set == null || set.Loose.Count == 0) return null;
            string L(params string[] keys)
            {
                foreach (var k in keys)
                    if (set.Loose.TryGetValue(k, out var p)) return p;
                return null;
            }
            var src = new MaskSources
            {
                Arm = L("ARM"),
                MetallicRoughness = L("MR"),
                Metallic = L("Metallic", "Metal"),
                Occlusion = L("AO", "Occlusion"),
                Roughness = L("Roughness", "Rough"),
                Smoothness = L("Smoothness"),
                Height = set.Get(TextureSuffix.H),
            };
            if (src.Arm == null && src.MetallicRoughness == null && src.Metallic == null && src.Occlusion == null &&
                src.Roughness == null && src.Smoothness == null) return null;
            return src;
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static string Full(string path) => Path.IsPathRooted(path) ? path : Path.Combine(BuilderUtil.ProjectRoot, path);

        private static Img Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var full = Full(path);
            if (!File.Exists(full)) throw new FileNotFoundException("mask input missing", path);
            var ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                throw new NotSupportedException("mask inputs must be PNG or JPG (" + path + ")");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(full), false)) throw new InvalidDataException("cannot decode " + path);
                return new Img { Px = tex.GetPixels32(), W = tex.width, H = tex.height };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string Write(Color32[] px, int w, int h, string outputPath)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                var full = Full(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
                File.WriteAllBytes(full, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var assetPath = outputPath.Replace('\\', '/');
            if (Path.IsPathRooted(assetPath))
            {
                var root = BuilderUtil.ProjectRoot.Replace('\\', '/') + "/";
                if (assetPath.StartsWith(root, StringComparison.Ordinal)) assetPath = assetPath.Substring(root.Length);
            }
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }
    }
}
