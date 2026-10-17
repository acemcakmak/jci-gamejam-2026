using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Komur
{
    // Snapshot of the whole map (taken once at start) with live icons for Kömür, villagers, rats,
    // open rat holes and the apothecary.
    public class Minimap : MonoBehaviour
    {
        [Header("Layout")]
        public UnityEngine.UI.RawImage mapImage;
        public RectTransform markerLayer;

        [Header("Icons")]
        public Sprite dotSprite;
        public Sprite komurSprite;
        public Sprite sickSprite;
        public Sprite deadSprite;
        public Sprite apothecarySprite;

        [Header("Colors")]
        public Color healthyColor = new Color(0.95f, 0.95f, 0.85f);
        public Color friendlyColor = new Color(1f, 0.6f, 0.8f);
        public Color sickColor = new Color(0.35f, 1f, 0.35f);
        public Color deadColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        public Color ratColor = new Color(0.9f, 0.2f, 0.2f);
        public Color bossColor = new Color(0.6f, 0.2f, 0.8f);
        public Color holeColor = new Color(0.2f, 0.1f, 0.05f);
        public Color apothecaryColor = new Color(1f, 0.85f, 0.45f);

        [Header("Sizes (minimap pixels)")]
        public float villagerSize = 12f;
        public float ratSize = 8f;
        public float iconSize = 20f;

        private Bounds worldBounds;
        private readonly Dictionary<Object, UnityEngine.UI.Image> markers = new Dictionary<Object, UnityEngine.UI.Image>();
        private readonly List<Object> stale = new List<Object>();
        private readonly HashSet<Object> seen = new HashSet<Object>();

        private readonly RectTransform[] layers = new RectTransform[7];

        private void Awake()
        {
            // One child layer per marker category so draw order is stable (holes at the bottom, Kömür on top).
            for (int i = 0; i < layers.Length; i++)
            {
                var go = new GameObject("Layer " + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(markerLayer, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                layers[i] = rt;
            }
        }

        private void Start()
        {
            worldBounds = ComputeWorldBounds();
            Paint();
        }

        private static Bounds ComputeWorldBounds()
        {
            bool first = true;
            Bounds b = new Bounds(Vector3.zero, Vector3.one * 10f);
            foreach (var r in FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None))
            {
                var tm = r.GetComponent<Tilemap>();
                if (tm == null) continue;
                tm.CompressBounds();
                if (tm.cellBounds.size.x == 0 || tm.cellBounds.size.y == 0) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---------- Painted map ----------

        [Header("Painted map")]
        public int pixelsPerTile = 6;
        public Color grass = new Color32(112, 140, 58, 255);
        public Color grassDark = new Color32(96, 124, 48, 255);
        public Color upperGrass = new Color32(146, 176, 82, 255);
        public Color cobble = new Color32(150, 146, 120, 255);
        public Color stone = new Color32(168, 163, 150, 255);
        public Color stoneEdge = new Color32(112, 96, 80, 255);
        public Color wallTop = new Color32(84, 66, 54, 255);
        public Color brick = new Color32(120, 102, 88, 255);
        public Color brickDark = new Color32(92, 76, 64, 255);
        public Color tree = new Color32(58, 92, 34, 255);
        public Color treeLight = new Color32(86, 124, 46, 255);
        public Color propWood = new Color32(150, 104, 62, 255);
        public Color propStone = new Color32(190, 186, 176, 255);
        public Color shadow = new Color(0f, 0f, 0f, 0.28f);

        private Texture2D painted;
        private int texW, texH;
        private Color[] px;

        // Draws the town from its tilemaps plus trees and props, so the minimap reads like the real map.
        private void Paint()
        {
            int tilesW = Mathf.CeilToInt(worldBounds.size.x), tilesH = Mathf.CeilToInt(worldBounds.size.y);
            texW = tilesW * pixelsPerTile;
            texH = tilesH * pixelsPerTile;
            px = new Color[texW * texH];

            var maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            Tilemap Find(string part) => System.Array.Find(maps, m => m.name.Contains(part));
            var grass1 = Find("Layer 1 - Grass");
            var stone1 = Find("Layer 1 - Stone");
            var wall1 = Find("Layer 1 - Wall");
            var grass2 = Find("Layer 2 - Grass");
            var wall2 = Find("Layer 2 - Wall");

            for (int ty = 0; ty < tilesH; ty++)
            for (int tx = 0; tx < tilesW; tx++)
            {
                Vector3 world = new Vector3(worldBounds.min.x + tx + 0.5f, worldBounds.min.y + ty + 0.5f, 0f);
                string w1 = TileName(wall1, world), w2 = TileName(wall2, world);
                bool upper = TileName(grass2, world) != null;
                bool onStone = TileName(stone1, world) != null;
                string g1 = TileName(grass1, world);
                for (int py = 0; py < pixelsPerTile; py++)
                for (int pxl = 0; pxl < pixelsPerTile; pxl++)
                {
                    int x = tx * pixelsPerTile + pxl, y = ty * pixelsPerTile + py;
                    Color c;
                    if (w1 != null && IsFace(w1)) c = ((y / 2 + (py < 3 ? 0 : 1) * (x / 3)) % 2 == 0) ? brick : brickDark;
                    else if (w1 != null || (w2 != null && !upper)) c = wallTop;
                    else if (upper) c = Noise(x, y) < 0.2f ? Color.Lerp(upperGrass, Color.white, 0.12f) : upperGrass;
                    else if (onStone) c = (pxl == 0 || py == 0) ? Color.Lerp(stone, stoneEdge, 0.25f) : stone;
                    else if (g1 != null && g1.Contains("Pavement")) c = ((pxl + py) % 3 == 0) ? Color.Lerp(cobble, grass, 0.5f) : cobble;
                    else c = Noise(x, y) < 0.25f ? grassDark : grass;
                    px[y * texW + x] = c;
                }
            }

            // Props: trees, bushes, stone props and wooden supplies, with a soft shadow.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name;
                if (!n.StartsWith("PF ")) continue;
                Vector2 p = ToPixel(t.position);
                if (n.Contains("Tree")) Blob(p, 1.5f * pixelsPerTile, tree, treeLight);
                else if (n.Contains("Bush")) Blob(p, 0.55f * pixelsPerTile, tree, treeLight);
                else if (n.Contains("Gravestone") || n.Contains("Statue") || n.Contains("Pillar") || n.Contains("Well") || n.Contains("Altar"))
                    Rect(p, 0.45f * pixelsPerTile, propStone);
                else if (n.Contains("Crate") || n.Contains("Barrel") || n.Contains("Pot") || n.Contains("Chest") || n.Contains("Sign"))
                    Rect(p, 0.35f * pixelsPerTile, propWood);
                else if (n.Contains("Stairs")) Rect(p + new Vector2(0, -pixelsPerTile), 0.6f * pixelsPerTile, stoneEdge);
            }

            painted = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            painted.SetPixels(px);
            painted.Apply();
            if (mapImage != null)
            {
                mapImage.texture = painted;
                var fitter = mapImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = (float)texW / texH;
            }
        }

        private static string TileName(Tilemap tm, Vector3 world)
        {
            if (tm == null) return null;
            var tile = tm.GetTile(tm.WorldToCell(world));
            return tile != null ? tile.name : null;
        }

        // Cainos wall tiles 23-25 are the brick faces under a raised edge.
        private static bool IsFace(string tileName)
        {
            return tileName.EndsWith("_23") || tileName.EndsWith("_24") || tileName.EndsWith("_25");
        }

        private static float Noise(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        private Vector2 ToPixel(Vector3 world)
        {
            return new Vector2((world.x - worldBounds.min.x) * pixelsPerTile, (world.y - worldBounds.min.y) * pixelsPerTile);
        }

        private void Put(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= texW || y >= texH) return;
            int i = y * texW + x;
            px[i] = c.a >= 1f ? c : Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), c.a);
        }

        private void Blob(Vector2 center, float radius, Color body, Color light)
        {
            int r = Mathf.CeilToInt(radius);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (dx * dx + dy * dy <= radius * radius) Put((int)center.x + dx + 2, (int)center.y + dy - 2, shadow);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float d2 = dx * dx + dy * dy;
                if (d2 > radius * radius) continue;
                bool lit = dx < 0 && dy > 0 && d2 < radius * radius * 0.45f;
                Put((int)center.x + dx, (int)center.y + dy, lit ? light : body);
            }
        }

        private void Rect(Vector2 center, float half, Color c)
        {
            int h = Mathf.Max(1, Mathf.RoundToInt(half));
            for (int dy = -h; dy <= h; dy++)
            for (int dx = -h; dx <= h; dx++)
            {
                Put((int)center.x + dx + 1, (int)center.y + dy - 1, shadow);
            }
            for (int dy = -h; dy <= h; dy++)
            for (int dx = -h; dx <= h; dx++)
                Put((int)center.x + dx, (int)center.y + dy, c);
        }

        private void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || markerLayer == null) return;
            seen.Clear();

            foreach (var hole in RatHole.All)
                if (hole.IsOpen) Mark(hole, hole.transform.position, dotSprite, holeColor, ratSize * 1.6f, 0);

            foreach (var apothecary in FindApothecaries())
            {
                bool ready = apothecary.IsReady;
                Mark(apothecary, apothecary.transform.position, apothecarySprite, ready ? apothecaryColor : apothecaryColor * 0.6f, iconSize, 1);
            }

            foreach (var v in gm.villagers)
            {
                switch (v.State)
                {
                    case VillagerState.Dead:
                        Mark(v, v.transform.position, deadSprite, deadColor, iconSize * 0.8f, 2);
                        break;
                    case VillagerState.Sick:
                        float pulse = 1f + 0.25f * Mathf.Sin(Time.time * (v.SickFraction < 0.3f ? 14f : 6f));
                        Color c = Color.Lerp(Color.red, sickColor, v.SickFraction);
                        Mark(v, v.transform.position, sickSprite, c, iconSize * pulse, 4);
                        break;
                    default:
                        Mark(v, v.transform.position, dotSprite, v.Friendly ? friendlyColor : healthyColor, villagerSize, 3);
                        break;
                }
            }

            foreach (var rat in gm.rats)
                Mark(rat, rat.transform.position, dotSprite, rat.Kind == RatKind.King ? bossColor : ratColor,
                     rat.Kind == RatKind.King ? ratSize * 2.2f : ratSize, 5);

            if (gm.komur != null) Mark(gm.komur, gm.komur.transform.position, komurSprite, Color.white, iconSize * 1.3f, 6);

            stale.Clear();
            foreach (var kv in markers)
                if (kv.Key == null || !seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var key in stale)
            {
                if (markers[key] != null) Destroy(markers[key].gameObject);
                markers.Remove(key);
            }
        }

        private Apothecary[] apothecaries;

        private Apothecary[] FindApothecaries()
        {
            if (apothecaries == null) apothecaries = FindObjectsByType<Apothecary>(FindObjectsSortMode.None);
            return apothecaries;
        }

        private void Mark(Object key, Vector3 world, Sprite sprite, Color color, float size, int order)
        {
            seen.Add(key);
            if (!markers.TryGetValue(key, out var image) || image == null)
            {
                var go = new GameObject("Marker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(layers[Mathf.Clamp(order, 0, layers.Length - 1)], false);
                image = go.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                markers[key] = image;
            }

            image.sprite = sprite;
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(size, size);

            Rect rect = markerLayer.rect;
            float nx = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, world.x);
            float ny = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, world.y);
            image.rectTransform.anchoredPosition = new Vector2(nx * rect.width, ny * rect.height);
        }

        private void OnDestroy()
        {
            if (painted != null) Destroy(painted);
        }
    }
}
