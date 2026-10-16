#!/usr/bin/env python3
"""Writes wp10fal_assets.json: per-asset processing spec for WP10_FAL (names, real size, budgets, refined prompts).
Edit this file and rerun to change specs. size = [mode, metres]; mode: length (largest horizontal extent),
height (vertical), max (largest extent). yaw = extra rotation (deg, about up) applied after auto-alignment.
"""
import json, pathlib
V = "3/4 elevated view from the front-left, camera perpendicular, no perspective distortion, entire object visible, centered, filling about 90% of the frame width."
def P(subject, view=V):
    return f"Photorealistic studio product photograph of {subject}, {view}"
A = {}
def add(i, name, kind, folder, tris, tex, size, prompt, prio, engine="tripo", aspect="1:1", tex_src=1024, **kw):
    A[i] = dict(name=name, kind=kind, folder=folder, tris=tris, tex=tex, tex_src=tex_src, size=size,
                ref_prompt=P(prompt), priority=prio, engine=engine, aspect=aspect, **kw)

add("FAL_SANDBAG", "Sandbag", "Props", "sandbag", 600, 512, ["length", 0.58],
    "a single filled burlap sandbag lying flat on the ground, the tied end folded under, slightly squashed and lumpy from the sand inside, coarse dusty tan hessian weave with frayed threads, faded grime and sun bleaching", "P0", tex_src=512)
add("FAL_CABLE_DRUM", "CableDrum", "Props", "cable_drum", 2500, 512, ["height", 1.3],
    "a large old wooden cable drum spool standing upright on its edge: two round plank flanges held by rusty bolts and a wooden core, wound with several layers of thick black rubber-sheathed power cable, weathered grey sun-bleached wood with cracks and dust", "P0")
add("FAL_DIESEL_PUMP", "DieselPump", "Props", "diesel_pump", 5000, 512, ["length", 1.6],
    "a 1960s single-cylinder stationary diesel engine driving a water pump, both bolted on a steel skid frame, a large spoked cast-iron flywheel, a vertical exhaust pipe, a centrifugal pump with inlet and outlet pipes, faded green paint, oil stains, rust and dust, well used", "P0")
add("FAL_MARKET_STALL", "MarketStall", "Props", "market_stall", 3000, 512, ["length", 2.4],
    "an empty old wooden market stall: a sloped plank display counter on sturdy legs, four thin wooden poles holding a faded, patched, slightly sagging plain canvas sunshade, weathered sun-bleached planks, dusty, well used", "P0")
add("FAL_FRUIT_CRATE_ORANGE", "FruitCrateOrange", "Props", "fruit_crate_orange", 3000, 512, ["length", 0.5],
    "a shallow slatted wooden fruit crate made of rough pine slats, heaped full of ripe oranges, dusty", "P0")
add("FAL_FRUIT_CRATE_POMEGRANATE", "FruitCratePomegranate", "Props", "fruit_crate_pomegranate", 3000, 512, ["length", 0.5],
    "a shallow slatted wooden fruit crate made of rough pine slats, heaped full of ripe red pomegranates, dusty", "P1")
add("FAL_ORANGE", "Orange", "Props", "orange", 300, 256, ["max", 0.078],
    "a single ripe orange with a dimpled peel and a small dry stem scar", "P0", engine="hy_rapid", tex_src=256)
add("FAL_POMEGRANATE", "Pomegranate", "Props", "pomegranate", 300, 256, ["max", 0.095],
    "a single ripe red pomegranate with its small crown", "P1", engine="hy_rapid", tex_src=256)
add("FAL_TELETYPE", "Teletype", "Props", "teletype", 3000, 1024, ["height", 1.05],
    "a 1960s floor-standing teleprinter machine on its own steel stand: a grey-green enamel steel cabinet with a hinged top cover, a mechanical keyboard with plain round keys, a paper roll and paper feed on top, worn paint and dust", "P0")
add("FAL_TUBE_RACK", "TubeRack", "Props", "tube_rack", 6000, 1024, ["height", 2.0],
    "a tall 1950s vacuum-tube radio transmitter cabinet: a grey steel rack cabinet with a perforated steel mesh front door, large warm orange glowing glass valves visible behind the mesh, analog meters and bakelite dials on an upper panel, cooling vents, scuffed paint", "P0", aspect="3:4", emissive="orange")
add("FAL_WALL_SAFE", "WallSafe", "Props", "wall_safe", 3000, 1024, ["height", 1.25],
    "a heavy steel vault safe door in its thick square steel frame, made to sit flush in a wall: a large chrome combination dial, a spoked handle wheel, two heavy hinges, chipped dark green paint with worn steel edges", "P0")
add("FAL_DRILL_RIG", "DrillRig", "Props", "drill_rig", 3000, 512, ["height", 0.6],
    "a portable 1980s magnetic-base core drill rig: a rectangular magnetic base, a vertical slide column with a three-spoke feed handle, an electric motor head, a long hollow core drill bit pointing down, a coiled rubber power cable, orange-red paint with scuffs and grime", "P0")
add("FAL_PA_HORN", "PAHorn", "Props", "pa_horn", 2000, 512, ["max", 1.0],
    "a cluster of three weathered grey steel public-address horn loudspeakers mounted on a short steel pole bracket and pointing in three directions, rust streaks, dust, a cable running down the pole", "P0")
add("FAL_FILM_CAN", "FilmCan", "Props", "film_can", 1500, 512, ["max", 0.38],
    "a round steel 16 mm film can lying flat with its lid off and leaning against it, a metal film reel wound with brown film inside, a blank paper label area on the lid, scratched grey metal", "P0", variants={"closed": "a closed round steel 16 mm film can lying flat, a blank cream paper label on the lid, scratched grey metal with dents"})
add("FAL_CINEMA_SEAT", "CinemaSeat", "Props", "cinema_seat", 1500, 512, ["height", 0.95],
    "a single 1950s cinema theatre seat: a folding seat and backrest upholstered in worn red velvet, cast-iron side standards with simple curved Art Deco lines, wooden armrests, scuffed and dusty", "P0")
add("FAL_TYPEWRITER", "Typewriter", "Props", "typewriter", 3000, 512, ["length", 0.45],
    "a 1960s manual office typewriter with a dark green enamel body, plain round keys, a black platen roller and a chrome carriage return lever, worn", "P1")
add("FAL_LEDGER_STACK", "LedgerStack", "Props", "ledger_stack", 1000, 512, ["length", 0.38],
    "a stack of three thick cloth-bound accounting ledgers with leather spines and leather corners in dark green, maroon and brown, worn and scuffed, slightly uneven stack, plain covers", "P0", engine="hy_rapid")
add("FAL_CODEBOOK", "Codebook", "Props", "codebook", 600, 512, ["length", 0.21],
    "a thin dark hardcover notebook closed with a black elastic band around it, worn corners, plain matte cover", "P0", engine="hy_rapid")
add("FAL_MG_TRIPOD", "MGTripod", "Props", "mg_tripod", 1500, 512, ["max", 1.1],
    "an empty folding steel machine-gun tripod mount with three splayed legs, a traversing head and an elevating screw, olive drab paint, chipped and worn", "P1")
add("FAL_SATCHEL_CHARGE", "SatchelCharge", "Props", "satchel_charge", 1200, 512, ["length", 0.36],
    "a military canvas demolition satchel charge: an olive drab canvas bag with a carrying strap and steel buckles, a pull-ring fuse igniter with a short length of safety fuse attached, worn and dusty", "P0")
add("FAL_BOLT_CUTTER", "BoltCutter", "Props", "bolt_cutter", 1200, 512, ["length", 0.9],
    "a pair of long-handled heavy-duty bolt cutters with red rubber grips, forged steel jaws and pivot bolts, worn paint", "P0", aspect="16:9")
add("FAL_ON_AIR_LAMP", "OnAirLamp", "Props", "on_air_lamp", 800, 512, ["length", 0.32],
    "a 1950s wall-mounted broadcast warning lamp: a rectangular steel housing with a plain blank red glass front panel, four screws, chipped cream paint", "P1", engine="hy_rapid", emissive="red")
add("FAL_KETTLE", "Kettle", "Props", "kettle", 1000, 512, ["height", 0.22],
    "an old enamel tea kettle, chipped white enamel with a blue rim, a black bakelite handle", "P2", engine="hy_rapid")
add("FAL_MEAL_PLATE", "MealPlate", "Props", "meal_plate", 1500, 512, ["length", 0.40],
    "a small round metal tray holding a ceramic plate with a half-eaten flatbread and a few olives, and a small clear glass of tea", "P1", engine="hy_rapid")
add("FAL_FUEL_BOWSER", "FuelBowser", "Props", "fuel_bowser", 5000, 512, ["length", 3.0],
    "a small two-wheeled fuel bowser trailer: a cylindrical steel tank on a chassis with two wheels, a towing drawbar with a jockey wheel and a hand pump, faded olive paint, rust streaks and dust", "P0")
add("FAL_PRC77", "PRC77", "Props", "prc77", 3000, 1024, ["height", 0.40],
    "a 1970s PRC-77 style military manpack radio set: an olive drab steel radio case with knobs and connectors on top, a battery box below, a canvas carrying harness with straps, a handset with a coiled cord clipped to the side, a short antenna base with the whip antenna removed", "P0")
add("FAL_CAMERA_35MM", "Camera35mm", "Props", "camera_35mm", 2000, 512, ["length", 0.145],
    "a black 1980s 35 mm SLR film camera with a standard 50 mm lens, no strap, worn black paint with brass showing on the edges", "P0")
add("FAL_DIVE_WATCH", "DiveWatch", "Props", "dive_watch", 1500, 512, ["max", 0.072],
    "a 1980s stainless steel diver's wristwatch with a black rotating bezel with plain tick marks, a black dial with round luminous hour markers and hands, a black rubber strap closed in a loop as if worn on a wrist", "P0")
add("FAL_WOODEN_BENCH", "WoodenBench", "Props", "wooden_bench", 1200, 512, ["length", 1.6],
    "a weathered wooden bench with a two-plank seat and simple square legs with cross braces, sun-bleached grey wood, cracks and dust", "P1")
add("FAL_WALL_SPEAKER", "WallSpeaker", "Props", "wall_speaker", 800, 512, ["length", 0.32],
    "a small 1960s wall-mounted loudspeaker box: a wooden cabinet with a woven cloth grille and a metal wall bracket, worn", "P1", engine="hy_rapid")
add("FAL_DRILL_BAG", "DrillBag", "Props", "drill_bag", 1200, 512, ["length", 0.42],
    "a worn olive canvas field bag with a leather-trimmed flap, buckles and a shoulder strap folded on top", "P2", engine="hy_rapid")
add("VEH_TECHNICAL", "Technical", "Vehicles", "technical", 12000, 2048, ["length", 4.3],
    "a 1970s compact two-door pickup truck converted into a technical: faded sun-bleached sand paint with primer patches, dented panels, rust and dust, no number plates, a welded steel pedestal post for a heavy machine gun standing empty in the middle of the cargo bed with no gun mounted, a pipe roll bar behind the cab, dull pitted chrome bumpers, worn tyres", "P0", aspect="4:3")
add("VEH_CARGO_TRUCK", "CargoTruck", "Vehicles", "cargo_truck", 12000, 1024, ["length", 6.7], 
    "a 1970s medium 6x6 military cargo truck with a canvas tarp-covered cargo bed on bows, faded khaki-tan paint, dust, mud and rust streaks, dents, no markings, no number plates, worn off-road tyres", "P0", aspect="4:3",
    variants={"tarp_off": "Same truck, identical design, paint and wear, same camera view, but with the canvas tarp removed: an open cargo bed with bare steel bows and wooden drop sides and bench seats visible. Pure white background, no text."})
add("VEH_HELICOPTER", "Helicopter", "Vehicles", "helicopter", 15000, 2048, ["length", 12.9],
    "an unmarked sand-painted 1970s twin-engine medium utility helicopter in the style of a Bell 212: twin-engine hump, landing skids, both sliding cabin doors slid open, the main rotor blades and tail rotor blades removed so only the rotor hub, mast and tail rotor hub remain, faded paint with exhaust soot and dust streaks, no markings", "P0", aspect="16:9")
add("VEG_OLIVE_TREE", "OliveTree", "Vegetation", "olive_tree", 8000, 1024, ["height", 4.5],
    "a mature gnarled olive tree with a thick twisted, partly hollow grey trunk splitting into several crooked branches, an open, airy, irregular canopy of narrow silver-green leaves, standing alone on bare ground, isolated", "P0", engine="tree")
add("VEG_POMEGRANATE_TREE", "PomegranateTree", "Vegetation", "pomegranate_tree", 6000, 1024, ["height", 3.0],
    "a small multi-stemmed pomegranate tree with several thin grey trunks, a dense rounded canopy of glossy green leaves and scattered red pomegranate fruit, standing alone on bare ground, isolated", "P0", engine="tree")

pathlib.Path(__file__).with_name("wp10fal_assets.json").write_text(json.dumps(A, indent=1))
print(len(A), "assets")
