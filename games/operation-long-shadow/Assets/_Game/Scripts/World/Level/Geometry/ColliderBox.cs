using System;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Geometry
{
    /// <summary>
    /// A box collider produced by a generator (local to the generated object): simplified physics for walls, slabs, steps
    /// and props. Builders turn these into BoxColliders with a <see cref="SurfaceTag"/> (impacts and footsteps).
    /// </summary>
    [Serializable]
    public struct ColliderBox
    {
        /// <summary>Centre.</summary>
        public Vector3 Center;
        /// <summary>Size.</summary>
        public Vector3 Size;
        /// <summary>Rotation.</summary>
        public Quaternion Rotation;
        /// <summary>Surface for impacts and footsteps.</summary>
        public SurfaceType Surface;
        /// <summary>Physics layer (Default for world geometry, PlayerClip for invisible walls).</summary>
        public int Layer;
        /// <summary>Optional name for the collider object.</summary>
        public string Name;

        /// <summary>Creates a box.</summary>
        public ColliderBox(Vector3 center, Vector3 size, Quaternion rotation, SurfaceType surface, int layer = LayerIds.Default, string name = null)
        {
            Center = center;
            Size = size;
            Rotation = rotation;
            Surface = surface;
            Layer = layer;
            Name = name;
        }
    }
}
