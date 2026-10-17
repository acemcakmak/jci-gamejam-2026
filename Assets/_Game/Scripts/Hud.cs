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
