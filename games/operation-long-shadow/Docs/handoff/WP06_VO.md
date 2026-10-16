# WP06_VO handoff: voice-over

Status: **done**. Every manifest `vo` entry is produced: 165 DIALOGUE.json lines, all P0 and P1. That is 305 clips in every chain the manifest lists, plus 165 envelopes, the Clockmaker token takes and the 10 locked voice castings. fal spend: **$10.77 of $45** (`WP06VO`).

The run was interrupted when the internet dropped and was then resumed. All 528 earlier takes were reused. The resumed run finished transcription and scoring, added the sound-alike guardrail, replaced three Greyspeak words, retook a few barks, spliced the Clockmaker lines, and built, audited and documented everything.

## 1. Inventory (import-ready, `RawAssets/audio/ready/`)

| What | Where | Count |
|---|---|---|
| VO clips, 48 kHz 16-bit mono WAV, −18 LUFS, ≤ −1 dBTP, lead ≤ 60 ms | `Assets/_Game/Audio/VO/<CHAIN>/VO_<id>_<CHAIN>[_A/_B].wav` | 305 |
| Envelopes (30 Hz RMS per clip, D10) | `Assets/_Game/Audio/Envelopes/VO_<id>.json` | 165 |
| Import index (one row per file, with notes) | `import_index_vo.json` | 470 rows |

Clips per chain: DRY 124, RADIO 104, FAR 30, SCRAMBLER 18, GREYCOAT_SQUELCH 9, INTERCOM 5, BOOTH 4, CLEAN 3, PA_HORN 3, TAPE 2, HF_AM 2, ROOM_SPEAKER 1. Total: 14.1 min of VO, ≈ 5.4 MB in the build at the §3.12 import rules (18.2 KB per clip on average; the budget is ≤ 30 KB).

Not imported (offline source, `RawAssets/audio/vo/`):
- `tokens/<token>/take_n/`: `VO_CLOCKMAKER_TOKENS`, 14 tokens, 68 takes; `token_choice.json` records the chosen take for each.
- `clockmaker/`: dry splices with their STT checks, `splice_report.json`, and `NUM_BASEMENT_LOOP_from_tokens.wav` (see §5).
- `raw/`, `work/`: every TTS take, decoded, trimmed and transcribed. `selection.json` holds the chosen take per take family. `verify_report.json` holds all metrics. `build_report.json` and `audit.json` cover the shipped files.
- `previews/`: waveform and spectrogram contact sheets per voice and per chain. `obsolete/`: takes of retired bark texts.

Casting (`VOICE_*`): `Tools/audio/vo/casting.json` (locked). The confirmation table is in `Docs/AUDIO_REVIEW.md` → VO → Voice casting.

## 2. Import instructions (editor holder)

1. Copy `RawAssets/audio/ready/Assets/_Game/Audio/VO/**` and `.../Envelopes/**` to the same paths under `Assets/`. Folder names are the chain names, and `AudioImportRules` keys on them (TECH_PLAN §3.12). The settings are repeated in every index row:
   - RADIO, INTERCOM, HF_AM, GREYCOAT_SQUELCH, PA_HORN: Force To Mono, Compressed In Memory, **Preload off**, Load In Background, Quality 60, 22 050 Hz override.
   - DRY, FAR, SCRAMBLER, TAPE, CLEAN, BOOTH, ROOM_SPEAKER: as above, but Quality 70 and a 24 000 Hz override.
   - All VO clips are one-shots (not looping).
2. Envelopes are TextAssets that `DialogueImporter` reads. Format:
   - Top level: `{id, rate_hz: 30, hop_samples_48k: 1600, kind: "rms_linear", clips: {<clip>: {chain, variant, duration_s, speech_offset_s, rms_max, rms[]}}, voice: {...}}`.
   - Clockmaker lines also carry `token_onsets_s`.
   - To sample: `rms[min(int(t*30), n-1)]`.
3. Then run `DialogueImporter`. Every line has a clip in every chain listed in its manifest entry. The independent audit checks this (`vo_audit.py`, 0 failures).

Notes the index rows carry where they matter:
- **DRY/RADIO pairs** (and D200 INTERCOM/RADIO) are sample-synchronous for the VOManager crossfade. Start both together.
- **FAR** is a separate distant bark, not synced to DRY.
- **TAPE**: the words start 0.22 s into the clip (motor start); `speech_offset_s` gives the exact offset.
- **D039**: switch to 2D at the first "Seven", 1.83 s into the clip.
- **D066**: cut into static, 2.2 s.
- **D082**: ends 30 ms after the last word.
- **D201 and D204**: the rotor bed is baked 12 dB under the voice.
- **Legion barks**: two voices, `_A` and `_B`.

## 3. Tools (`Tools/audio/vo/`) and how to rerun

Run with `Tools/.venv/bin/python` (numpy, scipy, soundfile, pyloudnorm, librosa, matplotlib, wordfreq, unidecode). fal calls go through `Tools/fal/falcall.py --wp WP06VO`, which the scripts call with the fal venv. Every step is idempotent.

```
cd Tools/audio/vo
../../.venv/bin/python vo_generate.py lines [--ids D001,...] [--start 4 --takes 6] [--variant A]   # TTS takes (3 by default)
../../.venv/bin/python vo_generate.py tokens [--ids attention --start 7 --takes 10]               # Clockmaker tokens
../../.venv/bin/python vo_edit.py                       # first-N-words edits (CO_HOL_04, D087) -> take_10n
../../.venv/bin/python vo_verify.py all [--ids ...]     # decode/trim/metrics, Scribe v2 STT, score + choose a take
../../.venv/bin/python vo_verify.py tokens              # same for the tokens
../../.venv/bin/python clockmaker_splice.py pick && ../../.venv/bin/python clockmaker_splice.py lines && ../../.venv/bin/python clockmaker_splice.py stt
../../.venv/bin/python vo_build.py [--ids ...]          # chains -> ready/ WAVs, envelopes, import_index_vo.json
../../.venv/bin/python vo_audit.py                      # independent re-measure of the files on disk (exit 1 on failure)
../../.venv/bin/python vo_report.py [--sheets]          # Docs/AUDIO_REVIEW.md VO section (+ preview PNGs)
```

| Script | Role |
|---|---|
| `casting.json` | Locked casting per `voice_id`. For EL take n the seed is the casting seed + 1000·(n−1). Gemini voices use a fixed style text. |
| `vo_generate.py` | Builds the requests. Delivery goes into the EL tag or the Gemini `style_instructions`, never into the spoken text. `RESPELL` holds TTS-only pronunciations. |
| `vo_verify.py` | Per-take checks: normalised WER, tag words spoken aloud, cut-off, clipping, speech rate, gaps, and pitch against sibling takes. For invented words it adds duration, the **sound-alike** check (wordfreq, 27 languages, zipf ≥ 3.2) and letter similarity. It then picks a take per family; `locked` entries in `selection.json` are respected. |
| `vo_edit.py` | Keeps the first N words of a take, cut by STT word timestamps. |
| `vo_chains.py` | All 12 offline chains (GDD §10.1, D9) plus `finalize` (−18 LUFS, true-peak limiter at 4× oversampling). It can also be used as a library by WP06_SFX. |
| `clockmaker_splice.py` | Token pick (penalises the stretch needed), 0.9 s grid splices, WSOLA fit for long tokens, STT check, and the seamless basement loop. |
| `vo_build.py` | Selected take → every manifest chain → finalize → lead enforcement on non-synced chains → envelopes and index. |
| `vo_audit.py`, `vo_report.py`, `vo_preview.py`, `fal_upload.py`, `common.py` | Audit, review document, contact sheets, free fal storage upload, shared helpers. |

To add or change a line: edit DIALOGUE.json and the manifest, then run `vo_generate.py lines --ids X`, `vo_verify.py all --ids X`, `vo_build.py --ids X`, `vo_audit.py` and `vo_report.py`.

## 4. Deviations from the manifest and spec (all logged in `Docs/DECISIONS.md`)

- **Lexicon changes in DIALOGUE.json and GDD §14.4** (subtitles unchanged):
  - Earlier run: `ras→vrask`, `os→isk`, `tor→tavv`, `om→uvan`, `veshti→veskri`.
  - This run, because the takes *sounded* like real speech: `Sauth→Ulsk` (English "south"), `Ekt-ha→Orvak` (Hindi "ek tha"), `Ivvo→Ottrek` (the Slavic name "Ivo").
  - The web check results are in `RawAssets/audio/vo/webcheck.json` and `AUDIO_REVIEW.md`.
- **Take edits instead of retakes:**
  - CO_HOL_04 and D087: EL repeats them in every take, so the first reading is kept.
  - BK_LEG_07 voice A uses a TTS-only respelling ("Mehn-nik!").
- **Clockmaker tokens:** Attention, Sundial and Zero are 1.0–1.08 s even with a brisk style. They are WSOLA-compressed (≤ 1.26×) to fit the 0.9 s grid. D039 is 13.53 s.
- **Chains:** INTERCOM gets a 5.2 kHz post-drive low-pass and ROOM_SPEAKER a 6 kHz one (realism). FAR and BOOTH heads longer than 60 ms are trimmed to meet the ≤ 60 ms lead rule. D004 loses most of a soft leading inhale.
- **The manifest says 3 takes per line.** Barks that failed the guardrail got 3–15 extra takes, and the tokens 6–10. All of it is cheap (Gemini about $0.01, EL about $0.002 per take).

## 5. Known issues and open items

- **No human listening.** O4 was waived, so quality rests on the objective checks and the spectrogram review. The accent guardrail is a proxy only: accents are never in the tags, Gemini styles ask for "faint, never exaggerated", and pitch and rate are consistent per voice.
- **Legion barks are transcribed as Italian, Czech, Estonian or English-ish non-words.** None of them is a common real word, apart from the reviewed exception `pira` (pt/it/es "pyre", harmless). BK_LEG_07_A is heard as the rare name "Menelik".
- **`NUM_BASEMENT_LOOP` belongs to WP06_SFX (music).** A ready source loop is at `RawAssets/audio/vo/clockmaker/NUM_BASEMENT_LOOP_from_tokens.wav` (66.1 s, seamless, ROOM_SPEAKER chain); the hand-over is noted in `Docs/REQUESTS.md`. It uses `RawAssets/audio/music/procedural/INTERVAL_SIGNAL_DRY.wav`; rebuild it if that file changes.
- **Hollis's shouted callouts sit an octave above his speaking voice** (103 Hz spoken, 301 Hz shouted). That is natural for shouting, but they are the timbre outliers of his voice.
- **The ready WAVs are 87 MB on disk** (48 kHz PCM, as the import rules expect). Unity re-encodes them to AAC at 22–24 kHz.
- **The TAPE hiss bed is −42 dB under the speech, by design.** If the booth mix feels hissy, lower it in `chain_tape` (one constant) and rebuild D002 and D003.
