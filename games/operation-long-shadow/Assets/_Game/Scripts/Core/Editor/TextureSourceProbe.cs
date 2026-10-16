using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Reads image headers (PNG, JPEG, TGA, EXR, Radiance HDR) for the import rules: size, alpha channel, bit depth.
    /// For PNGs that store alpha it decodes the pixels once to tell real opacity from an opaque alpha channel, so an
    /// RGBA PNG whose alpha is all 255 still gets DXT1 (half the size of DXT5). Results are cached per file length and
    /// write time, so re-imports cost nothing.
    /// </summary>
    public static class TextureSourceProbe
    {
        private struct CacheEntry
        {
            public long Length;
            public DateTime Written;
            public bool Scanned;
            public TextureSourceInfo Info;
        }

        private static readonly Dictionary<string, CacheEntry> s_cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Alpha values below this count as "used" (DXT5 is chosen).</summary>
        public const byte AlphaUsedThreshold = 250;

        /// <summary>
        /// Probes a project-relative or absolute path. Unknown formats fall back to the importer's own alpha query. With
        /// <paramref name="scanAlpha"/> false a stored alpha channel counts as used (no pixel decode; for N/M/H/A maps whose
        /// format does not depend on alpha).
        /// </summary>
        public static TextureSourceInfo Probe(string path, TextureImporter importerForFallback = null, bool scanAlpha = true)
        {
            var full = Path.IsPathRooted(path) ? path : Path.Combine(BuilderUtil.ProjectRoot, path);
            TextureSourceInfo info;
            try
            {
                var fi = new FileInfo(full);
                if (!fi.Exists) return default;
                if (s_cache.TryGetValue(full, out var c) && c.Length == fi.Length && c.Written == fi.LastWriteTimeUtc && (c.Scanned || !scanAlpha))
                    return c.Info;
                info = ProbeFile(full, scanAlpha);
                s_cache[full] = new CacheEntry { Length = fi.Length, Written = fi.LastWriteTimeUtc, Scanned = scanAlpha, Info = info };
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Assets, "Texture probe failed for " + path + ": " + e.Message);
                info = default;
            }
            if (!info.Known && importerForFallback != null)
            {
                try
                {
                    info.HasAlphaChannel = importerForFallback.DoesSourceTextureHaveAlpha();
                    info.AlphaUsed = info.HasAlphaChannel;
                }
                catch (Exception)
                {
                    // Not available before the first import; leave as opaque.
                }
            }
            return info;
        }

        /// <summary>Forgets cached results.</summary>
        public static void ClearCache() => s_cache.Clear();

        /// <summary>Parses one file (no cache).</summary>
        public static TextureSourceInfo ProbeFile(string fullPath, bool scanAlpha = true)
        {
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            switch (ext)
            {
                case ".png": return ProbePng(fullPath, scanAlpha);
                case ".jpg":
                case ".jpeg": return ProbeJpeg(fullPath);
                case ".tga": return ProbeTga(fullPath);
                case ".exr": return ProbeExr(fullPath);
                case ".hdr": return ProbeHdr(fullPath);
                default: return default;
            }
        }

        private static byte[] ReadHead(string path, int max)
        {
            using (var fs = File.OpenRead(path))
            {
                var n = (int)Math.Min(max, fs.Length);
                var b = new byte[n];
                var read = 0;
                while (read < n)
                {
                    var r = fs.Read(b, read, n - read);
                    if (r <= 0) break;
                    read += r;
                }
                return b;
            }
        }

        private static int BE32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        private static int BE16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
        private static int LE16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
        private static int LE32(byte[] b, int o) => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);

        private static TextureSourceInfo ProbePng(string path, bool scanAlpha)
        {
            var all = File.ReadAllBytes(path);
            var info = new TextureSourceInfo();
            if (all.Length < 33 || all[0] != 0x89 || all[1] != (byte)'P') return info;
            info.Known = true;
            info.Width = BE32(all, 16);
            info.Height = BE32(all, 20);
            info.BitsPerChannel = all[24];
            var colorType = all[25];
            var hasAlpha = colorType == 4 || colorType == 6;
            // Walk chunks for tRNS (transparency for grey/RGB/palette images) until IDAT.
            var o = 8;
            while (!hasAlpha && o + 8 <= all.Length)
            {
                var len = BE32(all, o);
                var type = Encoding.ASCII.GetString(all, o + 4, 4);
                if (type == "tRNS") hasAlpha = true;
                if (type == "IDAT" || type == "IEND" || len < 0) break;
                o += 12 + len;
            }
            info.HasAlphaChannel = hasAlpha;
            info.AlphaUsed = hasAlpha && (!scanAlpha || PngAlphaUsed(all));
            return info;
        }

        private static bool PngAlphaUsed(byte[] png)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!tex.LoadImage(png, false)) return true; // cannot decode: assume alpha matters
                var px = tex.GetPixels32();
                for (var i = 0; i < px.Length; i++)
                    if (px[i].a < AlphaUsedThreshold) return true;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static TextureSourceInfo ProbeJpeg(string path)
        {
            var b = ReadHead(path, 1 << 20);
            var info = new TextureSourceInfo();
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return info;
            var o = 2;
            while (o + 9 < b.Length)
            {
                if (b[o] != 0xFF) { o++; continue; }
                var m = b[o + 1];
                if (m == 0xFF) { o++; continue; }
                if (m == 0xD8 || (m >= 0xD0 && m <= 0xD7) || m == 0x01) { o += 2; continue; }
                var len = BE16(b, o + 2);
                var isSof = m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC;
                if (isSof)
                {
                    info.Known = true;
                    info.BitsPerChannel = b[o + 4];
                    info.Height = BE16(b, o + 5);
                    info.Width = BE16(b, o + 7);
                    return info;
                }
                o += 2 + len;
            }
            return info;
        }

        private static TextureSourceInfo ProbeTga(string path)
        {
            var b = ReadHead(path, 18);
            var info = new TextureSourceInfo();
            if (b.Length < 18) return info;
            info.Known = true;
            info.Width = LE16(b, 12);
            info.Height = LE16(b, 14);
            var depth = b[16];
            var alphaBits = b[17] & 0x0F;
            info.BitsPerChannel = 8;
            info.HasAlphaChannel = depth == 32 || alphaBits > 0;
            info.AlphaUsed = info.HasAlphaChannel; // TGA is not decoded here
            return info;
        }

        private static TextureSourceInfo ProbeExr(string path)
        {
            var b = ReadHead(path, 64 * 1024);
            var info = new TextureSourceInfo { IsHdr = true, BitsPerChannel = 16 };
            if (b.Length < 8 || LE32(b, 0) != 20000630) return info;
            var o = 8;
            while (o < b.Length && b[o] != 0)
            {
                var nameEnd = Array.IndexOf(b, (byte)0, o);
                if (nameEnd < 0) break;
                var name = Encoding.ASCII.GetString(b, o, nameEnd - o);
                var typeEnd = Array.IndexOf(b, (byte)0, nameEnd + 1);
                if (typeEnd < 0 || typeEnd + 4 >= b.Length) break;
                var size = LE32(b, typeEnd + 1);
                var val = typeEnd + 5;
                if (name == "dataWindow" && val + 16 <= b.Length)
                {
                    info.Known = true;
                    info.Width = LE32(b, val + 8) - LE32(b, val) + 1;
                    info.Height = LE32(b, val + 12) - LE32(b, val + 4) + 1;
                }
                if (name == "channels")
                {
                    var ch = Encoding.ASCII.GetString(b, val, Math.Min(size, b.Length - val));
                    info.HasAlphaChannel = ch.IndexOf("A\0", StringComparison.Ordinal) >= 0;
                    info.AlphaUsed = info.HasAlphaChannel;
                }
                o = val + size;
            }
            return info;
        }

        private static TextureSourceInfo ProbeHdr(string path)
        {
            var b = ReadHead(path, 8 * 1024);
            var info = new TextureSourceInfo { IsHdr = true, BitsPerChannel = 32 };
            var text = Encoding.ASCII.GetString(b);
            foreach (var line in text.Split('\n'))
            {
                var t = line.Trim();
                var parts = t.Split(' ');
                if (parts.Length == 4 && (parts[0] == "-Y" || parts[0] == "+Y") && int.TryParse(parts[1], out var h) && int.TryParse(parts[3], out var w))
                {
                    info.Known = true;
                    info.Width = w;
                    info.Height = h;
                    break;
                }
            }
            return info;
        }
    }
}
