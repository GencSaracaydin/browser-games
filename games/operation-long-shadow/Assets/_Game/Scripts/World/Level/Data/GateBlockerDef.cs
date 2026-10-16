using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Geometry of a gate or backtrack blocker (Appendix A kind "K", GDD §6.7, §7.13): the PlayerClip box enabled while it
    /// is closed and, for doors, the hinge layout the <see cref="GateDoorAnimator"/> swings.
    /// </summary>
    public sealed class GateBlockerDef
    {
        /// <summary>Gate id ("Gate.HallDoor", "Block.Breach").</summary>
        public string Id;
        /// <summary>Blocker box centre (world).</summary>
        public Vector3 Center;
        /// <summary>Blocker box size (local, before <see cref="Yaw"/>).</summary>
        public Vector3 Size;
        /// <summary>Box yaw (degrees).</summary>
        public float Yaw;
        /// <summary>True for backtrack blockers (start open, close at their beat).</summary>
        public bool StartsOpen;
        /// <summary>Door leaves: hinge positions (empty = no animated door).</summary>
        public Vector3[] Hinges = new Vector3[0];
        /// <summary>Closed yaw of each leaf (compass of the leaf's +X from its hinge).</summary>
        public float[] ClosedYaw = new float[0];
        /// <summary>Open yaw offset of each leaf (degrees, signed).</summary>
        public float[] OpenYaw = new float[0];
        /// <summary>Leaf width.</summary>
        public float LeafWidth;
        /// <summary>Leaf height.</summary>
        public float LeafHeight;
        /// <summary>Leaf kind: 0 plank door, 1 steel door, 2 iron bar gate.</summary>
        public int LeafKind;
        /// <summary>Kicked open (fast swing).</summary>
        public bool Kick;
        /// <summary>Notes.</summary>
        public string Notes;
    }
}
