# OPERATION LAMPLIGHTER

**Mission pitch, slug `night`** · single-mission FPS campaign level · Unity 6 URP · WebGL (desktop Chrome/Safari, mouse + keyboard)

> **Logline.** November 1985. A four-person CIA black-ops team slips into the blacked-out caravan town of Meraz by moonlight to photograph and seize an arms broker the Agency calls "the Lamplighter". A flare goes up directly over their rooftop before anyone has fired a shot. Someone gave the enemy their grid. The rest of the night is a running fight through alleys, a market, an old cistern and a rail yard to a helicopter that may not be on their side. Years later, strapped into a chair, the player is asked one question over and over: *where is the film?*

| | |
|---|---|
| **Operation** | LAMPLIGHTER (team callsign NIGHTJAR) |
| **Date / time** | 9 November 1985, 02:10–02:25 local; gibbous moon three nights from full |
| **Setting** | Walled caravan and rail town of **Meraz**, Saffa Basin province, **Republic of Hazrun** (fictional) |
| **Length** | About 13 min of gameplay on Regular, about 17 min including cinematics |
| **Player** | Elias Crane, CIA Special Activities Division paramilitary officer, callsign NIGHTJAR THREE |
| **Squad** | Walt "Boxer" Harlan (lead), Rafael "Rafe" Ocampo (point), Capt. Dalia Seferi "FLINT" (Hazruni liaison) |
| **Enemies** | 54 total, at most 10 alive at once, in two factions plus one human villain |
| **Footprint** | 380 × 330 m (playable), town walls 210 × 180 m |
| **Design pillar** | **Light is the mechanic.** Darkness protects you and light betrays you: flares, flashlights, lamps, muzzle flash. Late in the mission the enemy arrives with night vision and breaks that rule. |

---

## 1. The pitch in one paragraph

Black Ops always works the same trick: a mission that seems straightforward is retold by a man who can't trust his own memory, to an interrogator he can't see. LAMPLIGHTER uses that on a night raid. For the first five minutes it is a quiet, tense sneak through moonlit palm gardens and across flat rooftops, with whispers, hand signals and a camera with a telephoto lens. Then a parachute flare turns the night white directly overhead. Shadows swing, dogs howl and shutters slam all over town, and the mission turns into a CoD running firefight lit by muzzle flash, green enemy tracers and drifting flares. The squad carries a hooded prisoner they assume is the target, through a market under heavy-gun fire, down into a flooded cistern where the radio dies, and out through a rail yard as foreign advisors in night-vision goggles hunt them. They reach a helicopter. The helicopter changes course. In the debrief we learn that the warm, fatherly handler on the radio all night (**WICK**) is the Lamplighter, and that the processed voice interrogating the player was his.

---

## 2. Setting

### Republic of Hazrun (fictional)
- A landlocked, non-aligned desert republic that grew up around a 1920s international rail concession linking salt flats and copper mines to the coast. Hazrun is poor and proud, and both Cold War blocs court it because of the rail junction. In 1985 the elected civilian government is fragile, and parts of its security apparatus are for sale.
- **Flag:** an ochre field with a black eight-spoked rail wheel. Used on faded posters, the customs-house sign and one water tower.
- **Language and script:** Hazruni is an invented language. Signage uses an **invented geometric script** (a set of about 20 glyphs we design ourselves) with Latin transliteration on official rail and customs signs ("MERAZ · CUSTOMS · H.S.R."). No real-world script and no calligraphy.
- **Architecture:** sun-dried mud brick and lime plaster, flat roofs with parapets, external stairs, timber balconies, iron rain spouts, rooftop water tanks, TV aerials, laundry lines, wooden shutters, corrugated iron. Rail-era structures are made of stone and steel. **No domes, minarets, shrines or religious inscriptions anywhere.** The tallest structures are the citadel, the railway signal tower and the Halt 14 water tower.

### Meraz
- An old caravan and salt town of about 4,000 people, enclosed by a seven-metre mud-brick wall with three gates: the **Water Gate** (a dry culvert under the south wall), the **East Gate** (a vaulted passage with an iron gate) and the **Salt Gate** (steel rail doors in the north-east wall).
- Since the Ninth Bureau occupied the old citadel six weeks ago, Meraz has been under curfew. **Civilians are indoors and never on screen.** We hear them: a kettle, a muted TV, a shutter latching, a radio turned down when the shooting starts.
- **Districts:** the Coppersmiths' Quarter (SW), the Customs House compound (centre), the covered Souq (E), the Citadel (NW, off-limits), the Old Rail Yard (NE). Outside the walls are Jackal Ridge, the dry wadi, the Gardens, and the salt pan locals call **"the Glass"** with the derelict pumping station **Halt 14**.

### Sensitivity guardrails (apply to every asset and line)
- No real countries, militant groups, religions or ethnic groups as the enemy. No religious audio (no calls to prayer, chants or sermons) and no religious imagery.
- The enemy is a **rogue state-security unit selling itself** plus **sterile foreign "advisors" of an unnamed bloc**, and the true villain is **an American**.
- The Hazruni character on the team (Seferi) is competent, central and loyal. She is never comic relief and never the traitor.
- Enemy uniforms are generic olive fatigues and berets. No headscarf-as-villain shorthand.

---

## 3. Factions

| Faction | Who they are | Look | Gameplay role |
|---|---|---|---|
| **NIGHTJAR** (CIA Special Activities, deniable) | Four-person paramilitary team inserted by helicopter 4 km out | Dark sterile fatigues, chest rigs, knit caps, faces blacked with cork; no flags or patches | Player and squad |
| **WICK** | The team's Agency handler, apparently on an airborne radio relay. Real name in the end-card: G. LISLE | Voice only until the final shot (grey cardigan, reading glasses, a cigarette) | Radio voice; secretly **the Lamplighter** |
| **CHANDLER 2-1** | Contract helicopter crew flying an unmarked twin-engine utility helicopter | Pilot voice and door gunner (backdrop) | Scripted exfil; diverted at the end |
| **The Ninth Bureau ("the Ninth")** | A rogue directorate of Hazrun's state security under **Col. Tarek Vossam**, renting itself out as muscle for the arms meet | Olive fatigues, black berets or soft caps, canvas webbing; patch with nine black bars under an open eye; AKM and RPK; some with rifle-mounted flashlights | Bulk enemy (48 of 54) |
| **The Advisors** | Specialists from an unnamed foreign bloc who are buying the goods | Grey-green coveralls with no insignia, balaclavas, early **night-vision goggles** with a single green lens glow, suppressed short carbines | Elite enemy (6). Darkness does not hide you from them. |
| **The Lamplighter** | The broker selling **krytron trigger switches** (shock-proof steel cases) to the Advisors, and selling them an Agency investigator as a bonus | A silhouette behind a lit window until frame four develops | The man you are sent to identify. It is Wick. |
| **The Package** | **Martin Ashby**, an investigator from the Agency's Inspector General's office who disappeared in Hazrun three weeks ago while auditing Wick | Hooded, wrists bound, bruised civilian clothes | Escort target (invulnerable, carried by Harlan) |

---

## 4. Player character and squad roster

| Name | Callsign | Role | Personality | Voice |
|---|---|---|---|---|
| **Elias Crane** (player) | NIGHTJAR THREE | Rifleman and photographer: he carries the camera | Quiet, observant and stubborn, a former Army Ranger who notices details and can't stop. He speaks little in gameplay and is fuller in the interrogation. | Late 20s, flat Midwestern, tired and dry. A low, controlled, hoarse voice in the chair; clipped on the radio. |
| **Walt "Boxer" Harlan** | NIGHTJAR ONE / ACTUAL | Team leader | 46, a Vietnam-era veteran of covert ops. Unhurried, paternal and sardonic. He trusts the chain of command until the moment he stops. | Low gravel with a West Texas burr. He almost never raises his voice; when he does, it hits hard. |
| **Rafael "Rafe" Ocampo** | NIGHTJAR TWO | Point man and demolitions | 31, a former Marine Force Recon. Restless, funny and a superstitious gear-checker. He jokes in whispers to stay calm. | Quick New York cadence, a bright tenor; a gleeful whisper in stealth and a shout in combat. |
| **Capt. Dalia Seferi** | FLINT | Liaison from Hazrun Army intelligence | 36, educated at a foreign staff college, precise, and grimly patriotic. She is working with the CIA because the Ninth is arming a coup against her own government. She knows Meraz from childhood summers. | Measured, formal English with a light, non-specific accent. Low and calm, and drier than Harlan. |
| **Wick** | WICK | Handler (radio) | Avuncular and reassuring, and the only one who never sounds afraid. He calls Crane **"son"** exactly once on the radio (D16), and that word pays off in the debrief. | 50s, warm Mid-Atlantic, the cadence of a late-night radio host. **The same actor and TTS voice as the Interrogator.** |
| **Interrogator** | n/a | Unseen questioner | Patient, precise, faintly amused | Wick's voice run through an on-screen **voice-scrambler box**: about 3 semitones down, band-limited, with light ring modulation. It goes clean at the reveal. |
| **Martin Ashby** | "the Package" | Escort | Terrified, sharp and desperate to be believed | 40s, hoarse, educated East Coast, breaking voice |
| **Chandler 2-1 pilot** | CHANDLER | Exfil helicopter | Laconic contractor | Gruff and calm over rotor noise and intercom compression |
| **Station TALLOW** | n/a | Numbers station | n/a | A synthetic female voice reading in English, preceded by an **original** six-note music-box interval signal |

**Player loadout (four period weapons):** MP5SD-pattern suppressed SMG (start, slot 1), CAR-15 carbine (slot 2), and AKM and RPK as enemy pick-ups. Also 3 M67-pattern frag grenades, a combat knife (takedowns), field binoculars and a 35 mm SLR camera with a 300 mm lens.

**Feel targets (CoD arcade):** health regenerates (3 s delay, 2 s to full); sprint, slide and mantle; quick TTK (an AKM kills the player in 4–5 body hits on Regular and an enemy in 3); ADS with a 0.18 s raise; readable recoil kick plus recovery; empty reloads slower than tactical ones.

---

## 5. Premise and interrogation framing

### The present: SESSION 04
An unmarked concrete room at an unknown site. Crane is strapped into a steel chair with leather wrist and chest straps. Above him a single bare bulb on a cord sways slightly and never stops. In front of him is a one-way mirror that shows only his own silhouette. To his left is a steel trolley carrying three CRT monitors, a reel-to-reel tape deck, a shortwave receiver and a **grey voice-scrambler box with a toggle switch and a needle VU meter**. The interrogator is never shown above the wrist: grey cardigan cuff, leather glove, cigarette. The CRT header reads `SUBJECT: CRANE, E. / SESSION 04 / 11-11-85`.

**What the interrogator says he wants:** "the truth about Meraz", meaning who compromised the team.
**What he actually wants:** *the film*, and whoever else has seen frame four.

### The night: the mission
NIGHTJAR was briefed to (1) photograph the Lamplighter at a meet with Colonel Vossam and an Advisor, then (2) seize him. In fact, Wick *is* the Lamplighter. He arranged the meet to sell krytrons and the abducted investigator Ashby to the Advisors, and he threw in a deniable CIA team as a sweetener. The flare that compromises the team was called in by Wick's own numbers broadcast. The team survives and escapes with Ashby, and the flare's ignition gives Crane an accidental fourth frame of Wick's face in the window.

### Clue trail (planted in gameplay and paid off in the debrief)
1. **The handset sync (Beat 4).** The silhouette in the window raises its handset exactly as Wick speaks (D16). Its gestures are driven by Wick's audio waveform.
2. **"Son" (D16 → D65).** Wick says it once on the radio. The unscrambled interrogator says it at the reveal.
3. **7-4-4-1-9.** The number appears in the intro montage, is read faintly on a guard's transistor radio (D17), and Seferi decodes it as the team's own rooftop grid (D18). It closes the mission (D68).
4. **The warm radio (Beat 6).** The radio set in the Lamplighter's office is still lit, and its dial shows **NIGHTJAR's own net frequency, 38.90**, the same number seen on Crane's radio in the intro montage. This is optional and rewards players who look.
5. **Every route Wick gives is an ambush (Beat 7).**
6. **Four frames, not three.** As the flare ignites, Crane's camera fires a frame by reflex: the HUD contact strip flashes frame 4, which is pure white. In the interrogation Crane says "three photographs". The debrief shows frame four developing.

### Flash rules
- **Major flashes** (about 12 s each, two per mission): hard cut to the room, accompanied by a tape-stop audio effect, scanlines and a chromatic-aberration spike.
- **Subliminal flashes** (2–6 frames each, three per mission): the bulb at flare ignition (Beat 5), Crane's own strapped wrists when he first sees the hooded man in the chair (Beat 6), and the VU needle jumping when Wick goes silent (Beat 7).

---

## 6. Mission flow, beat by beat

**Timeline:** gameplay ≈ 13:00 · Intro 1:30 · Title 0:10 · Flash A 0:12 · Flash B 0:12 · Outro 0:50 · Debrief 1:10.

**Enemy archetypes**

| Type | Description | Health | Role |
|---|---|---|---|
| **N-RIF** | Ninth rifleman, AKM | 100 | Standard |
| **N-LT** | Ninth rifleman with a rifle-mounted flashlight (spotlight with cookie plus a cone mesh) | 100 | Stealth and darkness tell |
| **N-SUP** | Ninth support gunner, RPK | 120 | Suppression, slower movement |
| **ADV** | Advisor with NVG and a suppressed carbine | 150 (armour) | Flanks and throws grenades often; ignores the darkness modifier |
| **HMG** | Gunner on the signal-tower heavy machine gun, green tracers | 100, exposed head | Can be killed |

---

### Beat 1: JACKAL RIDGE (0:00–1:00)
- **Location:** a rocky ridge 12 m above the wadi, 150 m south-west of the town wall. Meraz is a dark, flat silhouette under the moon, and one sodium lamp is unlit over the Customs House.
- **Gameplay:** the player starts prone. Look around, then use binoculars (hold `B`) to tag **two blacked-out trucks** on the north road. They run with no headlights, only red blackout marker lamps, on a spline. This teaches prone and crouch, tagging, and that walking is silent while sprinting makes noise.
- **Squad:** prone in a line. Harlan has field glasses, Ocampo checks the MP5SD suppressor, and Seferi reads a map by red-lens flashlight. They rise together on D05. In the cold desert night their breath fogs as they whisper (a cheap particle puff on each VO line).
- **Encounter:** none. Ambience: distant dogs, wind in the scrub, a generator, a truck engine fading into the walls.
- **Objectives and markers:** `OBSERVE: Tag the convoy (0/2)` → `INFILTRATE: Water Gate culvert` (diamond marker with distance).
- **Radio:** D03–D05.
- **Transition:** as the trucks enter the town, a generator coughs and **the sodium lamp over the Customs House flickers on**, signalling the meet. The squad walks the ridge path down into the wadi. Checkpoint at mission start.

### Beat 2: THE GARDENS (1:00–2:30)
- **Location:** date palms and fruit trees in walled plots, a grid of 1 m-deep dry irrigation channels, reed beds, and a mud-brick pump house with a diesel pump. Moon shadows of palm fronds stripe the ground.
- **Gameplay:** scripted stealth, CoD-style. **Detection model:** an enemy's sight range is 35 m in moonlight, 12 m in deep shadow and 45 m inside his flashlight cone, halved when the player is crouched in a channel. A patrol pair walks the bank above the channel and **sweeps its flashlight across it** in a scripted near miss (D07 bark). The player then takes down the pump-house sentry with a knife from behind or a suppressed headshot. Harlan and Seferi kill the culvert sentry together, who is lit only by his cigarette ember (a 0.5 m point light).
- **Squad:** follows colour-chain nodes along the channel and freezes (crouch idle) when the patrol is near. Ocampo leads.
- **Encounter (4):** 2 N-LT (patrol loop that exits the play space and despawns), 1 N-RIF (pump house), 1 N-RIF (culvert).
- **Fail state:** detection triggers D09 and gives a 3 s window to silence the spotter. After that, `NIGHTJAR COMPROMISED` and a restart from the checkpoint. This keeps stealth tight and enemy counts bounded.
- **Objectives:** `INFILTRATE` marker, plus an optional `TAKE DOWN` icon over the pump-house sentry on D08.
- **Transition:** the squad drops into the culvert mouth. Checkpoint.

### Beat 3: WATER GATE AND COPPERSMITHS' ROOFS (2:30–4:00)
- **Location:** a 30 m dry culvert under the south wall, 2 m high, with one grate halfway through where moonlight pours in. It opens into a cistern yard in the Coppersmiths' Quarter. A drainpipe and stacked crates lead up to flat roofs at 4–7 m with parapets, water tanks, aerials and **laundry lines with pale sheets moving in the breeze**. In the workshop courtyards below, **hanging copper pots and trays** catch every light source (a cheap specular showcase).
- **Gameplay:** mantle onto the roofs, crouch-run along them, and make one scripted-safe 1.5 m gap jump. **Synchronised takedown** of two rooftop sentries (D10): Harlan kills the left one when the player fires on the right, or automatically after 8 s. Ocampo distracts the balcony guard below with a pebble (D11); the player can kill him or slip past. Two alley patrollers pass under the laundry lines; the player can let them go (they despawn) or kill them.
- **Encounter (5):** 2 N-RIF (roofs), 1 N-RIF (balcony), 2 N-LT (alley).
- **Objectives:** `REACH THE OVERLOOK`.
- **Transition:** the player reaches the parapet of the coppersmith's workshop roof facing the Customs House. A prompt to go prone appears and the camera equips automatically. Checkpoint.

### Beat 4: THE MEET (4:00–5:00)
- **Location:** the overlook roof, 45 m west of the Customs House courtyard. The courtyard holds the two trucks under the **sodium lamp's orange pool**. Ninth soldiers are opening small steel cases (krytrons, packed in foam). Col. Vossam walks between the trucks, a grey Advisor stands smoking by the arcade, and a second-floor window glows with a **desk lamp**, a figure moving behind it.
- **Gameplay:** **camera tool.** ADS brings up the viewfinder with a split-prism focus circle, and the mouse wheel focuses. A face held in focus inside the circle for 1 s triggers the shutter clack and film-advance sound, and a frame thumbnail appears in the HUD contact strip. Target 1 is Vossam (moving), target 2 the Advisor (turns away at intervals). Target 3, the window, can only ever be a silhouette. Framing it for 2 s triggers D15. On D16 the silhouette raises its handset in sync with Wick (clue 1). Three seconds later a guard's transistor radio in the courtyard reads the numbers in positional audio (D17), and Seferi decodes them (D18).
- **Squad:** prone along the parapet. Harlan uses binoculars and Ocampo watches the roofs behind them.
- **Encounter:** the courtyard occupants are present but passive; they are counted in Beat 5.
- **Objectives:** `PHOTOGRAPH: Colonel ✓ / Advisor ✓ / Broker (silhouette)`.
- **Transition:** D18 → **Flash A** → back in gameplay, a Ninth soldier at a flare mortar in the courtyard shouts D22. **A parachute flare ignites 40 m directly above the team's roof.** Crane's camera fires frame 4 (white), and a subliminal frame of the bulb flashes.

### Beat 5: WHITE SUN (5:00–6:30)
- **Location:** the overlook roofs, the adjoining roofline and the courtyard edges.
- **The moment:** hard pink-white light. Every shadow on the roofs swings as the flare sways on its parachute. Dogs erupt across town and shutters bang. Post-exposure dips 1 EV for half a second (eye adaptation), then recovers.
- **Gameplay:** the fight goes loud. Fight down from the roofs into the courtyard. **Lights are tactics:** shooting out the sodium lamp or a lantern darkens a zone. The **flare-mortar crew** relaunches a flare every 30 s until it is killed, after which darkness favours the player. A frag or Harlan's scripted grenade sets the second truck burning, which becomes a flickering fire light. Col. Vossam and the Advisor escape in the first truck through the north arch on a spline. This sets up the Advisors' return in Beat 9.
- **Squad:** Harlan and Ocampo suppress from the parapet. Seferi takes the external stair to flank and calls out balcony gunners.
- **Encounter (10, max alive 10):** 6 N-RIF (courtyard), 2 N-SUP (Customs House balconies), 1 N-RIF (flare-mortar crew), 1 N-RIF (reinforcement from the north arch).
- **Objectives:** `CLEAR THE COURTYARD` → `BREACH THE CUSTOMS HOUSE` (door marker).
- **Transition:** the squad stacks on the Customs House door. Checkpoint.

### Beat 6: THE MAN IN THE CHAIR (6:30–7:15)
- **Location:** the Customs House interior: an entry hall in patterned cement tile, a timber staircase, and a second-floor office containing a desk lamp, a radio set, an open safe, rail maps and stencilled krytron crates.
- **Gameplay:** door-kick breach with 0.4× slow motion for 2.5 s against two enemies in the hall, then the stairs. The office is empty. **The lamp is still swinging, the handset dangles on its cord, a cigarette is still burning in the ashtray, and a roof hatch is open.** A hooded man is tied to a steel chair whose framing deliberately mirrors the interrogation room (a 2-frame subliminal flash of Crane's own strapped wrists). The radio dial shows 38.90 (clue 4). An optional intel item on the desk is a TALLOW broadcast schedule page (collectible 1/3).
- **Squad:** Ocampo clears the office. Seferi cuts the man from the chair but keeps him bound. Harlan shoulders him in a fireman's carry.
- **Encounter (4):** 2 N-RIF (hall), 2 N-RIF (stairs).
- **Objectives:** `SECURE THE PACKAGE` → `ESCORT: Package` (a marker over Harlan; the package is invulnerable).
- **Transition:** out the back door into the Souq.

### Beat 7: THE SOUQ (7:15–9:00)
- **Location:** a covered market 70 × 60 m, with three north-south alleys 4 m wide and three cross-lanes. Canvas and corrugated roofing overhead. Shuttered stalls, strings of bare bulbs on generator wire (a few still lit and buzzing), bolts of cloth, copperware and sacks of spice. At the south-east end is the **East Gate**, a vaulted passage with an iron gate.
- **Gameplay:** a running firefight south-east along Wick's route. Enemies push in from the cross-lanes. **A heavy machine gun on the railway signal tower**, 70 m north, rakes the alleys through the canvas roof with green tracers. The canvas tears (tear decals spawned along the tracer path) and **moonlight shafts stab down through the holes**. Shot spice sacks burst into puffs of particles. At the midpoint a shutter rolls down and the player slides under it. Killing the HMG gunner is a long shot that brings relief. At the East Gate the iron gate is chained from outside, enemies appear on the wall above and behind, and the alley becomes a kill box (D35). The player fights out of the box and doubles back north along the eastern alley to the Well House.
- **Squad:** Harlan carries the package and moves between cover nodes without firing. Ocampo bounds, and Seferi takes point once the trap is sprung. Enemy AI targets the player and squad, never the package.
- **Encounter (10, max alive 8):** wave A has 3 N-RIF and 1 N-SUP. Then the HMG gunner. Wave B (kill box) has 2 N-RIF on the wall, 2 N-RIF in the alley behind and 1 N-SUP.
- **Objectives:** `EXFIL: East Gate` (Wick's route). After D35 it turns red with a strikethrough, `ROUTE BLOCKED`, then `FOLLOW SEFERI`.
- **Radio:** D32–D39. At D37, Wick's answer comes after an unnatural 1.5 s pause, and a subliminal frame of the VU needle jumps.
- **Transition:** a trapdoor in the Well House, then a 1 s fade while the package is lowered down the ladder. Checkpoint.

### Beat 8: THE CISTERN (9:00–10:00)
- **Location:** an old stone cistern: a vaulted chamber 20 × 15 m on squat columns, flooded ankle-deep (a normal-mapped water plane with a baked reflection probe), feeding a 30 m tunnel north to the rail yard. Two grates let in thin moon shafts.
- **Gameplay:** a quiet breather. The radio hisses and then goes silent. D40–D41 are spoken calmly. Then a Ninth flashlight squad enters from the far end (D42) and the fight is close-quarters, in near-total darkness. **Muzzle flash is the main light:** every shot strobes the vaults. The player's flashlight (`F`) shows you the room and shows them you. Seferi's red light stays low.
- **Squad:** Seferi leads, Harlan and the package are in the middle, Ocampo is the rear guard.
- **Encounter (5):** 3 N-LT (flashlight squad), 2 N-RIF (at the exit ladder).
- **Objectives:** `FOLLOW SEFERI` → `REACH THE RAIL YARD`.
- **Transition:** at the ladder, **Flash B**. Then the team climbs out among the boxcars.

### Beat 9: THE RAIL YARD (10:00–11:45)
- **Location:** an old Hazrun State Railways yard inside the north-east wall, 80 × 45 m. Three sidings, rusted boxcars, a tank wagon, a water crane, ballast heaps and the signal tower (with the HMG gunner if he survived Beat 7). The **Salt Gate** is a pair of steel rail doors in the wall. The citadel looms west, and its **illumination mortar** drops a flare over the yard every 25 s, so the boxcar shadows keep sliding across the gravel.
- **Gameplay:** hold the yard. Ocampo plants a charge on the Salt Gate (a 90 s timer). The Advisors arrive from the citadel side (D48–D49). **Their goggles cancel the darkness modifier,** and you see their green lens glow before you see them. They flank through the boxcars and throw grenades. Lamp posts can be destroyed. The tank wagon is a one-time scripted fireball if shot, a big light pulse for little cost.
- **Squad:** Ocampo is at the gate and invulnerable while planting. Harlan and the package shelter in an open boxcar. Seferi fights alongside the player.
- **Encounter (10, max alive 8):** wave 1 is 4 N-RIF. Waves 2 and 3 are 3 ADV each.
- **Objectives:** `DEFEND: Ocampo 1:30` → `GO: Salt Gate`.
- **Radio:** D46–D51. Wick's voice returns and is too calm.
- **Transition:** the Salt Gate blows. The shockwave raises a dust cloud that glows in the flare light, and the team spills out onto the salt pan. Checkpoint.

### Beat 10: THE GLASS (11:45–13:00)
- **Location:** a white salt crust that **glows under moon and flares** (a high-albedo realism showcase, with crunching footsteps). The rail line runs on a 1 m embankment, the only cover. **Halt 14** is a derelict pumping station beside an 18 m steel water tower that carries the faded Hazrun rail-wheel emblem. The LZ is next to it, 120 m from the gate.
- **Gameplay:** a 120 m dash under fire using **bounding overwatch**: the squad leapfrogs along embankment cover nodes while the player covers, and then the player runs. Enemies fire from the wall behind with green tracers arcing over the pan. A scripted truck arrives on the north road and drops 4 riflemen. **Chandler 2-1** sweeps in low over the water tower on a spline. Its spotlight crosses the pan and the door gunner hoses the wall with red tracers. The player sprints to the helicopter door and presses `E` to be hauled in (a hands-reaching animation).
- **Squad:** Harlan runs first with the package, and Ocampo and Seferi bound with the player. If the player lingers, the squad shouts at him (a reuse pool).
- **Encounter (6, max alive 6):** 2 N-SUP (wall), 4 N-RIF (truck dismounts).
- **Objectives:** `EXFIL: Halt 14` (a marker with a distance countdown) → `BOARD THE HELICOPTER`.
- **Transition:** hands grab the player and the scene cuts to the outro.

### Encounter totals

| Beat | 2 | 3 | 5 | 6 | 7 | 8 | 9 | 10 | **Total** |
|---|---|---|---|---|---|---|---|---|---|
| Enemies | 4 | 5 | 10 | 4 | 10 | 5 | 10 | 6 | **54** (48 Ninth + 6 Advisors) |
| Max alive | 2 | 3 | 10 | 4 | 8 | 5 | 8 | 6 | **≤ 10** |

Optional intel (3): TALLOW schedule (Beat 6), a Ninth duty roster naming "the American guest" (Beat 7, a souq stall), and a krytron shipping manifest signed with a lamp glyph (Beat 9, the boxcar).

---

## 7. Cinematics, shot by shot

All cinematics run in-engine with Timeline (and Cinemachine 3.x if installed) and stay first-person or locked-off. Interrogation-room shots are first-person from the chair, as in Black Ops.

### Intro: "SESSION 04" (≈ 1:30)
1. **Black.** Tape hiss, a reel-to-reel *clunk*, and the TALLOW music-box interval signal playing slightly warped.
2. **ECU of a CRT powering on.** A white dot blooms into a scanline raster. Typed text: `SUBJECT: CRANE, E. / SESSION 04 / 11-11-85 / 03:12`.
3. **POV from the chair.** Crane looks down at his strapped wrists (white knuckles), then up at the swaying bulb, then into the one-way mirror, where his silhouette and the trolley of CRTs are reflected. The grey **voice-scrambler box** on the trolley is in clear view, its needle twitching. Fluorescent hum.
4. **A gloved hand enters from the left** and drops a contact sheet onto the steel tray. Two frames are circled in grease pencil (Vossam, the Advisor). Frame three is a black rectangle with a silhouette. **The fourth frame has been cut off the sheet.**
5. **Interrogator (D01).** The needle on the scrambler moves with his voice.
6. **CRT montage (≈ 30 s, rapid cuts across three screens):** grainy black-and-white stills of Meraz's walls under moonlight; a Hazrun rail map with grid squares; `7 4 4 1 9` scrolling in green phosphor; a film-strip of Col. Vossam; a photo of Ashby stamped `MISSING`; a dossier headed `LAMPLIGHTER — IDENTITY UNKNOWN`; a radio dial frozen at **38.90**; a newsreel-style card reading `REPUBLIC OF HAZRUN — JUNCTION OF THE SAFFA BASIN`. Every cut lands on a beat of a low synth pulse.
7. **Crane (D02)**, his voice low over the montage.
8. **The bulb flares and blows out.** Hard cut to white.

### Title card (≈ 10 s)
- Black screen. A typewriter strikes out **L A M P L I G H T E R** in a stencil typeface, the letters slightly misregistered. Below it, typed line by line with carriage-return dings: `MERAZ — REPUBLIC OF HAZRUN` / `9 NOVEMBER 1985 — 02:10 LOCAL` / `ELIAS CRANE — CIA SPECIAL ACTIVITIES DIVISION`. A single row of numbers ticks along the bottom. CRT flicker, then a crossfade to first-person on Jackal Ridge. **There is no loading screen:** the ridge was loaded behind the card.

### Flash A: "Describe the third" (≈ 12 s, after D18)
1. Tape-stop audio and a jump cut to the chair. The bulb is still swinging.
2. One CRT shows frame three, the silhouette at the window, grainy and pushing in slowly.
3. D19 → D20 → D21. On "Elias?" the CRT dissolves into static and a static burst cuts back to the roof, just as D22 is shouted and the flare launches.

### Flash B: "Why did you stop answering" (≈ 12 s, at the cistern ladder)
1. Water drips and becomes the tick of the reel-to-reel.
2. The chair POV. The gloved hand turns a dial on the shortwave, and NIGHTJAR's radio traffic plays back in reverse.
3. D43 → D44 → D45. On "Interesting", the needle on the scrambler box spikes, and a cut back to Crane's hand on the ladder rung.

### Outro: "Alternate LZ" (≈ 50 s, in the helicopter)
1. Hands haul Crane through the door. Red cabin light, rotor roar, and the door gunner's red tracers stitching the wall. Meraz falls away, with fires and two flares drifting over the town.
2. Harlan lays the package on the cabin floor and pulls the hood off: **Ashby**, bruised and blinking. D55 → D56.
3. Ashby grabs Crane's sleeve. D57 → D58. His eyes go to the camera around Crane's neck.
4. **Crane's POV:** his thumb winds the rewind crank, pops the camera back and pulls out the film canister. He presses it into **Seferi's** hand. She looks at him once and slides it into her boot. No dialogue.
5. The intercom crackles: D59 → D60 → D61.
6. Ashby's face drains. The cabin light flickers from red to white. **Cut to black.**

### Debrief: "Who's asking" (≈ 1:10)
1. **The chair again.** The bulb is still swinging, and the contact sheet still sits on the tray.
2. **Interrogator (D62).** He says each squadmate's name as a tape reel turns.
3. **D63.** The gloved hand taps the empty space where frame four was cut away.
4. **Crane (D64).** A long silence follows, filled only by fluorescent hum.
5. **The gloved hand reaches to the scrambler box and flips the toggle.** The needle drops to zero, and the voice that follows is warm, clean and familiar: **Wick (D65).** For the first time the hand lifts into the light: a grey cardigan cuff, reading glasses held loosely, the same cigarette as in the Customs House.
6. **Insert on the CRT:** a darkroom tray under red safelight, where frame four develops. Wick's face fades up in the Customs House window, lit flat white by the flare, handset at his ear.
7. **Crane (D66).** POV: the strapped hands slowly unclench, empty.
8. A long pause. The reel-to-reel starts rewinding by itself. **Wick (D67).**
9. TALLOW's interval signal plays, then **D68.** The bulb stops swinging and the scene cuts to black.
10. **End card on a CRT:**
    ```
    OPERATION LAMPLIGHTER ........ STATUS: [REDACTED]
    PACKAGE (ASHBY, M.) .......... DECEASED IN TRANSIT
    FILM ......................... NOT RECOVERED
    NIGHTJAR ONE / TWO ........... IN CUSTODY
    FLINT (SEFERI, D.) ........... REPATRIATED
    ASSET CRANE, E. .............. SESSION 05 SCHEDULED
    AUTHORISING OFFICER .......... WICK (LISLE, G.)
    ```
    Then a stats page: time, kills, accuracy, headshots, intel 0–3/3, and stealth takedowns.

---

## 8. Time of day, lighting and weather plan

### The night
- **Time:** about 02:10 local. **Gibbous moon** at 48° elevation, azimuth 150° (SSE), so shadows fall long to the NNW and the team's moonlit faces point toward the walls during the approach. The sky is clear and dry with dense stars, and a thin haze near the horizon from the afternoon's wind.
- **Weather:** a light NW breeze of 3–4 m/s. Palm fronds, laundry, canvas and dust motes move with it (vertex-shader wind plus particles). It is cold, about 6 °C, so **breath fogs** on every whispered line and on heavy breathing after sprinting. That one particle effect does a lot for realism.
- **Colour plan:** moonlight is a desaturated silver-blue (around #9FB4D6 before grading). The practical lights are **sodium orange** (about 2000 K), kerosene amber and the tungsten desk lamp. Flares are a harsh magnesium **pink-white**. **Tracers are green for Ninth and Advisor fire and red for the helicopter**, a period-accurate split that also reads instantly. Grading uses a Purkinje-style shift: low-luminance areas desaturate toward blue, and hot sources keep their saturation.

### Why night is the right way to show realism in WebGL
1. **Darkness hides distance.** View distance falls off into blue-black haze beyond about 150 m. We set fog to absorb everything past about 220 m, cull aggressively, and avoid the problems that usually make WebGL look cheap: LOD pops, flat lighting on distant buildings, and low-resolution terrain.
2. **The look depends on contrast, not light count.** One hero directional light plus a few hot practicals is exactly what URP forward handles well. Moving shadows from a flare or the helicopter spotlight look expensive but cost one shadowed light.
3. **Dark scenes forgive asset fidelity.** fal-generated props and 1–2K CC0 textures hold up when the detail budget goes to the 15 m around the player and to whatever a light hits.
4. **Audio benefits most.** At night every gunshot, its slap-back off the mud walls, and the supersonic crack of a near miss stands out against near-silence.

### Light budget (per frame, WebGL 2)

| Source | Mode | Shadows | Concurrency | Notes |
|---|---|---|---|---|
| Moon | Mixed directional (baked GI plus realtime) | Realtime soft, 2 cascades, 60 m distance; shadowmask beyond | 1 | Progressive GPU lightmapper bakes moon and sky GI on static geometry; light probes for characters |
| Sky | Poly Haven night HDRI (verify a suitable moonlit or starry one) or a procedural sky with a star cubemap | n/a | n/a | Exposure tuned low; also feeds reflection probes |
| Parachute flare | **Spotlight pointing down, 160° cone** (one shadow map instead of six for a point light), plus an unshadowed point light for upward fill | 1 shadowed | 1 shadowed + 1 unshadowed | Intensity flicker, 25–35 s burn, pendulum sway on the parachute, smoke trail |
| Muzzle flash | Pooled point lights, 50 ms | None | ≤ 4 | Warm; also drives a brief exposure pulse in the cistern |
| Flashlights and searchlight | Spotlights with cookies plus additive cone meshes | Player flashlight only | ≤ 3 | Cones fake volumetrics, since URP has none |
| Destructible practicals | Realtime unshadowed (baked light can't be switched off at runtime) | None | ≤ 6 per area | Sodium lamp, lanterns, bulb strings, yard lamp posts |
| Static practicals | Baked plus emissive | n/a | n/a | Windows, distant lamps, citadel |
| Fire and explosions | Point light pulses and flicker | None | ≤ 2 | Burning truck, tank wagon, Salt Gate |

- **Target:** at most about 10 visible realtime additional lights and at most 2 shadow casters (moon plus flare; player flashlight in the cistern, where the moon does not reach). Use URP Forward+ if verified on WebGL 2; otherwise use Forward and respect its 8-lights-per-object limit by keeping lights small-ranged.
- **Post stack (URP Volume):** ACES tonemapping, bloom with a lens-dirt texture (flares, lamps, tracers), strong 16 mm-style film grain (it also hides **banding in the dark gradients**, and camera dithering should be enabled too), vignette, slight chromatic aberration, Lift/Gamma/Gain plus a LUT, and SSAO only if the frame-time cost holds up in Chrome. URP has no auto-exposure, so **per-area volumes blend post-exposure** (cistern +0.8 EV, salt pan −0.3 EV) and **scripted pulses** simulate eye adaptation when flares ignite.
- **Atmosphere:** exponential-squared fog in deep blue, soft dust particle sheets that catch flare light, fake light-shaft meshes (moon through grates and torn canvas, the helicopter spotlight), flare smoke, and gate-blast dust.
- **Quality tiers:** High (Chrome, default) and Medium (Safari fallback: render scale 0.8, no SSAO, 1 cascade). HDR rendering if the RGB111110 format is available on Safari, with an LDR bloom fallback otherwise.

### Audio realism plan (top user priority alongside visuals)
- **WebGL constraint:** Unity WebGL plays through Web Audio, and **Audio Mixer DSP effects (reverb, filters) are not available there** (verify on Unity 6). So **reverb is baked into the assets.** Each weapon has three layers: mechanical/close, body, and **zone tail**. Tail variants: open desert (long, with a 0.4 s slap-back off the wall), narrow alley (short flutter), interior (tight room), cistern (2.5 s stone wash) and salt pan (dry, vast). Trigger volumes swap tail sets.
- **Distance:** near and far crossfades for every gun, and **supersonic cracks plus whizzes** for enemy rounds passing within 3 m, the strongest single cue that you are under fire.
- **Ambience:** per-area beds (desert wind, insects, distant dogs, the generator, laundry flap, cistern drips, rail-yard metal ticking) plus spot emitters (transistor radios, the sodium lamp's buzz, a muffled TV behind a shutter).
- **Radio:** processed offline in Python with scipy: a 300–3000 Hz band-pass, soft clipping, a noise floor and squelch tails. Every line starts and ends with a key-up *kssht*. Wick's lines are *slightly* cleaner than everyone else's, a subtle hint that he is closer than he claims.
- **Foley:** footsteps by surface (sand, gravel, mud-brick roof, cement tile, timber stair, steel, shallow water, salt crust), cloth and gear rattle scaled to movement speed, breath when sprinting, and the camera's shutter and film-advance.
- **Music:** none during stealth. A sparse 1980s analog-synth score (pads, a sequenced pulse) enters at the flare and returns at the exfil. TALLOW's music-box interval is the mission's leitmotif.
- **VO:** fal TTS, a distinct voice per character. The Interrogator is Wick's voice processed offline (pitch, band-limit, ring-mod), with the unprocessed take used for D65–D67.

---

## 9. Level layout

**Scale:** 1 character = 5 m horizontally; 1 row = 10 m vertically. North is up. The playable footprint is 380 × 330 m. Numbers in parentheses are beats.

```
  z(m)  +----------------------------------------------------------------------------+   N
        | scrub / rock                                               ,,SALT PAN,,,,,,|
        |                                                            ,,"the Glass",,,|
  300   |                                                            ,(10),,PPPPPP,,,|
        |                    CITADEL (Ninth Bureau HQ)               ,HALT 14,,,,P,,,|
        |                  ###########################################,,,,,,PPPPPP,,,|
        |                  #xOFF-LIMITSxxxx            (9)       RAIL#LZ>,,,,,,,,,,,,|
        |                  #xenemy spawnxxx              BBB BBB YARD#,,,,,,,,H,,,,,,|
  250   |                  #xxxxxxxxxxxxxxx           ===============S===============|
        |                  #xxxxxxxxxxxxxxx                 :        #,,,,,,,,,,,,,,,|
        |                  #xxxxxxxxxxxxxxx    sig.towerT   :cistern #,,,,,,,,,,,,,,,|
        |                  #xxxxxxxxxxxxxxx                 :(8) well#,,,,,,,,,,,,,,,|
        |                  #               ++++++++++++     :        #,,,,,,,,,,,,,,,|
  200   |                  #               +CUSTOMS   +%  % w%  %  % #               |
        |                  #               +HOUSE (6) + %  %  %  %  %#               |
        |                  #               +courtyard +  %  %  %  %  #               |
        |                  #       overlook+(5)       +%  %  %  %  % #               |
        |                  #           (4) ++++++++++++ %  %  %  %  %#               |
  150   |                  # COPPERSMITHS                %  %  %  %  #               |
        |                  # QUARTER                   SOUQ (7)      #               |
        |                  # rooftops (3)              kill box(7b)> E               |
        |                  #                               EAST GATE #               |
        |                  #                                         #               |
  100   |                  ##########W################################               |
        |             Y  Y  Y  Y  Y  Y(2)  Y  Y   THE GARDENS  Y  Y                  |
        |                                                                            |
        |            ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~irrigation~~~~~~~~~                |
        |                                                                            |
   50   |             Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y  Y                 |
        |.DRY WADI...............ridge path..........................................|
        |............................................................................|
        |^^^^^^(1)^^^^^^^^^^^^^                                                      |
        |^JACKAL RIDGE OP^^^^^^                                                      |
    0   |^^^^^^^^^^^^^^^^^^^^^^                                                      |
        +----------------------------------------------------------------------------+
         0         50        100       150       200       250       300       350    x(m)
```

**Legend:** `#` town wall (7 m) · `W` Water Gate culvert · `E` East Gate (the kill box) · `S` Salt Gate (rail doors) · `x` citadel, off-limits, used as an enemy spawn · `+` Customs House compound · `%` covered souq alleys · `w` Well House (cistern entrance) · `:` cistern tunnel (underground) · `T` signal tower with HMG · `=` rail line · `B` boxcars · `P` Halt 14 pumping station and water tower · `H` LZ · `Y` palms · `~` irrigation channel · `^` ridge rocks · `,` salt crust.

**Critical path:** (1) ridge SW → wadi → (2) Gardens → W culvert → (3) Coppersmiths' roofs → (4) overlook → (5) courtyard → (6) Customs House → (7) souq SE to the East Gate kill box (7b) → back north up the east alley → (8) Well House and cistern N → (9) rail yard → S Salt Gate → (10) across the Glass to H.
**Route length:** about 900 m on foot. The path is linear with local width (two or three lanes in the Souq and Yard, multiple roof routes in Beat 3), in the CoD style.

| Area | Size | Elevation | Key sightlines and notes |
|---|---|---|---|
| Jackal Ridge OP | 110 × 30 m | +12 m above wadi | Full view of the south wall and the Customs House lamp; the north-road convoy is visible 300 m away through a gap |
| Dry wadi | 380 × 20 m | −2 m | Sandy bed with crunching gravel at the edges |
| The Gardens | 240 × 50 m | 0 | Channels 1 m deep form a hidden lane network; the pump house sits about 30 m from the W culvert |
| Water Gate culvert | 30 × 2.5 × 2 m | −1 m | One moon grate halfway along |
| Coppersmiths' Quarter | 75 × 60 m | Roofs 4–7 m | Three roof routes converging on the overlook |
| Overlook | Roof at 7 m | +7 m | 45 m to the courtyard centre and 50 m to the office window |
| Customs House compound | 55 × 50 m (courtyard 30 × 25) | 2 storeys | Balconies on the north side, office on floor 2, roof hatch |
| Souq | 70 × 60 m | 0, roof at 4 m | Alleys 4 m wide; the HMG at T looks down the N-S alleys |
| East Gate | Passage 12 m deep | 0 | Wall walk 7 m above, so enemies hold the high ground |
| Cistern | 20 × 15 m chamber + 30 m tunnel | −5 m | Ankle-deep water |
| Rail Yard | 80 × 45 m | 0 | Boxcars form lanes; the citadel spawn doors are 60 m west |
| The Glass and Halt 14 | 80 × 110 m | 0, water tower 18 m | Embankment cover every 15–20 m; the LZ is 120 m from S |
| Citadel | 75 × 60 m | Walls 12 m | Backdrop only: the illumination mortar, enemy spawn doors and searchlight |

The terrain is a 420 × 420 m height-mapped tile. Everything outside the footprint is backdrop: low hills, the road, distant lights of another town 6 km away (a skybox card).

---

## 10. Asset needs

Sources: **fal-3D** = fal image-to-3D (Hunyuan3D or TRELLIS class) from a fal-generated concept image, decimated with `gltfpack` (npm) · **fal-img** = fal text-to-image for decals, posters, photos and documents · **aCG** = ambientCG CC0 · **PH** = Poly Haven CC0 (textures, HDRIs, models) · **proc** = procedural or modular construction in Unity · **fal-audio** = fal SFX and music models · **fal-TTS** = fal speech models.

### Environment (P0 unless marked)
- **Mud-brick building kit (proc + aCG plaster/adobe/brick, vertex-colour grime, decals):** about 16 modules: walls 2/4 m, wall with window, wall with door, corner, parapet cap, roof slab, external stair, arch, buttress, timber balcony, awning, rain spout, shutter (open/closed), door (wood/iron), rooftop water tank, TV aerial.
- **Town-wall kit (proc + aCG):** a 10 m wall segment (7 m tall), corner tower, Water Gate culvert piece, East Gate arch with iron gate, and the Salt Gate steel doors (intact and blown variants).
- **Hero Customs House (kit + unique arcade):** cement-tile floor (fal-img tile pattern or aCG tiles), staircase, office dressing.
- **Souq:** 3 stall modules, tearable canvas roof panels (alpha-cutout with a tear-decal mask), corrugated sheets, wire with bare bulbs, cloth bolts, spice sacks, copperware clusters.
- **Cistern:** column, vault segment, tunnel segment, grate, iron ladder, water plane.
- **Rail yard:** spline-built track, buffer stop, ballast; **boxcar** (fal-3D, P0), tank wagon (fal-3D, P1), water crane (fal-3D, P1), signal tower (kit + steel), lamp posts.
- **Exterior:** terrain with ridge and wadi heightmap, salt-crust material (aCG ground + a custom sparkle in the normal and smoothness maps), sand, gravel, rock (PH scanned rocks), **date palm × 3** (fal-3D trunks plus alpha-card fronds), reeds and scrub cards, irrigation-channel kit, pump house, Halt 14 pumping station and **water tower** (kit + fal-3D).
- **Interrogation room:** concrete walls and floor (aCG), steel chair with straps (fal-3D), bulb on a cord, one-way mirror (reflection probe), steel trolley, 3 CRT monitors (fal-3D) with a scanline render-texture shader, reel-to-reel deck, shortwave receiver, **voice-scrambler box with a working toggle and VU needle** (fal-3D + animated parts), steel tray, contact sheet.

### Props (≈ 40, mostly fal-3D or PH models)
Krytron cases (open/closed), stencilled arms crates (invented stencils), oil drums, diesel generator, kerosene lantern, desk lamp (period), radio set with lit dial, transistor radio, telephone handset on a cord, ashtray and cigarette, open safe, rail maps and documents (fal-img), sandbags, flare mortar tube, the HMG on the tower, handcart, baskets, rugs and textiles (geometric patterns only), motorbike (static), bicycle, laundry sheets (cloth sim or vertex wind), copper pots and trays, chairs and tables, crates of soda bottles, water jugs, a faded Hazrun poster (fal-img, invented script).

### Characters (rigged humanoids assumed, as briefed)
First-person arms and sleeves (team fatigues, wristwatch, a knife sheath visible) · Harlan · Ocampo · Seferi · Ashby (hooded and unhooded) · Ninth soldier × 3 variants (beret, soft cap, helmet) · Advisor × 2 variants (NVG up/down, with a green lens glow emissive) · Col. Vossam (seen only through the camera lens) · door gunner (backdrop) · interrogator's gloved hand and cardigan forearm (first-person-adjacent rig) · Wick's face in the frame-four photo (fal-img portrait of a fictional man, B&W with grain).

**Extra animation needs beyond the standard set:** fireman's carry (walk/run/idle) for Harlan with the package, a hooded seated-bound idle, a prone idle and crawl, a stealth knife takedown pair (attacker and victim), synchronised squad kills (silenced-shot pose), a crouch freeze, a flashlight sweep upper-body layer, a planting-charge loop, a hands-reaching-down helicopter grab, and breathing and look-around idles for cinematics.

### Weapons and tools (fal-3D, hero quality)
MP5SD-pattern SMG · CAR-15 carbine · AKM · RPK · Advisor's suppressed short carbine (third-person only) · M67-pattern frag · combat knife · 35 mm SLR with a 300 mm lens (with an animated rewind crank and back door for the outro) · field binoculars · first-person reload, inspect and ADS animations for the four player weapons.

### Vehicles (scripted and spline-animated only)
Cargo truck × 2 (one with a burnt variant) with red blackout lamps · unmarked twin-engine utility helicopter with rotor blur discs, a door gun and a searchlight.

### VFX
Muzzle flashes (per weapon, flipbook plus light), **tracers** (green and red, HDR emissive with streak stretch), impact puffs by surface (sand, plaster, metal, wood, water, salt, plus a mild blood mist), parachute flare (light, sparks, smoke trail, parachute mesh), frag explosion, truck fire, tank-wagon fireball, gate-blast dust wall, dust motes, **breath fog**, spice bursts, canvas tear decals, bullet-hole decals, NVG lens glow, cigarette ember, a CRT and scanline shader, and the flash-transition shader (chromatic aberration, tape-warp, static).

### UI
A Black Ops-style objective diamond with label and distance; objective list text (a stencil-typewriter font, OFL licence); compass strip; squad callsign tags (green, shown when looked at within 25 m); hit markers; directional damage indicator; frag indicator; a camera viewfinder overlay (split-prism focus, frame counter, contact strip); binocular overlay; the title-card typewriter sequence; subtitles with speaker names; the end-card CRT; a stats screen; and the fail screen (`NIGHTJAR COMPROMISED`).

### Audio (fal-audio, PH and aCG have none, so all generated or synthesised)
- **Weapons:** 4 player weapons × (mech, body, 5 zone tails, far) plus the suppressed MP5SD; enemy AKM, RPK and carbine sets; the HMG; cracks and whizzes; frag explosion; shell casings on stone, metal and sand.
- **Foley:** 8 footstep surfaces × walk/run/land; cloth and gear; mantle and slide; knife takedown; door kick; camera shutter and film advance; binocular handling.
- **World:** flare pop, burn hiss and parachute rustle; mortar thump; truck engine (blackout drive-by and arrival); helicopter (approach, hover, interior); generator; buzzing sodium lamp; dogs (distant pool); shutters slamming; the gate charge.
- **Ambience:** 7 area beds plus spot emitters. Interrogation room: fluorescent hum, tape-reel mechanics, chair creak, strap leather, CRT whine, the scrambler-box toggle.
- **Music:** 3 cues (compromise, exfil, debrief sting) plus the original TALLOW music-box interval.
- **VO:** the 68 scripted lines in §12 plus systemic bark pools: 15 Ninth, 6 Advisor, 12 squad callouts ("Reloading!", "Grenade!", "Contact left!", "Moving!").

---

## 11. Scope and risk notes

| Risk | Impact | Mitigation |
|---|---|---|
| **Hosting limits** (each binary ≤ 15 MB, ≤ 255 files per publish) | Unity's single `.data` file will exceed 15 MB | Keep the core build small and move content into **Addressables bundles per area, each under 14 MB**. Gzip or Brotli with **Decompression Fallback**. Textures at 1–2K with dual DXT and ASTC targets. Audio as Vorbis (VO mono at 22 kHz). Total target ≤ 180 MB across several publishes. |
| **WebGL performance** (single-threaded, no compute) | Frame drops in firefights | The light budget in §8; SRP Batcher and GPU instancing for kit pieces and palms; baked occlusion culling; LOD groups on fal meshes; ≤ 10 NavMesh agents active; pooled VFX, decals and lights; no runtime GI. |
| **Stealth AI complexity** | Overrun and bugs | **Scripted stealth:** fixed patrols, simple vision cones with a light multiplier, and a fail-and-restart instead of a systemic alert state. Squad kills are scripted. |
| **Escort and carry animation** | Missing or broken fireman's carry | Fallback: the package walks bound with Seferi gripping his arm, using a standard walk with a bound-hands upper-body override. The package is always invulnerable and teleports to the squad when off-screen. |
| **fal-3D quality** (topology, baked lighting in albedo, scale) | Props look AI-made up close | Use fal-3D for props and silhouettes only. Retexture tiling surfaces with aCG/PH PBR. Decimate with `gltfpack`. Hero first-person weapons get the most iteration time. Darkness hides minor flaws. |
| **Night readability** | Players lost in black frames | A minimum moon-fill floor; flashlights as enemy tells; subtle rim light from the moon on characters; objective markers always visible; an "assist" brightness slider. |
| **Banding in dark gradients** | Cheap-looking skies and walls | HDR render target where available, camera dithering, film grain. Avoid large smooth dark gradients in the skybox. |
| **Safari WebGL 2 variance** | HDR format and performance gaps | The Medium tier and LDR fallback; test early in Safari. |
| **Invented-language barks via TTS** | May sound like a real language or come out garbled | Keep barks short. Spell them phonetically. Review every generated bark by ear. Run them through distance and radio processing. Reject any take that resembles real speech. |
| **No audio DSP in WebGL** | Flat, dry gunfire | Bake reverb per zone into the tail layers (§8). Pre-process all radio audio offline. |
| **Light-switching realism** | Shooting out a baked lamp leaves its baked glow behind | Destructible lights are realtime and unbaked only (≤ 6 per area). Everything else is baked and indestructible. |
| **Sensitivity** | Representation and rating concerns | The §2 guardrails, applied at asset review: no religious architecture or audio, no real flags or groups, invented script, no civilians on screen, the villain is American and the Hazruni ally is a hero. |

**Cut list, in order, if the schedule slips:** (1) the tank-wagon fireball, (2) the HMG tower (replace with an RPK on a roof), (3) slow-motion breach, (4) intel collectibles, (5) the truck dismount in Beat 10 (spawn from the wall instead), (6) Flash B, (7) the cistern combat (keep the walk-through and dialogue). The **flare moment, the camera tool, the interrogation framing and the debrief twist are never cut.** They are the mission.

**Dependencies:** Timeline (built in); Cinemachine 3.x (optional install); AI Navigation package (NavMesh); a stencil-typewriter font (OFL); `gltfpack` via npm; Python with scipy for radio processing.

---

## 12. Dialogue script (draft, 68 lines)

Radio lines are marked (R). All squad lines in Beats 1–4 are whispered. "Processed" means Wick's voice run through the scrambler chain.

| # | Speaker | Line | Trigger |
|---|---|---|---|
| D01 | INTERROGATOR (processed) | "Meraz. Two in the morning. Tell me about the lamps." | Intro shot 5, contact sheet drops |
| D02 | CRANE | "There weren't any lamps. That was the point. Curfew, a fat moon, and a whole town with its lights off." | Intro shot 7, over the montage |
| D03 | HARLAN | "Moon's up, town's dark. Just like the man promised." | Player gains control on the ridge |
| D04 | WICK (R) | "Nightjar, Wick. Two trucks on the north road, running blacked-out. Your Lamplighter's in one of them. Eyes on." | 4 s after D03; binocular prompt |
| D05 | HARLAN | "Crane, you've got the camera. We get a face before we get a body. Culvert. Go." | Both trucks tagged; squad rises |
| D06 | OCAMPO | "Flashlights, two o'clock. A pair. Let 'em walk." | Patrol pair enters view (Beat 2) |
| D07 | NINTH SOLDIER (bark) | "Brennek? ... Tsa. Vosh kanni." *[Who's there? ...Nothing. Jackals.]* | Scripted flashlight sweep over the channel |
| D08 | HARLAN | "Sentry on the pump-house roof. He's yours. Quiet." | Player within 25 m of the pump house |
| D09 | HARLAN | "He's made you. Drop him, now!" | Systemic: any enemy suspicion > 60 % (Beats 2–3) |
| D10 | HARLAN | "Two on the roofs. I take left, you take right. On my mark... mark." | Sync-takedown volume; "mark" when the player holds aim for 1 s or after 8 s |
| D11 | NINTH SOLDIER (bark) | "Hesh? ... Ah. Mirrat." *[Huh? ...Ah. Cats.]* | Ocampo's pebble lands near the balcony guard |
| D12 | SEFERI | "The Customs House. Lit window, second floor. That's the meet." | Player enters the overlook volume |
| D13 | WICK (R) | "I need three faces, Three. The Colonel, the advisor, the broker. Take your time." | Camera auto-equips |
| D14 | SEFERI | "Grey uniform, no insignia. One of the advisors. They have come a very long way to buy a lamp." | Frame 2 (Advisor) taken |
| D15 | CRANE (R) | "Wick, third man's behind the glass. Silhouette only. He's on a handset." | Window framed for 2 s |
| D16 | WICK (R) | "Silhouette's fine, son. You've done enough. Hold position." | Immediately after D15; the silhouette's handset gestures sync to this line |
| D17 | NUMBERS STATION (transistor radio, courtyard) | "...Seven. Four. Four. One. Nine..." | 3 s after D16, positional audio from the guard |
| D18 | SEFERI | "Seven-four-four, one-nine. Harlan, that's a grid. That's our roof." | After D17; triggers Flash A |
| D19 | INTERROGATOR (processed) | "You took three photographs. Describe the third." | Flash A |
| D20 | CRANE | "A man at a window. Desk lamp behind him. Talking into a handset." | Flash A |
| D21 | INTERROGATOR (processed) | "Talking to whom, Elias?" | Flash A; static cut back |
| D22 | NINTH SOLDIER (bark, shouted) | "Sarr! Sarr ka-nesh!" *[Light! Light it now!]* | Flare-mortar crew fires; flare launch |
| D23 | HARLAN | "Flare! They're on us. Weapons free, weapons free!" | Flare ignites overhead; combat music in |
| D24 | NINTH SOLDIER (bark) | "Hesh-ta! Vel tuun, vel tuun!" *[There! The roof, the roof!]* | First enemy volley (also systemic) |
| D25 | HARLAN | "Clear that courtyard. We are not leaving without the man in that room." | 6 s into the fight; objective update |
| D26 | NINTH SOLDIER (bark) | "Kesh vorr!" *[Grenade!]* | Systemic: player frag lands within 6 m of an enemy |
| D27 | OCAMPO | "Room's empty. Lamp's still swinging. Radio's warm." | Breach slow-mo ends; office entered |
| D28 | SEFERI | "Harlan. There's a man in the chair. Hooded. Tied." | Player looks at the chair, or 3 s after D27 |
| D29 | WICK (R) | "That's your package. Bring him out alive. Package is priority one." | After D28 |
| D30 | SEFERI | "This is a prisoner. Not a broker." | After D29 |
| D31 | HARLAN | "Then he's somebody's prisoner worth stealing. I've got him. Move!" | Harlan lifts the package; escort begins |
| D32 | WICK (R) | "East through the market. Chandler will meet you outside the East Gate. Go now." | Player exits the Customs House back door |
| D33 | NINTH SOLDIER (bark) | "Varo kel! Varo kel!" *[Flank! Flank them!]* | Souq wave A spawns at cross-lanes |
| D34 | OCAMPO | "Heavy gun on the signal tower! Get under the canvas!" | HMG opens fire through the roof |
| D35 | SEFERI | "The gate's chained from outside. This street goes nowhere. He sent us into a box." | Squad reaches the East Gate; shutters slam behind |
| D36 | HARLAN (R) | "Wick, the east road is a kill box. Give me another way out." | After D35, during kill-box wave B |
| D37 | WICK (R) | "...Stay on the route, Nightjar. Exfil is fixed." | 1.5 s pause after D36; subliminal VU-needle frame |
| D38 | SEFERI | "There is another way. The old cistern runs under the souq to the rail yard. My grandfather hid from tax collectors in it." | Wave B cleared |
| D39 | HARLAN | "Lead on, Captain. Crane, radio to receive only. We're done talking to Wick." | After D38; objective `FOLLOW SEFERI` |
| D40 | OCAMPO | "Boss. You think Wick's selling us?" | Cistern, halfway through the quiet section |
| D41 | HARLAN | "I think Wick's been in the selling business a lot longer than we've been buying." | After D40 |
| D42 | NINTH SOLDIER (bark, echoing) | "Ostu! Brennek ka?" *[Halt! Who's down there?]* | Flashlight squad enters the far end |
| D43 | INTERROGATOR (processed) | "Why did you stop answering Wick?" | Flash B at the ladder |
| D44 | CRANE | "Because every road he gave us had guns at the end of it." | Flash B |
| D45 | INTERROGATOR (processed) | "So you trusted the woman instead. Interesting." | Flash B; scrambler needle spikes and cuts back |
| D46 | WICK (R) | "Nightjar, Wick. You've gone off route. Report your position." | Player climbs out into the rail yard |
| D47 | HARLAN (R) | "Negative, Wick. We're taking the scenic route." | After D46 |
| D48 | ADVISOR (bark, clipped) | "Zvenn! Okru varst... ka!" *[Night eyes! Flank left... now!]* | Advisor wave 2 exits the citadel doors |
| D49 | OCAMPO | "Those aren't Ninth. Goggles! They've got night vision. Shadows won't hide you now!" | First Advisor within 40 m and in view |
| D50 | HARLAN | "Ocampo, charge on the Salt Gate. Crane, Seferi, buy him ninety seconds." | Yard entered; `DEFEND` timer starts |
| D51 | HARLAN (R) | "Gate's open! Chandler, Nightjar. New LZ, Halt Fourteen, east of the walls. Strobe is on." | Salt Gate blast |
| D52 | CHANDLER 2-1 (R) | "Nightjar, Chandler. Got your strobe. Taking fire from the wall. Make it quick." | Player crosses the rail embankment |
| D53 | HARLAN | "Bound! Crane, cover us, then run like hell!" | Bounding overwatch starts |
| D54 | HARLAN | "Package is aboard! Crane, get in the bird!" | Harlan reaches the helicopter; final sprint |
| D55 | OCAMPO | "Who the hell is this guy?" | Outro shot 2, hood off |
| D56 | SEFERI | "Not the Lamplighter." | Outro shot 2 |
| D57 | ASHBY | "Ashby. Inspector General's office. Wick sold me to them three weeks ago. Tonight he was selling you." | Outro shot 3 |
| D58 | ASHBY | "The man in the window, you photographed him? Don't give that film to Wick. Whatever happens." | Outro shot 3, eyes on the camera |
| D59 | CHANDLER 2-1 (intercom) | "Nightjar, be advised. We're being redirected to an alternate LZ." | Outro shot 5 |
| D60 | HARLAN | "On whose authority?" | Outro shot 5 |
| D61 | CHANDLER 2-1 (intercom) | "Wick's." | Outro shot 5; light flicker, cut to black |
| D62 | INTERROGATOR (processed) | "Mr. Ashby didn't survive the flight. His heart. Harlan and Ocampo are next door, very cooperative. And Captain Seferi has been returned to her government." | Debrief shot 2 |
| D63 | INTERROGATOR (processed) | "Which leaves one question. Where is the film, Elias?" | Debrief shot 3, glove taps the missing frame |
| D64 | CRANE | "Who's asking?" | Debrief shot 4 |
| D65 | WICK (unprocessed) | "You know who's asking, son. You've been listening to me all night." | Debrief shot 5, the scrambler toggle flipped off |
| D66 | CRANE | "Then you've already lost. Seferi had it in her boot before we touched down." | Debrief shot 7 |
| D67 | WICK | "...Then we'll start again. From the ridge." | Debrief shot 8, reel rewinds |
| D68 | NUMBERS STATION | "Seven. Four. Four. One. Nine." | Debrief shot 9; bulb stops; cut to black |

### Invented bark lexicon (for VO generation and systemic pools; never subtitled in-game)
**Ninth / Hazruni field speech:** soft consonants, open vowels and a rolled *r*. Spell phonetically for TTS.

| Bark | Meaning | Bark | Meaning |
|---|---|---|---|
| Brennek? | Who's there? | Hesh-ta! | There! / Contact! |
| Tsa. | Nothing. / Never mind. | Vel tuun | On the roof |
| Vosh kanni | Just jackals | Kesh vorr! | Grenade! |
| Hesh? | Huh? / What? | Varo kel! | Flank! |
| Mirrat | Cats | Ostu! | Halt! |
| Sarr! | Light! / Flare! | Tolun aza! | Reloading! |
| ka-nesh | Do it now | Ulvar dosh! | Man down! |
| Darre, darre! | Move, move! | Nesh-ta vel! | Suppress them! |

**Advisors:** hard clusters, clipped, mostly over their own radios with an alien squelch tone. `Zvenn!` = night eyes / goggles down · `Okru varst` = flank left · `ka` = now · `Drolm tesk` = target lost · `Pell, pell` = keep firing · `Vass-ost` = fall back.
