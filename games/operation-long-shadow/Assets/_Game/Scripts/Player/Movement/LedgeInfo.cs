using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>Result of a successful <see cref="LedgeProbe.Probe"/>: where the traversal starts, crests and ends.</summary>
    public struct LedgeInfo
    {
        /// <summary>Feet position when the probe ran.</summary>
        public Vector3 Start;
        /// <summary>Point on the ledge top right behind the face (the crest of the move).</summary>
        public Vector3 Edge;
        /// <summary>Feet position at the end of the move.</summary>
        public Vector3 End;
        /// <summary>Horizontal direction of the move.</summary>
        public Vector3 Forward;
        /// <summary>Ledge top height above <see cref="Start"/>.</summary>
        public float TopHeight;
        /// <summary>Horizontal distance from the body centre to the face.</summary>
        public float FaceDistance;
        /// <summary>True when the move goes over a thin wall instead of onto the ledge.</summary>
        public bool OverVault;
        /// <summary>True when only a crouched capsule fits at the end.</summary>
        public bool EndCrouched;
    }
}
