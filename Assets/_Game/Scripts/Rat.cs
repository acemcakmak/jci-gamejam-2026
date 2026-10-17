using UnityEngine;

namespace Komur
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Rat : MonoBehaviour
    {
        public float speed = 1.6f;
        public float biteRange = 0.45f;
        public float fleeSpeedMultiplier = 1.4f;
        public float retargetInterval = 1f;
        public SpriteRenderer visual;

        private Rigidbody2D body;
        private PathMover mover;
        private Villager target;
        private Vector3 home;
        private bool fleeing;
        private float nextRetargetAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (!TryGetComponent(out mover)) mover = gameObject.AddComponent<PathMover>();
            if (!TryGetComponent<SpriteRenderer>(out _)) gameObject.AddComponent<SpriteRenderer>();
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
                if (toTarget.sqrMagnitude <= biteRange * biteRange && Sight.Clear(transform.position, target.transform.position))
                {
                    target.Infect();
                    GameFeedbacks.Play(FeedbackId.Bite, target.transform.position);
                    StartFleeing(gm);
                    return;
                }
            }

            Vector2 dir = mover.NextDirection();
            body.linearVelocity = dir * currentSpeed;
            if (visual != null && Mathf.Abs(dir.x) > 0.05f) visual.flipX = dir.x < 0f;
        }

        private void StartFleeing(GameManager gm)
        {
            fleeing = true;
            target = null;
            home = gm.NearestRatSpawn(transform.position);
            mover.SetDestination(home, true);
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
