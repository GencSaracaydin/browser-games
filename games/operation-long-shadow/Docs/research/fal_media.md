# fal.ai for 2D and audio assets: research and test results

Researched 2026-10-16 against the live fal catalog (Platform API `GET api.fal.ai/v1/models`, 1,524 endpoints) and pricing API, plus per-model `llms.txt` schemas. 7 paid test calls (about $0.85 in total). Test outputs are in `RawAssets/falmedia/`, and the reusable scripts are in `RawAssets/falmedia/tools/`.

> **How the audio was judged:** I could not listen to the clips. Every audio verdict below comes from measurements (peak and true peak, LUFS, crest factor, clipped samples, leading and trailing silence, attack, bandwidth, tempo) and from waveform and spectrogram plots (`*.analysis.png` next to each file). Someone still needs to listen before we commit to a model, especially for gunshot punch and voice acting.

---

## 1. Recommendations (TL;DR)

| Asset class | Primary endpoint | Backup / complement | Unit price (2026-10-16) | Why |
|---|---|---|---|---|
| Weapon one-shots, impacts, explosions, whizz/crack, foley | `mirelo-ai/sfx2.0/text-to-audio` | `fal-ai/elevenlabs/sound-effects/v2` for "body"/tail layers | $0.010/s per sample (promo until 2026-12-13, then $0.015) | Tested: clean single transient (~3–5 ms rise), no clipping, peak-normalised to −1 dBFS, 48 kHz mono WAV, 2 variations per call. Needs pre-roll trim. |
| Loops: desert wind, distant firefight, rotor, radio static, room tones, UI blips | `fal-ai/elevenlabs/sound-effects/v2` (`loop: true`) | `mirelo-ai/sfx1.6/text-to-audio` (`ambience: true`), `sonilo/v1.1/text-to-sound-effects` | $0.002/s (EL); $0.01/s (Mirelo 1.6); $0.0018/s (Sonilo) | Native seamless-loop flag, cheap, true stereo, 48 kHz. Hot output that clips on "loud" prompts, so don't use it for weapon transients. |
| Radio chatter, squad VO, interrogator (4–6 consistent voices) | `elevenlabs/tts/eleven-v4` (preset voices + audio tags) | `google/gemini-3.8-flash-tts` (30 voices + free-text style direction); `fal-ai/minimax/speech-2.8-hd` + `fal-ai/minimax/voice-design` for a bespoke interrogator | $0.08/1k chars; $0.045/1k; $0.10/1k + $3/designed voice | Tested both: clean, fast (~3–4 s), tags not voiced, consistent preset identities. EL v4 = 48 kHz, Gemini = 24 kHz (fine for radio). |
| Music (briefing tension, combat loop, title stinger, debrief) | `elevenlabs/music/v2.5` | `google/lyria-3.5` for cheap exploration; `fal-ai/stable-audio-3/medium/text-to-audio` for drones/beds | $0.60 per started minute; $0.10/gen; $0.0376/gen | Tested: exact length (`music_length_ms`), `force_instrumental`, BPM honoured (96 BPM verified), 48 kHz stereo WAV, composition plans + `audio_reference` for a consistent score. |
| Photoreal concept art, intel photos, dossier images, loading screens | `google/nano-banana-2.1` | `blackforestlabs/flux-3/text-to-image`, `openai/gpt-image-2.5/flare/text-to-image` | $0.040 (1K) / $0.059 (2K) / $0.134 (4K) | Tested: rendered all 3 overlay strings exactly, period CRT look in 24 s. Slightly "diorama" clean; add grain/CRT in-engine. |
| Title cards (typography) | Text in Unity (TextMeshPro) over a generated plate | `ideogram/v4.5` (best typography), nano-banana-2.1 | $0.03/0.06/0.22 (low/med/high) | Live text is editable, crisp at any res, and can be animated. Ideogram if baked lettering is wanted. |
| UI icons | `openai/gpt-image-2.5/flare/text-to-image` with `background: "transparent"` | `fal-ai/recraft/v4.1/text-to-vector` (SVG, $0.08), `recraft/v4/style/*` for a locked style | ~$0.013 (medium, 1024²) | Alpha PNG straight out; Recraft for a consistent vector set. |
| Decals (bullet holes, scorch, blood, posters/graffiti) | gpt-image-2.5 transparent, or `fal-ai/ideogram/v3/generate-transparent` | generate on flat bg + `fal-ai/bria/background/remove` ($0.018) | $0.01–0.09 | Need alpha; 1:1 sheets. |
| Seamless/PBR textures (only for CC0 gaps) | `fal-ai/patina/material` (basecolor+normal+rough+metal+height) | `fal-ai/z-image/turbo/tiling` ($0.02/MP), `ideogram/v4/tiling` | $0.08 for 1K with 5 maps; $0.61 for 8K | Use ambientCG/Poly Haven first. |
| 360 sky panoramas | **Poly Haven HDRIs (CC0)**: 997 HDRIs, 64 arid/desert-tagged (e.g. `goegap`, `klippad_sunrise_1/2`, `aarfontein_*`), up to 24K, true HDR for lighting | fal: `fal-ai/qwen-image-2512/lora` + ProGamerGov `qwen-360-diffusion` LoRA (MIT) at 2048×1024; `fal-ai/hunyuan_world` (image→pano) | $0.035/MP (~$0.073 per 2048×1024); $0.15 | A real HDR beats an 8-bit generated LDR pano for URP lighting; use fal only for stylised/night/smoke skies. Untested. |

---

## 2. The 7 test calls

All outputs are in `RawAssets/falmedia/<NN_name>/` with `request.json`, `response.json`, `meta.json` (request id, wall time) and `*.analysis.{png,json}`.

| # | Endpoint | Ask | Wall time | Output format (measured) | What I found |
|---|---|---|---|---|---|
| 1 | `fal-ai/elevenlabs/sound-effects/v2` | AK single shot, 2.0 s, `prompt_influence 0.7`, `pcm_48000` | 4.5 s | **WAV** 48 kHz 16-bit **stereo** (true stereo, L/R corr 0.68); served as `audio/l16` | **Clipped**: 336 samples at 0 dBFS, true peak +0.6 dBTP. Onset rises over ~15–20 ms (soft for a gunshot), then 4–5 dense transients in the first 180 ms (shot + clatter/slap, or a double; needs a listen). Big and "hyped": momentary max −8 LUFS. Sound ends at 0.87 s, then 1.13 s of tail silence. |
| 2 | `mirelo-ai/sfx2.0/text-to-audio` | same AK prompt + negative prompt, 2.0 s, 2 samples, wav | 6.7 s | **WAV** 48 kHz 16-bit **mono**, peak-normalised to exactly −1.0 dBFS | **Best transient**: one clean crack, ~3–5 ms rise, low-frequency pressure wave after it, short echo; no clipping. But **205–375 ms pre-roll** of −50…−60 dB noise and a −45…−55 dB noise bed after the shot, so trim and fade. Thinner than #1 (momentary max −22…−23 LUFS; crest 27–29 dB). |
| 3 | `mirelo-ai/sfx2.0/text-to-audio` | large HE blast + debris, 6 s, 2 samples | 6.0 s | WAV 48k mono, −1.0 dBFS | Sample A: detonation at 31 ms, 68% of the energy below 150 Hz, ~2.5 s of debris/rumble tail. Sample B: **detonation 2.78 s into the file** (the onset position is random), darker (82% below 150 Hz). Very peaky (crest 31–34 dB); a look-ahead limiter adds only about 4 LU because the first 50–100 ms really is the loud part. |
| 4 | `elevenlabs/tts/eleven-v4` | "[urgent, low voice] Bravo Two, hold position. Tangos moving on the east wall.", voice `Brian`, stability 0.4, seed 1984, `pcm_48000`, `timestamps: true` | 3.9 s | **RAW headerless PCM** (`.pcm`, `application/octet-stream`), 48 kHz 16-bit mono LE. Confirmed by the timestamps (last char ends at 3.68 s = 176,640 samples @ 48k) | Clean, peak −3.3 dBFS, −20.9 LUFS, 54 ms lead, 280 ms tail, 0.4 s natural pause between sentences. Audio tag consumed silently (tag chars timestamped in 0–80 ms). Hard low-pass at ~15.6 kHz (irrelevant for radio). Median F0 134 Hz. **Must wrap to WAV** (`fal_post.py pcm2wav`) or request mp3. |
| 5 | `google/gemini-3.8-flash-tts` | same line, voice `Algenib` + `style_instructions` (gravel-voiced team leader, low, clipped, out of breath) | 3.3 s | **WAV 24 kHz** 16-bit mono, peak −1.2 dBFS | Clean, −19.9 LUFS. **290 ms of breath pre-roll** and a 0.63 s mid-line pause (the "out of breath" direction was followed). Breathier/brighter than EL (centroid 2.4 kHz versus 0.85 kHz). 24 kHz is plenty after the radio band-limit. |
| 6 | `elevenlabs/music/v2.5` | 30 s, `force_instrumental`, 80s Cold War synth briefing at 96 BPM, `pcm_48000` | **7.2 s** | WAV 48 kHz 16-bit stereo | Starts on beat 1 with no silence, builds steadily (−19 → −14 dB RMS), −14.8 LUFS, no clipping. **96 BPM honoured** (onset autocorrelation: 16th-note pulse 0.156 s, bar 2.5 s, bar-lag AC 0.75). **It wrote its own ending** (outro from 27.5 s = bar 11, fades to −50 dB), so loops must be cut before it. Billed as 1 minute. |
| 7 | `google/nano-banana-2.1` | grainy 1986 recon photo of a walled desert compound on a CRT, with 3 overlay strings; 4:3, 2K, png | 24.4 s | PNG 2400×1792 RGB, 6.9 MB | Excellent composition; **all text exact** ("SITE VULTURE-7", "14 MAR 1986 0412Z", "FRAME 0417"), reticle, bezel, trucks, water tower; no people, flags or religious buildings. A little too clean and miniature-like, with light grain and faint scanlines. |

Processed versions (made offline with `tools/fal_post.py`, no ffmpeg) are in `RawAssets/falmedia/processed/`:
- `sfx_ak_shot_mirelo_a.wav` / `_b.wav`: pre-roll cut to 3 ms before onset, DC removed, 80 ms tail fade, −1 dBFS (1.6–1.8 s long). `_a_punch.wav` adds 9 dB into a look-ahead limiter.
- `sfx_ak_shot_elevenlabs.wav`: trimmed to 0.88 s. The clipping distortion is baked in and can't be undone.
- `sfx_explosion_large_a_punch.wav` / `_b_punch.wav`: pre-roll removed (B goes from 6.0 s to 3.2 s), 12 dB limiter drive, 300 ms fade.
- `vo_radio_bravo2_hold_el4_brian.wav` / `_gemini_algenib.wav`: field-radio FX (320–3200 Hz band-pass, tanh drive, compression, 1.6 kHz honk, hiss bed, procedural key-up squelch and release burst, post band-limit 250–4200 Hz).
- `vo_clean_bravo2_hold_el4_brian.wav`: trimmed and set to −18 LUFS.
- `mus_briefing_loop_6bars.wav`: bars 6–11 (12.5–27.5 s) as a 15.0 s loop with a 0.25 s equal-power crossfade into the pre-loop audio. Seam jump is 0.004 (inaudible-level step).

---

## 3. Sound effects

### 3.1 Endpoint comparison

| Endpoint | Price | Duration | Samples/call | Output | Loop | Notes |
|---|---|---|---|---|---|---|
| `mirelo-ai/sfx2.0/text-to-audio` (2026-10-14) | $0.010/s per sample (→ $0.015 from 2026-12-14) | 0.1–600 s | 1–4 (default 2!) | wav/mp3/aac/flac, 48k mono | no flag | `prompt`, `negative_prompt`, `seed`. **Tested best for transients.** Random onset position → always trim. Note that default `num_samples=2` doubles the bill. |
| `mirelo-ai/sfx1.6/text-to-audio` | $0.01/s | 0.1–60 s | 1–4 | wav/mp3/aac/flac | **`ambience: true`** = seamless loop tile, `double_output` | Field is `text_prompt` (not `prompt`). Companion `sfx1.6/extend-audio` and `inpaint-audio`. |
| `fal-ai/elevenlabs/sound-effects/v2` | $0.002/s | 0.5–22 s (auto if unset) | 1 | mp3_* / **pcm_* (came back as WAV)** / opus / ulaw | **`loop: true`** | `text`, `prompt_influence` 0–1 (default 0.3). Stereo. Clipped on a "loud gunshot" prompt. |
| `sonilo/v1.1/text-to-sound-effects` | $0.0018/s per sample | 0.5–180 s | – | aac (default)/mp3/wav/flac | – | Marketed as "commercial-use-safe" (licensed training data). Untested. |
| `fal-ai/stable-audio-3/small/sfx/text-to-audio` | $0.0206 per clip (flat) | 1–120 s | 1 | mp3/wav/flac/ogg/opus/m4a | via `…/audio-outpainting`/`inpainting` | Cheapest for long beds; `negative_prompt`, 8 steps. Untested. |
| `cassetteai/sound-effects-generator` | $0.01/gen | 1–30 s | 1 | – | – | Older (2025-04). |
| `bytedance/seed-audio-1.0` | $0.1875/min | – | – | wav/mp3/pcm/ogg, 8–48k | – | Mostly a speech/voice model; not an SFX pick. |

### 3.2 Per-sound recipe

Build gun sounds the way shipped shooters do: **layers** (mechanical + crack/transient + body/boom + tail/echo), several variants each, with random pitch ±3% and volume ±1.5 dB per shot. Unity Web supports `pitch`, `PlayOneShot` and scheduled play. Auto fire = re-trigger the one-shot at the weapon RPM (AK ≈ 600 rpm → 100 ms) plus a separate tail on trigger release.

| Sound | Model | Prompt core (add "one shot only" for one-shots) | Takes |
|---|---|---|---|
| AK-pattern shot (close) | Mirelo 2.0, 2 s, 2–4 samples | *(tested, §5)* | 4 variants |
| M16-pattern shot | Mirelo 2.0 | "single 5.56mm M16A1 rifle shot outdoors, sharper and higher-pitched crack than an AK, tight snap, short desert echo" | 4 |
| Pistol (M1911) | Mirelo 2.0 | ".45 ACP M1911 pistol single shot outdoors, heavy blunt report, slide cycling click right after" | 3 |
| Suppressed pistol/SMG | Mirelo 2.0 | "suppressed subsonic pistol shot, muffled thump with mechanical slide clack louder than the shot, no echo" | 3 |
| Body/boom layer | EL SFX v2 (prompt without "loud"), lower `prompt_influence` 0.4 | "deep low-frequency rifle report body, chest thump, no crack" | 2 |
| Tail / echo (outdoor) | EL SFX v2 or Mirelo | "gunshot echo tail rolling across an open desert valley, no initial shot" | 3 |
| Reloads, bolt/charging handle, mag in/out | Mirelo 2.0, 1.5–3 s | "AK-47 magazine removed and new magazine inserted, metal on metal, close-mic foley, no gunshot" / "M16 charging handle pulled and released" | 2–3 each |
| Frag grenade | Mirelo 2.0, 4 s | "M67 fragmentation grenade explosion 15 m away outdoors, sharp crack, dirt spray, short rumble" | 4 |
| Large explosion | Mirelo 2.0, 6 s *(tested, §5)* | *(see §5)* | 3 |
| Footsteps sand/stone/wood | Mirelo 2.0, 10–15 s | "single person walking steadily on loose desert sand in combat boots, close-mic, no ambience" | 1 sequence per surface, **sliced into 8–12 steps** by onset detection (cheap variety) |
| Bullet impacts (sand, stone, wood, metal, flesh-neutral) | Mirelo 2.0, 6–8 s | "series of individual bullet impacts on adobe mud-brick wall, chips and dust, spaced one second apart" | 1 sequence → slice |
| Bullet whizz / supersonic crack | Mirelo 2.0, 1 s | "supersonic bullet passing close by the ear, sharp snap and short zip" | 4 |
| Desert wind ambience | **EL SFX v2 `loop: true`, 20 s** or Mirelo 1.6 `ambience: true`, 30 s | "dry desert wind gusting over sand and rocks, distant flapping cloth, no voices, no birds" | 2 |
| Distant gunfire bed | EL SFX v2 loop / Mirelo 1.6 ambience, 30 s | "distant sporadic AK bursts and single shots about one kilometre away, echoing across a desert valley" | 2 |
| Helicopter (UH-1 Huey) loop + flyby | EL SFX v2 `loop: true` (hover); Mirelo 2.0 (flyby 8 s) | "UH-1 Huey helicopter hovering nearby, characteristic two-blade wop-wop, turbine whine" | 2 + 2 |
| Radio static / squelch | EL SFX v2, or **procedural** (`fal_post.radio()` already generates key-up and release bursts) | "military field radio squelch break and short static burst" | 3 |
| UI blips (menu, objective, CRT typing) | EL SFX v2 0.5–1 s | "1980s computer terminal beep, single short square-wave blip" / "dot-matrix teletype chatter" | 6–8 |

Distant and occluded variants must be **baked** (WebGL has no low-pass filter; see §6). Make a `_far` version of each gunshot with a 1.5–3 kHz low-pass and a longer echo, then crossfade near and far by distance in script with volume only.

---

## 4. Speech (radio chatter, squad, interrogator, enemy barks)

### 4.1 Endpoint comparison

| Endpoint | Price | Voices / identity | Emotion / direction | Output | Verdict |
|---|---|---|---|---|---|
| `elevenlabs/tts/eleven-v4` (2026-09-28) **tested** | $0.08/1k chars | 20 named presets (Aria, Roger, Sarah, Laura, Charlie, George, Callum, River, Liam, Charlotte, Alice, Matilda, Will, Jessica, Eric, Chris, Brian, Daniel, Lily, Bill); `seed` | audio tags `[whispering]`, `[shouting]`, free tags like `[urgent, low voice]`; `stability`, `similarity_boost`; IPA in `/…/`; char `timestamps` | mp3_*, **pcm_* = raw headerless**, opus, ulaw/alaw | **Primary.** Best-known acting quality, fixed preset = consistent voice; timestamps are handy for subtitles. |
| `elevenlabs/tts/eleven-v4-turbo` | $0.04/1k | same | same | same | Use for drafts and placeholder VO. |
| `fal-ai/elevenlabs/text-to-dialogue/eleven-v3` | $0.10/1k | presets | tags | – | Multi-voice exchanges in one file (the interrogation scene), but you get one file to cut. |
| `google/gemini-3.8-flash-tts` (2026-09-25) **tested** | $0.045/1k | 30 prebuilt (e.g. Algenib "gravelly", Charon, Orus, Fenrir, Iapetus, Alnilam, Schedar, Umbriel, Kore…); 2-speaker `speakers`/`turns` | **free-text `style_instructions`** (best "director" control), inline `<laugh>` `<sigh>` | WAV 24 kHz | **Strong second.** Half the price, the best natural-language direction; 24 kHz only. |
| `fal-ai/minimax/speech-2.8-hd` | $0.10/1k (turbo $0.06) | 17+ presets (Deep_Voice_Man, Imposing_Manner, Determined_Man, Patient_Man, …) or a custom id | `emotion`, `voice_modify` (pitch/intensity/timbre ±100), pauses `<#0.5#>`, `(coughs)` `(sighs)`… | mp3/pcm/flac up to 44.1k; **built-in loudness normalisation (default −18 LUFS)** | Pair with `fal-ai/minimax/voice-design` ($3/voice, from a text description) for a one-off bespoke **interrogator** voice. |
| `fal-ai/qwen-3-tts/voice-design/1.7b` | $0.09/1k | voice from text prompt (per call, so not inherently consistent) | prompt | – | Design a reference once, then clone with `qwen-3-tts/clone-voice`. Two steps. |
| `resemble-ai/chatterboxhd/text-to-speech` | $0.04/1k | 9 presets or zero-shot clone (`audio_url`) | `exaggeration` 0.25–2, `cfg` | 24k, or 48k with `high_quality_audio` | Good emotion knob; cloning needs a reference we'd have to make first. |
| `fal-ai/chatterbox/text-to-speech` | $0.025/1k | clone from `audio_url` | `exaggeration`, `<sigh>`, `<gasp>`… | – | Cheap cloned VO; drift risk. |
| `fal-ai/dia-tts` (+ `/voice-clone`) | $0.04/1k | `[S1]/[S2]` dialogue | nonverbals | – | Good for 2-person banter; voices vary per call unless cloned. |
| `fal-ai/kokoro/american-english` | $0.02/1k | 20 voices (am_adam, am_michael, am_onyx, am_fenrir…) | none (speed only) | – | Flat delivery, so placeholder/temp VO only. |
| `fal-ai/f5-tts` | $0.05/1k | clone only (`ref_audio_url` required) | none | – | Older; skip. |
| `fal-ai/inworld-tts` (TTS-1.5 Max) | $0.01/1k | many presets (Hank, Graham, Callum, Mortimer…) | – | up to 48k | Cheapest quality option; untested. |
| `xai/tts/v1` | $0.015/1k | 25+ presets | `[pause]`, `<whisper>`… | mp3 24k default | Untested. |
| Others seen: `fal-ai/zonos2` (clone, $0.01/min), `fal-ai/index-tts-2/text-to-speech` (clone + emotion ref, $0.002/s), `fal-ai/bytedance/seed-speech/tts/v2` ($0.03/1k), `fal-ai/maya` ($0.002/s), `async/tts-pro/v1.0` ($0.01/1k), `fal-ai/vibevoice/7b` ($0.04/min). |

### 4.2 Casting plan (6 consistent voices)

Lock **voice + settings + seed** per character in a table and never change them mid-production. All on `elevenlabs/tts/eleven-v4` (preset names from the endpoint's example list):

| Character | Voice | Settings | Notes |
|---|---|---|---|
| Team leader (radio, "Bravo One") | `Brian` (tested: deep, F0 ≈ 134 Hz) | stability 0.4, sim 0.75 | radio FX |
| Point man / squadmate 2 | `Chris` or `Eric` | stability 0.35 | radio FX when off-screen |
| Young radio op / squadmate 3 | `Liam` or `Will` | stability 0.35 | |
| CIA handler (briefing, numbers-station vibe) | `Bill` or `Roger` | stability 0.6 (calmer) | phone-line FX (300–3000 Hz, no squelch) |
| Interrogator (intro) | `Callum` (husky), or a MiniMax `voice-design` custom voice "gruff, heavy-set man in his fifties, slow menacing cadence, smoker's rasp" | stability 0.5 | dry room, light baked slap reverb |
| Enemy barks (invented language) | 2–3 voices not reused for allies (e.g. `Daniel`, `George`) | `[shouting]` tag, stability 0.3 | see below |

**Enemy invented language (untested):** write a small fixed lexicon (about 20 words) with consistent phonotactics, e.g. *"Vesh! Kaddo marrin! Tal-esh, tal-esh!"*. Don't use real Arabic, Persian or Turkish words, and never anything religious. Feed the phonetic text with `[shouting]` and no `language_code`, or use Gemini with `style_instructions: "shouting in an unfamiliar language; pronounce every word exactly as spelled"`. For distant enemy chatter, use SFX walla instead ("distant soldiers shouting unintelligibly across a courtyard"). It is unintelligible by construction, so it can't accidentally say something real.

**Prompt rules that worked:** put delivery in a leading tag (EL) or in `style_instructions` (Gemini), never in the spoken text. Keep lines short (1–2 sentences) so each line is its own file; use a fixed `seed` for re-takes. Expect 50–300 ms of pre-roll and a 250–300 ms tail, so trim (§6).

---

## 5. Prompt recipes that worked (verbatim)

**AK shot, Mirelo SFX 2.0** (`duration 2.0, num_samples 2, upload_audio_format wav`):
```
prompt: A single gunshot from an AK-47 assault rifle (7.62x39mm) fired outdoors in open desert, close first-person perspective. Sharp, loud supersonic crack with a punchy low-end thump, then a short natural echo slap off distant rock walls. One shot only.
negative_prompt: music, voices, speech, burst fire, multiple shots, reverb hall, distortion, clipping
```
The same text on EL SFX v2 (`prompt_influence 0.7`) gave a hotter, clipped, multi-transient result. For EL, drop "loud" and try `prompt_influence` 0.4–0.5 (untested hypothesis).

**Large explosion, Mirelo SFX 2.0** (`duration 6, num_samples 2`):
```
prompt: A powerful high-explosive blast outdoors in a desert village, heard from about 30 meters away: an instant sharp detonation crack, a deep chest-thumping low boom, then dirt, gravel and debris raining down and a long rolling echo fading across open terrain.
negative_prompt: music, voices, speech, sci-fi, whoosh, riser, cinematic braam, distortion, clipping
```

**Radio line, ElevenLabs v4:** `text: "[urgent, low voice] Bravo Two, hold position. Tangos moving on the east wall."`, `voice: Brian, stability 0.4, similarity_boost 0.75, seed 1984, language_code en, output_format pcm_48000, timestamps true`.

**Radio line, Gemini 3.8 Flash TTS:** `prompt: "Bravo Two, hold position. Tangos moving on the east wall."`, `voice: Algenib`, `style_instructions: "A gravel-voiced American special-operations team leader in his forties, speaking low, fast and clipped into a field radio during a night raid. Tense but controlled, professional military cadence, slightly out of breath. No smile, no drama."` (drop "out of breath" if you don't want the breath pre-roll and pauses).

**Music, ElevenLabs Music v2.5** (`music_length_ms 30000, force_instrumental true, output_format pcm_48000`):
```
Tense 1980s Cold War espionage thriller score, instrumental underscore for a covert-ops mission briefing. Analog polysynth pads in the style of a Prophet-5 or Juno-60, a pulsing sequenced arpeggio bassline at 96 BPM, sparse gated-reverb LinnDrum snare and deep low toms, cold detuned string-machine swells, faint shortwave numbers-station radio static in the background. Minor key, slow-building dread, steady tempo, no melody resolution, no vocals.
```
For loops, add "seamless loop, no intro, no ending, constant intensity", or use a `composition_plan` (chunks of 3–120 s with `positive_styles`, `negative_styles` and `context_adherence`) and pass the main theme as `audio_reference` on chunk 1 of the other cues to keep one sound palette. **Billing rounds up to whole minutes ($0.60/min)**, so a 60 s cue costs the same as a 30 s one: generate 60–90 s and cut.

**CRT intel photo, Nano Banana 2.1** (`aspect_ratio 4:3, resolution 2K, output_format png, thinking_level medium`):
```
A grainy 1986 CIA reconnaissance photograph shown on an old curved CRT monitor in a dark briefing room: an oblique long-lens telephoto shot of a walled mud-brick compound on the edge of a desert town at dawn, flat roofs, a water tower, two parked military trucks, a dirt road, low dusty hills behind. Heavy film grain, slight motion blur, washed-out sepia-green phosphor tint, visible horizontal scanlines, faint screen curvature and vignette, slight chromatic fringing, a white thin-line targeting reticle drawn around one building. Overlay text in a blocky 1980s computer font in the top-left corner: 'SITE VULTURE-7' and on the next line '14 MAR 1986 0412Z', and bottom-right: 'FRAME 0417'. Photorealistic, documentary, no people visible, no flags, no religious buildings.
```
For the in-game montage, generate the **raw photo without the TV bezel** ("declassified 35mm Kodak Tri-X surveillance photograph, 600mm lens, heat haze, heavy grain") and apply CRT curvature, scanlines, flicker and typing overlays in a URP UI shader. Animated, consistent, and the text stays editable.

**360 sky via fal (untested recipe):** `fal-ai/qwen-image-2512/lora`, `image_size {width:2048,height:1024}`, `loras: [{path: "https://huggingface.co/ProGamerGov/qwen-360-diffusion/resolve/main/qwen-360-diffusion-2512-int8-bf16-v2.safetensors", scale: 1.0}]`, prompt starting "360 degree panorama with equirectangular projection, …". The card says 2:1 at 2048×1024 is required for a correct horizon, and the left and right edges wrap. Upscale with `fal-ai/seedvr/upscale/image` ($0.001/MP). The user's own `rehan-fal/minimax-h3-360-equirect-lora` is a **video** LoRA (H3, ~$1 for a 5 s 4K clip). Only worth it for an animated sky (moving clouds or smoke) behind the exfil.

---

## 6. Post-processing (no ffmpeg) and Unity Web constraints

### 6.1 What Unity's Web build can and cannot do (Unity 6 manual, "Audio in Web")
- Audio is **re-encoded to AAC** for Web builds, whatever the source format. Import WAV.
- **No audio effects at all:** no low-pass/high-pass/distortion/echo/reverb filters or reverb zones. **AudioMixer = group volume only.** So radio FX, reverb tails, distance and occlusion muffling must be **baked into the files**.
- Load types: DecompressOnLoad and CompressedInMemory. **Streaming is not supported**, so music is fully loaded and should be kept short (60–90 s loops).
- Supported: positional playback (pan, rolloff, Doppler), `pitch` (> 0), `PlayOneShot`, `PlayScheduled`/`SetScheduledStartTime/EndTime` (use these for sample-accurate intro→loop music transitions). `GetData/SetData` only on DecompressOnLoad clips.
- **Loop glitch:** AAC encoders can alter the first 1024 samples, which glitches the loop point. Unity's workarounds: keep the loop as WAV, or add ≥1024 samples of lead-in and set the `smpl` loop start after it. Alternatively, crossfade two AudioSources with `PlayScheduled`. Test loops in the real browser build.
- Browsers block audio until a user gesture, so the intro needs a "click to begin" before the interrogation scene's audio.
- The browser may decode at a different rate than 44.1k; read `AudioClip.frequency` at runtime.
- The AudioClip importer has **no trim, fade or loop-point controls** (only Force To Mono with Normalize, Load Type, Preload, Quality, Sample Rate override). Trimming happens offline, or with the Unity MCP tool `Unity_AudioClip_Edit` (`TrimSilence`, `TrimSound`, `ChangeVolume`, `LoopSound` with `crossfadeDurationMs`). It works on clips under `Assets/`; I didn't try it (it would import into `Assets/`). Check whether it consumes Unity AI credits before relying on it.

### 6.2 Offline toolchain (verified on this Mac)
- `soundfile` (libsndfile 1.2.2 wheel) **reads WAV/FLAC/OGG/MP3 and writes WAV/FLAC/OGG Vorbis/Opus/MP3 without ffmpeg** (checked with `sf.available_formats()`).
- `numpy` + `scipy` + `pyloudnorm` cover filters, limiting, LUFS, slicing and crossfades.
- **pydub is not usable** on Python 3.13 without `audioop-lts`, and it needs ffmpeg for anything but WAV.
- There is a bundled static **ffmpeg 7.1** at `/Users/gencsaracaydin/Documents/fun/.venv/lib/python3.12/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1` (from `imageio-ffmpeg` in the sandbox venv) if a real ffmpeg is ever needed (for example, to preview AAC artefacts). The pipeline doesn't need it.
- Scripts in `RawAssets/falmedia/tools/`:
  - `falrun.py <out_dir> <endpoint> <args.json>`: one fal call; saves request, response and timing, and downloads the outputs. Reads `FAL_KEY` from the environment and never prints it.
  - `fal_post.py`:
    - `sfx`: DC removal, onset-aware pre-roll trim, tail fade, peak normalisation, optional `--punch --drive N` look-ahead limiter.
    - `voice`: trim, optional `--radio` FX, optional `--lufs`.
    - `loop --start --end --xfade`: bar-aligned equal-power loop.
    - `pcm2wav`: wraps ElevenLabs raw PCM in a WAV header.
  - `analyze.py`: the metrics and plots used in this doc.
  - Setup: `uv venv .venv && uv pip install numpy scipy soundfile pyloudnorm matplotlib`.

### 6.3 Per-class processing and import settings

| Class | Offline steps | Level target | Unity import |
|---|---|---|---|
| Weapon/impact one-shots | `fal_post.py sfx` (Mirelo pre-roll is 0.2–2.8 s, so always trim); optional `--punch --drive 6–9`; layer; make `_far` variants (baked LPF + echo) | peak −1 dBFS; match layers by momentary loudness, not peak (Mirelo shots came back ~13 LU quieter than EL at equal peak) | Force To Mono, DecompressOnLoad, Preload |
| Explosions | `sfx --punch --drive 9–12 --floor-db -60 --fade-ms 300` | peak −1 | Mono, DecompressOnLoad |
| Ambience loops | EL `loop: true` or Mirelo 1.6 `ambience: true`; else `fal_post.py loop` | −24 to −20 LUFS | stereo OK (2D), CompressedInMemory |
| Radio VO | `fal_post.py pcm2wav` (EL) → `voice --radio` | −18 LUFS integrated | Mono, Sample Rate override 22050 (halves size; band-limited anyway) |
| Clean VO (intro, interrogation) | `voice --lufs -18`; bake a short room slap if wanted (convolve with a synthetic IR; or `fal-ai/workflow-utilities/impulse-response`, $0.001/s) | −18 LUFS | Mono, CompressedInMemory |
| Music | cut on bar lines before the generated outro, `loop` with a 0.25–0.5 s crossfade; stinger as a separate one-shot | −16 to −14 LUFS before mixer attenuation | CompressedInMemory; consider lower Quality to save size |

**Size budget:** the host caps each binary file at 15 MB, and Unity's `.data` holds every asset. At typical AAC rates, ~60 SFX (~2 s), ~70 VO lines (~3 s, mono 22k) and 4 music cues (60–90 s stereo) come to roughly 10–15 MB of audio before build compression. Keep music loops short, use mono wherever the sound is spatialised, and plan Addressables or bundle splits if `.data` grows past 15 MB.

---

## 7. Cost estimate for the full game

Assumes iteration (best-of-N takes), list prices on 2026-10-16, and Mirelo at its standard $0.015/s rate (the promo ends 2026-12-13).

| Bucket | Assumptions | Model | Est. cost |
|---|---|---|---|
| ~35 one-shots (weapons, reloads, impacts, grenades, whizz, footstep sequences) | avg 3 s, 2 samples per call, 2 calls per asset | Mirelo SFX 2.0 | 35×2×2×3×$0.015 ≈ **$6.30** ($4.20 at promo) |
| ~25 loops, layers, UI | avg 12 s, 3 takes | EL SFX v2 | 25×3×12×$0.002 ≈ **$1.80** |
| ~70 VO lines + enemy barks + interrogation | avg 70 chars incl. tags, 3 takes → ~15k chars | EL v4 ($0.08/1k) | **≈ $1.20** (Gemini: $0.70; MiniMax HD + 1 designed voice: $1.50 + $3) |
| 4 music cues (stinger 10 s, briefing 90 s, combat 90 s, debrief/exfil 120 s) | 4 takes each, billed per started minute (1+2+2+2 = 7 min per pass) | EL Music v2.5 | 4×7×$0.60 ≈ **$16.80** (explore first with Lyria 3.5 at $0.10/gen: ~$2) |
| ~30 images (concept, intel photos, dossiers, loading screens, title plates) | 3 takes at 2K, 6 loading screens re-rendered at 4K | Nano Banana 2.1 | 90×$0.059 + 6×$0.134 ≈ **$6.10** |
| Icons/decals (say 40 finals, 2 takes) | 1024² medium, transparent | GPT-image-2.5 | 80×$0.013 ≈ **$1.05** |
| Texture gaps (5 PBR sets, 2K base, 5 maps) | | Patina | 5×~$0.30 ≈ **$1.50** |
| Optional fal skies (4 panos, 3 takes) | 2048×1024 + SeedVR upscale | Qwen-Image-2512 + 360 LoRA | 12×~$0.08 ≈ **$1.00** |
| **Total** | | | **≈ $36–40** (a one-take floor is ~$12; a generous 2× re-iteration ceiling is ~$80) |

This test run cost about $0.85: SFX $0.004 + $0.04 + $0.12, TTS ~$0.01–0.02, music $0.60, image ~$0.06. I couldn't confirm the billed amounts because the key gets a 403 on the billing-events API.

---

## 8. Risks and open items
- **Nobody has listened yet.** Gunshot punch (Mirelo's clean but thin transient versus EL's fat but clipped one), voice acting, and music taste all need ears before bulk generation.
- EL SFX v2 **clips** on aggressive prompts (+0.6 dBTP, 336 clipped samples). Clipping can't be repaired, so regenerate rather than fix.
- Mirelo onset position is random (0.03–2.8 s in), so every file needs trimming. `num_samples` defaults to 2 and doubles the cost if you forget it.
- EL TTS `pcm_*` returns **headerless** PCM. If it is imported raw, Unity sees garbage, so always run `pcm2wav` or ask for mp3.
- AI music writes intros and endings even when asked not to, so loops have to be cut by hand on bar lines. Loop seams may glitch after Unity's AAC encode on Web; test in the browser build.
- Text-to-image may slip real-world script or slogans into graffiti and posters. Prompt for "invented geometric glyphs that resemble no real alphabet" and review every decal. Better still, design the invented script once and render posters in-engine.
- `Unity_AudioClip_Edit` (MCP) is untested, and I don't know whether it needs Unity AI credits.
- Prices are a snapshot. Mirelo 2.0's promo ends 2026-12-13, and fal adds models weekly (Mirelo SFX 2.0 and Krea v3 both appeared on 2026-10-14). Re-check `https://fal.ai/models/<id>/llms.txt` before bulk runs.

## Appendix: references
- Catalog and pricing snapshots: `RawAssets/falmedia/research/fal_catalog_audio_image_2026-10-16.json`, `fal_pricing_snapshot_2026-10-16.json`
- Unity Web audio: https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html
- Qwen 360 LoRA: https://huggingface.co/ProGamerGov/qwen-360-diffusion
- Poly Haven HDRI API: https://api.polyhaven.com/assets?t=hdris
