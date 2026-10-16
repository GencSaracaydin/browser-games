#!/usr/bin/env python3
"""Upload local files to fal storage (free; no inference) and cache the URL next to each file as <file>.url.
Run with the fal venv: /Users/gencsaracaydin/Documents/fun/.venv/bin/python Tools/audio/vo/fal_upload.py f1.wav f2.wav ...
Prints a JSON map {path: url}. FAL_KEY is read by falcall.load_key() and never printed.
"""
import concurrent.futures as cf, json, pathlib, sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / "fal"))
import falcall  # noqa: E402

falcall.load_key()
import fal_client  # noqa: E402


def up(p):
    p = pathlib.Path(p)
    cache = p.with_name(p.name + ".url")
    if cache.exists() and cache.stat().st_mtime >= p.stat().st_mtime:
        return str(p), cache.read_text().strip()
    for attempt in range(3):
        try:
            url = fal_client.upload_file(str(p))
            cache.write_text(url)
            return str(p), url
        except Exception as e:  # noqa: BLE001
            err = e
    return str(p), "ERROR " + str(err)[:200]


if __name__ == "__main__":
    with cf.ThreadPoolExecutor(8) as ex:
        print(json.dumps(dict(ex.map(up, sys.argv[1:])), indent=1))
