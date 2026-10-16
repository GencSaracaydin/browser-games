using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FunUnity.EditorTools
{
    /// <summary>
    /// Feasibility test for splitting content into small AssetBundles on a static web host.
    /// Configures the test texture/audio import settings for WebGL, creates a URP Lit material,
    /// and builds two bundles (material+textures, audio) with a chosen compression option.
    /// </summary>
    public static class WebGLBundleTest
    {
        public const string ContentDir = "Assets/WebGLTest/BundleContent";
        public const string Diffuse = ContentDir + "/beige_wall_001_diff_2k.jpg";
        public const string Normal = ContentDir + "/beige_wall_001_nor_gl_2k.jpg";
        public const string Mask = ContentDir + "/beige_wall_001_mask_2k.png";
        public const string Audio = ContentDir + "/test_noise_10s_stereo.wav";
        public const string MaterialPath = ContentDir + "/BeigeWall_Lit.mat";

        /// <summary>Import settings: 2k, DXT (WebGL override), optional Crunch with the given quality.</summary>
        public static string ConfigureImports(bool crunch, int crunchQuality)
        {
            var sb = new StringBuilder();
            void Tex(string path, bool srgb, bool normal, bool alpha)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = srgb;
                ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.crunchedCompression = crunch;
                ti.compressionQuality = crunchQuality;
                var ps = new TextureImporterPlatformSettings
                {
                    name = "WebGL",
                    overridden = true,
                    maxTextureSize = 2048,
                    format = alpha || normal ? (crunch ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT5)
                                             : (crunch ? TextureImporterFormat.DXT1Crunched : TextureImporterFormat.DXT1),
                    crunchedCompression = crunch,
                    compressionQuality = crunchQuality,
                    textureCompression = TextureImporterCompression.Compressed,
                };
                ti.SetPlatformTextureSettings(ps);
                ti.SaveAndReimport();
                sb.AppendLine($"{path}: {ps.format} crunch={crunch} q={crunchQuality}");
            }
            Tex(Diffuse, true, false, false);
            Tex(Normal, false, true, false);
            Tex(Mask, false, false, true);

            var ai = (AudioImporter)AssetImporter.GetAtPath(Audio);
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
            sb.AppendLine($"{Audio}: default {s.compressionFormat} q={s.quality} load={s.loadType} (WebGL encodes AAC)");
            return sb.ToString();
        }

        public static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Mask);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Diffuse));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Normal));
            mat.SetTexture("_MetallicGlossMap", mask);
            mat.SetTexture("_OcclusionMap", mask);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Builds wall_material.bundle and audio_test.bundle into outDir and returns a size listing.</summary>
        public static string BuildBundles(string outDir, BuildAssetBundleOptions options)
        {
            EnsureMaterial();
            Directory.CreateDirectory(outDir);
            var builds = new[]
            {
                new AssetBundleBuild { assetBundleName = "wall_material.bundle", assetNames = new[] { MaterialPath, Diffuse, Normal, Mask } },
                new AssetBundleBuild { assetBundleName = "audio_test.bundle", assetNames = new[] { Audio } },
            };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var manifest = BuildPipeline.BuildAssetBundles(outDir, builds, options | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
            sw.Stop();
            var sb = new StringBuilder();
            sb.AppendLine($"BuildAssetBundles({options}) -> {outDir} ok={(manifest != null)} in {sw.Elapsed.TotalSeconds:F1}s");
            foreach (var f in Directory.GetFiles(outDir).OrderBy(f => f))
                sb.AppendLine($"{new FileInfo(f).Length,12}  {Path.GetFileName(f)}");
            return sb.ToString();
        }
    }
}
