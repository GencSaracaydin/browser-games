"""Shared helpers for the WP06 VO pipeline (paths, audio IO, loudness, true peak, WER).

Run every VO tool with the Tools venv (numpy, scipy, soundfile, pyloudnorm, librosa):
    /Users/gencsaracaydin/Documents/fun/fun-unity/Tools/.venv/bin/python Tools/audio/vo/<tool>.py ...
fal calls go through Tools/fal/falcall.py with the fal venv (/Users/gencsaracaydin/Documents/fun/.venv).
"""
import difflib, json, math, pathlib, re, hashlib
import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[3]
DOCS = ROOT / "Docs"
TOOLS = ROOT / "Tools" / "audio" / "vo"
RAW = ROOT / "RawAssets" / "audio" / "vo"
RAWDIR = RAW / "raw"          # falcall outputs per line/take
WORK = RAW / "work"           # decoded + trimmed takes, STT results
TOKENS = RAW / "tokens"       # Clockmaker token takes (offline only)
PREVIEWS = RAW / "previews"
READY = ROOT / "RawAssets" / "audio" / "ready"
VO_OUT = READY / "Assets" / "_Game" / "Audio" / "VO"
ENV_OUT = READY / "Assets" / "_Game" / "Audio" / "Envelopes"
INDEX = READY / "import_index_vo.json"
FAL_PY = "/Users/gencsaracaydin/Documents/fun/.venv/bin/python"
FALCALL = ROOT / "Tools" / "fal" / "falcall.py"
WP_TAG = "WP06VO"
SR = 48000


def load_dialogue():
    return {l["id"]: l for l in json.loads((DOCS / "DIALOGUE.json").read_text())}


def load_manifest():
    return {e["id"]: e for e in json.loads((DOCS / "ASSET_MANIFEST.json").read_text())}


def load_casting():
    return json.loads((TOOLS / "casting.json").read_text())


def stable_seed(*parts):
    return int(hashlib.md5("|".join(map(str, parts)).encode()).hexdigest()[:8], 16)


# ---------------------------------------------------------------- audio IO
def read_audio(path, raw_sr=48000):
    """Return (mono float64, sr). Handles ElevenLabs headerless PCM (*.pcm)."""
    import soundfile as sf
    path = str(path)
    if path.endswith(".pcm"):
        x = np.frombuffer(open(path, "rb").read(), dtype="<i2").astype(np.float64) / 32768.0
        return x, raw_sr
    x, sr = sf.read(path, always_2d=True)
    return x.mean(axis=1), sr


def to_sr(x, sr, target=SR):
    if sr == target:
        return x
    from scipy.signal import resample_poly
    g = math.gcd(int(sr), int(target))
    return resample_poly(x, target // g, sr // g)


def write_wav16(path, x, sr=SR, seed=0):
    """16-bit PCM WAV with TPDF dither (1 LSB)."""
    import soundfile as sf
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(seed)
    lsb = 1.0 / 32768.0
    d = (rng.random(len(x)) - rng.random(len(x))) * lsb
    y = np.clip(x + d, -1.0, 32767 / 32768)
    sf.write(str(path), y, sr, subtype="PCM_16")


# ---------------------------------------------------------------- metering
def lufs(x, sr=SR):
    import pyloudnorm as pyln
    if len(x) < int(0.42 * sr):  # BS.1770 needs a full 400 ms block; zero pad (gated out)
        x = np.concatenate([x, np.zeros(int(0.42 * sr) - len(x))])
    return pyln.Meter(sr).integrated_loudness(x)


def true_peak_db(x, sr=SR, os=4):
    from scipy.signal import resample_poly
    y = resample_poly(x, os, 1)
    return 20 * np.log10(max(np.abs(y).max(), 1e-12))


def sample_peak_db(x):
    return 20 * np.log10(max(np.abs(x).max(), 1e-12))


def frame_rms_db(x, sr=SR, win_ms=10):
    w = max(1, int(sr * win_ms / 1000))
    n = len(x) // w
    if n == 0:
        return np.array([-120.0])
    f = x[: n * w].reshape(n, w)
    return 20 * np.log10(np.sqrt((f ** 2).mean(axis=1)) + 1e-12)


def lead_silence_ms(x, sr=SR, rel_db=-40, abs_db=-60):
    """Time before the first 10 ms frame above max(peak frame + rel_db, abs_db): anything 40 dB under the loudest
    frame (room noise, a faint breath) counts as silence; squelch, hiss beds and audible breaths count as sound."""
    db = frame_rms_db(x, sr, 10)
    thr = max(db.max() + rel_db, abs_db)
    on = np.where(db > thr)[0]
    return float(on[0] * 10) if len(on) else float(len(x) / sr * 1000)


def _old_lead_silence_ms(x, sr=SR, rel_db=-50, abs_db=-60):
    db = frame_rms_db(x, sr, 2)
    thr = max(db.max() + rel_db, abs_db)
    on = np.where(db > thr)[0]
    return float(on[0] * 2) if len(on) else float(len(x) / sr * 1000)


def tail_silence_ms(x, sr=SR, rel_db=-50, abs_db=-60):
    db = frame_rms_db(x, sr, 2)
    thr = max(db.max() + rel_db, abs_db)
    on = np.where(db > thr)[0]
    return float((len(db) - 1 - on[-1]) * 2) if len(on) else 0.0


def clipped_samples(x, thr=0.9995):
    return int((np.abs(x) >= thr).sum())


def envelope30(x, sr=SR, rate=30):
    hop = sr // rate
    n = int(math.ceil(len(x) / hop))
    pad = np.concatenate([x, np.zeros(n * hop - len(x))])
    f = pad.reshape(n, hop)
    return np.sqrt((f ** 2).mean(axis=1))


# ---------------------------------------------------------------- text normalisation + WER
ONES = "zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen".split()
TENS = "_ _ twenty thirty forty fifty sixty seventy eighty ninety".split()


def num_words(n):
    n = int(n)
    if n < 20:
        return ONES[n]
    if n < 100:
        return TENS[n // 10] + ("" if n % 10 == 0 else " " + ONES[n % 10])
    if n < 1000:
        r = n % 100
        return ONES[n // 100] + " hundred" + ("" if r == 0 else " " + num_words(r))
    if n < 10000 and n % 100 != 0 and 1100 <= n <= 2099:  # 1840 -> eighteen forty
        return num_words(n // 100) + " " + (("oh " + ONES[n % 100]) if n % 100 < 10 else num_words(n % 100))
    if n < 1000000:
        r = n % 1000
        return num_words(n // 1000) + " thousand" + ("" if r == 0 else " " + num_words(r))
    return " ".join(ONES[int(c)] for c in str(n))


def _expand_numbers(s):
    s = re.sub(r"\b(\d{1,2}):(\d{2})\b", lambda m: num_words(m.group(1)) + " " + (num_words(m.group(2)) if m.group(2) != "00" else "hundred"), s)
    def dig(m):
        t = m.group(0)
        if len(t) >= 5 or (len(t) > 1 and t.startswith("0")):
            return " ".join(ONES[int(c)] for c in t)
        return num_words(t)
    s = re.sub(r"\d+", dig, s)
    return s


CONTRACT = {"in'": "ing"}


def norm_words(s):
    s = s.lower().replace("’", "'").replace("‘", "'")
    s = re.sub(r"\[[^\]]*\]", " ", s)            # audio-event tags in transcripts, e.g. [breathing]
    s = re.sub(r"\([^)]*\)", " ", s)
    s = re.sub(r"(\w)in'(?=\W|$)", r"\1ing", s)   # cuttin' -> cutting
    s = re.sub(r"'em\b", "them", s)
    s = re.sub(r"([a-z])-(\d)", r"\1 \2", s)
    s = _expand_numbers(s)
    s = s.replace("-", " ").replace("'", "")
    s = re.sub(r"[^a-z0-9 ]+", " ", s)
    return [w for w in s.split() if w]


ALIASES = {  # spoken-equivalent spellings that STT may produce for our proper nouns / slang
    "cher": {"sha", "shah", "share", "chere", "chair", "shaw", "chez"},
    "zaroud": {"zarud", "zarood", "zaroo", "zarude", "zaruud", "zarouds"},
    "zarouds": {"zaruds", "zaroods", "zarud", "zaroud"},
    "harrous": {"harus", "harrows", "haroos", "harrus", "harrouss"},
    "ok": {"okay"},
    "nothing": {"nothin"},
    "tinsmiths": {"tinsmith", "tinsmiths"},
    "s": {"es", "ess"},
    "greycoats": {"graycoats", "grey", "gray"},
    "sundial": {"sun", "sundials"},
    "cal": {"cow", "kal", "carl", "cale"},
}


def word_eq(a, b):
    if a == b or b in ALIASES.get(a, ()) or a in ALIASES.get(b, ()):
        return True
    if len(a) >= 4 and len(b) >= 3 and difflib.SequenceMatcher(None, a, b).ratio() >= 0.8:
        return True
    return False


def wer(ref_text, hyp_text):
    """Normalised word error rate with fuzzy word equality. Returns dict(wer, errors, n, ops)."""
    r, h = norm_words(ref_text), norm_words(hyp_text)
    # join compound splits in the hypothesis/reference ("sun dial" vs "sundial")
    n, m = len(r), len(h)
    D = np.zeros((n + 1, m + 1), dtype=int)
    D[:, 0] = np.arange(n + 1)
    D[0, :] = np.arange(m + 1)
    for i in range(1, n + 1):
        for j in range(1, m + 1):
            sub = 0 if word_eq(r[i - 1], h[j - 1]) else 1
            D[i, j] = min(D[i - 1, j] + 1, D[i, j - 1] + 1, D[i - 1, j - 1] + sub)
    # backtrace for ops
    i, j, ops = n, m, []
    while i > 0 or j > 0:
        if i > 0 and j > 0 and D[i, j] == D[i - 1, j - 1] + (0 if word_eq(r[i - 1], h[j - 1]) else 1):
            if not word_eq(r[i - 1], h[j - 1]):
                ops.append(("S", r[i - 1], h[j - 1]))
            i, j = i - 1, j - 1
        elif i > 0 and D[i, j] == D[i - 1, j] + 1:
            ops.append(("D", r[i - 1], None)); i -= 1
        else:
            ops.append(("I", None, h[j - 1])); j -= 1
    ops.reverse()
    e = int(D[n, m])
    return {"wer": round(e / max(n, 1), 3), "errors": e, "n": n, "ref": r, "hyp": h, "ops": ops}


def allowed_errors(n):
    if n <= 3:
        return 0
    if n <= 9:
        return 1
    return int(round(0.12 * n))
