using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// One-call rebuild of everything WP10 Look generates (order matters: profiles and materials before the prefabs that
    /// reference them, prefabs before the rig): volume profiles → materials, tuning, ambient set, foliage conversion →
    /// sun lens flare → FX/practical prefabs → PF_LookRig → ambient SH capture. Safe to re-run after assets land
    /// (GUIDs kept). Unity_RunCommand: <c>LongShadow.World.Look.Editor.LookBuildAll.Run()</c>. Log: Logs/look_build.txt.
    /// </summary>
    public static class LookBuildAll
    {
        [MenuItem("LongShadow/Look/Build All (profiles, materials, prefabs, rig, SH)", priority = 101)]
        public static void Menu() => Run();

        /// <summary>Runs every builder; returns the log.</summary>
        public static string Run()
        {
            var log = new StringBuilder();
            void Step(string name, System.Action a)
            {
                try
                {
                    a();
                }
                catch (System.Exception e)
                {
                    log.Append("\n" + name + " FAILED: " + e.Message);
                    LSLog.Exception(LogCategory.Look, e);
                }
            }

            Step("Volumes", () => VolumeProfileBuilder.BuildAll(log));
            log.AppendLine();
            Step("Materials", () => LookMaterialBuilder.BuildAll(log));
            log.AppendLine();
            Step("LensFlare", () => LensFlareBuilder.Build(log));
            log.AppendLine();
            Step("FX prefabs", () => FXPrefabBuilder.BuildAll(log));
            log.AppendLine();
            Step("Look rig", () => LookRigBuilder.Build(log));
            log.AppendLine();
            Step("Ambient SH", () => AmbientSHCapture.CaptureAll(log));
            AssetDatabase.SaveAssets();
            var text = log.ToString();
            var full = Path.Combine(BuilderUtil.ProjectRoot, "Logs/look_build.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text);
            Debug.Log("[LS:Look] Build All:\n" + text);
            return text;
        }
    }
}
