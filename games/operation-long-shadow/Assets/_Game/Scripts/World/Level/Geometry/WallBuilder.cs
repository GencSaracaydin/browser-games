using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Builds a wall face with openings (rectangles, round and segmental arches) by strip decomposition, plus the reveals
    /// (jambs, sills, heads, arch soffits) and dark interior cards of blind openings (TECH_PLAN §7.15 kit: "windows/doors,
    /// colonial round arches (never pointed)"). Openings in one call must not overlap along u. Texture coordinates follow
    /// the frame (continuous around a building).
    /// </summary>
    public static class WallBuilder
    {
        /// <summary>Segments of a semicircular arch.</summary>
        public const int ArchSegments = 12;

        /// <summary>
        /// Writes the outer face of <paramref name="frame"/> between heights <paramref name="v0"/> and <paramref name="v1"/> and
        /// from <paramref name="u0"/> to <paramref name="u1"/> (defaults to the whole wall), punched by <paramref name="openings"/>.
        /// </summary>
        public static void Face(MeshBuilder mb, WallFrame frame, float v0, float v1, IReadOnlyList<WallOpening> openings,
                                string faceSlot, string revealSlot, string cardSlot, float u0 = 0f, float u1 = -1f)
        {
            if (u1 < 0f) u1 = frame.Length;
            if (v1 <= v0 || u1 <= u0) return;
            var sorted = new List<WallOpening>();
            if (openings != null)
                foreach (var o in openings)
                    if (o.U1 > u0 + 1e-3f && o.U0 < u1 - 1e-3f && o.V1 > v0 + 1e-3f && o.V0 < v1 - 1e-3f) sorted.Add(o);
            sorted.Sort((a, b) => a.U0.CompareTo(b.U0));

            mb.Slot(faceSlot);
            var cursor = u0;
            foreach (var o in sorted)
            {
                var ou0 = Mathf.Max(o.U0, u0);
                var ou1 = Mathf.Min(o.U1, u1);
                if (ou0 > cursor + 1e-4f) Quad(mb, frame, cursor, ou0, v0, v1);
                var ov0 = Mathf.Max(o.V0, v0);
                if (ov0 > v0 + 1e-4f) Quad(mb, frame, ou0, ou1, v0, ov0);
                var apex = Mathf.Min(o.V1, v1);
                if (o.Shape == OpeningShape.Rect || o.V1 > v1)
                {
                    if (apex < v1 - 1e-4f) Quad(mb, frame, ou0, ou1, apex, v1);
                }
                else
                {
                    Spandrel(mb, frame, o, v1);
                }
                cursor = Mathf.Max(cursor, ou1);
            }
            if (u1 > cursor + 1e-4f) Quad(mb, frame, cursor, u1, v0, v1);

            foreach (var o in sorted) Reveal(mb, frame, o, Mathf.Max(o.V0, v0), Mathf.Min(o.V1, v1), revealSlot, cardSlot);
        }

        /// <summary>A plain rectangle of the face.</summary>
        public static void Quad(MeshBuilder mb, WallFrame f, float u0, float u1, float v0, float v1, float w = 0f)
        {
            mb.AddQuad(f.P(u0, v0, w), f.P(u0, v1, w), f.P(u1, v1, w), f.P(u1, v0, w), f.Uv(u0, v0), f.Uv(u0, v1), f.Uv(u1, v1), f.Uv(u1, v0));
        }

        /// <summary>Points of an opening's arch from the left springing to the right one (u, v).</summary>
        public static Vector2[] ArchPoints(WallOpening o)
        {
            var w = o.Width;
            var spring = o.Springline;
            var rise = o.V1 - spring;
            var pts = new Vector2[ArchSegments + 1];
            if (o.Shape == OpeningShape.RoundArch)
            {
                var r = w * 0.5f;
                for (var i = 0; i <= ArchSegments; i++)
                {
                    var a = Mathf.PI * (1f - i / (float)ArchSegments);
                    pts[i] = new Vector2(o.CenterU + Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r);
                }
            }
            else
            {
                // Segmental arch: circle through both springings and the apex.
                var half = w * 0.5f;
                var radius = (half * half + rise * rise) / (2f * Mathf.Max(1e-3f, rise));
                var cy = o.V1 - radius;
                var a0 = Mathf.Atan2(spring - cy, -half);
                var a1 = Mathf.Atan2(spring - cy, half);
                for (var i = 0; i <= ArchSegments; i++)
                {
                    var a = Mathf.Lerp(a0, a1, i / (float)ArchSegments);
                    pts[i] = new Vector2(o.CenterU + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius);
                }
                pts[0] = new Vector2(o.U0, spring);
                pts[ArchSegments] = new Vector2(o.U1, spring);
            }
            return pts;
        }

        private static void Spandrel(MeshBuilder mb, WallFrame f, WallOpening o, float vTop)
        {
            var pts = ArchPoints(o);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var top = Mathf.Max(vTop, Mathf.Max(a.y, b.y));
                mb.AddQuad(f.P(a.x, a.y, 0f), f.P(a.x, top, 0f), f.P(b.x, top, 0f), f.P(b.x, b.y, 0f), f.Uv(a.x, a.y), f.Uv(a.x, top), f.Uv(b.x, top), f.Uv(b.x, b.y));
            }
        }

        /// <summary>Jambs, sill, head/soffit and (for blind openings) the dark card.</summary>
        public static void Reveal(MeshBuilder mb, WallFrame f, WallOpening o, float v0, float v1, string revealSlot, string cardSlot)
        {
            var d = Mathf.Max(0.02f, o.Depth);
            var spring = o.Shape == OpeningShape.Rect ? v1 : Mathf.Min(o.Springline, v1);
            mb.Slot(revealSlot);
            // Left jamb faces +U, right jamb faces −U.
            mb.AddQuad(f.P(o.U0, v0, 0f), f.P(o.U0, spring, 0f), f.P(o.U0, spring, -d), f.P(o.U0, v0, -d),
                new Vector2(0f, v0), new Vector2(0f, spring), new Vector2(d, spring), new Vector2(d, v0));
            mb.AddQuad(f.P(o.U1, v0, -d), f.P(o.U1, spring, -d), f.P(o.U1, spring, 0f), f.P(o.U1, v0, 0f),
                new Vector2(d, v0), new Vector2(d, spring), new Vector2(0f, spring), new Vector2(0f, v0));
            if (!o.NoSill)
            {
                mb.AddQuad(f.P(o.U0, v0, 0f), f.P(o.U0, v0, -d), f.P(o.U1, v0, -d), f.P(o.U1, v0, 0f),
                    new Vector2(o.U0, 0f), new Vector2(o.U0, d), new Vector2(o.U1, d), new Vector2(o.U1, 0f));
            }
            if (o.Shape == OpeningShape.Rect || o.V1 > v1)
            {
                mb.AddQuad(f.P(o.U0, v1, -d), f.P(o.U0, v1, 0f), f.P(o.U1, v1, 0f), f.P(o.U1, v1, -d),
                    new Vector2(o.U0, d), new Vector2(o.U0, 0f), new Vector2(o.U1, 0f), new Vector2(o.U1, d));
            }
            else
            {
                var pts = ArchPoints(o);
                var run = 0f;
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    var seg = Vector2.Distance(a, b);
                    mb.AddQuad(f.P(a.x, a.y, -d), f.P(a.x, a.y, 0f), f.P(b.x, b.y, 0f), f.P(b.x, b.y, -d),
                        new Vector2(run, d), new Vector2(run, 0f), new Vector2(run + seg, 0f), new Vector2(run + seg, d));
                    run += seg;
                }
            }
            if (!o.Blind || string.IsNullOrEmpty(cardSlot)) return;
            mb.Slot(cardSlot);
            if (o.Shape == OpeningShape.Rect || o.V1 > v1)
            {
                mb.AddQuad(f.P(o.U0, v0, -d), f.P(o.U0, v1, -d), f.P(o.U1, v1, -d), f.P(o.U1, v0, -d),
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            }
            else
            {
                mb.AddQuad(f.P(o.U0, v0, -d), f.P(o.U0, spring, -d), f.P(o.U1, spring, -d), f.P(o.U1, v0, -d),
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                var pts = ArchPoints(o);
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    mb.AddQuad(f.P(a.x, spring, -d), f.P(a.x, a.y, -d), f.P(b.x, b.y, -d), f.P(b.x, spring, -d),
                        Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
        }

        /// <summary>
        /// Writes the inner face of a wall (interiors you can enter) mirroring the openings, at the wall's thickness.
        /// Through openings get their reveals from the outer pass; this pass only adds the inner surface.
        /// </summary>
        public static void InnerFace(MeshBuilder mb, WallFrame outer, float thickness, float v0, float v1, IReadOnlyList<WallOpening> openings,
                                     string faceSlot, float u0 = 0f, float u1 = -1f)
        {
            if (u1 < 0f) u1 = outer.Length;
            var back = outer.Back(thickness);
            var mirrored = new List<WallOpening>();
            if (openings != null)
                foreach (var o in openings)
                {
                    var m = o;
                    m.U0 = outer.Length - o.U1;
                    m.U1 = outer.Length - o.U0;
                    m.Depth = 0.001f;
                    m.Blind = false;
                    mirrored.Add(m);
                }
            var bu0 = outer.Length - u1;
            var bu1 = outer.Length - u0;
            // Face only (no reveal quads: the outer pass covers the full thickness for through openings).
            var sorted = mirrored;
            sorted.Sort((a, b) => a.U0.CompareTo(b.U0));
            mb.Slot(faceSlot);
            var cursor = bu0;
            foreach (var o in sorted)
            {
                if (o.U1 <= bu0 || o.U0 >= bu1 || o.V1 <= v0 || o.V0 >= v1) continue;
                var ou0 = Mathf.Max(o.U0, bu0);
                var ou1 = Mathf.Min(o.U1, bu1);
                if (ou0 > cursor + 1e-4f) Quad(mb, back, cursor, ou0, v0, v1);
                var ov0 = Mathf.Max(o.V0, v0);
                if (ov0 > v0 + 1e-4f) Quad(mb, back, ou0, ou1, v0, ov0);
                if (o.Shape == OpeningShape.Rect || o.V1 > v1)
                {
                    var apex = Mathf.Min(o.V1, v1);
                    if (apex < v1 - 1e-4f) Quad(mb, back, ou0, ou1, apex, v1);
                }
                else Spandrel(mb, back, o, v1);
                cursor = Mathf.Max(cursor, ou1);
            }
            if (bu1 > cursor + 1e-4f) Quad(mb, back, cursor, bu1, v0, v1);
        }
    }
}
