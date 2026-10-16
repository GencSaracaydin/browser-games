using System.Collections.Generic;
using System.Text;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using UnityEngine;

namespace LongShadow.UI.Editor
{
    /// <summary>
    /// Checks that no two static HUD regions overlap and none leaves the canvas (WP08 definition of done: every HUD
    /// element at 1920 × 1080 and 1280 × 720 with no overlap; also 4:3 and 21:9). Builds a throwaway HUD in edit mode,
    /// lays it out for each screen size with the CanvasScaler formula, and compares the regions the views report.
    /// </summary>
    public static class HudLayoutAudit
    {
        /// <summary>Screen sizes checked.</summary>
        public static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080),
        };

        /// <summary>Runs the audit; returns the problems (empty = pass) and writes a report.</summary>
        public static List<string> Run(UITheme theme, StringBuilder report)
        {
            var problems = new List<string>();
            var go = new GameObject("HUD_LayoutAudit") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var hud = go.AddComponent<HudRoot>();
                hud.Initialize(theme);
                var regions = new List<HudLayoutRegion>();
                foreach (var screen in Screens)
                {
                    var size = HudMath.CanvasSize(screen);
                    hud.ForceLayout(size);
                    regions.Clear();
                    hud.CollectLayout(regions);
                    report?.AppendLine(screen.x + "x" + screen.y + " -> canvas " + size.x.ToString("0") + "x" + size.y.ToString("0"));
                    for (var i = 0; i < regions.Count; i++)
                    {
                        var a = regions[i];
                        report?.AppendLine("  " + a);
                        if (a.Rect.xMin < -0.5f || a.Rect.yMin < -0.5f || a.Rect.xMax > size.x + 0.5f || a.Rect.yMax > size.y + 0.5f)
                            problems.Add(screen.x + "x" + screen.y + ": " + a.Name + " leaves the canvas " + a);
                        for (var j = i + 1; j < regions.Count; j++)
                        {
                            var b = regions[j];
                            if (a.Rect.Overlaps(b.Rect))
                                problems.Add(screen.x + "x" + screen.y + ": " + a.Name + " overlaps " + b.Name + " (" + a + " / " + b + ")");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            report?.AppendLine(problems.Count == 0 ? "PASS: no overlaps." : "FAIL: " + problems.Count + " problem(s).");
            foreach (var p in problems) report?.AppendLine("  " + p);
            return problems;
        }
    }
}
