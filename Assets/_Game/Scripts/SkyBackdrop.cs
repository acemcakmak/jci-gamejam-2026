using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // The empty space around the map becomes sky: a banded pixel gradient that always fills the view,
    // with soft clouds drifting underneath the town (they only show where there is no ground).
    // Installs itself on the main camera; everything is generated in code.
    public class SkyBackdrop : MonoBehaviour
    {
        public Color top = new Color32(98, 163, 222, 255);
        public Color horizon = new Color32(196, 229, 247, 255);
        public int bands = 7;
        public int cloudCount = 14;
        public float cloudSpeed = 0.35f;
        public float pixelsPerUnit = 16f;

        private const string SortingLayerName = "Default";
        private Camera cam;
        private SpriteRenderer sky;
        private readonly List<Transform> clouds = new List<Transform>();
        private readonly List<float> cloudSpeeds = new List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var main = Camera.main;
            if (main != null && main.GetComponent<SkyBackdrop>() == null) main.gameObject.AddComponent<SkyBackdrop>();
        }

        private void Start()
        {
            cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = horizon;
            var gm = GameManager.I;
            Material material = gm != null ? gm.sceneryMaterial : null;

            sky = new GameObject("Sky").AddComponent<SpriteRenderer>();
            sky.sprite = GradientSprite();
            sky.sortingLayerName = SortingLayerName;
            sky.sortingOrder = -1000;
            if (material != null) sky.sharedMaterial = material;

            var cloudSprites = new[] { CloudSprite(1), CloudSprite(2), CloudSprite(3) };
            for (int i = 0; i < cloudCount; i++)
            {
                var sr = new GameObject("Cloud").AddComponent<SpriteRenderer>();
                sr.sprite = cloudSprites[i % cloudSprites.Length];
                sr.sortingLayerName = SortingLayerName;
                sr.sortingOrder = -900 + i;
                sr.color = new Color(1f, 1f, 1f, Random.Range(0.7f, 0.95f));
                sr.flipX = Random.value < 0.5f;
                if (material != null) sr.sharedMaterial = material;
                sr.transform.localScale = Vector3.one * Random.Range(1.2f, 2.4f);
                clouds.Add(sr.transform);
                cloudSpeeds.Add(cloudSpeed * Random.Range(0.6f, 1.4f));
            }
            ScatterClouds(true);
        }

        private Vector2 HalfView => new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);

        private void ScatterClouds(bool anywhere)
        {
            Vector2 half = HalfView + Vector2.one * 6f;
            Vector2 c = cam.transform.position;
            for (int i = 0; i < clouds.Count; i++)
            {
                if (!anywhere) continue;
                clouds[i].position = new Vector3(c.x + Random.Range(-half.x, half.x), c.y + Random.Range(-half.y, half.y), 0f);
            }
        }

        private void LateUpdate()
        {
            if (cam == null || sky == null) return;
            // Sky: always exactly fills the view, whatever the zoom.
            Vector2 half = HalfView;
            Vector3 c = cam.transform.position;
            sky.transform.position = new Vector3(c.x, c.y, 0f);
            Vector2 size = sky.sprite.bounds.size;
            sky.transform.localScale = new Vector3(half.x * 2.05f / size.x, half.y * 2.05f / size.y, 1f);

            // Clouds drift right and wrap around the view (with a margin so they never pop).
            Vector2 margin = half + Vector2.one * 6f;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < clouds.Count; i++)
            {
                var t = clouds[i];
                Vector3 p = t.position + Vector3.right * cloudSpeeds[i] * dt;
                if (p.x > c.x + margin.x) { p.x = c.x - margin.x; p.y = c.y + Random.Range(-margin.y, margin.y); }
                else if (p.x < c.x - margin.x) p.x = c.x + margin.x;
                if (p.y > c.y + margin.y) p.y -= margin.y * 2f;
                else if (p.y < c.y - margin.y) p.y += margin.y * 2f;
                t.position = p;
            }
        }

        // Vertical gradient in a few hard bands, to match the pixel art.
        private Sprite GradientSprite()
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float t = Mathf.Floor((float)y / h * bands) / Mathf.Max(1, bands - 1);
                tex.SetPixel(0, y, Color.Lerp(horizon, top, t));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 1f);
        }

        // Puffy pixel cloud: overlapping circles, white on top with a light blue underside.
        private Sprite CloudSprite(int seed)
        {
            var rng = new System.Random(seed * 7919);
            const int w = 48, h = 22;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var puffs = new List<Vector3>();
            int n = 4 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float cx = 8 + (w - 16) * (i + 0.5f) / n + rng.Next(-3, 4);
                float r = 5 + rng.Next(0, 5) + (i == n / 2 ? 3 : 0);
                puffs.Add(new Vector3(cx, 7 + rng.Next(0, 3), r));
            }
            var white = Color.white;
            var shade = new Color(0.84f, 0.91f, 0.98f, 1f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = false;
                foreach (var p in puffs)
                    if ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= p.z * p.z && y >= 3) { inside = true; break; }
                px[y * w + x] = inside ? (y < 7 ? shade : white) : new Color(0f, 0f, 0f, 0f);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }
}
