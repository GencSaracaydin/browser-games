"""Blender (headless) prop processor for WP10_FAL.

  Blender -b --factory-startup -P blender_prop.py -- --spec job.json

job.json keys:
  glb            source GLB
  work           work folder (textures from glb_extract.py live here as src_*; outputs written here)
  fbx            output FBX path
  name           mesh base name, e.g. "Sandbag" -> SM_Sandbag(_LODn)
  size           [mode, metres]  mode: length | height | max | width
  yaw            extra yaw (deg, CCW seen from above) after auto alignment (default 0)
  auto_align     true: rotate 45 deg (3/4 front-left reference) then snap to the min-area rectangle
  pivot          "bottom" (default) | "back" (wall-mounted: back face centre at origin) | "center"
  tris           LOD0 triangle budget
  lods           list of ratios, e.g. [1, 0.5, 0.25]; [1] -> single mesh "SM_<name>"
  tex_src        texture size written for the work maps (AO / rebakes)
  rebake         true: decimated LOD0 gets fresh UVs and baked BC/N/MR from the source mesh
  ao             true: bake ambient occlusion (work/ao.png) on LOD0 UVs
  ao_samples     default 64
  keep_parts     list of extra objects to export as-is (unused)
Writes work/stats.json with dims, tris per LOD, transforms.
Coordinate convention: the object's front faces Blender -Y. The FBX export (forward -Z, up Y, baked space
transform, FBX_SCALE_ALL) puts it facing Unity +Z with identity rotation and scale 1 (1 unit = 1 m).
"""
import bpy, bmesh, json, math, sys, os
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[argv.index("--spec") + 1]))
work = spec["work"]
os.makedirs(work, exist_ok=True)
stats = {}


def log(*a):
    print("[blender_prop]", *a, flush=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def import_glb(path):
    before = set(bpy.data.objects)
    if path.lower().endswith(".obj"):
        bpy.ops.wm.obj_import(filepath=path, forward_axis="NEGATIVE_Z", up_axis="Y")
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in objs if o.type == "MESH"]
    # apply parent transforms
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    for o in objs:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    return ob


def verts_world(ob):
    n = len(ob.data.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def hull2d(pts):
    pts = np.unique(np.round(pts, 6), axis=0)
    pts = pts[np.lexsort((pts[:, 1], pts[:, 0]))]
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(tuple(p))
    for p in pts[::-1]:
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(tuple(p))
    return np.array(lower[:-1] + upper[:-1])


def min_area_angle(xy):
    h = hull2d(xy)
    best = (1e18, 0.0)
    for i in range(len(h)):
        e = h[(i + 1) % len(h)] - h[i]
        ang = math.atan2(e[1], e[0])
        c, s = math.cos(-ang), math.sin(-ang)
        r = h @ np.array([[c, -s], [s, c]]).T
        area = np.ptp(r[:, 0]) * np.ptp(r[:, 1])
        if area < best[0]:
            best = (area, ang)
    return best[1]


def rotate_z(ob, deg):
    ob.data.transform(Matrix.Rotation(math.radians(deg), 4, "Z"))
    ob.data.update()


def align(ob):
    if spec.get("tilt"):   # [axis, deg] e.g. ["X", 90] lays an upright book flat
        ax, deg = spec["tilt"]
        ob.data.transform(Matrix.Rotation(math.radians(deg), 4, ax))
        ob.data.update()
    if spec.get("auto_align", True):
        rotate_z(ob, 45.0)
        co = verts_world(ob)
        ang = math.degrees(min_area_angle(co[:, :2]))
        # snap: smallest rotation that makes the rectangle axis-aligned
        r = -((ang + 45.0) % 90.0 - 45.0)
        if abs(r) > 0.01:
            rotate_z(ob, r)
        stats["auto_align_fine_deg"] = round(r, 2)
    if spec.get("yaw"):
        rotate_z(ob, float(spec["yaw"]))
    for ax, deg in spec.get("level", []):   # WP09: small post-yaw corrections for scans that came out tilted
        ob.data.transform(Matrix.Rotation(math.radians(float(deg)), 4, ax))
        ob.data.update()


def scale_and_pivot(ob):
    co = verts_world(ob)
    mn, mx = co.min(0), co.max(0)
    d = mx - mn
    mode, val = spec["size"]
    cur = {"length": max(d[0], d[1]), "width": min(d[0], d[1]), "height": d[2], "max": d.max()}[mode]
    s = val / cur
    ob.data.transform(Matrix.Scale(s, 4))
    co = co * s
    mn, mx = co.min(0), co.max(0)
    piv = spec.get("pivot", "bottom")
    if piv == "bottom":
        t = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]))
    elif piv == "front":   # wall-set: front face plane, bottom edge (body extends behind, into the wall)
        t = Vector(((mn[0] + mx[0]) / 2, mn[1], mn[2]))
    elif piv == "back":
        t = Vector(((mn[0] + mx[0]) / 2, mx[1], (mn[2] + mx[2]) / 2))
    else:
        t = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mn[2] + mx[2]) / 2))
    ob.data.transform(Matrix.Translation(-t))
    ob.data.update()
    co = verts_world(ob)
    stats["scale_applied"] = round(s, 5)
    stats["dims_m"] = [round(float(x), 4) for x in (co.max(0) - co.min(0))]
    stats["bbox_min"] = [round(float(x), 4) for x in co.min(0)]
    stats["bbox_max"] = [round(float(x), 4) for x in co.max(0)]


def stretch(ob, st):
    """WP09: lengthen along one axis by moving both ends outward and scaling only the middle band, so wheels,
    legs and end caps keep their shape. st = {"axis": 0|1|2, "target": metres, "keep": metres kept rigid at each end}."""
    ax = int(st.get("axis", 0)); keep = st.get("keep", 0.15)
    co = verts_world(ob)
    lo, hi = co[:, ax].min(), co[:, ax].max()
    if isinstance(keep, (list, tuple)):          # asymmetric: [rigid at the low end, rigid at the high end] (e.g. tripod legs)
        klo, khi = float(keep[0]), float(keep[1])
        b0, b1 = lo + klo, hi - khi                      # band to stretch
        grow = float(st["target"]) - (hi - lo)
        k = (b1 - b0 + grow) / (b1 - b0)
        x = co[:, ax]
        out = np.where(x >= b1, x + grow, np.where(x <= b0, x, b0 + (x - b0) * k))
        sp = float(st.get("splay", 1.0))
        if sp != 1.0:                                    # widen the footprint toward the low end (legs splay)
            t = np.clip((b1 - x) / max(b1 - lo, 1e-6), 0, 1)
            f = 1 + (sp - 1) * t
            for a2 in [i for i in range(3) if i != ax]:
                cc = (co[:, a2].min() + co[:, a2].max()) / 2 if a2 != ax else 0
                co[:, a2] = cc + (co[:, a2] - cc) * f
        co[:, ax] = out
    else:
        keep = float(keep)
        c = (lo + hi) / 2; half = (hi - lo) / 2
        new_half = float(st["target"]) / 2; inner = half - keep
        k = (new_half - keep) / inner
        x = co[:, ax] - c
        out = np.where(np.abs(x) > inner, x + np.sign(x) * (new_half - half), x * k)
        co[:, ax] = out + c
    ob.data.vertices.foreach_set("co", co.reshape(-1).astype(np.float32))
    ob.data.update()
    co = verts_world(ob)
    stats["stretch"] = st
    stats["dims_m"] = [round(float(v), 4) for v in (co.max(0) - co.min(0))]
    stats["bbox_min"] = [round(float(v), 4) for v in co.min(0)]
    stats["bbox_max"] = [round(float(v), 4) for v in co.max(0)]


def cleanup(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def decimate_copy(src, ratio, name):
    ob = src.copy()
    ob.data = src.data.copy()
    ob.name = name
    ob.data.name = name
    bpy.context.collection.objects.link(ob)
    if ratio < 0.999:
        # big reductions in steps (a single 0.006 collapse degenerates to a tetrahedron)
        target = max(4, int(tri_count(ob) * ratio))
        for _ in range(8):
            cur = tri_count(ob)
            if cur <= target * 1.03:
                break
            step = max(target / cur, 0.25)
            m = ob.modifiers.new("dec", "DECIMATE")
            m.decimate_type = "COLLAPSE"
            m.ratio = step
            m.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = ob
            bpy.ops.object.select_all(action="DESELECT")
            ob.select_set(True)
            bpy.ops.object.modifier_apply(modifier="dec")
    return ob


def weld(ob):
    """Merge split vertices (UV/normal seams) so collapse decimation works on one connected surface (rebake path only)."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    d = max(ob.dimensions) * 1e-5
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=d)
    bm.to_mesh(ob.data); bm.free(); ob.data.update()


def setup_cycles(samples=32):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.render.threads_mode = "FIXED"
    sc.render.threads = 4
    sc.render.bake.margin = 8


def new_image(name, size, noncolor):
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    return img


def add_bake_target(ob, img):
    nodes = []
    for slot in ob.material_slots:
        mat = slot.material
        if mat is None:
            continue
        mat.use_nodes = True
        n = mat.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = img
        n.select = True
        mat.node_tree.nodes.active = n
        nodes.append((mat, n))
    return nodes


def remove_bake_target(nodes):
    for mat, n in nodes:
        mat.node_tree.nodes.remove(n)


def save(img, path):
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def bake_ao(ob, size, path):
    setup_cycles(spec.get("ao_samples", 64))
    bpy.context.scene.world = bpy.data.worlds.new("w") if bpy.context.scene.world is None else bpy.context.scene.world
    # AO distance relative to the object (default world distance of 10 m makes small props almost black inside)
    try:
        bpy.context.scene.world.light_settings.distance = spec.get("ao_dist", 0.2) * max(ob.dimensions)
    except Exception as e:
        log("ao distance not set", e)
    img = new_image("ao", size, True)
    if not ob.material_slots:
        ob.data.materials.append(bpy.data.materials.new("tmp"))
    tg = add_bake_target(ob, img)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.context.scene.render.bake.use_selected_to_active = False
    bpy.ops.object.bake(type="AO", margin=8)
    save(img, path)
    remove_bake_target(tg)


def emit_route(mat, input_name):
    """Route a Principled input (its linked source or value) into an Emission shader for EMIT baking."""
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
    if bsdf is None or out is None:
        return None
    em = nt.nodes.new("ShaderNodeEmission")
    inp = bsdf.inputs[input_name]
    if inp.is_linked:
        nt.links.new(inp.links[0].from_socket, em.inputs["Color"])
    else:
        v = inp.default_value
        v = v if isinstance(v, float) else v[0]
        em.inputs["Color"].default_value = (v, v, v, 1)
    old = out.inputs["Surface"].links[0].from_socket if out.inputs["Surface"].is_linked else None
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return (nt, em, out, old)


def emit_restore(r):
    if r is None:
        return
    nt, em, out, old = r
    if old is not None:
        nt.links.new(old, out.inputs["Surface"])
    nt.nodes.remove(em)


def rebake(high, low, size):
    """Fresh UVs on low, then bake base colour, normal, roughness, metallic from high (selected to active)."""
    setup_cycles(16)
    bpy.ops.object.select_all(action="DESELECT")
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    # fresh UV atlas
    while low.data.uv_layers:
        low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(spec.get("uv_angle", 66)), island_margin=0.003, area_weight=0.0, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    # low gets its own simple material so bake targets don't touch high
    low.data.materials.clear()
    lm = bpy.data.materials.new("bake_low")
    lm.use_nodes = True
    low.data.materials.append(lm)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    d = max(low.dimensions)
    bk = bpy.context.scene.render.bake
    bk.use_selected_to_active = True
    bk.cage_extrusion = spec.get("cage", 0.05) * d
    bk.max_ray_distance = spec.get("ray", 0.2) * d
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    outs = {}
    src_mean = (0.4, 0.4, 0.4)
    try:
        import glob as _g
        sb = sorted(_g.glob(os.path.join(work, "src_basecolor.*")))
        if sb:
            im = bpy.data.images.load(sb[0]); px = np.empty(im.size[0] * im.size[1] * 4, np.float32); im.pixels.foreach_get(px)
            src_mean = tuple(float(x) for x in px.reshape(-1, 4)[:, :3].mean(0))
    except Exception as e:
        log("src mean failed", e)
    for kind, btype, noncolor in (("basecolor", "DIFFUSE", False), ("normal", "NORMAL", True),
                                  ("roughness", "ROUGHNESS", True), ("metallic", "EMIT", True)):
        ss = 2 if size <= 1024 else 1          # supersample, then downscale (cleaner edges, fewer missed texels)
        img = new_image(kind, size * ss, noncolor)
        fill = {"basecolor": src_mean, "normal": (0.5, 0.5, 1.0), "roughness": (0.75,) * 3, "metallic": (0.0,) * 3}[kind]
        img.generated_color = (fill[0], fill[1], fill[2], 1.0)
        tg = add_bake_target(low, img)
        routes = []
        if kind == "metallic":
            routes = [emit_route(s.material, "Metallic") for s in high.material_slots if s.material]
        kw = {}
        if btype == "DIFFUSE":
            kw = dict(pass_filter={"COLOR"})
        bpy.ops.object.bake(type=btype, margin=8, use_selected_to_active=True,
                            cage_extrusion=bk.cage_extrusion, max_ray_distance=bk.max_ray_distance, **kw)
        for r in routes:
            emit_restore(r)
        if ss > 1:
            img.scale(size, size)
        p = os.path.join(work, f"bake_{kind}.png")
        save(img, p)
        outs[kind] = p
        remove_bake_target(tg)
    bk.use_selected_to_active = False
    return outs


def glass_faces(ob, g):
    """Faces inside a box whose base colour is pale and neutral -> window glass. Writes their UV triangles."""
    import glob as _g
    srcs = sorted(_g.glob(os.path.join(work, "src_basecolor.*")))
    if not srcs:
        return
    img = bpy.data.images.load(srcs[0])
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    me = ob.data
    me.calc_loop_triangles()
    uvl = me.uv_layers.active.data
    (x0, x1), (y0, y1), (z0, z1) = g["box"]
    tris = []
    for lt in me.loop_triangles:
        c = sum((me.vertices[v].co for v in lt.vertices), Vector()) / 3
        if not (x0 <= c.x <= x1 and y0 <= c.y <= y1 and z0 <= c.z <= z1):
            continue
        if abs(lt.normal.z) > g.get("max_nz", 1.0):
            continue
        uvs = [uvl[l].uv for l in lt.loops]
        u = sum(q.x for q in uvs) / 3; v = sum(q.y for q in uvs) / 3
        col = px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w))), :3]
        mx, mn = col.max(), col.min()
        sat = (mx - mn) / max(mx, 1e-6)
        if col.mean() >= g.get("lum_min", 0.45) and sat <= g.get("sat_max", 0.2):
            tris.append([[q.x, q.y] for q in uvs])
    json.dump(tris, open(os.path.join(work, "glass_tris.json"), "w"))
    stats["glass_tris"] = len(tris)


def export_fbx(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
                             path_mode="STRIP", embed_textures=False, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)


def main():
    reset()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    src = import_glb(spec["glb"])
    stats["src_tris"] = tri_count(src)
    cleanup(src)
    align(src)
    scale_and_pivot(src)
    if spec.get("stretch"):
        stretch(src, spec["stretch"])
    if spec.get("post_translate"):   # WP09: e.g. drop a box onto its own base when a cable hangs lower than the box
        src.data.transform(Matrix.Translation(Vector(spec["post_translate"]))); src.data.update()
        co = verts_world(src)
        stats["dims_m"] = [round(float(v), 4) for v in (co.max(0) - co.min(0))]
        stats["bbox_min"] = [round(float(v), 4) for v in co.min(0)]; stats["bbox_max"] = [round(float(v), 4) for v in co.max(0)]
    budget = int(spec["tris"])
    t0 = tri_count(src)
    lods = spec.get("lods", [1.0])
    base = "SM_" + spec["name"]
    names = [base] if len(lods) == 1 else [f"{base}_LOD{i}" for i in range(len(lods))]
    ratio0 = min(1.0, budget / t0)
    if ratio0 > 0.97:
        ratio0 = 1.0
    will_rebake = spec.get("rebake") or ratio0 < 0.5
    if will_rebake:
        lowsrc = src.copy(); lowsrc.data = src.data.copy(); bpy.context.collection.objects.link(lowsrc)
        weld(lowsrc)
        if spec.get("remesh"):      # voxel remesh first: collapse on some generator meshes degenerates at tiny ratios
            m = lowsrc.modifiers.new("rm", "REMESH"); m.mode = "VOXEL"; m.voxel_size = float(spec["remesh"]) * max(lowsrc.dimensions)
            bpy.context.view_layer.objects.active = lowsrc; bpy.ops.object.select_all(action="DESELECT"); lowsrc.select_set(True)
            bpy.ops.object.modifier_apply(modifier="rm")
        lod0 = decimate_copy(lowsrc, budget / max(tri_count(lowsrc), 1), names[0])
        bpy.data.objects.remove(lowsrc, do_unlink=True)
    else:
        lod0 = decimate_copy(src, ratio0, names[0])
    stats["lod0_ratio"] = round(ratio0, 4)
    if spec.get("high_rough") is not None or spec.get("high_metal") is not None:
        for sl in src.material_slots:
            b = next((n for n in sl.material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if sl.material else None
            if b is None:
                continue
            for key, inp in (("high_rough", "Roughness"), ("high_metal", "Metallic")):
                if spec.get(key) is not None:
                    for l in list(b.inputs[inp].links):
                        sl.material.node_tree.links.remove(l)
                    b.inputs[inp].default_value = float(spec[key])
    if will_rebake:
        stats["rebaked"] = True
        stats["rebake_maps"] = rebake(src, lod0, int(spec.get("tex_src", 1024)))
    # the source mesh must leave the scene before AO: coincident surfaces would occlude LOD0
    bpy.data.objects.remove(src, do_unlink=True)
    if spec.get("ao", True):
        bake_ao(lod0, int(spec.get("tex_src", 1024)), os.path.join(work, "ao.png"))
    if spec.get("glass"):
        glass_faces(lod0, spec["glass"])
    objs = [lod0]
    for i, r in enumerate(lods[1:], start=1):
        objs.append(decimate_copy(lod0, r, names[i]))
    if spec.get("smooth_angle"):   # WP09: smooth shading with sharp edges above the angle (decimated scans export faceted otherwise)
        for o in objs:
            # weld the glTF seam duplicates (UVs stay per-loop, so texture seams survive) and fix flipped faces,
            # otherwise every UV seam is a shading crease
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=max(o.dimensions) * 2e-5)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(o.data); bm.free()
            o.data.shade_smooth()
            o.data.set_sharp_from_angle(angle=math.radians(float(spec["smooth_angle"])))
        stats["smooth_angle"] = spec["smooth_angle"]
    # one clean material slot named after the asset (Unity: Import Materials = None anyway)
    mat = bpy.data.materials.new("M_" + spec["name"])
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
        o.location = (0, 0, 0)
    if spec.get("extras_script"):
        # WP09: per-asset Blender snippet that adds/splits child parts; it may append to objs and fill stats["parts"]
        g = {"bpy": bpy, "bmesh": bmesh, "np": np, "math": math, "Vector": Vector, "Matrix": Matrix, "objs": objs,
             "lod0": objs[0], "spec": spec, "stats": stats, "tri_count": tri_count, "log": log}
        exec(compile(open(spec["extras_script"]).read(), spec["extras_script"], "exec"), g)
    export_fbx(objs, spec["fbx"])
    stats["lod_tris"] = {o.name: tri_count(o) for o in objs}
    stats["fbx"] = spec["fbx"]
    json.dump(stats, open(os.path.join(work, "stats.json"), "w"), indent=1)
    log(json.dumps(stats))


main()
