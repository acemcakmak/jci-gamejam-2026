using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Komur
{
    // HOW TO PLAY on the title screen: a button beside PLAY that opens a see-through dark panel with one
    // short line per rule, each next to a sprite from the game itself (tinted like in play).
    public class HowToPlay : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private Hud hud;
        private GameObject panel;
        private GameObject openButton;
        private RectTransform rows;
        private bool filled;

        private readonly List<(Image image, Sprite[] frames)> animated = new List<(Image, Sprite[])>();

        public static HowToPlay Install(Hud hud, Canvas canvas)
        {
            if (hud.playButton == null || hud.againButton == null) return null;
            var help = hud.gameObject.AddComponent<HowToPlay>();
            help.hud = hud;
            help.Build(canvas);
            return help;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsOpen = false;

        private void Build(Canvas canvas)
        {
            // Sprout Lands' "?" button to the right of PLAY (a text button if the sprite is missing).
            var play = (RectTransform)hud.playButton.transform;
            if (hud.helpButtonSprite != null)
            {
                openButton = new GameObject("Help Button", typeof(RectTransform), typeof(Image), typeof(Button));
                var ort = (RectTransform)openButton.transform;
                ort.SetParent(play.parent, false);
                float h = play.sizeDelta.y;
                var size = hud.helpButtonSprite.rect.size;
                float w = h * size.x / Mathf.Max(1f, size.y);
                ort.anchorMin = play.anchorMin;
                ort.anchorMax = play.anchorMax;
                ort.pivot = play.pivot;
                ort.sizeDelta = new Vector2(w, h);
                ort.anchoredPosition = play.anchoredPosition + new Vector2(play.sizeDelta.x * 0.5f + 28f + w * 0.5f, 0f);
                var img = openButton.GetComponent<Image>();
                img.sprite = hud.helpButtonSprite;
                img.preserveAspect = true;
                var b = openButton.GetComponent<Button>();
                b.targetGraphic = img;
                if (hud.helpButtonPressed != null)
                {
                    b.transition = Selectable.Transition.SpriteSwap;
                    // Same as PLAY: the darker pressed frame also shows on hover.
                    b.spriteState = new SpriteState { pressedSprite = hud.helpButtonPressed, highlightedSprite = hud.helpButtonPressed, selectedSprite = hud.helpButtonSprite };
                }
                b.onClick.AddListener(() => Show(true));
            }
            else
            {
                openButton = Instantiate(hud.againButton.gameObject, play.parent, false);
                openButton.name = "How To Play Button";
                openButton.SetActive(true);
                var ort = (RectTransform)openButton.transform;
                ort.anchorMin = play.anchorMin;
                ort.anchorMax = play.anchorMax;
                ort.pivot = play.pivot;
                ort.sizeDelta = new Vector2(340f, play.sizeDelta.y);
                ort.anchoredPosition = play.anchoredPosition + new Vector2(play.sizeDelta.x * 0.5f + 28f + 170f, 0f);
                SetButton(openButton, "HOW TO PLAY", () => Show(true));
            }

            // The panel.
            panel = new GameObject("How To Play", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)panel.transform;
            prt.SetParent(canvas.transform, false);
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 0.9f);

            var font = hud.waveText != null ? hud.waveText.font : null;
            var title = NewText(prt, "HOW TO PLAY", font, 72f, Color.white, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -50f);
            trt.sizeDelta = new Vector2(0f, 100f);

            var rowsGo = new GameObject("Rules", typeof(RectTransform));
            rows = (RectTransform)rowsGo.transform;
            rows.SetParent(prt, false);
            rows.anchorMin = new Vector2(0.5f, 0.5f);
            rows.anchorMax = new Vector2(0.5f, 0.5f);
            rows.pivot = new Vector2(0.5f, 0.5f);
            rows.anchoredPosition = new Vector2(0f, 10f);
            rows.sizeDelta = new Vector2(1440f, 780f);
            var layout = rowsGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var back = Instantiate(hud.againButton.gameObject, prt, false);
            back.name = "Back Button";
            back.SetActive(true);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 40f);
            brt.sizeDelta = new Vector2(340f, 87f);
            SetButton(back, "BACK", () => Show(false));

            panel.transform.SetAsLastSibling();
            panel.SetActive(false);
        }

        private static void SetButton(GameObject go, string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(onClick);
            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
            }
        }

        public void Show(bool open)
        {
            if (open && !filled) Fill();
            IsOpen = open;
            panel.SetActive(open);
            // The title screen hides behind the panel so nothing shows through the text.
            if (hud.introPanel != null && GameManager.I != null && GameManager.I.State == GameState.Intro) hud.introPanel.SetActive(!open);
            if (open) panel.transform.SetAsLastSibling();
        }

        private void Update()
        {
            var gm = GameManager.I;
            if (openButton != null) openButton.SetActive(gm == null || gm.State == GameState.Intro);
            if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || gm == null || gm.State != GameState.Intro)) Show(false);
            if (!IsOpen) return;
            float t = Time.unscaledTime;
            foreach (var (image, frames) in animated)
                if (frames != null && frames.Length > 1) image.sprite = frames[(int)(t * 5f) % frames.Length];
        }

        // The rules, each with a sprite pulled from the running game.
        private void Fill()
        {
            filled = true;
            var gm = GameManager.I;
            var font = hud.waveText != null ? hud.waveText.font : null;
            Sprite Of(Component c) => c != null ? c.GetComponentInChildren<SpriteRenderer>(true)?.sprite : null;
            Sprite[] Villager(int i)
            {
                if (gm == null || gm.villagers.Count == 0) return null;
                var v = gm.villagers[i % gm.villagers.Count];
                return v != null ? v.Portraits : null;
            }
            var apothecary = FindFirstObjectByType<Apothecary>();
            Sprite potion = apothecary != null && apothecary.potionSprites != null && apothecary.potionSprites.Length > 0
                ? apothecary.potionSprites[Random.Range(0, apothecary.potionSprites.Length)] : null;
            Sprite rat = gm != null && gm.ratPrefab != null && gm.ratPrefab.visual != null ? gm.ratPrefab.visual.sprite : Of(gm != null ? gm.ratPrefab : null);
            Sprite boss = gm != null && gm.bossPrefab != null && gm.bossPrefab.visual != null ? gm.bossPrefab.visual.sprite : null;
            Sprite cat = gm != null && gm.komur != null && gm.komur.visual != null ? gm.komur.visual.sprite : null;

            var white = Color.white;
            Row(new[] { rat }, white, Color.clear, "Waves of plague rats crawl out of their holes to bite the villagers.", font);
            Row(new[] { cat }, white, Color.clear, "You are Komur: move with WASD or the arrows, and pounce on rats with SPACE.", font);
            Row(Villager(0), white, new Color(1f, 0.18f, 0.12f, 1f), "The villagers think black cats bring bad luck: if they see you, they get angry and throw stones.", font);
            Row(Villager(1), new Color(0.55f, 1f, 0.45f, 1f), new Color(0.2f, 0.75f, 0.15f, 1f), "A bitten villager turns green and starts running out of time.", font);
            Row(new[] { potion }, white, new Color(1f, 0.82f, 0.35f, 1f), "Grab the glowing potion from the apothecary and carry it to them.", font);
            Row(Villager(2), new Color(1f, 0.82f, 0.92f, 1f), new Color(1f, 0.45f, 0.75f, 1f), "A villager you cure turns pink: they love you now.", font);
            Row(Villager(3), new Color(0.7f, 0.95f, 1f, 0.5f), new Color(0.55f, 0.9f, 1f, 0.4f), "Too late, and they die. Then the town panics and even your friends stop loving you.", font);
            Row(new[] { boss }, white, Color.clear, "Survive five waves and the Plague Slime. Save the town, and win its love.", font);
        }

        private void Row(Sprite[] frames, Color tint, Color rim, string line, TMP_FontAsset font)
        {
            var row = new GameObject("Rule", typeof(RectTransform));
            row.transform.SetParent(rows, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 28f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 86f;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(row.transform, false);
            var image = iconGo.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = tint;
            var sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            image.sprite = sprite;
            if (sprite == null) image.color = Color.clear;
            if (rim.a > 0f)
            {
                var outline = iconGo.AddComponent<Outline>();
                outline.effectColor = rim;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            var ile = iconGo.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 86f;
            ile.minWidth = 86f;
            animated.Add((image, frames));

            var text = NewText(row.transform, line, font, 36f, new Color(0.98f, 0.96f, 0.9f, 1f), TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.Normal;
            var tle = text.gameObject.AddComponent<LayoutElement>();
            tle.flexibleWidth = 1f;
        }

        private static TextMeshProUGUI NewText(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
    }
}
