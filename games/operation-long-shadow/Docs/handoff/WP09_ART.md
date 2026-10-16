# WP09_ART handoff: booth set, CRT/intel images, videos, document inserts (+ WP08 UI sprites and fonts)

Status: **done**. Every manifest id in the package is import-ready: all IMG_* (except IMG_CONCEPT_KEYFRAMES, WP10), both VID_*, the 16 booth-set ids (7 FAL_, 5 PH_, 2 TEX_, PROC_BOOTH_SHELL textures, PROC_BOOTH_SMALLS meshes) and every UI_* except UI_FONT_FALLBACK (bundled TMP LiberationSans, nothing to produce) and UI_FONT_SARDANI (built by the WP08 code agent).
fal spend: **WP09 $16.58 of $50** (58 ledger rows; $7.91 was the first run before the outage, $8.67 this run), **WP08 $0.34 of $15** (2 rows). No editor/MCP use. Nothing committed (orchestrator commits).

## 1. Where things are
| What | Path |
|---|---|
| Import-ready files (mirror of the Unity paths) | `RawAssets/wp09/ready/Assets/...`, `RawAssets/wp08/ready/Assets/...` |
| Import index (manifest_id, file, target_path, type, import_notes, bytes, pixels) | `RawAssets/wp09/ready/import_index.json` (107 rows), `RawAssets/wp08/ready/import_index.json` (47 rows) |
| Material specs for MaterialFactory (same schema as WP10's) | `RawAssets/wp09/ready/materials.json` (25 materials) |
| Per-tool rows the index is built from | `RawAssets/{wp09,wp08}/ready/rows/*.json` |
| Raw generations per id (refs, GLBs, takes, request/result/timing json) | `RawAssets/wp09/<ID>/`, `RawAssets/wp08/<ID>/` |
| Review sheets (look at these first) | `RawAssets/wp09/_review/FINAL_props_fal.png`, `FINAL_images.png`, `FINAL_textures.png`, `ph_props_shipped.png`, `VID_*_final.png`, `IMG_FILM_LEADER.png`, `docs_a.png`; `RawAssets/wp08/_review/ui_all.png`, `fonts_proof.png` |
| Per-prop processing sheets (6 views, wireframe, maps, reference) | `RawAssets/wp09/_review/FAL_<ID>__tripo_t*.png`; shipped-FBX renders `RawAssets/wp09/_review/fal/SM_*_{a..e}.png` |
| Measuring renders used to place parts | `RawAssets/wp09/_ortho/` |

## 2. Inventory
**Model conventions** (all FBX): Blender 5.2, forward -Z / up Y, baked space transform, FBX_SCALE_ALL -> Scale Factor 1, Convert Units on, 1 unit = 1 m, identity rotation, front = Unity +Z, pivot bottom-centre unless noted. Import Materials None, Normals Import, tangents Mikk. Mask maps `_M` = R metallic, G AO, B 0, A smoothness (Metallic Map + Occlusion Map). Child part pivots are given as Unity local positions (Blender (x,y,z) -> Unity (-x, z, -y); verify the X sign once on the first import).

### Booth props (fal Tripo P2 -> Blender)
| Id | Mesh (tris) | Maps (file / in game) | Moving parts and notes |
|---|---|---|---|
| FAL_SCRAMBLER_BOX | SM_ScramblerBox 2998 + Toggle 120 + Needle 48 | 1024 / 1024 (hero) | **Toggle** (chrome bat lever in the panel bushing, ON as exported; OFF = localEulerAngles.x +50). **Needle** (VU needle under the bezel lip, exported at rest full-left; drive localEulerAngles.z = 94 x level, positive swings right as seen from the front). Meter face repainted with scale ticks + red zone. Box spans Unity x -0.18..+0.08; the coiled cord dips 2.4 cm below the box base. |
| FAL_REEL_TO_REEL | SM_ReelToReel 2770 + ReelLeft/Right 468 each + TapePackLeft/Right 88 each + TapePath 12 (= 3894) | 1024 / 1024 (hero) | Procedural 7-inch metal reels (windows show the brown tape pack). Both reels turn counter-clockwise seen from above in playback (negative Y rate in Unity), deg/s = ips x 0.0254 / (2 pi r) x 360 with r = 0.075 x pack scale. TapePack localScale x=z sets the pack radius (supply 1.0 full -> 0.42 empty). Deck levelled 2.7 deg (the scan was tilted). Also the basement tape-loop deck. |
| FAL_ROTARY_PHONE | SM_RotaryPhone 1923 + Handset 568 | 1024 / 512 | **Handset** split from the scan (same material), pivot at its centre, for the outro receiver lift (D091). The top of the coiled cord lifts with it: frame the lift so the cord gap is not seen. |
| FAL_INTERROGATION_CHAIR | SM_InterrogationChair 5000 | 1024 / 1024 (hero) | Bolted steel chair with leather cuffs/straps and taped wires; seat at 0.46 m. Electrodes are separate (PROC_BOOTH_SMALLS). |
| FAL_AV_CART | SM_AVCart 1999 (1.13 x 0.85 x 0.56 m) and SM_AVCartLong 2000 (1.70 x 0.80 x 0.53 m) | 1024 / 512, shared | AVCart = scrambler trolley (top 0.85 m). AVCartLong = the 1.7 m CRT cart (middle band stretched, casters rigid); it uses the T_AVCart maps (materials.json has M_AVCartLong with the same maps, or assign M_AVCart). |
| FAL_ASHTRAY (P1) | SM_Ashtray 800 (0.16 m) | 1024 / 512 | Heavy amber pressed glass (opaque glossy), butts and ash inside. Cigarette = SM_Cigarette. |
| FAL_FILM_CAMERA (P1) | SM_FilmCamera16mm 2999 (1.95 m) | 1024 / 512 | 16 mm turret camera on a wooden tripod; lens = +Z; camera height ~1.85 m for the (-1.6, 1.9, 2.2) placement. |
| PH_TELEVISION_01 / _02 | 1918/958/478 and 2310/1154/577 (LOD0-2) | 1024 | First run. Screen glass is its own slot `M_Television0x_Screen` with a 0..1 UV (u right, v up) for SG_CRTScreen + RT_CRTn. Brand marks painted out. |
| PH_WALL_CLOCK | SM_WallClock 3658, children HandHour/HandMinute/HandSecond | 512 + crack overlay | Hands pivot on the dial centre at 12:00 (6:40 = hour -200, minute -240 deg about local Z). Booth instance: glass slot -> `M_WallClock_Glass_Cracked` (T_WallClockGlassCrack_A_BC). |
| PH_VINTAGE_LIGHTER | SM_SteelLighter 3366, children Lid / Wheel | 512 | Bronze recoloured to brushed steel, brand removed (GDD "steel lighter"). |
| PH_MOUNTED_FLUORESCENT_LIGHTS | SM_FluorescentBatten 8910 (7 battens as children) | 512 (+ `_E`) | Booth uses FluorescentBatten_A at (0, 2.95, 0.6); glass emission = tube flicker. |
| PROC_BOOTH_SMALLS | SM_BoothPen 256, SM_Electrode 254, SM_ElectrodeWire 440, SM_Cigarette 140 (+ empty SmokeAnchor) | shared T_BoothParts 512 | `Art/Models/Props/booth_smalls/`. Pen = the four-tap emitter; electrodes parent to Calder's forearm bones; cigarette ember slot `M_CigaretteEmber` (emissive flicker). |

`M_BoothParts` (T_BoothParts_BC/M 512, no normal) is shared by every procedural part: flange/tape discs + 22 swatches (layout in `Tools/images/wp09_parts_atlas.json`).

### Booth surfaces (PROC_BOOTH_SHELL is built in the editor; these are its materials)
| Id | Files | Use |
|---|---|---|
| TEX_ACG_PAINTEDPLASTER012 | `Env/PaintedPlaster012/T_PaintedPlaster012_{BC,N,M}` 2048 (hero) | olive walls, ~2 m per repeat |
| TEX_ACOUSTIC_TILE | `Env/acoustic_tile/T_AcousticTile_{BC,N,M,H}` 1024 | procedural perforated 12-in tiles, 2x2 per repeat = 0.6096 m (tiling 1/0.6096); walls upper band + ceiling; `_H` optional parallax |
| PROC_BOOTH_SHELL | `Env/Booth/T_BoothDrain_{BC,N,M}` 512 (alpha decal, 0.42 m, floor (0,0,0.8)), `Env/Booth/T_OneWayMirror_{BC,M}` 256 | drain = URP Decal Projector (`M_BoothDrain_Decal`); mirror = near-black glass, the baked reflection probe does the image. Floor = WP10 `concrete_floor_worn_001`. |

### Images (`Art/Textures/Images/`, period treatment baked; the CRT shader adds curvature/scanlines)
| Id | File(s) | Source / treatment |
|---|---|---|
| IMG_MAP_SARDAHN | T_MapSardahn_BC 2048x1536 | nano-banana take_b (hand-drawn sketch map, no text, Zaroud circled in grease pencil) |
| IMG_RECON_STATION | T_ReconStation_BC 2048x1536 | take_c (round 2) oblique long-lens B&W + Tri-X grain, vignette, recon fiducial marks |
| IMG_HARROU_STILL | T_HarrouStill_BC 2048x1536 | take_a, Super 8 frame, face scratched out (fallback for the video) |
| IMG_AIRPORT_PLATE | T_AirportPlate_BC 2048x1536 | take_a + 400 ASA colour-negative grade |
| IMG_SURVEIL_SPARROW | T_SurveilSparrow_BC 2048x1536 | nano-banana edit of the airport plate with the WP05 Sparrow renders as identity (braid, aviators, safari shirt, camera bag), long-lens crop, same grade. Burn-in `26.10.84 18:31` stays live TMP. |
| IMG_INTEL_LEDGERS / REELS / CODEBOOK / SCHEDULE / FILMLABEL | T_Intel*_BC 1024x1024 | SX-70 polaroids; only the square image area (the frame is T_UI_PolaroidFrame); imported as UI sprites by name |
| IMG_LEDGER_PAGE / IMG_CODEBOOK_PAGE | T_LedgerPage_BC, T_CodebookPage_BC 1536x2048 | flat document plates under TMP |
| IMG_TEAM_ID_PHOTOS | team_id/T_TeamID_{Hollis,Sparrow,Tinker}_BC 2048x1536 (+ bare photos, blank card) | photoreal B&W ID portraits derived from the WP05 head renders, clipped to a manila card, typed callsign/name/height/weight/blood, red callsign stamp (SUNDIAL-ACTUAL / -2 / -4) |
| IMG_ROSTER_PAGE | roster/T_RosterPage_FS_BC 1536x2048 (+ blank) | 14 typed callsigns; line 13 `SUNDIAL-3 ..... CALDER, N. ......... 0640`, line 14 `SUNDIAL-2 ..... CASTELLANOS, M. .... 1921` (text tops at y ~1446 / 1522 px from the top of the 2048 px page) |
| IMG_TELETYPE_FEED | teletype_feed/T_TeletypeFeed_BC 1024x2048 (+ blank paper) | canary roll, 30 lines of number groups incl. `74410 00640`; tiles vertically for scrolling |
| IMG_FILM_LEADER | leader/T_FilmLeader_{5,4,3}_BC 512x384 + `StreamingAssets/video/VID_FilmLeader.mp4` (4 s, 18 fps) | countdown leader with sweep, dust, scratches |
| IMG_DEBRIEF_DOSSIER | debrief/T_DebriefBackground_FS_UI_BC 2048x1536, T_RedactionBar{1-3}_UI_BC, T_StampAssetsActive_UI_BC, T_StrikeThrough_UI_BC | classified sheet in a folder; felt-marker redaction bars; `PROGRAM LONG SHADOW / ASSETS ACTIVE: 13` red stamp |

### Videos (`Assets/StreamingAssets/video/`, H.264 Main 512x384, no audio)
| Id | File | Content |
|---|---|---|
| VID_HARROU_8MM | VID_Harrou8mm.mp4 (8.0 s, 18 fps, 1.09 MB) | Harrou walks the Legion line in Clock Square, face scratched out for the whole shot; gate weave, flicker, dust, scratches |
| VID_GUNCAM | VID_GunCam.mp4 (12.0 s, 24 fps, 1.43 MB) | monochrome tube image beside the open cabin door: RPG trail passes under the skid at 4.2-4.6 s (lurch), one muzzle flash lights the cabin at 9.1 s; tint green in SG_GunCam |

### WP08 UI (`RawAssets/wp08/ready`)
Sprites `UI/Sprites/<group>/T_UI_*.png` (white = tintable unless noted): icons (Frag, Intel, Radio, FireAuto, FireSemi, Interact, Crate + Binoculars, 128 px), vignette (DamageVignette 1024x512), shapes (DiamondFill/Outline, Chevron, EdgeArrow, GrenadeArrow, Line [9-slice], HitMarker, DamageArc, Ring, RingFill, SoftDot, SoftPanel [9-slice]), polaroid_frame (PolaroidFrame 560x670, window rect in its row), binocular_reticle (BinocularMask 1024x576, BinocularReticle 512), doorgun_ring (DoorGunRing 512, HeatBarFrame [9-slice]), click_gate (ClickGateBG, CrtCursor), letterbox (LetterboxBar [9-slice]), pause_panel (CrtPanel [9-slice], Scanlines tile). Fonts in `Art/Fonts/`: SpecialElite-Regular (Apache 2.0), CourierPrime-Regular/Bold, BarlowCondensed-Regular/Medium/SemiBold, ShareTechMono-Regular, IBMPlexMono-Regular (OFL), each with `<font>_LICENSE.txt`.

## 3. Import instructions (editor holder)
1. Copy `RawAssets/wp09/ready/Assets/**` and `RawAssets/wp08/ready/Assets/**` into the project (same relative paths). WP00's import rules handle most settings; the per-row `import_notes` say what to check (texture type, sRGB, max size, NPOT, 9-slice borders, mask channels).
2. Models: Scale Factor 1, Convert Units on, Import Materials None, Animation None. Single-LOD booth props: Mesh LOD optional; PH TVs carry `_LOD0..2` (build LODGroups, importer Mesh LOD off for those). Keep the child objects (Toggle, Needle, Reel*, TapePack*, TapePath, Handset, clock hands, lighter Lid/Wheel, SmokeAnchor) - code drives them.
3. Materials: build from `RawAssets/wp09/ready/materials.json` (MaterialFactory schema: shader, textures by property, floats, colours, surface/blend, emission). Assign by FBX slot name (`M_<Name>`, `M_BoothParts`, `M_CigaretteEmber`, `M_<Name>_Screen/_Glass`).
4. Videos: leave in `Assets/StreamingAssets/video/`; `VideoPlayer.url = Path.Combine(Application.streamingAssetsPath, "video/VID_*.mp4")`, render into the CRT render texture (RT_CRT5 for Harrou, RT_CRT2 for the gun cam), no audio track.
5. Fonts: create TMP SDF font assets (typewriter atlas 1024, condensed and mono 512, Padding 9, dynamic) - WP08/WP11 own the UITheme.
6. Blank plates (T_TeamID_Card, T_TeamID_*Photo, T_RosterPage_Blank, T_TeletypePaper, leader plates) are only for an optional live-TMP re-bake; do not reference them unless used (StreamingAssets ships everything in the folder; Assets only what is referenced).

## 4. Tools (all in `Tools/images/`, rerunnable; fal calls go through `Tools/fal/falcall.py`)
```
PY=Tools/.venv/bin/python            # PIL/numpy/opencv/fontTools/imageio-ffmpeg (ffmpeg binary comes from the imageio-ffmpeg wheel)
FALPY=/Users/gencsaracaydin/Documents/fun/.venv/bin/python
B=/Applications/Blender.app/Contents/MacOS/Blender
$FALPY Tools/images/wp09_fal.py batch Tools/images/jobs/roundN.json   # fal: nb / edit / 3d / patina / gptimg / raw (video); idempotent per tag
$PY Tools/images/wp09_fetch.py [--fonts]          # Poly Haven / ambientCG / Google Fonts downloads (first run)
$B -b --factory-startup -P Tools/images/wp09_ph_model.py -- --ids PH_...   # Poly Haven booth props (first run), then
$PY Tools/images/wp09_paintout.py && $PY Tools/images/wp09_props_tex.py     # brand paint-out + PH texture packing
$PY Tools/images/wp09_parts_atlas.py              # T_BoothParts atlas + uv json for the parts
$PY Tools/images/wp09_prop.py <FAL_ID> --tag tripo_t1 [--variant long --vname AVCartLong]   # GLB -> FBX + maps + sheet
    # config: wp09_fal_props.json (name/size/tris/tex), wp09_fal_proc.json (yaw, lods, level, stretch, post_translate,
    # smooth_angle, extras=wp09_parts.py); vendored Blender scripts in vendor/ (blender_prop.py has WP09 hooks)
$PY Tools/images/wp09_texfix.py scrambler         # VU meter face ticks (run after the scrambler)
$B -b --factory-startup --python-exit-code 1 -P Tools/images/wp09_smalls.py && $PY Tools/images/wp09_smalls_rows.py
$PY Tools/images/wp09_fal_mats.py                 # material specs for the fal props
$PY Tools/images/wp09_booth_tex.py                # plaster, acoustic tile, drain, crack, mirror
$PY Tools/images/wp09_imgpost.py --sheet          # take picks + period treatment for the IMG plates (picks + reasons in PICKS)
$PY Tools/images/wp09_docs.py                     # ID cards, roster, teletype, film leader (+ mp4), debrief sprites
$PY Tools/images/wp09_video.py harrou|guncam      # video finishing
$PY Tools/images/wp08_ui.py                       # WP08 sprites + fonts
$PY Tools/images/wp09_index.py                    # rebuild both import_index.json + materials.json, validate, orphan check
```
Review helpers: `contact.py` (contact sheets), `bl_render_shipped.py` (shipped FBX + materials.json renders), `bl_ortho.py` + `ortho_grid.py` (metric ortho views), `bl_uvprobe.py` (texture pixel under a 3D point), `gridcrop.py`. One-shot runner: `RawAssets/wp09/_run_fal_all.sh`.

## 5. Deviations from the manifest (logged in Docs/DECISIONS.md)
- File names follow TECH_PLAN §4.2 (`T_<Name>_<Suffix>`) instead of the manifest's lower-case image names; rows keep `manifest_target_path`.
- VID_HARROU_8MM / VID_GUNCAM made offline with fal Kling v3 Pro + Python finishing instead of the D14 Unity Recorder pass. VID_FilmLeader.mp4 added. StreamingAssets/video = 2.7 MB (plan said <= 2 MB for 2 clips).
- Editor-bake IMG items baked offline (blank plates kept for a live re-bake). IMG_SURVEIL_SPARROW is a fal edit, not an editor composite.
- TEX_ACOUSTIC_TILE procedural (Patina rejected). FAL_ASHTRAY via Tripo, amber glass. FAL_FILM_CAMERA tripod legs stretched from the first scan. FAL_AV_CART has two meshes. PROC_BOOTH_SMALLS in `Props/booth_smalls/` not `Kit/Booth/`.
- Booth fal props are single-LOD; the reel deck uses 3894 tris incl. reels (budget 4000), the phone 2491 incl. handset (budget 2000: +25 % for the split and capped holes).

## 6. Known issues / follow-ups
- Part pivots and rotation signs (needle, toggle, reels) are derived from the Blender->Unity axis mapping, not tested in Unity: check once in the booth prefab (flip the sign in code if a needle swings the wrong way).
- Phone handset: capped holes under it are rough and the coiled cord breaks when it is lifted; frame the D091 lift accordingly.
- SM_ScramblerBox: the coiled cord sits 2.4 cm under the box base (it rests into the trolley top).
- The reel deck top and the reels are levelled, but the deck's wooden front face keeps a slight scan skew (invisible from C_POV).
- Ashtray glass is opaque (amber); if a translucent look is wanted, use Transparent with alpha ~0.9 (sorting risk with the smoke).
- Gun-cam: the composited RPG is a smoke trail without a visible rocket body (reads at 512x384 on the CRT); the muzzle flash is a 2-frame bloom.
- Intel schedule polaroid shows blurred pseudo-typing; at card size it reads as illegible text (no real words).
- The `_H` height map of the acoustic tile is optional and not referenced by M_AcousticTile.
