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
#   (space) outside the town - nothing is painted; the town wall follows this outline
#   .  grass            ,  flowery grass      =  cobbled path       #  stone plaza
#   ^  raised ground (upper floor, walkable via stairs)
#   B  building / walled block (raised, no stairs - cover and decoration)
#   s  sunken ground (graveyard pit)          r  ramp: sunken cell with no wall (entrance)
# The town wall takes the outermost ring of town cells: 1 row on top, 1 column on the sides, and
# 2 rows at the bottom (cap + brick face). Keep content 2 cells away from the outline.
# ---------------------------------------------------------------------------
grid = [['.'] * W for _ in range(H)]


def fill(x0, y0, x1, y1, ch):
    """Fill inclusive rect in map coords (y up)."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < W and 0 <= y < H:
                grid[y][x] = ch


def at(x, y):
    return grid[y][x] if 0 <= x < W and 0 <= y < H else ' '


rng = random.Random(1717)
CX, CY = W // 2, H // 2          # map centre: the apothecary altar

# --- Flowery meadows sprinkled everywhere first
for y in range(H):
    for x in range(W):
        if rng.random() < 0.10:
            grid[y][x] = ','

# --- Irregular town outline: bites out of the corners and edges
for x0, y0, x1, y1 in [
    (0, 35, 6, 39), (0, 30, 2, 34),          # NW corner, stepped
    (49, 36, 55, 39),                        # NE corner
    (51, 0, 55, 5), (53, 6, 55, 9),          # SE corner, stepped
    (0, 0, 4, 3),                            # SW corner
    (22, 37, 25, 39),                        # north bay between hill and market
    (19, 0, 24, 2),                          # south bay
    (0, 8, 1, 13),                           # west notch
    (54, 14, 55, 20),                        # east notch
]:
    fill(x0, y0, x1, y1, ' ')

# --- Centre: town square around the apothecary altar (octagon-ish plaza with road stubs)
fill(23, 15, 33, 25, '#')
for x, y in [(23, 15), (33, 15), (23, 25), (33, 25)]:
    grid[y][x] = '.'
fill(22, 18, 22, 22, '#')
fill(34, 18, 34, 22, '#')

# --- Roads (3 wide, bending between quarters)
fill(5, 19, 21, 21, '=')          # west road to the farmstead
fill(10, 8, 12, 18, '=')          # farm lane
fill(27, 26, 29, 29, '=')         # north road ...
fill(16, 28, 26, 30, '=')         # ... turning west to the chapel stairs
fill(35, 19, 37, 21, '=')         # east road ...
fill(36, 22, 38, 23, '=')         # ... turning north into the market
fill(27, 3, 29, 14, '=')          # south road
fill(30, 8, 35, 10, '=')          # graveyard path to the west ramp

# --- NW: Chapel Hill (raised): main body, west lobe, south bulge with the main stairs
fill(7, 31, 21, 36, '^')
fill(5, 31, 6, 34, '^')
fill(12, 31, 18, 31, '^')
fill(12, 29, 18, 30, '^')         # south bulge (church entrance)
# --- NE: Market quarter
fill(34, 24, 46, 33, '#')
fill(43, 29, 49, 34, '^')         # merchant terrace
fill(34, 34, 37, 36, 'B')         # shop
fill(50, 24, 53, 27, 'B')         # warehouse
fill(31, 31, 33, 33, 'B')         # gatehouse by the north road
# --- SW: Farmstead
fill(5, 4, 10, 8, 'B')            # barn
fill(15, 12, 18, 14, 'B')         # cottage
fill(4, 12, 7, 14, 'B')           # walled garden
for y in (4, 6, 8):               # crop rows
    fill(13, y, 18, y, ',')
# --- SE: Sunken graveyard
fill(36, 4, 49, 13, 's')
fill(41, 14, 46, 15, 's')
fill(36, 8, 36, 9, 'r')           # west ramp
fill(43, 15, 44, 15, 'r')         # north ramp (under the archway)
fill(45, 6, 48, 8, 'B')           # mausoleum

# Stairs on the south cap of raised ground. Pivot = (x, capY + 1).
stairs = [
    {'x': 15, 'capY': 29},        # chapel: main stairs into the church
    {'x': 9, 'capY': 31},         # chapel: west stairs
    {'x': 46, 'capY': 29},        # merchant terrace
]

# ---------------------------------------------------------------------------
# Objects (world coords). floor 2 = on raised ground.
# ---------------------------------------------------------------------------
props = []
taken = set()


def room(x, y):
    return at(x, y) != ' '


def free(x, y, r=1):
    return all((x + dx, y + dy) not in taken for dy in range(-r, r + 1) for dx in range(-r, r + 1))


def put(prefab, x, y, floor=1, mark=1, exact=False):
    px, py = (x, y) if exact else (x + 0.5, y + 0.2)
    props.append({'p': prefab, 'x': round(px, 3), 'y': round(py, 3), 'f': floor})
    for dy in range(-mark, mark + 1):
        for dx in range(-mark, mark + 1):
            taken.add((int(x) + dx, int(y) + dy))


def raised(x, y):
    return at(x, y) in '^B'


def near_outline(x, y, r=2):
    return any(not room(x + dx, y + dy) for dy in range(-r, r + 1) for dx in range(-r, r + 1))


def ground_ok(x, y):
    """Floor-1 spot for tall things: grass, off the town wall, not just south of a cliff."""
    if at(x, y) not in '.,s' or near_outline(x, y, 2):
        return False
    return not any(raised(x + dx, y + dy) for dy in range(-1, 5) for dx in range(-1, 2))


# Gameplay markers
apothecary = [float(CX), float(CY)]          # the altar, exactly at the map centre
komur = [CX + 0.5, CY - 2.5]
villagers = [
    {'x': 10.5, 'y': 34.5, 'f': 2}, {'x': 19.5, 'y': 32.5, 'f': 2},   # Chapel Hill
    {'x': 38.5, 'y': 30.5, 'f': 1}, {'x': 42.5, 'y': 26.5, 'f': 1},  # Market
    {'x': 14.5, 'y': 10.5, 'f': 1}, {'x': 7.5, 'y': 16.5, 'f': 1},   # Farmstead
    {'x': 42.5, 'y': 10.5, 'f': 1},                                  # Graveyard
    {'x': 31.5, 'y': 23.5, 'f': 1},                                  # Square
]
holes = [
    {'x': 3.5, 'y': 16.5, 'f': 1, 'wave': 1},    # SW farmstead
    {'x': 52.5, 'y': 30.5, 'f': 1, 'wave': 2},   # NE market
    {'x': 39.5, 'y': 5.5, 'f': 1, 'wave': 3},    # SE graveyard (in the pit)
    {'x': 8.5, 'y': 35.5, 'f': 2, 'wave': 4},    # NW chapel hill
]
for m in [komur, apothecary] + [[v['x'], v['y']] for v in villagers] + [[h['x'], h['y']] for h in holes]:
    for dy in range(-1, 2):
        for dx in range(-1, 2):
            taken.add((int(m[0]) + dx, int(m[1]) + dy))

P, PL, ST = 'Props/PF Props - ', 'Plant/PF Plant - ', 'Props/PF Struct - '

# --- Square: the altar in the middle, a ring of lanterns, benches facing it, pillars on the plaza corners
put(P + 'Stone Bench 01 S', CX, CY + 3, exact=True)
put(P + 'Stone Bench 01 E', CX - 3.4, CY, exact=True)
put(P + 'Stone Bench 01 W', CX + 3.4, CY, exact=True)
for dx, dy in [(-3, 3), (3, 3), (-3, -3), (3, -3)]:
    put(P + 'Stone Lantern 01', CX + dx, CY + dy, mark=0, exact=True)
for x, y in [(24, 24), (32, 24), (24, 16), (32, 16)]:
    put(P + 'Pillar 0' + str(1 + (x + y) % 2), x, y, mark=0)
put(P + 'Road Sign 01 W', 21, 22, mark=0)
put(P + 'Road Sign 01 E', 35, 22, mark=0)

# --- Chapel Hill: a ruined church - pews in the nave, coffin before the statue, archway at the entrance
put(ST + 'Gate 01', 15.0, 31.25, 2, exact=True)                 # church entrance, top of the main stairs
for y in (32.5, 33.7):
    for x in (12.6, 17.4):
        put(P + 'Stone Bench 01 S', x, y, 2, mark=0, exact=True)  # pews
put(P + 'Statue 01', 15.0, 35.6, 2, exact=True)
put(P + 'Stone Coffin 01 H', 15.0, 34.8, 2, mark=0, exact=True)
for x in (11.5, 18.5):
    put(P + 'Rune Pillar X3', x, 35.4, 2, mark=0, exact=True)
for y in (32, 34):
    put(P + 'Pillar 01', 10.5, y + 0.2, 2, mark=0, exact=True)
    put(P + 'Pillar 02', 19.5, y + 0.2, 2, mark=0, exact=True)
for x, y in [(11, 30), (19, 30)]:
    put(P + 'Stone Lantern 01', x + 0.5, y + 0.3, 2, mark=0, exact=True)
for x, y in [(6, 33), (7, 31), (20, 35), (21, 33)]:                # churchyard graves
    put(P + 'Gravestone 0' + str(1 + (x + y) % 3), x, y, 2, mark=0)
for x, y in [(8, 33), (13, 36), (17, 36), (21, 31)]:
    put(PL + 'Bush 0' + str(1 + (x * y) % 6), x, y, 2, mark=0)

# --- Market: stall rows (crate, barrel, pot), well, benches, lanterns, goods on the terrace and roofs
for i, x in enumerate(range(35, 42, 3)):
    put(P + 'Crate 0' + str(1 + i % 2), x, 28, mark=0)
    put(P + 'Barrel 01', x + 1, 28, mark=0)
    put(P + 'Pot 0' + str(1 + i % 3), x, 25, mark=0)
    put(P + 'Pot 0' + str(1 + (i + 1) % 3), x + 1, 25, mark=0)
put(P + 'Well 01', 39, 32)
put(P + 'Chest 01', 35, 32, mark=0)
put(P + 'Stone Bench 01 S', 41.5, 32.2, mark=0, exact=True)
put(P + 'Stone Lantern 01', 34.5, 30.2, mark=0, exact=True)
put(P + 'Stone Lantern 01', 42.5, 24.4, mark=0, exact=True)
put(ST + 'Gate 01', 37.0, 22.1, exact=True)                       # market archway on the east road
for x, y in [(44, 33), (45, 33), (48, 33), (48, 31), (44, 31)]:
    put(P + ('Crate 01' if (x + y) % 2 else 'Barrel 01'), x, y, 2, mark=0)
put(P + 'Stone Lantern 01', 46.5, 32.2, 2, mark=0, exact=True)
put(P + 'Pot 02', 35, 35, 2, mark=0)
put(P + 'Crate 02', 51, 26, 2, mark=0)
put(P + 'Barrel 01', 52, 25, 2, mark=0)

# --- Farmstead: barn supplies, fenced garden with an open gate, crops with hedges, a bench by the cottage
for x, y in [(11, 6), (11, 5), (9, 10)]:
    put(P + 'Crate 0' + str(1 + x % 2), x, y, mark=0)
put(P + 'Barrel 01', 4, 10, mark=0)
for x in range(3, 9):
    put(P + 'Brick 0' + str(1 + x % 3), x, 15.6, mark=0, exact=True)  # fence line
put(P + 'Wooden Gate 01 Opened', 9.0, 15.4, exact=True)
for x, y in [(5, 13), (6, 13), (16, 13)]:
    put(PL + 'Bush 0' + str(1 + (x + y) % 6), x, y, 2, mark=0)
for y in (4, 6, 8):
    put(PL + 'Bush 0' + str(1 + y % 6), 12, y, mark=0)
    put(PL + 'Bush 0' + str(2 + y % 5), 19, y, mark=0)
put(P + 'Stone Bench 01 S', 16.5, 11.2, mark=0, exact=True)
put(P + 'Pot 01', 7, 7, 2, mark=0)
put(P + 'Stone Cube 01', 13, 13, mark=0)

# --- Graveyard: rows of graves and coffins in the pit, lanterns at the corners, archway over the north ramp
for y in (5, 7, 9, 11):
    for x in range(38, 45, 2):
        if (x, y) in [(42, 11), (40, 5)]:
            continue
        put(P + 'Gravestone 0' + str(1 + (x + y) % 3), x, y, mark=0)
for x, y in [(46, 11), (48, 11)]:
    put(P + 'Stone Coffin 01 V', x, y, mark=0)
put(P + 'Stone Coffin 01 H', 41.0, 4.3, mark=0, exact=True)
for x, y in [(37.5, 12.2), (48.5, 12.2), (37.5, 4.3)]:
    put(P + 'Stone Lantern 01', x, y, mark=0, exact=True)
put(P + 'Statue 01', 46.5, 7.7, 2, mark=0, exact=True)            # on the mausoleum
put(P + 'Rune Pillar Broken', 49.0, 9.2, mark=0, exact=True)
put(ST + 'Gate 01', 44.0, 15.1, exact=True)                       # archway over the north ramp
put(P + 'Stone Bench 01 S', 40.0, 17.2, mark=0, exact=True)       # mourners' bench outside
put(P + 'Stone Bench 01 S', 48.0, 17.2, mark=0, exact=True)

# --- Trees: the town edge and between quarters, never over roads, plazas or right under a cliff
tree_spots = [(4, 27), (3, 22), (3, 6), (13, 2), (17, 3), (26, 5), (31, 13), (21, 25), (23, 32),
              (31, 36), (40, 36), (52, 32), (51, 21), (50, 16), (34, 15), (20, 12), (6, 24), (16, 24),
              (24, 9), (46, 21), (40, 18), (26, 33), (11, 26), (31, 4)]
def canopy_clear(x, y):
    """A tree's crown covers about 3x4 tiles above its trunk: keep that off roads, plazas and the pit."""
    return all(at(x + dx, y + dy) in '.,' for dy in range(0, 4) for dx in range(-1, 2))


for x, y in tree_spots:
    if ground_ok(x, y) and canopy_clear(x, y) and free(x, y, 1):
        put(PL + 'Tree 0' + str(1 + (x * 7 + y) % 3), x, y, mark=1)

# --- Low greenery everywhere else (bushes, rocks, flowers, grass tufts)
for _ in range(3000):
    if len(props) > 300:
        break
    x, y = rng.randrange(2, W - 2), rng.randrange(2, H - 2)
    if not free(x, y, 0) or near_outline(x, y, 1):
        continue
    ch = at(x, y)
    if ch in '.,s' and ground_ok(x, y):
        roll = rng.random()
        if roll < 0.12:
            name = PL + 'Bush 0' + str(rng.randrange(1, 7))
        elif roll < 0.2:
            name = P + 'Stone 0' + str(rng.randrange(1, 8))
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
for name in sorted({p['p'] for p in props} | {'Props/PF Struct - Stairs S 01 M', 'Props/PF Props - Altar 01'}):
    catalog['prefabs'][name] = prefab_parts(name)
catalog['tiles'] = {k: v for k, v in catalog['tiles'].items() if v}
with open(os.path.join(ROOT, 'docs/ui-design/cainos-catalog.json'), 'w') as f:
    json.dump(catalog, f, indent=0)

print('map %dx%d, %d props, %d villagers, %d holes' % (W, H, len(props), len(villagers), len(holes)))
for r in rows:
    print(r)
