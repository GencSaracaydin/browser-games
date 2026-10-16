using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Buildings;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Markup;
using LongShadow.World.Level.Editor.Terrain;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Proxy
{
    /// <summary>
    /// FarProxyBuilder (TECH_PLAN §7.15, D7): the far town of <c>S9_TownFar</c>. For every <see cref="ProxyBlocks"/> block a
    /// <see cref="FarProxyBlock"/> holds the Baked Lit proxy shells of its catalogue buildings and landmarks plus its ground
    /// (the S1 block holds the approach as a height-field mesh 0.5 m below the Terrain and tree impostors), shown by the
    /// <see cref="ProxyBlockDirector"/> whenever the real block is unloaded or more than its swap distance away. The dense
    /// housing (BLD-15, never enterable, the Beat 11 target roofs) is far-only and always on, merged per 80 m cell. Distant
    /// sodium lamps get LAMPFAR.nn markers (WP10 Look binds the sprites) inside the block they belong to.
    /// </summary>
    public static class FarProxyBuilder
    {
        /// <summary>Cell size for merging the dense housing.</summary>
        public const float DenseCell = 80f;

        /// <summary>Builds the far town under the context's Architecture group.</summary>
        public static void Build(SectionBuildContext ctx)
        {
            var folder = ctx.MeshFolder;
            var proxyRoot = ctx.Child(SectionBuildContext.GArchitecture, "FarProxies");
            var blocks = new Dictionary<string, Transform>();
            foreach (var def in ProxyBlocks.All)
            {
                var root = new GameObject("FarProxy_" + def.Id).transform;
                root.SetParent(proxyRoot, false);
                var mb = new MeshBuilder();
                foreach (var spec in BuildingCatalog.All)
                    if (!spec.FarOnly && BlockOf(spec) == def.Id) BuildingGenerator.AppendProxy(spec, mb);
                foreach (var spec in Landmarks(def.Id)) BuildingGenerator.AppendProxy(spec, mb);
                Ground(def, mb);
                LevelObjects.Build("PX_" + def.Id, root, Vector3.zero, Quaternion.identity, mb, LevelObjectOptions.FarProxy(folder));
                if (def.Section == SectionId.Approach) ApproachTerrain(ctx, root, folder);
                root.gameObject.AddComponent<FarProxyBlock>().Configure(def.Id, def.Rect, null);
                blocks[def.Id] = root;
            }
            Dense(ctx, proxyRoot, folder);
            LampSprites(ctx, blocks);
            ctx.Info("Far proxies: " + ProxyBlocks.All.Count + " blocks, " + DenseHousing.All.Count + " dense houses.");
        }

        /// <summary>The block a catalogue building's proxy belongs to (its spec block, else by position).</summary>
        public static string BlockOf(BuildingSpec spec)
        {
            if (!string.IsNullOrEmpty(spec.Block) && ProxyBlocks.Get(spec.Block) != null) return spec.Block;
            var b = ProxyBlocks.Find(spec.Section, BuildingGeometry.Origin(spec));
            return b != null ? b.Id : null;
        }

        /// <summary>Landmark stand-ins (as specs) per block: arches, tower, cinema, Relay Hall, compound walls.</summary>
        public static IEnumerable<BuildingSpec> Landmarks(string blockId)
        {
            BuildingSpec S(string id, float x0, float z0, float x1, float z1, float h, BuildingStyle style, int tint = 0)
            {
                return new BuildingSpec
                {
                    Id = id, Section = SectionId.TownFar, Footprint = new AreaRect(x0, z0, x1, z1), Storeys = new[] { h }, Parapet = 0.4f, Style = style, Tint = tint,
                    Facades = new[] { FacadeKind.Blank, FacadeKind.Blank, FacadeKind.Blank, FacadeKind.Blank }, RoofClutter = false, Wear = 0.4f, Seed = id.GetHashCode() & 0xffff,
                };
            }
            switch (blockId)
            {
                case "S1.Approach":
                    yield return S("PX.WeighbridgeArch.W", 176f, 160f, 180f, 170f, 8.6f, BuildingStyle.Sandstone);
                    yield return S("PX.WeighbridgeArch.E", 186f, 160f, 191f, 170f, 8.6f, BuildingStyle.Sandstone);
                    yield return S("PX.WeighbridgeArch.Top", 180f, 160f, 186f, 170f, 2.2f, BuildingStyle.Sandstone);
                    break;
                case "S3.Square":
                    yield return S("PX.MarketArch.W", 180.2f, 249.6f, 182f, 254f, 8f, BuildingStyle.Vernacular, 2);
                    yield return S("PX.MarketArch.E", 188f, 249.6f, 197f, 254f, 8f, BuildingStyle.Vernacular, 2);
                    yield return S("PX.ClockTower", 217f, 272f, 223f, 278f, 22f, BuildingStyle.Sandstone);
                    break;
                case "S3.Cinema":
                    yield return S("PX.Cinema", CinemaGeometry.Footprint.X0, CinemaGeometry.Footprint.Z0, CinemaGeometry.Footprint.X1, CinemaGeometry.Footprint.Z1,
                        CinemaGeometry.RoofY, BuildingStyle.ArtDeco);
                    break;
                case "S5.West":
                    yield return S("PX.RelayHall", RelayHallGeometry.Footprint.X0, RelayHallGeometry.Footprint.Z0, RelayHallGeometry.Footprint.X1,
                        RelayHallGeometry.Footprint.Z1, RelayHallGeometry.RoofY, BuildingStyle.ArtDeco);
                    yield return S("PX.Wall.W", 299.8f, 290f, 300.2f, 382f, 4f, BuildingStyle.Concrete);
                    yield return S("PX.Wall.SW", 300f, 289.8f, 340f, 290.2f, 4f, BuildingStyle.Concrete);
                    yield return S("PX.Wall.NW", 300f, 381.8f, 345.2f, 382.2f, 4f, BuildingStyle.Concrete);
                    break;
                case "S5.East":
                    yield return S("PX.Wall.E", 391.8f, 290f, 392.2f, 382f, 4f, BuildingStyle.Concrete);
                    yield return S("PX.Wall.SE", 352f, 289.8f, 392f, 290.2f, 4f, BuildingStyle.Concrete);
                    yield return S("PX.Wall.NE", 346.8f, 381.8f, 392f, 382.2f, 4f, BuildingStyle.Concrete);
                    break;
                case "S7.Pitch":
                    yield return S("PX.Bleacher", 328f, 392f, 348f, 397f, 1.6f, BuildingStyle.Concrete);
                    break;
            }
        }

        private static void Ground(ProxyBlockDef def, MeshBuilder mb)
        {
            if (def.Section == SectionId.Approach) return;
            var y = def.Section == SectionId.Pitch ? WorldConst.PitchY : 0f;
            var slot = def.Section == SectionId.Square || def.Id == "S2.North" || def.Id == "S2.South" ? KitSlots.Paving : KitSlots.Dirt;
            var r = def.Rect;
            mb.Slot(slot);
            mb.AddQuad(new Vector3(r.X0, y - 0.02f, r.Z0), new Vector3(r.X0, y - 0.02f, r.Z1), new Vector3(r.X1, y - 0.02f, r.Z1), new Vector3(r.X1, y - 0.02f, r.Z0));
        }

        private static void ApproachTerrain(SectionBuildContext ctx, Transform root, string folder)
        {
            var mesh = MeshAssets.Save(TerrainBuilder.FarMesh(6f, 0.5f), folder + "/PX_ApproachTerrain.asset", true);
            var go = new GameObject("PX_ApproachTerrain");
            go.transform.SetParent(root, false);
            go.layer = LayerIds.FarProxy;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = LevelMaterials.GetProxy(KitSlots.Sand);
            LevelObjects.SetLightmapScale(r, 0.15f);
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, LevelObjects.ArchitectureFlags);
            // Olive/pomegranate impostors (cross quads) where the grove is.
            var trees = new MeshBuilder();
            trees.Slot(KitSlots.Cloth(2));
            var rng = new DetRandom("S1.Grove");
            for (var i = 0; i < 24; i++)
            {
                var p = new Vector3(rng.Range(102f, 170f), 0f, rng.Range(92f, 150f));
                p.y = ApproachHeightField.Height(p.x, p.z) - 0.5f;
                var h = rng.Range(3.5f, 4.8f);
                trees.AddQuadDoubleSided(p + new Vector3(-1.8f, 0.8f, 0f), p + new Vector3(-1.8f, h, 0f), p + new Vector3(1.8f, h, 0f), p + new Vector3(1.8f, 0.8f, 0f));
                trees.AddQuadDoubleSided(p + new Vector3(0f, 0.8f, -1.8f), p + new Vector3(0f, h, -1.8f), p + new Vector3(0f, h, 1.8f), p + new Vector3(0f, 0.8f, 1.8f));
            }
            var o = LevelObjectOptions.FarProxy(folder);
            o.Shadows = UnityEngine.Rendering.ShadowCastingMode.Off;
            LevelObjects.Build("PX_GroveImpostors", root, Vector3.zero, Quaternion.identity, trees, o);
        }

        private static void Dense(SectionBuildContext ctx, Transform proxyRoot, string folder)
        {
            var cells = new Dictionary<Vector2Int, MeshBuilder>();
            foreach (var spec in DenseHousing.All)
            {
                var o = BuildingGeometry.Origin(spec);
                var key = new Vector2Int(Mathf.FloorToInt(o.x / DenseCell), Mathf.FloorToInt(o.z / DenseCell));
                if (!cells.TryGetValue(key, out var mb)) cells[key] = mb = new MeshBuilder();
                BuildingGenerator.AppendProxy(spec, mb);
            }
            var root = new GameObject("Town_Dense").transform;
            root.SetParent(proxyRoot, false);
            foreach (var kv in cells)
                LevelObjects.Build("PX_Dense_" + kv.Key.x + "_" + kv.Key.y, root, Vector3.zero, Quaternion.identity, kv.Value, LevelObjectOptions.FarProxy(folder));
        }

        private static void LampSprites(SectionBuildContext ctx, Dictionary<string, Transform> blocks)
        {
            var n = 0;
            foreach (var lamp in LampLayout.All)
            {
                n++;
                var block = ProxyBlocks.Find(lamp.Section, lamp.Base);
                var parent = block != null && blocks.TryGetValue(block.Id, out var t) ? t : ctx.Group(SectionBuildContext.GMarkup);
                var m = MarkupPlacer.Add<SceneMarker>(ctx, "LampSprites", "LAMPFAR." + n.ToString("00"), lamp.Head, lamp.Facing, "Far sprite of " + lamp.Id);
                m.transform.SetParent(parent, true);
            }
            // Dense housing streets: one lamp on every fourth house front.
            var i = 0;
            foreach (var spec in DenseHousing.All)
            {
                if (i++ % 4 != 0) continue;
                n++;
                var f = BuildingGeometry.Frames(spec)[0];
                var local = f.P(f.Length * 0.5f, LampLayout.BracketHeadHeight, LampLayout.Outreach);
                var world = Matrix4x4.TRS(BuildingGeometry.Origin(spec), Quaternion.Euler(0f, spec.Yaw, 0f), Vector3.one).MultiplyPoint3x4(local);
                var m = MarkupPlacer.Add<SceneMarker>(ctx, "LampSprites", "LAMPFAR." + n.ToString("00"), world, spec.Yaw + 180f, "Far sprite on " + spec.Id);
                m.transform.SetParent(ctx.Child(SectionBuildContext.GMarkup, "LampSprites"), true);
            }
        }
    }
}
