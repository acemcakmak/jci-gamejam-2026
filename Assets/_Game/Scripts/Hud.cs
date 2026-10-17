using System.Collections;
using TMPro;
using UnityEngine;

namespace Komur
{
    public class Hud : MonoBehaviour
    {
        public TextMeshProUGUI waveText;
        public TextMeshProUGUI villagersText;
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
                waveText.text = gm.WaveNumber > 0 ? $"Wave {gm.WaveNumber}/{gm.WaveCount}" : "";
            if (villagersText != null)
                villagersText.text = $"Villagers {gm.Alive}   <color=#7CFF6B>Sick {gm.Sick}</color>   <color=#FF8A8A>Lost {gm.Dead}/{gm.maxDeaths}</color>";
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
            bannerText.text = text;
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

        public void ShowEnd(bool won, int alive, int cured, int dead)
        {
            if (endPanel == null) return;
            endPanel.SetActive(true);
            if (endTitle != null) endTitle.text = won ? "The town is saved!" : "The plague took the town...";
            if (endBody != null)
            {
                endBody.text = won
                    ? $"{alive} villagers survived, {cured} cured by Kömür.\nThey'll never fear a black cat again."
                    : $"{dead} villagers were lost.\nThe townsfolk still blame the black cat...";
                endBody.text += "\n\nPress R or click Again";
            }
        }
    }
}
