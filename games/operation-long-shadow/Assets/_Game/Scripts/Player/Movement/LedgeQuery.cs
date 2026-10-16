using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>Parameters of one <see cref="LedgeProbe.Probe"/> call.</summary>
    public struct LedgeQuery
    {
        /// <summary>Feet position of the player (capsule bottom).</summary>
        public Vector3 Feet;
        /// <summary>Horizontal probe direction (normalised by the probe).</summary>
        public Vector3 Forward;
        /// <summary>Capsule radius.</summary>
        public float Radius;
        /// <summary>Standing capsule height.</summary>
        public float StandHeight;
        /// <summary>Crouched capsule height.</summary>
        public float CrouchHeight;
        /// <summary>Lowest ledge top above the feet that counts.</summary>
        public float MinTop;
        /// <summary>Highest ledge top above the feet that counts.</summary>
        public float MaxTop;
        /// <summary>Largest horizontal distance from the body centre to the ledge face.</summary>
        public float MaxFaceDistance;
        /// <summary>Allow vaulting over a thin wall to the far side.</summary>
        public bool AllowOverVault;
        /// <summary>Thickest wall vaulted over (metres).</summary>
        public float OverVaultMaxThickness;
        /// <summary>Walkable slope limit in degrees (ledge tops must be walkable).</summary>
        public float SlopeLimit;
        /// <summary>Controller step offset: a "face" must rise above it to count as a ledge.</summary>
        public float StepOffset;
    }
}
