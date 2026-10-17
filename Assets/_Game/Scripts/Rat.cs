using UnityEngine;

namespace Komur
{
    public enum RatKind { Normal, Fast, King }

    [RequireComponent(typeof(Rigidbody2D))]
    public class Rat : MonoBehaviour
    {
        public float speed = 1.6f;
        public float biteRange = 0.45f;
        public float fleeSpeedMultiplier = 1.4f;
        public float retargetInterval = 1f;
        public SpriteRenderer visual;

        [Header("Variants")]
        public Color fastTint = new Color(0.75f, 0.25f, 0.2f);
        public Color kingTint = new Color(0.3f, 0.1f, 0.35f);
        public int kingHealth = 3;
        public float kingBiteRadius = 1.6f;

        public RatKind Kind { get; private set; } = RatKind.Normal;
        public int Floor => FloorAgent.FloorOf(gameObject);

        private Rigidbody2D body;
        private PathMover mover;
        private FloorAgent floorAgent;
        private Villager target;
        private Vector3 home;
        private bool fleeing;
        private float nextRetargetAt;
        private int health = 1;
        private float invulnerableUntil;

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
                    health = kingHealth;
                    if (visual != null)
                    {
                        visual.color = kingTint;
                        visual.transform.localScale *= 2.2f;
                    }
                    break;
            }
        }

        private void FixedUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float currentSpeed = speed;

            if (fleeing)
            {
                currentSpeed *= fleeSpeedMultiplier;
                mover.SetDestination(home);
                if ((home - transform.position).sqrMagnitude < 0.09f || mover.Arrived)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                if (target == null || !target.CanBeBitten || Time.time >= nextRetargetAt)
                {
                    target = gm.FindReachableTarget(this, mover);
                    nextRetargetAt = Time.time + retargetInterval;
                    if (target == null)
                    {
                        StartFleeing(gm);
                        return;
                    }
                }
                else
                {
                    mover.SetDestination(target.transform.position);
                }

                Vector3 toTarget = target.transform.position - transform.position;
                if (toTarget.sqrMagnitude <= biteRange * biteRange && Sight.Clear(transform.position, target.transform.position, Floor))
                {
                    Bite(gm);
                    return;
                }
            }

            Vector2 dir = mover.NextDirection();
            body.linearVelocity = dir * currentSpeed;
            if (visual != null && Mathf.Abs(dir.x) > 0.05f) visual.flipX = dir.x < 0f;
        }

        private void Bite(GameManager gm)
        {
            if (Kind == RatKind.King)
            {
                // The Rat King spreads plague to everyone nearby and keeps going.
                float r2 = kingBiteRadius * kingBiteRadius;
                foreach (var v in gm.villagers)
                {
                    if (v.Floor != Floor || !v.CanBeBitten) continue;
                    if ((v.transform.position - transform.position).sqrMagnitude > r2) continue;
                    v.Infect(gm.CurrentSickDuration);
                    GameFeedbacks.Play(FeedbackId.Bite, v.transform.position);
                }
                target = null;
                nextRetargetAt = 0f;
                return;
            }

            target.Infect(gm.CurrentSickDuration);
            GameFeedbacks.Play(FeedbackId.Bite, target.transform.position);
            StartFleeing(gm);
        }

        private void StartFleeing(GameManager gm)
        {
            fleeing = true;
            target = null;
            home = gm.NearestRatHole(transform.position, Floor);
            mover.SetDestination(home, true);
        }

        // Called by Kömür's pounce. Returns true if the rat died.
        public bool Hit()
        {
            if (Time.time < invulnerableUntil) return false;
            health--;
            if (health <= 0)
            {
                Kill();
                return true;
            }
            invulnerableUntil = Time.time + 0.4f;
            GameFeedbacks.Play(FeedbackId.RatKilled, transform.position);
            CameraFollow2D.Shake(0.12f, 0.15f);
            return false;
        }

        public void Kill()
        {
            if (!gameObject.activeSelf) return;
            GameFeedbacks.Play(FeedbackId.RatKilled, transform.position);
            gameObject.SetActive(false); // unregisters immediately via OnDisable
            Destroy(gameObject);
        }
    }
}
