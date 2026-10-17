"""Reads Cainos sprite/tile/prefab data from the Unity .meta and .prefab files (numbers only, no art)."""
import glob
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CAINOS = os.path.join(ROOT, 'Assets/ThirdPartyAssets/Cainos/Pixel Art Top Down - Basic')

ALIGN = {0: (0.5, 0.5), 1: (0, 1), 2: (0.5, 1), 3: (1, 1), 4: (0, 0.5), 5: (1, 0.5), 6: (0, 0), 7: (0.5, 0), 8: (1, 0)}
guid_to_png = {}
for dirpath, _, files in os.walk(os.path.join(CAINOS, 'Texture')):
    for fn in files:
        if fn.endswith('.png.meta'):
            g = re.search(r'guid: (\w+)', open(os.path.join(dirpath, fn)).read()).group(1)
            guid_to_png[g] = os.path.join(dirpath, fn[:-5])

sheet_cache = {}


def sheet(png):
    """Sprites of a texture keyed by internalID (what prefabs and tiles reference as fileID)."""
    if png in sheet_cache:
        return sheet_cache[png]
    meta = open(png + '.meta').read()
    by_id = {}
    for block in re.split(r'\n\s+- serializedVersion: 2\n(?=\s+name: )', meta)[1:]:
        m = re.search(r'name: (.+)\n\s+rect:\n\s+serializedVersion: 2\n\s+x: (-?[\d.]+)\n\s+y: (-?[\d.]+)\n\s+width: ([\d.]+)\n\s+height: ([\d.]+)\n\s+alignment: (\d+)\n\s+pivot: \{x: (-?[\d.]+), y: (-?[\d.]+)\}', block)
        iid = re.search(r'internalID: (-?\d+)', block)
        if not m or not iid:
            continue
        n, x, y, w, h, al, px, py = m.groups()
        piv = ALIGN.get(int(al), (float(px), float(py)))
        by_id[iid.group(1)] = {'name': n.strip(), 'x': float(x), 'y': float(y), 'w': float(w), 'h': float(h), 'px': piv[0], 'py': piv[1]}
    ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', meta).group(1))
    sheet_cache[png] = (by_id, ppu)
    return sheet_cache[png]


def sprite_ref(fileid, guid):
    png = guid_to_png.get(guid)
    if not png:
        return None
    by_id, ppu = sheet(png)
    if fileid not in by_id:
        return None
    s = dict(by_id[fileid])
    s['sheet'] = os.path.relpath(png, ROOT)
    s['ppu'] = ppu
    return s


def prefab_parts(rel):
    path = os.path.join(CAINOS, 'Prefab', rel + '.prefab')
    text = open(path).read()
    docs = re.split(r'--- !u!', text)[1:]
    objs, transforms, renderers = {}, {}, []
    for d in docs:
        head, body = d.split('\n', 1)
        cls, fid = head.split()[0], head.split('&')[1].split()[0]
        if cls == '1':
            objs[fid] = re.search(r'm_Name: (.*)', body).group(1)
        elif cls == '4':
            go = re.search(r'm_GameObject: \{fileID: (\d+)', body).group(1)
            lp = re.search(r'm_LocalPosition: \{x: (-?[\d.e-]+), y: (-?[\d.e-]+)', body)
            father = re.search(r'm_Father: \{fileID: (\d+)', body).group(1)
            transforms[go] = (fid, float(lp.group(1)), float(lp.group(2)), father)
        elif cls == '212':
            go = re.search(r'm_GameObject: \{fileID: (\d+)', body).group(1)
            sp = re.search(r'm_Sprite: \{fileID: (-?\d+), guid: (\w+)', body)
            order = int(re.search(r'm_SortingOrder: (-?\d+)', body).group(1))
            col = re.search(r'm_Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}', body)
            if sp:
                renderers.append((go, sp.group(1), sp.group(2), order, float(col.group(4)) if col else 1.0))
    by_tf = {v[0]: (k, v) for k, v in transforms.items()}
    parts = []
    for go, fileid, guid, order, alpha in renderers:
        ref = sprite_ref(fileid, guid)
        if ref is None:
            continue
        # world offset relative to the prefab root: sum local positions up to (not including) the root
        x = y = 0.0
        cur = go
        while cur in transforms:
            tf, lx, ly, father = transforms[cur]
            if father == '0':
                break
            x += lx
            y += ly
            cur = by_tf.get(father, (None,))[0]
        ref.update({'ox': round(x, 4), 'oy': round(y, 4), 'order': order, 'alpha': alpha, 'part': objs.get(go, '')})
        parts.append(ref)
    return parts


def tile_ref(palette, name):
    path = os.path.join(CAINOS, 'Tile Palette', palette, name + '.asset')
    text = open(path).read()
    m = re.search(r'm_Sprite: \{fileID: (-?\d+), guid: (\w+)', text)
    return sprite_ref(m.group(1), m.group(2))



PALETTES = ['TP Wall', 'TP Grass', 'TP Stone Ground']


def tile_key(palette, asset_name):
    """Short key used by the design pages: wall_24, grass_3, flower_7, pavement_2, stone_18, shadow_1, or the asset name."""
    m = re.match(r'TX Tileset Wall_(\d+)$', asset_name)
    if m: return 'wall_' + m.group(1)
    m = re.match(r'TX Shadow Tileset Wall (\d+)$', asset_name)
    if m: return 'shadow_' + m.group(1)
    m = re.match(r'TX Tileset Grass Flower (\d+)$', asset_name)
    if m: return 'flower_' + m.group(1)
    m = re.match(r'TX Tileset Grass Pavement (\d+)$', asset_name)
    if m: return 'pavement_' + m.group(1)
    m = re.match(r'TX Tileset Grass (\d+)$', asset_name)
    if m: return 'grass_' + m.group(1)
    m = re.match(r'TX Tileset Stone Ground_(\d+)$', asset_name)
    if m: return 'stone_' + m.group(1)
    return asset_name


def all_tiles():
    """{key: sprite} for every tile asset in the Cainos palettes, plus {tile guid: key}."""
    tiles, by_guid = {}, {}
    for pal in PALETTES:
        for path in glob.glob(os.path.join(CAINOS, 'Tile Palette', pal, '*.asset')):
            name = os.path.basename(path)[:-6]
            ref = tile_ref(pal, name)
            if not ref:
                continue
            key = tile_key(pal, name)
            ref['asset'] = name
            tiles[key] = ref
            g = re.search(r'guid: (\w+)', open(path + '.meta').read()).group(1)
            by_guid[g] = key
    return tiles, by_guid


def all_prefabs():
    out = {}
    for path in glob.glob(os.path.join(CAINOS, 'Prefab', '*', '*.prefab')):
        rel = os.path.relpath(path, os.path.join(CAINOS, 'Prefab'))[:-7]
        out[rel] = prefab_parts(rel)
    return out


def write_catalog(path):
    tiles, _ = all_tiles()
    catalog = {'tiles': tiles, 'prefabs': all_prefabs()}
    with open(path, 'w') as f:
        json.dump(catalog, f, indent=0)
    return catalog
