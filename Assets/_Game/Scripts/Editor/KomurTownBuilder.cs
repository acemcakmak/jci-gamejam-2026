using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Komur.EditorTools
{
    // Paints the drafted 48x32 town with Cainos tiles: outer wall, cobbled roads, stone square,
    // raised platforms (upper floor) with stairs, wall shadows, district props, villagers and rat holes.
    // Coordinates are tile cells, (0,0) bottom-left, y up.
    public static partial class KomurSceneBuilder
    {
        private const string TownScenePath = GameRoot + "/Scenes/Town.unity";
        private const int MapW = 48, MapH = 32;

        // Raised areas (upper floor). Rect y is the brick face row; stairs are cut into the south side.
        private static readonly (RectInt rect, int[] stairsX, string name)[] Platforms =
        {
            (new RectInt(3, 20, 15, 10), new[] { 8, 14 }, "Chapel Hill"),
            (new RectInt(38, 25, 8, 6), new[] { 41 }, "Market Terrace"),
            (new RectInt(9, 8, 4, 4), new int[0], "Farm Plinth"),
            (new RectInt(40, 8, 5, 4), new int[0], "Mausoleum"),
            (new RectInt(28, 22, 4, 4), new int[0], "Garden Plinth"),
            (new RectInt(17, 3, 4, 4), new int[0], "South Plinth"),
        };

        private static readonly Vector2Int KomurStart = new Vector2Int(24, 13);
        private static readonly Vector2Int ApothecaryCell = new Vector2Int(24, 16);

        private static readonly (Vector2Int cell, int floor)[] VillagerCells =
        {
            (new Vector2Int(7, 26), 2), (new Vector2Int(13, 24), 2),     // Chapel Hill
            (new Vector2Int(35, 25), 1), (new Vector2Int(41, 22), 1),    // Market
            (new Vector2Int(7, 7), 1), (new Vector2Int(13, 4), 1),       // Farmstead
            (new Vector2Int(38, 6), 1),                                  // Graveyard
            (new Vector2Int(21, 17), 1),                                 // Square
        };

        private static readonly (Vector2Int cell, int floor, int wave)[] RatHoleCells =
        {
            (new Vector2Int(24, 3), 1, 1),   // south road
            (new Vector2Int(45, 24), 1, 2),  // east edge, market
            (new Vector2Int(2, 9), 1, 3),    // west edge, farmstead
            (new Vector2Int(5, 28), 2, 4),   // Chapel Hill, upper floor
            (new Vector2Int(45, 3), 1, 5),   // graveyard corner
        };

        [MenuItem("Kömür/Build Town Scene (drafted map)", priority = 3)]
        public static void BuildTownScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(TownScenePath) &&
                !EditorUtility.DisplayDialog("Kömür", "Town.unity already exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
                return;

            var prefabs = BuildPrefabsAndSettings();
            Random.InitState(1717);

            // Start from the demo scene so tilemaps, sorting layers, colliders and camera are configured.
            if (File.Exists(TownScenePath)) AssetDatabase.DeleteAsset(TownScenePath);
            AssetDatabase.CopyAsset(CainosRoot + "/Scene/SC Demo.unity", TownScenePath);
            var scene = EditorSceneManager.OpenScene(TownScenePath);
            var maps = StripDemoScene(scene);

            if (!maps.TryGetValue("Layer 1 - Grass", out var grass1) ||
                !maps.TryGetValue("Layer 1 - Wall", out var wall1) ||
                !maps.TryGetValue("Layer 2 - Grass", out var grass2) ||
                !maps.TryGetValue("Layer 2 - Wall", out var wall2))
            {
                Debug.LogError("[Kömür] Demo tilemaps not found; cannot paint the town.");
                return;
            }
            maps.TryGetValue("Layer 1 - Stone Ground", out var stone1);
            if (stone1 == null) stone1 = grass1;

            PaintGround(grass1);
            PaintRoads(grass1, stone1);
            PaintRoom(wall1, new RectInt(0, 0, MapW, MapH), true);   // outer town wall
            maps.TryGetValue("Layer 1 - Wall Shadow", out var shadow1);
            foreach (var platform in Platforms) PaintPlatform(platform.rect, platform.stairsX, grass2, wall1, wall2, shadow1);
            foreach (var tm in maps.Values) tm.CompressBounds();
            Physics2D.SyncTransforms();

            var markers = new List<Vector2>();
            markers.Add(CellCenter(grass1, KomurStart));
            markers.Add(CellCenter(grass1, ApothecaryCell));
            foreach (var v in VillagerCells) markers.Add(CellCenter(grass1, v.cell));
            foreach (var h in RatHoleCells) markers.Add(CellCenter(grass1, h.cell));

            var props = new GameObject("Props").transform;
            PlaceStairs(grass1, props);
            PlaceDistrictProps(grass1, props);
            PlaceGreenery(grass1, props, markers);

            BuildApothecary(CellCenter(grass1, ApothecaryCell));
            SetupScene(prefabs, CellCenter(grass1, KomurStart));
            foreach (var v in VillagerCells) PlaceVillager(prefabs.villager, CellCenter(grass1, v.cell), v.floor);
            foreach (var h in RatHoleCells) PlaceRatHole(prefabs.ratHole, CellCenter(grass1, h.cell), h.floor, h.wave);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddSceneToBuildSettings(TownScenePath);
            Debug.Log("[Kömür] Town scene built. Turn on NavGrid gizmos to check walkable areas.");
        }

        // ---------- Scene prep ----------

        // Keeps camera, lights and the tilemap grid (cleared); removes demo props, plants, stairs and the player.
        private static Dictionary<string, Tilemap> StripDemoScene(UnityEngine.SceneManagement.Scene scene)
        {
            // Lift the things we keep to the scene root, wherever the demo nested them.
            var keepers = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true)) keepers.Add(cam.transform);
                foreach (var grid in root.GetComponentsInChildren<Grid>(true)) keepers.Add(grid.transform);
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name.Contains("Light2D")) keepers.Add(c.transform);
            }
            foreach (var k in keepers.Distinct()) k.SetParent(null, true);
            foreach (var root in scene.GetRootGameObjects())
                if (!keepers.Contains(root.transform)) Object.DestroyImmediate(root);

            var maps = new Dictionary<string, Tilemap>();
            foreach (var grid in Object.FindObjectsByType<Grid>(FindObjectsSortMode.None))
            {
                foreach (Transform child in grid.transform.Cast<Transform>().ToArray())
                    if (child.GetComponent<Tilemap>() == null) Object.DestroyImmediate(child.gameObject);
                foreach (var tm in grid.GetComponentsInChildren<Tilemap>(true))
                {
                    tm.ClearAllTiles();
                    maps[tm.gameObject.name] = tm;
                }
            }
            // Cameras and lights should not carry demo children either.
            foreach (var k in keepers.Where(k => k != null && k.GetComponent<Grid>() == null))
                foreach (Transform child in k.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(child.gameObject);

            EnsureCamera();
            return maps;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        // ---------- Tiles ----------

        private static TileBase LoadTile(string palette, string name)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{CainosRoot}/Tile Palette/{palette}/{name}.asset");
            if (tile == null) Debug.LogWarning("[Kömür] Missing tile " + name);
            return tile;
        }

        private static TileBase Wall(int n) => LoadTile("TP Wall", "TX Tileset Wall_" + n);
        private static TileBase Stone(int n) => LoadTile("TP Stone Ground", "TX Tileset Stone Ground_" + n);
        private static TileBase GrassPlain() => LoadTile("TP Grass", "TX Tileset Grass " + Random.Range(0, 16));
        private static TileBase GrassFlower() => LoadTile("TP Grass", "TX Tileset Grass Flower " + Random.Range(0, 16));
        private static TileBase Pavement() => LoadTile("TP Grass", "TX Tileset Grass Pavement " + Random.Range(0, 8));

        private static void PaintGround(Tilemap grass)
        {
            for (int y = 0; y < MapH; y++)
            for (int x = 0; x < MapW; x++)
                grass.SetTile(new Vector3Int(x, y, 0), Random.value < 0.12f ? GrassFlower() : GrassPlain());
        }

        private static void PaintRoads(Tilemap grass, Tilemap stone)
        {
            // 3-wide cobbled roads from the square to each edge.
            for (int y = 2; y < 30; y++)
            for (int x = 23; x <= 25; x++)
                if (y < 12 || y > 19) grass.SetTile(new Vector3Int(x, y, 0), Pavement());
            for (int x = 1; x < 47; x++)
            for (int y = 14; y <= 16; y++)
                if (x < 19 || x > 28) grass.SetTile(new Vector3Int(x, y, 0), Pavement());

            // Town square: one big stone slab, 9-sliced from the Stone Ground sheet.
            var square = new RectInt(19, 12, 10, 8);
            for (int y = square.yMin; y < square.yMax; y++)
            for (int x = square.xMin; x < square.xMax; x++)
            {
                int col = x == square.xMin ? 0 : x == square.xMax - 1 ? 2 : 1;
                int row = y == square.yMax - 1 ? 0 : y == square.yMin ? 2 : 1;
                stone.SetTile(new Vector3Int(x, y, 0), Stone(row * 8 + col));
            }
        }

        // Cainos "room / platform" outline: rim on top and sides, cap row, then a brick face row underneath.
        // r.y is the face row; r.yMax - 1 is the top rim.
        private static void PaintRoom(Tilemap tm, RectInt r, bool withFace, ICollection<int> capGaps = null)
        {
            int left = r.xMin, right = r.xMax - 1, face = r.yMin, cap = r.yMin + 1, top = r.yMax - 1;
            for (int x = left; x <= right; x++)
            {
                tm.SetTile(new Vector3Int(x, top, 0), Wall(x == left ? 0 : x == right ? 2 : 1));
                if (capGaps == null || !capGaps.Contains(x))
                    tm.SetTile(new Vector3Int(x, cap, 0), Wall(x == left ? 15 : x == right ? 17 : 16));
                if (withFace)
                    tm.SetTile(new Vector3Int(x, face, 0), Wall(x == left ? 23 : x == right ? 25 : 24));
            }
            for (int y = cap + 1; y < top; y++)
            {
                tm.SetTile(new Vector3Int(left, y, 0), Wall(8));
                tm.SetTile(new Vector3Int(right, y, 0), Wall(9));
            }
        }

        private static void PaintPlatform(RectInt r, int[] stairsX, Tilemap grass2, Tilemap wall1, Tilemap wall2, Tilemap shadow)
        {
            // Upper-floor grass covers the rim and interior.
            for (int y = r.yMin + 1; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
                grass2.SetTile(new Vector3Int(x, y, 0), Random.value < 0.15f ? GrassFlower() : GrassPlain());

            // Ground floor sees the full cliff; the upper floor gets the rim with gaps where the stairs are.
            PaintRoom(wall1, r, true);
            var gaps = new HashSet<int>();
            foreach (int sx in stairsX) { gaps.Add(sx - 1); gaps.Add(sx); }
            PaintRoom(wall2, r, false, gaps);
            PaintShadowColumn(shadow, r);
        }

        // Drop shadow on the east side of a wall block, as in the Cainos demo.
        private static void PaintShadowColumn(Tilemap shadow, RectInt r)
        {
            if (shadow == null || r.xMax >= MapW) return;
            int x = r.xMax, top = r.yMax - 1;
            if (top + 1 < MapH) shadow.SetTile(new Vector3Int(x, top + 1, 0), LoadTile("TP Wall", "TX Shadow Tileset Wall 1"));
            for (int y = r.yMin + 1; y <= top; y++) shadow.SetTile(new Vector3Int(x, y, 0), LoadTile("TP Wall", "TX Shadow Tileset Wall 2"));
            shadow.SetTile(new Vector3Int(x, r.yMin, 0), LoadTile("TP Wall", "TX Shadow Tileset Wall 3"));
        }

        // ---------- Objects ----------

        private static Vector3 CellCenter(Tilemap tm, Vector2Int cell)
        {
            return tm.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        // True if a ground-floor object at this cell would sit inside a raised block or on stairs.
        private static bool BlockedForGround(int x, int y)
        {
            foreach (var p in Platforms)
            {
                var r = p.rect;
                if (x >= r.xMin - 1 && x <= r.xMax && y >= r.yMin - 1 && y < r.yMax) return true;
                foreach (int sx in p.stairsX)
                    if (x >= sx - 2 && x <= sx + 1 && y >= r.yMin - 3 && y <= r.yMin + 1) return true;
            }
            return false;
        }

        private static GameObject PlacePrefab(string relativePath, Vector3 position, Transform parent, int floor = 1)
        {
            if (floor == 1 && BlockedForGround(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CainosRoot}/Prefab/{relativePath}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[Kömür] Missing prefab " + relativePath);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            if (floor != 1) MoveToFloor(go, floor);
            return go;
        }

        private static void MoveToFloor(GameObject go, int floor)
        {
            string name = FloorAgent.LayerName(floor);
            int layer = LayerMask.NameToLayer(name);
            int ground = LayerMask.NameToLayer(FloorAgent.LayerName(1));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingLayerName = name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (layer >= 0 && (t.gameObject.layer == ground || t.gameObject.layer == 0)) t.gameObject.layer = layer;
        }

        private static void PlaceStairs(Tilemap tm, Transform parent)
        {
            foreach (var p in Platforms)
            foreach (int sx in p.stairsX)
            {
                // Stairs pivot sits on the first interior row above the cap (matches the Cainos demo).
                Vector3 pos = tm.CellToWorld(new Vector3Int(sx, p.rect.yMin + 2, 0));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CainosRoot}/Prefab/Props/PF Struct - Stairs S 01 M.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.position = pos;
            }
        }

        private static void PlaceDistrictProps(Tilemap tm, Transform parent)
        {
            Vector3 C(int x, int y) => CellCenter(tm, new Vector2Int(x, y));
            string Crate() => "Props/PF Props - Crate 0" + Random.Range(1, 3);
            string Pot() => "Props/PF Props - Pot 0" + Random.Range(1, 4);
            string Bush() => "Plant/PF Plant - Bush 0" + Random.Range(1, 7);
            string Tree() => "Plant/PF Plant - Tree 0" + Random.Range(1, 4);

            // Town square: well, pillars at the corners, a few supplies
            PlacePrefab("Props/PF Props - Well 01", C(27, 18), parent);
            foreach (var (x, y) in new[] { (18, 11), (29, 11), (18, 20), (29, 20) }) PlacePrefab("Props/PF Props - Pillar 0" + Random.Range(1, 3), C(x, y), parent);
            PlacePrefab("Props/PF Props - Barrel 01", C(20, 13), parent);
            PlacePrefab(Crate(), C(28, 13), parent);
            PlacePrefab(Pot(), C(20, 18), parent);
            PlacePrefab("Props/PF Props - Road Sign 01 E", C(30, 17), parent);
            PlacePrefab("Props/PF Props - Road Sign 01 W", C(17, 13), parent);

            // Market (north-east): stalls of crates and barrels, goods up on the terrace
            foreach (var (x, y) in new[] { (32, 21), (33, 21), (34, 21), (32, 27), (33, 27), (36, 29) }) PlacePrefab(Crate(), C(x, y), parent);
            foreach (var (x, y) in new[] { (36, 26), (39, 20), (44, 20), (45, 21) }) PlacePrefab("Props/PF Props - Barrel 01", C(x, y), parent);
            foreach (var (x, y) in new[] { (31, 24), (37, 23), (34, 24), (43, 19) }) PlacePrefab(Pot(), C(x, y), parent);
            PlacePrefab("Props/PF Props - Chest 01", C(36, 21), parent);
            foreach (var (x, y) in new[] { (43, 28), (44, 28), (39, 29) }) PlacePrefab(Crate(), C(x, y), parent, 2);
            PlacePrefab("Props/PF Props - Barrel 01", C(44, 27), parent, 2);
            PlacePrefab(Pot(), C(40, 29), parent, 2);
            PlacePrefab(Bush(), C(39, 27), parent, 2);

            // Farmstead (south-west): supplies, hedges, a garden plinth with a tree
            foreach (var (x, y) in new[] { (15, 6), (16, 7), (14, 8), (3, 5) }) PlacePrefab(Crate(), C(x, y), parent);
            foreach (var (x, y) in new[] { (11, 3), (4, 4), (5, 3) }) PlacePrefab("Props/PF Props - Barrel 01", C(x, y), parent);
            foreach (var (x, y) in new[] { (5, 11), (3, 12), (16, 11), (15, 12), (7, 3), (2, 6) }) PlacePrefab(Bush(), C(x, y), parent);
            PlacePrefab(Tree(), C(10, 10), parent, 2);
            PlacePrefab(Bush(), C(11, 10), parent, 2);

            // Graveyard (south-east): rows of gravestones around a raised mausoleum
            for (int x = 32; x <= 44; x += 2)
            for (int y = 4; y <= 10; y += 3)
            {
                if (Mathf.Abs(x - 38) <= 1 && Mathf.Abs(y - 6) <= 1) continue; // keep the villager's spot clear
                if (x >= 44 && y <= 4) continue;                                // rat hole corner
                PlacePrefab("Props/PF Props - Gravestone 0" + Random.Range(1, 4), C(x, y), parent);
            }
            PlacePrefab("Props/PF Props - Statue 01", C(42, 10), parent, 2);
            PlacePrefab("Props/PF Props - Rune Pillar X2", C(41, 10), parent, 2);
            PlacePrefab("Props/PF Props - Rune Pillar Broken", C(31, 3), parent);
            PlacePrefab("Props/PF Props - Rune Pillar Broken", C(31, 10), parent);

            // Garden and south plinths
            PlacePrefab(Tree(), C(29, 24), parent, 2);
            PlacePrefab(Bush(), C(30, 24), parent, 2);
            PlacePrefab(Tree(), C(18, 5), parent, 2);
            PlacePrefab(Bush(), C(19, 5), parent, 2);

            // Chapel Hill (upper floor): chapel ruins, trees and hedges
            PlacePrefab("Props/PF Props - Statue 01", C(10, 28), parent, 2);
            PlacePrefab("Props/PF Props - Altar 01", C(11, 27), parent, 2);
            foreach (var (x, y) in new[] { (5, 24), (16, 27), (8, 28), (12, 28) }) PlacePrefab("Props/PF Props - Pillar 0" + Random.Range(1, 3), C(x, y), parent, 2);
            PlacePrefab("Props/PF Props - Rune Pillar X2", C(15, 23), parent, 2);
            foreach (var (x, y) in new[] { (4, 22), (16, 22) }) PlacePrefab(Tree(), C(x, y), parent, 2);
            foreach (var (x, y) in new[] { (10, 23), (6, 23), (14, 26) }) PlacePrefab(Bush(), C(x, y), parent, 2);
            foreach (var (x, y) in new[] { (9, 25), (12, 22) }) PlacePrefab("Props/PF Props - Gravestone 0" + Random.Range(1, 4), C(x, y), parent, 2);
        }

        private static void PlaceGreenery(Tilemap tm, Transform parent, List<Vector2> markers)
        {
            Vector3 C(int x, int y) => CellCenter(tm, new Vector2Int(x, y));
            bool OnRoad(int x, int y) => (x >= 22 && x <= 26) || (y >= 13 && y <= 17) || (x >= 18 && x <= 29 && y >= 11 && y <= 20);

            // Trees along the edges and between districts.
            foreach (var (x, y) in new[] { (2, 18), (2, 12), (20, 27), (29, 29), (30, 7), (21, 7), (45, 12), (45, 18),
                                           (19, 22), (21, 3), (27, 3), (10, 18), (15, 18), (35, 18), (41, 18), (33, 12),
                                           (2, 2), (36, 2), (27, 9), (29, 19), (20, 29), (34, 29) })
            {
                if (OnRoad(x, y)) continue;
                PlacePrefab("Plant/PF Plant - Tree 0" + Random.Range(1, 4), C(x, y), parent);
            }

            // Bushes, rocks, grass tufts and flowers anywhere off the roads, away from gameplay markers.
            int placed = 0;
            for (int attempt = 0; attempt < 1200 && placed < 110; attempt++)
            {
                int x = Random.Range(1, MapW - 1), y = Random.Range(2, MapH - 1);
                if (OnRoad(x, y) || BlockedForGround(x, y)) continue;
                Vector2 p = C(x, y);
                if (markers.Any(m => (m - p).sqrMagnitude < 2.25f)) continue;
                float roll = Random.value;
                string prefab =
                    roll < 0.12f ? "Plant/PF Plant - Bush 0" + Random.Range(1, 7) :
                    roll < 0.22f ? "Props/PF Props - Stone 0" + Random.Range(1, 6) :
                    roll < 0.32f ? "Plant/PF Plant - Flower 01" :
                    $"Plant/PF Plant - Grass {Random.Range(1, 16):00}";
                if (PlacePrefab(prefab, p, parent) != null) placed++;
            }

            // A few tufts on the raised areas too.
            foreach (var platform in Platforms)
            {
                var r = platform.rect;
                for (int i = 0; i < r.width * r.height / 8; i++)
                {
                    int x = Random.Range(r.xMin + 1, r.xMax - 1), y = Random.Range(r.yMin + 2, r.yMax - 1);
                    Vector2 p = C(x, y);
                    if (markers.Any(m => (m - p).sqrMagnitude < 2.25f)) continue;
                    PlacePrefab($"Plant/PF Plant - Grass {Random.Range(1, 16):00}", p, parent, 2);
                }
            }
        }
    }
}
