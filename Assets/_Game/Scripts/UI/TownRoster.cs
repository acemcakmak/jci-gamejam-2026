using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Komur
{
    // The town at a glance: a small portrait of every villager (their own idle animation), coloured by
    // how they are doing - normal, pink when they like Kömür, red rim when angry at the cat, green and
    // pulsing with a timer bar when sick, a pale see-through spirit when dead.
    public class TownRoster : MonoBehaviour
    {
        public int columns = 4;
        public float cell = 60f;
        public float gap = 10f;
        public float padding = 20f;     // clears the panel's frame
        public float fps = 5f;

        [Header("Looks")]
        public Color normalRim = new Color(0.16f, 0.1f, 0.08f, 0.75f);
        public Color fondTint = new Color(1f, 0.82f, 0.92f, 1f);
        public Color fondRim = new Color(1f, 0.45f, 0.75f, 1f);
        public Color angryRim = new Color(1f, 0.18f, 0.12f, 1f);
        public Color sickTint = new Color(0.55f, 1f, 0.45f, 1f);
        public Color sickRim = new Color(0.2f, 0.75f, 0.15f, 1f);
        public Color deadTint = new Color(0.7f, 0.95f, 1f, 0.45f);
        public Color deadRim = new Color(0.55f, 0.9f, 1f, 0.35f);

        private class Slot
        {
            public Villager villager;
            public Image portrait;
            public Outline rim;
            public Image timerBack, timer;
            public float phase;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private RectTransform panel;

        // Takes over a HUD panel: hides what was in it and resizes it to fit the grid.
        public static TownRoster Install(RectTransform panel)
        {
            for (int i = 0; i < panel.childCount; i++) panel.GetChild(i).gameObject.SetActive(false);
            var roster = panel.gameObject.AddComponent<TownRoster>();
            roster.panel = panel;
            return roster;
        }

        private void Build(IReadOnlyList<Villager> villagers)
        {
            int n = villagers.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)columns));
            int cols = Mathf.Min(columns, Mathf.Max(1, n));
            panel.sizeDelta = new Vector2(padding * 2f + cols * cell + (cols - 1) * gap, padding * 2f + rows * cell + (rows - 1) * gap);

            for (int i = 0; i < n; i++)
            {
                var v = villagers[i];
                var slot = new Slot { villager = v, phase = Random.value * 3f };
                var go = new GameObject("Villager " + (i + 1), typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(padding + (i % columns) * (cell + gap), -(padding + (i / columns) * (cell + gap)));
                rt.sizeDelta = new Vector2(cell, cell);

                slot.portrait = go.AddComponent<Image>();
                slot.portrait.preserveAspect = true;
                slot.portrait.raycastTarget = false;
                var frames = v != null ? v.Portraits : null;
                if (frames != null && frames.Length > 0) slot.portrait.sprite = frames[0];
                slot.rim = go.AddComponent<Outline>();
                slot.rim.effectDistance = new Vector2(2f, -2f);

                // Sickness timer under the portrait.
                slot.timerBack = NewBar(rt, "Timer", new Color(0f, 0f, 0f, 0.6f), 1f);
                slot.timer = NewBar(rt, "Fill", sickRim, 0.94f);
                slots.Add(slot);
            }
        }

        private static Image NewBar(RectTransform parent, string name, Color color, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2((1f - width) / 2f, 0f);
            rt.anchorMax = new Vector2(1f - (1f - width) / 2f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, -7f);
            rt.sizeDelta = new Vector2(0f, 5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.SetActive(false);
            return img;
        }

        private void Update()
        {
            var gm = GameManager.I;
            if (gm == null || panel == null) return;
            if (slots.Count == 0)
            {
                if (gm.villagers.Count == 0) return;
                Build(gm.villagers);
            }

            float t = Time.unscaledTime;
            foreach (var s in slots)
            {
                var v = s.villager;
                if (v == null) continue;
                var frames = v.Portraits;
                bool dead = v.State == VillagerState.Dead;
                if (frames != null && frames.Length > 0)
                    s.portrait.sprite = frames[dead ? 0 : (int)((t + s.phase) * fps) % frames.Length];

                Color tint = Color.white, rim = normalRim;
                bool sick = v.State == VillagerState.Sick;
                if (dead)
                {
                    tint = deadTint;
                    tint.a *= 0.8f + 0.2f * Mathf.Sin(t * 2f + s.phase);
                    rim = deadRim;
                }
                else if (sick)
                {
                    float left = v.SickFraction;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * (left < 0.3f ? 12f : 4f));
                    tint = Color.Lerp(sickTint, Color.white, 0.25f * pulse);
                    rim = Color.Lerp(sickRim, Color.red, left < 0.3f ? pulse * 0.6f : 0f);
                }
                else if (v.Friendly)
                {
                    tint = fondTint;
                    rim = fondRim;
                }
                else if (v.Angry)
                {
                    rim = angryRim;
                }
                s.portrait.color = tint;
                s.rim.effectColor = rim;

                s.timerBack.gameObject.SetActive(sick);
                s.timer.gameObject.SetActive(sick);
                if (sick)
                {
                    var rt = s.timer.rectTransform;
                    float w = ((RectTransform)s.timerBack.transform).rect.width * 0.94f;
                    rt.sizeDelta = new Vector2(-(w * (1f - v.SickFraction)), rt.sizeDelta.y);
                    s.timer.color = Color.Lerp(Color.red, sickRim, v.SickFraction);
                }
            }
        }
    }
}
