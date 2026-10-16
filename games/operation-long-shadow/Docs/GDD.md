# OPERATION LONG SHADOW: Game Design Document (final, buildable)

**Version 1.1** · 2026-10-16 · Campaign creative director · completeness/feasibility review applied (changes listed in `TECH_PLAN.md` → Review log)
**Target:** Unity 6000.6.6f1 · URP 17.6 (Forward+) · Input System 1.20 · uGUI/TextMeshPro · WebGL2 build in desktop Chrome/Safari, keyboard and mouse, hosted as a private claude.ai artifact (static host).
**Companion files:** `Docs/DIALOGUE.json` (every spoken line, 157 entries) · research in `Docs/research/` (fal_3d, fal_media, cc0, webgl) · source pitches in `Docs/pitches/`.

This is the one mission we build: a Cold War black op in a fictional desert republic, about 14 minutes of play framed by an interrogation, with squad, radio and cinematic framing on the Black Ops model. Sections 0–1 record the decision. Sections 2–19 are the spec.

**Precedence rules (review 2026-10-16):** (1) coordinates and numbers in tables win over the ASCII maps, which are schematic; (2) where `TECH_PLAN.md` §1 records a decision (D1–D22) that refines this document, the decision wins and this document has been updated to match; (3) `DIALOGUE.json` is the only source for spoken text, subtitles and line triggers; (4) `TECH_PLAN.md` Appendix A is the only source for marker and trigger ids.

---

## 0. Pitch review: scoring, decision and grafts

### 0.1 Scores

All three pitches were read in full against the four research reports. Each criterion is scored 1–10. The weights reflect the user's stated priorities: realism first, squad/radio and cinematics next, and a scope that can actually be built in WebGL.

| Criterion (weight) | LAMPLIGHTER (night) | **LONG SHADOW (dusk)** | SALT MIRROR (defector) |
|---|:---:|:---:|:---:|
| Black Ops feel (×1.0) | 9 | **10** | 7 |
| Squad and radio emphasis (×1.5) | 9 | 8 | 9 |
| Cinematic framing (×1.5) | 9 | **10** | 8 |
| Realism showcase within WebGL limits (×2.0) | 7 | **9** | 7 |
| Feasibility / scope risk (×1.5, higher = safer) | 6 | 5 | **7** |
| Pacing (×1.0) | 7 | **9** | 8 |
| **Raw total (/60)** | 47 | **51** | 46 |
| **Weighted score (/10)** | 7.76 | **8.41** | 7.65 |

**Winner: OPERATION LONG SHADOW (dusk),** rebuilt below with grafts from the other two pitches and scope cuts aimed at its one weak score, feasibility. Its projected feasibility after the changes in 0.4 is 6.5.

### 0.2 Rationale by criterion

**Black Ops feel.**
- *Dusk (10):* a sleeper agent activated by numbers, a stopped clock, film of the hero's own conditioning, and an interrogator who turns out to be his handler. That is the Black Ops spine (Mason and the numbers), retold in the player's own invented setting. It ends with the player's hand raising a pistol at his own team leader.
- *Night (9):* the handler turns out to be the villain, revealed when the scrambled voice goes clean. Very strong, but the player is the victim rather than the weapon.
- *Defector (7):* the twist is guilt. That is fresher, but less "Black Ops": there is no conditioning and the handler is not the villain.

**Squad and radio.**
- *Night (9):* WICK is on the radio all night and the team stops trusting him in the middle of the mission.
- *Defector (9):* Sextant, Static and Spanner disagree on screen, and Static's receiver plays positional radio.
- *Dusk (8):* three distinct squadmates, a leapfrogging node-chain squad, and numbers breaking onto the squad's own net. Its handler only exists on tape, so we graft radio texture from the other two (0.3).

**Cinematic framing.**
- *Dusk (10):* the richest package: a diegetic click gate, a six-CRT montage, a title card whose number groups resolve into text, a live sunset as the mid-mission cinematic, a film-within-the-film, a reusable glitch-insert system, a pistol-rise climax and the activation outro.
- *Night (9)* and *Defector (8)* are excellent but more conventional.

**Realism within WebGL limits** (the user's top priority, weighted ×2). The research findings decide this one:
- *Dusk (9):* golden hour turning into blue hour is the most flattering light for what we can afford. Raking low sun plus cool sky fill makes CC0 PBR materials and simple modular masonry read as photographic, and it flatters the 2010-era Rocketbox characters (`cc0.md` §7). The research recipe of Mixed sun, baked non-directional lightmaps per additive section, light probes and baked reflection probes (`webgl.md` §4.3) works because each section is baked at its own fixed stage of the sunset. Only the rooftop sunset changes live, and at 1.5° sun elevation the bounce light we cannot re-bake is negligible. Both skies come from Poly Haven HDRIs that were checked in research (`kiara_8_sunset`, `kiara_9_dusk`). The second half of the mission runs on practical lights (sodium, floods, muzzle flash, flares), which Forward+ handles on WebGL2 (verified).
- *Night (7):* a static moon bakes perfectly and is cheap. But darkness hides the very realism the user wants to see, and it brings readability and banding risk. Its night HDRI (`rogland_*`) is a Norwegian sky with a masked horizon.
- *Defector (7):* noon is the least flattering light for simple geometry, and the opening's 2 km sightlines are the most expensive thing to draw in WebGL. After that the storm hides everything, which is cheap but turns the realism showcase into brown fog, with overdraw risk on Retina screens.

**Feasibility.** All three need about ten areas. What separates them:
- *Defector (7):* hangars, tanks and pipes are easy modular shapes, and Poly Haven is rich in industrial props. Its twist depends on the player comparing two faces at 1.5 m, which is risky with 2010-era heads and no lip-sync.
- *Night (6):* it depends on animations that no source has: a fireman's carry through four beats, paired knife takedowns and prone crawls (`cc0.md` §7.4). It also needs systemic stealth with a light-sensitive detection model and a fail state.
- *Dusk (5):* the biggest map. It has a hero cinema interior, a three-level station, an on-rails flight over the town and four lighting stages. Its animation needs (bolt cutter, satchel, drill, door gun, check watch) are all covered by UAL, Rocketbox or IK poses. Scope is the problem, and section 0.4 cuts it.

**Pacing.**
- *Dusk (9):* first shot at about 1:00, a loud gate at about 1:30, steady escalation, a sunset breather at the midpoint, a quiet film reel, a defense, an LZ hold and an on-rails finale.
- *Defector (8):* the storm reshapes play halfway through.
- *Night (7):* five minutes of scripted stealth before the first loud fight is long for a CoD-arcade brief.

### 0.3 Grafts from the runners-up

| # | Idea | From | Where it lands | Why |
|---|---|---|---|---|
| G1 | **Voice-scrambler box**: the interrogator's voice runs through an on-screen scrambler (−3 semitones, band-limited, light ring modulation, twitching VU needle). He flips the toggle off in the outro, and his clean voice is the briefing-tape voice. | Night | Intro, every booth line, outro shot 7 (`D090`, `D091`) | Dusk's voice-match twist becomes one unmistakable beat. It is pure offline audio processing, so it is cheap and safe. |
| G2 | **Parachute illumination flares** (a downward shadowed spot, pendulum sway, 30 s burn) | Night | Beat 10 LZ hold at blue hour | The night pitch's signature visual, placed where the sky is already dark. One shadowed light. |
| G3 | **Torn awnings let light through**: the balcony gun shreds awnings, opening golden sun shafts | Night | Beat 3 Long Market | Realism showcase at golden hour, built from swap meshes plus pre-placed shaft cards. |
| G4 | **Destructible practicals rule**: realtime, unshadowed, at most 6 per area, and everything else baked | Night | Beats 6, 10 (floods, yard lamps) | Prevents the "shot-out lamp still glows in the lightmap" bug. |
| G5 | **Civilians heard, never seen**: kettle, muted TV, shutters latching, a radio turned down when shooting starts | Night | Beats 1–4 ambience | Lived-in town with no civilian-harm risk. |
| G6 | **One-parameter LightStage** drives sun, sky, fog, exposure, ambient, AI sight and hearing, ambience beds and the HUD palette | Defector (StormController.t) | §9.1 | Gameplay and visuals cannot drift out of sync. One debug slider. |
| G7 | **Green chemlights** on squad shoulder straps after sunset | Defector | Beat 6 onward (`D043`) | Friendly ID at blue hour, plus a cheap emissive accent. |
| G8 | **Greycoat squelch-click code**: 1 click = hold, 2 = move, 3 = target sighted | Defector (Survey hoods) | Every Greycoat encounter | An elite tell that rewards listening with headphones. Very few words to generate. |
| G9 | **Positional radio first**: the Clockmaker is heard faintly from Tinker's PRC-77 backpack in 3D before it breaks onto everyone's headset | Defector (Static's receiver) | MID-A Last Light (`D038`, `D039`) | Audio realism and a creeping-dread build. |
| G10 | **Eye-adaptation volumes**: +1.0 EV over 1.5 s entering interiors, −0.8 EV over 1.0 s on exit | Defector / Night | All interiors | URP has no auto-exposure. |
| G11 | **Pre-baked emergency lightmap swap** when the transmitter dies | Defector (generator death) | Beat 9 | Offline-quality lighting change at no runtime cost. |
| G12 | **Photos and film rendered in-engine from the real character heads** | Defector | Intro team ID photos, MID-B reel | Faces always match the models. No generated likeness can drift. |
| G13 | **Optional intel**: 2 documents that hint TALLOW/"P" | Night (clue trail) | Beats 7–8 (`D052`, `D058`) | Rewards curious players. Shown on the debrief. |

### 0.4 What changes from the dusk pitch (scope and fixes)

- **Date fix.** The booth date moves from 22 Oct to **2 November 1984**, because the outro's surveillance burn-in (26.10.84) has to lie in the past. "Session 12" now reads as the continuation of his conditioning sessions (the reel shows Session 11).
- **Lighting.** The pitch planned exteriors with no baked GI. We **bake every section at its own stage** (§9.4).
- **Skies.** Four HDRIs are cut to two (`kiara_8_sunset`, `kiara_9_dusk`), with tinted variants for the in-between stages. The visible sky is Poly Haven's tonemapped LDR panorama with the sun painted out, plus our own sun disc. That ends the "two suns" problem and avoids shipping RGBAHalf cubemaps, because WebGL has no BC6H (`cc0.md` §2).
- **Weapons.** The PKM is merged into the RPK pattern (one asset). The SVD, RPG-7 and DShK are world-only models.
- **Interiors.** The cinema is lobby, auditorium, balcony stair and marquee only. The Relay Hall shrinks from 50 × 40 m to 40 × 32 m. The upper floor is the screening room and landing only.
- **Grove.** 40 trees become about 24 instances of two fal-generated olive and pomegranate meshes.
- **Exfil flight** only flies over areas that already exist, plus a far-LOD town proxy. It has 4 rooftop targets and 2 trucks.
- **Bark lexicon rewritten.** `Lamba` is a real everyday word ("lamp") in Turkish and several Arabic dialects, and `Sabrak` is close to a real Arabic word. Both are gone. The Greycoat language loses its Slavic-sounding words so the bloc stays unnamed. See §14.4.

---

## 1. Mission at a glance

| | |
|---|---|
| **Operation** | LONG SHADOW. The team thinks it is the raid's name; it is the conditioning program's name. |
| **Year / time** | Mission: 21 October 1984, 17:58–19:10 local, compressed about 5× into play. Frame: 2 November 1984, 03:12, "Session 12". |
| **Setting** | Zaroud, a customs town on the old trans-desert telegraph line, **Republic of Sardahn** (fictional) |
| **Player** | SFC Nathan "Cal" Calder, callsign **SUNDIAL-3** |
| **Squad** | Hollis (SUNDIAL-ACTUAL), Sparrow (SUNDIAL-2), Tinker (SUNDIAL-4) |
| **Enemies** | Harrou Customs Legion (mercenaries) and the Greycoats (advisors from an unnamed bloc). 59 total, designed peak 7 alive, hard cap 12. |
| **Length** | About 14 min of play over 11 beats (≈ 13:55 on Regular, §7.0), plus about 4.5 min of cinematics: intro with the title card overlapping its last shot 1:43, MID-B 0:45, climax 0:12, outro 1:50, and MID-A, which stays playable (≈ 0:47) |
| **Footprint** | 420 × 420 m playable. The critical path is about 650 m on foot plus a 50 s flight. |
| **Weapons** | Commando carbine, Hush Puppy suppressed pistol, AK and RPK pickups, frag grenades, door gun (on rails) |
| **Intel** | Mission items: Ledgers, S-3 Reels, Codebook (3/3). Optional: Broadcast schedule, Film-can label (0/2). |
| **Feel** | CoD arcade: health regenerates, sprint, slide, mantle, quick TTK, ADS |
| **Pillars** | (1) **The light is the clock**: the sun sets during the raid and the mission changes with it. (2) **The team is the mission**: squad and radio carry every beat. (3) **The chair is always there**: the interrogation intrudes, and it is the truth. |

---

## 2. Setting and sensitivity guardrails

### 2.1 Republic of Sardahn (fictional)
A landlocked desert republic of salt pans, basalt ridges and caravan roads. It became independent in the 1950s and has had a coup almost every year since. The capital controls the cities and very little else. On the border, whoever holds the customs posts holds the money. Its language, **Sardani**, and its script are invented.

### 2.2 Zaroud
A town of about 4,000 people on a low shelf above a dry salt pan.
- **Weighbridge Gate:** a customs arch with a truck scale.
- **Long Market:** a covered market street running north into **Clock Square**. The colonial clock tower there stopped at **6:40** years ago.
- **Cinema Zenith:** a faded 1950s picture house showing invented films.
- **Zaroud Relay Station:** a 1930s Art Deco telegraph and cable relay station with a **90 m lattice mast**, built by the defunct *Trans-Desert Telegraph & Cable Co.* (the name is painted on the façade in Latin letters). Colonel Harrou has it as his headquarters, and it hosts the numbers station Western intelligence calls **"the Clockmaker"**.

Harrou declared a **curfew at 17:30**. The siren is winding down when the mission starts. The town reads as lived-in: laundry, chalk drawings, cinema posters, food cooling on a café table, and radios murmuring behind shutters. **No civilian is ever on screen.**

### 2.3 Guardrails (apply to every asset, prompt and line)
1. Every country, town, faction and company is invented. The foreign bloc is **never named** and never coded through a real language. No real country, religion or ethnic group is the enemy, and no real militant group appears.
2. **No religious buildings, imagery or audio.** Skyline landmarks are a clock tower, a radio mast, a water tower, a cinema marquee and a grain silo. No domes, no minaret-like towers, no call to prayer, no chanting.
3. **No real-world script on any surface.** Signage, posters and graffiti use the invented Sardani glyph set, which we design once and render in-engine, or Latin letters and numerals. Every image prompt says "no text" or "invented geometric glyphs that resemble no real alphabet", and every decal gets a human review (`fal_media.md` §8).
4. Locals are Harrou's victims. The Legion is a stateless mercenary army; its silhouette comes from mismatched surplus camo, berets, boonies, aviator goggles and **orange customs armbands**. No keffiyeh-as-villain shorthand.
5. Before any bark is recorded, every invented word is checked against a web search for accidental meanings, with priority on Arabic, Persian, Turkish, Hebrew, Kurdish and Slavic languages. The lexicon in §14.4 has passed an internal pass; the recording-time check is still required.
6. Real period weapons (AK pattern, RPG-7) are fine. No national insignia appears on anyone.

---

## 3. Factions

| Faction | Who | Look | Gameplay role |
|---|---|---|---|
| **SUNDIAL** | A four-person deniable team from the Agency's special activities branch, run by **TALLOW** over HF | Mixed 1980s civilian and military kit: tan field shirts, chest rigs, boonie hats, Seiko dive watches, no rank or flags. **Green chemlights on both shoulder straps after sunset.** | Player plus 3 invulnerable AI squadmates |
| **Harrou Customs Legion** | The private army of **Col. Teodor Harrou**, a stateless ex-customs officer and arms broker who bought a town. Mercenaries from many places, none of them Zaroud. | Faded khaki, tiger stripe and olive, all mismatched. Bright orange armbands that catch the low sun. Berets, boonies, goggles. | Volume enemies: rifleman, gunner, RPG, technical. Aggressive and sloppy. |
| **The Greycoats** | Foreign "advisors" from an **unnamed bloc**, guarding the buyers and the station | Grey-green fatigues with no insignia, armor vests, helmets with goggles. One squad has early night-vision goggles with a green lens glow. | Elite enemies: they move in pairs, flank, throw grenades and communicate by squelch clicks. Includes a marksman. |
| **TALLOW / "the Program"** | The handler's own office. LONG SHADOW is TALLOW's sleeper program. The Clockmaker transmitter was built by TALLOW's people, captured by Harrou, and offered to the Greycoats. | Never seen in the field. In the booth: a grey suit, a steel lighter, a pen. | The twist |

---

## 4. Cast

### 4.1 Player and squad

| Name | Callsign | Role | Personality | Look | Voice casting (summary; full table in 4.3) |
|---|---|---|---|---|---|
| **SFC Nathan "Cal" Calder** (player) | SUNDIAL-3 | Point man, ex-Army Special Forces, mid-30s | **Silent during gameplay.** In the chair: exhausted, defensive, with holes in his memory, insisting on facts he can't actually remember. | First-person tan sleeves. A **Seiko dive watch showing the story clock**. Shaved-head variant appears in the reel. | `vo_calder`: hoarse, flat Western Pennsylvania vowels, short sentences, breath between them |
| **Frank "Hollis" Hollister** | SUNDIAL-ACTUAL | Team leader, 46, a veteran of the earlier war's cross-border recon teams | Laconic and paternal. Superstitious about the sun ("Never fight with the sun in your eyes"). Chews sunflower seeds. He recognizes the "P" signature, and it gets him killed. | Boonie hat, cigarette-pack rig, grey stubble | `vo_hollis`: deep gravel, slow West Texas drawl; calm on the radio, clipped only when truly worried |
| **Mira "Sparrow" Castellanos** | SUNDIAL-2 | Case officer, linguist (Sardani and Greyspeak), photographer | Precise, sardonic, curious to a fault. She photographs everything with her Minolta, including every page of the codebook, which is why she's next. Translates barks deadpan. | Ball cap, camera strap, braid | `vo_sparrow`: measured alto with a faint Miami Cuban-American lilt |
| **Walt "Tinker" Duquesne** | SUNDIAL-4 | Demolitions and radio, ex-Navy bomb disposal, carries the **PRC-77** | Chatty, nervous energy, gallows humor. Hums Cajun waltzes while he works. He is the first to hear the numbers. | Radio pack with whip antenna, rolled sleeves | `vo_tinker`: quick Louisiana Cajun tenor that rises when he's excited |

### 4.2 Supporting voices

| Voice | Who | Notes |
|---|---|---|
| **INTERROGATOR / TALLOW** | Arthur Penhallow, the program controller. **One actor, one TTS voice, three processing chains.** | Soft, educated New England voice that never rises. Smokes. Taps a pen in groups of four. In the booth his voice runs through the scrambler (G1). On the briefing tape it goes through the tape chain. After the toggle in the outro it is clean. |
| **MAGPIE** | Lou Briggs, pilot of an unmarked, sand-painted twin-blade utility helicopter | Bored airline-captain Midwestern calm, even under fire |
| **COL. HARROU** | Heard only on the town's horn loudspeakers. Seen once, with his face scratched out, in 8 mm footage. | Velvet baritone in his 60s, precise Continental-educated English with no identifiable national accent |
| **THE CLOCKMAKER** | The numbers station | A flat synthetic female voice reading spliced single digits. Its interval signal is **8 clock ticks plus an original three-note music-box chime**, which is also the score's main motif. |
| **Legion barks** | 2 voices (A/B) | Shouted invented trade pidgin, "Legion cant" (§14.4) |
| **Greycoat barks** | 1 voice plus squelch clicks | Low, clipped invented language, "Greyspeak", spoken over their own radios |

### 4.3 TTS casting table (keys used in `DIALOGUE.json` → `voice_id`)

Fix the voice, settings and seed per key and never change them mid-production (`fal_media.md` §4.2). Put delivery in a leading audio tag (ElevenLabs) or in `style_instructions` (Gemini), never in the spoken text. Prices are from the 2026-10-16 snapshot.

| voice_id | Character(s) | Primary model and voice | Settings / direction | Alternate | Processing chain(s) (§10.1) |
|---|---|---|---|---|---|
| `vo_hollis` | Hollis | `elevenlabs/tts/eleven-v4`, **Brian** (tested: deep, F0 ≈ 134 Hz) | stability 0.45, similarity 0.75, seed 1984; leading tag such as `[low, calm, slow Texas drawl]` | Gemini `Charon` + style text | DRY + RADIO (both baked) |
| `vo_sparrow` | Sparrow | `google/gemini-3.8-flash-tts`, **Kore** | style: "Alto woman in her thirties, measured and precise, dry wit, faint Cuban-American Miami lilt. Never theatrical." | EL v4 `Sarah` | DRY + RADIO |
| `vo_tinker` | Tinker | Gemini, **Fenrir** | style: "Quick Louisiana Cajun tenor in his thirties, nervous energy, gallows humor, voice rises when excited." | EL v4 `Chris` | DRY + RADIO |
| `vo_calder` | Calder (booth only) | Gemini, **Algenib** (tested "gravelly"; keep its breath pre-roll) | style: "Exhausted, sedated man in his mid-thirties strapped to a chair. Hoarse, flat Western Pennsylvania vowels, short sentences, audible breath between them." | EL v4 `Eric` | BOOTH (dry plus a small concrete-room slap) |
| `vo_tallow` | Interrogator, TALLOW tape, TALLOW clean | EL v4, **Callum** | stability 0.6, similarity 0.8, seed 640; `[soft, unhurried]`. **Record each line once and derive every chain from the same take.** | `fal-ai/minimax/voice-design` ($3): "soft-spoken, educated New England man in his late fifties, unhurried, faint smoker's grain", then `minimax/speech-2.8-hd` | SCRAMBLER (booth), TAPE (briefing), CLEAN (outro `D090`/`D091`) |
| `vo_magpie` | Magpie | EL v4, **Roger** | stability 0.5, seed 212; `[calm, bored, matter-of-fact]` | Gemini `Schedar` | RADIO, or INTERCOM with a rotor bed baked in 18 dB under |
| `vo_harrou` | Harrou (PA) | Gemini, **Algieba** | style: "Velvet baritone in his sixties, precise and unhurried, Continental-educated English with no identifiable national accent, amused contempt." | EL v4 `George` | PA HORN |
| `vo_clockmaker` | The Clockmaker | Gemini, **Erinome** | style: "Flat, emotionless, evenly spaced, like a machine reading digits." Generate **each token separately** (zero to nine, "Attention", "Sundial", "Two", "Three"), 3 takes each, and splice at a 0.9 s grid. | EL v4 `Alice` | HF/AM (radio), ROOM SPEAKER (basement), CLEAN (`D080`) |
| `vo_legion` | Legion barks, voices A and B | A: EL v4 **George** `[shouting]`, seed 31. B: EL v4 **Daniel** `[shouting]`, seed 32. stability 0.3. | Phonetic text, no `language_code` | Gemini `Orus` / `Charon` with "shouting in an unfamiliar language; pronounce every word exactly as spelled" | DRY near + baked `_far` |
| `vo_greycoat` | Greycoat barks | Gemini, **Iapetus** | style: "Low, clipped, disciplined soldier speaking quietly into a radio in an unfamiliar language; pronounce every word exactly as spelled." | EL v4 `Daniel` (pitched −1 st) | GREYCOAT SQUELCH |

Notes:
- Before bulk generation, a human listens to the test outputs and to one take per key. Reject any bark that resembles real speech (`fal_media.md` §8).
- Gemini voice genders and timbres are as listed in the catalog. Confirm each one on the first take.
- The estimated VO cost is under $3 for 157 lines with 3 takes each.

---

## 5. Story and interrogation framing

### 5.1 Now: the chair
**2 November 1984, 03:12, underground, location withheld. "SESSION 12 // SUBJECT S-3".**
- Calder is strapped into a steel chair: leather wrist cuffs, electrodes taped to his forearms.
- In front of him is a cart of **six mismatched CRTs**, and beyond them a one-way mirror that shows only his silhouette. Above the mirror, a wall clock is stopped at **6:40**.
- The interrogator stays behind the player's left shoulder: a voice, cigarette smoke, a pen tapping in groups of four.
- On a small trolley to the left sits a grey **voice-scrambler box** with a toggle and a VU needle that moves with his voice (G1).
- He wants one thing: *where is the codebook, and who else read it?*

### 5.2 Then: the mission as briefed
TALLOW briefed SUNDIAL to recover three items from the Zaroud Relay Station. Harrou had captured the Clockmaker numbers station and was about to sell its codebook to the Greycoats. The book supposedly lists a foreign network's agents, and the station's nightly broadcast starts at sunset, **18:40**: "You'll be gone before the Clockmaker opens his mouth."

### 5.3 The truth, revealed in order
1. **The ledgers (Beat 7):** Harrou's arms shipments were countersigned "P" (Penhallow, TALLOW himself). Hollis recognizes the signature.
2. **The S-3 reels (Beat 8):** 16 mm film of *Calder himself*, months earlier, strapped into this same chair under this same stopped clock: "S-3 / SESSION 11 / 03-84".
3. **The codebook (Beat 9):** its last page is a roster of LONG SHADOW sleepers with four-digit activation groups: `SUNDIAL-3 / CALDER, N. / 0640`. Group **74410** is the program's name.
4. **The numbers on the squad net** at sunset (Beat 5), and again on the helicopter *after Calder has destroyed the Zaroud transmitter*, are not coming from Zaroud. TALLOW is activating his asset.
5. **In the helicopter** the activated Calder raises his pistol at Hollis, the one man who knew the signature. The scene cuts to white before the shot.
6. **In the chair** Calder says "RPG." The interrogator says the crew reports that the RPG missed. He reads 0-6-4-0, Calder's fists unclench, and Calder gives up Sparrow. The interrogator flips the scrambler off and speaks clean: it is the briefing-tape voice. He lifts the phone: "Magpie, Tallow. Prep the aircraft. One more pickup."

### 5.4 Clue trail (planted, then paid off)
| Clue | Where it is planted | Payoff |
|---|---|---|
| 6:40 everywhere: stopped wall clock, clock tower, sunset 18:40, title card flicker, roster code 0640 | Intro, Beats 1/4/5 | Outro: the interrogator reads 0640 |
| Pen taps in fours, one tap per digit | Intro shot 6 | Outro shot 5: one tap per digit |
| The interrogator's cadence matches the tape (same take family, different chain) | Intro `D001` vs `D002` | Scrambler toggled off (`D090`); the phone call (`D091`) |
| "P" countersignature | Beat 7 `D050`, optional `D058` | Debrief dossier |
| "Tallow net" on the broadcast schedule (optional) | Beat 7 `D052` | The numbers continue after the transmitter dies (`D079`) |
| Hollis is the only one who doesn't watch the reel | Beat 8 staging | He is the one Calder is activated against |

### 5.5 How the chair intrudes: the glitch-insert system
Inserts are labelled **I1–I4** (not to be confused with the grafts G1–G13 in §0.3).

The booth is an **additive scene that is always loaded** (`S0_Booth`, §6.7), with its own camera, light and volume. Each insert:
1. sets `Time.timeScale = 0`, pauses gameplay audio (AudioListener.pause, except the insert bus) and suspends input;
2. switches to the booth camera for N frames;
3. runs a full-screen scanline tear plus RGB split (the `GlitchFeature` renderer feature, 0.15 s in and out);
4. plays `STG_GLITCH` and a VO line, then returns.

| Insert | When | Length | Booth shot | Line |
|---|---|---|---|---|
| I1 | Beat 2, 1 s after both sentries fall, only if the sync was clean | 0.9 s freeze, VO continues over gameplay | The pen on the steel table, mid-tap | `D017` |
| I2 | MID-A, on "Six. Four—" at the end of `D039` | 6 frames | The stopped clock | `D042`, which starts on "Calder?" inside `D041` and overlaps it |
| I3 | MID-B, shot 7 | 3 s | The projected room becomes the real room | `D054` |
| I4 | Exfil climax | a cascade of 4 inserts (6, 5, 4 and 8 frames) | The clock, the pen, CRT static, Calder's own eye | none (cut to white) |
| Death insert | Every player death | 2.0 s | POV from the chair with the static monitors | `DI_01`–`DI_06`, random without repeats |

---

## 6. World coordinates and level layout

### 6.1 Coordinate system
- **Units:** 1 Unity unit = 1 m. **Y is up**, **+X is east** and **+Z is north**. Unity heading 0° faces +Z (north) and 90° faces +X (east).
- **Origin (0, 0, 0)** is the south-west corner of the 420 × 420 m playable tile. **Y = 0 is the town datum**: the Clock Square paving, the market street and the Relay Station yard. The ridge top is at +22, the grove at 0, and the pitch (LZ) at −1.
- Every position in this document is `(x, y, z)` in world metres. Headings are compass degrees.
- The booth (`S0_Booth`) and the helicopter cabin use their own local frames, defined in §8.0. The booth scene root sits at world (−600, 0, −600), far outside the map, so its lighting never mixes with the town's.
- The sun sits at **azimuth 258°** (WSW). The critical path runs SW → NE, which keeps the sun behind the player and in the enemies' eyes (Hollis's rule). Long shadows point at the objective.

### 6.2 Map A: the whole level (1 char = 5 m east–west, 1 row = 10 m north–south)

```
  z(m)   0         50        100       150       200       250       300       350       400  x(m) ->
        +|---------|---------|---------|---------|---------|---------|---------|---------|---+
        |                                                <--(11)       +++++++++++++++  ~~~~~|
        |                                                              +(10)   @     +  ~~~~~|
   400  |                                                              ++++bbbb+++++++rrrrrrr|
        |                                                            #########p#########~r~~~|
        |                                                            #F               F#~r~~~|
        |                                                            #              M  #~r~~~|
        |                                     :::::::::::::::::::::::#  HHHHHHHH       #~r~~~|
   350  |                        ::::::::::::::::::::::::::::::::::::#  HH(7-9)H       #~r~~~|
        |                        ::::::::::::::::::::::============::#  HHHHHHHH       #~r~~~|
        |                        :::::::::::::ZZZZZZZZZ=====(5)====  X                 #~r~~~|
        |                        ::::::::::: :ZZZZZZZZZ============  #  (6)            #~r~~~|
        |                        :::::::::::  dZZmmmmmZ=L==========  #             g   #~r~~~|
   300  |                        ::::::::::: ..............::::::::::#F######GG#######F# r   |
        |                        ::::::::::: ..(4).....t...::::::::::                    r   |
        |                        ::::::::::: .......TT.....                              r   |
        |                        ::::::::::: .....o........::::::::::                    r   |
        |                        ::::::::::: ..............::::::::::                    r   |
   250  |                        :::::::::::   ::::::::::::::::::::::                    r   |
        |                        :::::::::     ::::::::::::::::::::::                    r   |
        |                        :::::::::-::  ::::::::::::::::::::::                    r   |
        |                        :::::::::-::xx::::::::::::::::::::::                    r   |
        |                        :::::::::-::  <(3)::::::::::::::::::                    r   |
   200  |                        :::::::::     ::::::::::::::::::::::                    r   |
        |                        ::::::::::::  ::::::::::::::::::::::                    r   |
        |                        ::::::::::::  ::::::::::::::::::::::                    r   |
        |                        :::::::::::CCC::::::::::::::::::::::                    r   |
        |                                  h   k                                         r   |
   150  |                    ,,,,,,,,,,,,,,,                                             r   |
        |                    ,,,,,,,,,PP,,,,                                             r   |
        |                    ,,,,,,,,,,,,,,,                                             r   |
        |                    ,,,,,,,,______,                                             r   |
        |                    ,,(2),,,,,,,,,,                                             r   |
   100  |                    ,,,,,,,,,,,,,,,                                                 |
        |                   ////                                                             |
        |                 ////                                                               |
        |               ////                                                                 |
        |^^^^^^^^^^^^^^///                                                                   |
    50  |^^^^^^^^^^^^^^^^^                                                                   |
        |^^^^^^(1)^^^^^^^^                                                                   |
        |^^^^^^^^^^^^^^^^^                                                                   |
        |^^^^^^^^^^^^^^^^^                                                                   |
        |^^^^^^^^^^^^^^^^^                                                                   |
        +|---------|---------|---------|---------|---------|---------|---------|---------|---+
     0   0         50        100       150       200       250       300       350       400
```
**Legend:**
- **Terrain and town:** `^` the Shoulder ridge (+22 m) · `/` wadi · `,` grove · `_` irrigation wall · `P` Pump House Six · `C` Weighbridge Gate arch · `k` Legion truck · `h` guard hut · `:` dense housing (out of bounds; façades and roofs block sight) · `-` Tinsmiths' Lane · `x` the Crossing.
- **Square and cinema:** `.` Clock Square (with its east street through the housing at z 272–281) · `T` clock tower · `o` dry fountain · `t` technical (DShK) · `Z` Cinema Zenith · `m` marquee walkway · `d` cinema side door · `L` ladder to the roofs · `=` Laundry Roofs.
- **Relay Station:** `#` compound wall (4 m, razor wire) · `X` breach point · `G` gatehouse · `H` Relay Hall · `M` 90 m mast · `F` floodlight towers · `g` generator shed · `p` north postern.
- **Exfil:** `r` Customs Road and salt-pan track · `+` the Pitch (LZ) · `b` bleacher · `@` landing pad · `~` salt pan (out of bounds).
- **Beats:** `(n)` beat number · `<--(11)` exfil flight leaving west.

**Critical path:**
1. (1) ridge SW, then the wadi NE
2. (2) through the grove to the Weighbridge Gate
3. (3) north up the Long Market
4. (4) Clock Square, then the side alley and the cinema
5. Marquee, then the ladder L
6. (5) east across the Laundry Roofs
7. Down into the alley along the station's west wall, then breach X
8. (6) yard, then the Hall door
9. (7) Hall, then up to (8) the screening room, then down to (9) the basement
10. Cable tunnel north, then the postern p
11. (10) the Pitch
12. (11) flight west over the station, roofs, square, market and grove toward the ridge

### 6.3 Map B: Relay Station compound and the Pitch (1 char = 2 m east–west, 1 row = 4 m north–south)

```
  z(m)          300       320       340       360       380  x(m) ->
        +-------|---------|---------|---------|---------|---------+
        |     ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~rrr~~~~~~~~~~~~~~~~~|
        |             +++++++++++++++++++++++++++++++++++       ~~|
        |             +                                 +       ~~|
        |             +                  @              +       ~~|
   406  |             Y                                 Y       ~~|
        |             +                                 +       ~~|
        |             +       bbbbbbbbbb        qqq     +rrrrrrrrr|
        |             ++++++++bbbbbbbbbb++++++++qqq++++++       ~~|
        |                                                       ~~|
   386  |                                                       ~~|
        |       #####################:#p######################  ~~|
        |       #  F                 :                     F #  ~~|
        |       #                    :                       #  ~~|
        |       #                    :              ......   #  ~~|
   366  |       #                    :              .  M .   #  ~~|
        |       #       HHHHHHHHHHHHHHHHHHHH        .    .   #  ~~|
        |       #       H              H o H        ......   #  ~~|
        |       #       H              H   H                 #  ~~|
        |       #       H              HHHHH                 #  ~~|
   346  |       #       H ttttttttttt      H                 #  ~~|
        |       #       H                  H                 #  ~~|
        |       #       H                  H                 #  ~~|
        |       #       HHHHHHHHHHDDHHHHHHHH                 #  ~~|
        |       >                   3                        #  ~~|
   326  |       X                              c             #  ~~|
        |       #1              c                            #  ~~|
        |       #                2                           #  ~~|
        |   RRR #            c           u                   #  ~~|
        |   RRR #                                gggggg      #  ~~|
   306  |       #       c                        gggggg      #  ~~|
        |       #                                gggggg      #  ~~|
        |       #  F                GGGGGG                 F #    |
        |       ####################GGGGGG####################    |
        |                                                         |
        +-------|---------|---------|---------|---------|---------+
   286          300       320       340       360       380
```
**Legend:** `R` STAND CLEAR ring (in the alley outside the west wall) · `>` breach hole (6 m) · `1 2 3` squad node sets Y1/Y2/Y3 · `c` sandbag and cable-drum cover · `u` fuel bowser · `t` teletype rows (inside the Hall) · `o` Harrou's desk · `DD` Hall double door · `.` mast fence · `:` cable tunnel (underground) · `q` groundsman's shed · `Y` goalposts.

### 6.4 Map C: Relay Hall floors (1 char = 1 m east–west, 1 row = 2 m north–south)

```
C1  RELAY HALL GROUND FLOOR (floor Y = +0.3, ceiling 4.2 m)
  z(m)         320       330       340       350  x(m) ->
        +------|---------|---------|---------|-------+
        |                                            |
        |  ####ccccccccccccccccccccccccc###########  |
   360  |  #                SSS          #  k     #  |
        |  #        f       SSS  f       #        #  |
        |  #                             #    L   #  |
        |  #                             #        #  |
   352  |  #      ttttttttttttttttttt    d        #  |
        |  #                             #        #  |
        |  #                             ##########  |
        |  #      ttttttttttttttttttt    ##########  |
   344  |  #                             #        #  |
        |  #                             d        #  |
        |  #wwww  ttttttttttttttttttt    # eeee   #  |
        |  #wwww                         # eeee   #  |
   336  |  #wwww                         # eeee   #  |
        |  #                             #        #  |
        |  ####################DDDD################  |
        |                                            |
        +------|---------|---------|---------|-------+
   328         320       330       340       350

C2  UPPER FLOOR (floor Y = +4.8): screening room + landing; v = open void over the switch hall
  z(m)         320       330       340       350  x(m) ->
        +------|---------|---------|---------|-------+
        |                                            |
        |  ..............          ................  |
   360  |  . ##========##                         .  |
        |  . #nnnnnnnnnn#                         .  |
        |  . #nnnnnnnnnn#                         .  |
        |  . #    P     # h                       .  |
   352  |  . ############                         .  |
        |  .                                      .  |
        |  .                                      .  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
   344  |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
   336  |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |  vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv  |
        |                                            |
        +------|---------|---------|---------|-------+
   328         320       330       340       350

C3  BASEMENT B1 (floor Y = -4.2, ceiling 3.6 m) + cable tunnel to the postern
  z(m)         320       330       340       350  x(m) ->
        +------|---------|---------|---------|-------+
   384  |                                            |
        |                            :U:             |
        |                            :::             |
        |                            :::             |
   376  |                            :::             |
        |                            :::             |
        |                            :::             |
        |                            :::             |
   368  |                            :::             |
        |                            :::             |
        |                            :::             |
        |                            :::             |
   360  |    ##########################o#            |
        |    #   TTTTTTTT RRTTTTTTTT    #            |
        |    #                          #            |
        |    #                          #            |
   352  |    #                    c     #            |
        |    #     c                    #            |
        |    #         kkkkkk           #            |
        |    #www                       #            |
   344  |    #www                c      V            |
        |    #www                       #            |
        |    #        c              eee#            |
        |    #                       eee#            |
   336  |    ##############h#############            |
        |                                            |
        |                                            |
        |                                            |
        +------|---------|---------|---------|-------+
   328         320       330       340       350
```
**Legend:**
- **C1 (ground floor):** `#` walls · `DDDD` double door · `t` teletype rows (1.1 m cover) · `c` world-time clock wall · `S` spiral stair up · `w` west stair down · `e` east stair down (archive room) · `d` doors · `L` ledgers on Harrou's desk · `k` typewriter desk · `f` ceiling fans.
- **C2 (upper floor):** `=` screen · `n` 20 velvet seats · `P` 16 mm projector · `r` film-can rack · `h` Hollis's door post · `v` void.
- **C3 (basement):** `T` vacuum-tube transmitter racks R1–R4 · `RR` tape-loop deck and speaker · `V` vault safe · `h` cable-shaft hatch · `k` receiver desk · `c` crates · `o` tunnel door · `:` cable tunnel · `U` ladder up to the postern.

### 6.5 Named areas

| # | Area | Extent (x, z) | Y | Size | Beats | Notes |
|---|---|---|---|---|---|---|
| A1 | The Shoulder | x 0–85, z 0–60 | +18 … +24 | 85 × 60 m | 1 | Basalt shoulder, spawn prone at (42, 22, 30); the whole route is visible from here |
| A2 | Wadi | (72, 52) → (110, 92), 6–10 m wide | +16 → 0 | 60 m | 1 | Scree slide chute (82, 13, 62)–(90, 8, 72); boulder mantle (98, 4, 82) |
| A3 | Pump House Six grove | x 100–176, z 90–150 | 0 | 76 × 60 m | 2 | About 24 olive and pomegranate trees; mudbrick irrigation wall x 140–170, z 109–110, 1.2 m high (cover for node set A), gaps at x 148 and x 162; pump house 11 × 8 m centred at (150, 0, 132) |
| A4 | Weighbridge Gate | arch x 176–191, z 160–170 | 0 | 15 m arch; 12 m scale plate z 145–157 | 2 | Truck (190, 0, 150), guard hut (172, 0, 152), barrier boom (182, 0, 144) |
| A5 | Long Market | street x 181–189, z 160–252 | 0 | 92 × 8 m | 3 | Crossing z 208–216; Tinsmiths' Lane x 166–171, z 195–240; cross-passages at z 195–200 (E–W) and z 236–241. West-side houses are 2 storeys (≤ 7 m), east-side houses 3 storeys (9–11 m). **Sunset Lane:** the z 236 passage continues west as a straight 5 m lane at azimuth 258° from (181, 0, 238) to the town edge (120, 0, 225), with nothing built in its line beyond x 120, so the low sun drops one physically correct bright band across the street floor at z ≈ 236 (§7.3). |
| A6 | Side alley | x 174–180, z 241–300, widening to a service yard x 174–186, z 300–318 | 0 | 77 × 6 m | 4 | Market west arcade gap (181, 0, 244) leads north to the service yard and the cinema side door (186, 0, 307) in the cinema's west façade |
| A7 | Clock Square | x 182–252, z 252–300 | 0 | 70 × 48 m | 4 | Tower 6 × 6 m, 22 m tall, at (220, 0, 275); fountain (205, 0, 266); technical (232, 0, 286); east street mouth (252, 0, 276) |
| A8 | Cinema Zenith | x 186–231, z 300–330 | 0 / +4.5 | 45 × 30 m | 4 | Lobby x 186–198, z 302–312; closed concession hall x 198–231, z 300–312 (ground, not enterable); auditorium x 186–231, z 312–330; balcony +4.5 at z 312–318; **upper foyer** x 198–231, z 302–312 at +4.5 connects the balcony to the marquee door (214, 4.5, 301); marquee walkway x 200–226, z 299–301 at +4.5. A crate-and-awning stack at the walkway's east end (227, 0, 298), top +2.2 m, gives a two-step drop to the square. |
| A9 | Laundry Roofs | x 232–292, z 304–336 | +8 … +12 | 60 × 32 m | 5 | R1 x 232–246 (+9), R2 248–261 (+8), R3 263–275 (+10), **R4 277–292 (+12, four storeys, the highest roof east of the cinema so it keeps the last sun, §8.5)**; 2 m gaps. Links: R1→R2 plank (z 318, 1 m down), R2→R3 water-tank stand + plank (stand top +9 at (260, 8, 324), plank to R3 at z 324), R3→R4 short steel stair (z 320, 2 m up). R4 has a pipe railing (no solid parapet) on its west and south edges and a 0.9 m solid parapet on its east edge only. |
| A10 | West-wall alley | x 293–299, z 300–340 | 0 | 40 × 6 m | 6 | External switchback stair down the R4 house from (291, 12, 330) to (295, 0, 334); STAND CLEAR ring (296, 0, 310) r 2.5 m, 12.6 m from the charge |
| A11 | Relay Yard | x 300–392, z 290–382, minus the Hall | 0 | 92 × 92 m | 6 | Gatehouse x 340–352, z 290–298; generator shed x 366–378, z 300–310; mast fence x 372–384, z 356–368 |
| A12 | Relay Hall ground floor | x 316–356, z 330–362 | +0.3 | 40 × 32 m | 7 | Switch hall x 316–346; Harrou's office x 346–356, z 346–362; archive x 346–356, z 330–346 |
| A13 | Screening room | x 318–330, z 350–360 | +4.8 | 12 × 10 m | 8 | Landing x 330–340, z 350–362 with the spiral stair at (334.5, 4.8, 358) |
| A14 | Basement B1 | x 318–346, z 334–360 | −4.2 | 28 × 26 m | 9 | West stair foot (320.5, −4.2, 343); east stair foot (343.5, −4.2, 338.5); hatch (332, −4.2, 334.3) |
| A15 | Cable tunnel | x 342–345, z 360–381 | −4.2 | 21 × 3 m | 9→10 | Ladder up to the postern (346, 0, 382) |
| A16 | The Pitch (LZ) | x 312–382, z 392–418 | −1.0 | 70 × 26 m | 10 | Bleacher x 328–348, z 392–397; shed x 364–370, z 392–398; pad (350, −1, 406) |
| A17 | Exfil corridor | spline N0–N6 (§7.11) | 0 … +80 | about 520 m | 11 | Over-flight of A11 → A9 → A7 → A5; the white-out (≈ t 42 s) comes before N5, so A3 and A1 are never seen from the air and need only their far proxies |
| — | Backdrop | outside the tile | — | — | all | Far-LOD town proxy (`S9_TownFar`), western ridge silhouette mesh at 2.5 km (it hides the sun disc during MID-A), salt pan to the N and E, distant hills |

### 6.6 Building list

| ID | Building | Footprint / height | Floors | Built from | Notes |
|---|---|---|---|---|---|
| BLD-01 | Pump House Six | 11 × 8 m, 5 m | 1 | Kit: plaster walls, corrugated roof | Diesel pump (fal), sentry bench, cassette radio |
| BLD-02 | Weighbridge arch and guard hut | 15 m arch; hut 4 × 3 m | — | Kit: sandstone arch (Poly Haven `sandstone_blocks_05`) | Truck-scale plate (`metal_plate`), barrier boom, chained iron gate (`large_iron_gate`) |
| BLD-03 | Market houses (both sides) | 6–8 m deep, 7–11 m tall | 2–3 | 3 m-grid house kit | Arcades, timber balconies, awnings (wind shader, swap meshes for the torn state), shutters, cables strung across the street |
| BLD-04 | Café | 8 × 6 m arcade bay | 1 | Kit | Chairs, a half-eaten meal, cassette player |
| BLD-05 | Clock tower | 6 × 6 m, 22 m | — | Kit: sandstone plus 4 clock faces at 6:40 | Non-religious civic tower: square, flat-topped, clock stage |
| BLD-06 | Square arcades | 3 sides, 4 m deep | 2 | Kit (colonial arches, never pointed or religious) | Shops shuttered |
| BLD-07 | Cinema Zenith | 45 × 30 m, 12 m | 1 + balcony | Hero: façade, marquee with invented-script film titles, lobby (terrazzo), auditorium (instanced seats, shrapnel-hole dust shafts, flickering EXIT sign), balcony stair | Ladder L on the R1 building's south face (236, 0, 301) |
| BLD-08 | Laundry Roofs houses | 4 buildings | 2–3 (R4: 4) | Kit roofs: parapets, water tanks, TV antennas, laundry lines, pigeon loft; planks, tank stand and steel stair per A9; R4 external switchback stair | Rooftop MG nest: sandbag ring around an empty tripod on R3 |
| BLD-09 | Relay Station wall and gatehouse | 92 × 92 m wall, 4 m, razor wire; gatehouse 12 × 8 m, 6 m | 1 + roof | Kit: concrete (`concrete_wall_007`) | Breach section with intact and blown variants |
| BLD-10 | Relay Hall | 40 × 32 m, 9 m | 2 + basement | Hero Art Deco: painted company name, stepped parapet, steel double door | Exterior shell and roof live in `S5_StationExt` (seen from the yard, the roofs and the flight); interiors per Map C live in `S6_StationInt` |
| BLD-11 | Generator shed | 12 × 10 m, 4 m | 1 | Kit (corrugated) | Poly Haven `portable_generator` ×2, cable drums |
| BLD-12 | Mast | 4 m base, 90 m, guyed | — | Procedural lattice mesh; guy wires as line renderers | Red aviation lights blink at 1 Hz after sunset; maintenance platform at +25 |
| BLD-13 | Floodlight towers ×4 | 8 m | — | Kit (steel) + Poly Haven `security_light` heads | Destructible: spark burst, buzzing die-off |
| BLD-14 | Groundsman's shed and bleacher | 6 × 6 m; 20 × 5 m | — | Kit | Rusting goalposts, salt crust |
| BLD-15 | Dense housing blockers | the `:` areas | 2–3 | Kit facades, roof LODs only | Never enterable. Roofs carry an OOB volume (§17) wherever the player could reach them. Roof LOD must hold up at 25–60 m for the flight (the Beat 11 targets stand on these roofs). |
| BLD-16 | Interrogation booth | 4.8 × 4.6 m, 3 m | 1 | Hero interior: acoustic-tile walls (`PaintedPlaster012` olive), concrete floor | §8.0 |

### 6.7 Streaming sections and bundles
Follow `webgl.md` §6: Addressables groups as LZ4 bundles of at most 12 MB each, served from StreamingAssets and named `*.bundle`, with Crunch on all textures. Each section is an **additive scene with its own lightmaps** baked at its LightStage (§9.4). `S_Boot` is the only scene in Build Settings; `S_Persistent` is an Addressables scene loaded at boot (TECH_PLAN D1).

**Loads happen only at named quiet points.** Bundle decompression and Crunch transcoding run on the main thread on WebGL (no threads), so a 12 MB section load can stall a frame for a few hundred milliseconds. Every load request below fires at a moment with no combat; unloads fire at beat starts. If a section is still loading when the player reaches its first trigger, the beat script holds the trigger (the squad waits at its node set, no hard stop is visible) until `ISectionStreamer.IsLoaded`.

| Scene | Contents | Load request at (quiet point) | Unloaded at |
|---|---|---|---|
| `S_Persistent` (Addressable, D1) | Systems, player rig, HUD, glitch feature, squad prefabs, lights, global volumes | boot | never |
| `S0_Booth` | Interrogation booth | boot (before the click gate is enabled) | never (glitch and death inserts need it) |
| `S9_TownFar` | Far-LOD roofs and facades for the whole tile including a decimated far mesh of the Shoulder, wadi and grove terrain with 2-plane tree impostors, sodium-lamp sprites, the 2.5 km ridge silhouette and far hills | boot, continues behind the click gate and intro | never |
| `S1_Approach` | Ridge, wadi, grove, Weighbridge | boot, continues behind the click gate and intro (needed by intro shot 7, 1:08) | Beat 4 start |
| `S2_Market` | Long Market, Tinsmiths' Lane, Sunset Lane, side alley and service yard | Beat 1 start (title card) | Beat 5 start |
| `S3_Square` | Clock Square, cinema interior and marquee | Beat 2 start (grove entry, before the sync shot) | Beat 6 start (its far proxy stays) |
| `S4_Roofs` | Laundry Roofs, west-wall alley | Beat 2, on `D019` (Weighbridge clear, bolt-cut) | Beat 7 start |
| `S5_StationExt` | Yard, walls, mast, floods, gatehouse, Relay Hall exterior shell and roof | Beat 3, on `D027` (market clear) | after the outro (the outro's gun-camera clip renders it) |
| `S6_StationInt` | Hall interiors, screening room, basement, cable tunnel | Beat 4, on `D034` (square clear) | Beat 10 start |
| `S7_Pitch` | Pitch, roads, trucks, salt pan near field | Beat 5, at MID-A start | end |
| `S8_Heli` | Helicopter exterior and cabin, door gun, Beat 11 rooftop targets and trucks | Beat 7, on `D051` | end |

**Far proxies by distance, not by load state:** a far-proxy block of `S9_TownFar` is hidden only while its section is loaded **and** the player is within 90 m of the block (LOD cross-fade over 10 m); beyond 90 m the baked proxy shows even when the section is loaded. This keeps baked long shadows on distant buildings, which lie beyond the 60–80 m realtime shadow distance (TECH_PLAN D7).

**Backtrack blockers:** when a section unloads behind the player, its boundary closes. Beat 4 start: the Weighbridge gate swings shut (scripted, behind the squad) and a PlayerClip spans the market at z 165. Beat 5 start: PlayerClip across the market arch (z 250) and the side alley (z 246). Beat 6 start: the R4 stair top is clipped (no return to the roofs). Beat 7 start: PlayerClip in the breach hole (x 300, z 319–325). Beat 10 start: the postern door closes behind the player. Each blocker sits behind an OOB volume (§17) so the player gets the warning first.

---

## 7. Mission flow, beat by beat

### 7.0 Pacing table

| Beat | Name | Areas | Story clock | LightStage `s` | Play time | Enemies | Peak alive | Checkpoint | Music |
|---|---|---|---|---|---|---|---|---|---|
| — | Click gate, Intro "SESSION 12", title card | Booth → A1 | 03:12 (2 Nov) → 17:58 | booth → 0.00 | 1:43 cine (the 8 s title card overlaps shot 9) | — | — | — | `MUS_01_SESSION`, `STG_TITLE` |
| 1 | THE SHOULDER | A1, A2 | 17:58 | 0.00 → 0.15 | 0:50 | 0 | 0 | CP1 | none (ambience) |
| 2 | PUMP HOUSE SIX | A3, A4 | 18:08 | 0.15 → 0.45 | 1:15 | 6 | 4 | CP2 | `MUS_02_MARKET` LO on first loud shot |
| 3 | THE LONG MARKET | A5 | 18:17 | 0.45 → 1.00 | 1:45 | 9 | 6 | CP3, CP3b | `MUS_02` LO → HI → LO |
| 4 | CLOCK SQUARE | A6, A7, A8 | 18:26 | 1.00 → 1.50 (sky swap inside the cinema) | 1:45 | 8 | 6 | CP4 | `MUS_02` HI, `STG_GREYCOAT` |
| 5 | LAST LIGHT | A9 | 18:34 → **18:40 sunset** | 1.50 → 2.00 | 1:20 (≈ 0:47 is MID-A) | 3 | 3 | CP5 | `MUS_03_LASTLIGHT` |
| 6 | THE RELAY YARD | A10, A11 | 18:41 | 2.00 → 2.40 | 1:20 | 8 | 6 | CP6 | `MUS_04_STATION` |
| 7 | THE RELAY HALL | A12 | 18:50 | 2.40 → 2.60 | 1:00 | 5 | 3 | CP7 | `MUS_04` (low layer) |
| 8 | THE REEL | A13 | 18:54 | 2.60 → 2.80 | 0:50 (0:45 is MID-B) | 0 | 0 | CP8 | none (projector) |
| 9 | THE CLOCKMAKER | A14, A15 | 18:58 | 2.80 → 3.20 | 1:45 | 9 | 5 | CP9 | `MUS_04` |
| 10 | THE PITCH | A16 | 19:05 | 3.20 → 3.70 | 1:15 | 7 | **7** | CP10 | `MUS_05_EXFIL` |
| 11 | MAGPIE | A17 | 19:10 | 3.70 → 4.00 | 0:50 | 4 | 4 | (CP10) | `MUS_05` → hard cut |
| — | Outro "SIX-FORTY" + debrief | Booth | 03:40 (2 Nov) | booth | 1:50 cine + dossier | — | — | — | `MUS_06_SIXFORTY` |
| | **Total** | | | | **≈ 13:55 play** | **59** | **7** | | |

The sun is driven by **beat progress, not the wall clock** (§9.1), so lingering can't freeze golden hour and a speedrunner still sees the sunset on the roofs. The "Story clock" column is the clock at the beat start, read from the story-clock curve in §9.2a (the Seiko on Calder's wrist shows it).

Conventions in the encounter tables:
- **Placed** means the enemy exists when the beat loads. **Closet** means it spawns at a named spawn closet that is out of the player's sight (the spawner raycast-checks the closet door) and at least 15 m from the player.
- Each beat's **max alive** is enforced by the EncounterSpawner. Extra spawns queue.
- Facing is a compass heading.

---

### 7.1 Beat 1: THE SHOULDER (ridge overlook and wadi)

| Field | Spec |
|---|---|
| **Area / coords** | A1 ridge (+22) and A2 wadi from (72, 16, 52) to (110, 0, 92) |
| **Light** | 17:58, sun 8.0° elevation at az 258°, behind the player. Zaroud glows ochre; every building throws a long shadow NE toward the mast. Dust haze past 250 m. |
| **Enter trigger** | Intro shot 9 white-out ends. **CP1** saved at spawn. |
| **Player start** | Prone at (42, 22.0, 30), heading 45°. Look-only during the 8 s title card, then a scripted kneel. Binoculars are in hand (hold RMB to glass; this beat only). |
| **Objectives → markers** | 1. `OBSERVE ZAROUD 0/3`: hollow diamonds on POI1 Relay Station + mast (378, 45, 362), POI2 clock tower (220, 18, 275), POI3 Weighbridge Gate (183, 4, 162). Glassing = binocular view with the POI within 4° of center for 1.0 s, which ticks it. If 40 s pass first, the remaining POIs tick silently and `D011` plays. 2. `FOLLOW HOLLISTER`: FOLLOW chevron over Hollis, from `D011` until the player enters the wadi-midpoint volume. 3. `REACH THE GROVE`: diamond at (108, 0, 96) with distance, from the wadi midpoint until the grove volume. (Full list with ids: §7.12.) |
| **Squad** | Start: Hollis prone at (36, 22, 28) with field glasses. Sparrow kneels at (46, 22, 26) photographing. Tinker at (39, 22, 24) extends, then coils, the PRC-77 whip. After `D011` they move in file (Hollis, Sparrow, player, Tinker, 4 m spacing) to the wadi head (70, 18, 50), then down the wadi. |
| **Encounter** | None |
| **Scripted / tutorial** | Prompts, each shown once: `HOLD RMB: BINOCULARS` · `TAB: OBJECTIVES` (after `D011`) · scree chute (82, 13, 62)–(90, 8, 72): `SPRINT + C: SLIDE` · boulder (98, 4, 82): `SPACE: MANTLE` · grove edge (`B1.T.GroveEdge`): `C: CROUCH`. Harrou's PA plays from all three horn emitters at (185, 8, 200), (220, 20, 275) and (346, 6, 294) at once, with baked slapback, logarithmic rolloff (min 25 m, max 500 m) and an always-on translated subtitle. The curfew siren winds down over the first 15 s. |
| **Radio / VO** | `D007` on control → `D008` (POI1), `D009` (POI2), `D010` (POI3) → `D011` (all 3 POIs, or 40 s) → `D012` PA (wadi midpoint volume, center (88, 10, 70), r 6 m) → `D013` |
| **Music / ambience** | `MUS_01` tail fades out by 0:10. Bed: dry wind, cicadas, distant dog, siren tail, PA echoes. |
| **Exit trigger** | Player enters the grove volume x 100–120, z 88–104 → Beat 2 |

### 7.2 Beat 2: PUMP HOUSE SIX (grove and Weighbridge Gate)

| Field | Spec |
|---|---|
| **Area / coords** | A3 grove (x 100–176, z 90–150) and A4 Weighbridge (arch center (183, 0, 165)) |
| **Light** | 18:08, sun 7.5° → 6°. Sun shafts slant through the canopy (shaft cards plus dust motes). Orange armbands flare. |
| **Enter trigger** | Grove volume. **CP2** at (108, 0, 96). |
| **Objectives → markers** | 1. `ELIMINATE THE SENTRIES`: first a `FIRING POSITION` diamond at (136, 0, 112). Within 3 m of it, red X markers appear on E2-01 and E2-02. 2. `CLEAR THE WEIGHBRIDGE`: diamond on the truck (190, 3, 150). 3. `ENTER ZAROUD`: diamond on the arch (183, 2, 166). |
| **Squad** | Hollis crouch-walks to (139, 0, 113) and takes the right sentry on "Mark". Sparrow and Tinker hold at (130, 0, 108). For the gate fight, node set A is behind the irrigation wall: Hollis (152, 0, 108), Sparrow (158, 0, 108), Tinker (164, 0, 108). Set B, once the truck gunner is dead: Hollis (170, 0, 146) at the guard hut, Sparrow (176, 0, 144), Tinker (180, 0, 140). |
| **Scripted** | **Silent opener.** The sentries smoke and chat (`BK_LEG_14` / `BK_LEG_15` every 15 s). While unaware they have 30 HP, so one Hush Puppy body hit kills. At the firing position the prompt `2: SUPPRESSED PISTOL` shows once if the Commando is drawn. `D015` "Mark" fires when the player is at the firing position AND (ADS on the left sentry for ≥ 1.5 s OR 12 s have passed at the position). Hollis kills the right sentry on "Mark" (a `ScriptedKill`, always lethal). **Clean** = both sentries dead within 1.0 s after "Mark" AND no unsuppressed player shot from grove entry until mark + 1.0 s. **Failure branch (no fail state):** if the player fires before the mark, or the left sentry is alive 0.5 s after it, `D016` plays, the left sentry runs north shouting `BK_LEG_01`, and the gate team starts alerted on grove-facing cover nodes along z 140. If both died in time but an unsuppressed shot was fired, there is no `D016` but the gate team starts alerted. **Clean branch:** glitch insert I1 + `D017`; the gate team starts unaware (idle, facing south). **Both branches, after the gate fight:** `D019` → `D020` while Tinker bolt-cuts the chained gate (3 s animation at (183, 0, 163)), then the gate creaks open on the Long Market. Prompts: `G: FRAG (HOLD TO COOK)` on first sight of the truck, `RMB: AIM`. |
| **Gate fight trigger** | Player crosses z > 136, OR the alert branch fires, OR 25 s after the sync shot |
| **Radio / VO** | `D014` (grove entry) → `D015` → [`D016`] → `D017` → `BK_LEG_01`/`BK_LEG_11` (gate alerted) → `D018` (gunner opens fire) → `D019` (all dead) → `D020` |
| **Music** | `MUS_02_MARKET` LO layer starts on the first unsuppressed shot |
| **Exit trigger** | The gate opens and the player passes z > 168 → Beat 3 |

**Encounter E2: 6 total, max alive 4**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E2-01 | Legion Rifleman (sentry L) | (146, 0, 125) | 200° | Placed | Smoking idle. 30 HP while unaware. |
| E2-02 | Legion Rifleman (sentry R) | (154, 0, 126) | 160° | Placed | Hollis's target |
| E2-03 | Legion Rifleman | (171, 0, 150) guard hut | 190° | Placed | Gate fight; cassette radio plays in the hut |
| E2-04 | Legion Rifleman | (186, 0, 146) barrier blocks | 200° | Placed | Gate fight |
| E2-05 | Legion Rifleman | (180, 0, 158) arch | 180° | Placed | Gate fight; falls back to the arch if pushed (`BK_LEG_09`) |
| E2-06 | Legion Gunner (RPK on the truck bed) | (190, 2.2, 150) | 200° | Placed (perch) | Fires long bursts with green tracers. Frag in the bed = kill. His RPK stays on its bed mount (no pickup; the first RPK pickup is E3-05's). |

### 7.3 Beat 3: THE LONG MARKET

| Field | Spec |
|---|---|
| **Area / coords** | A5: street x 181–189, z 160–252. Crossing z 208–216. Tinsmiths' Lane x 166–171, z 195–240. Cross-passages at z 195–200 and z 236–241. |
| **Light** | 18:17, sun 6° → 4.5°. With the sun this low in the WSW, **the 2-storey west-side houses put the whole street floor in blue shade while the upper storeys of the 3-storey east-side houses burn gold** (lit above ≈ 6 m). The one bright band on the street floor comes through Sunset Lane at z ≈ 236 (§6.5), so the last third of the fight is shade, glare, shade. The z 195 passage runs E–W and stays in shade. Every sunlit surface must be sunlit in the realtime shadow map: no shaft card or emissive "sun" is placed where the shadow map says shade (checked in LookCapture). |
| **Enter trigger** | Gate open. **CP3** at (185, 0, 170). **CP3b** at the Crossing (185, 0, 216), saved once the player passes it (deferred while enemies are within 10 m). |
| **Objectives → markers** | `PUSH THROUGH THE LONG MARKET`: diamond on the square arch (185, 4, 252) with distance |
| **Squad** | Leapfrog arcade to arcade. **M1:** Hollis at the café (182.5, 0, 180), Sparrow on the east arcade (188, 0, 184), Tinker (182.5, 0, 186). **M2**, when wave A ≤ 2 alive or the player passes z > 200: Hollis (182.5, 0, 201), Sparrow (188, 0, 204), Tinker (183, 0, 206). **M3**, after the Crossing: Hollis (182.5, 0, 224), Sparrow (188, 0, 228), Tinker (183, 0, 232). **M4**, market clear: Hollis (183, 0, 246), Sparrow (187, 0, 247), Tinker (185, 0, 244). |
| **Scripted** | **Torn awnings (G3):** awning panels AW-01 to AW-08 swap to their torn mesh when the balcony gun's tracer path crosses them. AW-01–AW-05 hang from the east-side upper balconies (z 214–230, +4.5 m, above the shadow line): tearing them exposes the sunlit wall behind. AW-06–AW-08 span the street at z 230–240 under the Sunset Lane band: tearing them lets the band's sun shaft (pre-placed card plus dust motes, enabled per panel) reach the floor. 6 orange and pomegranate crates spill physics fruit when shot. At the Crossing, Tinker throws white smoke at (185, 0, 212) on `D024` (it lasts 20 s). A half-collapsed awning forces a mantle at (184, 0, 226). Shutters latch and radios are turned down behind walls when shooting starts (G5). |
| **Radio / VO** | `D021` (entry) → `D022` (balcony gunner) → `D023` (first awning tear) → `D024` (squad at M2) → `D025` PA (player z > 216) → `D026` (wave B exits the cross-passage at z 197) → `D027` (market clear). Legion barks: `BK_LEG_02`, `BK_LEG_03` (wave B), `BK_LEG_12`. |
| **Music** | `MUS_02` LO → HI when wave B spawns → LO on `D027` |
| **Exit trigger** | Squad at M4 and the player within 8 m of the arch → Beat 4 |

**Encounter E3: 9 total, max alive 6**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E3-01 | Legion Rifleman | (183, 0, 192) behind a stall | 180° | Placed | Wave A |
| E3-02 | Legion Rifleman | (187, 0, 196) | 180° | Placed | Wave A |
| E3-03 | Legion Rifleman | (184, 0, 203) | 180° | Placed | Wave A |
| E3-04 | Legion Rifleman | (186.5, 0, 207) | 180° | Placed | Wave A |
| E3-05 | Legion Gunner (RPK) | balcony (190.5, 4.2, 222) | 225° | Closet `SC_B3_BAL` (room behind the balcony) | Player z > 185. Shreds the awnings. Drops an RPK pickup to the street. |
| E3-06 | Legion Rifleman | west roof (178.5, 8, 214) | 135° | Closet `SC_B3_ROOF` | With E3-05 |
| E3-07 | Legion Rifleman | `SC_B3_N` (168, 0, 240) | 180° | Closet | **Wave B:** player z > 216 OR wave A ≤ 2 alive. Path: Tinsmiths' Lane south, then the cross-passage at z 197 into the street, flanking from behind-left. |
| E3-08 | Legion Rifleman | `SC_B3_N` | 180° | Closet | Wave B (+1.0 s) |
| E3-09 | Legion Rifleman | `SC_B3_N` | 180° | Closet | Wave B (+2.0 s) |

### 7.4 Beat 4: CLOCK SQUARE / CINEMA ZENITH

| Field | Spec |
|---|---|
| **Area / coords** | A7 square (x 182–252, z 252–300), A6 side alley (x 174–180), A8 cinema (x 186–231, z 300–330) |
| **Light** | 18:26. The square is mostly in shadow now; only the tower top and the marquee letters are still lit. **Sky swap:** while the player is inside the lobby or auditorium (no sky visible), `s` blends 1.00 → 1.35 over 3 s (sun 4.5° → 3.0°, sky texture golden → sunset weight 0.5, ambient SH lerp). It reaches 1.50 by the end of the beat. |
| **Enter trigger** | Squad at M4. **CP4** at the arch (185, 0, 248). |
| **Objectives → markers** | 1. `FLANK THROUGH THE CINEMA`: diamond on the side door (186, 1.2, 307); completes when the player opens it (`[E] Open`). 2. On entering the auditorium: `NEUTRALIZE THE TECHNICAL`, a **red** diamond on the gunner (232, 2.6, 286), visible through walls. 3. `REGROUP WITH HOLLISTER`: FOLLOW marker on Hollis; completes when every E4 enemy is dead. 4. `UP TOP`: diamond on the ladder (236, 0, 301). |
| **Route** | Arch → west arcade gap (181, 0, 244) → side alley north → service yard → side door (186, 0, 307) → lobby (2 riflemen) → auditorium (near-dark, flickering EXIT sign, dust shafts through shrapnel holes) → balcony stair (226, 0, 314) → balcony (+4.5) → upper foyer → marquee door (214, 4.5, 301) → marquee walkway (x 200–226, +4.5). From the marquee the gunner is 18 m away and about 85° off his facing, outside his gun shield. Back down to the square via the crate stack at the walkway's east end (227, 0, 298) or back through the cinema. |
| **Squad** | **Q1**, pinned at the arch firing blind (suppression only, no kills): Hollis (183, 0, 250), Sparrow (187, 0, 250), Tinker (185, 0, 247). **Q2**, after the gunner dies: Hollis at the fountain (205, 0, 265), Sparrow at the tower SW (216, 0, 270), Tinker at the west arcade (198, 0, 262). They fight the Greycoats with the player, then go to the ladder. |
| **Scripted** | The technical opens fire 2 s after `D028`; its fire at the arch is scripted suppression (it ignores perception range). Its gun shield protects ±60° around its 234° facing. **When E4-01 dies (any cause) the pickup's ruptured fuel tank ignites 1.5 s later** (fireball, then a static fire prop with a flickering point light), so the burning technical can be relied on from the roofs in Beat 5 and from the flight. The sky swap (§9.3) runs when the player enters the lobby volume `B4.T.Lobby`. Ammo crate at the arch (184, 0, 247). When the player appears on the marquee, E4-03 and E4-04 switch target to the player. **Gate:** ladder L is blocked (PlayerClip at its foot) until every E4 enemy is dead. |
| **Radio / VO** | `D028` (arch reveal) → `D029` (technical fires) → [`D030` nag, 45 s] → `D031` (side door opened) → `D032` (gunner dead) → 3 clicks + `BK_GRY_01` → `D033` (Greycoat pair spawns) → `BK_GRY_02` → `D034` (all dead, player within 10 m of the ladder) |
| **Music** | `MUS_02` HI during the technical. `STG_GREYCOAT` on the Greycoat arrival. |
| **Exit trigger** | Player climbs the ladder → Beat 5 |

**Encounter E4: 8 total, max alive 6**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E4-01 | Technical Gunner (DShK) | (232, 1.6, 286) | 234° | Placed | Fires 3-round bursts at the arch. Immune from the front. |
| E4-02 | Legion Rifleman | (226, 0, 266) tower east | 220° | Placed | Engages the arch |
| E4-03 | Legion Rifleman | (240, 0, 272) east arcade | 240° | Placed | Retargets to the player once he is on the marquee |
| E4-04 | Legion Rifleman | (206, 0, 290) north arcade, below the marquee | 200° | Placed | Retargets to the player once he is on the marquee |
| E4-05 | Legion Rifleman | lobby (192, 0, 304) | 270° | Placed | Ambush; holds until the side door opens |
| E4-06 | Legion Rifleman | lobby (196, 0, 309) | 270° | Placed | Ambush |
| E4-07 | Greycoat Advisor | `SC_B4_E` (252, 0, 276) | 270° | Closet | Gunner dead + 4 s. Flanks along the north arcade to (240, 0, 292). |
| E4-08 | Greycoat Advisor | `SC_B4_E` | 270° | Closet | Pair partner. Suppresses from (242, 0, 262) on the south arcade. |

### 7.5 Beat 5: LAST LIGHT (the Laundry Roofs)

| Field | Spec |
|---|---|
| **Area / coords** | A9 roofs: R1 x 232–246 (+9), R2 248–261 (+8), R3 263–275 (+10, reached by the water-tank stand and plank at z 324), R4 277–292 (+12, reached by a short steel stair from R3 at z 320), all within z 304–336. Gaps of 2 m between them (planks at z 318 and z 324; the rest can be jumped with the air mantle, §12). |
| **Light** | 18:34 → 18:40. Sun 1.5° (held through the traversal) → 0° in MID-A. R1–R3 sit in the cinema's long shadow; climbing onto R4 (+12) takes the player out of the shade into the last direct sun. **The signature moment.** MID-A (§8.5) |
| **Enter trigger** | Ladder top. **CP5** at (236, 9, 304). |
| **Objectives → markers** | `REACH THE STATION WALL`: diamond on the R4 east parapet (291, 13, 328). After MID-A: `BREACH POINT`, a diamond on the wall X (300, 1.5, 322), about 15 m below and east. **All markers change from amber to pale green after sunset** (`s` ≥ 2.0). |
| **Squad** | Single file across the roofs. Positions (HOL / SPA / TIN): R1 (240, 9, 318) / (238, 9, 320) / (238, 9, 316); R2 (254, 8, 316) / (252, 8, 318) / (252, 8, 314); R3 (266, 10, 318) / (264, 10, 320) / (264, 10, 316) once the nest is clear. MID-A positions at the R4 east parapet: Hollis (289, 12, 326) with fist raised, Sparrow (289, 12, 330), Tinker (286, 12, 328). If the player turns west during MID-A, the squad turns their heads too (`ISquadService.SetLookTarget`). |
| **Scripted** | The marksman on the mast platform is **about 120 m** away. His scope glint is a physically motivated flare (the sun is behind the player) that shows 1.5 s before each shot. Water-tank cover: WT1 (240, 9, 312), WT2 (254, 8, 328), WT3 (266, 10, 312), WT4 (284, 12, 330). The rooftop MG nest on R3 is a sandbag ring around an empty tripod. **MID-A trigger:** player inside the R4 volume (x 277–292, z 312–332, Y 11–15) AND E5-02/03 dead. If the player has been inside R4 for 20 s with E5-02/03 still alive, they become `squadTarget` and Hollis and Tinker kill them (≤ 4 s); MID-A starts once both are dead. **The marksman ceases fire when MID-A starts** (he settles, `BK_GRY_05` faintly) and is despawned at `s` ≥ 2.0, so the forced walk is never under sniper fire. **Gate:** the R4 stair top is blocked (PlayerClip `B5.T.Stair`) until MID-A ends. |
| **Radio / VO** | `D035` (first glint) → `D036` (first shot) → MID-A: `D037` → `D038` → interval signal → `D039` (Clockmaker, positional from Tinker's pack, then on the net) → `D040` → `D041` → `D042` (insert I2) |
| **Music** | `MUS_03_LASTLIGHT` swells when the sun disc touches the ridge (strings over synth), then thins to silence under the numbers |
| **Exit trigger** | After MID-A, the player takes the switchback stair from R4 (291, 12, 330) down into the alley → Beat 6 |

**Encounter E5: 3 total, max alive 3**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E5-01 | Greycoat Marksman (SVD) | mast platform (378, 25, 362) | 250° | Placed | Active while the player is on R1–R4 with line of sight, until MID-A starts. Sight range 160 m regardless of LightStage (§14.2). `BK_GRY_05` can be heard faintly on his net when he settles. Killing him is optional; he counts toward kills but not toward any gate. |
| E5-02 | Legion Rifleman | R3 nest (267, 10, 324) | 270° | Placed | Guards the nest |
| E5-03 | Legion Rifleman | R3 nest (272, 10, 329) | 260° | Placed | Guards the nest |

### 7.6 Beat 6: THE RELAY YARD (breach and courtyard)

| Field | Spec |
|---|---|
| **Area / coords** | A10 alley (x 293–299, z 300–340) and A11 yard (Map B) |
| **Light** | 18:41–18:50, afterglow (`s` 2.0 → 2.4): a pink-orange western sky, a cobalt zenith, no direct sun, no sun shadows. The sodium lamps struck in MID-A. Then the floodlights. |
| **Enter trigger** | Alley volume. **CP6** at (295, 0, 334). |
| **Objectives → markers** | 1. `STAND CLEAR`: ring decal at (296, 0, 310), r 2.5 m. 2. `CLEAR THE YARD` (no marker; red markers only on the floods F1–F4 after `D046`). 3. `ENTER THE RELAY HALL`: diamond on the double door (338, 1.2, 330); prompt `[E] Kick` once the squad is at Y3 and the player is within 1.8 m. |
| **Squad** | All crack green chemlights on `D043` (G7). Tinker plants the satchel at (300, 1, 322) in 3 s. **Y1** (rubble at the breach): Hollis (303, 0, 318), Sparrow (304, 0, 326), Tinker (302, 0, 322). **Y2** (cable drums), when ≤ 3 enemies are alive: Hollis (330, 0, 314), Sparrow (334, 0, 320), Tinker (326, 0, 310). **Y3** (door stack), yard clear: Hollis (334, 0, 328.5), Sparrow (337, 0, 328.5), Tinker (341, 0, 328.5). |
| **Scripted** | **Breach:** with the player in the ring, `D044` plays, then 2 s later the blast. The wall at x 300, z 319–325 swaps to its blown mesh. Dust cloud, 0.3 s at timeScale 0.3, a 1.5 s tinnitus muffle crossfade, camera shake. Tinker never detonates while the player is within 8 m of the charge. **Floodlights:** 4 s after the blast `BK_LEG_08` plays and floods F1 (306, 8, 296), F2 (386, 8, 296), F3 (306, 8, 376), F4 (386, 8, 376) slam on. They are unshadowed spots with 45° cones aimed at (345, 0, 325), heavy bloom and lens-flare sprites. **Mechanic:** while the player is inside an active cone, enemy accuracy ×1.25, plus a subtle glare overlay when he looks toward a flood. Each lamp head dies to one hit within a 0.4 m radius: sparks, then a 0.8 s buzzing die-off, and that zone goes dark. Harrou's PA line plays 3 s after the blast. |
| **Radio / VO** | `D043` (alley) → `D044` (ring) → `D045` PA (+3 s) → `BK_LEG_08` → `D046` (+2 s) → `BK_GRY_03` (Greycoats exit the gatehouse) → `D047` (yard clear) → `D048` (Y3) |
| **Music** | `MUS_04_STATION` from the blast |
| **Exit trigger** | Squad at Y3 and the player presses E at the door (kick) → Beat 7 |

**Encounter E6: 8 total, max alive 6**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E6-01 | Legion Rifleman | sandbags (316, 0, 305) | 290° | Placed | Alerted by the blast |
| E6-02 | Legion Rifleman | cable drums (332, 0, 318) | 270° | Placed | |
| E6-03 | Legion Rifleman | fuel bowser (350, 0, 312) | 280° | Placed | Bowser is not explosive (no gameplay hazard) |
| E6-04 | Legion Rifleman | crates (362, 0, 322) | 270° | Placed | |
| E6-05 | Legion Gunner (RPK) | gatehouse roof (346, 4, 294) | 300° | Placed (perch) | Suppresses the breach |
| E6-06 | Greycoat Advisor | gatehouse door (346, 0, 298) | 315° | Closet `SC_B6_GATE` | ≥ 3 yard enemies dead OR 25 s after the blast (only while ≤ 3 are alive). Pair flanks along the south wall. |
| E6-07 | Greycoat Advisor | `SC_B6_GATE` | 315° | Closet | Pair partner (+1.0 s) |
| E6-08 | Greycoat Advisor | `SC_B6_GATE` | 315° | Closet | Holds (`BK_GRY_03`), then pushes to the cable drums |

### 7.7 Beat 7: THE RELAY HALL (ground floor)

| Field | Spec |
|---|---|
| **Area / coords** | A12 (Map C1) |
| **Light** | Interior tungsten at 2,700 K: pendant lamps (baked) plus 2 realtime unshadowed fill lights on the teletype rows. High windows show the sky going blue. Eye-adaptation volume (G10). |
| **Enter trigger** | Door kick. **CP7** at the door stack (338, 0, 327). |
| **Objectives → markers** | 1. `BREACH`. 2. `RECOVER THE LEDGERS`: intel icon on Harrou's desk (351, 1.1, 355); hold E for 1.2 s, then a polaroid intel card, INTEL 1/3. 3. `SEARCH UPSTAIRS`: diamond on the spiral stair (334.5, 0.3, 358). Optional O1: the teletype strip at (341, 1.2, 351). |
| **Squad** | They breach with the player. Hollis clears the west corner (320, 0.3, 333) and moves on to (330, 0.3, 342). Tinker clears the east corner (344, 0.3, 333) and moves on to (340, 0.3, 348). Sparrow follows the player into the office to (348, 0.3, 352) and reads aloud. Hollis goes quiet. |
| **Scripted** | **Slow-mo breach:** 1.5 s at timeScale 0.35. Only the breach mix is pitched (×0.7); VO stays at normal speed. Then CQB through the teletype rows (1.1 m cover) under the world-time clock wall, where every clock is stopped. **Stair wave** trigger: the breach enemies are dead AND the player is within 6 m of the office door (346, 0.3, 350), OR 25 s after the breach. **Gate:** the screening-room door on the landing (330, 4.8, 353.5) stays shut, and `B7.T.Landing` stays disarmed, until INTEL 1/3 (ledgers) is collected and the stair wave is dead. |
| **Radio / VO** | `D049` (stair wave) → `D050` (ledgers) → `D051` (+1.5 s) → [`D052` optional O1] |
| **Music** | `MUS_04` low layer |
| **Exit trigger** | Player reaches the landing (335, 4.8, 355) → Beat 8 |

**Encounter E7: 5 total, max alive 3**

| ID | Archetype | Position | Facing | Spawn | Trigger / notes |
|---|---|---|---|---|---|
| E7-01 | Legion Rifleman | (333, 0.3, 340) behind teletype row 1 | 180° | Placed | Slow-mo target |
| E7-02 | Legion Rifleman | (343, 0.3, 336) archive door | 200° | Placed | Slow-mo target |
| E7-03 | Greycoat Advisor | (328, 0.3, 353) behind row 3 | 160° | Placed | Slow-mo target. 3 clicks on breach. |
| E7-04 | Legion Rifleman | spiral stair (334.5, 4.8, 358) | 180° | Closet `SC_B7_UP` | Stair wave; comes down |
| E7-05 | Legion Rifleman | `SC_B7_UP` | 180° | Closet | Stair wave (+1.5 s) |

### 7.8 Beat 8: THE REEL (upstairs screening room)

| Field | Spec |
|---|---|
| **Area / coords** | A13 (Map C2): 20 velvet seats, pull-down screen on the north wall (z 359.7), 16 mm projector (323, 5.6, 352), can rack on the south wall all labelled **S-3** |
| **Light** | Darkness and one desk lamp, then the projector beam: a cone mesh with additive dust plus a flickering unshadowed spot on the screen |
| **Enter trigger** | Landing. **CP8** at (335, 4.8, 355). |
| **Objectives → markers** | 1. `THREAD THE PROJECTOR` (prompt `E` at the projector) → MID-B (§8.6). 2. `RECOVER THE FILM`: hold E at the can rack (324, 5.6, 350.5), INTEL 2/3. 3. `GET TO THE BASEMENT`: diamond on the west stair (319, 0.3, 337). Optional O2: a film-can label on the rack (319.5, 5.8, 350.5). |
| **Squad** | Sparrow (321, 4.8, 353.5) photographs the screen. Tinker (327, 4.8, 352.5) watches, quiet for once. **Hollis guards the door (331, 4.8, 353.5) facing east, with his back to the screen. He is the only one who doesn't see it.** (Node set `B8.NodeSet.Reel` is flagged `allowDoorway`, the one exception to the no-doorway rule in §15.) **Gate:** the ground-floor west stair door (319, 0.3, 337) stays shut until INTEL 2/3 (film) is collected; `B8.T.BasementFoot` arms at the same time. |
| **Radio / VO** | `D053` (enter) → MID-B with `D054` → `D055` → `D056` (reels taken) → `D057` → [`D058` optional O2] |
| **Music** | None. Projector clatter, distant shouting, generator thump. |
| **Exit trigger** | Player reaches the basement west stair foot (320.5, −4.2, 343) → Beat 9 |

### 7.9 Beat 9: THE CLOCKMAKER (basement transmitter and vault)

| Field | Spec |
|---|---|
| **Area / coords** | A14 (Map C3). The transmitter wall is 4 tube racks R1–R4 behind mesh along z 356–358, glowing orange. ON AIR lamp at (332, −1.2, 359.6). Tape-loop deck at (331.5, −3.4, 357) with a speaker above it relaying the live broadcast. Vault safe in the east wall at (345.8, −3.4, 342). |
| **Light** | Tube-orange glow (baked emissive), the ON AIR lamp pulsing red at 1 Hz, Greycoat flashlight cones. After the transmitter dies, a **pre-baked emergency lightmap swap** (G11) plus 2 red realtime lights at the stair feet. |
| **Enter trigger** | West stair foot. **CP9** saved there. |
| **Objectives → markers** | 1. `DEFEND CASTELLANOS`: progress ring on the safe, 0–100 % over **90 s of active drilling**. Drilling pauses while any enemy is within 5 m of Sparrow. 2. `RECOVER THE CODEBOOK`: Sparrow takes it automatically, INTEL 3/3. 3. `DESTROY THE TRANSMITTER`: red diamonds on R1–R4, each 60 HP. Destroyed racks shatter in sparks. 4. `EXFIL: THE PITCH`: diamond on the tunnel door (344, −3, 359.8). |
| **Squad** | Sparrow at the safe (344.8, −4.2, 342), unavailable. Hollis covers the west stair from (325, −4.2, 342). Tinker covers the east stair from (339, −4.2, 344). The player floats between them, guided by callouts. |
| **Scripted** | The interval-signal ticks loop from the room speaker and the reading plays live (`NUM_BASEMENT_LOOP`; `MUS_04` ducks under it). Wave timers `t` are wall-clock seconds since the drill started (`D060`), not drill progress. **Gates:** racks R1–R4 take hits (sparks) but no damage until `D065` starts; the tunnel door (344, −3, 359.8) stays shut until `D066`. When the 4th rack dies, the broadcast cuts mid-number (`D066`). |
| **Radio / VO** | `D059` (drill placed) → `D060` (defense start) → `D061` (wave 2) → `D062` (first wave-2 Greycoat at the east stair foot) → `D063` (wave 3, hatch) → `D064` (100 %) → `D065` → `D066` (4th rack) → `D067` (+1 s) |
| **Music** | `MUS_04_STATION` full |
| **Exit trigger** | Player enters the tunnel; 0.5 s fade at the ladder (it hides the `S7_Pitch` activation) → Beat 10 |

**Encounter E9: 9 total, max alive 5**

| ID | Archetype | Position | Spawn | Trigger / notes |
|---|---|---|---|---|
| E9-01..03 | Legion Rifleman ×3 | closet `SC_B9_W` on the ground floor (318, 0.3, 336), then down the west stair | Closet | **Wave 1:** drill start |
| E9-04..06 | Greycoat Advisor ×3 | closet `SC_B9_E` in the archive room (351, 0.3, 336), then down the east stair | Closet | **Wave 2:** t = 30 s OR wave 1 ≤ 1 alive. One throws a frag on arrival. 2 clicks, then 3 clicks. |
| E9-07..08 | Legion Rifleman ×2 | closet `SC_B9_H` in the cable trench below the south wall (332, −6.0, 331), then up through the hatch (332, −4.2, 334.3) | Closet | **Wave 3:** t = 60 s OR wave 2 ≤ 1 alive |
| E9-09 | Greycoat Advisor | `SC_B9_E` | Closet | Wave 3 (+3 s) |

### 7.10 Beat 10: THE PITCH (LZ)

| Field | Spec |
|---|---|
| **Area / coords** | A16 (Map B). The postern exit (346, 0, 382) leads down a ramp to (346, −1, 391). Bleacher x 328–348, z 392–397 (1.2 m back wall). Pad (350, −1, 406). |
| **Light** | 19:05, **blue hour** (`s` 3.2 → 3.7): a deep cobalt sky with a thin orange band on the west horizon. The town's sodium glow is behind the team. Truck headlights rake the field. Green smoke is lit by the headlights. **Parachute flares (G2):** FL1 ignites at (346, 70, 404) at t = 12 s and FL2 at (356, 70, 410) at t = 42 s; after a wave-off, FL3 ignites at (340, 70, 400) at t = 72 s. Each burns 30 s, descends at 1.3 m/s, sways ±4° on a 3 s pendulum, and casts swinging shadows (one shadowed 120° spot, so only one flare is ever lit). |
| **Enter trigger** | Postern. **CP10** at (346, 0, 383). |
| **Objectives → markers** | 1. `GET TO THE LZ`: diamond on the skirmish line behind the bleacher (340, −1, 390.5); completes on entering `B10.T.LZ`. 2. `DEFEND THE LZ`: starts at the end of `D070` wherever the player is; timer ring 1:15, shown as Magpie's distance (2,000 m → 0 m; after a wave-off it jumps back to 900 m and counts down again over 20 s). 3. `KILL THE RPG GUNNER`: high-priority red diamond, from `D073`. 4. `BOARD THE HELICOPTER`: prompt `[E] Board` at the left door (350, 0.2, 404), enabled after `D076`. |
| **Squad** | **P1**, a skirmish line on the south side of the bleacher facing north and east: Hollis (332, −1, 390.5), Sparrow (340, −1, 390.5), Tinker (346, −1, 390.5). Hollis pops green smoke at (350, −1, 404) when the hold starts. Boarding order: Sparrow (carrying the book), Tinker, Hollis, the player last. |
| **Hold timeline** | t counts from the end of `D070`. t 0: hold starts. t 8: **Truck 1** arrives on the Customs Road spline (430, −1, 395) → (392, −1, 396), and 3 Legion dismount. t 12: FL1. t 20: Greycoat NVG flanker from the west. t 40: **Truck 2** on the salt-pan track spline (362, −1, 470) → (362, −1, 424) with 2 Legion and the RPG gunner on the bed. t 42: FL2. t 60 (15 s left): Magpie's final approach along `B10.HeliApproach` from the east (visible from about 250 m). If the RPG gunner is alive at t 63, he fires at Magpie: **wave-off** (Magpie breaks north along `B10.HeliWaveOff` and re-joins the approach), +20 s. At most one wave-off: from that moment the RPG gunner is `squadTarget`, and if he is still alive 8 s later (when he would telegraph again) Hollis kills him. t 75 (or 95 after a wave-off): wheels down at the pad, heading 270°, left door facing the team. Before t 60 the RPG gunner fires at the bleacher where the squad is (4 m splash can hurt the player). |
| **Radio / VO** | `D068` → `D069` → `D070` → `BK_LEG_10` + `D071` (truck 1) → `D072` (FL1) → `BK_GRY_09` (once, at FL1) → `D073` (RPG spawns) → `D074` (final approach) → [`D075` wave-off] → `D076` (wheels down) |
| **Music** | `MUS_05_EXFIL` |
| **Exit trigger** | Player presses E at the left door → cut to Beat 11 (door-gun view) |

**Encounter E10: 7 total, max alive 7 (the mission peak)**

| ID | Archetype | Position | Spawn | Trigger / notes |
|---|---|---|---|---|
| E10-01..03 | Legion Rifleman ×3 | truck 1 tailgate (394, −1, 396) | Vehicle dismount | t = 8 s. They push west along the north edge (z 412). |
| E10-04 | Greycoat Advisor (NVG variant) | `SC_B10_W` (300, −1, 388), along the wall | Closet | t = 20 s. Flanks to (318, −1, 394). Green lens glow is his tell. |
| E10-05..06 | Legion Rifleman ×2 | truck 2 (362, −1, 424) | Vehicle dismount | t = 40 s |
| E10-07 | Legion RPG | truck 2 bed (362, 1.0, 424) | Vehicle | t = 40 s. Shouts and raises the launcher for 1.2 s before firing. Targets the squad, then Magpie. |

### 7.11 Beat 11: MAGPIE (exfil under fire, on rails)

| Field | Spec |
|---|---|
| **Area / coords** | A17. Spline, flown in 50 s: **N0** pad (350, 0, 406) → 8 s → **N1** (346, 30, 372) over the Hall → 8 s → **N2** (300, 35, 330) over the roofs → 10 s → **N3** (225, 40, 280) banking over Clock Square → 10 s → **N4** (186, 35, 215) down the Long Market's lamps → 8 s → **N5** (120, 55, 120) over the grove → 6 s → **N6** (40, 80, 40) toward the ridge where the mission began. What is seen is `S5_StationExt` plus `S9_TownFar`. The targets and trucks live in `S8_Heli`. |
| **Light** | 19:10, `s` 3.7 → 4.0, the end of civil twilight. Deep blue, sodium-orange streets, the burning technical, green Legion tracers, red door-gun tracers. |
| **Gameplay** | Left-door **M60D-pattern door gun** (§13.3): yaw ±70° around the door normal, pitch +15° / −60°, infinite ammo, overheat. **Incoming damage ×0.25 and a health floor of 1 (the player cannot die).** The cabin yaw follows the spline tangent, so the left door faces south-east for the whole run; every target below sits on that side (verified with the spline: each is inside the arc for its whole window, 40–90 m away, pitch −13° to −53°). |
| **Targets** | All are on the left (south-east) of the track. T1 gatehouse roof (346, 6, 294), active t 8–16 (the old E6-05 perch) · T2 housing roof east of Clock Square (285, 9, 268), active t 15–23 · T4 housing roof south-east of the square (236, 9, 238), active t 26–33 · T3 the RPG man, housing roof (224, 10, 226): hidden behind a parapet until t 28, stands, raises the launcher for 1.2 s and fires at t 30 whatever happens (if shot during the raise he fires as he falls). 2 Legion trucks drive into Clock Square along the east street from t 16 ((300, 0, 276) → (246, 0, 276) at 4.5 m/s, then stop), in the arc t 16–27. Each truck takes 20 hits, then a simple fireball. Targets and trucks live in `S8_Heli` and stand on the far-proxy roofs and street. `S8_Heli` also carries a copy of the burning-technical fire prop, because `S3_Square` is unloaded by then. The flight after t 42 (N5, N6) is behind the white-out and is never seen. |
| **Squad** | Tinker on the right door gun. Hollis in the middle of the cabin hauling Sparrow's bag in. Sparrow clutching the codebook. |
| **Radio / VO** | `D077` (t 0) → `D078` (T1 opens fire, ≈ t 8) → `D079` (t 25) → `D080` (clean, in his head) → `D081` (T3's RPG launches at t 30) → climax (§8.7) with `D082` → insert I4 → white |
| **Music** | `MUS_05` until the RPG launch. Hard cut to the cabin alarm and the tinnitus. |
| **Exit** | Cut to white → outro |

**Encounter E11: 4 total.** T1–T4 are Rooftop Targets (§14.1): stand, fire, die. Each is always aware of the helicopter (no perception) and fires only inside its active window.

### 7.12 Objective master list (ids used by `BeatTable` and `IObjectiveService`)

HUD text format: `OBJ <beat>/11 — <group> — <TEXT>`. `<group>` is a stable five-digit flavour group: FNV-1a 32-bit hash of the objective id, modulo 100000, zero-padded. Markers use the ids in TECH_PLAN Appendix A. Optional intel never gets a compass or edge marker: its intel icon shows only within 6 m.

| Id | Text | Marker (kind → target) | Completes when |
|---|---|---|---|
| `B1.Observe` | OBSERVE ZAROUD 0/3 | Diamond (hollow) → `B1.POI1..3` | 3 POIs glassed, or 40 s |
| `B1.Follow` | FOLLOW HOLLISTER | Follow → Hollis | player enters `B1.T.WadiMid` |
| `B1.Grove` | REACH THE GROVE | Diamond → `B1.GroveTarget` | `B1.T.GroveEnter` |
| `B2.Sentries` | ELIMINATE THE SENTRIES | Diamond → `B2.FiringPos`; within 3 m: TargetX → E2-01, E2-02 | both sentries dead |
| `B2.Weighbridge` | CLEAR THE WEIGHBRIDGE | Diamond → `B2.TruckTarget` | E2-03…06 dead |
| `B2.Enter` | ENTER ZAROUD | Diamond → `B2.ArchTarget` (after the bolt cut) | `B2.T.Exit` |
| `B3.Push` | PUSH THROUGH THE LONG MARKET | Diamond → `B3.ArchTarget` | `B3.T.Exit` with the squad at M4 |
| `B4.Flank` | FLANK THROUGH THE CINEMA | Diamond → `B4.SideDoor` | side door opened |
| `B4.Technical` | NEUTRALIZE THE TECHNICAL | HighPriority (through walls) → E4-01 | E4-01 dead |
| `B4.Regroup` | REGROUP WITH HOLLISTER | Follow → Hollis | every E4 enemy dead |
| `B4.UpTop` | UP TOP | Diamond → `B4.Ladder` | ladder top (`CP5`) |
| `B5.Wall` | REACH THE STATION WALL | Diamond → `B5.R4Parapet` | MID-A starts |
| `B5.Breach` | BREACH POINT | Diamond → `B5.BreachPoint` | `B6.T.Alley` |
| `B6.StandClear` | STAND CLEAR | Ring → `B6.StandClear` | player in the ring (`D044`) |
| `B6.Yard` | CLEAR THE YARD | HighPriority → live floods F1–F4 (after `D046`) | every E6 enemy dead |
| `B6.Hall` | ENTER THE RELAY HALL | Diamond → `B6.HallDoor` | door kicked |
| `B7.Breach` | BREACH | none | E7-01…03 dead |
| `B7.Ledgers` | RECOVER THE LEDGERS | Intel → `B7.Ledgers` | hold E 1.2 s (INTEL 1/3) |
| `B7.Upstairs` | SEARCH UPSTAIRS | Diamond → `B7.Spiral` | `B7.T.Landing` |
| `B7.O1` (optional) | — | Intel icon within 6 m → `B7.O1` | hold E 1.2 s (OPTIONAL) |
| `B8.Thread` | THREAD THE PROJECTOR | Diamond → `B8.Projector`; prompt `[E] Thread the projector` | MID-B ends |
| `B8.Film` | RECOVER THE FILM | Intel → `B8.CanRack` | hold E 1.2 s (INTEL 2/3) |
| `B8.Basement` | GET TO THE BASEMENT | Diamond → `B8.WestStair` | `B8.T.BasementFoot` |
| `B8.O2` (optional) | — | Intel icon within 6 m → `B8.O2` | hold E 1.2 s (OPTIONAL) |
| `B9.Defend` | DEFEND CASTELLANOS | Ring (drill %) → `B9.Safe` | drill 100 % |
| `B9.Codebook` | RECOVER THE CODEBOOK | none | completes with `B9.Defend` (INTEL 3/3, automatic) |
| `B9.Transmitter` | DESTROY THE TRANSMITTER 0/4 | HighPriority → `B9.R1..R4` | 4 racks destroyed |
| `B9.Exfil` | EXFIL: THE PITCH | Diamond → `B9.TunnelDoor` | `B9.T.Tunnel` |
| `B10.LZ` | GET TO THE LZ | Diamond → `B10.LZLine` | `B10.T.LZ` |
| `B10.Defend` | DEFEND THE LZ | Ring (Magpie m) → `B10.Pad` | wheels down |
| `B10.RPG` | KILL THE RPG GUNNER | HighPriority → E10-07 | E10-07 dead |
| `B10.Board` | BOARD THE HELICOPTER | Diamond → `B10.HeliDoor`; prompt `[E] Board` | E pressed |
| `B11.Cover` | COVER THE EXFIL | HighPriority → active T1–T4 and trucks | climax starts (t 30) |

Hold prompts elsewhere: `HOLD [E] Resupply` (crates), `HOLD [E] Take <weapon>` (pickups), `[E] Open` (cinema side door), `[E] Kick` (Hall door).

### 7.13 Progression gates (so no beat can be skipped or soft-locked)

| Gate | Blocks | Opens when |
|---|---|---|
| Weighbridge chained gate (183, 0, 163) | entry to the Long Market | 3 s after `D020` starts (bolt cut) |
| Cinema side door (186, 0, 307) | the lobby | player presses E |
| PlayerClip at the foot of ladder L (236, 0, 301) | the roofs | every E4 enemy dead |
| `B5.T.Stair` PlayerClip at the R4 stair top (291, 12, 330) | the alley | MID-A ends |
| Relay Hall double door (338, 0.3, 330) | the Hall | squad at Y3 and player kicks (E) |
| Screening-room door (330, 4.8, 353.5) | the screening room | INTEL 1/3 and the stair wave dead |
| Ground-floor west stair door (319, 0.3, 337) | the basement | INTEL 2/3 |
| Racks R1–R4 invulnerable | the broadcast ending early | `D065` starts |
| Tunnel door (344, −3, 359.8) | the cable tunnel | `D066` |
| Helicopter door prompt | boarding | `D076` |

Every gate's state is part of the checkpoint snapshot.

---

## 8. Cinematics, shot by shot

Everything runs in-engine on Unity Timeline (install `com.unity.timeline`; Cinemachine 3 is optional). Booth shots are first-person from the chair, Black Ops style, with limited mouse look (**±40° yaw, ±20° pitch**, springing back to center 2 s after input stops). Every cinematic can be skipped after the first viewing (hold Space for 1 s). Subtitles stay on.

### 8.0 Set coordinates

**Booth (`S0_Booth`, scene root at world (−600, 0, −600)).** Local frame: +Z faces the CRT cart and the mirror, +X is right, Y is up, and the origin is the chair's floor point.

| Element | Local position | Notes |
|---|---|---|
| Room shell | x −2.4 … 2.4, z −2.0 … 2.6, height 3.0 | Olive painted plaster (`PaintedPlaster012`) and acoustic tile; concrete floor with a drain at (0, 0, 0.8) |
| Chair | (0, 0, 0), facing +Z | Bolted steel chair, leather cuffs, electrodes taped to the forearms |
| **C_POV** (Calder's eye) | (0, 1.18, 0.05) | Vertical FOV 50° |
| CRT cart | (0, 0, 1.25), 1.7 m wide | Six mismatched CRTs (Poly Haven `Television_01/02` scaled and varied). Render textures 512 × 384 through the CRT shader (curvature, scanlines, flicker, phosphor tint). |
| CRT1–3 (bottom row) | (−0.55, 1.00, 1.30) · **(0.00, 1.02, 1.30) = center CRT2** · (0.56, 0.98, 1.30) | |
| CRT4–6 (top row) | (−0.50, 1.48, 1.34) · (0.04, 1.52, 1.34) · (0.55, 1.47, 1.34) | |
| One-way mirror | wall plane z 2.55, x −1.6 … 1.6, Y 0.9 … 2.1 | Planar-ish baked reflection probe; shows Calder's silhouette |
| Wall clock (6:40, cracked) | (0.9, 2.35, 2.52) | Second hand twitches in place |
| Fluorescent tube | (0, 2.95, 0.6) | Baked plus a realtime flicker curve |
| Scrambler trolley | (−0.75, 0, 1.0); top at Y 0.85 | **Scrambler box** at (−0.75, 0.92, 0.95) with a toggle and VU needle; reel-to-reel deck at (−0.78, 0.92, 1.30) |
| Desk phone | (0.95, 0.88, 1.20), right end of the cart | Black rotary phone |
| Interrogator's table | (−0.8, 0, −0.7), top at Y 0.75 | Ashtray, pen (the four-tap emitter), steel lighter. Smoke emitter at (−0.6, 1.7, −0.6). |
| Film camera (reel only) | (−1.6, 1.9, 2.2), looking at the chair | Used to shoot "Session 11" for MID-B |

**Helicopter cabin (`S8_Heli`, root rides the spline).** Local frame: +Z is the nose, +X is right, cabin floor Y = 0. Left door-gun pivot at (−1.05, 1.10, 0.30). Player eye at (−0.85, 1.25, 0.30) facing −X. Hollis crouched at (0.10, 1.55, −0.60). Sparrow seated at (0.60, 1.00, 0.90). Tinker on the right gun at (1.05, 1.10, 0.30). Red cabin lamp at (0, 1.90, 0).

### 8.1 Click gate (until the player clicks)
A black screen with a green CRT cursor: `SESSION 12 // SUBJECT S-3 // CLICK TO BEGIN`. The loading progress is a row of number groups that fills in as the bundles arrive. The click is accepted as soon as `S_Persistent` and `S0_Booth` are loaded (about 55 MB on a first visit, including the engine); `S9_TownFar` and `S1_Approach` keep loading behind the intro and are needed only at shot 7 (1:08, §8.2). A small line reads `HEADPHONES RECOMMENDED · MOUSE + KEYBOARD · ESC PAUSES`. The click unlocks WebAudio, requests pointer lock (`unadjustedMovement` first, with a fallback) and, if enabled in options, fullscreen. The browser's requirement doubles as part of the story.

### 8.2 Intro: "SESSION 12" (1:43)

| # | Time | Camera | On screen | Audio / SFX | Lines |
|---|---|---|---|---|---|
| 1 | 0:00–0:06 | — | Black. Typewriter text: `02 NOVEMBER 1984 — 03:12 — LOCATION: [REDACTED]` | A stuck clock tick that repeats without advancing. The three-note music-box chime at 0:03 (`MUS_01_SESSION` begins). | — |
| 2 | 0:06–0:12 | C_CLOCK at (0.9, 2.33, 2.20) looking +Z, pushing in 0.1 m | ECU of the wall clock at 6:40, cracked glass, second hand twitching in place. The fluorescent tube stutters on (4 blinks). | Tube buzz and ballast tick | — |
| 3 | 0:12–0:22 | C_POV, pitch −35° tilting up to 0° | The strapped wrists, the electrodes, his own breathing. Tilt up to the dark CRT cart; the mirror shows his silhouette. Cigarette smoke drifts in from behind the left shoulder. **Limited look unlocks at 0:14.** | Leather creak, breath, room hum | — |
| 4 | 0:22–0:30 | C_POV | CRT1–6 snap on 0.6 s apart and show static. The scrambler's VU needle (at the left edge of view) moves with the voice. | Degauss thunk and high whine per set | `D001` at 0:25 |
| 5 | 0:30–0:58 | C_POV | **Intel montage, one clip per CRT, cutting every 2–3 s on the low synth pulse:** (a) CRT4: hand-drawn map of Sardahn with Zaroud circled in grease pencil (Nano Banana 2K, no text, invented glyphs only); (b) CRT2: aerial recon photo of the station and mast with measurement ticks, raw photo plus live overlay text `ZAROUD RELAY / 14 OCT 1984 1012Z / FRAME 0640`; (c) CRT5: 8 mm footage of Harrou reviewing the Legion in Clock Square, **his face scratched out on the film** (pre-rendered in the editor from `S3_Square` with the film shader into `VID_Harrou8mm.mp4` and played with `VideoPlayer.url`, TECH_PLAN D14; until that exists, the still `IMG_HARROU_STILL` with a gate-weave shader); (d) CRT1: teletype paper scrolling `74410 00640 …`; (e) CRT6: team ID photos stamped `SUNDIAL-ACTUAL / -2 / -4` with height and blood type, rendered from the real heads (G12); (f) CRT3: a live close-up of the booth's own reel-to-reel deck (`Cam_Feed_Deck`) turning, playing TALLOW's briefing tape. One stopped-clock frame flashes on a random monitor 3 times. | Synth pulse; tape hiss | `D002` at 0:44, `D003` at 0:51 |
| 6 | 0:58–1:08 | C_POV | The CRTs hold on the stopped clock | Four pen taps on steel, positional from behind left, at 1:04–1:06 | `D004` at 0:59 |
| 7 | 1:08–1:18 | C_POV, forced turn to CRT2 (0.8 s ease) | A gloved hand in a grey suit sleeve reaches in from behind and turns Calder's head to the center CRT. CRT2 shows **the ridge at golden hour, live**: a render texture from a camera at (42, 23.2, 30), heading 45°, in `S1_Approach`. If `S1_Approach` or `S9_TownFar` is not loaded yet, CRT2 shows static and the timeline holds at the start of shot 8 (tube hum loops) until both are loaded. | Glove on stubble, chair creak | `D005` at 1:08 |
| 8 | 1:18–1:33 | Dolly from C_POV into CRT2: 1.0 m over 14 s, FOV 50° → 30° | The scanlines thicken until they *become* the hazy horizontal bands of the desert horizon | Fluorescent hum crossfades to wind and cicadas over the last 5 s | `D006` at 1:19 |
| 9 | 1:33–1:43 | Player camera, prone on the ridge | Overexposed white (post exposure +6 EV falling to 0 over 2 s) settles into the ridge. Hollis's boot is in frame on the left. The 8 s title card (§8.3) runs 1:35–1:43, so intro plus title is 1:43 in total. | Wind, distant siren winding down | — |

> **Added 2026-10-16:** a third-person helicopter **INSERTION** cold open (§8.2a, about 55 s, 12 shots) now plays between shot 8 and shot 9; its final push-in lands on shot 9. The full spec is in `Docs/CINEMATIC_DIRECTION.md` §2, which overrides this section where they conflict.

### 8.3 Mission title card (8 s, in-engine over the ridge, during prone → kneel)
- 2.35:1 letterbox bars slide in (0.5 s). Amber teletype type in a typewriter font (Special Elite, Apache 2.0, or Courier Prime, OFL; needs approval O1, otherwise TMP's bundled LiberationSans SDF) prints character by character with key clacks (`STG_TITLE`):
  ```
  OPERATION LONG SHADOW
  ZAROUD — REPUBLIC OF SARDAHN
  21 OCTOBER 1984 — 17:58 LOCAL
  SFC NATHAN CALDER — "SUNDIAL-3"
  ```
- The Black Ops touch: each line first prints as **five-digit number groups** that are "corrected" into the real text (0.4 s per line). On line 3 the time reads **18:40** for 6 frames before settling to 17:58.
- The bars retract, control returns, and `D007` plays.

### 8.4 Glitch inserts and death inserts
Section 5.5 has the schedule. Implementation: a `GlitchInsert` component with a booth camera preset, a length, an optional VO id and a stinger, run by a `GlitchFeature` renderer feature (scanline tear, RGB split, a 1-frame white). Insert lengths given in frames are at a 60 fps reference and are played as unscaled time (6 f = 0.100 s), with a minimum of 2 rendered frames, so they look the same at any frame rate.
**Death insert:** on death, 0.6 s of desaturated slow-down, then the glitch to C_POV in the booth for 2.0 s (CRTs on static, VU needle live) with a random `DI_01`–`DI_06`. Static, then a black screen with the teletype line `SESSION 12 — RESUMING` while the checkpoint reloads behind it.

### 8.5 MID-A: "LAST LIGHT" (Beat 5, in-engine, about 47 s; the player keeps control at a forced walk of 1.2 m/s, look free)

The Beat 5 script owns MID-A (TECH_PLAN §7.11) and drives LightStage directly while it runs: `s` = 1.80 at 0:00, held to 0:04, 1.97 at 0:14, 2.00 at 0:20 (piecewise linear; the curves it samples are in §9.2a). Times below are from the MID-A trigger.

**Geometry that makes the image work:** R4 is the highest roof east of the cinema (+12, §6.5), so at 1.5° nothing to the west (cinema 12 m at 46–61 m, market houses ≤ 11 m at ≥ 90 m) shades it; its west edge has a pipe railing, not a parapet. The western ridge-silhouette mesh at 2.5 km has its crest about 0.25° above R4's eye line (≈ 25 m world height), so the disc disappears behind it exactly as the sun reaches 0°.

| # | Time | What happens | Audio | Lines |
|---|---|---|---|---|
| 1 | 0:00 | The squad reaches the R4 east parapet. Hollis raises a fist and they crouch facing east. The player has just stepped out of the cinema's shadow into the last direct sun: **the player's and the squad's shadows run the full width of the roof, climb the east parapet and reach out over the station yard toward the mast.** The player's shadow comes from the shadow-proxy body (TECH_PLAN D19). That is the LONG SHADOW image, and nobody mentions it. | Wind, cicadas | — |
| 2 | 0:00–0:06 | If the player turns west, the sun disc (procedural disc + lens flare at az 258°) sits about 1.5° above the far western ridge; to its left, the Shoulder where the mission began is a dark silhouette. The squad turns their heads too (`SetLookTarget`). | | — |
| 3 | 0:04–0:20 | The disc slides down onto the ridge silhouette (sun 1.5° → 0°) and is fully hidden at 0:20. Direct sun fades to 0 at 0:20; shadow strength fades out between 0:12 and 0:20. The sky blends sunset → afterglow, fog shifts orange → violet. At 0:12 **Calder checks his watch automatically** (FP arm IK tween, 1.2 s); the Seiko reads **18:40**. At 0:14 the cicadas stop and the first crickets start. | `MUS_03_LASTLIGHT` swell starts at 0:05 | — |
| 4 | 0:14–0:22 | `PracticalsStrike` (`s` = 1.97). The mast's red aviation lights blink on from top to bottom (5 lights 0.3 s apart, then 1 Hz). **The sodium lamps strike street by street**, rippling out from the station generator (372, 0, 305) at 15 m/s. Each one starts a dim pink-red and warms to orange over 8 s (the real sodium warm-up, compressed). That is 38 emissive lamp heads plus at most 6 realtime unshadowed lamps near the player. | Ballast ticks and buzz, rippling | — |
| 5 | 0:20–0:27 | Sun at 0°: `D037`. Under it, the interval signal (8 ticks 0.5 s apart, then the three-note chime; 6.0 s) starts faintly **from Tinker's PRC-77 speaker in 3D, 2 m away** (G9). `D038` reacts to it. | Music thins under the ticks | `D037` at 0:20, `D038` at ≈ 0:23 |
| 6 | 0:27–0:40 | `D039`: the digits on a 0.9 s grid. At "Seven" (≈ 0:28.8) the broadcast jumps from the pack onto everyone's headset as 2D radio. Calder's vision contracts slightly with each digit (vignette pulse 0.15). | Music to silence under the numbers | `D039` from ≈ 0:27 |
| 7 | ≈ 0:38.7 | On "Six. Four—": **I2**, a 0.1 s insert of the stopped clock. Calder's vision pulses. | `STG_GLITCH` | `D040` at ≈ 0:39.6, then `D041` (≈ 0:41.6). `D042` starts on "Calder?" inside `D041` (≈ 0:44) and overlaps it. |
| 8 | ≈ 0:46 | Hollis switches frequency (hand to the handset) and the numbers cut. When `D042` ends (≈ 0:47), full speed returns, `B5.T.Stair` opens and the BREACH POINT marker pops up in its new pale-green palette. | | — |

### 8.6 MID-B: "THE REEL" (Beat 8, 45 s, Timeline, player POV with ±20° look)
Camera: the player's head at the projector (323, 6.45, 351.4), facing north toward the screen. The film is a live render texture from `BOOTH.FilmCam` in `S0_Booth` (always loaded) shown on the screen material plus the projector spot. For shots 4–6 the booth's seated Calder swaps to the shaved, gaunt "Session 11" head variant and the interrogator's lighter hand is posed in frame; at shot 7 (I3) the booth is switched back to its "Session 12" state before the cross-dissolve.

| # | Time | On screen | Audio | Lines |
|---|---|---|---|---|
| 1 | 0:00–0:08 | Calder's hands set a can on the table and thread the 16 mm film: an FP hands animation with IK on the reel props, reels spinning | Can lid, film rattle | — |
| 2 | 0:08–0:12 | He throws the lever. The projector clatters and its beam cuts through dust onto the cracked screen. Sparrow and Tinker are silhouetted in the beam. **Hollis is in the doorway with his back turned, watching the stairs.** | Projector clatter (loop) | — |
| 3 | 0:12–0:16 | Countdown leader **5… 4… 3…**, each number synced to the clatter | Leader beeps | — |
| 4 | 0:16–0:26 | Grainy black-and-white footage of the **booth**, shot from the booth's film camera and rendered in-engine from `S0_Booth` through a mono/scratch/gate-weave shader stepped at 18 fps. Burned-in slate: `S-3 / SESSION 11 / 03-84`. A concrete room, the wall clock at **6:40**, a man in the steel chair. | | — |
| 5 | 0:26–0:31 | A jerky zoom. The man lifts his head: **it's Calder** (shaved-head, gaunt variant of his Rocketbox head). Sparrow slowly lowers her camera. | | — |
| 6 | 0:31–0:36 | A hand with a steel lighter enters the film frame. The man's lips move silently with the numbers (jaw keyed to digit timing) and he goes limp. | | — |
| 7 | 0:36–0:40 | **I3:** the projected image grows to fill the frame and becomes the real booth from the same angle (a cross-dissolve to the booth camera with a matching transform) | Room tone of the booth | `D054`, whispered close to the ear |
| 8 | 0:40–0:45 | The film jams and the frame burns out from the center (film-burn shader) to white. Hollis's hand grips the player's shoulder (camera shake 0.3). Control returns. | Film flap, slap of a hand | `D055` |

### 8.7 Exfil climax (end of Beat 11, about 12 s, scripted, input locked)

| # | Time | On screen | Audio | Lines |
|---|---|---|---|---|
| 1 | 0:00–0:03 | An RPG streaks up from T3's rooftop on the left, south-east of Clock Square, trailing smoke, and **passes 3 m under the skid**, a clear miss if anyone is watching. The helicopter lurches (camera roll 8°), an alarm warbles, the cabin goes red. | Rocket whoosh passing under, alarm | `D081` |
| 2 | 0:03–0:06 | The door gun disengages. The camera is drawn across the cabin (yaw about 160° over 1.5 s) to Hollis, who is pulling Sparrow's bag in from the right door. | The music-box chime once, dry and centered (`D080` has already played clean) | — |
| 3 | 0:06–0:10 | **Calder's right hand comes up into frame with the Hush Puppy**: a slow, steady FP pistol rise over 3 s. Hollis turns (head IK to the camera). | Rotor, alarm, heartbeat | `D082` (jaw flap from the audio amplitude on the Rocketbox jaw bone) |
| 4 | 0:10–0:12 | **I4:** four inserts cascade (clock 6 f, pen 5 f, CRT static 4 f, Calder's own eye 8 f). **Cut to white before any shot.** | Tinnitus whine; every other sound cut | — |

### 8.8 Outro: "SIX-FORTY" (1:50)

| # | Time | Camera | On screen | Audio | Lines |
|---|---|---|---|---|---|
| 1 | 0:00–0:08 | C_POV | White gives way to a fluorescent flicker: the chair, ragged breathing, the clock still at 6:40 | Tube buzz | `D083` at 0:04 |
| 2 | 0:08–0:14 | C_POV | **First-person fists clench against the cuffs** (Rocketbox finger bones) | Leather strain | `D084` |
| 3 | 0:14–0:26 | C_POV | CRT2 plays grainy green gun-camera footage from Magpie (`VID_GunCam`: a camera on the left skid strut looking forward and up, framing the open cabin door, flown along the Beat 11 spline, green phosphor shader): the RPG trail passing under the skid, then **one muzzle flash lighting the cabin interior** | Faint projector-like whir | `D085` at 0:15 |
| 4 | 0:26–0:40 | C_POV pushing into CRT5 | The CRTs show Sparrow's photographs of the codebook. On the last page is a typed roster of 14 callsigns with four-digit groups. The push-in lands on `SUNDIAL-3 … CALDER, N. … 0640`; the next line reads `SUNDIAL-2 … CASTELLANOS, M. … 1921`. | Shutter clicks | `D086` at 0:27 |
| 5 | 0:40–0:50 | C_POV | One pen tap per digit. After the last digit the music-box chime plays. **The FP hands unclench and lie flat**, the breathing slows, and the grade loses 30 % of its saturation (volume weight). | Pen taps, chime | `D087` |
| 6 | 0:50–1:00 | C_POV | Stillness | | `D088`, `D089` |
| 7 | 1:00–1:12 | C_POV, look locked | **The interrogator walks past the chair for the first time**, seen only from behind: grey suit, head turned away, backlit by the CRTs. He reaches to the scrambler box and **flips the toggle**. Click, and the VU needle drops to zero (G1 payoff). | Toggle click; the room seems to get closer | `D090`, **clean for the first time** |
| 8 | 1:12–1:22 | C_POV | He lifts the desk phone on the right end of the cart. **It is the briefing-tape voice.** That's the click. | Receiver lift, line hum | `D091` |
| 9 | 1:22–1:34 | C_POV | The CRTs switch to a long-lens surveillance shot: Sparrow in a sunlit airport terminal, sunglasses, camera bag. Burn-in `26.10.84 18:31` (live TMP). The still `IMG_SURVEIL_SPARROW` is composited in the editor from the `IMG_AIRPORT_PLATE` background (no people) and an in-engine render of Sparrow. | | — |
| 10 | 1:34–1:42 | — | The fluorescent tube dies. Black. The interval signal (6.0 s: ticks, then the chime), then the line. | | `D092` at 1:40 |
| 11 | 1:42–1:50 | — | Hold black, then the debrief dossier types in | Teletype | — |

### 8.9 Debrief screen (a CRT dossier typed line by line, interactive)
```
AFTER-ACTION REPORT // OPERATION LONG SHADOW // EYES ONLY
MISSION TIME ....... 14:02      KILLS ........ 44     HEADSHOTS .... 17
ACCURACY ........... 38 %       GRENADES ..... 6      DEATHS ....... 2
INTEL .............. LEDGERS [X]  REELS [X]  CODEBOOK [X]   OPTIONAL 1/2
HOLLISTER, F. ...... KIA (HOSTILE FIRE)      <- "HOSTILE" is struck through and retyped "UNDER REVIEW"
DUQUESNE, W. ....... RTB
CASTELLANOS, M. .... PENDING
CALDER, N. ......... ASSET STATUS: ACTIVE
```
- The values shown are examples; the real stats fill in at runtime.
- Redaction bars wipe across the personnel lines, then a stamp lands: `PROGRAM LONG SHADOW — ASSETS ACTIVE: 13`.
- Buttons: `[REPLAY SESSION]` (restart from the intro) · `[SELECT BEAT]` (unlocked checkpoints) · `[END SESSION]` (back to the click gate).

---

## 9. Lighting, time of day, weather and post-processing

### 9.1 The LightStage controller (G6)
`LightStage.s` is a float from 0 to 4. The MissionDirector sets it from **beat progress**: each beat has an `s_start` and `s_end`, and progress is `max(previous, 0.5 × objective completion + 0.5 × critical-path progress)` (objective completion = completed objectives of the beat / total; critical-path progress = `ICriticalPath.Progress01`, spline knots in TECH_PLAN Appendix A). A jump is smoothed over 2 s. Two scripted sequences take `s` over while they run (`ILightStage.Override`): the cinema sky swap (1.00 → 1.35 over 3 s, Beat 4) and MID-A (§8.5). Curves sampled at `s` (keyframes in §9.2a) drive:
- **Sun** (directional): elevation, intensity, colour temperature, shadow on/off and strength, shadow distance. The light's Euler rotation is **(elevation, 78°, 0)**, which places the sun at azimuth 258°.
- **Sun disc** billboard and lens-flare intensity.
- **Sky material** (`Sky/PanoBlend`): texture A/B blend, tint, exposure.
- `RenderSettings.ambientProbe`: a lerp between 4 baked SH probes, one per stage (captured by `AmbientSHCapture` from each stage's sun-free HDRI).
- **Fog** colour and density (built-in exponential squared).
- **Global Volume weights** for VP_Golden, VP_Sunset, VP_Afterglow and VP_BlueHour.
- **Practicals**: the sodium-lamp strike at `s` = 1.97 and the mast aviation lights.
- **AI perception**: sight range and hearing scale (§14.2).
- **Ambience beds**: cicadas give way to crickets, and the generator hum comes in.
- **HUD palette**: amber, then pale green from `s` ≥ 2.0.

A debug slider on `s` is required. It lets one person check visuals and AI together.

### 9.2 Stages

| Stage | `s` | Beats | Clock | Key light | Sky | Fog (exp², density / colour) | Post exposure | Practicals | AI sight |
|---|---|---|---|---|---|---|---|---|---|
| **GOLDEN** | 0.0–1.0 | 1–3 | 17:58–18:20 | Sun 8.0° → 4.5° elevation. Colour 3,400 K → 2,800 K, intensity 2.0 → 1.6. Soft shadows (Medium), 4 cascades, 2048 map, 80 m, strength 0.9. | `kiara_8_sunset` LDR, exposure 0 | 0.0022 / #C9A27A | 0.0 EV | None. Sun shafts and dust motes. | 70 m |
| **SUNSET** | 1.0–2.0 | 4–5 | 18:26–18:40 | Sun 4.5° → 3.0° (cinema swap), 1.5° held through the roofs, then 0° at the end of MID-A. Colour 2,400 K → 1,900 K, intensity 1.4, falling to 0 during MID-A. Shadow strength 0.8 at 60 m, fading out between 0.6° and 0°. | `kiara_8_sunset` warmer (−0.3 EV), blending into `kiara_9_dusk` during MID-A | 0.0028 / #B9805E → #A7788A | +0.4 EV | Sodium lamps strike at `s` 1.97 (18:40), mast lights, the burning technical | 60 m (marksman exempt, §14.2) |
| **AFTERGLOW** | 2.0–3.0 | 6–8 | 18:41–18:58 | **No sun.** A shadowless "sky key" directional from az 258°, elevation 12°, 9,000 K, intensity 0.15. | `kiara_9_dusk`, exposure 0 | 0.0035 / #6E6A8C | +1.0 EV | 4 floodlights, yard sodium, tungsten interiors, muzzle flash | 45 m (70 m in a flood cone) |
| **BLUE HOUR** | 3.0–4.0 | 9–11 | 18:58–19:10 | Sky key 10,000 K, intensity 0.08, no shadows. Flare spots are the only shadow casters. | `kiara_9_dusk` at −1.2 EV, tint #8FA6D8 | 0.0040 / #3A4A6E | +1.5 EV, Purkinje grade | Sodium streets, fires, headlights, 2 flares, rotor lights, chemlights | 35 m (70 m in flare light or headlights) |

### 9.2a LightStage keyframes (authoritative; linear between rows)

| `s` | Moment | Sun el. | Sun int. | Sun K | Shadows (dist / strength) | Sky `_Blend` / tint / EV | Fog density / colour | Story clock |
|---|---|---|---|---|---|---|---|---|
| 0.00 | Beat 1 start | 8.0° | 2.00 | 3400 | 80 m / 0.9 | 0.00 / #FFFFFF / 0 | 0.0022 / #C9A27A | 17:58 |
| 0.15 | Beat 2 start | 7.5° | 1.97 | 3330 | 80 / 0.9 | 0.00 | 0.0022 | 18:08 |
| 0.45 | Beat 3 start | 6.0° | 1.86 | 3180 | 80 / 0.9 | 0.00 | 0.0022 | 18:17 |
| 1.00 | Beat 4 start | 4.5° | 1.60 | 2800 | 80 / 0.9 | 0.00 | 0.0024 / #C59474 | 18:26 |
| 1.35 | cinema swap done | 3.0° | 1.40 | 2400 | 60 / 0.8 | 0.25 / #FFE0C0 / −0.3 | 0.0028 / #B9805E | 18:30 |
| 1.50 | Beat 5 start | 1.5° | 1.40 | 2250 | 60 / 0.8 | 0.25 / #FFE0C0 / −0.3 | 0.0028 / #B9805E | 18:34 |
| 1.80 | MID-A 0:00 | 1.5° | 1.40 | 2100 | 60 / 0.8 | 0.30 / #FFE0C0 / −0.3 | 0.0029 / #B47F68 | 18:38 |
| 1.94 | MID-A 0:12 (watch check) | 0.6° | 0.65 | 1980 | 60 / 0.8 → fading | 0.70 | 0.0031 / #A9788A | 18:40 |
| 1.97 | MID-A 0:14, `PracticalsStrike` | 0.4° | 0.45 | 1950 | 60 / 0.2 | 0.85 | 0.0032 / #A7788A | 18:40 |
| 2.00 | MID-A 0:20, sun 0°: `SUN` off, `SKY_KEY` on | 0.0° | 0 (sky key 0.15, 9000 K) | — | off | 1.00 / #FFFFFF / 0 | 0.0035 / #6E6A8C | 18:41 |
| 2.40 | Beat 7 start | — | sky key 0.15 | 9000 | off | 1.00 / #FFFFFF / 0 | 0.0035 / #6E6A8C | 18:50 |
| 2.80 | Beat 9 start | — | sky key 0.12 | 9500 | off | 1.00 / #C8D2EC / −0.6 | 0.0037 / #54587C | 18:58 |
| 3.20 | Beat 10 start (basement swap done) | — | sky key 0.08 | 10000 | off (flares only) | 1.00 / #8FA6D8 / −1.2 | 0.0040 / #3A4A6E | 19:05 |
| 3.70 | Beat 11 start | — | sky key 0.08 | 10000 | off | 1.00 / #8FA6D8 / −1.2 | 0.0040 / #3A4A6E | 19:10 |
| 4.00 | end | — | sky key 0.06 | 10000 | off | 1.00 / #8FA6D8 / −1.5 | 0.0042 / #34425F | 19:12 |

- Stage volume weights cross-fade over the same `s` ranges (VP_Golden 0–1.0, VP_Sunset 1.0–2.0, VP_Afterglow 2.0–3.0, VP_BlueHour 3.0–4.0), each ramping in over 0.2 of `s` either side of a boundary. AI sight range steps per stage with 2 s smoothing. HUD palette switches at `s` ≥ 2.0. Ambience: cicadas → crickets at 1.97.
- The sky never changes visibly except in MID-A: the 1.00 → 1.35 step happens inside the cinema, the 2.80 → 3.20 step inside the basement.
- Fog densities are starting values. Acceptance test (LookCapture): through the binoculars in Beat 1 the Relay Station and mast (≈ 470 m) must still read clearly; lower the GOLDEN density if they don't.

| Interior | Beat | Light | Local volume |
|---|---|---|---|
| Cinema auditorium | 4 | Near-dark. Dust shafts through shrapnel holes (shaft cards plus 1 unshadowed spot). A flickering EXIT sign. | +1.2 EV |
| Relay Hall | 7 | Tungsten pendants at 2,700 K (baked) plus 2 realtime fills | +0.8 EV |
| Screening room | 8 | Desk lamp, then the projector spot (unshadowed) and a beam cone mesh | +1.0 EV |
| Basement | 9 | Tube emissive at 1,900 K, baked; swapped for the emergency lightmap set (red) | +1.0 EV |
| Booth | Intro, outro | Fluorescent at 4,000 K, baked, plus a realtime flicker; CRT emissive | 0 EV, VP_Booth |

All interior volumes blend over 1.5 s on entry and 1.0 s on exit (G10).

### 9.3 Sky and HDRI pipeline (avoids the "two suns" problem)
- **Download** (approved CC0 source): `kiara_8_sunset_2k.hdr` and `kiara_9_dusk_2k.hdr` (about 6.3 MB each; URLs in `cc0.md` §3), plus their Poly Haven tonemapped JPGs.
- **Alternates:** `rogland_sunset` and `kiara_1_dawn` if the golden sky or horizon doesn't fit. Check the azimuth and elevation of each HDRI's sun before committing.
- **Editor only:** the HDR is used to bake lightmaps (environment = the HDR cubemap rotated so its sun sits at 258°), to capture each stage's ambient SH, and to bake reflection probes.
  - Poly Haven HDRIs are unclipped: the sun is in the map. A python script paints the sun disc out of an editor copy (`*_nosun.exr`), and the realtime directional light supplies the sun (see the double-ambient warning in `cc0.md` §2).
- **Runtime:** the visible sky is the `Sky/PanoBlend` skybox shader.
  - It takes two LDR equirect textures, 2048 × 1024, DXT1 crunched (about 0.7 MB each), with the sun discs painted out.
  - Parameters: `_Blend`, `_Tint`, `_Exposure` and `_Rotation`. The lower hemisphere fades to the fog colour.
  - A `SunDisc` billboard (HDR emissive, so it blooms, 0.53° wide) sits 2.9 km from the camera along the sun direction (camera far plane 3,000 m), **ignores fog**, and is hidden by the **western ridge-silhouette mesh** at 2.5 km during MID-A (crest ≈ 25 m world height along az 240°–275°, i.e. ≈ 0.25° above R4's eye line).
  - Built-in exponential-squared fog would turn anything at 2.5 km into flat fog colour, so the ridge silhouette and far hills use their own backdrop shader: no built-in fog, an aerial-perspective lerp toward the fog/horizon colour capped at 0.8, so they still read as a darker band against the sky.
  - `hdri_prep.py` measures each HDRI's sun (or, for `kiara_9_dusk`, the brightest horizon glow) and sets `_Rotation` so it sits at az 258°.
- **No HDR cubemaps ship.** WebGL lacks BC6H, and an RGBAHalf fallback is large (`cc0.md` §2).
- **Sky swaps only happen with no sky in view:**
  - golden → sunset inside the cinema (Beat 4);
  - sunset → afterglow live in MID-A, where the ridge hides the disc and the change is the cinematic;
  - afterglow → blue inside the basement (Beat 9).

### 9.4 Baking plan (`webgl.md` §4.3)
- **Engine settings:** Progressive GPU lightmapper, **non-directional** lightmaps (the only kind WebGL2 supports), Lightmap Encoding Normal Quality, baked AO.
- **Texel density:** 16 texels/m on hero surfaces, 6–8 on houses, 4 on terrain.
- **Light Probe Groups** run along the playable path every 2–4 m.
- **Reflection probes** are 128 px by default, box-projected indoors; only 4 hero probes are 256 px (booth mirror, Relay Hall, screening room, basement). Baked reflection probes ship as RGBM (HDR Cubemap Encoding = Normal Quality), never as RGBAHalf or BC6H.
- **Sun:** each section bakes with an EditorOnly Mixed (Baked Indirect) `BAKE_Sun` at its stage; at runtime one Realtime `SUN` lights everything (TECH_PLAN D6).
- **Lightmap budget:** sizes below are in **1024²-equivalents** (1 eq ≈ 1.05 MB download as Normal Quality RGBM; a 2048² map = 4 eq). The whole game gets 27 eq plus about 4.4 MB of reflection probes, ≈ 33 MB (TECH_PLAN D16). Texel densities (16 / 6–8 / 4 texels per m) are targets that are lowered per section until it fits.

| Section | Baked at | Lightmaps (1024² eq, max) | Reflection probes | Notes |
|---|---|---|---|---|
| `S1_Approach` | GOLDEN, 6.5° | 2 | 3 | Terrain lightmapped at 4 texels/m |
| `S2_Market` | GOLDEN, 5° | 4 | 4 | Torn-awning shafts are realtime cards, not baked |
| `S3_Square` | SUNSET, 3° | 4 | 4 (2 interior) | The cinema interior bakes with its own lights |
| `S4_Roofs` | SUNSET, 1.5°, **sun bounce off** (sky-only indirect) | 2 | 2 | Bounce at ≤ 1.5° is negligible, so the live fade to 0° stays correct |
| `S5_StationExt` | AFTERGLOW (sky only) | 3 | 3 | Yard sodium baked (non-destructible); the floods are realtime (G4) |
| `S6_StationInt` | Interior tungsten, **plus an alternate emergency set** for the basement group | 2 + 1 | 4 | Lightmap swap at runtime (G11) |
| `S7_Pitch` | BLUE HOUR (sky only) | 1 | 1 | Flares and headlights are realtime |
| `S8_Heli` | — (light probes and the realtime red cabin lamp only) | 0 | 1 | |
| `S9_TownFar` | Baked Lit, fully baked: GOLDEN (7°), SUNSET (1.5°), DUSK (sky only) | 4 + 2 + 1 | 0 | Three sets swapped by `LightmapSwapper`: GOLDEN → SUNSET at `s` 1.35 (inside the cinema), SUNSET → DUSK at 1.97 (hidden by the sodium ripple) |
| `S0_Booth` | Interior | 1 | 1 (mirror) | |

### 9.5 Light budget (per frame, WebGL2 Forward+)

| Source | Mode | Shadows | Max concurrent |
|---|---|---|---|
| Sun (GOLDEN/SUNSET) | Mixed directional | 4 cascades, 2048, 80 m, soft Medium | 1 |
| Sky key (AFTERGLOW/BLUE) | Realtime directional | none | 1 |
| Parachute flare | Spot pointing down (120° cone, which covers the whole pitch from 70 m; a wider spot wastes its shadow map) plus an unshadowed point fill | 1 (FL1, FL2 and FL3 never overlap) | 1 shadowed |
| Floodlights F1–F4 | Realtime spots, destructible | none | 4 |
| Near practicals (sodium near the player, bulb strings, fires) | Realtime unshadowed | none | ≤ 6 per area |
| Muzzle flash | Pooled point lights, 50 ms, range 4 m | none | ≤ 4 |
| Projector / auditorium shaft | Spot | none | 1 |
| Greycoat weapon lights (basement) | Emissive lens plus an additive cone mesh. Only the 2 nearest are real spots. | none | 2 |

The total is at most 12 visible additional lights and at most 1 shadowed local light. Distant lamps are emissive sprites with bloom and cast no light. The player has no flashlight. The player does cast a sun shadow: a full-body, shadows-only proxy of Calder follows the rig (TECH_PLAN D19); the first-person view model never casts shadows.

### 9.6 Post-processing: the 1980s film grade
**URP asset** (`webgl.md` §5.3):
- HDR on, **Color Grading Mode HDR**, LUT 32.
- SMAA High.
- Render scale 1.0 (option 0.75–1.0) with Bilinear upscaling (FSR doesn't work on WebGL2).
- SSAO: Downsample on, Medium samples, intensity 0.6, radius 0.35.
- Decals: Screen Space technique.
- A trimmed PostProcessData that keeps only the Medium1 and Large01 grain textures.

**VP_Base**, applied to every exterior:

| Override | Value |
|---|---|
| Tonemapping | ACES |
| Bloom | intensity 0.6, threshold 1.0, scatter 0.65, lens-dirt texture (ambientCG `SurfaceImperfections003`) at 0.8 |
| Color Adjustments | contrast +12, saturation −8 |
| Split Toning | shadows #2E5C66 (teal), highlights #D9A066 (amber), balance −20 |
| Lift Gamma Gain | lift (0.98, 0.99, 1.02, −0.02), gain (1.03, 1.00, 0.96, 0) |
| Film Grain | Medium1, intensity 0.25, response 0.8 |
| Vignette | 0.25, smoothness 0.4 |
| Chromatic Aberration | 0.10 |
| Motion Blur | camera, 0.15, Low (user toggle, default on) |
| Depth of Field | Gaussian, far-only from 40 m during ADS and dialogue. Bokeh only in cinematics. |

**Stage overrides:**

| Profile | White balance (temp / tint) | Exposure | Saturation | Other |
|---|---|---|---|---|
| VP_Golden | +12 / +2 | 0 | −6 | — |
| VP_Sunset | +18 / +4 | +0.4 | 0 | Bloom 0.8 |
| VP_Afterglow | −5 / +6 | +1.0 | −10 | Split-toning balance −35 |
| VP_BlueHour | −18 / 0 | +1.5 | −22 | Shadows-Midtones-Highlights shadows (0.85, 0.92, 1.10) for the Purkinje shift; bloom 0.9 |
| VP_Booth | color filter #C8D6C0 (green-grey) | 0 | −35 | Grain Large01 at 0.55, lens distortion −0.12 (barrel), vignette 0.35, chromatic aberration 0.2, tube flicker drives exposure ±0.15 |
| VP_Death | — | — | −100 over 0.6 s | — |

The CRT, film and gun-camera looks are shaders on the render textures, not volumes.

### 9.7 Weather and atmosphere
- **Wind:** clear and dry, from the west at 3–5 m/s. Laundry, awnings, dust and smoke all drift east consistently (vertex-wind shader plus particles).
- **Clouds:** high cirrus in the HDRIs catches the sunset colour. There is no cloud simulation.
- **Dust:** dust-mote volumes; light-shaft cards in the grove, the market cross-alleys, the torn awnings, the cinema auditorium and the projector beam.
- **Salt pan:** haze sheets at the LZ; one dust devil visible from the ridge in Beat 1.
- **Not used:** no rain, no sandstorm, no volumetric renderer. Heat shimmer is cut because it would need the Opaque Texture. A depth-based height-fog full-screen pass is optional (P1).

### 9.8 Performance targets
- **Frame rate:** 60 fps on an M-series Mac in Chrome at an internal resolution of ≤ 2.1 MP (e.g. 1920 × 1080). DPR is capped at 1.5, and the default render scale is chosen at startup so that canvas pixels × render scale² ≤ 2.1 MP (the player can raise it in Options). A Retina laptop canvas at DPR 1.5 is about 3.3 MP, so it starts at render scale ≈ 0.8.
- **Geometry:** at most 500k visible triangles and at most 300 batches (SRP Batcher on, GPU instancing for kit pieces and trees).
- **Animation:** the peak is 7 enemies plus 3 squadmates, all skinned. Animators beyond 40 m update at half rate (culling mode, update interval).
- **Audio:** at most 24 voices.
- **Performance mode** (an option, and the Safari fallback): SSAO off, 2 cascades, 50 m shadow distance, render scale 0.8, motion blur off.

---

## 10. Audio direction

### 10.1 VO processing chains
Every chain runs offline (python with `RawAssets/falmedia/tools/fal_post.py`, scipy and soundfile; no ffmpeg needed), because the Web AudioMixer only does volume (`webgl.md` §4.2).

| Chain | Steps | Used by |
|---|---|---|
| DRY | Trim, −18 LUFS, mono, 24 kHz | Squad lines within 8 m; cinematic lines |
| RADIO | `fal_post.py voice --radio` (320–3200 Hz band-pass, tanh drive, 1.6 kHz honk, hiss bed, key-up squelch and release burst), −18 LUFS, mono, imported with a 22,050 Hz sample-rate override | Squad lines beyond 15 m; Magpie; every `radio_filter: true` line |
| INTERCOM | Radio chain with a wider band (250–4500 Hz), no squelch, rotor bed 18 dB under | Beat 11 cabin lines |
| SCRAMBLER | Pitch −3 semitones (phase vocoder; librosa in a scratch venv), 300–3400 Hz band-limit, ring modulation (30 Hz carrier, 15 % wet), 10-bit bitcrush, 50 Hz mains hum at −40 dB | Every booth INTERROGATOR line and the death inserts |
| TAPE | Wow 0.5 Hz ±0.3 %, flutter 7 Hz ±0.1 %, 80–7500 Hz, tape saturation, hiss at −42 dB, motor thump at start | TALLOW briefing tape (`D002`, `D003`) |
| CLEAN | Trim, −18 LUFS, a small concrete-room slap (synthetic 0.25 s IR) | `D090`, `D091` |
| PA HORN | 400–4000 Hz, +6 dB at 1.2 kHz, saturation, multi-tap slapback at 180/420/900 ms (−6/−10/−16 dB). Plays from the horn emitters. | Harrou |
| HF/AM | 200–2800 Hz, AM fading at 0.2 Hz (30 % depth), heterodyne whistle at 1.1 kHz (−30 dB), wow/flutter | The Clockmaker on radio |
| ROOM SPEAKER | 250–5000 Hz, small-cone resonance, concrete IR | The Clockmaker in the basement (`D066`) |
| GREYCOAT SQUELCH | Radio chain plus an alien two-tone squelch tail (1.4 kHz then 2.1 kHz, 60 ms each) | Greycoat barks and click codes |
| BOOTH | Dry plus a 0.18 s concrete slap | Calder |

- **Clockmaker lines** (`D039`, `D066`, `D080`, `D092`, the basement loop) are never generated as whole lines: they are spliced offline from the single-token takes (`VO_CLOCKMAKER_TOKENS`) on a 0.9 s grid, then run through their chain. `D039` is 15 tokens (13.5 s).
- **Interval signal:** 8 clock ticks 0.5 s apart, then the three-note music-box chime; 6.0 s in total. It precedes `D039` (overlapping `D037`/`D038`, §8.5) and `D092`, and loops in the basement.
- **Dry/radio crossfade:** every squad line is baked in both DRY and RADIO. The VOManager crossfades by distance: dry within 8 m, radio beyond 15 m, a blend in between. Squadmates sound next to you when they are, and on the net when they aren't.
- **VOManager:** a priority queue (scripted > objective > callout > bark). Ducking is done by volume.
- **Subtitles:** always shown.

### 10.2 Sound effects
- **Weapons** follow the layering in `fal_media.md` §3.2: a Mirelo SFX 2.0 transient plus an ElevenLabs SFX v2 body, with close, mid and far layers.
  - Each weapon has **3 baked zone tails**: narrow-street slapback, open square, interior room. Trigger volumes pick the tail set.
  - Every gun also gets a baked `_far` low-passed variant.
  - Enemy rounds passing within 3 m play **supersonic cracks and whizzes**.
- **Footsteps** by surface: sand, gravel, stone paving, roof plaster, tile/terrazzo, wood, metal stair, salt crust.
- **Foley:** cloth and gear scaled to movement speed.
- **Greycoat squelch code:** 1, 2 or 3 clicks, with variants.
- **Set-piece sounds:** sodium ballast buzz, floodlight die-off, projector clatter, tube shatter, safe drill, satchel, bolt cutter, the curfew siren, rotor (loop, approach, flyby), helicopter alarm, teletype, CRT degauss, and the stuck clock tick.
- **Ambience beds** change with the LightStage. Golden: wind, cicadas, distant dog, siren tail. Sunset: cicadas stop, crickets start, sodium buzz. Station: generator hum. Blue hour: fire crackle, distant shouting, salt-pan wind.
- **Anything with voices or music is built from our own material, never generated as "town" audio**, so no real language, song or religious sound can slip in: distant shouting is layered from our Legion bark takes through the FAR chain; the muffled TV is our own invented-language takes through a through-the-wall chain; the radio behind a shutter is `AMB_CASSETTE_TUNE` through a tiny-speaker chain. Every generated ambience prompt says "no voices, no singing, no music, no bells", and the listen pass (O4) rejects any take with chanting, a call to prayer, bells, hymn-like or regional song, or intelligible speech.

### 10.3 Music cues
Generate with `elevenlabs/music/v2.5`, `force_instrumental`, 60–90 s (billing is per started minute). `MUS_01` takes the synthesised motif (`MOTIF_MUSICBOX`) as its `audio_reference`; every later cue takes the motif plus the approved `MUS_01` take, so they share one palette. Cut loops on bar lines before the generated outro (`fal_post.py loop`) and test the seams in the browser build (`fal_media.md` §6).

| ID | Use | Brief | Loop |
|---|---|---|---|
| `MUS_01_SESSION` | Intro, booth | Analog synth drone, **the three-note music-box motif**, tape hiss, slow 72 BPM pulse | The last 30 s loops |
| `MUS_02_MARKET` (LO / HI) | Beats 2–4 | Cold War action at 110 BPM: sequenced bass arpeggio, gated toms, string machine. HI adds percussion and synth-brass stabs. | Bar-cut loops; layers crossfade |
| `MUS_03_LASTLIGHT` | MID-A | Strings swelling over synth pads, the motif carried by the strings, resolving into silence | One-shot |
| `MUS_04_STATION` | Beats 6–9 | Dark 96 BPM pulse, a ticking hi-hat like a clock; low and full layers | Bar-cut loops |
| `MUS_05_EXFIL` | Beats 10–11 | Driving 120 BPM, a rotor-like pulse, a heroic-but-wrong minor key | Loop |
| `MUS_06_SIXFORTY` | Outro | The music box alone, detuned, then silence | One-shot |
| Stingers | — | `STG_TITLE` (teletype plus a low hit) · `STG_GLITCH` (tape-stop plus a static burst) · `STG_GREYCOAT` (a low synth-brass swell) · `STG_DEATH` (tape rewind) | — |

**The motif** is the Clockmaker's interval chime: three music-box notes, an original melody. Station, score and outro all share it.

---

## 11. HUD and UI

- **Canvas:** reference resolution 1920 × 1080, Scale With Screen Size (match 0.5).
- **Fonts (OFL/Apache, from Google Fonts, which needs approval O1):** a typewriter face for objectives and title cards, a condensed sans (for example Barlow Condensed) for HUD numerals, and a monospace face for the Clockmaker. Until O1 is approved, every role uses TMP's bundled LiberationSans SDF; the invented Sardani glyph font is generated by us and needs no download.

| Element | Spec |
|---|---|
| **Compass strip** | Top center, 640 × 28 px. Shows the 180° in front of the player, with ticks every 5° and labels every 45° (N, NE, E…). Objective diamonds and squad dots (green) are projected onto it by bearing. |
| **Objective marker** | A 28 px diamond with a short label and the **distance in metres** (1 m steps below 100 m, 5 m steps beyond). Clamps to the screen edge with an arrow when off-screen. Fades to 30 % while ADS. Hidden within 4 m. **Amber #E3A33B before sunset and pale green #A6E3B4 after** (`s` ≥ 2.0). **Red #E04B3B** marks high-priority targets (the technical, the RPG gunner, the transmitter racks) and shows through walls. A FOLLOW chevron variant sits over a squadmate. |
| **Objective text** | Top-left, revealed teletype-style: `OBJ 4/11 — 47219 — NEUTRALIZE THE TECHNICAL` (a beat counter, a 5-digit flavour group, then the text). Tab shows the full list with intel counts. |
| **Ammo and grenades** | Bottom-right. Weapon name (`COMMANDO`), magazine and reserve in large numerals (`30 / 120`), a fire-mode icon, a flash below 25 % of the magazine, and `R — RELOAD` at 0. Frag icons (×3) beside it. |
| **Crosshair** | A dynamic hip-fire cross (4 lines that spread with weapon spread). Hidden while ADS (iron sights) and while sprinting. |
| **Hit markers** | Center, 4 diagonal ticks, 0.12 s. A kill turns them red and 20 % larger for 0.2 s. A headshot adds a distinct click. |
| **Damage direction** | Red arcs on a 160 px ring around the center, pointing at the source bearing, fading over 1.5 s. A **grenade indicator** (frag icon with an arrow) appears on the same ring when a live grenade is within 8 m. |
| **Health** | No bar. A screen-edge dust-and-blood vignette and desaturation below 50 %; heartbeat and muffled hearing below 25 %. |
| **Radio subtitle box** | Bottom center, at most 1,100 px wide, 2 lines. Format: `[SUNDIAL-ACTUAL] HOLLIS: Off the middle of the street.` Radio lines get a small radio-wave icon. Speaker colours: Hollis #E8C77A · Sparrow #9FD3C7 · Tinker #C9B3E6 · Magpie #B7C4D6 · Interrogator/TALLOW #E6E6E6 (italic in the booth) · Clockmaker #7FE07F (monospace) · Harrou PA #E09A6A · Legion #E0705A · Greycoat #A0A8A0. Enemy barks are subtitled only within 25 m and only when "Translate enemy chatter" is on (default on, as Sparrow's translations): `[LEGION] (shouting) Push! Push!` |
| **Squad nameplates** | The green callsign appears over a squadmate's head when looked at within 25 m |
| **Prompts** | Center-bottom: `[E] Thread the projector`, `HOLD [E] Take the ledgers` (with a radial fill), and the one-time tutorial prompts. Interact radius 1.8 m, with a line-of-sight check. |
| **Intel card** | A polaroid slides in from the right for 2.5 s: photo, name, `INTEL 1/3` (or `OPTIONAL 1/2`) |
| **Special overlays** | Binoculars (Beat 1): reticle with range ticks and a tick-off on each POI. Door gun (Beat 11): ring sight and a heat bar that goes white → red. World-space rings for drill progress (Beat 9, %) and the LZ hold (Beat 10, Magpie's distance in metres). |
| **Click-to-start** | §8.1 |
| **Pause menu** | Opens on pointer-lock loss (Esc). A dark CRT panel with: **RESUME** (shows a "Click to resume" overlay, because Chrome blocks re-locking for about 1 s) · **RESTART CHECKPOINT** · **OPTIONS** · **QUIT TO SESSION** (back to the click gate) |
| **Options** | **Mouse sensitivity** 0.1–10.0, default 3.0: rotation in degrees = delta in CSS px × sens × 0.03, so 3.0 gives 9° per 100 CSS px. The delta is normalized by `devicePixelRatio`, with raw input when the browser grants `unadjustedMovement`. **ADS sensitivity** ×0.5–1.5, default 0.85, also scaled by the FOV ratio · invert Y · **FOV** 75–105° horizontal, default 90 · volume for Master, Music, Effects and Voice (category gains applied to pooled `AudioSource.volume`; there is no AudioMixer, TECH_PLAN D9) · subtitles on/off, size S/M/L, translate enemy chatter · **Performance mode** · render scale 75–100 % · motion blur · film grain 0–100 % · brightness (−0.5 … +0.5 EV) · fullscreen · difficulty (applies at the next checkpoint) · a controls list. Saved in `localStorage` inside try/catch, falling back to defaults. |
| **Fail cards** | Death: the death insert (§8.4). Out of bounds: `YOU ABANDONED YOUR TEAM`, then the checkpoint. |

---

## 12. Controls and player movement

| Action | Input |
|---|---|
| Move / look | WASD / mouse |
| Fire / aim (ADS) | LMB / RMB (hold; a toggle option exists). In Beat 1, RMB raises the binoculars. |
| Reload | R |
| Sprint | Left Shift (hold) |
| Jump / mantle | Space |
| Crouch (tap) / slide (while sprinting) | **C**. Never Ctrl, because Ctrl+W closes the tab (`webgl.md` §4.8). |
| Frag (hold to cook) | G |
| Melee (knife) | V |
| Interact | E (hold for pickups) |
| Switch weapon | 1 / 2, mouse wheel |
| Objectives | Tab |
| Pause | Esc (the browser releases pointer lock, which opens the pause menu) |
| Skip cinematic | Hold Space for 1 s |

**Movement numbers:**
- **Speeds:** walk 4.4 m/s; ADS walk 2.6; crouch 2.4; sprint 6.9, unlimited.
- **Sprint-out-to-fire:** 0.20 s, or 0.32 s with the RPK.
- **Slide:** entered at ≥ 5.5 m/s with C. Lasts 0.75 s at 8.0 → 4.0 m/s with a 0.6 m camera dip, and hip-fire is allowed.
- **Jump and mantle:** jump apex 1.0 m. Ledges of 0.6–1.4 m auto-vault in 0.45 s when you move into them. Ledges of 1.4–2.1 m are climbed in 0.8 s by pressing Space. **Air mantle:** while airborne, a ledge whose top is 0–1.4 m above the feet and ≤ 0.8 m ahead is grabbed and climbed in 0.5 s (this is what makes the 2 m roof gaps reliable).
- **Fall damage:** none below 4.5 m, then 30 HP per metre above that. The roof gaps are safe.
- **Capsule:** 1.8 m tall. Eye height 1.65 m standing, 1.05 m crouched.
- **Head bob and sway:** 1.2 cm vertical and 0.8 cm lateral at walking pace, scaling with speed.
- **First-person body:** the Rocketbox Calder soldier on the view-model layer with the head, neck and both thighs scaled to 0 (arms, hands and upper chest only; no legs, because the view-model camera has its own FOV and draws over the world), arms IK-driven onto the weapon sockets, and procedural sway, kick, ADS and reload motion (`cc0.md` §7.3, `fal_3d.md` §6.4). A second, full-body copy renders **shadows only** on the world layer and plays simple locomotion (idle, walk, run, crouch, aim pose), so the player casts a correct sun shadow (TECH_PLAN D19).

---

## 13. Weapons

The default FOV is 90° horizontal, about 58.7° vertical at 16:9. ADS FOVs are given in vertical degrees at that default and scale with the player's FOV setting. Weapons use generic display names for real period patterns; the patterns are used to brief asset generation (`fal_3d.md` §6.2).

### 13.1 Player weapons

| | **COMMANDO** carbine | **HUSH PUPPY** suppressed pistol | **AK** rifle | **RPK** LMG |
|---|---|---|---|---|
| Pattern | XM177E2 / CAR-15 | S&W Mk 22 Mod 0 | AKM | RPK (75-round drum) |
| Slot / source | Primary, at start | Secondary, at start | Pickup (Legion riflemen) | Pickup (Legion gunners, first in Beat 3) |
| Damage, close → far | 34 → 26 | 40 → 25 | 42 → 33 | 42 → 35 |
| Falloff range | 25 → 45 m | 12 → 30 m | 22 → 45 m | 25 → 50 m |
| Damage multipliers | head ×2.0, limbs ×0.85 | head ×3.0, limbs ×0.85 | head ×2.0, limbs ×0.85 | head ×2.0, limbs ×0.85 |
| Fire mode / RPM | Auto / 750 | Semi, capped at 300 | Auto / 600 | Auto / 600 |
| Magazine / max reserve | 30 / 150 | 8 / 48 | 30 / 120 | 75 / 150 |
| Reload, tactical / empty | 2.1 s / 2.7 s | 1.6 s / 2.0 s | 2.4 s / 3.0 s | 4.2 s / 5.0 s |
| ADS time | 0.22 s | 0.15 s | 0.26 s | 0.38 s |
| ADS FOV (vertical) | 47° (1.25×) | 52° (1.13×) | 46° (1.28×) | 46° (1.28×) |
| Spread, hip / ADS (°) | 3.0 / 0.15 | 2.0 / 0.25 | 3.6 / 0.30 | 4.5 / 0.35 |
| Recoil per shot (ADS) | 0.55° vertical, drifting right +0.12° (±0.18 random). First shot 0.8°. | 1.4° vertical, ±0.3 | 0.85° vertical, ±0.35 horizontal (wild). First shot 1.1°. | 0.6° vertical, ±0.25. Settles to 0.45° after 8 shots. |
| Recoil pattern | The first 8 shots are fixed: up, then right, then hooking slightly left at shots 6–8. Randomized after that. | Single kick, snaps back | The first 6 are fixed: up, left, right, left. Randomized after that. | The first 8 are fixed: steep climb, then flattening |
| Recovery | 8°/s after 0.08 s | 12°/s | 6°/s | 7°/s |
| Move speed | ×1.00 | ×1.05 | ×0.97 | ×0.88 |
| Body hits to kill a Legion rifleman (100 HP), close / far | 3 / 4 | 3 / 4 (1 headshot) | 3 / 4 | 3 / 3 |
| Noise (alert radius) | 60 m | **6 m** | 60 m | 60 m |

- **Visual kick:** the camera takes 30 % of the aim kick. The weapon model kicks back 2.2 cm in Z and recovers with a spring.
- **Reload animation:** procedural from keyed transforms (tilt, magazine out, magazine in, charge), with left-hand IK following the separately generated magazine.
- **Slots and pickups:** slot 1 is the primary (Commando, AK or RPK), slot 2 is always the Hush Puppy. Every dead Legion rifleman drops an AK, and E3-05 and E6-05 drop RPKs (E2-06's stays on its mount). Greycoat AKMS and the SVD, RPG-7 and DShK never drop. Walking over a pickup of the weapon you carry tops up its reserve automatically; `HOLD [E] Take <weapon>` (0.4 s) swaps it into slot 1 and leaves your old primary on the ground. At most 6 dropped weapons exist at once (oldest despawns first), and drops in an unloaded section are gone. The Commando's ammunition comes only from the 5 resupply crates (§13.2).

### 13.2 Grenades and melee
- **Frag** (M67 pattern): 3 at start, 4 max.
  - **Fuse and throw:** 4.0 s fuse from the pin pull. Hold G to cook; fuse ticks show on the HUD. Throw range 18 m.
  - **Damage:** 220 at the center, lethal within 4.0 m, damage out to 8.0 m with linear falloff. Cover blocks it (raycast).
  - **Resupply crates** refill ammo and frags (`HOLD [E] Resupply`, 1.0 s, unlimited uses): Weighbridge (182, 0, 150) · market arch (184, 0, 247) · roof R4 (288, 12, 330) · basement (322, −4.2, 355) · LZ (338, −1, 389).
- **Knife** (V): 150 damage, 1.6 m reach with a 1.0 m lunge, 0.6 s.

### 13.3 Door gun (Beat 11 only)
- **Fire:** M60D pattern, 550 RPM, 60 damage per hit, infinite ammo, 0.6° spread, red tracer every 3rd round.
- **Heat:** +1 per shot, overheats at 55 (about 6 s of fire). Recovers 2.0 s after an overheat, or 1.0 s to full when idle.
- **Arc:** yaw ±70° around the left-door normal, pitch +15° / −60° (the trucks in Clock Square are engaged at −45° to −54°).

### 13.4 Enemy weapons (world-only models, 3–7k tris, 1K textures)
- AK (riflemen) · RPK (gunners) · AKMS (Greycoats) · SVD pattern (marksman) · RPG-7 pattern (RPG) · DShK (technical).
- **Tracers:** Legion and Greycoat fire is **green**; SUNDIAL and the door gun are **red**. Machine guns show 1 tracer per 3 rounds, rifles 1 per 5.

---

## 14. Enemies

### 14.1 Archetypes (Regular)

| Archetype | Count | HP | Weapon | Damage per hit to the player | Fire behaviour | Accuracy at 20 m | Range | Movement and cover | Grenades | Tell / special |
|---|---|---|---|---|---|---|---|---|---|---|
| **Legion Rifleman** | 38 | 100 | AK | 20 | Bursts of 3–6 with 0.6–1.2 s pauses | 0.30 | 10–35 m | Sloppy: takes half cover, peeks predictably, pushes when the player is suppressed | A shared Legion pool: 1 throw per 60 s mission-wide | Orange armband. Morale break: when 60 % of a wave is dead the rest fall back one node (`BK_LEG_09`). |
| **Legion Gunner** | 3 | 130 | RPK | 16 | Long bursts of 8–15, suppressing | 0.22 | 20–50 m | Perch nodes only (truck bed, balcony, gatehouse roof) | none | Long green tracer streams |
| **Legion RPG** | 1 | 100 | RPG-7 | 90 direct; 4 m splash with falloff | 1.2 s telegraph (shout plus raise), fire, 8 s reload | — | 30–80 m | Static on the truck bed | none | Scripted targets: the squad, then Magpie |
| **Technical Gunner** | 1 | 100 (head exposed) | DShK | 35 | 3-round bursts, 550 RPM, traverse 40°/s | 0.25 | 20–70 m | Static | none | Gun shield makes him immune within ±60° of his facing |
| **Greycoat Advisor** | 11 | 150 (vest: torso ×0.8) | AKMS | 24 | Controlled bursts of 2–3 | 0.45 | 8–30 m | Works in pairs: one suppresses while the other takes the designated flank route. Full cover, moves every 4–6 s. | 1 each, thrown after the player stays in the same cover for 6 s | **Squelch clicks:** 2 before moving, 3 on spotting the player. Always flees frags. One LZ variant wears NVG with a green lens glow. |
| **Greycoat Marksman** | 1 | 120 | SVD pattern | 70 | 1 shot per 3.5 s, **glint 1.5 s before each shot** | 0.8 if the player is still, 0.35 moving, 0.2 sprinting | 60–160 m | Perched | none | Withdrawn at 18:40 |
| **Rooftop Target** (Beat 11) | 4 | 60 | AK | 20 × 0.25 | Stand, fire, die | 0.30 | — | Static | none | — |

Per beat: B2 6 · B3 9 · B4 8 · B5 3 · B6 8 · B7 5 · B9 9 · B10 7 · B11 4 = **59**.

### 14.2 Perception
- **Sight range by LightStage:** 70 / 60 / 45 / 35 m (GOLDEN / SUNSET / AFTERGLOW / BLUE). Doubled, up to a 70 m cap, while the player is inside a flood cone, flare light or headlights.
- **Exceptions:** the Greycoat Marksman sees 160 m through his scope whatever the stage; the Technical Gunner's fire at the arch and the RPG gunner's fire at the bleacher and at Magpie are scripted and ignore perception; Rooftop Targets (Beat 11) are always aware of the helicopter.
- **Field of view:** 120°, widening to 160° within 8 m.
- **Detection time:** 0.4–1.2 s depending on distance. Crouching makes it ×1.5 slower, sprinting ×0.6 faster.
- **Hearing radii:** unsuppressed shot 60 m · Hush Puppy 6 m · frag 80 m · squad gunfire 40 m · sprint footsteps 12 m · walk 4 m · crouch 1.5 m.
- **Suspicious state** (a detection meter on a sentry's HUD icon) is used only in Beat 2. Everywhere else, enemies start in combat or idle → combat.

### 14.3 Combat AI
- **State machine:** Idle/Patrol → (Suspicious) → Combat → {Suppress | Flank | Fallback} → Death. On death, the death clip blends into a ragdoll after 0.2 s.
- **Attack tokens:** at most *N* enemies fire at the player at once (Regular 3). The rest shoot at squadmates (who can't be hurt) or reposition. Tokens rotate every 2–4 s, and the enemy the player is aiming at gets one first, so the fight feels responsive.
- **Hit chance per shot** = base accuracy × range curve × player motion (still 1.0, walking 0.8, sprinting 0.6, sliding 0.5) × in-cover factor (0.6) × time-on-target ramp (0.5 → 1.0 over 2 s, reset when line of sight breaks) × flood bonus (1.25) × difficulty. **Misses land 0.5–3 m from the player**, so they whiz and crack for drama.
- **Cover:** hand-placed `CoverNode`s per beat, each with stand or crouch, lean left/right and a facing arc. An enemy picks the nearest unclaimed node that breaks line of sight to the player but still allows a peek. Legion re-evaluate every 6–9 s, Greycoats every 4–6 s.
- **Flanks:** designated NavMesh routes per encounter (Greycoat pairs, Beat 3 wave B, Beat 10 NVG flanker).
- **Grenade avoidance:** Legion flee a frag within 5 m 60 % of the time, Greycoats every time.
- **Suppression:** rounds landing within 2 m of the player add suppression (vignette plus 15 % aim sway for 1 s). Squad fire on an enemy suppresses it: fire rate and accuracy ×0.5 for 2 s.
- **Spawning:** spawn closets only, out of sight and at least 15 m away. Hard alive cap 12. Stragglers left two beats behind are despawned.
- **Navigation:** AI Navigation NavMesh per section. Rooftop enemies use perch nodes only, with no roaming on roofs.
- **Animation:**
  - Bodies: Rocketbox.
  - Locomotion and reactions: Quaternius UAL1/UAL2 and Unity SAC strafes.
  - Rifle handling: Meshy rifle clips (aim, fire, reload, deaths). The ids to buy are the sets in `ASSET_MANIFEST.json` (`ANIM_MESHY_OWNED`, `_SET_A`, `_SET_B`, `_SET_C`); `cc0.md` and `fal_3d.md` number some clips differently, so every id is resolved against the live Meshy library before purchase. Test `_SET_A` first and check hand placement on a Rocketbox body (`cc0.md` §7.5).
  - Aim and left-hand IK via Animation Rigging; ragdoll from the Ragdoll Wizard on the Biped bones.

### 14.4 Invented languages (replaces the pitch lexicon; see §0.4)

**Legion cant**, a harsh shouted trade pidgin (`BK_LEG_01`–`15`):

| Bark | Meaning | Bark | Meaning |
|---|---|---|---|
| Tovek! | There! / Contact! | Fesko! | Lights! |
| Druna, druna! | Push! | Dovvo! | Fall back! |
| Vossek! | Go around! / Flank! | Zaroud-ka! | For Zaroud! |
| Pirrha! | Grenade! | Grakko (vel)! | Americans! |
| Ollo, ollo! | Reloading! | Brekka! | Hurry! |
| Tevo vrask! | Cover me! | Nehh! | (pain) |
| Mennik! | Man down! | Ruvo tesk, ollan? / Ollan, ollan. | Got a light, friend? / Sure, sure. |

**Greyspeak**, low and clipped, always over the Greycoats' own radios with the alien squelch tail (`BK_GRY_01`–`09`): `Hukt!` contact · `Trekt, isk!` suppress, move · `Drezo!` hold · `Ulsk-tavv` second team · `Kisk!` grenade · `Ottrek` marksman · `Selt` clear · `Orvak` eyes on · `Vaun dret!` fall back · `Nolvek` it is time (said once).

**Squelch code:** 1 click = hold · 2 = move · 3 = target sighted, a warning to the player.

**Sardani**, used for Harrou's PA only: `Ilo Zaroudan` people of Zaroud · `veskri kael` stay inside · `Morrhan uvan talvi-sa` the curfew protects you.

All words are built from invented syllables. Before recording, each word goes through the web check in §2.3 (5). Any take that sounds like real speech is rejected. *(2026-10-16, WP06_VO web check: `ras`, `os`, `tor`, `om` and `veshti` had real meanings (Arabic, Spanish/Portuguese, Hebrew/Turkish/German, a sacred syllable, Bulgarian/Tamil) and were replaced by `vrask`, `isk`, `tavv`, `uvan` and `veskri`. Resumed run, sound-alike check on the transcripts: `Sauth` is heard as English "south", `Ekt-ha` as Hindi "ek tha" ("there was one") and `Ivvo` as the Slavic name "Ivo", so they became `Ulsk`, `Orvak` and `Ottrek`; see `Docs/AUDIO_REVIEW.md`.)*

---

## 15. Squad AI

- **Invulnerable**, and there is never a squad fail state. The player's bullets pass through squadmates. Enemy rounds hit them for VFX only.
- **Node chains:** each beat has authored node sets (M1–M4, Q1–Q2, Y1–Y3, P1 and so on), each giving one node per squadmate. A set advances on any of: the player entering a volume, a kill-count threshold, an objective event, or a radio line finishing. Within the node radius (1.5 m) each squadmate picks a cover pose: crouch, stand or lean.
- **Follow mode** (no chain active): file along the critical-path spline. Hollis leads 6–10 m ahead and the others stay 3–6 m from the player. They wait at corners and doors (never more than 12 m ahead).
- **No blocking:** a squadmate sidesteps within 0.3 s when the player comes within 1 m (yield plus push-through collision), and never stands in a doorway (NavMesh area `Doorway`). The only exception is a node set flagged `allowDoorway` (`B8.NodeSet.Reel`, Hollis at the screening-room door); he still yields to the player.
- **Section seams:** squadmates cross between section NavMeshes only through the seam links listed in TECH_PLAN §7.15; a scripted move across a seam that is not loaded yet waits at the seam node.
- **Catch-up:** more than 25 m behind and off-screen for 2 s means a teleport to the nearest valid node behind the player.
- **Combat and kill share (about 25 %):** squadmates engage enemies within 40 m. Their shots only kill enemies flagged `squadTarget`: scripted kills such as Beat 2's right sentry, plus any Legion soldier the player hasn't engaged for 8 s. Against everyone else their fire suppresses (§14.3), so the player does most of the killing.
- **Frags:** each squadmate may throw 1 frag per beat at a cluster of 2 or more enemies away from the player (`CO_*` "Frag out!"), with a 30 s cooldown.
- **Callouts:**
  - Pools: `CO_HOL_*`, `CO_SPA_*`, `CO_TIN_*`, ranked below scripted VO, with a 4 s global cooldown and a 20 s cooldown on reload callouts.
  - Direction is relative to the player's view: front ±45°, left, right, rear. "Rooftop", "Balcony" and "Window" fire from the enemy's perch tag.
- **Look-at:** heads track the current speaker in dialogue, the player within 3 m, and threats in combat (Animation Rigging multi-aim). A beat script can override all three with `ISquadService.SetLookTarget` (MID-A: the sun when the player looks west).
- **Scripted actions:**
  - Tinker: bolt cutter (B2), white smoke (B3), satchel (B6).
  - Hollis: fist signal (B5), green smoke (B10).
  - Sparrow: camera (B1, B8), drill (B9, UAL `Fixing_Kneeling` loop).
  - All: chemlights (B6).
- **Locomotion:** strafe blend tree (Unity SAC) plus UAL jog, sprint and crouch, UAL2 `ClimbUp_1m` and Meshy `Vault_with_Rifle` over gaps. The AI never slides.

---

## 16. Difficulty, health and regen

| | Recruit | **Regular** (default) | Hardened | Veteran |
|---|---|---|---|---|
| Player HP | 100 | 100 | 100 | 100 |
| Enemy damage multiplier | 0.5 | 1.0 | 1.5 | 2.2 |
| Legion AK hits to down the player | 10 | 5 | 4 | 3 |
| Regen delay after the last damage | 3.0 s | **4.0 s** | 5.0 s | 5.5 s |
| Regen rate | 60 HP/s | **50 HP/s** (about 2 s to full) | 40 HP/s | 35 HP/s |
| "Last stand" immunity on dropping below 20 HP (once per regen cycle) | 1.5 s | 0.8 s | 0.4 s | none |
| Attack tokens | 2 | 3 | 4 | 5 |
| Enemy accuracy multiplier | 0.7 | 1.0 | 1.15 | 1.3 |
| Shared Legion grenade interval | 90 s | 60 s | 40 s | 30 s |
| Greycoat grenade trigger (player static in one cover) | 10 s | 6 s | 5 s | 4 s |
| Marksman damage per hit | 45 | 70 | 90 | 100 (lethal) |

The player's damage to enemies is the same on every difficulty. Health feedback is described in §11.

---

## 17. Failure, checkpoints and retry

- **Fail conditions:**
  1. Player death.
  2. Out of bounds. Entering an OOB volume starts a `RETURN TO THE MISSION AREA 6…` countdown and plays `D093`. At zero, the `YOU ABANDONED YOUR TEAM` card shows and the checkpoint reloads.
- **No escort or objective fails:**
  - Squadmates can't die, and Sparrow's drill can't fail (it only pauses).
  - Magpie can't be shot down; an RPG only forces a wave-off.
  - A blown sync shot only alerts the gate.
- **Checkpoints:** CP1–CP10 at beat starts, plus CP3b at the market's Crossing. They save automatically. A save is **deferred** (up to 10 s) while an enemy is within 10 m, a grenade is live, or the player is below 50 HP. A deferred save always commits before the next beat's first spawn.
- **Snapshot contents:** beat and sub-state, LightStage `s`, objective and intel flags, the weapons, ammo and frags the player carries (frags topped up to at least 2), squad positions (teleported to the checkpoint nodes), and encounter state (enemies killed before the checkpoint stay dead; open waves reset to their trigger state).
- **On death:** the death insert (§8.4), then a reload at full health. Music returns to the beat's cue. The set of played lines is part of the snapshot: lines that played before the checkpoint was saved do not play again, lines after it play again as the beat re-runs. Objective-critical lines repeat even if they played before the checkpoint, whenever their objective is still open after the reload: `D014`, `D029`, `D034`, `D048`, `D053`, `D059`, `D065`, `D067`.
- **Out-of-bounds volumes:** the tile edge outside the playable path (ridge flanks west and south of A1, grove south and west edges, the salt pan north and east of A16 beyond 6 m from the pitch), every reachable housing roof outside A9, and the zone in front of each backtrack blocker (§6.7). Each sits 4 m in front of a hard PlayerClip, so the countdown always has room to turn around.
- **Restart Checkpoint** (pause menu): the same reload, without the death insert.
- **Persistence:** the highest checkpoint reached and the options are kept in `localStorage` (try/catch). `SELECT BEAT` unlocks on the debrief after the first completion.

---

## 18. Production notes

### 18.1 Packages to add
The authoritative list, with versions, is TECH_PLAN §2.1: `com.unity.timeline`, `com.unity.ai.navigation`, `com.unity.splines`, `com.unity.animation.rigging`, `com.unity.addressables` 2.x, `com.unity.recorder` (editor-only, for the pre-rendered CRT clips) and `com.unity.test-framework`. **Not added:** glTFast (static meshes are converted offline to OBJ, D2; glTFast 6.20.0 is only the fallback) and Cinemachine (D3).

TextMeshPro is part of `com.unity.ugui` 2.x. Every package change goes through the Unity MCP package tools. **Never call the `Unity_AssetGeneration_*` tools** (the user has no Unity AI credits).

### 18.2 Asset sourcing map

| Need | Source | Ref |
|---|---|---|
| Every human body | **Microsoft Rocketbox** (MIT, credit it), with the import-script fix (`usingMixamoAnimations = false`, Humanoid). A PIL retexture pass converts the ACU kit to 1984 kit, and flags and name tapes are painted out. Legion: Military_Male_02/05/06. Greycoats: Military_Male_03/04 (helmets and goggles hide the faces). Tinker: Male_Adult_05. Sparrow: a Rocketbox female. Interrogator and Harrou: business males. Faces are spread across both sides. **Only Military_Male_01 (Calder) is fully on disk; Male_Adult_05 has its mesh but no textures. Every other avatar comes from raw.githubusercontent.com, which needs the user's approval (TECH_PLAN O2).** If O2 is refused, every role except Calder is built with the Meshy fallback (`CHR_FALLBACK_MESHY`: Nano Banana reference sheet → `meshy/v7.1/multi-image-to-3d` with rigging; 24 bones, no fingers, no jaw), so the climax jaw flap on Hollis is dropped and the interrogator's gloved hand stays open. | `cc0.md` §7.3, §7.6; `fal_3d.md` §6.4 |
| Locomotion, crouch, throw, slide, mantle, hits, deaths | Quaternius UAL1 + UAL2 (CC0) and Unity SAC strafes (UCL) | `cc0.md` §7.2 |
| Rifle aim, fire and reload, rifle deaths, vault | `fal-ai/meshy/rigging/multi-animation` on the UAL mannequin (test 10 clips first) | `cc0.md` §7.5 |
| Booth sitting and gestures | Local Rocketbox `m_sit_table_idle_nervous_01` (34 s, Calder) and `m_walk_neutral_01`, UAL2 `Idle_TalkingPhone_Loop` (phone), IK reaches for the toggle and the head turn. The extra Rocketbox `m_sit_table_*` / `m_gestic_*` set is a P1 nice-to-have behind O2. | `cc0.md` §7.3 |
| Hand signals | IK-posed upper-body clips, or `fal-ai/hunyuan-motion` | `cc0.md` §7.4 |
| Weapons | Nano Banana 2.1 side profile → `tripo3d/p2/image-to-3d` (20k tris for FP, 3–7k for world), with the magazine generated separately; Hunyuan Pro multi-view for the Commando (the hero gun) | `fal_3d.md` §6.2 |
| Props | **Poly Haven first:** crates, barrels, jerrycans, CRTs, radio transceiver, office desk, chairs, lamps, `filmstrip_projector_8mm` (as the base for the 16 mm projector), `projector_screen`, fluorescent fixtures, `wall_clock`, `vintage_lighter`, binoculars, generator, searchlight, barriers, ladder. **fal for the gaps** (Tripo P2 at 1.5–5k / Hunyuan Rapid): sandbags, PRC-77, DShK technical pickup, cargo trucks, a utility helicopter in the style of a Bell 212, tube transmitter racks, teletypes, safe and drill, olive and pomegranate trees, horn speakers. | `fal_3d.md` §6.3, `cc0.md` §6, §8 |
| Materials | Poly Haven core set (`sand_01`, `aerial_sand`, `clay_plaster`, `painted_plaster_wall`, `sandstone_blocks_05`, `concrete_wall_007`, `corrugated_iron`, `weathered_planks`, `terrazzo_tiles`, `old_linoleum_flooring_01`…) plus ambientCG gap fills (`Ground097`, `PaintedPlaster012`, `CorrugatedSteel006A`, `Fabric066`, the decals). **Crunch everything.** 2K only for the 4 hero materials (`sand_01`, `painted_plaster_wall`, and the booth's `PaintedPlaster012` and `concrete_floor_worn_001`); everything else is 1K with a shared tiling detail normal for close-ups. Standard URP/Lit with an offline-packed mask map (no `Lit_ARM` Shader Graph, TECH_PLAN D4). Per-class texture caps in TECH_PLAN §8.9 keep the build under 230 MB. | `cc0.md` §2, §4–5; `webgl.md` §4.4 |
| Skies | `kiara_8_sunset`, `kiara_9_dusk` | `cc0.md` §3 |
| Images | Nano Banana 2.1 raw photos (map, aerial recon, airport plate, ledger and codebook pages), with text and overlays set live in TMP; the roster page, team ID photos and the Sparrow surveillance still are composed in the editor. Posters and signs use the invented glyph set rendered in-engine. Every town or crowd prompt adds "no flags, no religious buildings, no domes, no minarets, no headscarves". | `fal_media.md` §5 |
| Audio | Mirelo SFX 2.0 transients · ElevenLabs SFX v2 loops and bodies · EL v4 and Gemini TTS (§4.3) · EL Music v2.5 | `fal_media.md` §1 |

### 18.3 Build order
1. **Systems skeleton:** MissionDirector (beats, checkpoints), LightStage, VOManager, squad node chains, EncounterSpawner, HUD, the custom web template (click gate, error handler, DPR cap; `webgl.md` §5.5).
2. **Vertical slice: Beat 3 (Long Market at golden hour) plus Beat 5 (Last Light).** It proves the look, the squad, the radio, the sunset and the WebGL budget. Then do one private artifact smoke-test publish to check pointer lock, fullscreen, audio and IndexedDB inside the frame (`webgl.md` §8). **That publish needs the user's go-ahead.**
3. The booth, the intro, the title card and the glitch and death inserts.
4. The remaining beats, in order.
5. Polish, an audio pass in which a human listens to everything, and a Safari pass (S3TC, memory).

### 18.4 Cut list (cut from the top first)
1. Door-gun gameplay. Beat 11 becomes a pure cinematic, keeping the climax.
2. Slow-motion breach.
3. Rooftop marksman (Beat 5 becomes traversal plus the sunset).
4. LZ flares (headlights only).
5. Destructible floods (they become static set dressing).
6. Live projector film (stills on a CRT instead).
7. Optional intel.
8. Torn awnings (static shafts instead).

**Never cut:** the sunset (MID-A), the numbers on the net, the glitch inserts, the scrambler reveal, the intro and outro chair scenes, the debrief.

### 18.5 Open risks
- The claude.ai artifact frame (pointer lock, autoplay, IndexedDB) is untested.
- Safari's S3TC support is unverified.
- Meshy rifle-clip hand placement is unverified.
- The quality of the Rocketbox retexture is unknown.
- **Nobody has listened to the generated audio yet.**
- Total size must stay at or under 230 MB, so Crunch is mandatory and the per-class texture caps in TECH_PLAN §8.9 apply; P1/P2 assets go in only while `BudgetReport` shows headroom.
- Low-sun shadows (0.5°–1.5°) are prone to acne and peter-panning; the R4 bias settings are tuned in the vertical slice (Beat 5 is part of it).
- Without O2 (Rocketbox downloads) every human except Calder is a Meshy character without fingers or a jaw.
- Ten skinned characters plus Forward+ lights at the LZ have not been profiled.
- The web check of the invented words is still to be done.
- Gemini voice genders need confirming on the first take.
- Characters will be sized against `cc0.md` estimates until real measurements replace them.

---

## 19. Dialogue index
`Docs/DIALOGUE.json` holds **157 entries**: 93 scripted lines (`D001`–`D093`), 6 death inserts (`DI_01`–`DI_06`), 34 squad callouts (`CO_HOL_*` 12, `CO_SPA_*` 11, `CO_TIN_*` 11), 15 Legion barks (`BK_LEG_*`) and 9 Greycoat barks (`BK_GRY_*`).

Fields: `id, speaker, callsign, voice_id, text, delivery, context, trigger, radio_filter`, plus an optional `subtitle` on every line whose spoken text is not English (the PA line `D012` and all 24 barks). The subtitle shown is `subtitle` if present, otherwise `text`. PA subtitles are always shown; bark subtitles follow the "Translate enemy chatter" option and the 25 m rule. TECH_PLAN Appendix B mirrors these values.
- `radio_filter: true` marks lines that are **always** radio (or HF/intercom). Every squad line is also baked in the RADIO chain for the distance crossfade (§10.1).
- `voice_id` keys map to the casting table in §4.3.

| Range | Scene |
|---|---|
| D001–D006 | Intro, booth |
| D007–D013 | Beat 1 |
| D014–D020 | Beat 2 |
| D021–D027 | Beat 3 |
| D028–D034 | Beat 4 |
| D035–D042 | Beat 5 / MID-A |
| D043–D048 | Beat 6 |
| D049–D052 | Beat 7 |
| D053–D058 | Beat 8 / MID-B |
| D059–D067 | Beat 9 |
| D068–D076 | Beat 10 |
| D077–D082 | Beat 11 and climax |
| D083–D092 | Outro |
| D093 | Out of bounds (system) |
