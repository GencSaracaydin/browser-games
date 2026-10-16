using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Street furniture (manifest PROC_LAMP_POSTS, PROC_SQUARE_PROPS, PROC_CLOCK_TOWER_FACES): sodium lamp brackets and
    /// posts (the head is returned so its marker and light sit on it), bollards, the dry octagonal fountain, clock faces
    /// stopped at 6:40, kerbs, door steps, shop signboards (blank boards for SignBaker) and wall-mounted meter boxes.
    /// </summary>
    public static class KitStreet
    {
        /// <summary>
        /// Cast-iron wall bracket with a cobra-head sodium lantern. <paramref name="wallPoint"/> is the fixing point on the
        /// facade at floor level; <paramref name="facing"/> points away from the wall. Returns the lens centre (light origin).
        /// </summary>
        public static Vector3 LampBracket(MeshBuilder mb, Vector3 wallPoint, float facing, float height, float outreach)
        {
            var dir = MathX.DirectionFromBearing(facing);
            var fix = wallPoint + Vector3.up * (height + 0.35f);
            mb.Slot(KitSlots.Iron);
            mb.Push(fix + dir * 0.02f, Quaternion.LookRotation(dir, Vector3.up));
            mb.AddBox(Vector3.zero, new Vector3(0.18f, 0.32f, 0.04f));
            mb.Pop();
            // Curved arm (quarter circle) and a stay.
            Vector3 prev = fix + dir * 0.04f;
            for (var i = 1; i <= 6; i++)
            {
                var a = i / 6f * Mathf.PI * 0.5f;
                var p = fix + dir * (0.04f + Mathf.Sin(a) * outreach) + Vector3.up * (Mathf.Cos(a) * 0.25f - 0.25f) * -1f;
                mb.AddTube(prev, p, 0.022f, 5);
                prev = p;
            }
            mb.AddTube(fix + Vector3.down * 0.45f, fix + dir * (outreach * 0.6f), 0.014f, 4);
            return LampHead(mb, prev, dir);
        }

        /// <summary>Tapered steel lamp post with an outreach arm; returns the lens centre.</summary>
        public static Vector3 LampPost(MeshBuilder mb, Vector3 foot, float facing, float height, float outreach)
        {
            var dir = MathX.DirectionFromBearing(facing);
            mb.Slot(KitSlots.SteelGreen);
            mb.AddFrustum(foot, 0.11f, 0.06f, height, 8, true, false);
            mb.AddFrustum(foot, 0.16f, 0.13f, 0.5f, 8, true, false);
            var top = foot + Vector3.up * height;
            var end = top + dir * outreach + Vector3.up * 0.25f;
            mb.AddTube(top + Vector3.down * 0.1f, end, 0.035f, 6);
            mb.AddCollider(foot + Vector3.up * height * 0.5f, new Vector3(0.22f, height, 0.22f), SurfaceType.Metal);
            return LampHead(mb, end, dir);
        }

        /// <summary>Cobra-head lantern housing with a lens underneath (lens slot driven by WP10 Look's sodium shader).</summary>
        public static Vector3 LampHead(MeshBuilder mb, Vector3 mount, Vector3 dir)
        {
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            mb.Slot(KitSlots.SteelGreen);
            mb.Push(mount, rot);
            mb.AddChamferBox(new Vector3(0f, -0.06f, 0.28f), new Vector3(0.34f, 0.14f, 0.62f), 0.06f, true, true);
            mb.Slot(KitSlots.SodiumLens);
            mb.AddBox(new Vector3(0f, -0.14f, 0.3f), new Vector3(0.26f, 0.03f, 0.44f), 8);
            mb.Pop();
            return mount + rot * new Vector3(0f, -0.17f, 0.3f);
        }

        /// <summary>Bollard (cast-iron post) with a cap.</summary>
        public static void Bollard(MeshBuilder mb, Vector3 foot)
        {
            mb.Slot(KitSlots.Iron);
            mb.AddFrustum(foot, 0.13f, 0.1f, 0.85f, 10, false, false);
            mb.AddFrustum(foot + Vector3.up * 0.85f, 0.12f, 0.02f, 0.12f, 10, true, false);
            mb.AddCollider(foot + Vector3.up * 0.48f, new Vector3(0.24f, 0.96f, 0.24f), SurfaceType.Metal);
        }

        /// <summary>
        /// The dry fountain of Clock Square (GDD §6.5 A7 (205, 0, 266)): an octagonal sandstone basin 0.7 m high (cover),
        /// a dust-filled bed, a central pier and an upper bowl.
        /// </summary>
        public static void Fountain(MeshBuilder mb, Vector3 center, float radius)
        {
            var outer = new Vector2[8];
            var inner = new Vector2[8];
            for (var i = 0; i < 8; i++)
            {
                var a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                outer[i] = new Vector2(center.x + Mathf.Cos(a) * radius, center.z + Mathf.Sin(a) * radius);
                inner[i] = new Vector2(center.x + Mathf.Cos(a) * (radius - 0.3f), center.z + Mathf.Sin(a) * (radius - 0.3f));
            }
            mb.Slot(KitSlots.Sandstone);
            // Basin wall: outer sides, top rim ring (as quads), inner sides.
            var y0 = center.y;
            var y1 = center.y + 0.7f;
            for (var i = 0; i < 8; i++)
            {
                var a = outer[i];
                var b = outer[(i + 1) % 8];
                var c = inner[i];
                var d = inner[(i + 1) % 8];
                mb.AddQuad(new Vector3(a.x, y0, a.y), new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), new Vector3(b.x, y0, b.y));
                mb.AddQuad(new Vector3(d.x, y0 + 0.15f, d.y), new Vector3(d.x, y1, d.y), new Vector3(c.x, y1, c.y), new Vector3(c.x, y0 + 0.15f, c.y));
                mb.AddQuad(new Vector3(a.x, y1, a.y), new Vector3(c.x, y1, c.y), new Vector3(d.x, y1, d.y), new Vector3(b.x, y1, b.y));
                var mid = (a + b) * 0.5f;
                var n = (mid - new Vector2(center.x, center.z)).normalized;
                var len = Vector2.Distance(a, b);
                var edge = new Vector3(b.x - a.x, 0f, b.y - a.y);
                mb.Push(new Vector3(mid.x - n.x * 0.15f, y0 + 0.35f, mid.y - n.y * 0.15f), Quaternion.LookRotation(edge.normalized, Vector3.up));
                mb.AddCollider(Vector3.zero, new Vector3(0.3f, 0.7f, len), SurfaceType.Stone);
                mb.Pop();
            }
            mb.Slot(KitSlots.Dirt);
            var bed = new Vector3[8];
            for (var i = 0; i < 8; i++) bed[i] = new Vector3(inner[7 - i].x, y0 + 0.15f, inner[7 - i].y);
            mb.AddConvexPolygon(bed);
            mb.Slot(KitSlots.Sandstone);
            mb.AddCylinder(center + Vector3.up * 0.15f, 0.32f, 1.6f, 12, false, false);
            mb.AddFrustum(center + Vector3.up * 1.75f, 0.25f, 0.95f, 0.28f, 16, true, true);
            mb.AddCylinder(center + Vector3.up * 2.03f, 0.12f, 0.55f, 8, true, false);
            mb.AddCollider(center + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f), SurfaceType.Stone);
        }

        /// <summary>
        /// A clock face on a wall frame centred at (u, v): enamel disc, twelve hour batons and iron hands stopped at
        /// 6:40 (GDD §2.2: "stopped at 6:40 years ago"; hour hand at 200°, minute hand at 240° from twelve).
        /// </summary>
        public static void ClockFace(MeshBuilder mb, WallFrame f, float u, float v, float radius, float proud)
        {
            mb.Push(f.Matrix);
            var z = -proud;
            mb.Slot(KitSlots.ClockFace);
            const int segs = 24;
            var rim = new Vector3[segs];
            for (var i = 0; i < segs; i++)
            {
                // Clockwise as seen from outside (local −z): angle decreasing in local xy with x = along the wall.
                var a = -i / (float)segs * Mathf.PI * 2f;
                rim[i] = new Vector3(u + Mathf.Cos(a) * radius, v + Mathf.Sin(a) * radius, z);
            }
            mb.AddConvexPolygon(rim);
            mb.Slot(KitSlots.Iron);
            for (var i = 0; i < segs; i++)
            {
                var a0 = -i / (float)segs * Mathf.PI * 2f;
                var a1 = -(i + 1) / (float)segs * Mathf.PI * 2f;
                var p0 = new Vector3(u + Mathf.Cos(a0) * radius, v + Mathf.Sin(a0) * radius, z);
                var p1 = new Vector3(u + Mathf.Cos(a1) * radius, v + Mathf.Sin(a1) * radius, z);
                mb.AddTube(p0, p1, 0.035f * radius, 4);
            }
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            for (var h = 0; h < 12; h++)
            {
                var ang = (90f - h * 30f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                var c = new Vector3(u, v, z - 0.005f) + d * radius * 0.82f;
                mb.AddBeam(c - d * radius * 0.08f, c + d * radius * 0.08f, radius * (h % 3 == 0 ? 0.06f : 0.035f), 0.01f);
            }
            mb.LodMask = keep;
            // Hands: hour at 6:40 → 200° clockwise from twelve; minute at 40 min → 240°.
            Hand(mb, u, v, z - 0.02f, 200f, radius * 0.5f, radius * 0.07f);
            Hand(mb, u, v, z - 0.03f, 240f, radius * 0.78f, radius * 0.045f);
            mb.Pop();
        }

        private static void Hand(MeshBuilder mb, float u, float v, float z, float clockDegrees, float length, float width)
        {
            var ang = (90f - clockDegrees) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            var c = new Vector3(u, v, z);
            mb.AddBeam(c - d * length * 0.15f, c + d * length, width, 0.012f);
        }

        /// <summary>A kerb run (stone edge) between two ground points, raised 0.15 m on the left side of a→b.</summary>
        public static void Kerb(MeshBuilder mb, Vector3 a, Vector3 b, string slot)
        {
            mb.Slot(slot);
            var dir = b - a;
            var len = dir.magnitude;
            if (len < 0.05f) return;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            mb.Push((a + b) * 0.5f, rot);
            mb.AddChamferBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.22f, 0.24f, len), 0.02f, true, false);
            mb.Pop();
        }

        /// <summary>Two-step stone door step in front of an entrance (frame coordinates).</summary>
        public static void DoorStep(MeshBuilder mb, WallFrame f, float u0, float u1, float height, string slot)
        {
            mb.Slot(slot);
            var steps = Mathf.Max(1, Mathf.RoundToInt(height / 0.16f));
            for (var i = 0; i < steps; i++)
            {
                var h = height * (steps - i) / steps;
                KitTrims.FrameBox(mb, f, u0 - 0.1f * (steps - i), u1 + 0.1f * (steps - i), 0f, h, -0.05f, 0.32f * (steps - i), KitTrims.NoBack & ~8);
            }
        }

        /// <summary>Blank shop signboard on a facade (SignBaker fills it with Sardani glyph text later).</summary>
        public static void SignBoard(MeshBuilder mb, WallFrame f, float u0, float u1, float v, float height, string slot)
        {
            mb.Slot(slot);
            KitTrims.FrameBox(mb, f, u0, u1, v, v + height, 0f, 0.05f, KitTrims.NoBack);
        }

        /// <summary>Electricity meter box and conduit (1984 street clutter).</summary>
        public static void MeterBox(MeshBuilder mb, WallFrame f, float u, float v)
        {
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.SteelFrame);
            KitTrims.FrameBox(mb, f, u - 0.18f, u + 0.18f, v, v + 0.45f, 0f, 0.14f, KitTrims.NoBack);
            mb.Slot(KitSlots.Cable);
            mb.AddTube(f.P(u, v + 0.45f, 0.06f), f.P(u, v + 2.6f, 0.06f), 0.02f, 4);
            mb.LodMask = keep;
        }
    }
}
