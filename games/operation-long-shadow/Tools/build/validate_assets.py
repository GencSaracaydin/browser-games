#!/usr/bin/env python3
"""Asset validator for OPERATION LONG SHADOW (TECH_PLAN §3.10-3.12, §8.9, §8.10; WP00 Phase B).

Checks content files the way the Unity import rules will see them, without the editor:

* textures: naming (T_<Name>_<BC|N|M|E|H|A>), readable header, power-of-two size, in-game size after the import
  rules' caps (a Python mirror of LongShadow.Core.Editor.TextureRules, fed by the same Tools/build/asset_budgets.json
  and Docs/ASSET_MANIFEST.json), set completeness (BC + N + M), D4 mask has an alpha (smoothness) channel, and with
  --pixels (numpy + Pillow) normal-map / mask / albedo plausibility and real alpha use;
* models (.fbx binary/ASCII, .obj): LOD0 triangle count (authored *_LOD1.. meshes excluded) against the manifest
  budget of the folder or the §8.9 class cap, LOD naming, naming prefixes;
* audio (.wav): readable PCM, folder known to the import rules (an unknown SFX group or VO chain folder is an
  error, §3.12), VO file naming VO_<line>_<CHAIN>[_A|_B] with a DIALOGUE.json line, durations against the manifest
  budget ("N s source") or the §8.9 class limits, channel layout;
* an estimated download size per §8.10 budget line (textures from the measured Crunch basis, audio as WebGL AAC at
  44.1 kHz, meshes per triangle) against the planned 220 MB / hard 230 MB;
* --coverage: which manifest entries are landed in Assets/, ready in RawAssets/<wp>/ready/, or missing (by priority).

Usage (from the project root; system python3 works, Tools/.venv/bin/python adds the pixel checks):
  python3 Tools/build/validate_assets.py                      # Assets/_Game + Assets/StreamingAssets/video
  python3 Tools/build/validate_assets.py --ready              # every RawAssets/*/ready tree, as if landed
  python3 Tools/build/validate_assets.py --ready wp10 --ready audio
  python3 Tools/build/validate_assets.py --path Assets/_Game/Art/Textures/Props
  python3 Tools/build/validate_assets.py --coverage           # manifest coverage report
  Tools/.venv/bin/python Tools/build/validate_assets.py --ready --pixels
  python3 Tools/build/validate_assets.py --check-mirror       # Python caps vs the editor's Logs/import_rules_table.tsv
  python3 Tools/build/validate_assets.py --selftest           # synthetic files with known problems -> expected codes
  options: --json FILE, --out FILE (default Logs/validate_assets.txt), --strict (warnings fail), --quiet, --max-issues N

Exit codes: 0 no errors (warnings allowed unless --strict), 1 errors, 2 usage/configuration problem.
Stdlib only; numpy + Pillow are optional (--pixels). Never modifies assets.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import re
import struct
import sys
import time
import wave
import zlib
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUDGETS_PATH = ROOT / "Tools/build/asset_budgets.json"
MANIFEST_PATH = ROOT / "Docs/ASSET_MANIFEST.json"
DIALOGUE_PATH = ROOT / "Docs/DIALOGUE.json"
RULE_TABLE_PATH = ROOT / "Logs/import_rules_table.tsv"
GAME = "Assets/_Game/"

TEXTURE_EXT = {".png", ".jpg", ".jpeg", ".tga", ".exr", ".hdr", ".psd", ".tif", ".tiff"}
MODEL_EXT = {".fbx", ".obj"}
AUDIO_EXT = {".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac"}
SURFACE_KINDS = {"env", "props", "weapons", "characters", "vehicles", "vegetation", "decals"}
SUFFIXES = ("BC", "N", "M", "E", "H", "A")

SEVERITY_ORDER = {"error": 0, "warning": 1, "info": 2}


# ----------------------------------------------------------------------------------------------- data


@dataclass
class Issue:
    severity: str
    code: str
    path: str
    message: str


@dataclass
class Item:
    """One file as it will exist in the project: target path (Assets/...) and where it is read from."""

    target: str
    source: Path
    origin: str  # "assets" or "ready:<wp>"


@dataclass
class Estimate:
    line: str
    bytes: float
    kind: str


@dataclass
class Report:
    issues: list = field(default_factory=list)
    estimates: list = field(default_factory=list)
    counts: Counter = field(default_factory=Counter)

    def add(self, severity, code, path, message):
        self.issues.append(Issue(severity, code, path, message))


def load_json(path: Path, default=None):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        return default


BUDGETS = load_json(BUDGETS_PATH, {}) or {}


def budget(section, key, default):
    return (BUDGETS.get(section) or {}).get(key, default)


def in_list(lst, name):
    if not name:
        return False
    n = name.lower()
    return any(n == s.lower() for s in lst)


# ----------------------------------------------------------------------------------------------- manifest


class Manifest:
    def __init__(self):
        self.entries = load_json(MANIFEST_PATH, []) or []
        self.by_id = {e["id"]: e for e in self.entries}
        self._caps = None
        self._folder_entries = None

    # Mirror of TextureBudgets.ParseCap (C#).
    _in_game = re.compile(r"(\d{3,4})\s*(?:maps\s*)?in game", re.I)
    _dims = re.compile(r"(\d{3,4})\s*x\s*(\d{3,4})", re.I)
    _maps = re.compile(r"(\d{3,4})\s*(?:maps|atlas|tile)\b", re.I)
    _px_range = re.compile(r"\d{2,4}\s*-\s*(\d{2,4})\s*px", re.I)
    _px_max = re.compile(r"<=\s*(\d{2,4})\s*px", re.I)
    _lead = re.compile(r"^\s*(\d{3,4})\b")

    @classmethod
    def parse_cap(cls, text):
        if not text:
            return None
        for rx in (cls._in_game,):
            m = rx.search(text)
            if m:
                return int(m.group(1))
        m = cls._dims.search(text)
        if m:
            return max(int(m.group(1)), int(m.group(2)))
        for rx in (cls._maps, cls._px_range, cls._px_max, cls._lead):
            m = rx.search(text)
            if m:
                return int(m.group(1))
        return None

    def caps(self):
        """(kind/folder lower) -> texture cap, mirror of TextureBudgets.Ensure."""
        if self._caps is not None:
            return self._caps
        caps = {}
        for e in self.entries:
            p = (e.get("target_path") or "").replace("\\", "/")
            kind = rest = None
            for root in ("Assets/_Game/Art/Textures/", "Assets/_Game/Art/Models/"):
                if p.lower().startswith(root.lower()):
                    r = p[len(root):]
                    slash = r.find("/")
                    if slash > 0:
                        kind, rest = r[:slash], r[slash + 1:]
                    break
            if kind is None and p.lower().startswith("assets/_game/ui/sprites/"):
                kind, rest = "UI/Sprites", p[len("Assets/_Game/UI/Sprites/"):]
            if not kind or not rest:
                continue
            fe = rest.find("/")
            if fe <= 0:
                continue
            folder = rest[:fe]
            if kind.lower() == "characters":
                continue
            cap = self.parse_cap(e.get("budget") or "")
            if cap is None:
                continue
            key = (kind + "/" + folder).lower()
            if key not in caps or cap > caps[key]:
                caps[key] = cap
        self._caps = caps
        return caps

    def folder_entries(self):
        """folder target (lower, with trailing /) -> [entries]."""
        if self._folder_entries is None:
            d = defaultdict(list)
            for e in self.entries:
                p = (e.get("target_path") or "").replace("\\", "/")
                if p.endswith("/"):
                    d[p.lower()].append(e)
            self._folder_entries = d
        return self._folder_entries


def to_max_size(pixels):
    s = 32
    while s < pixels and s < 8192:
        s <<= 1
    return s


# ----------------------------------------------------------------------------------------------- texture probing


@dataclass
class TexInfo:
    known: bool = False
    width: int = 0
    height: int = 0
    has_alpha: bool = False
    alpha_used: bool = False
    alpha_scanned: bool = False
    bits: int = 0
    hdr: bool = False

    @property
    def max_side(self):
        return max(self.width, self.height)


def probe_texture(path: Path) -> TexInfo:
    ext = path.suffix.lower()
    info = TexInfo()
    try:
        with open(path, "rb") as f:
            head = f.read(1 << 20)
    except OSError:
        return info
    if ext == ".png":
        if len(head) < 33 or head[:8] != b"\x89PNG\r\n\x1a\n":
            return info
        info.known = True
        info.width, info.height = struct.unpack(">II", head[16:24])
        info.bits = head[24]
        ct = head[25]
        has_alpha = ct in (4, 6)
        o = 8
        while not has_alpha and o + 8 <= len(head):
            ln = struct.unpack(">I", head[o:o + 4])[0]
            typ = head[o + 4:o + 8]
            if typ == b"tRNS":
                has_alpha = True
            if typ in (b"IDAT", b"IEND"):
                break
            o += 12 + ln
        info.has_alpha = has_alpha
        info.alpha_used = has_alpha  # refined by the pixel scan when Pillow is available
    elif ext in (".jpg", ".jpeg"):
        if head[:2] != b"\xff\xd8":
            return info
        o = 2
        while o + 9 < len(head):
            if head[o] != 0xFF:
                o += 1
                continue
            m = head[o + 1]
            if m == 0xFF:
                o += 1
                continue
            if m == 0xD8 or 0xD0 <= m <= 0xD7 or m == 0x01:
                o += 2
                continue
            ln = struct.unpack(">H", head[o + 2:o + 4])[0]
            if 0xC0 <= m <= 0xCF and m not in (0xC4, 0xC8, 0xCC):
                info.known = True
                info.bits = head[o + 4]
                info.height, info.width = struct.unpack(">HH", head[o + 5:o + 9])
                return info
            o += 2 + ln
    elif ext == ".tga":
        if len(head) < 18:
            return info
        info.known = True
        info.width, info.height = struct.unpack("<HH", head[12:16])
        depth, desc = head[16], head[17]
        info.bits = 8
        info.has_alpha = depth == 32 or (desc & 0x0F) > 0
        info.alpha_used = info.has_alpha
    elif ext == ".exr":
        info.hdr, info.bits = True, 16
        if len(head) < 8 or struct.unpack("<I", head[:4])[0] != 20000630:
            return info
        o = 8
        while o < len(head) and head[o] != 0:
            ne = head.find(b"\0", o)
            te = head.find(b"\0", ne + 1)
            if ne < 0 or te < 0 or te + 5 > len(head):
                break
            name = head[o:ne].decode("ascii", "replace")
            size = struct.unpack("<i", head[te + 1:te + 5])[0]
            val = te + 5
            if name == "dataWindow" and val + 16 <= len(head):
                x0, y0, x1, y1 = struct.unpack("<iiii", head[val:val + 16])
                info.known, info.width, info.height = True, x1 - x0 + 1, y1 - y0 + 1
            if name == "channels":
                info.has_alpha = b"A\0" in head[val:val + size]
                info.alpha_used = info.has_alpha
            o = val + size
    elif ext == ".hdr":
        info.hdr, info.bits = True, 32
        for line in head[:8192].split(b"\n"):
            parts = line.strip().split(b" ")
            if len(parts) == 4 and parts[0] in (b"-Y", b"+Y") and parts[1].isdigit() and parts[3].isdigit():
                info.known, info.height, info.width = True, int(parts[1]), int(parts[3])
                break
    return info


try:  # optional pixel checks
    import numpy as np  # type: ignore
    from PIL import Image  # type: ignore

    Image.MAX_IMAGE_PIXELS = None
    HAVE_PIXELS = True
except Exception:  # pragma: no cover - environment dependent
    np = None
    Image = None
    HAVE_PIXELS = False


def pixel_stats(path: Path, need_alpha_scan: bool):
    """Returns dict with mean rgb/alpha stats (downsampled) and min alpha (full res), or None."""
    if not HAVE_PIXELS:
        return None
    try:
        with Image.open(path) as im:
            im.load()
            mode = im.mode
            if mode in ("I;16", "I;16B", "I", "F"):
                return {"mode": mode}
            if mode not in ("RGB", "RGBA", "L", "LA", "P"):
                im = im.convert("RGBA")
            if mode == "P":
                im = im.convert("RGBA")
            out = {"mode": im.mode}
            if need_alpha_scan and "A" in im.getbands():
                a = np.asarray(im.getchannel("A"))
                out["alpha_min"] = int(a.min())
            small = im.reduce(4) if min(im.size) >= 256 else im
            arr = np.asarray(small.convert("RGBA"), dtype=np.float32) / 255.0
            out["mean"] = arr[..., :3].reshape(-1, 3).mean(axis=0).tolist()
            out["median"] = np.median(arr[..., :3].reshape(-1, 3), axis=0).tolist()
            out["alpha_mean"] = float(arr[..., 3].mean())
            out["std"] = arr[..., :3].reshape(-1, 3).std(axis=0).tolist()
            return out
    except Exception as e:  # pragma: no cover
        return {"error": str(e)}


# ----------------------------------------------------------------------------------------------- texture rules mirror


@dataclass
class TexRule:
    name: str
    max: int
    fmt: str
    quality: int
    mips: bool = True
    srgb: bool = True
    editor_only: bool = False
    cap_source: str = ""
    sprite: bool = False
    npot_none: bool = False


def block_aligned_after_cap(info, cap):
    """Mirror of TextureRules.BlockAlignedAfterCap: the NPOT source stays a multiple of 4 after Unity scales it
    proportionally (nearest pixel) to fit the cap (2400x1792 at 1024 -> 1024x765: not aligned)."""
    if not info.known or info.width <= 0 or info.height <= 0:
        return False
    w, h = info.width, info.height
    m = max(w, h)
    if m > cap:
        scale = cap / m
        w, h = max(1, math.floor(w * scale + 0.5)), max(1, math.floor(h * scale + 0.5))
    return w % 4 == 0 and h % 4 == 0


def _has_token(stem, token):
    return any(p.lower() == token.lower() for p in stem.split("_"))


def parse_suffix(file_name):
    stem = os.path.splitext(os.path.basename(file_name))[0]
    us = stem.rfind("_")
    if us <= 0 or us == len(stem) - 1:
        return None, stem[2:] if stem.startswith("T_") else stem
    suf = stem[us + 1:]
    if suf not in SUFFIXES:
        return None, stem[2:] if stem.startswith("T_") else stem
    head = stem[:us]
    return suf, head[2:] if head.startswith("T_") else head


class TextureRulesMirror:
    """Python mirror of LongShadow.Core.Editor.TextureRules (caps, formats, quality, mips, editor-only)."""

    Q_BC, Q_DATA, Q_FX_E = 55, 60, 70

    def __init__(self, manifest: Manifest):
        self.m = manifest
        t = BUDGETS.get("texture", {})
        self.hero_env = t.get("hero_env_sets", [])
        self.ground_env = t.get("ground_env_sets", [])
        self.hero_props = t.get("hero_props", [])
        self.fp = t.get("fp_weapons", [])
        self.small_fp = t.get("small_fp_weapons", [])
        self.tiny = t.get("tiny_weapons", [])
        self.squad = t.get("squad_characters", [])
        self.hero_heads = t.get("hero_heads", [])
        self.minor = t.get("minor_characters", [])
        self.folder_caps = {k.lower(): v for k, v in (t.get("folder_caps") or {}).items() if not k.startswith("_")}
        self.impostor_cap = t.get("impostor_cap", 256)
        self.fx_cap = t.get("fx_cap", 1024)
        self.emissive_cap = t.get("emissive_cap", 512)

    def _manifest_cap(self, kind, folder):
        if not folder:
            return None
        return self.m.caps().get((kind + "/" + folder).lower())

    def _override(self, kind, folder):
        if not folder:
            return None
        return self.folder_caps.get((kind + "/" + folder).lower())

    @staticmethod
    def _contains(s, part):
        return part.lower() in (s or "").lower()

    def _alpha_card(self, file):
        stem = os.path.splitext(file)[0]
        c = self._contains
        return (c(file, "Leaves") or _has_token(stem, "Leaf") or c(file, "Grass") or c(file, "Foliage") or c(file, "Cards")
                or c(file, "Impostor"))

    def _clamped_card(self, file):
        return self._alpha_card(file) or self._contains(file, "Blur")

    def surface(self, cls, suffix, info, cap, cap_source, q_bc, q_data):
        r = TexRule(name=f"{cls}/{suffix or 'Color'}", max=cap, fmt="", quality=q_bc, cap_source=cap_source)
        if suffix == "N":
            r.fmt, r.quality, r.srgb = "DXT5Crunched", q_data, False
        elif suffix == "M":
            r.fmt, r.quality, r.srgb = "DXT5Crunched", q_data, False
        elif suffix == "E":
            if cap > self.emissive_cap:
                r.max, r.cap_source = self.emissive_cap, f"emissive {self.emissive_cap}"
            r.fmt = "DXT5Crunched" if info.alpha_used else "DXT1Crunched"
            r.quality = q_data
        elif suffix == "H":
            r.srgb = False
            if info.bits >= 16 and not info.hdr:
                r.name, r.fmt, r.quality, r.mips, r.editor_only = f"{cls}/H16", "R16", -1, False, True
                r.max = max(cap, to_max_size(max(info.max_side, 32)))
                return r
            if r.max > 1024:
                r.max, r.cap_source = 1024, "height 1024"
            r.fmt, r.quality = "DXT1Crunched", self.Q_BC
        elif suffix == "A":
            r.fmt, r.quality, r.srgb = "DXT1Crunched", self.Q_BC, False
        else:
            r.fmt = "DXT5Crunched" if info.alpha_used else "DXT1Crunched"
            r.quality = q_bc
        return r

    def resolve(self, target, info: TexInfo):
        r = self._resolve(target, info)
        if r and info.known and info.max_side > 0 and not r.editor_only and r.name != "HDRI/EditorOnly":
            sc = to_max_size(info.max_side)
            if sc < r.max:
                r.max = sc
                r.cap_source += f", source {info.width}x{info.height}"
        return r

    def _resolve(self, target, info):
        t = target.replace("\\", "/")
        if not t.lower().startswith(GAME.lower()):
            return None
        rel = t[len(GAME):]
        file = os.path.basename(rel)
        suffix, _ = parse_suffix(file)
        low = rel.lower()
        if low.startswith("art/hdri/editoronly/"):
            return TexRule("HDRI/EditorOnly", 2048, "RGBAHalf", -1, editor_only=True, srgb=False, cap_source="bake HDRI 2048")
        if low.startswith("art/hdri/runtime/"):
            return TexRule("HDRI/Runtime/Sky", 2048, "DXT5Crunched" if info.alpha_used else "DXT1Crunched", 70, cap_source="sky 2048x1024")
        if low.startswith("ui/sprites/"):
            folder = rel.split("/")[2] if rel.count("/") >= 3 else None
            mc = self._manifest_cap("UI/Sprites", folder)
            cap = to_max_size(mc) if mc else 512
            small = info.known and (info.max_side <= 128 or not block_aligned_after_cap(info, cap))
            return TexRule("UI/Sprite/Uncompressed" if small else "UI/Sprite", cap, "RGBA32" if small else "DXT5Crunched",
                           -1 if small else 70, mips=False, sprite=True, npot_none=True, cap_source="manifest" if mc else "UI 512")
        if not low.startswith("art/textures/"):
            return None
        parts = rel.split("/")
        kind = parts[2] if len(parts) > 3 else ""
        folder = parts[3] if len(parts) > 4 else None
        k = kind.lower()
        if k == "env":
            if folder and folder.lower() == "_shared":
                r = self.surface("Env/_Shared", "A" if suffix in (None, "BC") else suffix, info, 512, "detail maps 512", self.Q_BC, self.Q_DATA)
                if suffix in (None, "BC"):
                    r.name, r.fmt, r.srgb = "Env/_Shared/DetailAlbedo", "DXT1Crunched", False
                return r
            o, mc = self._override("Env", folder), self._manifest_cap("Env", folder)
            if o:
                cap, src = to_max_size(o), "override (asset_budgets.json)"
            elif mc:
                cap, src = to_max_size(mc), "manifest"
            elif in_list(self.hero_env, folder):
                cap, src = 2048, "hero list"
            else:
                cap, src = 1024, "class default"
            return self.surface("Env", suffix, info, cap, src, self.Q_BC, self.Q_DATA)
        if k == "props":
            o, mc = self._override("Props", folder), self._manifest_cap("Props", folder)
            if o:
                cap, src = to_max_size(o), "override (asset_budgets.json)"
            elif mc:
                cap, src = to_max_size(mc), "manifest"
            elif in_list(self.hero_props, folder):
                cap, src = 1024, "hero list"
            else:
                cap, src = 512, "class default (small/medium prop)"
            r = self.surface("Props", suffix, info, cap, src, self.Q_BC, self.Q_DATA)
            return self._cards(r, file)
        if k == "weapons":
            q = 80
            if folder and folder.lower() == "_shared":
                r = self.surface("Weapons/_Shared", "A" if suffix in (None, "BC") else suffix, info, 512, "detail maps 512", q, q)
                if suffix in (None, "BC"):
                    r.srgb = False
                return r
            is_mag, is_rocket = self._contains(file, "_Mag"), self._contains(file, "_Rocket")
            o = self._override("Weapons", folder)
            if o:
                cap, src = to_max_size(o), "override (asset_budgets.json)"
            elif in_list(self.fp, folder):
                if is_mag:
                    cap, src = 512, "FP magazine 512 (§8.9)"
                elif suffix == "M":
                    cap, src = 1024, "FP mask 1024 (§8.9)"
                else:
                    cap, src = 2048, "FP weapon 2048 (§8.9)"
            elif in_list(self.small_fp, folder):
                cap, src = 1024, "small FP item 1024"
            elif in_list(self.tiny, folder) or is_rocket:
                cap, src = 512, "small item 512"
            else:
                mc = self._manifest_cap("Weapons", folder)
                cap, src = (to_max_size(mc), "manifest (world weapon)") if mc else (1024, "world weapon 1024")
            return self.surface("Weapons", suffix, info, cap, src, q, q)
        if k == "characters":
            is_head = self._contains(file, "_Head")
            q = 80 if is_head else 70
            o = self._override("Characters", folder)
            if o:
                cap, src = to_max_size(o), "override (asset_budgets.json)"
            elif folder and folder.lower() == "fabrics":
                cap, src = 1024, "fabric tile 1024"
            elif folder and folder.lower() == "props":
                prop = parts[4] if len(parts) > 5 else ""
                cap, src = (1024 if "prc" in prop.lower() else 512), "character prop (§8.9)"
            elif in_list(self.minor, folder):
                cap, src = 512, "enemy/minor character 512 (§8.9)"
            elif is_head and in_list(self.hero_heads, folder):
                cap, src = 2048, "hero head 2048 (§8.9)"
            else:
                cap, src = 1024, "squad 1024 (§8.9)" if in_list(self.squad, folder) else "class default"
            return self.surface("Characters", suffix, info, cap, src, q, q)
        if k == "vehicles":
            o, mc = self._override("Vehicles", folder), self._manifest_cap("Vehicles", folder)
            cap = to_max_size(o) if o else to_max_size(mc) if mc else 1024
            src = "override (asset_budgets.json)" if o else "manifest" if mc else "class default"
            if suffix not in (None, "BC") and cap > 1024:
                cap, src = 1024, "vehicle N/M 1024 (§8.9)"
            return self._cards(self.surface("Vehicles", suffix, info, cap, src, self.Q_BC, self.Q_DATA), file)
        if k == "vegetation":
            o, mc = self._override("Vegetation", folder), self._manifest_cap("Vegetation", folder)
            cap = to_max_size(o) if o else to_max_size(mc) if mc else 1024
            src = "override (asset_budgets.json)" if o else "manifest" if mc else "tree 1024 (§8.9)"
            return self._cards(self.surface("Vegetation", suffix, info, cap, src, self.Q_BC, self.Q_DATA), file)
        if k == "decals":
            o, mc = self._override("Decals", folder), self._manifest_cap("Decals", folder)
            cap = to_max_size(o) if o else to_max_size(mc) if mc else 1024
            src = "override (asset_budgets.json)" if o else "manifest" if mc else "decal atlas 1024"
            r = self.surface("Decals", suffix, info, cap, src, self.Q_DATA, self.Q_DATA)
            if suffix in (None, "BC", "M", "A", "H"):
                r.fmt = "DXT5Crunched"
            return r
        if k == "images":
            stem = os.path.splitext(file)[0]
            if stem.lower().startswith("concept") or self._contains(stem, "_Reference") or stem.lower().endswith("_ref"):
                return TexRule("Images/EditorReference", 2048, "DXT1", 100, mips=False, editor_only=True, cap_source="editor-only reference 2048")
            fs = self._contains(file, "_FS")
            cap = 2048 if fs else 1024
            world = (self._contains(stem, "Page") or self._contains(stem, "Document") or self._contains(stem, "Dossier")
                     or _has_token(stem, "3D"))
            ui_only = ((stem.lower().startswith("t_intel") or stem.lower().startswith("intel_") or self._contains(stem, "_UI")
                        or self._contains(stem, "_Overlay")) and not world and (not info.known or block_aligned_after_cap(info, cap)))
            keep_aspect = not world and block_aligned_after_cap(info, cap)
            return TexRule("Images/UI" if ui_only else "Images/World" if world else "Images/Plate", cap,
                           "DXT5Crunched" if info.alpha_used else "DXT1Crunched", 60, mips=world, sprite=ui_only,
                           npot_none=ui_only or keep_aspect, cap_source="full-screen image 2048" if fs else "image 1024")
        if k == "fx":
            r = self.surface("FX", suffix, info, self.fx_cap, f"FX {self.fx_cap}", self.Q_DATA, self.Q_DATA)
            if suffix == "E":
                r.max, r.cap_source, r.quality = self.fx_cap, f"FX {self.fx_cap}", self.Q_FX_E
            if self._contains(file, "LensDirt") or self._contains(file, "_Screen"):
                r.mips = False
            return r
        return self.surface("Textures/" + (kind or "?"), suffix, info, 1024, "class default (unknown kind)", self.Q_BC, self.Q_DATA)

    def _cards(self, r, file):
        if self._contains(file, "Impostor") and r.max > self.impostor_cap:
            r.max, r.cap_source = self.impostor_cap, f"impostor {self.impostor_cap}"
        return r


# ----------------------------------------------------------------------------------------------- FBX / OBJ


class FbxError(Exception):
    pass


def _fbx_binary_counts(data: bytes):
    """Returns (models: {model_id: name}, geoms: {geom_id: tris}, conns: [(child, parent)])."""
    if not data.startswith(b"Kaydara FBX Binary"):
        raise FbxError("not binary")
    version = struct.unpack("<I", data[23:27])[0]
    wide = version >= 7500
    hdr = struct.Struct("<QQQ" if wide else "<III")
    null_len = hdr.size + 1

    models, geoms, conns = {}, {}, []

    def read_props(o, n):
        props = []
        for _ in range(n):
            t = data[o:o + 1]
            o += 1
            if t == b"Y":
                props.append(struct.unpack_from("<h", data, o)[0]); o += 2
            elif t == b"C":
                props.append(data[o] != 0); o += 1
            elif t == b"I":
                props.append(struct.unpack_from("<i", data, o)[0]); o += 4
            elif t == b"F":
                props.append(struct.unpack_from("<f", data, o)[0]); o += 4
            elif t == b"D":
                props.append(struct.unpack_from("<d", data, o)[0]); o += 8
            elif t == b"L":
                props.append(struct.unpack_from("<q", data, o)[0]); o += 8
            elif t in (b"f", b"d", b"l", b"i", b"b"):
                alen, enc, clen = struct.unpack_from("<III", data, o)
                o += 12
                props.append(("array", t, alen, enc, o, clen))
                o += clen
            elif t in (b"S", b"R"):
                ln = struct.unpack_from("<I", data, o)[0]
                o += 4
                props.append(data[o:o + ln] if t == b"R" else data[o:o + ln].decode("utf-8", "replace"))
                o += ln
            else:
                raise FbxError(f"bad property type {t!r} at {o}")
        return props

    def decode_int_array(p):
        _, t, alen, enc, off, clen = p
        raw = data[off:off + clen]
        if enc == 1:
            raw = zlib.decompress(raw)
        if t != b"i":
            raise FbxError("PolygonVertexIndex is not int32")
        return struct.unpack(f"<{alen}i", raw[:alen * 4])

    def walk(o, end, path):
        while o < end:
            end_off, nprops, plen = hdr.unpack_from(data, o)
            if end_off == 0:
                return o + null_len
            nlen = data[o + hdr.size]
            name = data[o + hdr.size + 1:o + hdr.size + 1 + nlen].decode("ascii", "replace")
            po = o + hdr.size + 1 + nlen
            interesting = (path == ("Objects",) and name in ("Model", "Geometry")) or (path == ("Connections",) and name == "C") \
                or (len(path) == 2 and path[0] == "Objects" and path[1].startswith("Geometry:") and name == "PolygonVertexIndex")
            props = read_props(po, nprops) if interesting else None
            child_o = po + plen
            child_path = path + (name,)
            if path == ("Objects",) and name == "Geometry" and props:
                gid, kind = props[0], props[2] if len(props) > 2 else ""
                child_path = ("Objects", f"Geometry:{gid}:{kind}")
                geoms.setdefault(gid, 0)
            if path == ("Objects",) and name == "Model" and props:
                mname = props[1].split("\x00")[0] if isinstance(props[1], str) else str(props[1])
                models[props[0]] = (mname, props[2] if len(props) > 2 else "")
            if path == ("Connections",) and name == "C" and props and props[0] in ("OO", "OP"):
                conns.append((props[1], props[2]))
            if name == "PolygonVertexIndex" and props and len(path) == 2:
                _, gid, kind = path[1].split(":", 2)
                if kind == "Mesh":
                    idx = decode_int_array(props[0])
                    tris, n = 0, 0
                    for v in idx:
                        n += 1
                        if v < 0:
                            tris += max(n - 2, 0)
                            n = 0
                    geoms[int(gid)] = tris
            if child_o < end_off and name in ("Objects", "Connections", "Geometry"):
                walk(child_o, end_off, child_path)
            o = end_off
        return o

    walk(27, len(data), ())
    return models, geoms, conns


_ascii_geom = re.compile(r'Geometry:\s*(-?\d+),\s*"Geometry::([^"]*)",\s*"(\w+)"\s*\{')
_ascii_model = re.compile(r'Model:\s*(-?\d+),\s*"Model::([^"]*)",\s*"(\w+)"')
_ascii_conn = re.compile(r'C:\s*"O[OP]",\s*(-?\d+),\s*(-?\d+)')
_ascii_pvi = re.compile(r'PolygonVertexIndex:\s*\*\d+\s*\{\s*a:\s*([-\d,\s]+)\}')


def _fbx_ascii_counts(text: str):
    models, geoms, conns = {}, {}, []
    for m in _ascii_model.finditer(text):
        models[int(m.group(1))] = (m.group(2), m.group(3))
    for m in _ascii_conn.finditer(text):
        conns.append((int(m.group(1)), int(m.group(2))))
    for m in _ascii_geom.finditer(text):
        gid, kind = int(m.group(1)), m.group(3)
        if kind != "Mesh":
            continue
        pm = _ascii_pvi.search(text, m.end())
        tris = 0
        if pm:
            n = 0
            for tok in pm.group(1).split(","):
                tok = tok.strip()
                if not tok:
                    continue
                n += 1
                if int(tok) < 0:
                    tris += max(n - 2, 0)
                    n = 0
        geoms[gid] = tris
    return models, geoms, conns


_lod_rx = re.compile(r"_LOD(\d+)$", re.I)


def model_tris(path: Path):
    """Returns dict(total, lod0, lods={index: tris}, meshes=[(name, tris)]) or raises FbxError."""
    ext = path.suffix.lower()
    if ext == ".obj":
        meshes = defaultdict(int)
        cur = "default"
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                if line.startswith(("o ", "g ")):
                    cur = line[2:].strip() or cur
                elif line.startswith("f "):
                    n = len(line.split()) - 1
                    meshes[cur] += max(n - 2, 0)
        items = list(meshes.items())
    else:
        data = path.read_bytes()
        if data.startswith(b"Kaydara FBX Binary"):
            models, geoms, conns = _fbx_binary_counts(data)
        else:
            models, geoms, conns = _fbx_ascii_counts(data.decode("utf-8", "replace"))
        items = []
        used = set()
        for child, parent in conns:
            if child in geoms and parent in models:
                items.append((models[parent][0], geoms[child]))
                used.add(child)
        for gid, tris in geoms.items():  # geometry without a model connection
            if gid not in used:
                items.append((f"geometry {gid}", tris))
    lods = defaultdict(int)
    for name, tris in items:
        m = _lod_rx.search(name)
        lods[int(m.group(1)) if m else 0] += tris
    return {"total": sum(t for _, t in items), "lod0": lods.get(0, 0), "lods": dict(lods), "meshes": items}


# ----------------------------------------------------------------------------------------------- audio


def probe_wav(path: Path):
    """Returns dict(rate, channels, bits, frames, seconds, smpl) using the RIFF chunks (no decode)."""
    with open(path, "rb") as f:
        head = f.read(12)
        if len(head) < 12 or head[:4] != b"RIFF" or head[8:12] != b"WAVE":
            raise ValueError("not a RIFF/WAVE file")
        info = {"smpl": False}
        while True:
            ch = f.read(8)
            if len(ch) < 8:
                break
            cid, size = ch[:4], struct.unpack("<I", ch[4:])[0]
            if cid == b"fmt ":
                fmt = f.read(size)
                tag, chans, rate, _, align, bits = struct.unpack("<HHIIHH", fmt[:16])
                info.update(tag=tag, channels=chans, rate=rate, bits=bits, align=align)
            elif cid == b"data":
                info["data_bytes"] = size
                f.seek(size + (size & 1), 1)
            elif cid == b"smpl":
                info["smpl"] = True
                f.seek(size + (size & 1), 1)
            else:
                f.seek(size + (size & 1), 1)
        if "rate" not in info or "data_bytes" not in info:
            raise ValueError("missing fmt or data chunk")
        info["frames"] = info["data_bytes"] // max(info["align"], 1)
        info["seconds"] = info["frames"] / float(info["rate"])
        return info


# ----------------------------------------------------------------------------------------------- collection


def collect(args) -> list:
    items = []
    seen = set()

    def add_tree(base: Path, origin: str, target_prefix: str):
        if not base.exists():
            return
        for dirpath, _, files in os.walk(base):
            for fn in files:
                if fn.endswith(".meta") or fn.startswith("."):
                    continue
                full = Path(dirpath) / fn
                rel = full.relative_to(base).as_posix()
                target = target_prefix + rel
                key = (target, origin)
                if key in seen:
                    continue
                seen.add(key)
                items.append(Item(target, full, origin))

    if args.path:
        for p in args.path:
            full = (ROOT / p).resolve()
            if not full.exists():
                print(f"validate_assets: path not found: {p}", file=sys.stderr)
                sys.exit(2)
            rel = full.relative_to(ROOT).as_posix()
            m = re.search(r"/ready/(Assets/.*)$", rel)
            if full.is_file():
                target = m.group(1) if m else rel
                items.append(Item(target, full, "path"))
            else:
                prefix = (m.group(1) + "/") if m else rel + "/"
                add_tree(full, "path", prefix)
        return items
    if args.ready is not None:
        wps = args.ready or sorted(p.name for p in (ROOT / "RawAssets").iterdir() if (p / "ready/Assets").is_dir())
        for wp in wps:
            base = ROOT / "RawAssets" / wp / "ready" / "Assets"
            if not base.is_dir():
                print(f"validate_assets: no ready tree at RawAssets/{wp}/ready/Assets", file=sys.stderr)
                continue
            add_tree(base, f"ready:{wp}", "Assets/")
        return items
    add_tree(ROOT / "Assets/_Game", "assets", "Assets/_Game/")
    add_tree(ROOT / "Assets/StreamingAssets/video", "assets", "Assets/StreamingAssets/video/")
    return items


# ----------------------------------------------------------------------------------------------- checks


def budget_line_for(target: str):
    t = target.lower()
    if t.startswith("assets/streamingassets/video/") or "/art/textures/images/" in t:
        return "Images and video"
    if "/audio/" in t:
        return "Audio"
    if "/art/animation/" in t:
        return "Animation"
    if "/art/hdri/runtime/" in t or "/art/textures/env/" in t or "/art/textures/decals/" in t:
        return "Environment textures"
    for kind, line in (("props", "Props"), ("vegetation", "Props"), ("vehicles", "Vehicles"), ("weapons", "Weapons"),
                       ("characters", "Characters")):
        if f"/art/textures/{kind}/" in t or f"/art/models/{kind}/" in t:
            return line
    if any(f"/art/models/{k}/" in t for k in ("kit", "terrain", "backdrop")):
        return "Kit, buildings, terrain, far proxies"
    if "/art/textures/fx/" in t or "/ui/" in t or "/art/fonts/" in t:
        return "Shared shaders + UI + FX textures"
    return None


def texture_bytes(rule: TexRule, info: TexInfo):
    if rule.editor_only or not info.known:
        return 0.0
    w, h = info.width, info.height
    if not rule.npot_none:  # NPOT -> nearest power of two (Unity "ToNearest")
        w, h = _nearest_pot(w), _nearest_pot(h)
    scale = min(1.0, rule.max / max(w, h)) if max(w, h) > 0 else 1.0
    w, h = max(1, int(w * scale)), max(1, int(h * scale))
    bpp = (BUDGETS.get("download", {}).get("bytes_per_pixel_with_mips") or {}).get(rule.fmt, 1.0)
    if not rule.mips:
        bpp *= 0.75
    q = BUDGETS.get("download", {}).get("crunch_quality_factor", {"base_quality": 50, "per_quality_point": 0.005})
    if "Crunched" in rule.fmt and rule.quality >= 0:
        bpp *= 1.0 + (rule.quality - q.get("base_quality", 50)) * q.get("per_quality_point", 0.005)
    return w * h * bpp


def _nearest_pot(v):
    if v <= 0:
        return v
    nxt = 1
    while nxt < v:
        nxt <<= 1
    prev = nxt >> 1
    return prev if prev and (v - prev) < (nxt - v) else nxt


class Validator:
    def __init__(self, args):
        self.args = args
        self.manifest = Manifest()
        self.rules = TextureRulesMirror(self.manifest)
        self.report = Report()
        self.dialogue_ids = {l["id"] for l in (load_json(DIALOGUE_PATH, []) or []) if isinstance(l, dict) and "id" in l}
        self.audio_cfg = BUDGETS.get("audio", {})
        self.model_cfg = BUDGETS.get("model", {})
        self.tex_sets = defaultdict(dict)  # (folder target, set) -> {suffix: item}

    # ---- textures
    def check_texture(self, it: Item):
        r = self.report
        t = it.target
        rel = t[len(GAME):] if t.startswith(GAME) else t
        parts = rel.split("/")
        file = parts[-1]
        ext = Path(file).suffix.lower()
        kind = parts[2].lower() if rel.lower().startswith("art/textures/") and len(parts) > 3 else ""
        info = probe_texture(it.source)
        r.counts["textures"] += 1
        if ext in (".psd", ".tif", ".tiff"):
            r.add("warning", "TEX_FORMAT", t, f"{ext} source: export PNG/TGA (the validator cannot read it; Unity can)")
            return
        if not info.known:
            r.add("error", "TEX_UNREADABLE", t, "image header could not be read")
            return
        suffix, set_name = parse_suffix(file)
        if kind in SURFACE_KINDS:
            stem = Path(file).stem
            if not stem.startswith("T_") or suffix is None:
                r.add("error", "TEX_NAMING", t, "surface textures must be named T_<Name>_<BC|N|M|E|H|A> (TECH_PLAN §4.2)")
            else:
                folder = "/".join(parts[:-1])
                self.tex_sets[(folder, set_name)][suffix] = it
        elif kind == "fx" and not Path(file).stem.startswith("T_"):
            r.add("warning", "TEX_NAMING", t, "FX textures should be named T_<Name>[_<Suffix>]")

        rule = self.rules.resolve(t, info)
        if rule is None:
            if t.startswith(GAME + "Art/") or t.startswith(GAME + "UI/Sprites/"):
                r.add("info", "TEX_NO_RULE", t, "no import rule manages this folder (Unity defaults apply)")
            return

        # Pixel checks (optional)
        stats = None
        colour = suffix in (None, "BC", "E")
        if self.args.pixels and ext in (".png", ".tga", ".jpg", ".jpeg"):
            stats = pixel_stats(it.source, need_alpha_scan=colour and info.has_alpha)
            if stats and "alpha_min" in stats:
                info.alpha_used = stats["alpha_min"] < 250
                info.alpha_scanned = True
                rule = self.rules.resolve(t, info)

        pot = (info.width & (info.width - 1)) == 0 and (info.height & (info.height - 1)) == 0
        if not pot and not rule.npot_none and not rule.editor_only and "Crunched" in rule.fmt:
            r.add("info", "TEX_NPOT", t, f"{info.width}x{info.height} is not a power of two: Unity resamples it to "
                  f"{_nearest_pot(info.width)}x{_nearest_pot(info.height)} before crunching (UVs stay valid; extra "
                  "memory and a resampling blur)")
        if rule.npot_none and rule.mips:
            r.add("error", "TEX_NPOT_MIPS", t, "NPOT None with mips: Unity imports such textures uncompressed (RGB24)")
        if rule.fmt == "RGBA32" and not rule.editor_only and info.max_side > 128:
            big = texture_bytes(rule, info) > 256 * 1024
            r.add("warning" if big else "info", "TEX_UNCOMPRESSED", t, f"{info.width}x{info.height} sprite is not a multiple of 4 "
                  f"after scaling to {rule.max}: it imports uncompressed RGBA32 (~{texture_bytes(rule, info) / 1024:.0f} KB); "
                  "resize the source to multiples of 4")
        in_game = min(rule.max, to_max_size(info.max_side)) if info.max_side else rule.max
        if info.max_side > 2 * rule.max and not rule.editor_only:
            r.add("info", "TEX_OVERSIZE_SOURCE", t, f"source {info.width}x{info.height} is imported at <= {rule.max} "
                  f"({rule.cap_source}); a smaller source saves repository space and import time")
        if suffix == "M" and kind in SURFACE_KINDS and not info.has_alpha:
            r.add("error", "TEX_MASK_NO_ALPHA", t, "D4 mask without an alpha channel: smoothness reads 1.0 (mirror-like); "
                  "pack A = 1 - roughness")
        if suffix == "N" and info.has_alpha and kind in SURFACE_KINDS:
            r.add("info", "TEX_NORMAL_ALPHA", t, "normal map has an alpha channel (ignored; DXT5nm uses its own layout)")
        if stats and "mean" in stats and kind in SURFACE_KINDS | {"fx"}:
            mr, mg, mb = stats["mean"]
            if suffix == "N" and (mb < 0.55 or abs(mr - 0.5) > 0.15 or abs(mg - 0.5) > 0.15):
                r.add("warning", "TEX_NORMAL_SUSPECT", t, f"mean RGB {mr:.2f}/{mg:.2f}/{mb:.2f} does not look like a "
                      "tangent-space normal map (expected ~0.5/0.5/>0.7)")
            if suffix == "M" and kind in SURFACE_KINDS and kind != "decals":
                ao = stats.get("median", stats["mean"])[1]
                if ao < 0.4:
                    # Tiling env sets cover the whole texture; prop/vehicle/character atlases have unused space (often
                    # baked black), so for them this is only a hint.
                    r.add("warning" if kind == "env" else "info", "TEX_MASK_AO_DARK", t,
                          f"mask G (AO) median {ao:.2f} is very dark (ambient light will crush the surface"
                          + ("; WP10 rescales large tiling sets below a 0.65 mean)" if kind == "env" else "; unused atlas space counts too)"))
                if stats.get("alpha_mean", 0) > 0.9 and kind in ("env", "props", "vegetation", "characters"):
                    r.add("info", "TEX_MASK_GLOSSY", t, f"mask A (smoothness) mean {stats['alpha_mean']:.2f}: very glossy for "
                          "a dry, dusty setting")
            if suffix == "BC" and kind in ("env", "props", "vehicles", "characters", "weapons", "vegetation"):
                lum = 0.2126 * mr + 0.7152 * mg + 0.0722 * mb
                if lum > 0.9 or (lum < 0.03 and stats.get("alpha_mean", 1) > 0.5):
                    r.add("warning", "TEX_ALBEDO_RANGE", t, f"base colour mean luminance {lum:.2f} (sRGB) is outside the "
                          "plausible PBR albedo range (~0.03-0.9)")

        line = budget_line_for(t)
        if line and not self.never_referenced(kind, suffix, file):
            r.estimates.append(Estimate(line, texture_bytes(rule, info), "texture"))

    @staticmethod
    def never_referenced(kind, suffix, file):
        """Maps that MaterialFactory never assigns, so they only ship if a custom shader references them."""
        if suffix == "A" and kind in SURFACE_KINDS:
            return True  # aux authoring masks (URP/Lit has no slot)
        if suffix in ("M", "H") and kind == "decals":
            return True  # MAOS/height of decals: the default URP decal graph ignores them
        return False

    def finish_texture_sets(self):
        r = self.report
        for (folder, name), maps in sorted(self.tex_sets.items()):
            fparts = folder.split("/")  # "Art/Textures/<Kind>/<set folder>"
            kind = fparts[2].lower() if len(fparts) > 2 else ""
            low = name.lower()
            card = any(k in low for k in ("impostor", "blur", "leaves", "cards", "grass", "decal_"))
            shared = "/_shared" in folder.lower() or "/fabrics" in folder.lower()
            if "BC" not in maps:
                if not shared and kind != "decals":
                    for suf, it in maps.items():
                        if suf in ("N", "M", "E", "A"):
                            r.add("warning", "TEX_ORPHAN", it.target, f"no T_{name}_BC next to this map: no material will be "
                                  "built for it (MaterialFactory builds sets with a base colour)")
                            break
                continue
            if shared or card or kind in ("decals",):
                continue
            missing = [s for s in ("N", "M") if s not in maps]
            if missing and kind in ("env", "props", "weapons", "characters", "vehicles", "vegetation"):
                sev = "warning" if "M" in missing else "info"
                r.add(sev, "TEX_SET_INCOMPLETE", maps["BC"].target, f"set {name} has no {'/'.join('_' + m for m in missing)}"
                      + (" (material falls back to metallic 0, smoothness 0.3, no AO)" if "M" in missing else ""))

    # ---- models
    def model_budget(self, target: str, stem: str):
        """(tris cap, source) for a model file."""
        folder = target.rsplit("/", 1)[0].lower() + "/"
        entries = self.manifest.folder_entries().get(folder, [])
        su = stem.upper()
        pick = None
        if entries:
            def score(e):
                eid = e["id"].upper()
                s = 0
                if su.endswith("_MAG") or "_MAG_" in su:
                    s += 10 if eid.endswith("_MAG") else -10
                if "_ROCKET" in su:
                    s += 10 if eid.endswith("_ROCKET") else -10
                if eid.endswith("_MAG") and "_MAG" not in su:
                    s -= 5
                return s
            pick = max(entries, key=score)
        is_fp = _has_token(stem, "FP")
        is_world = _has_token(stem, "W") or _has_token(stem, "World")
        if pick is not None:
            b = pick.get("budget") or ""
            fp_m = re.search(r"FP:?\s*([\d.]+)\s*(k?)\s*tris", b, re.I)
            w_m = re.search(r"world:?\s*([\d.]+)\s*(k?)\s*tris", b, re.I)
            def val(m):
                return int(float(m.group(1)) * (1000 if m.group(2).lower() == "k" else 1))
            if is_fp and fp_m:
                return val(fp_m), f"manifest {pick['id']} (FP)"
            if (is_world or not is_fp) and w_m:
                return val(w_m), f"manifest {pick['id']} (world)"
            m = re.search(r"([\d.]+)\s*(k?)\s*-\s*([\d.]+)\s*(k?)\s*tris", b, re.I)
            if m and "per piece" not in b and "per building" not in b:
                return int(float(m.group(3)) * (1000 if m.group(4).lower() == "k" else 1)), f"manifest {pick['id']}"
            m = re.search(r"<=?\s*([\d.]+)\s*(k?)\s*tris", b, re.I) or re.search(r"([\d.]+)\s*(k?)\s*tris", b, re.I)
            if m and "per piece" not in b and "per building" not in b and "each" not in b:
                return int(float(m.group(1)) * (1000 if m.group(2).lower() == "k" else 1)), f"manifest {pick['id']}"
        caps = self.model_cfg.get("class_tris", {})
        t = target.lower()
        if "/art/models/weapons/" in t:
            if "_mag" in t:
                return caps.get("fp_magazine", 3000), "class fp_magazine (§8.9)"
            return (caps.get("fp_weapon", 20000), "class fp_weapon (§8.9)") if is_fp else (caps.get("world_weapon", 7000), "class world_weapon (§8.9)")
        if "/art/models/characters/props/" in t:
            return caps.get("character_prop", 3000), "class character_prop (§8.9)"
        if "/art/models/characters/" in t:
            if "/calder/" in t:
                return caps.get("calder", 15400), "class calder (§8.9)"
            return caps.get("squad_character", 15000), "class character (§8.9)"
        if "/art/models/vehicles/" in t:
            return caps.get("vehicle", 15000), "class vehicle (§8.9)"
        if "/art/models/vegetation/" in t:
            return caps.get("tree", 8000), "class tree (§8.9)"
        if "/art/models/backdrop/" in t or "/art/models/terrain/" in t:
            return caps.get("backdrop", 150000), "class far proxy (§8.9)"
        if "/art/models/props/" in t:
            return caps.get("prop", 8000), "class medium/large prop (§8.9)"
        return None, None

    def check_model(self, it: Item):
        r = self.report
        t = it.target
        stem = Path(t).stem
        clip = "/art/animation/" in t.lower()
        r.counts["clips" if clip else "models"] += 1
        if clip:
            if not stem.startswith("A_"):
                r.add("warning", "MDL_NAMING", t, "animation clips are named A_<Source>_<Name> (TECH_PLAN §4.2)")
            r.estimates.append(Estimate("Animation", 0.1e6, "clip"))  # §8.9: <= 0.1 MB per humanoid clip on average
            return
        if not (stem.startswith("SM_") or stem.startswith("SK_")):
            r.add("warning", "MDL_NAMING", t, "meshes are named SM_<Name> (static) or SK_<Name> (skinned) (TECH_PLAN §4.2)")
        try:
            counts = model_tris(it.source)
        except (FbxError, struct.error, zlib.error, ValueError, OSError) as e:
            r.add("warning", "MDL_UNREADABLE", t, f"triangle count unavailable ({e})")
            return
        if counts["total"] == 0:
            r.add("error", "MDL_NO_TRIS", t, "no triangles found")
            return
        lods = sorted(counts["lods"])
        if len(lods) > 1 and lods != list(range(len(lods))):
            r.add("warning", "MDL_LOD_GAP", t, f"LOD levels {lods} are not contiguous (Unity builds the LODGroup from _LOD0.._LODn)")
        cap, src = self.model_budget(t, stem)
        if cap and "/art/models/characters/" in t.lower() and "/art/models/characters/props/" not in t.lower():
            # Character manifest numbers describe the source avatar ("7.8k tris"); the §8.9 class row is the budget.
            cls_cap = self.model_cfg.get("class_tris", {}).get("calder" if "/calder/" in t.lower() else "squad_character", 15000)
            if cap < cls_cap:
                cap, src = cls_cap, src + " -> class character (§8.9)"
        lod0 = counts["lod0"]
        tol = self.model_cfg.get("tris_tolerance_error", 1.25)
        if cap:
            if lod0 > cap * tol:
                r.add("error", "MDL_TRIS", t, f"LOD0 {lod0} tris > {cap} ({src}) by more than {int((tol - 1) * 100)} %")
            elif lod0 > cap:
                r.add("warning", "MDL_TRIS", t, f"LOD0 {lod0} tris > {cap} ({src})")
        r.counts["tris_lod0"] += lod0
        line = budget_line_for(t)
        if line:
            fp = "/art/models/characters/" in t.lower() or (_has_token(stem, "FP") or _has_token(stem, "Mag"))
            per_tri = (self.args_download("mesh_bytes_per_tri") or {}).get("Off" if fp else "Medium", 20)
            r.estimates.append(Estimate(line, counts["total"] * per_tri, "mesh"))

    def args_download(self, key):
        return BUDGETS.get("download", {}).get(key)

    # ---- audio
    def sfx_manifest_seconds(self, stem: str):
        cand = re.sub(r"_\d+$", "", stem)
        for c in (cand, re.sub(r"_FAR$", "", cand), re.sub(r"_(A|B)$", "", cand)):
            e = self.manifest.by_id.get(c)
            if e:
                b = e.get("budget") or ""
                m = re.search(r"([\d.]+)\s*-\s*([\d.]+)\s*s\b", b) or re.search(r"([\d.]+)\s*s\b", b)
                if m:
                    return float(m.groups()[-1]), e["id"]
        return None, None

    def check_audio(self, it: Item):
        r = self.report
        t = it.target
        rel = t[len(GAME):] if t.startswith(GAME) else t
        parts = rel.split("/")
        file = parts[-1]
        stem = Path(file).stem
        ext = Path(file).suffix.lower()
        r.counts["audio"] += 1
        if ext != ".wav":
            r.add("warning", "AUD_FORMAT", t, f"{ext}: ship 16/24-bit PCM WAV sources (Unity re-encodes to AAC for WebGL)")
            return
        try:
            w = probe_wav(it.source)
        except (ValueError, OSError, struct.error) as e:
            r.add("error", "AUD_UNREADABLE", t, f"not a readable WAV ({e})")
            return
        if w.get("tag") not in (1, 3, 0xFFFE):
            r.add("warning", "AUD_FORMAT", t, f"WAV format tag {w.get('tag')} (expected PCM)")
        cfg = self.audio_cfg
        group = parts[2] if len(parts) > 3 else ""
        mono_rule = True
        sec = w["seconds"]
        if rel.startswith("Audio/SFX/"):
            known = cfg.get("one_shot_groups", []) + cfg.get("loop_groups", []) + cfg.get("other_sfx_groups", [])
            if len(parts) < 4:
                r.add("error", "AUD_FOLDER", t, "SFX must sit in a group folder Audio/SFX/<Group>/ (§3.12)")
            elif not in_list(known, group):
                r.add("error", "AUD_FOLDER", t, f"unknown SFX group '{group}': no import rule (§3.12 groups: {', '.join(known)})")
            budget_s, eid = self.sfx_manifest_seconds(stem)
            if budget_s and (_has_token(stem, "FAR") or "/far/" in t.lower()):
                budget_s *= 1.5  # §8.7: _far variants carry a longer baked echo tail
            if budget_s:
                if sec > budget_s * 1.25 + 0.25:
                    r.add("warning", "AUD_DURATION", t, f"{sec:.2f} s > {budget_s:g} s budget of {eid}")
            elif in_list(cfg.get("one_shot_groups", []), group):
                limit = cfg.get("one_shot_max_seconds", 2.0)
                long_ok = any(k in stem.upper() for k in cfg.get("one_shot_long_names", []))
                if sec > limit and not long_ok:
                    r.add("info", "AUD_DURATION", t, f"one-shot {sec:.2f} s > {limit:g} s (§8.9)")
        elif rel.startswith("Audio/VO/"):
            chains = cfg.get("narrow_vo_chains", []) + cfg.get("wide_vo_chains", [])
            chain = group
            if "TOKEN" in stem.upper():
                r.add("error", "AUD_TOKENS", t, "Clockmaker tokens never enter Assets/ (they stay in RawAssets/audio/vo/tokens/, §3.12)")
            if len(parts) < 4 or not in_list(chains, chain):
                r.add("error", "AUD_FOLDER", t, f"unknown VO chain folder '{chain}' (§3.12: {', '.join(chains)})")
            else:
                m = re.match(r"^VO_(.+)_" + re.escape(chain) + r"(?:_(A|B))?$", stem)
                if not m:
                    r.add("error", "AUD_VO_NAME", t, f"VO files are named VO_<LineId>_{chain}[_A|_B].wav (folder = chain)")
                elif self.dialogue_ids and m.group(1) not in self.dialogue_ids:
                    r.add("warning", "AUD_VO_LINE", t, f"line id {m.group(1)} is not in Docs/DIALOGUE.json")
            if sec > cfg.get("vo_line_max_seconds", 30.0):
                r.add("warning", "AUD_DURATION", t, f"VO line {sec:.1f} s is unusually long")
        elif rel.startswith("Audio/Music/"):
            mono_rule = False
            lo, hi = cfg.get("music_seconds", [2.0, 95.0])
            if not (lo <= sec <= hi):
                r.add("warning", "AUD_DURATION", t, f"music cue {sec:.1f} s outside {lo:g}-{hi:g} s (§8.9)")
        elif rel.startswith("Audio/Ambience/"):
            mono_rule = False
            lo, hi = cfg.get("ambience_seconds", [8.0, 35.0])
            if not (lo <= sec <= hi):
                r.add("warning", "AUD_DURATION", t, f"ambience loop {sec:.1f} s outside {lo:g}-{hi:g} s (§8.9)")
        else:
            r.add("warning", "AUD_FOLDER", t, "audio outside Audio/{SFX,VO,Music,Ambience}: no import rule")
        if mono_rule and w["channels"] > 1:
            r.add("info", "AUD_CHANNELS", t, f"{w['channels']} channels: Force To Mono downmixes it (ship mono to save repository space)")
        if w["rate"] < 22050:
            r.add("warning", "AUD_RATE", t, f"{w['rate']} Hz source is below 22.05 kHz (audible loss)")
        out_ch = 1 if mono_rule else min(w["channels"], 2)
        q = self._audio_quality(rel)
        kbps_map = cfg.get("aac_kbps_per_channel", {"60": 64})
        kbps = kbps_map.get(str(q)) or kbps_map.get(min(kbps_map, key=lambda k: abs(int(k) - q)))
        r.estimates.append(Estimate("Audio", sec * out_ch * kbps * 1000 / 8.0, "audio"))

    @staticmethod
    def _audio_quality(rel):
        if rel.startswith("Audio/Ambience/"):
            return 50
        if rel.startswith("Audio/Music/"):
            return 60
        if rel.startswith("Audio/VO/"):
            chain = rel.split("/")[2] if rel.count("/") >= 3 else ""
            return 60 if in_list(BUDGETS.get("audio", {}).get("narrow_vo_chains", []), chain) else 70
        group = rel.split("/")[2] if rel.count("/") >= 3 else ""
        if in_list(BUDGETS.get("audio", {}).get("one_shot_groups", []), group):
            return 70
        return 60

    # ---- other
    def check_other(self, it: Item):
        t = it.target.lower()
        if t.startswith("assets/streamingassets/video/") and t.endswith(".mp4"):
            size = it.source.stat().st_size
            self.report.estimates.append(Estimate("Images and video", size, "video"))
            if size > 14_000_000:
                self.report.add("error", "VID_SIZE", it.target, f"{size / 1e6:.1f} MB > 14 MB per file (host limit, §8.8)")

    def run(self, items):
        for it in items:
            ext = Path(it.target).suffix.lower()
            if ext in TEXTURE_EXT:
                self.check_texture(it)
            elif ext in MODEL_EXT:
                self.check_model(it)
            elif ext in AUDIO_EXT:
                self.check_audio(it)
            else:
                self.check_other(it)
        self.finish_texture_sets()
        return self.report


# ----------------------------------------------------------------------------------------------- material specs


def spec_files():
    """Same discovery as MaterialFactory.SpecFiles: JSON named *material* under RawAssets/*/ready (outside ready/Assets)
    plus Assets/_Game/Data/Materials/*.json."""
    out = []
    raw = ROOT / "RawAssets"
    if raw.is_dir():
        for wp in sorted(raw.iterdir()):
            ready = wp / "ready"
            if not ready.is_dir():
                continue
            for dp, _, fs in os.walk(ready):
                if (Path(dp) / "x").as_posix().startswith((ready / "Assets").as_posix() + "/"):
                    continue
                out += [Path(dp) / f for f in fs if f.endswith(".json") and "material" in f.lower()]
    proj = ROOT / "Assets/_Game/Data/Materials"
    if proj.is_dir():
        out += sorted(proj.rglob("*.json"))
    return sorted(out)


def check_specs(report: Report):
    """Every texture a material spec row references must exist in Assets/ or in some ready tree."""
    raw = ROOT / "RawAssets"
    ready_roots = [p / "ready" for p in raw.iterdir() if (p / "ready").is_dir()] if raw.is_dir() else []
    n = 0
    for f in spec_files():
        rel = f.relative_to(ROOT).as_posix()
        try:
            d = json.loads(f.read_text(encoding="utf-8"))
        except (ValueError, OSError) as e:
            report.add("error", "SPEC_UNREADABLE", rel, f"material spec file cannot be parsed ({e})")
            continue
        rows = d if isinstance(d, list) else (d.get("materials") if isinstance(d, dict) else None)
        if not isinstance(rows, list):
            report.add("error", "SPEC_UNREADABLE", rel, "expected a JSON array of rows or {\"materials\": [...]}")
            continue
        for r in rows:
            if not isinstance(r, dict):
                continue
            n += 1
            name = r.get("material") or r.get("target_path") or "?"
            if not r.get("target_path"):
                report.add("error", "SPEC_ROW", rel, f"{name}: row without target_path")
            refs = [v for v in (r.get("textures") or {}).values() if isinstance(v, str) and v]
            em = r.get("emission")
            if isinstance(em, dict) and isinstance(em.get("_EmissionMap"), str):
                refs.append(em["_EmissionMap"])
            for key in ("terrain_layer", "detail"):
                o = r.get(key)
                if isinstance(o, dict):
                    refs += [o[k] for k in ("diffuse", "normal", "mask", "albedo") if isinstance(o.get(k), str) and o.get(k)]
            for p in refs:
                if not (ROOT / p).exists() and not any((rr / p).exists() for rr in ready_roots):
                    report.add("error", "SPEC_MISSING_TEXTURE", rel, f"{name}: {p} exists neither in Assets/ nor in a ready tree")
    report.counts["spec_rows"] += n


# ----------------------------------------------------------------------------------------------- coverage


def coverage(manifest: Manifest):
    """Per manifest entry: 'landed' (Assets/), 'ready' (RawAssets/<wp>/ready/), 'partial', '~' suffix = renamed, or None."""
    raw = ROOT / "RawAssets"
    ready_roots = [p / "ready" for p in sorted(raw.iterdir()) if (p / "ready").is_dir()] if raw.is_dir() else []
    bases = [("landed", ROOT)] + [("ready", rr) for rr in ready_roots]
    shared = Counter((e.get("target_path") or "").lower() for e in manifest.entries if (e.get("target_path") or "").endswith("/"))

    def norm(s):
        return re.sub(r"[^a-z0-9]", "", s.lower())

    listing_cache = {}

    def files_in(folder: Path, recursive=False):
        key = (str(folder), recursive)
        if key not in listing_cache:
            names = []
            if folder.is_dir():
                if recursive:
                    for dp, _, fs in os.walk(folder):
                        names += [f for f in fs if not f.endswith(".meta") and not f.startswith(".")]
                else:
                    names = [f for f in os.listdir(folder) if not f.endswith(".meta") and not f.startswith(".") and (folder / f).is_file()]
            listing_cache[key] = names
        return listing_cache[key]

    generic = {"tex", "acg", "vfx", "flipbook", "set", "atlas", "cards", "ph", "fal", "proc", "img", "ui", "dcl", "veg", "chr",
               "chp", "anim", "veh", "hdri", "01", "02", "a", "b", "c"}

    def tokens_match(eid, fname):
        toks = [t for t in eid.lower().split("_") if t and t not in generic]
        fn = norm(fname)
        return bool(toks) and all(t in fn for t in toks)

    def id_tail(eid):
        return norm(re.sub(r"^(IMG|TEX|TEX_ACG|DCL|UI|VEG|PH|FAL|PROC|CHR|CHP|ANIM|VEH)_", "", eid))

    def find(target: str, e):
        variants = [target]
        if "{A|B}" in target:
            variants = [target.replace("{A|B}", "A"), target.replace("{A|B}", "B")]
        eid = e["id"]
        for status, base in bases:
            for v in variants:
                p = base / v
                if v.endswith("/"):
                    names = files_in(p, recursive=True)
                    if not names:
                        continue
                    if e["category"] in ("sfx", "music"):
                        if any(norm(f).startswith(norm(eid)) for f in names):
                            return status
                    elif "/art/animation/" in v.lower():
                        return status  # clip folders are shared by several sets; any clip counts
                    elif shared[v.lower()] > 1:
                        if any(tokens_match(eid, f) for f in names):
                            return status
                    else:
                        return status
                elif p.is_file():
                    return status
        if not target.endswith("/"):  # renamed delivery: a file in the target folder named after the stem or the id
            folder, stem = target.rsplit("/", 1)
            stem_n, tail = norm(Path(stem).stem), id_tail(eid)
            for status, base in bases:
                for f in files_in(base / folder):
                    fn = norm(Path(f).stem)
                    if (stem_n and stem_n in fn) or (tail and tail in fn):
                        return status + "~"
        return None

    rows = []
    for e in manifest.entries:
        tp = (e.get("target_path") or "").replace("\\", "/")
        if not tp or tp.startswith("RawAssets/") or e["id"].startswith("VOICE_"):
            continue  # VOICE_* rows are casting data, the Clockmaker tokens never enter Assets/
        if "{CHAIN}" in tp:
            chains = [v for v in (e.get("variants") or []) if re.fullmatch(r"[A-Z_]+", str(v))]
            sts = [find(tp.replace("{CHAIN}", c), e) for c in chains] or [None]
            if all(s and s.startswith("landed") for s in sts):
                st = "landed"
            elif all(s for s in sts):
                st = "ready"
            elif any(s for s in sts):
                st = "partial"
            else:
                st = None
        else:
            st = find(tp, e)
        rows.append((e, st))
    return rows


def print_coverage(rows, out):
    by = defaultdict(Counter)
    for e, st in rows:
        key = st or "missing"
        key = key.rstrip("~")
        by[e["priority"]][key] += 1
    out.append("## Manifest coverage (landed in Assets/, ready in RawAssets/<wp>/ready/, ~ = found under another name)")
    for pr in sorted(by):
        c = by[pr]
        total = sum(c.values())
        out.append(f"  {pr}: {total} entries - landed {c['landed']}, ready {c['ready']}, partial {c['partial']}, missing {c['missing']}")
    miss = [e for e, st in rows if st is None and e["priority"] == "P0"]
    if miss:
        out.append(f"  Missing P0 ({len(miss)}):")
        for e in sorted(miss, key=lambda e: (e["category"], e["id"])):
            out.append(f"    {e['category']:<9} {e['id']:<34} {e.get('source', ''):<11} {e['target_path']}")
    ren = [(e, st) for e, st in rows if st and st.endswith("~")]
    if ren:
        out.append(f"  Found under another file name ({len(ren)}; the manifest target_path differs from the delivered name):")
        for e, st in ren[:40]:
            out.append(f"    {e['id']:<34} {e['target_path']}")


# ----------------------------------------------------------------------------------------------- mirror check


def check_mirror(manifest: Manifest, out):
    if not RULE_TABLE_PATH.exists():
        out.append(f"--check-mirror: {RULE_TABLE_PATH.relative_to(ROOT)} not found; run LongShadow/Import/Export Rule Table in the editor")
        return 2
    rules = TextureRulesMirror(manifest)
    mism = 0
    total = 0
    with open(RULE_TABLE_PATH, encoding="utf-8") as f:
        header = f.readline().rstrip("\n").split("\t")
        for line in f:
            c = dict(zip(header, line.rstrip("\n").split("\t")))
            w, h = (int(x) for x in c["source"].split("x"))
            info = TexInfo(known=w > 0, width=w, height=h, alpha_used=c["alpha_used"] == "1", has_alpha=c["alpha_used"] == "1",
                           bits=int(c["bits"] or 0), hdr=c["target"].lower().endswith((".exr", ".hdr")))
            r = rules.resolve(c["target"], info)
            total += 1
            diffs = []
            if r is None:
                diffs.append("python: no rule")
            else:
                if r.max != int(c["max"]):
                    diffs.append(f"max {r.max} != {c['max']}")
                if r.fmt != c["format"]:
                    diffs.append(f"format {r.fmt} != {c['format']}")
                if r.quality != int(c["quality"]):
                    diffs.append(f"quality {r.quality} != {c['quality']}")
                if r.mips != (c["mips"] == "1"):
                    diffs.append(f"mips {r.mips} != {c['mips']}")
                if r.editor_only != (c["editor_only"] == "1"):
                    diffs.append(f"editor_only {r.editor_only} != {c['editor_only']}")
                if r.srgb != (c["srgb"] == "1") and c["type"] != "NormalMap":
                    diffs.append(f"srgb {r.srgb} != {c['srgb']}")
                if "npot_none" in c and r.npot_none != (c["npot_none"] == "1"):
                    diffs.append(f"npot_none {r.npot_none} != {c['npot_none']}")
                if "sprite" in c and r.sprite != (c["sprite"] == "1"):
                    diffs.append(f"sprite {r.sprite} != {c['sprite']}")
            if diffs:
                mism += 1
                if mism <= 50:
                    out.append(f"  MIRROR {c['target']}: {'; '.join(diffs)}  [{c['rule']} | {c['cap_source']}]")
    out.append(f"--check-mirror: {total} textures compared with the editor export, {mism} mismatch(es)")
    return 1 if mism else 0


# ----------------------------------------------------------------------------------------------- self-test


def _write_png(path: Path, w, h, alpha):
    raw = bytearray()
    px = b"\x80\x80\xff\xff" if alpha else b"\x80\x80\xff"
    for _ in range(h):
        raw += b"\x00" + px * w
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6 if alpha else 2, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(bytes(raw))) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def _write_wav(path: Path, seconds, channels=1, rate=48000):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(b"\x00\x00" * channels * int(seconds * rate))


def _write_obj(path: Path, quads, name="SM_Test"):
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [f"o {name}"] + ["v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0"] + ["f 1 2 3 4"] * quads
    path.write_text("\n".join(lines) + "\n")


def selftest():
    """Synthetic files with known problems must produce exactly the expected codes (and clean files none)."""
    import tempfile
    tmp = Path(tempfile.mkdtemp(prefix="ls_validate_selftest_"))
    g = "Assets/_Game/"
    cases = []  # (target, writer, expected codes)
    def case(target, writer, *codes):
        cases.append((target, writer, set(codes)))
    case(g + "Art/Textures/Props/st_prop/T_StProp_BC.png", lambda p: _write_png(p, 64, 64, False))
    case(g + "Art/Textures/Props/st_prop/T_StProp_N.png", lambda p: _write_png(p, 64, 64, False))
    case(g + "Art/Textures/Props/st_prop/T_StProp_M.png", lambda p: _write_png(p, 64, 64, False), "TEX_MASK_NO_ALPHA")
    case(g + "Art/Textures/Props/st_prop/stprop_diffuse.png", lambda p: _write_png(p, 64, 64, False), "TEX_NAMING")
    case(g + "Art/Textures/Props/st_lonely/T_StLonely_N.png", lambda p: _write_png(p, 64, 64, False), "TEX_ORPHAN")
    case(g + "Art/Textures/Props/st_nomask/T_StNoMask_BC.png", lambda p: _write_png(p, 64, 64, False), "TEX_SET_INCOMPLETE")
    case(g + "Art/Textures/Props/st_npot/T_StNpot_BC.png", lambda p: _write_png(p, 96, 64, True), "TEX_NPOT", "TEX_SET_INCOMPLETE")
    case(g + "Art/Models/Props/st_prop/SM_StProp.obj", lambda p: _write_obj(p, 100))
    case(g + "Art/Models/Props/st_big/SM_StBig.obj", lambda p: _write_obj(p, 6000), "MDL_TRIS")  # 12000 tris > 8000 x 1.25
    case(g + "Art/Models/Props/st_lods/SM_StLods_LOD2.obj", lambda p: _write_obj(p, 10, "SM_StLods_LOD2"))
    case(g + "Audio/VO/DRY/VO_D001_DRY.wav", lambda p: _write_wav(p, 1.0))
    case(g + "Audio/VO/RADIO/VO_D001_DRY.wav", lambda p: _write_wav(p, 1.0), "AUD_VO_NAME")
    case(g + "Audio/VO/ROBOT/VO_D001_ROBOT.wav", lambda p: _write_wav(p, 1.0), "AUD_FOLDER")
    case(g + "Audio/SFX/Gizmos/SFX_ST_01.wav", lambda p: _write_wav(p, 0.5), "AUD_FOLDER")
    case(g + "Audio/SFX/Impacts/SFX_ST_IMPACT_01.wav", lambda p: _write_wav(p, 0.4, channels=2), "AUD_CHANNELS")
    case(g + "Audio/Music/MUS_ST.wav", lambda p: _write_wav(p, 120.0, channels=2, rate=8000), "AUD_DURATION", "AUD_RATE")
    case(g + "Audio/VO/DRY/VO_ZZ_NOPE_DRY.wav", lambda p: _write_wav(p, 1.0), "AUD_VO_LINE")
    items = []
    for target, writer, _ in cases:
        src = tmp / target
        writer(src)
        items.append(Item(target, src, "selftest"))

    class A:
        pixels = False
    rep = Validator(A()).run(items)
    by_path = defaultdict(set)
    for i in rep.issues:
        by_path[i.path].add(i.code)
    failures = []
    for target, _, expected in cases:
        got = by_path.get(target, set())
        if got != expected:
            failures.append(f"{target}: expected {sorted(expected) or 'nothing'}, got {sorted(got) or 'nothing'}")
    import shutil
    shutil.rmtree(tmp, ignore_errors=True)
    if failures:
        print("validate_assets --selftest: FAILED")
        for f in failures:
            print("  " + f)
        return 1
    print(f"validate_assets --selftest: OK ({len(cases)} synthetic files, every expected code found, no extra codes)")
    return 0


# ----------------------------------------------------------------------------------------------- main


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0], formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--ready", nargs="*", metavar="WP", help="validate RawAssets/<wp>/ready trees (all when no WP is given)")
    ap.add_argument("--path", action="append", help="validate one file or folder (project-relative); repeatable")
    ap.add_argument("--coverage", action="store_true", help="also report manifest coverage")
    ap.add_argument("--pixels", action="store_true", help="pixel checks (needs numpy + Pillow, e.g. Tools/.venv/bin/python)")
    ap.add_argument("--check-mirror", action="store_true", help="compare the Python rule mirror with Logs/import_rules_table.tsv")
    ap.add_argument("--json", metavar="FILE", help="also write a machine-readable report")
    ap.add_argument("--out", metavar="FILE", default="Logs/validate_assets.txt", help="text report (default Logs/validate_assets.txt)")
    ap.add_argument("--strict", action="store_true", help="warnings fail too")
    ap.add_argument("--quiet", action="store_true", help="print only the summary")
    ap.add_argument("--max-issues", type=int, default=400, help="issues printed per severity (the files get all)")
    ap.add_argument("--selftest", action="store_true", help="validate synthetic files with known problems (temp dir) and exit")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()

    if not BUDGETS:
        print(f"validate_assets: {BUDGETS_PATH.relative_to(ROOT)} missing or unreadable", file=sys.stderr)
        return 2
    if args.pixels and not HAVE_PIXELS:
        print("validate_assets: --pixels needs numpy + Pillow (run with Tools/.venv/bin/python)", file=sys.stderr)
        return 2

    t0 = time.time()
    out = []
    manifest = Manifest()
    if args.check_mirror:
        rc = check_mirror(manifest, out)
        print("\n".join(out))
        return rc

    items = collect(args)
    v = Validator(args)
    report = v.run(items)
    if not args.path:
        check_specs(report)
    scope = ("ready: " + ", ".join(sorted({i.origin for i in items}))) if args.ready is not None else ("paths" if args.path else "Assets/_Game")

    # download estimate (adds BUDGET_LINE warnings before the summary is written)
    per_line = defaultdict(float)
    for e in report.estimates:
        per_line[e.line] += e.bytes
    dl = BUDGETS.get("download", {})
    lines_budget = dl.get("budget_mb", {})
    est = ["## Estimated download per TECH_PLAN §8.10 line (upper bound: every non-editor-only file counts; textures "
           "from the Crunch basis, audio as AAC 44.1 kHz at the assumed kbps, meshes per triangle, 0.1 MB per clip)"]
    total = 0.0
    for line in sorted(set(per_line) | set(lines_budget)):
        mb = per_line.get(line, 0.0) / 1e6
        total += mb
        b = lines_budget.get(line)
        over = bool(b) and mb > b
        est.append(f"  {line:<40} {mb:8.1f} MB" + (f"  / {b} MB" + ("  OVER" if over else "") if b else ""))
        if over:
            report.add("warning", "BUDGET_LINE", line, f"estimated {mb:.1f} MB > {b} MB (§8.10)")
    est.append(f"  {'Total content estimate':<40} {total:8.1f} MB  (§8.10: 220 MB planned / 230 MB hard cap for the whole "
               f"build incl. engine + boot 13 MB and lightmaps 33 MB)")

    cov = []
    if args.coverage:
        print_coverage(coverage(manifest), cov)

    issues = sorted(report.issues, key=lambda i: (SEVERITY_ORDER[i.severity], i.code, i.path))
    sev = Counter(i.severity for i in issues)
    codes = Counter((i.severity, i.code) for i in issues)
    out.append(f"# validate_assets {time.strftime('%Y-%m-%d %H:%M:%S')}  scope {scope}  "
               f"({len(items)} files: {report.counts['textures']} textures, {report.counts['models']} models, "
               f"{report.counts['clips']} clips, {report.counts['audio']} audio, {report.counts['spec_rows']} material spec rows; "
               f"pixel checks {'on' if args.pixels else 'off'})")
    out.append(f"# {sev['error']} error(s), {sev['warning']} warning(s), {sev['info']} info")
    if codes:
        out.append("# by code: " + ", ".join(f"{c} {s} x{n}" for (s, c), n in sorted(codes.items(), key=lambda x: (SEVERITY_ORDER[x[0][0]], x[0][1]))))
    out.append("")
    out.extend(est)
    out.append("")
    if cov:
        out.extend(cov)
        out.append("")

    full_lines = list(out)
    full_lines.append("## Issues")
    for i in issues:
        full_lines.append(f"{i.severity.upper():<7} {i.code:<20} {i.path}  {i.message}")
    shown = Counter()
    out.append("## Issues" + (" (truncated per severity; see the report file)" if any(n > args.max_issues for n in sev.values()) else ""))
    for i in issues:
        if shown[i.severity] >= args.max_issues:
            continue
        shown[i.severity] += 1
        out.append(f"{i.severity.upper():<7} {i.code:<20} {i.path}  {i.message}")
    out.append(f"\n# done in {time.time() - t0:.1f} s: {sev['error']} error(s), {sev['warning']} warning(s), {sev['info']} info")
    full_lines.append(out[-1])

    if args.out:
        op = ROOT / args.out
        op.parent.mkdir(parents=True, exist_ok=True)
        op.write_text("\n".join(full_lines) + "\n", encoding="utf-8")
    if args.json:
        jp = ROOT / args.json if not os.path.isabs(args.json) else Path(args.json)
        jp.parent.mkdir(parents=True, exist_ok=True)
        jp.write_text(json.dumps({
            "scope": scope, "files": len(items), "counts": dict(report.counts),
            "errors": sev["error"], "warnings": sev["warning"], "infos": sev["info"],
            "estimate_mb": {k: round(v / 1e6, 3) for k, v in per_line.items()},
            "issues": [i.__dict__ for i in issues],
        }, indent=1), encoding="utf-8")
    if args.quiet:
        print("\n".join(out[:3] + [out[-1]]))
    else:
        print("\n".join(out))
    if sev["error"] or (args.strict and sev["warning"]):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
