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
            if (againButton != null) againButton.onClick.AddListener(() => { if (GameManager.I != null) GameManager.I.Restart(); });
            if (bannerText != null) BannerObject.SetActive(false);
            if (endPanel != null) endPanel.SetActive(false);
            BuildPauseUi();
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

            // Small pause button to the right of the wave panel.
            var buttonParent = frame != null ? frame.transform.parent : canvas.transform;
            pauseButton = NewPanel("Pause Button", buttonParent, frame);
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

            // Overlay: dim the screen, a framed card with the title and two buttons.
            pauseOverlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var ort = (RectTransform)pauseOverlay.transform;
            ort.SetParent(canvas.transform, false);
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = ort.offsetMax = Vector2.zero;
            pauseOverlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.6f);

            var card = NewPanel("Card", pauseOverlay.transform, frame);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, 40f);
            crt.sizeDelta = new Vector2(520f, 300f);
            var title = NewText("Title", card.transform, "PAUSED", endTitle != null ? endTitle.font : font, 64f, waveText.color);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.55f);
            trt.anchorMax = new Vector2(1f, 1f);
            var hint = NewText("Hint", card.transform, "ESC / P TO RESUME   -   R TO RESTART", font, 22f, waveText.color);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0.38f);
            hrt.anchorMax = new Vector2(1f, 0.55f);

            if (againButton != null)
            {
                PauseMenuButton("Resume", new Vector2(-150f, -150f), () => GameManager.I?.SetPaused(false));
                PauseMenuButton("Restart", new Vector2(150f, -150f), () => GameManager.I?.Restart());
            }
            pauseOverlay.transform.SetAsLastSibling();
            pauseOverlay.SetActive(false);
        }

        private void PauseMenuButton(string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = Instantiate(againButton.gameObject, pauseOverlay.transform, false);
            go.name = label + " Button";
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
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

        public void ShowEnd(Ending ending, int alive, int grateful, int dead, int total)
        {
            if (endPanel == null) return;
            endPanel.SetActive(true);
            switch (ending)
            {
                case Ending.Loved:
                    SetEnd("THE VILLAGE LOVES YOU!",
                        $"{alive} OF {total} VILLAGERS SURVIVED AND {grateful} WERE CURED BY KOMUR.\nTHEY WILL NEVER FEAR A BLACK CAT AGAIN.");
                    break;
                case Ending.UnknownHero:
                    SetEnd("AN UNKNOWN HERO",
                        $"THE RATS ARE GONE AND {alive} OF {total} VILLAGERS SURVIVED,\nBUT ONLY {grateful} OF THEM KNOW WHO SAVED THEM.\nTHE TOWN STILL HATES THE BLACK CAT... YOU SAVED IT ANYWAY.");
                    break;
                default:
                    SetEnd("THE PLAGUE TOOK THE TOWN...",
                        $"ALL {total} VILLAGERS WERE LOST.\nTHE TOWNSFOLK WILL BLAME THE BLACK CAT FOREVER...");
                    break;
            }
        }

        private void SetEnd(string title, string body)
        {
            if (endTitle != null) endTitle.text = title;
            if (endBody != null) endBody.text = body;
        }
    }
}
