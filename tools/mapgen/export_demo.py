#!/usr/bin/env python3
"""Exports the Cainos demo scene (tilemaps + prop placements) to docs/ui-design/demo-scene.json so the
design page can render it as a reference. Numbers only, no art. Run from the repo root."""
import glob
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import cainos  # noqa: E402

scene_path = os.path.join(cainos.CAINOS, 'Scene/SC Demo.unity')
text = open(scene_path).read()
docs = text.split('--- !u!')[1:]
_, tile_by_guid = cainos.all_tiles()

prefab_by_guid = {}
for path in glob.glob(os.path.join(cainos.CAINOS, 'Prefab', '*', '*.prefab.meta')):
    g = re.search(r'guid: (\w+)', open(path).read()).group(1)
    prefab_by_guid[g] = os.path.relpath(path[:-5], os.path.join(cainos.CAINOS, 'Prefab'))[:-7]

names = {}
for d in docs:
    head, body = d.split('\n', 1)
    if head.split()[0] == '1':
        m = re.search(r'm_Name: (.*)', body)
        names[head.split('&')[1].split()[0]] = m.group(1) if m else ''

tilemaps = {}
for d in docs:
    head, body = d.split('\n', 1)
    if head.split()[0] != '1839735485':      # Tilemap
        continue
    go = re.search(r'm_GameObject: \{fileID: (\d+)', body).group(1)
    assets = re.search(r'm_TileAssetArray:(.*?)m_TileSpriteArray', body, re.S).group(1)
    index = [tile_by_guid.get(m.group(2)) for m in re.finditer(r'm_RefCount: \d+\n\s+m_Data: \{fileID: (-?\d+)(?:, guid: (\w+))?', assets)]
    cells = []
    for m in re.finditer(r'- first: \{x: (-?\d+), y: (-?\d+), z: 0\}\n\s+second:\n\s+serializedVersion: 2\n\s+m_TileIndex: (\d+)', body):
        key = index[int(m.group(3))]
        if key:
            cells.append([int(m.group(1)), int(m.group(2)), key])
    tilemaps[names.get(go, go)] = cells

props = []
for d in docs:
    head, body = d.split('\n', 1)
    if head.split()[0] != '1001':
        continue
    g = re.search(r'm_SourcePrefab: \{fileID: 100100000, guid: (\w+)', body)
    if not g or g.group(1) not in prefab_by_guid:
        continue
    x = re.search(r'propertyPath: m_LocalPosition\.x\n\s+value: (-?[\d.e-]+)', body)
    y = re.search(r'propertyPath: m_LocalPosition\.y\n\s+value: (-?[\d.e-]+)', body)
    layer = re.search(r'propertyPath: m_SortingLayer\n\s+value: (\d+)', body)
    props.append({'p': prefab_by_guid[g.group(1)], 'x': float(x.group(1)) if x else 0.0,
                  'y': float(y.group(1)) if y else 0.0, 'layer': int(layer.group(1)) if layer else 1})

out = {'tilemaps': tilemaps, 'props': props}
with open(os.path.join(cainos.ROOT, 'docs/ui-design/demo-scene.json'), 'w') as f:
    json.dump(out, f)
print({k: len(v) for k, v in tilemaps.items()}, len(props), 'props')
