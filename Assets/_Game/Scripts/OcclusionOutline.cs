using UnityEngine;

namespace Komur
{
    // Exact "see through walls": the character's own material writes stencil 1, scenery drawn over it clears
    // the stencil (Scenery material), and this silhouette - drawn on top of everything with the
    // Hidden Silhouette material - only shows where the stencil is no longer 1, i.e. the covered pixels.
    public class OcclusionOutline : MonoBehaviour
    {
        public SpriteRenderer source;
        [Tooltip("Hidden Silhouette material (Komur/Sprite, stencil NotEqual 1).")]
        public Material outlineMaterial;
        public Color color = Color.white;
        [Range(0f, 1f)] public float opacity = 0.55f;

        private const string OverlayLayer = "Layer 3";
        private const int OverlayOrder = 500;
        private static readonly int SilhouetteColor = Shader.PropertyToID("_SilhouetteColor");

        private SpriteRenderer silhouette;
        private MaterialPropertyBlock block;

        private void Start()
        {
            if (source == null) source = GetComponentInChildren<SpriteRenderer>();
            if (source == null || outlineMaterial == null) return;
            var go = new GameObject("Hidden Silhouette");
            go.transform.SetParent(source.transform, false);
            silhouette = go.AddComponent<SpriteRenderer>();
            silhouette.sharedMaterial = outlineMaterial;
            silhouette.sortingLayerName = OverlayLayer;
            silhouette.sortingOrder = OverlayOrder;
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (silhouette == null) return;
            bool show = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            silhouette.enabled = show;
            if (!show) return;
            silhouette.sprite = source.sprite;
            silhouette.flipX = source.flipX;
            silhouette.GetPropertyBlock(block);
            block.SetColor(SilhouetteColor, new Color(color.r, color.g, color.b, opacity));
            silhouette.SetPropertyBlock(block);
        }
    }
}
