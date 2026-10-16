using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>Editor loader for <see cref="MarkupIdTable"/> (Assets/_Game/Data/markup_ids.csv).</summary>
    public static class MarkupIds
    {
        /// <summary>Loads and parses the CSV (empty table and an error log if missing).</summary>
        public static MarkupIdTable Load()
        {
            var path = Path.Combine(BuilderUtil.ProjectRoot, MarkupIdTable.AssetPath);
            if (!File.Exists(path))
            {
                LSLog.Error(LogCategory.Level, "markup_ids.csv not found at " + path);
                return MarkupIdTable.Parse(string.Empty);
            }
            var t = MarkupIdTable.Parse(File.ReadAllText(path));
            foreach (var p in t.Problems) LSLog.Warn(LogCategory.Level, "markup_ids.csv: " + p);
            return t;
        }

        /// <summary>Toggles scene-view labels of markup gizmos.</summary>
        [MenuItem("LongShadow/Markup/Toggle Gizmo Labels")]
        public static void ToggleLabels()
        {
            MarkupGizmos.ShowLabels = !MarkupGizmos.ShowLabels;
            SceneView.RepaintAll();
            Debug.Log("[LS:Level] Markup labels " + (MarkupGizmos.ShowLabels ? "on" : "off"));
        }
    }
}
