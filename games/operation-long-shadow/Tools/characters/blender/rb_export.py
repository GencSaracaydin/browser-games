"""Rocketbox -> Unity-ready FBX (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/rb_export.py -- <spec.json>
Spec keys:
  src_fbx            source Rocketbox FBX (3ds Max Biped, cm, Y up)
  dump               rb_dump.py output for the same FBX (face order = island ids)
  out_fbx            output path
  mesh_name          name of the skinned body mesh object (e.g. SK_Calder)
  material_names     {source material: game material name}   (materials are placeholders; Unity uses Materials=None)
  delete_materials   [source material names whose faces are removed]
  delete_islands     [island ids (rb_islands) whose faces are removed]
  keep_faces         optional {"parts": [...], "min_z": z, "materials": [...]} -> keep only matching faces (FP arms)
  slim_vest          optional {"keep_islands": [shirt islands], "iterations": n, "shrink": m, "material": src material}: the
                     torso-weighted faces of that material that are not in keep_islands (= the modern armour vest shell) are
                     relaxed (Laplacian, boundary pinned) and pulled in along their normals, so the vest reads as a plain
                     shirt/jacket (booth prisoner without gear). UVs and weights unchanged.
  props              [{"file": glb/fbx/obj, "name": ..., "material": ..., "bone": "Bip01 Head" | null (null = transfer weights from body),
                      "matrix": 4x4 world matrix (Blender coords, metres) applied to the prop}]
  preview_textures   {game material: {"bc":..,"n":..,"m":..}} -> also writes <out>.blend for rb_render.py
  remove_bones       [bone names to delete] (default: none)
Fixes applied always:
  * Biped quirk: Bip01 L/R Thigh re-parented to Bip01 Pelvis, Bip01 L/R Clavicle to Bip01 Spine2 (keep offset;
    rest/bind pose unchanged) = FixRocketboxMaxImport.cs usingMixamoAnimations=false, done offline.
  * only the hipoly mesh is kept; Bip01 Footsteps removed; transforms applied (metres, scale 1).
Export: FBX 7.4 binary, axis -Z forward / Y up, apply transform baked, scale 1 m, no leaf bones, only deform-relevant
objects, no animation (bind pose only).
"""
import bpy, bmesh, sys, json, math, os, mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0]))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=spec["src_fbx"], automatic_bone_orientation=False, use_anim=False)
dump = json.load(open(spec["dump"])) if spec.get("dump") else None

arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for o in list(bpy.data.objects):
    if o.type == "EMPTY" or (o.type == "MESH" and "poly" in o.name.lower() and "hipoly" not in o.name.lower()):
        bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]


def part_of(bone):
    b = bone.lower()
    if "finger" in b or " hand" in b:
        return "hand"
    if "forearm" in b or "upperarm" in b:
        return "arm"
    if "toe" in b or " foot" in b:
        return "foot"
    if "calf" in b or "thigh" in b:
        return "leg"
    if "spine" in b or "pelvis" in b or "clavicle" in b:
        return "torso"
    return "head" if b else "none"


# ---------- face deletion
del_mats = set(spec.get("delete_materials", []))
del_isl = set(spec.get("delete_islands", []))
keep = spec.get("keep_faces")
off = 0
for ob in meshes:
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    dl = bm.verts.layers.deform.active
    gnames = {g.index: g.name for g in ob.vertex_groups}
    slim = spec.get("slim_vest")
    vest_layer = bm.faces.layers.int.new("wp05_vest") if slim else None
    kill = []
    for f in bm.faces:
        mname = me.materials[f.material_index].name
        k = off + f.index
        isl = dump["faces"][k]["isl"] if dump else -1
        if mname in del_mats or isl in del_isl:
            kill.append(f)
            continue
        if slim and mname == slim.get("material") and isl not in slim.get("keep_islands", []):
            w = {}
            for v in f.verts:
                for gi, wt in v[dl].items():
                    w[gi] = w.get(gi, 0) + wt
            if w and part_of(gnames.get(max(w, key=w.get), "")) == "torso":
                f[vest_layer] = 1
        if keep:
            w = {}
            for v in f.verts:
                for gi, wt in v[dl].items():
                    w[gi] = w.get(gi, 0) + wt
            bone = gnames.get(max(w, key=w.get), "") if w else ""
            z = (ob.matrix_world @ f.calc_center_median()).z
            ok = part_of(bone) in keep.get("parts", []) or (part_of(bone) == "torso" and keep.get("min_z", 99) <= z <= keep.get("max_z", 99))
            if keep.get("materials") and mname not in keep["materials"]:
                ok = False
            if not ok:
                kill.append(f)
    off += len(bm.faces)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    # drop unused material slots
    used = {p.material_index for p in me.polygons}
    for i in reversed(range(len(me.materials))):
        if i not in used:
            ob.active_material_index = i
            bpy.context.view_layer.objects.active = ob
            bpy.ops.object.material_slot_remove()
    for m in me.materials:
        if m and m.name in spec.get("material_names", {}):
            m.name = spec["material_names"][m.name]

# ---------- slim the vest shell (booth prisoner)
if spec.get("slim_vest"):
    sv = spec["slim_vest"]
    for ob in meshes:
        me = ob.data
        if "wp05_vest" not in me.attributes:
            continue
        bm = bmesh.new()
        bm.from_mesh(me)
        lay = bm.faces.layers.int.get("wp05_vest")
        vest_f = [f for f in bm.faces if f[lay] == 1]
        vv = {v for f in vest_f for v in f.verts}
        border = {v for v in vv if any(f[lay] != 1 for f in v.link_faces) or v.is_boundary}
        inner = [v for v in vv if v not in border]
        # hop distance from the pinned border (falloff for the inward pull)
        dist = {v: 0 for v in border}
        front = list(border)
        while front:
            nxt = []
            for v in front:
                for e in v.link_edges:
                    o = e.other_vert(v)
                    if o in vv and o not in dist:
                        dist[o] = dist[v] + 1
                        nxt.append(o)
            front = nxt
        nrm0 = {v: v.normal.copy() for v in inner}
        # Taubin lambda/mu smoothing: removes plate edges and buckles without the volume loss (pinched waist) of plain
        # Laplacian smoothing
        nb = {v: [e.other_vert(v) for e in v.link_edges] for v in inner}
        for it in range(sv.get("iterations", 30) * 2):
            k = 0.5 if it % 2 == 0 else -0.53
            new_co = {}
            for v in inner:
                c = sum((o.co for o in nb[v]), mathutils.Vector()) / max(len(nb[v]), 1)
                new_co[v] = v.co + (c - v.co) * k
            for v, c in new_co.items():
                v.co = c
        unit = 1.0 / max(ob.matrix_world.to_scale()[0], 1e-6)  # spec in metres -> mesh-local units (Rocketbox: cm)
        for v in inner:
            fall = min(1.0, dist.get(v, 1) / float(sv.get("falloff_hops", 3)))
            v.co -= nrm0[v] * sv.get("shrink", 0.012) * unit * fall
        bm.to_mesh(me)
        bm.free()
        me.attributes.remove(me.attributes["wp05_vest"])
        print("SLIMMED", ob.name, len(vest_f), "vest faces", len(inner), "inner verts")

# ---------- hierarchy fix (edit bones keep their head/tail: bind pose unchanged)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
for child, parent in (("Bip01 L Thigh", "Bip01 Pelvis"), ("Bip01 R Thigh", "Bip01 Pelvis"),
                      ("Bip01 L Clavicle", "Bip01 Spine2"), ("Bip01 R Clavicle", "Bip01 Spine2")):
    if child in eb and parent in eb:
        eb[child].use_connect = False
        eb[child].parent = eb[parent]
for bn in spec.get("remove_bones", []):
    if bn in eb:
        eb.remove(eb[bn])
bpy.ops.object.mode_set(mode="OBJECT")

# ---------- apply transforms: armature + meshes to identity, metres
for ob in meshes:
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw
for o in [arm] + meshes:
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for ob in meshes:
    ob.parent = arm
    ob.matrix_parent_inverse.identity()
    if not any(m.type == "ARMATURE" for m in ob.modifiers):
        ob.modifiers.new("Armature", "ARMATURE").object = arm
    else:
        for m in ob.modifiers:
            if m.type == "ARMATURE":
                m.object = arm
for ob in meshes:
    ob.name = spec.get("mesh_name", ob.name) if len(meshes) == 1 else ob.name
    ob.data.name = ob.name

# ---------- props
body = meshes[0]


def _bbox(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    mn = mathutils.Vector([min(v[i] for v in vs) for i in range(3)])
    mx = mathutils.Vector([max(v[i] for v in vs) for i in range(3)])
    return mn, mx


def head_frame():
    g = body.vertex_groups.get("Bip01 Head")
    hb = arm.matrix_world @ arm.data.bones["Bip01 Head"].head_local
    vs = [body.matrix_world @ v.co for v in body.data.vertices
          if any(x.group == g.index and x.weight > 0.5 for x in v.groups)]
    top = max(v.z for v in vs)
    band = [v for v in vs if top - 0.10 < v.z < top - 0.05]
    cx = (max(v.x for v in band) + min(v.x for v in band)) / 2
    cy = (max(v.y for v in band) + min(v.y for v in band)) / 2
    return top, cx, cy, max(v.x for v in band) - min(v.x for v in band), max(v.y for v in band) - min(v.y for v in band)


def fit_prop(po, body, arm, f):
    import bmesh
    mn, mx = _bbox(po)
    c = (mn + mx) / 2
    T = mathutils.Matrix.Translation
    R = mathutils.Matrix.Rotation
    po.data.transform(T(-c))
    po.data.transform(R(math.radians(f.get("rotz", -90)), 4, "Z"))
    mn, mx = _bbox(po)
    ext = mx - mn
    if isinstance(f.get("size"), list):
        s = [f["size"][i] / ext[i] for i in range(3)]
        po.data.transform(mathutils.Matrix.Diagonal((s[0], s[1], s[2], 1)))
    else:
        ax = "xyz".index(f.get("size_axis", "x"))
        s = f["size"] / ext[ax]
        po.data.transform(mathutils.Matrix.Scale(s, 4))
    mn, mx = _bbox(po)
    if f.get("anchor", "head") == "head":
        top, cx, cy, w, d = head_frame()
        print("HEAD", round(top, 3), round(cx, 3), round(cy, 3), "w", round(w, 3), "d", round(d, 3))
        off = f.get("offset", [0, 0, 0])
        po.data.transform(T(mathutils.Vector((cx - (mn.x + mx.x) / 2 + off[0], cy - (mn.y + mx.y) / 2 + off[1], top + f.get("top_gap", 0.02) - mx.z + off[2]))))
        if f.get("strap_cut"):  # [depth below the top, radius]: remove the hanging chin strap, keep the brim
            import bmesh as _bm
            bm = _bm.new()
            bm.from_mesh(po.data)
            ztop = max(v.co.z for v in bm.verts)
            ccx = (min(v.co.x for v in bm.verts) + max(v.co.x for v in bm.verts)) / 2
            ccy = (min(v.co.y for v in bm.verts) + max(v.co.y for v in bm.verts)) / 2
            dpt, rad = f["strap_cut"]
            if f.get("strap_cut_frac"):  # depth as a fraction of the prop height (scale independent)
                dpt = f["strap_cut_frac"] * (ztop - min(v.co.z for v in bm.verts))
            kill = [fa for fa in bm.faces if fa.calc_center_median().z < ztop - dpt
                    and math.hypot(fa.calc_center_median().x - ccx, fa.calc_center_median().y - ccy) < rad]
            _bm.ops.delete(bm, geom=kill, context="FACES")
            _bm.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
            bm.to_mesh(po.data)
            bm.free()
            print("STRAP CUT", len(kill), "faces")
        if f.get("cut_below"):
            import bmesh as _bm
            bm = _bm.new()
            bm.from_mesh(po.data)
            ztop = max(v.co.z for v in bm.verts)
            kill = [fa for fa in bm.faces if fa.calc_center_median().z < ztop - f["cut_below"]]
            _bm.ops.delete(bm, geom=kill, context="FACES")
            _bm.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
            bm.to_mesh(po.data)
            bm.free()
        if f.get("rotx"):
            pivot = mathutils.Vector((cx, cy, top - 0.08))
            po.data.transform(T(pivot) @ R(math.radians(f["rotx"]), 4, "X") @ T(-pivot))
        if f.get("roty"):
            pivot = mathutils.Vector((cx, cy, top - 0.08))
            po.data.transform(T(pivot) @ R(math.radians(f["roty"]), 4, "Y") @ T(-pivot))
        for tr in f.get("trim", []):  # delete body faces of a material inside the hat (hair cards poking through)
            bm = bmesh.new()
            bm.from_mesh(body.data)
            mi = [i for i, m in enumerate(body.data.materials) if m and m.name == tr["material"]]
            kill = [fa for fa in bm.faces if fa.material_index in mi and (body.matrix_world @ fa.calc_center_median()).z > top - tr.get("below_top", 0.09)]
            bmesh.ops.delete(bm, geom=kill, context="FACES")
            bm.to_mesh(body.data)
            bm.free()
    elif f.get("anchor") == "back":  # backpack: front face on the back surface, centred on Spine2 height (+offset)
        sb = arm.matrix_world @ arm.data.bones["Bip01 Spine2"].head_local
        off = f.get("offset", [0, 0, 0])
        zc = sb.z + off[2]
        back = [body.matrix_world @ v.co for v in body.data.vertices]
        back = [v for v in back if abs(v.z - zc) < 0.12 and abs(v.x - sb.x) < 0.12]
        ymax = max(v.y for v in back)
        po.data.transform(T(mathutils.Vector((sb.x - (mn.x + mx.x) / 2 + off[0], ymax + f.get("gap", 0.01) - mn.y + off[1],
                                              zc - (mn.z + mx.z) / 2))))
    elif f.get("anchor") == "neck":  # scarf: ring centred on the neck bone, top at neck height + top_gap
        nb = arm.matrix_world @ arm.data.bones["Bip01 Neck"].head_local
        off = f.get("offset", [0, 0, 0])
        po.data.transform(T(mathutils.Vector((nb.x - (mn.x + mx.x) / 2 + off[0], nb.y - (mn.y + mx.y) / 2 + off[1],
                                              nb.z + f.get("top_gap", 0.0) - mx.z + off[2]))))
    else:  # chest / torso
        nb = arm.matrix_world @ arm.data.bones["Bip01 Neck"].head_local
        sb = arm.matrix_world @ arm.data.bones["Bip01 Spine2"].head_local
        off = f.get("offset", [0, 0, 0])
        po.data.transform(T(mathutils.Vector((sb.x - (mn.x + mx.x) / 2 + off[0], sb.y - (mn.y + mx.y) / 2 + off[1], nb.z + f.get("top_gap", 0.0) - mx.z + off[2]))))
    if f.get("push_out"):
        mod = po.modifiers.new("sw", "SHRINKWRAP")
        mod.target = body
        mod.wrap_method = "NEAREST_SURFACEPOINT"
        mod.wrap_mode = "OUTSIDE"
        mod.offset = f["push_out"]
        bpy.ops.object.select_all(action="DESELECT")
        po.select_set(True)
        bpy.context.view_layer.objects.active = po
        bpy.ops.object.modifier_apply(modifier="sw")
for p in spec.get("props", []):
    before = set(bpy.data.objects)
    fp = p["file"]
    if fp.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=fp)
    elif fp.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=fp)
    else:
        bpy.ops.wm.obj_import(filepath=fp)
    new = [o for o in bpy.data.objects if o not in before]
    pm = [o for o in new if o.type == "MESH"]
    other = [o.name for o in new if o.type != "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in pm:
        o.select_set(True)
    bpy.context.view_layer.objects.active = pm[0]
    if len(pm) > 1:
        bpy.ops.object.join()
    po = bpy.context.view_layer.objects.active
    # unparent + apply the import transform, then the fit matrix
    mw = po.matrix_world.copy()
    po.parent = None
    po.matrix_world = mw
    for n in other:
        if n in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    if "matrix" in p:
        po.matrix_world = mathutils.Matrix(p["matrix"]) @ po.matrix_world
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if p.get("decimate"):  # e.g. the WP10 PRC-77 hero prop (4.9k) carried on Tinker's back -> character prop budget
        dm = po.modifiers.new("dec", "DECIMATE")
        dm.ratio = p["decimate"]
        bpy.ops.object.modifier_apply(modifier="dec")
    if "fit" in p:
        fit_prop(po, body, arm, p["fit"])
    po.name = p["name"]
    po.data.name = p["name"]
    for i, m in enumerate(po.data.materials):
        if m:
            m.name = p.get("material", "M_" + p["name"]) + ("" if i == 0 else f"_{i}")
    if len(po.data.materials) == 0:
        po.data.materials.append(bpy.data.materials.new(p.get("material", "M_" + p["name"])))
    # skin
    po.vertex_groups.clear()
    if p.get("bone"):
        g = po.vertex_groups.new(name=p["bone"])
        g.add(list(range(len(po.data.vertices))), 1.0, "REPLACE")
    else:
        for vg in body.vertex_groups:
            po.vertex_groups.new(name=vg.name)
        mod = po.modifiers.new("dt", "DATA_TRANSFER")
        mod.object = body
        mod.use_vert_data = True
        mod.data_types_verts = {"VGROUP_WEIGHTS"}
        mod.vert_mapping = "POLYINTERP_NEAREST"
        mod.layers_vgroup_select_src = "ALL"
        mod.layers_vgroup_select_dst = "NAME"
        bpy.ops.object.select_all(action="DESELECT")
        po.select_set(True)
        bpy.context.view_layer.objects.active = po
        bpy.ops.object.modifier_apply(modifier="dt")
        # smooth + limit to 4 influences, drop empty groups
        bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)
        for vg in list(po.vertex_groups):
            if not any(vg.index in [g.group for g in v.groups if g.weight > 1e-4] for v in po.data.vertices):
                po.vertex_groups.remove(vg)
    po.parent = arm
    mod = po.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

# ---------- booth restraints: leather wrist cuffs with a steel buckle + taped electrodes on both forearms
def _restraints(body, arm, mat_name):
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(body, dg)
    V = mathutils.Vector

    def quad(vs, uvs):
        f = bm.faces.new(vs)
        for l, uv in zip(f.loops, uvs):
            l[uvl].uv = uv
        return f

    def band(center, axis, ex, ey, ra, rb, width, thick, seg, uv0, uv1):
        # elliptical band: inner radii (ra along ex, rb along ey), outer = inner + thick
        rings = []
        for (dr, h) in ((0.0, -width / 2), (thick, -width / 2), (thick, width / 2), (0.0, width / 2)):
            ring = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                p = center + axis * h + ex * math.cos(a) * (ra + dr) + ey * math.sin(a) * (rb + dr)
                ring.append(bm.verts.new(p))
            rings.append(ring)
        vrow = [uv0[1], uv0[1] + (uv1[1] - uv0[1]) * 0.1, uv1[1] - (uv1[1] - uv0[1]) * 0.1, uv1[1], uv0[1]]
        for k in range(4):
            r0, r1 = rings[k], rings[(k + 1) % 4]
            for i in range(seg):
                j = (i + 1) % seg
                u0 = uv0[0] + (uv1[0] - uv0[0]) * i / seg
                u1 = uv0[0] + (uv1[0] - uv0[0]) * (i + 1) / seg
                quad([r0[i], r0[j], r1[j], r1[i]], [(u0, vrow[k]), (u1, vrow[k]), (u1, vrow[k + 1]), (u0, vrow[k + 1])])

    def box(center, ax, ay, az, sx, sy, sz, uv0, uv1):
        c = [center + ax * (sx * i) + ay * (sy * j) + az * (sz * k) for i in (-.5, .5) for j in (-.5, .5) for k in (-.5, .5)]
        vs = [bm.verts.new(p) for p in c]
        idx = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        for q in idx:
            quad([vs[i] for i in q], [(uv0[0], uv0[1]), (uv1[0], uv0[1]), (uv1[0], uv1[1]), (uv0[0], uv1[1])])

    def disc(center, n, r, h, seg, uv0, uv1):
        ex = n.orthogonal().normalized()
        ey = n.cross(ex)
        top = [bm.verts.new(center + n * h + (ex * math.cos(2 * math.pi * i / seg) + ey * math.sin(2 * math.pi * i / seg)) * r) for i in range(seg)]
        bot = [bm.verts.new(center + (ex * math.cos(2 * math.pi * i / seg) + ey * math.sin(2 * math.pi * i / seg)) * r * 1.05) for i in range(seg)]
        cu = ((uv0[0] + uv1[0]) / 2, (uv0[1] + uv1[1]) / 2)
        rad = (uv1[0] - uv0[0]) / 2
        f = bm.faces.new(top)
        for l, i in zip(f.loops, range(seg)):
            a = 2 * math.pi * i / seg
            l[uvl].uv = (cu[0] + math.cos(a) * rad, cu[1] + math.sin(a) * rad)
        for i in range(seg):
            j = (i + 1) % seg
            quad([bot[i], bot[j], top[j], top[i]], [uv0, (uv1[0], uv0[1]), uv1, (uv0[0], uv1[1])])

    groups = {}
    for side in "LR":
        fa = arm.data.bones[f"Bip01 {side} Forearm"]
        hb = arm.data.bones[f"Bip01 {side} Hand"]
        e = arm.matrix_world @ fa.head_local
        w = arm.matrix_world @ hb.head_local
        axis = (w - e).normalized()
        # dorsal direction from the hand anatomy (opposite of the palm normal), orthogonal to the forearm axis
        F = (arm.matrix_world @ arm.data.bones[f"Bip01 {side} Finger2"].head_local - w).normalized()
        K = (arm.matrix_world @ arm.data.bones[f"Bip01 {side} Finger1"].head_local - arm.matrix_world @ arm.data.bones[f"Bip01 {side} Finger4"].head_local).normalized()
        N = K.cross(F).normalized() * (1 if side == "R" else -1)
        dors = -(N - axis * N.dot(axis)).normalized()
        side_v = axis.cross(dors).normalized()
        start = len(bm.verts)
        # wrist cross-section from the body mesh (rays from the axis outward)
        c = w - axis * 0.035

        def radius(dirv):
            hit = bvh.ray_cast(c + dirv * 0.2, -dirv, 0.25)
            return (0.2 - hit[3]) if hit[0] is not None else 0.03
        ra = max(radius(dors), radius(-dors)) + 0.004
        rb = max(radius(side_v), radius(-side_v)) + 0.004
        band(c, axis, dors, side_v, ra, rb, 0.05, 0.006, 24, (0.0, 0.0), (0.5, 1.0))
        # buckle on the outer (thumb-away) side, steel
        box(c + side_v * (rb + 0.009), axis, dors, side_v, 0.034, 0.026, 0.006, (0.52, 0.02), (0.73, 0.48))
        # electrode + tape on the dorsal forearm, 45 % from the elbow
        p = e + (w - e) * 0.45
        hit = bvh.ray_cast(p + dors * 0.2, -dors, 0.25)
        if hit[0] is not None:
            n = hit[1].normalized()
            q = hit[0] + n * 0.0015
            # tape: thin square patch
            box(q, axis, n.cross(axis).normalized(), n, 0.042, 0.038, 0.0012, (0.76, 0.02), (0.98, 0.48))
            disc(q + n * 0.0008, n, 0.011, 0.0045, 16, (0.76, 0.52), (0.98, 0.98))
        groups[side] = list(range(start, len(bm.verts)))
    me = bpy.data.meshes.new("SK_Calder_Booth_Cuffs")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("SK_Calder_Booth_Cuffs", me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new(mat_name)
    me.materials.append(mat)
    for side, ids in groups.items():
        g = ob.vertex_groups.new(name=f"Bip01 {side} Forearm")
        g.add(ids, 1.0, "REPLACE")
    ob.parent = arm
    ob.modifiers.new("Armature", "ARMATURE").object = arm
    for p in me.polygons:
        p.use_smooth = True
    print("RESTRAINTS", len(me.polygons), "faces")


if spec.get("restraints"):
    _restraints(body, arm, spec.get("restraints_material", "M_Calder_Restraints"))

# limit body influences to 4 (Unity default skin weights = 4 bones)
for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

# ---------- preview materials (for rb_render.py; Unity ignores them: Materials = None)
pt = spec.get("preview_textures", {})
for m in bpy.data.materials:
    t = pt.get(m.name)
    if not t:
        continue
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    if t.get("bc"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["bc"])
        nt.links.new(i.outputs[0], bs.inputs["Base Color"])
        if t.get("alpha"):
            nt.links.new(i.outputs[1], bs.inputs["Alpha"])
            m.blend_method = "HASHED" if hasattr(m, "blend_method") else None
    if t.get("n"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["n"])
        i.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(i.outputs[0], nm.inputs["Color"])
        nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    if t.get("m"):
        i = nt.nodes.new("ShaderNodeTexImage")
        i.image = bpy.data.images.load(t["m"])
        i.image.colorspace_settings.name = "Non-Color"
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(i.outputs[0], sep.inputs[0])
        nt.links.new(sep.outputs[0], bs.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(i.outputs[1], inv.inputs[1])
        nt.links.new(inv.outputs[0], bs.inputs["Roughness"])

# ---------- T-pose node defaults (Unity builds the Humanoid avatar from the file's default node pose; the skin keeps
# the A-pose bind matrices). Arms horizontal along +-X, palms down, hands straight. Same idea as the official
# FixRocketboxMaxImport.cs clavicle/upper-arm rotation, done offline. tpose: false keeps the A-pose bind.
def _align_bone(arm, bn, child, target_dir):
    pb = arm.pose.bones[bn]
    h = pb.head.copy()
    c = arm.pose.bones[child].head.copy()
    cur = (c - h).normalized()
    R = cur.rotation_difference(target_dir).to_matrix().to_4x4()
    T = mathutils.Matrix.Translation
    pb.matrix = T(h) @ R @ T(-h) @ pb.matrix
    bpy.context.view_layer.update()


def _align_hand(arm, side, sx):
    pre = f"Bip01 {side} "
    pb = arm.pose.bones[pre + "Hand"]
    if pre + "Finger2" not in arm.pose.bones:
        return
    h = pb.head.copy()
    F = (arm.pose.bones[pre + "Finger2"].head - h).normalized()
    K = (arm.pose.bones[pre + "Finger1"].head - arm.pose.bones[pre + "Finger4"].head).normalized()
    N = K.cross(F).normalized() * (1 if side == "R" else -1)
    N = (N - F * N.dot(F)).normalized()
    Ft = mathutils.Vector((sx, 0, 0))
    Nt = mathutils.Vector((0, 0, -1))
    Mc = mathutils.Matrix((F, N, F.cross(N))).transposed()
    Mt = mathutils.Matrix((Ft, Nt, Ft.cross(Nt))).transposed()
    R = (Mt @ Mc.transposed()).to_4x4()
    T = mathutils.Matrix.Translation
    pb.matrix = T(h) @ R @ T(-h) @ pb.matrix
    bpy.context.view_layer.update()


if spec.get("tpose", True):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for side, sx in (("L", 1.0), ("R", -1.0)):
        _align_bone(arm, f"Bip01 {side} UpperArm", f"Bip01 {side} Forearm", mathutils.Vector((sx, 0, 0)))
        _align_bone(arm, f"Bip01 {side} Forearm", f"Bip01 {side} Hand", mathutils.Vector((sx, 0, 0)))
        _align_hand(arm, side, sx)
    bpy.ops.object.mode_set(mode="OBJECT")

# ---------- export
os.makedirs(os.path.dirname(spec["out_fbx"]), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects:
    if o.type in ("ARMATURE", "MESH"):
        o.select_set(True)
bpy.ops.export_scene.fbx(filepath=spec["out_fbx"], use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                         axis_forward="-Z", axis_up="Y", add_leaf_bones=False, use_armature_deform_only=False,
                         bake_anim=False, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE",
                         use_tspace=True, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL")
if spec.get("save_blend"):
    bpy.ops.wm.save_as_mainfile(filepath=spec["save_blend"])
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
print("EXPORTED", spec["out_fbx"], "tris", tris, "bones", len(arm.data.bones),
      "meshes", [(o.name, [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == "MESH"])
