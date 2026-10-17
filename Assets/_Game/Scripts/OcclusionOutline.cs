using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Komur
{
    // Draws a coloured outline of a character on top of everything, but only while it is hidden:
    // behind a higher floor's ground or walls, or behind a tree crown.
    public class OcclusionOutline : MonoBehaviour
    {
        public SpriteRenderer source;
        public Material outlineMaterial;
        public Color color = Color.white;
        [Tooltip("Show the outline all the time, not only when hidden.")]
        public bool alwaysOn;
        public float checkInterval = 0.1f;

        private const string OverlayLayer = "Layer 3";
        private const int OverlayOrder = 500;
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

        private SpriteRenderer outline;
        private MaterialPropertyBlock block;
        private float nextCheckAt;
        private bool hidden;

        // Tilemaps per floor (1..3) and tree crowns, cached once per scene.
        private static readonly List<Tilemap>[] floorMaps = { null, new List<Tilemap>(), new List<Tilemap>(), new List<Tilemap>() };
        private static readonly List<SpriteRenderer> crowns = new List<SpriteRenderer>();
        private static int cachedScene = -1;

        private void Start()
        {
            if (source == null) source = GetComponentInChildren<SpriteRenderer>();
            var go = new GameObject("Hidden Outline");
            go.transform.SetParent(source.transform, false);
            outline = go.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = outlineMaterial;
            outline.sortingLayerName = OverlayLayer;
            outline.sortingOrder = OverlayOrder;
            outline.enabled = false;
            block = new MaterialPropertyBlock();
            CacheScene();
        }

        private static void CacheScene()
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene == cachedScene) return;
            cachedScene = scene;
            for (int f = 1; f <= 3; f++) floorMaps[f].Clear();
            crowns.Clear();
            foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                string n = tm.name;
                for (int f = 1; f <= 3; f++)
                    if (n.StartsWith(FloorAgent.LayerName(f) + " - ") && !n.Contains("Shadow")) floorMaps[f].Add(tm);
            }
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (sr.name == "Upper" && sr.transform.parent != null && sr.transform.parent.name.Contains("Tree")) crowns.Add(sr);
        }

        private void LateUpdate()
        {
            if (source == null || outline == null) return;
            if (Time.time >= nextCheckAt)
            {
                nextCheckAt = Time.time + checkInterval;
                hidden = alwaysOn || IsHidden();
            }

            bool show = hidden && source.enabled && source.gameObject.activeInHierarchy;
            outline.enabled = show;
            if (!show) return;
            outline.sprite = source.sprite;
            outline.flipX = source.flipX;
            outline.GetPropertyBlock(block);
            block.SetColor(OutlineColor, color);
            outline.SetPropertyBlock(block);
        }

        private bool IsHidden()
        {
            int floor = FloorOfCharacter();
            Bounds b = source.bounds;
            Vector3[] probes =
            {
                new Vector3(b.center.x, b.min.y + b.size.y * 0.2f),
                b.center,
                new Vector3(b.center.x, b.max.y - b.size.y * 0.2f),
            };

            // Anything painted on a higher floor over the character is drawn on top of it.
            for (int f = floor + 1; f <= 3; f++)
                foreach (var tm in floorMaps[f])
                {
                    if (tm == null) continue;
                    foreach (var p in probes)
                        if (tm.HasTile(tm.WorldToCell(p))) return true;
                }

            // Tree crowns in front of the character (trees lower on screen draw over it).
            float feet = transform.position.y;
            foreach (var crown in crowns)
            {
                if (crown == null || crown.transform.parent.position.y > feet) continue;
                if (crown.sortingLayerID != source.sortingLayerID) continue;
                foreach (var p in probes)
                    if (crown.bounds.Contains(new Vector3(p.x, p.y, crown.bounds.center.z))) return true;
            }
            return false;
        }

        private int FloorOfCharacter()
        {
            var root = transform;
            return FloorAgent.FloorOf(root.gameObject);
        }
    }
}
