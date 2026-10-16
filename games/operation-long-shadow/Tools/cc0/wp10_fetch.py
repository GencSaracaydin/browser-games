#!/usr/bin/env python3
"""WP10_CC0 downloader: every polyhaven / ambientcg entry of Docs/ASSET_MANIFEST.json that WP10 owns.

Usage (from anywhere):
  Tools/.venv/bin/python -I Tools/cc0/wp10_fetch.py --list                 # show the work list
  Tools/.venv/bin/python -I Tools/cc0/wp10_fetch.py [--ids A,B] [--priority P0] [--category texture] [--force]

Raw files go to RawAssets/wp10/cc0/<MANIFEST_ID>/ with a provenance.json. Idempotent: files that
exist with the right size/md5 are skipped. Research downloads in RawAssets/cc0/ are reused (copied).
ambientCG zips are extracted with a whitelist (Color, NormalGL, Roughness, AO, Metalness, Opacity,
Displacement); .blend/.usdc/.mtlx/.tres/NormalDX are never extracted. No secrets are used.
"""
import argparse, hashlib, json, os, shutil, subprocess, sys, time, urllib.parse, zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MANIFEST = os.path.join(ROOT, 'Docs', 'ASSET_MANIFEST.json')
RAW = os.path.join(ROOT, 'RawAssets', 'wp10', 'cc0')
RESEARCH = os.path.join(ROOT, 'RawAssets', 'cc0')
UA = 'fun-unity-wp10-cc0-fetch'
# booth set belongs to WP09 (TECH_PLAN 11.1)
WP09 = {'PH_TELEVISION_01', 'PH_TELEVISION_02', 'PH_WALL_CLOCK', 'PH_VINTAGE_LIGHTER',
        'PH_MOUNTED_FLUORESCENT_LIGHTS', 'TEX_ACOUSTIC_TILE', 'TEX_ACG_PAINTEDPLASTER012'}
# D17 exclusions (never fetched even if someone adds them)
D17 = {'russian_food_cans_01', 'cement_bag', 'cigarette_pack', 'quiver_tree_01',
       'PaintedMetal017', 'PaintedMetal018', 'GraffitiSet001'}
PH_TEX_MAPS = ('Diffuse', 'nor_gl', 'arm', 'Displacement', 'Mask', 'Metal')
ACG_KEEP = ('_Color.', '_NormalGL.', '_Roughness.', '_AmbientOcclusion.', '_Metalness.',
            '_Opacity.', '_Displacement.')
# Barrel_01: ship only the plain oxide skins (default "explosive" skin carries hazard art, radiation has the trefoil)
PH_MODEL_SKINS = {'Barrel_01': ['oxide_green', 'oxide_grey']}


def worklist():
    m = json.load(open(MANIFEST))
    out = []
    for e in m:
        if e['source'] not in ('polyhaven', 'ambientcg') or e['id'] in WP09:
            continue
        if e['model_or_asset_id'] in D17:
            continue
        out.append(e)
    return out


def md5(path):
    h = hashlib.md5()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def curl(url, dest, size=None, digest=None, force=False):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if os.path.exists(dest) and not force:
        ok = os.path.getsize(dest) > 0 and (size is None or os.path.getsize(dest) == size)
        if ok and digest and size is None:
            ok = md5(dest) == digest
        if ok:
            return 'skip'
    tmp = dest + '.part'
    if 'ambientcg.com/get' in url:
        # the 302 target can contain spaces and '^' that curl -L sends unencoded (HTTP 400): resolve + quote
        r = subprocess.run(['curl', '-sS', '-A', UA, '-o', '/dev/null', '-w', '%{redirect_url}', url],
                           capture_output=True, text=True)
        loc = r.stdout.strip()
        if loc:
            p = urllib.parse.urlsplit(loc)
            url = urllib.parse.urlunsplit((p.scheme, p.netloc, urllib.parse.quote(urllib.parse.unquote(p.path)), p.query, ''))
    for attempt in range(4):
        r = subprocess.run(['curl', '-sSfL', '--retry', '3', '-A', UA, url, '-o', tmp])
        if r.returncode == 0:
            break
        time.sleep(2 + 3 * attempt)
    else:
        raise RuntimeError(f'download failed: {url}')
    if digest and md5(tmp) != digest:
        if size is not None and os.path.getsize(tmp) == size:
            os.replace(tmp, dest)       # Poly Haven sometimes lists a stale md5; size matches, keep it
            print('  warn: md5 differs from API listing (size matches):', url)
            return 'ok_md5_mismatch'
        os.remove(tmp)
        raise RuntimeError(f'md5 mismatch: {url}')
    os.replace(tmp, dest)
    return 'ok'


def api(url):
    r = subprocess.run(['curl', '-sSfL', '--retry', '3', '-A', UA, url], capture_output=True, check=True)
    return json.loads(r.stdout)


def safe_extract(zpath, dest):
    """Whitelist extraction, flat, no path traversal."""
    names = []
    with zipfile.ZipFile(zpath) as z:
        for info in z.infolist():
            base = os.path.basename(info.filename)
            if not base or info.is_dir():
                continue
            if not any(k in base for k in ACG_KEEP):
                continue
            if os.path.splitext(base)[1].lower() not in ('.jpg', '.jpeg', '.png', '.exr'):
                continue
            out = os.path.join(dest, base)
            with z.open(info) as src, open(out, 'wb') as dst:
                shutil.copyfileobj(src, dst)
            names.append(base)
    return names


def reuse(src, dest):
    if os.path.exists(src) and not os.path.exists(dest):
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copy2(src, dest)
        return True
    return False


def fetch_ph_texture(e, force):
    pid = e['model_or_asset_id']
    res = '2k' if '2k' in e['variants'] else '1k'
    d = os.path.join(RAW, e['id'])
    files = api(f'https://api.polyhaven.com/files/{pid}')
    got = []
    for mapname, byres in files.items():
        if mapname not in PH_TEX_MAPS or res not in byres:
            continue
        jpg = byres[res].get('jpg') or byres[res].get('png')
        if not jpg:
            continue
        dest = os.path.join(d, os.path.basename(jpg['url']))
        reuse(os.path.join(RESEARCH, 'textures', 'polyhaven', pid, os.path.basename(jpg['url'])), dest)
        st = curl(jpg['url'], dest, jpg.get('size'), jpg.get('md5'), force)
        got.append({'map': mapname, 'res': res, 'url': jpg['url'], 'md5': jpg.get('md5'), 'status': st})
    return got


def fetch_ph_model(e, force):
    pid = e['model_or_asset_id']
    d = os.path.join(RAW, e['id'], 'gltf_1k')
    files = api(f'https://api.polyhaven.com/files/{pid}')
    x = files['gltf']['1k']['gltf']
    got = []
    dest = os.path.join(d, os.path.basename(x['url']))
    reuse(os.path.join(RESEARCH, 'models', 'polyhaven', pid, 'gltf_1k', os.path.basename(x['url'])), dest)
    got.append({'url': x['url'], 'status': curl(x['url'], dest, x.get('size'), x.get('md5'), force)})
    for rel, inc in x.get('include', {}).items():
        dest = os.path.join(d, rel)
        reuse(os.path.join(RESEARCH, 'models', 'polyhaven', pid, 'gltf_1k', rel), dest)
        got.append({'url': inc['url'], 'status': curl(inc['url'], dest, inc.get('size'), inc.get('md5'), force)})
    for skin in PH_MODEL_SKINS.get(pid, []):
        for mp in ('diff', 'nor_gl', 'arm', 'roughness', 'metallic'):
            k = f'{skin}_{mp}'
            if k in files and '1k' in files[k]:
                j = files[k]['1k'].get('jpg') or files[k]['1k'].get('png')
                dest = os.path.join(RAW, e['id'], 'skins', os.path.basename(j['url']))
                got.append({'url': j['url'], 'status': curl(j['url'], dest, j.get('size'), j.get('md5'), force)})
    # metadata (dimensions, authors) for provenance
    try:
        info = api(f'https://api.polyhaven.com/info/{pid}')
        json.dump(info, open(os.path.join(RAW, e['id'], 'ph_info.json'), 'w'), indent=1)
    except Exception as ex:  # noqa
        print('  info failed', ex)
    return got


def fetch_ph_hdri(e, force):
    pid = e['model_or_asset_id']
    d = os.path.join(RAW, e['id'])
    files = api(f'https://api.polyhaven.com/files/{pid}')
    h = files['hdri']['2k']['hdr']
    got = [{'url': h['url'], 'status': curl(h['url'], os.path.join(d, os.path.basename(h['url'])), h.get('size'), h.get('md5'), force)}]
    tm = files.get('tonemapped')
    if tm:
        got.append({'url': tm['url'], 'status': curl(tm['url'], os.path.join(d, pid + '_tonemapped.jpg'), tm.get('size'), tm.get('md5'), force)})
    try:
        info = api(f'https://api.polyhaven.com/info/{pid}')
        json.dump(info, open(os.path.join(d, 'ph_info.json'), 'w'), indent=1)
    except Exception as ex:  # noqa
        print('  info failed', ex)
    return got


def fetch_acg(e, force):
    ids = [s.strip() for s in e['model_or_asset_id'].split('/')]
    res = '2K' if '2k' in e['variants'] else '1K'
    d = os.path.join(RAW, e['id'])
    got = []
    for aid in ids:
        sub = os.path.join(d, aid) if len(ids) > 1 else d
        zname = f'{aid}_{res}-JPG.zip'
        zdest = os.path.join(sub, '_zip', zname)
        reuse(os.path.join(RESEARCH, 'textures', 'ambientcg', aid, zname), zdest)
        st = curl(f'https://ambientcg.com/get?file={zname}', zdest, None, None, force)
        names = safe_extract(zdest, sub)
        got.append({'url': f'https://ambientcg.com/get?file={zname}', 'status': st, 'extracted': names})
        if aid == 'Terrain003':   # float heightmap: avoids 8-bit terracing
            ex = f'{aid}_{res}.exr'
            got.append({'url': f'https://ambientcg.com/get?file={ex}',
                        'status': curl(f'https://ambientcg.com/get?file={ex}', os.path.join(sub, ex), None, None, force)})
    return got


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--list', action='store_true')
    ap.add_argument('--ids')
    ap.add_argument('--priority')
    ap.add_argument('--category')
    ap.add_argument('--force', action='store_true')
    a = ap.parse_args()
    wl = worklist()
    if a.ids:
        want = set(a.ids.split(','))
        wl = [e for e in wl if e['id'] in want]
    if a.priority:
        wl = [e for e in wl if e['priority'] in a.priority.split(',')]
    if a.category:
        wl = [e for e in wl if e['category'] in a.category.split(',')]
    if a.list:
        for e in wl:
            print(e['priority'], e['category'], e['source'], e['id'], e['model_or_asset_id'], e['variants'])
        print(len(wl), 'entries')
        return
    for e in wl:
        t0 = time.time()
        try:
            if e['source'] == 'ambientcg':
                got = fetch_acg(e, a.force)
            elif e['category'] == 'hdri':
                got = fetch_ph_hdri(e, a.force)
            elif e['category'] == 'texture':
                got = fetch_ph_texture(e, a.force)
            else:
                got = fetch_ph_model(e, a.force)
        except Exception as ex:
            print('FAIL', e['id'], ex)
            continue
        prov = {'manifest_id': e['id'], 'source': e['source'], 'asset_id': e['model_or_asset_id'],
                'license': 'CC0 1.0', 'fetched': time.strftime('%Y-%m-%dT%H:%M:%S'),
                'wall_s': round(time.time() - t0, 1), 'list_price_usd': 0.0, 'files': got,
                'tool': 'Tools/cc0/wp10_fetch.py'}
        os.makedirs(os.path.join(RAW, e['id']), exist_ok=True)
        json.dump(prov, open(os.path.join(RAW, e['id'], 'provenance.json'), 'w'), indent=1)
        n_ok = sum(1 for g in got if g.get('status') == 'ok')
        print(f"{e['id']}: {len(got)} files ({n_ok} new) {time.time() - t0:.1f}s", flush=True)


if __name__ == '__main__':
    main()
