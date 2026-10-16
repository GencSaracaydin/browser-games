#!/usr/bin/env python3
"""Generates Docs/CORE_CONTRACTS.md: a compact reference of every public type in LongShadow.Core (+ Core.Editor and
LongShadow.Input), one line each, read from the landed sources and their XML <summary> docs.

    python3 Tools/build/gen_core_contracts.py            # write Docs/CORE_CONTRACTS.md
    python3 Tools/build/gen_core_contracts.py --check    # exit 1 if it is out of date

Run it after every Core change (WP00, later WP11). It is a reading aid: the C# files are the contract.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CORE = ROOT / "Assets" / "_Game" / "Scripts" / "Core"
INPUT = ROOT / "Assets" / "_Game" / "Scripts" / "Input"
OUT = ROOT / "Docs" / "CORE_CONTRACTS.md"

TYPE_RE = re.compile(
    r"^\s*public\s+(?:(?:sealed|static|abstract|readonly|partial)\s+)*(class|struct|interface|enum)\s+(\w+)(<[^>]+>)?([^{\n]*)",
    re.M)
SUMMARY_RE = re.compile(r"((?:^\s*///.*\n)+)\s*(?:\[[^\n]*\]\s*\n\s*)*public\s", re.M)


def clean_doc(lines: str) -> str:
    text = " ".join(l.strip()[3:].strip() for l in lines.strip().splitlines())
    text = re.sub(r"<see(?:also)? cref=\"([^\"]+)\"\s*/>", lambda m: "`" + m.group(1).split(".")[-1].split("(")[0] + "`", text)
    text = re.sub(r"<paramref name=\"([^\"]+)\"\s*/>", r"`\1`", text)
    text = re.sub(r"<c>(.*?)</c>", r"`\1`", text)
    text = re.sub(r"<code>.*?</code>", "", text)
    text = re.sub(r"</?(summary|para|b|i|remarks|inheritdoc)\s*/?>", " ", text)
    text = re.sub(r"<[^>]+>", "", text)
    text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
    return re.sub(r"\s+", " ", text).strip()


def first_sentence(text: str, limit: int = 240) -> str:
    depth, cut = 0, len(text)
    for i, ch in enumerate(text):
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth = max(0, depth - 1)
        elif ch == "." and depth == 0 and (i + 1 == len(text) or text[i + 1].isspace()):
            # not an abbreviation such as "e.g." or a section number such as "§6.4."
            if not re.search(r"(e\.g|i\.e|etc|vs|approx)$", text[:i]):
                cut = i + 1
                break
    s = text[:cut]
    if len(s) > limit:
        s = s[: limit - 1].rstrip() + "…"
    return s


def type_summary(src: str, start: int) -> str:
    head = src[:start]
    m = None
    for m in re.finditer(r"((?:^[ \t]*///.*\n)+)((?:^[ \t]*\[.*\]\s*\n)*)", head, re.M):
        pass
    if m and m.end() >= len(head.rstrip(" \t")) - 1:
        return clean_doc(m.group(1))
    return ""


def body_of(src: str, start: int) -> str:
    i = src.index("{", start)
    depth = 0
    for j in range(i, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[i + 1: j]
    return src[i + 1:]


def strip_nested(body: str) -> str:
    """Removes nested { ... } blocks (method bodies, nested types) so only member heads remain."""
    out, depth = [], 0
    for ch in body:
        if ch == "{":
            depth += 1
            out.append(";" if depth == 1 else "")
            continue
        if ch == "}":
            depth -= 1
            continue
        if depth == 0:
            out.append(ch)
    return "".join(out)


def members(body: str, kind: str):
    flat = strip_nested(body)
    flat = re.sub(r"^\s*//.*$", "", flat, flags=re.M)
    flat = re.sub(r"^\s*\[[^\]]*\]\s*$", "", flat, flags=re.M)
    res = []
    for stmt in re.split(r";", flat):
        s = " ".join(stmt.split())
        if not s or s.startswith("#"):
            continue
        s = re.sub(r"\[[^\]]*\]\s*", "", s)
        if kind != "interface" and not s.startswith("public"):
            continue
        s = s.replace("public ", "").replace("static ", "static ").strip()
        s = re.sub(r"\s*=>.*$", "", s)
        s = re.sub(r"\s*=\s*[^,()]+$", "", s) if "(" not in s else s
        if s in ("get", "set") or s.startswith(("get ", "set ")):
            continue
        res.append(s)
    return res


def enum_values(body: str):
    flat = re.sub(r"///.*", "", body)
    flat = re.sub(r"//.*", "", flat)
    vals = []
    for part in flat.split(","):
        p = part.strip()
        if p:
            vals.append(p.split("=")[0].strip())
    return vals


def collect(folder: Path):
    items = []
    for f in sorted(folder.rglob("*.cs")):
        if f.name == "AssemblyInfo.cs":
            continue
        src = f.read_text(encoding="utf-8")
        ns = re.search(r"namespace\s+([\w.]+)", src)
        for m in TYPE_RE.finditer(src):
            # skip nested types (indent deeper than the namespace level)
            indent = len(m.group(0)) - len(m.group(0).lstrip(" "))
            if indent > 4:
                continue
            kind, name, generic, tail = m.group(1), m.group(2), m.group(3) or "", m.group(4).strip()
            summary = type_summary(src, m.start())
            items.append({"file": f.relative_to(ROOT).as_posix(), "kind": kind, "name": name + generic,
                          "base": tail.lstrip(":").strip(), "summary": summary, "body": body_of(src, m.end()),
                          "ns": ns.group(1) if ns else "", "folder": f.parent.name})
    return items


def esc(s: str) -> str:
    return s.replace("|", "\\|")


def render() -> str:
    core = collect(CORE)
    inp = collect(INPUT)
    by = lambda pred: [t for t in core if pred(t)]
    lines = ["# Core contracts reference", "",
             "Generated by `Tools/build/gen_core_contracts.py` from the landed sources (`Assets/_Game/Scripts/Core`, "
             "`Assets/_Game/Scripts/Input`). The C# files and their XML docs are the contract; this page is the index. "
             "Changes are additive only and go through `Docs/CORE_CHANGES.md` (TECH_PLAN §6).", "",
             "How systems use it: register your service in `Awake` with `GameServices.Register<IMyService>(this)`; "
             "look services up lazily with a `ServiceRef<T>` field (`_audio.Value?.Play2D(...)`), never by scene reference; "
             "find level markup by id through `IMarkerRegistry` (ids in `Assets/_Game/Data/markup_ids.csv`); "
             "raise and subscribe to `GameEvents` (subscribe in `OnEnable`, unsubscribe in `OnDisable`); "
             "look assets up through `Libraries.Get<T>()` (runtime) or `AssetManifest.Find<T>(id)` (editor builders) and "
             "fall back to `Placeholders` when an asset has not landed (log with `LSLog.MissingAsset`). "
             "Every static resets on `SubsystemRegistration` (domain reload is off, R6).", ""]

    def section(title, items, show_members=False):
        if not items:
            return
        lines.append(f"## {title}")
        lines.append("")
        lines.append("| Type | Summary |" + (" Members |" if show_members else ""))
        lines.append("|---|---|" + ("---|" if show_members else ""))
        for t in sorted(items, key=lambda x: x["name"]):
            label = f"`{t['name']}`" + (f" : {t['base']}" if t["base"] and t["kind"] != "enum" else "")
            row = f"| {esc(label)} | {esc(first_sentence(t['summary']))} |"
            if show_members:
                if t["kind"] == "enum":
                    mem = ", ".join(enum_values(t["body"]))
                else:
                    mem = "; ".join(members(t["body"], t["kind"]))
                row += f" {esc(mem)} |"
            lines.append(row)
        lines.append("")

    section("Service interfaces (`GameServices.Register/Get<T>`)",
            by(lambda t: t["kind"] == "interface" and t["folder"] == "Interfaces"), True)
    section("Enums", by(lambda t: t["kind"] == "enum"), True)
    section("Data types (structs and classes in Types/, Save/)",
            by(lambda t: t["kind"] in ("struct", "class") and t["folder"] in ("Types", "Save")), True)

    ge = next((t for t in core if t["name"] == "GameEvents"), None)
    if ge:
        lines += ["## Events (`GameEvents`, raise only through `GameEvents.Raise<Name>(...)`)", "",
                  "| Event | Payload | When |", "|---|---|---|"]
        for m in re.finditer(r"((?:^\s*///.*\n)+)\s*public static event (Action(?:<[^;]*>)?) (\w+);", ge["body"], re.M):
            payload = m.group(2)[len("Action"):].strip("<>") or "-"
            lines.append(f"| `{m.group(3)}` | {esc(payload)} | {esc(first_sentence(clean_doc(m.group(1))))} |")
        lines.append("")

    section("Markup components (data only, gizmos; placed by WP10/WP09, found by id)",
            by(lambda t: t["folder"] == "Markup" and t["kind"] in ("class", "struct", "enum") and t["name"] != "MarkupGizmos"))
    section("Generic components", by(lambda t: t["folder"] == "Components"))
    section("Runtime utilities, registries and id tables",
            by(lambda t: t["folder"] == "Runtime" and t["kind"] != "enum"))
    section("Core.Editor (builders, settings, tests; editor only)", by(lambda t: t["folder"] == "Editor"))
    lines.append("## Input (`LongShadow.Input`, WP00)")
    lines.append("")
    lines.append("| Type | Summary |")
    lines.append("|---|---|")
    for t in sorted(inp, key=lambda x: x["name"]):
        lines.append(f"| `{t['name']}` | {esc(first_sentence(t['summary']))} |")
    lines.append("")
    return "\n".join(lines) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    text = render()
    if a.check:
        same = OUT.exists() and OUT.read_text() == text
        print("CORE_CONTRACTS.md " + ("up to date" if same else "OUT OF DATE"))
        return 0 if same else 1
    OUT.write_text(text)
    print(f"wrote {OUT.relative_to(ROOT)} ({text.count(chr(10))} lines)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
