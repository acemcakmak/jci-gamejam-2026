using UnityEngine;

namespace Komur
{
    // Thrown by a frightened villager at the spot Kömür was standing; flies in an arc with a ground
    // shadow and can be dodged.
    public class ThrownStone : MonoBehaviour
    {
        public float speed = 3.6f;
        public float hitRadius = 0.45f;
        public float spinSpeed = 540f;
        public float arcHeight = 1.4f;
        [Tooltip("The rock sprite; lifted along the arc while the root slides along the ground.")]
        public Transform visual;
        public Transform shadow;
        [Header("Readability")]
        public float rockSize = 1.9f;                       // scale of the rock sprite
        [Tooltip("Shadow size in world units (width, height).")]
        public Vector2 shadowSize = new Vector2(0.6f, 0.26f);
        [Tooltip("After landing: one small bounce, a short roll, then it fades away.")]
        public float settleTime = 0.7f;
        public Color rim = new Color(0.12f, 0.09f, 0.07f, 1f);   // dark pixel rim so it reads on grass and stone

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
                if (isShadow) sr.color = new Color(0f, 0f, 0f, 0.3f);
                else if (material != null)
                {
                    var block = new MaterialPropertyBlock();
                    sr.GetPropertyBlock(block);
                    block.SetColor(OutlineColorId, rim);
                    sr.SetPropertyBlock(block);
                }
            }
            if (visual != null) visual.localScale = Vector3.one * rockSize;
            if (shadow != null)
            {
                var shadowSprite = shadow.GetComponent<SpriteRenderer>();
                Vector2 s = shadowSprite != null && shadowSprite.sprite != null ? (Vector2)shadowSprite.sprite.bounds.size : Vector2.one;
                shadow.localScale = new Vector3(shadowSize.x / Mathf.Max(0.01f, s.x), shadowSize.y / Mathf.Max(0.01f, s.y), 1f);
            }
            shadowBase = shadow != null ? shadow.localScale : Vector3.one;
            target = komur;
            start = transform.position;
            destination = komur.transform.position;
            distance = Mathf.Max(0.1f, Vector3.Distance(start, destination));
            stunDuration = stun;
            Destroy(gameObject, 4f);
        }

        // The boss's version: a sickly green rock with a dark green rim, a bit bigger.
        public void MakePlague(Color tint, Color rimColor, float sizeScale)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.transform == shadow) continue;
                sr.color = tint;
                var block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                block.SetColor(OutlineColorId, rimColor);
                sr.SetPropertyBlock(block);
            }
            if (visual != null) visual.localScale = Vector3.one * rockSize * sizeScale;
        }

        private float landedAt = -1f;
        private Vector3 rollDirection;

        // Thud, dust, a little hop and roll, then fade.
        private void Land(Vector3 bounceAway)
        {
            landedAt = Time.time;
            rollDirection = bounceAway;
            GameFeedbacks.Play(FeedbackId.StoneImpact, transform.position);
        }

        private void Settle()
        {
            float k = (Time.time - landedAt) / settleTime;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.position += rollDirection * (1f - k) * 1.2f * Time.deltaTime;
            float hop = 0.22f * Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(k * 2.2f) * Mathf.PI));
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, hop, 0f);
                visual.Rotate(0f, 0f, spinSpeed * 0.3f * (1f - k) * Time.deltaTime);
            }
            float alpha = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                var c = sr.color;
                c.a = sr.transform == shadow ? 0.3f * alpha : alpha;
                sr.color = c;
            }
        }

        private void Update()
        {
            if (landedAt >= 0f) { Settle(); return; }
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
            Vector3 flight = (destination - start).normalized;
            if (t > 0.6f && (target.transform.position - transform.position).sqrMagnitude <= hitRadius * hitRadius)
            {
                target.Stun(stunDuration);
                Land(-flight * 0.8f);          // bounces back off the cat
            }
            else if (t >= 1f)
            {
                Land(flight * 0.5f);           // rolls on a little
            }
        }
    }
}
