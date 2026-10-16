using System;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>One squad member's slot in a <see cref="SquadNodeSet"/>: position/facing and pose hints.</summary>
    [Serializable]
    public struct SquadNode
    {
        /// <summary>Slot transform (position and facing).</summary>
        public Transform Point;
        /// <summary>Stance to hold (Stand or Crouch).</summary>
        public Stance Stance;
        /// <summary>Cover pose index for the animator (0 = none).</summary>
        public int CoverPose;

        /// <summary>True if the slot has a transform.</summary>
        public bool IsValid => Point != null;
    }
}
