using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Animator parameter names shared by the controller builders (WP05: AC_Soldier, AC_ShadowProxy) and the code that
    /// drives them (WP05 HumanoidBody, WP02 PlayerShadowProxy, WP09 cinematic clips). TECH_PLAN §7.8. Use the hashed
    /// ids at runtime (no string lookups per frame).
    /// </summary>
    public static class AnimatorParams
    {
        // ---- AC_Soldier (enemies and squad)
        /// <summary>float: local velocity X in m/s (strafe).</summary>
        public const string VelX = "VelX";
        /// <summary>float: local velocity Z in m/s (forward).</summary>
        public const string VelZ = "VelZ";
        /// <summary>float: planar speed in m/s. Also used by AC_ShadowProxy.</summary>
        public const string Speed = "Speed";
        /// <summary>bool: crouched. Also used by AC_ShadowProxy.</summary>
        public const string Crouch = "Crouch";
        /// <summary>bool: weapon raised / aiming. Also used by AC_ShadowProxy.</summary>
        public const string Aim = "Aim";
        /// <summary>trigger: one shot (upper-body recoil).</summary>
        public const string Fire = "Fire";
        /// <summary>trigger: reload.</summary>
        public const string Reload = "Reload";
        /// <summary>trigger: grenade throw.</summary>
        public const string Throw = "Throw";
        /// <summary>trigger: additive hit reaction.</summary>
        public const string Hit = "Hit";
        /// <summary>int: hit direction 0 front, 1 back, 2 left, 3 right.</summary>
        public const string HitDir = "HitDir";
        /// <summary>int: death clip index (0 = alive).</summary>
        public const string Death = "Death";
        /// <summary>int: scripted action index (see <see cref="ActionIds"/>; mapping owned by WP05).</summary>
        public const string Action = "Action";
        /// <summary>trigger: start the action in <see cref="Action"/>.</summary>
        public const string ActionTrigger = "ActionTrigger";
        /// <summary>bool: perched (gunner/marksman idle).</summary>
        public const string Perch = "Perch";
        /// <summary>int: cover pose (0 none, 1 crouch cover, 2 stand cover, 3 lean left, 4 lean right).</summary>
        public const string Cover = "Cover";

        // ---- AC_ShadowProxy only (D19, driven by WP02 PlayerShadowProxy)
        /// <summary>bool: airborne (jump, fall, mantle).</summary>
        public const string Airborne = "Airborne";

        public static readonly int VelXId = Animator.StringToHash(VelX);
        public static readonly int VelZId = Animator.StringToHash(VelZ);
        public static readonly int SpeedId = Animator.StringToHash(Speed);
        public static readonly int CrouchId = Animator.StringToHash(Crouch);
        public static readonly int AimId = Animator.StringToHash(Aim);
        public static readonly int FireId = Animator.StringToHash(Fire);
        public static readonly int ReloadId = Animator.StringToHash(Reload);
        public static readonly int ThrowId = Animator.StringToHash(Throw);
        public static readonly int HitId = Animator.StringToHash(Hit);
        public static readonly int HitDirId = Animator.StringToHash(HitDir);
        public static readonly int DeathId = Animator.StringToHash(Death);
        public static readonly int ActionId = Animator.StringToHash(Action);
        public static readonly int ActionTriggerId = Animator.StringToHash(ActionTrigger);
        public static readonly int PerchId = Animator.StringToHash(Perch);
        public static readonly int CoverId = Animator.StringToHash(Cover);
        public static readonly int AirborneId = Animator.StringToHash(Airborne);
    }
}
