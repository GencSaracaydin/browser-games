using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// The Relay Hall (GDD §6.4 Map C, §6.5 A12–A15, §6.6 BLD-10): a 1930s Art Deco telegraph hall, 40 x 32 m, 9 m, with
    /// the painted company name, a stepped crown and a steel double door at (338, 0.3, 330). The exterior shell lives in
    /// <c>S5_StationExt</c> (<see cref="BuildShell"/>); the interiors in <c>S6_StationInt</c> (<see cref="BuildInterior"/>):
    /// <list type="bullet">
    /// <item>ground floor (+0.3, ceiling 4.5): the switch hall x 316–346 under a double-height void, three teletype rows
    /// (1.1 m cover) at z 339.5 / 346 / 352.5, the world-time clock wall on the north wall, the spiral stair at
    /// (334.5, 358), the west stair down in an enclosure with its door (Gate.WestStairDoor), Harrou's office x 346–356,
    /// z 347–362 (door at z 351.5) and the archive x 346–356, z 330–347 (door at z 342) with the east stair down;</item>
    /// <item>upper floor (+4.8): the screening room x 316–330, z 350–362 with its door on the landing (Gate.ScreeningDoor
    /// (330, 4.8, 353.5)), the landing x 330–340 and a closed loft;</item>
    /// <item>basement (−4.2, 3.6 m): x 318–346, z 334–360, the transmitter racks R1–R4 behind mesh along z 356–358, the
    /// tape deck and speaker, the vault safe in the east wall, the hatch to the cable trench below the south wall, the
    /// tunnel door at (344, −3, 359.8) and the cable tunnel x 342–345 to the ladder shaft behind the postern.</item>
    /// </list>
    /// </summary>
    public static class RelayHallGeometry
    {
        /// <summary>Footprint (GDD §6.5 A12).</summary>
        public static readonly AreaRect Footprint = new AreaRect(316f, 330f, 356f, 362f);
        /// <summary>Exterior wall thickness.</summary>
        public const float Wall = 0.4f;
        /// <summary>Ground floor level.</summary>
        public const float GroundY = 0.3f;
        /// <summary>Ground floor ceiling (office, archive, under the upper floor).</summary>
        public const float GroundCeiling = 4.5f;
        /// <summary>Upper floor level (GDD Map C2).</summary>
        public const float UpperY = 4.8f;
        /// <summary>Roof slab top.</summary>
        public const float RoofY = 8.6f;
        /// <summary>Parapet top.</summary>
        public const float TopY = 9.2f;
        /// <summary>Basement floor (GDD Map C3).</summary>
        public const float BasementY = -4.2f;
        /// <summary>Basement ceiling (3.6 m).</summary>
        public const float BasementCeiling = -0.6f;
        /// <summary>Hall double door span (x) on the south wall.</summary>
        public static readonly Vector2 HallDoorX = new Vector2(336.8f, 339.2f);
        /// <summary>Ladder shaft x range at the tunnel's north end (up to the yard hatch behind the postern).</summary>
        public static readonly Vector2 ShaftX = new Vector2(343.6f, 345.0f);
        /// <summary>North end of the cable tunnel (the shaft's back wall).</summary>
        public const float TunnelEndZ = 381.2f;
        /// <summary>Basement interior.</summary>
        public static readonly AreaRect Basement = new AreaRect(318f, 334f, 346f, 360f);

        private static readonly Vector2 s_westStairX = new Vector2(318.3f, 320.9f);
        private const float WestStairTopZ = 335.6f;
        private const float WestStairBottomZ = 343.6f;
        private static readonly Vector2 s_eastStairZ = new Vector2(336.6f, 339.0f);
        private const float EastStairTopX = 352.0f;
        private const float EastStairBottomX = 344.0f;
        private static readonly Vector2 s_officeDoorZ = new Vector2(350.8f, 352.2f);
        private static readonly Vector2 s_archiveDoorZ = new Vector2(341.4f, 342.6f);
        private static readonly Vector2 s_screeningDoorZ = new Vector2(352.9f, 354.1f);
        private static readonly Vector2 s_westStairDoorX = new Vector2(318.6f, 320.6f);
        private const float WestStairDoorZ = 334.6f;

        // ================================================================== exterior shell (S5)

        /// <summary>The Art Deco exterior: facades, door steps, fins, speed lines, stepped crown, clerestory windows, roof.</summary>
        public static void BuildShell(MeshBuilder mb, StructureAnchors anchors)
        {
            var r = Footprint;
            var frames = WallFrame.Perimeter(r.X0, r.Z0, r.X1, r.Z1, 0f);
            var render = KitSlots.DecoRender;
            var trim = KitSlots.DecoTrim;
            var d = Wall;
            // South facade: the double door, clerestory windows in five bays, fins between bays.
            var south = frames[0];
            var so = new List<WallOpening> { WallOpening.Make(HallDoorX.x - r.X0, HallDoorX.y - r.X0, GroundY, GroundY + 3.0f, OpeningShape.Rect, d, false, OpeningFill.None, 0, true) };
            var bayW = r.Width / 8f;
            for (var i = 0; i < 8; i++)
            {
                var c = (i + 0.5f) * bayW;
                if (Mathf.Abs(c - (HallDoorX.x + HallDoorX.y) * 0.5f + r.X0) < 3f) continue;
                so.Add(WallOpening.Make(c - 1.1f, c + 1.1f, 5.4f, 8.0f, OpeningShape.Rect, d, true, OpeningFill.SteelWindow, 100 + i));
                so.Add(WallOpening.Make(c - 0.8f, c + 0.8f, 1.8f, 3.6f, OpeningShape.Rect, d, true, OpeningFill.SteelWindow, 120 + i));
            }
            WallBuilder.Face(mb, south, 0f, RoofY, so, render, render, KitSlots.InteriorCard);
            foreach (var o in so) KitFills.Fill(mb, south, o);
            for (var i = 1; i < 8; i++) KitTrims.DecoFin(mb, south, i * bayW, 0.6f, RoofY - 0.2f, 0.22f, 0.36f, trim);
            KitTrims.SpeedLines(mb, south, 4.2f, 0.3f, r.Width - 0.3f, trim);
            var doorC = (HallDoorX.x + HallDoorX.y) * 0.5f - r.X0;
            KitTrims.DecoStepped(mb, south, doorC, 9f, RoofY, 3, 0.7f, 0.5f, trim, render);
            // Door surround and canopy.
            mb.Slot(trim);
            KitTrims.FrameBox(mb, south, doorC - 2.0f, doorC + 2.0f, GroundY + 3.0f, GroundY + 3.5f, 0f, 1.2f);
            KitTrims.FrameBox(mb, south, doorC - 1.9f, doorC - 1.4f, 0f, GroundY + 3.0f, 0f, 0.18f, KitTrims.NoBack);
            KitTrims.FrameBox(mb, south, doorC + 1.4f, doorC + 1.9f, 0f, GroundY + 3.0f, 0f, 0.18f, KitTrims.NoBack);
            anchors.Add("CompanyName", south.P(doorC, 5.9f, 0.02f), 180f);
            anchors.Add("HallDoor.LeftHinge", new Vector3(HallDoorX.x, GroundY, r.Z0 + 0.12f), 0f);
            anchors.Add("HallDoor.RightHinge", new Vector3(HallDoorX.y, GroundY, r.Z0 + 0.12f), 0f);
            // Entrance steps (plinth at +0.3).
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(new Vector3(r.X0 + doorC, 0.15f, r.Z0 - 0.6f), new Vector3(5f, 0.3f, 1.2f), MeshBuilder.FacesNoBottom);
            mb.AddCollider(new Vector3(r.X0 + doorC, 0.15f, r.Z0 - 0.6f), new Vector3(5f, 0.3f, 1.2f), SurfaceType.Concrete);
            // Other facades.
            var sideOpenings = new List<WallOpening>[4];
            for (var side = 1; side < 4; side++)
            {
                var f = frames[side];
                var list = new List<WallOpening>();
                var n = Mathf.Max(3, Mathf.RoundToInt(f.Length / 5f));
                var bw = f.Length / n;
                for (var i = 0; i < n; i++)
                {
                    var c = (i + 0.5f) * bw;
                    list.Add(WallOpening.Make(c - 1.0f, c + 1.0f, 5.4f, 7.9f, OpeningShape.Rect, d, true, OpeningFill.SteelWindow, side * 20 + i));
                    if (side == 1) list.Add(WallOpening.Make(c - 0.75f, c + 0.75f, 1.5f, 3.4f, OpeningShape.Rect, d, false, OpeningFill.SteelWindow, side * 40 + i));
                }
                sideOpenings[side] = list;
                WallBuilder.Face(mb, f, 0f, RoofY, list, render, render, KitSlots.InteriorCard);
                foreach (var o in list) KitFills.Fill(mb, f, o);
                for (var i = 1; i < n; i++) KitTrims.DecoFin(mb, f, i * bw, 0.6f, RoofY - 0.2f, 0.18f, 0.3f, trim);
                KitTrims.SpeedLines(mb, f, 4.2f, 0.3f, f.Length - 0.3f, trim);
            }
            foreach (var f in frames)
            {
                KitTrims.Plinth(mb, f, 0.6f, 0.05f, trim);
                WallBuilder.Face(mb, f, RoofY, TopY, null, render, render, null);
                var inner = new WallFrame(f.Origin + f.U * f.Length - f.N * 0.3f, -f.U, f.Length, f.UvU + 5f);
                mb.Slot(render);
                WallBuilder.Quad(mb, inner, 0.3f, f.Length - 0.3f, RoofY, TopY);
            }
            KitTrims.Coping(mb, r.X0, r.Z0, r.X1, r.Z1, TopY, 0.3f, 0.05f, 0.1f, trim);
            mb.Slot(KitSlots.ConcreteFloor);
            mb.AddQuad(new Vector3(r.X0 + 0.3f, RoofY, r.Z0 + 0.3f), new Vector3(r.X0 + 0.3f, RoofY, r.Z1 - 0.3f), new Vector3(r.X1 - 0.3f, RoofY, r.Z1 - 0.3f), new Vector3(r.X1 - 0.3f, RoofY, r.Z0 + 0.3f));
            // Roof: hatch, two ventilators, the cable gantry to the mast.
            mb.Slot(KitSlots.SheetMetal);
            mb.AddBox(new Vector3(340f, RoofY + 0.5f, 357f), new Vector3(1.4f, 1.0f, 1.4f), MeshBuilder.FacesNoBottom);
            mb.AddCylinder(new Vector3(326f, RoofY, 340f), 0.45f, 1.4f, 10, true, false);
            mb.AddCylinder(new Vector3(346f, RoofY, 340f), 0.45f, 1.4f, 10, true, false);
            mb.AddCollider(new Vector3(r.X0 + r.Width * 0.5f, RoofY - 0.2f, r.Z0 + r.Depth * 0.5f), new Vector3(r.Width - 0.6f, 0.4f, r.Depth - 0.6f), SurfaceType.Concrete);
            // Exterior wall colliders (the double door carved; its leaves are the gate's blocker).
            Wall4Colliders(mb, r, new[] { new WallGap(HallDoorX.x - r.X0, HallDoorX.y - r.X0, GroundY, GroundY + 3.0f) });
            anchors.Add("Hall.LampSouth", new Vector3(r.X0 + doorC, 4.0f, r.Z0 - 1.0f), 180f);
        }

        private static void Wall4Colliders(MeshBuilder mb, AreaRect r, IList<WallGap> southGaps)
        {
            var dirs = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            var starts = new[] { new Vector3(r.X0, 0f, r.Z0), new Vector3(r.X1, 0f, r.Z0), new Vector3(r.X1, 0f, r.Z1), new Vector3(r.X0, 0f, r.Z1) };
            var lengths = new[] { r.Width, r.Depth, r.Width, r.Depth };
            for (var i = 0; i < 4; i++)
            {
                var n = Vector3.Cross(Vector3.up, dirs[i]);
                Interiors.WallColliders(mb, starts[i] - n * (Wall * 0.5f), dirs[i], n, lengths[i], TopY, Wall, i == 0 ? southGaps : null, SurfaceType.Concrete);
            }
        }

        // ================================================================== interiors (S6)

        /// <summary>Ground floor, upper floor, basement, cable trench, tunnel and ladder shaft.</summary>
        public static void BuildInterior(MeshBuilder mb, StructureAnchors anchors)
        {
            GroundFloor(mb, anchors);
            UpperFloor(mb, anchors);
            BasementLevel(mb, anchors);
            Tunnel(mb, anchors);
        }

        private static void GroundFloor(MeshBuilder mb, StructureAnchors anchors)
        {
            var r = Footprint;
            var x0 = r.X0 + Wall;
            var x1 = r.X1 - Wall;
            var z0 = r.Z0 + Wall;
            var z1 = r.Z1 - Wall;
            // Floor slab (top +0.3, underside = basement ceiling −0.6) with the two stairwell holes.
            var westHole = Rect.MinMaxRect(s_westStairX.x, WestStairTopZ, s_westStairX.y, WestStairBottomZ);
            var eastHole = Rect.MinMaxRect(346.4f, s_eastStairZ.x, EastStairTopX, s_eastStairZ.y);
            Interiors.Slab(mb, x0, z0, 346f, z1, GroundY, 0.9f, KitSlots.FloorTiles, KitSlots.Concrete, SurfaceType.Tile, new[] { westHole });
            Interiors.Slab(mb, 346f, z0, x1, 347f, GroundY, 0.9f, KitSlots.Linoleum, KitSlots.Concrete, SurfaceType.Tile, new[] { eastHole });
            Interiors.Slab(mb, 346f, 347f, x1, z1, GroundY, 0.9f, KitSlots.WoodDark, KitSlots.Concrete, SurfaceType.Wood);

            // Switch hall shell: inner faces of the exterior walls up to the roof underside (double height over the void).
            var hallGaps = new IList<WallGap>[4];
            hallGaps[0] = new[] { new WallGap(346f - HallDoorX.y, 346f - HallDoorX.x, 0f, 3.0f) };
            Interiors.InwardRoom(mb, x0, z0, 346f, z1, GroundY, RoofY - 0.4f, KitSlots.DecoRender, null, KitSlots.PlasterRough, hallGaps,
                new[] { false, true, false, false });
            // East face above the ground floor (the loft wall seen across the void); the partition below has its own faces.
            mb.Slot(KitSlots.DecoRender);
            mb.AddQuad(new Vector3(346f, GroundCeiling, 350f), new Vector3(346f, RoofY - 0.4f, 350f), new Vector3(346f, RoofY - 0.4f, z0), new Vector3(346f, GroundCeiling, z0));
            // Wainscot (tiled dado) round the hall.
            var hallFrames = Interiors.InwardFrames(x0, z0, 346f, z1, GroundY);
            foreach (var f in hallFrames)
            {
                mb.Slot(KitSlots.FloorTiles);
                var keep = mb.LodMask;
                mb.LodMask = MeshBuilder.LodAll;
                KitTrims.FrameBox(mb, f, 0f, f.Length, GroundY, GroundY + 1.2f, 0f, 0.02f, KitTrims.NoBack);
                mb.LodMask = keep;
            }

            // Office / archive partition (x 346) with the two doors, and the office/archive wall (z 347).
            Interiors.Partition(mb, new Vector3(346f, GroundY, z0), new Vector3(346f, GroundY, z1), GroundCeiling - GroundY, 0.3f,
                new[] { new WallGap(s_archiveDoorZ.x - z0, s_archiveDoorZ.y - z0, 0f, 2.2f), new WallGap(s_officeDoorZ.x - z0, s_officeDoorZ.y - z0, 0f, 2.2f) },
                KitSlots.PlasterRough, KitSlots.DecoRender, SurfaceType.Plaster);
            Interiors.Partition(mb, new Vector3(346.15f, GroundY, 347f), new Vector3(x1, GroundY, 347f), GroundCeiling - GroundY, 0.3f, null, KitSlots.PlasterRough, KitSlots.PlasterRough, SurfaceType.Plaster);
            // Office and archive inner faces of the exterior walls (they share the exterior shell).
            Interiors.InwardRoom(mb, 346.15f, 347.15f, x1, z1, GroundY, GroundCeiling, KitSlots.PlasterRough, null, null, null, new[] { true, false, false, true });
            Interiors.InwardRoom(mb, 346.15f, z0, x1, 346.85f, GroundY, GroundCeiling, KitSlots.PlasterRough, null, null, null, new[] { false, false, true, true });
            anchors.Add("Office.Desk", new Vector3(351f, GroundY, 355f), 180f);
            anchors.Add("Office.Typewriter", new Vector3(349.5f, GroundY, 360.4f), 0f);
            anchors.Add("Office.Cabinet.0", new Vector3(354.9f, GroundY, 350f), 270f);
            anchors.Add("Office.Cabinet.1", new Vector3(354.9f, GroundY, 352.4f), 270f);
            anchors.Add("Office.Light", new Vector3(351f, GroundCeiling - 0.3f, 355f), 0f);
            for (var i = 0; i < 4; i++) anchors.Add("Archive.Shelf." + i, new Vector3(354.6f, GroundY, 340.5f + i * 1.3f), 270f);
            for (var i = 0; i < 3; i++) anchors.Add("Archive.Shelf." + (4 + i), new Vector3(349.5f + i * 1.6f, GroundY, 331.0f), 0f);

            // West stair enclosure (door on its south wall = Gate.WestStairDoor) and the flight down.
            var encZ1 = WestStairBottomZ + 0.4f;
            Interiors.Partition(mb, new Vector3(x0, GroundY, WestStairDoorZ), new Vector3(321.2f, GroundY, WestStairDoorZ), GroundCeiling - GroundY, 0.25f,
                new[] { new WallGap(s_westStairDoorX.x - x0, s_westStairDoorX.y - x0, 0f, 2.2f) }, KitSlots.DecoRender, KitSlots.PlasterRough, SurfaceType.Plaster);
            Interiors.Partition(mb, new Vector3(321.2f, GroundY, WestStairDoorZ), new Vector3(321.2f, GroundY, encZ1), GroundCeiling - GroundY, 0.25f, null, KitSlots.PlasterRough, KitSlots.DecoRender, SurfaceType.Plaster);
            Interiors.Partition(mb, new Vector3(321.2f, GroundY, encZ1), new Vector3(x0, GroundY, encZ1), GroundCeiling - GroundY, 0.25f, null, KitSlots.PlasterRough, KitSlots.DecoRender, SurfaceType.Plaster);
            var wsMid = (s_westStairX.x + s_westStairX.y) * 0.5f;
            KitStructures.Stairs(mb, new Vector3(wsMid, BasementY, WestStairBottomZ), Vector3.back, s_westStairX.y - s_westStairX.x, GroundY - BasementY, true,
                KitSlots.Concrete, KitSlots.Concrete, SurfaceType.Concrete, WestStairBottomZ - WestStairTopZ);
            KitStructures.StairRail(mb, new Vector3(s_westStairX.y - 0.05f, BasementY, WestStairBottomZ), new Vector3(s_westStairX.y - 0.05f, GroundY, WestStairTopZ), 0.95f, KitSlots.Iron);
            anchors.Add("WestStair.DoorHinge", new Vector3(s_westStairDoorX.x, GroundY, WestStairDoorZ), 0f);
            anchors.Add("WestStair.Top", new Vector3(wsMid, GroundY, (WestStairDoorZ + WestStairTopZ) * 0.5f), 0f);
            anchors.Add("WestStair.Foot", new Vector3(wsMid, BasementY, WestStairBottomZ + 0.3f), 180f);

            // East stair from the archive down west into the basement, railing round the hole.
            var esMid = (s_eastStairZ.x + s_eastStairZ.y) * 0.5f;
            KitStructures.Stairs(mb, new Vector3(EastStairBottomX, BasementY, esMid), Vector3.right, s_eastStairZ.y - s_eastStairZ.x, GroundY - BasementY, true,
                KitSlots.Concrete, KitSlots.Concrete, SurfaceType.Concrete, EastStairTopX - EastStairBottomX);
            KitStructures.PipeRailing(mb, new[] { new Vector3(346.5f, GroundY, s_eastStairZ.y + 0.05f), new Vector3(EastStairTopX, GroundY, s_eastStairZ.y + 0.05f) }, 1.0f, KitSlots.Iron);
            KitStructures.PipeRailing(mb, new[] { new Vector3(346.5f, GroundY, s_eastStairZ.x - 0.05f), new Vector3(EastStairTopX - 1.2f, GroundY, s_eastStairZ.x - 0.05f) }, 1.0f, KitSlots.Iron);
            mb.AddCollider(new Vector3((346.5f + EastStairTopX) * 0.5f, GroundY + 0.5f, s_eastStairZ.y + 0.05f), new Vector3(EastStairTopX - 346.5f, 1.0f, 0.1f), SurfaceType.Metal);
            anchors.Add("EastStair.Top", new Vector3(EastStairTopX + 0.6f, GroundY, esMid), 270f);
            anchors.Add("EastStair.Foot", new Vector3(EastStairBottomX - 0.4f, BasementY, esMid), 270f);

            // Teletype rows (1.1 m cover) under the clock wall: cabinets; the teletype props sit on the anchors.
            var rows = new[] { 339.5f, 346.0f, 352.5f };
            for (var row = 0; row < rows.Length; row++)
            {
                var z = rows[row];
                mb.Slot(KitSlots.SteelFrame);
                mb.AddBox(new Vector3(332.5f, GroundY + 0.36f, z), new Vector3(19f, 0.72f, 0.8f), MeshBuilder.FacesNoBottom);
                mb.Slot(KitSlots.WoodDark);
                mb.AddBox(new Vector3(332.5f, GroundY + 0.75f, z), new Vector3(19.2f, 0.06f, 0.9f));
                mb.AddCollider(new Vector3(332.5f, GroundY + 0.55f, z), new Vector3(19f, 1.1f, 0.9f), SurfaceType.Metal);
                for (var i = 0; i < 15; i++) anchors.Add("Teletype." + row + "." + i, new Vector3(323.6f + i * 1.27f, GroundY + 0.78f, z), 180f);
            }
            // World-time clock wall (north wall): twelve clocks stopped, name plates for SignBaker.
            var nf = hallFrames[2];
            for (var i = 0; i < 12; i++)
            {
                var u = 4.6f + i * 2.05f;
                KitStreet.ClockFace(mb, nf, u, GroundY + 2.9f, 0.32f, 0.08f);
                KitStreet.SignBoard(mb, nf, u - 0.45f, u + 0.45f, GroundY + 2.25f, 0.18f, KitSlots.WoodDark);
                anchors.Add("ClockPlate." + i, nf.P(u, GroundY + 2.34f, 0.06f), 180f);
            }
            // Pendant lamps over the switch hall (baked tungsten, GDD §7.7) and ceiling fans under the upper floor.
            for (var i = 0; i < 4; i++)
            for (var j = 0; j < 2; j++)
                anchors.Add("Pendant." + i + "." + j, new Vector3(321f + i * 7.5f, RoofY - 0.4f, 336f + j * 8f), 0f);
            anchors.Add("Fan.0", new Vector3(324f, GroundCeiling - 0.05f, 357f), 0f);
            anchors.Add("Fan.1", new Vector3(341f, GroundCeiling - 0.05f, 357f), 0f);
        }

        private static void UpperFloor(MeshBuilder mb, StructureAnchors anchors)
        {
            var r = Footprint;
            var x0 = r.X0 + Wall;
            var x1 = r.X1 - Wall;
            var z1 = r.Z1 - Wall;
            var spiralHole = Rect.MinMaxRect(332.9f, 356.9f, 336.1f, 360.1f);
            // Upper slab: screening room + landing + loft (z 350–362) and over the office/archive (x 346–356).
            Interiors.Slab(mb, x0, 350f, x1, z1, UpperY, 0.3f, KitSlots.Linoleum, KitSlots.PlasterRough, SurfaceType.Wood, new[] { spiralHole });
            Interiors.Slab(mb, 346f, r.Z0 + Wall, x1, 350f, UpperY, 0.3f, KitSlots.ConcreteFloor, KitSlots.PlasterRough, SurfaceType.Concrete);
            // Spiral stair (334.5, 358) from the ground floor to the landing.
            KitStructures.SpiralStair(mb, new Vector3(334.5f, GroundY, 358.5f), 1.45f, UpperY - GroundY, 180f, 15f, KitSlots.SteelFrame, KitSlots.Iron, KitSlots.Iron, SurfaceType.Metal);
            // Screening room walls: east wall with the door (gate), south wall (can rack inside), inward faces.
            Interiors.Partition(mb, new Vector3(330f, UpperY, 350f), new Vector3(330f, UpperY, z1), RoofY - 0.4f - UpperY, 0.25f,
                new[] { new WallGap(s_screeningDoorZ.x - 350f, s_screeningDoorZ.y - 350f, 0f, 2.2f) }, KitSlots.DecoRender, KitSlots.PlasterRough, SurfaceType.Plaster);
            Interiors.Partition(mb, new Vector3(330f, UpperY, 350f), new Vector3(x0, UpperY, 350f), RoofY - 0.4f - UpperY, 0.25f, null, KitSlots.DecoRender, KitSlots.PlasterRough, SurfaceType.Plaster);
            // Screen on the north wall (z 359.7), seat platforms, projector stand.
            mb.Slot(KitSlots.Screen);
            mb.AddBox(new Vector3(324f, UpperY + 2.1f, 359.75f), new Vector3(6.8f, 2.8f, 0.04f), 32);
            mb.Slot(KitSlots.SteelFrame);
            mb.AddBox(new Vector3(324f, UpperY + 3.6f, 359.7f), new Vector3(7.2f, 0.14f, 0.18f));
            anchors.Add("Screening.Screen", new Vector3(324f, UpperY + 2.1f, 359.7f), 180f);
            for (var row = 0; row < 2; row++)
            for (var i = 0; i < 10; i++)
                anchors.Add("Screening.Seat." + (row * 10 + i), new Vector3(319.6f + i * 0.85f, UpperY, 357.2f - row * 1.4f), 0f);
            anchors.Add("Screening.Projector", new Vector3(323f, UpperY + 0.8f, 352f), 0f);
            anchors.Add("Screening.Table", new Vector3(323f, UpperY, 352f), 0f);
            anchors.Add("Screening.CanRack", new Vector3(324f, UpperY, 350.45f), 0f);
            anchors.Add("Screening.DeskLamp", new Vector3(320.5f, UpperY + 0.8f, 351.2f), 0f);
            anchors.Add("Screening.DoorHinge", new Vector3(330f, UpperY, s_screeningDoorZ.x), 270f);
            // Landing balustrade over the void (z 350, x 330–340) and the loft wall (x 340).
            KitStructures.PipeRailing(mb, new[] { new Vector3(330.2f, UpperY, 350.15f), new Vector3(340f, UpperY, 350.15f) }, 1.05f, KitSlots.SteelFrame);
            mb.AddCollider(new Vector3(335.1f, UpperY + 0.55f, 350.15f), new Vector3(9.8f, 1.1f, 0.1f), SurfaceType.Metal);
            Interiors.Partition(mb, new Vector3(340f, UpperY, 350f), new Vector3(340f, UpperY, z1), RoofY - 0.4f - UpperY, 0.25f, null, KitSlots.DecoRender, KitSlots.DecoRender, SurfaceType.Plaster);
            Interiors.Partition(mb, new Vector3(340f, UpperY, 350f), new Vector3(x1, UpperY, 350f), RoofY - 0.4f - UpperY, 0.25f, null, KitSlots.DecoRender, KitSlots.DecoRender, SurfaceType.Plaster);
            anchors.Add("Landing.Center", new Vector3(335f, UpperY, 355f), 0f);
            anchors.Add("Landing.Light", new Vector3(335f, RoofY - 0.7f, 354f), 0f);
        }

        private static void BasementLevel(MeshBuilder mb, StructureAnchors anchors)
        {
            var b = Basement;
            // Floor with the hatch hole; walls (inner faces, concrete) with openings for the east stair and the tunnel door.
            var hatch = Rect.MinMaxRect(331.4f, 334.05f, 332.6f, 334.95f);
            Interiors.Slab(mb, b.X0, b.Z0, b.X1, b.Z1, BasementY, 0.3f, KitSlots.ConcreteFloor, null, SurfaceType.Concrete, new[] { hatch });
            var gaps = new IList<WallGap>[4];
            // East wall (index 1, faces west, u from z1 southwards): the east stair passage.
            gaps[1] = new[] { new WallGap(b.Z1 - s_eastStairZ.y, b.Z1 - s_eastStairZ.x, 0f, BasementCeiling - BasementY) };
            // North wall (index 2, faces south, u from x0 eastwards): the tunnel door.
            gaps[2] = new[] { new WallGap(343.0f - b.X0, 345.0f - b.X0, 0f, 2.2f) };
            Interiors.InwardRoom(mb, b.X0, b.Z0, b.X1, b.Z1, BasementY, BasementCeiling, KitSlots.Concrete, null, null, gaps);
            // Basement walls' colliders (outside faces are buried).
            var t = 0.3f;
            Interiors.WallColliders(mb, new Vector3(b.X0, BasementY, b.Z0 - t * 0.5f), Vector3.right, Vector3.back, b.Width, BasementCeiling - BasementY, t, null, SurfaceType.Concrete);
            Interiors.WallColliders(mb, new Vector3(b.X1, BasementY, b.Z1 + t * 0.5f), Vector3.left, Vector3.forward, b.Width, BasementCeiling - BasementY, t,
                new[] { new WallGap(b.X1 - 345.0f, b.X1 - 343.0f, 0f, 2.2f) }, SurfaceType.Concrete);
            Interiors.WallColliders(mb, new Vector3(b.X0 - t * 0.5f, BasementY, b.Z1), Vector3.back, Vector3.left, b.Depth, BasementCeiling - BasementY, t, null, SurfaceType.Concrete);
            Interiors.WallColliders(mb, new Vector3(b.X1 + t * 0.5f, BasementY, b.Z0), Vector3.forward, Vector3.right, b.Depth, BasementCeiling - BasementY, t,
                new[] { new WallGap(s_eastStairZ.x - b.Z0, s_eastStairZ.y - b.Z0, 0f, BasementCeiling - BasementY) }, SurfaceType.Concrete);
            // East stair passage under the archive (walls and ceiling).
            Interiors.InwardRoom(mb, b.X1, s_eastStairZ.x - 0.1f, EastStairTopX + 0.2f, s_eastStairZ.y + 0.1f, BasementY, GroundY - 0.9f, KitSlots.Concrete, null, null,
                new IList<WallGap>[] { null, null, null, new[] { new WallGap(0f, s_eastStairZ.y - s_eastStairZ.x + 0.2f, 0f, BasementCeiling - BasementY) } });
            // Racks behind a mesh cage along z 356–358: the cage blocks the player (PlayerClip) but not bullets.
            mb.Slot(KitSlots.Iron);
            for (var x = 321.5f; x <= 340.5f; x += 1.9f) mb.AddTube(new Vector3(x, BasementY, 355.6f), new Vector3(x, BasementCeiling, 355.6f), 0.025f, 4);
            mb.AddTube(new Vector3(321.5f, BasementY + 2.2f, 355.6f), new Vector3(340.5f, BasementY + 2.2f, 355.6f), 0.025f, 4);
            mb.Slot(KitSlots.InteriorCard);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodAll;
            mb.AddQuad(new Vector3(321.5f, BasementY, 355.6f), new Vector3(321.5f, BasementY + 2.2f, 355.6f), new Vector3(329.3f, BasementY + 2.2f, 355.6f), new Vector3(329.3f, BasementY, 355.6f));
            mb.AddQuad(new Vector3(332.8f, BasementY, 355.6f), new Vector3(332.8f, BasementY + 2.2f, 355.6f), new Vector3(340.5f, BasementY + 2.2f, 355.6f), new Vector3(340.5f, BasementY, 355.6f));
            mb.LodMask = keep;
            mb.AddCollider(new Vector3(331f, BasementY + 1.5f, 355.6f), new Vector3(19f, 3f, 0.1f), SurfaceType.Default, LayerIds.PlayerClip, "RackCage");
            for (var i = 0; i < 4; i++)
            {
                var cx = i < 2 ? 323.75f + i * 3.5f : 334.75f + (i - 2) * 3.5f;
                anchors.Add("Rack." + (i + 1), new Vector3(cx, BasementY, 357.4f), 180f);
            }
            anchors.Add("TapeDeck", new Vector3(331.5f, BasementY + 0.8f, 357.2f), 180f);
            anchors.Add("Speaker", new Vector3(331.5f, -3.4f + 0.6f, b.Z1 - 0.05f), 180f);
            anchors.Add("OnAir", new Vector3(332f, -1.2f, b.Z1 - 0.05f), 180f);
            anchors.Add("Safe", new Vector3(b.X1 - 0.02f, BasementY + 0.5f, 342f), 270f);
            anchors.Add("ReceiverDesk", new Vector3(330.5f, BasementY, 348f), 0f);
            anchors.Add("Crate.0", new Vector3(322.5f, BasementY, 351f), 15f);
            anchors.Add("Crate.1", new Vector3(334.5f, BasementY, 352f), 80f);
            anchors.Add("Crate.2", new Vector3(326.5f, BasementY, 340.5f), 40f);
            anchors.Add("Crate.3", new Vector3(337.5f, BasementY, 344.5f), 0f);
            anchors.Add("Basement.Light", new Vector3(332f, BasementCeiling - 0.2f, 347f), 0f);
            anchors.Add("TunnelDoor.Hinge", new Vector3(343.0f, BasementY, b.Z1), 180f);
            // Cable trench below the south wall and the hatch (SC_B9_H).
            Interiors.InwardRoom(mb, 330.6f, 328f, 333.4f, 334.0f, -6.0f, -3.6f, KitSlots.Concrete, KitSlots.ConcreteFloor, KitSlots.Concrete,
                new IList<WallGap>[] { null, null, new[] { new WallGap(0.8f, 2.0f, 0f, 1.5f) }, null });
            Interiors.InwardRoom(mb, 331.2f, 334.0f, 332.8f, 335.2f, -6.0f, -4.5f, KitSlots.Concrete, KitSlots.ConcreteFloor, null, null);
            mb.AddCollider(new Vector3(332f, -6.15f, 331.6f), new Vector3(3f, 0.3f, 7.4f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(330.45f, -4.8f, 331.6f), new Vector3(0.3f, 2.4f, 7.4f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(333.55f, -4.8f, 331.6f), new Vector3(0.3f, 2.4f, 7.4f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(332f, -4.8f, 327.85f), new Vector3(3f, 2.4f, 0.3f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(332f, -3.45f, 331f), new Vector3(3f, 0.3f, 6f), SurfaceType.Concrete);
            mb.Slot(KitSlots.Iron);
            KitStructures.Ladder(mb, new Vector3(332f, -6.0f, 334.85f), new Vector3(332f, -4.2f, 334.85f), Vector3.back, 0.42f, KitSlots.Iron, false);
            anchors.Add("Hatch", new Vector3(332f, BasementY, 334.5f), 0f);
            anchors.Add("Trench.Spawn.0", new Vector3(332f, -6.0f, 330.0f), 0f);
            anchors.Add("Trench.Spawn.1", new Vector3(332f, -6.0f, 331.6f), 0f);
        }

        private static void Tunnel(MeshBuilder mb, StructureAnchors anchors)
        {
            const float tx0 = 342f;
            const float tx1 = 345f;
            const float tz0 = 360f;
            const float tz1 = TunnelEndZ;
            const float ceiling = -1.9f;
            Interiors.InwardRoom(mb, tx0, tz0, tx1, tz1, BasementY, ceiling, KitSlots.Concrete, KitSlots.ConcreteFloor, null,
                new IList<WallGap>[] { new[] { new WallGap(tx1 - 345.0f, tx1 - 343.0f, 0f, 2.2f) }, null, null, null });
            // Ceiling, open over the ladder shaft at the north end.
            mb.Slot(KitSlots.Concrete);
            mb.AddQuad(new Vector3(tx0, ceiling, tz1 - 1.8f), new Vector3(tx0, ceiling, tz0), new Vector3(tx1, ceiling, tz0), new Vector3(tx1, ceiling, tz1 - 1.8f));
            mb.AddQuad(new Vector3(tx0, ceiling, tz1), new Vector3(tx0, ceiling, tz1 - 1.8f), new Vector3(ShaftX.x, ceiling, tz1 - 1.8f), new Vector3(ShaftX.x, ceiling, tz1));
            mb.AddQuad(new Vector3(ShaftX.y, ceiling, tz1), new Vector3(ShaftX.y, ceiling, tz1 - 1.8f), new Vector3(tx1, ceiling, tz1 - 1.8f), new Vector3(tx1, ceiling, tz1));
            mb.AddCollider(new Vector3((tx0 + tx1) * 0.5f, BasementY - 0.15f, (tz0 + tz1) * 0.5f), new Vector3(tx1 - tx0, 0.3f, tz1 - tz0), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(tx0 - 0.15f, (BasementY + ceiling) * 0.5f, (tz0 + tz1) * 0.5f), new Vector3(0.3f, ceiling - BasementY, tz1 - tz0), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(tx1 + 0.15f, (BasementY + ceiling) * 0.5f, (tz0 + tz1) * 0.5f), new Vector3(0.3f, ceiling - BasementY, tz1 - tz0), SurfaceType.Concrete);
            mb.AddCollider(new Vector3((tx0 + tx1) * 0.5f, ceiling + 0.15f, (tz0 + tz1 - 1.8f) * 0.5f), new Vector3(tx1 - tx0, 0.3f, tz1 - tz0 - 1.8f), SurfaceType.Concrete);
            // Cable trays along both walls and bulkhead lamps.
            mb.Slot(KitSlots.Iron);
            for (var side = 0; side < 2; side++)
            {
                var x = side == 0 ? tx0 + 0.15f : tx1 - 0.15f;
                mb.AddBox(new Vector3(x, -2.9f, (tz0 + tz1) * 0.5f), new Vector3(0.25f, 0.04f, tz1 - tz0));
                mb.Slot(KitSlots.Cable);
                for (var k = 0; k < 3; k++) mb.AddTube(new Vector3(x, -2.85f + k * 0.03f, tz0), new Vector3(x, -2.85f + k * 0.03f, tz1), 0.018f, 4);
                mb.Slot(KitSlots.Iron);
            }
            for (var z = tz0 + 3f; z < tz1 - 1f; z += 6f) anchors.Add("Tunnel.Lamp." + Mathf.RoundToInt(z), new Vector3(tx1 - 0.1f, -2.4f, z), 270f);
            // Ladder shaft up to the yard behind the postern (hatch frame at ground level is part of the yard in S5).
            Interiors.InwardRoom(mb, ShaftX.x, tz1 - 1.8f, ShaftX.y, tz1, ceiling, 0f, KitSlots.Concrete, null, null, null);
            var lx = (ShaftX.x + ShaftX.y) * 0.5f;
            KitStructures.Ladder(mb, new Vector3(lx, BasementY, tz1 - 0.12f), new Vector3(lx, 0f, tz1 - 0.12f), Vector3.back, 0.45f, KitSlots.Iron, false);
            anchors.Add("Tunnel.LadderBottom", new Vector3(lx, BasementY, tz1 - 0.6f), 180f);
            anchors.Add("Tunnel.LadderTop", new Vector3(lx, 0f, tz1 - 0.8f), 0f);
        }
    }
}
