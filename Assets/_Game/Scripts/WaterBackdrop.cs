using UnityEngine;

namespace Komur
{
    // The empty space around the map becomes water: a pixel-art tile with glinting wave dashes,
    // animated in a few frames like the rest of the art and drifting slowly. It always fills the view,
    // sits behind everything, and is generated in code. Installs itself on the main camera.
    public class WaterBackdrop : MonoBehaviour
    {
        public Color deep = new Color32(46, 104, 150, 255);
        public Color shallow = new Color32(58, 122, 168, 255);
        public Color glint = new Color32(132, 190, 226, 255);
        public Color foam = new Color32(206, 234, 248, 255);
        public float pixelsPerUnit = 16f;
        public float frameTime = 0.32f;
        public Vector2 drift = new Vector2(0.12f, 0.04f);

        private const int Tile = 32;
        private const int Frames = 4;
        private const string SortingLayerName = "Default";

        private Camera cam;
        private SpriteRenderer water;
        private Sprite[] frames;

        // Runs once at startup, then again for every loaded scene: Main Menu / Restart reload the scene,
        // which brings a fresh camera without the water.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            AddToMainCamera();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => AddToMainCamera();

        private static void AddToMainCamera()
        {
            var main = Camera.main;
            if (main != null && main.GetComponent<WaterBackdrop>() == null) main.gameObject.AddComponent<WaterBackdrop>();
        }

        private void Start()
        {
            cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = deep;
            frames = BuildFrames();

            water = new GameObject("Water").AddComponent<SpriteRenderer>();
            water.sprite = frames[0];
            water.drawMode = SpriteDrawMode.Tiled;
            water.tileMode = SpriteTileMode.Continuous;
            water.sortingLayerName = SortingLayerName;
            water.sortingOrder = -1000;
            var gm = GameManager.I;
            if (gm != null && gm.sceneryMaterial != null) water.sharedMaterial = gm.sceneryMaterial;
        }

        private void LateUpdate()
        {
            if (cam == null || water == null) return;
            float tile = Tile / pixelsPerUnit;
            water.sprite = frames[(int)(Time.unscaledTime / frameTime) % Frames];

            // Cover the view plus a tile of margin; anchor to the tile grid (plus a slow drift) so the
            // pattern stays put in the world instead of sliding with the camera.
            Vector2 half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
            water.size = (half + Vector2.one * tile) * 2f;
            Vector2 offset = new Vector2(Mathf.Repeat(Time.unscaledTime * drift.x, tile), Mathf.Repeat(Time.unscaledTime * drift.y, tile));
            Vector3 c = cam.transform.position;
            float px = 1f / pixelsPerUnit;
            float x = Mathf.Floor((c.x - offset.x) / tile) * tile + offset.x;
            float y = Mathf.Floor((c.y - offset.y) / tile) * tile + offset.y;
            water.transform.position = new Vector3(Mathf.Round(x / px) * px, Mathf.Round(y / px) * px, 0f);
        }

        private Sprite[] BuildFrames()
        {
            var tex = new Texture2D(Tile * Frames, Tile, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var rng = new System.Random(1234);
            // Base: deep water with soft lighter patches (fixed across frames).
            var baseCol = new Color[Tile * Tile];
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float n = Mathf.Sin((x + y * 0.5f) * 0.39f) + Mathf.Sin((y - x * 0.3f) * 0.27f + 1.3f);
                baseCol[y * Tile + x] = n > 0.9f ? shallow : deep;
            }
            // Wave dashes: short horizontal glints that shift sideways a pixel per frame (wrapping).
            var dashes = new (int x, int y, int len, bool bright)[9];
            for (int i = 0; i < dashes.Length; i++)
                dashes[i] = (rng.Next(Tile), (i * 7 + rng.Next(3)) % Tile, 2 + rng.Next(4), rng.Next(4) == 0);

            for (int f = 0; f < Frames; f++)
            {
                var px = (Color[])baseCol.Clone();
                foreach (var d in dashes)
                {
                    // Glints swell and shrink over the cycle so the water looks alive.
                    int len = Mathf.Max(1, d.len - (f == 1 || f == 3 ? 1 : 0) + (f == 2 ? 1 : 0));
                    for (int k = 0; k < len; k++)
                    {
                        int x = (d.x + k + f) % Tile;
                        px[d.y * Tile + x] = d.bright && f == 2 && k == len / 2 ? foam : glint;
                    }
                }
                tex.SetPixels(f * Tile, 0, Tile, Tile, px);
            }
            tex.Apply();

            var sprites = new Sprite[Frames];
            for (int f = 0; f < Frames; f++)
                sprites[f] = Sprite.Create(tex, new Rect(f * Tile, 0, Tile, Tile), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            return sprites;
        }
    }
}
