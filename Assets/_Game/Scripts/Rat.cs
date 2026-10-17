using System.Collections;
using UnityEngine;

namespace Komur
{
    public enum RatKind { Normal, Fast, King }

    [RequireComponent(typeof(Rigidbody2D))]
    public class Rat : MonoBehaviour
    {
        public float speed = 1.6f;
        public float biteRange = 0.55f;
        public float fleeSpeedMultiplier = 1.4f;
        public float retargetInterval = 1f;
        public SpriteRenderer visual;
        public SpriteFlipbook flipbook;
        [Tooltip("Prefab is the dedicated boss (Plague Slime): its look comes from the prefab, not from tinting.")]
        public bool isBoss;
        [Tooltip("Seconds a rat waits for a reachable target before giving up and leaving.")]
        public float giveUpAfter = 4f;

        [Header("Variants")]
        public Color fastTint = new Color(0.75f, 0.25f, 0.2f);
        public Color kingTint = new Color(0.3f, 0.1f, 0.35f);
        public int kingHealth = 3;
        public float kingBiteRadius = 1.6f;

        [Header("Health and death")]
        public int health = 1;
        public WorldBar healthBar;
        public float hitFlashTime = 0.15f;
        public float hitStunTime = 0.25f;
        public float deathFadeTime = 0.5f;
        [Tooltip("Looping plague effect (Epic Toon FX); stopped when the rat dies.")]
        public ParticleSystem plagueEffect;

        public RatKind Kind { get; private set; } = RatKind.Normal;
        public int Floor => FloorAgent.FloorOf(gameObject);

        private Rigidbody2D body;
        private PathMover mover;
        private FloorAgent floorAgent;
        private Villager target;
        private Vector3 home;
        private int homeFloor = 1;
        private bool fleeing;
        private float nextRetargetAt;
        private float invulnerableUntil;
        private int maxHealth = 1;
        private bool dying;
        private float hitFlashUntil;
        private float hitStunUntil;
        private MaterialPropertyBlock block;
        private static readonly int HitBlend = Shader.PropertyToID("_HitEffectBlend");
        private static readonly int FadeAmount = Shader.PropertyToID("_FadeAmount");

        public bool IsDying => dying;
        private float noTargetSince = -1f;
        private float bitingUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (!TryGetComponent(out mover)) mover = gameObject.AddComponent<PathMover>();
            if (!TryGetComponent<SpriteRenderer>(out _)) gameObject.AddComponent<SpriteRenderer>();
            if (!TryGetComponent(out floorAgent)) floorAgent = gameObject.AddComponent<FloorAgent>();
            home = transform.position;
        }

        private void OnEnable()
        {
            if (GameManager.I != null) GameManager.I.rats.Add(this);
        }

        private void OnDisable()
        {
            if (GameManager.I != null) GameManager.I.rats.Remove(this);
        }

        public void Init(Vector3 homePosition, float moveSpeed, RatKind kind, int floor)
        {
            home = homePosition;
            homeFloor = floor;
            speed = moveSpeed;
            Kind = kind;
            floorAgent.SetFloor(floor);

            switch (kind)
            {
                case RatKind.Fast:
                    speed *= 1.5f;
                    if (visual != null)
                    {
                        visual.color = fastTint;
                        visual.transform.localScale *= 0.8f;
                    }
                    break;
                case RatKind.King:
                    speed *= 0.75f;
                    health = Mathf.Max(health, kingHealth);
                    if (visual != null && !isBoss)
                    {
                        visual.color = kingTint;
                        visual.transform.localScale *= 2.2f;
                    }
                    break;
            }
            maxHealth = Mathf.Max(1, health);
            if (healthBar != null) healthBar.SetFill(1f);
        }

        private void Update()
        {
            if (visual == null) return;
            block ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(block);
            block.SetFloat(HitBlend, Mathf.Clamp01((hitFlashUntil - Time.time) / Mathf.Max(0.01f, hitFlashTime)));
            visual.SetPropertyBlock(block);
        }

        private void FixedUpdate()
        {
            if (dying) return;
            var gm = GameManager.I;
            if (gm == null || !gm.IsSimulating || Time.time < hitStunUntil)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float currentSpeed = speed;

            if (fleeing)
            {
                currentSpeed *= fleeSpeedMultiplier;
                mover.SetDestination(home, false, homeFloor);
                if ((home - transform.position).sqrMagnitude < 0.09f || mover.Arrived)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                bool waiting = target == null && noTargetSince >= 0f && Time.time < nextRetargetAt;
                if (waiting)
                {
                    body.linearVelocity = Vector2.zero;
                    Animate(Vector2.zero);
                    if (Time.time - noTargetSince > giveUpAfter) StartFleeing(gm);
                    return;
                }
                if (target == null || !target.CanBeBitten || Time.time >= nextRetargetAt)
                {
                    target = gm.FindReachableTarget(this, mover);
                    nextRetargetAt = Time.time + retargetInterval;
                    if (target == null)
                    {
                        // Nobody reachable right now: wait a bit (someone may get cured or come into reach), then leave.
                        if (noTargetSince < 0f) noTargetSince = Time.time;
                        body.linearVelocity = Vector2.zero;
                        Animate(Vector2.zero);
                        if (Time.time - noTargetSince > giveUpAfter) StartFleeing(gm);
                        return;
                    }
                    noTargetSince = -1f;
                }
                else
                {
                    mover.SetDestination(target.transform.position, false, target.Floor);
                }

                Vector3 toTarget = target.transform.position - transform.position;
                if (target.Floor == Floor && toTarget.sqrMagnitude <= biteRange * biteRange && Sight.Clear(transform.position, target.transform.position, Floor))
                {
                    Bite(gm);
                    return;
                }
            }

            Vector2 dir = Time.time < bitingUntil ? Vector2.zero : mover.NextDirection();
            body.linearVelocity = dir * currentSpeed;
            Animate(dir);
        }

        private void Animate(Vector2 dir)
        {
            if (visual != null && Mathf.Abs(dir.x) > 0.05f) visual.flipX = dir.x < 0f;
            if (flipbook == null || Time.time < bitingUntil) return;
            flipbook.Play(dir.sqrMagnitude > 0.01f ? "Run" : "Idle");
        }

        private void PlayBite()
        {
            bitingUntil = Time.time + 0.35f;
            if (flipbook != null) flipbook.Play("Bite", true);
        }

        private void Bite(GameManager gm)
        {
            PlayBite();
            if (Kind == RatKind.King)
            {
                // The Rat King spreads plague to everyone nearby and keeps going.
                float r2 = kingBiteRadius * kingBiteRadius;
                foreach (var v in gm.villagers)
                {
                    if (v.Floor != Floor || !v.CanBeBitten) continue;
                    if ((v.transform.position - transform.position).sqrMagnitude > r2) continue;
                    v.Infect(gm.CurrentSickDuration);
                    GameFeedbacks.Play(FeedbackId.Bite, v.transform.position, v.transform);
                }
                target = null;
                nextRetargetAt = 0f;
                return;
            }

            target.Infect(gm.CurrentSickDuration);
            GameFeedbacks.Play(FeedbackId.Bite, target.transform.position, target.transform);
            StartFleeing(gm);
        }

        private void StartFleeing(GameManager gm)
        {
            fleeing = true;
            target = null;
            // Run back to the hole it came out of (possibly on another floor).
            mover.SetDestination(home, true, homeFloor);
        }

        // Called by Kömür's pounce: flash, knock the health bar down, die at zero. Returns true if the rat died.
        public bool Hit()
        {
            if (dying || Time.time < invulnerableUntil) return false;
            health--;
            hitFlashUntil = Time.time + hitFlashTime;
            hitStunUntil = Time.time + hitStunTime;
            body.linearVelocity = Vector2.zero;
            if (healthBar != null) healthBar.SetFill((float)health / maxHealth);
            if (health <= 0)
            {
                Kill();
                return true;
            }
            invulnerableUntil = Time.time + 0.4f;
            GameFeedbacks.Play(FeedbackId.RatHit, transform.position, transform);
            CameraFollow2D.Shake(0.12f, 0.15f);
            return false;
        }

        // Death: stop counting as a live rat at once, play the death frames, burst, then dissolve.
        public void Kill()
        {
            if (dying) return;
            dying = true;
            if (GameManager.I != null) GameManager.I.rats.Remove(this);
            hitFlashUntil = Time.time + hitFlashTime;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            if (plagueEffect != null) plagueEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (flipbook != null) flipbook.Play("Death", true);
            GameFeedbacks.Play(FeedbackId.RatKilled, transform.position);
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            float clip = flipbook != null ? flipbook.ClipLength("Death") : 0f;
            yield return new WaitForSeconds(Mathf.Max(0.35f, clip));
            if (healthBar != null) healthBar.SetVisible(false);

            float t = 0f;
            Color start = visual != null ? visual.color : Color.white;
            while (t < deathFadeTime && visual != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / deathFadeTime);
                visual.GetPropertyBlock(block ??= new MaterialPropertyBlock());
                block.SetFloat(FadeAmount, k);
                visual.SetPropertyBlock(block);
                // Fallback for materials without the dissolve: fade the sprite out.
                if (!visual.sharedMaterial.HasProperty("_FadeAmount"))
                    visual.color = new Color(start.r, start.g, start.b, 1f - k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
