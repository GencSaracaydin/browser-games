#!/usr/bin/env python3
"""Offline compile gate for OPERATION LONG SHADOW (TECH_PLAN §10.1, D18).

Compiles the project's own assemblies (every ``*.asmdef`` under ``Assets/_Game``)
with the Roslyn compiler that ships inside the Unity editor, without opening the
editor. Two passes mirror what Unity does:

* **player** pass: the WebGL player. ``UNITY_WEBGL`` defined, ``UNITY_EDITOR``
  not defined, engine references are the WebGL player modules, no UnityEditor.
  Editor-only and test assemblies are skipped (their define constraints / platform
  filters exclude them, exactly as in a player build).
* **editor** pass: the Unity editor. ``UNITY_EDITOR`` defined, UnityEditor and the
  editor modules referenced. Runtime assemblies are compiled against the .NET
  Standard 2.1 profile, editor-only assemblies against the .NET 4.8 profile (as
  Unity does with ``editorAssembliesCompatibilityLevel`` = 4.8).

The two passes together catch ``#if UNITY_EDITOR`` mistakes.

Source sets are a *virtual tree*: the landed files under
``Assets/_Game/Scripts`` and ``Assets/_Game/Tests`` overlaid with the staged files
of the selected systems:

    Staging/<System path>/**   ->  Assets/_Game/Scripts/<System path>/**
    Staging/Tests/**           ->  Assets/_Game/Tests/**

A ``.cs`` file belongs to the nearest ancestor folder that holds an ``.asmdef``
(Unity's rule). Package references come from ``Library/ScriptAssemblies``
(compiled by the editor; read only) and precompiled DLLs from
``Library/PackageCache``. Our own assemblies are always compiled from source into a
private temp folder, never read from ``Library``.

Usage examples (run from the project root):

    python3 Tools/build/compile_check.py --system Player          # staged Player + its Editor + its tests
    python3 Tools/build/compile_check.py --system World.Look --system World.Level
    python3 Tools/build/compile_check.py --all                    # everything, staged overlays applied
    python3 Tools/build/compile_check.py --all --overlay none      # landed state only
    python3 Tools/build/compile_check.py --system Core --dependents   # + every landed system against the new Core
    python3 Tools/build/compile_check.py --list                   # systems, assemblies, references
    python3 Tools/build/compile_check.py --selftest               # proves green-on-clean and red-on-error

Exit codes: 0 = no errors (and no warnings with --warnaserror), 1 = compile errors,
2 = configuration problem (missing reference, bad asmdef, file outside any asmdef).
"""
from __future__ import annotations

import argparse
import concurrent.futures as cf
import dataclasses
import fnmatch
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set, Tuple

# --------------------------------------------------------------------------------------
# Paths and constants
# --------------------------------------------------------------------------------------

PROJECT_ROOT = Path(__file__).resolve().parents[2]
SCRIPTS_ROOT = PROJECT_ROOT / "Assets" / "_Game" / "Scripts"
TESTS_ROOT = PROJECT_ROOT / "Assets" / "_Game" / "Tests"
DEFAULT_STAGING = PROJECT_ROOT / "Staging"
LIB_SCRIPT_ASSEMBLIES = PROJECT_ROOT / "Library" / "ScriptAssemblies"
LIB_PACKAGE_CACHE = PROJECT_ROOT / "Library" / "PackageCache"
LIB_BEE_ARTIFACTS = PROJECT_ROOT / "Library" / "Bee" / "artifacts"

UNITY_VERSION = "6000.6.6f1"
UNITY_ROOT = Path(os.environ.get("UNITY_EDITOR_ROOT", f"/Applications/Unity/Hub/Editor/{UNITY_VERSION}"))
UNITY_CONTENTS = UNITY_ROOT / "Unity.app" / "Contents"
SCRIPTING = UNITY_CONTENTS / "Resources" / "Scripting"
DOTNET = SCRIPTING / "DotNetSdk" / "dotnet"
WEBGL_SUPPORT = UNITY_ROOT / "PlaybackEngines" / "WebGLSupport"
OUR_PREFIX = "LongShadow."

# Unity's own nowarn list (from the editor .rsp files).
NOWARN = ["0169", "0649", "0282", "1701", "1702"]
LANGVERSION = "9.0"

# Defines Unity passes for a WebGL target in the editor (captured from
# Library/Bee/artifacts/*/Assembly-CSharp.rsp, Unity 6000.6.6f1). The live .rsp is
# preferred when present; this list is the fallback.
FALLBACK_EDITOR_DEFINES = """
UNITY_6000_6_6 UNITY_6000_6 UNITY_6000 UNITY_5_3_OR_NEWER UNITY_5_4_OR_NEWER UNITY_5_5_OR_NEWER
UNITY_5_6_OR_NEWER UNITY_2017_1_OR_NEWER UNITY_2017_2_OR_NEWER UNITY_2017_3_OR_NEWER UNITY_2017_4_OR_NEWER
UNITY_2018_1_OR_NEWER UNITY_2018_2_OR_NEWER UNITY_2018_3_OR_NEWER UNITY_2018_4_OR_NEWER UNITY_2019_1_OR_NEWER
UNITY_2019_2_OR_NEWER UNITY_2019_3_OR_NEWER UNITY_2019_4_OR_NEWER UNITY_2020_1_OR_NEWER UNITY_2020_2_OR_NEWER
UNITY_2020_3_OR_NEWER UNITY_2021_1_OR_NEWER UNITY_2021_2_OR_NEWER UNITY_2021_3_OR_NEWER UNITY_2022_1_OR_NEWER
UNITY_2022_2_OR_NEWER UNITY_2022_3_OR_NEWER UNITY_2023_1_OR_NEWER UNITY_2023_2_OR_NEWER UNITY_2023_3_OR_NEWER
UNITY_6000_0_OR_NEWER UNITY_6000_1_OR_NEWER UNITY_6000_2_OR_NEWER UNITY_6000_3_OR_NEWER UNITY_6000_4_OR_NEWER
UNITY_6000_5_OR_NEWER UNITY_6000_6_OR_NEWER UNITY_INCLUDE_TESTS ENABLE_AUDIO ENABLE_BURST ENABLE_CLOTH
ENABLE_MICROPHONE ENABLE_MULTIPLE_DISPLAYS ENABLE_PHYSICS ENABLE_TEXTURE_STREAMING ENABLE_UNITYEVENTS ENABLE_VR
ENABLE_WEBCAM ENABLE_UNITYWEBREQUEST ENABLE_WWW ENABLE_CLOUD_SERVICES ENABLE_CLOUD_SERVICES_ADS
ENABLE_CLOUD_SERVICES_USE_WEBREQUEST ENABLE_UNITY_CONSENT ENABLE_UNITY_CLOUD_IDENTIFIERS
ENABLE_CLOUD_SERVICES_CRASH_REPORTING ENABLE_CLOUD_SERVICES_PURCHASING ENABLE_CLOUD_SERVICES_ANALYTICS
ENABLE_CLOUD_SERVICES_BUILD ENABLE_EDITOR_GAME_SERVICES ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT
ENABLE_CLOUD_LICENSE ENABLE_EDITOR_HUB_LICENSE ENABLE_WEBSOCKET_CLIENT ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API
ENABLE_DIRECTOR_AUDIO ENABLE_DIRECTOR_TEXTURE ENABLE_MANAGED_JOBS ENABLE_MANAGED_TRANSFORM_JOBS
ENABLE_MANAGED_ANIMATION_JOBS ENABLE_MANAGED_AUDIO_JOBS ENABLE_ENGINE_CODE_STRIPPING ENABLE_ONSCREEN_KEYBOARD
ENABLE_MANAGED_UNITYTLS RENDER_SOFTWARE_CURSOR ENABLE_MARSHALLING_TESTS ENABLE_VIDEO
ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK ENABLE_ACCELERATOR_CLIENT_DEBUGGING TEXTCORE_1_0_OR_NEWER
EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED INCLUDE_UNIFIED_RAYTRACING_COMPUTE_BACKEND
INCLUDE_UNIFIED_RAYTRACING_HARDWARE_BACKEND PLATFORM_WEBGL PLATFORM_SUPPORTS_PROGRESSIVE_ASSET_LOADING UNITY_WEBGL
UNITY_WEBGL_API UNITY_DISABLE_WEB_VERIFICATION UNITY_GFX_USE_PLATFORM_VSYNC ENABLE_CRUNCH_TEXTURE_COMPRESSION
ENABLE_SPATIALTRACKING ENABLE_MONO NET_STANDARD_2_0 NET_STANDARD NET_STANDARD_2_1 NETSTANDARD NETSTANDARD2_1
ENABLE_PROFILER UNITY_INCLUDE_INSTRUMENTATION DEBUG TRACE UNITY_ASSERTIONS UNITY_ENABLE_CHECKS UNITY_EDITOR
UNITY_EDITOR_64 UNITY_EDITOR_OSX ENABLE_UNITY_COLLECTIONS_CHECKS ENABLE_BURST_AOT UNITY_TEAM_LICENSE
ENABLE_CUSTOM_RENDER_TEXTURE ENABLE_DIRECTOR ENABLE_LOCALIZATION ENABLE_SPRITES ENABLE_TERRAIN ENABLE_TILEMAP
ENABLE_TIMELINE ENABLE_INPUT_SYSTEM TEXTCORE_FONT_ENGINE_1_5_OR_NEWER TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER
TEXTCORE_FONT_ENGINE_1_6_OR_NEWER CSHARP_7_OR_LATER CSHARP_7_3_OR_NEWER UNITY_WEBGL_RENDERER_ONLY
""".split()

# Defines that exist only inside the editor (removed for the player pass).
EDITOR_ONLY_DEFINES = {
    "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_OSX", "UNITY_EDITOR_WIN", "UNITY_EDITOR_LINUX",
    "UNITY_INCLUDE_TESTS", "UNITY_TEAM_LICENSE", "ENABLE_MONO", "ENABLE_UNITY_COLLECTIONS_CHECKS",
    "ENABLE_BURST_AOT", "ENABLE_EDITOR_GAME_SERVICES", "ENABLE_CLOUD_LICENSE", "ENABLE_EDITOR_HUB_LICENSE",
    "ENABLE_WEBSOCKET_CLIENT", "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API",
    "ENABLE_ACCELERATOR_CLIENT_DEBUGGING", "ENABLE_MARSHALLING_TESTS", "UNITY_EDITOR_ONLY_COMPILATION",
    "EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED",
    # development-build only (the player pass models a release build; --dev adds them back)
    "DEBUG", "ENABLE_PROFILER", "UNITY_ASSERTIONS", "UNITY_INCLUDE_INSTRUMENTATION", "UNITY_ENABLE_CHECKS",
}
PLAYER_EXTRA_DEFINES = ["ENABLE_IL2CPP", "UNITY_WEBGL", "PLATFORM_WEBGL"]
DEV_DEFINES = ["DEVELOPMENT_BUILD", "DEBUG", "ENABLE_PROFILER", "UNITY_ASSERTIONS", "LS_DEV"]
NETSTANDARD_DEFINES = {"NET_STANDARD_2_0", "NET_STANDARD", "NET_STANDARD_2_1", "NETSTANDARD", "NETSTANDARD2_1"}
NET48_DEFINES = ["NET_4_6", "NET_UNITY_4_8", "UNITY_EDITOR_ONLY_COMPILATION"]

# Assemblies Unity 6 references implicitly from every assembly with engine references.
IMPLICIT_REFS_ALL = ["UnityEngine.UI"]
IMPLICIT_REFS_EDITOR = ["UnityEditor.UI"]

DIAG_RE = re.compile(
    r"^(?P<file>.+?)\((?P<line>\d+),(?P<col>\d+)\): (?P<sev>error|warning) (?P<code>[A-Z]+\d+): (?P<msg>.*)$"
)
DIAG_NOFILE_RE = re.compile(r"^(?:error|warning) (?P<code>[A-Z]+\d+): (?P<msg>.*)$")

# --------------------------------------------------------------------------------------
# Data classes
# --------------------------------------------------------------------------------------


@dataclasses.dataclass
class Asmdef:
    name: str
    landed_dir: str                 # project-relative POSIX path of the folder the asmdef sits in
    source_path: Path               # actual file read (staged or landed)
    staged: bool
    data: dict
    sources: List[Tuple[str, Path]] = dataclasses.field(default_factory=list)  # (virtual rel path, real path)

    @property
    def references(self) -> List[str]:
        return list(self.data.get("references", []))

    @property
    def include_platforms(self) -> List[str]:
        return list(self.data.get("includePlatforms", []))

    @property
    def exclude_platforms(self) -> List[str]:
        return list(self.data.get("excludePlatforms", []))

    @property
    def editor_only(self) -> bool:
        return self.include_platforms == ["Editor"]

    @property
    def kind(self) -> str:
        rel = self.landed_dir
        if rel.startswith("Assets/_Game/Tests/"):
            return "tests"
        if rel.endswith("/Editor") or self.editor_only:
            return "editor"
        return "runtime"

    @property
    def system(self) -> str:
        return system_of_dir(self.landed_dir)


@dataclasses.dataclass
class Diagnostic:
    severity: str
    code: str
    message: str
    file: str = ""
    line: int = 0
    col: int = 0
    assembly: str = ""
    passes: Set[str] = dataclasses.field(default_factory=set)

    def key(self):
        return (self.severity, self.code, self.file, self.line, self.col, self.message)

    def format(self) -> str:
        where = f"{self.file}:{self.line}:{self.col}: " if self.file else ""
        tag = f" [{self.assembly}; {'+'.join(sorted(self.passes))}]"
        return f"{where}{self.severity} {self.code}: {self.message}{tag}"


@dataclasses.dataclass
class CompileResult:
    assembly: str
    pass_name: str
    ok: bool
    skipped_reason: str = ""
    seconds: float = 0.0
    diagnostics: List[Diagnostic] = dataclasses.field(default_factory=list)
    file_count: int = 0


class ConfigError(Exception):
    pass


# --------------------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------------------


def rel(p: Path) -> str:
    try:
        return p.resolve().relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return p.as_posix()


def system_of_dir(landed_dir: str) -> str:
    """'Assets/_Game/Scripts/World/Look/Editor' -> 'World.Look'; tests dirs map the same way."""
    parts = landed_dir.split("/")
    if landed_dir.startswith("Assets/_Game/Scripts/"):
        parts = parts[3:]
    elif landed_dir.startswith("Assets/_Game/Tests/"):
        parts = parts[4:]  # drop Assets/_Game/Tests/<EditMode|PlayMode>
    else:
        return "?"
    if parts and parts[-1] == "Editor":
        parts = parts[:-1]
    return ".".join(parts) if parts else "?"


def system_dir(system: str) -> str:
    return system.replace(".", "/")


def normalise_system(name: str) -> str:
    return name.strip().strip("/").replace("/", ".")


def load_json_lenient(path: Path) -> dict:
    text = path.read_text(encoding="utf-8-sig")
    # asmdef files are JSON but tolerate // comments defensively
    text = re.sub(r"^\s*//.*$", "", text, flags=re.M)
    return json.loads(text)


def staged_to_landed(staging_root: Path, path: Path) -> Optional[str]:
    """Maps a file under Staging/ to its virtual project-relative landed path."""
    r = path.relative_to(staging_root).as_posix()
    first = r.split("/", 1)[0]
    if first in ("obj", "bin") or first.startswith("."):
        return None
    if first == "Assets":
        return None  # non-script assets (land.py --assets); never compiled
    if first == "Tests":
        return "Assets/_Game/Tests/" + r.split("/", 1)[1] if "/" in r else None
    return "Assets/_Game/Scripts/" + r


def landed_to_staged(staging_root: Path, landed: str) -> Path:
    if landed.startswith("Assets/_Game/Tests/"):
        return staging_root / "Tests" / landed[len("Assets/_Game/Tests/"):]
    return staging_root / landed[len("Assets/_Game/Scripts/"):]


def staging_dirs_for_system(staging_root: Path, system: str) -> List[Path]:
    """Staging folders that belong to a system (runtime + Editor + tests)."""
    sd = system_dir(system)
    return [
        staging_root / sd,
        staging_root / "Tests" / "EditMode" / sd,
        staging_root / "Tests" / "PlayMode" / sd,
    ]


def read_delete_list(folder: Path) -> Set[str]:
    """DELETE.txt in a staged system folder lists landed paths (relative to that folder) to drop."""
    f = folder / "DELETE.txt"
    out: Set[str] = set()
    if f.is_file():
        for line in f.read_text(encoding="utf-8").splitlines():
            line = line.split("#", 1)[0].strip()
            if line:
                out.add(line.replace("\\", "/"))
    return out


# --------------------------------------------------------------------------------------
# Environment discovery (Unity install, defines, package assemblies)
# --------------------------------------------------------------------------------------


class Environment:
    def __init__(self, dev: bool = False):
        if not DOTNET.exists():
            raise ConfigError(f"Unity bundled dotnet not found at {DOTNET} (set UNITY_EDITOR_ROOT)")
        cscs = sorted((SCRIPTING / "DotNetSdk" / "sdk").glob("*/Roslyn/bincore/csc.dll"))
        if not cscs:
            raise ConfigError("Roslyn csc.dll not found inside the Unity DotNetSdk")
        self.csc = cscs[-1]
        self.dev = dev
        self.editor_defines_base = self._load_editor_defines()
        self.project_defines = self._project_scripting_defines()
        self.bcl_ns21 = self._bcl_netstandard21()
        self.bcl_net48 = self._bcl_net48()
        self.engine_editor = self._engine_refs_editor()
        self.engine_player = self._engine_refs_player()
        self.editor_refs = self._editor_refs()
        self.package_dlls = self._package_script_assemblies()
        self.precompiled = self._precompiled_dlls()
        self.package_versions = self._package_versions()
        self.guid_to_asmdef_name = self._package_asmdef_guids()

    # ---- defines
    def _load_editor_defines(self) -> List[str]:
        candidates = sorted(LIB_BEE_ARTIFACTS.glob("*E.dag/Assembly-CSharp.rsp"), key=lambda p: p.stat().st_mtime)
        for rsp in reversed(candidates):
            try:
                defs = [l[len("-define:"):].strip() for l in rsp.read_text().splitlines() if l.startswith("-define:")]
                if "UNITY_WEBGL" in defs and "UNITY_EDITOR" in defs:
                    return defs
            except OSError:
                continue
        return list(FALLBACK_EDITOR_DEFINES)

    def _project_scripting_defines(self) -> List[str]:
        """Scripting define symbols set in Player Settings for WebGL (read only)."""
        ps = PROJECT_ROOT / "ProjectSettings" / "ProjectSettings.asset"
        out: List[str] = []
        try:
            text = ps.read_text(encoding="utf-8", errors="replace")
        except OSError:
            return out
        m = re.search(r"^  scriptingDefineSymbols:\s*\n((?:    .*\n)*)", text, flags=re.M)
        if m:
            for line in m.group(1).splitlines():
                mm = re.match(r"\s+(\w+):\s*(.*)$", line)
                if mm and mm.group(1) in ("WebGL", "1"):  # key is the build target group name
                    out += [d for d in re.split(r"[;\s]+", mm.group(2).strip()) if d]
        return out

    def defines(self, pass_name: str, asm: Asmdef) -> List[str]:
        base = list(self.editor_defines_base)
        if pass_name == "player":
            d = [x for x in base if x not in EDITOR_ONLY_DEFINES]
            d += PLAYER_EXTRA_DEFINES
            if self.dev:
                d += DEV_DEFINES
        else:
            d = list(base)
            if self.dev:
                d += ["LS_DEV"]
            if asm.editor_only:
                d = [x for x in d if x not in NETSTANDARD_DEFINES] + NET48_DEFINES
        d += self.project_defines
        d += self.version_defines(asm)
        seen: Set[str] = set()
        return [x for x in d if not (x in seen or seen.add(x))]

    def version_defines(self, asm: Asmdef) -> List[str]:
        out = []
        for vd in asm.data.get("versionDefines", []) or []:
            name, expr, define = vd.get("name", ""), vd.get("expression", ""), vd.get("define", "")
            if not define:
                continue
            if name == "Unity":
                version = UNITY_VERSION.split("f")[0]
            else:
                version = self.package_versions.get(name)
                if version is None and name.startswith("com.unity.modules."):
                    version = "1.0.0"
            if version is not None and version_matches(version, expr):
                out.append(define)
        return out

    # ---- BCL
    def _bcl_netstandard21(self) -> List[Path]:
        ns = SCRIPTING / "NetStandard"
        refs = [ns / "ref" / "2.1.0" / "netstandard.dll"]
        refs += sorted((ns / "compat" / "2.1.0" / "shims").glob("*/*.dll"))
        refs += sorted((ns / "Extensions" / "2.0.0").glob("*.dll"))
        refs += sorted((SCRIPTING / "BCLExtensions" / "TargetingPacks" / "netstandard2.1" / "ref").glob("*.dll"))
        return dedupe_by_name(refs)

    def _bcl_net48(self) -> List[Path]:
        api = SCRIPTING / "UnityReferenceAssemblies" / "unity-4.8-api"
        wanted = [
            "mscorlib.dll", "System.dll", "System.Core.dll", "System.Data.dll", "System.Drawing.dll",
            "System.IO.Compression.dll", "System.IO.Compression.FileSystem.dll", "System.Net.Http.dll",
            "System.Numerics.dll", "System.Numerics.Vectors.dll", "System.Runtime.Serialization.dll",
            "System.Transactions.dll", "System.Xml.dll", "System.Xml.Linq.dll", "Microsoft.CSharp.dll",
            "System.ComponentModel.Composition.dll", "System.Data.DataSetExtensions.dll",
        ]
        refs = [api / w for w in wanted if (api / w).exists()]
        refs += sorted((api / "Facades").glob("*.dll"))
        refs += sorted((SCRIPTING / "BCLExtensions" / "TargetingPacks" / "netstandard2.1" / "ref").glob("*.dll"))
        return dedupe_by_name(refs)

    # ---- engine
    def _engine_refs_editor(self) -> List[Path]:
        mu = SCRIPTING / "Managed" / "UnityEngine"
        refs = sorted(p for p in mu.glob("UnityEngine*.dll"))
        refs.append(WEBGL_SUPPORT / "Variations" / "il2cpp" / "Managed" / "UnityEngine.WebGLModule.dll")
        refs.append(SCRIPTING / "Managed" / "Bootstrap" / "Unity.Scripting.dll")
        return dedupe_by_name([r for r in refs if r.exists()])

    def _engine_refs_player(self) -> List[Path]:
        pm = WEBGL_SUPPORT / "Variations" / "il2cpp" / "Managed"
        refs = sorted(pm.glob("UnityEngine*.dll")) + sorted(pm.glob("Unity.Scripting.dll"))
        if not refs:  # fall back to editor modules if the WebGL module is missing
            return [r for r in self._engine_refs_editor()]
        return dedupe_by_name(refs)

    def _editor_refs(self) -> List[Path]:
        mu = SCRIPTING / "Managed" / "UnityEngine"
        refs = [mu / "UnityEditor.dll"] + sorted(mu.glob("UnityEditor.*Module.dll"))
        refs += [SCRIPTING / "Managed" / "UnityEditor.Graphs.dll", WEBGL_SUPPORT / "UnityEditor.WebGL.Extensions.dll"]
        return [r for r in refs if r.exists()]

    # ---- packages
    def _package_script_assemblies(self) -> Dict[str, Path]:
        out: Dict[str, Path] = {}
        if LIB_SCRIPT_ASSEMBLIES.is_dir():
            for p in LIB_SCRIPT_ASSEMBLIES.glob("*.dll"):
                name = p.stem
                if name.startswith(OUR_PREFIX) or name.startswith("Assembly-CSharp"):
                    continue
                out[name] = p
        return out

    def _precompiled_dlls(self) -> Dict[str, dict]:
        """name.dll -> {path, auto, editor, player} for DLL plugins in packages and Assets."""
        out: Dict[str, dict] = {}
        roots = [LIB_PACKAGE_CACHE, PROJECT_ROOT / "Assets"]
        for root in roots:
            if not root.is_dir():
                continue
            for p in root.rglob("*.dll"):
                srel = p.as_posix()
                if "/Library/PackageCache/" in srel and ("/Tests/" in srel or "/Samples~/" in srel or "~/" in srel):
                    continue
                if "/Assets/_Game/" in srel:
                    continue
                if not is_managed_dll(p):
                    continue
                info = parse_plugin_meta(Path(str(p) + ".meta"))
                info["path"] = p
                out.setdefault(p.name, info)
        return out

    def _package_versions(self) -> Dict[str, str]:
        lock = PROJECT_ROOT / "Packages" / "packages-lock.json"
        try:
            deps = json.loads(lock.read_text())["dependencies"]
            return {k: v.get("version", "") for k, v in deps.items()}
        except (OSError, ValueError, KeyError):
            return {}

    def _package_asmdef_guids(self) -> Dict[str, str]:
        out: Dict[str, str] = {}
        if not LIB_PACKAGE_CACHE.is_dir():
            return out
        for meta in LIB_PACKAGE_CACHE.rglob("*.asmdef.meta"):
            try:
                guid = re.search(r"^guid:\s*([0-9a-f]{32})", meta.read_text(), flags=re.M)
                if not guid:
                    continue
                data = load_json_lenient(Path(str(meta)[:-5]))
                out[guid.group(1)] = data.get("name", "")
            except (OSError, ValueError):
                continue
        return out


def dedupe_by_name(paths: Iterable[Path]) -> List[Path]:
    seen: Set[str] = set()
    out = []
    for p in paths:
        if p.name in seen:
            continue
        seen.add(p.name)
        out.append(p)
    return out


def is_managed_dll(path: Path) -> bool:
    """True if the PE file has a CLI (COR20) header, i.e. it is a .NET assembly, not a native plugin."""
    import struct
    try:
        with open(path, "rb") as f:
            data = f.read(4096)
        if data[:2] != b"MZ":
            return False
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            return False
        opt = pe + 24
        magic = struct.unpack_from("<H", data, opt)[0]
        dd = opt + (96 if magic == 0x10B else 112)  # data directories
        rva, size = struct.unpack_from("<II", data, dd + 14 * 8)
        return rva != 0 and size != 0
    except (OSError, struct.error):
        return False


def parse_plugin_meta(meta: Path) -> dict:
    """Very small PluginImporter reader: auto-referenced? enabled in Editor / WebGL?"""
    info = {"auto": True, "editor": True, "player": True, "defineConstraints": []}
    try:
        text = meta.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return info
    if "PluginImporter" not in text:
        # Minimal metas (e.g. nunit.framework.dll) behave as editor/test-only in practice.
        info.update(auto=True, editor=True, player=False)
        return info
    m = re.search(r"isExplicitlyReferenced:\s*(\d)", text)
    info["auto"] = not (m and m.group(1) == "1")
    dc = re.search(r"defineConstraints:\s*\[(.*?)\]", text)
    if dc and dc.group(1).strip():
        info["defineConstraints"] = [x.strip() for x in dc.group(1).split(",") if x.strip()]
    # Any platform block
    any_enabled = False
    excl_editor = excl_webgl = False
    any_block = re.search(r"'':\s*Any\s*\n\s*second:\s*\n\s*enabled:\s*(\d)(.*?)(?=\n  - first:|\n  userData|\Z)", text, flags=re.S)
    if any_block:
        any_enabled = any_block.group(1) == "1"
        excl_editor = bool(re.search(r"Exclude Editor:\s*1", any_block.group(2)))
        excl_webgl = bool(re.search(r"Exclude WebGL:\s*1", any_block.group(2)))
    def plat(name: str) -> Optional[bool]:
        mm = re.search(name + r"\s*\n\s*second:\s*\n\s*enabled:\s*(\d)", text)
        return None if not mm else mm.group(1) == "1"
    ed = plat(r"Editor:\s*Editor")
    wg = plat(r"WebGL:\s*WebGL")
    info["editor"] = (any_enabled and not excl_editor) or bool(ed)
    info["player"] = (any_enabled and not excl_webgl) or bool(wg)
    return info


def parse_version(v: str) -> Tuple:
    core = re.split(r"[-+]", v, 1)[0]
    nums = []
    for part in core.split("."):
        m = re.match(r"(\d+)", part)
        nums.append(int(m.group(1)) if m else 0)
    while len(nums) < 3:
        nums.append(0)
    pre = 0 if "-" in v else 1  # pre-release sorts before release
    return tuple(nums[:3]) + (pre,)


def version_matches(version: str, expr: str) -> bool:
    """Unity versionDefines expression: '' (any), '1.2.3' (>=), '[a,b]' / '(a,b)' / '[a,)' intervals."""
    expr = (expr or "").strip()
    if not expr:
        return True
    v = parse_version(version)
    if expr[0] in "[(":
        lo_incl, hi_incl = expr[0] == "[", expr[-1] == "]"
        inner = expr[1:-1]
        if "," not in inner:
            return v[:3] == parse_version(inner)[:3]
        lo, hi = [x.strip() for x in inner.split(",", 1)]
        if lo:
            lv = parse_version(lo)
            if v < lv or (v == lv and not lo_incl):
                return False
        if hi:
            hv = parse_version(hi)
            if v > hv or (v == hv and not hi_incl):
                return False
        return True
    return v >= parse_version(expr)


def eval_define_constraint(expr: str, defines: Set[str]) -> bool:
    """Unity define constraint: 'A', '!A', 'A || B', 'A || !B'."""
    for alt in expr.split("||"):
        alt = alt.strip()
        if not alt:
            continue
        neg = alt.startswith("!")
        sym = alt[1:].strip() if neg else alt
        if (sym in defines) != neg:
            return True
    return False


# --------------------------------------------------------------------------------------
# Virtual tree and assembly graph
# --------------------------------------------------------------------------------------


class Project:
    def __init__(self, staging_root: Path, overlay_systems: Optional[Set[str]], overlay_all: bool):
        self.staging_root = staging_root
        self.files: Dict[str, Path] = {}       # virtual landed rel path -> real file
        self.staged_files: Set[str] = set()    # virtual paths that come from staging
        self.problems: List[str] = []
        self._collect_landed()
        self._staged = self._collect_staged()  # list of (virtual path, real path)
        self.known_systems = self._known_systems()
        self.staged_by_system: Dict[str, List[Tuple[str, Path]]] = {}
        for v, real in self._staged:
            s = self.system_for_path(v)
            if s is None:
                self.problems.append(f"{rel(real)}: staged file is not under any known system folder")
                continue
            self.staged_by_system.setdefault(s, []).append((v, real))
        self.staged_systems_present = set(self.staged_by_system)
        if overlay_all:
            overlay = set(self.staged_systems_present)
        else:
            overlay = set()
            for t in overlay_systems or ():
                overlay |= self.expand_system(t)
        self.overlay_systems = overlay
        for s in sorted(overlay):
            self._apply_overlay(s)
        self.asmdefs: Dict[str, Asmdef] = {}
        self._build_asmdefs()

    def _collect_landed(self):
        for root in (SCRIPTS_ROOT, TESTS_ROOT):
            if not root.is_dir():
                continue
            for p in root.rglob("*"):
                if p.is_file() and p.suffix in (".cs", ".asmdef"):
                    self.files[rel(p)] = p

    def _collect_staged(self) -> List[Tuple[str, Path]]:
        out: List[Tuple[str, Path]] = []
        if not self.staging_root.is_dir():
            return out
        for p in sorted(self.staging_root.rglob("*")):
            if not p.is_file():
                continue
            if p.suffix not in (".cs", ".asmdef") and p.name != "DELETE.txt":
                continue
            v = staged_to_landed(self.staging_root, p)
            if v:
                out.append((v, p))
        return out

    def _known_systems(self) -> Set[str]:
        dirs = {str(Path(v).parent.as_posix()) for v in self.files if v.endswith(".asmdef")}
        dirs |= {str(Path(v).parent.as_posix()) for v, _ in self._staged if v.endswith(".asmdef")}
        return {system_of_dir(d) for d in dirs if system_of_dir(d) != "?"}

    def system_for_path(self, virtual_path: str) -> Optional[str]:
        """Longest known system whose folder contains this virtual path."""
        d = str(Path(virtual_path).parent.as_posix())
        if d.startswith("Assets/_Game/Scripts/"):
            parts = d.split("/")[3:]
        elif d.startswith("Assets/_Game/Tests/"):
            parts = d.split("/")[4:]
        else:
            return None
        if parts and parts[-1] == "Editor":
            parts = parts[:-1]
        for i in range(len(parts), 0, -1):
            head = parts[:i]
            if head[-1] == "Editor":  # World/Look/Editor/Sub -> World.Look
                head = head[:-1]
            cand = ".".join(head)
            if cand in self.known_systems:
                return cand
        return None

    def expand_system(self, name: str) -> Set[str]:
        name = normalise_system(name)
        if name in self.known_systems:
            return {name}
        return {s for s in self.known_systems if s.startswith(name + ".")}

    def _apply_overlay(self, system: str):
        for v, p in self.staged_by_system.get(system, []):
            if p.name == "DELETE.txt":
                base = str(Path(v).parent.as_posix())
                for d in read_delete_list(p.parent):
                    target = base + "/" + d
                    if target in self.files and target not in self.staged_files:
                        del self.files[target]
                continue
            self.files[v] = p
            self.staged_files.add(v)

    def _build_asmdefs(self):
        asmdef_dirs: Dict[str, str] = {}
        for v, real in sorted(self.files.items()):
            if v.endswith(".asmdef"):
                d = str(Path(v).parent.as_posix())
                if d in asmdef_dirs:
                    self.problems.append(f"two asmdefs in {d}")
                    continue
                try:
                    data = load_json_lenient(real)
                except (OSError, ValueError) as e:
                    self.problems.append(f"bad asmdef {rel(real)}: {e}")
                    continue
                name = data.get("name") or Path(v).stem
                if name in self.asmdefs:
                    self.problems.append(f"duplicate assembly name {name} ({d} and {self.asmdefs[name].landed_dir})")
                    continue
                asmdef_dirs[d] = name
                self.asmdefs[name] = Asmdef(name=name, landed_dir=d, source_path=real, staged=v in self.staged_files, data=data)
        for v, real in sorted(self.files.items()):
            if not v.endswith(".cs"):
                continue
            d = Path(v).parent
            owner = None
            while True:
                ds = d.as_posix()
                if ds in asmdef_dirs:
                    owner = asmdef_dirs[ds]
                    break
                if ds in ("Assets/_Game", "Assets", ".", ""):
                    break
                d = d.parent
            if owner is None:
                self.problems.append(f"{v}: not under any .asmdef (would compile into Assembly-CSharp; not allowed)")
                continue
            self.asmdefs[owner].sources.append((v, real))

    # ---- graph
    def resolve_ref(self, env: Environment, ref: str) -> str:
        if ref.startswith("GUID:"):
            g = ref[5:].lower()
            if g in env.guid_to_asmdef_name:
                return env.guid_to_asmdef_name[g]
            for a in self.asmdefs.values():
                meta = Path(str(PROJECT_ROOT / (a.landed_dir + "/" + a.name + ".asmdef")) + ".meta")
                try:
                    if meta.exists() and g in meta.read_text():
                        return a.name
                except OSError:
                    pass
            return ref
        return ref

    def our_deps(self, env: Environment, name: str) -> List[str]:
        a = self.asmdefs[name]
        return [r for r in (self.resolve_ref(env, x) for x in a.references) if r in self.asmdefs]

    def topo(self, env: Environment, names: Iterable[str]) -> List[str]:
        order: List[str] = []
        state: Dict[str, int] = {}

        def visit(n: str, stack: List[str]):
            if state.get(n) == 2:
                return
            if state.get(n) == 1:
                raise ConfigError("assembly reference cycle: " + " -> ".join(stack + [n]))
            state[n] = 1
            for d in self.our_deps(env, n):
                visit(d, stack + [n])
            state[n] = 2
            order.append(n)

        for n in sorted(names):
            visit(n, [])
        return order

    def dependents(self, env: Environment, names: Set[str]) -> Set[str]:
        out = set(names)
        changed = True
        while changed:
            changed = False
            for a in self.asmdefs:
                if a in out:
                    continue
                if any(d in out for d in self.our_deps(env, a)):
                    out.add(a)
                    changed = True
        return out

    def systems(self) -> Dict[str, List[str]]:
        out: Dict[str, List[str]] = {}
        for a in self.asmdefs.values():
            out.setdefault(a.system, []).append(a.name)
        return {k: sorted(v) for k, v in sorted(out.items())}

    def assemblies_of_system(self, system: str) -> List[str]:
        systems = self.expand_system(system)
        return sorted(a.name for a in self.asmdefs.values() if a.system in systems)


# --------------------------------------------------------------------------------------
# Compilation
# --------------------------------------------------------------------------------------


class Compiler:
    def __init__(self, env: Environment, project: Project, out_root: Path, warnaserror: bool, use_server: bool, verbose: bool):
        self.env = env
        self.project = project
        self.out_root = out_root
        self.warnaserror = warnaserror
        self.use_server = use_server
        self.verbose = verbose
        self.outputs: Dict[Tuple[str, str], Path] = {}

    def applies(self, asm: Asmdef, pass_name: str) -> Tuple[bool, str]:
        inc, exc = asm.include_platforms, asm.exclude_platforms
        plat = "WebGL" if pass_name == "player" else "Editor"
        if inc and plat not in inc:
            return False, f"not built for {plat}"
        if plat in exc:
            return False, f"excluded on {plat}"
        defs = set(self.env.defines(pass_name, asm))
        for c in asm.data.get("defineConstraints", []) or []:
            if not eval_define_constraint(c, defs):
                return False, f"define constraint '{c}' not met"
        return True, ""

    def references_for(self, asm: Asmdef, pass_name: str) -> Tuple[List[Path], List[str]]:
        problems: List[str] = []
        refs: List[Path] = []
        editor_profile = pass_name == "editor" and asm.editor_only
        refs += self.env.bcl_net48 if editor_profile else self.env.bcl_ns21
        if not asm.data.get("noEngineReferences", False):
            refs += self.env.engine_player if pass_name == "player" else self.env.engine_editor
            if pass_name == "editor":
                refs += self.env.editor_refs
        names = [self.project.resolve_ref(self.env, r) for r in asm.references]
        dups = sorted({n for n in names if names.count(n) > 1})
        if dups:
            problems.append("Assembly has duplicate references: " + ",".join(dups) + " (Unity rejects this)")
        if not asm.data.get("noEngineReferences", False):
            names += IMPLICIT_REFS_ALL + (IMPLICIT_REFS_EDITOR if pass_name == "editor" else [])
        seen: Set[str] = set()
        for n in names:
            if n in seen:
                continue
            seen.add(n)
            if n in self.project.asmdefs:
                out = self.outputs.get((n, pass_name))
                if out is None:
                    dep = self.project.asmdefs[n]
                    ok, why = self.applies(dep, pass_name)
                    if not ok:
                        if pass_name == "player":
                            continue  # editor-only reference from a runtime asmdef: Unity drops it in the player
                        problems.append(f"reference {n} does not apply to the {pass_name} pass ({why})")
                    else:
                        problems.append(f"__DEPFAIL__{n}")
                    continue
                refs.append(out)
            elif n.startswith("GUID:"):
                problems.append(f"unresolved GUID reference {n}")
            elif n in self.env.package_dlls:
                refs.append(self.env.package_dlls[n])
            elif n.startswith(OUR_PREFIX):
                problems.append(f"reference {n}: no such LongShadow assembly")
            else:
                problems.append(f"reference {n}: not found in Library/ScriptAssemblies (package not installed or editor never compiled it)")
        # precompiled
        defs = set(self.env.defines(pass_name, asm))
        if asm.data.get("overrideReferences", False):
            for dll in asm.data.get("precompiledReferences", []) or []:
                info = self.env.precompiled.get(dll)
                if not info:
                    problems.append(f"precompiled reference {dll} not found")
                    continue
                refs.append(info["path"])
        else:
            for dll, info in sorted(self.env.precompiled.items()):
                if not info["auto"]:
                    continue
                if not (info["player"] if pass_name == "player" else info["editor"]):
                    continue
                if any(not eval_define_constraint(c, defs) for c in info["defineConstraints"]):
                    continue
                refs.append(info["path"])
        return dedupe_by_name(refs), problems

    def compile(self, name: str, pass_name: str) -> CompileResult:
        asm = self.project.asmdefs[name]
        ok, why = self.applies(asm, pass_name)
        if not ok:
            return CompileResult(name, pass_name, True, skipped_reason=why)
        refs, problems = self.references_for(asm, pass_name)
        res = CompileResult(name, pass_name, False, file_count=len(asm.sources))
        depfail = [p[len("__DEPFAIL__"):] for p in problems if p.startswith("__DEPFAIL__")]
        problems = [p for p in problems if not p.startswith("__DEPFAIL__")]
        if depfail and not problems:
            res.skipped_reason = "not compiled: dependency failed (" + ", ".join(depfail) + ")"
            return res
        if problems:
            for p in problems:
                res.diagnostics.append(Diagnostic("error", "LS0001", p, rel(asm.source_path), 1, 1, name, {pass_name}))
            return res
        out_dir = self.out_root / pass_name
        out_dir.mkdir(parents=True, exist_ok=True)
        out = out_dir / f"{name}.dll"
        rsp = out_dir / f"{name}.rsp"
        lines = [
            "-target:library", "-nologo", "-nostdlib+", f"-langversion:{LANGVERSION}",
            "-deterministic", "-optimize-", "-debug-", "-utf8output", "-preferreduilang:en-US",
            f"-out:{out}", f"-nowarn:{','.join(NOWARN)}",
        ]
        if asm.data.get("allowUnsafeCode", False):
            lines.append("-unsafe")
        if self.warnaserror:
            lines.append("-warnaserror+")
        for d in self.env.defines(pass_name, asm):
            lines.append(f"-define:{d}")
        for r in refs:
            lines.append(f'-r:"{r}"')
        srcs = sorted(asm.sources)
        if not srcs:
            # Unity skips assemblies with no scripts; we compile a stub so dependents can reference it
            stub = out_dir / f"{name}.__empty__.cs"
            stub.write_text("// empty assembly stub\n")
            lines.append(f'"{stub}"')
        for _, real in srcs:
            lines.append(f'"{real}"')
        rsp.write_text("\n".join(lines) + "\n")
        cmd = [str(DOTNET), str(self.env.csc), "-noconfig"] + (["-shared"] if self.use_server else []) + [f"@{rsp}"]
        t0 = time.time()
        try:
            proc = subprocess.run(cmd, cwd=str(PROJECT_ROOT), capture_output=True, text=True, timeout=600)
        except subprocess.TimeoutExpired:
            res.diagnostics.append(Diagnostic("error", "LS0002", "compiler timed out", rel(asm.source_path), 1, 1, name, {pass_name}))
            return res
        res.seconds = time.time() - t0
        output = (proc.stdout or "") + (proc.stderr or "")
        for line in output.splitlines():
            line = line.strip()
            m = DIAG_RE.match(line)
            if m:
                f = m.group("file")
                fp = Path(f)
                fr = rel(fp if fp.is_absolute() else PROJECT_ROOT / fp)
                res.diagnostics.append(Diagnostic(m.group("sev"), m.group("code"), m.group("msg"), fr,
                                                  int(m.group("line")), int(m.group("col")), name, {pass_name}))
                continue
            m2 = DIAG_NOFILE_RE.match(line)
            if m2:
                sev = "error" if line.startswith("error") else "warning"
                res.diagnostics.append(Diagnostic(sev, m2.group("code"), m2.group("msg"), "", 0, 0, name, {pass_name}))
        has_err = any(d.severity == "error" for d in res.diagnostics)
        if proc.returncode != 0 and not has_err:
            res.diagnostics.append(Diagnostic("error", "LS0003", f"compiler exited {proc.returncode}: {output.strip()[:400]}",
                                              rel(asm.source_path), 1, 1, name, {pass_name}))
            has_err = True
        res.ok = not has_err
        if res.ok:
            self.outputs[(name, pass_name)] = out
        return res


# --------------------------------------------------------------------------------------
# Driver
# --------------------------------------------------------------------------------------


def run_check(targets: Optional[List[str]], all_: bool, overlay: str, passes: List[str], staging_root: Path,
              dependents: bool, warnaserror: bool, jobs: int, use_server: bool, verbose: bool, dev: bool,
              keep: Optional[Path] = None) -> dict:
    env = Environment(dev=dev)
    norm_targets = [normalise_system(t) for t in (targets or [])]
    overlay_all = overlay == "all" or (overlay == "targets" and all_)
    overlay_set = set(norm_targets) if overlay == "targets" else set()
    project = Project(staging_root, overlay_set, overlay_all)
    report = {"ok": False, "problems": list(project.problems), "results": [], "diagnostics": [], "targets": norm_targets,
              "overlay": sorted(project.overlay_systems), "seconds": 0.0, "assemblies": []}
    t0 = time.time()
    if all_:
        target_asms = set(project.asmdefs)
    else:
        target_asms = set()
        for t in norm_targets:
            names = project.assemblies_of_system(t)
            if not names:
                report["problems"].append(f"unknown system '{t}' (known: {', '.join(project.systems())})")
            target_asms.update(names)
    if dependents:
        target_asms = project.dependents(env, target_asms)
    try:
        order = project.topo(env, target_asms)
    except ConfigError as e:
        report["problems"].append(str(e))
        order = []
    report["assemblies"] = order
    if report["problems"]:
        report["seconds"] = time.time() - t0
        return report
    out_root = Path(tempfile.mkdtemp(prefix="ls_compile_")) if keep is None else keep
    try:
        compiler = Compiler(env, project, out_root, warnaserror, use_server, verbose)
        for pass_name in passes:
            done: Set[str] = set()
            failed: Set[str] = set()
            remaining = list(order)
            with cf.ThreadPoolExecutor(max_workers=max(1, jobs)) as pool:
                while remaining:
                    ready = [n for n in remaining if all(d in done or d in failed or d not in order for d in project.our_deps(env, n))]
                    if not ready:
                        report["problems"].append("scheduler stalled (cycle?)")
                        break
                    futs = {pool.submit(compiler.compile, n, pass_name): n for n in ready}
                    for fut in cf.as_completed(futs):
                        n = futs[fut]
                        r = fut.result()
                        report["results"].append(r)
                        (done if r.ok else failed).add(n)
                        if verbose:
                            state = ("skip (" if r.ok else "BLOCKED (") + r.skipped_reason + ")" if r.skipped_reason else ("ok" if r.ok else "FAILED")
                            print(f"  [{pass_name:6}] {n:44} {state:10} {r.file_count:4} files {r.seconds:5.2f}s", file=sys.stderr)
                    remaining = [n for n in remaining if n not in ready]
    finally:
        if keep is None:
            shutil.rmtree(out_root, ignore_errors=True)
    # merge diagnostics across passes
    merged: Dict[tuple, Diagnostic] = {}
    for r in report["results"]:
        for d in r.diagnostics:
            k = d.key()
            if k in merged:
                merged[k].passes |= d.passes
            else:
                merged[k] = dataclasses.replace(d, passes=set(d.passes))
    diags = sorted(merged.values(), key=lambda d: (d.severity != "error", d.file, d.line, d.col))
    report["diagnostics"] = diags
    errors = [d for d in diags if d.severity == "error"]
    report["ok"] = not errors and not report["problems"]
    report["seconds"] = time.time() - t0
    return report


def print_report(report: dict, quiet: bool = False) -> None:
    for p in report["problems"]:
        print(f"config error: {p}")
    for d in report["diagnostics"]:
        print(d.format())
    results = report["results"]
    compiled = [r for r in results if not r.skipped_reason]
    errs = sum(1 for d in report["diagnostics"] if d.severity == "error")
    warns = sum(1 for d in report["diagnostics"] if d.severity == "warning")
    by_pass: Dict[str, int] = {}
    for r in compiled:
        by_pass[r.pass_name] = by_pass.get(r.pass_name, 0) + 1
    passes = ", ".join(f"{k} {v}" for k, v in sorted(by_pass.items()))
    status = "GREEN" if report["ok"] else "RED"
    if not quiet or not report["ok"]:
        print(f"compile_check: {status} — {len(report['assemblies'])} assemblies ({passes}); "
              f"{errs} error(s), {warns} warning(s); overlay={','.join(report['overlay']) or 'none'}; "
              f"{report['seconds']:.1f}s")


def report_to_json(report: dict) -> dict:
    return {
        "ok": report["ok"],
        "problems": report["problems"],
        "targets": report["targets"],
        "overlay": report["overlay"],
        "assemblies": report["assemblies"],
        "seconds": round(report["seconds"], 2),
        "errors": sum(1 for d in report["diagnostics"] if d.severity == "error"),
        "warnings": sum(1 for d in report["diagnostics"] if d.severity == "warning"),
        "diagnostics": [
            {"severity": d.severity, "code": d.code, "message": d.message, "file": d.file, "line": d.line,
             "col": d.col, "assembly": d.assembly, "passes": sorted(d.passes)} for d in report["diagnostics"]
        ],
        "results": [
            {"assembly": r.assembly, "pass": r.pass_name, "ok": r.ok, "skipped": r.skipped_reason,
             "files": r.file_count, "seconds": round(r.seconds, 2)} for r in report["results"]
        ],
    }


def cmd_list(staging_root: Path) -> int:
    env = Environment()
    project = Project(staging_root, set(), True)
    for p in project.problems:
        print("config error:", p)
    print(f"Unity {UNITY_VERSION}; csc {rel(env.csc)}; staging {rel(staging_root)}")
    staged = project.staged_systems_present
    for system, names in project.systems().items():
        flag = "  (staged changes)" if system in staged else ""
        print(f"{system}{flag}")
        for n in names:
            a = project.asmdefs[n]
            refs = [project.resolve_ref(env, r) for r in a.references]
            print(f"  {n:46} {a.kind:7} {len(a.sources):3} files  refs: {', '.join(refs) or '-'}")
    return 0


def cmd_selftest(args) -> int:
    """Green on the current tree, red with an injected error in a scratch staging root."""
    print("selftest 1/2: landed state, no overlay (expect GREEN)")
    r1 = run_check(None, True, "none", ["player", "editor"], args.staging_root, False, False, args.jobs, not args.no_server, False, False)
    print_report(r1)
    scratch = Path(tempfile.mkdtemp(prefix="ls_selftest_staging_"))
    try:
        bad = scratch / "Core" / "__SelfTestBroken.cs"
        bad.parent.mkdir(parents=True)
        bad.write_text("namespace LongShadow.Core { internal static class SelfTestBroken { static int X() { return \"nope\"; } } }\n")
        editor_bad = scratch / "Core" / "Editor" / "__SelfTestEditorLeak.cs"
        editor_bad.parent.mkdir(parents=True)
        # runtime file that uses UnityEditor without #if: must fail only in the player pass
        leak = scratch / "Core" / "__SelfTestEditorLeak.cs"
        leak.write_text("namespace LongShadow.Core { internal static class SelfTestLeak { static void X() { UnityEditor.EditorApplication.Beep(); } } }\n")
        print("selftest 2/2: Core with an injected CS0029 and an unguarded UnityEditor call (expect RED)")
        r2 = run_check(["Core"], False, "targets", ["player", "editor"], scratch, False, False, args.jobs, not args.no_server, False, False)
        print_report(r2)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
    codes = {d.code for d in r2["diagnostics"] if d.severity == "error"}
    leak_player_only = any(d.code in ("CS0234", "CS0246", "CS0103") and d.passes == {"player"} for d in r2["diagnostics"])
    passed = r1["ok"] and not r2["ok"] and "CS0029" in codes and leak_player_only
    print("selftest:", "PASS" if passed else "FAIL",
          f"(clean={'green' if r1['ok'] else 'red'}, injected={'red' if not r2['ok'] else 'green'}, "
          f"CS0029={'yes' if 'CS0029' in codes else 'no'}, editor-leak caught in player pass only={'yes' if leak_player_only else 'no'})")
    return 0 if passed else 1


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0], formatter_class=argparse.RawDescriptionHelpFormatter,
                                 epilog="See Tools/build/README.md for the Staging layout.")
    ap.add_argument("--system", "-s", action="append", default=[], help="system to check (Core, Player, World.Look, World/Level, ...); repeatable")
    ap.add_argument("--all", action="store_true", help="check every LongShadow assembly")
    ap.add_argument("--overlay", choices=["targets", "all", "none"], default="targets",
                    help="which staged systems overlay the landed tree (default: the targets; with --all: all staged)")
    ap.add_argument("--pass", dest="passes", choices=["player", "editor", "both"], default="both")
    ap.add_argument("--staging-root", type=Path, default=DEFAULT_STAGING, help="alternate Staging root (default: Staging/)")
    ap.add_argument("--dependents", action="store_true", help="also compile every assembly that depends on the targets")
    ap.add_argument("--warnaserror", action="store_true", help="treat warnings as errors")
    ap.add_argument("--dev", action="store_true", help="player pass as a development build (DEVELOPMENT_BUILD, LS_DEV, ...)")
    ap.add_argument("--jobs", "-j", type=int, default=4, help="parallel compiler invocations (default 4)")
    ap.add_argument("--no-server", action="store_true", help="do not use the Roslyn compiler server (VBCSCompiler)")
    ap.add_argument("--json", type=Path, help="write a machine-readable report here")
    ap.add_argument("--keep", type=Path, help="keep compiled DLLs and .rsp files in this folder")
    ap.add_argument("--list", action="store_true", help="list systems, assemblies and references, then exit")
    ap.add_argument("--selftest", action="store_true", help="prove green on the clean tree and red on an injected error")
    ap.add_argument("-v", "--verbose", action="store_true")
    ap.add_argument("-q", "--quiet", action="store_true", help="print only diagnostics and a RED summary")
    args = ap.parse_args(argv)
    args.staging_root = args.staging_root.resolve()
    try:
        if args.list:
            return cmd_list(args.staging_root)
        if args.selftest:
            return cmd_selftest(args)
        if not args.all and not args.system:
            ap.error("give --system NAME (repeatable) or --all")
        passes = ["player", "editor"] if args.passes == "both" else [args.passes]
        if args.keep:
            args.keep.mkdir(parents=True, exist_ok=True)
        report = run_check(args.system, args.all, args.overlay, passes, args.staging_root, args.dependents,
                           args.warnaserror, args.jobs, not args.no_server, args.verbose, args.dev, args.keep)
    except ConfigError as e:
        print(f"config error: {e}")
        return 2
    print_report(report, quiet=args.quiet)
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(report_to_json(report), indent=2))
    if report["problems"]:
        return 2
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
