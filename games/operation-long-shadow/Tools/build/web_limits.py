#!/usr/bin/env python3
"""Shared helpers for the web build tools (WP01): limits, file walking, the alert() check.

Used by ``validate_build.py`` and ``publish_plan.py``. The C# twin of these rules is
``LongShadow.Platform.Editor.PostBuildValidator`` / ``BuildLimits``; both read ``Tools/build/build_limits.json``
so Python and C# never disagree. Standard library only (python3 3.9+).
"""
from __future__ import annotations

import fnmatch
import json
import os
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple

PROJECT = Path(__file__).resolve().parents[2]
LIMITS_PATH = PROJECT / "Tools" / "build" / "build_limits.json"

DEFAULT_LIMITS: Dict[str, object] = {
    "max_file_bytes": 14_500_000,
    "max_text_file_bytes": 16_000_000,
    "max_bundle_bytes": 12_000_000,
    "max_files": 250,
    "max_total_bytes": 230_000_000,
    "warn_total_bytes": 220_000_000,
    "max_reflection_probe_px": 256,
    "publish_batch_max_bytes": 60_000_000,
    "publish_batch_max_files": 250,
    "host_call_max_bytes": 64_000_000,
    "host_call_max_files": 255,
    "host_version_max_bytes": 256_000_000,
    "host_version_max_files": 511,
    "host_binary_max_bytes": 15_000_000,
    "host_text_max_bytes": 16_000_000,
    "publish_suffix": ".wasm",
    "served_extensions": ["html", "css", "js", "mjs", "wasm", "json", "map", "geojson", "webmanifest", "txt", "md", "csv",
                          "xml", "svg", "png", "jpg", "jpeg", "gif", "webp", "avif", "ico", "woff", "woff2", "ttf", "otf",
                          "mp3", "wav", "ogg", "mp4", "webm", "pdf"],
    "text_extensions": ["html", "css", "js", "mjs", "json", "map", "geojson", "webmanifest", "txt", "md", "csv", "xml", "svg",
                        "hash"],
    "strip_patterns": ["*.manifest", ".DS_Store", "Thumbs.db"],
    "allowed_prefixes": ["index.html", "Build/", "StreamingAssets/aa/", "StreamingAssets/video/", "TemplateData/"],
    "content_types": {},
}

# Never shipped even though they sit under an allowed prefix.
EXCLUDED_PREFIXES = ("StreamingAssets/aa/AddressablesLink/",)


def load_limits(path: Optional[Path] = None) -> Dict[str, object]:
    """Limits from build_limits.json merged over the defaults."""
    p = Path(path) if path else LIMITS_PATH
    limits = dict(DEFAULT_LIMITS)
    if p.exists():
        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)
        limits.update({k: v for k, v in data.items() if not k.startswith("_")})
    return limits


def ext_of(rel: str) -> str:
    """Lower-case extension of the last path segment, '' when there is none (mirrors PublishedNames.ExtensionOf)."""
    rel = rel.split("?", 1)[0].split("#", 1)[0]
    name = rel.replace("\\", "/").rsplit("/", 1)[-1]
    if "." not in name or name.endswith("."):
        return ""
    return name.rsplit(".", 1)[1].lower()


def served_set(limits: Dict[str, object]) -> set:
    return {str(e).lower().lstrip(".") for e in limits["served_extensions"]}


def is_text(rel: str, limits: Dict[str, object]) -> bool:
    return ext_of(rel) in {str(e).lower() for e in limits["text_extensions"]}


def content_type(rel: str, limits: Dict[str, object]) -> str:
    types = limits.get("content_types") or {}
    return str(types.get(ext_of(rel), "application/octet-stream"))


def matches_any(name: str, patterns: List[str]) -> bool:
    return any(fnmatch.fnmatch(name.lower(), str(p).lower()) for p in patterns or [])


def allowed(rel: str, limits: Dict[str, object]) -> bool:
    prefixes = limits.get("allowed_prefixes") or []
    if not prefixes:
        return True
    return any(rel == p or rel.startswith(p) for p in prefixes)


def list_files(root: Path) -> List[Tuple[str, Path, int]]:
    """(relative posix path, absolute path, size) for every file under root, sorted by path."""
    out = []
    for dirpath, _dirs, files in os.walk(root):
        for fn in files:
            p = Path(dirpath) / fn
            rel = p.relative_to(root).as_posix()
            out.append((rel, p, p.stat().st_size))
    out.sort(key=lambda t: t[0])
    return out


def contains_alert_call(html: str) -> bool:
    """True when the text calls alert( outside // and /* */ comments (mirrors PostBuildValidator.ContainsAlertCall)."""
    in_block = False
    for raw in (html or "").split("\n"):
        line = raw
        if in_block:
            end = line.find("*/")
            if end < 0:
                continue
            line = line[end + 2:]
            in_block = False
        start = line.find("/*")
        if start >= 0:
            end = line.find("*/", start + 2)
            if end < 0:
                in_block = True
                line = line[:start]
            else:
                line = line[:start] + line[end + 2:]
        slash = line.find("//")
        if slash >= 0 and (slash == 0 or line[slash - 1] != ":"):
            line = line[:slash]
        for m in re.finditer(r"alert\(", line):
            prev = line[m.start() - 1] if m.start() > 0 else " "
            if not (prev.isalnum() or prev in "_$"):
                return True
    return False


def mb(n: int) -> str:
    return f"{n / 1_000_000:.2f}"
