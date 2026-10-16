"""Clip tables for build_clips.py (WP05).

Meshy clips are listed by library action id; their source FBX is found through each take's result.json.
Per clip: name (A_<Source>_<Name>), loop, root ("full" | "inplace_xy" | "fixed"), rm (True = the clip travels: import
with Root XZ NOT baked so applyRootMotion can move the body during actions), fingers (finger pose baked into the
clip when the source has no finger bones), use (where AC_Soldier / cinematics use it).
"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
W5 = ROOT / "RawAssets/wp05"
RB_CLIPS_LOCAL = ROOT / "RawAssets/cc0/characters/rocketbox/Animations"
RB_CLIPS_DL = W5 / "rocketbox/Animations/static"

R, RX, F = "RifleGrip", "Relaxed", "Fist"

# action id: (name, loop, root, rm, fingers, use)
MESHY = {
    # ---- owned research clips (Meshy soldier rig, RawAssets/fal3d/05_soldier_meshy_rig_multianim)
    0: ("Idle", True, "full", False, RX, "unarmed idle (Meshy)"),
    170: ("StandingReload", False, "full", False, R, "L1 UpperBody reload (standing)"),
    183: ("ShotAndFallBackward", False, "full", True, RX, "death (blend to ragdoll after 0.2 s)"),
    654: ("RifleChargeInplace", True, "full", False, R, "rifle run / charge; INSERTION crouch-run (I-9)"),
    690: ("WalkForwardWhileShootingInplace", True, "full", False, R, "aim walk forward"),
    # ---- set A
    165: ("KneelingReload", False, "full", False, R, "reload while crouched/kneeling"),
    585: ("RifleAimTurnRight", False, "full", False, R, "turn in place, aiming"),
    425: ("VaultWithRifle", False, "full", True, R, "vault over gaps (squad traversal)"),
    685: ("WalkBackwardWithGunInplace", True, "full", False, R, "aim walk backward"),
    694: ("WalkLeftWithGunInplace", True, "full", False, R, "aim strafe left (mirror for right)"),
    184: ("ShotAndFallForward", False, "full", True, RX, "death"),
    182: ("ShotAndBlownBack", False, "full", True, RX, "death (explosion / shotgun)"),
    239: ("CrouchPullAndThrow", False, "full", False, RX, "grenade throw from cover"),
    426: ("RollBehindCover", False, "full", True, R, "dive/roll to cover"),
    171: ("HitReactionToWaist", False, "full", False, R, "L2 additive hit (waist)"),
    # ---- set B
    623: ("CrouchWalkLeftWithGunInplace", True, "full", False, R, "crouch strafe left (mirror for right)"),
    686: ("WalkBackwardWithGun1Inplace", True, "full", False, R, "aim walk backward (variant)"),
    680: ("WalkBackwardWhileShootingInplace", True, "full", False, R, "retreat firing"),
    644: ("LeanForwardSprintInplace", True, "full", False, R, "sprint"),
    666: ("RunningReloadInplace", True, "full", False, R, "reload on the move"),
    185: ("ShotAndSlowFallBackward", False, "full", True, RX, "death (slow)"),
    180: ("ShotInTheBackAndFall", False, "full", True, RX, "death (hit from behind)"),
    177: ("GunshotReaction", False, "full", False, R, "L2 additive hit"),
    178: ("HitReaction", False, "full", False, R, "L2 additive hit"),
    2: ("Alert", True, "full", False, R, "alert idle"),
    # ---- set B2
    573: ("RifleTurnLeft", False, "full", False, R, "turn in place, rifle"),
    95: ("GunHoldLeftTurn", False, "full", False, R, "turn in place, rifle held"),
    169: ("ForwardReloadSubtle", False, "full", False, R, "reload (subtle, aim stance)"),
    670: ("SlowWalkReloadInplace", True, "full", False, R, "reload while walking"),
    188: ("FallDeadFromAbdominalInjury", False, "full", True, RX, "death (slow, gut shot)"),
    616: ("CautiousCrouchWalkForwardInplace", True, "full", False, R, "crouch walk forward"),
    618: ("CautiousCrouchWalkRightInplace", True, "full", False, R, "crouch walk right"),
    334: ("LowerWeaponLookRaise", False, "full", False, R, "idle variation"),
    168: ("ProneReload", False, "full", False, R, "prone reload; A_Meshy_ProneIdle is derived from it"),
    617: ("CautiousCrouchWalkLeftInplace", True, "full", False, R, "crouch walk left"),
    # ---- set C
    427: ("VaultAndLand", False, "full", True, RX, "vault and land (unarmed hands)"),
    103: ("SimpleKick", False, "full", False, R, "door kick (B6 breach)"),
    307: ("SittingAnsweringQuestions", True, "full", False, RX, "booth: seated answering"),
    312: ("PhoneConversation", True, "full", False, RX, "radio handset (Tinker), phone"),
    56: ("StandAndChat", True, "full", False, RX, "briefing / chat"),
    33: ("ChairSitIdleM", True, "full", False, RX, "seated idle; INSERTION sit in helicopter (I-2..I-6)"),
    290: ("WaveOneHand", False, "full", False, RX, "wave"),
    365: ("KneelOnOneKneeAndStand", False, "full", False, R, "kneel and stand"),
    341: ("WalkSlowlyAndLookAround", True, "full", True, RX, "patrol look-around walk"),
    364: ("SitOnChairArmsCrossed", True, "full", False, RX, "seated, arms crossed (interrogator / booth)"),
    # ---- insertion set
    470: ("JumpingDown", False, "full", True, R, "INSERTION I-8: drop from the skid (cut on landing)"),
    292: ("GestureWithHandOnGun", False, "full", False, R, "INSERTION I-9 hand signal fallback"),
    29: ("CallGesture", False, "full", False, RX, "hand to the ear (radio handset / call), not a come-here wave"),
    615: ("CautiousCrouchWalkBackwardInplace", True, "full", False, R, "crouch walk backward"),
    98: ("RunAndShoot", True, "full", True, R, "run and shoot"),
    589: ("AlertQuickTurnRight", False, "full", False, R, "alert snap turn"),
    575: ("CombatIdleTurnLeft", False, "full", False, R, "combat turn left"),
    279: ("CrouchPickGunPointForward", False, "full", False, R, "crouch, pick a rifle up from the ground, point it forward"),
    404: ("CrouchAndStepBack", False, "full", False, R, "crouch and step back"),
    # 506 Dive_Down_and_Land was bought but is a dive off a ledge (lands ~0.5 m lower): not shipped
}
MESHY_SETS = ["ANIM_MESHY_SET_A", "ANIM_MESHY_SET_B", "ANIM_MESHY_SET_B2", "ANIM_MESHY_SET_C", "ANIM_INSERTION_SET"]
MESHY_OWNED = {0: "anim_0_idle.fbx", 170: "anim_170_standing_reload.fbx", 183: "anim_183_shot_fall_backward.fbx",
               654: "anim_654_rifle_charge_inplace.fbx", 690: "anim_690_walk_fwd_shooting_inplace.fbx"}
MESHY_MANIFEST = {}
for _i in (0, 170, 183, 654, 690):
    MESHY_MANIFEST[_i] = "ANIM_MESHY_OWNED"

# Rocketbox Biped clips (MIT): file stem -> (name, loop, rm, use, manifest)
ROCKETBOX = {
    "m_sit_table_idle_nervous_01": ("SitTableIdleNervous01", True, False, "booth prisoner (34 s)", "ANIM_ROCKETBOX_LOCAL"),
    "m_idle_look_around_01": ("IdleLookAround01", True, False, "idle variation", "ANIM_ROCKETBOX_LOCAL"),
    "m_walk_neutral_01": ("WalkNeutral01", True, False, "interrogator walk-past (outro)", "ANIM_ROCKETBOX_LOCAL"),
    "m_run_neutral_01": ("RunNeutral01", True, False, "unarmed run", "ANIM_ROCKETBOX_LOCAL"),
    "m_crouch_in": ("CrouchIn", False, False, "stand -> crouch", "ANIM_ROCKETBOX_LOCAL"),
    "m_crouch_idle": ("CrouchIdle", True, False, "crouch idle", "ANIM_ROCKETBOX_LOCAL"),
    "m_sit_chair_idle_nervous_01": ("SitChairIdleNervous01", True, False, "booth prisoner (chair)", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_breathe_01": ("SitChairBreathe01", True, False, "booth prisoner breathing", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_idle_look_around": ("SitChairIdleLookAround", True, False, "booth / heli seat", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_idle_cough": ("SitChairIdleCough", False, False, "booth prisoner cough", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_idle_roll_head": ("SitChairIdleRollHead", False, False, "booth prisoner", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_idle_neutral_01": ("SitChairIdleNeutral01", True, False, "seated idle (heli bench)", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_chair_idle_waiting_01": ("SitChairIdleWaiting01", True, False, "seated waiting", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_table_breathe_01": ("SitTableBreathe01", True, False, "booth at table", "ANIM_ROCKETBOX_BOOTH"),
    "m_sit_table_idle_look_around": ("SitTableIdleLookAround", True, False, "booth at table", "ANIM_ROCKETBOX_BOOTH"),
    "m_gestic_talk_neutral_01": ("GesticTalkNeutral01", True, False, "interrogator / briefing", "ANIM_ROCKETBOX_BOOTH"),
    "m_gestic_talk_neutral_02": ("GesticTalkNeutral02", True, False, "interrogator / briefing", "ANIM_ROCKETBOX_BOOTH"),
    "m_gestic_talk_relaxed_01": ("GesticTalkRelaxed01", True, False, "briefing", "ANIM_ROCKETBOX_BOOTH"),
    "m_gestic_listen_neutral_01": ("GesticListenNeutral01", True, False, "listening", "ANIM_ROCKETBOX_BOOTH"),
    "m_cell_phone_talk_01": ("CellPhoneTalk01", True, False, "radio handset to ear (Tinker, I-6)", "ANIM_ROCKETBOX_BOOTH"),
    "m_cell_phone_listen_01": ("CellPhoneListen01", True, False, "radio handset listening", "ANIM_ROCKETBOX_BOOTH"),
    "m_take_picture": ("TakePicture", False, False, "Sparrow camera raise (I-5, B1, B8)", "ANIM_ROCKETBOX_BOOTH"),
    "m_documents_check": ("DocumentsCheck", False, False, "intel / codebook", "ANIM_ROCKETBOX_BOOTH"),
    "m_idle_neutral_01": ("IdleNeutral01", True, False, "neutral idle", "ANIM_ROCKETBOX_BOOTH"),
    "m_idle_breathe_01": ("IdleBreathe01", True, False, "breathing idle", "ANIM_ROCKETBOX_BOOTH"),
    "m_idle_look_around_02": ("IdleLookAround02", True, False, "idle variation", "ANIM_ROCKETBOX_BOOTH"),
    "f_idle_neutral_01": ("FIdleNeutral01", True, False, "Sparrow idle", "ANIM_ROCKETBOX_BOOTH"),
    "f_idle_look_around_01": ("FIdleLookAround01", True, False, "Sparrow idle variation", "ANIM_ROCKETBOX_BOOTH"),
}
