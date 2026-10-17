using UnityEngine;
using UnityEngine.Rendering;

namespace Komur
{
    // The medicine: glows and bobs above the apothecary, orbits Kömür once picked up, and flies into the
    // villager it cures. The root sits on the ground (it is what the sorting group sorts by); the bottle
    // floats above it, so the orbit passes in front of and behind the cat naturally.
    public class Potion : MonoBehaviour
    {
        private enum Mode { Altar, Orbit, Deliver }

        [Header("Look")]
        public float size = 0.6f;
        public Color glowColor = new Color(1f, 0.92f, 0.55f, 0.85f);
        public float glowSize = 2.2f;
        public Color coreColor = new Color(1f, 1f, 0.85f, 0.9f);
        public float coreSize = 0.9f;

        [Header("On the altar")]
        public float altarHeight = 1.1f;
        public float bobHeight = 0.12f;

        [Header("Orbiting Kömür")]
        public float orbitRadius = 0.8f;
        public float orbitFlatten = 0.45f;     // top-down view: the orbit is an ellipse
        public float orbitHeight = 0.7f;
        public float orbitSpeed = 3.2f;        // radians per second
        public float followSharpness = 12f;

        [Header("Delivery")]
        public float deliverSpeed = 9f;

        private Mode mode;
        private Transform bottle;
        private SpriteRenderer bottleRenderer, glowRenderer, coreRenderer;
        private SortingGroup group;
        private Transform target;
        private SortingGroup sortSource;
        private float angle, height, spawnedAt;
        private Vector3 deliverPoint;

        private static Sprite glowSprite;

        // glowMaterial: an All In 1 Sprite Shader material (outline glow + shine). Without it the potion
        // falls back to a soft halo sprite.
        public static Potion Create(Sprite sprite, Material material, Material glowMaterial, Vector3 groundPosition, int sortingLayerId, int sortingOrder, float height)
        {
            var go = new GameObject("Potion");
            go.transform.position = groundPosition;
            var potion = go.AddComponent<Potion>();
            potion.altarHeight = height;
            potion.glowMaterial = glowMaterial;
            potion.Build(sprite, material, sortingLayerId, sortingOrder);
            return potion;
        }

        private Material glowMaterial;
        private MaterialPropertyBlock glowBlock;
        private static readonly int ShineLocationId = Shader.PropertyToID("_ShineLocation");
        private static readonly int OutlineGlowId = Shader.PropertyToID("_OutlineGlow");
        private static readonly int OutlineAlphaId = Shader.PropertyToID("_OutlineAlpha");

        private void Build(Sprite sprite, Material material, int sortingLayerId, int sortingOrder)
        {
            group = gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = sortingLayerId;
            group.sortingOrder = sortingOrder;

            bottle = new GameObject("Bottle").transform;
            bottle.SetParent(transform, false);

            glowRenderer = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glowRenderer.transform.SetParent(bottle, false);
            glowRenderer.sprite = GlowSprite();
            glowRenderer.color = glowColor;
            glowRenderer.sortingOrder = 0;
            if (material != null) glowRenderer.sharedMaterial = material;

            coreRenderer = new GameObject("Core").AddComponent<SpriteRenderer>();
            coreRenderer.transform.SetParent(bottle, false);
            coreRenderer.sprite = GlowSprite();
            coreRenderer.sortingOrder = 0;
            if (material != null) coreRenderer.sharedMaterial = material;

            bottleRenderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            bottleRenderer.transform.SetParent(bottle, false);
            bottleRenderer.sprite = sprite;
            bottleRenderer.sortingOrder = 1;
            if (glowMaterial != null)
            {
                bottleRenderer.sharedMaterial = glowMaterial;
                glowRenderer.enabled = false;
                coreRenderer.enabled = false;
                glowBlock = new MaterialPropertyBlock();
            }
            else if (material != null) bottleRenderer.sharedMaterial = material;
            float h = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.y) : 1f;
            bottleRenderer.transform.localScale = Vector3.one * (size / h);

            mode = Mode.Altar;
            height = altarHeight;
            spawnedAt = Time.time;
            bottle.localScale = Vector3.zero;
            Update();
        }

        // Picked up: circle around this character, sorting with its group (layer follows it up the stairs).
        public void Orbit(Transform character)
        {
            target = character;
            sortSource = character.GetComponent<SortingGroup>();
            mode = Mode.Orbit;
            Vector2 d = transform.position - character.position;
            angle = Mathf.Atan2(d.y / Mathf.Max(0.01f, orbitFlatten), d.x);
        }

        public void DeliverTo(Transform villager)
        {
            target = villager;
            deliverPoint = villager.position;
            mode = Mode.Deliver;
        }

        private void Update()
        {
            float t = Time.time;
            // Pop in, then breathe.
            float pop = Mathf.Clamp01((t - spawnedAt) / 0.3f);
            float scale = Mathf.SmoothStep(0f, 1f, pop) * (1f + 0.25f * Mathf.Sin(pop * Mathf.PI));
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);

            switch (mode)
            {
                case Mode.Altar:
                    bottle.localPosition = new Vector3(0f, height + bobHeight * Mathf.Sin(t * 2.5f), 0f);
                    break;

                case Mode.Orbit:
                    if (target == null) { Destroy(gameObject); return; }
                    angle += orbitSpeed * Time.deltaTime;
                    Vector3 ground = target.position + new Vector3(Mathf.Cos(angle) * orbitRadius, Mathf.Sin(angle) * orbitRadius * orbitFlatten, 0f);
                    // Nudge the sort point so the bottle is in front exactly while it is on the near half.
                    ground.y += Mathf.Sin(angle) * 0.01f;
                    transform.position = Vector3.Lerp(transform.position, ground, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
                    height = Mathf.Lerp(height, orbitHeight, 1f - Mathf.Exp(-6f * Time.deltaTime));
                    bottle.localPosition = new Vector3(0f, height + 0.06f * Mathf.Sin(t * 5f), 0f);
                    bottleRenderer.flipX = false;
                    if (sortSource != null)
                    {
                        group.sortingLayerID = sortSource.sortingLayerID;
                        group.sortingOrder = sortSource.sortingOrder;
                    }
                    scale *= 0.85f;
                    break;

                case Mode.Deliver:
                    if (target != null) deliverPoint = target.position;
                    transform.position = Vector3.MoveTowards(transform.position, deliverPoint, deliverSpeed * Time.deltaTime);
                    height = Mathf.MoveTowards(height, 0.5f, deliverSpeed * 0.5f * Time.deltaTime);
                    bottle.localPosition = new Vector3(0f, height, 0f);
                    float left = Vector3.Distance(transform.position, deliverPoint);
                    scale *= Mathf.Clamp01(left / 0.6f);
                    if (left < 0.05f) { Destroy(gameObject); return; }
                    break;
            }

            bottle.localScale = Vector3.one * scale;
            bottle.localRotation = Quaternion.Euler(0f, 0f, mode == Mode.Altar ? 6f * Mathf.Sin(t * 1.7f) : 0f);
            if (glowBlock != null)
            {
                // Golden rim that breathes, and a shine sweeping across the bottle every couple of seconds.
                float sweep = Mathf.Repeat((t - spawnedAt) / 2.2f, 1f);
                bottleRenderer.GetPropertyBlock(glowBlock);
                glowBlock.SetFloat(ShineLocationId, Mathf.Clamp01(sweep * 1.6f - 0.3f));
                glowBlock.SetFloat(OutlineGlowId, Mathf.Lerp(2f, 4.5f, pulse));
                glowBlock.SetFloat(OutlineAlphaId, Mathf.Lerp(0.7f, 1f, pulse));
                bottleRenderer.SetPropertyBlock(glowBlock);
                bottleRenderer.color = Color.white;
                return;
            }
            var g = glowColor;
            g.a *= Mathf.Lerp(0.55f, 1f, pulse);
            glowRenderer.color = g;
            float glowScale = mode == Mode.Orbit ? 0.7f : 1f;
            glowRenderer.transform.localScale = Vector3.one * (glowSize * Mathf.Lerp(0.85f, 1.1f, pulse) * glowScale);
            var core = coreColor;
            core.a *= Mathf.Lerp(0.7f, 1f, pulse);
            coreRenderer.color = core;
            coreRenderer.transform.localScale = Vector3.one * (coreSize * Mathf.Lerp(0.9f, 1.15f, pulse) * glowScale);
            bottleRenderer.color = Color.Lerp(Color.white, new Color(1.25f, 1.25f, 1.15f), pulse * 0.5f);
        }

        // Soft radial gradient made once in code (no extra art).
        private static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2.2f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            glowSprite.name = "Potion Glow";
            return glowSprite;
        }
    }
}
