"""Single source of truth for the LongShadow assembly layout (TECH_PLAN §4.1, §4.3).

Used by ``skeleton.py`` (generates folders, asmdefs and placeholder files) and by
``land.py`` (validates staged asmdef changes against the reference rules).

Rule (§4.3): a system assembly references only ``LongShadow.Core`` (+ Unity
packages). Only ``LongShadow.Integration`` references other systems. Editor
assemblies may reference Core, Core.Editor and their own runtime assembly. Test
assemblies may reference Core, their own runtime (and, for EditMode, their own
Editor assembly).
"""
from __future__ import annotations

from typing import Dict, List

# Package assembly names (Library/ScriptAssemblies/<name>.dll).
ADDR = ["Unity.Addressables", "Unity.ResourceManager"]
RP_CORE = ["Unity.RenderPipelines.Core.Runtime"]
URP = ["Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime"]
INPUT = ["Unity.InputSystem"]
TMP = ["Unity.TextMeshPro"]
UGUI = ["UnityEngine.UI"]
TIMELINE = ["Unity.Timeline"]
SPLINES = ["Unity.Splines", "Unity.Mathematics"]
NAV = ["Unity.AI.Navigation"]
RIGGING = ["Unity.Animation.Rigging"]
MATH = ["Unity.Mathematics"]

# Order matters only for readability; the compile order comes from the references.
# key: system id (folder under Assets/_Game/Scripts with '/' -> '.')
SYSTEMS: Dict[str, dict] = {
    "Core": {
        "wp": "WP00",
        "runtime": ADDR + RP_CORE,
        "editor": ADDR + ["Unity.Addressables.Editor"] + URP + ["Unity.RenderPipelines.Core.Editor",
                  "Unity.RenderPipelines.Universal.Editor"] + INPUT + ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    },
    "Input": {"wp": "WP00", "runtime": INPUT, "editor": INPUT},
    "Platform": {
        "wp": "WP01",
        "runtime": ADDR + URP,
        "editor": ADDR + ["Unity.Addressables.Editor", "Unity.ScriptableBuildPipeline", "Unity.ScriptableBuildPipeline.Editor"]
                  + URP + ["Unity.RenderPipelines.Universal.Editor"],
    },
    "Player": {"wp": "WP02", "runtime": URP, "editor": URP + ["Unity.RenderPipelines.Universal.Editor"]},
    "Combat": {"wp": "WP03", "runtime": URP, "editor": URP + ["Unity.RenderPipelines.Universal.Editor"]},
    "AI": {"wp": "WP04", "runtime": NAV + SPLINES, "editor": NAV + ["Unity.AI.Navigation.Editor"] + SPLINES + ["Unity.Splines.Editor"]},
    "Characters": {"wp": "WP05", "runtime": RIGGING, "editor": RIGGING + ["Unity.Animation.Rigging.Editor"]},
    "Audio": {"wp": "WP06", "runtime": [], "editor": []},
    "Mission": {"wp": "WP07", "runtime": [], "editor": []},
    "UI": {"wp": "WP08", "runtime": TMP + UGUI + INPUT, "editor": TMP + UGUI + INPUT + ["Unity.TextMeshPro.Editor", "UnityEditor.UI"]},
    "Cinematics": {
        "wp": "WP09",
        "runtime": TIMELINE + TMP + UGUI + URP,
        "editor": TIMELINE + ["Unity.Timeline.Editor"] + TMP + UGUI + URP + ["Unity.RenderPipelines.Universal.Editor",
                  "Unity.Recorder", "Unity.Recorder.Editor", "Unity.Recorder.Base"],
    },
    "World.Look": {
        "wp": "WP10",
        "runtime": URP + MATH,
        "editor": URP + MATH + ["Unity.RenderPipelines.Core.Editor", "Unity.RenderPipelines.Universal.Editor"],
    },
    "World.Level": {
        "wp": "WP10",
        "runtime": SPLINES + NAV + URP + TMP,
        "editor": SPLINES + ["Unity.Splines.Editor"] + NAV + ["Unity.AI.Navigation.Editor"] + URP
                  + ["Unity.RenderPipelines.Universal.Editor"] + TMP + ["Unity.TextMeshPro.Editor"],
    },
    "Integration": {
        "wp": "WP11",
        "runtime": None,  # filled below: every system + every package used by any system
        "editor": None,
    },
}

RUNTIME_SYSTEMS = [s for s in SYSTEMS if s != "Integration"]


def _uniq(xs: List[str]) -> List[str]:
    seen = set()
    out = []
    for x in xs:
        if x not in seen:
            seen.add(x)
            out.append(x)
    return out


_all_runtime_pkgs = _uniq([p for s in RUNTIME_SYSTEMS for p in SYSTEMS[s]["runtime"]])
_all_editor_pkgs = _uniq([p for s in RUNTIME_SYSTEMS for p in SYSTEMS[s]["editor"]])
SYSTEMS["Integration"]["runtime"] = _all_runtime_pkgs
SYSTEMS["Integration"]["editor"] = _uniq(_all_runtime_pkgs + _all_editor_pkgs)

TEST_FRAMEWORK = ["UnityEngine.TestRunner", "UnityEditor.TestRunner"]


def asm_runtime(system: str) -> str:
    return "LongShadow." + system


def asm_editor(system: str) -> str:
    return "LongShadow." + system + ".Editor"


def asm_tests(system: str, mode: str) -> str:
    return f"LongShadow.Tests.{mode}.{system}"


def folder(system: str) -> str:
    return system.replace(".", "/")


def our_runtime_refs(system: str) -> List[str]:
    if system == "Core":
        return []
    if system == "Integration":
        return [asm_runtime(s) for s in RUNTIME_SYSTEMS]
    return ["LongShadow.Core"]


def our_editor_refs(system: str) -> List[str]:
    if system == "Core":
        return ["LongShadow.Core"]
    refs = ["LongShadow.Core", "LongShadow.Core.Editor", asm_runtime(system)]
    if system == "Integration":
        refs = ["LongShadow.Core", "LongShadow.Core.Editor"] + [asm_runtime(s) for s in RUNTIME_SYSTEMS if s != "Core"] \
               + [asm_runtime("Integration")] + [asm_editor(s) for s in RUNTIME_SYSTEMS if s != "Core"]
    return _uniq(refs)


def our_test_refs(system: str, mode: str) -> List[str]:
    refs = ["LongShadow.Core", asm_runtime(system)]
    if system == "Integration":
        refs = [asm_runtime(s) for s in RUNTIME_SYSTEMS] + [asm_runtime("Integration")]
    if mode == "EditMode":
        refs += ["LongShadow.Core.Editor", asm_editor(system)]
    return _uniq(refs)


def allowed_our_refs(asm_name: str) -> List[str]:
    """LongShadow.* references an assembly may have (used by land.py to validate staged asmdefs)."""
    for s in SYSTEMS:
        if asm_name == asm_runtime(s):
            extra = []
            if s == "World.Level":
                extra = []  # decided: Level talks to Look through Core only (DECISIONS.md)
            return our_runtime_refs(s) + extra
        if asm_name == asm_editor(s):
            return our_editor_refs(s)
        for mode in ("EditMode", "PlayMode"):
            if asm_name == asm_tests(s, mode):
                return our_test_refs(s, mode)
    return []


def system_of_assembly(asm_name: str) -> str:
    for s in SYSTEMS:
        if asm_name in (asm_runtime(s), asm_editor(s), asm_tests(s, "EditMode"), asm_tests(s, "PlayMode")):
            return s
    return ""
