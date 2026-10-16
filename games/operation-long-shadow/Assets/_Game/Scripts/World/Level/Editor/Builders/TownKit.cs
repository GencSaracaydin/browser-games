using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Buildings;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// Shared section-builder pieces: ground slabs with box colliders, lamp fixtures at the <see cref="LampLayout"/> heads
    /// (the head itself is WP10 Look's PF_SodiumLamp_Head bound to the LAMP.nn marker), catalogue buildings, props by
    /// catalogue key, ladders with <see cref="LadderVolume"/>s, PlayerClips, free walls, overhead cables, awnings
    /// (<see cref="AwningPanel"/>) and fruit crates (<see cref="FruitCrate"/>) on their Appendix A markers.
    /// </summary>
    public static class TownKit
    {
        /// <summary>Batch categories.</summary>
        public const string BGround = "Ground", BStreet = "Street", BDressing = "Dressing", BWalls = "Walls";

        /// <summary>A flat ground quad (top face) of a slot over a rectangle.</summary>
        public static void GroundQuad(MeshBuilder mb, AreaRect r, float y, string slot)
        {
            mb.Slot(slot);
            mb.AddQuad(new Vector3(r.X0, y, r.Z0), new Vector3(r.X0, y, r.Z1), new Vector3(r.X1, y, r.Z1), new Vector3(r.X1, y, r.Z0));
        }

        /// <summary>A ground slab: top quad plus a 2 m deep box collider with its top at <paramref name="y"/>.</summary>
        public static void Ground(SectionBuildContext ctx, AreaRect r, float y, string slot, SurfaceType surface, bool collider = true)
        {
            var mb = ctx.Batch(BGround, r.Center);
            GroundQuad(mb, r, y, slot);
            if (collider) mb.AddCollider(new Vector3(r.Center.x, y - 1f, r.Center.z), new Vector3(r.Width, 2f, r.Depth), surface);
        }

        /// <summary>A collider-only ground box (under a decorative ground made of several quads).</summary>
        public static void GroundCollider(SectionBuildContext ctx, AreaRect r, float y, SurfaceType surface)
        {
            ctx.Batch(BGround, r.Center).AddCollider(new Vector3(r.Center.x, y - 1f, r.Center.z), new Vector3(r.Width, 2f, r.Depth), surface);
        }

        /// <summary>Kerb line.</summary>
        public static void Kerb(SectionBuildContext ctx, Vector3 a, Vector3 b) => KitStreet.Kerb(ctx.Batch(BStreet, (a + b) * 0.5f), a, b, KitSlots.SandstoneTrim);

        /// <summary>Every catalogue building of the section.</summary>
        public static int Buildings(SectionBuildContext ctx)
        {
            var n = 0;
            foreach (var spec in BuildingGenerator.OfSection(ctx.Section))
            {
                ctx.Building(spec);
                n++;
            }
            ctx.Info("Buildings: " + n);
            return n;
        }

        /// <summary>
        /// Lamp fixtures of the section: wall brackets or posts whose arm ends at the LAMP.nn head (GDD §8.5), and a cobra
        /// housing shell over the head; WP10 Look binds the lit head (lens, glow, light) to the marker.
        /// </summary>
        public static void Lamps(SectionBuildContext ctx)
        {
            foreach (var lamp in LampLayout.For(ctx.Section)) LampFixture(ctx.Batch(BStreet, lamp.Base), lamp);
        }

        /// <summary>One lamp fixture (arm + housing) ending at <see cref="LampDef.Head"/>.</summary>
        public static void LampFixture(MeshBuilder mb, LampDef lamp)
        {
            var dir = MathX.DirectionFromBearing(lamp.Facing);
            var head = lamp.Head;
            var housingTop = head + Vector3.up * 0.16f;
            if (lamp.Mount == LampMount.Post)
            {
                var foot = lamp.Base;
                var topY = head.y + 0.35f;
                mb.Slot(KitSlots.SteelGreen);
                mb.AddFrustum(foot, 0.11f, 0.06f, topY - foot.y, 8, true, false);
                mb.AddFrustum(foot, 0.17f, 0.14f, 0.55f, 8, true, false);
                var top = new Vector3(foot.x, topY, foot.z);
                mb.AddTube(top + Vector3.down * 0.05f, housingTop - dir * 0.25f, 0.035f, 6);
                mb.AddCollider(foot + Vector3.up * (topY - foot.y) * 0.5f, new Vector3(0.24f, topY - foot.y, 0.24f), SurfaceType.Metal);
            }
            else
            {
                var plate = lamp.Base + Vector3.up * (head.y - lamp.Base.y + 0.3f) + dir * 0.03f;
                mb.Slot(KitSlots.Iron);
                mb.Push(plate, Quaternion.LookRotation(dir, Vector3.up));
                mb.AddBox(Vector3.zero, new Vector3(0.18f, 0.34f, 0.05f));
                mb.Pop();
                var prev = plate + dir * 0.03f;
                for (var i = 1; i <= 6; i++)
                {
                    var t = i / 6f;
                    var p = Vector3.Lerp(plate, housingTop - dir * 0.25f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.18f;
                    mb.AddTube(prev, p, 0.022f, 5);
                    prev = p;
                }
                mb.AddTube(plate + Vector3.down * 0.45f, Vector3.Lerp(plate, housingTop, 0.6f), 0.014f, 4);
            }
            // Cobra housing above the lens (Look's glass bowl hangs below the head point).
            mb.Slot(KitSlots.SteelGreen);
            mb.Push(head, Quaternion.LookRotation(dir, Vector3.up));
            mb.AddChamferBox(new Vector3(0f, 0.08f, 0.05f), new Vector3(0.5f, 0.16f, 0.74f), 0.05f, true, true);
            mb.Pop();
        }

        /// <summary>Places a static prop under the block that contains it.</summary>
        public static GameObject Prop(SectionBuildContext ctx, string key, Vector3 pos, float heading, float scale = 1f, string name = null)
        {
            return LevelProps.Place(key, ctx.RootAt(pos), pos, heading, scale, name);
        }

        /// <summary>Places a prop at ground height (ray-cast).</summary>
        public static GameObject PropOnGround(SectionBuildContext ctx, string key, float x, float z, float heading, float scale = 1f, float fallbackY = 0f)
        {
            var y = ctx.GroundY(x, z, fallbackY);
            return Prop(ctx, key, new Vector3(x, y, z), heading, scale);
        }

        /// <summary>A row of props from <paramref name="a"/> to <paramref name="b"/> every <paramref name="spacing"/> metres.</summary>
        public static void PropRow(SectionBuildContext ctx, string key, Vector3 a, Vector3 b, float spacing, float heading)
        {
            var len = Vector3.Distance(a, b);
            var n = Mathf.Max(1, Mathf.FloorToInt(len / spacing) + 1);
            for (var i = 0; i < n; i++) Prop(ctx, key, Vector3.Lerp(a, b, n == 1 ? 0.5f : i / (float)(n - 1)), heading);
        }

        /// <summary>A sandbag wall (stacked props) between two points, <paramref name="courses"/> high.</summary>
        public static void SandbagWall(SectionBuildContext ctx, Vector3 a, Vector3 b, int courses)
        {
            var dir = b - a;
            dir.y = 0f;
            var len = dir.magnitude;
            if (len < 0.1f) return;
            var heading = MathX.BearingOf(dir);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 0.58f));
            for (var c = 0; c < courses; c++)
            for (var i = 0; i < n; i++)
            {
                var t = (i + (c % 2) * 0.5f) / n;
                if (t > 1f) continue;
                var p = Vector3.Lerp(a, b, t) + Vector3.up * (c * 0.17f);
                LevelProps.Place("Sandbag", ctx.RootAt(p), p, heading + ((i + c) % 3 - 1) * 4f, 1f, null, true, false);
            }
            var mb = ctx.Batch(BWalls, (a + b) * 0.5f);
            mb.Push((a + b) * 0.5f + Vector3.up * courses * 0.085f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            mb.AddCollider(Vector3.zero, new Vector3(0.42f, courses * 0.17f, len), SurfaceType.Sand);
            mb.Pop();
        }

        /// <summary>A ladder mesh plus an anonymous <see cref="LadderVolume"/> from bottom to top.</summary>
        public static void Ladder(SectionBuildContext ctx, string name, Vector3 bottom, Vector3 top, Vector3 awayFromWall, bool hoops)
        {
            KitStructures.Ladder(ctx.Batch(BDressing, bottom), bottom, top, awayFromWall, 0.5f, KitSlots.SteelFrame, hoops);
            LadderVolume(ctx, name, bottom, top, awayFromWall);
        }

        /// <summary>An anonymous <see cref="LadderVolume"/> for a ladder whose mesh another generator wrote.</summary>
        public static void LadderVolume(SectionBuildContext ctx, string name, Vector3 bottom, Vector3 top, Vector3 awayFromWall)
        {
            var go = new GameObject("Ladder_" + name);
            go.transform.SetParent(ctx.Group(SectionBuildContext.GVolumes), false);
            go.transform.position = (bottom + top) * 0.5f + awayFromWall.normalized * 0.35f;
            go.layer = LayerIds.Trigger;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, Vector3.Distance(bottom, top) + 1f, 0.9f);
            var b = new GameObject("Bottom").transform;
            b.SetParent(go.transform, false);
            b.position = bottom + awayFromWall.normalized * 0.45f;
            var t = new GameObject("Top").transform;
            t.SetParent(go.transform, false);
            t.position = top - awayFromWall.normalized * 0.4f;
            var lv = go.AddComponent<global::LongShadow.Core.LadderVolume>();
            lv.Configure(b, t);
            lv.Configure(true, false, true);
        }

        /// <summary>
        /// A baked interior practical (WP10 Look request (7): Light component, Baked mode, 2700 K tungsten, no runtime cost),
        /// under Lights/<paramref name="group"/> (S6 bakes NORMAL without "Emergency*" lights and EMERGENCY without the
        /// "BasementTungsten" group).
        /// </summary>
        public static Light BakedLight(SectionBuildContext ctx, string group, string name, Vector3 position, float range, float intensity, float kelvin = 2700f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Child(SectionBuildContext.GLights, group), false);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.useColorTemperature = true;
            l.colorTemperature = kelvin;
            l.color = Color.white;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            return l;
        }

        /// <summary>A PlayerClip box under Blockers.</summary>
        public static GameObject Clip(SectionBuildContext ctx, string name, Vector3 center, Vector3 size, float yaw = 0f) =>
            LevelObjects.PlayerClip("Clip_" + name, ctx.Group(SectionBuildContext.GBlockers), center, size, Quaternion.Euler(0f, yaw, 0f));

        /// <summary>A plastered free-standing wall with coping (garden/compound walls).</summary>
        public static void Wall(SectionBuildContext ctx, Vector3 a, Vector3 b, float height, float thickness, string slot, SurfaceType surface)
        {
            Landmarks.FreeWall(ctx.Batch(BWalls, (a + b) * 0.5f, WorldConst.LightmapScaleHouse), a, b, height, thickness, slot, KitSlots.SandstoneTrim, surface);
        }

        /// <summary>An overhead cable between two points.</summary>
        public static void Cable(SectionBuildContext ctx, Vector3 a, Vector3 b, float sag) =>
            KitRoofProps.Cable(ctx.Batch(BDressing, (a + b) * 0.5f), a, b, sag, 0.012f, KitSlots.Cable);

        /// <summary>
        /// An awning on its Appendix A marker (GDD §7.3 B3.AW01…08): intact and torn cloth meshes (probe-lit, not static),
        /// a timber frame, and an <see cref="AwningPanel"/> whose local bounds cover the cloth for the tracer test.
        /// <paramref name="wallLeft"/>/<paramref name="wallRight"/> are the fixing points (world), <paramref name="out"/> the
        /// direction and length the cloth projects, <paramref name="drop"/> how far its front edge hangs lower.
        /// </summary>
        public static void Awning(SectionBuildContext ctx, string id, Vector3 wallLeft, Vector3 wallRight, Vector3 @out, float drop, int tint)
        {
            if (!ctx.Markers.TryGetValue(id, out var marker))
            {
                ctx.Warn("Awning marker missing: " + id);
                return;
            }
            var origin = marker.transform.position;
            var frontL = wallLeft + @out + Vector3.down * drop;
            var frontR = wallRight + @out + Vector3.down * drop;
            var intact = new MeshBuilder();
            KitStructures.ClothPanel(intact, wallLeft - origin, wallRight - origin, frontL - origin, frontR - origin, 0.12f, false, tint, KitSlots.Cloth(tint));
            var torn = new MeshBuilder();
            KitStructures.ClothPanel(torn, wallLeft - origin, wallRight - origin, frontL - origin, frontR - origin, 0.2f, true, tint + 7, KitSlots.Cloth(tint));
            var gi = ctx.Mesh(MeshAssets.SafeName(id) + "_Intact", marker.transform, origin, Quaternion.identity, intact, 0f, false);
            var gt = ctx.Mesh(MeshAssets.SafeName(id) + "_Torn", marker.transform, origin, Quaternion.identity, torn, 0f, false);
            LevelObjects.ClearStatic(gi);
            LevelObjects.ClearStatic(gt);
            gt.SetActive(false);
            KitStructures.AwningFrame(ctx.Batch(BDressing, origin), wallLeft, wallRight, frontL, frontR, KitSlots.WoodDark);
            var b = new Bounds(Vector3.zero, Vector3.zero);
            var first = true;
            foreach (var p in new[] { wallLeft, wallRight, frontL, frontR })
            {
                var l = p - origin;
                if (first)
                {
                    b = new Bounds(l, Vector3.one * 0.1f);
                    first = false;
                }
                else b.Encapsulate(l);
            }
            b.Expand(new Vector3(0.05f, 0.35f, 0.05f));
            marker.AddComponent<AwningPanel>().Configure(gi, gt, null, b);
        }

        /// <summary>
        /// A fruit crate on its Appendix A marker (GDD §7.3 B3.Fruit1…6): the crate prop, a heaped fruit layer, a box
        /// collider (the weapon ray hits it; <see cref="FruitCrate"/> is its own IDamageable) and the pooled fruit prefab.
        /// </summary>
        public static void FruitCrate(SectionBuildContext ctx, string id, bool pomegranate, float heading)
        {
            if (!ctx.Markers.TryGetValue(id, out var marker))
            {
                ctx.Warn("Fruit crate marker missing: " + id);
                return;
            }
            var t = marker.transform;
            t.rotation = Quaternion.Euler(0f, heading, 0f);
            var crate = LevelProps.Place(pomegranate ? "FruitCratePomegranate" : "FruitCrateOrange", t, t.position, heading, 1f, "Crate", true, false);
            var heap = new GameObject("Heap");
            heap.transform.SetParent(t, false);
            heap.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var fruitKey = pomegranate ? "Pomegranate" : "Orange";
            for (var i = 0; i < 9; i++)
            {
                var p = new Vector3(((i % 3) - 1) * 0.16f, 0.04f + (i % 2) * 0.03f, ((i / 3) - 1) * 0.26f);
                LevelProps.Place(fruitKey, heap.transform, t.TransformPoint(heap.transform.localPosition + p), heading + i * 37f, 1f, "Fruit" + i, false, false);
            }
            var col = marker.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.25f, 0f);
            col.size = new Vector3(0.62f, 0.5f, 0.92f);
            marker.AddComponent<SurfaceTag>().Surface = SurfaceType.Wood;
            var fc = marker.AddComponent<global::LongShadow.World.Level.FruitCrate>();
            fc.Configure(FruitPrefab(fruitKey), heap, 7, 16);
            if (crate != null) crate.name = "Crate";
        }

        /// <summary>The pooled physics fruit prefab (model or greybox sphere, SphereCollider, Rigidbody, layer Debris).</summary>
        public static GameObject FruitPrefab(string key)
        {
            var path = LevelPaths.PropPrefabs + "/Market/PF_Fruit_" + key + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && LevelProps.Model(key) == null) return existing;
            BuilderUtil.EnsureFolder(LevelPaths.PropPrefabs + "/Market");
            var root = new GameObject("PF_Fruit_" + key);
            var model = LevelProps.Model(key);
            if (model != null) ((GameObject)PrefabUtility.InstantiatePrefab(model)).transform.SetParent(root.transform, false);
            else
                BuilderUtil.PlaceholderPrimitive("PH_" + key, PrimitiveType.Sphere, Vector3.one * (key == "Orange" ? 0.08f : 0.09f),
                    key == "Orange" ? new Color(0.95f, 0.5f, 0.08f) : new Color(0.62f, 0.08f, 0.1f), root.transform);
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = key == "Orange" ? 0.04f : 0.045f;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = key == "Orange" ? 0.2f : 0.3f;
            rb.interpolation = RigidbodyInterpolation.None;
            root.AddComponent<SurfaceTag>().Surface = SurfaceType.Fruit;
            BuilderUtil.SetLayerRecursively(root, LayerIds.Debris);
            return BuilderUtil.SavePrefab(root, path);
        }
    }
}
