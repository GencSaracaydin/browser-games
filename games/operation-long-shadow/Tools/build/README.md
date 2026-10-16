# Tools/build: compile gate, landing, layout

Code packages never write `.cs` files into `Assets/` directly (TECH_PLAN D18). You write under `Staging/`, prove it compiles offline with `compile_check.py`, then `land.py` copies it into `Assets/` behind the same gate. Nobody but the editor holder calls Unity MCP tools.

| Script | Purpose |
|---|---|
| `compile_check.py` | Offline compile of our assemblies with Unity's bundled Roslyn (`.../Scripting/DotNetSdk`), two passes: **player** (WebGL, `UNITY_WEBGL`, no `UNITY_EDITOR`, no UnityEditor.dll) and **editor** (`UNITY_EDITOR`, UnityEditor + editor modules). Prints `file:line:col: error CSxxxx` lines. |
| `land.py` | Lands `Staging/<System>` into `Assets/_Game/Scripts/<System>` (and the system's tests) only if the gate is green; logs to `Docs/LANDING_LOG.md`. |
| `ls_layout.py` | The single table of systems, assemblies and allowed references (TECH_PLAN §4.3). |
| `skeleton.py` | Creates/checks the folder + asmdef skeleton from `ls_layout.py` (WP00/WP11 only). |
| `gen_markup_csv.py` | Regenerates `Assets/_Game/Data/markup_ids.csv` from TECH_PLAN Appendix A (+ CINEMATIC_DIRECTION §2). Owned by WP10 after Phase A. |
| `editor_lock.py` | Optional editor slot lock (TECH_PLAN §11.2). The workflow already serialises editor holders. |
| `validate_assets.py` | Offline asset validator (TECH_PLAN §8.9/§8.10): naming, texture caps as the import rules will apply them, D4 masks, LOD0 triangles vs manifest budgets, audio folders/naming/durations, download estimate per §8.10 line, manifest coverage. WP00-B. |
| `copy_ready.py` | Copies content agents' `RawAssets/<wp>/ready/Assets/**` into `Assets/` (only `_Game/` and `StreamingAssets/video/`; never metas; identical files skipped; differing targets are conflicts unless `--overwrite`; `--validate` runs validate_assets first). Editor holder only, then refresh + MaterialFactory. WP00-B. |
| `asset_budgets.json` | Budget data shared by the Unity import rules (`LongShadow.Core.Editor.ImportBudgetsFile`) and `validate_assets.py`: §3.10/§8.9 lists, folder cap overrides, lightmapped props, class triangle caps, audio limits, download basis. Edit, then run **LongShadow/Import/Reapply Rules (All Game Assets)**. |

Run everything from the project root with the system python (`python3`, 3.13; no packages needed).

## Staging layout

`Staging/` mirrors the landed tree. **Asmdefs are not mirrored**: every system folder in `Assets/_Game/Scripts` and `Assets/_Game/Tests` already has its asmdef and a placeholder `AssemblyInfo.cs`, and the gate compiles your staged files *into those landed asmdefs*. Put only `.cs` files in Staging.

```
Staging/<System>/**.cs                 -> Assets/_Game/Scripts/<System>/**        assembly LongShadow.<System>
Staging/<System>/Editor/**.cs          -> Assets/_Game/Scripts/<System>/Editor/** assembly LongShadow.<System>.Editor
Staging/Tests/EditMode/<System>/**.cs  -> Assets/_Game/Tests/EditMode/<System>/** assembly LongShadow.Tests.EditMode.<System>
Staging/Tests/PlayMode/<System>/**.cs  -> Assets/_Game/Tests/PlayMode/<System>/** assembly LongShadow.Tests.PlayMode.<System>
Staging/Assets/**                      -> Assets/**   (non-code assets only, `land.py --assets`, no compile gate)
```

`<System>` is one of `Core Input Platform Player Combat AI Characters Audio Mission UI Cinematics World/Look World/Level Integration`. On the command line a system is written with a dot or a slash: `World.Look` = `World/Look`. Sub-folders inside a system (`AI/Squad`, `Audio/VO`, `Mission/Beats`) are plain folders of the same assembly; namespaces follow folders (`LongShadow.AI.Squad`).

Rules the gate enforces:
- A `.cs` file must sit under a folder that has an asmdef (anything else would fall into Assembly-CSharp and is a configuration error).
- References follow TECH_PLAN §4.3: a system assembly sees **only `LongShadow.Core`** (+ its Unity packages); Editor assemblies see Core, Core.Editor and their own runtime; tests see Core + their system (+ the Editor assemblies in EditMode). Only Integration references other systems. `LongShadow.World.Level` does **not** reference `LongShadow.World.Look` (they talk through Core, e.g. `ILightStage`).
- Need another *Unity package* reference (say `Unity.Splines` in Cinematics)? Stage a copy of your landed asmdef with the extra name in `references` (assembly names, never `GUID:`); `land.py` accepts a staged asmdef only if it replaces an existing one of the same name and adds no forbidden `LongShadow.*` reference. A brand-new asmdef or a package that is not installed needs a request in `Docs/REQUESTS.md`.
- Warnings in your staged files block landing (use `--allow-warnings` only for warnings you cannot fix, and say why in your handoff).
- Never use `System.Reflection` in runtime code (R5), never `#if UNITY_EDITOR` around code the player needs: the player pass compiles without UnityEditor.dll and catches `UnityEditor.*` leaks.

Removing a file you landed earlier: put a `DELETE.txt` in the staged folder listing paths relative to that folder (one per line, `#` comments allowed). `land.py` deletes only files it created itself (recorded in `Tools/build/landed_files.json`) together with their `.meta`; anything else is refused and reported.

## compile_check.py

```
python3 Tools/build/compile_check.py --system Player                 # staged Player (+ Player.Editor + Player tests) over the landed tree
python3 Tools/build/compile_check.py --system World.Look --system World.Level
python3 Tools/build/compile_check.py --system Core --dependents      # a Core change against every landed system
python3 Tools/build/compile_check.py --all                           # everything, every staged system overlaid
python3 Tools/build/compile_check.py --all --overlay none            # the landed state only
python3 Tools/build/compile_check.py --system Combat --pass player   # only the WebGL player pass
python3 Tools/build/compile_check.py --system Audio --dev            # player pass as a development build (LS_DEV, DEVELOPMENT_BUILD)
python3 Tools/build/compile_check.py --system AI --json /tmp/ai.json # machine-readable report
python3 Tools/build/compile_check.py --list                          # systems, assemblies, references
python3 Tools/build/compile_check.py --selftest                      # green on the clean tree, red on an injected error
```

Exit codes: `0` green, `1` compile errors (or warnings with `--warnaserror`), `2` configuration problem (missing reference, bad asmdef, file outside any asmdef). Output lines look like
`Staging/Player/PlayerMotor.cs:41:17: error CS0103: The name 'foo' does not exist in the current context [LongShadow.Player; editor+player]`
(`[assembly; passes]`: an error only in `player` means you used editor-only API without a guard).

What it compiles against: `UnityEngine.*Module.dll` (WebGL player modules for the player pass, editor modules for the editor pass), `UnityEditor.dll` (editor pass), .NET Standard 2.1 for runtime assemblies and the .NET 4.8 profile for editor-only ones (like Unity), package assemblies from `Library/ScriptAssemblies` (read only; compiled by the editor), precompiled package DLLs from `Library/PackageCache`, and our own assemblies compiled from source in dependency order (staged Core if staged, else landed Core). Defines are read from the editor's own `.rsp` files (`UNITY_6000_6_OR_NEWER` chain, `ENABLE_INPUT_SYSTEM`, ...) plus asmdef `versionDefines`/`defineConstraints`. Typical run: 1-3 s per system, about 3 s for `--all`.

## land.py

```
python3 Tools/build/land.py --status                        # what differs between Staging and Assets
python3 Tools/build/land.py Player --wp WP02 --dry-run      # gate + plan, copies nothing
python3 Tools/build/land.py Player --wp WP02                # land Staging/Player + Staging/Tests/*/Player
python3 Tools/build/land.py World.Look --wp WP10            # one track of WP10
python3 Tools/build/land.py --all --wp WP11                 # every staged system, dependency order (Core, Input, ..., Integration)
python3 Tools/build/land.py --assets --wp WP01              # Staging/Assets/** (e.g. the WebGL template), no compile gate
```

- The gate for a system is the full `compile_check` of that system **against the current landed state** (both passes), plus every landed assembly that depends on it.
- Only `.cs`/`.asmdef` files land from a system's staging; other files are listed as ignored. Unchanged files are not rewritten (no needless reimports); writes are atomic (temp file + rename). Staging is never modified, so keep editing there and land again.
- `--assets` copies non-code files only, never over a file it did not create unless `--overwrite`.
- Every attempt (LANDED / RED / REFUSED) is appended to `Docs/LANDING_LOG.md`.
- Landing writes into `Assets/`: the open editor recompiles and reloads its domain (20-30 s in which Unity MCP calls fail). That is expected; the editor holder refreshes and checks the console at the start of every slot. Because the gate is green, a landing never breaks compilation.

## Typical loop for a code agent

1. Write `Staging/<System>/...` and `Staging/Tests/EditMode/<System>/...` (namespace `LongShadow.<System>[.<Folder>]`).
2. `python3 Tools/build/compile_check.py --system <System>` until green with 0 warnings.
3. `python3 Tools/build/land.py <System> --wp <WPxx>`.
4. Record in your handoff which editor steps (menu items, tests) the editor holder must run.

## validate_assets.py (asset budgets, WP00-B)

```
python3 Tools/build/validate_assets.py                      # Assets/_Game + Assets/StreamingAssets/video
python3 Tools/build/validate_assets.py --ready              # every RawAssets/<wp>/ready tree, as if it had landed
python3 Tools/build/validate_assets.py --ready wp10 --ready audio
python3 Tools/build/validate_assets.py --path RawAssets/wp03/ready/Assets/_Game/Art/Models/Weapons/AK
python3 Tools/build/validate_assets.py --coverage           # manifest ids: landed / ready / missing (P0 list)
Tools/.venv/bin/python Tools/build/validate_assets.py --ready --pixels    # + pixel checks (numpy, Pillow)
python3 Tools/build/validate_assets.py --check-mirror       # Python rule mirror vs the editor's Logs/import_rules_table.tsv
python3 Tools/build/validate_assets.py --json Logs/va.json --strict
```

Exit codes: `0` no errors, `1` errors (or warnings with `--strict`), `2` usage/configuration problem. The full report goes to `Logs/validate_assets.txt` (`--out` to change). Stdlib only; `--pixels` needs numpy + Pillow.

Checks (code, severity): `TEX_NAMING` error (surface textures `T_<Name>_<BC|N|M|E|H|A>`), `TEX_UNREADABLE` error, `TEX_MASK_NO_ALPHA` error (D4 mask without smoothness alpha), `TEX_NPOT_MIPS` error, `TEX_SET_INCOMPLETE` warning (BC without _M; info without _N), `TEX_ORPHAN` warning, `TEX_NPOT` info, `TEX_OVERSIZE_SOURCE` info; with `--pixels`: `TEX_NORMAL_SUSPECT`, `TEX_ALBEDO_RANGE` warnings, `TEX_MASK_AO_DARK` (warning for env sets, info for atlases), `TEX_MASK_GLOSSY` info. `MDL_TRIS` warning above the manifest/§8.9 budget, error above 1.25x; `MDL_NO_TRIS` error; `MDL_LOD_GAP`, `MDL_NAMING`, `MDL_UNREADABLE` warnings. `AUD_FOLDER` error (SFX group / VO chain folder the import rules do not know), `AUD_VO_NAME` error, `AUD_TOKENS` error, `AUD_UNREADABLE` error, `AUD_VO_LINE`, `AUD_DURATION`, `AUD_RATE`, `AUD_FORMAT` warnings, `AUD_CHANNELS` info. `BUDGET_LINE` warning when the estimate of a §8.10 line exceeds its budget. `VID_SIZE` error above 14 MB. `SPEC_UNREADABLE`, `SPEC_ROW`, `SPEC_MISSING_TEXTURE` errors for material spec files (`*material*.json` under `RawAssets/<wp>/ready/`, same discovery as MaterialFactory) whose rows reference textures that exist neither in `Assets/` nor in any ready tree. `--selftest` validates synthetic files with known problems.

The texture caps come from a Python mirror of `TextureRules` that reads the same `asset_budgets.json` and manifest budgets; after changing either side run **LongShadow/Import/Export Rule Table** in the editor and `--check-mirror` (must report 0 mismatches). The download estimate is an upper bound (every non-editor-only file counts): textures use the measured Crunch basis of §8.9, audio is AAC 44.1 kHz at the assumed kbps of `asset_budgets.json` (Unity 6000.6 ignores sample-rate overrides on WebGL), meshes a per-triangle figure, clips 0.1 MB each. WP11's build report replaces it with real numbers.
