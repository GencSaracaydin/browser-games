using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Markup;
using LongShadow.World.Level.Geometry;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.World.Level.Editor.Nav
{
    /// <summary>
    /// Bakes a section's NavMesh (TECH_PLAN §7.15: one surface per section, agent radius 0.35, height 1.8, step 0.4, slope 40;
    /// area "Doorway" cost 3 over every doorway so the squad never stops in one) from the section's physics colliders on
    /// layer Default (PlayerClips, triggers and "DYN_" door leaves excluded), over the section bounds plus a 4 m seam overlap.
    /// The data is saved as an asset and loaded by a <see cref="NavMeshSurface"/>; the section's seam links
    /// (<see cref="NavSeams"/>, owned by the later-loading section) and in-section links (<see cref="NavLinks"/>) become
    /// <see cref="NavMeshLink"/>s. Uses NavMeshBuilder directly (the editor bake API is deprecated in this Unity).
    /// </summary>
    public static class SectionNavBaker
    {
        /// <summary>Bakes and installs the NavMesh; returns the links by name (for BreachWall etc.).</summary>
        public static Dictionary<string, NavMeshLink> Bake(SectionBuildContext ctx)
        {
            var links = new Dictionary<string, NavMeshLink>();
            if (ctx.Section == SectionId.Heli || ctx.Section == SectionId.TownFar || ctx.Section == SectionId.Booth)
            {
                ctx.Info("NavMesh: none for this section.");
                return links;
            }
            Physics.SyncTransforms();
            var bounds = BakeBounds(ctx.Section);
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var t in ctx.Root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(GateLeaves.DynamicPrefix)) markups.Add(new NavMeshBuildMarkup { root = t, ignoreFromBuild = true });
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, 1 << LayerIds.Default, NavMeshCollectGeometry.PhysicsColliders, 0, false, markups, false, sources);
            var area = DoorwayArea(ctx);
            var doors = 0;
            foreach (var box in DoorwayBoxes(ctx.Section))
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox, transform = Matrix4x4.TRS(box.center, Quaternion.identity, Vector3.one), size = box.size, area = area,
                });
                doors++;
            }
            var settings = Settings();
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                ctx.Warn("NavMesh bake failed (no sources?)");
                return links;
            }
            data.name = "NavMesh_" + Sections.SceneName(ctx.Section);
            var folder = LevelPaths.SectionDataFolder(ctx.Section);
            BuilderUtil.EnsureFolder(folder);
            data = BuilderUtil.CreateOrReplaceAsset(data, folder + "/" + data.name + ".asset");

            var go = new GameObject("NavMeshSurface");
            go.transform.SetParent(ctx.Group(SectionBuildContext.GNav), false);
            var surface = go.AddComponent<NavMeshSurface>();
            surface.agentTypeID = settings.agentTypeID;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = bounds.center;
            surface.size = bounds.size;
            surface.layerMask = 1 << LayerIds.Default;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.navMeshData = data;

            var tri = NavMesh.CalculateTriangulation();
            ctx.Info("NavMesh: " + sources.Count + " sources (" + doors + " doorway boxes, area " + area + "), " + tri.indices.Length / 3 + " triangles in the open scenes.");

            foreach (var seam in NavSeams.OwnedBy(ctx.Section)) links[seam.Name] = Link(ctx, seam);
            foreach (var l in NavLinks.Of(ctx.Section)) links[l.Name] = Link(ctx, l);
            return links;
        }

        /// <summary>NavMesh build settings of the humanoid agent (TECH_PLAN §7.15).</summary>
        public static NavMeshBuildSettings Settings()
        {
            var s = NavMesh.GetSettingsByID(0);
            s.agentRadius = WorldConst.AgentRadius;
            s.agentHeight = WorldConst.AgentHeight;
            s.agentClimb = WorldConst.AgentClimb;
            s.agentSlope = WorldConst.AgentSlope;
            s.minRegionArea = 1.5f;
            s.overrideVoxelSize = true;
            s.voxelSize = WorldConst.AgentRadius / 3f;
            return s;
        }

        /// <summary>World bounds of a section's bake (section rectangles + the seam overlap, height range ± margins).</summary>
        public static Bounds BakeBounds(SectionId section)
        {
            var r = SectionLayout.Bounds(section).Expanded(WorldConst.SeamOverlap);
            var h = SectionLayout.HeightRange(section);
            return r.ToBounds(h.x - 3f, h.y + 6f);
        }

        /// <summary>The "Doorway" area index (ProjectSettings) or the fallback index 3 (logged; Docs/REQUESTS.md WP00/WP11).</summary>
        public static int DoorwayArea(SectionBuildContext ctx)
        {
            var a = NavMesh.GetAreaFromName(WorldConst.DoorwayAreaName);
            if (a >= 0) return a;
            ctx.Info("NavMesh area 'Doorway' is not defined in ProjectSettings; using area index " + WorldConst.DoorwayAreaFallback + " (request filed).");
            return WorldConst.DoorwayAreaFallback;
        }

        private static NavMeshLink Link(SectionBuildContext ctx, NavSeam seam)
        {
            var go = new GameObject("Link_" + seam.Name);
            go.transform.SetParent(ctx.Group(SectionBuildContext.GNav), false);
            var link = go.AddComponent<NavMeshLink>();
            link.agentTypeID = 0;
            link.startPoint = seam.Start;
            link.endPoint = seam.End;
            link.width = seam.Width;
            link.bidirectional = seam.Bidirectional;
            link.autoUpdate = false;
            link.area = 0;
            link.costModifier = -1f;
            link.enabled = !seam.StartsInactive;
            return link;
        }

        /// <summary>Doorway modifier boxes of a section: gate doors, closet doors, ground-floor feature doors and structure doors.</summary>
        public static List<Bounds> DoorwayBoxes(SectionId section)
        {
            var list = new List<Bounds>();
            foreach (var g in GateBlockers.All.Values)
            {
                if (g.Hinges.Length == 0) continue;
                if (!MarkupSections.TryGetSection(g.Id, null, out var s) || s != section) continue;
                var thin = new Vector3(g.Size.x < 1f ? 1.6f : g.Size.x + 0.2f, 3f, g.Size.z < 1f ? 1.6f : g.Size.z + 0.2f);
                list.Add(new Bounds(g.Center, thin));
            }
            foreach (var c in ClosetLayout.All.Values)
            {
                if (!MarkupSections.TryGetSection(c.Id, null, out var s) || s != section) continue;
                list.Add(new Bounds(c.Door + Vector3.up * 1.2f, new Vector3(1.6f, 3f, 1.6f)));
            }
            foreach (var spec in BuildingCatalog.Of(section))
            {
                if (spec.FarOnly || spec.Features.Count == 0) continue;
                var frames = BuildingGeometry.Frames(spec);
                var m = Matrix4x4.TRS(BuildingGeometry.Origin(spec), Quaternion.Euler(0f, spec.Yaw, 0f), Vector3.one);
                foreach (var f in spec.Features)
                {
                    if (!f.Through || f.V0 > 0.1f) continue;
                    var local = frames[f.Side].P((f.U0 + f.U1) * 0.5f, 1.2f, 0f);
                    var w = Mathf.Abs(f.U1 - f.U0) + 0.4f;
                    var size = (f.Side % 2 == 0) ? new Vector3(w, 3f, 1.8f) : new Vector3(1.8f, 3f, w);
                    if (Mathf.Abs(spec.Yaw) > 1f) size = new Vector3(Mathf.Max(size.x, size.z), 3f, Mathf.Max(size.x, size.z));
                    list.Add(new Bounds(m.MultiplyPoint3x4(local), size));
                }
            }
            if (section == SectionId.Square)
            {
                list.Add(new Bounds(new Vector3((CinemaGeometry.AuditoriumDoorX.x + CinemaGeometry.AuditoriumDoorX.y) * 0.5f, 1.2f, CinemaGeometry.AuditoriumZ0), new Vector3(4f, 3f, 1.8f)));
                list.Add(new Bounds(new Vector3((CinemaGeometry.BalconyDoorX.x + CinemaGeometry.BalconyDoorX.y) * 0.5f, CinemaGeometry.UpperY + 1.2f, CinemaGeometry.AuditoriumZ0), new Vector3(4.4f, 3f, 1.8f)));
                list.Add(new Bounds(new Vector3((CinemaGeometry.MarqueeDoorX.x + CinemaGeometry.MarqueeDoorX.y) * 0.5f, CinemaGeometry.UpperY + 1.2f, CinemaGeometry.Footprint.Z0), new Vector3(2.8f, 3f, 1.8f)));
            }
            if (section == SectionId.StationInt)
            {
                list.Add(new Bounds(new Vector3(346f, 1.5f, 351.5f), new Vector3(1.8f, 3f, 1.8f))); // office door
                list.Add(new Bounds(new Vector3(346f, 1.5f, 342f), new Vector3(1.8f, 3f, 1.8f))); // archive door
            }
            return list;
        }
    }
}
