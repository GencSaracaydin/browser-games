using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Turns a <see cref="BuildingSpec"/> into geometry (TECH_PLAN §7.15 BuildingGenerator): facades with reveals and fills,
    /// arcades, trims per style, timber balconies, parapets and copings, roof decks and corrugated roofs, wear patches,
    /// drain spouts, rooftop clutter, and simplified colliders (one volume box, arcade piers, walkable roofs, PlayerClip
    /// lids). <see cref="BuildProxy"/> writes the cheap far-proxy shell (walls, window darks, parapet, roof) for
    /// <c>S9_TownFar</c>. Geometry is local to the footprint centre at the base height.
    /// </summary>
    public static class BuildingGeometry
    {
        /// <summary>Parapet wall thickness.</summary>
        public const float ParapetThickness = 0.3f;

        /// <summary>Local origin of a spec (footprint centre at the base height).</summary>
        public static Vector3 Origin(BuildingSpec spec) => new Vector3((spec.Footprint.X0 + spec.Footprint.X1) * 0.5f, spec.BaseY, (spec.Footprint.Z0 + spec.Footprint.Z1) * 0.5f);

        /// <summary>The four facade frames in local space (south, east, north, west).</summary>
        public static WallFrame[] Frames(BuildingSpec spec)
        {
            var hx = spec.Footprint.Width * 0.5f;
            var hz = spec.Footprint.Depth * 0.5f;
            var frames = WallFrame.Perimeter(-hx, -hz, hx, hz, 0f);
            var uvShift = (spec.Seed % 97) * 0.37f;
            foreach (var f in frames) f.UvU += uvShift;
            return frames;
        }

        /// <summary>Plaster slot of a building (tint), or the style's main wall material.</summary>
        public static string WallSlot(BuildingSpec spec)
        {
            switch (spec.Style)
            {
                case BuildingStyle.Sandstone: return KitSlots.Sandstone;
                case BuildingStyle.ArtDeco: return KitSlots.DecoRender;
                case BuildingStyle.Concrete: return KitSlots.Concrete;
                case BuildingStyle.Shed: return KitSlots.Corrugated;
                default: return KitSlots.Plaster(spec.Tint);
            }
        }

        /// <summary>Trim slot of a style.</summary>
        public static string TrimSlot(BuildingSpec spec)
        {
            switch (spec.Style)
            {
                case BuildingStyle.Colonial:
                case BuildingStyle.Sandstone: return KitSlots.SandstoneTrim;
                case BuildingStyle.ArtDeco: return KitSlots.DecoTrim;
                case BuildingStyle.Concrete: return KitSlots.Concrete;
                default: return KitSlots.PlasterRough;
            }
        }

        /// <summary>Surface type of the main walls.</summary>
        public static SurfaceType WallSurface(BuildingSpec spec)
        {
            switch (spec.Style)
            {
                case BuildingStyle.Sandstone: return SurfaceType.Stone;
                case BuildingStyle.Concrete:
                case BuildingStyle.ArtDeco: return SurfaceType.Concrete;
                case BuildingStyle.Shed: return SurfaceType.Metal;
                default: return SurfaceType.Plaster;
            }
        }

        /// <summary>Writes the full building (LOD0/LOD1 via the builder's LOD masks) and its colliders.</summary>
        public static void Build(BuildingSpec spec, MeshBuilder mb)
        {
            var frames = Frames(spec);
            var wallH = spec.WallHeight;
            var wallSlot = WallSlot(spec);
            var trim = TrimSlot(spec);
            var rng = new DetRandom(DetRandom.Mix((ulong)spec.Seed + 77UL, 3));
            mb.UvOffset = new Vector2((spec.Seed % 53) * 0.71f, (spec.Seed % 29) * 0.43f);

            var sideBands = new List<FacadeBand>[4];
            for (var side = 0; side < 4; side++)
            {
                var f = frames[side];
                var kind = spec.Facades[side];
                if (kind == FacadeKind.Party)
                {
                    var from = Mathf.Max(0f, spec.PartyHeights[side]);
                    if (from < wallH - 0.05f) WallBuilder.Face(mb, f, from, wallH, null, wallSlot, wallSlot, KitSlots.InteriorCard);
                    continue;
                }
                var bands = FacadeComposer.Compose(spec, side, f.Length);
                sideBands[side] = bands;
                foreach (var band in bands)
                {
                    if (band.IsArcade) Arcade(mb, spec, f, band, rng);
                    else
                    {
                        WallBuilder.Face(mb, f, band.V0, band.V1, band.Openings, wallSlot, wallSlot, KitSlots.InteriorCard);
                        foreach (var o in band.Openings) OpeningTrims(mb, spec, f, o, trim);
                    }
                    foreach (var span in band.Balconies) KitStructures.Balcony(mb, f, span.x, span.y, band.V0, rng.Range(0.95f, 1.25f), spec.Seed + side * 31 + band.Storey);
                }
                FacadeTrims(mb, spec, f, kind, bands, trim, rng);
                if (spec.Wear > 0f) WearPatches(mb, spec, f, bands, rng);
                foreach (var feat in spec.Features)
                {
                    if (feat.Side != side || feat.Tag == null || !feat.Tag.EndsWith("Loggia")) continue;
                    // Timber balustrade across the loggia opening (E3-05's balcony, GDD §7.3).
                    mb.Slot(KitSlots.WoodPainted);
                    KitTrims.FrameBox(mb, f, feat.U0, feat.U1, feat.V0, feat.V0 + 0.08f, -0.25f, 0.05f);
                    KitTrims.FrameBox(mb, f, feat.U0, feat.U1, feat.V0 + 0.92f, feat.V0 + 1.0f, -0.25f, 0.05f);
                    var keep = mb.LodMask;
                    mb.LodMask = MeshBuilder.LodDetail;
                    for (var u = feat.U0 + 0.12f; u < feat.U1 - 0.06f; u += 0.14f)
                        KitTrims.FrameBox(mb, f, u - 0.02f, u + 0.02f, feat.V0 + 0.08f, feat.V0 + 0.92f, -0.12f, -0.08f);
                    mb.LodMask = keep;
                }
            }

            foreach (var room in spec.Rooms) RoomInterior(mb, spec, frames, sideBands, room);

            if (spec.Roof == RoofKind.FlatParapet) ParapetAndRoof(mb, spec, frames, wallH, trim, rng);
            else if (spec.Roof == RoofKind.Corrugated) CorrugatedRoof(mb, spec, wallH);

            Colliders(mb, spec, wallH, frames, sideBands);
            if (spec.RoofClutter && spec.Roof == RoofKind.FlatParapet) RoofClutter(mb, spec, wallH, rng);
        }

        // ------------------------------------------------------------------ arcade

        private static void Arcade(MeshBuilder mb, BuildingSpec spec, WallFrame f, FacadeBand band, DetRandom rng)
        {
            var wallSlot = WallSlot(spec);
            var trim = TrimSlot(spec);
            var n = FacadeComposer.Bays(f.Length, new DetRandom((ulong)spec.Seed * 31UL + 5UL), out var bay);
            var pier = Mathf.Clamp(bay * 0.22f, 0.55f, 0.8f);
            var h = band.V1 - band.V0;
            var apex = band.V0 + h - 0.35f;
            var depth = spec.ArcadeDepth;
            var arches = new List<WallOpening>();
            for (var i = 0; i < n; i++)
            {
                var u0 = i * bay + pier * 0.5f;
                var u1 = (i + 1) * bay - pier * 0.5f;
                arches.Add(WallOpening.Make(u0, u1, band.V0, apex, OpeningShape.RoundArch, 0.45f, false, OpeningFill.None, i, true));
            }
            // Front plane with the arches, archivolts with keystones on colonial/sandstone buildings.
            WallBuilder.Face(mb, f, band.V0, band.V1, arches, wallSlot, wallSlot, null);
            if (spec.Style != BuildingStyle.Vernacular)
                foreach (var a in arches) KitTrims.Surround(mb, f, a, 0.16f, 0.05f, trim, true);
            // Pier back faces (inside the arcade) and the soffit.
            var inner = new WallFrame(f.Origin - f.N * depth, f.U, f.Length, f.UvU + 11.3f);
            mb.Slot(KitSlots.PlasterRough);
            mb.AddQuad(f.P(0f, band.V1 - 0.02f, -depth), f.P(0f, band.V1 - 0.02f, -0.45f), f.P(f.Length, band.V1 - 0.02f, -0.45f), f.P(f.Length, band.V1 - 0.02f, -depth));
            for (var i = 0; i <= n; i++)
            {
                var uc = i * bay;
                var a0 = Mathf.Max(0f, uc - pier * 0.5f);
                var a1 = Mathf.Min(f.Length, uc + pier * 0.5f);
                // Back face of the pier (facing into the arcade).
                mb.Slot(wallSlot);
                mb.AddQuad(f.P(a1, band.V0, -0.45f), f.P(a1, band.V1, -0.45f), f.P(a0, band.V1, -0.45f), f.P(a0, band.V0, -0.45f));
            }
            // Arcade end returns (closed: neighbours' arcades are separate).
            mb.Slot(wallSlot);
            mb.AddQuad(f.P(0f, band.V0, -0.45f), f.P(0f, band.V1, -0.45f), f.P(0f, band.V1, -depth), f.P(0f, band.V0, -depth));
            mb.AddQuad(f.P(f.Length, band.V0, -depth), f.P(f.Length, band.V1, -depth), f.P(f.Length, band.V1, -0.45f), f.P(f.Length, band.V0, -0.45f));
            // Arcade floor (paving continues under the arches).
            mb.Slot(KitSlots.Paving);
            mb.AddQuad(f.P(0f, band.V0 + 0.01f, 0f), f.P(0f, band.V0 + 0.01f, -depth), f.P(f.Length, band.V0 + 0.01f, -depth), f.P(f.Length, band.V0 + 0.01f, 0f));
            // Inner wall: shopfronts, roller shutters and a house door.
            var innerOpenings = new List<WallOpening>();
            var doorBay = n > 2 ? rng.Range(0, n) : -1;
            for (var i = 0; i < n; i++)
            {
                var c = (i + 0.5f) * bay;
                if (i == doorBay)
                {
                    innerOpenings.Add(WallOpening.Make(c - 0.55f, c + 0.55f, band.V0, band.V0 + 2.2f, OpeningShape.Rect, 0.3f, true, OpeningFill.Door, rng.Range(0, 9999), true));
                    continue;
                }
                var w = Mathf.Min(bay - 0.5f, rng.Range(2.0f, 2.6f));
                innerOpenings.Add(WallOpening.Make(c - w * 0.5f, c + w * 0.5f, band.V0, band.V0 + Mathf.Min(h - 0.9f, 2.6f), OpeningShape.Rect, 0.3f, true,
                    rng.Chance(0.75f) ? OpeningFill.RollerShutter : OpeningFill.Shopfront, rng.Range(0, 9999), true));
            }
            WallBuilder.Face(mb, inner, band.V0, band.V1, innerOpenings, wallSlot, wallSlot, KitSlots.InteriorCard);
            foreach (var o in innerOpenings) KitFills.Fill(mb, inner, o);
            // Colliders: piers (the volume box is inset behind the arcade).
            for (var i = 0; i <= n; i++)
            {
                var uc = Mathf.Clamp(i * bay, pier * 0.5f, f.Length - pier * 0.5f);
                mb.Push(f.Matrix);
                mb.AddCollider(new Vector3(uc, band.V0 + h * 0.5f, 0.225f), new Vector3(pier, h, 0.45f), WallSurface(spec));
                mb.Pop();
            }
            mb.Push(f.Matrix);
            mb.AddCollider(new Vector3(f.Length * 0.5f, band.V0 - 0.1f, depth * 0.5f), new Vector3(f.Length, 0.2f, depth), SurfaceType.Stone, LayerIds.Default, "ArcadeFloor");
            mb.Pop();
        }

        // ------------------------------------------------------------------ trims

        private static void OpeningTrims(MeshBuilder mb, BuildingSpec spec, WallFrame f, WallOpening o, string trim)
        {
            KitFills.Fill(mb, f, o);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            switch (spec.Style)
            {
                case BuildingStyle.Vernacular:
                    if (o.Shape == OpeningShape.Rect && o.Fill != OpeningFill.RollerShutter && o.Fill != OpeningFill.Shopfront) KitTrims.TimberLintel(mb, f, o, KitSlots.Wood);
                    if (!o.NoSill) KitTrims.Sill(mb, f, o, KitSlots.PlasterRough, 0.05f, 0.05f);
                    break;
                case BuildingStyle.Colonial:
                case BuildingStyle.Sandstone:
                    if (!o.NoSill) KitTrims.Sill(mb, f, o, trim);
                    if (o.Fill == OpeningFill.ShutteredWindow || o.Fill == OpeningFill.BalconyDoor || o.Fill == OpeningFill.Door)
                        KitTrims.Surround(mb, f, o, 0.12f, 0.03f, trim, o.Shape != OpeningShape.Rect);
                    break;
                default:
                    if (!o.NoSill) KitTrims.Sill(mb, f, o, trim, 0.04f, 0.05f);
                    break;
            }
            mb.LodMask = keep;
        }

        private static void FacadeTrims(MeshBuilder mb, BuildingSpec spec, WallFrame f, FacadeKind kind, List<FacadeBand> bands, string trim, DetRandom rng)
        {
            var arcade = kind == FacadeKind.Arcade;
            switch (spec.Style)
            {
                case BuildingStyle.Vernacular:
                    if (!arcade) KitTrims.Plinth(mb, f, rng.Range(0.45f, 0.75f), 0.025f, KitSlots.PlasterBase);
                    break;
                case BuildingStyle.Colonial:
                case BuildingStyle.Sandstone:
                    if (!arcade) KitTrims.Plinth(mb, f, 0.55f, 0.04f, KitSlots.Sandstone);
                    for (var i = 1; i < bands.Count; i++) KitTrims.StringCourse(mb, f, bands[i].V0 - 0.05f, 0.14f, 0.05f, trim);
                    KitTrims.Cornice(mb, f, spec.WallHeight - 0.3f, trim);
                    break;
                default:
                    KitTrims.Plinth(mb, f, 0.4f, 0.03f, trim);
                    break;
            }
            // Drain spouts at roof level on street and side facades.
            if (spec.Roof == RoofKind.FlatParapet && spec.Style == BuildingStyle.Vernacular && kind != FacadeKind.Party)
            {
                var count = Mathf.Max(1, Mathf.FloorToInt(f.Length / 4.5f));
                for (var i = 0; i < count; i++)
                {
                    var u = (i + 0.5f) * f.Length / count + rng.Range(-0.4f, 0.4f);
                    KitTrims.Spout(mb, f, u, spec.WallHeight + 0.05f, rng.Range(0.45f, 0.7f), KitSlots.Wood);
                }
            }
            // Street clutter on house fronts: meter boxes and cables.
            if ((kind == FacadeKind.Residential || kind == FacadeKind.Shops) && rng.Chance(0.6f)) KitStreet.MeterBox(mb, f, rng.Range(0.4f, f.Length - 0.4f), 1.2f);
        }

        private static void WearPatches(MeshBuilder mb, BuildingSpec spec, WallFrame f, List<FacadeBand> bands, DetRandom rng)
        {
            if (spec.Style != BuildingStyle.Vernacular && spec.Style != BuildingStyle.Colonial) return;
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.Mudbrick);
            var count = Mathf.RoundToInt(spec.Wear * f.Length / 4f * rng.Range(0.5f, 1.5f));
            for (var k = 0; k < count; k++)
            {
                var w = rng.Range(0.35f, 1.4f);
                var h = rng.Range(0.25f, 0.9f);
                var u = rng.Range(0.3f, Mathf.Max(0.4f, f.Length - w - 0.3f));
                var v = rng.Value01() < 0.6f ? rng.Range(0.1f, 1.2f) : rng.Range(1.2f, Mathf.Max(1.3f, spec.WallHeight - h - 0.2f));
                if (Overlaps(bands, u - 0.1f, u + w + 0.1f, v - 0.1f, v + h + 0.1f)) continue;
                // Irregular chipped outline (convex octagon with jittered corners), 4 mm proud of the render.
                var pts = new Vector3[8];
                for (var i = 0; i < 8; i++)
                {
                    var a = -i / 8f * Mathf.PI * 2f;
                    var ru = (Mathf.Cos(a) * 0.5f + 0.5f) * w + rng.Range(-0.06f, 0.06f) * w;
                    var rv = (Mathf.Sin(a) * 0.5f + 0.5f) * h + rng.Range(-0.08f, 0.08f) * h;
                    pts[i] = f.P(u + ru, v + rv, 0.004f);
                }
                mb.AddConvexPolygon(pts);
            }
            mb.LodMask = keep;
        }

        private static bool Overlaps(List<FacadeBand> bands, float u0, float u1, float v0, float v1)
        {
            foreach (var b in bands)
            {
                if (b.IsArcade && v0 < b.V1) return true;
                foreach (var o in b.Openings)
                    if (u1 > o.U0 - 0.3f && u0 < o.U1 + 0.3f && v1 > o.V0 - 0.2f && v0 < o.V1 + 0.35f) return true;
                foreach (var s in b.Balconies)
                    if (u1 > s.x && u0 < s.y && v1 > b.V0 - 1f && v0 < b.V0 + 1.2f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ roofs

        private static void ParapetAndRoof(MeshBuilder mb, BuildingSpec spec, WallFrame[] frames, float wallH, string trim, DetRandom rng)
        {
            var wallSlot = WallSlot(spec);
            var top = wallH + spec.Parapet;
            var hx = spec.Footprint.Width * 0.5f;
            var hz = spec.Footprint.Depth * 0.5f;
            var t = ParapetThickness;
            for (var side = 0; side < 4; side++)
            {
                var f = frames[side];
                var partyAbove = spec.Facades[side] == FacadeKind.Party && spec.PartyHeights[side] >= top - 0.05f;
                if (partyAbove) continue;
                var from = spec.Facades[side] == FacadeKind.Party ? Mathf.Max(wallH, spec.PartyHeights[side]) : wallH;
                if (spec.Parapet > 0.05f)
                {
                    WallBuilder.Face(mb, f, from, top, null, wallSlot, wallSlot, null);
                    // Inner face of the parapet (seen from the roof and from the flight).
                    var innerF = new WallFrame(f.Origin + f.U * f.Length - f.N * t, -f.U, f.Length, f.UvU + 5f);
                    mb.Slot(wallSlot);
                    WallBuilder.Quad(mb, innerF, t, f.Length - t, wallH, top);
                }
            }
            if (spec.Parapet > 0.05f) KitTrims.Coping(mb, -hx, -hz, hx, hz, top, t, 0.04f, 0.07f, spec.Style == BuildingStyle.Vernacular ? KitSlots.PlasterRough : trim);
            // Roof deck.
            mb.Slot(spec.Style == BuildingStyle.Vernacular ? KitSlots.RoofDeck : KitSlots.ConcreteFloor);
            var inset = spec.Parapet > 0.05f ? t : 0f;
            mb.AddQuad(new Vector3(-hx + inset, wallH, -hz + inset), new Vector3(-hx + inset, wallH, hz - inset), new Vector3(hx - inset, wallH, hz - inset), new Vector3(hx - inset, wallH, -hz + inset));
            if (spec.Parapet <= 0.05f)
            {
                mb.Slot(trim);
                KitTrims.Coping(mb, -hx, -hz, hx, hz, wallH - 0.05f, 0.05f, 0.03f, 0.08f, trim);
            }
        }

        private static void CorrugatedRoof(MeshBuilder mb, BuildingSpec spec, float wallH)
        {
            var hx = spec.Footprint.Width * 0.5f + 0.35f;
            var hz = spec.Footprint.Depth * 0.5f + 0.35f;
            // Mono-pitch falling to the south (rain is rare; it sheds the dust), 0.5 m over the depth.
            var high = wallH + 0.55f;
            var low = wallH + 0.05f;
            mb.Slot(KitSlots.Corrugated);
            mb.AddQuad(new Vector3(-hx, low, -hz), new Vector3(-hx, high, hz), new Vector3(hx, high, hz), new Vector3(hx, low, -hz));
            mb.AddQuad(new Vector3(hx, low - 0.03f, -hz), new Vector3(hx, high - 0.03f, hz), new Vector3(-hx, high - 0.03f, hz), new Vector3(-hx, low - 0.03f, -hz));
            // Gable infill triangles on east and west walls.
            mb.Slot(WallSlot(spec));
            var wx = spec.Footprint.Width * 0.5f;
            var wz = spec.Footprint.Depth * 0.5f;
            mb.AddTriangle(new Vector3(wx, wallH, -wz), new Vector3(wx, wallH + 0.5f, wz), new Vector3(wx, wallH, wz));
            mb.AddTriangle(new Vector3(-wx, wallH, wz), new Vector3(-wx, wallH + 0.5f, wz), new Vector3(-wx, wallH, -wz));
            mb.AddQuad(new Vector3(wx, wallH, wz), new Vector3(wx, wallH + 0.5f, wz), new Vector3(-wx, wallH + 0.5f, wz), new Vector3(-wx, wallH, wz));
            // Purlins under the eaves.
            mb.Slot(KitSlots.Wood);
            mb.AddBox(new Vector3(0f, low - 0.08f, -hz + 0.1f), new Vector3(hx * 2f, 0.1f, 0.1f));
            mb.AddCollider(new Vector3(0f, (low + high) * 0.5f, 0f), new Vector3(hx * 2f, 0.2f, hz * 2f), SurfaceType.Metal);
        }

        private static void RoofClutter(MeshBuilder mb, BuildingSpec spec, float wallH, DetRandom rng)
        {
            var hx = spec.Footprint.Width * 0.5f - 0.8f;
            var hz = spec.Footprint.Depth * 0.5f - 0.8f;
            if (hx < 1f || hz < 1f) return;
            var area = spec.Footprint.Area;
            if (rng.Chance(spec.WalkableRoof ? 0f : 0.55f))
                KitRoofProps.WaterTank(mb, new Vector3(rng.Range(-hx + 0.8f, hx - 0.8f), wallH, rng.Range(-hz + 0.8f, hz - 0.8f)), rng.Range(0.3f, 1.4f), spec.Seed + 1, rng.Range(0f, 90f));
            if (rng.Chance(0.6f)) KitRoofProps.TvAntenna(mb, new Vector3(rng.Range(-hx, hx), wallH, rng.Range(-hz, hz)), rng.Range(2.5f, 4.5f), spec.Seed + 2);
            if (area > 50f && rng.Chance(0.5f) && !spec.WalkableRoof)
            {
                var a = new Vector3(-hx + 0.2f, wallH + 1.7f, rng.Range(-hz * 0.6f, hz * 0.6f));
                var b = new Vector3(hx - 0.2f, wallH + 1.7f, a.z + rng.Range(-0.6f, 0.6f));
                KitRoofProps.LaundryLine(mb, a, b, spec.Seed + 3, true);
            }
            if (area > 60f && rng.Chance(0.35f) && !spec.WalkableRoof)
                KitRoofProps.StairHut(mb, new Vector3(hx - 1.3f, wallH, hz - 1.3f), rng.Chance(0.5f) ? 0f : 180f, WallSlot(spec), spec.Seed + 4);
        }

        // ------------------------------------------------------------------ colliders

        private static void Colliders(MeshBuilder mb, BuildingSpec spec, float wallH, WallFrame[] frames, List<FacadeBand>[] sideBands)
        {
            var hx = spec.Footprint.Width * 0.5f;
            var hz = spec.Footprint.Depth * 0.5f;
            // Inset the ground storey behind arcades.
            float sx0 = -hx, sx1 = hx, sz0 = -hz, sz1 = hz;
            var hasArcade = false;
            if (spec.Facades[0] == FacadeKind.Arcade) { sz0 += spec.ArcadeDepth; hasArcade = true; }
            if (spec.Facades[1] == FacadeKind.Arcade) { sx1 -= spec.ArcadeDepth; hasArcade = true; }
            if (spec.Facades[2] == FacadeKind.Arcade) { sz1 -= spec.ArcadeDepth; hasArcade = true; }
            if (spec.Facades[3] == FacadeKind.Arcade) { sx0 += spec.ArcadeDepth; hasArcade = true; }
            var surface = WallSurface(spec);
            var boxes = new List<Bounds>();
            var groundH = hasArcade ? spec.Storeys[0] : 0f;
            if (groundH > 0f)
            {
                boxes.Add(MinMax(new Vector3(sx0, 0f, sz0), new Vector3(sx1, groundH, sz1)));
                boxes.Add(MinMax(new Vector3(-hx, groundH, -hz), new Vector3(hx, wallH, hz)));
            }
            else boxes.Add(MinMax(new Vector3(-hx, 0f, -hz), new Vector3(hx, wallH, hz)));

            // Rooms and the through openings that lead into them are cut out of the volume.
            var holes = new List<Bounds>();
            var origin = Origin(spec);
            foreach (var room in spec.Rooms)
            {
                var r0 = new Vector3(room.Rect.X0 - origin.x, room.FloorY - spec.BaseY, room.Rect.Z0 - origin.z);
                var r1 = new Vector3(room.Rect.X1 - origin.x, room.CeilingY - spec.BaseY, room.Rect.Z1 - origin.z);
                holes.Add(MinMax(r0, r1));
                for (var side = 0; side < 4; side++)
                {
                    if (sideBands[side] == null) continue;
                    var f = frames[side];
                    foreach (var band in sideBands[side])
                    foreach (var o in band.Openings)
                    {
                        if (o.Blind) continue;
                        var a = f.P(o.U0, o.V0, 0.1f);
                        var b = f.P(o.U1, o.V1, -1.0f);
                        var hole = MinMax(Vector3.Min(a, b), Vector3.Max(a, b));
                        if (BoxCarve.Intersects(hole, MinMax(r0 - Vector3.one * 1.1f, r1 + Vector3.one * 1.1f))) holes.Add(hole);
                    }
                }
            }
            foreach (var b in BoxCarve.SubtractAll(boxes, holes)) mb.AddCollider(b.center, b.size, surface);

            if (spec.Roof == RoofKind.FlatParapet && spec.Parapet > 0.05f)
            {
                var top = wallH + spec.Parapet;
                var t = ParapetThickness;
                var py = (wallH + top) * 0.5f;
                var ph = top - wallH;
                mb.AddCollider(new Vector3(0f, py, -hz + t * 0.5f), new Vector3(hx * 2f, ph, t), surface);
                mb.AddCollider(new Vector3(0f, py, hz - t * 0.5f), new Vector3(hx * 2f, ph, t), surface);
                mb.AddCollider(new Vector3(-hx + t * 0.5f, py, 0f), new Vector3(t, ph, hz * 2f), surface);
                mb.AddCollider(new Vector3(hx - t * 0.5f, py, 0f), new Vector3(t, ph, hz * 2f), surface);
            }
            if (spec.RoofClip)
                mb.AddCollider(new Vector3(0f, wallH + 1.6f, 0f), new Vector3(hx * 2f + 0.4f, 3.2f, hz * 2f + 0.4f), SurfaceType.Default, LayerIds.PlayerClip, "RoofClip");
        }

        private static Bounds MinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        // ------------------------------------------------------------------ rooms

        private static void RoomInterior(MeshBuilder mb, BuildingSpec spec, WallFrame[] frames, List<FacadeBand>[] sideBands, InteriorRoom room)
        {
            var o = Origin(spec);
            var x0 = room.Rect.X0 - o.x;
            var x1 = room.Rect.X1 - o.x;
            var z0 = room.Rect.Z0 - o.z;
            var z1 = room.Rect.Z1 - o.z;
            var y0 = room.FloorY - spec.BaseY;
            var y1 = room.CeilingY - spec.BaseY;
            var hx = spec.Footprint.Width * 0.5f;
            var hz = spec.Footprint.Depth * 0.5f;
            // Floor and ceiling.
            mb.Slot(KitSlots.Linoleum);
            mb.AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0));
            mb.Slot(KitSlots.PlasterRough);
            mb.AddQuad(new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1));
            // Walls, facing into the room. A wall that lies on the facade (within a wall thickness) is built with the facade's
            // openings so the loggia or doorway shows through.
            var walls = new[]
            {
                new WallFrame(new Vector3(x1, y0, z0), Vector3.left, x1 - x0),   // south wall, seen from inside (faces north)
                new WallFrame(new Vector3(x1, y0, z1), Vector3.back, z1 - z0),   // east wall (faces west)
                new WallFrame(new Vector3(x0, y0, z1), Vector3.right, x1 - x0),  // north wall (faces south)
                new WallFrame(new Vector3(x0, y0, z0), Vector3.forward, z1 - z0), // west wall (faces east)
            };
            var onFacade = new[] { z0 - -hz < 0.7f, hx - x1 < 0.7f, hz - z1 < 0.7f, x0 - -hx < 0.7f };
            for (var side = 0; side < 4; side++)
            {
                var w = walls[side];
                var openings = new List<WallOpening>();
                if (onFacade[side] && sideBands[side] != null)
                {
                    // Facade frame u for this side runs the other way round than the room wall seen from inside.
                    var facade = frames[side];
                    foreach (var band in sideBands[side])
                    foreach (var op in band.Openings)
                    {
                        if (op.Blind) continue;
                        var a = facade.P(op.U0, op.V0, 0f);
                        var b = facade.P(op.U1, op.V0, 0f);
                        var ua = Vector3.Dot(a - w.Origin, w.U);
                        var ub = Vector3.Dot(b - w.Origin, w.U);
                        var m = op;
                        m.U0 = Mathf.Min(ua, ub);
                        m.U1 = Mathf.Max(ua, ub);
                        m.V0 = op.V0 - y0;
                        m.V1 = op.V1 - y0;
                        m.Depth = 0.01f;
                        m.Blind = false;
                        if (m.U1 > 0f && m.U0 < w.Length && m.V1 > 0f && m.V0 < y1 - y0) openings.Add(m);
                    }
                }
                var local = new WallFrame(w.Origin, w.U, w.Length, w.UvU);
                WallBuilder.Face(mb, Offset(local, -y0), y0, y1, Shift(openings, y0), KitSlots.PlasterRough, KitSlots.PlasterRough, null);
            }
        }

        private static WallFrame Offset(WallFrame f, float dy) => new WallFrame(f.Origin + Vector3.up * dy, f.U, f.Length, f.UvU);

        private static List<WallOpening> Shift(List<WallOpening> list, float dy)
        {
            var r = new List<WallOpening>(list.Count);
            foreach (var o in list)
            {
                var m = o;
                m.V0 += dy;
                m.V1 += dy;
                r.Add(m);
            }
            return r;
        }

        // ------------------------------------------------------------------ far proxy

        /// <summary>
        /// The far-proxy shell of a building for <c>S9_TownFar</c> (TECH_PLAN D7): flat facades with dark window quads
        /// (no reveals), arcade recesses as darks, the parapet as one band, the roof deck, a water tank silhouette.
        /// Same slots as the full build; the editor maps them to Baked Lit proxy materials.
        /// </summary>
        public static void BuildProxy(BuildingSpec spec, MeshBuilder mb)
        {
            var frames = Frames(spec);
            var wallH = spec.WallHeight;
            var wallSlot = WallSlot(spec);
            var top = spec.Roof == RoofKind.FlatParapet ? wallH + spec.Parapet : wallH;
            mb.UvOffset = new Vector2((spec.Seed % 53) * 0.71f, (spec.Seed % 29) * 0.43f);
            for (var side = 0; side < 4; side++)
            {
                var f = frames[side];
                var kind = spec.Facades[side];
                var from = kind == FacadeKind.Party ? Mathf.Max(0f, spec.PartyHeights[side]) : 0f;
                if (from >= top - 0.05f) continue;
                mb.Slot(wallSlot);
                WallBuilder.Quad(mb, f, 0f, f.Length, from, top);
                if (kind == FacadeKind.Party) continue;
                mb.Slot(KitSlots.ProxyWindow);
                foreach (var band in FacadeComposer.Compose(spec, side, f.Length))
                {
                    if (band.IsArcade)
                    {
                        var n = FacadeComposer.Bays(f.Length, new DetRandom((ulong)spec.Seed * 31UL + 5UL), out var bay);
                        var pier = Mathf.Clamp(bay * 0.22f, 0.55f, 0.8f);
                        for (var i = 0; i < n; i++)
                            WallBuilder.Quad(mb, f, i * bay + pier * 0.5f, (i + 1) * bay - pier * 0.5f, band.V0, band.V1 - 0.6f, 0.01f);
                        continue;
                    }
                    foreach (var o in band.Openings)
                    {
                        var vtop = o.Shape == OpeningShape.Rect ? o.V1 : o.Springline + (o.V1 - o.Springline) * 0.6f;
                        WallBuilder.Quad(mb, f, o.U0, o.U1, o.V0, vtop, 0.01f);
                    }
                }
            }
            var hx = spec.Footprint.Width * 0.5f;
            var hz = spec.Footprint.Depth * 0.5f;
            mb.Slot(spec.Style == BuildingStyle.Vernacular ? KitSlots.RoofDeck : KitSlots.ConcreteFloor);
            if (spec.Roof == RoofKind.Corrugated)
            {
                mb.Slot(KitSlots.Corrugated);
                mb.AddQuad(new Vector3(-hx - 0.3f, wallH + 0.05f, -hz - 0.3f), new Vector3(-hx - 0.3f, wallH + 0.55f, hz + 0.3f), new Vector3(hx + 0.3f, wallH + 0.55f, hz + 0.3f), new Vector3(hx + 0.3f, wallH + 0.05f, -hz - 0.3f));
                return;
            }
            var inset = spec.Parapet > 0.05f ? ParapetThickness : 0f;
            mb.AddQuad(new Vector3(-hx + inset, wallH, -hz + inset), new Vector3(-hx + inset, wallH, hz - inset), new Vector3(hx - inset, wallH, hz - inset), new Vector3(hx - inset, wallH, -hz + inset));
            if (inset > 0f)
            {
                // Parapet: top ring as four strips and the inner faces down to the deck.
                mb.Slot(wallSlot);
                var t = inset;
                mb.AddQuad(new Vector3(-hx, top, -hz), new Vector3(-hx, top, -hz + t), new Vector3(hx, top, -hz + t), new Vector3(hx, top, -hz));
                mb.AddQuad(new Vector3(-hx, top, hz - t), new Vector3(-hx, top, hz), new Vector3(hx, top, hz), new Vector3(hx, top, hz - t));
                mb.AddQuad(new Vector3(-hx, top, -hz + t), new Vector3(-hx, top, hz - t), new Vector3(-hx + t, top, hz - t), new Vector3(-hx + t, top, -hz + t));
                mb.AddQuad(new Vector3(hx - t, top, -hz + t), new Vector3(hx - t, top, hz - t), new Vector3(hx, top, hz - t), new Vector3(hx, top, -hz + t));
                foreach (var f in frames)
                {
                    var innerF = new WallFrame(f.Origin + f.U * f.Length - f.N * t, -f.U, f.Length, f.UvU + 5f);
                    WallBuilder.Quad(mb, innerF, t, f.Length - t, wallH, top);
                }
            }
        }
    }
}
