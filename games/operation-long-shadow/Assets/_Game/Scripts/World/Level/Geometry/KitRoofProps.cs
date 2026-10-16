using LongShadow.Core;
using LongShadow.World.Level.Data;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Rooftop dressing of the Laundry Roofs and every flat roof seen from the flight (GDD §6.6 BLD-08, BLD-15; manifest
    /// PROC_KIT_ROOFS, PROC_CABLES_LAUNDRY): water tanks on steel stands (the WT cover of Beat 5), TV antennas, laundry
    /// lines with cloth, catenary cables, pigeon lofts, roof-access stair huts and the R3 sandbag nest ring.
    /// </summary>
    public static class KitRoofProps
    {
        /// <summary>
        /// Water tank on a four-leg steel stand: cylindrical (seed even) or a rectangular galvanised tank. Returns the
        /// tank's top height above <paramref name="foot"/>. Adds colliders (full cover).
        /// </summary>
        public static float WaterTank(MeshBuilder mb, Vector3 foot, float standHeight, int seed, float yaw = 0f)
        {
            var rng = new DetRandom((ulong)(seed * 131 + 5));
            var cylindrical = (seed & 1) == 0;
            var tankH = rng.Range(1.1f, 1.5f);
            var r = rng.Range(0.55f, 0.75f);
            mb.Push(foot, Quaternion.Euler(0f, yaw, 0f));
            mb.Slot(KitSlots.Iron);
            var half = cylindrical ? r * 0.75f : r;
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var p = new Vector3(sx * half, 0f, sz * half);
                mb.AddBox(p + Vector3.up * standHeight * 0.5f, new Vector3(0.06f, standHeight, 0.06f));
            }
            if (standHeight > 0.6f)
            {
                var keep = mb.LodMask;
                mb.LodMask = MeshBuilder.LodDetail;
                mb.AddBeam(new Vector3(-half, 0.1f, -half), new Vector3(half, standHeight - 0.05f, -half), 0.04f, 0.04f);
                mb.AddBeam(new Vector3(half, 0.1f, half), new Vector3(-half, standHeight - 0.05f, half), 0.04f, 0.04f);
                mb.AddBeam(new Vector3(-half, 0.1f, half), new Vector3(-half, standHeight - 0.05f, -half), 0.04f, 0.04f);
                mb.AddBeam(new Vector3(half, 0.1f, -half), new Vector3(half, standHeight - 0.05f, half), 0.04f, 0.04f);
                mb.LodMask = keep;
            }
            mb.AddBox(new Vector3(0f, standHeight - 0.03f, 0f), new Vector3(half * 2f + 0.12f, 0.06f, half * 2f + 0.12f));
            mb.Slot(KitSlots.SheetMetal);
            if (cylindrical)
            {
                mb.AddCylinder(new Vector3(0f, standHeight, 0f), r, tankH, 14, false, true);
                mb.AddFrustum(new Vector3(0f, standHeight + tankH, 0f), r, r * 0.15f, 0.18f, 14, true, false);
                mb.AddCollider(new Vector3(0f, standHeight + tankH * 0.5f, 0f), new Vector3(r * 1.7f, tankH, r * 1.7f), SurfaceType.Metal);
            }
            else
            {
                mb.AddBox(new Vector3(0f, standHeight + tankH * 0.5f, 0f), new Vector3(r * 2f, tankH, r * 1.6f));
                mb.AddCollider(new Vector3(0f, standHeight + tankH * 0.5f, 0f), new Vector3(r * 2f, tankH, r * 1.6f), SurfaceType.Metal);
            }
            mb.AddCollider(new Vector3(0f, standHeight * 0.5f, 0f), new Vector3(half * 2f, standHeight, half * 2f), SurfaceType.Metal);
            // Overflow pipe down to the roof.
            mb.Slot(KitSlots.Iron);
            mb.AddTube(new Vector3(r * 0.8f, standHeight + tankH * 0.8f, 0f), new Vector3(r * 0.8f, 0.05f, 0f), 0.025f, 5);
            mb.Pop();
            return standHeight + tankH;
        }

        /// <summary>TV antenna: a pole with two Yagi booms and guy wires (1984 rooftop clutter).</summary>
        public static void TvAntenna(MeshBuilder mb, Vector3 foot, float height, int seed)
        {
            var rng = new DetRandom((ulong)(seed * 17 + 3));
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            mb.Slot(KitSlots.Iron);
            mb.AddTube(foot, foot + Vector3.up * height, 0.022f, 5);
            for (var b = 0; b < 2; b++)
            {
                var y = height - b * 0.7f;
                var yaw = rng.Range(0f, 360f);
                var dir = MathX.DirectionFromBearing(yaw);
                var side = Vector3.Cross(Vector3.up, dir);
                var boomA = foot + Vector3.up * y - dir * 0.3f;
                var boomB = foot + Vector3.up * y + dir * 1.1f;
                mb.AddTube(boomA, boomB, 0.01f, 3);
                for (var e = 0; e < 6; e++)
                {
                    var p = Vector3.Lerp(boomA, boomB, e / 5f);
                    var w = 0.55f - e * 0.05f;
                    mb.AddTube(p - side * w * 0.5f, p + side * w * 0.5f, 0.005f, 3);
                }
            }
            for (var g = 0; g < 3; g++)
            {
                var a = g * 120f + rng.Range(-10f, 10f);
                mb.AddTube(foot + Vector3.up * height * 0.7f, foot + MathX.DirectionFromBearing(a) * height * 0.55f, 0.004f, 3);
            }
            mb.LodMask = keep;
        }

        /// <summary>Catenary cable between two points (street cables, guy wires); <paramref name="sag"/> at midspan.</summary>
        public static void Cable(MeshBuilder mb, Vector3 a, Vector3 b, float sag, float radius, string slot, int segments = 10)
        {
            mb.Slot(slot);
            var keep = mb.LodMask;
            mb.LodMask = MeshBuilder.LodDetail;
            var prev = a;
            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                mb.AddTube(prev, p, radius, 3);
                prev = p;
            }
            mb.LodMask = keep;
        }

        /// <summary>Point on a catenary (parabola) cable at fraction <paramref name="t"/>.</summary>
        public static Vector3 CablePoint(Vector3 a, Vector3 b, float sag, float t) =>
            Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));

        /// <summary>Laundry line between two posts or hooks, with cloth hanging from it (double-sided, wind-ready).</summary>
        public static void LaundryLine(MeshBuilder mb, Vector3 a, Vector3 b, int seed, bool posts)
        {
            var rng = new DetRandom((ulong)(seed * 53 + 11));
            if (posts)
            {
                mb.Slot(KitSlots.Iron);
                mb.AddTube(a + Vector3.down * 1.8f, a, 0.025f, 5);
                mb.AddTube(b + Vector3.down * 1.8f, b, 0.025f, 5);
            }
            var sag = Vector3.Distance(a, b) * 0.04f;
            Cable(mb, a, b, sag, 0.006f, KitSlots.Rope, 8);
            var len = Vector3.Distance(a, b);
            var count = Mathf.Max(1, Mathf.FloorToInt(len / 0.9f));
            var keep = mb.LodMask;
            for (var i = 0; i < count; i++)
            {
                if (rng.Chance(0.2f)) continue;
                var t0 = (i + rng.Range(0.1f, 0.3f)) / count;
                var t1 = Mathf.Min(1f, t0 + rng.Range(0.35f, 0.7f) / len);
                var p0 = CablePoint(a, b, sag, t0);
                var p1 = CablePoint(a, b, sag, t1);
                var drop = rng.Range(0.45f, 1.0f);
                var swing = Vector3.Cross((b - a).normalized, Vector3.up) * rng.Range(-0.08f, 0.08f);
                mb.LodMask = MeshBuilder.LodAll;
                mb.Slot(KitSlots.Cloth(rng.Range(0, 4)));
                var q0 = p0 + Vector3.down * drop + swing;
                var q1 = p1 + Vector3.down * (drop * rng.Range(0.85f, 1.05f)) + swing;
                mb.AddQuad(q0, p0, p1, q1);
                mb.AddQuad(q1, p1, p0, q0);
            }
            mb.LodMask = keep;
        }

        /// <summary>Roof-access stair hut (2.2 m cube with a door and a flat roof), the usual way onto Zaroud's roofs.</summary>
        public static void StairHut(MeshBuilder mb, Vector3 foot, float yaw, string wallSlot, int seed)
        {
            mb.Push(foot, Quaternion.Euler(0f, yaw, 0f));
            mb.Slot(wallSlot);
            mb.AddBox(new Vector3(0f, 1.15f, 0f), new Vector3(2.2f, 2.3f, 2.2f), MeshBuilder.FacesNoBottom);
            mb.Slot(KitSlots.WoodPainted);
            mb.AddBox(new Vector3(0f, 1.0f, -1.11f), new Vector3(0.85f, 1.95f, 0.04f), 0x3F & ~8);
            mb.Slot(KitSlots.PlasterRough);
            mb.AddBox(new Vector3(0f, 2.35f, 0f), new Vector3(2.35f, 0.1f, 2.35f));
            mb.AddCollider(new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 2.4f, 2.2f), SurfaceType.Plaster);
            mb.Pop();
        }

        /// <summary>Pigeon loft: a timber coop on legs with a dark mesh front and a landing board.</summary>
        public static void PigeonLoft(MeshBuilder mb, Vector3 foot, float yaw)
        {
            mb.Push(foot, Quaternion.Euler(0f, yaw, 0f));
            mb.Slot(KitSlots.Wood);
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
                mb.AddBox(new Vector3(sx * 0.9f, 0.35f, sz * 0.6f), new Vector3(0.07f, 0.7f, 0.07f));
            mb.AddBox(new Vector3(0f, 1.25f, 0f), new Vector3(2f, 1.1f, 1.4f));
            mb.AddBox(new Vector3(0f, 1.9f, 0.1f), new Vector3(2.2f, 0.06f, 1.7f));
            mb.Slot(KitSlots.InteriorCard);
            mb.AddBox(new Vector3(0f, 1.25f, -0.705f), new Vector3(1.7f, 0.8f, 0.01f), 32);
            mb.Slot(KitSlots.Wood);
            mb.AddBox(new Vector3(0f, 0.8f, -0.85f), new Vector3(1.9f, 0.04f, 0.3f));
            mb.AddCollider(new Vector3(0f, 0.95f, 0f), new Vector3(2.1f, 1.9f, 1.5f), SurfaceType.Wood);
            mb.Pop();
        }

        /// <summary>
        /// Sandbag ring around a gun position (R3 MG nest, GDD §6.6 BLD-08) built from stacked bag shapes: a fallback when
        /// the FAL_SANDBAG prop is not in yet (the section builder prefers prop instances).
        /// </summary>
        public static void SandbagRing(MeshBuilder mb, Vector3 center, float radius, float height, float gapBearing, int seed)
        {
            var rng = new DetRandom((ulong)(seed * 7 + 1));
            mb.Slot(KitSlots.Hessian);
            var layers = Mathf.Max(2, Mathf.RoundToInt(height / 0.18f));
            var per = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / 0.55f));
            for (var l = 0; l < layers; l++)
            {
                for (var i = 0; i < per; i++)
                {
                    var bearing = (i + (l % 2) * 0.5f) * 360f / per;
                    if (Mathf.Abs(Mathf.DeltaAngle(bearing, gapBearing)) < 28f) continue;
                    var dir = MathX.DirectionFromBearing(bearing);
                    var p = center + dir * radius + Vector3.up * (l * 0.17f + 0.09f);
                    mb.Push(p, Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir), Vector3.up) * Quaternion.Euler(0f, rng.Range(-6f, 6f), 0f));
                    mb.AddChamferBox(Vector3.zero, new Vector3(0.36f, 0.17f, 0.56f), 0.07f, true, false);
                    mb.Pop();
                }
            }
            for (var i = 0; i < 12; i++)
            {
                var bearing = i * 30f;
                if (Mathf.Abs(Mathf.DeltaAngle(bearing, gapBearing)) < 28f) continue;
                var dir = MathX.DirectionFromBearing(bearing);
                mb.Push(center + dir * radius, Quaternion.LookRotation(dir, Vector3.up));
                mb.AddCollider(new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 0.55f, height, 0.4f), SurfaceType.Sand);
                mb.Pop();
            }
        }
    }
}
