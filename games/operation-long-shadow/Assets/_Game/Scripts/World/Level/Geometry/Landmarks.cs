using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// The named civic structures of Zaroud that are not generic houses (GDD §6.6): the Weighbridge customs arch (BLD-02),
    /// the market arch into Clock Square, the clock tower (BLD-05, square, flat-topped, four faces at 6:40 — never a
    /// religious silhouette), free-standing walls (mudbrick garden and irrigation walls, the square's west wall, the
    /// compound wall of BLD-09 with razor wire). World coordinates; each writes visuals and colliders.
    /// </summary>
    public static class Landmarks
    {
        /// <summary>
        /// The Weighbridge Gate (GDD §6.5 A4: arch x 176–191, z 160–170; Gate.Weighbridge (183, 0, 163)): two massive
        /// sandstone piers with a round-arched passage 6 m wide and 6.4 m high, a 10 m deep vaulted gate passage, a cornice
        /// and a flat roof at +9. Returns the passage opening's centre at the gate line.
        /// </summary>
        public static Vector3 WeighbridgeArch(MeshBuilder mb, float x0, float x1, float z0, float z1, float passageX0, float passageX1)
        {
            const float height = 9f;
            const float apex = 6.4f;
            var w = passageX1 - passageX0;
            // Outer block faces (south and north) with the arch, east/west sides plain.
            var south = new WallFrame(new Vector3(x0, 0f, z0), Vector3.right, x1 - x0);
            var north = new WallFrame(new Vector3(x1, 0f, z1), Vector3.left, x1 - x0, 40f);
            var east = new WallFrame(new Vector3(x1, 0f, z0), Vector3.forward, z1 - z0, 20f);
            var west = new WallFrame(new Vector3(x0, 0f, z1), Vector3.back, z1 - z0, 60f);
            var archS = WallOpening.Make(passageX0 - x0, passageX1 - x0, 0f, apex, OpeningShape.RoundArch, z1 - z0, false, OpeningFill.None, 0, true);
            var archN = WallOpening.Make(x1 - passageX1, x1 - passageX0, 0f, apex, OpeningShape.RoundArch, 0.05f, false, OpeningFill.None, 0, true);
            WallBuilder.Face(mb, south, 0f, height, new[] { archS }, KitSlots.Sandstone, KitSlots.Sandstone, null);
            WallBuilder.Face(mb, north, 0f, height, new[] { archN }, KitSlots.Sandstone, KitSlots.Sandstone, null);
            WallBuilder.Face(mb, east, 0f, height, null, KitSlots.Sandstone, KitSlots.Sandstone, null);
            WallBuilder.Face(mb, west, 0f, height, null, KitSlots.Sandstone, KitSlots.Sandstone, null);
            KitTrims.Surround(mb, south, archS, 0.45f, 0.12f, KitSlots.SandstoneTrim, true);
            KitTrims.Surround(mb, north, archN, 0.45f, 0.12f, KitSlots.SandstoneTrim, true);
            foreach (var f in new[] { south, north, east, west })
            {
                KitTrims.Plinth(mb, f, 0.9f, 0.08f, KitSlots.SandstoneTrim);
                KitTrims.StringCourse(mb, f, 7.2f, 0.22f, 0.1f, KitSlots.SandstoneTrim);
                KitTrims.Cornice(mb, f, height - 0.35f, KitSlots.SandstoneTrim, 1.4f);
            }
            // Pilasters flanking the arch on the south (approach) face.
            KitTrims.Pilaster(mb, south, archS.U0 - 1.0f, 0.7f, 0.9f, 7.2f, 0.18f, KitSlots.SandstoneTrim);
            KitTrims.Pilaster(mb, south, archS.U1 + 1.0f, 0.7f, 0.9f, 7.2f, 0.18f, KitSlots.SandstoneTrim);
            // Roof and a low parapet.
            mb.Slot(KitSlots.ConcreteFloor);
            mb.AddQuad(new Vector3(x0, height, z0), new Vector3(x0, height, z1), new Vector3(x1, height, z1), new Vector3(x1, height, z0));
            KitTrims.Coping(mb, x0, z0, x1, z1, height, 0.4f, 0.06f, 0.12f, KitSlots.SandstoneTrim);
            // Colliders: two piers and the mass above the passage.
            var spring = apex - w * 0.5f;
            mb.AddCollider(new Vector3((x0 + passageX0) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(passageX0 - x0, height, z1 - z0), SurfaceType.Stone);
            mb.AddCollider(new Vector3((passageX1 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - passageX1, height, z1 - z0), SurfaceType.Stone);
            mb.AddCollider(new Vector3((passageX0 + passageX1) * 0.5f, (apex + height) * 0.5f, (z0 + z1) * 0.5f), new Vector3(w, height - apex, z1 - z0), SurfaceType.Stone);
            // The vault haunches (between springline and apex) as two collider slabs per side.
            mb.AddCollider(new Vector3(passageX0 + 0.6f, (spring + apex) * 0.5f, (z0 + z1) * 0.5f), new Vector3(1.2f, apex - spring, z1 - z0), SurfaceType.Stone);
            mb.AddCollider(new Vector3(passageX1 - 0.6f, (spring + apex) * 0.5f, (z0 + z1) * 0.5f), new Vector3(1.2f, apex - spring, z1 - z0), SurfaceType.Stone);
            // Passage floor (worn stone).
            mb.Slot(KitSlots.Paving);
            mb.AddQuad(new Vector3(passageX0, 0.02f, z0), new Vector3(passageX0, 0.02f, z1), new Vector3(passageX1, 0.02f, z1), new Vector3(passageX1, 0.02f, z0));
            return new Vector3((passageX0 + passageX1) * 0.5f, 0f, z0 + 3f);
        }

        /// <summary>
        /// The market arch into Clock Square (B3.ArchTarget (185, 4, 252)): a two-storey gate building over the street
        /// x <paramref name="x0"/>–<paramref name="x1"/> between z <paramref name="z0"/> and <paramref name="z1"/>, with a round
        /// arch 6 m wide and 5.6 m high.
        /// </summary>
        public static void MarketArch(MeshBuilder mb, float x0, float x1, float z0, float z1, float passageX0, float passageX1, int tint)
        {
            const float height = 8.4f;
            const float apex = 5.6f;
            var south = new WallFrame(new Vector3(x0, 0f, z0), Vector3.right, x1 - x0);
            var north = new WallFrame(new Vector3(x1, 0f, z1), Vector3.left, x1 - x0, 30f);
            var archS = WallOpening.Make(passageX0 - x0, passageX1 - x0, 0f, apex, OpeningShape.RoundArch, z1 - z0, false, OpeningFill.None, 0, true);
            var archN = WallOpening.Make(x1 - passageX1, x1 - passageX0, 0f, apex, OpeningShape.RoundArch, 0.05f, false, OpeningFill.None, 0, true);
            var plaster = KitSlots.Plaster(tint);
            WallBuilder.Face(mb, south, 0f, height, new[] { archS }, plaster, KitSlots.SandstoneTrim, null);
            WallBuilder.Face(mb, north, 0f, height, new[] { archN }, plaster, KitSlots.SandstoneTrim, null);
            KitTrims.Surround(mb, south, archS, 0.38f, 0.1f, KitSlots.SandstoneTrim, true);
            KitTrims.Surround(mb, north, archN, 0.38f, 0.1f, KitSlots.SandstoneTrim, true);
            // Upper-storey windows over the arch (a customs office), shuttered.
            var winS = WallOpening.Make(archS.CenterU - 0.55f, archS.CenterU + 0.55f, 6.4f, 7.8f, OpeningShape.Rect, 0.35f, true, OpeningFill.ShutteredWindow, 41);
            KitFills.Fill(mb, south, winS);
            foreach (var f in new[] { south, north })
            {
                KitTrims.Plinth(mb, f, 0.6f, 0.05f, KitSlots.Sandstone);
                KitTrims.StringCourse(mb, f, 6.0f, 0.16f, 0.06f, KitSlots.SandstoneTrim);
                KitTrims.Cornice(mb, f, height - 0.3f, KitSlots.SandstoneTrim);
            }
            mb.Slot(KitSlots.RoofDeck);
            mb.AddQuad(new Vector3(x0, height, z0), new Vector3(x0, height, z1), new Vector3(x1, height, z1), new Vector3(x1, height, z0));
            KitTrims.Coping(mb, x0, z0, x1, z1, height, 0.3f, 0.04f, 0.1f, KitSlots.PlasterRough);
            mb.AddCollider(new Vector3((x0 + passageX0) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(passageX0 - x0, height, z1 - z0), SurfaceType.Plaster);
            mb.AddCollider(new Vector3((passageX1 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - passageX1, height, z1 - z0), SurfaceType.Plaster);
            mb.AddCollider(new Vector3((passageX0 + passageX1) * 0.5f, (apex + height) * 0.5f, (z0 + z1) * 0.5f), new Vector3(passageX1 - passageX0, height - apex, z1 - z0), SurfaceType.Plaster);
            mb.Slot(KitSlots.Paving);
            mb.AddQuad(new Vector3(passageX0, 0.02f, z0), new Vector3(passageX0, 0.02f, z1), new Vector3(passageX1, 0.02f, z1), new Vector3(passageX1, 0.02f, z0));
        }

        /// <summary>
        /// The clock tower (GDD §6.6 BLD-05: 6 x 6 m, 22 m, sandstone, four clock faces at 6:40; PA horn 2 on top): a
        /// rusticated shaft with a door, slit windows, a projecting clock stage with a face on every side, a corbelled cornice
        /// and a flat top with a parapet. Square and flat-topped: a civic silhouette, never a minaret or a dome.
        /// </summary>
        public static void ClockTower(MeshBuilder mb, Vector3 foot, float size, float height)
        {
            var h = size * 0.5f;
            var frames = WallFrame.Perimeter(foot.x - h, foot.z - h, foot.x + h, foot.z + h, foot.y);
            var clockStage = height - 5.6f;
            for (var side = 0; side < 4; side++)
            {
                var f = frames[side];
                var openings = new List<WallOpening>();
                if (side == 0) openings.Add(WallOpening.Make(h - 0.6f, h + 0.6f, 0f, 2.6f, OpeningShape.RoundArch, 0.5f, true, OpeningFill.Door, 5, true));
                for (var v = 5f; v < clockStage - 2f; v += 4.2f)
                    openings.Add(WallOpening.Make(h - 0.18f, h + 0.18f, v, v + 1.4f, OpeningShape.Rect, 0.5f, true, OpeningFill.None, 0));
                WallBuilder.Face(mb, f, 0f, height, openings, KitSlots.Sandstone, KitSlots.Sandstone, KitSlots.InteriorCard);
                foreach (var o in openings) KitFills.Fill(mb, f, o);
                KitTrims.Plinth(mb, f, 1.1f, 0.1f, KitSlots.SandstoneTrim);
                // Corner quoins (rusticated pilasters).
                KitTrims.Pilaster(mb, f, 0.35f, 0.7f, 1.1f, clockStage - 0.4f, 0.06f, KitSlots.SandstoneTrim);
                KitTrims.Pilaster(mb, f, size - 0.35f, 0.7f, 1.1f, clockStage - 0.4f, 0.06f, KitSlots.SandstoneTrim);
                KitTrims.StringCourse(mb, f, clockStage - 0.4f, 0.3f, 0.16f, KitSlots.SandstoneTrim);
                // Clock stage: a projecting band with the face.
                KitTrims.FrameBox(mb, f, -0.15f, size + 0.15f, clockStage, clockStage + 4.0f, 0f, 0.15f, KitTrims.NoBack);
                KitStreet.ClockFace(mb, f, h, clockStage + 2.0f, 1.25f, 0.2f);
                KitTrims.Cornice(mb, f, height - 1.2f, KitSlots.SandstoneTrim, 1.6f);
            }
            var top = foot.y + height;
            mb.Slot(KitSlots.ConcreteFloor);
            mb.AddQuad(new Vector3(foot.x - h, top, foot.z - h), new Vector3(foot.x - h, top, foot.z + h), new Vector3(foot.x + h, top, foot.z + h), new Vector3(foot.x + h, top, foot.z - h));
            // Parapet with corner blocks.
            for (var side = 0; side < 4; side++)
            {
                var f = frames[side];
                KitTrims.FrameBox(mb, f, -0.2f, size + 0.2f, height, height + 0.9f, -0.3f, 0.2f, 0x3F);
                KitTrims.FrameBox(mb, f, -0.3f, 0.6f, height, height + 1.3f, -0.4f, 0.3f, 0x3F);
            }
            mb.AddCollider(new Vector3(foot.x, foot.y + height * 0.5f, foot.z), new Vector3(size, height, size), SurfaceType.Stone);
        }

        /// <summary>
        /// A free-standing wall from <paramref name="a"/> to <paramref name="b"/> (ground points) with a coping: mudbrick
        /// garden and irrigation walls (GDD §6.5 A3: 1.2 m, cover for node set A), the square's west wall, yard walls.
        /// Both faces, end caps, collider.
        /// </summary>
        public static void FreeWall(MeshBuilder mb, Vector3 a, Vector3 b, float height, float thickness, string faceSlot, string copingSlot, SurfaceType surface,
                                    bool endCaps = true)
        {
            var dir = b - a;
            dir.y = 0f;
            var len = dir.magnitude;
            if (len < 0.05f) return;
            dir /= len;
            var n = Vector3.Cross(Vector3.up, dir);
            var front = new WallFrame(a + n * (thickness * 0.5f), dir, len);
            var back = new WallFrame(b - n * (thickness * 0.5f), -dir, len, 13f);
            WallBuilder.Face(mb, front, 0f, height, null, faceSlot, faceSlot, null);
            WallBuilder.Face(mb, back, 0f, height, null, faceSlot, faceSlot, null);
            if (endCaps)
            {
                KitTrims.EndCap(mb, front, 0f, true, 0f, height, thickness, faceSlot);
                KitTrims.EndCap(mb, front, len, false, 0f, height, thickness, faceSlot);
            }
            mb.Slot(copingSlot);
            mb.Push((a + b) * 0.5f + Vector3.up * (height + 0.04f), Quaternion.LookRotation(dir, Vector3.up));
            mb.AddChamferBox(Vector3.zero, new Vector3(thickness + 0.08f, 0.08f, len + 0.06f), 0.02f, true, true);
            mb.AddCollider(Vector3.down * (height * 0.5f), new Vector3(thickness, height + 0.1f, len), surface);
            mb.Pop();
        }

        /// <summary>
        /// A run of the Relay Station compound wall (GDD §6.6 BLD-09: concrete, 4 m, razor wire) between two corners, with
        /// pilasters every 6 m on the outside, a coping and razor wire on top. <paramref name="razor"/> false for runs nobody
        /// sees close up.
        /// </summary>
        public static void CompoundWall(MeshBuilder mb, Vector3 a, Vector3 b, bool outsideOnRight, bool razor)
        {
            const float height = 4f;
            const float thickness = 0.35f;
            FreeWall(mb, a, b, height, thickness, KitSlots.Concrete, KitSlots.Concrete, SurfaceType.Concrete);
            var dir = (b - a).normalized;
            var n = Vector3.Cross(Vector3.up, dir) * (outsideOnRight ? 1f : -1f);
            var len = Vector3.Distance(a, b);
            mb.Slot(KitSlots.Concrete);
            for (var d = 3f; d < len - 1f; d += 6f)
            {
                var p = a + dir * d + n * (thickness * 0.5f + 0.12f);
                mb.Push(p + Vector3.up * (height * 0.5f), Quaternion.LookRotation(dir, Vector3.up));
                mb.AddBox(Vector3.zero, new Vector3(0.24f, height, 0.45f), 0x3F & ~8);
                mb.Pop();
            }
            if (!razor) return;
            // Y-brackets and two concertina coils on top.
            mb.Slot(KitSlots.Iron);
            for (var d = 1.5f; d < len; d += 3f)
            {
                var p = a + dir * d + Vector3.up * height;
                mb.AddTube(p, p + Vector3.up * 0.55f + n * 0.3f, 0.015f, 4);
                mb.AddTube(p, p + Vector3.up * 0.55f - n * 0.3f, 0.015f, 4);
            }
            KitIndustrial.RazorWire(mb, a + Vector3.up * (height + 0.05f), b + Vector3.up * (height + 0.05f), 0.55f, 4f);
        }
    }
}
