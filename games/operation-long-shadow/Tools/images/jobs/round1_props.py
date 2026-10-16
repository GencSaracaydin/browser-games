"""Round-1 booth prop reference images (nano-banana 2K) -> jobs/round1_props.json (prop tail appended by --prop-tail)."""
import json, pathlib
V = " 3/4 elevated view from the front-left, camera perpendicular, no perspective distortion, entire object visible, centered, filling about 90% of the frame width."
P = {
 "FAL_SCRAMBLER_BOX": ("4:3", "Photorealistic studio product photograph of a grey 1970s military-surplus electronic voice scrambler unit: a rectangular steel box about 35 cm wide in worn grey hammertone paint with rounded corners and a front panel holding one small rectangular analog VU meter with a cream dial face and a black needle, one chrome bat-handle toggle switch, three black bakelite knobs with skirts, a jewel indicator lamp, screw heads at the corners, a coiled black handset cable plugged into the side, scuffed edges showing bare metal."),
 "FAL_INTERROGATION_CHAIR": ("1:1", "Photorealistic studio product photograph of a heavy institutional steel chair from the 1970s bolted to a small square floor plate: square tube frame, flat steel seat and upright backrest, flat armrests with worn brown leather wrist cuffs with steel buckles, leather ankle straps on the front legs, chipped olive-grey paint showing rust and bare steel, thin black and red wires taped to the armrests with grey cloth tape."),
 "FAL_ROTARY_PHONE": ("1:1", "Photorealistic studio product photograph of a black 1970s bakelite rotary desk telephone with a plain blank rotary dial with finger holes and a chrome finger stop, handset resting in the cradle, a coiled black handset cord, slightly scuffed glossy plastic."),
 "FAL_REEL_TO_REEL": ("4:3", "Photorealistic studio product photograph of a 1970s professional reel-to-reel tape recorder deck lying flat on its back: brushed aluminium top plate with two large metal NAB tape reels holding brown tape, a tape path across the head block with a hinged head cover, two small analog VU meters with cream dial faces, chunky piano-key transport buttons, black knobs, a dark walnut wooden side frame."),
 "FAL_AV_CART": ("4:3", "Photorealistic studio product photograph of a wide tall 1970s steel audio-visual equipment cart on four rubber swivel casters: three flat rectangular steel shelves (one low near the floor, one at mid height, one on top) on four square-tube corner legs, the cart much wider than deep, worn grey hammertone paint with chipped edges and scuffs, a black rubber bumper strip along the top shelf edge, empty shelves."),
}
jobs = []
for k, (asp, pr) in P.items():
    jobs.append(["nb", k, "--tag", "ref_t1", "--aspect", asp, "--seed", "7", "--prop-tail", "--prompt", pr + V])
pathlib.Path(__file__).with_suffix(".json").write_text(json.dumps(jobs, indent=1))
print(len(jobs), "jobs")
