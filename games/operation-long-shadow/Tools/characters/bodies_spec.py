"""Body build specs for build_bodies.py (WP05). One entry per shipped character FBX.

Keys per body:
  manifest      ASSET_MANIFEST id
  avatar        Rocketbox avatar (source FBX + dump)
  folder        Unity folder under Art/Models/Characters/ (and Art/Textures/Characters/)
  mesh          skinned body mesh name (= FBX file stem)
  mats          {source material: (game material, texture variant, part, size)}; texture = T_<variant>_<part>_{BC,N,M}
  delete_materials / delete_islands   removed faces (Rocketbox helmets, modern equipment, sunglasses, caps ...)
  props         [(prop key, fit overrides)] from PROPS (CHP_* models fitted, skinned and merged as separate meshes)
  extra         optional {"keep_faces": ..., "cuffs": true, "tpose": bool}
Fit keys (rb_export.py fit_prop): anchor head|chest, rotz, size / size_axis, top_gap, offset [x,y,z] (m, Blender
coords: +x = character left, -y = forward, +z = up), rotx/roty (deg), strap_cut [depth, radius], trim (hair cards
under hats), push_out (shrinkwrap offset over the body, for rigs/vests).
"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
RB_LOCAL = ROOT / "RawAssets/cc0/characters/rocketbox"
RB_DL = ROOT / "RawAssets/wp05/rocketbox"


def src_fbx(avatar):
    for base in (RB_DL, RB_LOCAL):
        p = base / avatar / "Export" / f"{avatar}.fbx"
        if p.exists() and (base / avatar / "Textures").exists():
            return p
    return RB_LOCAL / avatar / "Export" / f"{avatar}.fbx"


W5 = ROOT / "RawAssets/wp05"
PROPS = {
    # key: model, game material, texture name (Art/Textures/Characters/Props/<tex>/T_<tex>_*), bone (None = skin from body)
    "boonie": {"file": W5 / "CHP_BOONIE/tripo_take1/00_Xvaggrlg2CQLS9YjaDeyb_model.glb", "id": "CHP_BOONIE", "tex": "CHP_Boonie",
               "bone": "Bip01 Head", "fit": {"anchor": "head", "rotz": -90, "size_axis": "x", "size": 0.37, "top_gap": 0.065, "rotx": -5,
                                             "strap_cut": [0.0, 1.0], "strap_cut_frac": 0.885}},
    "ballcap": {"file": W5 / "CHP_BALLCAP/tripo_take1", "id": "CHP_BALLCAP", "tex": "CHP_Ballcap", "bone": "Bip01 Head",
                "fit": {"anchor": "head", "rotz": -90, "size_axis": "y", "size": 0.29, "top_gap": 0.012, "rotx": 6,
                         "offset": [0, -0.035, 0]}},
    "beret": {"file": W5 / "CHP_BERET/tripo_take1", "id": "CHP_BERET", "tex": "CHP_Beret", "bone": "Bip01 Head",
              "fit": {"anchor": "head", "rotz": -90, "size_axis": "x", "size": 0.26, "top_gap": 0.055, "rotx": -8, "roty": 10}},
    "helmet_goggles": {"file": W5 / "CHP_HELMET_GOGGLES/tripo_take1", "id": "CHP_HELMET_GOGGLES", "tex": "CHP_HelmetGoggles",
                       "bone": "Bip01 Head", "fit": {"anchor": "head", "rotz": -90, "size_axis": "x", "size": 0.27, "top_gap": 0.075,
                                                     "offset": [0, 0.005, 0]}},
    "nvg": {"file": W5 / "CHP_NVG/tripo_take1", "id": "CHP_NVG", "tex": "CHP_NVG", "bone": "Bip01 Head",
            "fit": {"anchor": "head", "rotz": -90, "size_axis": "x", "size": 0.195, "top_gap": 0.0, "rotx": -22, "offset": [0, -0.01, 0]}},
    "aviator_goggles": {"file": W5 / "CHP_AVIATOR_GOGGLES/tripo_take1", "id": "CHP_AVIATOR_GOGGLES", "tex": "CHP_AviatorGoggles",
                        "bone": "Bip01 Head", "fit": {"anchor": "head", "rotz": -90, "size_axis": "x", "size": 0.21, "top_gap": -0.02}},
    "chest_rig": {"file": W5 / "CHP_CHEST_RIG/tripo_take1/00_33CUw_itUP--e-nwbXjw1_model.glb", "id": "CHP_CHEST_RIG", "tex": "CHP_ChestRig",
                  "bone": None, "fit": {"anchor": "chest", "rotz": -90, "size": [0.40, 0.33, 0.50], "top_gap": 0.0, "push_out": 0.008}},
    "armor_vest": {"file": W5 / "CHP_ARMOR_VEST/tripo_take1", "id": "CHP_ARMOR_VEST", "tex": "CHP_ArmorVest", "bone": None,
                   "fit": {"anchor": "chest", "rotz": -90, "size": [0.44, 0.34, 0.62], "top_gap": 0.03, "push_out": 0.006}},
    # resume pass additions (not in ASSET_MANIFEST): Magpie's flight helmet, Legion C's plain scarf, procedural aviators
    "flight_helmet": {"file": W5 / "CHP_FLIGHT_HELMET/tripo_take1", "id": "CHP_FLIGHT_HELMET", "tex": "CHP_FlightHelmet",
                      "bone": "Bip01 Head", "fit": {"anchor": "head", "rotz": -33, "size_axis": "x", "size": 0.27, "top_gap": 0.03,
                                                    "rotx": -4, "offset": [0, 0.012, 0]}},
    "scarf": {"file": W5 / "CHP_SCARF/tripo_take1", "id": "CHP_SCARF", "tex": "CHP_Scarf", "bone": None,
              "fit": {"anchor": "neck", "rotz": -90, "size": [0.215, 0.20, 0.17], "top_gap": 0.035, "offset": [0, -0.012, 0],
                      "push_out": 0.006}},
    "sunglasses": {"file": W5 / "CHP_SUNGLASSES/proc/sunglasses_model.glb", "id": "CHP_SUNGLASSES", "tex": "CHP_Sunglasses",
                   "bone": "Bip01 Head", "fit": None, "procedural": "sunglasses", "tex_size": 256,
                   "loose": {"rotz": 0, "size_axis": "x", "size": 0.152, "anchor": "eyes"}},
    "prc77": {"file": ROOT / "RawAssets/wp10/ready/Assets/_Game/Art/Models/Props/prc77/SM_PRC77.fbx", "id": "FAL_PRC77 (WP10)",
              "tex": None, "material": "M_PRC77", "bone": "Bip01 Spine2", "decimate": 0.45,
              "fit": {"anchor": "back", "rotz": 0, "size_axis": "z", "size": 0.27, "offset": [0, -0.13, -0.10]}},
}

MM01_DEL_ISL = [107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 9, 10, 14, 15, 95, 96]  # head eyewear, back hydration pack, drink tube

BODIES = {
    # ------------------------------------------------------------------ squad
    "Calder": {
        "manifest": "CHR_CALDER", "avatar": "Military_Male_01", "folder": "Calder", "mesh": "SK_Calder",
        "mats": {"sm002_body": ("M_Calder_Body", "Calder", "Body", 1024), "sm002_head": ("M_Calder_Head", "Calder", "Head", 1024)},
        "delete_materials": ["sm002_helmet", "sm002_equipment", "sm002_combat_knife"], "delete_islands": MM01_DEL_ISL,
        "props": [("chest_rig", {}), ("boonie", {})],
        "note": "Full body. Also the shadow proxy PFB_Calder_Shadow (ShadowsOnly) and the third-person body in the INSERTION cinematic.",
    },
    "CalderFP": {
        "manifest": "CHR_CALDER", "avatar": "Military_Male_01", "folder": "Calder", "mesh": "SK_Calder_FPArms",
        "mats": {"sm002_body": ("M_Calder_Body", "Calder", "Body", 1024)},
        "delete_materials": ["sm002_helmet", "sm002_equipment", "sm002_combat_knife", "sm002_head"], "delete_islands": MM01_DEL_ISL,
        "props": [],
        "extra": {"keep_faces": {"parts": ["arm", "hand"]}},
        "note": "First-person viewmodel arms: sleeves and gloved hands only (faces whose dominant bone is an UpperArm/Forearm/Hand/Finger); "
                "no torso, head, neck or legs, so nothing jagged can enter the view at any pitch. Same skeleton, bone names and "
                "textures as SK_Calder (one Humanoid avatar; clips and FPArmsIK work unchanged). Alternative per TECH_PLAN D19: SK_Calder "
                "with head/neck/thighs scaled to 0.",
    },
    "CalderBooth": {
        "manifest": "CHR_CALDER", "avatar": "Military_Male_01", "folder": "Calder", "mesh": "SK_Calder_Booth",
        "mats": {"sm002_body": ("M_CalderBooth_Body", "CalderBooth", "Body", 1024), "sm002_head": ("M_Calder_Head", "Calder", "Head", 1024)},
        "delete_materials": ["sm002_helmet", "sm002_equipment", "sm002_combat_knife"], "delete_islands": MM01_DEL_ISL,
        "props": [],
        "extra": {"cuffs": True, "slim_vest": {"material": "sm002_body", "iterations": 20, "shrink": 0.008, "falloff_hops": 4,
                                                "keep_islands": [33, 36, 31, 27, 34, 43, 39, 46, 42, 30, 32, 47, 48, 37, 38, 40, 41, 44, 45]}},
        "note": "Booth / Session 12 prisoner (GDD 5.1): no gear, bare hands; the Rocketbox armour-vest shell is relaxed and pulled in offline (slim_vest) so the torso reads as a plain field shirt. Restraints = separate renderer SK_Calder_Booth_Cuffs (M_Calder_Restraints): leather wrist cuffs with steel buckles + taped electrodes on both forearms, skinned to the forearms. Reel variant (MID-B Session 11): swap M_Calder_Head for M_CalderReel_Head (T_CalderReel_Head_*).",
    },
    "Hollis": {
        "manifest": "CHR_HOLLIS", "avatar": "Male_Adult_05", "folder": "Hollis", "mesh": "SK_Hollis",
        "mats": {"m009_body": ("M_Hollis_Body", "Hollis", "Body", 1024), "m009_head": ("M_Hollis_Head", "Hollis", "Head", 2048),
                 "m009_opacity": ("M_Hollis_Hair", "Hollis", "Hair", 512)},
        "props": [("boonie", {"trim": [{"material": "m009_opacity", "below_top": 0.10}]}), ("chest_rig", {})],
    },
    "Tinker": {
        "manifest": "CHR_TINKER", "avatar": "Male_Adult_08", "folder": "Tinker", "mesh": "SK_Tinker",
        "mats": {"m014_body": ("M_Tinker_Body", "Tinker", "Body", 1024), "m014_head": ("M_Tinker_Head", "Tinker", "Head", 1024),
                 "m014_opacity": ("M_Tinker_Hair", "Tinker", "Hair", 512)},
        "props": [("chest_rig", {}), ("prc77", {}), ("sunglasses", {})],
        "note": "Tinker wears gold-frame aviator sunglasses (CINEMATIC_DIRECTION I-6): separate renderer SK_Tinker_Sunglasses, hide it per shot if needed.",
    },
    "Sparrow": {
        "manifest": "CHR_SPARROW", "avatar": "Military_Female_02", "folder": "Sparrow", "mesh": "SK_Sparrow",
        "mats": {"sf004_body": ("M_Sparrow_Body", "Sparrow", "Body", 1024), "sf004_head": ("M_Sparrow_Head", "Sparrow", "Head", 1024)},
        "delete_materials": ["sf004_helmet", "sf004_equipment"], "delete_islands": [9, 10, 14, 15, 91, 144, 146, 147],
        "props": [("ballcap", {"size": 0.325, "top_gap": 0.016, "rotx": -8, "offset": [0, -0.03, 0],
                                "trim": [{"material": "sf004_head", "below_top": 0.072}]})],
        "note": "Rocketbox Military_Female_02 (not a civilian female: those wear heeled boots and blazers). Hair bun at the back under the ball cap.",
    },
    # ------------------------------------------------------------------ Legion
    "LegionA": {
        "manifest": "CHR_LEGION_A", "avatar": "Military_Male_02", "folder": "LegionA", "mesh": "SK_LegionA",
        "mats": {"sm024_body": ("M_LegionA_Body", "LegionA", "Body", 1024), "sm024_head": ("M_LegionA_Head", "LegionA", "Head", 512)},
        "delete_islands": [45, 46, 47, 48, 49, 50, 51],  # patrol cap
        "props": [("beret", {}), ("chest_rig", {})],
    },
    "LegionB": {
        "manifest": "CHR_LEGION_B", "avatar": "Military_Male_05", "folder": "LegionB", "mesh": "SK_LegionB",
        "mats": {"sm022_body": ("M_LegionB_Body", "LegionB", "Body", 1024), "sm022_head": ("M_LegionB_Head", "LegionB", "Head", 512)},
        "delete_islands": [28, 29, 30, 31, 32, 33, 34],
        "props": [("boonie", {}), ("aviator_goggles", {"size": 0.25, "top_gap": 0.04, "rotx": -6, "offset": [0, -0.022, 0]}), ("chest_rig", {})],
    },
    "LegionC": {
        "manifest": "CHR_LEGION_C", "avatar": "Military_Male_06", "folder": "LegionC", "mesh": "SK_LegionC",
        "mats": {"sm021_body": ("M_LegionC_Body", "LegionC", "Body", 1024), "sm021_head": ("M_LegionC_Head", "LegionC", "Head", 512)},
        "props": [("chest_rig", {}), ("scarf", {})],
        "note": "Legion RPG / rooftop: olive shirt, khaki trousers, patrol cap, plain solid olive neck scarf knotted at the front (no pattern, no fringe: never keffiyeh styling).",
    },
    # ------------------------------------------------------------------ Greycoats
    "GreycoatA": {
        "manifest": "CHR_GREYCOAT_A", "avatar": "Military_Male_03", "folder": "GreycoatA", "mesh": "SK_GreycoatA",
        "mats": {"sm004_body": ("M_GreycoatA_Body", "GreycoatA", "Body", 1024), "sm004_head": ("M_GreycoatA_Head", "GreycoatA", "Head", 512)},
        "delete_materials": ["sm004_helmet", "sm004_equipment", "sm004_pistol"],
        "delete_islands": [175, 176, 177, 178, 179, 180, 181, 182, 183, 9, 10, 14, 15, 66, 70],  # sunglasses, hydration pack + tube
        "props": [("helmet_goggles", {})],
    },
    "GreycoatB": {
        "manifest": "CHR_GREYCOAT_B", "avatar": "Military_Male_04", "folder": "GreycoatB", "mesh": "SK_GreycoatB",
        "mats": {"sm005_body": ("M_GreycoatB_Body", "GreycoatB", "Body", 1024), "sm005_head": ("M_GreycoatB_Head", "GreycoatB", "Head", 512)},
        "delete_materials": ["sm005_helmet", "sm005_equipment"], "delete_islands": [9, 10, 14, 15, 60, 64, 65],
        "props": [("helmet_goggles", {})],
        "note": "Greycoat advisor B and the marksman (B5).",
    },
    "GreycoatBNVG": {
        "manifest": "CHR_GREYCOAT_B", "avatar": "Military_Male_04", "folder": "GreycoatB", "mesh": "SK_GreycoatB_NVG",
        "mats": {"sm005_body": ("M_GreycoatB_Body", "GreycoatB", "Body", 1024), "sm005_head": ("M_GreycoatB_Head", "GreycoatB", "Head", 512)},
        "delete_materials": ["sm005_helmet", "sm005_equipment"], "delete_islands": [9, 10, 14, 15, 60, 64, 65],
        "props": [("nvg", {})],
        "note": "NVG flanker E10-04 (LZ, B10): early-1980s head-harness night-vision goggle, no helmet. Lens glow: emissive on the NVG lens (WP10 VFX / material).",
    },
    "Magpie": {
        "manifest": "CHR_MAGPIE", "avatar": "Military_Male_04", "folder": "Magpie", "mesh": "SK_Magpie",
        "mats": {"sm005_body": ("M_Magpie_Body", "Magpie", "Body", 512), "sm005_head": ("M_Magpie_Head", "Magpie", "Head", 512)},
        "delete_materials": ["sm005_helmet", "sm005_equipment"], "delete_islands": [9, 10, 14, 15, 60, 64, 65],
        "props": [("flight_helmet", {})],
        "note": "Magpie, the helicopter pilot (glimpsed in the cockpit): sage-green flight suit, olive survival vest, flight gloves, SPH-4-style "
                "flight helmet with the smoked visor down (hides the shared Military_Male_04 face).",
    },
    # ------------------------------------------------------------------ booth / others
    "Interrogator": {
        "manifest": "CHR_INTERROGATOR", "avatar": "Business_Male_01", "folder": "Interrogator", "mesh": "SK_Interrogator",
        "mats": {"m005_body": ("M_Interrogator_Body", "Interrogator", "Body", 1024), "m005_head": ("M_Interrogator_Head", "Interrogator", "Head", 512),
                 "m005_opacity": ("M_Interrogator_Hair", "Interrogator", "Hair", 512)},
        "props": [],
    },
    "Harrou": {
        "manifest": "CHR_HARROU", "avatar": "Business_Male_04", "folder": "Harrou", "mesh": "SK_Harrou",
        "mats": {"m015_body": ("M_Harrou_Body", "Harrou", "Body", 1024), "m015_head": ("M_Harrou_Head", "Harrou", "Head", 1024)},
        "props": [("beret", {"size": 0.265, "top_gap": 0.05}), ("sunglasses", {})],
        "note": "EDITOR-ONLY (VID_HARROU_8MM pre-render, never shipped): Col. Harrou, 60s, bald, khaki shirt and drill trousers, dark olive "
                "waistcoat, Legion beret, gold aviators, orange customs armband. Face is scratched out by the film shader.",
    },
}
