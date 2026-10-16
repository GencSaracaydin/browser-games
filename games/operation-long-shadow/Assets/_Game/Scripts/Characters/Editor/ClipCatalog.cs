using System.Collections.Generic;
using LongShadow.Characters.Animation;
using UnityEngine;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// The clip library plan (TECH_PLAN §7.8, GDD §14.3 "Animation", §15 "Locomotion"): for every <see cref="ClipKeys"/>
    /// slot an ordered list of candidate clip names across the sources (Meshy rifle clips first for armed soldiers, then
    /// Quaternius UAL, Unity SAC strafes, Rocketbox, HY-Motion signals), role variants for the override controllers, the
    /// nominal speed of locomotion slots (blend positions when a clip cannot be measured) and the generated placeholder
    /// used when nothing has landed. Clip names are the ClipImporter names (A_&lt;Source&gt;[RM]_&lt;Take&gt;[_Mirror]); the
    /// "_Arms"/"_Perch" aliases are identical copies so the arm and perch slots are distinct clips from the idle and walk
    /// (an override controller can then replace the idle without touching the rifle hold).
    /// </summary>
    public static class ClipCatalog
    {
        private static readonly Dictionary<string, string[]> s_candidates = new Dictionary<string, string[]>
        {
            // ---- locomotion (in place)
            [ClipKeys.Idle] = new[] { "A_Meshy_Alert", "A_SAC_StrafeIdle", "A_Meshy_Idle", "A_UAL1_Idle_Loop", "A_Rocketbox_IdleNeutral01", "A_SAC_Idle" },
            [ClipKeys.TurnLeft] = new[] { "A_Meshy_RifleTurnLeft", "A_Meshy_RifleAimTurnRight_Mirror", "A_SAC_TurningLeft" },
            [ClipKeys.TurnRight] = new[] { "A_Meshy_RifleAimTurnRight", "A_Meshy_RifleTurnLeft_Mirror", "A_SAC_TurningLeft_Mirror" },
            [ClipKeys.WalkFwd] = new[] { "A_Meshy_WalkForwardWhileShootingInplace", "A_UAL1_Walk_Loop", "A_SAC_WalkForwards", "A_Rocketbox_WalkNeutral01" },
            [ClipKeys.WalkBack] = new[] { "A_Meshy_WalkBackwardWithGunInplace", "A_Meshy_WalkBackwardWithGun1Inplace", "A_Meshy_WalkBackwardWhileShootingInplace" },
            [ClipKeys.WalkLeft] = new[] { "A_Meshy_WalkLeftWithGunInplace", "A_SAC_StrafeRight_Mirror" },
            [ClipKeys.WalkRight] = new[] { "A_Meshy_WalkLeftWithGunInplace_Mirror", "A_SAC_StrafeRight" },
            [ClipKeys.JogFwd] = new[] { "A_Meshy_RifleChargeInplace", "A_UAL1_Jog_Fwd_Loop", "A_SAC_RunForwards", "A_Rocketbox_RunNeutral01" },
            [ClipKeys.JogLeft] = new[] { "A_SAC_RapidStrafeRight_Mirror" },
            [ClipKeys.JogRight] = new[] { "A_SAC_RapidStrafeRight" },
            [ClipKeys.JogBack] = new string[0],
            [ClipKeys.Sprint] = new[] { "A_Meshy_LeanForwardSprintInplace", "A_UAL1_Sprint_Loop", "A_SAC_SprintForwards" },
            [ClipKeys.CrouchIdle] = new[] { "A_Rocketbox_CrouchIdle", "A_UAL1_Crouch_Idle_Loop" },
            [ClipKeys.CrouchFwd] = new[] { "A_Meshy_CautiousCrouchWalkForwardInplace", "A_UAL1_Crouch_Fwd_Loop" },
            [ClipKeys.CrouchBack] = new[] { "A_Meshy_CautiousCrouchWalkBackwardInplace" },
            [ClipKeys.CrouchLeft] = new[] { "A_Meshy_CautiousCrouchWalkLeftInplace", "A_Meshy_CrouchWalkLeftWithGunInplace" },
            [ClipKeys.CrouchRight] = new[] { "A_Meshy_CautiousCrouchWalkRightInplace", "A_Meshy_CrouchWalkLeftWithGunInplace_Mirror" },
            [ClipKeys.PerchIdle] = new[] { "A_Meshy_Alert_Perch", "A_Meshy_Alert", "A_Meshy_Idle" },
            [ClipKeys.Airborne] = new[] { "A_UAL1_Jump_Loop", "A_UAL2_NinjaJump_Idle_Loop", "A_SAC_Falling" },
            [ClipKeys.AimIdle] = new[] { "A_Meshy_Alert", "A_Meshy_Idle" },
            [ClipKeys.AimWalk] = new[] { "A_Meshy_WalkForwardWhileShootingInplace", "A_UAL1_Walk_Loop" },

            // ---- arms layer
            [ClipKeys.ArmsAim] = new[] { "A_Meshy_WalkForwardWhileShootingInplace_Arms", "A_Meshy_WalkForwardWhileShootingInplace", "A_Meshy_WalkBackwardWhileShootingInplace" },
            [ClipKeys.ArmsLowReady] = new[] { "A_Meshy_Alert_Arms", "A_Meshy_Alert", "A_Meshy_RifleChargeInplace" },
            [ClipKeys.ArmsPistol] = new[] { "A_UAL1_Pistol_Idle_Loop" },
            [ClipKeys.ArmsPistolAim] = new[] { "A_UAL1_Pistol_Aim_Neutral", "A_UAL1_Pistol_Idle_Loop" },
            [ClipKeys.ArmsLauncher] = new[] { "A_Meshy_WalkForwardWhileShootingInplace_Arms", "A_Meshy_Alert_Arms" },
            [ClipKeys.Reload] = new[] { "A_Meshy_StandingReload", "A_Meshy_ForwardReloadSubtle", "A_UAL1_Pistol_Reload" },
            [ClipKeys.ThrowArms] = new[] { "A_UAL2_OverhandThrow", "A_Meshy_CrouchPullAndThrow" },
            [ClipKeys.SignalArms] = new[] { "A_HYMotion_MoveUp", "A_Meshy_GestureWithHandOnGun" },
            [ClipKeys.FistHoldArms] = new[] { "A_HYMotion_FistHold" },
            [ClipKeys.PhoneArms] = new[] { "A_Rocketbox_CellPhoneTalk01", "A_Meshy_PhoneConversation", "A_UAL2_Idle_TalkingPhone_Loop", "A_Meshy_CallGesture" },
            [ClipKeys.InteractArms] = new[] { "A_UAL1_Interact", "A_Rocketbox_DocumentsCheck" },
            [ClipKeys.CameraArms] = new[] { "A_Rocketbox_TakePicture" },
            [ClipKeys.WatchArms] = new[] { "A_HYMotion_CheckWatch" },
            [ClipKeys.OneFingerArms] = new[] { "A_HYMotion_OneFinger" },
            [ClipKeys.WaveArms] = new[] { "A_Meshy_WaveOneHand" },

            // ---- additive hits
            [ClipKeys.HitFront] = new[] { "A_Meshy_HitReaction", "A_UAL1_Hit_Chest" },
            [ClipKeys.HitBack] = new[] { "A_Meshy_GunshotReaction", "A_UAL2_Hit_Knockback" },
            [ClipKeys.HitLeft] = new[] { "A_Meshy_HitReactionToWaist", "A_UAL1_Hit_Head" },
            [ClipKeys.HitRight] = new[] { "A_Meshy_HitReactionToWaist_Mirror", "A_UAL1_Hit_Head" },

            // ---- deaths (blend to ragdoll after 0.2 s, GDD §14.3)
            [ClipKeys.DeathBackward] = new[] { "A_Meshy_ShotAndFallBackward", "A_UAL1_Death01" },
            [ClipKeys.DeathForward] = new[] { "A_Meshy_ShotInTheBackAndFall", "A_Meshy_ShotAndFallForward", "A_UAL1_Death01" },
            [ClipKeys.DeathBlown] = new[] { "A_Meshy_ShotAndBlownBack", "A_Meshy_ShotAndFallBackward" },
            [ClipKeys.DeathCrumple] = new[] { "A_Meshy_FallDeadFromAbdominalInjury", "A_Meshy_ShotAndSlowFallBackward" },
            [ClipKeys.DeathHead] = new[] { "A_Meshy_ShotAndFallForward", "A_Meshy_ShotAndFallBackward" },

            // ---- full-body actions
            [ClipKeys.KneelWork] = new[] { "A_UAL1_Fixing_Kneeling", "A_HYMotion_PlantCharge" },
            [ClipKeys.PlantCharge] = new[] { "A_HYMotion_PlantCharge", "A_UAL1_Fixing_Kneeling" },
            [ClipKeys.ThrowFull] = new[] { "A_Meshy_CrouchPullAndThrow", "A_UAL2_OverhandThrow" },
            [ClipKeys.Kick] = new[] { "A_Meshy_SimpleKick" },
            [ClipKeys.Vault] = new[] { "A_Meshy_VaultWithRifle", "A_Meshy_VaultAndLand" },
            [ClipKeys.Climb] = new[] { "A_UAL2RM_ClimbUp_1m", "A_UAL2_ClimbUp_1m" },
            [ClipKeys.Drop] = new[] { "A_Meshy_JumpingDown", "A_UAL1RM_Jump_Land", "A_UAL1_Jump_Land" },
            [ClipKeys.Ladder] = new string[0],
            [ClipKeys.Carry] = new[] { "A_HYMotion_HaulBag", "A_UAL2_Walk_Carry_Loop" },
            [ClipKeys.SitChair] = new[] { "A_Meshy_ChairSitIdleM", "A_Rocketbox_SitChairIdleNeutral01", "A_UAL1_Sitting_Idle_Loop" },
            [ClipKeys.SitTalk] = new[] { "A_Meshy_SittingAnsweringQuestions", "A_UAL1_Sitting_Talking_Loop" },
            [ClipKeys.SitNervous] = new[] { "A_Rocketbox_SitChairIdleNervous01", "A_Rocketbox_SitTableIdleNervous01", "A_Rocketbox_SitChairBreathe01", "A_Meshy_ChairSitIdleM" },
            [ClipKeys.SitArmsCrossed] = new[] { "A_Meshy_SitOnChairArmsCrossed", "A_Meshy_ChairSitIdleM" },
            [ClipKeys.Prone] = new[] { "A_Meshy_ProneIdle" },
            [ClipKeys.LookAround] = new[] { "A_Meshy_LowerWeaponLookRaise", "A_Rocketbox_IdleLookAround01" },
            [ClipKeys.Kneel] = new[] { "A_Rocketbox_CrouchIdle", "A_UAL1_Crouch_Idle_Loop" },
            [ClipKeys.StandIdle] = new[] { "A_Rocketbox_IdleNeutral01", "A_Rocketbox_IdleBreathe01", "A_Meshy_Idle", "A_UAL1_Idle_Loop" },
            [ClipKeys.StandTalk] = new[] { "A_Meshy_StandAndChat", "A_Rocketbox_GesticTalkNeutral01", "A_UAL1_Idle_Talking_Loop" },
            [ClipKeys.BoothWalk] = new[] { "A_Rocketbox_WalkNeutral01", "A_UAL1_Walk_Loop", "A_SAC_WalkForwards" },

            // ---- first-person arms base pose (both arms are IK'd onto the view model)
            [ClipKeys.FPArmsBase] = new[] { "A_Meshy_Alert_Arms", "A_Meshy_Alert", "A_Meshy_Idle" },

            // ---- finger poses (content FBX first, then FingerPoseAuthoring .anim)
            [ClipKeys.PoseRelaxed] = new[] { ClipKeys.PoseRelaxed },
            [ClipKeys.PoseRifleGrip] = new[] { ClipKeys.PoseRifleGrip },
            [ClipKeys.PosePistolGrip] = new[] { ClipKeys.PosePistolGrip },
            [ClipKeys.PoseFist] = new[] { ClipKeys.PoseFist },
            [ClipKeys.PoseFlat] = new[] { ClipKeys.PoseFlat },
        };

        private static readonly string[] CivilianIdle = { "A_Rocketbox_IdleNeutral01", "A_Rocketbox_IdleBreathe01", "A_Meshy_Idle", "A_UAL1_Idle_Loop" };
        private static readonly string[] CivilianWalk = { "A_Rocketbox_WalkNeutral01", "A_UAL1_Walk_Loop", "A_SAC_WalkForwards" };

        private static readonly Dictionary<ControllerRole, Dictionary<string, string[]>> s_roleVariants =
            new Dictionary<ControllerRole, Dictionary<string, string[]>>
            {
                // Legion irregulars run with the rifle held out (Meshy Run_and_Shoot plays in place in the blend tree).
                [ControllerRole.Legion] = new Dictionary<string, string[]>
                {
                    [ClipKeys.JogFwd] = new[] { "A_Meshy_RunAndShoot", "A_Meshy_RifleChargeInplace" },
                    [ClipKeys.Reload] = new[] { "A_Meshy_ForwardReloadSubtle", "A_Meshy_StandingReload" },
                },
                // Greycoat advisors: the subtle aimed reload; otherwise the base set.
                [ControllerRole.Greycoat] = new Dictionary<string, string[]>
                {
                    [ClipKeys.Reload] = new[] { "A_Meshy_ForwardReloadSubtle", "A_Meshy_StandingReload" },
                },
                // The squad idles relaxed (Meshy's alert idle stands on deeply bent knees, WP05_ART §5); arms stay on the rifle.
                [ControllerRole.Squad] = new Dictionary<string, string[]>
                {
                    [ClipKeys.Idle] = new[] { "A_SAC_StrafeIdle", "A_Rocketbox_IdleNeutral01", "A_Meshy_Alert" },
                },
                // Unarmed people: neutral idles, walks and runs (the arms layer is off for unarmed bodies).
                [ControllerRole.Civilian] = new Dictionary<string, string[]>
                {
                    [ClipKeys.Idle] = CivilianIdle,
                    [ClipKeys.WalkFwd] = CivilianWalk,
                    [ClipKeys.JogFwd] = new[] { "A_Rocketbox_RunNeutral01", "A_UAL1_Jog_Fwd_Loop", "A_SAC_RunForwards" },
                    [ClipKeys.Sprint] = new[] { "A_UAL1_Sprint_Loop", "A_SAC_SprintForwards" },
                    [ClipKeys.WalkLeft] = new[] { "A_SAC_StrafeRight_Mirror" },
                    [ClipKeys.WalkRight] = new[] { "A_SAC_StrafeRight" },
                    [ClipKeys.CrouchFwd] = new[] { "A_UAL1_Crouch_Fwd_Loop" },
                },
            };

        /// <summary>Every slot with candidates.</summary>
        public static IEnumerable<string> Keys => s_candidates.Keys;

        /// <summary>Candidates for a slot (empty when unknown).</summary>
        public static IReadOnlyList<string> CandidatesFor(string key) =>
            key != null && s_candidates.TryGetValue(key, out var c) ? c : (IReadOnlyList<string>)new string[0];

        /// <summary>Role-specific candidates for a slot, or null when the role uses the base list.</summary>
        public static IReadOnlyList<string> VariantFor(ControllerRole role, string key)
        {
            if (key == null || !s_roleVariants.TryGetValue(role, out var map)) return null;
            return map.TryGetValue(key, out var c) ? c : null;
        }

        /// <summary>Slots that a role overrides.</summary>
        public static IEnumerable<string> VariantKeys(ControllerRole role) =>
            s_roleVariants.TryGetValue(role, out var map) ? map.Keys : (IEnumerable<string>)new string[0];

        /// <summary>
        /// Nominal blend position (m/s; x = right, y = forward) of a locomotion slot: used when the clip's foot speed cannot
        /// be measured. Walks ≈ 1.3, rifle jog ≈ 3.4, sprint ≈ 5.8, crouch walk ≈ 1.0 (Meshy/UAL clips at their authored cadence).
        /// </summary>
        public static Vector2 NominalVelocity(string key)
        {
            switch (key)
            {
                case ClipKeys.WalkFwd: return new Vector2(0f, 1.35f);
                case ClipKeys.WalkBack: return new Vector2(0f, -1.15f);
                case ClipKeys.WalkLeft: return new Vector2(-1.25f, 0f);
                case ClipKeys.WalkRight: return new Vector2(1.25f, 0f);
                case ClipKeys.JogFwd: return new Vector2(0f, 3.4f);
                case ClipKeys.JogBack: return new Vector2(0f, -2.6f);
                case ClipKeys.JogLeft: return new Vector2(-3.0f, 0f);
                case ClipKeys.JogRight: return new Vector2(3.0f, 0f);
                case ClipKeys.Sprint: return new Vector2(0f, 5.8f);
                case ClipKeys.CrouchFwd: return new Vector2(0f, 1.0f);
                case ClipKeys.CrouchBack: return new Vector2(0f, -0.9f);
                case ClipKeys.CrouchLeft: return new Vector2(-0.95f, 0f);
                case ClipKeys.CrouchRight: return new Vector2(0.95f, 0f);
                case ClipKeys.AimWalk: return new Vector2(0f, 1.35f);
                default: return Vector2.zero;
            }
        }

        /// <summary>The generated stand-in for a slot.</summary>
        public static PlaceholderKind PlaceholderFor(string key)
        {
            switch (key)
            {
                case ClipKeys.WalkFwd:
                case ClipKeys.WalkBack:
                case ClipKeys.WalkLeft:
                case ClipKeys.WalkRight:
                case ClipKeys.AimWalk:
                case ClipKeys.BoothWalk:
                case ClipKeys.Carry:
                    return PlaceholderKind.Walk;
                case ClipKeys.JogFwd:
                case ClipKeys.JogBack:
                case ClipKeys.JogLeft:
                case ClipKeys.JogRight:
                    return PlaceholderKind.Jog;
                case ClipKeys.Sprint: return PlaceholderKind.Sprint;
                case ClipKeys.CrouchIdle:
                case ClipKeys.Kneel:
                    return PlaceholderKind.CrouchIdle;
                case ClipKeys.CrouchFwd:
                case ClipKeys.CrouchBack:
                case ClipKeys.CrouchLeft:
                case ClipKeys.CrouchRight:
                    return PlaceholderKind.CrouchWalk;
                case ClipKeys.ArmsAim:
                case ClipKeys.ArmsLowReady:
                case ClipKeys.ArmsLauncher:
                case ClipKeys.Reload:
                case ClipKeys.FPArmsBase:
                case ClipKeys.InteractArms:
                case ClipKeys.CameraArms:
                case ClipKeys.ThrowArms:
                case ClipKeys.WatchArms:
                    return PlaceholderKind.ArmsRifle;
                case ClipKeys.ArmsPistol:
                case ClipKeys.ArmsPistolAim:
                    return PlaceholderKind.ArmsPistol;
                case ClipKeys.SitChair:
                case ClipKeys.SitTalk:
                case ClipKeys.SitNervous:
                case ClipKeys.SitArmsCrossed:
                    return PlaceholderKind.Sit;
                case ClipKeys.KneelWork:
                case ClipKeys.PlantCharge:
                    return PlaceholderKind.Kneel;
                case ClipKeys.Prone: return PlaceholderKind.Prone;
                case ClipKeys.SignalArms:
                case ClipKeys.FistHoldArms:
                case ClipKeys.PhoneArms:
                case ClipKeys.OneFingerArms:
                case ClipKeys.WaveArms:
                    return PlaceholderKind.Signal;
                case ClipKeys.DeathBackward:
                case ClipKeys.DeathForward:
                case ClipKeys.DeathBlown:
                case ClipKeys.DeathCrumple:
                case ClipKeys.DeathHead:
                    return PlaceholderKind.Slump;
                case ClipKeys.HitFront:
                case ClipKeys.HitBack:
                case ClipKeys.HitLeft:
                case ClipKeys.HitRight:
                    return PlaceholderKind.Additive;
                case ClipKeys.Airborne: return PlaceholderKind.Airborne;
                default: return PlaceholderKind.Stand;
            }
        }
    }
}
