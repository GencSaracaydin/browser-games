using System;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Axis-aligned rectangle on the ground plane in world metres (x east, z north), as the GDD writes extents
    /// ("x 181–189, z 160–252"). Used for named areas, building footprints, section bounds and trigger boxes.
    /// </summary>
    [Serializable]
    public struct AreaRect : IEquatable<AreaRect>
    {
        /// <summary>West edge.</summary>
        public float X0;
        /// <summary>South edge.</summary>
        public float Z0;
        /// <summary>East edge.</summary>
        public float X1;
        /// <summary>North edge.</summary>
        public float Z1;

        /// <summary>Creates a rectangle (edges are sorted).</summary>
        public AreaRect(float x0, float z0, float x1, float z1)
        {
            X0 = Mathf.Min(x0, x1);
            X1 = Mathf.Max(x0, x1);
            Z0 = Mathf.Min(z0, z1);
            Z1 = Mathf.Max(z0, z1);
        }

        /// <summary>A rectangle from a centre and a size.</summary>
        public static AreaRect FromCenter(float cx, float cz, float sx, float sz) =>
            new AreaRect(cx - sx * 0.5f, cz - sz * 0.5f, cx + sx * 0.5f, cz + sz * 0.5f);

        /// <summary>Width along x.</summary>
        public float Width => X1 - X0;
        /// <summary>Depth along z.</summary>
        public float Depth => Z1 - Z0;
        /// <summary>Centre on the ground plane (y 0).</summary>
        public Vector3 Center => new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
        /// <summary>Area in square metres.</summary>
        public float Area => Width * Depth;

        /// <summary>True if the XZ position lies inside (edges included).</summary>
        public bool Contains(float x, float z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;

        /// <summary>True if the position's XZ lies inside.</summary>
        public bool Contains(Vector3 p) => Contains(p.x, p.z);

        /// <summary>True if the rectangles overlap by more than <paramref name="epsilon"/> on both axes.</summary>
        public bool Overlaps(AreaRect o, float epsilon = 0.01f) =>
            X0 < o.X1 - epsilon && o.X0 < X1 - epsilon && Z0 < o.Z1 - epsilon && o.Z0 < Z1 - epsilon;

        /// <summary>Grown (or shrunk with a negative value) on every side.</summary>
        public AreaRect Expanded(float amount) => new AreaRect(X0 - amount, Z0 - amount, X1 + amount, Z1 + amount);

        /// <summary>Horizontal distance from a point to the rectangle (0 inside).</summary>
        public float DistanceTo(Vector3 p)
        {
            var dx = Mathf.Max(Mathf.Max(X0 - p.x, 0f), p.x - X1);
            var dz = Mathf.Max(Mathf.Max(Z0 - p.z, 0f), p.z - Z1);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The smallest rectangle containing both.</summary>
        public AreaRect Union(AreaRect o) => new AreaRect(Mathf.Min(X0, o.X0), Mathf.Min(Z0, o.Z0), Mathf.Max(X1, o.X1), Mathf.Max(Z1, o.Z1));

        /// <summary>World bounds from <paramref name="y0"/> to <paramref name="y1"/>.</summary>
        public Bounds ToBounds(float y0, float y1)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(X0, y0, Z0), new Vector3(X1, y1, Z1));
            return b;
        }

        /// <inheritdoc/>
        public bool Equals(AreaRect o) => X0 == o.X0 && Z0 == o.Z0 && X1 == o.X1 && Z1 == o.Z1;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is AreaRect r && Equals(r);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var h = X0.GetHashCode();
                h = h * 397 ^ Z0.GetHashCode();
                h = h * 397 ^ X1.GetHashCode();
                return h * 397 ^ Z1.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => "x " + X0 + "–" + X1 + ", z " + Z0 + "–" + Z1;
    }
}
