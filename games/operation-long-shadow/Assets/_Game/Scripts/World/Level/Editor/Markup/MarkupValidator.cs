using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>
    /// MarkupTools' validator (TECH_PLAN §7.15 "validate every Appendix A marker/trigger"): checks a section scene against
    /// <c>markup_ids.csv</c> and the derived markers — every id present exactly once, the right component, within 0.6 m of
    /// its Appendix A point (or its <see cref="MarkupOverrides"/> position), triggers with trigger colliders on layer
    /// Trigger, gates with their blockers, closets with a door and 2+ spawn points (SC_B7_UP ≥ 15 m from the office door),
    /// node sets with three slots on the NavMesh, routes and splines with points, InteractionPoints on the four interaction
    /// ids, and no ids the table does not know. Writes <c>Logs/markup_validate_&lt;scene&gt;.txt</c>.
    /// </summary>
    public static class MarkupValidator
    {
        /// <summary>Position tolerance against Appendix A.</summary>
        public const float PositionTolerance = 0.6f;

        /// <summary>Validates an open section scene.</summary>
        public static MarkupReport Validate(Scene scene, SectionId section, MarkupIdTable table)
        {
            var report = new MarkupReport();
            var found = Collect(scene);
            var hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
            var expected = new HashSet<string>();
            foreach (var row in table.Rows)
            {
                if (row.Id.EndsWith(".*")) continue;
                if (!MarkupSections.TryGetSection(row.Id, row.Beat, out var s) || s != section) continue;
                if (row.Kind == "COVER" && !row.HasPosition) continue; // the group (B2.AlertCover) is anonymous nodes
                expected.Add(row.Id);
                report.Checked++;
                if (!found.TryGetValue(row.Id, out var list) || list.Count == 0)
                {
                    report.Errors.Add("missing " + row.Id + " (" + row.Kind + " " + row.Component + ")");
                    continue;
                }
                if (list.Count > 1) report.Errors.Add("duplicate id " + row.Id + " x" + list.Count);
                CheckRow(row, list[0], report, hasNavMesh);
            }
            foreach (var d in DerivedMarkers.For(section))
            {
                if (string.IsNullOrEmpty(d.Id)) continue;
                expected.Add(d.Id);
                report.Checked++;
                if (!found.TryGetValue(d.Id, out var list) || list.Count == 0) report.Errors.Add("missing derived marker " + d.Id);
                else if (Vector3.Distance(list[0].transform.position, d.Position) > PositionTolerance)
                    report.Errors.Add(d.Id + " is " + Vector3.Distance(list[0].transform.position, d.Position).ToString("0.00") + " m from its derived position");
            }
            if (section == SectionId.Approach) CheckAnonymousCover(scene, "B2.AlertCover", 4, report);
            foreach (var extra in ExtraIds(section)) expected.Add(extra);
            foreach (var kv in found)
            {
                if (expected.Contains(kv.Key)) continue;
                if (kv.Key.StartsWith("LAMPFAR.")) continue;
                report.Warnings.Add("id not in Appendix A or the derived table: " + kv.Key);
            }
            return report;
        }

        /// <summary>Ids WP10 Level adds besides the CSV and derived table (Docs/REQUESTS.md: B11.Truck1/2).</summary>
        public static IEnumerable<string> ExtraIds(SectionId section)
        {
            if (section == SectionId.Heli)
            {
                yield return VehicleRoutes.SquareTruck1Id;
                yield return VehicleRoutes.SquareTruck2Id;
            }
        }

        /// <summary>Every SceneMarker with an id in a scene, by id.</summary>
        public static Dictionary<string, List<SceneMarker>> Collect(Scene scene)
        {
            var map = new Dictionary<string, List<SceneMarker>>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var m in root.GetComponentsInChildren<SceneMarker>(true))
            {
                if (string.IsNullOrEmpty(m.Id)) continue;
                if (!map.TryGetValue(m.Id, out var list)) map[m.Id] = list = new List<SceneMarker>(1);
                list.Add(m);
            }
            return map;
        }

        private static void CheckRow(MarkupIdRow row, SceneMarker m, MarkupReport report, bool hasNavMesh)
        {
            var id = row.Id;
            // Position (XZ only for floor-relative and point-laid boxes; splines and paths have no single point).
            if (row.HasPosition && row.Kind != "SPLINE" && row.Kind != "POLY" && row.Kind != "N")
            {
                var want = MarkupOverrides.TryGet(id, out var ov) ? ov.Position : row.Position;
                var have = id.StartsWith("HELI.") ? m.transform.localPosition : m.transform.position;
                var xzOnly = row.Kind == "T" && (row.Notes != null && row.Notes.Contains("floor-relative") || row.Points != null && row.Points.Count == 2);
                var dist = xzOnly ? Vector2.Distance(new Vector2(have.x, have.z), new Vector2(want.x, want.z)) : Vector3.Distance(have, want);
                if (dist > PositionTolerance) report.Errors.Add(id + " is " + dist.ToString("0.00") + " m from Appendix A " + want);
            }
            switch (row.Kind)
            {
                case "T":
                {
                    var t = m as TriggerVolume;
                    if (t == null) { report.Errors.Add(id + " is not a TriggerVolume"); break; }
                    if (row.Component == "OutOfBoundsVolume" && !(t is OutOfBoundsVolume)) report.Errors.Add(id + " is not an OutOfBoundsVolume");
                    var c = m.GetComponent<Collider>();
                    if (c == null || !c.isTrigger) report.Errors.Add(id + " has no trigger collider");
                    if (m.gameObject.layer != LayerIds.Trigger) report.Warnings.Add(id + " is not on layer Trigger");
                    break;
                }
                case "K":
                {
                    var g = m as GateMarker;
                    if (g == null) { report.Errors.Add(id + " is not a GateMarker"); break; }
                    var def = GateBlockers.Get(id);
                    if (def != null && def.Size != Vector3.zero && m.GetComponentInChildren<BoxCollider>(true) == null) report.Errors.Add(id + " has no blocker collider");
                    if (def != null && def.Hinges.Length > 0 && m.GetComponent<GateDoorAnimator>() == null) report.Warnings.Add(id + " has no GateDoorAnimator");
                    break;
                }
                case "CP":
                {
                    var cp = m as CheckpointMarker;
                    if (cp == null) { report.Errors.Add(id + " is not a CheckpointMarker"); break; }
                    var want = MarkupDefaults.CheckpointNodeSet(id);
                    if (cp.SquadNodeSetId != want) report.Errors.Add(id + " squad node set '" + cp.SquadNodeSetId + "' != '" + want + "'");
                    break;
                }
                case "N":
                {
                    var set = m as SquadNodeSet;
                    if (set == null) { report.Errors.Add(id + " is not a SquadNodeSet"); break; }
                    CheckNode(id, "HOL", set.Hollis, row, report, hasNavMesh);
                    CheckNode(id, "SPA", set.Sparrow, row, report, hasNavMesh);
                    CheckNode(id, "TIN", set.Tinker, row, report, hasNavMesh);
                    break;
                }
                case "G":
                    if (!(m is GlassingPOI)) report.Errors.Add(id + " is not a GlassingPOI");
                    break;
                case "COVER":
                    if (!(m is CoverNode)) report.Errors.Add(id + " is not a CoverNode");
                    break;
                case "P":
                    if (!(m is PerchNode)) report.Errors.Add(id + " is not a PerchNode");
                    break;
                case "C":
                {
                    var sc = m as SpawnCloset;
                    if (sc == null) { report.Errors.Add(id + " is not a SpawnCloset"); break; }
                    if (sc.SpawnPointCount < 2) report.Errors.Add(id + " has fewer than 2 spawn points");
                    if (sc.Door == sc.transform) report.Warnings.Add(id + " has no door transform");
                    if (id == "SC_B7_UP")
                        for (var i = 0; i < sc.SpawnPointCount; i++)
                            if (Vector3.Distance(sc.SpawnPoint(i).position, new Vector3(346f, 0.3f, 350f)) < 15f)
                                report.Errors.Add("SC_B7_UP spawn " + i + " is closer than 15 m to the office door (WP04)");
                    break;
                }
                case "R":
                {
                    var r = m as FlankRoute;
                    if (r == null) report.Errors.Add(id + " is not a FlankRoute");
                    else if (r.Points.Count < 2) report.Errors.Add(id + " route has fewer than 2 points");
                    break;
                }
                case "SPLINE":
                {
                    var sc = m.GetComponent<SplineContainer>();
                    if (sc == null || sc.Spline == null || sc.Spline.Count < 2) report.Errors.Add(id + " has no spline");
                    if (!id.StartsWith("INS.") && m.GetComponent<ISplineVehicle>() == null) report.Errors.Add(id + " has no ISplineVehicle (vehicle builder)");
                    break;
                }
                case "M":
                    if (MarkupPlacer.IsInteraction(id) && !(m is InteractionPoint)) report.Errors.Add(id + " is not an InteractionPoint (WP07)");
                    break;
            }
        }

        private static void CheckNode(string id, string label, SquadNode node, MarkupIdRow row, MarkupReport report, bool hasNavMesh)
        {
            if (!node.IsValid)
            {
                report.Errors.Add(id + "." + label + " slot missing");
                return;
            }
            var p = node.Point.position;
            if (row.Points != null && row.PointLabels != null)
                for (var i = 0; i < row.Points.Count && i < row.PointLabels.Count; i++)
                    if (row.PointLabels[i] == label && Vector3.Distance(row.Points[i], p) > PositionTolerance)
                        report.Errors.Add(id + "." + label + " is " + Vector3.Distance(row.Points[i], p).ToString("0.00") + " m from Appendix A");
            if (hasNavMesh && !NavMesh.SamplePosition(p, out _, 1.0f, NavMesh.AllAreas))
                report.Warnings.Add(id + "." + label + " is not within 1 m of the NavMesh");
        }

        private static void CheckAnonymousCover(Scene scene, string group, int min, MarkupReport report)
        {
            var n = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<CoverNode>(true))
                if (c.Group == group) n++;
            report.Checked++;
            if (n < min) report.Errors.Add(group + " has " + n + " cover nodes (want " + min + "+)");
        }

        /// <summary>Writes a report to Logs/markup_validate_&lt;scene&gt;.txt and the console; returns the file path.</summary>
        public static string Write(SectionId section, MarkupReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Markup validation " + Sections.SceneName(section) + ": " + report.Summary);
            foreach (var e in report.Errors) sb.AppendLine("ERROR " + e);
            foreach (var w in report.Warnings) sb.AppendLine("WARN  " + w);
            var path = Path.Combine(BuilderUtil.ProjectRoot, LevelPaths.LogFolder, "markup_validate_" + Sections.SceneName(section) + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            if (report.Errors.Count > 0) LSLog.Error(LogCategory.Level, "[" + Sections.SceneName(section) + "] markup: " + report.Summary + " (" + path + ")");
            else LSLog.Info(LogCategory.Level, "[" + Sections.SceneName(section) + "] markup: " + report.Summary);
            return path;
        }
    }
}
