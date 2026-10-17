using TMPro;
using UnityEngine;

namespace Komur
{
    public class Apothecary : MonoBehaviour
    {
        public float pickupRange = 1.3f;
        [Tooltip("Seconds to brew the next dose after one is taken.")]
        public float brewTime = 2f;
        public TextMeshPro statusText;
        public Color readyColor = new Color(0.3f, 1f, 0.4f);
        public Color brewingColor = new Color(1f, 0.85f, 0.3f);

        [Header("Potion")]
        [Tooltip("One is picked at random for every dose (looks only; they all cure the same).")]
        public Sprite[] potionSprites;
        [Tooltip("Where the potion floats, relative to the centre of the altar sprite.")]
        public Vector2 potionOffset = new Vector2(0f, 0.15f);

        public bool IsReady => Time.time >= readyAt;
        public int Floor => FloorAgent.FloorOf(gameObject);

        private float readyAt;
        private Potion potion;

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
            if (IsReady && potion == null) SpawnPotion();

            var gm = GameManager.I;
            if (gm == null || !gm.IsSimulating || gm.komur == null) return;
            if (gm.komur.HasMedicine || !IsReady || gm.komur.Floor != Floor) return;
            if ((gm.komur.transform.position - transform.position).sqrMagnitude <= pickupRange * pickupRange)
            {
                gm.komur.TakePotion(potion);
                potion = null;
                readyAt = Time.time + brewTime;
            }
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            if (IsReady)
            {
                statusText.text = potion != null ? "" : "+";      // the glowing potion is the sign
                statusText.color = readyColor;
            }
            else
            {
                statusText.text = Mathf.CeilToInt(readyAt - Time.time).ToString();
                statusText.color = brewingColor;
            }
        }

        private void SpawnPotion()
        {
            if (potionSprites == null || potionSprites.Length == 0) return;
            var sprite = potionSprites[Random.Range(0, potionSprites.Length)];
            if (sprite == null) return;
            // Float over the middle of the altar sprite (its pivot is not its centre), drawn above it.
            int layer = SortingLayer.NameToID(FloorAgent.LayerName(Floor)), order = 0;
            Bounds altar = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.sprite == null || sr.name.Contains("Shadow")) continue;
                layer = sr.sortingLayerID;
                order = Mathf.Max(order, sr.sortingOrder);
                if (!any) { altar = sr.bounds; any = true; }
                else altar.Encapsulate(sr.bounds);
            }
            const float lift = 0.3f;     // ground point just below the bottle, so it sorts with the altar
            Vector3 centre = altar.center + (Vector3)potionOffset;
            var gm = GameManager.I;
            Material material = gm != null && gm.komur != null && gm.komur.visual != null ? gm.komur.visual.sharedMaterial : null;
            potion = Potion.Create(sprite, material, centre + Vector3.down * lift, layer, order + 1, lift);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
