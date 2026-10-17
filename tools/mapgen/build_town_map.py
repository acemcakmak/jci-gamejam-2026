#!/usr/bin/env python3
"""Builds the Kömür town map.

Writes:
  Assets/_Game/Map/town-map.json   - the map (terrain rows, stairs, props, villagers, holes). Read by
                                     Kömür > Build Town Scene in Unity and by docs/ui-design/map-spec.html.
  docs/ui-design/cainos-catalog.json - sprite rectangles/pivots of the Cainos tiles and prefabs the map
                                     uses, so the design page can draw them (numbers only, no art).

Run from the repo root:  python3 tools/mapgen/build_town_map.py
"""
import json
import os
import random
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
CAINOS = os.path.join(ROOT, 'Assets/ThirdPartyAssets/Cainos/Pixel Art Top Down - Basic')
W, H = 56, 40

# ---------------------------------------------------------------------------
# Terrain legend (one char per tile, rows top to bottom):
#   .  grass            ,  flowery grass      =  cobbled path       #  stone plaza
#   ^  raised ground (upper floor, walkable via stairs)
#   B  building / walled block (raised, no stairs - cover and decoration)
#   s  sunken ground (graveyard pit)          r  ramp: sunken cell with no wall (entrance)
# The outer town wall is added automatically around the border.
# ---------------------------------------------------------------------------
grid = [['.'] * W for _ in range(H)]


def fill(x0, y0, x1, y1, ch):
    """Fill inclusive rect in map coords (y up)."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < W and 0 <= y < H:
                grid[y][x] = ch


def at(x, y):
    return grid[y][x] if 0 <= x < W and 0 <= y < H else 'X'


rng = random.Random(1717)

# --- Flowery meadows sprinkled everywhere first
for y in range(H):
    for x in range(W):
        if rng.random() < 0.10:
            grid[y][x] = ','

# --- Central square (irregular: main plaza + steps out to the roads)
fill(23, 15, 32, 23, '#')
fill(22, 17, 22, 21, '#')
fill(33, 17, 33, 21, '#')
fill(25, 24, 30, 24, '#')
fill(25, 14, 30, 14, '#')

# --- Roads (3 wide, with bends so quarters don't feel like boxes)
fill(26, 25, 28, 30, '=')       # north road ...
fill(28, 31, 30, 37, '=')       # ... bends east toward the market
fill(26, 31, 28, 31, '=')
fill(34, 18, 42, 20, '=')       # east road into the market
fill(40, 21, 42, 24, '=')
fill(2, 18, 21, 20, '=')        # west road to the farmstead
fill(12, 9, 14, 17, '=')        # farm lane south
fill(26, 2, 28, 13, '=')        # south road ...
fill(29, 9, 33, 11, '=')        # ... and the path into the graveyard

# --- NW: Chapel Hill (raised, with an eastern spur and a southern bulge)
fill(3, 27, 19, 36, '^')
fill(20, 31, 23, 35, '^')
fill(8, 25, 13, 26, '^')       # bulge carrying the main stairs
# --- NE: Market quarter (plaza, merchant terrace, shop blocks)
fill(34, 24, 47, 33, '#')
fill(36, 34, 43, 35, '#')
fill(44, 31, 52, 37, '^')       # merchant terrace
fill(48, 30, 52, 30, '^')
fill(35, 36, 38, 38, 'B')       # shop
fill(49, 24, 52, 27, 'B')       # warehouse
fill(31, 32, 33, 34, 'B')       # gatehouse by the north road

# --- SW: Farmstead (barn, cottage, walled garden; rows of flowers as crops)
fill(4, 4, 9, 8, 'B')           # barn
fill(16, 12, 19, 14, 'B')       # cottage
fill(3, 12, 7, 14, 'B')         # walled garden
for y in (3, 5, 7):             # crop rows
    fill(16, y, 22, y, ',')

# --- SE: Sunken graveyard (pit with a northern extension, ramps west and north)
fill(34, 3, 51, 12, 's')
fill(40, 13, 46, 14, 's')
fill(34, 9, 34, 10, 'r')        # west ramp (from the graveyard path)
fill(42, 14, 43, 14, 'r')       # north ramp (towards the market)
fill(46, 6, 49, 8, 'B')         # mausoleum inside the pit

# Stairs: (x, region) on the south cap of a raised region. Pivot = (x, capRow + 1).
stairs = [
    {'x': 10, 'capY': 25},      # chapel hill main stairs (on the bulge)
    {'x': 17, 'capY': 27},      # chapel hill side stairs
    {'x': 47, 'capY': 31},      # merchant terrace
]

# ---------------------------------------------------------------------------
# Objects (world coords: x+0.5 is a cell centre). floor 2 = on a raised region.
# ---------------------------------------------------------------------------
props = []
taken = set()


def free(x, y, r=1):
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if (x + dx, y + dy) in taken:
                return False
    return True


def put(prefab, x, y, floor=1, mark=1):
    props.append({'p': prefab, 'x': round(x + 0.5, 3), 'y': round(y + 0.2, 3), 'f': floor})
    for dy in range(-mark, mark + 1):
        for dx in range(-mark, mark + 1):
            taken.add((x + dx, y + dy))


def raised(x, y):
    return at(x, y) in '^B'


def ground_ok(x, y):
    """Floor-1 spot: grass, not near raised edges (tall sprites would clip under the upper layer)."""
    if at(x, y) not in '.,s':
        return False
    for dy in range(-1, 5):          # nothing tall just south of a cliff
        for dx in range(-1, 2):
            if raised(x + dx, y + dy):
                return False
    return 1 < x < W - 2 and 1 < y < H - 2


# Gameplay markers
komur = [27.5, 18.5]
apothecary = [28.5, 21.5]
villagers = [
    {'x': 9.5, 'y': 33.5, 'f': 2}, {'x': 17.5, 'y': 30.5, 'f': 2},   # Chapel Hill
    {'x': 38.5, 'y': 28.5, 'f': 1}, {'x': 45.5, 'y': 26.5, 'f': 1},  # Market
    {'x': 9.5, 'y': 11.5, 'f': 1}, {'x': 20.5, 'y': 5.5, 'f': 1},    # Farmstead
    {'x': 39.5, 'y': 7.5, 'f': 1},                                   # Graveyard
    {'x': 24.5, 'y': 20.5, 'f': 1},                                  # Square
]
holes = [
    {'x': 2.5, 'y': 10.5, 'f': 1, 'wave': 1},    # SW farmstead
    {'x': 53.5, 'y': 22.5, 'f': 1, 'wave': 2},   # NE market
    {'x': 50.5, 'y': 4.5, 'f': 1, 'wave': 3},    # SE graveyard (in the pit)
    {'x': 5.5, 'y': 35.5, 'f': 2, 'wave': 4},    # NW chapel hill
]
for m in [komur, apothecary] + [[v['x'], v['y']] for v in villagers] + [[h['x'], h['y']] for h in holes]:
    for dy in range(-1, 2):
        for dx in range(-1, 2):
            taken.add((int(m[0]) + dx, int(m[1]) + dy))

P, PL = 'Props/PF Props - ', 'Plant/PF Plant - '

# Square: well, pillars at the corners, a statue facing the apothecary
put(P + 'Well 01', 31, 22)
put(P + 'Statue 01', 24, 23)
for x, y in [(22, 22), (33, 22), (22, 16), (33, 16)]:
    put(P + 'Pillar 0' + str(1 + (x + y) % 2), x, y, mark=0)
put(P + 'Road Sign 01 E', 34, 21, mark=0)
put(P + 'Road Sign 01 W', 21, 21, mark=0)

# Chapel Hill: ruined nave (two rows of pillars), altar and statue, graves behind the church
for x in (7, 15):
    for y in (29, 31, 33):
        put(P + 'Pillar 0' + str(1 + y % 2), x, y, 2, mark=0)
put(P + 'Altar 01', 11, 34, 2)
put(P + 'Statue 01', 11, 35, 2, mark=0)
put(P + 'Rune Pillar X3', 21, 33, 2)
put(P + 'Rune Pillar Broken', 4, 28, 2, mark=0)
for x, y in [(16, 35), (18, 35), (16, 33), (18, 33)]:
    put(P + 'Gravestone 0' + str(1 + x % 3), x, y, 2, mark=0)
for x, y in [(4, 31), (5, 34), (13, 28), (22, 32)]:
    put(PL + 'Bush 0' + str(1 + (x * y) % 6), x, y, 2, mark=0)

# Market: stall rows, goods on the terrace and on the shop roofs
for i, x in enumerate(range(35, 45, 3)):
    put(P + 'Crate 0' + str(1 + i % 2), x, 30, mark=0)
    put(P + 'Barrel 01', x + 1, 30, mark=0)
    put(P + 'Pot 0' + str(1 + i % 3), x, 26, mark=0)
put(P + 'Chest 01', 37, 33)
put(P + 'Well 01', 41, 28)
for x, y in [(45, 35), (47, 35), (50, 34), (51, 36), (49, 32)]:
    put(P + ('Crate 01' if (x + y) % 2 else 'Barrel 01'), x, y, 2, mark=0)
put(P + 'Pot 02', 36, 37, 2, mark=0)
put(P + 'Crate 02', 50, 26, 2, mark=0)

# Farmstead: supplies by the barn, hay (bushes) on the garden wall, scarecrow sign
for x, y in [(10, 5), (11, 6), (10, 9)]:
    put(P + 'Crate 0' + str(1 + x % 2), x, y, mark=0)
put(P + 'Barrel 01', 3, 9, mark=0)
for x, y in [(5, 13), (6, 13), (17, 13)]:
    put(PL + 'Bush 0' + str(1 + (x + y) % 6), x, y, 2, mark=0)
put(P + 'Pot 01', 7, 7, 2, mark=0)
put(P + 'Road Sign 01 W', 11, 16, mark=0)

# Graveyard: rows of graves in the pit, statue and pillars, mausoleum top
for y in (4, 6, 8, 10):
    for x in range(36, 46, 2):
        if (x, y) in [(38, 8), (40, 8)]:
            continue
        put(P + 'Gravestone 0' + str(1 + (x + y) % 3), x, y, mark=0)
put(P + 'Statue 01', 47, 10, mark=0)
put(P + 'Rune Pillar X2', 47, 7, 2, mark=0)
put(P + 'Rune Pillar Broken', 36, 12, mark=0)
put(P + 'Rune Pillar Broken', 50, 11, mark=0)

# Trees: around the outside of the town and between quarters, never over roads or plazas
tree_spots = [(2, 37), (2, 24), (2, 15), (2, 3), (11, 2), (20, 2), (23, 6), (33, 15), (21, 25), (24, 34),
              (33, 37), (53, 37), (53, 29), (53, 15), (53, 8), (36, 16), (46, 16), (18, 9), (31, 2), (6, 22),
              (16, 22), (45, 22), (24, 11), (31, 29)]
for x, y in tree_spots:
    if ground_ok(x, y) and free(x, y, 1):
        put(PL + 'Tree 0' + str(1 + (x * 7 + y) % 3), x, y, mark=1)

# Bushes, rocks, flowers and grass tufts to make the ground feel alive (low, never blocks the view)
for _ in range(2000):
    if len(props) > 260:
        break
    x, y = rng.randrange(2, W - 2), rng.randrange(2, H - 2)
    if not free(x, y, 0):
        continue
    ch = at(x, y)
    if ch in '.,s' and ground_ok(x, y):
        roll = rng.random()
        if roll < 0.12:
            name = PL + 'Bush 0' + str(rng.randrange(1, 7))
        elif roll < 0.2:
            name = P + 'Stone 0' + str(rng.randrange(1, 6))
        elif roll < 0.3:
            name = PL + 'Flower 01'
        else:
            name = PL + 'Grass %02d' % rng.randrange(1, 16)
        put(name, x, y, mark=0)
    elif ch == '^' and at(x, y + 1) == '^' and at(x, y - 1) == '^' and rng.random() < 0.5:
        put(PL + 'Grass %02d' % rng.randrange(1, 16), x, y, 2, mark=0)

# ---------------------------------------------------------------------------
# Write the map
# ---------------------------------------------------------------------------
rows = [''.join(grid[y]) for y in range(H - 1, -1, -1)]   # top row first
town = {
    'note': 'Generated by tools/mapgen/build_town_map.py - edit the script, not this file. Rows are top to bottom; '
            'x right, y up; one char per tile (legend in the script).',
    'width': W, 'height': H,
    'rows': rows,
    'stairs': stairs,
    'komur': komur, 'apothecary': apothecary,
    'villagers': villagers, 'holes': holes,
    'props': props,
}
os.makedirs(os.path.join(ROOT, 'Assets/_Game/Map'), exist_ok=True)
with open(os.path.join(ROOT, 'Assets/_Game/Map/town-map.json'), 'w') as f:
    json.dump(town, f, indent=1)

# ---------------------------------------------------------------------------
# Sprite catalog for the design page (rects/pivots read from the Cainos .meta files)
# ---------------------------------------------------------------------------
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


catalog = {'tiles': {}, 'prefabs': {}}
for n in range(61):
    catalog['tiles']['wall_%d' % n] = tile_ref('TP Wall', 'TX Tileset Wall_%d' % n)
for n in range(1, 5):
    catalog['tiles']['shadow_%d' % n] = tile_ref('TP Wall', 'TX Shadow Tileset Wall %d' % n)
for n in range(16):
    catalog['tiles']['grass_%d' % n] = tile_ref('TP Grass', 'TX Tileset Grass %d' % n)
    catalog['tiles']['flower_%d' % n] = tile_ref('TP Grass', 'TX Tileset Grass Flower %d' % n)
for n in range(8):
    catalog['tiles']['pavement_%d' % n] = tile_ref('TP Grass', 'TX Tileset Grass Pavement %d' % n)
for n in range(50):
    catalog['tiles']['stone_%d' % n] = tile_ref('TP Stone Ground', 'TX Tileset Stone Ground_%d' % n)
for name in sorted({p['p'] for p in props} | {'Props/PF Struct - Stairs S 01 M'}):
    catalog['prefabs'][name] = prefab_parts(name)
catalog['tiles'] = {k: v for k, v in catalog['tiles'].items() if v}
with open(os.path.join(ROOT, 'docs/ui-design/cainos-catalog.json'), 'w') as f:
    json.dump(catalog, f, indent=0)

print('map %dx%d, %d props, %d villagers, %d holes' % (W, H, len(props), len(villagers), len(holes)))
for r in rows:
    print(r)
