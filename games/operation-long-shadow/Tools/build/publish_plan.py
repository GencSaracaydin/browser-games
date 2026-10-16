#!/usr/bin/env python3
"""Turn a Unity web build into a publish mirror and an artifact publish plan (TECH_PLAN §9.2, D22; WP01).

The claude.ai artifact host serves only a fixed list of extensions (Tools/build/build_limits.json "served_extensions";
.unityweb, .bundle, .hash, .data and extensionless files are refused, so is application/octet-stream), allows
<= 15 MB per binary file / 16 MB per text file, <= 255 files and 64 MB per publish call and <= 511 files / 256 MB per
version, and keeps every file a later publish leaves out. This tool:

1. copies Builds/Web into a publish mirror (default Builds/Web_publish), appending the publish suffix (".wasm") to every
   file whose extension the host does not serve: Build/<hash>.data.unityweb -> Build/<hash>.data.unityweb.wasm,
   StreamingAssets/aa/WebGL/x.bundle -> x.bundle.wasm (x.bundle.wasm still matches the loader's /\\.bundle/ IndexedDB
   cache rule), catalog.hash -> catalog.hash.wasm. Foreign files, *.manifest and AddressablesLink/ are left out;
2. patches the mirror's index.html (every reference to a renamed Build/ file; the CovertOps markers
   /*LS:ASSET_SUFFIX*/ and /*LS:SERVED_EXT*/ so the runtime InternalIdTransformFunc maps bundle URLs too) and the
   Addressables JSON files under StreamingAssets/aa (catalog internal ids that end in a renamed file name);
3. diffs the mirror against Builds/Web_published_files.json (the last approved publish): unchanged files are skipped
   (the host keeps them), vanished paths become null removals in the first batch (D22);
4. packs the rest into batches of <= 60 MB and <= 250 entries (index.html, the page, is sent with every batch) and
   writes Builds/Web_publish_plan.json. Each batch is ready for the Artifact tool:
   publish(file_path=batch.file_path, files=batch.files[, url=<artifact url from batch 1>]).
After an approved publish, record it:  python3 Tools/build/publish_plan.py --mark-published
Nothing here publishes anything (ORCHESTRATION O5: the orchestrator publishes).

Usage (from the project root):
    python3 Tools/build/publish_plan.py                       # Builds/Web -> Builds/Web_publish + Builds/Web_publish_plan.json
    python3 Tools/build/publish_plan.py --build Builds/ArtifactSrc --out /tmp/mirror --plan /tmp/plan.json
    python3 Tools/build/publish_plan.py --full                # re-send every file (ignore the published list)
    python3 Tools/build/publish_plan.py --mark-published      # after the publish succeeded
    python3 Tools/build/publish_plan.py --selftest
Exit codes: 0 ok, 1 host/plan limits violated, 2 usage/configuration problem.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Dict, List, Optional, Tuple

sys.path.insert(0, str(Path(__file__).resolve().parent))
import web_limits as wl  # noqa: E402

BUILDS = wl.PROJECT / "Builds"
DEFAULT_BUILD = BUILDS / "Web"
DEFAULT_OUT = BUILDS / "Web_publish"
DEFAULT_PLAN = BUILDS / "Web_publish_plan.json"
DEFAULT_PUBLISHED = BUILDS / "Web_published_files.json"
AA_PREFIX = "StreamingAssets/aa/"
SUFFIX_MARKER = re.compile(r'/\*LS:ASSET_SUFFIX\*/"[^"\n]*"')
SERVED_MARKER = re.compile(r'/\*LS:SERVED_EXT\*/"[^"\n]*"')


class PlanError(Exception):
    """A configuration problem (exit code 2)."""


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def published_name(rel: str, suffix: str, served: set) -> str:
    """Mirror of PublishedNames.Map for project-relative paths (idempotent)."""
    if not suffix or wl.ext_of(rel) in served or rel.lower().endswith(suffix.lower()):
        return rel
    return rel + suffix


def replace_names(text: str, renames: Dict[str, str]) -> Tuple[str, int]:
    """Replaces every quoted reference that ends with a renamed basename ('Build/x.data.unityweb', "/x.bundle")."""
    count = 0
    for old, new in sorted(renames.items(), key=lambda kv: -len(kv[0])):
        pattern = re.compile(r'(?<=[/"\'#])' + re.escape(old) + r'(?=["\'?#])')
        text, n = pattern.subn(new, text)
        count += n
    return text, count


def safe_out_dir(out: Path, build: Path) -> None:
    out_r, build_r = out.resolve(), build.resolve()
    if out_r == build_r or build_r.is_relative_to(out_r) or out_r.is_relative_to(build_r):
        raise PlanError(f"--out {out} must not overlap the build folder {build}")
    if len(out_r.parts) < 3:
        raise PlanError(f"refusing to use {out} as the mirror (too close to the filesystem root)")


def make_mirror(build: Path, out: Path, limits: Dict[str, object], suffix: str, patch_catalog: bool,
                report: List[str]) -> Tuple[List[Dict[str, object]], Dict[str, str], List[str]]:
    """Copies the build into the mirror with served names; returns (files, renames, problems)."""
    served = wl.served_set(limits)
    problems: List[str] = []
    if not (build / "index.html").exists():
        raise PlanError(f"{build} has no index.html (not a web build)")
    safe_out_dir(out, build)
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)

    renames: Dict[str, str] = {}  # rel -> published rel
    kept: List[Tuple[str, Path]] = []
    for rel, path, _size in wl.list_files(build):
        name = rel.rsplit("/", 1)[-1]
        if wl.matches_any(name, limits["strip_patterns"]):
            report.append(f"left out {rel} (build by-product)")
            continue
        if not wl.allowed(rel, limits) or rel.startswith(wl.EXCLUDED_PREFIXES):
            report.append(f"left out {rel} (not part of the web build)")
            continue
        new = published_name(rel, suffix, served)
        if new != rel:
            renames[rel] = new
        kept.append((rel, path))
    targets = {renames.get(r, r) for r, _ in kept}
    if len(targets) != len(kept):
        raise PlanError("renaming would make two files collide; change publish_suffix")

    base_renames = {r.rsplit("/", 1)[-1]: n.rsplit("/", 1)[-1] for r, n in renames.items()}
    build_renames = {r.rsplit("/", 1)[-1]: n.rsplit("/", 1)[-1] for r, n in renames.items() if r.startswith("Build/")}
    aa_renames = {r.rsplit("/", 1)[-1]: n.rsplit("/", 1)[-1] for r, n in renames.items() if r.startswith(AA_PREFIX)}
    if len(base_renames) != len(renames):
        problems.append("two renamed files share a basename; index/catalog patching may be ambiguous")

    files: List[Dict[str, object]] = []
    for rel, src in kept:
        new = renames.get(rel, rel)
        dst = out / new
        dst.parent.mkdir(parents=True, exist_ok=True)
        if rel == "index.html":
            html = src.read_text(encoding="utf-8")
            html, n = replace_names(html, build_renames)
            if build_renames and n == 0:
                problems.append("index.html: no reference to the renamed Build/ files was found (template changed?)")
            report.append(f"index.html: {n} Build/ reference(s) renamed")
            if renames:
                served_list = ",".join(sorted(served))
                html, a = SUFFIX_MARKER.subn(f'/*LS:ASSET_SUFFIX*/"{suffix}"', html)
                html, b = SERVED_MARKER.subn(f'/*LS:SERVED_EXT*/"{served_list}"', html)
                if a == 0 and any(r.startswith(AA_PREFIX) for r in renames):
                    report.append("WARNING index.html has no /*LS:ASSET_SUFFIX*/ marker: the runtime bundle remap stays off "
                                  "(the patched JSON catalog still points at the renamed bundles)")
                elif a:
                    report.append(f"index.html: LS_ASSET_SUFFIX = {suffix!r}, LS_SERVED_EXT set ({b} marker)")
            if wl.contains_alert_call(html):
                problems.append("index.html calls alert() (the CovertOps template never does)")
            dst.write_text(html, encoding="utf-8")
        elif patch_catalog and rel.startswith(AA_PREFIX) and wl.ext_of(rel) == "json" and aa_renames:
            text = src.read_text(encoding="utf-8")
            text, n = replace_names(text, aa_renames)
            if n:
                try:
                    json.loads(text)
                except ValueError as e:
                    raise PlanError(f"patching {rel} broke its JSON: {e}")
                report.append(f"{rel}: {n} internal id(s) renamed")
            dst.write_text(text, encoding="utf-8")
        else:
            shutil.copy2(src, dst)
        size = dst.stat().st_size
        files.append({
            "path": new,
            "source_path": rel,
            "local": str(dst.resolve()),
            "bytes": size,
            "sha256": sha256(dst),
            "content_type": wl.content_type(new, limits),
            "kind": "text" if wl.is_text(new, limits) else "binary",
        })
    if any(r.startswith(AA_PREFIX) and r.endswith(".bin") for r in renames):
        report.append("WARNING the Addressables catalog is binary (catalog.bin): only the runtime remap can map bundle names; "
                      "enable the JSON catalog (LongShadow/Platform/Setup Addressables)")
    return files, renames, problems


def check_host_limits(files: List[Dict[str, object]], limits: Dict[str, object]) -> List[str]:
    served = wl.served_set(limits)
    problems = []
    total = 0
    for f in files:
        rel, size = str(f["path"]), int(f["bytes"])
        total += size
        if wl.ext_of(rel) not in served:
            problems.append(f"{rel}: extension not served by the host")
        cap = int(limits["host_text_max_bytes"] if f["kind"] == "text" else limits["host_binary_max_bytes"])
        if size > cap:
            problems.append(f"{rel}: {wl.mb(size)} MB is over the host's {wl.mb(cap)} MB per-file limit")
    if len(files) > int(limits["host_version_max_files"]) or len(files) > int(limits["max_files"]):
        problems.append(f"{len(files)} files is over the per-version limit ({limits['max_files']} planned, "
                        f"{limits['host_version_max_files']} host)")
    if total > int(limits["max_total_bytes"]):
        problems.append(f"version total {wl.mb(total)} MB is over the {wl.mb(int(limits['max_total_bytes']))} MB cap "
                        f"(host {wl.mb(int(limits['host_version_max_bytes']))} MB)")
    return problems


def load_published(path: Path) -> Dict[str, Dict[str, object]]:
    if not path.exists():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    files = data.get("files", {})
    if isinstance(files, list):  # tolerate a bare list of paths
        return {str(p): {} for p in files}
    return {str(k): dict(v or {}) for k, v in files.items()}


def make_batches(page: Dict[str, object], uploads: List[Dict[str, object]], removals: List[str],
                 limits: Dict[str, object]) -> List[Dict[str, object]]:
    """First-fit decreasing into batches; the page goes with every batch, removals with the first."""
    max_bytes = int(limits["publish_batch_max_bytes"])
    max_files = int(limits["publish_batch_max_files"])
    page_bytes = int(page["bytes"])
    if page_bytes >= max_bytes:
        raise PlanError("index.html alone is over the batch size")
    bins: List[Dict[str, object]] = []

    def new_bin(first: bool) -> Dict[str, object]:
        b = {"bytes": page_bytes, "entries": 1 + (len(removals) if first else 0), "items": []}
        bins.append(b)
        return b

    new_bin(True)
    if 1 + len(removals) > max_files:
        raise PlanError(f"{len(removals)} removals do not fit one batch (max {max_files} entries)")
    for f in sorted(uploads, key=lambda x: (-int(x["bytes"]), str(x["path"]))):
        size = int(f["bytes"])
        if page_bytes + size > max_bytes:
            raise PlanError(f"{f['path']} ({wl.mb(size)} MB) does not fit a {wl.mb(max_bytes)} MB batch")
        for b in bins:
            if int(b["bytes"]) + size <= max_bytes and int(b["entries"]) + 1 <= max_files:
                break
        else:
            b = new_bin(False)
        b["items"].append(f)
        b["bytes"] = int(b["bytes"]) + size
        b["entries"] = int(b["entries"]) + 1

    batches = []
    for i, b in enumerate(bins):
        files_map: Dict[str, Optional[str]] = {}
        if i == 0:
            for r in sorted(removals):
                files_map[r] = None
        for f in sorted(b["items"], key=lambda x: str(x["path"])):
            files_map[str(f["path"])] = str(f["local"])
        batches.append({
            "index": i + 1,
            "file_path": str(page["local"]),
            "files": files_map,
            "upload_bytes": int(b["bytes"]),
            "entries": int(b["entries"]),
            "uploads": len(b["items"]),
            "removals": len(removals) if i == 0 else 0,
        })
    if len(batches) > 1 and not batches[-1]["files"]:
        batches.pop()
    return batches


def plan(build: Path, out: Path, plan_path: Path, published_path: Path, limits: Dict[str, object], suffix: str,
         full: bool, patch_catalog: bool, quiet: bool = False) -> int:
    report: List[str] = []
    files, renames, problems = make_mirror(build, out, limits, suffix, patch_catalog, report)
    problems += check_host_limits(files, limits)

    page = next(f for f in files if f["path"] == "index.html")
    previous = load_published(published_path)
    current = {str(f["path"]): f for f in files}
    removals = sorted(p for p in previous if p not in current and p != "index.html")
    uploads, unchanged = [], []
    for f in files:
        if f["path"] == "index.html":
            continue
        prev = previous.get(str(f["path"]))
        if not full and prev and prev.get("sha256") == f["sha256"] and int(prev.get("bytes", -1)) == int(f["bytes"]):
            unchanged.append(f["path"])
        else:
            uploads.append(f)
    try:
        batches = make_batches(page, uploads, removals, limits)
    except PlanError as e:
        problems.append(str(e))
        batches = []
    for b in batches:
        if b["upload_bytes"] > int(limits["host_call_max_bytes"]) or b["entries"] > int(limits["host_call_max_files"]):
            problems.append(f"batch {b['index']} exceeds a host publish call ({wl.mb(b['upload_bytes'])} MB, {b['entries']} entries)")

    total = sum(int(f["bytes"]) for f in files)
    result = {
        "generated": _dt.datetime.now().isoformat(timespec="seconds"),
        "build_dir": str(build.resolve()),
        "publish_dir": str(out.resolve()),
        "published_list": str(published_path.resolve()),
        "suffix": suffix,
        "page": "index.html",
        "ok": not problems,
        "problems": problems,
        "version_files": len(files),
        "version_bytes": total,
        "renamed": renames,
        "removals": removals,
        "unchanged": sorted(unchanged),
        "files": files,
        "batches": batches,
        "how_to_publish": [
            "Only with the user's explicit approval (TECH_PLAN §9.2, ORCHESTRATION O5).",
            "Batch 1: Artifact publish with file_path = batches[0].file_path and files = batches[0].files "
            "(null entries remove stale paths, D22). Keep the returned artifact url.",
            "Batches 2..n: the same call with url = that artifact url.",
            "Then: python3 Tools/build/publish_plan.py --mark-published",
        ],
        "log": report,
    }
    plan_path.parent.mkdir(parents=True, exist_ok=True)
    plan_path.write_text(json.dumps(result, indent=2), encoding="utf-8")

    if not quiet:
        print(f"publish_plan: {len(files)} files, {wl.mb(total)} MB in {out}")
        for line in report:
            print("  " + line)
        print(f"  renamed {len(renames)}, unchanged {len(unchanged)}, removals {len(removals)}, uploads {len(uploads)}")
        for b in batches:
            print(f"  batch {b['index']}: {b['uploads']} upload(s), {b['removals']} removal(s), "
                  f"{wl.mb(b['upload_bytes'])} MB incl. index.html, {b['entries']} entries")
        for p in problems:
            print("  PROBLEM " + p)
        print(f"  plan -> {plan_path}")
    return 0 if not problems else 1


def mark_published(plan_path: Path, published_path: Path) -> int:
    if not plan_path.exists():
        print(f"publish_plan: no plan at {plan_path}")
        return 2
    data = json.loads(plan_path.read_text(encoding="utf-8"))
    if not data.get("ok", False):
        print("publish_plan: the plan has problems; not recording it as published")
        return 1
    record = {
        "published_at": _dt.datetime.now().isoformat(timespec="seconds"),
        "plan_generated": data.get("generated"),
        "build_dir": data.get("build_dir"),
        "files": {f["path"]: {"bytes": f["bytes"], "sha256": f["sha256"]} for f in data.get("files", [])},
    }
    published_path.write_text(json.dumps(record, indent=2), encoding="utf-8")
    print(f"publish_plan: recorded {len(record['files'])} published files in {published_path}")
    return 0


# ---- selftest ---------------------------------------------------------------------------------------------------

TEST_HTML = """<title>t</title>
<script>
  window.LS_ASSET_SUFFIX = /*LS:ASSET_SUFFIX*/"";
  window.LS_SERVED_EXT = /*LS:SERVED_EXT*/"";
  var BUILD = { loader: "Build/abc.loader.js", data: "Build/abc.data.unityweb",
                framework: "Build/abc.framework.js.unityweb", code: "Build/abc.wasm.unityweb" };
  var buildUrl = "Build"; var other = buildUrl + "/abc.data.unityweb";
  var config = { errorHandler: function () { return true; } };
</script>
"""


def _w(p: Path, data) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, int):
        with open(p, "wb") as f:
            f.write(os.urandom(min(data, 4096)))
            f.truncate(data)
    else:
        p.write_text(data, encoding="utf-8")


def selftest() -> int:
    tmp = Path(tempfile.mkdtemp(prefix="ls_publish_"))
    fails: List[str] = []
    try:
        build, out, plan_path, pub = tmp / "Web", tmp / "Web_publish", tmp / "plan.json", tmp / "published.json"
        _w(build / "index.html", TEST_HTML)
        _w(build / "Build/abc.loader.js", "function createUnityInstance(){}")
        _w(build / "Build/abc.data.unityweb", 3_000_000)
        _w(build / "Build/abc.framework.js.unityweb", 70_000)
        _w(build / "Build/abc.wasm.unityweb", 5_000_000)
        catalog = {"m_LocatorId": "AddressablesMainContentCatalog",
                   "m_InternalIdPrefixes": ["{UnityEngine.AddressableAssets.Addressables.RuntimePath}/WebGL"],
                   "m_InternalIds": ["0#/core_persistent_assets_all_aa11.bundle", "0#/s0_booth_scenes_all_bb22.bundle",
                                     "Assets/_Game/Scenes/Dev/Placeholders/PH_S0_Booth.unity",
                                     "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/WebGL/s1_approach_scenes_all_cc33.bundle"]}
        _w(build / "StreamingAssets/aa/catalog.json", json.dumps(catalog))
        _w(build / "StreamingAssets/aa/settings.json",
           json.dumps({"m_CatalogLocations": [{"m_InternalId": "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/catalog.json"}]}))
        _w(build / "StreamingAssets/aa/catalog.hash", "0123456789abcdef")
        _w(build / "StreamingAssets/aa/WebGL/core_persistent_assets_all_aa11.bundle", 2_500_000)
        _w(build / "StreamingAssets/aa/WebGL/s0_booth_scenes_all_bb22.bundle", 2_000_000)
        _w(build / "StreamingAssets/aa/WebGL/s1_approach_scenes_all_cc33.bundle", 1_500_000)
        _w(build / "StreamingAssets/aa/AddressablesLink/link.xml", "<linker/>")
        _w(build / "StreamingAssets/video/VID_TEST.mp4", 900_000)
        _w(build / "timing.html", "foreign")
        _w(build / "Build/abc.manifest", "x")
        limits = wl.load_limits()
        small = dict(limits, publish_batch_max_bytes=6_000_000, publish_batch_max_files=4)

        # First publish: nothing published yet.
        code = plan(build, out, plan_path, pub, small, ".wasm", False, True, quiet=True)
        data = json.loads(plan_path.read_text())
        if code != 0:
            fails.append(f"first plan should succeed: {data['problems']}")
        names = {f["path"] for f in data["files"]}
        expect = {"index.html", "Build/abc.loader.js", "Build/abc.data.unityweb.wasm", "Build/abc.framework.js.unityweb.wasm",
                  "Build/abc.wasm.unityweb.wasm", "StreamingAssets/aa/catalog.json", "StreamingAssets/aa/settings.json",
                  "StreamingAssets/aa/catalog.hash.wasm", "StreamingAssets/aa/WebGL/core_persistent_assets_all_aa11.bundle.wasm",
                  "StreamingAssets/aa/WebGL/s0_booth_scenes_all_bb22.bundle.wasm",
                  "StreamingAssets/aa/WebGL/s1_approach_scenes_all_cc33.bundle.wasm", "StreamingAssets/video/VID_TEST.mp4"}
        if names != expect:
            fails.append(f"mirror names wrong:\n  got {sorted(names)}\n  want {sorted(expect)}")
        html = (out / "index.html").read_text()
        if '"Build/abc.data.unityweb"' in html or "/abc.data.unityweb\"" in html:
            fails.append("index.html still references an unrenamed Build file")
        if 'Build/abc.data.unityweb.wasm"' not in html or '"/abc.data.unityweb.wasm"' not in html:
            fails.append("index.html references were not renamed (full path and buildUrl + name forms)")
        if '/*LS:ASSET_SUFFIX*/".wasm"' not in html or '/*LS:SERVED_EXT*/"' in html and '/*LS:SERVED_EXT*/""' in html:
            fails.append("index.html markers were not patched")
        cat = json.loads((out / "StreamingAssets/aa/catalog.json").read_text())
        if cat["m_InternalIds"][0] != "0#/core_persistent_assets_all_aa11.bundle.wasm" or \
                not cat["m_InternalIds"][3].endswith("s1_approach_scenes_all_cc33.bundle.wasm") or \
                cat["m_InternalIds"][2] != "Assets/_Game/Scenes/Dev/Placeholders/PH_S0_Booth.unity":
            fails.append(f"catalog ids not patched correctly: {cat['m_InternalIds']}")
        served = wl.served_set(limits)
        for f in data["files"]:
            if wl.ext_of(f["path"]) not in served:
                fails.append(f"unserved name in mirror: {f['path']}")
        for b in data["batches"]:
            if b["upload_bytes"] > 6_000_000 or b["entries"] > 4:
                fails.append(f"batch {b['index']} over the test limits: {b['upload_bytes']} B, {b['entries']} entries")
        sent = [k for b in data["batches"] for k in b["files"]]
        if sorted(sent) != sorted(names - {"index.html"}):
            fails.append("first publish must upload every file once")
        if any((out / p).exists() for p in ("timing.html", "Build/abc.manifest", "StreamingAssets/aa/AddressablesLink/link.xml")):
            fails.append("foreign/by-product files leaked into the mirror")

        # Mark published, then rebuild with one bundle changed, one gone, one new.
        if mark_published(plan_path, pub) != 0:
            fails.append("mark-published failed")
        (build / "StreamingAssets/aa/WebGL/s1_approach_scenes_all_cc33.bundle").unlink()
        _w(build / "StreamingAssets/aa/WebGL/s1_approach_scenes_all_dd44.bundle", 1_600_000)
        _w(build / "Build/abc.data.unityweb", 3_000_001)
        cat2 = dict(catalog)
        cat2["m_InternalIds"] = [i.replace("cc33", "dd44") for i in catalog["m_InternalIds"]]
        _w(build / "StreamingAssets/aa/catalog.json", json.dumps(cat2))
        code = plan(build, out, plan_path, pub, small, ".wasm", False, True, quiet=True)
        data = json.loads(plan_path.read_text())
        if code != 0:
            fails.append(f"second plan should succeed: {data['problems']}")
        b1 = data["batches"][0]["files"]
        if b1.get("StreamingAssets/aa/WebGL/s1_approach_scenes_all_cc33.bundle.wasm", "missing") is not None:
            fails.append("the vanished bundle must be a null removal in batch 1 (D22)")
        sent = {k for b in data["batches"] for k, v in b["files"].items() if v is not None}
        want = {"Build/abc.data.unityweb.wasm", "StreamingAssets/aa/WebGL/s1_approach_scenes_all_dd44.bundle.wasm",
                "StreamingAssets/aa/catalog.json"}
        if sent != want:
            fails.append(f"incremental uploads wrong: {sorted(sent)} (want {sorted(want)})")

        # Host limits: a 15.5 MB binary must be refused.
        _w(build / "StreamingAssets/video/VID_BIG.mp4", 15_500_000)
        code = plan(build, out, plan_path, pub, limits, ".wasm", True, True, quiet=True)
        if code != 1:
            fails.append("a 15.5 MB file must make the plan fail")
    except Exception as e:  # noqa: BLE001 - a selftest reports everything
        fails.append(f"exception: {e!r}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    if fails:
        print("publish_plan selftest: FAIL\n  " + "\n  ".join(fails))
        return 1
    print("publish_plan selftest: PASS (served names, index/catalog patching, batching, incremental uploads, D22 removals, host limits)")
    return 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Make the artifact publish mirror and plan for a Unity web build.")
    ap.add_argument("--build", default=str(DEFAULT_BUILD), help="Unity web build folder (default Builds/Web)")
    ap.add_argument("--out", default=None, help="publish mirror folder (default <build>_publish)")
    ap.add_argument("--plan", default=None, help="plan JSON (default <build>_publish_plan.json)")
    ap.add_argument("--published", default=str(DEFAULT_PUBLISHED), help="file list of the last approved publish (D22)")
    ap.add_argument("--limits", default=str(wl.LIMITS_PATH))
    ap.add_argument("--suffix", default=None, help="suffix for unserved files (default build_limits.json publish_suffix)")
    ap.add_argument("--full", action="store_true", help="upload every file, even if unchanged since the last publish")
    ap.add_argument("--no-catalog-patch", action="store_true", help="leave catalog.json alone (runtime remap only)")
    ap.add_argument("--mark-published", action="store_true", help="record the current plan as published")
    ap.add_argument("--quiet", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()

    build = Path(args.build)
    if not build.is_absolute():
        build = (Path.cwd() / build).resolve()
    out = Path(args.out) if args.out else build.parent / (build.name + "_publish")
    plan_path = Path(args.plan) if args.plan else build.parent / (build.name + "_publish_plan.json")
    published = Path(args.published)
    if args.mark_published:
        return mark_published(plan_path, published)
    try:
        limits = wl.load_limits(Path(args.limits))
        suffix = args.suffix if args.suffix is not None else str(limits["publish_suffix"])
        if suffix and wl.ext_of("x" + suffix) not in wl.served_set(limits):
            raise PlanError(f"suffix {suffix!r} is not a served extension")
        if not build.is_dir():
            raise PlanError(f"no build at {build}")
        return plan(build, out, plan_path, published, limits, suffix, args.full, not args.no_catalog_patch, args.quiet)
    except PlanError as e:
        print(f"publish_plan: {e}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
