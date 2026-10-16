using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// A detached copy of a transform hierarchy (parents before children) that can be re-posed without touching the
    /// imported objects, then written out as the <see cref="HumanDescription.skeleton"/> of an avatar. Used by
    /// <see cref="TPoseSolver"/> to give raw Rocketbox avatars a T-pose reference (Unity's "Enforce T-Pose").
    /// </summary>
    public sealed class PoseSkeleton
    {
        private readonly List<PoseNode> _nodes = new List<PoseNode>(128);
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(128);

        /// <summary>Nodes in hierarchy order (index 0 = root).</summary>
        public IReadOnlyList<PoseNode> Nodes => _nodes;

        /// <summary>Copies the local poses of <paramref name="root"/> and every descendant.</summary>
        public static PoseSkeleton FromHierarchy(Transform root)
        {
            var s = new PoseSkeleton();
            if (root != null) s.Add(root, -1);
            s.ComputeWorld();
            return s;
        }

        private void Add(Transform t, int parent)
        {
            var i = _nodes.Count;
            _nodes.Add(new PoseNode
            {
                Name = t.name,
                Parent = parent,
                LocalPosition = parent < 0 ? Vector3.zero : t.localPosition,
                LocalRotation = parent < 0 ? Quaternion.identity : t.localRotation,
                LocalScale = parent < 0 ? Vector3.one : t.localScale,
            });
            if (!_index.ContainsKey(t.name)) _index.Add(t.name, i);
            for (var c = 0; c < t.childCount; c++) Add(t.GetChild(c), i);
        }

        /// <summary>Index of the first node with this name, or -1.</summary>
        public int IndexOf(string name) => name != null && _index.TryGetValue(name, out var i) ? i : -1;

        /// <summary>World position of a node by name (zero when missing).</summary>
        public Vector3 WorldPosition(string name)
        {
            var i = IndexOf(name);
            return i >= 0 ? _nodes[i].WorldPosition : Vector3.zero;
        }

        /// <summary>True if the node exists.</summary>
        public bool Has(string name) => IndexOf(name) >= 0;

        /// <summary>Recomputes every world pose from the local poses.</summary>
        public void ComputeWorld()
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n.Parent < 0)
                {
                    n.WorldPosition = n.LocalPosition;
                    n.WorldRotation = n.LocalRotation;
                    n.WorldScale = n.LocalScale;
                    continue;
                }
                var p = _nodes[n.Parent];
                n.WorldScale = Vector3.Scale(p.WorldScale, n.LocalScale);
                n.WorldPosition = p.WorldPosition + p.WorldRotation * Vector3.Scale(p.WorldScale, n.LocalPosition);
                n.WorldRotation = p.WorldRotation * n.LocalRotation;
            }
        }

        /// <summary>Rotates a node in world space (children follow) and refreshes the world poses.</summary>
        public void RotateWorld(int index, Quaternion delta)
        {
            if (index < 0 || index >= _nodes.Count) return;
            var n = _nodes[index];
            var world = delta * n.WorldRotation;
            var parentRot = n.Parent >= 0 ? _nodes[n.Parent].WorldRotation : Quaternion.identity;
            n.LocalRotation = Quaternion.Normalize(Quaternion.Inverse(parentRot) * world);
            ComputeWorld();
        }

        /// <summary>The skeleton for a <see cref="HumanDescription"/> (every node, local poses).</summary>
        public SkeletonBone[] ToSkeletonBones()
        {
            var bones = new SkeletonBone[_nodes.Count];
            for (var i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                bones[i] = new SkeletonBone { name = n.Name, position = n.LocalPosition, rotation = n.LocalRotation, scale = n.LocalScale };
            }
            return bones;
        }
    }
}
