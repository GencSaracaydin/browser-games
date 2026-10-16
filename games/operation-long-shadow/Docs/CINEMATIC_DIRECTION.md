# Cinematic and realism direction (added 2026-10-16 at the user's request)

The user, verbatim: *"please put in effort for like scene cuts and stuff (especially in the beginning), aim for super realistic like CoD, this will engage people a lot."*

This document **adds to** GDD §8 and the realism targets in GDD §9, §12 and §13. Where it conflicts with them, this document wins. It applies to WP02, WP03, WP05, WP06, WP07, WP09, WP10_LOOK, WP10_LEVEL and the integration phase.

---

## 1. The bar: a Call of Duty campaign opening

The first three minutes decide whether people keep playing. Treat the intro as the game's trailer. Reference points are the Black Ops and Modern Warfare campaign openings:
- Cold-open shots with real cinematography.
- Cut on action, with sound leading the picture.
- Physical camera lenses and hand-held energy.
- A hero reveal of the battlefield, then a seamless hand-off into first-person control.

### 1.1 Camera language (every Timeline shot)
- **Physical cameras.** Use `Camera.usePhysicalProperties` with a Super 35 sensor (24.89 × 18.66 mm), focal lengths from 18 to 85 mm and a stated aperture. URP Bokeh DoF is driven from the shot's focus distance, so rack-focus is a keyable track. Wide shots use 18–24 mm at f/5.6–8. Medium shots use 35–50 mm at f/2.8–4. Close-ups and extreme close-ups use 50–85 mm at f/1.8–2.8.
- **Shot variety.** Never hold one shot size for more than two cuts in a row. Mix EWS, WS, MS, CU and ECU. Hold the 180° rule within a scene. Each cut changes either the angle by more than 30° or the shot size.
- **Cut on action** (a hand slap, boots landing, a door, a muzzle flash). This hides animation transitions that our clip libraries do not have.
- **Hand-held energy.** Layer Perlin noise on every non-locked camera: 0.15–0.35° rotation at 0.6–1.2 Hz for "operator breathing", plus impulse shake for impacts and the rotor (0.5–1.5°, fast decay). Booth POV shots get subtle breathing sway.
- **Speed ramps** for two or three hero moments, such as boots hitting the sand: time scale 1.0 → 0.45 → 1.0 over about 1.5 s with an audio pitch dip and a whoosh. Never use one more than once per sequence.
- **Motion blur** on fast pans and whip transitions: URP motion blur at camera-only intensity 0.3–0.5 during cinematics, off in gameplay unless the options turn it on.
- **Lens flare and glare** when the camera looks toward the sun (URP Lens Flare SRP on the sun disc, plus bloom). **Letterbox 2.39:1** for every third-person shot, with bars animated in over 0.5 s.
- **Transitions:** hard cuts by default. Use match cuts between the booth and the mission (the GDD's G-inserts). Use exposure blow-outs (+6 EV) for memory jumps. Use whip-pans with motion blur for energy. No cross-dissolves except the scripted ones in GDD §8.
- **Audio leads picture.** J-cuts: the next shot's audio starts 0.5–2 s before the cut. L-cuts let dialogue carry over a cut. Every cut gets a foley or SFX accent, and silence is a tool.

### 1.2 Making 2010-era characters read as photoreal
The Rocketbox and Meshy faces will not survive a lit close-up. Direct around that the way real productions hide stunt doubles:
- **Backlight and silhouette.** Shoot toward the low WSW sun (azimuth 258°) so faces fall into shadow, with a warm rim from the sun.
- **Partial occlusion.** Hat brims (Hollis's boonie), sunglasses (Tinker), the camera in front of Sparrow's face, smoke and dust.
- Use **extreme close-ups of hands, gear, eyes in shadow, boots and weapons** instead of face close-ups. Faces appear only in medium shots or wider, or in profile with hard shadow.
- **Shallow depth of field** on any close-up that includes a face, focused on the gear in front of it.
- Mouths stay unseen while lines play, unless the jaw flap from the D10 envelopes is wired.

### 1.3 Look
- **Grade:** 1980s film stock. Warm highlights, teal-leaning shadows, gently lifted blacks, halation around bright sources (bloom with a warm tint), film grain at intensity 0.35 and size 1.2 during cinematics. Subtle chromatic aberration only at the frame edges. Vignette 0.25–0.35.
- **Atmosphere** sells realism: height fog, a dust particle layer catching the sun (soft particles, lit), heat-haze distortion near the ground on the long lens (a cheap screen-space refraction on an opaque-texture pass, cinematics only), and rotor downwash dust (ring burst plus streaks).
- **Light:** one sun, physically placed. No fake fill on faces. Bounce comes from probes and lightmaps, and contact shadows come from SSAO.

---

## 2. New sequence: §8.2a INSERTION (third-person cold open, about 55 s)

**Placement.** Between GDD §8.2 shot 8 and shot 9. Shot 8's dolly into CRT2 ends on the desert horizon bands. The scanlines *become* the horizon, then hard-cut to I-1. I-12 replaces the original shot 9 opening: the white blow-out lands on the player prone on the ridge, and the title card follows as before.
- **Time of day:** story clock 17:52, six minutes before Beat 1. Use LightStage at the Beat 1 value (`s` = 0, sun 8° at azimuth 258°) so no extra bake is needed.
- **Length:** the intro grows from 1:43 to about 2:38.
- **Skip:** hold Space for 1 s to skip, allowed from the **first** viewing for the whole intro. This overrides GDD §8's "after first viewing", because web players are impatient. The title card always plays.

**Where.** A cinematic-only apron south-west of the playable tile, inside `S1_Approach`:
- Terrain covers x −220 … 0 and z −160 … 20. Flat-ish gravel desert and dunes at Y 10–14 rise to the Shoulder's reverse slope and the crest at (42, 22, 30).
- It is never reachable in gameplay: an OOB volume covers it.
- Detail level is cinematic, but cheap: CC0 sand and gravel materials, scattered rocks and dry shrubs, and two or three boulders at the landing zone.
- The helicopter exterior `VEH_HELICOPTER` must also exist in `S1_Approach`, as a second instance of the same prefab, because `S8_Heli` is not loaded during the intro.

**Markers for WP10_LEVEL** (add these to `markup_ids.csv` during integration):

| Id | Kind | Position / shape | Notes |
|---|---|---|---|
| `INS.LZ0` | SceneMarker | (−46, 12.5, −28), heading 45° | Skid contact point of the landing |
| `INS.Heli.Approach` | Spline | (−420, 34, −150) → (−260, 18, −95) → (−120, 9, −50) → (−60, 4.5, −33) → `INS.LZ0` | Low over the dunes out of the sun; nose-up flare over the last 25 m |
| `INS.Heli.Depart` | Spline | `INS.LZ0` → (−20, 18, −60) → (60, 40, −140) → (200, 60, −260) | Banks away SSE, staying clear of the town |
| `INS.Squad.Path` | Spline | `INS.LZ0` → (−10, 15, 0) → (22, 20, 18) → crest positions of GDD §7.1 | Crouch-run route up the reverse slope |
| `INS.Cam.*` | SceneMarker per shot | see the table below | WP09 may refine them in its builder |

**Shot list.** "Cast" means the squad, using their Beat 1 prefabs. Calder appears in third person through `PFB_Calder_Shadow`, rendered visibly with ShadowCastingMode On for this sequence.

| # | Time | Shot | Camera (lens, f-stop, move) | Action | Audio | Lines |
|---|---|---|---|---|---|---|
| I-1 | 0:00–0:05 | EWS aerial | 24 mm f/8. Low drone track left to right at Y 20, about 120 m from the helicopter, looking south toward the sun-lit dunes | Magpie skims the dunes along `INS.Heli.Approach`. Its rotor downwash raises a dust wake. The low sun throws long shadows from every dune crest. | **J-cut:** rotor approach starts under the last 2 s of booth shot 8. Music: the first synth pulse of `MUS_01` continues, low. | — |
| I-2 | 0:05–0:09 | MS tracking, door height | 50 mm f/2.8. Hand-held, flying alongside 8 m off the open left door | Tinker's boots dangle out of the door; rifle muzzles. The sun flares through the rotor disc and rotor-blade shadows strobe across the cabin. | Rotor, wind buffet | `D200` (Magpie, intercom) |
| I-3 | 0:09–0:12 | CU, cabin interior | 35 mm f/2. Hand-held | Hollis, backlit, boonie brim shading his eyes, turns to the cabin and raises one finger | Rotor (cabin bed) | `D201` (Hollis, shouted) |
| I-4 | 0:12–0:15 | ECU, hands | 85 mm f/1.8. Rack focus from the magazine to Hollis behind | Calder's gloved hands seat a magazine in the Commando (slap), then the charging handle (cut on the slap) | Mag seat and charging-handle foley, dry and close | — |
| I-5 | 0:15–0:18 | OTS, Sparrow | 50 mm f/2.8 | Sparrow raises her Minolta at the passing ridge line. The camera body hides her face. Shutter. | Shutter click and film advance | — |
| I-6 | 0:18–0:22 | CU, profile | 50 mm f/2.8, slight push-in | Tinker in sunglasses keys the PRC-77 handset. The whip antenna shivers in the downwash. | Squelch in/out | `D202`, then `D203` (Magpie, radio) |
| I-7 | 0:22–0:27 | Low WS, ground level | 18 mm f/5.6. Locked on the sand at (−30, 12.6, −15) looking WSW into the sun | Magpie flares nose-up toward camera and settles onto `INS.LZ0`. A dust wall rolls at the lens; silhouettes against the sun disc. Speed ramp starts at 0:25. | Rotor peaks, sand hiss on the lens | — |
| I-8 | 0:27–0:31 | ECU, slow motion | 35 mm f/2. Camera at boot height beside the skid | **Speed ramp 0.45×:** boots hit the sand and the dust puff hangs in the sunlight. Ramp back to 1.0× as the squad breaks into the crouch-run (cut on the first stride). | Low thump, whoosh on the ramp-out | `D204` (Hollis) |
| I-9 | 0:31–0:36 | MS frontal, backing up | 35 mm f/4. Hand-held, moving backward up the slope | The squad crouch-runs out of the dust toward the camera, rifles low. Hollis gives the "move" hand signal. | Boots on gravel, gear rattle | — |
| I-10 | 0:36–0:42 | WS | 35 mm f/8, slow pan | Magpie lifts and banks away along `INS.Heli.Depart` with its dust settling. The squad, small, climbs the reverse slope. | Rotor dopplers away | `D205` (Magpie, radio) |
| I-11 | 0:42–0:51 | **Hero reveal** | 28 → 40 mm, f/4. Crane up behind the squad as they drop prone on the crest (cut on the drop, from I-10). The camera rises over their heads and reveals Zaroud below. | Ochre town in haze, every building throwing a long shadow NE toward the relay mast, which glints. Slow push-in. | Music swell (motif); wind; distant curfew siren | `D206` (Calder, BOOTH chain, voice-over from the chair) at 0:44, `D207` (Interrogator, SCRAMBLER) at 0:49 |
| I-12 | 0:51–0:55 | Push-in to POV | 40 → 50 mm. The camera glides over Calder's shoulder into his eye line and matches the player camera at (42, 22.0, 30), heading 45°. | Exposure +6 EV blow-out, falling to 0 over 2 s, lands exactly on the gameplay view: the original shot 9. The title card starts. | Fluorescent buzz for 3 frames (a booth flash) | — |

**New lines** (added to `DIALOGUE.json`):
- `D200` MAGPIE: "Sundial, Magpie. One minute. Sun's right where you ordered it." (INTERCOM chain with the rotor bed)
- `D201` HOLLIS: "One minute!" (shouted over the rotor, DRY chain with the rotor bed)
- `D202` TINKER: "Magpie, Sundial-Four. Comms check." (RADIO)
- `D203` MAGPIE: "Lima Charlie, Four." (RADIO)
- `D204` HOLLIS: "Go, go!" (shouted)
- `D205` MAGPIE: "Wheels up. See you at last light, Sundial." (RADIO)
- `D206` CALDER: "Zaroud. Every shadow in that town pointed at the mast." (BOOTH chain, voice-over)
- `D207` INTERROGATOR: "Go on." (SCRAMBLER chain)

**Animation needs and fallbacks** (WP05). This is cut-on-action design, so no transition clips are needed:
- Seated in a helicopter (idle sitting).
- A drop from the skid, using the land/jump clip from UAL or Meshy. Cut I-8 starts at the landing.
- Crouch-run with a rifle.
- Hand signals (`ANIM_HYMOTION_SIGNALS`, P1). Fallback: Hollis's arm via IK pose.
- Prone idle. Cut I-11 starts *after* the drop to prone, so no prone transition is needed.
- Tinker's handset to the ear (IK pose).
- Sparrow raising the camera (IK pose).

**VFX** (WP10_LOOK): rotor downwash dust (a burst ring at the LZ plus streaks), a hanging dust sheet lit by the sun, sand blowing off dune crests, and a rotor-blade shadow strobe in the cabin (a projector cookie or a rotating-blade shadow caster).

---

## 3. Upgrades to the existing sequences

- **Booth (GDD §8.2 shots 1–8):** apply breathing sway to C_POV. Rack focus from the wrists (0.4 m) to the CRTs (1.3 m) in shot 3, f/2 equivalent. Show the interrogator only as a **shadow** crossing the CRT glow in shot 7. Cigarette smoke volumetric-ish (lit particles) in the CRT light. Degauss flashes light the room (one realtime point light flash per CRT).
- **Title card:** keep it. Add soft lens dirt and bloom to the amber type, and have the letterbox slide match the insertion's bars.
- **MID-A Last Light:** gameplay, no cuts. Make sure the sun disc, flare, sodium ripple and shadows are art-directed at the highest quality, because this is the signature screenshot.
- **MID-B The Reel:** the projector beam gets dust motes, a volumetric cone (an additive mesh cone with a noise texture) and a flickering light on the faces.
- **Exfil climax:** add a 3-frame booth insert on the RPG launch, a heavy rotor and impact shake, a red alarm strobe and a tinnitus duck.
- **Outro:** give the interrogator's walk-past real cinematography. Keep the POV, but rack focus from the cuffs to his backlit silhouette, with smoke curling through the CRT light.
- **Death:** 0.6 s slow-motion with a desaturate, then the booth glitch, per GDD.

## 4. Gameplay realism checklist (CoD feel)

- **WP02 player camera:** sprint bob and roll (subtle), FOV kick on sprint (+5°) and slide, landing dip scaled by fall height, an ADS FOV blend with an aim-in sound, procedural breathing sway when ADS, view punch on damage, and a gentle tilt on strafe.
- **WP03 weapons:**
  - Spring-based procedural view-model motion: position and rotation springs for recoil kick, sway lagging the look by about 1.5°, inertia on turn, sprint pose, and a jump/land bounce.
  - Visible bolt or charging-handle cycling on reload.
  - Muzzle flash with a light pulse lighting the arms and nearby walls.
  - Shell ejection with physics and per-surface clinks.
  - Tracers every third round for enemies, smoke puffs and surface-specific impact VFX with decals.
  - The gun sound layered with zone tails.
- **WP05 characters:** hands on weapons (IK), aim offsets, hit reactions per body part, death clips that blend into ragdoll, and footstep sync.
- **WP06 audio:** the most important realism lever per dollar. Layered gunshots with tails per space, bullet cracks and whizzes that tell you you're being shot at, distant firefights, radio squelch, and a dynamic ducking mix (combat ducks ambience, VO ducks combat by −6 dB).
- **WP10 look:** see GDD §9 and §1.3 above. Contact shadows, dust motes in sun shafts, heat haze on long sightlines, and decals everywhere (dirt, grime, bullet pocks, water stains) to break up tiling.
