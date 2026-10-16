using UnityEngine;

namespace LongShadow.Combat.Viewmodel
{
    /// <summary>
    /// Pose of a hand action (grenade throw, knife) at one moment (output of <see cref="HandActions"/>): the gun dips out
    /// of the way and the left hand leaves the handguard to carry the grenade or the knife.
    /// </summary>
    public struct ActionPose
    {
        /// <summary>Weapon offset (metres, view-model space).</summary>
        public Vector3 WeaponOffset;
        /// <summary>Weapon rotation offset (Euler degrees).</summary>
        public Vector3 WeaponEuler;
        /// <summary>Left hand (and the held item) position in view-model space.</summary>
        public Vector3 HandPosition;
        /// <summary>Left hand rotation in view-model space (Euler degrees; forward = fingers/blade).</summary>
        public Vector3 HandEuler;
        /// <summary>Left hand on the action hand target instead of the weapon (0..1).</summary>
        public float HandWeight;
        /// <summary>The held item (grenade, knife) renders.</summary>
        public bool ItemVisible;
        /// <summary>True while an action runs.</summary>
        public bool Active;
    }
}
