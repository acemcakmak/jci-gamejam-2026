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
        [Tooltip("Placeholder sprite: move and scale it in the scene to place the potion. Hidden in play.")]
        public SpriteRenderer potionPlaceholder;
        [Tooltip("Used only without a placeholder: where the potion floats, relative to the altar sprite's centre.")]
        public Vector2 potionOffset = Vector2.zero;
        private float potionSize = 0.6f;
        private Vector3 placeholderCentre;     // local to this object
        [Tooltip("All In 1 Sprite Shader material for the potion's glowing rim and shine (optional).")]
        public Material potionGlowMaterial;

        public bool IsReady => !Blocked && Time.time >= readyAt;
        public bool Blocked => GameManager.I != null && GameManager.I.BossAlive;
        public Color blockedColor = new Color(1f, 0.25f, 0.2f);
        public int Floor => FloorAgent.FloorOf(gameObject);

        private float readyAt;
        private Potion potion;

        private void Awake()
        {
            if (potionPlaceholder != null)
            {
                if (potionPlaceholder.sprite != null)
                    potionSize = potionPlaceholder.sprite.bounds.size.y * Mathf.Abs(potionPlaceholder.transform.lossyScale.y);
                placeholderCentre = transform.InverseTransformPoint(potionPlaceholder.bounds.center);
                potionPlaceholder.enabled = false;
            }
            if (statusText == null)
            {
                var cross = transform.Find("Cross");
                if (cross != null) statusText = cross.GetComponent<TextMeshPro>();
            }
        }

        private void Update()
        {
            UpdateStatus();
            var state = GameManager.I != null ? GameManager.I.State : GameState.Intro;
            bool over = state == GameState.Won || state == GameState.Lost;
            if (IsReady && potion == null && !over) SpawnPotion();
            else if (over && potion != null) ClearPotion(false);

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
            if (Blocked)
            {
                statusText.text = "X";
                statusText.color = blockedColor;
            }
            else if (IsReady)
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

        // Middle of the altar sprite (its pivot is not its centre).
        public Vector3 AltarCentre
        {
            get
            {
                Bounds altar = new Bounds(transform.position, Vector3.zero);
                bool any = false;
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite == null || sr.name.Contains("Shadow") || sr == potionPlaceholder) continue;
                    if (!any) { altar = sr.bounds; any = true; }
                    else altar.Encapsulate(sr.bounds);
                }
                return new Vector3(altar.center.x, altar.center.y, 0f);
            }
        }

        // The boss smashes the waiting potion; brewing resumes once it is dead.
        public void OnBossArrived() => ClearPotion(true);

        public void ClearPotion(bool smash)
        {
            if (potion == null) return;
            if (smash) GameFeedbacks.Play(FeedbackId.RatHit, potion.transform.position);
            Destroy(potion.gameObject);
            potion = null;
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
                if (sr.sprite == null || sr.name.Contains("Shadow") || sr == potionPlaceholder) continue;
                layer = sr.sortingLayerID;
                order = Mathf.Max(order, sr.sortingOrder);
                if (!any) { altar = sr.bounds; any = true; }
                else altar.Encapsulate(sr.bounds);
            }
            const float lift = 0.3f;     // ground point just below the bottle, so it sorts with the altar
            Vector3 centre = potionPlaceholder != null
                ? transform.TransformPoint(placeholderCentre)
                : altar.center + (Vector3)potionOffset;
            var gm = GameManager.I;
            Material material = gm != null && gm.komur != null && gm.komur.visual != null ? gm.komur.visual.sharedMaterial : null;
            potion = Potion.Create(sprite, material, potionGlowMaterial, centre + Vector3.down * lift, layer, order + 1, lift, potionSize);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
