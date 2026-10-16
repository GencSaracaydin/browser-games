"""Dry grass / weed tuft card meshes from the VEG_DRY_GRASS_CARDS atlas layout (WP10_FAL).

  Blender -b --factory-startup -P blender_grass_cards.py -- --layout T_DryGrass_layout.json --fbx SM_DryGrass.fbx [--max-h 0.7]
One object per atlas element (SM_DryGrass_<name>): three vertical quads crossed at 60 deg, base at the origin, height from
the element's pixel height (tallest element = --max-h metres), width from its aspect. UVs = the element rect.
Vertex colour R = sway weight (0 at the ground, 1 at the top), G = random phase, B = 1 (flutter). Normals point up
(soft, grass-like shading). Export: forward -Z, up Y, baked space transform, 1 unit = 1 m.
"""
import bpy, bmesh, json, math, random, sys

argv = sys.argv[sys.argv.index("--") + 1:]
L = json.load(open(argv[argv.index("--layout") + 1]))
FBX = argv[argv.index("--fbx") + 1]
MAXH = float(argv[argv.index("--max-h") + 1]) if "--max-h" in argv else 0.7
bpy.ops.wm.read_factory_settings(use_empty=True)
W, H = L["size"]
hmax = max(e["px"][3] - e["px"][1] for e in L["elements"])
objs = []
rng = random.Random(4)
for e in L["elements"]:
    x0, y0, x1, y1 = e["px"]
    u0, v0, u1, v1 = e["uv"]
    h = MAXH * (y1 - y0) / hmax
    w = h * (x1 - x0) / (y1 - y0)
    me = bpy.data.meshes.new("SM_DryGrass_" + e["name"])
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    col = bm.loops.layers.color.new("Col")
    ph = rng.random()
    for k in range(3):
        a = math.radians(60 * k + rng.uniform(-8, 8))
        dx, dy = math.cos(a) * w / 2, math.sin(a) * w / 2
        vs = [bm.verts.new(p) for p in ((-dx, -dy, 0), (dx, dy, 0), (dx, dy, h), (-dx, -dy, h))]
        f = bm.faces.new(vs)
        for l, uv, s in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1)), (0, 0, 1, 1)):
            l[uvl].uv = uv
            l[col] = (s, ph, 1.0, 1.0)
    bm.to_mesh(me); bm.free()
    me.normals_split_custom_set([(0.0, 0.0, 1.0)] * len(me.loops))
    ob = bpy.data.objects.new(me.name, me); bpy.context.collection.objects.link(ob)
    ob.data.materials.append(bpy.data.materials.get("M_DryGrass") or bpy.data.materials.new("M_DryGrass"))
    objs.append(ob)
bpy.ops.object.select_all(action="DESELECT")
for o in objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="OFF", use_tspace=False,
                         path_mode="STRIP", embed_textures=False, add_leaf_bones=False, bake_anim=False, colors_type="LINEAR")
print("GRASS", json.dumps({o.name: round(o.dimensions.z, 3) for o in objs}))
