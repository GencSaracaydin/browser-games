using LongShadow.Core;
using LongShadow.Player.Movement;
using UnityEngine;

namespace LongShadow.Player.View
{
    /// <summary>What <see cref="PlayerCameraRig.Tick"/> needs from the rest of the rig for one frame.</summary>
    public struct CameraFrame
    {
        /// <summary>Locomotion state.</summary>
        public Stance Stance;
        /// <summary>Measured horizontal speed (m/s).</summary>
        public float PlanarSpeed;
        /// <summary>Velocity in the body frame (x = right, z = forward), m/s.</summary>
        public Vector3 LocalVelocity;
        /// <summary>Grounded.</summary>
        public bool Grounded;
        /// <summary>Crouched capsule.</summary>
        public bool Crouched;
        /// <summary>Target eye height from the motor (m).</summary>
        public float EyeHeight;
        /// <summary>ADS blend 0..1 (weapon).</summary>
        public float AdsBlend;
        /// <summary>Binoculars raised.</summary>
        public bool Glassing;
        /// <summary>Player dead.</summary>
        public bool Dead;
        /// <summary>Running traversal kind.</summary>
        public LedgeKind Traversal;
        /// <summary>Traversal progress 0..1.</summary>
        public float Traversal01;
        /// <summary>Settings horizontal FOV (GDD §11, 75–105).</summary>
        public float FovHorizontal;
        /// <summary>Reference walk speed (m/s).</summary>
        public float WalkSpeed;
        /// <summary>Reference sprint speed (m/s).</summary>
        public float SprintSpeed;
    }
}
