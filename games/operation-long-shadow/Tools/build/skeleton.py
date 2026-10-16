#!/usr/bin/env python3
"""Creates the Assets/_Game folder skeleton, every LongShadow asmdef and the Staging/ tree.

Idempotent: existing files are left alone unless --force is given (then asmdefs and
placeholder AssemblyInfo.cs files are rewritten from ls_layout.py). Never deletes.

    python3 Tools/build/skeleton.py            # create what is missing
    python3 Tools/build/skeleton.py --check    # report what is missing or differs, change nothing
    python3 Tools/build/skeleton.py --force    # rewrite asmdefs/placeholders from the table
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ls_layout as L  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "Assets" / "_Game"

ART_FOLDERS = [
    "Art/Materials/" + x for x in ["Env", "Props", "Characters", "Weapons", "Vehicles", "FX", "Sky", "UI"]
] + [
    "Art/Textures/" + x for x in ["Env", "Props", "Characters", "Weapons", "Vehicles", "Decals", "Images", "FX"]
] + [
    "Art/Models/" + x for x in ["Kit", "Props", "Weapons", "Vehicles", "Vegetation", "Characters", "Terrain", "Backdrop"]
] + [
    "Art/Animation/" + x for x in ["Clips", "Controllers", "Masks", "Poses"]
] + ["Art/HDRI/Runtime", "Art/HDRI/EditorOnly", "Art/Shaders", "Art/VFX", "Art/Fonts"]

AUDIO_FOLDERS = (
    ["Audio/SFX/" + x for x in ["Weapons", "Weapons/Far", "Impacts", "Foley", "Footsteps", "Explosions", "Whizz", "Synth",
                                "SetPieces", "Vehicles", "UI"]]
    + ["Audio/VO/" + x for x in ["RADIO", "INTERCOM", "HF_AM", "GREYCOAT_SQUELCH", "PA_HORN", "DRY", "FAR", "SCRAMBLER",
                                 "TAPE", "CLEAN", "BOOTH", "ROOM_SPEAKER"]]
    + ["Audio/Music", "Audio/Ambience", "Audio/Envelopes"]
)

OTHER_FOLDERS = (
    ["Prefabs/" + x for x in ["Systems", "Player", "Squad", "Bodies", "Enemies", "Weapons", "Props", "Kit", "Vehicles", "FX", "Booth"]]
    + ["Scenes/Sections", "Scenes/Dev", "Timelines", "UI/Sprites", "UI/Themes", "Data/Libraries", "Data/Encounters",
       "Data/Lighting", "Settings/URP", "Settings/Volumes", "Settings/Lighting", "Settings/Input", "Plugins/WebGL",
       "Tests/EditMode", "Tests/PlayMode"]
)

# Sub-namespaces called out in §4.1 (folders only; one assembly per system).
SUBFOLDERS = {
    "AI": ["Common", "Enemy", "Squad", "Encounters"],
    "Audio": ["Core", "VO", "Music", "Ambience"],
    "Mission": ["Beats", "Objectives", "Checkpoints"],
    "UI": ["Hud", "Menus", "Screens"],
    "Cinematics": ["Playables", "Inserts", "Booth"],
}


def asmdef_json(name: str, root_ns: str, refs, include_platforms=None, define_constraints=None,
                precompiled=None, override_refs=False) -> str:
    data = {
        "name": name,
        "rootNamespace": root_ns,
        "references": L._uniq(list(refs)),
        "includePlatforms": include_platforms or [],
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "overrideReferences": override_refs,
        "precompiledReferences": precompiled or [],
        "autoReferenced": False,
        "defineConstraints": define_constraints or [],
        "versionDefines": [],
        "noEngineReferences": False,
    }
    return json.dumps(data, indent=4) + "\n"


def placeholder_runtime(system: str) -> str:
    asm = L.asm_runtime(system)
    friends = [L.asm_editor(system), L.asm_tests(system, "EditMode"), L.asm_tests(system, "PlayMode")]
    lines = [
        f"// {asm}: assembly attributes (WP00 skeleton). Keep this file; add code in other files.",
        "using System.Runtime.CompilerServices;",
        "",
    ]
    lines += [f'[assembly: InternalsVisibleTo("{f}")]' for f in friends]
    return "\n".join(lines) + "\n"


def placeholder_plain(asm: str) -> str:
    return f"// {asm}: placeholder so the assembly compiles while empty (WP00 skeleton). Safe to keep.\n"


class Writer:
    def __init__(self, force: bool, check: bool):
        self.force, self.check = force, check
        self.created, self.updated, self.differs = [], [], []

    def folder(self, p: Path):
        if not p.exists():
            if self.check:
                self.differs.append(f"missing folder {p.relative_to(ROOT)}")
                return
            p.mkdir(parents=True, exist_ok=True)
            self.created.append(str(p.relative_to(ROOT)) + "/")
        keep = p / ".gitkeep"
        if not self.check and not any(x for x in p.iterdir() if x.name != ".DS_Store"):
            keep.write_text("")

    def file(self, p: Path, content: str, managed: bool = True):
        if p.exists():
            if p.read_text() == content:
                return
            if self.check:
                self.differs.append(f"differs {p.relative_to(ROOT)}")
                return
            if self.force and managed:
                p.write_text(content)
                self.updated.append(str(p.relative_to(ROOT)))
            return
        if self.check:
            self.differs.append(f"missing {p.relative_to(ROOT)}")
            return
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(content)
        self.created.append(str(p.relative_to(ROOT)))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    w = Writer(a.force, a.check)

    for f in ART_FOLDERS + AUDIO_FOLDERS + OTHER_FOLDERS:
        w.folder(GAME / f)
    for extra in ["Assets/StreamingAssets/video", "Assets/WebGLTemplates"]:
        w.folder(ROOT / extra)

    for system, spec in L.SYSTEMS.items():
        sdir = GAME / "Scripts" / L.folder(system)
        # runtime
        w.file(sdir / f"{L.asm_runtime(system)}.asmdef",
               asmdef_json(L.asm_runtime(system), L.asm_runtime(system), L.our_runtime_refs(system) + spec["runtime"]))
        w.file(sdir / "AssemblyInfo.cs", placeholder_runtime(system))
        for sub in SUBFOLDERS.get(system, []):
            w.folder(sdir / sub)
        # editor
        edir = sdir / "Editor"
        w.file(edir / f"{L.asm_editor(system)}.asmdef",
               asmdef_json(L.asm_editor(system), L.asm_editor(system), L.our_editor_refs(system) + spec["editor"],
                           include_platforms=["Editor"]))
        w.file(edir / "AssemblyInfo.cs", placeholder_plain(L.asm_editor(system)))
        # tests
        for mode in ("EditMode", "PlayMode"):
            tdir = GAME / "Tests" / mode / L.folder(system)
            pkgs = spec["editor"] if mode == "EditMode" else spec["runtime"]
            w.file(tdir / f"{L.asm_tests(system, mode)}.asmdef",
                   asmdef_json(L.asm_tests(system, mode), L.asm_tests(system, mode),
                               L.our_test_refs(system, mode) + pkgs + L.TEST_FRAMEWORK,
                               include_platforms=["Editor"] if mode == "EditMode" else [],
                               define_constraints=["UNITY_INCLUDE_TESTS"],
                               precompiled=["nunit.framework.dll"], override_refs=True))
            w.file(tdir / "AssemblyInfo.cs", placeholder_plain(L.asm_tests(system, mode)))
        # staging mirrors (empty folders; asmdefs are NOT mirrored, see Tools/build/README.md)
        for p in [ROOT / "Staging" / L.folder(system), ROOT / "Staging" / "Tests" / "EditMode" / L.folder(system),
                  ROOT / "Staging" / "Tests" / "PlayMode" / L.folder(system)]:
            w.folder(p)
    w.folder(ROOT / "Staging" / "Assets")

    if a.check:
        print("\n".join(w.differs) or "skeleton: complete")
        return 1 if w.differs else 0
    print(f"skeleton: {len(w.created)} created, {len(w.updated)} updated")
    for c in w.created + w.updated:
        print("  ", c)
    return 0


if __name__ == "__main__":
    sys.exit(main())
