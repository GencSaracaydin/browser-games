# OPERATION SALT MIRROR

**Mission pitch, slug `defector`** · single-mission FPS campaign level · Unity 6 URP · WebGL (desktop Chrome/Safari, mouse + keyboard)

> **Logline.** August 1986. A four-person black-ops team crosses a white-hot desert airfield at high noon to pull a defecting weather scientist out of a cold-storage bunker, and a dust storm that shouldn't arrive until four o'clock rolls over the field at one-fifteen. In the brownout they find a wounded woman reading their own recognition code into a live transmitter. Her face doesn't match the photo, so the team leader ties her to the chair and they go on to collect the woman who does match. Eleven weeks later, strapped into a chair of his own, the player keeps insisting they brought the defector home. Someone behind the one-way glass has been listening to every word.

| | |
|---|---|
| **Operation** | SALT MIRROR (Task Group CINDER) |
| **Date / time** | 19 August 1986, 12:41 to 13:51 local (compressed into about 13 minutes of play) |
| **Framing date** | 4 November 1986, "Session 07", location withheld |
| **Setting** | **Saffan Field**: an airstrip, pipeline pumping station and radar site in the Selvan Basin, **Republic of Tazrak** (fictional) |
| **Length** | About 13 min of gameplay on Regular, about 17.5 min including cinematics |
| **Player** | SSgt. Jonah Pryce, CIA Special Activities paramilitary officer, callsign **DRIFT** |
| **Squad** | Royce Tamblyn "SEXTANT" (lead), Beatrix Halloran "STATIC" (NSA intercept specialist), CWO Tarik Bassan "SPANNER" (Tazraki Air Force crew chief) |
| **Enemies** | 57 total, at most 10 alive at once, in two factions |
| **Footprint** | 400 × 400 m playable; dune and mesa backdrop out to about 3 km |
| **Design pillar** | **You can see everything, then nothing.** The first third is a sniper's mission with 2 km sightlines. The haboob turns it into a knife fight at 20 m, and the mission's real question becomes *what do you trust when you can't see: the photo, or the voice?* |

---

## 1. The pitch in one paragraph

Dr. Anika Sorell, the chief physicist of a foreign program called DUSTLINE ("a storm you can schedule"), has asked to defect. Her recognition group is five numbers, **6-1-3-8-2**. Task Group CINDER walks in over the ridge at noon, when the guards are slowest and the sun is hardest. You open with binoculars and a scoped rifle across a shimmering airfield, fight a loud mid-range battle in a pipeline pumping station, and sprint across an open runway under a tower gun. Then the haboob arrives. A 600-metre wall of dust swallows the field, and the fight drops to hangar interiors, flashlight cones and silhouettes. A numbers station has been reading your recognition group all mission. Inside the transmitter house you meet the reader: a wounded scientist who says she is Sorell. The photo in your team leader's hand says she isn't. You leave her. In the bunker's cold store you find the woman from the photo, hood her, and fight your way to a salt-pan LZ as the storm thins and the sun breaks through. The debrief reveals that the photo was swapped by someone in your own building. The woman you left behind survived, and she is sitting on the other side of the interrogation-room glass.

**What makes this mission distinct from its siblings (`dusk`, `night`):** it's a daylight mission that turns into a weather mission, and the twist is guilt rather than conditioning. The player isn't a sleeper and the handler isn't the villain. The player did a wrong thing by trusting the wrong evidence, and the person he wronged gets the last word.

---

## 2. Setting

### Republic of Tazrak (fictional)
Tazrak is a small, dry, non-aligned republic. Its wealth is a single oil pipeline that runs from the interior fields across the Selvan Basin to the coast. The capital, Hadrel, is off-map. In 1979, short of money, the government leased **Saffan Field** to the **Halvane Concession**, a foreign-registered engineering and security consortium, "for pipeline security." By 1984 Halvane had stopped paying and stopped answering. By 1986 it was hosting a technical detachment from an unnamed foreign bloc, and the field was effectively a sovereign island. Tazrak can't retake it openly without a diplomatic disaster. Quietly, though, it has lent the CIA the one man who knows every drain and hatch on the base.

### Saffan Field, Selvan Basin
- A 1950s air-force strip (**Runway 09/27**, 375 × 30 m) with sand drifting across the asphalt, tin-arched hangars, and a three-storey control tower repainted in Halvane's company grey.
- **Pumping Station K-4** is a 1960s pump house with two storage tanks and a nest of pipe racks. The pipeline runs east–west across the whole map.
- An early-warning **radome** on a lattice tower, on an 18 m hill at the northwest corner.
- A **transmitter house** under a **90 m guyed lattice mast** with red aviation lights. Shortwave listeners call its station "the Saffan Lady."
- A **concrete bunker** (two levels) under Hangar Two, with a freezer-grade **cold store** on B2: DUSTLINE's sample archive.
- To the south is **the wadi**, a dry gully holding the ruined mud-brick walls of the abandoned well-hamlet that gave the field its name. To the southeast is **the Shelf**, a 25 m escarpment. To the west is the **salt pan**: a white crust so flat and bright that at noon it mirrors the sky (hence SALT MIRROR). To the north is the Selvan Erg dune sea, the impassable boundary.

### Sensitivity guardrails (apply to every asset, prompt and line)
1. Every country, town, company and faction is invented. The foreign bloc is never named, and no real country, religion or ethnic group is the enemy. No real militant groups appear.
2. **No religious architecture, imagery or audio.** That means no domes other than the radome (which must read as a white geodesic panel sphere on a steel tower), no minarets, no arches with religious associations, no calls to prayer, chanting or scripture.
3. **No Arabic-script signage at all.** Image generators produce pseudo-script that can accidentally form real or religious words. All signage is English-language stencil ("SAFFAN FIELD", "PUMP STN K-4", "NO NAKED FLAME") plus invented Latin-letter company codes ("HVC-7", "HALVANE / SECTOR 4"). Every image and texture prompt must say "no text" or "English stencil only".
4. The enemies are multinational corporate mercenaries in uniform and a faceless foreign technical unit. There are no civilians in the level. The only Tazraki characters with lines are heroes (SPANNER, and the HALFMOON pilot).
5. All invented bark words are checked against a web search for accidental meanings before recording. The glossary is in §12.
6. The interrogation is tense, not graphic. The cuffs, the chair and the voice do all the work.

---

## 3. Factions

| Faction | Who they are | Look | Combat role | Voice |
|---|---|---|---|---|
| **Task Group CINDER** (player side) | A deniable CIA Special Activities team plus a borrowed NSA intercept specialist and a Tazraki crew chief | Mixed 1980s field kit: tan shirts, chest rigs, boonie hats and ball caps, sand-taped rifles. **Green chemlights on each shoulder strap** (friendly ID in the storm). | Player plus 3 invulnerable squadmates | English |
| **Halvane Concession Guard** ("the Guard") | The consortium's private army: hired professionals from everywhere, with no national identity | Sand-grey company fatigues, **orange armbands and helmet bands** (company safety colour, which doubles as a readability tell in the storm), goggles, neck gaiters, steel helmets or soft caps. AKM, RPK, one scoped rifle. | The bulk of enemies (about 70%). Disciplined mid-range fighters who use cover, suppress and flank, and lose coordination when their Foreman dies. | Barks in an invented work-site pidgin, "Concession cant". A calm, pre-recorded English **PA** announces protocols. |
| **The Survey Detachment** ("hoods") | A foreign bloc's atmospheric team and its security element, running DUSTLINE. They're in on the photo swap. | Off-white rubberized NBC coveralls, rubber hoods, **round-lens respirators**, AKMs with **taped-on flashlights**. They are never seen unmasked. | About 30% of enemies, only in the storm and the bunker. 1.5× HP, aggressive at close range, move in pairs. | **They never speak.** They communicate with respirator breath and **radio squelch-click codes**: 1 click = hold, 2 = move, 3 = target sighted. Three clicks means you've been seen. |
| **Republic of Tazrak** | The legitimate government, officially neutral | Represented by SPANNER and by HALFMOON (an unmarked, sand-painted utility helicopter flown by a young Tazraki Air Force pilot) | Ally | English with a Tazraki accent |

**Offstage:** "the Concessionaire" (Halvane's site director), heard only as the PA's recorded voice, and the **mole**, "someone in our building."

---

## 4. Player character and squad roster

| Name | Callsign | Role | Personality | Voice (for TTS casting) |
|---|---|---|---|---|
| **SSgt. Jonah Pryce** (player) | **DRIFT** | Point man, 29, ex-Army Ranger, seconded to the Agency | Silent in gameplay. In the chair he's dehydrated, defensive and stubborn. He keeps repeating "it's in the report" because the report is the only version he can live with. | Upper-Midwest (Minnesota) flat vowels, sand-scoured hoarse baritone, short sentences, audible swallow before he lies. |
| **Cmdr. Royce Tamblyn** | **SEXTANT** | Team lead, 41, ex-Navy UDT/SEAL turned Agency paramilitary | Clinical, procedural and polite. He never swears and never raises his voice. He calls women "ma'am". He makes every hard decision instantly, and every one of them happens to serve the mole. (He is the mole. He vanishes after the op.) | Quiet South Shore Boston, clipped, unhurried. The calmest voice in any room. |
| **Beatrix "Bea" Halloran** | **STATIC** | NSA intercept specialist, 33, attached for SIGINT. Carries an HF receiver and a PRC-77-pattern radio. | Introverted, deadpan and precise. She has logged the Saffan Lady for two years and knows that voice better than anyone alive. She is the team's ear, and the first to suspect. | Chicago, dry low alto, fast and flat. When she's right she sounds like she's apologising. |
| **CWO Tarik Bassan** | **SPANNER** | Tazraki Air Force crew chief, 54. Ran Saffan Field's maintenance for eleven years before Halvane took it. | Warm, wry, practical. He speaks of aircraft and buildings as "she" and judges men by how they treat machines. He's the team's conscience. He doesn't argue with Sextant; he simply does the right thing later. | Husky, warm tenor-baritone, a chuckle under fire. Careful, formal English learned from foreign maintenance manuals, with no contractions. |

**Supporting voices**

| Voice | Who | Description |
|---|---|---|
| **THE INTERROGATOR** | A CIA counterintelligence officer running a mole hunt. Unseen, behind Pryce's right shoulder. | Sixties, plain Iowa vowels, patient, tired, folksy. The kindness of a family doctor. Stirs his coffee before every important question: **three spoon clinks**. |
| **THE LADY / DR. ANIKA SORELL** | The numbers station *is* Sorell reading into the transmitter. **One voice and one actor for both.** Players should be able to make the match by ear. | Mid-forties, low, tired, precise English with an unplaceable soft accent. As the Lady she reads each digit with a full stop and through HF processing (heterodyne whistle, fading). In person she's breathless and wounded. |
| **THE PACKAGE** | A bloc agent impersonating Sorell, wearing the face in the swapped photo | Early thirties. A *good imitation* of Sorell's accent, a lighter voice, frightened. She reads the group with commas, like a memorized PIN: "six, one, three, eight, two". |
| **HOUSEKEEPER** | CIA desk officer, HF radio only | Middle-aged Virginian, bored and bureaucratic, all reports and windows. |
| **HALFMOON 2-1** | Young Tazraki Air Force pilot, a former student of Spanner's | Early twenties, eager, accented English, nervous laugh, rotor noise and intercom compression. |
| **PA** | Halvane's recorded site announcements | Calm, slightly bored corporate male in English, through horn speakers, with a two-tone chime first. |
| **Concession Guard barks** | 3 voice presets | Shouted invented pidgin (§12) |
| **Survey "hoods"** | Breath plus squelch | Respirator inhale/exhale loops and HF squelch clicks. No words. |

**Player loadout (four period weapons):**
- **FN FAL Para** (folding-stock battle rifle), primary: 20-round mags, semi-auto, heavy and accurate. Built for the long first half.
- **M21-pattern scoped rifle**, Beat 1 only, picked up from the ridge; it can be carried until swapped.
- **AKM**, enemy pick-up: full auto, the right tool once the storm closes in. The Survey version carries a taped flashlight.
- **Browning Hi-Power** pistol.
- Plus **3 M67-pattern frags** (resupplied at 4 ammo crates: pump house, Hangar One, bunker B1, LZ) and a rifle-butt melee.

**Feel targets (CoD arcade):** health regenerates (3 s delay, 2.5 s to full) with a dust-and-blood vignette. Sprint 6.8 m/s, walk 4.2 m/s, crouch. Slide 0.8 s out of a sprint (under pipe racks and the wadi pipeline). Vault ≤ 1.3 m, climb ≤ 2.1 m (sandbags, pipe racks, crates, the wreck). ADS in 0.25 s. TTK: an AKM kills the player in about 4 body hits on Regular. The FAL kills a Guard in 2 body hits and a hood in 3. Recoil comes from a procedural kick pattern plus recovery, with visible sway when sprint-stopping.

---

## 5. Premise and interrogation framing

### The present: SESSION 07, 4 November 1986
Pryce sits in a soundproofed **listening booth**: acoustic-tile walls, a steel chair bolted to the floor, leather wrist cuffs, a flickering fluorescent tube. To his left is a cart with **five mismatched CRTs** and a reel-to-reel deck. Ahead is a **one-way window into a darkened listening room**. Behind the glass a faint silhouette in headphones sits perfectly still. The player will assume it's a technician. The Interrogator is behind his right shoulder, unseen except for one hand late in the debrief. He drinks coffee and stirs it.

**What the Interrogator says he wants:** the true account of Saffan Field.
**What he actually wants:** to find out whether Pryce was Tamblyn's accomplice or just his instrument. And the woman behind the glass wants to hear, in his own words, why they left her.

### The mission as Pryce remembers it
Collect Dr. Anika Sorell from the cold store under Hangar Two, using the recognition group 6-1-3-8-2 and the photo in Sextant's packet. Get out before the 14:00 window closes. "We brought her out. It's in the report."

### What actually happened
- **Eleven days before the op, the mole swapped the briefing photo** for the face of a bloc agent. Commander Tamblyn signed for the packet.
- The Survey learned Sorell meant to defect and locked her in a cold-store cell. They put **the Package** in her place, wearing the photo's face and knowing the code (the mole supplied it), and **scheduled the storm** (DUSTLINE) for 13:15 to blind any outside eyes during the swap.
- The real Sorell escaped, barricaded herself in the transmitter house, and began **reading her own recognition group on the station's frequency in broad daylight**. It was the only way she had to say "it's me, I'm here." The Survey was cutting through her door when CINDER arrived.
- Sextant used the photo to call her an impostor, zip-tied her to the chair and had the mast brought down: **the reading stopped at 13:42**.
- **SPANNER rigged the mast to fall east, away from the house,** then refused to board the helicopter. He walked back into the storm, cut her loose, and spent nine weeks carrying her across the basin and over the border.
- Tamblyn disappeared after the debrief. The Package hasn't said one true thing since.

### Clue trail (planted in play, paid off in the debrief)
1. **Daylight reading (Beat 2).** STATIC: the Lady never broadcasts in daylight and never repeats a group. Today she is reading *our* group, over and over.
2. **The early storm (Beat 3).** SPANNER: "Storms here do not come before four." Optional intel in Hangar One: a typed Survey sheet reading **"DUSTLINE — SAFFAN — 19 AUG — 13:15."**
3. **The RF bleed (Beat 6).** The numbers come through the hangar PA horns. The transmitter is so close it bleeds into the amplifiers, so the reading is live and inside the wire, not a tape.
4. **The face and the photo (Beat 8).** Sextant holds the photo beside her face, and look-assist frames both for 2 seconds. Optional intel on the console is her **facility ID badge, which shows her real face**. A player who picks it up can see the photo is the lie.
5. **The voice (Beats 8 and 9).** STATIC recognises the Lady's voice in the woman at the console. The Package's voice is different, reads with commas, and sounds rehearsed.
6. **"Thank you, Sergeant Pryce." (Outro).** The team used only callsigns on the op. Nobody told the Package his name.
7. **"She falls east." (Beat 10).** SPANNER aims the mast collapse away from the house.
8. **The silhouette behind the glass (Intro, Beat 5).** She lifts her head at "Every station in the world reads numbers."

### Flash rules
- **One major flash** (about 20 s, Beat 5): a hard cut to the booth with a tape-stop sound, scanline tear, a chromatic-aberration spike and three spoon clinks.
- **Three subliminal flashes** (3–6 frames each):
  - the booth's cuffed wrist, when Sextant zip-ties Sorell's wrist (Beat 8)
  - the dark one-way window, when the Lady's reading cuts off mid-group (Beat 10)
  - the Interrogator's coffee mug, when the Package says Pryce's name (Outro)

---

## 6. Mission flow, beat by beat

**Timeline:** Intro 1:45 · Title 0:10 · Beats 1–10 ≈ 13:15 (including the 0:20 flash and about 0:45 of scripted scene) · Outro 0:45 · Debrief 1:15 · **Total ≈ 17:30.**

| Beat | Name | Clock | Play time | Phase | Enemies (max alive) |
|---|---|---|---|---|---|
| 1 | GLASS | 12:41 | 1:15 | A Noon glare | 2 (2) |
| 2 | THE SAFFAN LADY | 12:50 | 1:15 | A | 4 (4) |
| 3 | PRESSURE | 13:02 | 1:45 | B Wall on horizon | 9 (7) |
| 4 | OPEN GROUND | 13:12 | 1:00 | B → C | 6 (6) |
| 5 | INTERRUPTION (flash) | — | 0:20 | Booth | 0 |
| 6 | HANGAR ONE | 13:16 | 1:30 | C exterior / E interior | 8 (6) |
| 7 | BROWNOUT | 13:24 | 1:15 | D Brownout | 6 (5) |
| 8 | THE READER | 13:30 | 1:30 | D / E | 4 (4) |
| 9 | COLD STORE | 13:35 | 1:45 | E Bunker | 7 (5) |
| 10 | HALFMOON | 13:42 | 2:00 | D → F Clearing | 11 (10) |
| | | | | **Total** | **57** |

---

### Beat 1 — GLASS (The Shelf) · 12:41 · ~1:15
- **Location:** the crest of the Shelf escarpment, 25 m above the basin floor. The team lies prone among sun-bleached boulders under a camo net. Ahead, 230–400 m of heat-shimmering flats, then Saffan Field, with the radome flashing white on its hill and the mast's red lights blinking even at noon.
- **Gameplay:** observation tutorial. The player raises binoculars and scans: resting on four points of interest for 1 s each tags them on the compass ribbon (Radar Hill, the hangars, the transmitter mast, the pump station). Then he swaps to the M21 lying beside him for a **synced shot** on two pump-house roof sentries at about 230 m (bullets are hitscan with a short travel delay and a small, visible drop). The beat ends with the **first slide**: the team drops down a sand chute off the Shelf into the wadi, which teaches sprint plus slide.
- **Squad:** SEXTANT is prone on the left with binoculars. SPANNER is prone on the right with his rifle and takes the left target. STATIC kneels behind them on the HF set, whip antenna up.
- **Encounter:** 2 Guard sentries (static idles). If the player misses, the second sentry runs for the siren and SEXTANT drops him with an alt line. That's not a fail state, but the Beat 2 patrol arrives alerted and searching, so it can't be ambushed.
- **Objectives / markers:** "Survey Saffan Field (0/4)" → "Eliminate the pump-house sentries" → "Move to Pumping Station K-4" [marker, 260 m].
- **Radio:** #08–#11.
- **Transition:** the chute slide, from heat and glare down into the shade of the wadi.

### Beat 2 — THE SAFFAN LADY (The Wadi) · 12:50 · ~1:15
- **Location:** a dry gully 4–8 m deep and 160 m long, winding west. Cracked mud floor, tamarisk scrub, ruined mud-brick walls and the capped wellhead of the old Saffan hamlet. A rusted pipeline segment crosses overhead, low enough that you slide under it.
- **Gameplay:** stealth-optional walk-and-talk. The team moves in file. A 3-man Guard patrol comes up the wadi toward them, and a spotter stands on the rim. Everyone fires on SEXTANT's cue, or the instant the player fires. Melee works on the spotter if the player flanks up the rim ramp.
- **Squad:** STATIC walks just ahead of the player. **The Lady's voice comes positionally from her receiver** (3D audio on her backpack). SEXTANT leads, and SPANNER takes rear guard.
- **Encounter:** 4 Guards (3 patrol + 1 rim spotter).
- **Objectives / markers:** "Follow Sextant" (follow marker) → optional "Ambush the patrol" → "Reach the pumping station" [marker].
- **Radio:** #12–#17. Concession cant chatter plays from the patrol at 15 m and comes over STATIC's set when she tunes past their net.
- **Transition:** the wadi ends at the station's chain-link fence. SEXTANT cuts it. A Foreman on a catwalk spots them, and the alarm goes up regardless of how stealthy the player was.

### Beat 3 — PRESSURE (Pumping Station K-4) · 13:02 · ~1:45
- **Location:** an 80 × 70 m fenced compound. A two-storey concrete pump house with a corrugated roof, a valve house, two 20 m storage tanks with ladders and catwalks, three pipelines on racks 1–3 m high (vault over or slide under), a pipe trench along the west side, a generator shed, and a **siren pole with a relay box on the pump house's north wall**.
- **Gameplay:** the first loud firefight, at 20–60 m with plenty of cover. Destroying the relay box (shoot it or melee it) **cancels Guard wave 3**, which rewards aggression. If the player ignores it, 3 more Guards arrive. That's the only case where the total goes above 57 (to a hard cap of 60), and the player is warned by "Kill it, or the whole field comes running."
- **Squad:** SEXTANT pushes up the central pipe rack and calls targets ("Tank catwalk!"). SPANNER flanks left through the trench. STATIC lays tracer suppression from the fence line. The squad gets scripted credit for about 30% of kills.
- **Encounter:** wave 1 is 5 Guard riflemen and the Foreman (bullhorn). Wave 2 is 3 more (1 RPK gunner, 2 riflemen) arriving on foot from the runway, with a Halvane truck pulling up on a spline in the background. 9 total, at most 7 alive.
- **Set dressing moment:** after wave 2, SPANNER points west. Look-assist turns the camera gently to the horizon, where **the haboob wall** stands about 2 km away and 600 m tall, a rolling brown cliff with the sun still blazing above it. Wind gusts start, flags snap, sand streamers begin crossing the asphalt.
- **Objectives / markers:** "Silence the alarm relay" → "Clear Pumping Station K-4" → "Cross the runway to Hangar One" [marker].
- **Radio:** #18–#24.
- **Transition:** SEXTANT kicks open the north gate onto the runway.

### Beat 4 — OPEN GROUND (Runway crossing) · 13:12 · ~1:00
- **Location:** 30 m of runway plus 25 m of taxiway: cracked asphalt, sand drifts, pallets of fuel drums, a tow tractor, and the **burned-out twin-engine cargo plane wreck** in the middle of the runway (static prop and the main cover). The control tower to the northwest has a **heavy machine gun on its catwalk**. A marksman's scope glints on Radar Hill.
- **Gameplay:** move between cover under suppression (screen shake, dust kicks, visible tracers). From the wreck (about 70 m) the player frags the catwalk, whose **rotten rail collapses** and drops the gunner, or shoots him. **The fog ramps during the crossing (Phase B → C)**, so the marksman's glint fades and he stops firing: the first lesson that the storm protects you as well as blinding you.
- **Squad:** SEXTANT goes first and stops at each cover point, SPANNER covers the tower, and STATIC stays with the player.
- **Encounter:** 1 HMG gunner + 1 loader, 3 Guard riflemen on the apron, 1 marksman on Radar Hill (ignorable). 6 total.
- **Objectives / markers:** "Neutralize the tower gun" → "Reach Hangar One" [marker, sliding door].
- **Radio:** #25–#27. The PA announcement echoes across the whole field.
- **Transition:** reaching taxiway cover at Hangar One's door triggers Beat 5.

### Beat 5 — INTERRUPTION (flash) · ~0:20
- A non-interactive hard cut to the booth (see §7.3). We come back **at the exact moment the storm front hits**: from a silent room straight into roaring wind, and the world's visibility drops from 120 m to 60 m in a single frame.
- **Radio:** #28–#30.

### Beat 6 — HANGAR ONE · 13:16 · ~1:30
- **Location:** a 60 × 50 m arched hangar, 14 m high. A disassembled transport plane sits on jacks (fuselage plus one wing on cradles) under scaffolding and an overhead crane. Work lights swing on cables. A glassed mezzanine office has stairs. The big doors stand half open and bang, and **sand pours in sheets through every gap**, lit by thinning orange storm light.
- **Gameplay:** close quarters at 5–25 m, with height: enemies on the mezzanine and on the scaffolds. The player and SPANNER clear the fuselage interior together, a tight tube with light leaking through rivet holes. The player interacts with the manifest on the office desk. Optional intel: the DUSTLINE schedule sheet. The PA horns crackle with the Lady's voice (RF bleed).
- **Squad:** SEXTANT **cracks a big green chemlight** and clips it to his shoulder; this becomes the follow marker for the rest of the storm. STATIC holds the stairs, and SPANNER pairs with the player.
- **Encounter:** 6 Guard riflemen (2 on the mezzanine), 1 RPK gunner behind the wing cradle, 1 Foreman. 8 total, at most 6 alive.
- **Objectives / markers:** "Search Hangar One" → "Find the cold store records" → "Shut down the transmitter" (SEXTANT's addition) [marker on the transmitter house, 190 m NE].
- **Radio:** #31–#35.
- **Transition:** SEXTANT shoulders open the east door into total brown.

### Beat 7 — BROWNOUT (Apron and barracks row) · 13:24 · ~1:15
- **Location:** the open apron and barracks row (six prefab huts), a water tower, power poles with sparking, slapping cables, and wind socks. The **mast's red aviation lights** blink high above the dust, the only fixed landmark.
- **Gameplay:** visibility 15–25 m. Objective markers beyond 30 m dissolve into flicker, so **you follow SEXTANT's chemlight**. Enemy sight range drops to 18 m both ways, which means you can slip past hoods or ambush them. Readability tells: **hood flashlight cones** (see them before they see you), **Guard orange armbands**, **friendly green chemlights**, and the **three-click squelch** that means you've been spotted. Footsteps are muffled, and gunfire is dulled and has no tail.
- **Squad:** a tight wedge within 6 m of the player. They don't run ahead, and SEXTANT waits at each hut corner.
- **Encounter:** 4 Survey hoods (2 pairs sweeping with flashlights) and 2 Guards sheltering behind the barracks. 6 total, at most 5 alive.
- **Objectives / markers:** "Stay on Sextant's light" → "Reach the transmitter house".
- **Radio:** #36–#38.
- **Transition:** **sparks in the brown**. Two hoods with a gas-cutting torch are halfway through the transmitter house's steel door, with a third on guard.

### Beat 8 — THE READER (Transmitter house) · 13:30 · ~1:30
- **Location:** a 15 × 10 m concrete block at the foot of the mast. Inside, racks of valve transmitter cabinets glow orange, a green oscilloscope traces her voice, a gooseneck mic sits on a desk, there's a reel-to-reel and a cot, and **a steel chair that is the same model as the booth chair**. A back hatch hangs open.
- **Gameplay:** kill the 3 hoods at the door (torch operator plus 2 guards). Then the **breach**: the player kicks the door, followed by **1.6 s of slow motion** (timescale 0.35). One more hood, who came in through the back hatch, holds a pistol to the woman's head. Then the scripted scene (§7.4): about 45 s with free look and locked movement. Optional intel: Sorell's ID badge on the console.
- **Squad:** SEXTANT runs the scene. SPANNER goes out to the mast footing afterwards (charges). STATIC stays a beat too long, staring at the woman.
- **Encounter:** 4 hoods.
- **Objectives / markers:** "Breach the transmitter house" → (after the scene) "Reach the cold store (B2)" [marker on Hangar Two, 80 m].
- **Radio:** #39–#48.
- **Transition:** as the team leaves, the reading starts again behind them. From now on it plays faintly from STATIC's receiver, weaker and breathier with each repeat.

### Beat 9 — COLD STORE (Hangar Two and bunker) · 13:35 · ~1:45
- **Location:** Hangar Two (50 × 45 m, darker, storm howling through its roof vents) leads down stairs to **B1**: 2.5 m concrete corridors, lockers, a decontamination shower room, a thumping diesel generator room, and a lab with fume hoods and pressurised sample canisters stencilled DUSTLINE. Below that is **B2, the cold store**: walk-in freezer rooms, frost on every surface, hanging plastic strip curtains, racks of glass ampoules, **breath vapour from every mouth**, and cold blue fluorescents on a separate circuit.
- **Gameplay:** the tightest close quarters in the mission. Strip curtains hide silhouettes, and hoods come out of side rooms. **The generator dies mid-fight**, sodium yellow snaps to red emergency light, and the flashlight becomes essential (toggle with F). The final door on B2 opens on the Package: frost in her hair, wrapped in a lab coat, hands up. She says the group with commas. The player interacts to put the respirator hood over her head, with a 1 s fade. From here she follows SEXTANT as an invulnerable escort who crouches when fire comes close.
- **Squad:** SEXTANT leads through doors and STATIC covers the rear. SPANNER is outside at the mast, so this is a three-person fight and noticeably harder.
- **Encounter:** 5 Survey hoods and 2 Guards. 7 total, at most 5 alive.
- **Objectives / markers:** "Reach the cold store (B2)" → "Secure the package" → "Escort the package to the salt pan" [marker, 300 m W].
- **Radio:** #49–#51.
- **Transition:** a freight-elevator ride up (it hides a streaming load of the exfil area). The doors open on the storm, and it is starting to thin.

### Beat 10 — HALFMOON (Apron → runway west → salt pan LZ) · 13:42 · ~2:00
- **Location:** a 300 m run west along the runway in fading brownout, then the **salt pan**: a blinding, nearly featureless white crust with a cluster of drums, runway threshold markers and a fallen wind sock for cover. STATIC drops an IR/white strobe at the pad.
- **Gameplay:** a moving escort fight, then a hold. At the runway's edge SPANNER detonates the mast (§7.5). It falls east, red lights arcing down through the dust, and **the Lady stops mid-group** on STATIC's radio. The Guard counterattacks from the fuel farm and Hangar One. A Halvane truck runs a spline along the taxiway with a mounted gunner. He's killable, and when he dies the truck rolls to a stop (it's never drivable). **The storm clears (Phase F)** over the hold: the sun appears as a white disc, then breaks out in full. Light shafts cross the pan, and the salt goes mirror-bright. HALFMOON comes in on a spline with its searchlight cutting the dust, and a ring of rotor wash kicks up salt. Hold until wheels down, then "Board Halfmoon".
- **Squad:** SEXTANT stays with the Package behind the drums. STATIC marks the LZ, then fights. SPANNER rejoins at the runway and fights the hold, then **stays behind** at the skid (§7.5).
- **Encounter:** wave 1 is 6 Guards. Wave 2 is 2 hoods, 2 Guards and the truck gunner. 11 total, at most 10 alive.
- **Objectives / markers:** "Escort the package to the salt pan" → "Hold the LZ" (60–75 s timer, shown as HALFMOON's distance in metres) → "Board Halfmoon".
- **Radio:** #52–#58.
- **Transition:** into the outro.

---

## 7. Cinematic sequences (shot by shot)

Everything is in-engine and driven by Timeline. Booth scenes are first-person with limited look. Mission scenes keep free look wherever possible.

### 7.1 Intro: SESSION 07 (1:45)
1. **0:00–0:06** Black. A reel-to-reel clunks, tape hisses, a spoon clinks three times in a mug. **#01**.
2. **0:06–0:14** Extreme close-up from a camera outside the player: Pryce's eye opens and the pupil contracts as the fluorescent tube stutters on.
3. **0:14–0:32** POV with limited look (±35° yaw, ±20° pitch). The player's wrists are in leather cuffs on steel armrests, with a pale band where a watch used to be. Acoustic tiles. Ahead is the dark one-way window with a **faint seated silhouette in headphones**. On the left is the CRT cart. Mouse-shake rattles the chair (a struggle animation) and changes nothing. **#02**, **#03**.
4. **0:32–0:45** The CRTs power on one at a time with a degauss *thunk* and a high whine, showing static. **#04**.
5. **0:45–1:15** **The intel montage**, one CRT at a time:
   - CRT 1: a grainy black-and-white overhead strip of Saffan Field
   - CRT 2: the radome on its tower
   - CRT 3: the ID photo stamped "SORELL, A.", showing **the Package's face**
   - CRT 4: an oscilloscope trace of the Lady's voice
   - CRT 5: 16 mm-look footage of a haboob swallowing a road

   The reel plays **#05**. One digit of 6-1-3-8-2 burns in on each CRT in turn, then all five glitch together. **#06** plays over the photo.
6. **1:15–1:28** A slow push toward the window. Behind the glass the silhouette's headphones catch the CRT glow for a moment. She doesn't move.
7. **1:28–1:45** **#07**. All five CRTs cut to the same image, the Shelf at noon. The camera pushes into CRT 2, and the scanlines bloom into white **sun glare** that fills the frame.

### 7.2 Title card (0:10)
Instead of the usual black card, the sun glare holds as a **bleached white frame**, the colour of the salt pan. Black teletype text stamps onto it line by line, with a clatter for each line:

```
SALT MIRROR
SAFFAN FIELD — SELVAN BASIN — REPUBLIC OF TAZRAK
19 AUGUST 1986 — 12:41 LOCAL
SSGT. JONAH PRYCE — "DRIFT" — TASK GROUP CINDER
```

For 2 frames the date flickers to **04 NOV 1986**, and for 1 frame the digits **6 1 3 8 2** replace the time. The white then exposes down into the real noon scene: you're on your belly on the Shelf, and the heat shimmer is the first thing you see.

### 7.3 Beat 5 flash: INTERRUPTION (0:20)
1. A scanline tear and tape-stop sound effect. Hard cut to the booth, POV.
2. For the first time we see one of the Interrogator's objects: **his coffee mug** set on the CRT cart beside Pryce's hand. Three clinks. **#28**.
3. **#29**. On the CRTs, the transmitter mast in brownout, red light blinking.
4. Behind the glass, **the silhouette lifts her head and leans toward the window**. **#30**.
5. A smash cut back to the hangar door, timed to the storm front's arrival: a wall of noise, fog jumping to Phase C, and sand stinging the screen-space overlay.

### 7.4 THE READER (Beat 8, in-engine, about 45 s; free look, movement locked)
1. **Breach.** The player kicks the door. 1.6 s slow motion: a hood behind the console holds a pistol to her head. The player shoots, and time returns to normal.
2. **Her.** She's at the console: a mid-forties woman in a torn grey coverall, a bloodied shoulder, cracked wire glasses, a headset around her neck. She pushes the pistol on the desk away with two fingers. She's sitting **in the same steel chair as the booth**, and the shot composition matches the intro POV on purpose. **#40**.
3. **The photo.** SEXTANT steps in from the left and holds the photo up beside her face. Look-assist frames both for 2 seconds, and they are clearly different women (photo: light-brown bob, round face, early thirties; her: dark hair with a grey streak, angular face, mid-forties). **#41**, **#42**.
4. **The cuff.** SEXTANT zip-ties her left wrist to the chair arm. A 4-frame subliminal of Pryce's cuffed wrist in the booth. **#43**.
5. **#44**, **#45**. SPANNER looks at the player for a long beat, then goes out to the mast.
6. **#46**, **#47**. SEXTANT takes the headset off STATIC and hangs it on its hook.
7. Movement unlocks. If the player stays within 2 m of her while turning to leave, she whispers **#48**. The ID badge on the console is optional intel and shows her real face.
8. As the team leaves, she pulls the mic to her mouth with her free hand and starts reading again.

### 7.5 The mast and SPANNER (Beat 10, in-engine, player keeps control)
1. **#53**. SPANNER thumbs the detonator. Three flashes at the mast footing through the dust. **#54**.
2. The mast leans **east**. Its red aviation lights trace a long arc down through the brown and vanish, and a steel groan rolls across the field. On STATIC's radio: "Six. One. Thr—", then dead carrier. A 3-frame subliminal of the dark one-way window.
3. At the skid, after "wheels down": SPANNER **taps the skid twice**, the way a crew chief clears an aircraft. **#57**. He walks back into the thinning dust toward the transmitter house, his flashlight shrinking. **#58**.

### 7.6 Outro: HALFMOON (0:45)
1. Inside the helicopter, POV. Lift-off. The rotor-wash ring of salt falls away, and the storm's trailing edge slides east like a curtain.
2. The Package sits hooded opposite. SEXTANT pulls the hood off: it's **the photo's face**, frost melting in her hair.
3. Through the open door the field turns gold, and the fallen mast lies across the dunes like a dropped ladder.
4. She looks straight at the player. **#59**. A 4-frame subliminal of the coffee mug.
5. STATIC, beside her, slowly turns her head to look at Pryce. She heard it too.
6. The sun's glare whites out the frame.

### 7.7 Debrief: SESSION 07, END OF TAPE (1:15)
1. Back in the booth, POV. The fluorescent is steady now. For the first time **the Interrogator's hand** appears (a tweed sleeve, a wedding ring) and lays two photos on the armrest: left, the briefing photo (the Package); right, **a hospital-bed snapshot of the woman from the console**. **#60**.
2. A third item: a signature card reading "RECEIVED — PACKET 7/A — R. TAMBLYN". **#61**.
3. Three clinks. **#62**.
4. **The one-way window lights up from behind.** The listening room: Sorell, arm in a sling, headphones around her neck, a reel-to-reel on the desk playing her own broadcast. In soft focus at the back, a grey-haired man in a Tazraki flight jacket leans in the doorway: **SPANNER, alive**.
5. She leans to the mic. Her voice comes through the booth speaker, the same voice the player has heard all mission: **#63**. A pause. **#64**.
6. Smash to black. A typed **after-action dossier** with stamps, a cursor and teletype sound:

```
OPERATION SALT MIRROR — AFTER-ACTION — SESSION 07
PACKAGE "SORELL" (EXTRACTED 19 AUG) ..... IDENTITY: UNKNOWN — DETAINED
SORELL, ANIKA (RECOVERED 21 OCT) ......... DEBRIEF ONGOING
BASSAN, T. — "SPANNER" ................... RETURNED TO DUTY
TAMBLYN, R. — "SEXTANT" .................. WHEREABOUTS UNKNOWN
PRYCE, J. — "DRIFT" ...................... ________
```

The blank stays with a blinking cursor. Under it: time, kills, accuracy, headshots, and intel (0/3).

---

## 8. Time of day, lighting and weather plan

### Phase table (driven by a single `StormController.t` from 0 to 1, plus per-room local volumes)

| Phase | Beats | Sun (directional light) | Fog (URP exponential²) | Visibility / far clip | Grade (URP Volume) | Air and audio |
|---|---|---|---|---|---|---|
| **A · Noon glare** | 1–2 | Elevation 72°, behind the player's right shoulder. About 6000 K (#FFF4E5), intensity 3.0. Hard shadows, 2 cascades, 150 m. Aligned to the HDRI's sun. | Density 0.0010, #D6CCB8 (bleached horizon) | 2 km+ / far clip 2500 m | ACES. Post exposure +0.25, contrast +12, saturation −10 (sun-bleached), bloom threshold 1.0 / intensity 0.6, vignette 0.2, grain 0.15. **Heat-shimmer** full-screen pass (masked to lower screen and far depth). | Low wind, metal ticking in the heat, flag flaps, a distant generator. Two **dust devils** spin out on the flats (foreshadowing). |
| **B · Wall on the horizon** | 3–4 | 5600 K (#FFE9CC), 2.6. Shadow strength 0.9. | 0.0025, #CBB08A | 600 m–1 km / far clip 1500 m | Warmer white balance, bloom up slightly | **Haboob wall** visible to the west and advancing. Gusts, snapping tarps, sand streamers across the asphalt. |
| **C · The front** | end of 4 → 6 | A 40 s ramp: 4300 K (#F0B47A), 1.2 → 0.6. Shadow strength 0.5, distance 60 m. | 0.012 → 0.03, #A8764A | 120 → 60 m / far clip 400 m | Saturation −20 toward orange, grain 0.25. The skybox shader lerps the HDRI toward a flat dust gradient. | The roar arrives. Corrugated sheets rattle, doors bang. A screen-space dust overlay and camera-local particle sheets switch on. |
| **D · Brownout** | 7, 8 exterior, start of 10 | 3400 K (#C98A55), intensity 0.25, **shadows off**. The sun is a pale coin painted into the sky shader. | **0.045**, #7A5838 (50% fogged at about 18 m, 90% at about 34 m) | 15–25 m / far clip 120 m | Lifted blacks, crushed highlights, grain 0.3, vignette 0.3 | Full roar. All world sounds switch to the **pre-muffled** variants. Flashlights, chemlights and aviation lights become the light design. |
| **E · Interiors** | 6, 8, 9 | Baked. Hangar: 3000 K tungsten work lights (2 realtime unshadowed spots on the swinging lamps). TX house: emissive orange valve glow and a green scope. B1: sodium yellow, **swapped at runtime for a pre-baked red emergency lightmap set** when the generator dies. B2: 7000 K fluorescents. | Local particle haze only | n/a | Local volumes: **+1.0 EV over 1.5 s on entry** (fake eye adaptation), and −0.8 EV on exit | Hangar storm howl through gaps; generator thump; freezer hum; the breath vapour is visible. |
| **F · Clearing** | rest of 10 | 0.25 → 1.8, 4800 K (#FFD9A8). Shadows back on at 60 m. | 0.045 → 0.008, #C49A6A | 150 m+ / far clip 800 m | Gold grade. The salt pan blooms. **Light-shaft cards** angled to the sun. | Wind falls away and the rotor rises. The storm's trailing wall recedes east. |
| **Booth** | Intro, 5, debrief | Baked. A 4000 K fluorescent with a flicker curve, CRT emissive glow. | none | n/a | Desaturated green-grey, heavier grain, a slight barrel distortion | Fluorescent buzz, reel hiss, spoon clinks, a chair creak |

### The storm, technically
- **One parameter drives everything.** `StormController.t` (0 → 1, animated by Timeline and triggers) drives fog density and colour, sun intensity, colour and shadow distance, skybox tint, camera far clip, Volume weights, particle emission, the wind-audio mix, the dry/muffled audio crossfade, **AI sight and hearing ranges** (120 m → 18 m and 40 m → 15 m), the objective-marker cutoff distance, and the haboob wall's position. Gameplay and visuals can't drift out of sync.
- **The haboob wall** is a curved strip mesh about 3 km wide and 600 m tall, starting 2.5 km west. A Shader Graph material uses two scrolling, tiling dust-cloud textures (fal-generated, seamless), noise-driven vertex billowing, a wispy alpha falloff at the top, and wrap lighting from the sun, and it's fog-blended. As it closes to within 300 m it dithers out, and the near-field system takes over: fog ramp, a full-screen dust overlay with scrolling noise and edge streaks, and 2–3 camera-local soft-particle sheets.
- **Ground streamers:** sand blowing across the floor is a scrolling, alpha-masked decal on the terrain and asphalt, plus low Shuriken wisps.
- **Flashlights are faked.** Each hood's light is an emissive lens plus an additive, depth-faded cone mesh. Only the player's flashlight and the two nearest enemy lights are real spot lights (the forward renderer has a per-object light limit).
- **Readability is designed in:** orange armbands (Guard), white cones (hoods), green chemlights (friendlies), red mast lights (landmark).

### Why this showcases realism inside WebGL limits
1. **Desert noon is cheap realism.** A few big forms, one sun and a high-quality HDRI do most of the work. The quality comes from CC0 PBR materials (sand micro-normals, cracked asphalt, rusted corrugated steel), correct ACES exposure, and hard, crisp sun shadows. Harsh sun hides the lack of realtime GI, because sky ambient plus baked AO reads as correct.
2. **The storm turns WebGL's weaknesses into features.** Fog hides LOD pops and the short draw distance. The far clip drops from 2500 m to 120 m, so draw calls collapse just when the frame budget is needed for particles and characters near the camera. Exponential fog is nearly free in URP. The "volumetric" look is layered fog, a screen overlay and a few large soft particles, not a volumetric renderer.
3. **Interiors are baked** with the GPU lightmapper, with light probes for characters and baked reflection probes: offline quality at zero runtime GI cost. The B1 emergency lighting is a second baked set swapped at runtime.
4. **Colour storytelling carries the cinema:** bleached white → amber → brown → sodium yellow → red → cold blue → gold. It costs nothing at runtime and makes the mission memorable.
5. **Audio realism without mixer effects.** Unity's WebGL audio backend doesn't run audio filters or mixer effects, so the radio band-pass, saturation, squelch tails and the Lady's HF heterodyne are **baked offline** (python with scipy and soundfile, because ffmpeg isn't installed). The storm's muffling is a crossfade between dry and pre-filtered copies of each key loop, and gunfire has close, mid and far layers chosen by distance. Slapback off hangars and tanks plays as separate tail one-shots near large surfaces, and the storm conveniently removes far tails.

---

## 9. Level layout

North is up. 1 column = 5 m and 1 row = 10 m (the map is stretched vertically ×2). The playable square is 400 × 400 m. `*` marks the player route and numbers mark beats.

```
      0        50        100       150       200       250       300       350       400 m
     +|---------|---------|---------|---------|---------|---------|---------|---------+
 400 |~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~ |
     | ~     ~  :::::::::::::::~     ~     ~     ~     ~     ~     ~     ~     ~     ~|
     |    [A] RADAR HILL +18m :                                                       |
     |          :             :                                                       |
     |          :    (( O ))  :                                        [I] MAST 90m   |
 350 |          :          . .:. . . . . . . . . . . . . . . . . . . . . . ./. .\     |
     |          :             :                                               X >>E   |
     |          :::::::::::.:::                n  n  n [E] BARRACKS         \   /     |
     |                                         n  n  n                 +---+          |
     |                     .                                   7*******|*8 |    .     |
 300 | [B] FUEL FARM                           *****************    ***+---+          |
     |                     .      +----------+**                 ***[H] TX HOUSE.     |
     |          o   o   o         |       ***|         +--------+::::::               |
     |                     .      | [D] HGR 1|         |[F] HGR 2     :         .     |
     |          o   o   o  [C]    |     6    |         |   *9   |[G]  :               |
 250 |                     . +--+ |     *    |         | **     |::::::         .     |
     |                       |##| +----------+         +--------+                     |
     |                     . +--+       *5         ****                         .     |
     |                        d         *******d***     APRON / TAXIWAY               |
     |    ============================================================================|
 200 |     |            10***********  d     *4  WWWWWW     RUNWAY 09/27  375 x 30 m| |
     |     |-  -  -  -**-  -  -  -  -  -  -d*-* -  -W -  -  -  -  -  -  -  -  - .-  | |
     |    ============================================================================|
     |, , , , , , ,*,      .           **      [J] WRECK                        .     |
     | [K] SALT PAN LZ                **                                              |
 150 |, , , , ,*, , ,      . . . . .*. . . . . . . . . . . . . . . . . . . . . ..     |
     | , , , ,*, ,d, ,            **                                                  |
     |, , ,(LZ) , , ,             *                                                   |
     | , , , , , , , ,       +---------------+                                        |
     |, , , , ,d, , ,        |    * +----+   S                                        |
 100 | , , , , , , , ,       | O  * |####|   |                                        |
     |, , , , , , , ,        |    * |####|   |:                                       |
     |PIPELINE===============|    3*+----+***|========================================|
     |                       |            +-+|   ******* :    :       :               |
     |                       | O          +-+|:     :  ****2*********   ^ ^:          |
  50 |                       +---------------+           :        :  *^**         ^ ^ |
     |                       [L] PUMPING STATION K-4          :       : ***:*         |
     |                                                                       1       ^|
     |                                    [M] THE WADI (4-8 m deep)    ^   ^     ^   ^|
     |                                                             [N] THE SHELF +25m |
     +|---------|---------|---------|---------|---------|---------|---------|---------+  y = 0
```

**Legend:** `~` dune sea (boundary) · `:` slopes, wadi banks, bunker outline · `.` perimeter fence · `=` runway edges and pipeline · `#` solid buildings · `o` fuel tanks · `O` storage tanks · `n` barracks huts · `W` cargo-plane wreck · `d` drum and pallet cover · `S` siren and relay box · `X` mast (falls east, `>>E`) · `,` salt crust · `^` boulders on the Shelf.

| Key | Area | Size | Elevation | Beats | Notes |
|---|---|---|---|---|---|
| N | The Shelf | 80 × 50 m | +25 m | 1 | Start. Boulders, camo net, sand chute down to the wadi. |
| M | The Wadi | 160 m long, 8–15 m wide | −4 to −8 m | 2 | Ruined well-hamlet walls, capped wellhead, low pipeline overpass to slide under |
| L | Pumping Station K-4 | 80 × 70 m | 0 | 3 | Pump house 25 × 20 m (2 storeys), valve house, 2 tanks Ø20 m, pipe racks, west trench, siren and relay box |
| J | Runway 09/27 and wreck | 375 × 30 m; wreck 30 × 20 m | 0 | 4, 10 | Sand drifts, drum pallets, tow tractor |
| C | Control tower | 15 × 20 m, 14 m tall | 0 | 4 | HMG on the catwalk, rail breaks when fragged |
| A | Radar Hill | 70 × 65 m | +18 m | 4 (backdrop) | Lattice tower and radome, marksman |
| D | Hangar One | 60 × 50 m, 14 m high | 0 | 6 | Disassembled transport plane, scaffolding, crane, mezzanine office |
| E | Barracks row | 6 huts of 10 × 6 m | 0 | 7 | Water tower, power poles, wind socks |
| H | Transmitter house | 15 × 10 m building in a 20 × 20 m yard | 0 | 8 | Console, valve cabinets, back hatch, steel door with a cut-progress state |
| I | Lattice mast | 90 m tall, 4 m base, guyed | 0 | 7–10 | Red aviation lights, 3-segment collapse to the east |
| F | Hangar Two | 50 × 45 m | 0 | 9 | Stairs and freight elevator to the bunker |
| G | Bunker B1 / B2 | B1 70 × 30 m, B2 40 × 25 m | −6 m / −11 m | 9 | Corridors, decon, generator, lab, cold store |
| B | Fuel farm | 60 × 45 m | 0 | 10 | Counterattack flank route |
| K | Salt pan LZ | 75 × 80 m | −1 m | 10 | Blinding white crust, drum cover, helicopter pad |

**Route length** is about 1.1 km: Shelf → K-4 ≈ 230 m, K-4 → Hangar One ≈ 170 m, Hangar One → TX house ≈ 190 m, TX → Hangar Two ≈ 80 m, Hangar Two → LZ ≈ 300 m. Gameplay is linear, with local width: two lanes in K-4, three cover lines on the runway, two routes through Hangar One (floor or mezzanine). The bunker is a separate additive scene loaded during the Beat 8 scene, so the exterior can unload its far LODs.

---

## 10. Asset needs

### Environment and structures (procedural or modular, built in-engine with ProBuilder or C# mesh generation, plus CC0 materials)
| Asset | Notes |
|---|---|
| Terrain 600 × 600 m | Noise heightmap: dunes to the north, flats, the Shelf, and a spline-carved wadi. 4 layers: fine sand, rippled sand, cracked mud, gravel/rock. |
| Backdrop ring | Low-poly dune and mesa silhouettes at 1–3 km, fog-blended |
| Runway / taxiway | Asphalt strips, marking decals, sand-drift decals, broken edge lights |
| Hangar kit (2 sizes) | Arched corrugated wall and roof segments, end walls with sliding door leaves, damaged and gapped panels (for sand sheets), skylights |
| Control tower | 3 storeys, glass cab, external catwalk with a breakable rail |
| Pumping station kit | Pump house, valve house, 2 tanks with ladders and catwalks. **Procedural pipe system** (straights, elbows, flanges, valve wheels, supports), pipe racks, trench, siren pole, relay box (destructible). |
| Radar Hill | Lattice tower and geodesic radome, cable runs |
| Transmitter house and mast | Concrete block, steel door (3 cut states), back hatch. **Procedural 90 m lattice mast** in 3 collapse segments, guy wires, aviation lights. |
| Barracks kit | Prefab hut, water tower, power poles with sagging cables |
| Fuel farm | 6 small tanks, bund walls |
| Bunker kit | Corridor straight, corner, T and stair pieces, blast doors, decon room, generator room, lab, cold-store rooms, freight elevator |
| Perimeter | Chain-link fence (alpha), posts, gates, razor wire, concrete barriers, modular sandbags |
| Wadi ruins | Mud-brick wall pieces (plain, no religious forms), capped wellhead |
| Booth and listening room | Acoustic-tile walls, one-way window (a two-state material), steel chair, cuffs, door |

### Props (fal image-to-3D, or Poly Haven CC0 models where they exist; decimate offline with python, e.g. pymeshlab or fast-simplification, since Blender isn't installed)
Fuel drums (3 variants), jerrycans, wooden crates (3), steel ammo crates, pallets, tarps, tow tractor, fuel bowser (static), generator unit, cable reels, gas cylinders, tool chest, workbench, engine stand, propeller, aircraft engine, office desk, chair and filing cabinet, wall phone, PA horn speakers, siren, **valve transmitter cabinets**, gooseneck microphone, **reel-to-reel deck**, oscilloscope, **5 CRT monitors and cart**, **steel interrogation chair** (also used in the TX house), cot, fume hood, DUSTLINE sample canisters, glass ampoule racks, freezer shelving, plastic strip curtains (procedural), **HF receiver and PRC-77-pattern radio** (STATIC), binoculars, chemlight, gas-cutting torch rig (bottles, hoses, torch), the photo, a clipboard manifest, an ID badge, zip-ties, a coffee mug and spoon, a strobe.

### Vehicles (static or spline-animated only)
Burned twin-engine cargo-plane wreck (static). Disassembled transport plane in Hangar One (fuselage and wing on cradles). **HALFMOON**: an unmarked, sand-painted Huey-style utility helicopter with animated rotors, open doors and an interior. Halvane cargo truck with a mounted MG (spline). Company pickup (static).

### Weapons (fal image-to-3D plus manual cleanup; a first-person version and a third-person LOD)
FN FAL Para, M21-pattern scoped rifle, AKM (plus a taped-flashlight variant), RPK (AKM variant with a long barrel and bipod), Browning Hi-Power, M67 frag, tower heavy MG (static mount). Separate magazine meshes for reloads. **First-person arms:** tan shirt sleeves, fingerless gloves.

### Characters (rigged humanoids sharing the standard animation set)
SEXTANT, STATIC, SPANNER; Concession Guard ×3 variants (rifleman A, rifleman B, gunner), marksman (sand cloak), Foreman (bullhorn); Survey hood ×2 variants; **Dr. Sorell** (wounded variant, plus a sling variant for the debrief); **the Package** (lab coat, plus a hooded variant); the HALFMOON pilot (seated); the Interrogator's forearm and hand only.
**The two faces carry the twist.** Sorell and the Package must be unmistakably different (age, hair, face shape, glasses). **Render both briefing photos in Unity from the actual head meshes** with a period photo filter, so the face in the photo exactly matches the model.
**Extra animations beyond the standard set:** first-person breach kick, hood placement and chemlight crack. SEXTANT: prone with binoculars, zip-tie. Sorell: seated reading at the console. Package: escort crouch-follow. Hood: cutting-torch work loop. Seated helicopter idles. SPANNER: skid tap and walk-away.

### Textures and HDRIs (CC0: ambientCG and Poly Haven)
Sand (fine and rippled), cracked dry mud, desert gravel and rock, cracked asphalt, concrete (raw, painted, stained, floor), corrugated metal (painted and rusty), rusted plate, painted steel, chain-link (alpha), canvas, wood planks, rubber, acoustic ceiling tile, linoleum, frosted metal, plastic, grimy glass.
**Decals:** sand drifts, oil stains, runway markings, **English-only stencils and invented company codes**, bullet holes (concrete and metal), frost, minimal blood.
**HDRIs:** one clear high-sun sky (downsampled to a 2K cubemap), plus a hazy or overcast one (or an offline-tinted variant) for the storm blend.

### VFX
Heat shimmer, dust devils, ground sand streamers, sand sheets through hangar gaps, the haboob wall, the screen dust overlay, camera-local dust sheets, muzzle flashes (4), shell ejection, impact puffs (sand, concrete, metal sparks, wood), tracers, frag explosion with a dust ring, cutting-torch sparks, breath vapour, rotor-wash salt ring, light-shaft cards, flashlight cones, chemlight glow, mast collapse debris, smoking wreck.

### UI
Compass ribbon, objective diamonds with distance in metres (with a storm cutoff and flicker), follow marker on SEXTANT's chemlight, hit marker, directional damage indicator, regenerating-health vignette, ammo and grenade counters, subtitles with speaker callsigns and colours, the teletype title card, the after-action dossier and stats, a pause menu, a CRT scanline shader for the booth montage.

### Audio
- **VO:** 64 scripted lines plus 6 bark pools (about 3 variants × 3 Guard voices), plus hood breath and squelch sets. Generate with fal TTS, then post-process offline (see §8, "Why this showcases realism", point 5).
- **SFX:**
  - Weapons and grenades: 5 weapon sets (close/mid/far shot, tail, mechanics, reload foley, dry fire); grenade pin, throw, bounces (sand, metal) and explosion (near/far)
  - Impacts and movement: impacts; footsteps (sand, gravel, concrete, metal grate, salt crust, wood); body falls
  - Storm: wind (base, gust, howl, sand hiss on metal and on fabric), debris rattle, door banging
  - Facility: siren, PA chime, generator idle and death, transmitter hum, valve-cabinet buzz, freezer hum, fluorescent buzz
  - Booth: reel-to-reel, CRT degauss and whine, spoon clink, chair creak and strap
  - Radio: squelch, static, HF heterodyne, **an original 4-note glockenspiel interval signal** for the Lady
  - Vehicles and set pieces: helicopter (distant, approach, hover, interior, rotor wash), truck engine and MG, mast collapse (steel groans, cable snaps, impact), cutting torch, chemlight crack
- **Music:** original synth/drone cues: intro, tension, firefight, brownout, exfil, debrief sting (fal music generation or minimal hand-layered drones).

### Video and stills for the CRT montage
Satellite-strip still and radome still (fal image), oscilloscope (procedural shader), haboob footage (fal video, about 5 s, 16 mm look), ID photo and hospital photo (rendered in Unity from the character heads), signature card (fal image, English text only, or built in UI).

---

## 11. Scope and risk notes

1. **The storm is the hero tech. Build it first as a vertical slice** (Shelf → K-4 → runway → Hangar One door) with `StormController`, the fog and Volume ramps, the haboob wall and the overlay. If it doesn't look great by week one, everything else is decoration.
2. **Transparent overdraw** on high-DPI Macs is the main performance risk. Mitigations: a render scale of 0.75–0.85 on Retina, a few large soft particles plus one full-screen overlay instead of many small ones, and a far clip that collapses in the brownout.
3. **WebGL audio limits.** There are no filters, mixer effects or reverb zones (verify Doppler and reverb on the target Unity version). All processing is baked offline, and simultaneous voices are capped at about 24.
4. **Hosting limits.** Use Brotli with **Decompression Fallback** (the host sends no Content-Encoding header). Keep every binary ≤ 15 MB: split content into **Addressables or asset bundles per area** (Exterior A, Exterior storm, Bunker, Booth) so no single `.data` or bundle exceeds the cap. The target total is 100–140 MB, comfortably under 256 MB per version and split across publishes of ≤ 64 MB. Textures are 1–2K with crunch compression, using a desktop BC/DXT texture format (test Safari on Apple Silicon early).
5. **CPU budget (single-threaded WebGL).** At most 10 enemies, 3 squadmates and 1 escort: 14 agents. NavMesh is baked and AI perception updates are staggered across frames. Cover nodes are hand-placed per beat, and squadmates follow CoD-style "colour chain" nodes keyed to the player's triggers.
6. **First-person weapon animation** is the biggest content gap, since humanoid animations are assumed but first-person ones aren't. Plan for procedural first-person animation: ADS lerp, kick and recovery, sway, a sprint pose, and a reload built from keyed transforms (tilt, magazine out, magazine in, charge).
7. **The twist must be legible.** The photo/face mismatch in Beat 8 needs both faces to read clearly at 1.5 m in brownout light, so the TX house interior is lit from the valve cabinets and is not fogged. Playtest the question "did you notice she didn't match?". The answer should be "yes, and I went along with it anyway."
8. **fal 3D quality.** Image-to-3D props can be heavy or soft. Budget iterations for the hero props (chair, CRTs, reel deck, transmitter cabinets, FAL, AKM) and accept rougher background props. The fog forgives far props, but the booth forgives nothing.
9. **Content sensitivity.** Follow the guardrails in §2 for every prompt. Watch especially for pseudo-script on generated textures and religious silhouettes in generated buildings, and check bark words before recording.
10. **Escort AI** is kept trivial on purpose: the Package is invulnerable, tethers to SEXTANT, and crouches when shot near. There's no fail state.

**Cut list (cut from the top first):**
1. Heat-shimmer pass.
2. Radar Hill marksman.
3. Beat 10 truck; replace it with a foot wave.
4. Beat 4 tower HMG; replace it with riflemen.
5. The M21 opener; do the synced shot with the FAL's ADS.
6. B2 frost decals and breath vapour.
7. Merge Beat 7 into the Hangar One exit (a shorter storm traverse).

**Never cut:** the storm transition, THE READER scene, SPANNER staying behind, and the reveal behind the glass.

---

## 12. Dialogue script (70 lines: 64 scripted lines and 6 bark pools)

Radio lines are pre-processed (band-pass, squelch). Nearby squad lines play as 3D voices. Subtitles show callsigns. Guard barks are subtitled only as *[shouting, Concession cant]*. Hoods are subtitled *[respirator breath / squelch clicks]*.

| # | Speaker | Line | Trigger |
|---|---|---|---|
| 01 | INTERROGATOR | "Tape's running. Fourth of November, nineteen eighty-six. Session seven. ...Morning, Sergeant Pryce." | Intro shot 1, after three spoon clinks |
| 02 | PRYCE | "Where's Tamblyn?" | Intro shot 3, POV gains limited look |
| 03 | INTERROGATOR | "We were hoping you'd tell us. Saffan Field. From the top." | After #02 |
| 04 | PRYCE | "We went in for a defector. We brought her out. It's all in the report." | CRTs power on |
| 05 | THE LADY | "Six. One. Three. Eight. Two. ...Six. One. Three. Eight. Two." | Reel-to-reel plays during the montage |
| 06 | INTERROGATOR | "Doctor Anika Sorell. Ran a weather program called DUSTLINE for people who don't exist. Sent us five numbers and asked to come home." | ID photo on CRT 3 |
| 07 | INTERROGATOR | "I've read your report. I want what you left out of it. Start on the ridge. And this time, don't skip the radio." | Push into CRT 2 |
| 08 | SEXTANT | "Drift, glass the field. Radome on the hill, hangars on the apron, transmitter mast northeast." | Gameplay start, binoculars up |
| 09 | SPANNER | "Eleven years I kept that field flying. Look at it. They have painted my tower company grey." | Binoculars rest on the tower > 1 s, or 6 s after #08 |
| 10 | SEXTANT | "Two on the pump-house roof. Spanner has left, you have right. On my mark... Mark." | M21 equipped and scoped |
| 11 | SEXTANT | "Clean. Nobody looked up." | Both sentries down. If missed, alt: "He's running. Got him. They'll be twitchy now." |
| 12 | STATIC | "Hold up. I've got the Saffan Lady on the HF. She's on air in daylight. She never does daylight." | 20 m into the wadi |
| 13 | THE LADY | "...Six. One. Three. Eight. Two." | From STATIC's receiver (3D, on her pack) |
| 14 | STATIC | "Sextant. That's our recognition group. She's reading *our* group." | After #13 |
| 15 | SEXTANT | "Numbers are numbers. Get off that band, Static." | After #14 |
| 16 | STATIC | "Two years I've logged that voice. She has never once read the same group twice." | After #15, muttered, near the player only |
| 17 | SPANNER | "Patrol. Three men, coming up the wadi. Let them bunch, then we take them." | Patrol spawns |
| 18 | FOREMAN | "Varr! Varr-ottem! Klaxa, klaxa!" | Fence cut; the alarm triggers (bullhorn) |
| 19 | SEXTANT | "So much for quiet. Pipe racks, Drift! Static, suppress the valve house!" | Siren starts |
| 20 | SEXTANT | "Relay box, north wall of the pump house. Kill it, or the whole field comes running." | 5 s after #19, objective marker appears |
| 21 | SPANNER | "Sextant. Look west." | Wave 2 cleared |
| 22 | SEXTANT | "...How long?" | Look-assist reaches the haboob |
| 23 | SPANNER | "Fifteen minutes. Less. And it is early. Storms here do not come before four." | After #22 |
| 24 | SEXTANT | "Then it's our storm. Runway. Move." | After #23, north gate opens |
| 25 | SEXTANT | "Heavy gun, tower catwalk! Wreck, drums, taxiway. Don't you stop in the open. Frag that catwalk!" | HMG opens fire |
| 26 | PA | "*(chime)* Attention. Storm protocol is in effect. All personnel to shelter. Visibility nil in five minutes." | HMG gunner killed (field-wide horn speakers) |
| 27 | HOUSEKEEPER | "Sextant, Housekeeper. Package confirms cold store, level two. Be advised, Halfmoon will not fly into that. You get one pass when it thins." | After #26 |
| 28 | INTERROGATOR | "You're skipping again, Sergeant. The radio." | Beat 5 flash, taxiway cover at Hangar One |
| 29 | PRYCE | "It was a numbers station. Every station in the world reads numbers." | After #28 (the silhouette lifts her head) |
| 30 | INTERROGATOR | "Not your numbers. Not in daylight." | After #29, smash back to the storm front |
| 31 | SEXTANT | "Storm's on us. Close and dirty from here. Stay on my light." | Chemlight crack at the hangar door |
| 32 | STATIC | "Manifest. 'Cold store, B-two. One guest.' That's under Hangar Two." | Player interacts with the office desk |
| 33 | THE LADY | "Six... one... three..." | Hangar PA horns, distorted RF bleed |
| 34 | SPANNER | "That is not a tape. The mast is so close she bleeds into the speakers. Someone is reading live, inside the wire." | After #33 |
| 35 | SEXTANT | "Then we shut her off on the way. Transmitter house, then Hangar Two." | After #34, objective updates |
| 36 | SEXTANT | "Lights off. Theirs give them away. Ours don't exist." | Exit Hangar One into brownout |
| 37 | STATIC | "Contact close! Hoods! Respirators, ten meters!" | First hood within 15 m |
| 38 | SPANNER | "Sparks, eleven o'clock. They are cutting into the transmitter house." | 40 m from the TX house |
| 39 | SEXTANT | "Drift, door. Hard and fast." | Door hoods dead, squad stacks |
| 40 | SORELL | "...Eight. Two. Who's there? Six. One. Three. Eight. Two. I sent that. I am the one who sent it." | Slow motion ends |
| 41 | SEXTANT | "That's not her." | Photo raised beside her face (look-assist) |
| 42 | SORELL | "Look at me, not the picture. I am Anika Sorell. I have read that group for an hour so you would find me." | After #41 |
| 43 | SEXTANT | "Anybody can read numbers off a pad, ma'am." | Zip-tie animation |
| 44 | SPANNER | "Sextant. She is bleeding." | After #43 |
| 45 | SEXTANT | "Then she'll keep. Our package is in the cold store. Spanner, charges on the mast footing." | After #44, SPANNER exits |
| 46 | STATIC | "That's the voice. Sextant, that's the Lady. Two years. That's her voice." | After #45 |
| 47 | SEXTANT | "It's a radio voice, Static. We go with the photo." | After #46, he hangs up her headset |
| 48 | SORELL | "When the reading stops, they will think I am dead. Make them wrong." | Player within 2 m while turning to leave (whisper) |
| 49 | SEXTANT | "Generator's gone. Flashlights. Short bursts. Check your corners." | B1 lights fail |
| 50 | THE PACKAGE | "Don't shoot, please! I'm Sorell. Six, one, three, eight, two." | B2 cold-store door opens |
| 51 | SEXTANT | "Doctor. We're your ride home. Drift, hood on her." | After #50, interact prompt |
| 52 | HALFMOON | "Sextant, Halfmoon Two-One! I'm feeling my way in at fifty feet. Give me a light on that pan!" | Elevator doors open on the apron |
| 53 | SEXTANT | "Spanner. The mast." | Squad reaches the runway's west half |
| 54 | SPANNER | "She falls east. I know how she falls." | Detonation; the Lady cuts off: "Six. One. Thr—" |
| 55 | SEXTANT | "Hold the pan! Keep her down behind the drums!" | Arrive at the LZ, wave 1 |
| 56 | HALFMOON | "I have your strobe. I have it! Wheels down in ten, get aboard!" | Hold timer ends or wave 2 is cleared |
| 57 | SPANNER | "Go. I have one more thing to fix." | Player at the skid; SPANNER taps it twice |
| 58 | SEXTANT | "Spanner! ...Leave him. Go, go!" | SPANNER walks into the dust |
| 59 | THE PACKAGE | "Thank you, Sergeant Pryce." | Outro, hood off (nobody used his name on the op) |
| 60 | INTERROGATOR | "The mast came down at thirteen forty-two. The reading stopped at thirteen forty-two. We assumed she had, too." | Debrief: two photos placed |
| 61 | INTERROGATOR | "The woman you flew out hasn't told us one true thing in eleven weeks. Somebody in our building swapped that photograph. Commander Tamblyn signed for the packet." | Signature card placed |
| 62 | INTERROGATOR | "And your Mister Bassan walked back into that storm. Nine weeks later he walked across a border with a woman on his back." | Three spoon clinks |
| 63 | SORELL | "Six. One. Three. Eight. Two." | The window lights up; booth speaker (dry voice with a hint of radio EQ) |
| 64 | SORELL | "Hello, Sergeant. You walked past me." | After a 2 s pause, then smash to black |
| B1 | GUARD | "Granno! Granno-vé!" | Guard throws a grenade, or spots one landing |
| B2 | GUARD | "Lokka! Lokka-tai!" | Guard reloading or calling for cover |
| B3 | GUARD | "Varr-ottem! Tesk, tesk!" | Player spotted; flank order |
| B4 | GUARD | "Dobba! Dobba-ka!" | Guard under suppression or diving into cover |
| B5 | FOREMAN | "Ottem-varr! Brusk, brusk! Tesk!" | Foreman orders a push (bullhorn, with whistle) |
| B6 | HOOD | *[respirator inhale] "Hhk... tet." [three squelch clicks]* | A hood spots the player (always with a flashlight sweep onto the player) |

**Concession cant glossary** (an invented work-site pidgin; check every word for accidental meanings before recording): *varr* = contact, enemy · *ottem* = there · *klaxa* = alarm · *tesk* = go around, flank · *dobba* = get down · *lokka* = reloading, cover me · *granno* = grenade · *brusk* = push, advance · *-vé / -tai / -ka* = emphatic suffixes · patrol idle chatter is assembled from the same syllables ("Mekto sulan, fena?" / "Fena, fena.").
**Survey squelch code:** 1 click = hold · 2 = move · 3 = target sighted (the player's warning).

**Casting summary:** INTERROGATOR · PRYCE · SEXTANT · STATIC · SPANNER · SORELL/THE LADY (same voice) · THE PACKAGE · HOUSEKEEPER · HALFMOON · PA · Guard ×3 · Foreman · hood breath set. That's 13 voice presets plus the breath set.
