using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Axis-aligned box subtraction for simplified colliders: a solid building volume minus the rooms and doorways cut into
    /// it becomes a handful of boxes (at most six per cut), so walls stay solid and openings stay passable.
    /// </summary>
    public static class BoxCarve
    {
        /// <summary>Returns <paramref name="box"/> minus <paramref name="hole"/> as up to six disjoint boxes.</summary>
        public static List<Bounds> Subtract(Bounds box, Bounds hole)
        {
            var result = new List<Bounds>(6);
            if (!Intersects(box, hole))
            {
                result.Add(box);
                return result;
            }
            var bmin = box.min;
            var bmax = box.max;
            var hmin = Vector3.Max(hole.min, bmin);
            var hmax = Vector3.Min(hole.max, bmax);
            // Slabs along x (full y, z), then y within the hole's x span, then z within the hole's x and y spans.
            AddIfValid(result, new Vector3(bmin.x, bmin.y, bmin.z), new Vector3(hmin.x, bmax.y, bmax.z));
            AddIfValid(result, new Vector3(hmax.x, bmin.y, bmin.z), new Vector3(bmax.x, bmax.y, bmax.z));
            AddIfValid(result, new Vector3(hmin.x, bmin.y, bmin.z), new Vector3(hmax.x, hmin.y, bmax.z));
            AddIfValid(result, new Vector3(hmin.x, hmax.y, bmin.z), new Vector3(hmax.x, bmax.y, bmax.z));
            AddIfValid(result, new Vector3(hmin.x, hmin.y, bmin.z), new Vector3(hmax.x, hmax.y, hmin.z));
            AddIfValid(result, new Vector3(hmin.x, hmin.y, hmax.z), new Vector3(hmax.x, hmax.y, bmax.z));
            return result;
        }

        /// <summary>Subtracts every hole from every box.</summary>
        public static List<Bounds> SubtractAll(IEnumerable<Bounds> boxes, IEnumerable<Bounds> holes)
        {
            var current = new List<Bounds>(boxes);
            foreach (var h in holes)
            {
                var next = new List<Bounds>(current.Count + 6);
                foreach (var b in current) next.AddRange(Subtract(b, h));
                current = next;
            }
            return current;
        }

        /// <summary>True if two boxes overlap with positive volume.</summary>
        public static bool Intersects(Bounds a, Bounds b) =>
            a.min.x < b.max.x - 1e-4f && b.min.x < a.max.x - 1e-4f &&
            a.min.y < b.max.y - 1e-4f && b.min.y < a.max.y - 1e-4f &&
            a.min.z < b.max.z - 1e-4f && b.min.z < a.max.z - 1e-4f;

        private static void AddIfValid(List<Bounds> list, Vector3 min, Vector3 max)
        {
            if (max.x - min.x < 0.01f || max.y - min.y < 0.01f || max.z - min.z < 0.01f) return;
            var b = new Bounds();
            b.SetMinMax(min, max);
            list.Add(b);
        }
    }
}
