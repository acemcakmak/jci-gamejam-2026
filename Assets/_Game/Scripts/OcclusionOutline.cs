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
        private static readonly List<SpriteRenderer> occluders = new List<SpriteRenderer>();
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
            occluders.Clear();
            foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                string n = tm.name;
                // Only the ground of higher floors hides characters; their wall rims are thin lines.
                for (int f = 1; f <= 3; f++)
                    if (n.StartsWith(FloorAgent.LayerName(f) + " - ") && !n.Contains("Shadow") && !n.Contains("Wall")) floorMaps[f].Add(tm);
            }
            // Tall scenery that can be drawn over a character: trees, statues, pillars, gates, lanterns, crates...
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.sprite == null || sr.name.Contains("Shadow")) continue;
                // Stairs sprites are big mostly-transparent boxes that characters walk over, not behind.
                if (sr.transform.root.name.Contains("Stairs") || (sr.transform.parent != null && sr.transform.parent.name.Contains("Stairs"))) continue;
                if (sr.GetComponentInParent<KomurController>() || sr.GetComponentInParent<Villager>() || sr.GetComponentInParent<Rat>()
                    || sr.GetComponentInParent<RatHole>() || sr.GetComponentInParent<ThrownStone>()) continue;
                if (sr.bounds.size.y < 0.9f) continue;
                occluders.Add(sr);
            }
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

            // Scenery drawn over the character: on a higher sorting layer, or the same layer and lower on
            // screen (sprites are sorted by Y), or a higher order on the same layer.
            float feet = SortY(source);
            int myLayer = SortingLayer.GetLayerValueFromID(source.sortingLayerID);
            foreach (var crown in occluders)
            {
                if (crown == null || !crown.enabled) continue;
                int layer = SortingLayer.GetLayerValueFromID(crown.sortingLayerID);
                bool inFront = layer > myLayer
                               || layer == myLayer && (crown.sortingOrder > source.sortingOrder
                                                      || crown.sortingOrder == source.sortingOrder && SortY(crown) < feet);
                if (!inFront) continue;
                var cb = crown.bounds;
                // Shrink the sprite box a little: its corners are transparent.
                cb.Expand(new Vector3(-cb.size.x * 0.2f, -cb.size.y * 0.1f, 0f));
                foreach (var p in probes)
                    if (cb.Contains(new Vector3(p.x, p.y, cb.center.z))) return true;
            }
            return false;
        }

        // The Y Unity sorts a sprite by (custom Y axis): its pivot or its bounds centre, per Sprite Sort Point.
        private static float SortY(SpriteRenderer r)
        {
            return r.spriteSortPoint == SpriteSortPoint.Pivot ? r.transform.position.y : r.bounds.center.y;
        }

        private int FloorOfCharacter()
        {
            var root = transform;
            return FloorAgent.FloorOf(root.gameObject);
        }
    }
}
