using UnityEngine;

namespace Komur
{
    // Thrown by a frightened villager at the spot Kömür was standing; flies in an arc with a ground
    // shadow and can be dodged.
    public class ThrownStone : MonoBehaviour
    {
        public float speed = 6f;
        public float hitRadius = 0.45f;
        public float spinSpeed = 540f;
        public float arcHeight = 0.9f;
        [Tooltip("The rock sprite; lifted along the arc while the root slides along the ground.")]
        public Transform visual;
        public Transform shadow;
        [Header("Readability")]
        public float rockSize = 1.0f;                       // scale of the rock sprite
        public Vector2 shadowSize = new Vector2(0.24f, 0.12f);
        public Color rim = new Color(1f, 1f, 1f, 0.9f);     // outline so it reads against the ground

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private KomurController target;
        private Vector3 start;
        private Vector3 destination;
        private float stunDuration;
        private float travelled;
        private float distance;
        private Vector3 shadowBase = Vector3.one;

        // floor: the thrower's floor (sorting layer); material: an unlit character material so the rock is
        // drawn like the characters (the default sprite material can render it invisible here).
        public void Launch(KomurController komur, float stun, int floor = 1, Material material = null)
        {
            int layer = SortingLayer.NameToID(FloorAgent.LayerName(floor));
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (SortingLayer.IsValid(layer)) sr.sortingLayerID = layer;
                bool isShadow = sr.transform == shadow;
                sr.sortingOrder = isShadow ? 90 : 100;     // in flight it is above the ground and the characters
                if (material != null && !isShadow) sr.sharedMaterial = material;
                if (isShadow) sr.color = new Color(0f, 0f, 0f, 0.25f);
                else if (material != null)
                {
                    var block = new MaterialPropertyBlock();
                    sr.GetPropertyBlock(block);
                    block.SetColor(OutlineColorId, rim);
                    sr.SetPropertyBlock(block);
                }
            }
            if (visual != null) visual.localScale = Vector3.one * rockSize;
            if (shadow != null) shadow.localScale = new Vector3(shadowSize.x, shadowSize.y, 1f);
            shadowBase = shadow != null ? shadow.localScale : Vector3.one;
            target = komur;
            start = transform.position;
            destination = komur.transform.position;
            distance = Mathf.Max(0.1f, Vector3.Distance(start, destination));
            stunDuration = stun;
            Destroy(gameObject, 4f);
        }

        private void Update()
        {
            if (target == null) return;
            travelled += speed * Time.deltaTime;
            float t = Mathf.Clamp01(travelled / distance);
            transform.position = Vector3.Lerp(start, destination, t);

            float lift = arcHeight * 4f * t * (1f - t);
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, lift, 0f);
                visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
            if (shadow != null) shadow.localScale = shadowBase * Mathf.Lerp(1f, 0.6f, lift / Mathf.Max(0.01f, arcHeight));

            // Only hits once it's coming down near the ground.
            if (t > 0.6f && (target.transform.position - transform.position).sqrMagnitude <= hitRadius * hitRadius)
            {
                target.Stun(stunDuration);
                Destroy(gameObject);
            }
            else if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
