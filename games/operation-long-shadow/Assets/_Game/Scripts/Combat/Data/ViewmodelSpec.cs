using System;
using UnityEngine;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// First-person presentation of a weapon (procedural view-model motion, TECH_PLAN §7.5 ViewmodelController). Positions
    /// are the grip position in the view-model root's space (camera at the origin looking along +Z), angles are Euler
    /// degrees (x pitch down, y yaw right, z roll). The WeaponPrefabBuilder copies these into each <c>PF_VM_*</c> prefab's
    /// ViewmodelRig, where an editor holder can fine-tune them; these values are the defaults. Treat as immutable.
    /// </summary>
    [Serializable]
    public sealed class ViewmodelSpec
    {
        /// <summary>Hip grip position.</summary>
        public Vector3 HipPosition;
        /// <summary>Hip rotation (a slight inward yaw converges the muzzle toward the crosshair).</summary>
        public Vector3 HipEuler;
        /// <summary>Distance from the eye to the rear sight while aiming down sights (metres).</summary>
        public float EyeRelief = 0.2f;
        /// <summary>Additional ADS offset (fine alignment), metres.</summary>
        public Vector3 AdsOffset;
        /// <summary>Sprint pose offset added at full sprint.</summary>
        public Vector3 SprintPosition;
        /// <summary>Sprint pose rotation.</summary>
        public Vector3 SprintEuler;
        /// <summary>Lowered pose (switching, holstering).</summary>
        public Vector3 LoweredPosition = new Vector3(0.02f, -0.28f, -0.06f);
        /// <summary>Lowered pose rotation.</summary>
        public Vector3 LoweredEuler = new Vector3(40f, -10f, -12f);
        /// <summary>Crouch offset (lower and canted slightly).</summary>
        public Vector3 CrouchPosition = new Vector3(-0.004f, -0.008f, -0.004f);
        /// <summary>Crouch rotation.</summary>
        public Vector3 CrouchEuler = new Vector3(0f, 0f, 3.5f);
        /// <summary>Slide offset.</summary>
        public Vector3 SlidePosition = new Vector3(-0.025f, -0.03f, -0.01f);
        /// <summary>Slide rotation.</summary>
        public Vector3 SlideEuler = new Vector3(4f, -4f, 14f);
        /// <summary>View-model camera vertical FOV at full ADS (hip is 55°, TECH_PLAN §3.6).</summary>
        public float AdsViewmodelFov = 45f;
        /// <summary>Mouse-lag sway scale (1 = rifle).</summary>
        public float SwayScale = 1f;
        /// <summary>Walk bob scale (1 = rifle).</summary>
        public float BobScale = 1f;
        /// <summary>Muzzle flash scale (0 = none, suppressed).</summary>
        public float MuzzleFlashScale = 1f;
        /// <summary>CombatFx casing id ejected per shot (null = none).</summary>
        public string CasingId;
        /// <summary>Weight feel 0..1: slows springs and raises inertia (RPK, M60D).</summary>
        public float Heft = 0.3f;
        /// <summary>Default sockets (used when the model has none, and for the greybox).</summary>
        public WeaponSockets Sockets;
    }
}
