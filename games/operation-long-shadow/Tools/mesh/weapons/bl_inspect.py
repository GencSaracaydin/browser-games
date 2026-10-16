"""Blender headless: import a GLB and print objects, bbox, tris, materials, image sizes.
Run: Blender -b --factory-startup -P bl_inspect.py -- file.glb
"""
import bpy, sys
from mathutils import Vector
path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=path)
dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.context.scene.objects:
    print("OBJ", o.name, o.type, "parent", o.parent.name if o.parent else None, "loc", tuple(round(v, 4) for v in o.location), "rot", tuple(round(v, 4) for v in o.rotation_euler), "scale", tuple(round(v, 4) for v in o.scale))
    if o.type == "MESH":
        me = o.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
        mn = [min(v[i] for v in ws) for i in range(3)]
        mx = [max(v[i] for v in ws) for i in range(3)]
        print("  verts", len(me.vertices), "tris", tris, "uv", [u.name for u in me.uv_layers])
        print("  world bbox min", [round(v, 4) for v in mn], "max", [round(v, 4) for v in mx], "size", [round(mx[i] - mn[i], 4) for i in range(3)])
        for m in me.materials:
            print("  MAT", m.name if m else None)
            if m and m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type == "TEX_IMAGE" and n.image:
                        links = [l.to_socket.name + "@" + l.to_node.name for l in n.outputs[0].links] + [l.to_socket.name + "@" + l.to_node.name for l in n.outputs[1].links]
                        print("    IMG", n.image.name, n.image.size[:], n.image.colorspace_settings.name, links)
