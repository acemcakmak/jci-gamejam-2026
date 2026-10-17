using UnityEngine;

namespace Komur
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Rat : MonoBehaviour
    {
        public float speed = 1.6f;
        public float biteRange = 0.45f;
        public float fleeSpeedMultiplier = 1.4f;
        public float retargetInterval = 0.5f;
        public SpriteRenderer visual;

        private Rigidbody2D body;
        private Villager target;
        private Vector3 home;
        private bool fleeing;
        private float nextRetargetAt;

        // Simple unstuck: if the rat barely moves, sidestep for a moment.
        private Vector3 lastCheckPos;
        private float nextStuckCheckAt;
        private float sidestepUntil;
        private float sidestepSign = 1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
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

        public void Init(Vector3 homePosition, float moveSpeed)
        {
            home = homePosition;
            speed = moveSpeed;
        }

        private void FixedUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector3 goal;
            float currentSpeed = speed;

            if (fleeing)
            {
                goal = home;
                currentSpeed *= fleeSpeedMultiplier;
                if ((home - transform.position).sqrMagnitude < 0.09f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                if (target == null || !target.CanBeBitten || Time.time >= nextRetargetAt)
                {
                    target = gm.NearestBiteable(transform.position);
                    nextRetargetAt = Time.time + retargetInterval;
                }
                if (target == null)
                {
                    StartFleeing(gm);
                    return;
                }

                goal = target.transform.position;
                if ((goal - transform.position).sqrMagnitude <= biteRange * biteRange)
                {
                    target.Infect();
                    GameFeedbacks.Play(FeedbackId.Bite, target.transform.position);
                    StartFleeing(gm);
                    return;
                }
            }

            Vector2 dir = ((Vector2)(goal - transform.position)).normalized;
            UpdateStuck(dir);
            if (Time.time < sidestepUntil) dir = (dir + new Vector2(-dir.y, dir.x) * sidestepSign * 1.5f).normalized;

            body.linearVelocity = dir * currentSpeed;
            if (visual != null && Mathf.Abs(dir.x) > 0.05f) visual.flipX = dir.x < 0f;
        }

        private void StartFleeing(GameManager gm)
        {
            fleeing = true;
            home = gm.NearestRatSpawn(transform.position);
        }

        private void UpdateStuck(Vector2 dir)
        {
            if (Time.time < nextStuckCheckAt) return;
            if ((transform.position - lastCheckPos).sqrMagnitude < 0.01f)
            {
                sidestepUntil = Time.time + 0.6f;
                sidestepSign = Random.value < 0.5f ? -1f : 1f;
            }
            lastCheckPos = transform.position;
            nextStuckCheckAt = Time.time + 0.4f;
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
