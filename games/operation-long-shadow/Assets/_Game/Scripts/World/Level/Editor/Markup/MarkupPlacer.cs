using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace LongShadow.World.Level.Editor.Markup
{
    /// <summary>
    /// MarkupTools' placer (TECH_PLAN §7.15, D13): creates every Appendix A marker, trigger, gate, checkpoint, node set,
    /// cover/perch node, closet, route, spline and POI of a section from <c>Assets/_Game/Data/markup_ids.csv</c>, plus the
    /// derived markers WP10 Level owns (<see cref="DerivedMarkers"/>). Section assignment comes from
    /// <see cref="MarkupSections"/>; positions from the CSV unless <see cref="MarkupOverrides"/> moves them; facings, stances
    /// and tags the appendix leaves implicit from <see cref="MarkupDefaults"/>; closets from <see cref="ClosetLayout"/>; gate
    /// blockers and door leaves from <see cref="GateBlockers"/>. Run after the section's geometry exists (floor-relative
    /// trigger heights ray-cast the ground).
    /// </summary>
    public static class MarkupPlacer
    {
        /// <summary>Sub-groups under Markup.</summary>
        public const string SubMarkers = "Markers", SubTriggers = "Triggers", SubOob = "OutOfBounds", SubGates = "Gates", SubCheckpoints = "Checkpoints",
            SubNodeSets = "NodeSets", SubCover = "Cover", SubPerches = "Perches", SubClosets = "Closets", SubRoutes = "Routes", SubSplines = "Splines",
            SubPois = "POIs", SubDestructibles = "Destructibles", SubCamera = "Cameras", SubCriticalPath = "CriticalPath";

        /// <summary>Places every marker of the context's section; returns how many were placed.</summary>
        public static int Place(SectionBuildContext ctx)
        {
            var count = 0;
            foreach (var row in ctx.Markup.Rows)
            {
                if (row.Id.EndsWith(".*")) continue; // pattern rows (CAM.Look.*, INS.Cam.*)
                if (!MarkupSections.TryGetSection(row.Id, row.Beat, out var section) || section != ctx.Section) continue;
                if (ctx.Markers.ContainsKey(row.Id)) continue;
                if (row.Id.StartsWith("HELI.")) continue; // cabin-local markers: the vehicle builder parents them to Magpie
                if (!row.HasPosition && DerivedMarkers.Get(row.Id) != null) continue; // placed from the derived table below
                if (PlaceRow(ctx, row) != null) count++;
            }
            foreach (var d in DerivedMarkers.For(ctx.Section))
            {
                if (!string.IsNullOrEmpty(d.Id) && ctx.Markers.ContainsKey(d.Id)) continue;
                if (PlaceDerived(ctx, d) != null) count++;
            }
            ctx.Info("Markup: " + count + " markers placed.");
            return count;
        }

        // ------------------------------------------------------------------ rows

        private static GameObject PlaceRow(SectionBuildContext ctx, MarkupIdRow row)
        {
            var pos = row.HasPosition ? row.Position : Vector3.zero;
            if (MarkupOverrides.TryGet(row.Id, out var ov))
            {
                pos = ov.Position;
                ctx.Info("override " + row.Id + " -> " + pos + " (" + ov.Reason + ")");
            }
            var heading = row.HasHeading ? row.Heading : 0f;
            switch (row.Kind)
            {
                case "M": return Plain(ctx, row.Id, pos, heading, row.Notes, row.Id.StartsWith("E") && char.IsDigit(row.Id[1]) ? "Encounters" : SubMarkers);
                case "T": return Trigger(ctx, row, pos);
                case "K": return Gate(ctx, row.Id, pos, row.Notes);
                case "CP": return Checkpoint(ctx, row.Id, pos, row.HasHeading ? heading : MarkupDefaults.CheckpointHeading(row.Id), row.Notes);
                case "N": return NodeSet(ctx, row);
                case "G": return Add<GlassingPOI>(ctx, SubPois, row.Id, pos, heading, row.Notes).gameObject;
                case "D":
                case "D + I": return Destructible(ctx, row, pos);
                case "COVER":
                    if (!row.HasPosition) return null; // B2.AlertCover: the group's nodes are derived markers
                    return CoverWithId(ctx, row.Id, pos, row.Notes);
                case "P": return Perch(ctx, row.Id, pos, row.Notes);
                case "C": return Closet(ctx, row.Id, pos, row.Notes);
                case "R": return Route(ctx, row);
                case "POLY": return CritPath(ctx, row);
                case "SPLINE": return SplineMarker(ctx, row.Id, row.Notes);
                default:
                    ctx.Warn("Unknown markup kind '" + row.Kind + "' for " + row.Id);
                    return null;
            }
        }

        private static GameObject PlaceDerived(SectionBuildContext ctx, DerivedMarker d)
        {
            switch (d.Component)
            {
                case "OutOfBoundsVolume":
                {
                    var oob = Add<OutOfBoundsVolume>(ctx, SubOob, d.Id, d.Position, d.Heading, d.Notes);
                    var box = oob.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = d.BoxSize;
                    oob.gameObject.layer = LayerIds.Trigger;
                    var blocker = d.Id.StartsWith("OOB.Block.") ? d.Id.Substring(4) : null;
                    oob.Configure(true, false, blocker == null);
                    if (blocker != null) oob.gameObject.AddComponent<GateLinkedVolume>().Configure(blocker, true);
                    return oob.gameObject;
                }
                case "CoverNode":
                {
                    var c = Add<CoverNode>(ctx, SubCover, d.Id, d.Position, d.Heading, d.Notes);
                    c.Configure(d.Standing, true, true, 120f, d.Group);
                    if (string.IsNullOrEmpty(d.Id)) c.name = (d.Group ?? "Cover") + "_" + c.transform.GetSiblingIndex();
                    return c.gameObject;
                }
                default:
                {
                    var sub = d.Id.StartsWith("CAM.") ? SubCamera : d.Id.StartsWith("LAMP") || d.Id.StartsWith("MAST.") ? "Practicals" : SubMarkers;
                    var m = Add<SceneMarker>(ctx, sub, d.Id, d.Position, d.Heading, d.Notes);
                    m.transform.rotation = Quaternion.Euler(d.Pitch, d.Heading, 0f);
                    if (d.BoxSize != Vector3.zero) m.transform.localScale = d.BoxSize;
                    return m.gameObject;
                }
            }
        }

        // ------------------------------------------------------------------ creation helpers

        /// <summary>Creates a GameObject with a marker component under Markup/<paramref name="sub"/> and records it.</summary>
        public static T Add<T>(SectionBuildContext ctx, string sub, string id, Vector3 position, float heading, string notes) where T : SceneMarker
        {
            var go = new GameObject(string.IsNullOrEmpty(id) ? typeof(T).Name : id);
            go.transform.SetParent(ctx.Child(SectionBuildContext.GMarkup, sub), false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            var m = go.AddComponent<T>();
            m.SetId(id ?? string.Empty);
            m.SetNotes(notes ?? string.Empty);
            if (!string.IsNullOrEmpty(id)) ctx.Markers[id] = go;
            return m;
        }

        private static GameObject Plain(SectionBuildContext ctx, string id, Vector3 pos, float heading, string notes, string sub)
        {
            if (IsInteraction(id)) return Interaction(ctx, id, pos, heading, notes);
            return Add<SceneMarker>(ctx, sub, id, pos, heading, notes).gameObject;
        }

        /// <summary>The four Appendix A markers that are InteractionPoints (Docs/REQUESTS.md WP07 → WP10 (3)).</summary>
        public static bool IsInteraction(string id) => id == "B4.SideDoor" || id == "B6.HallDoor" || id == "B8.Projector" || id == "B10.HeliDoor";

        /// <summary>
        /// An InteractionPoint (a SceneMarker, so Mission's Gates find it by id): prompt per WP07's GateTable, unavailable
        /// until Mission enables it, used once; collider on layer Interactable for the player's interaction ray.
        /// </summary>
        private static GameObject Interaction(SectionBuildContext ctx, string id, Vector3 pos, float heading, string notes)
        {
            string prompt;
            switch (id)
            {
                case "B4.SideDoor": prompt = "[E] Open"; break;
                case "B6.HallDoor": prompt = "[E] Kick"; break;
                case "B8.Projector": prompt = "[E] Thread the projector"; break;
                default: prompt = "[E] Board"; break;
            }
            var ip = Add<InteractionPoint>(ctx, "Interactions", id, pos, heading, notes);
            var go = ip.gameObject;
            go.layer = LayerIds.Interactable;
            var col = go.AddComponent<BoxCollider>();
            col.size = id == "B10.HeliDoor" ? new Vector3(1.6f, 1.8f, 1.2f) : new Vector3(1.2f, 1.6f, 0.6f);
            ip.Configure(prompt, false, 0f, false, true);
            return go;
        }

        private static GameObject Trigger(SectionBuildContext ctx, MarkupIdRow row, Vector3 pos)
        {
            var isOob = row.Component == "OutOfBoundsVolume";
            TriggerVolume t = isOob
                ? Add<OutOfBoundsVolume>(ctx, SubOob, row.Id, pos, 0f, row.Notes)
                : Add<TriggerVolume>(ctx, SubTriggers, row.Id, pos, 0f, row.Notes);
            var go = t.gameObject;
            go.layer = LayerIds.Trigger;
            if (row.TryGetRadius(out var radius))
            {
                var s = go.AddComponent<SphereCollider>();
                s.isTrigger = true;
                s.radius = radius;
            }
            else
            {
                var size = row.TryGetBox(out var b) ? b : new Vector3(2f, 3f, 2f);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                if (row.Points != null && row.Points.Count == 2)
                {
                    // A box laid along two ground points (B1.T.Scree): follow the slope.
                    var a = row.Points[0];
                    var c = row.Points[1];
                    go.transform.SetPositionAndRotation((a + c) * 0.5f + Vector3.up * (size.y * 0.5f), Quaternion.LookRotation(c - a, Vector3.up));
                    size.z = Vector3.Distance(a, c);
                }
                else
                {
                    if (row.HasHeading) go.transform.rotation = Quaternion.Euler(0f, row.Heading, 0f);
                    if (row.Notes != null && row.Notes.Contains("floor-relative")) FitToGround(ctx, go.transform, ref size);
                }
                box.size = size;
            }
            var startArmed = row.Id != "B7.T.Landing" && row.Id != "B8.T.BasementFoot";
            t.Configure(true, false, startArmed);
            return go;
        }

        /// <summary>Stretches a floor-relative box from 1 m below the lowest to 4 m above the highest ground under it.</summary>
        private static void FitToGround(SectionBuildContext ctx, Transform t, ref Vector3 size)
        {
            var lo = float.MaxValue;
            var hi = float.MinValue;
            for (var i = 0; i <= 4; i++)
            for (var j = 0; j <= 4; j++)
            {
                var local = new Vector3((i / 4f - 0.5f) * size.x, 0f, (j / 4f - 0.5f) * size.z);
                var w = t.position + t.rotation * local;
                // Terrain: cast from high above; town: cast from 3 m above the Appendix A centre so roofs, landings and
                // balconies over the box do not lift it.
                var approach = ctx.Section == SectionId.Approach;
                var y = ctx.GroundY(w.x, w.z, approach ? ApproachHeightField.Height(w.x, w.z) : t.position.y - 1.5f, approach ? 120f : t.position.y + 3f);
                lo = Mathf.Min(lo, y);
                hi = Mathf.Max(hi, y);
            }
            var y0 = lo - 1f;
            var y1 = hi + 4f;
            var p = t.position;
            p.y = (y0 + y1) * 0.5f;
            t.position = p;
            size.y = y1 - y0;
        }

        private static GameObject Gate(SectionBuildContext ctx, string id, Vector3 pos, string notes)
        {
            var gate = Add<GateMarker>(ctx, SubGates, id, pos, 0f, notes);
            var def = GateBlockers.Get(id);
            var blockers = new List<Collider>();
            if (def != null && def.Size != Vector3.zero)
            {
                var clip = LevelObjects.PlayerClip("Blocker", gate.transform, def.Center, def.Size, Quaternion.Euler(0f, def.Yaw, 0f));
                blockers.Add(clip.GetComponent<Collider>());
            }
            else if (def == null) ctx.Warn("No blocker geometry for gate " + id);
            gate.Configure(def != null && def.StartsOpen, blockers.ToArray(), null, null);
            if (def != null && def.Hinges.Length > 0) GateLeaves.Build(ctx, gate.transform, def);
            return gate.gameObject;
        }

        private static GameObject Checkpoint(SectionBuildContext ctx, string id, Vector3 pos, float heading, string notes)
        {
            var cp = Add<CheckpointMarker>(ctx, SubCheckpoints, id, pos, heading, notes);
            cp.SetSquadNodeSet(MarkupDefaults.CheckpointNodeSet(id));
            return cp.gameObject;
        }

        private static GameObject NodeSet(SectionBuildContext ctx, MarkupIdRow row)
        {
            var set = Add<SquadNodeSet>(ctx, SubNodeSets, row.Id, row.Position, 0f, row.Notes);
            var pose = MarkupDefaults.NodeSet(row.Id);
            var stance = pose.Crouch ? Stance.Crouch : Stance.Stand;
            SquadNode Slot(string label, float facing, Vector3 fallback)
            {
                var p = fallback;
                if (row.Points != null && row.PointLabels != null)
                    for (var i = 0; i < row.Points.Count && i < row.PointLabels.Count; i++)
                        if (row.PointLabels[i] == label) p = row.Points[i];
                var t = new GameObject(label).transform;
                t.SetParent(set.transform, false);
                t.SetPositionAndRotation(p, Quaternion.Euler(0f, facing, 0f));
                return new SquadNode { Point = t, Stance = stance, CoverPose = pose.Crouch ? 1 : 0 };
            }
            var c = row.Position;
            set.Configure(Slot("HOL", pose.Hollis, c), Slot("SPA", pose.Sparrow, c + Vector3.right * 2f), Slot("TIN", pose.Tinker, c - Vector3.right * 2f),
                MarkupDefaults.AllowDoorway(row.Id));
            return set.gameObject;
        }

        private static GameObject Destructible(SectionBuildContext ctx, MarkupIdRow row, Vector3 pos)
        {
            var heading = 0f;
            if (row.Id.StartsWith("B6.F"))
            {
                // Floods aim at the yard centre (345, 0, 325) (GDD §7.6; WP10 Look's PF_Floodlight binds here).
                var aim = new Vector3(345f, 0f, 325f) - pos;
                heading = MathX.BearingOf(aim);
            }
            var m = Add<SceneMarker>(ctx, SubDestructibles, row.Id, pos, heading, row.Notes);
            if (row.Id.StartsWith("B6.F")) m.transform.rotation = Quaternion.LookRotation(new Vector3(345f, 0f, 325f) - pos, Vector3.up);
            return m.gameObject;
        }

        private static GameObject CoverWithId(SectionBuildContext ctx, string id, Vector3 pos, string notes)
        {
            var c = Add<CoverNode>(ctx, SubCover, id, pos, MarkupDefaults.WaterTankCoverFacing(id), notes);
            c.Configure(true, true, true, 140f, id.StartsWith("B5.WT") ? "B5.WT" : string.Empty);
            return c.gameObject;
        }

        private static GameObject Perch(SectionBuildContext ctx, string id, Vector3 pos, string notes)
        {
            var tag = MarkupDefaults.PerchTagOf(id, out var facing);
            var p = Add<PerchNode>(ctx, SubPerches, id, pos, facing, notes);
            p.SetTag(tag);
            return p.gameObject;
        }

        private static GameObject Closet(SectionBuildContext ctx, string id, Vector3 pos, string notes)
        {
            var def = ClosetLayout.Get(id);
            var sc = Add<SpawnCloset>(ctx, SubClosets, id, pos, def != null ? def.DoorHeading : 0f, notes);
            if (def == null)
            {
                ctx.Warn("No closet layout for " + id + "; spawns at the marker.");
                return sc.gameObject;
            }
            var door = new GameObject("Door").transform;
            door.SetParent(sc.transform, false);
            door.SetPositionAndRotation(def.Door, Quaternion.Euler(0f, def.DoorHeading, 0f));
            var spawns = new Transform[def.Spawns.Length];
            for (var i = 0; i < spawns.Length; i++)
            {
                var p = def.Spawns[i];
                // The structure generators may know the trench better (RelayHallGeometry anchors).
                if (id == "SC_B9_H" && ctx.Anchors.TryGet("Trench.Spawn." + i, out var a)) p = a.position;
                spawns[i] = new GameObject("Spawn" + i).transform;
                spawns[i].SetParent(sc.transform, false);
                spawns[i].SetPositionAndRotation(p, Quaternion.Euler(0f, def.SpawnHeading, 0f));
            }
            sc.Configure(door, spawns, def.MinPlayerDistance);
            return sc.gameObject;
        }

        private static GameObject Route(SectionBuildContext ctx, MarkupIdRow row)
        {
            var r = Add<FlankRoute>(ctx, SubRoutes, row.Id, row.Position, 0f, row.Notes);
            r.SetPoints(row.Points != null ? row.Points.ToArray() : new[] { row.Position });
            return r.gameObject;
        }

        private static GameObject CritPath(SectionBuildContext ctx, MarkupIdRow row)
        {
            var beat = CriticalPathTable.BeatOf(row.Id);
            var knots = CriticalPathTable.Knots(beat);
            var pos = knots != null && knots.Length > 0 ? knots[0] : VehicleRoutes.Get(VehicleRoutes.Exfil)?.Knots[0] ?? Vector3.zero;
            var m = Add<SceneMarker>(ctx, SubCriticalPath, row.Id, pos, 0f, row.Notes);
            if (knots != null)
                for (var i = 0; i < knots.Length; i++)
                {
                    var k = new GameObject("K" + i).transform;
                    k.SetParent(m.transform, false);
                    k.position = knots[i];
                }
            return m.gameObject;
        }

        /// <summary>
        /// A spline marker: SceneMarker + SplineContainer from <see cref="VehicleRoutes"/> (knots above ground resolved on the
        /// approach height field). The vehicle builder adds the SplineVehicle to the same object.
        /// </summary>
        public static GameObject SplineMarker(SectionBuildContext ctx, string id, string notes)
        {
            if (ctx.Markers.TryGetValue(id, out var existing)) return existing;
            var route = VehicleRoutes.Get(id);
            if (route == null)
            {
                ctx.Warn("No route data for spline " + id);
                return null;
            }
            var m = Add<SceneMarker>(ctx, SubSplines, id, Vector3.zero, 0f, notes ?? route.Notes);
            var container = m.gameObject.AddComponent<SplineContainer>();
            container.Spline = BuildSpline(route);
            return m.gameObject;
        }

        /// <summary>World-space spline through a route's knots (AutoSmooth tangents).</summary>
        public static Spline BuildSpline(VehicleRouteDef route)
        {
            var pts = new List<float3>(route.Knots.Length);
            var agl = VehicleRoutes.KnotHeightsAboveGround(route.Id);
            foreach (var k in route.Knots)
            {
                var p = k;
                if (agl) p.y += ApproachHeightField.Height(k.x, k.z);
                pts.Add(new float3(p.x, p.y, p.z));
            }
            return new Spline(pts, TangentMode.AutoSmooth, false);
        }
    }
}
