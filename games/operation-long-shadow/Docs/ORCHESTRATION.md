# Orchestration rules for build agents (read first; overrides other docs where they conflict)

Last updated 2026-10-16 by the orchestrator (main Claude session).

## 1. The user is asleep: run autonomously
- The user said: "don't ask any more questions, just do it". **Never ask the user anything and never stop to wait for approval.** When a doc says "needs the user" or "asked by the orchestrator", decide yourself using the defaults below, log the decision in `Docs/DECISIONS.md`, and keep going.
- Quality bar: the user's top priority is **realism** of visuals and audio. Slowness is fine. Spend effort on fidelity, not shortcuts. Still deliver something that works end to end.

## 2. Open decisions from TECH_PLAN §12.1, now resolved
| # | Resolution |
|---|---|
| O1 | **Approved.** Download OFL/Apache fonts from Google Fonts (fonts.google.com / github.com/google/fonts). |
| O2 | **Approved.** Download Microsoft Rocketbox avatars/animations (MIT) and Unity Standard Assets Characters clips (Unity Companion License) from GitHub (raw.githubusercontent.com / github.com). Credit them. |
| O3 | Delegated to the agent: run the web check for invented words yourself (search each bark word; reject any with a real meaning in a major language) and judge the glyphs against real alphabets. Log the result. |
| O4 | **Waived.** No human listening gate. Use objective checks (loudness, clipping, true peak, onset, silence, spectral sanity, duration) plus the content guardrails (no religious audio, no real-language words in barks, no caricatured accents). Record results in `Docs/AUDIO_REVIEW.md`. Generate everything in the manifest. |
| O5 | Agents **never publish** artifacts. The orchestrator publishes builds. |
| O6 | **Quality over cost (user, 2026-10-16): the $1000 figure may be exceeded.** Caps are now generous (total $3000; per-WP caps in `Tools/fal/caps.json`, enforced by `Tools/fal/falcall.py`). Take more takes, compare several models (e.g. Tripo vs Hunyuan vs Rodin for 3D; several TTS/SFX models), and keep the most realistic result. Don't waste money on pointless repeats, but never ship a weak asset to save a few dollars. |
| O7 | Done: `fun-unity` is a local git repo (no remote). **Agents do not commit, branch, stash, reset or checkout.** The orchestrator commits between phases. Read-only git (`status`, `diff`, `log`) is fine. |
| O8 | Done: **Blender 5.2.2 is installed** at `/Applications/Blender.app/Contents/MacOS/Blender` (run headless: `Blender -b --factory-startup -P script.py -- args`). |

## 3. Changes to TECH_PLAN decisions
- **D2 refined:** with Blender available, static meshes may go through Blender (import GLB/glTF → apply transforms, real-world scale in meters, Unity axis conversion, decimate/clean, optional part separation, LODs) and be exported as **FBX** with external PNG/TGA textures. OBJ via `gltf_to_obj.py` stays acceptable. Characters and animation stay FBX. Log which path each asset class uses.
- Blender may also be used for character work (finger bones, weight fixes, retargeting, merging clips, adding a jaw), first-person arms, mesh LODs and texture baking.

## 4. fal calls
- Every paid fal call **must** go through `Tools/fal/falcall.py` (ledger + caps). Example:
  `/Users/gencsaracaydin/Documents/fun/.venv/bin/python Tools/fal/falcall.py --wp WP03 --id WPN_AK --endpoint tripo3d/p2/image-to-3d --args-json '{...}' --est-usd 1.20 --out RawAssets/wp03/WPN_AK/tripo_take1`
- Pass an honest `--est-usd` from the model's pricing (see `Docs/research/fal_*.md` and the model's `https://fal.ai/models/<id>/llms.txt`). Exit code 2 means you hit your cap: stop generating, finish with what you have, and report it.
- `falcall.py --report` prints spend per WP. FAL_KEY is never printed, logged or written anywhere.

## 5. Unity Editor access
- Only an agent whose prompt says **EDITOR HOLDER** may call `mcp__unity-mcp__*` tools. Everyone else must not call any Unity MCP tool (not even read-only ones) and must not touch `Library/`, `Temp/` or `ProjectSettings/`.
- The workflow script guarantees one editor holder at a time, so the lock file in TECH_PLAN §11.2 is optional. Editor holders still keep the console clean and save scenes/assets before finishing.
- Never call `Unity_AssetGeneration_*` or `Unity_AudioClip_Edit` (no Unity AI credits).

## 6. Where files go
- Ownership per TECH_PLAN §11.1 still applies.
- **Content agents** (asset production, no editor) write raw downloads/generations to `RawAssets/<wp lower>/<asset id>/` and their **import-ready outputs** to `RawAssets/<wp lower>/ready/` mirroring the final Unity path (e.g. `RawAssets/wp03/ready/Assets/_Game/Art/Models/Weapons/SM_WPN_AK_FP.fbx`), plus `RawAssets/<wp lower>/ready/import_index.json` (one row per file: manifest id, target path, type, import notes such as normal-map flag, sRGB, mask packing, scale, avatar type). An editor holder copies them into `Assets/` later.
- **Code agents** write C# under `Staging/<System>/` mirroring `Assets/_Game/Scripts/<System>/` and land through `Tools/build/land.py` after `Tools/build/compile_check.py` passes (both built by WP00-A). Never leave half-written `.cs` files under `Assets/`.
- Tool scripts for your area go in `Tools/<area>/` (audio → `Tools/audio`, meshes → `Tools/mesh`, textures → `Tools/tex`, CC0 → `Tools/cc0`, characters → `Tools/characters`, images → `Tools/images`). Python: create/use `Tools/.venv` with `uv` (`uv venv Tools/.venv && uv pip install --python Tools/.venv ...`); if two agents race on creating it, just retry. fal_client lives in `/Users/gencsaracaydin/Documents/fun/.venv`.
- Append decisions to `Docs/DECISIONS.md` as `- [WPxx] decision — reason`. Cross-package requests go to `Docs/REQUESTS.md`; Core contract changes to `Docs/CORE_CHANGES.md`.

## 7. Shared machine etiquette
- 8 CPUs / 16 GB RAM shared with the Unity Editor and other agents: run at most one Blender process at a time per agent, keep `-t 2` style thread limits where possible, and avoid loading huge meshes all at once.
- Local web servers: use a free port in 8800–8899, never 8765, and stop the server when done. Do not use the Browser pane tab that shows the claude.ai artifact (left for the user).
- Content guardrails (GDD §2.3) apply to every prompt, line, texture and decal: fictional country and factions, no real groups/flags/insignia, no real-world slogans or scripts on signage, no religious audio or imagery (no domes/minarets, no call to prayer, no chanting), no caricatured accents or keffiyeh stereotypes.

## 8. Cinematic and realism direction (added after kickoff, 2026-10-16)
The user asked for **CoD-level realism and real scene cuts, especially at the beginning**. Read `Docs/CINEMATIC_DIRECTION.md` in full if you are WP02, WP03, WP05, WP06, WP07, WP09, WP10_LOOK, WP10_LEVEL, an integration agent or the content audit. It adds the §8.2a INSERTION cold-open cinematic (helicopter insertion, 12 shots, ~55 s) between intro shots 8 and 9, new lines D200–D207 in DIALOGUE.json, new manifest entries (VO_D200–VO_D207, ANIM_INSERTION_SET, VFX_ROTOR_DOWNWASH), a cinematic-only terrain apron SW of the tile in S1_Approach, and a camera/lighting language for every sequence. It overrides GDD §8 where they conflict.

## 9. Resuming after an interruption (2026-10-16)
The first content and code runs were stopped part-way because the machine lost internet. If you find outputs from an earlier attempt in your folders (RawAssets/<wp>/, Staging/<System>/, Assets/_Game/, Tools/<area>/), **inventory them first and reuse or finish them instead of regenerating**. Check `Tools/fal/ledger.jsonl` / `falcall.py --report` for what was already paid for. Ledger rows with status `submitted` but no `done` have a fal `request_id` whose result can still be fetched (`fal_client.result(endpoint, request_id)`).

## 10. Never open modal dialogs from editor code (added 2026-10-16)
A modal dialog freezes the Unity main thread, and with it the MCP bridge, until a human clicks it. The LAND agent hung for 20+ minutes on `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()`. Editor code and RunCommand scripts must **never** call `EditorUtility.DisplayDialog*`, `SaveCurrentModifiedScenesIfUserWantsTo`, `SaveModifiedScenesIfUserWantsTo`, `EditorUtility.SaveFilePanel*`/`OpenFilePanel*`, or anything else that prompts. Save scenes explicitly with `EditorSceneManager.SaveScene(scene)` (or `SaveScene(scene, path)` for new scenes), and log instead of asking.
