"""Triangle / vertex / bone / height stats of every WP05 model FBX (for make_index.py budget checks) (WP05).

Run: Blender -b --factory-startup -P Tools/characters/blender/fbx_stats.py -- <ready_root> <out.json>
For skinned characters also reports the standing height (top of the body mesh, metres) and the per-renderer tris.
"""
import bpy, sys, json, os, glob

argv = sys.argv[sys.argv.index("--") + 1:]
root, out = argv[0], argv[1]
res = {}
for f in sorted(glob.glob(os.path.join(root, "Assets/_Game/Art/Models/Characters/**/*.fbx"), recursive=True)):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f, automatic_bone_orientation=False, use_anim=False)
    ms = [o for o in bpy.data.objects if o.type == "MESH"]
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in ms}
    verts = sum(len(o.data.vertices) for o in ms)
    zs = [(o.matrix_world @ v.co).z for o in ms[:1] for v in o.data.vertices]
    rel = os.path.relpath(f, root)
    res[os.path.basename(f)] = {"file": rel, "folder": rel.split("/")[-2] if "/Props/" not in rel else "Props",
                                "type": "skinned" if arm else "static", "tris": sum(tris.values()), "per_mesh": tris,
                                "verts": verts, "bones": len(arm.data.bones) if arm else 0,
                                "height_m": round(max(zs) - min(zs), 3) if zs else 0, "size_mb": round(os.path.getsize(f) / 1e6, 2)}
    print("STAT", os.path.basename(f), res[os.path.basename(f)]["tris"], res[os.path.basename(f)]["height_m"])
json.dump(res, open(out, "w"), indent=1)
