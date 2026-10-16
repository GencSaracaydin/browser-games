using System.Collections.Generic;
using LongShadow.Core;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Light-probe placement along the playable path (TECH_PLAN §3.8 "Light Probe Groups along the path every 2–4 m"):
    /// samples the Appendix A critical-path polylines every 3 m, finds the floor with a downward ray against the
    /// section's colliders, and adds probes at 0.6, 1.7 and 3.2 m above it on the path and 3 m to either side. Used by
    /// <see cref="SectionBaker"/> when a section has no LightProbeGroup; also a menu for the open scene.
    /// </summary>
    public static class ProbePlacer
    {
        /// <summary>Spacing along the path (m).</summary>
        public const float Spacing = 3f;
        private static readonly float[] Heights = { 0.6f, 1.7f, 3.2f };
        private static readonly float[] Sides = { -3f, 0f, 3f };

        [MenuItem("LongShadow/Look/Place Light Probes On Critical Path (open scene)", priority = 160)]
        private static void PlaceMenu()
        {
            var beats = new List<int>();
            for (var b = 1; b <= 10; b++) beats.Add(b);
            var pos = AlongBeats(beats.ToArray(), true);
            if (pos.Count == 0)
            {
                Debug.Log("[LS:Look] No floor found under any critical-path knot in the open scene(s).");
                return;
            }
            var go = new GameObject("LightProbes_CriticalPath");
            go.AddComponent<LightProbeGroup>().probePositions = pos.ToArray();
            Undo.RegisterCreatedObjectUndo(go, "Place light probes");
            Debug.Log("[LS:Look] Placed " + pos.Count + " light probes.");
        }

        /// <summary>Probe positions (world = local of a group at the origin) along the beats' knots.</summary>
        public static List<Vector3> AlongBeats(int[] beats, bool onlyWhereFloorExists = true)
        {
            var result = new List<Vector3>();
            var seen = new HashSet<Vector3Int>();
            foreach (var beat in beats)
            {
                var knots = SectionBakeTable.Knots(beat);
                if (knots.Length < 2) continue;
                var length = MathX.PolylineLength(knots);
                for (var d = 0f; d <= length + 0.01f; d += Spacing)
                {
                    var p = MathX.PointAlongPolyline(knots, d);
                    var ahead = MathX.PointAlongPolyline(knots, Mathf.Min(length, d + 0.5f));
                    var dir = ahead - p;
                    dir.y = 0f;
                    var side = dir.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, dir.normalized) : Vector3.right;
                    foreach (var s in Sides)
                    {
                        var q = p + side * s;
                        if (!Floor(q, out var floor))
                        {
                            if (onlyWhereFloorExists) continue;
                            floor = q.y;
                        }
                        foreach (var h in Heights)
                        {
                            var pos = new Vector3(q.x, floor + h, q.z);
                            var key = Vector3Int.RoundToInt(pos * 2f);
                            if (seen.Add(key)) result.Add(pos);
                        }
                    }
                }
            }
            return result;
        }

        private static bool Floor(Vector3 p, out float y)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 12f, LayerIds.Ground, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }
            y = p.y;
            return false;
        }
    }
}
