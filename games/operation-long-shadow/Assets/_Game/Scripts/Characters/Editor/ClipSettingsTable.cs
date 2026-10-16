using System;

namespace LongShadow.Characters.Editor
{
    /// <summary>
    /// Import settings per clip name (TECH_PLAN D12: tables are code). Generated from the content pipeline's final
    /// RawAssets/wp05/ready/import_index.json (loop, root motion, rotation bake, Foot_L/Foot_R contact times measured on the
    /// baked clips, SAC mirror and root-height notes) plus the Quaternius UAL takes the catalog uses, with WP05's deliberate
    /// overrides: the Meshy turn clips loop for the idle turn blend, Meshy left strafes/hits get mirrored duplicates, hit
    /// reactions are additive, the UAL drill (Fixing_Kneeling) loops (GDD §15), and A_Meshy_Alert / the aimed walk get
    /// alias copies so arm and perch slots are distinct clips. Clips not listed fall back to <see cref="ClipRules"/> naming
    /// heuristics. Mirror rows also produce a "_Mirror" duplicate.
    /// </summary>
    public static class ClipSettingsTable
    {
        private static readonly ClipSetting[] s_rows =
        {
            new ClipSetting("A_HYMotion_CheckWatch", false, false),
            new ClipSetting("A_HYMotion_CrouchRunCover", false, true),
            new ClipSetting("A_HYMotion_FistHold", false, false),
            new ClipSetting("A_HYMotion_HaulBag", false, false),
            new ClipSetting("A_HYMotion_MoveUp", false, false),
            new ClipSetting("A_HYMotion_OneFinger", false, false),
            new ClipSetting("A_HYMotion_PlantCharge", false, false),
            new ClipSetting("A_Meshy_Alert", true, false, aliases: new[] { "_Arms", "_Perch" }),
            new ClipSetting("A_Meshy_AlertQuickTurnRight", false, false, bakeRotation: false),
            new ClipSetting("A_Meshy_CallGesture", false, false),
            new ClipSetting("A_Meshy_CautiousCrouchWalkBackwardInplace", true, false, footL: new[] { 0.342f }, footR: new[] { 0.816f }),
            new ClipSetting("A_Meshy_CautiousCrouchWalkForwardInplace", true, false, footL: new[] { 0.235f }, footR: new[] { 0.647f }),
            new ClipSetting("A_Meshy_CautiousCrouchWalkLeftInplace", true, false, footL: new[] { 0.4f }, footR: new[] { 0.714f }, mirror: true),
            new ClipSetting("A_Meshy_CautiousCrouchWalkRightInplace", true, false, footL: new[] { 0.158f }, footR: new[] { 0.711f }),
            new ClipSetting("A_Meshy_ChairSitIdleM", true, false),
            new ClipSetting("A_Meshy_CombatIdleTurnLeft", false, false, bakeRotation: false),
            new ClipSetting("A_Meshy_CrouchAndStepBack", false, false),
            new ClipSetting("A_Meshy_CrouchPickGunPointForward", false, false),
            new ClipSetting("A_Meshy_CrouchPullAndThrow", false, false),
            new ClipSetting("A_Meshy_CrouchWalkLeftWithGunInplace", true, false, footL: new[] { 0.211f }, footR: new[] { 0.789f }, mirror: true),
            new ClipSetting("A_Meshy_FallDeadFromAbdominalInjury", false, true),
            new ClipSetting("A_Meshy_ForwardReloadSubtle", false, false),
            new ClipSetting("A_Meshy_GestureWithHandOnGun", false, false),
            new ClipSetting("A_Meshy_GunHoldLeftTurn", false, false, bakeRotation: false),
            new ClipSetting("A_Meshy_GunshotReaction", false, false, additive: true),
            new ClipSetting("A_Meshy_HitReaction", false, false, additive: true),
            new ClipSetting("A_Meshy_HitReactionToWaist", false, false, mirror: true, additive: true),
            new ClipSetting("A_Meshy_Idle", true, false),
            new ClipSetting("A_Meshy_JumpingDown", false, true),
            new ClipSetting("A_Meshy_KneelOnOneKneeAndStand", false, false),
            new ClipSetting("A_Meshy_KneelingReload", false, false),
            new ClipSetting("A_Meshy_LeanForwardSprintInplace", true, false, footL: new[] { 0.235f }, footR: new[] { 0.765f }),
            new ClipSetting("A_Meshy_LowerWeaponLookRaise", false, false),
            new ClipSetting("A_Meshy_PhoneConversation", true, false),
            new ClipSetting("A_Meshy_ProneIdle", true, false),
            new ClipSetting("A_Meshy_ProneReload", false, false),
            new ClipSetting("A_Meshy_RifleAimTurnRight", true, false, bakeRotation: false, mirror: true),
            new ClipSetting("A_Meshy_RifleChargeInplace", true, false, footL: new[] { 0.333f }, footR: new[] { 0.8f }),
            new ClipSetting("A_Meshy_RifleTurnLeft", true, false, bakeRotation: false, mirror: true),
            new ClipSetting("A_Meshy_RollBehindCover", false, true),
            new ClipSetting("A_Meshy_RunAndShoot", true, true, footL: new[] { 0.65f }, footR: new[] { 0.15f }),
            new ClipSetting("A_Meshy_RunningReloadInplace", true, false, footL: new[] { 0.027f, 0.236f, 0.436f, 0.627f, 0.836f }, footR: new[] { 0.127f, 0.327f, 0.527f, 0.727f, 0.927f }),
            new ClipSetting("A_Meshy_ShotAndBlownBack", false, true),
            new ClipSetting("A_Meshy_ShotAndFallBackward", false, true),
            new ClipSetting("A_Meshy_ShotAndFallForward", false, true),
            new ClipSetting("A_Meshy_ShotAndSlowFallBackward", false, true),
            new ClipSetting("A_Meshy_ShotInTheBackAndFall", false, true),
            new ClipSetting("A_Meshy_SimpleKick", false, false),
            new ClipSetting("A_Meshy_SitOnChairArmsCrossed", true, false),
            new ClipSetting("A_Meshy_SittingAnsweringQuestions", true, false),
            new ClipSetting("A_Meshy_SlowWalkReloadInplace", true, false, footL: new[] { 0.041f, 0.374f, 0.707f }, footR: new[] { 0.203f, 0.545f, 0.87f }),
            new ClipSetting("A_Meshy_StandAndChat", true, false),
            new ClipSetting("A_Meshy_StandingReload", false, false),
            new ClipSetting("A_Meshy_VaultAndLand", false, true),
            new ClipSetting("A_Meshy_VaultWithRifle", false, true),
            new ClipSetting("A_Meshy_WalkBackwardWhileShootingInplace", true, false, footL: new[] { 0.605f }, footR: new[] { 0.237f }),
            new ClipSetting("A_Meshy_WalkBackwardWithGun1Inplace", true, false, footL: new[] { 0.333f }, footR: new[] { 0.833f }),
            new ClipSetting("A_Meshy_WalkBackwardWithGunInplace", true, false, footL: new[] { 0.367f }, footR: new[] { 0.8f }),
            new ClipSetting("A_Meshy_WalkForwardWhileShootingInplace", true, false, footL: new[] { 0.143f }, footR: new[] { 0.735f }, aliases: new[] { "_Arms" }),
            new ClipSetting("A_Meshy_WalkLeftWithGunInplace", true, false, footL: new[] { 0.029f }, footR: new[] { 0.543f, 0.657f }, mirror: true),
            new ClipSetting("A_Meshy_WalkSlowlyAndLookAround", true, true, footL: new[] { 0.105f, 0.213f, 0.324f, 0.43f, 0.541f, 0.651f, 0.758f, 0.867f, 0.894f }, footR: new[] { 0.158f, 0.266f, 0.38f, 0.479f, 0.494f, 0.595f, 0.706f, 0.724f, 0.808f, 0.819f, 0.934f }),
            new ClipSetting("A_Meshy_WaveOneHand", false, false),
            new ClipSetting("A_Rocketbox_CellPhoneListen01", true, false),
            new ClipSetting("A_Rocketbox_CellPhoneTalk01", true, false),
            new ClipSetting("A_Rocketbox_CrouchIdle", true, false),
            new ClipSetting("A_Rocketbox_CrouchIn", false, false),
            new ClipSetting("A_Rocketbox_DocumentsCheck", false, false),
            new ClipSetting("A_Rocketbox_FIdleLookAround01", true, false),
            new ClipSetting("A_Rocketbox_FIdleNeutral01", true, false),
            new ClipSetting("A_Rocketbox_GesticListenNeutral01", true, false),
            new ClipSetting("A_Rocketbox_GesticTalkNeutral01", true, false),
            new ClipSetting("A_Rocketbox_GesticTalkNeutral02", true, false),
            new ClipSetting("A_Rocketbox_GesticTalkRelaxed01", true, false),
            new ClipSetting("A_Rocketbox_IdleBreathe01", true, false),
            new ClipSetting("A_Rocketbox_IdleLookAround01", true, false),
            new ClipSetting("A_Rocketbox_IdleLookAround02", true, false),
            new ClipSetting("A_Rocketbox_IdleNeutral01", true, false),
            new ClipSetting("A_Rocketbox_RunNeutral01", true, false, footL: new[] { 0.727f }, footR: new[] { 0.318f }),
            new ClipSetting("A_Rocketbox_SitChairBreathe01", true, false),
            new ClipSetting("A_Rocketbox_SitChairIdleCough", false, false),
            new ClipSetting("A_Rocketbox_SitChairIdleLookAround", true, false),
            new ClipSetting("A_Rocketbox_SitChairIdleNervous01", true, false),
            new ClipSetting("A_Rocketbox_SitChairIdleNeutral01", true, false),
            new ClipSetting("A_Rocketbox_SitChairIdleRollHead", false, false),
            new ClipSetting("A_Rocketbox_SitChairIdleWaiting01", true, false),
            new ClipSetting("A_Rocketbox_SitTableBreathe01", true, false),
            new ClipSetting("A_Rocketbox_SitTableIdleLookAround", true, false),
            new ClipSetting("A_Rocketbox_SitTableIdleNervous01", true, false),
            new ClipSetting("A_Rocketbox_TakePicture", false, false),
            new ClipSetting("A_Rocketbox_WalkNeutral01", true, false, footL: new[] { 0.639f }, footR: new[] { 0.167f }),
            new ClipSetting("A_SAC_FallLand", false, true, originalHeight: true, firstFrame: 2, lastFrame: 39),
            new ClipSetting("A_SAC_FallLandRoll", false, true, bakeRotation: false, originalHeight: true, firstFrame: 2, lastFrame: 40),
            new ClipSetting("A_SAC_Falling", true, false, originalHeight: true, firstFrame: 2, lastFrame: 41),
            new ClipSetting("A_SAC_Idle", true, false, originalHeight: true, firstFrame: 2, lastFrame: 92),
            new ClipSetting("A_SAC_IdleLeftTurn180", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 62),
            new ClipSetting("A_SAC_RapidStrafeForwards", true, false, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidStrafeRight", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidStrafeRightRunBackwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidStrafeRightRunForward", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidStrafeRightWalkBackwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidStrafeRightWalkFowards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_RapidTurningLeft", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 47),
            new ClipSetting("A_SAC_RunForwardTurnLeft180", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 36),
            new ClipSetting("A_SAC_RunForwards", true, false, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_SprintForwards", true, false, originalHeight: true, firstFrame: 2, lastFrame: 18),
            new ClipSetting("A_SAC_StrafeIdle", true, false, originalHeight: true, firstFrame: 2, lastFrame: 92),
            new ClipSetting("A_SAC_StrafeRight", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_SAC_StrafeRightRunBackwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_StrafeRightRunForwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_StrafeRightTurnLeft", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_SAC_StrafeRightWalkBackwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 17),
            new ClipSetting("A_SAC_StrafeRightWalkForwards", true, false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_SAC_StrafeWalkForwards", true, false, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_SAC_TurningLeft", true, false, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 62),
            new ClipSetting("A_SAC_TurningLeftRunForward", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 20),
            new ClipSetting("A_SAC_TurningLeftWalkForward", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 31),
            new ClipSetting("A_SAC_WalkForwardTurnLeft180", false, true, bakeRotation: false, mirror: true, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_SAC_WalkForwards", true, false, originalHeight: true, firstFrame: 2, lastFrame: 32),
            new ClipSetting("A_UAL1RM_Jump_Land", false, true),
            new ClipSetting("A_UAL1RM_Roll", false, true),
            new ClipSetting("A_UAL1_Crouch_Fwd_Loop", true, false),
            new ClipSetting("A_UAL1_Crouch_Idle_Loop", true, false),
            new ClipSetting("A_UAL1_Death01", false, false),
            new ClipSetting("A_UAL1_Fixing_Kneeling", true, false),
            new ClipSetting("A_UAL1_Hit_Chest", false, false, additive: true),
            new ClipSetting("A_UAL1_Hit_Head", false, false, additive: true),
            new ClipSetting("A_UAL1_Idle_Loop", true, false),
            new ClipSetting("A_UAL1_Idle_Talking_Loop", true, false),
            new ClipSetting("A_UAL1_Interact", false, false),
            new ClipSetting("A_UAL1_Jog_Fwd_Loop", true, false),
            new ClipSetting("A_UAL1_Jump_Land", false, false),
            new ClipSetting("A_UAL1_Jump_Loop", true, false),
            new ClipSetting("A_UAL1_Pistol_Aim_Neutral", false, false),
            new ClipSetting("A_UAL1_Pistol_Idle_Loop", true, false),
            new ClipSetting("A_UAL1_Pistol_Reload", false, false),
            new ClipSetting("A_UAL1_Sitting_Idle_Loop", true, false),
            new ClipSetting("A_UAL1_Sitting_Talking_Loop", true, false),
            new ClipSetting("A_UAL1_Sprint_Loop", true, false),
            new ClipSetting("A_UAL1_Walk_Loop", true, false),
            new ClipSetting("A_UAL2RM_ClimbUp_1m", false, true),
            new ClipSetting("A_UAL2_ClimbUp_1m", false, false),
            new ClipSetting("A_UAL2_Hit_Knockback", false, false, additive: true),
            new ClipSetting("A_UAL2_Idle_TalkingPhone_Loop", true, false),
            new ClipSetting("A_UAL2_NinjaJump_Idle_Loop", true, false),
            new ClipSetting("A_UAL2_OverhandThrow", false, false),
            new ClipSetting("A_UAL2_Walk_Carry_Loop", true, false),
        };

        /// <summary>Every row.</summary>
        public static ClipSetting[] All => s_rows;

        /// <summary>Finds the row for a clip name.</summary>
        public static bool TryGet(string clipName, out ClipSetting row)
        {
            foreach (var r in s_rows)
                if (string.Equals(r.Name, clipName, StringComparison.Ordinal))
                {
                    row = r;
                    return true;
                }
            row = default;
            return false;
        }
    }
}
