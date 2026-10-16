using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Interior building blocks for the enterable structures (cinema, Relay Hall, gatehouse, closets): inward-facing room
    /// shells, partition walls with doorways (both faces and through reveals), floor and ceiling slabs, and the matching
    /// colliders with openings carved out (<see cref="BoxCarve"/>). World coordinates.
    /// </summary>
    public static class Interiors
    {
        /// <summary>
        /// A wall from <paramref name="a"/> to <paramref name="b"/> (base points) of the given height and thickness, both
        /// faces built, gaps punched through with full-depth reveals, end caps optional, collider minus the gaps.
        /// <paramref name="frontSlot"/> is the face whose outward normal is up × (b − a); <paramref name="backSlot"/> the other.
        /// </summary>
        public static void Partition(MeshBuilder mb, Vector3 a, Vector3 b, float height, float thickness, IList<WallGap> gaps,
                                     string frontSlot, string backSlot, SurfaceType surface, bool caps = false)
        {
            var dir = b - a;
            dir.y = 0f;
            var len = dir.magnitude;
            if (len < 0.02f) return;
            dir /= len;
            var n = Vector3.Cross(Vector3.up, dir);
            var front = new WallFrame(a + n * (thickness * 0.5f), dir, len);
            var back = new WallFrame(b - n * (thickness * 0.5f), -dir, len, 7f);
            var fo = new List<WallOpening>();
            var bo = new List<WallOpening>();
            if (gaps != null)
                foreach (var g in gaps)
                {
                    var shape = g.Arched ? OpeningShape.RoundArch : OpeningShape.Rect;
                    fo.Add(WallOpening.Make(g.From, g.To, g.V0, g.V1, shape, thickness, false, g.Fill, 0, true));
                    bo.Add(WallOpening.Make(len - g.To, len - g.From, g.V0, g.V1, shape, 0.002f, false, OpeningFill.None, 0, true));
                }
            WallBuilder.Face(mb, front, 0f, height, fo, frontSlot, frontSlot, null);
            WallBuilder.Face(mb, back, 0f, height, bo, backSlot, backSlot, null);
            foreach (var o in fo)
                if (o.Fill != OpeningFill.None) KitFills.Fill(mb, front, o);
            if (caps)
            {
                KitTrims.EndCap(mb, front, 0f, true, 0f, height, thickness, frontSlot);
                KitTrims.EndCap(mb, front, len, false, 0f, height, thickness, frontSlot);
            }
            // Top of the wall when it does not reach a ceiling.
            mb.Slot(frontSlot);
            mb.AddQuad(front.P(0f, height, -thickness), front.P(0f, height, 0f), front.P(len, height, 0f), front.P(len, height, -thickness));
            WallColliders(mb, a, dir, n, len, height, thickness, gaps, surface);
        }

        /// <summary>Colliders of a straight wall minus its gaps.</summary>
        public static void WallColliders(MeshBuilder mb, Vector3 a, Vector3 dir, Vector3 n, float len, float height, float thickness, IList<WallGap> gaps, SurfaceType surface)
        {
            // Local frame: z along the wall, x across it (= n), y up.
            var frame = Matrix4x4.TRS(a, Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
            var whole = new Bounds(new Vector3(0f, height * 0.5f, len * 0.5f), new Vector3(thickness, height, len));
            var holes = new List<Bounds>();
            if (gaps != null)
                foreach (var g in gaps)
                {
                    var hb = new Bounds();
                    hb.SetMinMax(new Vector3(-thickness, g.V0, g.From), new Vector3(thickness, g.V1, g.To));
                    holes.Add(hb);
                }
            mb.Push(frame);
            foreach (var box in BoxCarve.SubtractAll(new[] { whole }, holes)) mb.AddCollider(box.center, box.size, surface);
            mb.Pop();
        }

        /// <summary>A horizontal slab (floor below, ceiling above) with a collider; optional holes (stairwells, hatches).</summary>
        public static void Slab(MeshBuilder mb, float x0, float z0, float x1, float z1, float topY, float thickness, string topSlot, string bottomSlot,
                                SurfaceType surface, IList<Rect> holes = null)
        {
            var rects = new List<Rect> { Rect.MinMaxRect(x0, z0, x1, z1) };
            if (holes != null)
                foreach (var h in holes)
                {
                    var next = new List<Rect>();
                    foreach (var r in rects) next.AddRange(SubtractRect(r, h));
                    rects = next;
                }
            foreach (var r in rects)
            {
                if (topSlot != null)
                {
                    mb.Slot(topSlot);
                    mb.AddQuad(new Vector3(r.xMin, topY, r.yMin), new Vector3(r.xMin, topY, r.yMax), new Vector3(r.xMax, topY, r.yMax), new Vector3(r.xMax, topY, r.yMin));
                }
                if (bottomSlot != null)
                {
                    mb.Slot(bottomSlot);
                    var by = topY - thickness;
                    mb.AddQuad(new Vector3(r.xMin, by, r.yMax), new Vector3(r.xMin, by, r.yMin), new Vector3(r.xMax, by, r.yMin), new Vector3(r.xMax, by, r.yMax));
                }
                mb.AddCollider(new Vector3(r.center.x, topY - thickness * 0.5f, r.center.y), new Vector3(r.width, thickness, r.height), surface);
            }
            // Edges of holes (slab thickness visible from below/in the stairwell).
            if (holes == null || bottomSlot == null) return;
            mb.Slot(bottomSlot);
            foreach (var h in holes)
            {
                var by = topY - thickness;
                mb.AddQuad(new Vector3(h.xMin, by, h.yMin), new Vector3(h.xMin, topY, h.yMin), new Vector3(h.xMax, topY, h.yMin), new Vector3(h.xMax, by, h.yMin));
                mb.AddQuad(new Vector3(h.xMax, by, h.yMax), new Vector3(h.xMax, topY, h.yMax), new Vector3(h.xMin, topY, h.yMax), new Vector3(h.xMin, by, h.yMax));
                mb.AddQuad(new Vector3(h.xMin, by, h.yMax), new Vector3(h.xMin, topY, h.yMax), new Vector3(h.xMin, topY, h.yMin), new Vector3(h.xMin, by, h.yMin));
                mb.AddQuad(new Vector3(h.xMax, by, h.yMin), new Vector3(h.xMax, topY, h.yMin), new Vector3(h.xMax, topY, h.yMax), new Vector3(h.xMax, by, h.yMax));
            }
        }

        /// <summary>Rectangle minus rectangle (XZ, as Rect x/y) into up to four rectangles.</summary>
        public static List<Rect> SubtractRect(Rect r, Rect h)
        {
            var result = new List<Rect>(4);
            if (!r.Overlaps(h))
            {
                result.Add(r);
                return result;
            }
            var ix0 = Mathf.Max(r.xMin, h.xMin);
            var ix1 = Mathf.Min(r.xMax, h.xMax);
            var iy0 = Mathf.Max(r.yMin, h.yMin);
            var iy1 = Mathf.Min(r.yMax, h.yMax);
            if (ix0 > r.xMin + 0.01f) result.Add(Rect.MinMaxRect(r.xMin, r.yMin, ix0, r.yMax));
            if (ix1 < r.xMax - 0.01f) result.Add(Rect.MinMaxRect(ix1, r.yMin, r.xMax, r.yMax));
            if (iy0 > r.yMin + 0.01f) result.Add(Rect.MinMaxRect(ix0, r.yMin, ix1, iy0));
            if (iy1 < r.yMax - 0.01f) result.Add(Rect.MinMaxRect(ix0, iy1, ix1, r.yMax));
            return result;
        }

        /// <summary>
        /// Inward-facing room shell (floor, ceiling, four walls) for spaces inside an exterior shell that already provides
        /// the outer faces. <paramref name="gapsBySide"/> (south, east, north, west; distances along each wall measured
        /// left to right as seen from inside) punch doorways and windows. No colliders: the shell's walls carry them.
        /// </summary>
        public static void InwardRoom(MeshBuilder mb, float x0, float z0, float x1, float z1, float y0, float y1, string wallSlot, string floorSlot,
                                      string ceilingSlot, IList<WallGap>[] gapsBySide, bool[] skipSides = null)
        {
            var frames = InwardFrames(x0, z0, x1, z1, y0);
            for (var side = 0; side < 4; side++)
            {
                if (skipSides != null && side < skipSides.Length && skipSides[side]) continue;
                var list = new List<WallOpening>();
                if (gapsBySide != null && side < gapsBySide.Length && gapsBySide[side] != null)
                    foreach (var g in gapsBySide[side])
                        list.Add(WallOpening.Make(g.From, g.To, y0 + g.V0, y0 + g.V1, g.Arched ? OpeningShape.RoundArch : OpeningShape.Rect, 0.01f, false, OpeningFill.None, 0, true));
                WallBuilder.Face(mb, Rebase(frames[side], y0), y0, y1, list, wallSlot, wallSlot, null);
            }
            if (floorSlot != null)
            {
                mb.Slot(floorSlot);
                mb.AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0));
            }
            if (ceilingSlot != null)
            {
                mb.Slot(ceilingSlot);
                mb.AddQuad(new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1));
            }
        }

        private static WallFrame Rebase(WallFrame f, float y0) => new WallFrame(new Vector3(f.Origin.x, 0f, f.Origin.z), f.U, f.Length, f.UvU);

        /// <summary>Inward-facing frames of a rectangle (south wall faces north, east faces west, north faces south, west faces east).</summary>
        public static WallFrame[] InwardFrames(float x0, float z0, float x1, float z1, float y)
        {
            return new[]
            {
                new WallFrame(new Vector3(x1, y, z0), Vector3.left, x1 - x0, 3f),
                new WallFrame(new Vector3(x1, y, z1), Vector3.back, z1 - z0, 9f),
                new WallFrame(new Vector3(x0, y, z1), Vector3.right, x1 - x0, 17f),
                new WallFrame(new Vector3(x0, y, z0), Vector3.forward, z1 - z0, 23f),
            };
        }

        /// <summary>Distance along an inward frame of a world point (for converting world door positions into gaps).</summary>
        public static float Along(WallFrame f, Vector3 p) => Vector3.Dot(p - f.Origin, f.U);
    }
}
