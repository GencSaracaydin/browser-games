"""Standalone character-prop FBX (CHP_*) for swaps, pickups and corpse drops (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/export_props.py -- <spec.json>
spec: [{"file": glb, "out": fbx, "name": SM_CHP_X, "material": M_CHP_X, "rotz": -90, "size_axis": "x", "size": 0.37,
        "strap_cut_frac": 0.885 (optional), "pivot": "bottom" | "center"}]
Output: static mesh, metres, faces +Z in Unity (Blender -Y), Y up, pivot at the bottom centre (hats: rim plane centre) or
the bbox centre (rigs, vests), transforms applied, one material slot. The skinned, fitted copies of the same props are
already inside each SK_<Character>.fbx; these are the loose versions.
"""
import bpy, bmesh, sys, json, math, mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
specs = json.load(open(argv[0]))
for sp in specs:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    f = sp["file"]
    if f.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=f)
    else:
        bpy.ops.import_scene.fbx(filepath=f)
    ms = [o for o in bpy.data.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in ms:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1:
        bpy.ops.object.join()
    po = bpy.context.view_layer.objects.active
    mw = po.matrix_world.copy()
    po.parent = None
    po.matrix_world = mw
    for o in list(bpy.data.objects):
        if o != po:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = po.data
    vs = [v.co for v in me.vertices]
    mn = mathutils.Vector([min(v[i] for v in vs) for i in range(3)])
    mx = mathutils.Vector([max(v[i] for v in vs) for i in range(3)])
    me.transform(mathutils.Matrix.Translation(-(mn + mx) / 2))
    me.transform(mathutils.Matrix.Rotation(math.radians(sp.get("rotz", -90)), 4, "Z"))
    vs = [v.co for v in me.vertices]
    ext = [max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3)]
    s = sp["size"] / ext["xyz".index(sp.get("size_axis", "x"))]
    me.transform(mathutils.Matrix.Scale(s, 4))
    if sp.get("strap_cut_frac"):
        bm = bmesh.new()
        bm.from_mesh(me)
        zt = max(v.co.z for v in bm.verts)
        zb = min(v.co.z for v in bm.verts)
        kill = [fa for fa in bm.faces if fa.calc_center_median().z < zt - sp["strap_cut_frac"] * (zt - zb)]
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        bm.to_mesh(me)
        bm.free()
    vs = [v.co for v in me.vertices]
    mn = mathutils.Vector([min(v[i] for v in vs) for i in range(3)])
    mx = mathutils.Vector([max(v[i] for v in vs) for i in range(3)])
    piv = (mn + mx) / 2
    if sp.get("pivot", "bottom") == "bottom":
        piv.z = mn.z
    me.transform(mathutils.Matrix.Translation(-piv))
    po.name = me.name = sp["name"]
    for i, m in enumerate(me.materials):
        if m:
            m.name = sp["material"] + ("" if i == 0 else f"_{i}")
    bpy.ops.export_scene.fbx(filepath=sp["out"], use_selection=False, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", use_tspace=True, bake_anim=False)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    dims = [round(max(v.co[i] for v in me.vertices) - min(v.co[i] for v in me.vertices), 3) for i in range(3)]
    print("PROP", sp["name"], "tris", tris, "dims_m", dims)
