using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Writes a <see cref="TextureRule"/> into a TextureImporter (default platform + an explicit WebGL override) and
    /// compares an importer against a rule (used by the self-test and the "Check Import Rules" report).
    /// </summary>
    public static class TextureRuleApplier
    {
        /// <summary>WebGL platform name for platform overrides.</summary>
        public const string WebGL = "WebGL";

        /// <summary>Applies the rule. Safe inside OnPreprocessTexture.</summary>
        public static void Apply(TextureImporter ti, TextureRule r)
        {
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.textureType = r.Type;
            s.textureShape = r.Shape;
            if (r.Shape == TextureImporterShape.TextureCube)
            {
                s.generateCubemap = r.CubeMapping;
                s.cubemapConvolution = TextureImporterCubemapConvolution.None;
            }
            if (r.Type == TextureImporterType.Sprite)
            {
                s.spriteMode = (int)SpriteImportMode.Single;
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteGenerateFallbackPhysicsShape = false;
            }
            // NPOT scaling goes into the same settings block: sprites only accept None, and setting it afterwards would
            // make SetTextureSettings warn "You cannot set npotScale to ToNearest for this texture type".
            s.npotScale = r.NpotScale;
            ti.SetTextureSettings(s);

            ti.sRGBTexture = r.SRGB;
            ti.mipmapEnabled = r.Mipmaps;
            ti.mipmapFilter = r.MipFilter;
            ti.mipMapsPreserveCoverage = r.PreserveCoverage;
            ti.alphaTestReferenceValue = r.AlphaTestReference;
            ti.streamingMipmaps = false;
            ti.anisoLevel = r.Aniso;
            ti.filterMode = r.Filter;
            ti.wrapModeU = r.WrapU;
            ti.wrapModeV = r.WrapV;
            ti.alphaSource = r.AlphaSource;
            ti.alphaIsTransparency = r.AlphaIsTransparency;
            ti.isReadable = r.Readable;
            if (r.Type == TextureImporterType.NormalMap)
            {
                ti.convertToNormalmap = false;
                ti.flipGreenChannel = false; // sources are OpenGL (+Y), Unity's convention
            }

            var d = ti.GetDefaultPlatformTextureSettings();
            d.maxTextureSize = r.MaxSize;
            d.textureCompression = r.Compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            d.crunchedCompression = r.Crunched;
            d.compressionQuality = r.Quality;
            d.format = r.Compressed ? TextureImporterFormat.Automatic : r.WebGLFormat;
            d.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            ti.SetPlatformTextureSettings(d);

            var w = ti.GetPlatformTextureSettings(WebGL);
            w.overridden = true;
            w.maxTextureSize = r.MaxSize;
            w.format = r.WebGLFormat;
            w.textureCompression = r.Compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            w.crunchedCompression = r.Crunched;
            w.compressionQuality = r.Quality;
            w.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            ti.SetPlatformTextureSettings(w);
        }

        /// <summary>Differences between an importer and a rule ("field: actual != expected"); empty when it matches.</summary>
        public static List<string> Diff(TextureImporter ti, TextureRule r)
        {
            var diffs = new List<string>();
            void Check<T>(string name, T actual, T expected)
            {
                if (!Equals(actual, expected)) diffs.Add(name + ": " + actual + " != " + expected);
            }
            Check("textureType", ti.textureType, r.Type);
            Check("textureShape", ti.textureShape, r.Shape);
            Check("sRGBTexture", ti.sRGBTexture, r.SRGB);
            Check("mipmapEnabled", ti.mipmapEnabled, r.Mipmaps);
            if (r.Mipmaps) Check("mipmapFilter", ti.mipmapFilter, r.MipFilter);
            Check("mipMapsPreserveCoverage", ti.mipMapsPreserveCoverage, r.PreserveCoverage);
            Check("anisoLevel", ti.anisoLevel, r.Aniso);
            Check("filterMode", ti.filterMode, r.Filter);
            Check("wrapModeU", ti.wrapModeU, r.WrapU);
            Check("wrapModeV", ti.wrapModeV, r.WrapV);
            Check("alphaIsTransparency", ti.alphaIsTransparency, r.AlphaIsTransparency);
            Check("isReadable", ti.isReadable, r.Readable);
            Check("streamingMipmaps", ti.streamingMipmaps, false);
            Check("npotScale", ti.npotScale, r.NpotScale);
            if (r.Type == TextureImporterType.Default) Check("alphaSource", ti.alphaSource, r.AlphaSource);
            var w = ti.GetPlatformTextureSettings(WebGL);
            Check("WebGL.overridden", w.overridden, true);
            Check("WebGL.maxTextureSize", w.maxTextureSize, r.MaxSize);
            Check("WebGL.format", w.format, r.WebGLFormat);
            if (r.Compressed) Check("WebGL.compressionQuality", w.compressionQuality, r.Quality);
            if (r.Compressed) Check("WebGL.crunchedCompression", w.crunchedCompression, r.Crunched);
            Check("default.maxTextureSize", ti.GetDefaultPlatformTextureSettings().maxTextureSize, r.MaxSize);
            return diffs;
        }
    }
}
