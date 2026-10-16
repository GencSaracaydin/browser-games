using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>IK targets of the current view model for the FP arms (WP03 ViewmodelController -> WP05 FPArmsIK).</summary>
    public interface IWeaponGrips
    {
        /// <summary>Right-hand grip (pistol grip).</summary>
        Transform RightGrip { get; }
        /// <summary>Left-hand grip (foregrip; follows the magazine during reloads).</summary>
        Transform LeftGrip { get; }
        /// <summary>Elbow hint for the left arm.</summary>
        Transform SupportHint { get; }
        /// <summary>Left-hand IK weight 0..1.</summary>
        float LeftHandWeight { get; }
    }
}
