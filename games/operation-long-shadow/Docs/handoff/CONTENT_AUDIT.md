# CONTENT_AUDIT: content phase audit and gap fill

Status: **done.** All 646 entries in ASSET_MANIFEST.json are accounted for, plus 9 extra ids that content agents delivered:

| Priority | Manifest ids | Import-ready | Realised offline | Built in the editor (procedural) | Cut | Missing |
|---|---|---|---|---|---|---|
| P0 | 519 | 488 | 12 | 19 | 0 | **0** |
| P1 | 100 | 99 | 0 | 1 | 0 | **0** |
| P2 | 27 | 26 | 0 | 0 | 1 | 0 |

- **Realised offline:**
  - The 10 `VOICE_*` castings exist through their VO lines.
  - `VO_CLOCKMAKER_TOKENS` stays in `RawAssets/audio/vo/tokens/` by design.
  - `UI_FONT_FALLBACK` is TMP's bundled LiberationSans.
- **Cut:** `CHR_FALLBACK_MESHY`. O2 was approved, so every role uses Rocketbox.
- **Built in the editor:** the kit, buildings, terrain, backdrop, lamps, cables, mast, pitch props, chemlight, fire prop, light shafts, rotor downwash and parachute flare. Builders are staged in `Staging/World/{Level,Look}` and `Staging/AI/Squad`; the per-id table in §10 names each one.

**This run filled 7 ids and made 6 fixes. fal spend: $0.**
- **Filled ids:** 6 P0 and 1 P1, all of them gaps with no producer.
  - `DCL_SARDANI_SIGNS`, `DCL_MARQUEE_TITLES`, `DCL_POSTERS_COMPOSED` (P1), `PROC_CLOCK_TOWER_FACES`, `PROC_FILM_CAN_LABELS`, `PROC_EXIT_SIGN`. These were waiting for SignBaker, which does not exist in the code. WP10_LEVEL leaves its sign boards blank.
  - `VEH_HELI_CABIN`. Only a 5-box greybox existed.
- **Fixes:** a guardrail paint-out, two realism repaints of WP10_FAL textures, one broken index row, WP03's open material request, and a cabin placement bug in WP10_LEVEL's code (filed as a request). See §2, items #8–#13.

Review sheet of everything this run made or changed: `RawAssets/_audit/AUDIT_fills_sheet.png`.

## 1. Inventory of the ready trees

| Package | Index file(s) | Rows | Material specs | Size on disk |
|---|---|---|---|---|
| WP06_VO | `RawAssets/audio/ready/import_index_vo.json` | 470 | none | 323 MB (audio) |
| WP06_SFX | `RawAssets/audio/ready/import_index_sfx.json` | 543 | none | (in audio) |
| WP03_ART | `RawAssets/wp03/ready/import_index.json` | 79 | `materials_wp03.json` (7, **new, this audit**) | 69 MB |
| WP05_ART | `RawAssets/wp05/ready/import_index.json` | 295 | none (RocketboxImporter builds them) | 408 MB |
| WP08 (by WP09_ART) | `RawAssets/wp08/ready/import_index.json` | 49 | none | 2 MB |
| WP09_ART | `RawAssets/wp09/ready/import_index.json` | 107 | `materials.json` (25) | 148 MB |
| WP10_CC0 | `RawAssets/wp10/ready/import_index_cc0.json` | 429 | `materials_cc0.json` (124) | 673 MB (all of wp10) |
| WP10_FAL | `RawAssets/wp10/ready/import_index_fal.json` | 218 | `materials_fal.json` (57) | |
| CONTENT_AUDIT | `RawAssets/wp10/ready/import_index_audit.json` | 36 | `materials_audit.json` (13) | |

Integrity, checked by `Tools/audit/content_audit.py` and `Tools/build/validate_assets.py --ready --pixels`:
- Missing files: 0. Files without an index row: 0.
- `file` differs from `target_path`: 0. Duplicate targets across packages: 0.
- validate_assets: **0 errors**. Its 26 warnings are explained in §3.
- Material spec rows: 251, all resolving to existing textures (SPEC_* checks).

## 2. What this run changed

| # | Item | Problem found | Fix | Files / tool |
|---|---|---|---|---|
| 1 | `DCL_SARDANI_SIGNS` (P0) | Not produced: blocked on WP08's Sardani font. The font is ready now, but no SignBaker exists. | Baked offline into a 1024 decal atlas (BC/N/M + layout json). Contents: 2 hand-painted shop fascias, 2 shop plates, 2 pasted curfew notices ("VESKRI KAEL" + 17:30, stamped HARROU 12.10.84), 4 enamel street plates. Weathering: flaking, sun fade, grime runs, enamel chips with rust halos. | `Decals/sardani_signs/`, `Tools/audit/sign_bake.py` |
| 2 | `DCL_MARQUEE_TITLES` (P0) | Same as #1 | 3 changeable-letter title panels with invented Sardani titles and Latin showtimes (one letter slot empty), a "CINEMA ZENITH" name panel and the "ZENITH" blade face | `Decals/marquee_titles/` |
| 3 | `DCL_POSTERS_COMPOSED` (P1) | Same as #1 | WP10_FAL's 3 poster plates with Sardani titles and Latin dates printed in the blank band: 3 current posters plus 3 sun-faded, torn older bills | `Decals/posters_composed/` |
| 4 | `PROC_CLOCK_TOWER_FACES` (P0) | No texture: the kit uses a flat enamel colour | Tower dial (2.5 m, Roman numerals), world-time wall dial, and 12 city name plates (invented Sardani names, gilt on dark wood). No hands: the kit's iron hands sit at 6:40. | `Textures/Props/clock_faces/` |
| 5 | `PROC_FILM_CAN_LABELS` (P0) | Not produced | 4 typewritten masking-tape labels: "S-3 / SESSION 11 / 03-84" plus sessions 10, 09 and 08 | `Textures/Props/film_labels/` |
| 6 | `PROC_EXIT_SIGN` (P0) | Flat green colour only | EXIT box face (Latin, allowed by the manifest) with a Sardani subline; BC, emissive, N, M; `M_ExitSign` spec | `Textures/Props/exit_sign/` |
| 7 | `VEH_HELI_CABIN` (P0) | Only a greybox (floor box, bench box, one post) in `VehicleBuilder`, placed 2.4 m behind the door | Blender-built cabin kit, 6.4k tris (see §2.1). Also `SM_Helicopter_DoorsOpen.fbx`: WP10_FAL's hull with the baked cargo-door panels removed and normals made consistent. | `Models/Vehicles/heli_cabin/`, `Models/Vehicles/helicopter/SM_Helicopter_DoorsOpen.fbx`, `Textures/Vehicles/heli_cabin/`, `Tools/audit/heli_cabin.py` |
| 8 | `VEH_HELICOPTER` texture (WP10_FAL) | Tripo baked bright "teeth" into the windshield, roof and side-window glass. They are visible head-on in the insertion cinematic. | Every canopy and side-window glass texel repainted as clean dark tinted glass (BC). M is glossy non-metal, N flat. 5 % of the atlas. Original kept. | `Tools/audit/heli_windshield.py` (+ `blender_heli_glass_mask.py`), backup in `RawAssets/wp10/fal/_audit_backup/` |
| 9 | `FAL_DIVE_WATCH` texture (WP05 → WP10 request) | The dial had painted hands at 10:10. WP05 adds procedural hands for the MID-A watch check, so a close-up would show two sets. | Inner dial repainted procedurally: matte black, aged lume dots, batons and 12 o'clock triangle, minute track, no text, no hands. The bezel is untouched. | `Tools/audit/dive_watch_dial.py`, before/after in `RawAssets/_audit/watch/` |
| 10 | `IMG_HARROU_STILL` (WP09), guardrail | 1. A bell hangs in the clock tower's arch, which reads as a church belfry. 2. Six beret cap badges; one is crown-topped and regimental-looking (GDD §2.3: no religious imagery, no real insignia). | Bell and badges painted out, with film grain matched. Original kept as `RawAssets/wp09/IMG_HARROU_STILL/pre_audit_T_HarrouStill_BC.png`. VID_HARROU_8MM is unchanged: at 512x384 neither detail is legible. | `Tools/audit/guardrail_paintout.py` |
| 11 | WP03_ART request: smoke-grenade green variant and detail normal | Requested from the editor holder; not done | Spec file: `M_WPN_SmokeGrenade_Green`, plus the FP gun materials `M_WPN_{Commando,HushPuppy,AK,RPK,M60D,Knife}` with the shared detail normal (scale 0.35, tiling 8). Spec rows win over the auto-built set materials. | `RawAssets/wp03/ready/materials_wp03.json` |
| 12 | `RawAssets/wp09/ready/import_index.json` row for `VID_FilmLeader.mp4` | `type: "texture"` on an mp4 | Changed to `video`. `wp09_docs.py` and `wp09_index.py` are patched so a rebuild keeps it. The index was rebuilt and only that row changed (plus refreshed byte counts). | `Tools/images/wp09_docs.py`, `wp09_index.py` |
| 13 | WP10_LEVEL `VehicleBuilder.Magpie` | The cabin root is at heli-local (0, 1, 0). The hull's cargo doors (measured) are at z 2.00–3.35, so the player eye, gun pivots and squad seats would land 2.4 m aft, inside the hull. | Not mine to edit (code). Filed in REQUESTS.md "CONTENT_AUDIT → WP10_LEVEL Magpie cabin": root at **(0, 1.00, 2.375)**, use SM_HeliCabin + SM_Helicopter_DoorsOpen. | Docs/REQUESTS.md |

### 2.1 VEH_HELI_CABIN in detail

- **Frame:** GDD §8.0. +Z is the nose, +X is right, cabin floor at Y = 0.
  - Door-gun pivots: `Socket_DoorGunPivot_L/R` at (∓1.05, 1.10, 0.30).
  - Marks for the player eye, Hollis, Sparrow, Tinker, the red lamp and both pilot seats.
  - Parent the cabin under the hull at heli-local (0, 1.00, 2.375).
- **Skin:** the cabin skin follows the Tripo hull. Rays are cast from the cabin axis every 0.1 m along the length and every 7.5° around it, then inset 5 cm along the hull normal. The skin is clamped to floor 0, a 1.72 m ceiling and a 1.42 m half width.
  - The cargo-door rectangle stays open on both sides.
  - Dark-painted hull windows become glass, filled out to clean rectangles.
- **Fit-out:** ceiling ribs and stringers, canvas blankets, the transmission well cover, grab rails, and tie-down cups. Also a canvas troop bench on the rear bulkhead, and two gunner stations (door-gun post with pintle sleeve and brace, ammo tray, outboard-facing canvas seat at 0.45 m, which gives the GDD's 1.25 m eye height). The cockpit is laid out from the measured hull:
  - armoured pilot seats with cushions, cyclic, collective and pedals;
  - radio pedestal and instrument panel faces (`T_HeliCabin_Gauges`, Latin numerals only);
  - glare shield and overhead console.
- **Materials:** walls, floor and tubes bind to existing WP10 env materials by slot name (`M_GreenMetalRust`, `M_MetalPlate`, `M_MetalPlates013`; UVs are metric). New specs: `M_HeliCabin_Canvas`, `_Dark`, `_Glass`, `_RedLamp` and `_Gauges` in `materials_audit.json`.
- **Deviations from the GDD:**
  - The red lamp hangs at 1.60 m, not 1.90 m. The hull roof is about 1.75 m above the floor.
  - `Mark_Sparrow` (0.60, 1.00, 0.90) touches the right pilot seat's back plate. Seat her on the floor or move her about 0.3 m aft. Filed in the request.
- **Review renders:** `RawAssets/_audit/heli_cabin/cabin_*.png`: gunner views, across-cabin, rear bench, cockpit, and three exteriors.

## 3. Budget, naming and guardrail findings left as they are (with reasons)

- **MDL_TRIS (16 warnings, all within the validator's 25 % tolerance).**
  - The FP Commando, Hush Puppy, AK and RPK count 20k body + a 0.8–2.5k `Mag` child. The §8.9 row is "12–20 k (+1–3 k magazine)", so they are within budget.
  - Character props are +1–8 % (ballcap 1018/1000, chest rig 3136/3000, helmet+goggles 2690/2500, NVG 2017/2000).
  - Props: rotary phone 2491/2000 (handset split for the D091 lift; a close-up booth hero); scrambler box 3166/3000; cinema seat, kettle, on-air lamp and wall speaker +1–3 %.
  - Vehicles: technical 12359/12000, tarp-off truck 12151/12000.
  - Decimating these would cost visible quality for a few hundred triangles each, so they were not re-exported.
- **MDL_NAMING:** `SK_SAC_DefaultMaleRig.fbx` sits in `Animation/Clips/SAC/` because it is the avatar source for the SAC clips, not a clip.
- **TEX_SET_INCOMPLETE:** `T_WPN_SmokeGrenade_Green_BC` and `T_WallClockGlassCrack_A_BC` both have explicit material spec rows (`materials_wp03.json`, WP09 `materials.json`), so the auto-material fallback the validator warns about is never used.
- **Per-file audio caps (§8.9).**
  - The eight explosion `_FAR` bakes are 30–33 KB against 30 KB because they carry a 3 s echo (WP06_SFX decision).
  - VO averages 25.3 KB per clip over 305 clips (cap: 30 KB average). The long TAPE, HF_AM and PA_HORN lines are 61–79 KB each.
  - Single-take loops keep the manifest id as their file name, without `_nn`; WP06's CueNaming parses both forms.
- **Images:** `T_RosterPage_FS_BC` is about 0.55 MB because it is the full-screen 2048 push-in that §8.9 allows.
- **Guardrail review.** Contact sheets viewed:
  - every WP09 image plate and the Harrou 8 mm frames;
  - every decal atlas (posters, chalk, grime, company lettering);
  - every WP10_FAL props, vehicles and trees sheet, plus vehicle BC atlases;
  - CC0 prop sheets with paint-outs, booth props and textures;
  - all 31 UI sprites (`RawAssets/_audit/ui_sprites_sheet.png`; WP08's own `ui_all.png` only shows the vignette);
  - all character bodies, weapons and skies;
  - audio guardrails in AUDIO_REVIEW.md (O3 word checks, sound-alikes, no chanting, bells or call to prayer).

  The Harrou still was the only finding (fixed, #10). Latin text that the GDD allows remains on a TV back label ("WARNING"), on crate stencils and on the gauges.

## 4. Deviations from the manifest (consolidated)

| Id | Deviation | Reason |
|---|---|---|
| PH_WOODENCHAIR_01 | Delivered as Poly Haven `painted_wooden_chair_02`. WP10_CC0 also swapped PH_DRAWER_CABINET → `vintage_wooden_drawer_01`. | WoodenChair_01 is a Gothic throne with church tracery |
| TEX_FABRIC_TAN_SHIRT | Files named `T_Fabric_TanPoplin_*` | WP05 naming |
| IMG_* (12) | `T_<Name>_BC.png` instead of the manifest's lower-case names; `manifest_target_path` kept in each row | TECH_PLAN §4.2 |
| DCL_POSTER_ART | 1536x768 atlas (manifest: 1024) | three 2:3 posters keep resolution |
| CHP_FLIGHT_HELMET, CHP_SCARF, CHP_SUNGLASSES | Not in the manifest (WP05 extras for Magpie, Legion C and Tinker/Harrou) | Add them to the manifest when it is next edited |
| EXTRA_SFX_* (5), LOOP_POINTS | Not in the manifest (WP06_SFX extras) | insertion cinematic, booth, M60D overheat |
| Bark words | `Ulsk`, `Orvak` and `Ottrek` replace Sauth, Ekt-ha and Ivvo (DIALOGUE.json + GDD §14.4 already updated) | sound-alike check |
| Characters | Tinker uses Male_Adult_08. Sparrow has a hair bun. Magpie wears an SPH-4-style helmet. | WP05_ART |
| VID_HARROU_8MM, VID_GUNCAM | Made offline (fal Kling + Python finishing) instead of the D14 Unity Recorder pass | WP09_ART |
| VEH_HELI_CABIN | Blender FBX instead of an editor KitGenerator piece; lamp at 1.60 m | this audit (§2.1) |
| PROC_CLOCK_TOWER_FACES, PROC_FILM_CAN_LABELS, PROC_EXIT_SIGN, DCL_SARDANI_SIGNS, DCL_MARQUEE_TITLES, DCL_POSTERS_COMPOSED | Baked offline instead of by SignBaker or editor tools; delivered as textures and decal atlases, while the geometry stays kit | this audit |
| CHR_FALLBACK_MESHY | Cut | O2 approved |

## 5. Size estimate against TECH_PLAN §8.10

The estimate is an upper bound from `Tools/audit/content_audit.py`, which uses `validate_assets`'s basis:
- textures from the measured Crunch figures;
- audio as AAC at the assumed kbps;
- meshes per triangle;
- 0.1 MB per clip file.

"Not referenced" covers editor-only sources, re-bake spare plates, licence texts and the Harrou body.

| §8.10 line | P0 | P1 | P2 | Not referenced | Budget | P0 vs budget |
|---|---|---|---|---|---|---|
| Environment textures (+ decals, sky) | 37.6 | 16.4 | 6.6 | 0.1 | 45 | ok (7.4 spare) |
| Props (+ vegetation) | 27.3 | 10.1 | 3.8 | 0 | 28 | ok (0.7 spare) |
| Vehicles | 7.7 | 0 | 0 | 0 | 6 | **+1.7** |
| Weapons | 16.8 | 4.9 | 0 | 0 | 19 | ok (2.2 spare) |
| Characters | 21.5 | 1.7 | 1.0 | 1.8 | 24 | ok (2.5 spare) |
| Animation (clip files) | 6.8 | 5.9 | 0 | 0.1 | 6 | **+0.8** |
| Audio | 24.3 | 3.2 | 0.1 | 0 | 18 | **+6.3** |
| Images and video | 2.5 | 2.7 | 0 | 0.4 | 8 | ok (5.5 spare) |
| Shared shaders + UI + FX textures | 1.9 | 0.1 | 0 | 0 | 6 | ok (content part only) |
| Kit, buildings, terrain, far proxies | built in the editor | | | | 14 | not measurable offline |
| **Content total** | **146.5** | **45.0** | **11.4** | 2.4 | | |

Audio P0 by folder: VO 7.5, music 6.7, SFX 5.8, ambience 4.3 MB.

**Whole-build projection (P0):**

| Part | MB |
|---|---|
| Content | 146.5 |
| Engine | 7 |
| Boot | 6 |
| Shaders/UI remainder | ~4 |
| Kit, buildings, terrain (budget) | 14 |
| Lightmaps (budget) | 33 |
| **Total** | **~211** |

That is within the 220 MB plan.
- **P1 adds about 45 MB** (about 256 MB in total), which is over the 230 MB hard cap. P1 has to be included selectively until BudgetReport reads 220 MB.
- **Vehicles is +1.7 MB over** because of two choices, both with a fix:
  - The two cargo-truck models and the burnt texture sets (realism) cost about 1.2 MB.
  - `SM_Helicopter_DoorsOpen` duplicates the hull mesh. If Magpie uses only the doors-open hull everywhere, the closed `SM_Helicopter` is never referenced and the duplicate (about 0.6 MB) disappears.
- **Audio is +6.3 MB over** even for P0. Rebalance the line against the Images (+5.5) and Shared (+4) slack rather than cut audio. If cuts are needed, WP06_SFX §6 gives a drop order worth about 2.6 MB.

**Suggested P1 order by gameplay value per MB.** M60D and Legion C are P1 in the manifest but Beat 11 depends on them.

| # | P1 item | MB | Why |
|---|---|---|---|
| 1 | WPN_M60D | 3.0 | Beat 11 door gun |
| 2 | CHR_LEGION_C (+ CHP_SCARF) | 0.8 | Beat 11 RPG man and the rooftop targets |
| 3 | VID_GUNCAM + VID_HARROU_8MM | 2.5 | They ship anyway: StreamingAssets ships every file |
| 4 | WPN_SVD and WPN_KNIFE | 1.6 | |
| 5 | P1 ambience beds | 2.2 | |
| 6 | Up to about 6 MB of the 0.68 MB P1 env sets that the section builders actually reference | | |

Leave until last: ANIM_ROCKETBOX_BOOTH, ANIM_SAC_FULL and ANIM_MESHY_SET_C (5.2 MB as clip files; only clips a controller references ship), and the P2 set.

## 6. Import instructions (editor holder)

1. **Copy:** `python3 Tools/build/copy_ready.py --all --validate`. Nothing is in `Assets/` yet, so there are no conflicts. If any WP10_FAL or WP09 file was copied earlier, re-copy these with `--overwrite`, because this audit changed them in place:
   - `Textures/Vehicles/helicopter/T_Helicopter_{BC,M,N}.png`
   - `Textures/Props/dive_watch/T_DiveWatch_{BC,M,N}.png`
   - `Textures/Images/T_HarrouStill_BC.png`
2. **Refresh.** The import rules apply. Then run **LongShadow/Materials/Build From Ready Specs**. It picks up `materials_wp03.json` and `materials_audit.json` with the others.
3. **LongShadow/Materials/Bind All Model Materials.** SM_HeliCabin needs *Material Creation Mode = Import via MaterialDescription* so its slot names survive; see its row.
4. **Audit-filled assets (row notes are authoritative):**
   - Decals: one URP DecalProjector per rect in each `*_layout.json` (uv = x, y, w, h, origin bottom-left; `size_m` = projector size).
   - Clock dials: projectors over the kit discs.
   - `M_ExitSign`: assign to the cinema ExitSign slot.
   - Cabin: parent SM_HeliCabin under SM_Helicopter_DoorsOpen at (0, 1.00, 2.375).
   - These follow the two CONTENT_AUDIT requests in REQUESTS.md.
5. Everything else follows the package handoffs: WP06_VO, WP06_SFX, WP03_ART, WP05_ART, WP09_ART, WP10_CC0, WP10_FAL.

## 7. Tools written by this run (all in `Tools/audit/`, idempotent, no fal)

| Tool | Run | What it does |
|---|---|---|
| `content_audit.py` | `Tools/.venv/bin/python Tools/audit/content_audit.py [--md FILE] [--pixels]` | Manifest × import-index coverage; integrity (missing, unindexed, duplicate targets, row fields, naming); `validate_assets` checks plus a per-file download estimate split by priority and "ships / not referenced"; per-file audio and image caps. Writes `RawAssets/_audit/content_audit.json`; `--md` writes the per-id table in §10. |
| `sign_bake.py` | `Tools/.venv/bin/python -I Tools/audit/sign_bake.py [--only signs,marquee,posters,clocks,filmcans,exit]` | Items #1–6. Writes rows and specs into `RawAssets/wp10/ready/import_index_audit.json` / `materials_audit.json`; review images in `RawAssets/_audit/signs/`. |
| `heli_cabin.py` (+ `blender_heli_cabin.py`) | `Tools/.venv/bin/python -I Tools/audit/heli_cabin.py [--render]` | Item #7: gauges texture, Blender cabin build, doors-open hull, rows and specs, review renders. |
| `heli_windshield.py` (+ `blender_heli_glass_mask.py`) | `Blender -b --factory-startup -P Tools/audit/blender_heli_glass_mask.py -- <SM_Helicopter.fbx> RawAssets/wp10/fal/_audit_backup/T_Helicopter_BC.png RawAssets/_audit/heli_cabin/windshield_polys.json`, then `Tools/.venv/bin/python -I Tools/audit/heli_windshield.py` | Item #8 |
| `dive_watch_dial.py` (+ `blender_uv_probe.py`, `blender_watch_review.py`) | `Blender ... -P Tools/audit/blender_uv_probe.py -- <SM_DiveWatch.fbx> RawAssets/_audit/watch/faces.json`, then `Tools/.venv/bin/python -I Tools/audit/dive_watch_dial.py` | Item #9 |
| `guardrail_paintout.py` | `Tools/.venv/bin/python -I Tools/audit/guardrail_paintout.py` | Item #10 |

**Order after a package rebuild:**
- If WP10_FAL regenerates the helicopter or the watch, rerun `heli_windshield.py` and `dive_watch_dial.py`. Delete the matching backup in `RawAssets/wp10/fal/_audit_backup/` first, so the new maps become the base.
- If WP09 regenerates IMG_HARROU_STILL, delete `RawAssets/wp09/IMG_HARROU_STILL/pre_audit_T_HarrouStill_BC.png` and rerun `guardrail_paintout.py`.
- If WP10_FAL changes the hull, rerun `heli_cabin.py --render`.

## 8. Known issues and follow-ups

**This audit**
- The cabin is plain: one material per surface type, no decals or stencils (on purpose: no text), and flat window glass. Up close in Beat 11 it reads as a believable olive-drab Huey interior, not a hero asset.
- The cockpit was fitted to the Tripo hull. Its windshield sits further aft than on a real UH-1, so the instrument panel is at cabin z 2.22.
- The marquee and street-plate texts are Sardani glyphs at 1024. Close-ups from the marquee walkway resolve about 100 px per glyph.
- WP10_LEVEL must still place these decals and the cabin (two requests). Until then the sign boards stay blank and the cabin stays a greybox in the wrong place.

**Carried from the packages (unchanged)**
- No human listening (O4 waived). Audio QA is objective checks plus the machine listen (WP06).
- A long AK or RPK burst can peak around +1.6 dBTP at master and effects volume 1.0. 1–2 dB of effects headroom is suggested (WP06_SFX).
- RPK FP has tiny back-face slivers. The M60D receiver top keeps a soft texture. DShK and M60D sockets may be 1–2 cm off. The RPG nozzle is shallow. Split parts (RPG warhead, DShK mount) must be added to every LOD (WP03_ART).
- Meshy idles have deep knees. Seat heights differ by clip (0.52–0.65 m pelvis). Booth torso strap fragments are visible in close-up. Electrodes exist both on SK_Calder_Booth_Cuffs and in PROC_BOOTH_SMALLS: use one (WP05_ART).
- PH_ROCK_07/09 ship at real scan size, to be scaled 3–15x. Heavy single-LOD props: iron gate, projector, binoculars. Sky textures have the land removed, so far terrain must come from geometry (WP10_CC0).
- The satchel charge reads as a plain bag. Generated props have fragmented UVs, so a detail normal helps close-ups (WP10_FAL).
- Booth part rotation signs are unverified in Unity. The phone handset split leaves capped holes. The scrambler cord sinks 2.4 cm into the trolley. StreamingAssets/video is 2.7 MB against the 2 MB planned (WP09_ART).
- The Commando's collapsible stock reads closer to a later M4-style stock than the 1970s CAR-15 tube stock. It is mostly out of view in first person. This is a realism note for a future WP03 pass.

## 9. fal spend

$0 this run. Every fill is procedural or reworks existing outputs. Package totals (from their reports):

| Package | Spend |
|---|---|
| WP06VO | $10.77 |
| WP06SFX | $75.95 |
| WP03 | $29.53 |
| WP05 | $22.48 |
| WP10_FAL | $60.75 |
| WP09 | $16.58 |
| WP08 | $0.34 |
| WP10_CC0 | $0 |

## 10. Per-id table (every manifest id + extras)

Status codes:
- **ready:** indexed and on disk.
- **offline:** no Assets/ file by design.
- **editor:** procedural; built by the named staged builder.
- **cut:** not produced, on purpose.

Path column: the ready tree, then the Unity folder(s) the files mirror. "MB (ships)" is the upper-bound download estimate.

| Manifest id | Cat | Pri | Owner | Status | Files | MB (ships) | Import-ready path / note |
|---|---|---|---|---|---|---|---|
| WPN_AK | weapon | P0 | WP03 | ready | 5 | 2.99 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/AK; Assets/_Game/Art/Textures/Weapons/AK |
| WPN_AKMS | weapon | P0 | WP03 | ready | 4 | 0.96 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/AKMS; Assets/_Game/Art/Textures/Weapons/AKMS |
| WPN_AK_MAG | weapon | P0 | WP03 | ready | 4 | 0.23 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/AK; Assets/_Game/Art/Textures/Weapons/AK |
| WPN_COMMANDO | weapon | P0 | WP03 | ready | 6 | 3.07 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/Commando; Assets/_Game/Art/Textures/Weapons/Commando; Assets/_Game/Art/Textures/Weapons/_Shared |
| WPN_COMMANDO_MAG | weapon | P0 | WP03 | ready | 4 | 0.23 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/Commando; Assets/_Game/Art/Textures/Weapons/Commando |
| WPN_DSHK | weapon | P0 | WP03 | ready | 4 | 1.03 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/DShK; Assets/_Game/Art/Textures/Weapons/DShK |
| WPN_HUSHPUPPY | weapon | P0 | WP03 | ready | 5 | 2.87 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/HushPuppy; Assets/_Game/Art/Textures/Weapons/HushPuppy |
| WPN_HUSHPUPPY_MAG | weapon | P0 | WP03 | ready | 4 | 0.21 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/HushPuppy; Assets/_Game/Art/Textures/Weapons/HushPuppy |
| WPN_M67 | weapon | P0 | WP03 | ready | 5 | 0.66 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/M67; Assets/_Game/Art/Textures/Weapons/M67 |
| WPN_RPG7 | weapon | P0 | WP03 | ready | 4 | 0.94 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/RPG7; Assets/_Game/Art/Textures/Weapons/RPG7 |
| WPN_RPG7_ROCKET | weapon | P0 | WP03 | ready | 4 | 0.23 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/RPG7; Assets/_Game/Art/Textures/Weapons/RPG7 |
| WPN_RPK | weapon | P0 | WP03 | ready | 5 | 3.06 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/RPK; Assets/_Game/Art/Textures/Weapons/RPK |
| WPN_RPK_MAG | weapon | P0 | WP03 | ready | 4 | 0.27 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/RPK; Assets/_Game/Art/Textures/Weapons/RPK |
| ANIM_FINGER_POSES | character | P0 | WP05 | ready | 5 | 0.50 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Poses |
| ANIM_INSERTION_SET | character | P0 | WP05 | ready | 10 | 1.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Meshy |
| ANIM_MESHY_OWNED | character | P0 | WP05 | ready | 5 | 0.50 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Meshy |
| ANIM_MESHY_SET_A | character | P0 | WP05 | ready | 10 | 1.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Meshy |
| ANIM_MESHY_SET_B | character | P0 | WP05 | ready | 20 | 2.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Meshy |
| ANIM_ROCKETBOX_LOCAL | character | P0 | WP05 | ready | 6 | 0.60 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Rocketbox |
| ANIM_SAC_SUBSET | character | P0 | WP05 | ready | 9 | 0.80 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/SAC |
| ANIM_UAL1 | character | P0 | WP05 | ready | 2 | 0.20 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/UAL1 |
| ANIM_UAL2 | character | P0 | WP05 | ready | 2 | 0.20 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/UAL2 |
| CHP_BALLCAP | character | P0 | WP05 | ready | 4 | 0.21 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/ballcap; Assets/_Game/Art/Textures/Characters/Props/CHP_Ballcap |
| CHP_BERET | character | P0 | WP05 | ready | 4 | 0.20 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/beret; Assets/_Game/Art/Textures/Characters/Props/CHP_Beret |
| CHP_BOONIE | character | P0 | WP05 | ready | 4 | 0.21 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/boonie; Assets/_Game/Art/Textures/Characters/Props/CHP_Boonie |
| CHP_CHEST_RIG | character | P0 | WP05 | ready | 4 | 0.28 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/chest_rig; Assets/_Game/Art/Textures/Characters/Props/CHP_ChestRig |
| CHP_HELMET_GOGGLES | character | P0 | WP05 | ready | 4 | 0.27 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/helmet_goggles; Assets/_Game/Art/Textures/Characters/Props/CHP_HelmetGoggles |
| CHR_CALDER | character | P0 | WP05 | ready | 18 | 3.69 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Calder; Assets/_Game/Art/Textures/Characters/Calder |
| CHR_GREYCOAT_A | character | P0 | WP05 | ready | 7 | 0.74 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/GreycoatA; Assets/_Game/Art/Textures/Characters/GreycoatA |
| CHR_GREYCOAT_B | character | P0 | WP05 | ready | 8 | 1.03 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/GreycoatB; Assets/_Game/Art/Textures/Characters/GreycoatB |
| CHR_HOLLIS | character | P0 | WP05 | ready | 10 | 4.30 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Hollis; Assets/_Game/Art/Textures/Characters/Hollis |
| CHR_INTERROGATOR | character | P0 | WP05 | ready | 10 | 0.82 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Interrogator; Assets/_Game/Art/Textures/Characters/Interrogator |
| CHR_LEGION_A | character | P0 | WP05 | ready | 7 | 0.74 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/LegionA; Assets/_Game/Art/Textures/Characters/LegionA |
| CHR_LEGION_B | character | P0 | WP05 | ready | 7 | 0.76 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/LegionB; Assets/_Game/Art/Textures/Characters/LegionB |
| CHR_SPARROW | character | P0 | WP05 | ready | 7 | 1.76 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Sparrow; Assets/_Game/Art/Textures/Characters/Sparrow |
| CHR_TINKER | character | P0 | WP05 | ready | 10 | 2.12 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Tinker; Assets/_Game/Art/Textures/Characters/Tinker |
| CHP_SUNGLASSES | extra | P0 | WP05 | ready | 4 | 0.07 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/sunglasses; Assets/_Game/Art/Textures/Characters/Props/CHP_Sunglasses |
| TEX_FABRIC_GREYGREEN | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| TEX_FABRIC_KHAKI_DRILL | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| TEX_FABRIC_OLIVE_SATEEN | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| TEX_FABRIC_ORANGE_ARMBAND | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| TEX_FABRIC_TAN_SHIRT | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| TEX_FABRIC_TIGERSTRIPE | texture | P0 | WP05 | ready | 3 | 0.72 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Textures/Characters/Fabrics |
| EXTRA_SFX_CINE_RAMP | extra | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| EXTRA_SFX_DOWNWASH_SAND | extra | P0 | WP06 | ready | 2 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| EXTRA_SFX_REEL_TO_REEL_LOOP | extra | P0 | WP06 | ready | 1 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| EXTRA_SFX_TAPE_TRANSPORT | extra | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| EXTRA_SFX_WPN_M60D_OVERHEAT | extra | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| LOOP_POINTS | extra | P0 | WP06 | ready | 1 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| INTERVAL_SIGNAL | music | P0 | WP06 | ready | 3 | 0.14 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MOTIF_MUSICBOX | music | P0 | WP06 | ready | 1 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_01_SESSION | music | P0 | WP06 | ready | 1 | 1.23 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_02_MARKET_HI | music | P0 | WP06 | ready | 1 | 0.54 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_02_MARKET_LO | music | P0 | WP06 | ready | 1 | 0.54 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_03_LASTLIGHT | music | P0 | WP06 | ready | 1 | 1.20 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_04_STATION_FULL | music | P0 | WP06 | ready | 1 | 0.64 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_04_STATION_LOW | music | P0 | WP06 | ready | 1 | 0.64 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_05_EXFIL | music | P0 | WP06 | ready | 1 | 0.54 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| MUS_06_SIXFORTY | music | P0 | WP06 | ready | 1 | 0.37 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| NUM_BASEMENT_LOOP | music | P0 | WP06 | ready | 1 | 0.53 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| STG_DEATH | music | P0 | WP06 | ready | 1 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| STG_GLITCH | music | P0 | WP06 | ready | 1 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| STG_GREYCOAT | music | P0 | WP06 | ready | 1 | 0.13 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| STG_TITLE | music | P0 | WP06 | ready | 1 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Music |
| AMB_BASEMENT_TUBES | sfx | P0 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_CICADAS | sfx | P0 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_CRICKETS | sfx | P0 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_DESERT_WIND | sfx | P0 | WP06 | ready | 2 | 0.55 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_GENERATOR_HUM | sfx | P0 | WP06 | ready | 2 | 0.42 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_ROOMTONE_BOOTH | sfx | P0 | WP06 | ready | 2 | 0.55 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_SALTPAN_WIND | sfx | P0 | WP06 | ready | 2 | 0.55 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_TOWN_CURFEW | sfx | P0 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| SFX_AWNING_TEAR | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_BODYFALL | sfx | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_BOLTCUTTER_CHAIN | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_BULLET_CRACK | sfx | P0 | WP06 | ready | 6 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Whizz |
| SFX_BULLET_WHIZ | sfx | P0 | WP06 | ready | 4 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Whizz |
| SFX_CAMERA_SHUTTER | sfx | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_CHAIR_CREAK | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_CHEMLIGHT_CRACK | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_CLOCK_TICK_STUCK | sfx | P0 | WP06 | ready | 1 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_CRT_DEGAUSS | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_CRT_STATIC_LOOP | sfx | P0 | WP06 | ready | 1 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_CRT_WHINE | sfx | P0 | WP06 | ready | 1 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_CURFEW_SIREN | sfx | P0 | WP06 | ready | 1 | 0.14 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_DOOR_CREAK | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_DOOR_KICK | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_DRILL_LOOP | sfx | P0 | WP06 | ready | 1 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_DRILL_START_STOP | sfx | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_EXP_BREACH | sfx | P0 | WP06 | ready | 4 | 0.11 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Explosions |
| SFX_EXP_DEBRIS | sfx | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Explosions |
| SFX_EXP_FIREBALL | sfx | P0 | WP06 | ready | 4 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Explosions |
| SFX_EXP_FRAG | sfx | P0 | WP06 | ready | 6 | 0.13 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Explosions |
| SFX_EXP_RPG | sfx | P0 | WP06 | ready | 4 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Explosions |
| SFX_FILM_CAN | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FILM_FLAP | sfx | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FIRE_CRACKLE_LOOP | sfx | P0 | WP06 | ready | 1 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FLOOD_DIEOFF | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FLOOD_SLAM_ON | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FLUORESCENT_BUZZ | sfx | P0 | WP06 | ready | 1 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FLUORESCENT_FLICKER | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FOLEY_GEAR | sfx | P0 | WP06 | ready | 2 | 0.20 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_FRUIT_SPILL | sfx | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FS_GRAVEL | sfx | P0 | WP06 | ready | 12 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_METALSTAIR | sfx | P0 | WP06 | ready | 12 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_ROOF | sfx | P0 | WP06 | ready | 12 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_SAND | sfx | P0 | WP06 | ready | 12 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_STONE | sfx | P0 | WP06 | ready | 10 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_TILE | sfx | P0 | WP06 | ready | 12 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_FS_WOOD | sfx | P0 | WP06 | ready | 12 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_GATE_CREAK | sfx | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_GREN_BOUNCE | sfx | P0 | WP06 | ready | 4 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_GREN_FUSE_TICK | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_GREN_PIN_SPOON | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_HEARTBEAT | sfx | P0 | WP06 | ready | 1 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_HELI_ALARM | sfx | P0 | WP06 | ready | 1 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_HELI_CABIN_LOOP | sfx | P0 | WP06 | ready | 1 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_IMPACT_FABRIC | sfx | P0 | WP06 | ready | 8 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_FLESH | sfx | P0 | WP06 | ready | 8 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_FRUIT | sfx | P0 | WP06 | ready | 8 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_METAL | sfx | P0 | WP06 | ready | 5 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_PLASTER | sfx | P0 | WP06 | ready | 7 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_SAND | sfx | P0 | WP06 | ready | 8 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_STONE | sfx | P0 | WP06 | ready | 8 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_IMPACT_WOOD | sfx | P0 | WP06 | ready | 8 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_LEADER_BEEP | sfx | P0 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_LEATHER_CREAK | sfx | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_LIGHTER | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_MOVE_LADDER | sfx | P0 | WP06 | ready | 6 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_MOVE_LAND | sfx | P0 | WP06 | ready | 4 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_MOVE_MANTLE | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_MOVE_SLIDE | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Foley |
| SFX_PEN_TAP | sfx | P0 | WP06 | ready | 4 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_PHONE | sfx | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_PROJECTOR_LOOP | sfx | P0 | WP06 | ready | 1 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_PROJECTOR_START | sfx | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_RADIO_CLICKS | sfx | P0 | WP06 | ready | 6 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_RADIO_KEYUP | sfx | P0 | WP06 | ready | 6 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_RADIO_STATIC_LOOP | sfx | P0 | WP06 | ready | 1 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_ROTOR_APPROACH | sfx | P0 | WP06 | ready | 2 | 0.15 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_ROTOR_LOOP | sfx | P0 | WP06 | ready | 2 | 0.24 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_RPG_FLYBY | sfx | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_RPG_LAUNCH | sfx | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_SATCHEL_PLANT | sfx | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_SMOKE_GRENADE | sfx | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_SODIUM_BALLAST | sfx | P0 | WP06 | ready | 2 | 0.16 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_SODIUM_STRIKE | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_SPARKS | sfx | P0 | WP06 | ready | 4 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TELETYPE | sfx | P0 | WP06 | ready | 2 | 0.13 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TINNITUS | sfx | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Synth |
| SFX_TOGGLE_CLICK | sfx | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TRANSMITTER_HUM | sfx | P0 | WP06 | ready | 1 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TRUCK_ARRIVE | sfx | P0 | WP06 | ready | 2 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TRUCK_ENGINE_LOOP | sfx | P0 | WP06 | ready | 1 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TUBE_RACK_SHATTER | sfx | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_UI_HEADSHOT | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_HITMARKER | sfx | P0 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_INTEL_CARD | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_KILL | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_MENU_MOVE | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_MENU_SELECT | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_OBJECTIVE | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_PICKUP | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_TELETYPE_LINE | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_UI_TYPE_KEY | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_WPN_AKMS_SHOT | sfx | P0 | WP06 | ready | 6 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_AK_MECH | sfx | P0 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_AK_RELOAD | sfx | P0 | WP06 | ready | 6 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_AK_SHOT | sfx | P0 | WP06 | ready | 6 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_BODY_HEAVY | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_BODY_RIFLE | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_COMMANDO_MECH | sfx | P0 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_COMMANDO_RELOAD | sfx | P0 | WP06 | ready | 6 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_COMMANDO_SHOT | sfx | P0 | WP06 | ready | 6 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_DSHK_SHOT | sfx | P0 | WP06 | ready | 6 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_HANDLING | sfx | P0 | WP06 | ready | 10 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_HUSHPUPPY_MECH | sfx | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_HUSHPUPPY_RELOAD | sfx | P0 | WP06 | ready | 6 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_HUSHPUPPY_SHOT | sfx | P0 | WP06 | ready | 6 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_RPK_MECH | sfx | P0 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_RPK_RELOAD | sfx | P0 | WP06 | ready | 6 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_RPK_SHOT | sfx | P0 | WP06 | ready | 6 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_SVD_SHOT | sfx | P0 | WP06 | ready | 6 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_TAIL_DESERT | sfx | P0 | WP06 | ready | 3 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_TAIL_INTERIOR | sfx | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_TAIL_SQUARE | sfx | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_TAIL_STREET | sfx | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| STG_LAYERS_TAPE | sfx | P0 | WP06 | ready | 4 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| VOICE_vo_calder | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_clockmaker | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_greycoat | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_harrou | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_hollis | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_legion | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_magpie | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_sparrow | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_tallow | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VOICE_vo_tinker | vo | P0 | WP06 | offline | 0 | 0.00 | realised by the VO_* lines of this voice (casting entry; no file of its own) |
| VO_BK_GRY_01 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_02 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_03 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_04 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_05 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_06 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_07 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_08 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_GRY_09 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/GREYCOAT_SQUELCH |
| VO_BK_LEG_01 | vo | P0 | WP06 | ready | 5 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_02 | vo | P0 | WP06 | ready | 5 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_03 | vo | P0 | WP06 | ready | 5 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_04 | vo | P0 | WP06 | ready | 5 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_05 | vo | P0 | WP06 | ready | 5 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_06 | vo | P0 | WP06 | ready | 5 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_07 | vo | P0 | WP06 | ready | 5 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_08 | vo | P0 | WP06 | ready | 5 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_09 | vo | P0 | WP06 | ready | 5 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_10 | vo | P0 | WP06 | ready | 5 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_11 | vo | P0 | WP06 | ready | 5 | 0.11 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_12 | vo | P0 | WP06 | ready | 5 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_13 | vo | P0 | WP06 | ready | 5 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_14 | vo | P0 | WP06 | ready | 5 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_BK_LEG_15 | vo | P0 | WP06 | ready | 5 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/FAR |
| VO_CLOCKMAKER_TOKENS | vo | P0 | WP06 | offline | 0 | 0.00 | RawAssets/audio/vo/tokens/ (offline splice material, never imported; TECH_PLAN §3.12) |
| VO_CO_HOL_01 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_02 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_03 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_04 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_05 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_06 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_07 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_08 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_09 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_10 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_11 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_HOL_12 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_01 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_02 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_03 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_04 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_05 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_06 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_07 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_08 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_09 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_10 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_SPA_11 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_01 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_02 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_03 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_04 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_05 | vo | P0 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_06 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_07 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_08 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_09 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_10 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_CO_TIN_11 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D001 | vo | P0 | WP06 | ready | 2 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D002 | vo | P0 | WP06 | ready | 2 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/TAPE |
| VO_D003 | vo | P0 | WP06 | ready | 2 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/TAPE |
| VO_D004 | vo | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/BOOTH |
| VO_D005 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D006 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D007 | vo | P0 | WP06 | ready | 3 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D008 | vo | P0 | WP06 | ready | 3 | 0.17 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D009 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D010 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D011 | vo | P0 | WP06 | ready | 3 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D012 | vo | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/PA_HORN |
| VO_D013 | vo | P0 | WP06 | ready | 3 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D014 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D015 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D016 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D017 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D018 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D019 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D020 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D021 | vo | P0 | WP06 | ready | 3 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D022 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D023 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D024 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D025 | vo | P0 | WP06 | ready | 2 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/PA_HORN |
| VO_D026 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D027 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D028 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D029 | vo | P0 | WP06 | ready | 3 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D030 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D031 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D032 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D033 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D034 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D035 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D036 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D037 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D038 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D039 | vo | P0 | WP06 | ready | 2 | 0.11 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/HF_AM |
| VO_D040 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D041 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D042 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D043 | vo | P0 | WP06 | ready | 3 | 0.08 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D044 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D045 | vo | P0 | WP06 | ready | 2 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/PA_HORN |
| VO_D046 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D047 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D048 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D049 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D050 | vo | P0 | WP06 | ready | 3 | 0.15 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D051 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D053 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D054 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D055 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D056 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D057 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D059 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D060 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D061 | vo | P0 | WP06 | ready | 3 | 0.09 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D062 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D063 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D064 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D065 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D066 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/ROOM_SPEAKER |
| VO_D067 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D068 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D069 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D070 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D071 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D072 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D073 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D074 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D075 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D076 | vo | P0 | WP06 | ready | 3 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D077 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/INTERCOM |
| VO_D078 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/INTERCOM |
| VO_D079 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/INTERCOM |
| VO_D080 | vo | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/CLEAN |
| VO_D081 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/INTERCOM |
| VO_D082 | vo | P0 | WP06 | ready | 3 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D083 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D084 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/BOOTH |
| VO_D085 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D086 | vo | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D087 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D088 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_D089 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/BOOTH |
| VO_D090 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/CLEAN |
| VO_D091 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/CLEAN |
| VO_D092 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/HF_AM |
| VO_D093 | vo | P0 | WP06 | ready | 3 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D200 | vo | P0 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/INTERCOM; Assets/_Game/Audio/VO/RADIO |
| VO_D201 | vo | P0 | WP06 | ready | 3 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D202 | vo | P0 | WP06 | ready | 3 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D203 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D204 | vo | P0 | WP06 | ready | 3 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D205 | vo | P0 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/RADIO |
| VO_D206 | vo | P0 | WP06 | ready | 2 | 0.06 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/BOOTH |
| VO_D207 | vo | P0 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_01 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_02 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_03 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_04 | vo | P0 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_05 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| VO_DI_06 | vo | P0 | WP06 | ready | 2 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/SCRAMBLER |
| UI_BINOCULAR_RETICLE | ui | P0 | WP08 | ready | 2 | 0.08 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/binocular_reticle |
| UI_CLICK_GATE | ui | P0 | WP08 | ready | 2 | 0.04 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/click_gate |
| UI_DAMAGE_VIGNETTE | ui | P0 | WP08 | ready | 1 | 0.11 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/vignette |
| UI_FONT_CONDENSED | ui | P0 | WP08 | ready | 6 | 0.00 | `RawAssets/wp08/ready/` → Assets/_Game/Art/Fonts |
| UI_FONT_FALLBACK | ui | P0 | WP08 | offline | 0 | 0.00 | TMP Essential Resources LiberationSans SDF (com.unity.ugui), nothing to deliver |
| UI_FONT_SARDANI | ui | P0 | WP08 | ready | 2 | 0.00 | `RawAssets/wp08/ready/` → Assets/_Game/Art/Fonts |
| UI_FONT_TYPEWRITER | ui | P0 | WP08 | ready | 6 | 0.00 | `RawAssets/wp08/ready/` → Assets/_Game/Art/Fonts |
| UI_HUD_ICONS | ui | P0 | WP08 | ready | 8 | 0.52 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/icons |
| UI_LETTERBOX | ui | P0 | WP08 | ready | 1 | 0.00 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/letterbox |
| UI_PAUSE_PANEL | ui | P0 | WP08 | ready | 2 | 0.01 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/pause_panel |
| UI_POLAROID_FRAME | ui | P0 | WP08 | ready | 1 | 0.04 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/polaroid_frame |
| UI_SHAPES | ui | P0 | WP08 | ready | 12 | 0.23 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/shapes |
| IMG_AIRPORT_PLATE | image | P0 | WP09 | ready | 1 | 0.08 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_CODEBOOK_PAGE | image | P0 | WP09 | ready | 1 | 0.14 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_DEBRIEF_DOSSIER | image | P0 | WP09 | ready | 6 | 0.42 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images/debrief |
| IMG_FILM_LEADER | image | P0 | WP09 | ready | 4 | 0.33 | `RawAssets/wp09/ready/` → Assets/StreamingAssets/video; Assets/_Game/Art/Textures/Images/leader |
| IMG_HARROU_STILL | image | P0 | WP09 | ready | 1 | 0.08 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_INTEL_CODEBOOK | image | P0 | WP09 | ready | 1 | 0.10 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_INTEL_LEDGERS | image | P0 | WP09 | ready | 1 | 0.10 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_INTEL_REELS | image | P0 | WP09 | ready | 1 | 0.10 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_LEDGER_PAGE | image | P0 | WP09 | ready | 1 | 0.14 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_MAP_SARDAHN | image | P0 | WP09 | ready | 1 | 0.08 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_RECON_STATION | image | P0 | WP09 | ready | 1 | 0.08 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_ROSTER_PAGE | image | P0 | WP09 | ready | 2 | 0.55 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images/roster |
| IMG_SURVEIL_SPARROW | image | P0 | WP09 | ready | 1 | 0.08 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_TEAM_ID_PHOTOS | image | P0 | WP09 | ready | 7 | 0.23 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images/team_id |
| IMG_TELETYPE_FEED | image | P0 | WP09 | ready | 2 | 0.05 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images/teletype_feed |
| FAL_AV_CART | prop | P0 | WP09 | ready | 5 | 0.25 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/av_cart; Assets/_Game/Art/Textures/Props/av_cart |
| FAL_INTERROGATION_CHAIR | prop | P0 | WP09 | ready | 4 | 0.78 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/interrogation_chair; Assets/_Game/Art/Textures/Props/interrogation_chair |
| FAL_REEL_TO_REEL | prop | P0 | WP09 | ready | 4 | 0.76 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/reel_to_reel; Assets/_Game/Art/Textures/Props/reel_to_reel |
| FAL_ROTARY_PHONE | prop | P0 | WP09 | ready | 4 | 0.22 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/rotary_phone; Assets/_Game/Art/Textures/Props/rotary_phone |
| FAL_SCRAMBLER_BOX | prop | P0 | WP09 | ready | 6 | 0.85 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/scrambler_box; Assets/_Game/Art/Textures/Props/booth_parts; Assets/_Game/Art/Textures/Props/scrambler_box |
| PH_MOUNTED_FLUORESCENT_LIGHTS | prop | P0 | WP09 | ready | 8 | 0.55 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/mounted_fluorescent_lights; Assets/_Game/Art/Textures/Props/mounted_fluorescent_lights |
| PH_TELEVISION_01 | prop | P0 | WP09 | ready | 4 | 0.75 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/Television_01; Assets/_Game/Art/Textures/Props/Television_01 |
| PH_TELEVISION_02 | prop | P0 | WP09 | ready | 4 | 0.76 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/television_02; Assets/_Game/Art/Textures/Props/television_02 |
| PH_VINTAGE_LIGHTER | prop | P0 | WP09 | ready | 4 | 0.24 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/vintage_lighter; Assets/_Game/Art/Textures/Props/vintage_lighter |
| PH_WALL_CLOCK | prop | P0 | WP09 | ready | 5 | 0.31 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/wall_clock; Assets/_Game/Art/Textures/Props/wall_clock |
| PROC_BOOTH_SHELL | prop | P0 | WP09 | ready | 5 | 0.23 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Env/Booth |
| PROC_BOOTH_SMALLS | prop | P0 | WP09 | ready | 4 | 0.02 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/booth_smalls |
| TEX_ACG_PAINTEDPLASTER012 | texture | P0 | WP09 | ready | 3 | 2.73 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Env/PaintedPlaster012 |
| TEX_ACOUSTIC_TILE | texture | P0 | WP09 | ready | 4 | 0.68 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Env/acoustic_tile |
| DCL_BULLET_HOLES | decal | P0 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/bullet_holes |
| DCL_COMPANY_LETTERING | decal | P0 | WP10 | ready | 3 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/company_lettering |
| DCL_MARQUEE_TITLES | decal | P0 | WP10 | ready | 4 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/marquee_titles |
| DCL_SARDANI_SIGNS | decal | P0 | WP10 | ready | 4 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/sardani_signs |
| DCL_SCORCH | decal | P0 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/scorch |
| DCL_STAND_CLEAR_RING | decal | P0 | WP10 | ready | 1 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/stand_clear_ring |
| HDRI_KIARA_8_SUNSET | hdri | P0 | WP10 | ready | 3 | 0.29 | `RawAssets/wp10/ready/` → Assets/_Game/Art/HDRI/EditorOnly; Assets/_Game/Art/HDRI/Runtime |
| HDRI_KIARA_9_DUSK | hdri | P0 | WP10 | ready | 3 | 0.29 | `RawAssets/wp10/ready/` → Assets/_Game/Art/HDRI/EditorOnly; Assets/_Game/Art/HDRI/Runtime |
| FAL_BOLT_CUTTER | prop | P0 | WP10 | ready | 4 | 0.21 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/bolt_cutter; Assets/_Game/Art/Textures/Props/bolt_cutter |
| FAL_CABLE_DRUM | prop | P0 | WP10 | ready | 4 | 0.25 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/cable_drum; Assets/_Game/Art/Textures/Props/cable_drum |
| FAL_CAMERA_35MM | prop | P0 | WP10 | ready | 4 | 0.23 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/camera_35mm; Assets/_Game/Art/Textures/Props/camera_35mm |
| FAL_CINEMA_SEAT | prop | P0 | WP10 | ready | 4 | 0.22 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/cinema_seat; Assets/_Game/Art/Textures/Props/cinema_seat |
| FAL_CODEBOOK | prop | P0 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/codebook; Assets/_Game/Art/Textures/Props/codebook |
| FAL_DIESEL_PUMP | prop | P0 | WP10 | ready | 4 | 0.34 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/diesel_pump; Assets/_Game/Art/Textures/Props/diesel_pump |
| FAL_DIVE_WATCH | prop | P0 | WP10 | ready | 4 | 0.22 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/dive_watch; Assets/_Game/Art/Textures/Props/dive_watch |
| FAL_DRILL_RIG | prop | P0 | WP10 | ready | 4 | 0.28 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/drill_rig; Assets/_Game/Art/Textures/Props/drill_rig |
| FAL_FILM_CAN | prop | P0 | WP10 | ready | 8 | 0.44 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/film_can; Assets/_Game/Art/Textures/Props/film_can |
| FAL_FRUIT_CRATE_ORANGE | prop | P0 | WP10 | ready | 4 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/fruit_crate_orange; Assets/_Game/Art/Textures/Props/fruit_crate_orange |
| FAL_FUEL_BOWSER | prop | P0 | WP10 | ready | 4 | 0.34 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/fuel_bowser; Assets/_Game/Art/Textures/Props/fuel_bowser |
| FAL_LEDGER_STACK | prop | P0 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/ledger_stack; Assets/_Game/Art/Textures/Props/ledger_stack |
| FAL_MARKET_STALL | prop | P0 | WP10 | ready | 4 | 0.26 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/market_stall; Assets/_Game/Art/Textures/Props/market_stall |
| FAL_ORANGE | prop | P0 | WP10 | ready | 4 | 0.05 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/orange; Assets/_Game/Art/Textures/Props/orange |
| FAL_PA_HORN | prop | P0 | WP10 | ready | 4 | 0.24 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/pa_horn; Assets/_Game/Art/Textures/Props/pa_horn |
| FAL_PRC77 | prop | P0 | WP10 | ready | 4 | 0.78 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/prc77; Assets/_Game/Art/Textures/Props/prc77 |
| FAL_SANDBAG | prop | P0 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/sandbag; Assets/_Game/Art/Textures/Props/sandbag |
| FAL_SATCHEL_CHARGE | prop | P0 | WP10 | ready | 4 | 0.21 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/satchel_charge; Assets/_Game/Art/Textures/Props/satchel_charge |
| FAL_TELETYPE | prop | P0 | WP10 | ready | 4 | 0.78 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/teletype; Assets/_Game/Art/Textures/Props/teletype |
| FAL_TUBE_RACK | prop | P0 | WP10 | ready | 5 | 0.88 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/tube_rack; Assets/_Game/Art/Textures/Props/tube_rack |
| FAL_WALL_SAFE | prop | P0 | WP10 | ready | 4 | 0.78 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wall_safe; Assets/_Game/Art/Textures/Props/wall_safe |
| PH_AMMO_BOX | prop | P0 | WP10 | ready | 4 | 0.25 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/ammo_box; Assets/_Game/Art/Textures/Props/ammo_box |
| PH_BARREL_01 | prop | P0 | WP10 | ready | 7 | 0.43 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/Barrel_01; Assets/_Game/Art/Textures/Props/Barrel_01 |
| PH_BINOCULARS | prop | P0 | WP10 | ready | 4 | 0.87 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/binoculars; Assets/_Game/Art/Textures/Props/binoculars |
| PH_CASSETTE_PLAYER | prop | P0 | WP10 | ready | 7 | 0.42 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/cassette_player; Assets/_Game/Art/Textures/Props/cassette_player |
| PH_FILMSTRIP_PROJECTOR_8MM | prop | P0 | WP10 | ready | 4 | 1.01 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/filmstrip_projector_8mm; Assets/_Game/Art/Textures/Props/filmstrip_projector_8mm |
| PH_HANGING_INDUSTRIAL_LAMP | prop | P0 | WP10 | ready | 5 | 0.48 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/hanging_industrial_lamp; Assets/_Game/Art/Textures/Props/hanging_industrial_lamp |
| PH_LARGE_IRON_GATE | prop | P0 | WP10 | ready | 4 | 1.06 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/large_iron_gate; Assets/_Game/Art/Textures/Props/large_iron_gate |
| PH_METAL_OFFICE_DESK | prop | P0 | WP10 | ready | 4 | 0.41 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/metal_office_desk; Assets/_Game/Art/Textures/Props/metal_office_desk |
| PH_OLD_MILITARY_CRATE | prop | P0 | WP10 | ready | 5 | 0.54 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/old_military_crate; Assets/_Game/Art/Textures/Props/old_military_crate |
| PH_PAINTED_WOODEN_TABLE | prop | P0 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/painted_wooden_table; Assets/_Game/Art/Textures/Props/painted_wooden_table |
| PH_PORTABLE_GENERATOR | prop | P0 | WP10 | ready | 4 | 0.63 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/portable_generator; Assets/_Game/Art/Textures/Props/portable_generator |
| PH_PROJECTOR_SCREEN | prop | P0 | WP10 | ready | 7 | 0.65 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/projector_screen; Assets/_Game/Art/Textures/Props/projector_screen |
| PH_ROCK_07 | prop | P0 | WP10 | ready | 4 | 0.66 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/rock_07; Assets/_Game/Art/Textures/Props/rock_07 |
| PH_ROCK_09 | prop | P0 | WP10 | ready | 4 | 0.58 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/rock_09; Assets/_Game/Art/Textures/Props/rock_09 |
| PH_ROCK_FACE_01 | prop | P0 | WP10 | ready | 4 | 1.03 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/rock_face_01; Assets/_Game/Art/Textures/Props/rock_face_01 |
| PH_ROLLERSHUTTER_DOOR | prop | P0 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/rollershutter_door; Assets/_Game/Art/Textures/Props/rollershutter_door |
| PH_ROLLERSHUTTER_WINDOW_01 | prop | P0 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/rollershutter_window_01; Assets/_Game/Art/Textures/Props/rollershutter_window_01 |
| PH_SECURITY_LIGHT | prop | P0 | WP10 | ready | 4 | 0.26 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/security_light; Assets/_Game/Art/Textures/Props/security_light |
| PH_WOODENCHAIR_01 | prop | P0 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/painted_wooden_chair_02; Assets/_Game/Art/Textures/Props/painted_wooden_chair_02 |
| PH_WOODEN_CRATE_01 | prop | P0 | WP10 | ready | 4 | 0.40 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wooden_crate_01; Assets/_Game/Art/Textures/Props/wooden_crate_01 |
| PH_WOODEN_LADDER | prop | P0 | WP10 | ready | 5 | 0.47 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wooden_ladder; Assets/_Game/Art/Textures/Props/wooden_ladder |
| PH_WOODEN_TABLE_02 | prop | P0 | WP10 | ready | 4 | 0.17 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wooden_table_02; Assets/_Game/Art/Textures/Props/wooden_table_02 |
| PROC_AWNINGS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Geometry/KitStructures.cs + AwningPanel.cs (tear) -> Assets/_Game/Art/Models/Kit/Awnings/ |
| PROC_BUILDINGS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Buildings/BuildingGenerator.cs, Geometry/BuildingGeometry.cs -> Assets/_Game/Art/Models/Kit/Buildings/ |
| PROC_CABLES_LAUNDRY | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Geometry/KitRoofProps.cs, Editor/Builders/TownKit.cs -> Assets/_Game/Art/Models/Props/lines/ |
| PROC_CHEMLIGHT | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/AI/Squad/Chemlight.cs (runtime emissive stick, WP04) -> Assets/_Game/Art/Models/Props/chemlight/ |
| PROC_CLOCK_TOWER_FACES | prop | P0 | WP10 | ready | 10 | 0.51 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Props/clock_faces |
| PROC_EXIT_SIGN | prop | P0 | WP10 | ready | 4 | 0.10 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Props/exit_sign |
| PROC_FAR_PROXY | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Proxy/FarProxyBuilder.cs -> Assets/_Game/Art/Models/Backdrop/ |
| PROC_FILM_CAN_LABELS | prop | P0 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Props/film_labels |
| PROC_FIRE_PROP | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Look/Practicals/FireProp.cs + Editor/FXPrefabBuilder.cs (T_VFX_Fire flipbook from WP10_CC0) -> Assets/_Game/Prefabs/FX/ |
| PROC_KIT_INTERIORS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Geometry/Interiors.cs, CinemaGeometry.cs, RelayHallGeometry.cs -> Assets/_Game/Art/Models/Kit/Interiors/ |
| PROC_KIT_ROOFS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Kit/KitGenerator.cs, Geometry/KitRoofProps.cs, Landmarks.cs -> Assets/_Game/Art/Models/Kit/ |
| PROC_KIT_WALLS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Kit/KitGenerator.cs, Geometry/Wall*.cs, Kit*.cs (section builders merge in place) -> Assets/_Game/Art/Models/Kit/ |
| PROC_LAMP_POSTS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Data/LampLayout.cs, Editor/Builders/TownKit.cs -> Assets/_Game/Art/Models/Props/lamps/ |
| PROC_LIGHT_SHAFTS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Look/Atmosphere/LightShaft.cs, DustMotes.cs + Editor/FXPrefabBuilder.cs -> Assets/_Game/Prefabs/FX/ |
| PROC_MAST | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Geometry/KitIndustrial.cs (+ Staging/World/Look/Practicals/AviationLights.cs) -> Assets/_Game/Art/Models/Props/mast/ |
| PROC_PITCH_PROPS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Builders/S7PitchBuilder.cs, Geometry/KitIndustrial.cs -> Assets/_Game/Art/Models/Props/pitch/ |
| PROC_RAZOR_WIRE | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Geometry/KitIndustrial.cs -> Assets/_Game/Art/Models/Props/razor_wire/ |
| PROC_RIDGE_SILHOUETTE | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Proxy/BackdropBuilder.cs -> Assets/_Game/Art/Models/Backdrop/ |
| PROC_SALT_PAN | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Proxy/BackdropBuilder.cs -> Assets/_Game/Art/Models/Backdrop/ |
| PROC_SQUARE_PROPS | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Builders/S3SquareBuilder.cs, Geometry/KitStreet.cs -> Assets/_Game/Art/Models/Props/square/ |
| PROC_TERRAIN_S1 | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Level/Editor/Terrain/TerrainBuilder.cs, Editor/Builders/S1ApproachBuilder.cs -> Assets/_Game/Art/Models/Terrain/ |
| VEG_OLIVE_TREE | prop | P0 | WP10 | ready | 12 | 1.37 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vegetation/olive_tree; Assets/_Game/Art/Textures/Vegetation/olive_tree |
| VEG_POMEGRANATE_TREE | prop | P0 | WP10 | ready | 12 | 1.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vegetation/pomegranate_tree; Assets/_Game/Art/Textures/Vegetation/pomegranate_tree |
| VEH_CARGO_TRUCK | prop | P0 | WP10 | ready | 10 | 2.66 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vehicles/cargo_truck; Assets/_Game/Art/Textures/Vehicles/cargo_truck |
| VEH_HELICOPTER | prop | P0 | WP10 | ready | 9 | 1.72 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vehicles/helicopter; Assets/_Game/Art/Textures/Vehicles/helicopter |
| VEH_HELI_CABIN | prop | P0 | WP10 | ready | 6 | 1.37 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vehicles/heli_cabin; Assets/_Game/Art/Models/Vehicles/helicopter; Assets/_Game/Art/Textures/Vehicles/heli_cabin |
| VEH_TECHNICAL | prop | P0 | WP10 | ready | 6 | 1.95 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vehicles/technical; Assets/_Game/Art/Textures/Vehicles/technical |
| VFX_ROTOR_DOWNWASH | prop | P0 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Look/FX/RotorDownwash.cs + Editor/FXPrefabBuilder.cs (PF_FX_RotorDownwash) -> Assets/_Game/Art/VFX/ |
| TEX_ACG_FABRIC066 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Fabric066 |
| TEX_ACG_METALPLATES013 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/MetalPlates013 |
| TEX_ACG_PAINTEDMETAL006 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/PaintedMetal006 |
| TEX_ACG_PLASTER001 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Plaster001 |
| TEX_ACG_SHEETMETAL001 | texture | P0 | WP10 | ready | 3 | 0.82 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/SheetMetal001 |
| TEX_ACG_TERRAIN003 | texture | P0 | WP10 | ready | 3 | 0.00 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Terrain003 |
| TEX_ACG_TERRAZZO003 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Terrazzo003 |
| TEX_AERIAL_SAND | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/aerial_sand |
| TEX_AWNING_STRIPES | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/awning_stripes |
| TEX_BEIGE_WALL_001 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/beige_wall_001 |
| TEX_CLAY_PLASTER | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/clay_plaster |
| TEX_CONCRETE_DEBRIS | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/concrete_debris |
| TEX_CONCRETE_FLOOR_WORN_001 | texture | P0 | WP10 | ready | 3 | 2.73 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/concrete_floor_worn_001 |
| TEX_CONCRETE_WALL_007 | texture | P0 | WP10 | ready | 4 | 0.75 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/_Shared; Assets/_Game/Art/Textures/Env/concrete_wall_007 |
| TEX_CORRUGATED_IRON | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/corrugated_iron |
| TEX_CRACKED_CONCRETE_WALL | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/cracked_concrete_wall |
| TEX_DRY_GROUND_ROCKS | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/dry_ground_rocks |
| TEX_GRAVELLY_SAND | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/gravelly_sand |
| TEX_GREEN_METAL_RUST | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/green_metal_rust |
| TEX_HESSIAN_230 | texture | P0 | WP10 | ready | 4 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/hessian_230 |
| TEX_LENS_DIRT | texture | P0 | WP10 | ready | 1 | 0.10 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_METAL_PLATE | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/metal_plate |
| TEX_OLD_LINOLEUM_FLOORING_01 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/old_linoleum_flooring_01 |
| TEX_PAINTED_CONCRETE | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/painted_concrete |
| TEX_PAINTED_PLASTER_WALL | texture | P0 | WP10 | ready | 3 | 2.73 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/painted_plaster_wall |
| TEX_ROCKY_TRAIL_02 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/rocky_trail_02 |
| TEX_ROCK_FACE_03 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/rock_face_03 |
| TEX_ROOF_09 | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/roof_09 |
| TEX_RUSTY_CORRUGATED_IRON | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/rusty_corrugated_iron |
| TEX_SALTCRUST | texture | P0 | WP10 | ready | 4 | 0.81 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/saltcrust |
| TEX_SANDSTONE_BLOCKS_05 | texture | P0 | WP10 | ready | 4 | 0.75 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/_Shared; Assets/_Game/Art/Textures/Env/sandstone_blocks_05 |
| TEX_SAND_01 | texture | P0 | WP10 | ready | 5 | 2.89 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/_Shared; Assets/_Game/Art/Textures/Env/sand_01 |
| TEX_STONE_PAVING | texture | P0 | WP10 | ready | 4 | 0.81 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/stone_paving |
| TEX_TERRAZZO_TILES | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/terrazzo_tiles |
| TEX_VFX_DUST_IMPACT | texture | P0 | WP10 | ready | 1 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_FIRE_FLIPBOOK | texture | P0 | WP10 | ready | 1 | 0.14 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_GLINT | texture | P0 | WP10 | ready | 1 | 0.01 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_MUZZLE | texture | P0 | WP10 | ready | 1 | 0.07 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_SMOKE_FLIPBOOK | texture | P0 | WP10 | ready | 1 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_SPARKS | texture | P0 | WP10 | ready | 1 | 0.04 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_VFX_TRACER | texture | P0 | WP10 | ready | 1 | 0.00 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/FX |
| TEX_WEATHERED_PLANKS | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/weathered_planks |
| TEX_WHITE_ROUGH_PLASTER | texture | P0 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/white_rough_plaster |
| TEX_WOOD_PEELING_PAINT_WEATHERED | texture | P0 | WP10 | ready | 4 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/wood_peeling_paint_weathered |
| PROC_SHELL_CASINGS | prop | P1 | WP03 | ready | 3 | 0.03 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/Casings; Assets/_Game/Art/Textures/Weapons/Casings |
| WPN_KNIFE | weapon | P1 | WP03 | ready | 4 | 0.62 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/Knife; Assets/_Game/Art/Textures/Weapons/Knife |
| WPN_M60D | weapon | P1 | WP03 | ready | 5 | 3.01 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/M60D; Assets/_Game/Art/Textures/Weapons/M60D |
| WPN_SMOKE_GRENADE | weapon | P1 | WP03 | ready | 5 | 0.26 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/Smoke; Assets/_Game/Art/Textures/Weapons/Smoke |
| WPN_SVD | weapon | P1 | WP03 | ready | 4 | 0.99 | `RawAssets/wp03/ready/` → Assets/_Game/Art/Models/Weapons/SVD; Assets/_Game/Art/Textures/Weapons/SVD |
| ANIM_HYMOTION_SIGNALS | character | P1 | WP05 | ready | 7 | 0.70 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/HYMotion |
| ANIM_MESHY_SET_C | character | P1 | WP05 | ready | 10 | 1.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Meshy |
| ANIM_ROCKETBOX_BOOTH | character | P1 | WP05 | ready | 22 | 2.20 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/Rocketbox |
| ANIM_SAC_FULL | character | P1 | WP05 | ready | 20 | 2.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Animation/Clips/SAC |
| CHP_ARMOR_VEST | character | P1 | WP05 | ready | 4 | 0.27 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/armor_vest; Assets/_Game/Art/Textures/Characters/Props/CHP_ArmorVest |
| CHP_AVIATOR_GOGGLES | character | P1 | WP05 | ready | 4 | 0.21 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/aviator_goggles; Assets/_Game/Art/Textures/Characters/Props/CHP_AviatorGoggles |
| CHP_NVG | character | P1 | WP05 | ready | 4 | 0.24 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/nvg; Assets/_Game/Art/Textures/Characters/Props/CHP_NVG |
| CHR_HARROU | character | P1 | WP05 | ready | 7 | 0.00 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Harrou; Assets/_Game/Art/Textures/Characters/Harrou |
| CHR_LEGION_C | character | P1 | WP05 | ready | 7 | 0.75 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/LegionC; Assets/_Game/Art/Textures/Characters/LegionC |
| CHP_SCARF | extra | P1 | WP05 | ready | 4 | 0.23 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/scarf; Assets/_Game/Art/Textures/Characters/Props/CHP_Scarf |
| AMB_CASSETTE_TUNE | music | P1 | WP06 | ready | 1 | 0.12 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| AMB_CINEMA_INTERIOR | sfx | P1 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_DISTANT_GUNFIRE | sfx | P1 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_DISTANT_SHOUTING | sfx | P1 | WP06 | ready | 2 | 0.56 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| AMB_ROOMTONE_HALL | sfx | P1 | WP06 | ready | 2 | 0.54 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Ambience |
| SFX_BREATH_BOOTH | sfx | P1 | WP06 | ready | 2 | 0.13 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FLARE | sfx | P1 | WP06 | ready | 2 | 0.10 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_FS_SALTCRUST | sfx | P1 | WP06 | ready | 11 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Footsteps |
| SFX_GLOVE_STUBBLE | sfx | P1 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_HAND_SLAP | sfx | P1 | WP06 | ready | 2 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_IMPACT_GLASS | sfx | P1 | WP06 | ready | 8 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Impacts |
| SFX_KNIFE_HIT | sfx | P1 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_KNIFE_SWING | sfx | P1 | WP06 | ready | 3 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_RADIO_TURNED_DOWN | sfx | P1 | WP06 | ready | 1 | 0.04 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_ROTOR_FLYBY | sfx | P1 | WP06 | ready | 2 | 0.13 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_SHUTTER_LATCH | sfx | P1 | WP06 | ready | 2 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_UI_LOW_AMMO | sfx | P1 | WP06 | ready | 2 | 0.01 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/UI |
| SFX_WPN_M60D_MECH | sfx | P1 | WP06 | ready | 2 | 0.00 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_M60D_RELOAD | sfx | P1 | WP06 | ready | 6 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| SFX_WPN_M60D_SHOT | sfx | P1 | WP06 | ready | 6 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons; Assets/_Game/Audio/SFX/Weapons/Far |
| SFX_WPN_SHELLS | sfx | P1 | WP06 | ready | 8 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/Weapons |
| VO_D052 | vo | P1 | WP06 | ready | 3 | 0.14 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| VO_D058 | vo | P1 | WP06 | ready | 3 | 0.07 | `RawAssets/audio/ready/` → Assets/_Game/Audio/Envelopes; Assets/_Game/Audio/VO/DRY; Assets/_Game/Audio/VO/RADIO |
| UI_DOORGUN_RING | ui | P1 | WP08 | ready | 2 | 0.06 | `RawAssets/wp08/ready/` → Assets/_Game/UI/Sprites/doorgun_ring |
| UI_FONT_MONO | ui | P1 | WP08 | ready | 4 | 0.00 | `RawAssets/wp08/ready/` → Assets/_Game/Art/Fonts |
| IMG_INTEL_FILMLABEL | image | P1 | WP09 | ready | 1 | 0.10 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| IMG_INTEL_SCHEDULE | image | P1 | WP09 | ready | 1 | 0.10 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Textures/Images |
| VID_GUNCAM | image | P1 | WP09 | ready | 1 | 1.43 | `RawAssets/wp09/ready/` → Assets/StreamingAssets/video |
| VID_HARROU_8MM | image | P1 | WP09 | ready | 1 | 1.09 | `RawAssets/wp09/ready/` → Assets/StreamingAssets/video |
| FAL_ASHTRAY | prop | P1 | WP09 | ready | 4 | 0.19 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/ashtray; Assets/_Game/Art/Textures/Props/ashtray |
| FAL_FILM_CAMERA | prop | P1 | WP09 | ready | 4 | 0.23 | `RawAssets/wp09/ready/` → Assets/_Game/Art/Models/Props/film_camera; Assets/_Game/Art/Textures/Props/film_camera |
| DCL_BLOOD_SMALL | decal | P1 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/blood_small |
| DCL_CHALK_DRAWINGS | decal | P1 | WP10 | ready | 2 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/chalk_drawings |
| DCL_GRIME | decal | P1 | WP10 | ready | 2 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/grime |
| DCL_LEAKS | decal | P1 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/leaks |
| DCL_POSTERS_COMPOSED | decal | P1 | WP10 | ready | 4 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/posters_composed |
| DCL_POSTER_ART | decal | P1 | WP10 | ready | 2 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/poster_art |
| DCL_SCRATCHES | decal | P1 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/scratches |
| DCL_TIRE_TRACKS | decal | P1 | WP10 | ready | 3 | 0.14 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/tire_tracks |
| IMG_CONCEPT_KEYFRAMES | image | P1 | WP10 | ready | 1 | 0.00 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Images |
| FAL_FRUIT_CRATE_POMEGRANATE | prop | P1 | WP10 | ready | 4 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/fruit_crate_pomegranate; Assets/_Game/Art/Textures/Props/fruit_crate_pomegranate |
| FAL_MEAL_PLATE | prop | P1 | WP10 | ready | 4 | 0.22 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/meal_plate; Assets/_Game/Art/Textures/Props/meal_plate |
| FAL_MG_TRIPOD | prop | P1 | WP10 | ready | 4 | 0.22 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/mg_tripod; Assets/_Game/Art/Textures/Props/mg_tripod |
| FAL_ON_AIR_LAMP | prop | P1 | WP10 | ready | 5 | 0.23 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/on_air_lamp; Assets/_Game/Art/Textures/Props/on_air_lamp |
| FAL_POMEGRANATE | prop | P1 | WP10 | ready | 4 | 0.04 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/pomegranate; Assets/_Game/Art/Textures/Props/pomegranate |
| FAL_TYPEWRITER | prop | P1 | WP10 | ready | 4 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/typewriter; Assets/_Game/Art/Textures/Props/typewriter |
| FAL_WALL_SPEAKER | prop | P1 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wall_speaker; Assets/_Game/Art/Textures/Props/wall_speaker |
| FAL_WOODEN_BENCH | prop | P1 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wooden_bench; Assets/_Game/Art/Textures/Props/wooden_bench |
| PH_BARREL_03 | prop | P1 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/barrel_03; Assets/_Game/Art/Textures/Props/barrel_03 |
| PH_CAGED_HANGING_LIGHT | prop | P1 | WP10 | ready | 5 | 0.60 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/caged_hanging_light; Assets/_Game/Art/Textures/Props/caged_hanging_light |
| PH_CEILING_FAN | prop | P1 | WP10 | ready | 5 | 0.37 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/ceiling_fan; Assets/_Game/Art/Textures/Props/ceiling_fan |
| PH_CONCRETE_ROAD_BARRIER_02 | prop | P1 | WP10 | ready | 4 | 0.59 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/concrete_road_barrier_02; Assets/_Game/Art/Textures/Props/concrete_road_barrier_02 |
| PH_COVERED_CAR | prop | P1 | WP10 | ready | 4 | 0.45 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/covered_car; Assets/_Game/Art/Textures/Props/covered_car |
| PH_DESK_LAMP_ARM_01 | prop | P1 | WP10 | ready | 4 | 0.59 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/desk_lamp_arm_01; Assets/_Game/Art/Textures/Props/desk_lamp_arm_01 |
| PH_DRAWER_CABINET | prop | P1 | WP10 | ready | 4 | 0.35 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/vintage_wooden_drawer_01; Assets/_Game/Art/Textures/Props/vintage_wooden_drawer_01 |
| PH_DRY_BRANCHES_MEDIUM_01 | prop | P1 | WP10 | ready | 6 | 0.46 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/dry_branches_medium_01; Assets/_Game/Art/Textures/Props/dry_branches_medium_01 |
| PH_METAL_JERRYCAN_GREEN | prop | P1 | WP10 | ready | 4 | 0.34 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/metal_jerrycan_green; Assets/_Game/Art/Textures/Props/metal_jerrycan_green |
| PH_METAL_TRASH_CAN | prop | P1 | WP10 | ready | 8 | 0.81 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/metal_trash_can; Assets/_Game/Art/Textures/Props/metal_trash_can |
| PH_NAMAQUALAND_BOULDER_04 | prop | P1 | WP10 | ready | 4 | 0.45 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/namaqualand_boulder_04; Assets/_Game/Art/Textures/Props/namaqualand_boulder_04 |
| PH_OFFICE_NOTEPADS | prop | P1 | WP10 | ready | 4 | 0.18 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/office_notepads; Assets/_Game/Art/Textures/Props/office_notepads |
| PH_OLD_TYRE | prop | P1 | WP10 | ready | 4 | 0.24 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/old_tyre; Assets/_Game/Art/Textures/Props/old_tyre |
| PH_STEEL_FRAME_SHELVES_01 | prop | P1 | WP10 | ready | 4 | 0.32 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/steel_frame_shelves_01; Assets/_Game/Art/Textures/Props/steel_frame_shelves_01 |
| PH_UTILITY_BOX_01 | prop | P1 | WP10 | ready | 4 | 0.33 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/utility_box_01; Assets/_Game/Art/Textures/Props/utility_box_01 |
| PH_VINTAGE_RADIO_TRANSCEIVER | prop | P1 | WP10 | ready | 7 | 1.08 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/vintage_radio_transceiver; Assets/_Game/Art/Textures/Props/vintage_radio_transceiver |
| PH_WOODEN_CRATE_02 | prop | P1 | WP10 | ready | 4 | 0.35 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wooden_crate_02; Assets/_Game/Art/Textures/Props/wooden_crate_02 |
| PROC_PARACHUTE_FLARE | prop | P1 | WP10 | editor | 0 | 0.00 | built in the editor: Staging/World/Look/Editor/FXPrefabBuilder.cs (flare FX), Practicals/FlickerNoise.cs -> Assets/_Game/Art/Models/Props/flare/ |
| TEX_ACG_BRICKS043 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Bricks043 |
| TEX_ACG_CONCRETE042B | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Concrete042B |
| TEX_ACG_CORRUGATEDSTEEL005 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/CorrugatedSteel005 |
| TEX_ACG_CORRUGATEDSTEEL006A | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/CorrugatedSteel006A |
| TEX_ACG_GROUND097 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Ground097 |
| TEX_ACG_PAINTEDPLASTER006 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/PaintedPlaster006 |
| TEX_ACG_ROCKS017 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Rocks017 |
| TEX_ACG_ROPE001 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Rope001 |
| TEX_BROWN_PLANKS_03 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/brown_planks_03 |
| TEX_DAMAGED_PLASTER | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/damaged_plaster |
| TEX_FLOOR_TILES_08 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/floor_tiles_08 |
| TEX_MUDBRICK | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/mudbrick |
| TEX_MUD_CRACKED_DRY_03 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/mud_cracked_dry_03 |
| TEX_PEELING_PAINTED_WALL | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/peeling_painted_wall |
| TEX_REBAR_REINFORCED_CONCRETE | texture | P1 | WP10 | ready | 4 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/rebar_reinforced_concrete |
| TEX_ROAD_DAMAGED | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/road_damaged |
| TEX_RUSTY_METAL_02 | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/rusty_metal_02 |
| TEX_VELVET | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/velvet |
| TEX_YELLOW_PLASTER | texture | P1 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/yellow_plaster |
| VEG_DRY_GRASS_CARDS | texture | P1 | WP10 | ready | 3 | 0.27 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Vegetation/dry_grass; Assets/_Game/Art/Textures/Props/dry_grass |
| CHR_FALLBACK_MESHY | character | P2 | WP05 | cut | 0 | 0.00 | P2 fallback only needed if O2 (Rocketbox download) was refused; O2 was approved |
| CHR_MAGPIE | character | P2 | WP05 | ready | 7 | 0.71 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Magpie; Assets/_Game/Art/Textures/Characters/Magpie |
| CHP_FLIGHT_HELMET | extra | P2 | WP05 | ready | 4 | 0.26 | `RawAssets/wp05/ready/` → Assets/_Game/Art/Models/Characters/Props/flight_helmet; Assets/_Game/Art/Textures/Characters/Props/CHP_FlightHelmet |
| SFX_KETTLE | sfx | P2 | WP06 | ready | 1 | 0.03 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_PIGEONS | sfx | P2 | WP06 | ready | 1 | 0.02 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| SFX_TV_MUFFLED | sfx | P2 | WP06 | ready | 1 | 0.05 | `RawAssets/audio/ready/` → Assets/_Game/Audio/SFX/SetPieces |
| DCL_ASPHALT_DAMAGE | decal | P2 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/asphalt_damage |
| DCL_FINGERPRINTS | decal | P2 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/fingerprints |
| DCL_TAPE | decal | P2 | WP10 | ready | 3 | 0.55 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Decals/tape |
| HDRI_KIARA_1_DAWN | hdri | P2 | WP10 | ready | 3 | 0.29 | `RawAssets/wp10/ready/` → Assets/_Game/Art/HDRI/EditorOnly; Assets/_Game/Art/HDRI/Runtime |
| HDRI_ROGLAND_SUNSET | hdri | P2 | WP10 | ready | 3 | 0.29 | `RawAssets/wp10/ready/` → Assets/_Game/Art/HDRI/EditorOnly; Assets/_Game/Art/HDRI/Runtime |
| FAL_DRILL_BAG | prop | P2 | WP10 | ready | 4 | 0.21 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/drill_bag; Assets/_Game/Art/Textures/Props/drill_bag |
| FAL_KETTLE | prop | P2 | WP10 | ready | 4 | 0.20 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/kettle; Assets/_Game/Art/Textures/Props/kettle |
| PH_CLIPBOARD | prop | P2 | WP10 | ready | 4 | 0.28 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/clipboard; Assets/_Game/Art/Textures/Props/clipboard |
| PH_DEAD_TREE_TRUNK | prop | P2 | WP10 | ready | 4 | 0.45 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/dead_tree_trunk; Assets/_Game/Art/Textures/Props/dead_tree_trunk |
| PH_METAL_JUG | prop | P2 | WP10 | ready | 4 | 0.28 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/metal_jug; Assets/_Game/Art/Textures/Props/metal_jug |
| PH_METAL_STOOL_01 | prop | P2 | WP10 | ready | 4 | 0.32 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/metal_stool_01; Assets/_Game/Art/Textures/Props/metal_stool_01 |
| PH_OLD_GAS_MASK | prop | P2 | WP10 | ready | 4 | 0.50 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/old_gas_mask; Assets/_Game/Art/Textures/Props/old_gas_mask |
| PH_PORTABLE_SEARCHLIGHT | prop | P2 | WP10 | ready | 4 | 0.50 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/portable_searchlight; Assets/_Game/Art/Textures/Props/portable_searchlight |
| PH_PROPANE_TANK | prop | P2 | WP10 | ready | 4 | 0.26 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/propane_tank; Assets/_Game/Art/Textures/Props/propane_tank |
| PH_TRASHBAG | prop | P2 | WP10 | ready | 4 | 0.25 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/trashbag; Assets/_Game/Art/Textures/Props/trashbag |
| PH_WICKER_BASKET_01 | prop | P2 | WP10 | ready | 4 | 0.56 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Models/Props/wicker_basket_01; Assets/_Game/Art/Textures/Props/wicker_basket_01 |
| TEX_ACG_CARDBOARD004 | texture | P2 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Cardboard004 |
| TEX_ACG_FABRIC044 | texture | P2 | WP10 | ready | 3 | 0.82 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Fabric044 |
| TEX_ACG_PAINTEDMETAL014 | texture | P2 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/PaintedMetal014 |
| TEX_ACG_WICKER010B | texture | P2 | WP10 | ready | 3 | 0.82 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/Wicker010B |
| TEX_CINEMA_CARPET | texture | P2 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/cinema_carpet |
| TEX_WORN_CORRUGATED_IRON | texture | P2 | WP10 | ready | 3 | 0.68 | `RawAssets/wp10/ready/` → Assets/_Game/Art/Textures/Env/worn_corrugated_iron |
