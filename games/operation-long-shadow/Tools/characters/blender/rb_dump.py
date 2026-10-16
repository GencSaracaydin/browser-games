"""Dump per-face UV / 3D / skin-weight data of a Rocketbox FBX for 2D mask building.

Run: Blender -b --factory-startup -P Tools/characters/blender/rb_dump.py -- <in.fbx> <out.json>
Output JSON: {materials:[names], faces:[{m, uv:[[u,v]...], c:[x,y,z], n:[x,y,z], b:bone, isl:island}], bones:{name:[head xyz]}}
Coordinates are Blender world (Z up, metres, character faces -Y).
"""
import bpy, bmesh, sys, json

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
res = {"materials": [], "faces": [], "bones": {}}
for b in arm.data.bones:
    res["bones"][b.name] = list(arm.matrix_world @ b.head_local)
mat_index = {}
for ob in meshes:
    if "poly" in ob.name.lower() and "hipoly" not in ob.name.lower():
        continue  # Rocketbox LOD meshes: only hipoly is used
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    dl = bm.verts.layers.deform.active
    gnames = {g.index: g.name for g in ob.vertex_groups}
    # UV islands via union-find over edges with matching UVs
    parent = list(range(len(bm.faces)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for e in bm.edges:
        lf = e.link_faces
        if len(lf) != 2:
            continue
        f0, f1 = lf

        def uvs(f):
            d = {}
            for l in f.loops:
                d[l.vert.index] = tuple(round(x, 5) for x in l[uvl].uv)
            return d

        u0, u1 = uvs(f0), uvs(f1)
        if f0.material_index == f1.material_index and all(u0[v.index] == u1[v.index] for v in e.verts):
            a, b = find(f0.index), find(f1.index)
            if a != b:
                parent[a] = b
    mw = ob.matrix_world
    for f in bm.faces:
        mname = me.materials[f.material_index].name if me.materials[f.material_index] else "none"
        if mname not in mat_index:
            mat_index[mname] = len(res["materials"])
            res["materials"].append(mname)
        w = {}
        for v in f.verts:
            if dl is not None:
                for gi, wt in v[dl].items():
                    w[gi] = w.get(gi, 0) + wt
        bone = gnames.get(max(w, key=w.get), "") if w else ""
        c = mw @ f.calc_center_median()
        n = (mw.to_3x3() @ f.normal).normalized()
        res["faces"].append({"m": mat_index[mname], "uv": [[round(l[uvl].uv[0], 5), round(l[uvl].uv[1], 5)] for l in f.loops],
                             "c": [round(x, 4) for x in c], "n": [round(x, 3) for x in n], "b": bone,
                             "isl": f"{ob.name}:{find(f.index)}"})
    bm.free()
# compact island ids
ids = {}
for f in res["faces"]:
    f["isl"] = ids.setdefault(f["isl"], len(ids))
json.dump(res, open(out, "w"))
print("dumped", len(res["faces"]), "faces", len(ids), "islands", res["materials"])
