using System.Collections;
using TMPro;
using UnityEngine;

namespace Komur
{
    public class Hud : MonoBehaviour
    {
        public TextMeshProUGUI waveText;
        [Tooltip("Legacy single-line counter; optional.")]
        public TextMeshProUGUI villagersText;
        [Header("Town panel")]
        public UnityEngine.UI.Image aliveIcon;
        public TextMeshProUGUI aliveText;
        public UnityEngine.UI.Image sickIcon;
        public TextMeshProUGUI sickText;
        public UnityEngine.UI.Image lostIcon;
        public TextMeshProUGUI lostText;
        [Header("Other")]
        public TextMeshProUGUI hintText;
        public TextMeshProUGUI bannerText;
        public GameObject introPanel;
        public GameObject endPanel;
        public TextMeshProUGUI endTitle;
        [Tooltip("Line under the end title (like the title screen's subtitle).")]
        public TextMeshProUGUI endSubtitle;
        public TextMeshProUGUI endBody;
        public UnityEngine.UI.Button playButton;
        public UnityEngine.UI.Button againButton;
        [Tooltip("Optional frame behind the banner text; shown and hidden with it.")]
        public GameObject bannerRoot;
        [Tooltip("Status panel, minimap, hint and arrows; faded out while the title screen is up.")]
        public CanvasGroup gameplayGroup;

        private Coroutine bannerRoutine;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(() => { if (GameManager.I != null) GameManager.I.StartGame(); });
            if (againButton != null) againButton.onClick.AddListener(() => { if (GameManager.I != null) GameManager.I.ReturnToMenu(); });
            // The hint line at the bottom was hard to read and the banners already say it.
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (bannerText != null) BannerObject.SetActive(false);
            if (endPanel != null) endPanel.SetActive(false);
            BuildPauseUi();
            var rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) HowToPlay.Install(this, rootCanvas);
            // The town panel shows a portrait per villager instead of the three counters.
            // (the counters sit in "Counter N" cells inside the "Town Panel": take over the panel itself)
            RectTransform townPanel = null;
            for (var t = aliveText != null ? aliveText.transform : null; t != null && t != transform; t = t.parent)
                if (t.name == "Town Panel") { townPanel = t as RectTransform; break; }
            if (townPanel == null && aliveText != null && aliveText.transform.parent != null)
                townPanel = aliveText.transform.parent.parent as RectTransform;
            if (townPanel != null) TownRoster.Install(townPanel);
        }

        // ---------- Pause ----------

        private GameObject pauseOverlay;
        private GameObject pauseButton;

        public void ShowPause(bool paused)
        {
            if (pauseOverlay != null) pauseOverlay.SetActive(paused);
        }

        // Built in code from the HUD's own pieces (panel frame, font, the end screen's button) so it
        // matches the rest of the HUD without rebuilding it.
        private void BuildPauseUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || waveText == null) return;
            var frame = waveText.GetComponentInParent<UnityEngine.UI.Image>();
            var font = waveText.font;

            // Small pause button to the right of the wave panel. It lives on the canvas itself: the
            // gameplay group doesn't take clicks (so the HUD never steals them from the game).
            pauseButton = NewPanel("Pause Button", canvas.transform, frame);
            var brt = (RectTransform)pauseButton.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 1f);
            var frameRt = frame != null ? frame.rectTransform : null;
            float x = frameRt != null ? frameRt.anchoredPosition.x + frameRt.sizeDelta.x + 12f : 246f;
            float y = frameRt != null ? frameRt.anchoredPosition.y : -20f;
            float s = frameRt != null ? frameRt.sizeDelta.y : 64f;
            brt.anchoredPosition = new Vector2(x, y);
            brt.sizeDelta = new Vector2(s, s);
            NewText("Icon", pauseButton.transform, "II", font, waveText.fontSize, waveText.color);
            var button = pauseButton.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => { if (GameManager.I != null) GameManager.I.TogglePause(); });

            // Overlay in the title screen's style: a light dark backdrop, PAUSED as the big title,
            // the key strip ([ESC] OR [P] TO CONTINUE) and two buttons where PLAY sits.
            pauseOverlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var ort = (RectTransform)pauseOverlay.transform;
            ort.SetParent(canvas.transform, false);
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = ort.offsetMax = Vector2.zero;
            pauseOverlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.4f);

            if (endTitle != null)
            {
                var title = Instantiate(endTitle.gameObject, pauseOverlay.transform, false);
                title.name = "Title";
                title.SetActive(true);
                title.GetComponent<TextMeshProUGUI>().text = "PAUSED";
            }

            BuildPauseKeys(font);

            if (againButton != null)
            {
                var anchorFrom = playButton != null ? (RectTransform)playButton.transform : (RectTransform)againButton.transform;
                PauseMenuButton(pauseOverlay.transform, "Resume", anchorFrom, new Vector2(-190f, 0f), () => GameManager.I?.SetPaused(false));
                PauseMenuButton(pauseOverlay.transform, "Main Menu", anchorFrom, new Vector2(190f, 0f), () => GameManager.I?.ReturnToMenu());
            }
            pauseOverlay.transform.SetAsLastSibling();
            pauseOverlay.SetActive(false);
        }

        // [ESC] OR [P] TO CONTINUE on a dark strip, using the title screen's key sheets and placement.
        private void BuildPauseKeys(TMP_FontAsset font)
        {
            Texture letters = null, symbols = null;
            RectTransform titleStrip = null;
            if (introPanel != null)
            {
                foreach (var raw in introPanel.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
                {
                    if (raw.texture == null) continue;
                    if (raw.texture.name.Contains("alphanumeric")) letters = raw.texture;
                    else if (raw.texture.name.Contains("symbols")) symbols = raw.texture;
                }
                var controls = introPanel.transform.Find("Controls");
                if (controls != null) titleStrip = (RectTransform)controls;
            }

            var strip = new GameObject("Keys", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var srt = (RectTransform)strip.transform;
            srt.SetParent(pauseOverlay.transform, false);
            if (titleStrip != null)
            {
                srt.anchorMin = titleStrip.anchorMin;
                srt.anchorMax = titleStrip.anchorMax;
                srt.pivot = titleStrip.pivot;
                srt.anchoredPosition = titleStrip.anchoredPosition;
            }
            else
            {
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            }
            srt.sizeDelta = new Vector2(760f, 110f);
            strip.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var row = strip.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 12f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            const float keyHeight = 64f;
            if (symbols != null) PauseKey(srt, symbols, 0, new RectInt(2, 2, 12, 11), keyHeight, 0f);      // ESC
            PauseLabel(srt, "OR", font, 28f);
            if (letters != null) PauseKey(srt, letters, 'P' - 'A', new RectInt(3, 2, 9, 11), keyHeight, 0.8f);
            PauseLabel(srt, "TO CONTINUE", font, 36f);
        }

        private static void PauseKey(Transform row, Texture sheet, int sheetRow, RectInt art, float height, float phase)
        {
            const int cell = 16;
            var go = new GameObject("Key", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var raw = go.AddComponent<UnityEngine.UI.RawImage>();
            raw.texture = sheet;
            raw.raycastTarget = false;
            Rect Uv(int col) => new Rect((col * cell + art.x) / (float)sheet.width, 1f - (sheetRow * cell + art.y + art.height) / (float)sheet.height,
                art.width / (float)sheet.width, art.height / (float)sheet.height);
            var press = go.AddComponent<KeyPress>();
            press.normalUv = Uv(0);
            press.pressedUv = Uv(2);
            press.phase = phase;
            press.period = 1.6f;
            raw.uvRect = press.normalUv;
            var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = height * art.width / art.height;
        }

        private static void PauseLabel(Transform row, string text, TMP_FontAsset font, float size)
        {
            var label = NewText(text, row, text, font, size, Color.white);
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void PauseMenuButton(Transform parent, string label, RectTransform placeLike, Vector2 offset, UnityEngine.Events.UnityAction onClick)
        {
            var go = Instantiate(againButton.gameObject, parent, false);
            go.name = label + " Button";
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = placeLike.anchorMin;
            rt.anchorMax = placeLike.anchorMax;
            rt.pivot = placeLike.pivot;
            rt.anchoredPosition = placeLike.anchoredPosition + offset;
            rt.sizeDelta = new Vector2(340f, 87f);
            var b = go.GetComponent<UnityEngine.UI.Button>();
            b.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            b.onClick.AddListener(onClick);
            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label.ToUpperInvariant();
        }

        private static GameObject NewPanel(string name, Transform parent, UnityEngine.UI.Image style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (style != null)
            {
                img.sprite = style.sprite;
                img.type = style.type;
                img.color = style.color;
                img.pixelsPerUnitMultiplier = style.pixelsPerUnitMultiplier;
            }
            return go;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        private void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            if (pauseButton != null) pauseButton.SetActive(gm.State == GameState.Playing);

            if (waveText != null)
                waveText.text = gm.WaveNumber > 0 ? $"WAVE {gm.WaveNumber}/{gm.WaveCount}" : "GET READY";
            if (aliveText != null) aliveText.text = gm.Alive.ToString();
            if (sickText != null) sickText.text = gm.Sick.ToString();
            if (lostText != null) lostText.text = $"{gm.Dead}/{gm.Total}";
            if (villagersText != null)
                villagersText.text = $"Villagers {gm.Alive}   <color=#7CFF6B>Sick {gm.Sick}</color>   <color=#FF8A8A>Lost {gm.Dead}/{gm.Total}</color>";
            if (hintText != null)
            {
                if (gm.State != GameState.Playing) hintText.text = "";
                else if (gm.komur != null && gm.komur.HasMedicine) hintText.text = "Carrying medicine: bring it to a sick villager!";
                else if (gm.BossAlive) hintText.text = "The boss took the Apothecary! Defeat it to free the medicine.";
                else if (gm.Sick > 0) hintText.text = "Someone is sick! Grab medicine at the Apothecary.";
                else hintText.text = "";
            }
        }

        private GameObject BannerObject => bannerRoot != null ? bannerRoot : bannerText.gameObject;

        public void ShowIntro(bool visible)
        {
            if (introPanel != null) introPanel.SetActive(visible);
            if (gameplayGroup != null) gameplayGroup.alpha = visible ? 0f : 1f;
        }

        public void ShowBanner(string text, float duration = 1.6f)
        {
            if (bannerText == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text, duration));
        }

        private IEnumerator BannerRoutine(string text, float duration)
        {
            bannerText.text = text.ToUpperInvariant();
            var banner = BannerObject;
            banner.SetActive(true);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float pop = 1f + 0.25f * Mathf.Max(0f, 1f - t * 5f);
                banner.transform.localScale = Vector3.one * pop;
                yield return null;
            }
            banner.SetActive(false);
        }

        // The gameplay HUD fades away while the ending plays out.
        public void HideGameplay()
        {
            if (gameplayGroup != null) gameplayGroup.alpha = 0f;
            if (bannerText != null) BannerObject.SetActive(false);
        }

        public void ShowEnd(Ending ending, int alive, int grateful, int dead, int total)
        {
            if (endPanel == null) return;
            HideGameplay();
            endPanel.SetActive(true);
            switch (ending)
            {
                case Ending.Loved:
                    SetEnd("THE VILLAGE LOVES YOU", $"{grateful} OF {total} VILLAGERS ADORE KOMUR",
                        "THE PLAGUE IS OVER, AND THE TOWN SAW WHO SAVED IT.\nNOBODY WILL FEAR A BLACK CAT AGAIN.");
                    break;
                case Ending.UnknownHero:
                    SetEnd("AN UNKNOWN HERO", $"{alive} OF {total} VILLAGERS SURVIVED",
                        "THE RATS ARE GONE, BUT THE TOWN STILL BLAMES THE BLACK CAT.\nONLY KOMUR KNOWS WHO REALLY SAVED THEM.");
                    break;
                default:
                    SetEnd("THE PLAGUE WON", $"ALL {total} VILLAGERS WERE LOST",
                        "ONLY THEIR SPIRITS REMAIN IN THE EMPTY TOWN...\nAND EVEN THEY BLAME THE BLACK CAT.");
                    break;
            }
        }

        private void SetEnd(string title, string subtitle, string body)
        {
            if (endTitle != null) endTitle.text = title;
            if (endSubtitle != null) endSubtitle.text = subtitle;
            if (endBody != null) endBody.text = body;
        }
    }
}
