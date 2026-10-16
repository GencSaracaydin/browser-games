#!/usr/bin/env python3
"""Validate a Unity web build folder against the host and plan limits (TECH_PLAN §9.1 step 5, webgl.md §6.2; WP01).

Same file rules as the editor's PostBuildValidator (both read Tools/build/build_limits.json):
  * every file <= 14.5 MB (text <= 16 MB), every *.bundle <= 12 MB, <= 250 files, <= 230 MB (warning above 220 MB);
  * index.html exists, has an errorHandler and never calls alert(); Build/ has the loader, data, framework and a
    compressed wasm (development builds are never hostable);
  * *.manifest / .DS_Store and foreign files (outside index.html, Build/, StreamingAssets/aa|video/, TemplateData/)
    are reported.
The content rules (EditorOnly assets, HDR formats, cubemaps, probe sizes) need the AssetDatabase and run only in the
editor validator.

--published validates a publish mirror made by publish_plan.py instead: every file must have an extension the
artifact host serves, binary files <= 15 MB, text <= 16 MB, and every Build/ file index.html names must exist.

Usage (from the project root):
    python3 Tools/build/validate_build.py                      # Builds/Web
    python3 Tools/build/validate_build.py Builds/Web --json Builds/Web_validation.json
    python3 Tools/build/validate_build.py Builds/Web_publish --published
    python3 Tools/build/validate_build.py --selftest            # synthetic builds: clean passes, a 15 MB file fails
Exit codes: 0 pass, 1 fail, 2 usage/configuration problem.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Dict, List

sys.path.insert(0, str(Path(__file__).resolve().parent))
import web_limits as wl  # noqa: E402


class Result:
    def __init__(self) -> None:
        self.errors: List[str] = []
        self.warnings: List[str] = []
        self.notes: List[str] = []
        self.file_count = 0
        self.total_bytes = 0
        self.largest = ("", 0)

    @property
    def ok(self) -> bool:
        return not self.errors

    def to_dict(self) -> Dict[str, object]:
        return {"ok": self.ok, "files": self.file_count, "total_bytes": self.total_bytes,
                "largest": {"path": self.largest[0], "bytes": self.largest[1]},
                "errors": self.errors, "warnings": self.warnings, "notes": self.notes}

    def to_text(self) -> str:
        lines = [f"{'PASS' if self.ok else 'FAIL'}: {self.file_count} files, {wl.mb(self.total_bytes)} MB, "
                 f"largest {self.largest[0]} {wl.mb(self.largest[1])} MB"]
        lines += [f"ERROR   {e}" for e in self.errors]
        lines += [f"WARNING {w}" for w in self.warnings]
        lines += [f"        {n}" for n in self.notes]
        return "\n".join(lines)


def validate_build(build: Path, limits: Dict[str, object]) -> Result:
    """File rules for an unpublished build (mirrors PostBuildValidator.ValidateOutput)."""
    r = Result()
    if not build.is_dir():
        r.errors.append(f"Build folder not found: {build}")
        return r
    served = wl.served_set(limits)
    has = {"index": False, "loader": False, "data": False, "framework": False, "code": False}
    for rel, path, size in wl.list_files(build):
        name = rel.rsplit("/", 1)[-1]
        r.file_count += 1
        r.total_bytes += size
        if size > r.largest[1]:
            r.largest = (rel, size)
        if wl.matches_any(name, limits["strip_patterns"]):
            r.warnings.append(f"{rel}: should have been removed after the build ({', '.join(limits['strip_patterns'])})")
        if not wl.allowed(rel, limits):
            r.warnings.append(f"{rel}: foreign file (not part of a Unity web build)")
        cap = int(limits["max_text_file_bytes"] if wl.is_text(rel, limits) else limits["max_file_bytes"])
        if size > cap:
            r.errors.append(f"{rel}: {wl.mb(size)} MB is over the {wl.mb(cap)} MB per-file limit")
        if rel.lower().endswith(".bundle") and size > int(limits["max_bundle_bytes"]):
            r.errors.append(f"{rel}: bundle {wl.mb(size)} MB is over {wl.mb(int(limits['max_bundle_bytes']))} MB "
                            "(split the group, TECH_PLAN §8.10)")
        if rel == "index.html":
            has["index"] = True
            html = path.read_text(encoding="utf-8", errors="replace")
            if wl.contains_alert_call(html):
                r.errors.append("index.html calls alert(): the template must never show an alert dialog (TECH_PLAN §7.1)")
            if "errorHandler" not in html:
                r.warnings.append("index.html has no errorHandler (not the CovertOps template?)")
        if rel.startswith("Build/"):
            if name.endswith(".loader.js"):
                has["loader"] = True
            elif ".data" in name:
                has["data"] = True
            elif ".framework.js" in name:
                has["framework"] = True
            elif ".wasm" in name:
                has["code"] = True
            if name.endswith(".wasm") and ".unityweb" not in name and size > int(limits["max_file_bytes"]) // 2:
                r.errors.append(f"{rel}: uncompressed wasm (development build or compression off); only compressed "
                                "release builds are hostable (webgl.md §7.1)")
        if wl.ext_of(rel) not in served:
            r.notes.append(f"{rel}: not served by the host as is; publish_plan.py publishes it as '{name}{limits['publish_suffix']}'")
    for key, what in (("index", "index.html"), ("loader", "Build/*.loader.js"), ("data", "Build/*.data*"),
                      ("framework", "Build/*.framework.js*"), ("code", "Build/*.wasm*")):
        if not has[key]:
            r.errors.append(f"{what} is missing")
    if r.file_count > int(limits["max_files"]):
        r.errors.append(f"{r.file_count} files is over the {limits['max_files']} file limit")
    if r.total_bytes > int(limits["max_total_bytes"]):
        r.errors.append(f"Total {wl.mb(r.total_bytes)} MB is over the {wl.mb(int(limits['max_total_bytes']))} MB cap")
    elif r.total_bytes > int(limits["warn_total_bytes"]):
        r.warnings.append(f"Total {wl.mb(r.total_bytes)} MB is over the planned {wl.mb(int(limits['warn_total_bytes']))} MB")
    return r


BUILD_REF = re.compile(r"""["'/]((?:Build/)?[A-Za-z0-9_.\-]+\.(?:loader\.js|wasm|unityweb|js|json))["']""")


def validate_published(mirror: Path, limits: Dict[str, object]) -> Result:
    """Host rules for a publish mirror (every extension served, host per-file limits, index references resolve)."""
    r = Result()
    if not mirror.is_dir():
        r.errors.append(f"Publish folder not found: {mirror}")
        return r
    served = wl.served_set(limits)
    names = set()
    for rel, path, size in wl.list_files(mirror):
        names.add(rel)
        r.file_count += 1
        r.total_bytes += size
        if size > r.largest[1]:
            r.largest = (rel, size)
        ext = wl.ext_of(rel)
        if ext not in served:
            r.errors.append(f"{rel}: extension '.{ext}' is not served by the artifact host")
        cap = int(limits["host_text_max_bytes"] if wl.is_text(rel, limits) else limits["host_binary_max_bytes"])
        if size > cap:
            r.errors.append(f"{rel}: {wl.mb(size)} MB is over the host's {wl.mb(cap)} MB per-file limit")
    index = mirror / "index.html"
    if not index.exists():
        r.errors.append("index.html is missing")
    else:
        html = index.read_text(encoding="utf-8", errors="replace")
        if wl.contains_alert_call(html):
            r.errors.append("index.html calls alert()")
        build_names = {n.split("/", 1)[1] for n in names if n.startswith("Build/")}
        for m in BUILD_REF.finditer(html):
            ref = m.group(1)
            base = ref.split("/", 1)[1] if ref.startswith("Build/") else ref
            looks_unity = any(base.endswith(s) for s in (".loader.js", ".unityweb", ".unityweb.wasm", ".wasm")) or ".data" in base \
                or ".framework" in base
            if looks_unity and base not in build_names and "{{{" not in base:
                r.errors.append(f"index.html references Build/{base}, which is not in the mirror")
    if r.file_count > int(limits["host_version_max_files"]):
        r.errors.append(f"{r.file_count} files is over the host's {limits['host_version_max_files']} files per version")
    if r.total_bytes > int(limits["max_total_bytes"]):
        r.errors.append(f"Total {wl.mb(r.total_bytes)} MB is over the {wl.mb(int(limits['max_total_bytes']))} MB cap")
    return r


def _write(p: Path, data) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, int):
        with open(p, "wb") as f:
            f.truncate(data)
    else:
        p.write_text(data, encoding="utf-8")


def make_synthetic_build(root: Path) -> None:
    _write(root / "index.html", "<title>x</title><script>var config = { errorHandler: function () { return true; } };</script>")
    _write(root / "Build/abc.loader.js", "function createUnityInstance(){}")
    _write(root / "Build/abc.data.unityweb", 4_000_000)
    _write(root / "Build/abc.framework.js.unityweb", 70_000)
    _write(root / "Build/abc.wasm.unityweb", 6_900_000)
    _write(root / "StreamingAssets/aa/catalog.json", "{}")
    _write(root / "StreamingAssets/aa/settings.json", "{}")
    _write(root / "StreamingAssets/aa/WebGL/core_persistent_assets_all_00.bundle", 3_000_000)


def selftest() -> int:
    limits = wl.load_limits()
    tmp = Path(tempfile.mkdtemp(prefix="ls_validate_"))
    failures = []
    try:
        make_synthetic_build(tmp)
        r = validate_build(tmp, limits)
        if not r.ok:
            failures.append("clean synthetic build should pass:\n" + r.to_text())
        _write(tmp / "StreamingAssets/video/VID_BIG.mp4", 15_000_000)
        r = validate_build(tmp, limits)
        if r.ok or not any("VID_BIG.mp4" in e for e in r.errors):
            failures.append("a 15 MB file must fail:\n" + r.to_text())
        (tmp / "StreamingAssets/video/VID_BIG.mp4").unlink()
        _write(tmp / "index.html", "<script>var config = { errorHandler: function (e) { alert(e); } };</script>")
        r = validate_build(tmp, limits)
        if r.ok or not any("alert" in e for e in r.errors):
            failures.append("alert() in index.html must fail:\n" + r.to_text())
        _write(tmp / "index.html", "<script>// alert(1)\nvar config = { errorHandler: function () { return true; } };</script>")
        r = validate_build(tmp, limits)
        if not r.ok:
            failures.append("alert( inside a comment must pass:\n" + r.to_text())
        _write(tmp / "StreamingAssets/aa/WebGL/big.bundle", 12_500_000)
        r = validate_build(tmp, limits)
        if r.ok or not any("big.bundle" in e for e in r.errors):
            failures.append("a 12.5 MB bundle must fail:\n" + r.to_text())
        (tmp / "StreamingAssets/aa/WebGL/big.bundle").unlink()
        r = validate_published(tmp, limits)
        if r.ok or not any(".unityweb" in e for e in r.errors):
            failures.append("--published must reject .unityweb/.bundle names:\n" + r.to_text())
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    if failures:
        print("validate_build selftest: FAIL\n" + "\n\n".join(failures))
        return 1
    print("validate_build selftest: PASS (clean build passes; 15 MB file, alert(), 12.5 MB bundle and unserved names fail)")
    return 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Validate a Unity web build (or its publish mirror) against the host limits.")
    ap.add_argument("folder", nargs="?", default=str(wl.PROJECT / "Builds" / "Web"))
    ap.add_argument("--published", action="store_true", help="validate a publish mirror (served extensions, host limits)")
    ap.add_argument("--limits", default=str(wl.LIMITS_PATH))
    ap.add_argument("--json", help="also write the result as JSON")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()
    try:
        limits = wl.load_limits(Path(args.limits))
    except (OSError, ValueError) as e:
        print(f"validate_build: cannot read limits: {e}")
        return 2
    folder = Path(args.folder)
    if not folder.is_absolute():
        folder = (Path.cwd() / folder).resolve()
    r = validate_published(folder, limits) if args.published else validate_build(folder, limits)
    print(r.to_text())
    if args.json:
        Path(args.json).write_text(json.dumps(r.to_dict(), indent=2), encoding="utf-8")
    return 0 if r.ok else 1


if __name__ == "__main__":
    sys.exit(main())
