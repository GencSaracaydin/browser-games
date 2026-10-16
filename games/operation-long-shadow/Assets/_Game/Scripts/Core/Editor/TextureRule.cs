using System.Text;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Resolved import settings for one texture (output of <see cref="TextureRules.Resolve"/>, applied by
    /// <see cref="TextureRuleApplier"/>). Every field maps to one TextureImporter setting; WebGL gets an explicit
    /// platform override with <see cref="WebGLFormat"/>, the default platform the same size/quality with Automatic format.
    /// </summary>
    public sealed class TextureRule
    {
        /// <summary>Rule id for logs and reports ("Env/BC", "Weapons/N", "UI/Sprite").</summary>
        public string Name = "";
        /// <summary>Where the size cap came from ("manifest", "hero list", "class default").</summary>
        public string CapSource = "";
        /// <summary>Texture type.</summary>
        public TextureImporterType Type = TextureImporterType.Default;
        /// <summary>2D or cube.</summary>
        public TextureImporterShape Shape = TextureImporterShape.Texture2D;
        /// <summary>Cube mapping for <see cref="Shape"/> = Cube.</summary>
        public TextureImporterGenerateCubemap CubeMapping = TextureImporterGenerateCubemap.AutoCubemap;
        /// <summary>sRGB sampling (colour data).</summary>
        public bool SRGB = true;
        /// <summary>Max Size on every platform.</summary>
        public int MaxSize = 1024;
        /// <summary>Explicit WebGL format (alpha already decided).</summary>
        public TextureImporterFormat WebGLFormat = TextureImporterFormat.DXT1Crunched;
        /// <summary>Compressed at all (false = uncompressed, e.g. RGBA32 small UI, R16 height data, RGBAHalf HDRI).</summary>
        public bool Compressed = true;
        /// <summary>Crunch on (Crunched formats).</summary>
        public bool Crunched = true;
        /// <summary>Compression / crunch quality 0–100.</summary>
        public int Quality = 55;
        /// <summary>Generate mip maps.</summary>
        public bool Mipmaps = true;
        /// <summary>Mip filter.</summary>
        public TextureImporterMipFilter MipFilter = TextureImporterMipFilter.BoxFilter;
        /// <summary>Keep alpha-test coverage in the mips (foliage cards).</summary>
        public bool PreserveCoverage;
        /// <summary>Alpha-test reference for <see cref="PreserveCoverage"/>.</summary>
        public float AlphaTestReference = 0.5f;
        /// <summary>Anisotropic level (Quality setting is "Per Texture").</summary>
        public int Aniso = 2;
        /// <summary>Filter mode.</summary>
        public FilterMode Filter = FilterMode.Trilinear;
        /// <summary>Wrap U.</summary>
        public TextureWrapMode WrapU = TextureWrapMode.Repeat;
        /// <summary>Wrap V.</summary>
        public TextureWrapMode WrapV = TextureWrapMode.Repeat;
        /// <summary>Dilate colour into transparent texels (UI, decals, cards).</summary>
        public bool AlphaIsTransparency;
        /// <summary>Alpha source.</summary>
        public TextureImporterAlphaSource AlphaSource = TextureImporterAlphaSource.FromInput;
        /// <summary>Read/Write (only editor-only height data).</summary>
        public bool Readable;
        /// <summary>NPOT handling.</summary>
        public TextureImporterNPOTScale NpotScale = TextureImporterNPOTScale.ToNearest;
        /// <summary>Editor-only data (bake inputs, height sources): must never be referenced by shipped content.</summary>
        public bool EditorOnly;

        /// <summary>One-line description for logs and reports.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name).Append(": ").Append(Type);
            if (Shape != TextureImporterShape.Texture2D) sb.Append(" ").Append(Shape).Append("/").Append(CubeMapping);
            sb.Append(SRGB ? " sRGB" : " linear").Append(" max ").Append(MaxSize).Append(" (").Append(CapSource).Append(")");
            sb.Append(" WebGL ").Append(WebGLFormat);
            if (Compressed) sb.Append(" q").Append(Quality);
            sb.Append(Mipmaps ? " mips " + MipFilter : " no-mips");
            if (PreserveCoverage) sb.Append(" coverage@").Append(AlphaTestReference);
            sb.Append(" aniso ").Append(Aniso).Append(" ").Append(Filter).Append(" wrap ").Append(WrapU).Append("/").Append(WrapV);
            if (AlphaIsTransparency) sb.Append(" alpha-is-transparency");
            if (Readable) sb.Append(" read/write");
            if (EditorOnly) sb.Append(" EDITOR-ONLY");
            return sb.ToString();
        }
    }
}
