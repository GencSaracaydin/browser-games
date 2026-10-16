# WP05_ART handoff: characters, character props, humanoid clips, fabrics

Content agent for WP05 (no editor use). Everything import-ready lives in
`RawAssets/wp05/ready/` mirroring the Unity paths, with one row per file in
`RawAssets/wp05/ready/import_index.json` (295 rows: manifest id, file, target path, type, import notes).
Status: **all P0 and P1 entries of the package are done, plus the P2 Magpie.** Only `CHR_FALLBACK_MESHY` (P2) was not built,
because O2 was approved and every role uses a Rocketbox avatar.
fal spend: **$22.48 of the $90 WP05 cap** (40 calls; this resume pass added 4 calls for $2.80).

Review sheets (look at these first): `RawAssets/wp05/_review/`
- `ALL_bodies_overview.png`: every body side by side. Per body: `<Name>_sheet.png` (front, side, back, 3/4, head close-up).
- `CalderFP_firstperson_views.png`: the FP arms seen from the eye at level / −40° / −70° / +30° / yawed.
- Clips: `clips_meshy_0..4.png`, `clips_hymotion_0.png`, `clips_rocketbox_0..2.png` (4 frames per clip on Calder holding the
  WP03 AK), `clips_poses_0.png` (the finger poses, both hands).

---

## 1. Inventory

### 1.1 Bodies (`Art/Models/Characters/<Folder>/`, textures in `Art/Textures/Characters/<Folder>/`)
All are Rocketbox (MIT) 80-bone Biped skeletons fixed for Humanoid, metres, Unity axes, with props as separate skinned
renderers in the same FBX. Heights: men 1.80 m (to the crown; hats/hair make 1.80–1.83), Sparrow 1.675 m.

| File | Manifest | Avatar | Tris | Textures (in game) | Look |
|---|---|---|---|---|---|
| `Calder/SK_Calder.fbx` | CHR_CALDER | Military_Male_01 | 12.0k | Body 1024, Head 1024 | tan poplin shirt, olive trousers, olive canvas vest, Nomex gloves, chest rig, boonie. Also the shadow proxy and the third-person body in the INSERTION cinematic. |
| `Calder/SK_Calder_FPArms.fbx` | CHR_CALDER (fp_arms_only) | same skeleton | 1.8k | shares T_Calder_Body | sleeves + gloved hands only |
| `Calder/SK_Calder_Booth.fbx` | CHR_CALDER (booth_seated) | same | 8.4k | CalderBooth Body 1024 + Calder Head | prisoner: plain field shirt (vest shell slimmed), bare hands, `SK_Calder_Booth_Cuffs` = leather wrist cuffs, steel buckles, taped electrodes (256 atlas `T_Calder_Restraints_*`) |
| `T_CalderReel_Head_*` (Calder folder) | CHR_CALDER (reel_shaved_head) | — | — | 1024 | shaved, gaunt "Session 11" head: material swap on the booth body |
| `Hollis/SK_Hollis.fbx` | CHR_HOLLIS | Male_Adult_05 | 11.6k | Body 1024, Head 2048, Hair 512 | grey-stubbled 50s face, tan shirt, olive field vest, boonie, chest rig |
| `Tinker/SK_Tinker.fbx` | CHR_TINKER | Male_Adult_08 | 13.6k | Body 1024, Head 1024, Hair 512 | rolled-sleeve khaki shirt, chest rig, PRC-77 on the back (WP10 prop, decimated copy), gold aviators |
| `Sparrow/SK_Sparrow.fbx` | CHR_SPARROW | Military_Female_02 | 9.0k | Body 1024, Head 1024 | tan shirt, khaki trousers, olive vest, olive ball cap (bun under it) |
| `LegionA/SK_LegionA.fbx` | CHR_LEGION_A | Military_Male_02 | 11.8k | 1024 source / **512 in game** | faded khaki drill, khaki beret, chest rig, orange armband |
| `LegionB/SK_LegionB.fbx` | CHR_LEGION_B | Military_Male_05 | 12.3k | 512 in game | tiger-stripe shirt, olive trousers, boonie + aviator dust goggles, chest rig, armband |
| `LegionC/SK_LegionC.fbx` | CHR_LEGION_C | Military_Male_06 | 12.0k | 512 in game | olive shirt, khaki trousers, olive patrol cap, plain olive neck scarf (not keffiyeh), chest rig, armband |
| `GreycoatA/SK_GreycoatA.fbx` | CHR_GREYCOAT_A | Military_Male_03 | 11.8k | 512 in game | grey-green fatigues (rolled sleeves), plain fabric flak vest, black gloves, steel helmet + goggles |
| `GreycoatB/SK_GreycoatB.fbx` | CHR_GREYCOAT_B | Military_Male_04 | 10.8k | 512 in game | same kit, helmet + goggles (advisor B, marksman B5) |
| `GreycoatB/SK_GreycoatB_NVG.fbx` | CHR_GREYCOAT_B (NVG variant) | Military_Male_04 | 10.1k | shares GreycoatB | head-harness single-tube NVG (E10-04), no helmet |
| `Interrogator/SK_Interrogator.fbx` | CHR_INTERROGATOR | Business_Male_01 | 7.4k | 512 in game | grey suit, navy tie, black leather gloves |
| `Magpie/SK_Magpie.fbx` | CHR_MAGPIE (P2) | Military_Male_04 | 10.6k | 512 | sage flight suit, olive survival vest, flight gloves, SPH-4-style helmet, smoked visor down |
| `Harrou/SK_Harrou.fbx` | CHR_HARROU | Business_Male_04 | 9.0k | 1024 | **editor-only** (VID_HARROU_8MM): bald, khaki shirt and drill trousers, dark olive waistcoat, beret, gold aviators, orange armband |

### 1.2 Character props
Fitted, skinned copies are inside the bodies above; loose static copies (for swaps, pickups, dropped hats) are in
`Art/Models/Characters/Props/<prop>/SM_CHP_*.fbx` with 512 maps in `Art/Textures/Characters/Props/CHP_*/`.

| Prop | Manifest | Tris | Source | Worn by |
|---|---|---|---|---|
| SM_CHP_Boonie | CHP_BOONIE | 1.0k | Tripo P2 | Calder, Hollis, Legion B |
| SM_CHP_Ballcap | CHP_BALLCAP | 1.0k | Tripo P2 | Sparrow |
| SM_CHP_Beret | CHP_BERET | 0.8k | Tripo P2 | Legion A, Harrou |
| SM_CHP_AviatorGoggles | CHP_AVIATOR_GOGGLES | 1.0k | Tripo P2 | Legion B (on the boonie) |
| SM_CHP_HelmetGoggles | CHP_HELMET_GOGGLES | 2.7k | Tripo P2 | Greycoat A/B |
| SM_CHP_NVG | CHP_NVG | 2.0k | Tripo P2 | Greycoat B NVG (lens: add emissive in the material, see §3) |
| SM_CHP_ChestRig | CHP_CHEST_RIG | 3.1k | Tripo P2 | Calder, Hollis, Tinker, Legion A/B/C |
| SM_CHP_ArmorVest | CHP_ARMOR_VEST | 2.9k | Tripo P2 | loose only (the Greycoats' own vests were de-MOLLEd instead) |
| SM_CHP_FlightHelmet | *CHP_FLIGHT_HELMET (new)* | 2.6k | Tripo P2 | Magpie |
| SM_CHP_Scarf | *CHP_SCARF (new)* | 1.5k | Tripo P2 | Legion C |
| SM_CHP_Sunglasses | *CHP_SUNGLASSES (new)* | 0.9k | procedural (Blender) | Tinker (INSERTION I-6), Harrou |

### 1.3 Animation (`Art/Animation/Clips/<Source>/`), 127 clip files + 2 libraries
| Folder | Manifest | Count | Notes |
|---|---|---|---|
| `Meshy/A_Meshy_*` | ANIM_MESHY_OWNED (5), SET_A (10), SET_B (20, two calls), SET_C (10), ANIM_INSERTION_SET (10 incl. derived ProneIdle) | 55 | baked on the Calder skeleton, rifle/relaxed finger pose baked in, foot events for loops |
| `HYMotion/A_HYMotion_*` | ANIM_HYMOTION_SIGNALS | 7 | FistHold, MoveUp, CrouchRunCover, PlantCharge, CheckWatch, HaulBag, OneFinger (seated "One minute!") |
| `Rocketbox/A_Rocketbox_*` | ANIM_ROCKETBOX_LOCAL (6), ANIM_ROCKETBOX_BOOTH (22) | 28 | sitting (chair/table), gestures, phone/radio handset, take picture, documents, idles, walk/run, crouch |
| `SAC/A_SAC_*` + `SK_SAC_DefaultMaleRig.fbx` | ANIM_SAC_SUBSET (8 + rig), ANIM_SAC_FULL (20) | 28 | copied unchanged; avatar source = the rig file |
| `UAL1/A_UAL1_Library(_RM).fbx`, `UAL2/...` | ANIM_UAL1, ANIM_UAL2 | 2 + 2 libraries | 43 takes each, in-place and root-motion versions, copied unchanged |
| `Poses/A_Pose_*` | ANIM_FINGER_POSES | 5 | RifleGrip, PistolGrip, Fist, Relaxed, FlatHands (2-frame static poses) |

Useful mapping for `AC_Soldier` / cinematics:
- L0 locomotion: SAC StrafeIdle / StrafeWalkForwards / StrafeRight* (+ mirrored Left) / RunForwards / SprintForwards; Meshy
  WalkLeft/WalkBackward/WalkForwardWhileShooting *WithGun* for aimed walks; LeanForwardSprintInplace / RifleChargeInplace
  for rifle sprint. Crouch: Meshy CautiousCrouchWalk{Forward,Backward,Left,Right}Inplace, CrouchWalkLeftWithGunInplace,
  UAL1 Crouch_Idle_Loop / Crouch_Fwd_Loop, Rocketbox CrouchIdle. Turns: Meshy RifleAimTurnRight, RifleTurnLeft,
  GunHoldLeftTurn, CombatIdleTurnLeft, AlertQuickTurnRight; SAC TurningLeft (mirror for right).
- Idle variations: Meshy Alert, LowerWeaponLookRaise; Rocketbox IdleNeutral01/Breathe01/LookAround01/02.
- L1 upper body: StandingReload, KneelingReload, ForwardReloadSubtle, RunningReloadInplace, SlowWalkReloadInplace,
  ProneReload; throw = Meshy CrouchPullAndThrow or UAL2 OverhandThrow; signals = HYMotion FistHold / MoveUp, Meshy
  GestureWithHandOnGun, CallGesture (hand to ear).
- L2 hits: Meshy GunshotReaction, HitReaction, HitReactionToWaist; UAL1 Hit_Chest / Hit_Head; UAL2 Hit_Knockback.
- Deaths (blend to ragdoll after 0.2 s): Meshy ShotAndFallBackward, ShotAndFallForward, ShotAndBlownBack,
  ShotAndSlowFallBackward, ShotInTheBackAndFall, FallDeadFromAbdominalInjury; UAL1 Death01.
- Actions: VaultWithRifle, VaultAndLand, RollBehindCover, SimpleKick (B6 door), KneelOnOneKneeAndStand,
  CrouchPickGunPointForward, CrouchAndStepBack, HYMotion PlantCharge / CheckWatch / HaulBag / CrouchRunCover; UAL2
  ClimbUp_1m, Slide_*, Walk_Carry_Loop; UAL1 Fixing_Kneeling (drill), Interact, PickUp_Table, Push_Loop.
- INSERTION (CINEMATIC_DIRECTION §2): I-2..I-6 seated = Meshy ChairSitIdleM or Rocketbox SitChairIdleNeutral01/LookAround,
  I-3 Hollis = HYMotion OneFinger, I-5 Sparrow = Rocketbox TakePicture, I-6 Tinker handset = Rocketbox CellPhoneTalk01,
  I-8 = Meshy JumpingDown (cut on landing), I-9 = Meshy RifleChargeInplace / HYMotion CrouchRunCover + MoveUp,
  I-11 = Meshy ProneIdle.
- Booth: Rocketbox SitChairIdleNervous01 / SitChairBreathe01 / SitTableIdleNervous01 (34 s) / SitChairIdleCough /
  RollHead (prisoner); Meshy SitOnChairArmsCrossed, SittingAnsweringQuestions; interrogator walk-past Rocketbox
  WalkNeutral01, gestures GesticTalk*; phone = Rocketbox CellPhoneTalk01 or UAL2 Idle_TalkingPhone_Loop.

### 1.4 Fabric tiles (`Art/Textures/Characters/Fabrics/T_Fabric_*`, TEX_FABRIC_*)
TigerStripe, KhakiDrill, OliveSateen, GreyGreen, TanPoplin (TEX_FABRIC_TAN_SHIRT), OrangeArmband: 1024 seamless BC/N/M
(fal-ai/patina). They are already baked into the character atlases, so no shipped material needs them; import them, but they
only reach the build if a runtime material references them.

---

## 2. Import instructions (editor holder)
Copy `RawAssets/wp05/ready/Assets/**` into `Assets/` as is, then apply the per-file notes in `import_index.json`. Summary:

**Character FBX (`type: model_skinned_humanoid`).** Scale Factor 1, Convert Units ON (files are metres), Unity axes
(+Z forward, Y up), no root rotation. Rig: **Humanoid, Create From This Model**. The Rocketbox quirk is fixed in the file
(thighs under Bip01 Pelvis, clavicles under Bip01 Spine2) and the default node pose is a T-pose (arms out, palms down) with
the A-pose bind kept in the skin, so: **RocketboxImporter must not re-parent or rotate bones again; no Enforce T-Pose needed.**
Bone map (auto-map finds all): Hips = Bip01 Pelvis, Spine = Bip01 Spine, Chest = Bip01 Spine1, UpperChest = Bip01 Spine2,
Neck, Head, Jaw = Bip01 MJaw (check auto-map did not pick a face bone), eyes = Bip01 LEye/REye, shoulders = Clavicles,
UpperArm/Forearm/Hand, Thigh/Calf/Foot/Toe0, fingers Finger0x (thumb) … Finger4x (little). Import Animation OFF,
Materials: Material Creation Mode None → URP/Lit per slot with the textures named in each row (mask = Metallic AND Occlusion
map, Smoothness from Metallic alpha). Optimize Game Objects ON, expose Bip01 R Hand, Bip01 L Hand, Bip01 Head, Bip01 Spine2.
Skin weights 4. Normals Import, Tangents Mikk. Mesh compression Off.

**Clip FBX baked on the Rocketbox skeleton (Meshy, HYMotion, Rocketbox, Poses).** Humanoid, **Copy From Other Avatar =
the avatar of `Art/Models/Characters/Calder/SK_Calder.fbx`** (same skeleton, names and hierarchy). One take per file,
rename it to the file name. Loop / root-motion / mirror / events are in each row. Defaults: Root Transform Rotation Bake
Into Pose (Body Orientation), Y Bake (Feet), XZ baked for in-place loops and NOT baked for travelling actions (deaths,
vaults, rolls, RunAndShoot, WalkSlowlyAndLookAround, JumpingDown, HYMotion CrouchRunCover: enable applyRootMotion while they
play). Mirror OFF on the files; make mirrored duplicates in the importer where noted (strafe/turn "Left/Right" clips).
Anim compression Optimal 0.5° / 0.5 %; poses: compression Off.

**SAC (`A_SAC_*`).** First import `SK_SAC_DefaultMaleRig.fbx` as Humanoid, Create From This Model; then every `A_SAC_*` clip
uses Copy From Other Avatar = that avatar. Strafe "Right" clips get a mirrored duplicate named "Left" (manifest variant
mirror_left). Turn clips keep root rotation (NOT baked). Measured frames, travel and yaw are in each row.

**UAL1/UAL2 (`A_UAL*_Library.fbx` in place, `_RM.fbx` root motion).** Model tab **Bake Axis Conversion ON** (Quaternius setup),
Humanoid Create From This Model, one clip per take renamed `A_UAL1_<Take>` (`_RM` suffix for the root-motion file), Loop on
every take ending in `_Loop`, skip `A_TPose`; for `_RM` set Root Motion Node = `Rig/root` and do not bake XZ. The mannequin
mesh in these files is never placed in a scene.

**Textures.** BC sRGB, N = Normal map (OpenGL, no flip), M = mask (sRGB OFF: R metal, G AO, A smoothness). Max sizes are in
each row: squad 1024 (Hollis head 2048), Calder booth/reel 1024, **Legion / Greycoat / Interrogator / Magpie: 512 in game
(the files are 1024 source; manifest "all sets 512")**, props 512 (sunglasses 256). Hair (`*_Hair_BC`, RGBA): Alpha
Clipping 0.35, Render Face Both, and disable the ShadowCaster pass on the hair material (hair cards self-shadowing the scalp
render as black streaks in previews).

**Material notes.** URP/Lit: cloth smoothness comes from the mask (0.2–0.35), skin ~0.36–0.45 (TECH_PLAN asks for URP Lit
Specular on Rocketbox; the masks are authored for the Metallic workflow with metal = 0 on cloth and skin, so either works).
NVG: add an emissive green to the lens area of `M_CHP_NVG` (WP10 VFX) for E10-04. Sunglasses / flight-helmet visor are opaque
dark glass with high smoothness (no transparency needed).

**Prefab inputs (for the WP05 editor pass and WP02's request in REQUESTS.md).** `PFB_Calder_FP`: either
`SK_Calder_FPArms` (arms only, cleanest) or `SK_Calder` with head/neck/thighs scaled to 0 (TECH_PLAN D19; then the chest rig
is visible when looking down). `PFB_Calder_Shadow`: `SK_Calder` (ShadowsOnly). Booth: `SK_Calder_Booth` (+ reel head swap),
`SK_Interrogator`. Squad: `SK_Hollis`, `SK_Tinker`, `SK_Sparrow`. Enemies: `SK_LegionA/B/C`, `SK_GreycoatA/B`,
`SK_GreycoatB_NVG`. Cockpit: `SK_Magpie`. Editor-only: `SK_Harrou` (keep it out of Addressables groups).

---

## 3. Tools (all in `Tools/characters/`, Python = `Tools/.venv/bin/python`, Blender 5.2 headless)
| Tool | What it does | Rerun |
|---|---|---|
| `fetch_rocketbox.py` | downloads Rocketbox avatars/animations file by file from GitHub raw (cached tree listing) | `fetch_rocketbox.py avatar Business_Male_04`, `anim m_sit_chair_breathe_01 --variant static` |
| `blender/rb_dump.py`, `rb_islands.py` | per-face UV/3D/bone dump and UV-island maps of a Rocketbox FBX (`_work/<Avatar>/`) | `Blender -b --factory-startup -P blender/rb_dump.py -- <fbx> <dump.json>`; `rb_islands.py view <Avatar>` |
| `rb_configs.py` + `retexture_rocketbox.py` | 1984 retexture: class masks per island/bone/colour, ACU → fal fabric tiles × rebuilt shading, paint-outs (flags, tapes, patches, buckles), MOLLE flattening, armbands, dust/sweat/wear, head ops (shave, gaunt, grey hair); 2048 masters in `_work/<Variant>/master/` | `retexture_rocketbox.py GreycoatA GreycoatB` |
| `bodies_spec.py` + `build_bodies.py` (+ `blender/rb_export.py`, `blender/rb_render.py`) | the whole body pipeline: masters → budget textures → fixed Humanoid FBX with fitted, skinned props (hats, rigs, helmet, scarf, sunglasses, PRC-77), face/island deletion, FP arms cut, booth vest slimming, restraints → preview sheet → index rows | `build_bodies.py` (all) / `build_bodies.py Magpie Tinker` / `--props` (loose CHP meshes) / `--reel` (reel head) / `--only-preview` |
| `gen_props.py` | CHP references (nano-banana-2.1) and Tripo P2 3D via `Tools/fal/falcall.py --wp WP05` | `gen_props.py ref --ids CHP_SCARF`; `gen_props.py 3d --ids CHP_SCARF --ref <png>` |
| `prop_textures.py` | Tripo GLB PBR → T_*_BC/N/M | called by build_bodies |
| `blender/make_sunglasses.py` | procedural aviator sunglasses in Rocketbox male head space | `Blender ... -P blender/make_sunglasses.py -- RawAssets/wp05/CHP_SUNGLASSES/proc/sunglasses_model.glb` |
| `gen_fabrics.py`, `process_fabrics.py` | patina fabric tiles → T_Fabric_* + working copies for the retexture | `process_fabrics.py` |
| `gen_meshy.py`, `gen_hymotion.py` | Meshy multi-animation and HY-Motion calls (through falcall) | see their headers |
| `clips_spec.py` + `build_clips.py` (+ `blender/bake_clip.py`, `blender/clip_preview.py`, `blender/finger_poses.py`, `blender/pose_clips.py`) | retarget/bake every clip onto the Calder skeleton (world-rotation retarget, hip-height scaling, grounding, finger pose), foot events, previews holding the AK, finger pose clips | `build_clips.py meshy\|rocketbox\|hymotion\|poses\|local\|all [--only A_Meshy_Idle] [--no-preview]` |
| `package_local_clips.py` (+ `blender/clip_scan.py`) | copies UAL1/UAL2/SAC clips with measured flags | `package_local_clips.py` |
| `make_index.py` (+ `blender/fbx_stats.py`) | merges `_work/index/*.json` into `ready/import_index.json`, applies the 512-in-game rule, adds fabric rows, budget report `_work/budget_report.json` | `Blender ... -P blender/fbx_stats.py -- RawAssets/wp05/ready RawAssets/wp05/_work/fbx_stats.json; make_index.py --stats RawAssets/wp05/_work/fbx_stats.json` |
| `overview_sheet.py` | `_review/ALL_bodies_overview.png` | `overview_sheet.py` |

FP check: `Blender ... -P blender/clip_preview.py -- RawAssets/wp05/_work/specs/fp_view_spec.json` renders the FP arms from the
eye with a rifle pose (`fp_views` option).

---

## 4. Deviations from the manifest / plan
- **Tinker = Male_Adult_08**, not Male_Adult_05: Male_Adult_05 is the older grey-stubbled face and became Hollis.
- **Sparrow = Military_Female_02** (hair bun, not a braid); camera strap is not on the body (the camera is a separate prop).
- **Magpie** uses an SPH-4-style flight helmet (new prop `CHP_FLIGHT_HELMET`) and a flight suit, not the Rocketbox helmet;
  his face is Greycoat B's (Military_Male_04), hidden by the visor.
- **New props not in ASSET_MANIFEST:** `CHP_FLIGHT_HELMET`, `CHP_SCARF`, `CHP_SUNGLASSES` (manifest ids used in the index;
  please add them to the manifest if `validate_assets.py` needs every id there).
- **ANIM_MESHY_SET_B** was bought as two fal calls (ledger ids ANIM_MESHY_SET_B and ANIM_MESHY_SET_B2), 20 clips; index rows
  all say ANIM_MESHY_SET_B. Meshy #506 Dive_Down_and_Land was bought but is not shipped.
- **Legion bodies are 1024 source files, 512 in game** (the import notes set Max Size 512).
- **Finger poses are delivered as clips** (`Clips/Poses/A_Pose_*`), authored offline with the same per-joint angles that
  `FingerPoseAuthoring` would write as Humanoid muscles; the editor tool can still regenerate them.
- **Greycoat vests**: de-MOLLEd in the texture instead of swapping in `CHP_ARMOR_VEST` (the loose armour vest still ships).
- `CHR_FALLBACK_MESHY` not built (O2 approved).

## 5. Known issues / follow-ups
- Meshy's "Idle" and aimed walks stand with deeply bent knees (pelvis 0.81 m vs 0.89 m for Rocketbox idles): fine for
  combat stances, but use SAC StrafeIdle / Rocketbox idles for relaxed squad idles.
- Seated clips are grounded by the feet: pelvis joint at 0.58–0.60 m (Rocketbox m_sit_*), 0.52 m (Meshy ChairSitIdleM,
  HYMotion OneFinger), 0.65 m (Meshy SittingAnsweringQuestions). Seat surface ≈ pelvis − 0.10 m; offset the body (or IK the
  hips) if the booth chair / helicopter bench differ.
- The slimmed booth torso keeps a few thin strap fragments at the shoulders (small flaps visible in the close-up only).
- Preview renders hold the rifle by placing it between the palms, so the rifle flips during reloads in the sheets: that is
  the preview, not the clip. Runtime two-bone IK puts the left hand on the foregrip (TECH_PLAN §7.8).
- Booth electrodes are modelled on the cuffs mesh (`SK_Calder_Booth_Cuffs`); WP09's `PROC_BOOTH_SMALLS` also lists
  electrodes: use one of the two.
- Rocketbox booth clips are long (up to 51 s): trim clip ranges if the animation budget (6 MB) gets tight; only clips used by a
  controller or timeline reach the build.
- Interrogator hair renders with dark streaks in Blender previews (hair-card self-shadowing): disable the hair ShadowCaster
  pass as noted.
- Credits: Microsoft Rocketbox (MIT), Unity Standard Assets Characters (Unity Companion License), Quaternius Universal
  Animation Library 1/2 (CC0), fal.ai models (Tripo P2, nano-banana-2.1, patina, Meshy multi-animation, HY-Motion).
