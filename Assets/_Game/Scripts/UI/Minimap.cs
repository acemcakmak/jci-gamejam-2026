using System.Collections;
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
        public int textureWidth = 384;

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
        private RenderTexture texture;
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
            StartCoroutine(Capture());
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

        // Renders the map once with characters hidden.
        private IEnumerator Capture()
        {
            float aspect = worldBounds.size.x / Mathf.Max(0.01f, worldBounds.size.y);
            int w = textureWidth, h = Mathf.Max(1, Mathf.RoundToInt(textureWidth / aspect));
            texture = new RenderTexture(w, h, 16) { filterMode = FilterMode.Point, name = "Minimap" };

            var camGo = new GameObject("Minimap Capture Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = worldBounds.extents.y;
            cam.aspect = aspect;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.targetTexture = texture;
            cam.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -20f);

            var hidden = new List<Renderer>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is KomurController || mb is Villager || mb is Rat || mb is RatHole)) continue;
                foreach (var r in mb.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            Destroy(camGo);
            foreach (var r in hidden) if (r != null) r.enabled = true;

            if (mapImage != null)
            {
                mapImage.texture = texture;
                var fitter = mapImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = aspect;
            }
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
            if (texture != null) texture.Release();
        }
    }
}
