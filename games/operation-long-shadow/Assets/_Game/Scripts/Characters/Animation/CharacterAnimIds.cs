using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Characters.Animation
{
    /// <summary>
    /// WP05-internal animator vocabulary shared by the controller builder (AC_Soldier, AC_ShadowProxy, AC_Booth,
    /// AC_FPArms) and the runtime drivers. The cross-package parameters (VelX, VelZ, Speed, Crouch, Aim, Fire, Reload,
    /// Throw, Hit, HitDir, Death, Action, ActionTrigger, Perch, Cover, Airborne) live in Core <see cref="AnimatorParams"/>;
    /// everything here is private to the Characters package and may change with the builder.
    /// </summary>
    public static class CharacterAnimIds
    {
        // ---- parameters (WP05 only)
        /// <summary>int: <see cref="Animation.HandPose"/> on the Hands layer.</summary>
        public const string HandPose = "HandPose";
        /// <summary>int: <see cref="WeaponHold"/> on the Arms layer.</summary>
        public const string Hold = "Hold";
        /// <summary>bool: keeps a looping or timed full-body action state alive; false lets it exit.</summary>
        public const string ActionHold = "ActionHold";
        /// <summary>int: arms action index on the Arms layer (<see cref="ActionTable"/> rows with ActionLayer.Arms; 0 = none).</summary>
        public const string ArmsAction = "ArmsAction";
        /// <summary>trigger: start the arms action in <see cref="ArmsAction"/>.</summary>
        public const string ArmsTrigger = "ArmsTrigger";
        /// <summary>bool: keeps a looping or timed arms action state alive.</summary>
        public const string ArmsHold = "ArmsHold";
        /// <summary>float: in-place turn rate -1 (left) .. 1 (right), drives the idle turn blend.</summary>
        public const string Turn = "Turn";
        /// <summary>int: booth idle set (0 stand, 1 seated).</summary>
        public const string IdleSet = "IdleSet";

        public static readonly int HandPoseId = Animator.StringToHash(HandPose);
        public static readonly int HoldId = Animator.StringToHash(Hold);
        public static readonly int ActionHoldId = Animator.StringToHash(ActionHold);
        public static readonly int ArmsActionId = Animator.StringToHash(ArmsAction);
        public static readonly int ArmsTriggerId = Animator.StringToHash(ArmsTrigger);
        public static readonly int ArmsHoldId = Animator.StringToHash(ArmsHold);
        public static readonly int TurnId = Animator.StringToHash(Turn);
        public static readonly int IdleSetId = Animator.StringToHash(IdleSet);

        /// <summary>
        /// Every parameter of every WP05 controller (AC_Soldier, AC_ShadowProxy, AC_FPArms and their overrides): the Core
        /// <see cref="AnimatorParams"/> contract plus the WP05 ones above. All controllers carry all of them so any driver
        /// (HumanoidBody, ShadowProxyBody, WP02 PlayerShadowProxy, WP09 clips) can set any parameter without warnings.
        /// </summary>
        public static readonly ParameterSpec[] AllParameters =
        {
            new ParameterSpec(AnimatorParams.VelX, AnimatorControllerParameterType.Float),
            new ParameterSpec(AnimatorParams.VelZ, AnimatorControllerParameterType.Float),
            new ParameterSpec(AnimatorParams.Speed, AnimatorControllerParameterType.Float),
            new ParameterSpec(AnimatorParams.Crouch, AnimatorControllerParameterType.Bool),
            new ParameterSpec(AnimatorParams.Aim, AnimatorControllerParameterType.Bool),
            new ParameterSpec(AnimatorParams.Fire, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(AnimatorParams.Reload, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(AnimatorParams.Throw, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(AnimatorParams.Hit, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(AnimatorParams.HitDir, AnimatorControllerParameterType.Int),
            new ParameterSpec(AnimatorParams.Death, AnimatorControllerParameterType.Int),
            new ParameterSpec(AnimatorParams.Action, AnimatorControllerParameterType.Int),
            new ParameterSpec(AnimatorParams.ActionTrigger, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(AnimatorParams.Perch, AnimatorControllerParameterType.Bool),
            new ParameterSpec(AnimatorParams.Cover, AnimatorControllerParameterType.Int),
            new ParameterSpec(AnimatorParams.Airborne, AnimatorControllerParameterType.Bool),
            new ParameterSpec(HandPose, AnimatorControllerParameterType.Int),
            new ParameterSpec(Hold, AnimatorControllerParameterType.Int),
            new ParameterSpec(ActionHold, AnimatorControllerParameterType.Bool),
            new ParameterSpec(ArmsAction, AnimatorControllerParameterType.Int),
            new ParameterSpec(ArmsTrigger, AnimatorControllerParameterType.Trigger),
            new ParameterSpec(ArmsHold, AnimatorControllerParameterType.Bool),
            new ParameterSpec(Turn, AnimatorControllerParameterType.Float),
            new ParameterSpec(IdleSet, AnimatorControllerParameterType.Int),
        };

        // ---- layers (names; indices are resolved at runtime with Animator.GetLayerIndex)
        /// <summary>Full-body locomotion, actions and deaths.</summary>
        public const string LayerBase = "Base";
        /// <summary>Arms-only weapon hold and arm actions (mask AM_Arms).</summary>
        public const string LayerArms = "Arms";
        /// <summary>Additive hit reactions (mask AM_UpperBody).</summary>
        public const string LayerHits = "Hits";
        /// <summary>Finger poses (mask AM_Hands).</summary>
        public const string LayerHands = "Hands";

        // ---- state tags
        /// <summary>Tag of every action state (full-body or arms).</summary>
        public const string TagAction = "Action";
        /// <summary>Tag of an action state whose clip could not be resolved: the body plays its procedural fallback.</summary>
        public const string TagProcedural = "Procedural";
        /// <summary>Tag of death states.</summary>
        public const string TagDeath = "Death";
        /// <summary>Tag of locomotion states.</summary>
        public const string TagLocomotion = "Locomotion";

        public static readonly int TagActionHash = Animator.StringToHash(TagAction);
        public static readonly int TagProceduralHash = Animator.StringToHash(TagProcedural);
        public static readonly int TagDeathHash = Animator.StringToHash(TagDeath);
        public static readonly int TagLocomotionHash = Animator.StringToHash(TagLocomotion);

        // ---- death clip indices (AnimatorParams.Death int; 0 = alive)
        /// <summary>Shot from the front: falls backward.</summary>
        public const int DeathBackward = 1;
        /// <summary>Shot from behind: falls forward.</summary>
        public const int DeathForward = 2;
        /// <summary>Explosion: blown back.</summary>
        public const int DeathBlown = 3;
        /// <summary>Torso hit, slow crumple.</summary>
        public const int DeathCrumple = 4;
        /// <summary>Headshot: drops straight down.</summary>
        public const int DeathHead = 5;
        /// <summary>Number of death states (1..DeathCount).</summary>
        public const int DeathCount = 5;

        // ---- hit directions (AnimatorParams.HitDir)
        public const int HitFront = 0;
        public const int HitBack = 1;
        public const int HitLeft = 2;
        public const int HitRight = 3;

        /// <summary>
        /// Hit direction index from the shot direction (world, travelling from the shooter) relative to the body
        /// facing: a bullet travelling along the body's backward axis came from the front.
        /// </summary>
        public static int HitDirFrom(Vector3 bodyForward, Vector3 shotDirection)
        {
            bodyForward.y = 0f;
            shotDirection.y = 0f;
            if (shotDirection.sqrMagnitude < 1e-6f || bodyForward.sqrMagnitude < 1e-6f) return HitFront;
            // The side the shot came from is opposite to its travel direction.
            var from = -shotDirection.normalized;
            var angle = MathX.SignedAngleXZ(bodyForward.normalized, from);
            if (angle > -45f && angle <= 45f) return HitFront;
            if (angle > 45f && angle <= 135f) return HitRight;
            if (angle < -45f && angle >= -135f) return HitLeft;
            return HitBack;
        }

        /// <summary>
        /// Death index for a fatal hit (GDD §14.3 deaths: clip then ragdoll). <paramref name="random01"/> picks between
        /// the two frontal torso deaths (pass <c>Random.value</c>; tests pass a fixed value).
        /// </summary>
        public static int DeathFor(in DamageInfo d, Vector3 bodyForward, float random01)
        {
            if (d.Type == DamageType.Explosion) return DeathBlown;
            if (d.Zone == HitZone.Head) return DeathHead;
            var dir = HitDirFrom(bodyForward, d.Direction);
            if (dir == HitBack) return DeathForward;
            if (dir == HitFront && d.Zone == HitZone.Torso && random01 < 0.4f) return DeathCrumple;
            return DeathBackward;
        }
    }
}
