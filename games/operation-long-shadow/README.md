# Operation Long Shadow

A Cold War first-person shooter for the browser, in the style of Call of Duty: Black Ops. It is built in Unity 6 (URP) and targets WebGL.

**October 1984, the fictional Republic of Sardahn.** You are Sergeant Nathan Calder, SUNDIAL-3, part of a four-man black-ops team sent into the market town of Zaroud at golden hour to recover a warlord's ledgers, film reels and a numbers-station codebook. The sun sets in the middle of the raid. Then the station starts reading numbers on your own squad's radio.

The mission is told as a flashback from an interrogation chair: about 14 minutes of play, with a helicopter insertion cold open, squadmates, radio chatter, mission title cards and a debrief.

> **Status: in development.** Assets and game systems are in place. Level assembly, cinematics and the first web build are in progress. There is no playable build yet.

## Controls

| Action | Input |
| --- | --- |
| Move / look | WASD / mouse |
| Fire / aim down sights | Left / right mouse button |
| Reload | R |
| Sprint | Left Shift (hold) |
| Jump / mantle | Space |
| Crouch / slide (while sprinting) | C |
| Frag grenade (hold to cook) | G |
| Knife | V |
| Interact | E |
| Switch weapon | 1 / 2, mouse wheel |
| Objectives | Tab |
| Pause | Esc |
| Skip cinematic | Hold Space for 1 s |

## Open the project

1. Install **Unity 6000.6.6f1** with the **Web Build Support** module.
2. Open this folder in Unity Hub. The first import takes a while.
3. Design docs are in [`Docs/`](Docs/): [`GDD.md`](Docs/GDD.md) for the mission, [`TECH_PLAN.md`](Docs/TECH_PLAN.md) for the architecture, and [`CINEMATIC_DIRECTION.md`](Docs/CINEMATIC_DIRECTION.md) for the camera and realism direction.
4. Builder menus live under **LongShadow/** in the editor menu bar. The web release build is **LongShadow/Build/Web Release**.

## How it was made

The game was built by Claude (Anthropic) agents driving the Unity Editor through Unity MCP.
- **Generated with [fal.ai](https://fal.ai):** 3D models (Tripo, Hunyuan 3D), images, sound effects, music and voice lines.
- **CC0 libraries:** textures, skies and props from Poly Haven and ambientCG.
- **Open-licence downloads:** characters and animation; see [CREDITS.md](CREDITS.md).

The setting, factions, languages and scripts are fictional.
