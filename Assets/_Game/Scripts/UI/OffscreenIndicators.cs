using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Edge-of-screen arrows pointing at sick villagers that are off camera
    // (and at the apothecary when someone needs medicine and Kömür has none).
    public class OffscreenIndicators : MonoBehaviour
    {
        public RectTransform container;
        public Sprite arrowSprite;
        public Sprite sickIcon;
        public Sprite apothecaryIcon;
        public Sprite backgroundSprite;
        public float edgePadding = 70f;
        public float size = 64f;
        public Color apothecaryColor = new Color(1f, 0.85f, 0.45f);
        [Tooltip("Show a pointer to the apothecary when villagers are sick and Kömür carries no medicine.")]
        public bool pointToApothecary = true;

        private class Indicator
        {
            public RectTransform root;
            public RectTransform arrow;
            public UnityEngine.UI.Image arrowImage;
            public UnityEngine.UI.Image icon;
        }

        private readonly List<Indicator> pool = new List<Indicator>();
        private Camera cam;
        private Apothecary apothecary;

        private void Start()
        {
            cam = Camera.main;
            apothecary = FindFirstObjectByType<Apothecary>();
            if (container == null) container = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            var gm = GameManager.I;
            int used = 0;
            if (gm != null && gm.State == GameState.Playing && cam != null)
            {
                foreach (var v in gm.villagers)
                {
                    if (v.State != VillagerState.Sick) continue;
                    Color c = Color.Lerp(Color.red, new Color(0.35f, 1f, 0.35f), v.SickFraction);
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.time * (v.SickFraction < 0.3f ? 14f : 6f));
                    if (Show(used, v.transform.position, sickIcon, c, pulse)) used++;
                }

                if (pointToApothecary && apothecary != null && gm.Sick > 0 && gm.komur != null && !gm.komur.HasMedicine)
                    if (Show(used, apothecary.transform.position, apothecaryIcon, apothecaryColor, 1f)) used++;
            }

            for (int i = used; i < pool.Count; i++) pool[i].root.gameObject.SetActive(false);
        }

        // Returns true if an indicator was needed (target is off screen).
        private bool Show(int index, Vector3 world, Sprite icon, Color color, float scale)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            bool onScreen = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
            if (onScreen) return false;

            var ind = Get(index);
            ind.root.gameObject.SetActive(true);

            Rect rect = container.rect;
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir;
            dir = Vector2.Scale(dir, rect.size);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

            // Push the point out along dir until it touches the padded screen edge.
            float halfW = rect.width * 0.5f - edgePadding, halfH = rect.height * 0.5f - edgePadding;
            float t = Mathf.Min(halfW / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), halfH / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
            ind.root.anchoredPosition = dir * t;
            ind.root.localScale = Vector3.one * scale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            ind.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            ind.arrow.anchoredPosition = dir.normalized * size * 0.55f;
            ind.arrowImage.color = color;
            ind.icon.sprite = icon;
            ind.icon.color = color;
            return true;
        }

        private Indicator Get(int index)
        {
            while (pool.Count <= index)
            {
                var root = new GameObject("Indicator", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(container, false);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.sizeDelta = new Vector2(size, size);

                if (backgroundSprite != null)
                {
                    var bg = NewImage("Background", root, backgroundSprite, Color.white, size);
                    bg.preserveAspect = true;
                }
                var icon = NewImage("Icon", root, null, Color.white, size * 0.5f);
                icon.preserveAspect = true;
                var arrow = NewImage("Arrow", root, arrowSprite, Color.white, size * 0.45f);
                arrow.preserveAspect = true;

                pool.Add(new Indicator { root = root, arrow = arrow.rectTransform, arrowImage = arrow, icon = icon });
            }
            return pool[index];
        }

        private static UnityEngine.UI.Image NewImage(string name, RectTransform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
