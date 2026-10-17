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

        private Coroutine bannerRoutine;

        private void Awake()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
            if (endPanel != null) endPanel.SetActive(false);
        }

        private void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;

            if (waveText != null)
                waveText.text = gm.WaveNumber > 0 ? $"Wave {gm.WaveNumber}/{gm.WaveCount}" : "";
            if (villagersText != null)
                villagersText.text = $"Villagers  {gm.Alive}   Sick  {gm.Sick}   Lost  {gm.Dead}/{gm.maxDeaths}";
            if (hintText != null)
            {
                if (gm.State != GameState.Playing) hintText.text = "";
                else if (gm.komur != null && gm.komur.HasMedicine) hintText.text = "Carrying medicine: bring it to a sick villager!";
                else if (gm.Sick > 0) hintText.text = "Someone is sick! Grab medicine at the Apothecary.";
                else hintText.text = "";
            }
        }

        public void ShowIntro(bool visible)
        {
            if (introPanel != null) introPanel.SetActive(visible);
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
            bannerText.gameObject.SetActive(true);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float pop = 1f + 0.25f * Mathf.Max(0f, 1f - t * 5f);
                bannerText.transform.localScale = Vector3.one * pop;
                yield return null;
            }
            bannerText.gameObject.SetActive(false);
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
                endBody.text += "\n\nPress R to try again";
            }
        }
    }
}
