# Stillwater — ASCII Exploration Adventure

A small, complete 3D exploration game inspired by the supplied ASCII fantasy-game recording. Everything in the scene is original procedural geometry. The world is rendered as colored characters, including the traveler's real, rippling reflection.

Wander a forgotten lakeside abbey, gather three echoes, and return them to the northern altar. After the ending, the world stays open for exploration.

## Play locally

```sh
npm ci
npm run dev
```

Open the URL printed by Vite. For a production build:

```sh
npm run build
npm run preview
```

## Controls

| Action | Desktop | Touch |
| --- | --- | --- |
| Move | WASD or arrow keys | Left thumbstick |
| Run | Hold Shift | Toggle ↑↑ |
| Interact | E, Enter, or Space | ✦ or on-screen prompt |
| Orbit camera | Drag the scene; Q / R | Drag the scene |
| Zoom | Scroll; settings slider | Settings slider |
| Map | M or map icon | Map icon |
| Journal | J or book icon | Book icon |
| Pause / settings | Esc or sliders icon | Sliders icon |
| Fullscreen | F or fullscreen icon | Fullscreen icon where supported |

Sound starts muted; enable it with the speaker icon. Settings include glyph density, three palettes, reduced motion, and camera distance. Your journey and visual preferences save locally in the current browser. The restart confirmation clears only this game's journey.

## Implementation

- Three.js scene with instanced vegetation and masonry, deterministic terrain and colliders.
- GPU ASCII postprocess with separate grass, stone, and water character patterns.
- Planar reflected scene with animated surface distortion, pooled footstep rings, and particles.
- Animated cloaked traveler and opt-in synthesized Web Audio ambience.
- Responsive DOM interface, keyboard/touch input, journal, map, and quest progression.
- No API keys, server, external fonts, downloaded art, analytics, or network dependencies at runtime.

## Verification

`npm test` runs the Playwright scenario suite against an already running server (default `http://127.0.0.1:5173`). Install the browser with `npx playwright install chromium` if needed. Test results and screenshots are written to `test-results/qa/`.

The supplied web-game skill client is also exercised without modifications, using `tests/resolve-playwright.mjs` to resolve the local Playwright dependency. The game exposes `window.render_game_to_text()` and `window.advanceTime(ms)` for deterministic testing. Additional diagnostic controls exist only when opened with `?test=1`.

This is a single-player exploration game. It recreates the clip's ASCII world, movement, vegetation, ruins, and water-reflection experience; the source recording does not establish networking or MMO systems.

Additional focused checks are available in `tests/audio-audit.mjs` (keyboard activation, tab visibility/audio, single completion chime) and `tests/camera-occlusion.mjs` (24 camera poses, visibility and character framing). These target the development server by default. The camera automatically moves inward around solid architecture and eases back out when the view clears.

## Checkpoint

Saved on October 15, 2026 as `stillwater-ascii-v1.0.0`. This checkpoint preserves the complete playable game, source, dependency lockfile, and verification scripts. The original source snapshot was `ee10e8a54dd823cc31ca00ea7d3fe260a48a6981`. Deployment-specific configuration and generated files are excluded.
