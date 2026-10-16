using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>One transform of a <see cref="PoseSkeleton"/> (local and derived world pose).</summary>
    public sealed class PoseNode
    {
        public string Name;
        /// <summary>Index of the parent node, -1 for the root.</summary>
        public int Parent = -1;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        public Vector3 WorldPosition;
        public Quaternion WorldRotation = Quaternion.identity;
        /// <summary>Accumulated world scale (component-wise; Rocketbox skeletons are unscaled).</summary>
        public Vector3 WorldScale = Vector3.one;
    }
}
