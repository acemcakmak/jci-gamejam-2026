using TMPro;
using UnityEngine;

namespace Komur
{
    public class Apothecary : MonoBehaviour
    {
        public float pickupRange = 1.3f;
        [Tooltip("Seconds to brew the next dose after one is taken.")]
        public float brewTime = 3f;
        public TextMeshPro statusText;
        public Color readyColor = new Color(0.3f, 1f, 0.4f);
        public Color brewingColor = new Color(1f, 0.85f, 0.3f);

        public bool IsReady => Time.time >= readyAt;
        public int Floor => FloorAgent.FloorOf(gameObject);

        private float readyAt;

        private void Awake()
        {
            if (statusText == null)
            {
                var cross = transform.Find("Cross");
                if (cross != null) statusText = cross.GetComponent<TextMeshPro>();
            }
        }

        private void Update()
        {
            UpdateStatus();

            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || gm.komur == null) return;
            if (gm.komur.HasMedicine || !IsReady || gm.komur.Floor != Floor) return;
            if ((gm.komur.transform.position - transform.position).sqrMagnitude <= pickupRange * pickupRange)
            {
                gm.komur.SetMedicine(true);
                readyAt = Time.time + brewTime;
            }
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            if (IsReady)
            {
                statusText.text = "+";
                statusText.color = readyColor;
            }
            else
            {
                statusText.text = Mathf.CeilToInt(readyAt - Time.time).ToString();
                statusText.color = brewingColor;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
