using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>One sodium street lamp: where its head hangs, how it is mounted and which section builds it.</summary>
    public sealed class LampDef
    {
        /// <summary>Marker id of the lamp head ("LAMP.07").</summary>
        public string Id;
        /// <summary>Section scene that builds the fixture.</summary>
        public SectionId Section;
        /// <summary>Mount type.</summary>
        public LampMount Mount;
        /// <summary>Foot of the post, or the wall point the bracket is fixed to.</summary>
        public Vector3 Base;
        /// <summary>Lamp head position (light source).</summary>
        public Vector3 Head;
        /// <summary>Compass heading the arm points to (from the wall/post towards the head).</summary>
        public float Facing;
        /// <summary>Distance from the ripple origin (generator) in metres: strike delay = distance / 15 m/s (GDD §8.5).</summary>
        public float RippleDistance;
    }
}
