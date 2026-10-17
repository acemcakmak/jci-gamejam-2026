#!/usr/bin/env python3
"""Builds the Kömür town map - a hand-designed layout, not random generation.

Writes:
  Assets/_Game/Map/town-map.json      - read by Kömür > Build Town Scene and docs/ui-design/map-spec.html
  docs/ui-design/cainos-catalog.json  - sprite rects for the design pages (numbers only, no art)

Design rules this file follows:
  * every raised area has stairs and a purpose (church hill, merchant house, farmhouse)
  * props are placed as compositions: symmetric around an axis, in rows, flanking entrances
  * no random scatter except sparse grass tufts on open grass
  * the apothecary altar is the exact centre of the map, on a studded slab inside a cobbled ring

Run from the repo root:  python3 tools/mapgen/build_town_map.py
"""
import json
import os
import random
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
W, H = 57, 41
CX, CY = 28, 20                 # centre cell; its centre (28.5, 20.5) is the centre of the map

# Terrain legend (rows top to bottom in the JSON; x right, y up here):
#   (space) outside the town   .  grass   ,  flowery grass   =  cobbles
#   #  stone slab on the ground (9-sliced)     %  stone slab on raised ground (9-sliced, upper floor)
#   ^  raised ground (upper floor, reached by stairs)
#   s  sunken graveyard floor                  r  ramp into the graveyard (no wall)
grid = [['.'] * W for _ in range(H)]
rng = random.Random(7)


def put_cell(x, y, ch):
    if 0 <= x < W and 0 <= y < H:
        grid[y][x] = ch


def fill(x0, y0, x1, y1, ch):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put_cell(x, y, ch)


def at(x, y):
    return grid[y][x] if 0 <= x < W and 0 <= y < H else ' '


def rounded(x0, y0, x1, y1, ch, cut=1):
    """Rectangle with its corners cut diagonally (cut tiles), so shapes read as organic."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            dx = min(x - x0, x1 - x)
            dy = min(y - y0, y1 - y)
            if dx + dy >= cut:
                put_cell(x, y, ch)


# ---------------------------------------------------------------------------
# 1. Town outline: an island like the Cainos demo, wider in the middle
# ---------------------------------------------------------------------------
inset = {40: 11, 39: 8, 38: 6, 37: 4, 36: 3, 3: 3, 2: 4, 1: 6, 0: 9}
for y in range(H):
    k = inset.get(y, 1)
    for x in range(W):
        if x < k or x >= W - k:
            grid[y][x] = ' '
fill(0, 17, 2, 23, ' ')         # west bay
fill(W - 3, 9, W - 1, 14, ' ')  # east bay
fill(22, 0, 34, 1, ' ')         # south bay between farm and graveyard
fill(22, 39, 31, 40, ' ')       # north bay between hill and market

# sparse flowers in the meadow
for y in range(H):
    for x in range(W):
        if at(x, y) == '.' and rng.random() < 0.08:
            grid[y][x] = ','

# ---------------------------------------------------------------------------
# 2. Paths (cobbles): a ring round the apothecary, four roads out to the quarters
# ---------------------------------------------------------------------------
for y in range(CY - 4, CY + 5):
    for x in range(CX - 4, CX + 5):
        d2 = (x - CX) ** 2 + (y - CY) ** 2
        if 5.5 <= d2 <= 17.5:
            put_cell(x, y, '=')
fill(CX - 1, CY + 4, CX + 1, 29, '=')       # north road ...
fill(CX + 1, 27, 41, 29, '=')               # ... to the market
fill(CX - 1, 12, CX + 1, CY - 4, '=')       # south road ...
fill(CX + 1, 14, 44, 16, '=')               # ... to the graveyard gate
fill(13, CY - 1, CX - 4, CY + 1, '=')       # west road ...
fill(12, CY - 1, 14, 25, '=')               # ... north to the church stairs
fill(12, 9, 14, CY - 1, '=')                # ... south to the farm
fill(7, 7, 14, 9, '=')                      # farm yard path to the farmhouse stairs
fill(CX + 4, CY - 1, 41, CY + 1, '=')       # east road ...
fill(39, CY - 1, 41, 33, '=')               # ... becomes the market lane

# ---------------------------------------------------------------------------
# 3. Raised ground with stairs
# ---------------------------------------------------------------------------
# NW Chapel Hill: church nave (stone) on the axis x=13, churchyard to the east, garden to the west
rounded(4, 26, 22, 36, '^', cut=2)
fill(10, 28, 15, 35, '%')                   # church nave floor (axis between cells 12 and 13 -> x=13.0)
# NE merchant house: a terrace with a windowed front
rounded(44, 30, 52, 36, '^', cut=1)
# SW farmhouse: windowed front, yard in front
rounded(3, 10, 11, 15, '^', cut=1)

stairs = [
    {'x': 13, 'capY': 26},      # church: main stairs on the nave axis
    {'x': 48, 'capY': 30},      # merchant house
    {'x': 7, 'capY': 10},       # farmhouse
]
windows = [                     # brick face cells with a window (face row = cap row - 1)
    {'x': 6, 'y': 25}, {'x': 9, 'y': 25}, {'x': 17, 'y': 25}, {'x': 20, 'y': 25},     # church hill
    {'x': 45, 'y': 29}, {'x': 51, 'y': 29},                                         # merchant house
    {'x': 4, 'y': 9}, {'x': 10, 'y': 9},                                            # farmhouse
]

# ---------------------------------------------------------------------------
# 4. SE sunken graveyard, entered through a gate on its north side
# ---------------------------------------------------------------------------
rounded(36, 3, 50, 13, 's', cut=1)
fill(42, 13, 43, 13, 'r')                   # ramp under the archway (axis x=43.0)

# ---------------------------------------------------------------------------
# 5. Stone stamps: decorative slabs set into the ground
# ---------------------------------------------------------------------------
stamps = []
STUD = [[5, 6, 7], [13, 14, 15], [21, 22, 23]]      # studded 3x3 slab, rows top to bottom
for j in range(3):
    for i in range(3):
        stamps.append({'x': CX - 1 + i, 'y': CY + 1 - j, 't': 'stone_%d' % STUD[j][i], 'f': 1})
for x in range(CX - 1, CX + 2):                      # the slab replaces the grass under it
    for y in range(CY - 1, CY + 2):
        put_cell(x, y, '.')

# ---------------------------------------------------------------------------
# Objects (world coordinates)
# ---------------------------------------------------------------------------
props = []
P, PL, ST = 'Props/PF Props - ', 'Plant/PF Plant - ', 'Props/PF Struct - '


def put(prefab, x, y, floor=1):
    props.append({'p': prefab, 'x': round(x, 3), 'y': round(y, 3), 'f': floor})


def sym(prefab, axis, dx, y, floor=1):
    """A pair mirrored around a vertical axis."""
    put(prefab, axis - dx, y, floor)
    put(prefab, axis + dx, y, floor)


ax, ay = CX + 0.5, CY + 0.5          # the map centre
apothecary = [ax, ay - 0.35]          # altar pivot sits slightly below its visual centre
komur = [ax, ay - 3.0]

# --- Apothecary green: lanterns on the ring diagonals, benches facing the altar on three sides
for dx, dy in [(-2.6, 2.6), (2.6, 2.6), (-2.6, -2.6), (2.6, -2.6)]:
    put(P + 'Stone Lantern 01', ax + dx, ay + dy - 0.3)
put(P + 'Stone Bench 01 S', ax, ay + 3.0)
put(P + 'Stone Bench 01 E', ax - 3.6, ay - 0.2)
put(P + 'Stone Bench 01 W', ax + 3.6, ay - 0.2)
for dx in (-5.5, 5.5):                                  # flower beds framing the ring
    for dy in (-4.5, 4.5):
        put(PL + 'Bush 0' + str(1 + int(dx + dy) % 6), ax + dx, ay + dy)
put(P + 'Road Sign 01 W', 22.5, 22.3)
put(P + 'Road Sign 01 E', 34.5, 22.3)

# --- Chapel Hill: archway over the stairs, pews in two columns, statue and coffin at the altar end
cax = 13.0
put(ST + 'Gate 01', cax, 28.1, 2)                       # church door at the top of the stairs
for y in (29.6, 31.0, 32.4):
    sym(P + 'Stone Bench 01 S', cax, 1.6, y, 2)          # pews
put(P + 'Stone Coffin 01 H', cax, 33.6, 2)
put(P + 'Statue 01', cax, 34.6, 2)
sym(P + 'Rune Pillar X3', cax, 2.4, 34.4, 2)
for y in (29.3, 31.3, 33.3):
    sym(P + 'Pillar 01', cax, 3.3, y, 2)                 # nave walls
sym(P + 'Stone Lantern 01', cax, 1.9, 24.6)              # lanterns at the foot of the stairs
for i, y in enumerate((28.4, 30.4, 32.4)):               # churchyard: two aligned rows of graves
    for x in (17.5, 19.5):
        put(P + 'Gravestone 0' + str(1 + (i + int(x)) % 3), x, y, 2)
put(P + 'Stone Coffin 01 V', 21.0, 33.6, 2)
for x, y in [(6.5, 33.5), (8.0, 31.0), (6.5, 28.5)]:     # garden west of the church
    put(PL + 'Bush 0' + str(1 + int(x * y) % 6), x, y, 2)
put(P + 'Stone Bench 01 E', 6.4, 30.6, 2)

# --- Market: stalls lining both sides of the lane, well at the crossing, merchant house goods
for i, y in enumerate((23.6, 26.6, 30.6)):
    put(P + 'Crate 0' + str(1 + i % 2), 37.5, y)
    put(P + 'Barrel 01', 37.5, y - 1.0)
    put(P + 'Pot 0' + str(1 + i % 3), 43.0, y)
    put(P + 'Crate 0' + str(2 - i % 2), 43.0, y - 1.0)
put(P + 'Well 01', 36.0, 28.0)
put(P + 'Stone Bench 01 S', 34.2, 30.4)
sym(P + 'Stone Lantern 01', 48.0, 1.9, 28.6)             # flanking the merchant house stairs
for x in (45.5, 46.5, 49.5, 50.5):
    put(P + ('Crate 01' if x < 48 else 'Barrel 01'), x, 34.4, 2)
put(P + 'Chest 01', 48.0, 34.4, 2)
put(P + 'Pot 02', 45.5, 32.0, 2)
put(P + 'Pot 03', 50.5, 32.0, 2)

# --- Farmstead: farmhouse yard, hedged crop fields with a wooden gate
sym(P + 'Stone Lantern 01', 7.0, 1.9, 8.4)
for x, y in [(4.5, 7.2), (4.5, 6.2), (5.5, 7.2)]:
    put(P + 'Crate 0' + str(1 + int(x + y) % 2), x, y)
put(P + 'Barrel 01', 10.5, 6.2)
put(P + 'Barrel 01', 11.5, 6.2)
for x in (4.5, 6.5, 8.5, 10.5):                          # goods on the farmhouse roof terrace
    put(P + ('Pot 01' if x < 7 else 'Crate 02'), x, 14.2, 2)
for y in (3, 5, 7):                                      # crop rows (flowery grass) separated by hedges
    fill(17, y, 22, y, ',')
for y in (4.2, 6.2):
    for x in (17.5, 19.5, 21.5):
        put(PL + 'Bush 0' + str(1 + int(x) % 6), x, y)
put(P + 'Wooden Gate 01 Opened', 16.5, 8.2)
put(P + 'Stone Cube 01', 15.5, 8.2)
put(P + 'Stone Cube 01', 23.5, 8.2)

# --- Graveyard: archway, statue on the axis, mirrored grave rows, coffins on the back wall, corner lanterns
gax = 43.0
put(ST + 'Gate 01', gax, 13.1)
put(P + 'Statue 01', gax, 9.6)
for y in (4.6, 6.6, 8.6):
    for dx in (2.0, 4.0, 6.0):
        sym(P + 'Gravestone 0' + str(1 + int(dx + y) % 3), gax, dx, y)
sym(P + 'Stone Coffin 01 V', gax, 2.5, 11.2)
sym(P + 'Stone Lantern 01', gax, 6.0, 11.4)
sym(P + 'Stone Lantern 01', gax, 6.0, 3.4)
sym(P + 'Stone Bench 01 S', gax, 3.5, 16.6)             # mourners' benches outside the gate

# --- Trees: only along the town edge and in the gaps between quarters, crowns clear of paths
tree_spots = [(16.5, 37.2), (34.5, 37.2), (40.5, 37.2), (2.6, 13.0), (2.6, 26.0), (53.5, 18.0), (53.5, 25.0),
              (25.5, 4.0), (32.5, 4.0), (19.0, 11.5), (34.0, 9.0), (23.5, 32.0), (33.5, 33.5), (53.0, 6.0)]
for x, y in tree_spots:
    put(PL + 'Tree 0' + str(1 + int(x * 3 + y) % 3), x, y)

# --- Sparse grass tufts on open meadow only (the one random element)
taken = {(int(p['x']), int(p['y'])) for p in props}
for _ in range(400):
    x, y = rng.randrange(3, W - 3), rng.randrange(3, H - 3)
    if at(x, y) in '.,' and (x, y) not in taken and all(at(x + dx, y + dy) in '.,' for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
        put(PL + 'Grass %02d' % rng.randrange(1, 16), x + 0.5, y + 0.3)
        taken.add((x, y))
    if sum(1 for p in props if 'Grass' in p['p']) >= 60:
        break

# ---------------------------------------------------------------------------
# Gameplay markers
# ---------------------------------------------------------------------------
villagers = [
    {'x': 15.5, 'y': 30.3, 'f': 2}, {'x': 18.5, 'y': 34.0, 'f': 2},   # Chapel Hill
    {'x': 40.0, 'y': 30.0, 'f': 1}, {'x': 40.0, 'y': 24.0, 'f': 1},   # Market lane
    {'x': 9.5, 'y': 8.0, 'f': 1}, {'x': 18.5, 'y': 9.5, 'f': 1},      # Farmstead
    {'x': 43.0, 'y': 7.4, 'f': 1},                                    # Graveyard
    {'x': 31.0, 'y': 17.0, 'f': 1},                                   # Apothecary green
]
holes = [
    {'x': 4.5, 'y': 4.5, 'f': 1, 'wave': 1},    # SW farmstead, behind the yard
    {'x': 51.5, 'y': 26.5, 'f': 1, 'wave': 2},  # NE market, beside the merchant house
    {'x': 48.5, 'y': 7.5, 'f': 1, 'wave': 3},   # SE graveyard, inside the pit
    {'x': 6.5, 'y': 35.0, 'f': 2, 'wave': 4},   # NW chapel hill garden
]

# ---------------------------------------------------------------------------
# Write
# ---------------------------------------------------------------------------
rows = [''.join(grid[y]) for y in range(H - 1, -1, -1)]
town = {
    'note': 'Generated by tools/mapgen/build_town_map.py - edit the script, not this file.',
    'width': W, 'height': H, 'rows': rows,
    'stairs': stairs, 'windows': windows, 'stamps': stamps,
    'komur': komur, 'apothecary': apothecary,
    'villagers': villagers, 'holes': holes, 'props': props,
}
os.makedirs(os.path.join(ROOT, 'Assets/_Game/Map'), exist_ok=True)
with open(os.path.join(ROOT, 'Assets/_Game/Map/town-map.json'), 'w') as f:
    json.dump(town, f, indent=1)

sys.path.insert(0, os.path.dirname(__file__))
import cainos  # noqa: E402
cainos.write_catalog(os.path.join(ROOT, 'docs/ui-design/cainos-catalog.json'))

print('map %dx%d, %d props, %d villagers, %d holes' % (W, H, len(props), len(villagers), len(holes)))
for r in rows:
    print(r)
