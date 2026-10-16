"""Per-variant retexture configs for retexture_rocketbox.py (WP05).

Coordinates: paint-out rectangles are pixels of the 2048 source atlas. 3D points are Blender world
coordinates of the source FBX (metres, Z up, character faces -Y): [x, y, z].
Island ids come from RawAssets/wp05/_work/<Avatar>/islands_<material>.png (Tools/characters/rb_islands.py view <Avatar>).
Colours are sRGB 0..1. Fabric names = Tools/characters/process_fabrics.py game names.
Fill modes: fabric (tile * rebuilt shading, camo removed), recolor (target colour * original luminance detail),
keep, tint (multiply original).
"""

# --- reusable fills ---------------------------------------------------------------------------------
TAN_SHIRT = {"mode": "fabric", "fabric": "TanPoplin", "tile_px": 420, "value": 0.40, "tint": [1.0, 0.90, 0.74], "smooth": 0.26}
OLIVE_TROUSER = {"mode": "fabric", "fabric": "OliveSateen", "tile_px": 420, "value": 0.95, "smooth": 0.27}
OLIVE_CANVAS_VEST = {"mode": "fabric", "fabric": "OliveSateen", "tile_px": 380, "value": 0.78, "tint": [0.97, 1.0, 0.92],
                     "flatten": 0.9, "flatten_sigma": 16, "cav_w": 0.45, "smooth": 0.20}
KHAKI = {"mode": "fabric", "fabric": "KhakiDrill", "tile_px": 420, "value": 0.88, "smooth": 0.25}
KHAKI_FADED = {"mode": "fabric", "fabric": "KhakiDrill", "tile_px": 420, "value": 0.86, "tint": [1.0, 0.97, 0.91], "smooth": 0.25}
TIGER = {"mode": "fabric", "fabric": "TigerStripe", "tile_px": 760, "value": 1.0, "smooth": 0.24}
GREYGREEN = {"mode": "fabric", "fabric": "GreyGreen", "tile_px": 420, "value": 0.80, "tint": [0.96, 1.0, 0.88], "smooth": 0.26}
GREYGREEN_VEST = {"mode": "fabric", "fabric": "GreyGreen", "tile_px": 380, "value": 0.62, "tint": [0.95, 1.0, 0.88],
                  "flatten": 1.0, "flatten_sigma": 14, "cav_w": 0.40, "smooth": 0.22}  # 1980s plain fabric flak vest: PALS rows flattened away
ORANGE = {"mode": "fabric", "fabric": "OrangeArmband", "tile_px": 300, "value": 0.85, "smooth": 0.22}
NOMEX_GLOVE = {"mode": "recolor", "color": [0.40, 0.42, 0.33], "contrast": 0.9, "smooth": 0.25}
BROWN_BOOT = {"mode": "recolor", "color": [0.29, 0.21, 0.14], "contrast": 1.0, "smooth": 0.33}
BLACK_BOOT = {"mode": "recolor", "color": [0.11, 0.10, 0.09], "contrast": 1.1, "smooth": 0.42}
SKIN_KEEP = {"mode": "keep", "smooth": 0.36, "smooth_var": 0.35}

SQUAD_GRIME = {"dust_color": [0.66, 0.58, 0.47]}
ENEMY_GRIME = {"dust_color": [0.64, 0.56, 0.45]}

# Military_Male_01 (sm002) body atlas, shared by the Calder variants
_MM01_BODY_CLASSES = {
    "trousers": [43, 39, 46, 42, 30, 32],
    "shirt": [33, 36, 31, 27, 34],
    "glove": [47, 48, 37, 38],
    "boot": [40, 41, 44, 45],
}
_MM01_PAINTOUT = [
    (298, 1080, 418, 1172), (282, 1162, 408, 1266),      # right sleeve: flag, rank, name tape
    (1652, 1078, 1772, 1164), (1672, 1160, 1794, 1302),  # left sleeve: rank, unit patch
    (978, 1143, 1068, 1188), (978, 1252, 1068, 1303), (978, 1362, 1068, 1408),  # side-release buckles
    (952, 1172, 998, 1318),                               # red chemlight
    (992, 708, 1090, 756),                                # name on the back strap
]
_MM01_GRIME = dict(SQUAD_GRIME, dust={"trousers": [0.32, 0.04, 0.0, 0.95], "boot": [0.32, 0.22, 0.0, 0.2],
                                       "shirt": [0.07, 0.03, 0.9, 1.6], "vest": [0.12, 0.05, 0.9, 1.6], "glove": [0.12, 0.12, 0.9, 1.1]},
                   sweat=[["shirt", -0.20, 0.0, 1.33, 0.10, 0.8], ["shirt", 0.20, 0.0, 1.33, 0.10, 0.8], ["shirt", 0.0, 0.16, 1.32, 0.16, 0.6]],
                   wear=[["trousers", -0.11, -0.07, 0.50, 0.07, 0.35], ["trousers", 0.11, -0.07, 0.50, 0.07, 0.35]])

VARIANTS = {
    # ---------------- squad ----------------
    "Calder": {
        "avatar": "Military_Male_01", "seed": 11,
        "materials": {
            "sm002_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "glove", "foot": "boot"},
                           "classes": _MM01_BODY_CLASSES, "paintout": _MM01_PAINTOUT,
                           "fills": {"shirt": TAN_SHIRT, "trousers": OLIVE_TROUSER, "vest": OLIVE_CANVAS_VEST, "glove": NOMEX_GLOVE, "boot": BROWN_BOOT},
                           "grime": _MM01_GRIME},
            "sm002_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
            "sm002_helmet": {"skip": True}, "sm002_equipment": {"skip": True}, "sm002_combat_knife": {"skip": True},
        },
    },
    "CalderBooth": {  # booth / Session 12: no gear, vest area reads as shirt, bare hands
        "avatar": "Military_Male_01", "seed": 12,
        "materials": {
            "sm002_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "hands", "foot": "boot"},
                           "classes": dict(_MM01_BODY_CLASSES, glove=[], hands=[47, 48, 37, 38]), "paintout": _MM01_PAINTOUT,
                           "fills": {"shirt": dict(TAN_SHIRT, flatten=0.8, value=0.74),
                                     "vest": dict(TAN_SHIRT, flatten=1.0, flatten_sigma=22, cav_w=0.15, ao_w=0.25, value=0.74),
                                     "trousers": OLIVE_TROUSER,
                                     "hands": {"mode": "skin_tile", "src": ["sm002_head", 880, 400, 1120, 560], "tile_px": 220, "value": 0.95,
                                               "flatten": 0.65, "cav_w": 0.35, "smooth": 0.40},
                                     "boot": BROWN_BOOT},
                           "grime": dict(_MM01_GRIME, dust={"trousers": [0.22, 0.04, 0.0, 0.95], "shirt": [0.08, 0.05, 0.9, 1.6]},
                                         sweat=_MM01_GRIME["sweat"] + [["shirt", 0.0, -0.13, 1.30, 0.14, 0.5]])},
            "sm002_head": {"skip": True}, "sm002_helmet": {"skip": True}, "sm002_equipment": {"skip": True}, "sm002_combat_knife": {"skip": True},
        },
    },
    "CalderReel": {  # MID-B "Session 11" head: shaved, gaunt
        "avatar": "Military_Male_01", "seed": 13,
        "materials": {
            "sm002_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP},
                           "head_ops": [{"op": "shave", "stubble": 0.9, "scalp_mul": 0.9}, {"op": "gaunt"}]},
            "sm002_body": {"skip": True}, "sm002_helmet": {"skip": True}, "sm002_equipment": {"skip": True}, "sm002_combat_knife": {"skip": True},
        },
    },
}

# =====================================================================================================
# Enemies: Harrou Customs Legion (Military_Male_02 / 05 / 06: patrol-cap ACU bodies, no vest)
# =====================================================================================================
_PATROL_PAINTOUT = [  # union over MM02/05/06 (same atlas family): sleeve flag/rank/name/unit patches, chest tapes, rank
    (255, 1030, 420, 1272), (1625, 1040, 1778, 1302),
    (795, 1080, 948, 1142), (978, 1182, 1068, 1278), (1088, 1082, 1240, 1138),
    (858, 1186, 938, 1248), (1166, 1186, 1248, 1248),   # MM02 strap ID markers
]
ARMBAND_L = {"within": ["shirt"], "bone_a": "Bip01 L UpperArm", "bone_b": "Bip01 L Forearm", "t": [0.26, 0.42], "radius": 0.11,
             "fill": dict(ORANGE)}
_LEGION_GRIME = dict(ENEMY_GRIME, dust={"trousers": [0.36, 0.05, 0.0, 0.95], "boot": [0.40, 0.25, 0.0, 0.2], "shirt": [0.10, 0.05, 0.9, 1.6]},
                     sweat=[["shirt", -0.20, 0.0, 1.33, 0.10, 0.9], ["shirt", 0.20, 0.0, 1.33, 0.10, 0.9], ["shirt", 0.0, 0.15, 1.30, 0.17, 0.7]],
                     wear=[["trousers", -0.11, -0.07, 0.50, 0.07, 0.4], ["trousers", 0.11, -0.07, 0.50, 0.07, 0.4]])


def _legion(avatar, prefix, seed, shirt, trousers, boot, cap_islands, cap_fill=None, extra_classes=None):
    cls = {"trousers": [], "pack": []}
    if extra_classes:
        cls.update(extra_classes)
    head_classes = {"cap": cap_islands} if cap_fill else {}
    head_fills = {"skin": SKIN_KEEP}
    if cap_fill:
        head_fills["cap"] = cap_fill
    return {"avatar": avatar, "seed": seed, "materials": {
        f"{prefix}_body": {"part": "Body", "default_by_part": {"torso": "shirt", "leg": "trousers", "arm": "shirt", "hand": "skin", "foot": "boot"},
                           "classes": cls, "paintout": _PATROL_PAINTOUT,
                           # ACU sleeve cuffs sit on the hand-weighted islands: low-saturation pixels there are shirt, not skin
                           "color_rules": [{"class": "shirt", "within": ["skin"], "sat": [0.0, 0.17], "val": [0.0, 1.0]}],
                           "fills": {"shirt": shirt, "trousers": trousers, "skin": SKIN_KEEP, "boot": boot, "pack": dict(OLIVE_CANVAS_VEST)},
                           "bands": [ARMBAND_L], "grime": _LEGION_GRIME},
        f"{prefix}_head": {"part": "Head", "default_by_part": {"*": "skin"}, "classes": head_classes, "fills": head_fills,
                           "paintout": [(1300, 1050, 2048, 1110), (1600, 1150, 2048, 1300)]},  # cap name tape (cap removed or refilled)
    }}


VARIANTS["LegionA"] = _legion("Military_Male_02", "sm024", 21, KHAKI_FADED, dict(KHAKI, value=0.80), BLACK_BOOT, [45, 46, 47, 48, 49, 50, 51],
                              extra_classes={"trousers": [2]})
VARIANTS["LegionB"] = _legion("Military_Male_05", "sm022", 22, TIGER, OLIVE_TROUSER, BROWN_BOOT, [28, 29, 30, 31, 32, 33, 34],
                              extra_classes={"trousers": [3]})
VARIANTS["LegionC"] = _legion("Military_Male_06", "sm021", 23, dict(OLIVE_TROUSER, value=0.82), dict(KHAKI, value=0.78), BLACK_BOOT,
                              [29, 30, 31, 32, 33, 34, 35], cap_fill={"mode": "fabric", "fabric": "OliveSateen", "tile_px": 260, "value": 0.7, "smooth": 0.22},
                              extra_classes={"trousers": [7]})

# =====================================================================================================
# Enemies: Greycoats (Military_Male_03 / 04: vest bodies; Rocketbox helmet/equipment/pistol/sunglasses removed)
# =====================================================================================================
_GREY_GRIME = dict(ENEMY_GRIME, dust={"trousers": [0.26, 0.04, 0.0, 0.95], "boot": [0.30, 0.18, 0.0, 0.2], "shirt": [0.06, 0.03, 0.9, 1.6],
                                      "vest": [0.10, 0.05, 0.9, 1.6]},
                   sweat=[["shirt", -0.20, 0.0, 1.33, 0.10, 0.6], ["shirt", 0.20, 0.0, 1.33, 0.10, 0.6]],
                   wear=[["trousers", -0.11, -0.07, 0.50, 0.07, 0.3], ["trousers", 0.11, -0.07, 0.50, 0.07, 0.3]])
BLACK_GLOVE = {"mode": "recolor", "color": [0.07, 0.065, 0.06], "contrast": 0.8, "smooth": 0.40, "flatten": 0.3}
VARIANTS["GreycoatA"] = {"avatar": "Military_Male_03", "seed": 31, "materials": {
    "sm004_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "glove", "foot": "boot"},
                   "classes": {"trousers": [35, 39, 38, 42, 30, 32], "shirt": [44, 45, 31, 27, 33], "skin": [46, 47], "glove": [48, 60, 49, 61],
                               "boot": [36, 37, 40, 41]},
                   "paintout": [(318, 1012, 440, 1206), (1630, 1022, 1760, 1206), (955, 705, 1095, 765), (978, 1143, 1068, 1188),
                                (978, 1252, 1068, 1303), (978, 1362, 1068, 1408)],
                   "fills": {"shirt": GREYGREEN, "trousers": dict(GREYGREEN, value=0.74), "vest": GREYGREEN_VEST, "glove": BLACK_GLOVE,
                             "boot": BLACK_BOOT, "skin": SKIN_KEEP},
                   "grime": _GREY_GRIME},
    "sm004_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
}}
VARIANTS["GreycoatB"] = {"avatar": "Military_Male_04", "seed": 32, "materials": {
    "sm005_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "glove", "foot": "boot"},
                   "classes": {"trousers": [43, 39, 48, 42, 30, 32], "shirt": [33, 36, 31, 27, 34], "glove": [44, 45, 37, 38], "boot": [46, 47, 40, 41]},
                   "paintout": [(288, 1072, 420, 1286), (1650, 1072, 1780, 1296), (955, 705, 1095, 765), (978, 1143, 1068, 1188),
                                (978, 1252, 1068, 1303), (978, 1362, 1068, 1408), (1258, 1255, 1338, 1405)],
                   "fills": {"shirt": GREYGREEN, "trousers": dict(GREYGREEN, value=0.74), "vest": GREYGREEN_VEST, "glove": BLACK_GLOVE,
                             "boot": BLACK_BOOT},
                   "grime": _GREY_GRIME},
    "sm005_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
}}

# =====================================================================================================
# Squad civilians-in-kit and booth (clean civilian atlases: recolour keeps the cloth detail)
# =====================================================================================================
HAIR_KEEP = {"mode": "keep", "smooth": 0.35}
VARIANTS["Hollis"] = {"avatar": "Male_Adult_05", "seed": 41, "materials": {
    "m009_body": {"part": "Body", "default_by_part": {"torso": "shirt", "leg": "trousers", "arm": "shirt", "hand": "skin", "foot": "boot"},
                  "color_rules": [{"class": "vest", "within": ["shirt"], "hue": [20, 60], "sat": [0.18, 0.75], "val": [0.35, 1.0]}],
                  "fills": {"vest": {"mode": "recolor", "color": [0.40, 0.38, 0.28], "contrast": 1.0, "smooth": 0.22},
                            "shirt": {"mode": "recolor", "color": [0.62, 0.53, 0.38], "contrast": 1.05, "smooth": 0.26},
                            "trousers": {"mode": "recolor", "color": [0.36, 0.30, 0.22], "contrast": 1.0, "smooth": 0.26},
                            "skin": SKIN_KEEP, "boot": {"mode": "keep", "smooth": 0.3}},
                  "grime": dict(SQUAD_GRIME, dust={"trousers": [0.30, 0.04, 0.0, 0.95], "boot": [0.30, 0.2, 0.0, 0.2], "vest": [0.10, 0.04, 0.9, 1.6],
                                                   "shirt": [0.08, 0.03, 0.9, 1.6]},
                                sweat=[["shirt", -0.19, 0.0, 1.33, 0.10, 0.8], ["shirt", 0.19, 0.0, 1.33, 0.10, 0.8]])},
    "m009_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}, "head_ops": [{"op": "grey_hair", "amount": 0.45, "above": 0.0, "otsu": True}]},
    "m009_opacity": {"part": "Hair", "default_by_part": {"*": "hair"}, "fills": {"hair": HAIR_KEEP},
                     "head_ops": [{"op": "grey_hair", "amount": 0.45, "above": -1.0, "otsu": True, "otsu_mul": 1.6}]},
}}
VARIANTS["Tinker"] = {"avatar": "Male_Adult_08", "seed": 42, "materials": {
    "m014_body": {"part": "Body", "default_by_part": {"torso": "shirt", "leg": "trousers", "arm": "shirt", "hand": "skin", "foot": "boot"},
                  "color_rules": [{"class": "skin", "within": ["shirt"], "hue": [0, 40], "sat": [0.2, 0.7], "val": [0.45, 1.0]}],
                  "fills": {"shirt": {"mode": "recolor", "color": [0.50, 0.46, 0.33], "contrast": 1.1, "smooth": 0.26},
                            "trousers": {"mode": "recolor", "color": [0.30, 0.30, 0.21], "contrast": 1.1, "smooth": 0.27},
                            "skin": SKIN_KEEP, "boot": {"mode": "recolor", "color": [0.20, 0.15, 0.11], "contrast": 1.0, "smooth": 0.35}},
                  "grime": dict(SQUAD_GRIME, dust={"trousers": [0.30, 0.04, 0.0, 0.95], "boot": [0.30, 0.2, 0.0, 0.2], "shirt": [0.08, 0.03, 0.9, 1.6]},
                                sweat=[["shirt", -0.19, 0.0, 1.33, 0.10, 0.9], ["shirt", 0.19, 0.0, 1.33, 0.10, 0.9], ["shirt", 0.0, 0.14, 1.30, 0.16, 0.7]])},
    "m014_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
    "m014_opacity": {"part": "Hair", "default_by_part": {"*": "hair"}, "fills": {"hair": HAIR_KEEP}},
}}
VARIANTS["SparrowCivilian"] = {"avatar": "Female_Adult_07", "seed": 43, "materials": {  # superseded (heeled boots, blazer)
    "f007_body": {"part": "Body", "default_by_part": {"torso": "jacket", "leg": "jeans", "arm": "jacket", "hand": "skin", "foot": "boot"},
                  "fills": {"jacket": {"mode": "recolor", "color": [0.55, 0.47, 0.33], "contrast": 1.05, "smooth": 0.26},
                            "jeans": {"mode": "tint", "mul": [0.95, 0.95, 0.92], "smooth": 0.28}, "skin": SKIN_KEEP, "boot": {"mode": "keep", "smooth": 0.33}}},
    "f007_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
}}
TAN_GLOVE = {"mode": "recolor", "color": [0.47, 0.36, 0.25], "contrast": 0.9, "smooth": 0.30}
VARIANTS["Sparrow"] = {
    "avatar": "Military_Female_02", "seed": 43,
    "materials": {
        "sf004_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "glove", "foot": "boot"},
                       "classes": _MM01_BODY_CLASSES, "paintout": _MM01_PAINTOUT,
                       "fills": {"shirt": dict(TAN_SHIRT, tint=[1.0, 0.92, 0.78]), "trousers": dict(KHAKI, value=0.72),
                                 "vest": OLIVE_CANVAS_VEST, "glove": TAN_GLOVE, "boot": BROWN_BOOT},
                       "grime": dict(_MM01_GRIME, sweat=[["shirt", -0.18, 0.0, 1.27, 0.09, 0.7], ["shirt", 0.18, 0.0, 1.27, 0.09, 0.7]])},
        "sf004_head": {"part": "Head", "default_by_part": {"*": "skin"}, "classes": {"tie": [150]},
                       "fills": {"skin": SKIN_KEEP, "tie": {"mode": "recolor", "color": [0.08, 0.06, 0.05], "contrast": 0.8, "smooth": 0.3}}},
    },
}
VARIANTS["Interrogator"] = {"avatar": "Business_Male_01", "seed": 51, "materials": {
    "m005_body": {"part": "Body", "default_by_part": {"torso": "suit", "leg": "suit", "arm": "suit", "hand": "glove", "foot": "shoe"},
                  "color_rules": [{"class": "tie", "within": ["suit"], "hue": [330, 20], "sat": [0.35, 1.0], "val": [0.15, 1.0]},
                                  {"class": "shirt", "within": ["suit"], "sat": [0.0, 0.25], "val": [0.55, 1.0]},
                                  {"class": "skin", "within": ["suit", "shirt"], "hue": [5, 40], "sat": [0.2, 0.7], "val": [0.45, 1.0]}],
                  "fills": {"suit": {"mode": "recolor", "color": [0.33, 0.33, 0.34], "contrast": 1.15, "smooth": 0.24},
                            "tie": {"mode": "recolor", "color": [0.10, 0.12, 0.20], "contrast": 1.0, "smooth": 0.32},
                            "shirt": {"mode": "keep", "smooth": 0.26},
                            "glove": dict(BLACK_GLOVE, flatten=0.55, contrast=0.6, smooth=0.48), "shoe": {"mode": "keep", "smooth": 0.55},
                            "skin": SKIN_KEEP}},
    "m005_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},  # natural dark hair (grey ops looked dyed)
    "m005_opacity": {"part": "Hair", "default_by_part": {"*": "hair"}, "fills": {"hair": {"mode": "keep", "smooth": 0.3}}},
}}

# =====================================================================================================
# Resume-pass additions: Magpie (pilot, CHR_MAGPIE P2) and Col. Harrou (editor-only, CHR_HARROU P1)
# =====================================================================================================
SAGE_FLIGHTSUIT = {"mode": "fabric", "fabric": "GreyGreen", "tile_px": 420, "value": 0.86, "tint": [0.90, 1.0, 0.72], "smooth": 0.27}
VARIANTS["Magpie"] = {"avatar": "Military_Male_04", "seed": 61, "materials": {
    "sm005_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "glove", "foot": "boot"},
                   "classes": {"trousers": [43, 39, 48, 42, 30, 32], "shirt": [33, 36, 31, 27, 34], "glove": [44, 45, 37, 38], "boot": [46, 47, 40, 41]},
                   "paintout": [(288, 1072, 420, 1286), (1650, 1072, 1780, 1296), (955, 705, 1095, 765), (978, 1143, 1068, 1188),
                                (978, 1252, 1068, 1303), (978, 1362, 1068, 1408), (1258, 1255, 1338, 1405)],
                   "fills": {"shirt": SAGE_FLIGHTSUIT, "trousers": SAGE_FLIGHTSUIT, "vest": dict(OLIVE_CANVAS_VEST, value=0.70),
                             "glove": {"mode": "recolor", "color": [0.47, 0.46, 0.37], "contrast": 0.9, "smooth": 0.28}, "boot": BLACK_BOOT},
                   "grime": dict(SQUAD_GRIME, dust={"trousers": [0.12, 0.03, 0.0, 0.95], "boot": [0.20, 0.12, 0.0, 0.2]},
                                 sweat=[["shirt", -0.20, 0.0, 1.33, 0.10, 0.6], ["shirt", 0.20, 0.0, 1.33, 0.10, 0.6]])},
    "sm005_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
}}
VARIANTS["Harrou"] = {"avatar": "Business_Male_04", "seed": 71, "materials": {
    "m015_body": {"part": "Body", "default_by_part": {"torso": "vest", "leg": "trousers", "arm": "shirt", "hand": "skin", "foot": "boot"},
                  "classes": {"trousers": [19, 20], "shirt": [2, 7, 12, 4, 9, 17], "tie": [11], "skin": [34, 35], "boot": [21, 22, 23, 24],
                              "vest": [0, 1, 6, 3, 8, 5, 10, 13, 15, 16, 14]},
                  "fills": {"shirt": {"mode": "recolor", "color": [0.60, 0.53, 0.40], "contrast": 1.05, "smooth": 0.26},
                            "trousers": {"mode": "recolor", "color": [0.50, 0.43, 0.31], "contrast": 1.0, "smooth": 0.26},
                            "vest": {"mode": "recolor", "color": [0.20, 0.20, 0.14], "contrast": 1.0, "smooth": 0.30},
                            "tie": {"mode": "recolor", "color": [0.16, 0.11, 0.08], "contrast": 1.0, "smooth": 0.32},
                            "skin": SKIN_KEEP, "boot": {"mode": "keep", "smooth": 0.5}},
                  "bands": [dict(ARMBAND_L, within=["shirt"], pos_blur=16)],
                  "grime": dict(ENEMY_GRIME, dust={"trousers": [0.14, 0.03, 0.0, 0.95], "boot": [0.18, 0.1, 0.0, 0.2]})},
    "m015_head": {"part": "Head", "default_by_part": {"*": "skin"}, "fills": {"skin": SKIN_KEEP}},
}}
