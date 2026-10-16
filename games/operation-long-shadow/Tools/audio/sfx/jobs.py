"""WP06_SFX generation jobs, derived from Docs/ASSET_MANIFEST.json (category sfx/music, source fal) plus overrides.

Candidate counts are higher than the shipped variant counts on purpose: we generate several takes per asset and
keep the best by measurement (single transient, clean onset, no clipping, spectral sanity, loop seam) and by an
audio-understanding listen (fal-ai/audio-understanding) for guardrails.

usage:
  python jobs.py --dry-run [--priority P0] [--ids A,B]     # cost estimate
  python jobs.py --run [--priority P0] [--ids A,B] [--workers 6]
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import gen  # noqa: E402

ROOT = gen.ROOT
MANIFEST = ROOT / "Docs/ASSET_MANIFEST.json"
RAW = "RawAssets/audio/sfx"

NEG = "music, voices, speech, sci-fi, whoosh, riser, cinematic braam, distortion, clipping"
NEG_NOWHOOSH = "music, voices, speech, sci-fi, riser, cinematic braam, distortion, clipping"
NEG_SHOT = NEG + ", burst fire, multiple shots, double shot, automatic fire, explosion"

# ---- prompt refinements (realism / grammar); logged in Docs/DECISIONS.md
PROMPT = {
    "SFX_WPN_COMMANDO_MECH": "close-mic mechanical action of an M16-pattern CAR-15 carbine firing, with no gunshot: aluminium bolt carrier cycling, buffer spring twang and a light metallic clack, one cycle only.",
    "SFX_WPN_HUSHPUPPY_MECH": "close-mic mechanical action of a 9mm semi-automatic pistol firing, with no gunshot: steel slide snapping back and forward and locking, one cycle only.",
    "SFX_WPN_AK_MECH": "close-mic mechanical action of an AK-47 rifle firing, with no gunshot: heavy steel bolt carrier slamming back and forward, recoil spring twang and receiver rattle, one cycle only.",
    "SFX_WPN_RPK_MECH": "close-mic mechanical action of an RPK light machine gun firing, with no gunshot: heavy bolt carrier slamming back and forward, long recoil spring twang and drum magazine rattle, one cycle only.",
    "SFX_WPN_M60D_MECH": "close-mic mechanical action of an M60 belt-fed machine gun firing, with no gunshot: heavy bolt cycling, feed pawl clack and ammunition belt links rattling, one cycle only.",
    "SFX_WPN_COMMANDO_RELOAD": "close-mic foley of reloading an M16-pattern carbine, no gunshot: magazine release button pressed, empty aluminium magazine pulled out, new full magazine inserted and slapped home with a click, charging handle pulled back and released, bolt slams forward. Metal on metal, dry room.",
    "SFX_WPN_HUSHPUPPY_RELOAD": "close-mic foley of reloading a 9mm pistol, no gunshot: magazine release clicked and the steel magazine slides out, new magazine inserted and seated with a click, slide pulled back and released forward. Metal on metal, dry room.",
    "SFX_WPN_AK_RELOAD": "close-mic foley of reloading an AK-47 rifle, no gunshot: magazine release lever pressed, curved steel magazine rocked out, new magazine rocked in and locked with a firm click, charging handle pulled back and released with a heavy slam. Metal on metal, dry room.",
    "SFX_WPN_RPK_RELOAD": "close-mic foley of reloading an RPK light machine gun, no gunshot: heavy drum magazine unlatched and pulled off, new drum magazine rocked in and locked with a firm click, charging handle pulled back and released with a heavy slam. Metal on metal, dry room.",
    "SFX_WPN_M60D_RELOAD": "close-mic foley of reloading an M60 machine gun, no gunshot: feed cover latch opened, ammunition belt laid on the feed tray with link rattle, feed cover slammed shut, charging handle pulled back. Metal on metal.",
    "SFX_WPN_COMMANDO_SHOT": "a single 5.56mm XM177 short-barrelled carbine shot outdoors, close first-person perspective: sharp high-pitched crack, tight snap with a punchy low thump, very short desert echo. Realistic field recording. One shot only.",
    "SFX_WPN_HANDLING": "close-mic rifle handling foley sequence with no gunshot, each sound one second apart: a dry-fire trigger click, a rifle raised with sling swivel rattle, cloth rustle of shouldering to aim, the rifle lowered, a fire selector switch clicked.",
}
PROMPT_B = {  # second-call prompt variants for diversity (weapon shots, cracks)
    "SFX_WPN_AK_SHOT": "Realistic field recording of one AK-47 rifle shot (7.62x39mm), recorded close at the shooter's position outdoors in dry desert: violent sharp crack, heavy low-frequency boom, quick slap echo. One shot only.",
    "SFX_WPN_COMMANDO_SHOT": "Realistic field recording of one M16 carbine shot (5.56mm) at the shooter's position outdoors: very sharp snappy crack, tight punchy report, quick slap echo. One shot only.",
    "SFX_WPN_HUSHPUPPY_SHOT": "One shot from a silenced 9mm pistol, recorded close: soft muffled pop and puff of gas, loud metallic slide clack, no echo. One shot only.",
    "SFX_WPN_RPK_SHOT": "Realistic field recording of one RPK machine gun shot (7.62x39mm) at the gunner's position outdoors: deep heavy crack and chest-thumping boom, short echo. One shot only.",
    "SFX_WPN_AKMS_SHOT": "One AK rifle shot heard from about 30 metres away in a desert town: sharp crack, thinner low end, short echo off walls. Realistic field recording. One shot only.",
    "SFX_WPN_SVD_SHOT": "One sniper rifle shot heard from far across a town, about 130 metres away: sharp distant crack followed by a long rolling echo across rooftops. Realistic field recording. One shot only.",
    "SFX_WPN_DSHK_SHOT": "One shot from a 12.7mm heavy machine gun mounted on a truck in a town square: huge concussive boom, deep chest-thumping low end, long echo off buildings. Realistic field recording. One shot only.",
    "SFX_WPN_M60D_SHOT": "One shot from an M60 machine gun fired from an open helicopter door, close: punchy crack and boom over helicopter rotor noise. One shot only.",
    "SFX_BULLET_CRACK": "a rifle bullet cracking past one metre from the listener's ear, supersonic snap with a short zip, recorded downrange, no gunshot",
    "SFX_BULLET_WHIZ": "a bullet whizzing past overhead with a short descending whistle, recorded downrange, no gunshot",
}
EXTRA_JOBS = [  # material the manifest implies but does not list as its own fal entry
    # stuck clock tick (manifest: synth + Mirelo tick layer) and real clock ticks for the interval signal (D15)
    dict(id="SFX_CLOCK_TICK_STUCK", take="mirelo_tick", ep="mirelo", dur=6.0, n=2,
         prompt="an old mechanical wall clock ticking slowly, one tick per second, close-mic in a quiet room, no chime",
         neg=NEG + ", chime, bell, alarm"),
    # shell casings split by surface (manifest prompt mixes both)
    dict(id="SFX_WPN_SHELLS", take="stone", ep="mirelo", dur=6.0, n=2,
         prompt="series of individual brass rifle shell casings dropping and bouncing on stone paving, spaced half a second apart, close, no gunshots", neg=NEG + ", gunshot"),
    dict(id="SFX_WPN_SHELLS", take="sand", ep="mirelo", dur=6.0, n=2,
         prompt="series of individual brass rifle shell casings dropping onto packed sand and dirt, soft thuds and small tinks, spaced half a second apart, close, no gunshots", neg=NEG + ", gunshot"),
    # static burst for STG_GLITCH and radio material
    dict(id="STG_GLITCH", take="static_burst", ep="el", dur=2.0, loop=False,
         prompt="a short harsh burst of analog radio and television static, crackling white noise"),
    # low hit for STG_TITLE (manifest: SFX_EXP_DEBRIS sub-layer; a dedicated boom gives a cleaner hit)
    dict(id="STG_TITLE", take="low_hit", ep="mirelo", dur=3.0, n=2,
         prompt="a single deep cinematic low boom impact with a long sub-bass decay, dry, no music", neg="music, voices, speech, riser, whoosh, clipping"),
    # reload parts as dedicated clips (slicing the 4 s sequences by onset cannot label mag_out/mag_in/charge reliably)
    *[dict(id=f"SFX_WPN_{w}_RELOAD", take=part, ep="mirelo", dur=1.5, n=4, prompt=pr,
           neg="music, voices, speech, gunshot, explosion, clipping")
      for w, parts in {
          "COMMANDO": {"magout": "close-mic foley of an M16 carbine magazine release button pressed and the empty aluminium magazine pulled out of the rifle, metal on metal, no gunshot",
                       "magin": "close-mic foley of a full aluminium M16 magazine inserted into the rifle and slapped home with a firm click, metal on metal, no gunshot",
                       "charge": "close-mic foley of an M16 charging handle pulled back and released, the bolt slamming forward, metal on metal, no gunshot"},
          "HUSHPUPPY": {"magout": "close-mic foley of a 9mm pistol magazine release clicked and the steel magazine sliding out, no gunshot",
                        "magin": "close-mic foley of a 9mm pistol magazine inserted and seated with a click, no gunshot",
                        "charge": "close-mic foley of a 9mm pistol slide pulled back and released forward with a snap, no gunshot"},
          "AK": {"magout": "close-mic foley of an AK-47 magazine release lever pressed and the curved steel magazine rocked out of the rifle, metal on metal, no gunshot",
                 "magin": "close-mic foley of a curved steel AK-47 magazine rocked into the rifle and locked with a firm metallic click, no gunshot",
                 "charge": "close-mic foley of an AK-47 charging handle pulled back and released, the heavy bolt carrier slamming forward, no gunshot"},
          "RPK": {"magout": "close-mic foley of a heavy drum magazine unlatched and pulled off a light machine gun, metal rattle, no gunshot",
                  "magin": "close-mic foley of a heavy drum magazine rocked into a light machine gun and locked with a firm click, no gunshot",
                  "charge": "close-mic foley of a light machine gun charging handle pulled back and released, the heavy bolt slamming forward, no gunshot"},
          "M60D": {"magout": "close-mic foley of an M60 machine gun feed cover latch opened and the cover flipped up, metal clank, no gunshot",
                   "magin": "close-mic foley of an ammunition belt laid on a machine gun feed tray with link rattle and the feed cover slammed shut, no gunshot",
                   "charge": "close-mic foley of an M60 machine gun charging handle pulled back hard and pushed forward, metal clank, no gunshot"},
      }.items() for part, pr in parts.items()],
    # handling actions as dedicated clips (manifest: slice into dry_fire / raise / ads_in / ads_out / switch)
    *[dict(id="SFX_WPN_HANDLING", take=part, ep="mirelo", dur=1.2, n=4, prompt=pr,
           neg="music, voices, speech, gunshot, explosion, whoosh, clipping")
      for part, pr in {
          "dryfire": "close-mic foley of a rifle trigger pulled on an empty chamber: a single dry metallic hammer click, no gunshot",
          "raise": "close-mic foley of a rifle quickly raised from the hip with sling swivel rattle and webbing movement, no gunshot",
          "adsin": "close-mic foley of a soldier shouldering a rifle to aim: cloth rustle, stock pressed into the shoulder, a soft metallic tick, no gunshot",
          "adsout": "close-mic foley of a soldier lowering a rifle from the shoulder: cloth rustle and a soft sling rattle, no gunshot",
          "switch": "close-mic foley of a rifle fire selector lever flicked with a crisp metallic click, no gunshot",
      }.items()],
    # extras (not in the manifest; GDD intro shot 5f booth reel-to-reel deck playing TALLOW's tape)
    dict(id="EXTRA_SFX_REEL_TO_REEL_LOOP", take="t1", ep="el", dur=12.0, loop=True,
         prompt="a 1970s reel-to-reel tape recorder running: soft motor hum, tape transport hiss and gentle reel rotation, steady, no music, no voices"),
    dict(id="EXTRA_SFX_REEL_TO_REEL_LOOP", take="t2", ep="el", dur=12.0, loop=True,
         prompt="close-up of a reel-to-reel tape deck spooling steadily, quiet capstan motor whir and faint tape hiss, no music, no voices"),
    dict(id="EXTRA_SFX_TAPE_TRANSPORT", take="t1", ep="mirelo", dur=4.0, n=4,
         prompt="a 1970s reel-to-reel tape recorder: the heavy play key pressed with a mechanical clunk and the capstan motor engaging, then the stop key pressed with a clunk, close-mic, no music, no voices",
         neg="music, voices, speech, singing, clipping"),
]


# ---- resume pass (2026-10-16 09:40): more weapon transient candidates (the realism lever; $0.10 per 4-sample call)
SHOT_MORE = {
    "SFX_WPN_COMMANDO_SHOT": ("an M16-pattern 5.56mm carbine", "very sharp high-pitched supersonic crack and a tight punchy muzzle blast"),
    "SFX_WPN_AK_SHOT": ("an AK-47 rifle (7.62x39mm)", "violent sharp crack and a heavy chest-thumping muzzle blast"),
    "SFX_WPN_RPK_SHOT": ("an RPK light machine gun (7.62x39mm, long heavy barrel)", "hard crack and a deep heavy boom"),
    "SFX_WPN_M60D_SHOT": ("an M60 machine gun (7.62x51mm)", "hard crack and a deep punchy boom"),
    "SFX_WPN_DSHK_SHOT": ("a DShK 12.7mm heavy machine gun", "thunderous concussive boom with a sharp crack"),
}
SHOT_MORE_TEMPLATES = [
    "Close-mic recording of a single {w} shot, dry and tight: {c}, very short natural decay, no echo, no reverb. One shot only.",
    "Field recording of one real {w} shot at an outdoor firing range, microphone at the shooter's shoulder: {c}, a brief slap off the ground. One shot only.",
    "One {w} shot fired outdoors, recorded right next to the shooter: {c}, the steel receiver clacks, quick decay. One shot only.",
]
EXTRA_JOBS_2 = []
for _id, (_w, _c) in SHOT_MORE.items():
    for _k, _t in enumerate(SHOT_MORE_TEMPLATES):
        EXTRA_JOBS_2.append(dict(id=_id, take=f"t{_k + 3}", ep="mirelo", dur=2.0, n=4, prompt=_t.format(w=_w, c=_c),
                                 neg=NEG_SHOT + ", echo, reverb hall"))
EXTRA_JOBS_2 += [
    dict(id="SFX_WPN_HUSHPUPPY_SHOT", take="t3", ep="mirelo", dur=2.0, n=4,
         prompt="Close recording of one suppressed 9mm pistol shot: a dull compressed thump of gas through the silencer, the steel slide snapping back and forward loudly, no crack, no echo. One shot only.",
         neg=NEG_SHOT + ", echo, loud bang"),
    dict(id="SFX_WPN_HUSHPUPPY_SHOT", take="t4", ep="mirelo", dur=2.0, n=4,
         prompt="A silenced pistol fired once in a quiet room, recorded close: soft muffled pop, metallic slide clack and a brass casing tinkling on the floor, no echo. One shot only.",
         neg=NEG_SHOT + ", echo, loud bang"),
    dict(id="SFX_WPN_AKMS_SHOT", take="t3", ep="mirelo", dur=2.5, n=4,
         prompt="One AK rifle shot heard from about 30 metres away across a dusty street in a desert town: sharp crack, lighter boom, a short slap off plaster walls. Realistic field recording. One shot only.",
         neg=NEG_SHOT),
    dict(id="SFX_WPN_TAIL_SQUARE", take="t3", ep="mirelo", dur=3.5, n=4,
         prompt="only the echo of a single rifle shot reverberating around an open town square and off the surrounding rooftops, the initial shot itself very faint, long rolling reflections fading out",
         neg=NEG_SHOT + ", second shot"),
    dict(id="SFX_WPN_TAIL_STREET", take="t3", ep="mirelo", dur=3.0, n=4,
         prompt="only the echo of a single rifle shot slapping back and forth between the plaster walls of a narrow street, the initial shot itself very faint, quick flutter echo then a short decay",
         neg=NEG_SHOT + ", second shot"),
]


# ---- resume pass: retakes for ElevenLabs takes that came back clipped (clipping cannot be repaired; EL is $0.002/s)
EXTRA_JOBS_3 = [
    dict(id="SFX_FIRE_CRACKLE_LOOP", take="t3", ep="el", dur=15.0, loop=True,
         prompt="a burning car crackling steadily, flames roaring softly with small pops and hisses, recorded from a few metres away"),
    dict(id="SFX_FIRE_CRACKLE_LOOP", take="t4", ep="el", dur=15.0, loop=True,
         prompt="steady crackle and low roar of a vehicle fire, occasional small pops of burning rubber and metal ticking, medium distance"),
    dict(id="SFX_HELI_ALARM", take="t3", ep="el", dur=3.0, loop=True,
         prompt="a helicopter cockpit warning tone, a moderate two-tone electronic warble heard inside the cabin"),
    dict(id="SFX_HELI_ALARM", take="t4", ep="el", dur=3.0, loop=True,
         prompt="1970s aircraft master caution alarm, repeating electronic beeping tone, small cockpit speaker"),
    dict(id="SFX_CURFEW_SIREN", take="t3", ep="el", dur=20.0,
         prompt="a hand-cranked civil-defence siren far away over a desert town, slowly winding down and fading out, distant and echoing"),
    dict(id="SFX_CURFEW_SIREN", take="t4", ep="el", dur=20.0,
         prompt="distant air-raid siren winding down slowly across empty rooftops at sunset, faint echo, fading to silence"),
    dict(id="SFX_DRILL_LOOP", take="t3", ep="el", dur=10.0, loop=True,
         prompt="a heavy magnetic core drill cutting into a steel safe door, steady grinding whine and motor hum, close"),
    dict(id="AMB_DISTANT_GUNFIRE", take="t4", ep="el", dur=20.0, loop=True,
         prompt="faint distant sporadic rifle fire far across a desert town at dusk, single shots and short bursts about a kilometre away, soft rolling echoes, no voices, no singing, no music, no bells"),
    dict(id="AMB_DISTANT_GUNFIRE", take="t5", ep="el", dur=20.0, loop=True,
         prompt="a firefight very far away beyond the rooftops, muffled occasional gunshots and bursts echoing, quiet wind, no voices, no singing, no music, no bells"),
    dict(id="AMB_CICADAS", take="t4", ep="el", dur=20.0, loop=True,
         prompt="cicadas buzzing in olive trees at a moderate distance on a hot evening, steady, no voices, no singing, no music, no bells"),
]


# ---- resume pass: room tones / beds that came back too quiet (EL wrote -52 dBFS peaks = ~7-bit audio) or flagged
EXTRA_JOBS_4 = [
    dict(id="AMB_ROOMTONE_BOOTH", take="t4", ep="el", dur=20.0, loop=True,
         prompt="close-mic room tone of a small underground concrete interrogation room: steady low electrical hum, a ventilation duct breathing air, faint fluorescent buzz, no voices, no singing, no music, no bells"),
    dict(id="AMB_ROOMTONE_BOOTH", take="t5", ep="el", dur=20.0, loop=True,
         prompt="steady ventilation fan rumble and mains hum in a windowless concrete bunker room, recorded close, no voices, no singing, no music, no bells"),
    dict(id="AMB_BASEMENT_TUBES", take="t4", ep="el", dur=20.0, loop=True,
         prompt="close-mic recording in a basement equipment room: steady hum of old vacuum-tube radio transmitters, occasional relay clicks, a cooling fan, no voices, no singing, no music, no bells"),
    dict(id="AMB_BASEMENT_TUBES", take="t5", ep="el", dur=20.0, loop=True,
         prompt="electrical transformer hum and cooling fans of radio equipment racks in a concrete basement, a relay clicking now and then, no voices, no singing, no music, no bells"),
    dict(id="AMB_SALTPAN_WIND", take="t4", ep="el", dur=20.0, loop=True,
         prompt="steady night wind blowing across a wide flat salt pan, hollow airy whoosh with slow gusts, recorded outdoors, no voices, no singing, no music, no bells, no birds"),
]


# ---- resume pass 2 (2026-10-16, after the visual QA of every ready clip): dedicated single-event takes where the
# sliced 8-12 s sequences gave noise beds or multi-event slices (impacts, footsteps, ladder), plus whizzes, the flare,
# the fluorescent flicker and a clean knife swing. Mirelo 2.0 at $0.010/s/sample.
NEG_ONE = "music, voices, speech, gunshot, explosion, riser, whoosh, clipping, multiple impacts"
IMPACT_PROMPTS = {
    "SFX_IMPACT_SAND": ["a single rifle bullet striking loose desert sand at close range: one sharp dull thud and a short spray of sand grains pattering down, recorded close, no gunshot",
                        "one bullet impact into a sand dune two metres away: a punchy thump and sand hissing back down, field recording, no gunshot",
                        "a single bullet hitting dry packed dirt and sand: a hard thud and a puff of grit scattering, close, no gunshot"],
    "SFX_IMPACT_STONE": ["a single rifle bullet striking a stone wall at close range: one hard sharp crack and stone chips and grit scattering, recorded close, no gunshot",
                         "one bullet impact on a limestone block two metres away: a sharp stony snap and small fragments falling, field recording, no gunshot",
                         "a single bullet hitting stone paving: a hard crack and gravel debris skittering, close, no gunshot, no ricochet"],
    "SFX_IMPACT_PLASTER": ["a single bullet hitting a plastered mud-brick wall at close range: one sharp dusty crack and crumbling plaster bits falling, recorded close, no gunshot",
                           "one bullet impact into an old plaster wall: a dry thwack, plaster dust and small chunks pattering down, field recording, no gunshot",
                           "a single bullet punching into a whitewashed adobe wall: a dull crack and crumbling debris, close, no gunshot"],
    "SFX_IMPACT_WOOD": ["a single rifle bullet hitting a thick wooden door at close range: one sharp splintering knock and wood splinters falling, recorded close, no gunshot",
                        "one bullet impact into a wooden market crate: a hard woody crack with splintering, field recording, no gunshot",
                        "a single bullet striking a heavy wooden beam: a sharp thock and small splinters, close, no gunshot"],
    "SFX_IMPACT_METAL": ["a single rifle bullet hitting a steel sheet at close range: one sharp metallic clang with a short ring, recorded close, no gunshot, no ricochet whine",
                         "one bullet impact on a car door: a hard metallic punch and a short tinny ring, field recording, no gunshot",
                         "a single bullet striking a steel oil drum: a sharp metallic bang and a brief hollow ring, close, no gunshot"],
    "SFX_IMPACT_FABRIC": ["a single bullet punching into a canvas sandbag at close range: one dull heavy thump and sand trickling, recorded close, no gunshot",
                          "one bullet impact into a stack of grain sacks: a muffled thud and a short trickle, field recording, no gunshot",
                          "a single bullet hitting a thick canvas tarp over crates: a dull pop and fabric flutter, close, no gunshot"],
    "SFX_IMPACT_FLESH": ["a single bullet hitting a soldier through thick clothing and webbing: one short heavy dull thump, recorded close, no gunshot, no voice",
                         "one bullet impact on a padded body target through a canvas jacket: a deep short wet thud, field recording, no gunshot, no voice",
                         "a single heavy dull thud of a bullet hitting a person through clothing, very short, close, no gunshot, no voice, no scream"],
    "SFX_IMPACT_GLASS": ["a single rifle bullet shattering a window pane at close range: one sharp glass break and shards tinkling down onto a stone floor, recorded close, no gunshot",
                         "one bullet impact through a shop window: a crisp glass shatter and falling fragments, field recording, no gunshot",
                         "a single bullet breaking a glass bottle and window: a sharp smash and small shards tinkling, close, no gunshot"],
    "SFX_IMPACT_FRUIT": ["a single bullet hitting a crate of oranges at close range: one wet splat, a wooden crate knock and fruit rolling, recorded close, no gunshot",
                         "one bullet impact into a pile of melons at a market stall: a wet thwack and juice spatter, field recording, no gunshot",
                         "a single bullet tearing through a crate of fruit: a pulpy splat and splintering slats, close, no gunshot"],
}
FS_PROMPTS = {
    "SFX_FS_SAND": "soft loose desert sand",
    "SFX_FS_GRAVEL": "loose gravel and small stones",
    "SFX_FS_STONE": "worn stone paving in a town street",
    "SFX_FS_ROOF": "a flat plaster-and-mud rooftop with grit on it",
    "SFX_FS_TILE": "a terrazzo tile floor indoors",
    "SFX_FS_WOOD": "old wooden floorboards indoors",
    "SFX_FS_METALSTAIR": "a steel grating stair that rings slightly",
    "SFX_FS_SALTCRUST": "a dry crunchy salt crust on a salt pan",
}
FS_TEMPLATES = [
    "a single footstep of a soldier's leather combat boot on {s}, heel then toe, close-mic, quiet surroundings, one step only",
    "one walking step of a heavy boot on {s}, recorded close at ground level, natural and realistic, one step only",
    "one firm running footstep of a soldier with gear on {s}, close-mic, one step only",
    "a single careful slow footstep of a combat boot on {s}, close-mic, one step only",
]
EXTRA_JOBS_5 = []
for _id, _ps in IMPACT_PROMPTS.items():
    for _k, _p in enumerate(_ps):
        EXTRA_JOBS_5.append(dict(id=_id, take=f"one{_k + 1}", ep="mirelo", dur=1.5, n=4, prompt=_p, neg=NEG_ONE))
for _id, _s in FS_PROMPTS.items():
    for _k, _t in enumerate(FS_TEMPLATES):
        EXTRA_JOBS_5.append(dict(id=_id, take=f"one{_k + 1}", ep="mirelo", dur=1.2, n=4, prompt=_t.format(s=_s),
                                 neg="music, voices, speech, gunshot, explosion, whoosh, clipping, several footsteps, walking sequence"))
EXTRA_JOBS_5 += [
    dict(id="SFX_BULLET_WHIZ", take="t3", ep="mirelo", dur=1.2, n=4,
         prompt="a single rifle bullet flying past one metre from the listener's head: a short sharp zip with a quick descending whine, recorded downrange, no gunshot, no impact",
         neg="music, voices, speech, gunshot, explosion, impact, ricochet, clipping"),
    dict(id="SFX_BULLET_WHIZ", take="t4", ep="mirelo", dur=1.2, n=4,
         prompt="one subsonic bullet whizzing close past the ear with a fast buzzing flutter that fades away, recorded downrange, no gunshot",
         neg="music, voices, speech, gunshot, explosion, impact, ricochet, clipping"),
    dict(id="SFX_MOVE_LADDER", take="one1", ep="mirelo", dur=1.0, n=4,
         prompt="a single boot step onto a wooden ladder rung with a hand gripping the rail: a woody thud and a small creak, close-mic, one step only",
         neg=NEG_ONE),
    dict(id="SFX_MOVE_LADDER", take="one2", ep="mirelo", dur=1.0, n=4,
         prompt="one heavy step on the rung of an old wooden ladder, hollow knock and creak of wood, close, one step only",
         neg=NEG_ONE),
    dict(id="SFX_KNIFE_SWING", take="one1", ep="mirelo", dur=1.0, n=4,
         prompt="a single fast slash of a combat knife through the air: one short sharp whoosh, close, one swing only",
         neg="music, voices, speech, gunshot, explosion, impact, clipping, two swings"),
    dict(id="SFX_FLARE", take="t3", ep="mirelo", dur=6.0, n=4,
         prompt="a military parachute illumination flare fired: a hollow pop high overhead, then a steady loud fizzing hiss as it burns, outdoors at night",
         neg="music, voices, speech, gunshot, explosion, fireworks crackle, clipping"),
    dict(id="SFX_FLUORESCENT_FLICKER", take="t3", ep="mirelo", dur=3.0, n=4,
         prompt="an old fluorescent tube light flickering on in a concrete room: starter clicks, stuttering buzzing flashes, then a steady electrical hum",
         neg="music, voices, speech, gunshot, explosion, clipping"),
]

EXTRA_JOBS_5 += [  # second single-impact batch for the surfaces where the first one drifted off brief (machine listen)
    dict(id="SFX_IMPACT_FLESH", take="one4", ep="mirelo", dur=1.2, n=4,
         prompt="video game bullet impact on a body: one short punchy meaty thwack of a bullet hitting a soldier through a thick jacket, dull and heavy, no voice",
         neg=NEG_ONE + ", paper, wood, voice, scream"),
    dict(id="SFX_IMPACT_FLESH", take="one5", ep="mirelo", dur=1.2, n=4,
         prompt="one bullet hitting a side of meat hanging in a butcher's shop: a short dull wet thump, close, no gunshot",
         neg=NEG_ONE + ", paper, wood, voice, scream"),
    dict(id="SFX_IMPACT_FABRIC", take="one4", ep="mirelo", dur=1.2, n=4,
         prompt="one bullet hitting a full sandbag: a deep dull thud and a short pour of sand from the hole, close, no gunshot",
         neg=NEG_ONE + ", cardboard, paper"),
    dict(id="SFX_IMPACT_FABRIC", take="one5", ep="mirelo", dur=1.2, n=4,
         prompt="video game bullet impact into cloth and padding: one soft punchy thump with a fabric flutter, close, no gunshot",
         neg=NEG_ONE + ", cardboard, paper"),
    dict(id="SFX_IMPACT_FRUIT", take="one4", ep="mirelo", dur=1.2, n=4,
         prompt="one bullet hitting a watermelon at close range: a loud wet splat and pulp spattering, no gunshot",
         neg=NEG_ONE + ", glass, paper"),
    dict(id="SFX_IMPACT_FRUIT", take="one5", ep="mirelo", dur=1.2, n=4,
         prompt="a single bullet smashing into a crate of ripe oranges: a juicy squelch and a wooden knock, close, no gunshot",
         neg=NEG_ONE + ", glass, paper"),
]

EXTRA_JOBS_5 += [  # sand/gravel/salt singles drifted into sustained crunch beds: tighter prompts with silence around
    dict(id=_id, take=f"one{_k + 5}", ep="mirelo", dur=1.2, n=4, prompt=_p,
         neg="music, voices, speech, gunshot, explosion, whoosh, clipping, several footsteps, walking sequence, shuffling")
    for _id, _surf in (("SFX_FS_SAND", "soft dry desert sand"), ("SFX_FS_GRAVEL", "loose gravel"),
                       ("SFX_FS_SALTCRUST", "a brittle salt crust"))
    for _k, _p in enumerate([
        f"a single footstep of a combat boot landing in {_surf}: one short crunch with a soft thud, heel then toe, close-mic, silence before and after, one step only",
        f"one footstep on {_surf}, clearly separated, short and natural, recorded close at ground level in a quiet desert, silence before and after"])
]

# ---- candidate counts: (calls, samples per call) for Mirelo; calls for EL
SHOT_IDS = {"SFX_WPN_COMMANDO_SHOT", "SFX_WPN_HUSHPUPPY_SHOT", "SFX_WPN_AK_SHOT", "SFX_WPN_RPK_SHOT",
            "SFX_WPN_AKMS_SHOT", "SFX_WPN_SVD_SHOT", "SFX_WPN_DSHK_SHOT", "SFX_WPN_M60D_SHOT"}



# ---- resume pass 3 (2026-10-16 afternoon): retakes for set pieces whose shipped takes the machine listen scored as
# off-brief (MATCH <= 5: "zipper" for the awning tear, "fire crackle" for blast debris, "lighter" for leather creak,
# "wrapper" for the satchel plant ...), plus single-event takes for the sequence cues. Each id gets two refined Mirelo
# prompts (4 samples) and four cheap ElevenLabs takes; the recipes pick by measurement + listen as before.
NEG_SP = "music, voices, speech, singing, sci-fi, whoosh, riser, cinematic braam, distortion, clipping"
RETAKE_SP = {  # id: (duration s, [mirelo prompt A, mirelo prompt B], el prompt, extra negative)
    "SFX_AWNING_TEAR": (2.5, [
        "a thick canvas market awning shredded by bullets: several violent fabric rips in quick succession, heavy canvas tearing apart, then the torn canvas flapping loose, outdoors, no gunshots",
        "close outdoor foley of heavy old canvas being torn apart in long ragged rips, stiff fabric fibres snapping, the loose sheet flapping, no gunshot"],
        "Heavy canvas tarpaulin violently ripped apart in several long ragged tears, then the loose canvas flapping, outdoors, no gunshots", ", gunshot, zipper"),
    "SFX_EXP_DEBRIS": (3.0, [
        "after a grenade explosion: clumps of dirt, gravel and small stones raining down onto hard ground and a tin roof, a burst of pattering impacts that thins out to a few last pebbles, outdoors, no blast",
        "debris settling after an explosion: sand, grit and pebbles falling back to earth, dense pattering then scattered small stone impacts, outdoors, no explosion"],
        "Debris falling after an explosion: dirt clods, gravel and small rocks raining down onto hard ground, dense pattering that thins out, no explosion, outdoors", ", explosion, fire crackle, rain"),
    "SFX_FRUIT_SPILL": (3.0, [
        "a wooden crate of oranges tipping over: the crate thuds on stone paving and dozens of oranges spill out, soft heavy thumps and oranges rolling across the stones",
        "many round oranges dropping out of a toppled wooden fruit crate onto a stone street, soft fruit thuds and rolling, wooden crate knock"],
        "A wooden crate of oranges tipped over onto stone paving: crate thud, dozens of oranges spilling out with soft thumps and rolling across the stones", ", glass, metal"),
    "SFX_LEATHER_CREAK": (3.0, [
        "close-up foley of thick leather straps creaking and stretching under tension as a man strains against wrist restraints, slow leather squeaks and groans, small buckle jingle, quiet room",
        "slow tight leather creaking: a leather belt pulled taut and twisted, stretching squeaks and creaks, very close microphone, quiet room"],
        "Close-up thick leather straps creaking and stretching under strain, slow leather squeaks, a small metal buckle jingle, quiet room", ", lighter, keys, plastic"),
    "SFX_SATCHEL_PLANT": (3.0, [
        "close foley: a heavy canvas satchel charge pressed and scraped against a concrete wall, canvas rustle, webbing straps pulled tight through a buckle, then a single metallic click of a fuse igniter",
        "a soldier placing a demolition charge: heavy canvas bag thudding against concrete, nylon straps cinched, metal clip snapping shut, a small mechanical igniter click, close"],
        "A heavy canvas demolition satchel pressed against a concrete wall, canvas scraping, webbing straps pulled tight, then a sharp metallic igniter click, close-up foley", ", plastic wrapper, paper"),
    "SFX_BOLTCUTTER_CHAIN": (3.0, [
        "large bolt cutters squeezing a thick steel chain: metal creak under pressure, a loud sharp metallic snap as the link breaks, then the heavy chain rattling and clattering down onto concrete",
        "heavy steel chain cut with long bolt cutters, a hard metallic crunch and snap, the heavy chain slithering through a gate and clanking onto the ground"],
        "Large bolt cutters snapping a thick heavy steel chain with a loud metallic crunch, then the heavy chain rattling and clattering onto the ground", ", padlock, keys"),
    "SFX_FLOOD_SLAM_ON": (2.5, [
        "a huge industrial electrical contactor slamming shut with a heavy metallic clunk, then the loud deep electrical buzz of high-power stadium floodlights coming on and humming",
        "big stadium floodlight bank switched on: heavy relay bang, an electrical thump and a rising loud 50 Hz buzzing hum of the lamps"],
        "A huge electrical contactor slamming shut with a heavy metallic clunk, followed by the loud deep electrical buzz of stadium floodlights switching on", ""),
    "SFX_PROJECTOR_START": (3.0, [
        "an old 16 mm film projector switched on: lever clack, the electric motor whirring up to speed and the film gate starting to clatter rhythmically, small room",
        "vintage cine projector starting: switch click, motor spin-up whine, then the fast rhythmic mechanical clatter of film running through the gate"],
        "An old 16mm film projector being switched on: lever click, the motor whirring up to speed and the film gate clattering rhythmically", ""),
    "SFX_TUBE_RACK_SHATTER": (3.0, [
        "a rack of glowing glass vacuum tubes hit by bullets: several tubes bursting with sharp glass pops, shattering glass tinkling down, loud crackling electrical arcing and sparks, metal cabinet rattle",
        "electronic equipment cabinet shot to pieces: glass vacuum tubes exploding, crackling high-voltage arcs and sputtering sparks, falling glass shards"],
        "A rack of glowing glass vacuum tubes shot: several glass tubes bursting with sharp pops, shattering glass, loud crackling electrical arcing and sparks", ", gunshot"),
    "SFX_KETTLE": (4.0, [
        "a metal stovetop kettle beginning to whistle in a kitchen behind a wall: a faint breathy hiss that slowly rises into a steady high whistle, muffled through the wall",
        "a kettle on a gas stove starting to boil and whistle, the whistle rising slowly in pitch and level, heard from the next room"],
        "A metal stovetop kettle starting to whistle, rising slowly from a breathy hiss to a steady high whistle, heard from the next room through a wall", ", door hinge, squeak"),
}
RETAKE_SINGLE = {  # sequence cues -> single-event takes (sliced() cuts each take's main event)
    "SFX_GREN_BOUNCE": (1.5, [
        "a single hand grenade thrown onto stone pavement: one heavy metallic clunk of a cast-iron grenade on stone, then a short rattling roll, outdoors",
        "a single hand grenade landing on packed sand and gravel: a dull heavy thud, a small bounce and a short gritty roll, outdoors",
        "a single hand grenade bouncing once on a corrugated sheet-metal roof: a hollow metallic bang and a short roll, outdoors"]),
    "SFX_BODYFALL": (2.0, [
        "a single soldier's body in combat gear collapsing onto sandy ground: a heavy dull thud of the body, a rifle and webbing gear clattering, one fall only",
        "a single body falling onto a stone floor: heavy limp thud, equipment and a rifle clattering on the stones, one fall only, close"]),
    "SFX_MOVE_LAND": (1.5, [
        "a soldier landing on his feet after jumping down two metres onto packed dirt: a heavy double boot thud, knees bending, webbing and gear rattling, one landing only, close",
        "a soldier dropping from a low wall onto stone paving: heavy boots slam down, gear and ammunition pouches rattle, one landing only, close"]),
}
RETAKE_EL_LOOP = {  # EL loop beds: (duration, prompt, n)
    "SFX_RADIO_STATIC_LOOP": (12.0, "Steady analog military field radio static, warm band-limited hiss with soft random crackles and pops, no voices, no tones, no music", 4),
    "AMB_CINEMA_INTERIOR": (20.0, "Inside a large abandoned cinema auditorium at dusk: quiet room tone, occasional old wooden seat creaks, dust settling, a faint buzzing exit sign, distant wind outside, no voices, no music, no singing, no bells", 3),
}
RETAKE_UI = {  # 0.6 s UI blips, EL
    "SFX_UI_MENU_MOVE": "A single short soft click of a vintage computer terminal key, dry, very short, no hum",
    "SFX_UI_MENU_SELECT": "A short clean confirm blip of a 1980s computer terminal, one soft two-tone beep, dry, no hum",
    "SFX_UI_OBJECTIVE": "A single short square-wave beep of a 1980s computer terminal, soft, dry, one blip only",
}
EXTRA_JOBS_6 = []
for _id, (_d, _ms, _el, _neg) in RETAKE_SP.items():
    for _k, _p in enumerate(_ms, 1):
        EXTRA_JOBS_6.append(dict(id=_id, take=f"r{_k}", ep="mirelo", dur=_d, n=4, prompt=_p, neg=NEG_SP + _neg))
    for _k, _pi in enumerate((0.45, 0.6, 0.45, 0.7), 1):
        EXTRA_JOBS_6.append(dict(id=_id, take=f"e{_k}", ep="el", dur=_d, prompt=_el, pi=_pi))
for _id, (_d, _ps) in RETAKE_SINGLE.items():
    for _k, _p in enumerate(_ps, 1):
        EXTRA_JOBS_6.append(dict(id=_id, take=f"r{_k}", ep="mirelo", dur=_d, n=4, prompt=_p,
                                 neg=NEG_SP + ", several impacts, footsteps, explosion, gunshot"))
for _id, (_d, _p, _n) in RETAKE_EL_LOOP.items():
    for _k in range(1, _n + 1):
        EXTRA_JOBS_6.append(dict(id=_id, take=f"e{_k}", ep="el", dur=_d, prompt=_p, pi=(0.45, 0.6)[_k % 2], loop=True))
for _id, _p in RETAKE_UI.items():
    for _k, _pi in enumerate((0.45, 0.6, 0.5, 0.7), 1):
        EXTRA_JOBS_6.append(dict(id=_id, take=f"e{_k}", ep="el", dur=0.6, prompt=_p, pi=_pi))

# resume pass 3b: brass casings (the pass-2 stone slices carry a constant 11-12 kHz model whine, sand slices have no clear
# tick), the M60 overheat WP03 asked for, and two cinematic accents CINEMATIC_DIRECTION §8.2a needs (I-7 sand hiss on
# the lens in the downwash, I-8 low thump + whoosh on the speed ramp).
NEG_ONE_SHELL = "music, voices, speech, gunshot, explosion, whoosh, clipping, constant tone, whine, ringing tone"
EXTRA_JOBS_7 = [
    dict(id="SFX_WPN_SHELLS", take="stone_r1", ep="mirelo", dur=1.0, n=4, neg=NEG_ONE_SHELL,
         prompt="a single empty brass rifle cartridge case dropping onto stone paving: a bright metallic tink, two small bounces and a short rattle, close-up, outdoors"),
    dict(id="SFX_WPN_SHELLS", take="stone_r2", ep="mirelo", dur=1.0, n=4, neg=NEG_ONE_SHELL,
         prompt="one spent steel-cased 7.62 mm casing hitting a concrete floor: a sharp metallic clink and a quick bounce, close-up"),
    dict(id="SFX_WPN_SHELLS", take="sand_r1", ep="mirelo", dur=1.0, n=4, neg=NEG_ONE_SHELL,
         prompt="a single empty brass rifle cartridge case dropping onto packed sand and grit: a soft dull tick and a tiny gritty settle, close-up, outdoors"),
    dict(id="EXTRA_SFX_WPN_M60D_OVERHEAT", take="r1", ep="mirelo", dur=4.0, n=4, neg=NEG_SP + ", gunshot",
         prompt="a hot machine gun barrel after a long burst: faint steam hiss, metallic ticking and pinging as the steel cools, close-up, no gunshots"),
    dict(id="EXTRA_SFX_DOWNWASH_SAND", take="r1", ep="mirelo", dur=6.0, n=4, neg=NEG_SP + ", engine, rotor blades, voices",
         prompt="helicopter rotor downwash blasting sand and grit: a loud gusting hiss of sand grains peppering a camera lens and clothing, rushing wind, outdoors in the desert, no engine"),
]
for _k, _pi in enumerate((0.45, 0.6, 0.5, 0.7), 1):
    EXTRA_JOBS_7.append(dict(id="SFX_WPN_SHELLS", take=f"stone_e{_k}", ep="el", dur=1.0, pi=_pi,
                             prompt="A single empty brass bullet casing dropping onto a stone floor, bright metallic tink and two small bounces, close-up"))
    EXTRA_JOBS_7.append(dict(id="SFX_WPN_SHELLS", take=f"sand_e{_k}", ep="el", dur=1.0, pi=_pi,
                             prompt="A single empty brass bullet casing dropping onto packed sand, soft dull tick, close-up"))
    EXTRA_JOBS_7.append(dict(id="EXTRA_SFX_WPN_M60D_OVERHEAT", take=f"e{_k}", ep="el", dur=4.0, pi=_pi,
                             prompt="A hot machine gun barrel cooling after a long burst: faint hiss and metallic ticking and pinging, close-up, no gunshots"))
    EXTRA_JOBS_7.append(dict(id="EXTRA_SFX_DOWNWASH_SAND", take=f"e{_k}", ep="el", dur=6.0, pi=_pi,
                             prompt="Helicopter rotor downwash blasting sand: loud gusting hiss of sand grains hitting a camera lens and clothing, rushing wind, desert, no engine sound"))
    EXTRA_JOBS_7.append(dict(id="EXTRA_SFX_CINE_RAMP", take=f"e{_k}", ep="el", dur=2.0, pi=_pi,
                             prompt="A deep low cinematic body thump followed by a short airy whoosh, subtle, realistic, no music, no braam"))

# resume pass 3c: compound set pieces the models cannot do in one take are built from separately generated parts
# (sound-design layering): bolt-cutter snap + heavy chain fall; tube burst + arc crackle; more blast-debris showers;
# the M60 overheat retried without "machine gun" in the EL text (EL rendered a burst every time).
NEG_PART = "music, voices, speech, singing, sci-fi, whoosh, riser, braam, clipping"
EXTRA_JOBS_8 = [
    dict(id="SFX_BOLTCUTTER_CHAIN", take="snap_r1", ep="mirelo", dur=1.5, n=4, neg=NEG_PART + ", chain rattle, keys",
         prompt="one loud sharp metallic snap of a thick hardened steel chain link breaking under long bolt cutters, close, one snap only"),
    dict(id="SFX_BOLTCUTTER_CHAIN", take="chain_r1", ep="mirelo", dur=2.5, n=4, neg=NEG_PART + ", keys, small chain",
         prompt="a heavy thick steel chain sliding off an iron gate and clattering loudly in a heap onto concrete, heavy metal links, close"),
    dict(id="SFX_TUBE_RACK_SHATTER", take="glass_r1", ep="mirelo", dur=2.5, n=4, neg=NEG_PART + ", gunshot, fire",
         prompt="several glass vacuum tubes bursting one after another with sharp pops, thin glass shattering and shards falling onto a metal shelf, close"),
    dict(id="SFX_EXP_DEBRIS", take="r3", ep="mirelo", dur=3.0, n=4, neg=NEG_SP + ", explosion, fire crackle, frying",
         prompt="a dense burst of small rocks, dirt clods and gravel raining down onto hard ground and a sheet-metal roof right after a blast, the impacts quickly thinning out, outdoors"),
    dict(id="SFX_EXP_DEBRIS", take="r4", ep="mirelo", dur=3.0, n=4, neg=NEG_SP + ", explosion, fire crackle, frying",
         prompt="stones and clumps of earth thrown up by a grenade falling back down around the listener: heavy thuds and pattering on dirt and rubble, thinning out, outdoors"),
    dict(id="EXTRA_SFX_WPN_M60D_OVERHEAT", take="r2", ep="mirelo", dur=4.0, n=4, neg=NEG_SP + ", gunshot, machine gun, firing, fire crackle, clock",
         prompt="a very hot steel gun barrel cooling down in a quiet room: slow irregular metallic ticks and pings as the metal contracts, a faint sizzle, close"),
]
for _k, _pi in enumerate((0.45, 0.6, 0.5, 0.7), 1):
    EXTRA_JOBS_8.append(dict(id="SFX_BOLTCUTTER_CHAIN", take=f"snap_e{_k}", ep="el", dur=1.5, pi=_pi,
                             prompt="A single loud sharp metallic snap of a thick steel chain link cut by heavy bolt cutters, close-up"))
    EXTRA_JOBS_8.append(dict(id="SFX_BOLTCUTTER_CHAIN", take=f"chain_e{_k}", ep="el", dur=2.5, pi=_pi,
                             prompt="A heavy thick steel chain falling off a gate and clattering loudly onto concrete, heavy links"))
    EXTRA_JOBS_8.append(dict(id="SFX_TUBE_RACK_SHATTER", take=f"arc_e{_k}", ep="el", dur=2.0, pi=_pi,
                             prompt="High-voltage electrical arc: loud crackling and sputtering sparks with a buzzing zap, close-up, no explosion"))
    EXTRA_JOBS_8.append(dict(id="SFX_TUBE_RACK_SHATTER", take=f"glass_e{_k}", ep="el", dur=2.5, pi=_pi,
                             prompt="Several glass vacuum tubes bursting with sharp pops, thin glass shattering and shards falling on a metal shelf"))
    EXTRA_JOBS_8.append(dict(id="EXTRA_SFX_WPN_M60D_OVERHEAT", take=f"e{_k + 4}", ep="el", dur=4.0, pi=_pi,
                             prompt="A very hot steel pipe cooling down in a quiet room: slow irregular metallic ticking and pinging as the metal contracts, faint sizzle"))

def plan(e):
    """(calls, samples, duration) for one manifest entry."""
    i = e["id"]
    nv = max(1, len(e["variants"]))
    dur = parse_dur(e)
    if i in SHOT_IDS:
        return 2, 4, 2.5
    if i.startswith("SFX_WPN_TAIL_"):
        return 2, 4, 3.5
    if i in ("SFX_BULLET_CRACK", "SFX_BULLET_WHIZ"):
        return 2, 4, 1.2
    if i.startswith("SFX_IMPACT_") or i.startswith("SFX_FS_"):
        return 1, 2, dur
    if i.startswith("SFX_EXP_"):
        return (2 if nv >= 4 else 1), 4, dur
    if e["model_or_asset_id"].startswith("mirelo"):
        if dur >= 6:
            return 1, 2, dur
        return 1, 4 if nv >= 2 else 2, dur
    return 1, 1, dur


def parse_dur(e):
    m = re.search(r"duration(?:_seconds)? ([0-9.]+)", e["notes"] or "")
    if m:
        return float(m.group(1))
    m = re.search(r"([0-9.]+) s source", e["budget"] or "")
    return float(m.group(1)) if m else 2.0


def parse_neg(e):
    m = re.search(r"negative_prompt: (.*?)(?:\. Always| \(no |$)", e["notes"] or "")
    return m.group(1).strip().rstrip(".") if m else NEG


def el_takes(e):
    i = e["id"]
    nv = max(1, len(e["variants"]))
    if i.startswith("AMB_"):
        return nv + 1          # 3 takes for 2 shipped beds
    if i.startswith("SFX_UI_"):
        return nv + 2          # 4 takes for 2 shipped blips
    return nv + 1


def manifest_entries():
    m = json.loads(MANIFEST.read_text())
    return [e for e in m if e["category"] in ("sfx", "music")]


def build_jobs(ids=None, priority=None):
    jobs = []
    for e in manifest_entries():
        if e["source"] != "fal" or e["model_or_asset_id"] == "elevenlabs/music/v2.5":
            continue
        if ids and e["id"] not in ids:
            continue
        if priority and e["priority"] not in priority:
            continue
        i = e["id"]
        if i == "SFX_WPN_SHELLS":  # replaced by the stone/sand split in EXTRA_JOBS
            continue
        ep = e["model_or_asset_id"]
        if i == "STG_LAYERS_TAPE":
            for take, pr in zip(("tape_stop", "tape_rewind"), e["prompt"].split(" | ")):
                args = dict(prompt=pr, negative_prompt="music, voices, speech, singing, clipping", duration=3.0,
                            num_samples=4, upload_audio_format="wav")
                jobs.append(dict(id=i, take=take, endpoint=ep, args=args, est_usd=gen.est_mirelo(3.0, 4),
                                 out=f"{RAW}/{i}/{take}", priority=e["priority"]))
            continue
        if ep.startswith("mirelo"):
            calls, ns, dur = plan(e)
            for c in range(calls):
                pr = PROMPT.get(i, e["prompt"])
                if c == 1 and i in PROMPT_B:
                    pr = PROMPT_B[i]
                neg = NEG_SHOT if i in SHOT_IDS else parse_neg(e)
                args = dict(prompt=pr, negative_prompt=neg, duration=dur, num_samples=ns, upload_audio_format="wav")
                jobs.append(dict(id=i, take=f"t{c + 1}", endpoint=ep, args=args, est_usd=gen.est_mirelo(dur, ns),
                                 out=f"{RAW}/{i}/t{c + 1}", priority=e["priority"]))
        elif ep == "fal-ai/elevenlabs/sound-effects/v2":
            dur = parse_dur(e)
            loop = "loop: true" in (e["notes"] or "")
            for c in range(el_takes(e)):
                args = dict(text=e["prompt"], duration_seconds=dur, prompt_influence=0.45 if c % 2 == 0 else 0.5,
                            output_format="pcm_48000")
                if loop:
                    args["loop"] = True
                jobs.append(dict(id=i, take=f"t{c + 1}", endpoint=ep, args=args, est_usd=gen.est_el_sfx(dur),
                                 out=f"{RAW}/{i}/t{c + 1}", priority=e["priority"]))
    for x in EXTRA_JOBS + EXTRA_JOBS_2 + EXTRA_JOBS_3 + EXTRA_JOBS_4 + EXTRA_JOBS_5 + EXTRA_JOBS_6 + EXTRA_JOBS_7 + EXTRA_JOBS_8:
        if ids and x["id"] not in ids:
            continue
        pri = next((e["priority"] for e in manifest_entries() if e["id"] == x["id"]), "P0")
        if priority and pri not in priority:
            continue
        if x["ep"] == "mirelo":
            args = dict(prompt=x["prompt"], negative_prompt=x["neg"], duration=x["dur"], num_samples=x["n"],
                        upload_audio_format="wav")
            jobs.append(dict(id=x["id"], take=x["take"], endpoint="mirelo-ai/sfx2.0/text-to-audio", args=args,
                             est_usd=gen.est_mirelo(x["dur"], x["n"]), out=f"{RAW}/{x['id']}/{x['take']}", priority=pri))
        else:
            args = dict(text=x["prompt"], duration_seconds=x["dur"], prompt_influence=x.get("pi", 0.45), output_format="pcm_48000")
            if x.get("loop"):
                args["loop"] = True
            jobs.append(dict(id=x["id"], take=x["take"], endpoint="fal-ai/elevenlabs/sound-effects/v2", args=args,
                             est_usd=gen.est_el_sfx(x["dur"]), out=f"{RAW}/{x['id']}/{x['take']}", priority=pri))
    return jobs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--run", action="store_true")
    ap.add_argument("--priority")
    ap.add_argument("--ids")
    ap.add_argument("--workers", type=int, default=6)
    a = ap.parse_args()
    ids = set(a.ids.split(",")) if a.ids else None
    pri = set(a.priority.split(",")) if a.priority else None
    jobs = build_jobs(ids, pri)
    todo = [j for j in jobs if not gen.done(ROOT / j["out"])]
    print(f"{len(jobs)} jobs, {len(todo)} to run, est ${sum(j['est_usd'] for j in todo):.2f} "
          f"(all: ${sum(j['est_usd'] for j in jobs):.2f})")
    if a.dry_run:
        by = {}
        for j in jobs:
            by.setdefault(j["priority"], 0)
            by[j["priority"]] += j["est_usd"]
        print(json.dumps({k: round(v, 2) for k, v in by.items()}))
        return
    if a.run:
        gen.run_jobs(todo, workers=a.workers)


if __name__ == "__main__":
    main()
