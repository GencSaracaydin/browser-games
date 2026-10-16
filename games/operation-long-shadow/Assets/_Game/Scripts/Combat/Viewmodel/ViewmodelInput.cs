using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>One frame of state for <see cref="ViewmodelMotion.Step"/> (filled by ViewmodelController from the rig and PlayerWeapon).</summary>
    public struct ViewmodelInput
    {
        /// <summary>Frame time.</summary>
        public float Dt;
        /// <summary>Time for idle breathing.</summary>
        public float Time;
        /// <summary>ADS blend 0..1 (linear; smoothed inside).</summary>
        public float AdsBlend;
        /// <summary>Sprinting (pose target).</summary>
        public bool Sprinting;
        /// <summary>Crouched.</summary>
        public bool Crouched;
        /// <summary>Sliding.</summary>
        public bool Sliding;
        /// <summary>Airborne.</summary>
        public bool Airborne;
        /// <summary>Lowered amount 0..1 (switch, holster, mount transitions; already eased).</summary>
        public float Lowered01;
        /// <summary>Look change this frame in degrees (x pitch up, y yaw right).</summary>
        public Vector2 LookDelta;
        /// <summary>Player velocity in view space (x right, z forward), m/s.</summary>
        public Vector3 LocalVelocity;
        /// <summary>Walking speed (normalises strafe roll).</summary>
        public float WalkSpeed;
        /// <summary>Head-bob phase (radians; one step = π, IPlayerRig.BobPhase).</summary>
        public float BobPhase;
        /// <summary>Head-bob amount (planar speed / sprint speed).</summary>
        public float BobAmount01;
        /// <summary>Reload choreography pose this frame.</summary>
        public ReloadPose Reload;
        /// <summary>Grenade/knife pose this frame.</summary>
        public ActionPose Action;
    }
}
