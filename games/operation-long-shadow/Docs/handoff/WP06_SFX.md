# WP06_SFX handoff: SFX, ambience, music

Status: **done**. Every manifest entry with category `sfx` or `music` is produced (163/163: all P0, P1 and P2), plus 5 extras. Everything passes the objective QA (`qa_report.py`: 0 failed checks on 542 files) and `validate_assets.py --ready audio` (0 errors; the only warning is the §8.10 audio budget line, see §6).

fal spend `WP06SFX`: **$75.95** (the task cap was $110; `caps.json` now allows $300, which was not needed). The first run spent $68.11. This resumed run (pass 3) spent $7.84 on retakes and machine listening.

The first run had generated and processed everything, but it was interrupted before the decisions, review and handoff were written. Pass 3 reused every earlier take and did the following:

- Measured and reviewed everything (contact sheets, machine listen).
- Fixed what was weak (§5).
- Wrote `Docs/DECISIONS.md` (`[WP06_SFX]` entries), the SFX section of `Docs/AUDIO_REVIEW.md`, and this file.

## 1. Inventory (import-ready, `RawAssets/audio/ready/`)

All audio is 48 kHz 16-bit WAV, true peak ≤ −1 dBTP, with 0 clipped samples:

- SFX are mono.
- Ambience beds and the ElevenLabs (EL) music are stereo.
- The procedural music and Clockmaker material are mono or stereo, as noted in each row.

The index is `import_index_sfx.json`: 543 rows (542 WAVs plus `LOOP_POINTS.json`), each with import notes.

| Folder (`Assets/_Game/Audio/…`) | Files | Content |
|---|---|---|
| `SFX/Weapons` | 124 | Per gun: 4 transients `SFX_WPN_<ID>_SHOT_0n` (COMMANDO, HUSHPUPPY, AK, RPK, M60D, AKMS, SVD, DSHK); mech layers `_MECH`; body layers `SFX_WPN_BODY_RIFLE/HEAVY`; zone tails `SFX_WPN_TAIL_{STREET,SQUARE,INTERIOR,DESERT}` (3 each); reload slices `SFX_WPN_<ID>_RELOAD_{MAGOUT,MAGIN,CHARGE}`; handling `SFX_WPN_HANDLING_{DRYFIRE,RAISE,ADSIN,ADSOUT,SWITCH}`; casings `SFX_WPN_SHELLS_{STONE,SAND}`; grenade pin/bounce/fuse tick; RPG launch and flyby; knife swing and hit |
| `SFX/Weapons/Far` | 16 | Baked distance variants `SFX_WPN_<ID>_SHOT_FAR_0n`: air-absorption low-pass and a rolling outdoor echo (D9: no runtime filters on the Web) |
| `SFX/Whizz` | 10 | 6 procedural supersonic cracks (N-wave physics), 4 whizzes |
| `SFX/Impacts` | 68 | Bullet impacts per SurfaceTag: sand, stone, plaster, wood, metal, fabric, flesh, glass, fruit (5–8 each) |
| `SFX/Footsteps` | 93 | One heel-toe step per clip: sand, gravel, stone, roof, tile, wood, metal stair, salt crust (10–12 each) |
| `SFX/Foley` | 19 | Landing, slide, mantle, ladder rungs, gear-rustle beds, body falls |
| `SFX/Explosions` | 20 | Frag ×4, breach, RPG and fireball ×2 each, each with 2 `_FAR` bakes; debris layer ×2 |
| `SFX/SetPieces` | 111 | Every set-piece one-shot and loop in the manifest: rotor approach, flyby and loop; heli cabin and alarm; trucks; floods and sodium lamps; projector; drill; teletype; CRT; booth foley; G5 civilian sounds; the procedural beds; `AMB_CASSETTE_TUNE` (diegetic, mono 3D); `STG_LAYERS_TAPE_{STOP,REWIND}`; the extras |
| `SFX/Synth` | 18 | Procedural: Greycoat squelch code `SFX_RADIO_CLICKS_{1,2,3}` (1/2/3 PTT clicks + two-tone tail), `SFX_RADIO_KEYUP`/`_RELEASE`, tinnitus, leader beep, CRT whine (loop), stuck clock (loop) |
| `SFX/UI` | 22 | 11 UI cues × 2; the hit marker is procedural |
| `Ambience` | 24 | 12 stereo beds × 2 alternate 20 s loops (15 s for the generator) |
| `Music` | 17 | See below |

The 17 `Music` files:

- `MUS_01_SESSION` (intro + 9-bar loop), `MUS_02_MARKET_LO/HI` (synced layers), `MUS_03_LASTLIGHT` (one-shot), `MUS_04_STATION_LOW/FULL` (synced layers), `MUS_05_EXFIL`.
- `MUS_06_SIXFORTY` (procedural outro), `MOTIF_MUSICBOX`, and `INTERVAL_SIGNAL` (6.0 s) with its `_HF_AM` and `_ROOM_SPEAKER` chain versions.
- `NUM_BASEMENT_LOOP` (from WP06_VO, accepted).
- Stingers: `STG_TITLE`, `STG_GLITCH`, `STG_DEATH` (procedural), `STG_GREYCOAT` (EL).
- `LOOP_POINTS.json`.

The 5 extras are not in the manifest. Their manifest id is `EXTRA_*` and their file names drop the prefix:

- `SFX_REEL_TO_REEL_LOOP` and `SFX_TAPE_TRANSPORT`: the booth reel-to-reel deck.
- `SFX_WPN_M60D_OVERHEAT`: requested by WP03.
- `SFX_DOWNWASH_SAND` and `SFX_CINE_RAMP`: CINEMATIC_DIRECTION §8.2a. I-7 needs "sand hiss on the lens"; I-8 and the speed ramps need a low thump and whoosh.

Offline material that is not imported (`RawAssets/audio/sfx/`, `RawAssets/audio/music/`):

- Raw takes per id and take folder, each with `request.json`, `result.json` and `provenance.json`.
- `_procedural/`
- `_preview/`: the in-game weapon stacks per zone, exactly as WeaponShotPlayer sums them at cue volume 1.0.
- `_qa/`: `scan.json`, the listen cache, `qa_report.json`, `build_report_sfx.json` (selection and layer-balance report) and contact sheets (`sheets/*_pass3_*.png`).
- `music/procedural/`
- `music/selection.json`

## 2. Import instructions (editor holder)

1. Copy the files with `python3 Tools/build/copy_ready.py audio --validate`. This takes `RawAssets/audio/ready/Assets/_Game/Audio/{SFX,Music,Ambience}/**`. WP06_VO's `VO/` and `Envelopes/` come along from the same tree.

2. `AudioImportRules` (TECH_PLAN §3.12) applies by folder. Every row repeats the settings:

   | Folder | Mono | Load type | Preload | Quality | Sample rate |
   |---|---|---|---|---|---|
   | `SFX/{Weapons,Weapons/Far,Impacts,Foley,Footsteps,Explosions,Whizz,Synth}` | Force To Mono | Decompress On Load | on | 70 | – |
   | `SFX/SetPieces` | Force To Mono | Compressed In Memory | on | 60 | – |
   | `SFX/UI` | Force To Mono | Decompress On Load | on | 60 | 22 050 Hz |
   | `Ambience` | stereo | Compressed In Memory | **off**, load in background | 50 | – |
   | `Music` | stereo | Compressed In Memory | off | 60 | – |

3. Loops (ambience beds, set-piece loops, music):
   - Every loop has a **2048-sample lead-in and tail-out**.
   - The loop region is in `Audio/Music/LOOP_POINTS.json`, keyed by Assets path, in samples at 48 kHz and in seconds. Music entries also carry the bar grid. The same region is in each WAV's `smpl` chunk.
   - Play loops with two AudioSources and `PlayScheduled`/`SetScheduledEndTime` over that region. Never use `AudioSource.loop` on the whole clip (§3.12: AAC priming).
   - Ambience and set-piece loops with two variants should alternate every few cycles.

4. Cue ids for `AudioLibraryBuilder` (WP06 code; request filed):
   - A cue id is the file name without the trailing `_nn`.
   - `<CUE>_FAR_nn` files are the far clips of `<CUE>`.
   - Sliced or derived ids: reload `…_RELOAD_{MAGOUT,MAGIN,CHARGE}` and handling `SFX_WPN_HANDLING_*` match `LongShadow.Combat.CombatCues`. Also `SFX_WPN_SHELLS_{STONE,SAND}` (WP03 asked to choose by surface, with a fallback to `SFX_WPN_SHELLS`), `SFX_RADIO_CLICKS_{1,2,3}`, `SFX_RADIO_KEYUP`/`_RELEASE`, `STG_LAYERS_TAPE_{STOP,REWIND}`, and `INTERVAL_SIGNAL_{HF_AM,ROOM_SPEAKER}`.

5. **Weapon stack levels.** Keep every weapon layer's cue volume at 1.0. The layers are balanced against each other and against the headroom exactly as `WeaponShotPlayer` and `WeaponSoundProfile.For()` play them:
   - Body classes: COMMANDO/AK/AKMS → RIFLE; RPK/SVD/M60D/DSHK → HEAVY.
   - Tail gains: Hush Puppy 0.18, DShK 1.1.
   - The worst single shot is ≤ −1.2 dBTP.

   Scale the whole stack with category or mix gains, never one layer.

6. One-shot roles worth knowing:
   - Zone tails are reflections only and start late by design (street ~15 ms, desert ~0.4 s).
   - `SFX_RPG_FLYBY` builds up before its peak.
   - `SFX_EXP_DEBRIS` is the debris falling back ~1.5 s after the blast; play it at the blast instant.
   - `SFX_KETTLE` whistles in its last second.

## 3. Tools (`Tools/audio/sfx/`) and how to rerun them

Run everything with `Tools/.venv/bin/python` (numpy, scipy, soundfile, pyloudnorm, librosa, matplotlib). fal calls go through `Tools/fal/falcall.py --wp WP06SFX` with the fal venv. Every step is cached and idempotent.

```
cd Tools/audio/sfx
../../.venv/bin/python jobs.py --dry-run [--ids A,B] [--priority P0]   # cost of the fal jobs (manifest + EXTRA_JOBS_* retakes)
../../.venv/bin/python jobs.py --run --ids A,B                         # generate (Mirelo SFX 2.0 / EL SFX v2), skips done takes
../../.venv/bin/python listen_batch.py --ids A,B                       # machine listen (match/realism/unwanted, guardrails)
../../.venv/bin/python scan.py --ids A,B                               # measure takes + join listen answers -> _qa/scan.json
../../.venv/bin/python proc_sfx.py                                     # procedural cracks + hit-marker ticks
../../.venv/bin/python motif_synth.py all                              # motif, interval signal, MUS_06, synth SFX, music refs
../../.venv/bin/python music_generate.py --stage 1|2|3                 # EL Music v2.5 cues (composition_plan + audio_reference)
../../.venv/bin/python music_build.py select && ../../.venv/bin/python music_build.py build   # bar-line loops -> ready/Music
../../.venv/bin/python sfx_process.py [--only weapons,setpieces,...] [--preview]               # everything else -> ready/
../../.venv/bin/python qa_report.py && ../../.venv/bin/python sfx_review.py                    # QA + Docs/AUDIO_REVIEW.md SFX section
../../.venv/bin/python qa_sheets.py --prefix SFX_WPN_ --max-dur 1.2 --name weapons            # contact sheets (PNG)
python3 ../../build/validate_assets.py --ready audio                                          # project validator
```

`sfx_process.py` groups: `weapons` (shots, bodies, mechs, tails, layer balance), `weapon_foley`, `whizz`, `impacts`, `explosions`, `footsteps`, `foley`, `setpieces` (includes the compound recipes), `extras`, `ui`, `synth`, `ambience`, `beds`. The index is merged by manifest id. A rebuilt id drops its stale files.

To fix one cue:
1. Add prompts to `jobs.py` (`EXTRA_JOBS_*`).
2. Run `jobs.py --run --ids X`, then `listen_batch.py --ids X`, then `scan.py --ids X`.
3. Run `sfx_process.py --only <group>`, then `qa_report.py` and `sfx_review.py`.

| Script | Role |
|---|---|
| `alib.py` | IO, filters, limiter and soft clip, true peak, LUFS/momentary, onset trim, event slicing, synthetic IRs, loop packaging and seam metrics |
| `gen.py` / `jobs.py` | fal jobs through falcall (ledger + caps) with provenance; prompt refinements and retake batches |
| `listen.py` / `listen_batch.py` / `scan.py` | `fal-ai/audio-understanding` machine listen (cached) and the candidate table |
| `sfx_process.py` | Recipes: Mirelo pre-roll trim; single-event pickers; slicing; per-category loudness (`LEVELS`); `_far` bakes; procedural zone tails (street canyon, room, square, desert IRs); weapon layer balance (`balance_layers`, `BAL`); compound set pieces; loop packaging; procedural beds from our own material; index/report writer |
| `proc_sfx.py` / `motif_synth.py` | Physics-based cracks and ticks; music-box motif D6–F6–C#6, interval signal, outro, synth SFX |
| `music_generate.py` / `music_analyze.py` / `music_loop.py` / `music_build.py` | EL Music v2.5 cues, tempo/grid/motif analysis, bar-line loop cutting, ready music |
| `qa_report.py` / `sfx_review.py` / `qa_sheets.py` / `sheet.py` | QA, the review section, contact sheets |

## 4. Deviations from the manifest and spec (all logged in `Docs/DECISIONS.md`)

**Variant counts.** Where takes were clean, counts are higher than the manifest asks: shots 4 + 2 far, impacts 5–8, footsteps 10–12, reload slices instead of one reload take. Body fall ships 3 variants and sand casings 2: fewer clean singles survived, and a smaller round robin beats padding it with bad takes.

**No whole-take `SFX_WPN_<ID>_RELOAD`.** Only the MAGOUT, MAGIN and CHARGE slices ship (WP03's cue ids).

**Procedural instead of generated.** These are physical models or our own material:
- Bullet cracks and the hit marker.
- All four zone tails. They convolve our dry shot with a synthetic IR, because the generated "tail" takes still contained another gun's full crack.
- The squelch code, key-up, tinnitus, leader beep, CRT whine and stuck clock.

**Compound set pieces.** `SFX_BOLTCUTTER_CHAIN` and `SFX_TUBE_RACK_SHATTER` are layered from separately generated parts, because no single take matched the brief.

**Levels.** Weapon layers are not at their category targets on their own. They are balanced as a stack (§2.5). The Hush Puppy sits ~7 dB under the rifles.

**Extras** were added (§1).

**Budget.** Nothing was dropped for size (§6).

## 5. What pass 3 changed (resume run)

**Weapon stack.** Pass 2 summed to +3…+6.3 dBTP at cue volume 1.0, and the body layer was as loud as the transient. The fix is a linked-gain layer balance (table in AUDIO_REVIEW):
- Worst single shot ≤ −1.2 dBTP.
- Simulated 12-round bursts at the weapon's RPM peak around 0…+1.6 dBTP.
- Stack loudness about −14 LUFS (100 ms max) for the rifles.

**Street and interior tails** were rebuilt from image-source models. The generated takes carried a second gunshot.

**Retakes** with refined prompts, Mirelo + EL. Earlier selections were judged off-brief (MATCH 1–5) and were replaced:

| Cue | Earlier take sounded like |
|---|---|
| Awning tear | a zipper |
| Blast debris | fire crackle |
| Leather creak | a lighter |
| Satchel plant | a wrapper |
| Fruit spill | a wrapper or a wooden floor |

Retakes also replaced flood slam-on, projector start, kettle, grenade bounce, body fall, jump landing, radio static (realism 3, "digital hiss"), the cinema bed's second variant, and three UI blips with hum or clipping.

**Casings.** Every pass-2 stone slice had a constant 11–12 kHz model whine. They were replaced by single-casing takes.

**Trimming.** Inaudible ends were trimmed everywhere, fast one-shots start within 3 ms of their onset, and footstep/impact heads were aligned. Frag/RPG/breach explosion tails no longer end in a −40 dB plateau. The fireball is now 3 s.

**QA fixes.** Seams of bar-line music loops are now judged against bar lines (MUS_01 had been a false failure). The lead check exempts tails and masked mechs by design, and the HF-tone check exempts tonal-by-design cues (casing ring, UI beeps).

**Builds are now deterministic.** Seeds use crc32 instead of `hash()`.

## 6. Known issues and notes

**Budget.** SFX + ambience + music ≈ **19.9 MB** at the validator's AAC assumption (56/64/80 kbps per channel at quality 50/60/70). VO adds 5.4–7.7 MB. The §8.10 audio line is 18 MB, but its own per-class caps sum to ≈ 25 MB. Every per-file cap holds, except the eight explosion `_FAR` bakes (30–33 KB each, 3 s rolling echo).

Drop order if BudgetReport shows no headroom:
1. The `_02` variant of the stationary beds (ROOMTONE_BOOTH/HALL, GENERATOR_HUM, BASEMENT_TUBES, CINEMA_INTERIOR, CRICKETS, CICADAS): ≈ 1.9 MB, practically inaudible.
2. The `_02` variant of AMB_DISTANT_GUNFIRE and AMB_DISTANT_SHOUTING: ≈ 0.56 MB.
3. The P2 cues SFX_TV_MUFFLED, SFX_KETTLE, SFX_PIGEONS: ≈ 0.1 MB.

The real AAC sizes come from the build's BudgetReport. WP11 should measure them.

**Bursts can still peak slightly over 0 dBTP.** At full master and effects volume (both default to 1.0), a long AK or RPK burst with an unlucky draw of variants reaches up to +1.6 dBTP for single samples. Options in code: a −1 to −2 dB effects headroom in `AudioMix`, or the planned VO/combat ducking.

**The machine listen is a proxy.** `fal-ai/audio-understanding` judges match and realism. It is good at catching wrong content ("zipper", "fire crackle") and guardrail violations (none found), but it is not a mixing ear. A human pass on the weapon stacks (`RawAssets/audio/sfx/_preview/*_composite_*.wav`) is still the best next step once someone is awake.

**Some peaky one-shots stay under their 400 ms target.** Their soft-clip allowance is capped (3–4 dB, 8 dB for the flood slam) to keep them clean:
- `SFX_BULLET_WHIZ` reaches ≈ −25 vs −17. The 100 ms reading is higher, and the crack layer carries the near miss.
- `SFX_FLOOD_SLAM_ON` reaches −20…−22 vs −15.
- `SFX_PIGEONS` (P2) reaches −31 vs −19.
- `SFX_KETTLE` reaches −28 vs −22, which is fine because it plays behind a wall.

Short clicks (handling, UI, lighter, toggle) read 6 dB lower in the 400 ms window than in the 100 ms window they are levelled by. That is not a shortfall. AudioService can raise any of these with cue volume.

**Sand casings ship 2 variants.** The surface split needs WP03's surface pick (REQUESTS.md). Until then, a builder that keys by manifest id puts both sets under `SFX_WPN_SHELLS`.

**`SFX_PROJECTOR_START_02` runs 17–24 dB under its lever click after the first 0.6 s** (a quiet motor). It is usable, but `_01` (lever + motor + clatter) is the better take for MID-B.

**The SVD body layer.** The code maps SVD to the HEAVY body, but the SVD transients are designed as heard from ~130 m with their own echo. Beyond 40 m the near layers fade out, so this only matters if the player gets close to the marksman.
