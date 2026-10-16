using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Structural kit pieces (TECH_PLAN §7.15): straight stairs (solid or steel), spiral stairs, ladders, pipe railings,
    /// timber balconies, cloth awnings (intact and torn), planks and steel stair links between roofs. Each writes its
    /// visual geometry and simplified colliders (ramps for stairs so players and the NavMesh get smooth slopes).
    /// </summary>
    public static class KitStructures
    {
        /// <summary>Standard riser height (m).</summary>
        public const float Riser = 0.18f;
        /// <summary>Standard tread depth (m).</summary>
        public const float Tread = 0.28f;

        /// <summary>
        /// Straight stair from <paramref name="bottom"/> (centre of the first nosing at floor level) up along
        /// <paramref name="ascent"/> (horizontal) by <paramref name="rise"/>. Solid stairs fill underneath (masonry);
        /// open stairs are steel stringers with plate treads. Adds a ramp collider over the nosings.
        /// </summary>
        public static void Stairs(MeshBuilder mb, Vector3 bottom, Vector3 ascent, float width, float rise, bool solid, string treadSlot, string sideSlot,
                                  SurfaceType surface, float run = -1f)
        {
            ascent.y = 0f;
            ascent.Normalize();
            var steps = Mathf.Max(1, Mathf.RoundToInt(rise / Riser));
            var riser = rise / steps;
            var tread = run > 0f ? run / steps : Tread;
            var length = tread * steps;
            var rot = Quaternion.LookRotation(ascent, Vector3.up);
            mb.Push(bottom, rot);
            for (var i = 0; i < steps; i++)
            {
                var z0 = i * tread;
                var y1 = (i + 1) * riser;
                mb.Slot(treadSlot);
                if (solid)
                {
                    mb.AddBox(new Vector3(0f, y1 * 0.5f, z0 + tread * 0.5f), new Vector3(width, y1, tread), 0x3F & ~8 & ~2 & ~1);
                }
                else
                {
                    mb.AddBox(new Vector3(0f, y1 - 0.02f, z0 + tread * 0.5f + 0.02f), new Vector3(width - 0.1f, 0.04f, tread + 0.03f));
                }
            }
            mb.Slot(sideSlot);
            if (solid)
            {
                // Side walls of the stair mass (a sawtooth reads as stepped masonry).
                for (var i = 0; i < steps; i++)
                {
                    var z0 = i * tread;
                    var y1 = (i + 1) * riser;
                    mb.AddBox(new Vector3(width * 0.5f - 0.001f, y1 * 0.5f, z0 + tread * 0.5f), new Vector3(0.002f, y1, tread), 1);
                    mb.AddBox(new Vector3(-width * 0.5f + 0.001f, y1 * 0.5f, z0 + tread * 0.5f), new Vector3(0.002f, y1, tread), 2);
                }
            }
            else
            {
                var slope = Mathf.Atan2(rise, length) * Mathf.Rad2Deg;
                var len = Mathf.Sqrt(rise * rise + length * length);
                for (var s = -1; s <= 1; s += 2)
                {
                    mb.Push(new Vector3(s * (width * 0.5f - 0.03f), rise * 0.5f - 0.06f, length * 0.5f), Quaternion.Euler(-slope, 0f, 0f));
                    mb.AddBox(Vector3.zero, new Vector3(0.06f, 0.22f, len + 0.1f));
                    mb.Pop();
                }
            }
            // Ramp collider: its top plane runs through the nosings.
            var angle = Mathf.Atan2(rise, length);
            var slopeLen = Mathf.Sqrt(rise * rise + length * length);
            var up = new Vector3(0f, Mathf.Cos(angle), -Mathf.Sin(angle));
            var mid = new Vector3(0f, rise * 0.5f, length * 0.5f) - up * 0.15f;
            mb.Push(mid, Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f));
            mb.AddCollider(Vector3.zero, new Vector3(width, 0.3f, slopeLen + 0.1f), surface);
            mb.Pop();
            mb.Pop();
        }

        /// <summary>Length on the ground of a stair of <paramref name="rise"/> at the standard tread.</summary>
        public static float RunOf(float rise) => Mathf.Max(1, Mathf.RoundToInt(rise / Riser)) * Tread;

        /// <summary>Simple stair railing along a stair side (posts and a sloped handrail).</summary>
        public static void StairRail(MeshBuilder mb, Vector3 bottom, Vector3 top, float height, string slot)
        {
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            var n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(bottom, top) / 1.2f));
            for (var i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(bottom, top, i / (float)n);
                mb.AddTube(p, p + Vector3.up * height, 0.02f, 5);
            }
            mb.AddTube(bottom + Vector3.up * height, top + Vector3.up * height, 0.025f, 6);
            mb.LodMask = keep;
        }

        /// <summary>
        /// Spiral stair around a vertical axis (Relay Hall, Map C1 'S'): wedge treads approximated by boxes, a central
        /// column and a helical handrail. Starts at <paramref name="startBearing"/> and turns clockwise seen from above.
        /// Step colliders are added so the CharacterController and the NavMesh climb it step by step.
        /// </summary>
        public static void SpiralStair(MeshBuilder mb, Vector3 center, float radius, float rise, float startBearing, float degreesPerStep,
                                       string treadSlot, string columnSlot, string railSlot, SurfaceType surface)
        {
            var steps = Mathf.Max(2, Mathf.RoundToInt(rise / Riser));
            var riser = rise / steps;
            mb.Slot(columnSlot);
            mb.AddCylinder(center, 0.11f, rise + 1.1f, 10, true, false);
            for (var i = 0; i < steps; i++)
            {
                var bearing = startBearing + i * degreesPerStep;
                var dir = MathX.DirectionFromBearing(bearing + degreesPerStep * 0.5f);
                var y = (i + 1) * riser;
                var treadDepth = 2f * radius * Mathf.Sin(degreesPerStep * 0.5f * Mathf.Deg2Rad) + 0.08f;
                var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
                var c = center + dir * (radius * 0.5f + 0.05f) + Vector3.up * (y - 0.03f);
                mb.Slot(treadSlot);
                mb.Push(c, rot);
                mb.AddBox(Vector3.zero, new Vector3(radius - 0.1f, 0.06f, treadDepth));
                mb.AddCollider(new Vector3(0f, -riser * 0.5f + 0.03f, 0f), new Vector3(radius - 0.05f, riser, treadDepth + 0.04f), surface);
                mb.Pop();
            }
            // Helical handrail on the outer edge and balusters.
            mb.Slot(railSlot);
            var keep = mb.LodMask;
            Vector3 prev = default;
            for (var i = 0; i <= steps; i++)
            {
                var bearing = startBearing + i * degreesPerStep;
                var p = center + MathX.DirectionFromBearing(bearing) * (radius - 0.04f) + Vector3.up * (i * riser + 0.95f);
                if (i > 0)
                {
                    mb.LodMask = MeshBuilder.LodAll;
                    mb.AddTube(prev, p, 0.025f, 5);
                }
                mb.LodMask = MeshBuilder.LodDetail;
                mb.AddTube(p - Vector3.up * 0.95f, p, 0.012f, 4);
                prev = p;
            }
            mb.LodMask = keep;
        }

        /// <summary>
        /// Steel ladder (two rails, rungs every 0.3 m) from <paramref name="bottom"/> to <paramref name="top"/> standing
        /// off the wall towards <paramref name="awayFromWall"/>.
        /// </summary>
        public static void Ladder(MeshBuilder mb, Vector3 bottom, Vector3 top, Vector3 awayFromWall, float width, string slot, bool hoops)
        {
            mb.Slot(slot);
            var up = (top - bottom).normalized;
            var side = Vector3.Cross(up, awayFromWall).normalized;
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            var half = side * (width * 0.5f);
            mb.AddTube(bottom - half, top - half + up * 1.0f, 0.022f, 6);
            mb.AddTube(bottom + half, top + half + up * 1.0f, 0.022f, 6);
            var len = Vector3.Distance(bottom, top);
            var rungs = Mathf.Max(2, Mathf.FloorToInt(len / 0.3f));
            for (var i = 1; i <= rungs; i++)
            {
                var p = bottom + up * (len * i / rungs);
                mb.AddTube(p - half, p + half, 0.014f, 5);
            }
            // Wall brackets.
            for (var i = 0; i < 4; i++)
            {
                var p = Vector3.Lerp(bottom, top, (i + 0.5f) / 4f);
                mb.AddTube(p - half, p - half - awayFromWall * 0.18f, 0.015f, 4);
                mb.AddTube(p + half, p + half - awayFromWall * 0.18f, 0.015f, 4);
            }
            if (!hoops) return;
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            for (var h = 2.4f; h < len; h += 0.9f)
            {
                // Safety cage hoop: a semicircle from rail to rail bulging away from the wall.
                var c = bottom + up * h;
                Vector3 prev = default;
                for (var k = 0; k <= 8; k++)
                {
                    var a = Mathf.PI * k / 8f;
                    var p = c + side * (Mathf.Cos(a) * 0.38f) + awayFromWall * (Mathf.Sin(a) * 0.62f);
                    if (k > 0) mb.AddTube(prev, p, 0.01f, 4);
                    prev = p;
                }
            }
            mb.LodMask = keep;
        }

        /// <summary>Pipe railing (posts every 1.5 m, top rail at <paramref name="height"/>, mid rail) along a polyline.</summary>
        public static void PipeRailing(MeshBuilder mb, Vector3[] path, float height, string slot, bool midRail = true)
        {
            mb.Slot(slot);
            for (var s = 1; s < path.Length; s++)
            {
                var a = path[s - 1];
                var b = path[s];
                var len = Vector3.Distance(a, b);
                var posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
                for (var i = (s == 1 ? 0 : 1); i <= posts; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)posts);
                    mb.AddTube(p, p + Vector3.up * height, 0.024f, 6);
                }
                mb.AddTube(a + Vector3.up * height, b + Vector3.up * height, 0.024f, 6);
                if (midRail) mb.AddTube(a + Vector3.up * height * 0.5f, b + Vector3.up * height * 0.5f, 0.018f, 5);
            }
        }

        /// <summary>
        /// Timber balcony on a facade: deck, fascia, diagonal brackets, posts and a balustrade with square balusters, at
        /// floor height <paramref name="floorV"/> from <paramref name="u0"/> to <paramref name="u1"/>, projecting
        /// <paramref name="depth"/>. Adds a deck collider and a railing collider.
        /// </summary>
        public static void Balcony(MeshBuilder mb, WallFrame f, float u0, float u1, float floorV, float depth, int seed)
        {
            var rng = new DetRandom((ulong)(seed * 31 + 7));
            mb.Slot(KitSlots.WoodDark);
            KitTrims.FrameBox(mb, f, u0, u1, floorV - 0.12f, floorV, 0f, depth);
            mb.Slot(KitSlots.WoodPainted);
            KitTrims.FrameBox(mb, f, u0, u1, floorV - 0.24f, floorV - 0.04f, depth - 0.08f, depth + 0.01f, KitTrims.NoBack);
            // Brackets under the deck.
            var keep = mb.LodMask;
            var brackets = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 1.4f) + 1);
            for (var i = 0; i < brackets; i++)
            {
                var u = Mathf.Lerp(u0 + 0.12f, u1 - 0.12f, i / (float)(brackets - 1));
                mb.Slot(KitSlots.WoodDark);
                mb.AddBeam(f.P(u, floorV - 0.85f, 0.02f), f.P(u, floorV - 0.1f, depth * 0.85f), 0.08f, 0.08f);
                KitTrims.FrameBox(mb, f, u - 0.05f, u + 0.05f, floorV - 0.9f, floorV - 0.1f, 0f, 0.06f, KitTrims.NoBack);
            }
            // Posts and rails.
            const float railH = 1.0f;
            mb.Slot(KitSlots.WoodPainted);
            var posts = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 1.2f) + 1);
            for (var i = 0; i < posts; i++)
            {
                var u = Mathf.Lerp(u0 + 0.05f, u1 - 0.05f, i / (float)(posts - 1));
                KitTrims.FrameBox(mb, f, u - 0.045f, u + 0.045f, floorV, floorV + railH + 0.05f, depth - 0.09f, depth);
            }
            KitTrims.FrameBox(mb, f, u0, u1, floorV + railH - 0.03f, floorV + railH + 0.04f, depth - 0.1f, depth + 0.01f);
            KitTrims.FrameBox(mb, f, u0, u0 + 0.06f, floorV, floorV + railH, 0.02f, depth);
            KitTrims.FrameBox(mb, f, u1 - 0.06f, u1, floorV, floorV + railH, 0.02f, depth);
            KitTrims.FrameBox(mb, f, u0, u1, floorV + 0.06f, floorV + 0.12f, depth - 0.08f, depth);
            mb.LodMask = MeshBuilder.LodDetail;
            var balusters = Mathf.RoundToInt((u1 - u0) / 0.13f);
            for (var i = 1; i < balusters; i++)
            {
                var u = u0 + (u1 - u0) * i / balusters;
                KitTrims.FrameBox(mb, f, u - 0.018f, u + 0.018f, floorV + 0.12f, floorV + railH - 0.03f, depth - 0.065f, depth - 0.03f);
            }
            mb.LodMask = MeshBuilder.LodOnly1;
            // LOD1: a solid balustrade panel replaces the balusters.
            KitTrims.FrameBox(mb, f, u0, u1, floorV + 0.12f, floorV + railH - 0.03f, depth - 0.06f, depth - 0.04f);
            mb.LodMask = keep;
            // Pot plants or a chair on some balconies (cheap dressing).
            if (rng.Chance(0.35f))
            {
                mb.Slot(KitSlots.Brick);
                var u = Mathf.Lerp(u0 + 0.3f, u1 - 0.3f, rng.Value01());
                mb.Push(f.P(u, floorV, depth * 0.5f), Quaternion.identity);
                mb.AddFrustum(Vector3.zero, 0.12f, 0.16f, 0.3f, 8, true, false);
                mb.Pop();
            }
            mb.Push(f.Matrix);
            mb.AddCollider(new Vector3((u0 + u1) * 0.5f, floorV - 0.06f, -depth * 0.5f), new Vector3(u1 - u0, 0.12f, depth), SurfaceType.Wood);
            mb.AddCollider(new Vector3((u0 + u1) * 0.5f, floorV + railH * 0.5f, -depth + 0.05f), new Vector3(u1 - u0, railH, 0.1f), SurfaceType.Wood);
            mb.Pop();
        }

        /// <summary>
        /// A sagging cloth panel spanning four corners (awnings, street-spanning shades, stall covers). Top edge a0→a1, front
        /// (lower) edge b0→b1. <paramref name="sag"/> pulls the middle down. Torn panels lose cells and get hanging strips.
        /// Double-sided.
        /// </summary>
        public static void ClothPanel(MeshBuilder mb, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float sag, bool torn, int seed, string slot,
                                      int segU = 8, int segV = 5)
        {
            mb.Slot(slot);
            var rng = new DetRandom((ulong)(seed * 977 + 13));
            var holes = new bool[segU * segV];
            if (torn)
            {
                // A tracer burst: a ragged diagonal tear plus random holes.
                var tearV = rng.Range(1, segV);
                for (var i = 0; i < segU; i++)
                {
                    var j = Mathf.Clamp(tearV + Mathf.RoundToInt((i - segU * 0.5f) * rng.Range(-0.4f, 0.4f)), 0, segV - 1);
                    holes[j * segU + i] = rng.Chance(0.75f);
                    if (j + 1 < segV && rng.Chance(0.4f)) holes[(j + 1) * segU + i] = true;
                }
                for (var k = 0; k < segU * segV / 6; k++) holes[rng.Range(0, holes.Length)] = true;
            }
            Vector3 P(float fu, float fv)
            {
                var top = Vector3.Lerp(a0, a1, fu);
                var bot = Vector3.Lerp(b0, b1, fu);
                var p = Vector3.Lerp(top, bot, fv);
                var s = Mathf.Sin(fu * Mathf.PI) * Mathf.Sin(fv * Mathf.PI * 0.9f + 0.1f);
                return p + Vector3.down * (sag * s);
            }
            var lenU = Vector3.Distance(a0, a1);
            var lenV = Vector3.Distance(a0, b0);
            for (var j = 0; j < segV; j++)
            for (var i = 0; i < segU; i++)
            {
                if (holes[j * segU + i]) continue;
                float u0 = i / (float)segU, u1 = (i + 1) / (float)segU, v0 = j / (float)segV, v1 = (j + 1) / (float)segV;
                var p00 = P(u0, v0);
                var p10 = P(u1, v0);
                var p01 = P(u0, v1);
                var p11 = P(u1, v1);
                var uv00 = new Vector2(u0 * lenU, v0 * lenV);
                var uv10 = new Vector2(u1 * lenU, v0 * lenV);
                var uv01 = new Vector2(u0 * lenU, v1 * lenV);
                var uv11 = new Vector2(u1 * lenU, v1 * lenV);
                mb.AddQuad(p01, p00, p10, p11, uv01, uv00, uv10, uv11);
                mb.AddQuad(p11, p10, p00, p01, uv11, uv10, uv00, uv01);
            }
            if (!torn) return;
            // Hanging strips at the tear edges.
            var strips = rng.Range(3, 7);
            for (var k = 0; k < strips; k++)
            {
                var fu = rng.Range(0.1f, 0.9f);
                var fv = rng.Range(0.2f, 0.8f);
                var root = P(fu, fv);
                var w = rng.Range(0.06f, 0.16f);
                var len = rng.Range(0.3f, 0.9f);
                var side = (a1 - a0).normalized * w;
                var tip = root + Vector3.down * len + (b0 - a0).normalized * rng.Range(-0.1f, 0.2f);
                mb.AddQuad(tip - side * 0.3f, root - side * 0.5f, root + side * 0.5f, tip + side * 0.3f);
                mb.AddQuad(tip + side * 0.3f, root + side * 0.5f, root - side * 0.5f, tip - side * 0.3f);
            }
        }

        /// <summary>Awning frame: two arms from the facade and a front bar.</summary>
        public static void AwningFrame(MeshBuilder mb, Vector3 wallLeft, Vector3 wallRight, Vector3 frontLeft, Vector3 frontRight, string slot)
        {
            mb.Slot(slot);
            mb.AddTube(wallLeft, frontLeft, 0.02f, 5);
            mb.AddTube(wallRight, frontRight, 0.02f, 5);
            mb.AddTube(frontLeft, frontRight, 0.022f, 5);
            mb.AddTube(wallLeft, wallRight, 0.03f, 5);
        }

        /// <summary>A timber plank bridging a roof gap (A9 R1→R2, R2→R3) with a walkable collider.</summary>
        public static void Plank(MeshBuilder mb, Vector3 from, Vector3 to, float width, string slot)
        {
            mb.Slot(slot);
            var dir = to - from;
            var len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            mb.Push((from + to) * 0.5f, rot);
            mb.AddBox(new Vector3(0f, -0.03f, 0f), new Vector3(width, 0.06f, len + 0.4f));
            mb.AddCollider(new Vector3(0f, -0.06f, 0f), new Vector3(width + 0.1f, 0.12f, len + 0.4f), SurfaceType.Wood);
            mb.Pop();
        }
    }
}
