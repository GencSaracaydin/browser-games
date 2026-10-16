#!/usr/bin/env python3
"""GLB -> import-ready FBX + Unity textures + review sheet, for one WP10_FAL asset.

  Tools/.venv/bin/python Tools/mesh/props/process_prop.py FAL_SANDBAG --tag tripo_t1
        [--variant closed --vname FilmCanClosed] [--yaw 90] [--pivot back] [--rebake] [--no-render] [--lods 1,0.5,0.25]
Reads the spec from wp10fal_assets.json (name, kind, folder, size, tris, tex, tex_src, emissive) and
RawAssets/wp10/fal/<ID>/<tag>.glb. Writes:
  RawAssets/wp10/ready/Assets/_Game/Art/Models/<Kind>/<folder>/SM_<Name>.fbx
  RawAssets/wp10/ready/Assets/_Game/Art/Textures/<Kind>/<folder>/T_<Name>_{BC,N,M[,E]}.png
  RawAssets/wp10/fal/<ID>/work_<tag>/ (extracted maps, AO, stats.json, renders)
  RawAssets/wp10/fal/_review/<ID>__<tag>.png (contact sheet: 6 views, wireframe, maps, stats)
  RawAssets/wp10/fal/<ID>/ready_<Name>.json (index rows for import_index_fal.json)
Per-asset overrides (yaw, pivot, lods, rebake) can also live in the config under "proc".
"""
import argparse, json, pathlib, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
RAW = ROOT / "RawAssets/wp10/fal"
READY = ROOT / "RawAssets/wp10/ready"
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
PY = sys.executable


def run(cmd, **kw):
    r = subprocess.run(cmd, capture_output=True, text=True, **kw)
    if r.returncode != 0:
        print(r.stdout[-3000:], r.stderr[-3000:])
        raise SystemExit(f"failed: {' '.join(map(str, cmd[:6]))}")
    return r.stdout


def default_lods(tris, kind):
    if kind == "Vehicles":
        return [1.0, 0.5, 0.25, 0.12]
    if tris >= 1500:
        return [1.0, 0.5, 0.25]
    if tris >= 800:
        return [1.0, 0.5]
    return [1.0]


def sheet(paths, labels, out, info_lines, cell=420):
    cols = 4
    rows = (len(paths) + cols - 1) // cols
    txt_h = 18 * (len(info_lines) + 1)
    im = Image.new("RGB", (cols * cell, rows * (cell + 20) + txt_h), (30, 30, 30))
    d = ImageDraw.Draw(im)
    try:
        f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 14)
    except Exception:
        f = ImageFont.load_default()
    for i, (p, lab) in enumerate(zip(paths, labels)):
        x, y = (i % cols) * cell, (i // cols) * (cell + 20)
        try:
            t = Image.open(p)
            if t.mode == "RGBA" and "_M" in lab:
                t = t.convert("RGB")
            elif t.mode == "RGBA":
                bg = Image.new("RGB", t.size, (128, 128, 128)); bg.paste(t, (0, 0), t); t = bg
            t = t.convert("RGB"); t.thumbnail((cell, cell))
            im.paste(t, (x + (cell - t.width) // 2, y + 20))
        except Exception as e:  # noqa
            d.text((x + 5, y + 40), f"missing {e}", fill=(255, 80, 80), font=f)
        d.text((x + 4, y + 2), lab, fill=(255, 255, 255), font=f)
    y0 = rows * (cell + 20) + 4
    for j, line in enumerate(info_lines):
        d.text((6, y0 + 18 * j), line, fill=(220, 220, 160), font=f)
    im.save(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id"); ap.add_argument("--tag", required=True)
    ap.add_argument("--glb"); ap.add_argument("--variant"); ap.add_argument("--vname")
    ap.add_argument("--yaw", type=float); ap.add_argument("--pivot"); ap.add_argument("--rebake", action="store_true")
    ap.add_argument("--no-align", action="store_true"); ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--lods"); ap.add_argument("--tris", type=int); ap.add_argument("--size", nargs=2)
    ap.add_argument("--no-ao", action="store_true")
    ap.add_argument("--proc", help="key in wp10fal_proc.json[id] (default: variant or main)")
    ap.add_argument("--scratch", action="store_true", help="write FBX/textures under the work folder, not ready/")
    a = ap.parse_args()
    C = json.loads((HERE / "wp10fal_assets.json").read_text())[a.id]
    procf = HERE / "wp10fal_proc.json"   # per-asset review overrides: {id: {main|variant: {yaw, pivot, lods, color, ...}}}
    P = json.loads(procf.read_text()) if procf.exists() else {}
    proc = dict(P.get(a.id, {}).get("main", {})) if a.proc else {}
    proc.update(P.get(a.id, {}).get(a.proc or a.variant or "main", {}))
    name = a.vname or C["name"]
    kind, folder = C["kind"], C["folder"]
    tris = a.tris or proc.get("tris") or C["tris"]
    glb = pathlib.Path(a.glb) if a.glb else RAW / a.id / f"{a.tag}.glb"
    work = RAW / a.id / f"work_{a.tag}"
    work.mkdir(parents=True, exist_ok=True)
    is_obj = False
    if not glb.exists() and list((RAW / a.id / a.tag).glob("*.obj")):
        # Hunyuan Rapid: OBJ + base colour only -> tidy copy with a matching MTL, then a full rebake
        src_dir = RAW / a.id / a.tag
        od = RAW / a.id / f"{a.tag}_obj"; od.mkdir(exist_ok=True)
        import shutil
        shutil.copy(next(src_dir.glob("*.obj")), od / "model.obj")
        tex = next(p for p in src_dir.glob("*.png") if "preview" not in p.name)
        shutil.copy(tex, od / "basecolor.png")
        (od / "material.mtl").write_text("newmtl Material\nKd 0.8 0.8 0.8\nNs 50\nillum 2\nmap_Kd basecolor.png\n")
        objtxt = (od / "model.obj").read_text()
        if "usemtl" not in objtxt:
            lines = objtxt.splitlines()
            idx = next(i for i, l in enumerate(lines) if l.startswith("f "))
            lines.insert(idx, "usemtl Material")
            (od / "model.obj").write_text("\n".join(lines) + "\n")
        glb = od / "model.obj"; is_obj = True
    else:
        run([PY, str(HERE / "glb_extract.py"), str(glb), str(work)])
    global READY
    if a.scratch:
        READY = work / "scratch_ready"
    fbx = READY / f"Assets/_Game/Art/Models/{kind}/{folder}/SM_{name}.fbx"
    tdir = READY / f"Assets/_Game/Art/Textures/{kind}/{folder}"
    lods = [float(x) for x in a.lods.split(",")] if a.lods else proc.get("lods") or default_lods(tris, kind)
    size = [a.size[0], float(a.size[1])] if a.size else proc.get("size") or C["size"]
    spec = {"glb": str(glb), "work": str(work), "fbx": str(fbx), "name": name, "size": size,
            "yaw": a.yaw if a.yaw is not None else proc.get("yaw", 0), "auto_align": not (a.no_align or proc.get("no_align")),
            "pivot": a.pivot or proc.get("pivot", "bottom"), "tris": tris, "lods": lods,
            "tex_src": C["tex_src"], "rebake": a.rebake or proc.get("rebake", False) or is_obj, "ao": not a.no_ao,
            "ao_samples": 64, "tilt": proc.get("tilt"), "glass": proc.get("glass"),
            "high_rough": proc.get("rough", 0.75 if is_obj else None), "high_metal": proc.get("metal", 0.0 if is_obj else None)}
    for k in ("cage", "ray", "uv_angle", "ao_dist", "remesh"):
        if k in proc:
            spec[k] = proc[k]
    (work / "job.json").write_text(json.dumps(spec, indent=1))
    for stale in list(work.glob("bake_*.png")) + list(work.glob("glass_tris.json")):
        stale.unlink()
    out = run([BLENDER, "-b", "--factory-startup", "-t", "4", "-P", str(HERE / "blender_prop.py"), "--", "--spec", str(work / "job.json")])
    stats = json.loads((work / "stats.json").read_text())
    em = C.get("emissive") if not a.variant else proc.get("emissive", C.get("emissive"))
    tp = [PY, str(HERE / "tex_pack.py"), "--work", str(work), "--name", name, "--out", str(tdir), "--size", str(C["tex_src"])]
    if em:
        tp += ["--emissive", em]
    if C.get("tex_bc"):
        tp += ["--bc-size", str(C["tex_bc"])]
    col = proc.get("color", {})
    if col.get("match_ref"):
        tp += ["--match-ref", str(RAW / a.id / col["match_ref"]), "--match-strength", str(col.get("strength", 0.7))]
    if col.get("gain"):
        tp += ["--gain"] + [str(x) for x in col["gain"]]
    if col.get("sat"):
        tp += ["--sat", str(col["sat"])]
    tinfo = json.loads(run(tp).strip().splitlines()[-1])
    T = {k: tdir / f"T_{name}_{k}.png" for k in ("BC", "N", "M", "E")}
    rel = lambda p: str(p.relative_to(READY))
    rows = []
    lod_txt = ", ".join(f"{k}: {v}" for k, v in stats["lod_tris"].items())
    dims = stats["dims_m"]
    has_lods = len(lods) > 1
    rows.append({"manifest_id": a.id, "file": rel(fbx), "target_path": rel(fbx), "type": "model",
                 "import_notes": (f"Static mesh FBX (Blender 5.2, FBX_SCALE_ALL, forward -Z / up Y, baked space transform): "
                                  f"Scale Factor 1, Convert Units on -> 1 unit = 1 m, identity rotation; front faces +Z, pivot "
                                  f"{spec['pivot']} centre. Size {dims[0]} x {dims[2]} x {dims[1]} m (X x Y x Z in Unity). "
                                  f"Tris {lod_txt}. " + ("Contains _LOD0.._LODn meshes: Unity builds the LODGroup; turn the importer's "
                                  "Mesh LOD generation off for this file; suggested screen-relative heights " +
                                  ("0.5/0.25/0.1/0.02 (cull)" if len(lods) == 4 else "0.4/0.15/0.03 (cull)" if len(lods) == 3 else "0.3/0.05 (cull)") + ". "
                                  if has_lods else "Single LOD; importer Mesh LOD generation optional. ") +
                                  f"Animation None, Import Materials None (MaterialFactory builds M_{name}: URP/Lit, Base Map T_{name}_BC, "
                                  f"Metallic Map + Occlusion Map T_{name}_M, smoothness source Metallic Alpha, Normal Map T_{name}_N"
                                  + (f", Emission T_{name}_E (HDR intensity ~3-6)" if T['E'].exists() and em else "") + "). Normals Import, tangents Calculate Mikk. "
                                  "Collider: box from the size above, or convex MeshCollider on the lowest LOD.")})
    tex_size = C["tex"]
    rows.append({"manifest_id": a.id, "file": rel(T["BC"]), "target_path": rel(T["BC"]), "type": "texture",
                 "import_notes": f"Base colour, sRGB on, Max Size {C.get('tex_bc', tex_size)} (file {C.get('tex_bc', C['tex_src'])} px), DXT1 Crunched (TextureImportRules {kind}/**), mips on."})
    rows.append({"manifest_id": a.id, "file": rel(T["N"]), "target_path": rel(T["N"]), "type": "texture",
                 "import_notes": f"Normal map (Texture Type Normal map, OpenGL +Y as exported), sRGB off, Max Size {min(tex_size, C['tex_src'])}."})
    rows.append({"manifest_id": a.id, "file": rel(T["M"]), "target_path": rel(T["M"]), "type": "texture",
                 "import_notes": f"Mask: R metallic, G ambient occlusion (baked), B unused, A smoothness. sRGB OFF, Max Size {min(tex_size, C['tex_src'])}, DXT5 Crunched. Use as URP Metallic Map (smoothness from alpha) and as Occlusion Map."})
    if em and T["E"].exists():
        rows.append({"manifest_id": a.id, "file": rel(T["E"]), "target_path": rel(T["E"]), "type": "texture",
                     "import_notes": "Emission colour, sRGB on, Max Size 512; glowing parts only (black elsewhere). Material Emission on, HDR colour white x intensity."})
    (RAW / a.id / (f"scratch_{a.tag}_{name}.json" if a.scratch else f"ready_{name}.json")).write_text(json.dumps(rows, indent=1))
    if a.no_render:
        print(json.dumps(stats)); return
    rnd = work / "render"
    rc = [BLENDER, "-b", "--factory-startup", "-t", "4", "-P", str(HERE / "blender_render.py"), "--", "--fbx", str(fbx),
          "--bc", str(T["BC"]), "--n", str(T["N"]), "--m", str(T["M"]), "--out", str(rnd), "--res", "560"]
    if T["E"].exists() and em:
        rc += ["--e", str(T["E"])]
    if max(dims) > 0.8:
        rc += ["--human"]
    run(rc)
    views = [f"{rnd}_v{i}.png" for i in range(7)]
    labels = ["front-left 3/4", "back-right 3/4", "side (-X)", "high 3/4", "front (-Y = Unity +Z)", "close-up", "wireframe LOD0"]
    views.append(str(T["BC"])); labels.append(f"T_{name}_BC")
    paths = views + [str(T["N"]), str(T["M"])]
    labels += [f"T_{name}_N", f"T_{name}_M (RGB)"]
    ref = RAW / a.id / "ref_t1.png"
    if ref.exists():
        paths.append(str(ref)); labels.append("reference")
    info = [f"{a.id} [{a.tag}] SM_{name}  dims(m) X{dims[0]} Y{dims[1]} Z{dims[2]}  {lod_txt}",
            f"src tris {stats['src_tris']}  lod0 ratio {stats['lod0_ratio']}  rebaked {stats.get('rebaked', False)}  yaw {spec['yaw']} fine {stats.get('auto_align_fine_deg')}  tex {tinfo.get('stats')}"]
    rv = RAW / "_review"; rv.mkdir(exist_ok=True)
    outp = rv / f"{a.id}__{a.tag}{('_' + a.variant) if a.variant else ''}.png"
    sheet(paths, labels, outp, info)
    print("SHEET", outp)
    print(json.dumps(stats))


if __name__ == "__main__":
    main()
