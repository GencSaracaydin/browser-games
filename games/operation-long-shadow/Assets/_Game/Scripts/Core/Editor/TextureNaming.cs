using System.IO;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Parses texture file names of the form <c>T_&lt;Name&gt;_&lt;Suffix&gt;</c> (TECH_PLAN §4.2). The set name is
    /// everything between "T_" and the suffix: "T_WPN_AK_Mag_N" is set "WPN_AK_Mag", suffix N. Suffixes are matched
    /// case-sensitively so "rock_a" is not read as an A map.
    /// </summary>
    public static class TextureNaming
    {
        /// <summary>Suffix of a file name or path (extension ignored); <paramref name="setName"/> gets the set name.</summary>
        public static TextureSuffix ParseSuffix(string fileNameOrPath, out string setName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileNameOrPath ?? string.Empty);
            setName = stem.StartsWith("T_") ? stem.Substring(2) : stem;
            var us = stem.LastIndexOf('_');
            if (us <= 0 || us == stem.Length - 1) return TextureSuffix.None;
            TextureSuffix s;
            switch (stem.Substring(us + 1))
            {
                case "BC": s = TextureSuffix.BC; break;
                case "N": s = TextureSuffix.N; break;
                case "M": s = TextureSuffix.M; break;
                case "E": s = TextureSuffix.E; break;
                case "H": s = TextureSuffix.H; break;
                case "A": s = TextureSuffix.A; break;
                default: return TextureSuffix.None;
            }
            var head = stem.Substring(0, us);
            setName = head.StartsWith("T_") ? head.Substring(2) : head;
            return s;
        }

        /// <summary>Suffix only.</summary>
        public static TextureSuffix ParseSuffix(string fileNameOrPath) => ParseSuffix(fileNameOrPath, out _);

        /// <summary>The file name of a set member ("T_" + set + "_" + suffix, no extension).</summary>
        public static string FileStem(string setName, TextureSuffix suffix) =>
            suffix == TextureSuffix.None ? "T_" + setName : "T_" + setName + "_" + suffix;

        /// <summary>True for linear data maps (N, M, H, A).</summary>
        public static bool IsLinear(TextureSuffix s) =>
            s == TextureSuffix.N || s == TextureSuffix.M || s == TextureSuffix.H || s == TextureSuffix.A;
    }
}
