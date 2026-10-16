using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Steel and compound pieces (GDD §6.6 BLD-09, BLD-12, BLD-13, BLD-14; manifest PROC_MAST, PROC_RAZOR_WIRE,
    /// PROC_PITCH_PROPS): the 90 m guyed lattice mast with its +25 m platform, floodlight towers, razor-wire coils,
    /// chain-link fence panels, goalposts, the bleacher with its 1.2 m back wall, the barrier boom and the truck-scale plate.
    /// </summary>
    public static class KitIndustrial
    {
        /// <summary>
        /// Square lattice mast: four tapering legs, horizontal girts every panel and X bracing, a maintenance platform with a
        /// railing and an internal ladder (GDD §6.6 BLD-12: 4 m base, 90 m, platform at +25). Returns the platform height.
        /// </summary>
        public static float LatticeMast(MeshBuilder mb, Vector3 foot, float height, float baseWidth, float topWidth, float platformHeight)
        {
            mb.Slot(KitSlots.SteelGreen);
            const float panel = 3f;
            var panels = Mathf.CeilToInt(height / panel);
            Vector3 Leg(int i, float y)
            {
                var w = Mathf.Lerp(baseWidth, topWidth, Mathf.Clamp01(y / height)) * 0.5f;
                var sx = i == 0 || i == 3 ? -1f : 1f;
                var sz = i < 2 ? -1f : 1f;
                return foot + new Vector3(sx * w, y, sz * w);
            }
            for (var leg = 0; leg < 4; leg++) mb.AddTube(Leg(leg, 0f), Leg(leg, height), 0.07f, 6);
            var keep = mb.LodMask;
            for (var p = 0; p < panels; p++)
            {
                var y0 = p * panel;
                var y1 = Mathf.Min(height, y0 + panel);
                mb.LodMask = MeshBuilder.LodAll;
                for (var side = 0; side < 4; side++)
                {
                    var a = side;
                    var b = (side + 1) % 4;
                    mb.AddTube(Leg(a, y1), Leg(b, y1), 0.035f, 4);
                    mb.LodMask = p % 2 == 0 ? MeshBuilder.LodAll : MeshBuilder.LodDetail;
                    mb.AddTube(Leg(a, y0), Leg(b, y1), 0.025f, 4);
                    mb.AddTube(Leg(b, y0), Leg(a, y1), 0.025f, 4);
                    mb.LodMask = MeshBuilder.LodAll;
                }
            }
            mb.LodMask = keep;
            // Platform: grating deck larger than the mast, toe boards and a railing.
            var pw = Mathf.Lerp(baseWidth, topWidth, platformHeight / height) + 2.2f;
            var pc = foot + Vector3.up * platformHeight;
            mb.Slot(KitSlots.SteelPlate);
            mb.AddBox(pc + Vector3.down * 0.05f, new Vector3(pw, 0.1f, pw));
            mb.AddCollider(pc + Vector3.down * 0.05f, new Vector3(pw, 0.1f, pw), SurfaceType.Metal);
            mb.Slot(KitSlots.SteelGreen);
            var h = pw * 0.5f;
            var ring = new[]
            {
                pc + new Vector3(-h, 0f, -h), pc + new Vector3(h, 0f, -h), pc + new Vector3(h, 0f, h), pc + new Vector3(-h, 0f, h), pc + new Vector3(-h, 0f, -h),
            };
            KitStructures.PipeRailing(mb, ring, 1.1f, KitSlots.SteelGreen);
            for (var i = 0; i < 4; i++)
            {
                var a = ring[i];
                var b = ring[i + 1];
                var mid = (a + b) * 0.5f + Vector3.up * 0.55f;
                var dir = (b - a).normalized;
                mb.Push(mid, Quaternion.LookRotation(dir, Vector3.up));
                mb.AddCollider(Vector3.zero, new Vector3(0.1f, 1.1f, pw), SurfaceType.Metal);
                mb.Pop();
            }
            // Struts from the legs to the platform corners.
            for (var leg = 0; leg < 4; leg++) mb.AddTube(Leg(leg, platformHeight - 2.2f), ring[leg], 0.04f, 4);
            // Ladder up the inside face.
            KitStructures.Ladder(mb, foot + new Vector3(0f, 0.2f, -baseWidth * 0.25f), foot + new Vector3(0f, platformHeight, -baseWidth * 0.18f), Vector3.back, 0.45f, KitSlots.Iron, true);
            // Foundation blocks.
            mb.Slot(KitSlots.Concrete);
            for (var leg = 0; leg < 4; leg++) mb.AddBox(Leg(leg, 0f) + Vector3.up * 0.2f, new Vector3(0.8f, 0.4f, 0.8f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(foot + Vector3.up * platformHeight * 0.5f, new Vector3(baseWidth * 0.8f, platformHeight, baseWidth * 0.8f), SurfaceType.Metal, LayerIds.Default, "MastCore");
            return platformHeight;
        }

        /// <summary>Guy-wire anchor block on the ground.</summary>
        public static void GuyAnchor(MeshBuilder mb, Vector3 p)
        {
            mb.Slot(KitSlots.Concrete);
            mb.AddChamferBox(p + Vector3.up * 0.25f, new Vector3(1.2f, 0.5f, 1.2f), 0.08f, true, false);
            mb.Slot(KitSlots.Iron);
            mb.AddTube(p + Vector3.up * 0.5f, p + Vector3.up * 0.9f, 0.03f, 4);
        }

        /// <summary>
        /// Floodlight tower (BLD-13, 8 m): a four-leg steel tower with a caged ladder and a head platform. Returns the head
        /// mount point (the lamp head itself is the PH_SECURITY_LIGHT prop or a procedural head, placed by the builder).
        /// </summary>
        public static Vector3 FloodTower(MeshBuilder mb, Vector3 foot, float height, float aimBearing)
        {
            mb.Slot(KitSlots.SteelGreen);
            const float w = 0.7f;
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
                mb.AddTube(foot + new Vector3(sx * w, 0f, sz * w), foot + new Vector3(sx * w * 0.6f, height, sz * w * 0.6f), 0.05f, 5);
            for (var y = 1.5f; y < height; y += 1.5f)
            {
                var s = Mathf.Lerp(w, w * 0.6f, y / height);
                var c = foot + Vector3.up * y;
                mb.AddTube(c + new Vector3(-s, 0f, -s), c + new Vector3(s, 0f, -s), 0.025f, 4);
                mb.AddTube(c + new Vector3(s, 0f, -s), c + new Vector3(s, 0f, s), 0.025f, 4);
                mb.AddTube(c + new Vector3(s, 0f, s), c + new Vector3(-s, 0f, s), 0.025f, 4);
                mb.AddTube(c + new Vector3(-s, 0f, s), c + new Vector3(-s, 0f, -s), 0.025f, 4);
            }
            var top = foot + Vector3.up * height;
            mb.Slot(KitSlots.SteelPlate);
            mb.AddBox(top + Vector3.down * 0.05f, new Vector3(1.6f, 0.1f, 1.6f));
            KitStructures.PipeRailing(mb, new[] { top + new Vector3(-0.8f, 0f, -0.8f), top + new Vector3(0.8f, 0f, -0.8f), top + new Vector3(0.8f, 0f, 0.8f), top + new Vector3(-0.8f, 0f, 0.8f), top + new Vector3(-0.8f, 0f, -0.8f) }, 1.0f, KitSlots.SteelGreen, false);
            KitStructures.Ladder(mb, foot + new Vector3(0f, 0f, -w - 0.1f), top + new Vector3(0f, 0f, -w * 0.6f - 0.1f), Vector3.back, 0.42f, KitSlots.Iron, true);
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(foot + Vector3.up * 0.15f, new Vector3(2f, 0.3f, 2f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(foot + Vector3.up * height * 0.5f, new Vector3(1.3f, height, 1.3f), SurfaceType.Metal);
            return top + Vector3.up * 0.4f + MathX.DirectionFromBearing(aimBearing) * 0.3f;
        }

        /// <summary>Razor-wire concertina along a line (a helix of thin ribbons on posts), coils 0.6 m across.</summary>
        public static void RazorWire(MeshBuilder mb, Vector3 a, Vector3 b, float diameter, float loopsPerMetre)
        {
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.Iron);
            var len = Vector3.Distance(a, b);
            var dir = (b - a) / Mathf.Max(1e-4f, len);
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var r = diameter * 0.5f;
            var steps = Mathf.Max(8, Mathf.RoundToInt(len * loopsPerMetre * 10f));
            Vector3 prev = default;
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var ang = t * len * loopsPerMetre * Mathf.PI * 2f;
                var p = a + dir * (len * t) + Vector3.up * (r + Mathf.Sin(ang) * r) + side * (Mathf.Cos(ang) * r);
                if (i > 0)
                {
                    var d = p - prev;
                    var n = Vector3.Cross(d, side).normalized * 0.012f;
                    mb.AddQuad(prev - n, prev + n, p + n, p - n);
                    mb.AddQuad(p - n, p + n, prev + n, prev - n);
                }
                prev = p;
            }
            mb.LodMask = keep;
        }

        /// <summary>Chain-link fence run: posts every 2.5 m, top rail and a dark see-through panel (mast fence).</summary>
        public static void ChainLinkFence(MeshBuilder mb, Vector3 a, Vector3 b, float height, string meshSlot)
        {
            mb.Slot(KitSlots.SteelGreen);
            var len = Vector3.Distance(a, b);
            var posts = Mathf.Max(1, Mathf.CeilToInt(len / 2.5f));
            for (var i = 0; i <= posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)posts);
                mb.AddTube(p, p + Vector3.up * height, 0.03f, 5);
            }
            mb.AddTube(a + Vector3.up * height, b + Vector3.up * height, 0.022f, 5);
            mb.Slot(meshSlot);
            mb.AddQuad(a, a + Vector3.up * height, b + Vector3.up * height, b);
            mb.AddQuad(b, b + Vector3.up * height, a + Vector3.up * height, a);
            var dir = (b - a) / Mathf.Max(1e-4f, len);
            mb.Push((a + b) * 0.5f + Vector3.up * height * 0.5f, Quaternion.LookRotation(dir, Vector3.up));
            mb.AddCollider(Vector3.zero, new Vector3(0.05f, height, len), SurfaceType.Metal);
            mb.Pop();
        }

        /// <summary>Rusting football goalposts (7.32 x 2.44 m) at <paramref name="center"/> facing along <paramref name="facing"/>.</summary>
        public static void Goalpost(MeshBuilder mb, Vector3 center, float facing)
        {
            var dir = MathX.DirectionFromBearing(facing);
            var side = Vector3.Cross(Vector3.up, dir);
            mb.Slot(KitSlots.SteelGreen);
            var l = center - side * 3.66f;
            var r = center + side * 3.66f;
            mb.AddTube(l, l + Vector3.up * 2.44f, 0.06f, 8);
            mb.AddTube(r, r + Vector3.up * 2.44f, 0.06f, 8);
            mb.AddTube(l + Vector3.up * 2.44f, r + Vector3.up * 2.44f, 0.06f, 8);
            mb.AddTube(l + Vector3.up * 2.44f, l - dir * 1.5f, 0.03f, 5);
            mb.AddTube(r + Vector3.up * 2.44f, r - dir * 1.5f, 0.03f, 5);
            mb.AddTube(l - dir * 1.5f, r - dir * 1.5f, 0.03f, 5);
            mb.AddCollider(l + Vector3.up * 1.22f, new Vector3(0.14f, 2.44f, 0.14f), SurfaceType.Metal);
            mb.AddCollider(r + Vector3.up * 1.22f, new Vector3(0.14f, 2.44f, 0.14f), SurfaceType.Metal);
        }

        /// <summary>
        /// The Pitch bleacher (GDD §6.5 A16: x 328–348, z 392–397, 1.2 m back wall): a concrete back wall on the south side,
        /// stepped timber benches facing north. Returns nothing; adds colliders (the back wall is full cover from the north
        /// when crouched behind it on the south side).
        /// </summary>
        public static void Bleacher(MeshBuilder mb, AreaRect r, float groundY)
        {
            var len = r.Width;
            var cx = (r.X0 + r.X1) * 0.5f;
            // Back wall along the south edge (z0), 1.2 m high, 0.25 m thick (the skirmish line crouches behind it).
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(new Vector3(cx, groundY + 0.6f, r.Z0 + 0.125f), new Vector3(len, 1.2f, 0.25f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3(cx, groundY + 0.6f, r.Z0 + 0.125f), new Vector3(len, 1.2f, 0.25f), SurfaceType.Concrete);
            // Four tiers stepping down towards the pitch (north).
            const int tiers = 4;
            var depth = (r.Depth - 0.4f) / tiers;
            for (var t = 0; t < tiers; t++)
            {
                var z1 = r.Z0 + 0.25f + depth * (t + 1);
                var z0 = z1 - depth;
                var h = 1.1f - t * 0.25f;
                mb.Slot(KitSlots.Concrete);
                mb.AddBox(new Vector3(cx, groundY + h * 0.5f, (z0 + z1) * 0.5f), new Vector3(len, h, depth), MeshBuilder.FacesNoBottom);
                mb.AddCollider(new Vector3(cx, groundY + h * 0.5f, (z0 + z1) * 0.5f), new Vector3(len, h, depth), SurfaceType.Concrete);
                mb.Slot(KitSlots.WoodDark);
                mb.AddBox(new Vector3(cx, groundY + h + 0.03f, (z0 + z1) * 0.5f), new Vector3(len - 0.2f, 0.06f, depth * 0.55f));
            }
        }

        /// <summary>Barrier boom: a post with a counterweight and a striped arm (raised or down).</summary>
        public static void BarrierBoom(MeshBuilder mb, Vector3 foot, float facing, float length, bool raised, string stripeSlotA, string stripeSlotB)
        {
            var dir = MathX.DirectionFromBearing(facing);
            mb.Slot(KitSlots.SteelFrame);
            mb.AddBox(foot + Vector3.up * 0.55f, new Vector3(0.3f, 1.1f, 0.3f), MeshBuilder.FacesNoBottom);
            var pivot = foot + Vector3.up * 1.0f;
            var armDir = raised ? (dir * 0.25f + Vector3.up).normalized : dir;
            mb.Slot(KitSlots.Iron);
            mb.AddBox(pivot - armDir * 0.6f, new Vector3(0.35f, 0.35f, 0.35f));
            var segs = Mathf.Max(2, Mathf.RoundToInt(length / 0.6f));
            for (var i = 0; i < segs; i++)
            {
                mb.Slot(i % 2 == 0 ? stripeSlotA : stripeSlotB);
                mb.AddBeam(pivot + armDir * (length * i / segs), pivot + armDir * (length * (i + 1) / segs), 0.09f, 0.09f);
            }
            mb.AddCollider(foot + Vector3.up * 0.55f, new Vector3(0.3f, 1.1f, 0.3f), SurfaceType.Metal);
        }

        /// <summary>
        /// The Weighbridge truck-scale plate (GDD §6.5 A4: 12 m plate, z 145–157) flush with the ground in a steel frame,
        /// with a raised edge kerb.
        /// </summary>
        public static void ScalePlate(MeshBuilder mb, Vector3 center, float length, float width)
        {
            mb.Slot(KitSlots.SteelPlate);
            mb.AddBox(center + Vector3.up * 0.02f, new Vector3(width, 0.04f, length), 4);
            mb.Slot(KitSlots.Iron);
            mb.AddBox(center + new Vector3(-width * 0.5f - 0.06f, 0.03f, 0f), new Vector3(0.12f, 0.06f, length + 0.24f), MeshBuilder.FacesNoBottom);
            mb.AddBox(center + new Vector3(width * 0.5f + 0.06f, 0.03f, 0f), new Vector3(0.12f, 0.06f, length + 0.24f), MeshBuilder.FacesNoBottom);
            mb.AddBox(center + new Vector3(0f, 0.03f, -length * 0.5f - 0.06f), new Vector3(width, 0.06f, 0.12f), MeshBuilder.FacesNoBottom);
            mb.AddBox(center + new Vector3(0f, 0.03f, length * 0.5f + 0.06f), new Vector3(width, 0.06f, 0.12f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(center + Vector3.down * 0.05f, new Vector3(width, 0.14f, length), SurfaceType.Metal);
        }
    }
}
