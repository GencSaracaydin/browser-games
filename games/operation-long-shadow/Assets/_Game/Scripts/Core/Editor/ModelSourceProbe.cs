using System;
using System.IO;
using System.Text;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Scans a model file (FBX binary or ASCII, OBJ) for authored LOD names ("_LOD1"). Object names are stored as plain
    /// bytes in both FBX encodings, so a byte search is enough and needs no FBX parser.
    /// </summary>
    public static class ModelSourceProbe
    {
        private static readonly byte[][] s_lodMarkers =
        {
            Encoding.ASCII.GetBytes("_LOD1"), Encoding.ASCII.GetBytes("_lod1"), Encoding.ASCII.GetBytes("_Lod1"),
        };

        /// <summary>Files larger than this are not scanned (reported as unknown).</summary>
        public const long MaxScanBytes = 256L * 1024 * 1024;

        /// <summary>Probes a project-relative or absolute path.</summary>
        public static ModelSourceInfo Probe(string path)
        {
            var full = Path.IsPathRooted(path) ? path : Path.Combine(BuilderUtil.ProjectRoot, path);
            var info = new ModelSourceInfo();
            try
            {
                var fi = new FileInfo(full);
                if (!fi.Exists) return info;
                info.Bytes = fi.Length;
                if (fi.Length > MaxScanBytes) return info;
                var bytes = File.ReadAllBytes(full);
                info.Known = true;
                foreach (var marker in s_lodMarkers)
                {
                    if (IndexOf(bytes, marker) < 0) continue;
                    info.HasAuthoredLods = true;
                    break;
                }
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Assets, "Model probe failed for " + path + ": " + e.Message);
            }
            return info;
        }

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            var last = hay.Length - needle.Length;
            var first = needle[0];
            for (var i = 0; i <= last; i++)
            {
                if (hay[i] != first) continue;
                var j = 1;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
