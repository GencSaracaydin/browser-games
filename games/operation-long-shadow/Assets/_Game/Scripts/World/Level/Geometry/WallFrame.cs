using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// Coordinate frame of one wall face: <c>P(u, v, w) = Origin + U·u + up·v + N·w</c>. U runs to the right as seen from
    /// outside, N is the outward normal (N = up × U), w &gt; 0 is outside the wall surface and w &lt; 0 inside the wall.
    /// <see cref="UvU"/> offsets the horizontal texture coordinate so textures wrap continuously around a building.
    /// </summary>
    public sealed class WallFrame
    {
        /// <summary>Bottom-left corner of the outer face (y = 0 of the frame).</summary>
        public Vector3 Origin;
        /// <summary>Unit direction along the wall.</summary>
        public Vector3 U;
        /// <summary>Outward unit normal.</summary>
        public Vector3 N;
        /// <summary>Wall length.</summary>
        public float Length;
        /// <summary>Texture u offset.</summary>
        public float UvU;

        /// <summary>Creates a frame; N is derived from U (N = up × U).</summary>
        public WallFrame(Vector3 origin, Vector3 u, float length, float uvU = 0f)
        {
            Origin = origin;
            U = u.normalized;
            N = Vector3.Cross(Vector3.up, U).normalized;
            Length = length;
            UvU = uvU;
        }

        /// <summary>Point in the frame.</summary>
        public Vector3 P(float u, float v, float w) => Origin + U * u + Vector3.up * v + N * w;

        /// <summary>Texture coordinate of a facade point (metres).</summary>
        public Vector2 Uv(float u, float v) => new Vector2(UvU + u, v);

        /// <summary>The frame of the same wall seen from the other side, offset inward by <paramref name="thickness"/>.</summary>
        public WallFrame Back(float thickness) => new WallFrame(Origin + U * Length - N * thickness, -U, Length, UvU + 37.3f);

        /// <summary>
        /// Proper rotation+translation from "frame local" space to building space: local x = U (along the wall), y = up,
        /// z = −N (into the wall), origin at <see cref="Origin"/>. A frame point (u, v, w) is local (u, v, −w).
        /// </summary>
        public Matrix4x4 Matrix
        {
            get
            {
                var m = Matrix4x4.identity;
                m.SetColumn(0, new Vector4(U.x, U.y, U.z, 0f));
                m.SetColumn(1, new Vector4(0f, 1f, 0f, 0f));
                m.SetColumn(2, new Vector4(-N.x, -N.y, -N.z, 0f));
                m.SetColumn(3, new Vector4(Origin.x, Origin.y, Origin.z, 1f));
                return m;
            }
        }

        /// <summary>Rotation of <see cref="Matrix"/> (local +z points into the wall).</summary>
        public Quaternion Rotation => Quaternion.LookRotation(-N, Vector3.up);

        /// <summary>
        /// The four walls of an axis-aligned footprint, outward-facing, in the order south, east, north, west, with
        /// continuous texture u along the perimeter.
        /// </summary>
        public static WallFrame[] Perimeter(float x0, float z0, float x1, float z1, float baseY)
        {
            var w = x1 - x0;
            var d = z1 - z0;
            return new[]
            {
                new WallFrame(new Vector3(x0, baseY, z0), Vector3.right, w, 0f),
                new WallFrame(new Vector3(x1, baseY, z0), Vector3.forward, d, w),
                new WallFrame(new Vector3(x1, baseY, z1), Vector3.left, w, w + d),
                new WallFrame(new Vector3(x0, baseY, z1), Vector3.back, d, w + d + w),
            };
        }
    }
}
