using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// The view model's reload pose at one moment (output of <see cref="ReloadChoreography.Evaluate"/>): an offset of the
    /// whole weapon (view-model root space), the magazine's offset from its seat (weapon space) and where the left hand is.
    /// </summary>
    public struct ReloadPose
    {
        /// <summary>Weapon offset (metres, view-model space).</summary>
        public Vector3 WeaponOffset;
        /// <summary>Weapon rotation offset (Euler degrees: x pitch down, y yaw right, z roll left).</summary>
        public Vector3 WeaponEuler;
        /// <summary>Magazine offset from its seat in weapon space (metres).</summary>
        public Vector3 MagOffset;
        /// <summary>Magazine rotation from its seat (Euler degrees, weapon space).</summary>
        public Vector3 MagEuler;
        /// <summary>The magazine renders (false between an empty reload's drop and the new magazine coming into view).</summary>
        public bool MagVisible;
        /// <summary>Left hand on the magazine (0 = on the handguard, 1 = holding the magazine).</summary>
        public float LeftOnMag;
        /// <summary>Left hand on the charging handle / slide (0..1; wins over <see cref="LeftOnMag"/>).</summary>
        public float LeftOnCharge;
        /// <summary>Charging handle travel 0..1 (pulled back = 1).</summary>
        public float ChargeTravel;
        /// <summary>True while any reload motion is active.</summary>
        public bool Active;

        /// <summary>The rest pose (no reload).</summary>
        public static ReloadPose Rest => new ReloadPose { MagVisible = true };
    }
}
