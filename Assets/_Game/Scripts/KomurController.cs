using UnityEngine;

namespace Komur
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class KomurController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 4.5f;

        [Header("Pounce")]
        public float pounceSpeed = 13f;
        public float pounceDuration = 0.15f;
        public float pounceCooldown = 0.35f;
        public float pounceKillRadius = 0.75f;

        [Header("Medicine")]
        public float deliverRange = 0.9f;
        public GameObject medicineIcon;

        [Header("Visuals")]
        public SpriteFlipbook flipbook;
        public SpriteRenderer visual;

        public bool HasMedicine { get; private set; }
        public bool IsStunned => Time.time < stunnedUntil;

        private Rigidbody2D body;
        private Vector2 input;
        private Vector2 facing = Vector2.right;
        private float pounceEndsAt;
        private float nextPounceAt;
        private float stunnedUntil;
        private bool IsPouncing => Time.time < pounceEndsAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (medicineIcon != null) medicineIcon.SetActive(false);
        }

        private void Update()
        {
            var gm = GameManager.I;
            bool playing = gm == null || gm.State == GameState.Playing;

            input = playing && !IsStunned
                ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized
                : Vector2.zero;
            if (input.sqrMagnitude > 0.01f) facing = input;

            if (playing && !IsStunned && Input.GetKeyDown(KeyCode.Space) && Time.time >= nextPounceAt)
            {
                pounceEndsAt = Time.time + pounceDuration;
                nextPounceAt = Time.time + pounceCooldown;
                GameFeedbacks.Play(FeedbackId.Pounce, transform.position);
            }

            if (gm != null && playing && HasMedicine) TryDeliverMedicine();
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (IsPouncing)
            {
                body.linearVelocity = facing * pounceSpeed;
                KillRatsInRange();
            }
            else
            {
                body.linearVelocity = input * moveSpeed;
            }
        }

        private void KillRatsInRange()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float r2 = pounceKillRadius * pounceKillRadius;
            for (int i = gm.rats.Count - 1; i >= 0; i--)
            {
                var rat = gm.rats[i];
                if ((rat.transform.position - transform.position).sqrMagnitude <= r2) rat.Kill();
            }
        }

        private void TryDeliverMedicine()
        {
            float r2 = deliverRange * deliverRange;
            foreach (var v in GameManager.I.villagers)
            {
                if (v.State != VillagerState.Sick) continue;
                if ((v.transform.position - transform.position).sqrMagnitude > r2) continue;
                v.Cure();
                SetMedicine(false);
                return;
            }
        }

        public void SetMedicine(bool has)
        {
            HasMedicine = has;
            if (medicineIcon != null) medicineIcon.SetActive(has);
            if (has) GameFeedbacks.Play(FeedbackId.MedicinePicked, transform.position);
        }

        public void Stun(float duration)
        {
            if (IsStunned) return;
            stunnedUntil = Time.time + duration;
            pounceEndsAt = 0f;
            GameFeedbacks.Play(FeedbackId.Stunned, transform.position);
        }

        private void UpdateVisuals()
        {
            if (visual != null && Mathf.Abs(facing.x) > 0.01f) visual.flipX = facing.x < 0f;
            if (visual != null) visual.color = IsStunned && Mathf.Repeat(Time.time * 12f, 1f) > 0.5f
                ? new Color(1f, 1f, 1f, 0.4f)
                : Color.white;

            if (flipbook == null) return;
            if (IsPouncing) flipbook.Play("Pounce");
            else if (IsStunned) flipbook.Play("Stunned");
            else if (input.sqrMagnitude > 0.01f) flipbook.Play("Run");
            else flipbook.Play("Idle");
        }
    }
}
