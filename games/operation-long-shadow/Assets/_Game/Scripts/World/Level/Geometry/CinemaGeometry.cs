using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Cinema Zenith (GDD §6.5 A8, §6.6 BLD-07, §7.4): a faded 1950s picture house, 45 x 30 m, 12 m to the parapet top so
    /// R4 (+12) keeps the last sun at 1.5° (GDD §8.5). Lobby x 186–198, z 302–312 (terrazzo) with the side door at
    /// (186, 0, 307) in the west facade; the closed concession hall x 198–231 under the upper foyer (+4.5) that connects the
    /// balcony (+4.5, z 312–318) to the marquee door (214, 4.5, 301); the auditorium x 186–231, z 312–330 with seats, the
    /// screen on the north wall, shrapnel holes in the roof for dust shafts and the balcony stair along the east wall; the
    /// marquee walkway on the canopy (x 200–226, z 299–301 at +4.5) and the crate stack at (227, 0, 298), top +2.2.
    /// <para>Writes the exterior into <c>shell</c>, the interior into <c>interior</c> and leaves anchors for doors, signs,
    /// shafts, lights and seats.</para>
    /// </summary>
    public static class CinemaGeometry
    {
        /// <summary>Footprint (GDD §6.5 A8, south facade on the square at z 301).</summary>
        public static readonly AreaRect Footprint = new AreaRect(186f, 301f, 231f, 330f);
        /// <summary>Exterior wall thickness.</summary>
        public const float Wall = 0.4f;
        /// <summary>Roof deck height.</summary>
        public const float RoofY = 11.2f;
        /// <summary>Parapet top (≤ 12 m: the R4 sun line, GDD §8.5).</summary>
        public const float TopY = 12.0f;
        /// <summary>Balcony / upper foyer floor.</summary>
        public const float UpperY = 4.5f;
        /// <summary>Lobby x range.</summary>
        public const float LobbyX1 = 198f;
        /// <summary>Auditorium south edge (lobby/concession north wall).</summary>
        public const float AuditoriumZ0 = 312f;
        /// <summary>Balcony front (north edge).</summary>
        public const float BalconyZ1 = 318.2f;
        /// <summary>Side door span on the west wall (z).</summary>
        public static readonly Vector2 SideDoorZ = new Vector2(306.4f, 307.6f);
        /// <summary>Marquee door span on the south wall (x).</summary>
        public static readonly Vector2 MarqueeDoorX = new Vector2(212.8f, 215.2f);
        /// <summary>Lobby → auditorium doorway (x) in the z 312 wall.</summary>
        public static readonly Vector2 AuditoriumDoorX = new Vector2(191.2f, 194.8f);
        /// <summary>Upper foyer → balcony doorway (x) in the z 312 wall at +4.5.</summary>
        public static readonly Vector2 BalconyDoorX = new Vector2(212f, 216f);
        /// <summary>Balcony stair x range along the east wall.</summary>
        public static readonly Vector2 StairX = new Vector2(227.4f, 229.6f);

        /// <summary>Builds the whole cinema.</summary>
        public static void Build(MeshBuilder shell, MeshBuilder interior, StructureAnchors anchors, int tint)
        {
            Exterior(shell, anchors, tint);
            Lobby(interior, anchors);
            Auditorium(interior, anchors);
            UpperFoyerAndBalcony(interior, anchors);
            Marquee(shell, anchors);
            CrateStack(shell, anchors);
        }

        // ------------------------------------------------------------------ exterior

        private static void Exterior(MeshBuilder mb, StructureAnchors anchors, int tint)
        {
            var r = Footprint;
            var plaster = KitSlots.Plaster(tint);
            var frames = WallFrame.Perimeter(r.X0, r.Z0, r.X1, r.Z1, 0f);
            var south = frames[0];
            var east = frames[1];
            var north = frames[2];
            var west = frames[3];
            var d = Wall;

            // South facade: lobby entrance (boarded glazed doors), concession doors behind the portico, upper foyer windows
            // and the marquee door; 1950s horizontal bands and a vertical blade sign.
            var so = new List<WallOpening>
            {
                WallOpening.Make(2.6f, 9.4f, 0f, 3.1f, OpeningShape.Rect, d, true, OpeningFill.Shopfront, 3, true),
                WallOpening.Make(14.5f, 18.5f, 0f, 2.9f, OpeningShape.Rect, d, true, OpeningFill.Shopfront, 4, true),
                WallOpening.Make(22.0f, 26.0f, 0f, 2.9f, OpeningShape.Rect, d, true, OpeningFill.Shopfront, 5, true),
                WallOpening.Make(29.5f, 33.5f, 0f, 2.9f, OpeningShape.Rect, d, true, OpeningFill.RollerShutter, 6, true),
                WallOpening.Make(MarqueeDoorX.x - r.X0, MarqueeDoorX.y - r.X0, UpperY, UpperY + 2.6f, OpeningShape.Rect, d, false, OpeningFill.None, 0, true),
            };
            foreach (var u in new[] { 14.0f, 19.8f, 31.0f, 36.6f })
                so.Add(WallOpening.Make(u, u + 1.6f, UpperY + 0.9f, UpperY + 3.6f, OpeningShape.Rect, d, true, OpeningFill.SteelWindow, (int)u));
            WallBuilder.Face(mb, south, 0f, RoofY, so, plaster, plaster, KitSlots.InteriorCard);
            foreach (var o in so) KitFills.Fill(mb, south, o);
            KitTrims.Plinth(mb, south, 0.5f, 0.04f, KitSlots.Terrazzo);
            KitTrims.StringCourse(mb, south, 8.8f, 0.18f, 0.06f, KitSlots.PlasterRough);
            KitTrims.SpeedLines(mb, south, 9.3f, 0.5f, r.Width - 0.5f, KitSlots.PlasterRough);
            // Vertical blade sign at the facade centre (kept ≤ 13.5 m so the R4 sun line clears it, GDD §8.5).
            mb.Slot(KitSlots.SteelFrame);
            KitTrims.FrameBox(mb, south, 22.3f, 22.7f, 6.4f, 13.4f, 0f, 1.6f);
            mb.Slot(KitSlots.ClockFace);
            KitTrims.FrameBox(mb, south, 22.25f, 22.75f, 6.8f, 13.0f, 0.15f, 1.5f, KitTrims.NoBack);
            anchors.Add("Sign.Blade", south.P(22.5f, 9.9f, 0.9f), 180f);

            // West facade on the service yard: the side door, high windows, a drainpipe.
            var doorU = r.Z1 - SideDoorZ.y;
            var wo = new List<WallOpening>
            {
                WallOpening.Make(doorU, doorU + (SideDoorZ.y - SideDoorZ.x), 0f, 2.4f, OpeningShape.Rect, d, false, OpeningFill.None, 0, true),
                WallOpening.Make(4f, 5.2f, 6.6f, 8.2f, OpeningShape.Rect, d, true, OpeningFill.Vent, 11),
                WallOpening.Make(13f, 14.2f, 6.6f, 8.2f, OpeningShape.Rect, d, true, OpeningFill.Vent, 12),
            };
            WallBuilder.Face(mb, west, 0f, RoofY, wo, plaster, plaster, KitSlots.InteriorCard);
            foreach (var o in wo) KitFills.Fill(mb, west, o);
            KitTrims.Plinth(mb, west, 0.5f, 0.03f, KitSlots.PlasterBase);
            mb.Slot(KitSlots.Iron);
            mb.AddTube(west.P(1.2f, 0f, 0.08f), west.P(1.2f, RoofY + 0.3f, 0.08f), 0.05f, 6);
            anchors.Add("SideDoor.Hinge", new Vector3(r.X0, 0f, SideDoorZ.x), 270f);
            anchors.Add("SideDoor.Center", new Vector3(r.X0, 1.2f, (SideDoorZ.x + SideDoorZ.y) * 0.5f), 270f);
            anchors.Add("ExitSign", new Vector3(r.X0 + d + 0.06f, 2.75f, (SideDoorZ.x + SideDoorZ.y) * 0.5f), 90f);

            // East and north facades: blank with vents (they face a 1 m light well and housing).
            var eo = new List<WallOpening> { WallOpening.Make(8f, 8.8f, 9f, 9.8f, OpeningShape.Rect, d, true, OpeningFill.Vent, 21) };
            WallBuilder.Face(mb, east, 0f, RoofY, eo, plaster, plaster, KitSlots.InteriorCard);
            WallBuilder.Face(mb, north, 0f, RoofY, new[] { WallOpening.Make(20f, 21f, 8.6f, 9.4f, OpeningShape.Rect, d, true, OpeningFill.Vent, 22) }, plaster, plaster, KitSlots.InteriorCard);
            KitTrims.Plinth(mb, east, 0.5f, 0.03f, KitSlots.PlasterBase);
            KitTrims.Plinth(mb, north, 0.5f, 0.03f, KitSlots.PlasterBase);

            // Parapet, coping, roof with shrapnel holes (dust shafts, GDD §7.4) and roof clutter.
            foreach (var f in frames)
            {
                WallBuilder.Face(mb, f, RoofY, TopY, null, plaster, plaster, null);
                var inner = new WallFrame(f.Origin + f.U * f.Length - f.N * 0.3f, -f.U, f.Length, f.UvU + 5f);
                mb.Slot(plaster);
                WallBuilder.Quad(mb, inner, 0.3f, f.Length - 0.3f, RoofY, TopY);
            }
            KitTrims.Coping(mb, r.X0, r.Z0, r.X1, r.Z1, TopY, 0.3f, 0.04f, 0.08f, KitSlots.PlasterRough);
            var holes = new List<Rect>
            {
                Rect.MinMaxRect(199.4f, 321.6f, 200.4f, 322.4f), Rect.MinMaxRect(209.6f, 325.4f, 210.8f, 326.2f),
                Rect.MinMaxRect(218.8f, 319.6f, 219.5f, 320.5f), Rect.MinMaxRect(205.7f, 316.0f, 206.5f, 316.9f),
            };
            // The slab underside is the auditorium ceiling (holes included, so the dust shafts come through).
            Interiors.Slab(mb, r.X0 + 0.3f, r.Z0 + 0.3f, r.X1 - 0.3f, r.Z1 - 0.3f, RoofY, 0.5f, KitSlots.RoofDeck, KitSlots.PlasterRough, SurfaceType.Concrete, holes);
            for (var i = 0; i < holes.Count; i++)
                anchors.Add("Shaft." + i, new Vector3(holes[i].center.x, RoofY - 0.5f, holes[i].center.y), 0f);
            mb.Slot(KitSlots.SheetMetal);
            mb.AddBox(new Vector3(194f, RoofY + 0.6f, 318f), new Vector3(2.4f, 1.2f, 1.4f), MeshBuilder.FacesNoBottom);
            mb.AddBox(new Vector3(222f, RoofY + 0.4f, 309f), new Vector3(1.2f, 0.8f, 3.2f), MeshBuilder.FacesNoBottom);

            // Exterior wall colliders (doorways carved).
            WallCollider(mb, new Vector3(r.X0, 0f, r.Z0), Vector3.right, r.Width, new[] { new WallGap(MarqueeDoorX.x - r.X0, MarqueeDoorX.y - r.X0, UpperY, UpperY + 2.6f) });
            WallCollider(mb, new Vector3(r.X1, 0f, r.Z0), Vector3.forward, r.Depth, null);
            WallCollider(mb, new Vector3(r.X1, 0f, r.Z1), Vector3.left, r.Width, null);
            WallCollider(mb, new Vector3(r.X0, 0f, r.Z1), Vector3.back, r.Depth, new[] { new WallGap(r.Z1 - SideDoorZ.y, r.Z1 - SideDoorZ.x, 0f, 2.4f) });
        }

        private static void WallCollider(MeshBuilder mb, Vector3 outerStart, Vector3 dir, float length, IList<WallGap> gaps)
        {
            var n = Vector3.Cross(Vector3.up, dir);
            var a = outerStart - n * (Wall * 0.5f);
            Interiors.WallColliders(mb, a, dir, n, length, TopY, Wall, gaps, SurfaceType.Plaster);
        }

        // ------------------------------------------------------------------ lobby

        private static void Lobby(MeshBuilder mb, StructureAnchors anchors)
        {
            var x0 = Footprint.X0 + Wall;
            var z0 = Footprint.Z0 + Wall;
            const float ceiling = 4.2f;
            var frames = Interiors.InwardFrames(x0, z0, LobbyX1, AuditoriumZ0, 0f);
            var gaps = new IList<WallGap>[4];
            // North wall (index 2, faces south, u from x0 eastwards): the auditorium doorway.
            gaps[2] = new[] { new WallGap(AuditoriumDoorX.x - x0, AuditoriumDoorX.y - x0, 0f, 2.8f) };
            // West wall (index 3, faces east, u from z0 northwards): the side door.
            gaps[3] = new[] { new WallGap(SideDoorZ.x - z0, SideDoorZ.y - z0, 0f, 2.4f) };
            // South wall (index 0, u from x = LobbyX1 westwards): the boarded entrance (seen from inside as dark glass).
            gaps[0] = new[] { new WallGap(LobbyX1 - (Footprint.X0 + 9.4f), LobbyX1 - (Footprint.X0 + 2.6f), 0f, 3.1f) };
            Interiors.InwardRoom(mb, x0, z0, LobbyX1, AuditoriumZ0, 0f, ceiling, KitSlots.PlasterRough, KitSlots.Terrazzo, KitSlots.PlasterRough, gaps);
            // Dado band and a ticket counter.
            mb.Slot(KitSlots.WoodDark);
            KitTrims.FrameBox(mb, frames[1], 0f, frames[1].Length, 0f, 1.1f, 0f, 0.03f, KitTrims.NoBack);
            mb.AddBox(new Vector3(196.2f, 0.55f, 305.5f), new Vector3(0.6f, 1.1f, 3.2f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3(196.2f, 0.55f, 305.5f), new Vector3(0.6f, 1.1f, 3.2f), SurfaceType.Wood);
            mb.Slot(KitSlots.Glass);
            mb.AddBox(new Vector3(196.2f, 1.6f, 305.5f), new Vector3(0.05f, 1.0f, 3.2f));
            // Partitions: lobby/concession (x 198) and lobby/auditorium (z 312) walls with colliders.
            Interiors.WallColliders(mb, new Vector3(LobbyX1 + 0.15f, 0f, z0), Vector3.forward, Vector3.left, AuditoriumZ0 - z0, UpperY, 0.3f, null, SurfaceType.Plaster);
            Interiors.WallColliders(mb, new Vector3(x0, 0f, AuditoriumZ0), Vector3.right, Vector3.forward, LobbyX1 - x0, UpperY,
                0.3f, new[] { new WallGap(AuditoriumDoorX.x - x0, AuditoriumDoorX.y - x0, 0f, 2.8f) }, SurfaceType.Plaster);
            // Slab above the lobby (projection rooms over it are closed).
            Interiors.Slab(mb, x0, z0, LobbyX1, AuditoriumZ0, UpperY, 0.3f, null, null, SurfaceType.Concrete);
            // Concession hall: closed, solid.
            mb.AddCollider(new Vector3((LobbyX1 + Footprint.X1 - Wall) * 0.5f, UpperY * 0.5f - 0.15f, (z0 + AuditoriumZ0) * 0.5f),
                new Vector3(Footprint.X1 - Wall - LobbyX1, UpperY - 0.3f, AuditoriumZ0 - z0), SurfaceType.Plaster, LayerIds.Default, "ConcessionBlock");
            anchors.Add("Lobby.Light", new Vector3(192f, 3.9f, 307f), 0f);
            anchors.Add("Lobby.Posters", new Vector3(x0 + 0.02f, 1.6f, 309.5f), 90f);
            anchors.Add("Lobby.AuditoriumDoor", new Vector3((AuditoriumDoorX.x + AuditoriumDoorX.y) * 0.5f, 0f, AuditoriumZ0), 0f);
        }

        // ------------------------------------------------------------------ auditorium

        private static void Auditorium(MeshBuilder mb, StructureAnchors anchors)
        {
            var x0 = Footprint.X0 + Wall;
            var x1 = Footprint.X1 - Wall;
            var z1 = Footprint.Z1 - Wall;
            var ceiling = RoofY - 0.5f;
            var gaps = new IList<WallGap>[4];
            gaps[0] = new[] { new WallGap(x1 - AuditoriumDoorX.y, x1 - AuditoriumDoorX.x, 0f, 2.8f), new WallGap(x1 - BalconyDoorX.y, x1 - BalconyDoorX.x, UpperY, UpperY + 2.6f) };
            Interiors.InwardRoom(mb, x0, AuditoriumZ0, x1, z1, 0f, ceiling, KitSlots.Velvet, KitSlots.ConcreteFloor, null, gaps);
            // Dark plaster above 3 m on the side walls and pilasters every 5 m.
            var frames = Interiors.InwardFrames(x0, AuditoriumZ0, x1, z1, 0f);
            foreach (var side in new[] { 1, 3 })
            {
                var f = frames[side];
                for (var u = 2f; u < f.Length - 1f; u += 5f) KitTrims.Pilaster(mb, f, u, 0.6f, 0f, ceiling - 0.4f, 0.12f, KitSlots.PlasterRough);
            }
            // Proscenium, screen and curtains on the north wall.
            var nf = frames[2];
            mb.Slot(KitSlots.PlasterRough);
            KitTrims.FrameBox(mb, nf, 1.5f, 3.5f, 0f, 9.6f, 0f, 0.5f);
            KitTrims.FrameBox(mb, nf, nf.Length - 3.5f, nf.Length - 1.5f, 0f, 9.6f, 0f, 0.5f);
            KitTrims.FrameBox(mb, nf, 1.5f, nf.Length - 1.5f, 8.6f, 9.6f, 0f, 0.5f);
            mb.Slot(KitSlots.Screen);
            KitTrims.FrameBox(mb, nf, 7f, nf.Length - 7f, 1.6f, 8.0f, 0.6f, 0.65f, 0x3F);
            mb.Slot(KitSlots.Velvet);
            KitStructures.ClothPanel(mb, nf.P(3.5f, 8.6f, 0.9f), nf.P(7.4f, 8.6f, 0.9f), nf.P(3.5f, 0.05f, 0.95f), nf.P(7.4f, 0.05f, 1.05f), -0.0f, false, 7, KitSlots.Velvet, 6, 6);
            KitStructures.ClothPanel(mb, nf.P(nf.Length - 7.4f, 8.6f, 0.9f), nf.P(nf.Length - 3.5f, 8.6f, 0.9f), nf.P(nf.Length - 7.4f, 0.05f, 1.05f), nf.P(nf.Length - 3.5f, 0.05f, 0.95f), 0f, false, 8, KitSlots.Velvet, 6, 6);
            // Stage riser.
            mb.Slot(KitSlots.WoodDark);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, 0.35f, z1 - 1.6f), new Vector3(x1 - x0 - 3f, 0.7f, 3.2f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f, 0.35f, z1 - 1.6f), new Vector3(x1 - x0 - 3f, 0.7f, 3.2f), SurfaceType.Wood);
            anchors.Add("Screen.Center", nf.P(nf.Length * 0.5f, 4.8f, 0.7f), 180f);
            // Carpet runners in the aisles.
            mb.Slot(KitSlots.Carpet);
            mb.AddQuad(new Vector3(188.4f, 0.01f, BalconyZ1 + 0.2f), new Vector3(188.4f, 0.01f, z1 - 3.4f), new Vector3(190.0f, 0.01f, z1 - 3.4f), new Vector3(190.0f, 0.01f, BalconyZ1 + 0.2f));
            mb.AddQuad(new Vector3(207.6f, 0.01f, AuditoriumZ0 + 0.2f), new Vector3(207.6f, 0.01f, z1 - 3.4f), new Vector3(209.4f, 0.01f, z1 - 3.4f), new Vector3(209.4f, 0.01f, AuditoriumZ0 + 0.2f));
            // Seats: low-poly rows (the FAL seat is too heavy for ~600 instances); rows face the screen (north).
            var rowIndex = 0;
            for (var z = BalconyZ1 + 1.2f; z < z1 - 4.0f; z += 0.95f, rowIndex++) SeatRow(mb, 190.6f, 207.3f, z, 0f, rowIndex);
            rowIndex = 0;
            for (var z = BalconyZ1 + 1.2f; z < z1 - 4.0f; z += 0.95f, rowIndex++) SeatRow(mb, 209.7f, 226.9f, z, 0f, rowIndex + 50);
            for (var z = AuditoriumZ0 + 0.9f; z < BalconyZ1 - 0.2f; z += 0.95f, rowIndex++) SeatRow(mb, 190.6f, 207.3f, z, 0f, rowIndex + 100);
            for (var z = AuditoriumZ0 + 0.9f; z < BalconyZ1 - 0.2f; z += 0.95f, rowIndex++) SeatRow(mb, 209.7f, 226.9f, z, 0f, rowIndex + 150);
            // Balcony stair along the east wall: foot near the screen end, rising south onto the balcony.
            var stairW = StairX.y - StairX.x;
            var run = KitStructures.RunOf(UpperY);
            var foot = new Vector3((StairX.x + StairX.y) * 0.5f, 0f, BalconyZ1 + run);
            KitStructures.Stairs(mb, foot, Vector3.back, stairW, UpperY, true, KitSlots.Carpet, KitSlots.PlasterRough, SurfaceType.Wood);
            KitStructures.StairRail(mb, new Vector3(StairX.x, 0f, foot.z), new Vector3(StairX.x, UpperY, BalconyZ1), 0.95f, KitSlots.WoodDark);
            anchors.Add("Stair.Foot", foot, 180f);
            anchors.Add("Stair.Top", new Vector3(foot.x, UpperY, BalconyZ1 - 0.4f), 180f);
            // Dim practicals: wall sconces (emissive heads placed by the builder) every 10 m.
            for (var z = 316f; z < 328f; z += 6f)
            {
                anchors.Add("Sconce.W." + z, new Vector3(x0 + 0.15f, 3.4f, z), 90f);
                anchors.Add("Sconce.E." + z, new Vector3(x1 - 0.15f, 3.4f, z), 270f);
            }
        }

        private static void SeatRow(MeshBuilder mb, float xFrom, float xTo, float z, float floorY, int seed)
        {
            var count = Mathf.FloorToInt((xTo - xFrom) / 0.56f);
            var rng = new DetRandom((ulong)(seed * 13 + 5));
            for (var i = 0; i < count; i++)
            {
                var x = xFrom + 0.28f + i * 0.56f;
                var folded = rng.Chance(0.3f);
                var broken = rng.Chance(0.06f);
                mb.Push(new Vector3(x, floorY, z), Quaternion.identity);
                mb.Slot(KitSlots.Iron);
                mb.AddBox(new Vector3(-0.27f, 0.32f, 0f), new Vector3(0.04f, 0.64f, 0.5f), MeshBuilder.FacesNoBottom);
                mb.Slot(KitSlots.Velvet);
                if (!broken)
                {
                    if (folded) mb.AddBox(new Vector3(0f, 0.55f, 0.05f), new Vector3(0.48f, 0.4f, 0.08f));
                    else mb.AddBox(new Vector3(0f, 0.44f, -0.05f), new Vector3(0.48f, 0.09f, 0.44f));
                }
                mb.Push(new Vector3(0f, 0.78f, 0.24f), Quaternion.Euler(-10f, 0f, 0f));
                mb.AddBox(Vector3.zero, new Vector3(0.48f, 0.62f, 0.08f));
                mb.Pop();
                mb.Pop();
            }
            mb.Slot(KitSlots.Iron);
            mb.AddBox(new Vector3(xFrom + count * 0.56f + 0.02f, floorY + 0.32f, z), new Vector3(0.04f, 0.64f, 0.5f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3((xFrom + xTo) * 0.5f, floorY + 0.5f, z), new Vector3(xTo - xFrom, 1.0f, 0.55f), SurfaceType.Fabric);
        }

        // ------------------------------------------------------------------ balcony and upper foyer

        private static void UpperFoyerAndBalcony(MeshBuilder mb, StructureAnchors anchors)
        {
            var x0 = Footprint.X0 + Wall;
            var x1 = Footprint.X1 - Wall;
            var z0 = Footprint.Z0 + Wall;
            // Balcony slab (z 312–318.2), underside visible from the stalls.
            Interiors.Slab(mb, x0, AuditoriumZ0, x1, BalconyZ1, UpperY, 0.4f, KitSlots.Carpet, KitSlots.PlasterRough, SurfaceType.Wood);
            // Balcony front: solid balustrade with a gap where the stair arrives.
            var gaps = new[] { new WallGap(StairX.x - x0, StairX.y - x0, 0f, 1.2f) };
            Interiors.Partition(mb, new Vector3(x0, UpperY, BalconyZ1 - 0.1f), new Vector3(x1, UpperY, BalconyZ1 - 0.1f), 1.0f, 0.2f, gaps, KitSlots.Velvet, KitSlots.PlasterRough, SurfaceType.Wood);
            // Stepped balcony seating (three risers towards the back).
            for (var row = 0; row < 3; row++)
            {
                var z = BalconyZ1 - 1.4f - row * 1.0f;
                var y = UpperY + row * 0.35f;
                if (row > 0)
                {
                    mb.Slot(KitSlots.Carpet);
                    mb.AddBox(new Vector3((x0 + StairX.x) * 0.5f, y - 0.175f, z - 0.15f), new Vector3(StairX.x - x0 - 0.2f, 0.35f, 1.0f), MeshBuilder.FacesNoBottom);
                    mb.AddCollider(new Vector3((x0 + StairX.x) * 0.5f, y - 0.175f, z - 0.15f), new Vector3(StairX.x - x0 - 0.2f, 0.35f, 1.0f), SurfaceType.Wood);
                }
                SeatRow(mb, 189.6f, 207.3f, z, y, 300 + row);
                SeatRow(mb, 209.7f, 224.6f, z, y, 320 + row);
            }
            // Upper foyer slab over the concession hall (x 198–231, z 301–312) and its walls.
            Interiors.Slab(mb, LobbyX1, z0, x1, AuditoriumZ0, UpperY, 0.3f, KitSlots.Terrazzo, null, SurfaceType.Tile);
            var foyerGaps = new IList<WallGap>[4];
            // South wall (index 0, u from x1 westwards): the marquee door and the windows.
            foyerGaps[0] = new[] { new WallGap(x1 - MarqueeDoorX.y, x1 - MarqueeDoorX.x, 0f, 2.6f) };
            // North wall (index 2, u from LobbyX1 eastwards): the balcony doorway.
            foyerGaps[2] = new[] { new WallGap(BalconyDoorX.x - LobbyX1, BalconyDoorX.y - LobbyX1, 0f, 2.6f) };
            Interiors.InwardRoom(mb, LobbyX1, z0, x1, AuditoriumZ0, UpperY, 8.4f, KitSlots.PlasterRough, null, KitSlots.PlasterRough, foyerGaps);
            Interiors.WallColliders(mb, new Vector3(LobbyX1, UpperY, AuditoriumZ0), Vector3.right, Vector3.forward, x1 - LobbyX1, 4.0f, 0.3f,
                new[] { new WallGap(BalconyDoorX.x - LobbyX1, BalconyDoorX.y - LobbyX1, 0f, 2.6f) }, SurfaceType.Plaster);
            Interiors.WallColliders(mb, new Vector3(LobbyX1, UpperY, z0), Vector3.forward, Vector3.right, AuditoriumZ0 - z0, 4.0f, 0.3f, null, SurfaceType.Plaster);
            // Stacked chairs and boxes in the foyer (it was a store).
            mb.Slot(KitSlots.Wood);
            mb.AddBox(new Vector3(201f, UpperY + 0.5f, 304f), new Vector3(1.2f, 1.0f, 0.8f), MeshBuilder.FacesNoBottom);
            mb.AddBox(new Vector3(201.2f, UpperY + 1.3f, 304.1f), new Vector3(0.8f, 0.6f, 0.6f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3(201f, UpperY + 0.8f, 304f), new Vector3(1.2f, 1.6f, 0.8f), SurfaceType.Wood);
            anchors.Add("Foyer.Light", new Vector3(214f, 8.1f, 307f), 0f);
            anchors.Add("Balcony.Center", new Vector3(208.5f, UpperY, 315f), 0f);
        }

        // ------------------------------------------------------------------ marquee and crates

        private static void Marquee(MeshBuilder mb, StructureAnchors anchors)
        {
            const float x0 = 199f;
            const float x1 = 227f;
            const float zFront = 296.8f;
            var zBack = Footprint.Z0;
            // Canopy slab (deck on top at +4.5).
            mb.Slot(KitSlots.ConcreteFloor);
            mb.AddQuad(new Vector3(x0, UpperY, zFront), new Vector3(x0, UpperY, zBack), new Vector3(x1, UpperY, zBack), new Vector3(x1, UpperY, zFront));
            mb.Slot(KitSlots.PlasterRough);
            mb.AddQuad(new Vector3(x0, 3.9f, zBack), new Vector3(x0, 3.9f, zFront), new Vector3(x1, 3.9f, zFront), new Vector3(x1, 3.9f, zBack));
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f, 4.2f, (zFront + zBack) * 0.5f), new Vector3(x1 - x0, 0.6f, zBack - zFront), SurfaceType.Concrete);
            // Letter boards on the front and ends (below the deck), SignBaker titles go on the front one.
            mb.Slot(KitSlots.SteelFrame);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, 3.6f, zFront - 0.08f), new Vector3(x1 - x0 + 0.2f, 1.25f, 0.16f));
            mb.AddBox(new Vector3(x0 - 0.08f, 3.6f, (zFront + zBack) * 0.5f), new Vector3(0.16f, 1.25f, zBack - zFront));
            mb.AddBox(new Vector3(x1 + 0.08f, 3.6f, (zFront + zBack) * 0.5f), new Vector3(0.16f, 1.25f, zBack - zFront));
            mb.Slot(KitSlots.ClockFace);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, 3.6f, zFront - 0.17f), new Vector3(x1 - x0 - 1f, 0.95f, 0.02f), 32);
            anchors.Add("Marquee.Titles", new Vector3((x0 + x1) * 0.5f, 3.6f, zFront - 0.2f), 180f);
            // Bulb chaser strip (emissive, off) along the board's lower edge.
            mb.Slot(KitSlots.SodiumLens);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, 2.95f, zFront - 0.12f), new Vector3(x1 - x0, 0.06f, 0.06f));
            // Walkway railing on the canopy front (pipe) so the player can see and shoot the technical (GDD §7.4).
            KitStructures.PipeRailing(mb, new[] { new Vector3(x0 + 0.1f, UpperY, zFront + 0.15f), new Vector3(x1 - 0.1f, UpperY, zFront + 0.15f) }, 1.0f, KitSlots.SteelFrame);
            KitStructures.PipeRailing(mb, new[] { new Vector3(x0 + 0.1f, UpperY, zFront + 0.15f), new Vector3(x0 + 0.1f, UpperY, zBack) }, 1.0f, KitSlots.SteelFrame);
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f - 1f, UpperY + 0.5f, zFront + 0.15f), new Vector3(x1 - x0 - 2f, 1.0f, 0.08f), SurfaceType.Metal);
            mb.AddCollider(new Vector3(x0 + 0.1f, UpperY + 0.5f, (zFront + zBack) * 0.5f), new Vector3(0.08f, 1.0f, zBack - zFront), SurfaceType.Metal);
            // Portico columns under the canopy front ("north arcade below the marquee", E4-04).
            mb.Slot(KitSlots.PlasterRough);
            for (var x = 200.5f; x < x1; x += 6.25f)
            {
                mb.AddCylinder(new Vector3(x, 0f, zFront + 0.5f), 0.26f, 3.9f, 12, false, false);
                mb.AddCollider(new Vector3(x, 1.95f, zFront + 0.5f), new Vector3(0.5f, 3.9f, 0.5f), SurfaceType.Plaster);
            }
            anchors.Add("Marquee.Door", new Vector3((MarqueeDoorX.x + MarqueeDoorX.y) * 0.5f, UpperY, Footprint.Z0), 180f);
            anchors.Add("Marquee.EastEnd", new Vector3(x1 - 0.5f, UpperY, 299.5f), 90f);
        }

        private static void CrateStack(MeshBuilder mb, StructureAnchors anchors)
        {
            // Two tiers of market crates under a fallen awning: top +2.2 (GDD §6.5 A8).
            var rng = new DetRandom(2272UL);
            mb.Slot(KitSlots.Wood);
            void Crate(Vector3 c, Vector3 s)
            {
                mb.Push(c, Quaternion.Euler(0f, rng.Range(-6f, 6f), 0f));
                mb.AddChamferBox(Vector3.zero, s, 0.02f, true, false);
                mb.Pop();
            }
            Crate(new Vector3(226.8f, 0.55f, 297.7f), new Vector3(1.2f, 1.1f, 1.0f));
            Crate(new Vector3(228.1f, 0.55f, 297.9f), new Vector3(1.2f, 1.1f, 1.0f));
            Crate(new Vector3(227.4f, 0.55f, 298.9f), new Vector3(1.2f, 1.1f, 1.0f));
            Crate(new Vector3(227.2f, 1.65f, 298.3f), new Vector3(1.3f, 1.1f, 1.2f));
            mb.AddCollider(new Vector3(227.45f, 0.55f, 298.3f), new Vector3(2.6f, 1.1f, 2.2f), SurfaceType.Wood);
            mb.AddCollider(new Vector3(227.2f, 1.65f, 298.3f), new Vector3(1.3f, 1.1f, 1.2f), SurfaceType.Wood);
            KitStructures.ClothPanel(mb, new Vector3(225.6f, 2.35f, 297.2f), new Vector3(229f, 2.25f, 297.4f), new Vector3(225.8f, 0.4f, 296.6f), new Vector3(229.2f, 0.2f, 296.9f), 0.15f, true, 9, KitSlots.Awning, 6, 4);
            anchors.Add("CrateStack.Top", new Vector3(227.2f, 2.2f, 298.3f), 0f);
        }
    }
}
