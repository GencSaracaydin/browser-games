using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Wall trims of the Sardahn vernacular and colonial kit (TECH_PLAN §7.15): plinths, string courses, cornices,
    /// parapet copings, timber lintels, sills, stone surrounds and archivolts with keystones, pilasters, drain spouts and
    /// Art Deco fins and stepped crowns. All take a <see cref="WallFrame"/> and frame coordinates (u along, v up, w out).
    /// Small pieces are written as LOD0-only detail.
    /// </summary>
    public static class KitTrims
    {
        /// <summary>Box in the wall frame (u0..u1 along, v0..v1 up, w0..w1 out of the wall surface).</summary>
        public static void FrameBox(MeshBuilder mb, WallFrame f, float u0, float u1, float v0, float v1, float w0, float w1, int faces = 0x3F)
        {
            if (u1 <= u0 || v1 <= v0 || w1 <= w0) return;
            mb.Push(f.Matrix);
            mb.AddBox(new Vector3((u0 + u1) * 0.5f, (v0 + v1) * 0.5f, -(w0 + w1) * 0.5f), new Vector3(u1 - u0, v1 - v0, w1 - w0), faces);
            mb.Pop();
        }

        /// <summary>Faces of a frame box except the one against the wall (local +Z).</summary>
        public const int NoBack = 0x3F & ~16;

        /// <summary>Base course: a slightly proud band at the foot of a wall (splash zone).</summary>
        public static void Plinth(MeshBuilder mb, WallFrame f, float height, float proud, string slot, float u0 = 0f, float u1 = -1f)
        {
            if (u1 < 0f) u1 = f.Length;
            mb.Slot(slot);
            FrameBox(mb, f, u0, u1, 0f, height, 0f, proud, NoBack & ~8);
        }

        /// <summary>A horizontal band (string course at a floor line, or a sill course).</summary>
        public static void StringCourse(MeshBuilder mb, WallFrame f, float v, float height, float proud, string slot, float u0 = 0f, float u1 = -1f)
        {
            if (u1 < 0f) u1 = f.Length;
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodAll;
            FrameBox(mb, f, u0, u1, v, v + height, 0f, proud, NoBack);
            mb.LodMask = keep;
        }

        /// <summary>Stepped cornice under a parapet (three corbelled layers).</summary>
        public static void Cornice(MeshBuilder mb, WallFrame f, float v, string slot, float scale = 1f, float u0 = 0f, float u1 = -1f)
        {
            if (u1 < 0f) u1 = f.Length;
            mb.Slot(slot);
            FrameBox(mb, f, u0, u1, v, v + 0.08f * scale, 0f, 0.05f * scale, NoBack);
            FrameBox(mb, f, u0 - 0.02f, u1 + 0.02f, v + 0.08f * scale, v + 0.16f * scale, 0f, 0.11f * scale, NoBack);
            FrameBox(mb, f, u0 - 0.04f, u1 + 0.04f, v + 0.16f * scale, v + 0.26f * scale, 0f, 0.18f * scale, NoBack);
        }

        /// <summary>
        /// Parapet coping ring on a rectangular roof edge: a slab slightly wider than the parapet with a small drip
        /// overhang, chamfered top edges for plaster.
        /// </summary>
        public static void Coping(MeshBuilder mb, float x0, float z0, float x1, float z1, float y, float parapetThickness, float overhang, float height, string slot)
        {
            mb.Slot(slot);
            var t = parapetThickness + overhang * 2f;
            var h = height;
            var yc = y + h * 0.5f;
            mb.AddChamferBox(new Vector3((x0 + x1) * 0.5f, yc, z0 + parapetThickness * 0.5f), new Vector3(x1 - x0 + overhang * 2f, h, t), 0.015f, true, true);
            mb.AddChamferBox(new Vector3((x0 + x1) * 0.5f, yc, z1 - parapetThickness * 0.5f), new Vector3(x1 - x0 + overhang * 2f, h, t), 0.015f, true, true);
            mb.AddChamferBox(new Vector3(x0 + parapetThickness * 0.5f, yc, (z0 + z1) * 0.5f), new Vector3(t, h, z1 - z0 - parapetThickness * 2f), 0.015f, true, true);
            mb.AddChamferBox(new Vector3(x1 - parapetThickness * 0.5f, yc, (z0 + z1) * 0.5f), new Vector3(t, h, z1 - z0 - parapetThickness * 2f), 0.015f, true, true);
        }

        /// <summary>Exposed timber lintel over a mudbrick opening (extends past both jambs).</summary>
        public static void TimberLintel(MeshBuilder mb, WallFrame f, WallOpening o, string slot)
        {
            mb.Slot(slot);
            FrameBox(mb, f, o.U0 - 0.18f, o.U1 + 0.18f, o.V1, o.V1 + 0.14f, -0.02f, 0.02f, NoBack);
        }

        /// <summary>Window sill: a proud slab under the opening, wider than it.</summary>
        public static void Sill(MeshBuilder mb, WallFrame f, WallOpening o, string slot, float proud = 0.07f, float thick = 0.06f)
        {
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodAll;
            FrameBox(mb, f, o.U0 - 0.06f, o.U1 + 0.06f, o.V0 - thick, o.V0 + 0.005f, -0.03f, proud, NoBack);
            mb.LodMask = keep;
        }

        /// <summary>
        /// Stone/plaster surround: flat bands round a rectangular opening, or an archivolt with a keystone round an arch
        /// (colonial civic buildings, the market arch, Clock Square arcades).
        /// </summary>
        public static void Surround(MeshBuilder mb, WallFrame f, WallOpening o, float band, float proud, string slot, bool keystone)
        {
            mb.Slot(slot);
            var spring = o.Shape == OpeningShape.Rect ? o.V1 : o.Springline;
            FrameBox(mb, f, o.U0 - band, o.U0, o.V0, spring, 0f, proud, NoBack);
            FrameBox(mb, f, o.U1, o.U1 + band, o.V0, spring, 0f, proud, NoBack);
            if (o.Shape == OpeningShape.Rect)
            {
                FrameBox(mb, f, o.U0 - band, o.U1 + band, o.V1, o.V1 + band, 0f, proud, NoBack);
                if (keystone) FrameBox(mb, f, o.CenterU - band * 0.4f, o.CenterU + band * 0.4f, o.V1 - 0.02f, o.V1 + band * 1.35f, 0f, proud + 0.03f, NoBack);
                return;
            }
            // Archivolt: a ring of segments following the arch, band wide.
            var inner = WallBuilder.ArchPoints(o);
            var cx = o.CenterU;
            var cy = o.Shape == OpeningShape.RoundArch ? spring : o.V1 - (o.Width * o.Width * 0.25f + (o.V1 - spring) * (o.V1 - spring)) / (2f * Mathf.Max(1e-3f, o.V1 - spring));
            mb.Push(f.Matrix);
            for (var i = 0; i < inner.Length - 1; i++)
            {
                var a = inner[i];
                var b = inner[i + 1];
                var na = (a - new Vector2(cx, cy)).normalized;
                var nb = (b - new Vector2(cx, cy)).normalized;
                var ao = a + na * band;
                var bo = b + nb * band;
                // Front face (local −z is outward).
                var z = -proud;
                mb.AddQuad(new Vector3(a.x, a.y, z), new Vector3(ao.x, ao.y, z), new Vector3(bo.x, bo.y, z), new Vector3(b.x, b.y, z));
                // Outer rim.
                mb.AddQuad(new Vector3(ao.x, ao.y, z), new Vector3(ao.x, ao.y, 0f), new Vector3(bo.x, bo.y, 0f), new Vector3(bo.x, bo.y, z));
                // Inner rim (soffit edge).
                mb.AddQuad(new Vector3(b.x, b.y, z), new Vector3(b.x, b.y, 0f), new Vector3(a.x, a.y, 0f), new Vector3(a.x, a.y, z));
            }
            mb.Pop();
            if (keystone)
            {
                var apex = new Vector2(cx, o.V1);
                FrameBox(mb, f, apex.x - band * 0.45f, apex.x + band * 0.45f, apex.y - 0.05f, apex.y + band * 1.25f, 0f, proud + 0.035f, NoBack);
            }
        }

        /// <summary>Pilaster: a shallow vertical pier on a facade.</summary>
        public static void Pilaster(MeshBuilder mb, WallFrame f, float uCenter, float width, float v0, float v1, float proud, string slot)
        {
            mb.Slot(slot);
            FrameBox(mb, f, uCenter - width * 0.5f, uCenter + width * 0.5f, v0, v1, 0f, proud, NoBack);
            // Capital and base.
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            FrameBox(mb, f, uCenter - width * 0.5f - 0.04f, uCenter + width * 0.5f + 0.04f, v1 - 0.12f, v1, 0f, proud + 0.04f, NoBack);
            FrameBox(mb, f, uCenter - width * 0.5f - 0.03f, uCenter + width * 0.5f + 0.03f, v0, v0 + 0.18f, 0f, proud + 0.03f, NoBack);
            mb.LodMask = keep;
        }

        /// <summary>Roof drain spout (the clay or timber mizab of flat earthen roofs), throwing water clear of the wall.</summary>
        public static void Spout(MeshBuilder mb, WallFrame f, float u, float v, float length, string slot)
        {
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            FrameBox(mb, f, u - 0.07f, u - 0.05f, v, v + 0.1f, -0.05f, length, NoBack);
            FrameBox(mb, f, u + 0.05f, u + 0.07f, v, v + 0.1f, -0.05f, length, NoBack);
            FrameBox(mb, f, u - 0.07f, u + 0.07f, v - 0.02f, v, -0.05f, length, NoBack);
            mb.LodMask = keep;
        }

        /// <summary>Art Deco vertical fins (Relay Hall piers between window bays).</summary>
        public static void DecoFin(MeshBuilder mb, WallFrame f, float u, float v0, float v1, float proud, float width, string slot)
        {
            mb.Slot(slot);
            FrameBox(mb, f, u - width * 0.5f, u + width * 0.5f, v0, v1, 0f, proud, NoBack);
            FrameBox(mb, f, u - width * 0.25f, u + width * 0.25f, v1, v1 + 0.6f, 0f, proud * 0.7f, NoBack);
        }

        /// <summary>
        /// Art Deco stepped crown over a facade centre (BLD-10 "stepped parapet"): <paramref name="steps"/> tiers, each
        /// narrower and higher, with horizontal "speed line" grooves.
        /// </summary>
        public static void DecoStepped(MeshBuilder mb, WallFrame f, float uCenter, float baseWidth, float v, int steps, float stepHeight, float depth, string slot, string lineSlot)
        {
            for (var i = 0; i < steps; i++)
            {
                var w = baseWidth * (1f - i / (float)(steps + 1));
                var v0 = v + i * stepHeight;
                mb.Slot(slot);
                FrameBox(mb, f, uCenter - w * 0.5f, uCenter + w * 0.5f, v0, v0 + stepHeight, -depth, 0.08f + 0.04f * i, 0x3F);
                var keep = mb.LodMask;
                mb.LodMask = MeshBuilder.LodDetail;
                mb.Slot(lineSlot);
                FrameBox(mb, f, uCenter - w * 0.5f + 0.1f, uCenter + w * 0.5f - 0.1f, v0 + stepHeight * 0.45f, v0 + stepHeight * 0.55f, 0.08f + 0.04f * i, 0.11f + 0.04f * i, NoBack);
                mb.LodMask = keep;
            }
        }

        /// <summary>Speed lines: three thin horizontal grooves/bands across a facade (Art Deco).</summary>
        public static void SpeedLines(MeshBuilder mb, WallFrame f, float v, float u0, float u1, string slot)
        {
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            for (var i = 0; i < 3; i++) FrameBox(mb, f, u0, u1, v + i * 0.16f, v + i * 0.16f + 0.05f, 0f, 0.035f, NoBack);
            mb.LodMask = keep;
        }

        /// <summary>Wall end cap: the exposed thickness of a free wall end (ruins, parapet ends, garden walls).</summary>
        public static void EndCap(MeshBuilder mb, WallFrame f, float u, bool atStart, float v0, float v1, float thickness, string slot)
        {
            mb.Slot(slot);
            if (atStart)
                mb.AddQuad(f.P(u, v0, -thickness), f.P(u, v1, -thickness), f.P(u, v1, 0f), f.P(u, v0, 0f));
            else
                mb.AddQuad(f.P(u, v0, 0f), f.P(u, v1, 0f), f.P(u, v1, -thickness), f.P(u, v0, -thickness));
        }
    }
}
