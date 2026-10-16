using System;
using System.IO;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>Project paths for the mission tests (the editor's project root; LS_PROJECT_ROOT or the working tree offline).</summary>
    internal static class TestPaths
    {
        private static string s_root;

        /// <summary>Project root (folder that contains Docs/ and Assets/).</summary>
        public static string Root
        {
            get
            {
                if (s_root != null) return s_root;
                var env = Environment.GetEnvironmentVariable("LS_PROJECT_ROOT");
                if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "Docs"))) return s_root = env;
                try
                {
                    var data = UnityEngine.Application.dataPath;
                    if (!string.IsNullOrEmpty(data)) return s_root = Directory.GetParent(data).FullName;
                }
                catch (Exception)
                {
                    // Offline harness: no engine; fall through to the working tree.
                }
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !System.IO.File.Exists(Path.Combine(dir.FullName, "Docs", "GDD.md"))) dir = dir.Parent;
                return s_root = dir?.FullName ?? Directory.GetCurrentDirectory();
            }
        }

        /// <summary>A file under the project root.</summary>
        public static string File(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

        /// <summary>Text of a project file.</summary>
        public static string Read(params string[] parts) => System.IO.File.ReadAllText(File(parts));

        /// <summary>The mission beat sources (landed, else staged).</summary>
        public static string BeatSourceFolder
        {
            get
            {
                var landed = File("Assets", "_Game", "Scripts", "Mission", "Beats");
                if (Directory.Exists(landed) && Directory.GetFiles(landed, "*.cs").Length > 0) return landed;
                return File("Staging", "Mission", "Beats");
            }
        }
    }
}
