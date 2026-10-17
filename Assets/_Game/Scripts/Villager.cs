using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Komur
{
    public enum VillagerState { Healthy, Sick, Dead }

    [RequireComponent(typeof(Rigidbody2D))]
    public class Villager : MonoBehaviour
    {
        [Header("Wander")]
        public float walkSpeed = 1.1f;
        public float wanderRadius = 3f;

        [Header("Sickness")]
        public float sickDuration = 25f;
        public Color sickTint = new Color(0.55f, 1f, 0.45f);

        [Header("Fear of black cats")]
        public bool fearEnabled = true;
        public float fearRadius = 2.2f;
        public float fearBuildTime = 0.8f;
        public float fleeDuration = 1.5f;
        public float fleeSpeed = 2.4f;
        public float throwCooldown = 3f;
        public float stunDuration = 1f;
        public ThrownStone stonePrefab;

        [Header("Fear of rats")]
        [Tooltip("Healthy villagers back away from rats within this radius, a bit slower than the rats.")]
        public float ratFearRadius = 2.6f;
        public float ratFleeSpeed = 1.3f;

        [Header("References")]
        public Animator animator;
        public WorldBar sickBar;
        public TextMeshPro icon;
        public GameObject gravestonePrefab;
        public GameObject visualRoot;

        public VillagerState State { get; private set; } = VillagerState.Healthy;
        public bool Friendly { get; private set; }
        public bool CanBeBitten => State == VillagerState.Healthy;
        public int Floor => FloorAgent.FloorOf(gameObject);
        // 1 = just bitten, 0 = about to die. Only meaningful while sick.
        public float SickFraction => State == VillagerState.Sick ? Mathf.Clamp01(sickTimeLeft / sickDuration) : 1f;
        private NavGrid Grid => NavGrid.For(Floor);

        private Rigidbody2D body;
        private PathMover mover;
        private readonly List<SpriteRenderer> tintables = new List<SpriteRenderer>();
        private Vector3 home;
        private float idleUntil;
        private float sickTimeLeft;
        private float fear;
        private float fleeUntil;
        private float nextThrowAt;
        private float iconHideAt;
        private float nextRatCheckAt;
        private Rat nearbyRat;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (!TryGetComponent(out mover)) mover = gameObject.AddComponent<PathMover>();
            if (!TryGetComponent<SpriteRenderer>(out _)) gameObject.AddComponent<SpriteRenderer>();
            if (!TryGetComponent<FloorAgent>(out _)) gameObject.AddComponent<FloorAgent>();
            home = transform.position;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sickBar != null && sr.transform.IsChildOf(sickBar.transform)) continue;
                if (sr.name == "Shadow") continue;
                tintables.Add(sr);
            }
            if (sickBar != null) sickBar.SetVisible(false);
            SetIcon("", Color.white);
        }

        private void Start()
        {
            // Make sure we start on reachable ground (NavGrid builds in its Start, which runs first).
            var grid = Grid;
            if (grid != null && !grid.IsWalkable(transform.position))
            {
                transform.position = grid.SnapToWalkable(transform.position);
                home = transform.position;
            }
        }

        private void OnEnable()
        {
            if (GameManager.I != null) GameManager.I.villagers.Add(this);
        }

        private void OnDisable()
        {
            if (GameManager.I != null) GameManager.I.villagers.Remove(this);
        }

        private void Update()
        {
            var gm = GameManager.I;
            bool playing = gm == null || gm.IsSimulating;

            if (State == VillagerState.Sick && playing)
            {
                sickTimeLeft -= Time.deltaTime;
                float t = sickTimeLeft / sickDuration;
                if (sickBar != null) sickBar.SetFill(t);
                SetTint(Color.Lerp(sickTint * 0.8f, sickTint, Mathf.PingPong(Time.time * (t < 0.3f ? 6f : 2f), 1f)));
                if (sickTimeLeft <= 0f) Die();
            }

            if (State == VillagerState.Healthy && playing && fearEnabled && !Friendly) UpdateFear(gm);

            if (iconHideAt > 0f && Time.time >= iconHideAt)
            {
                iconHideAt = 0f;
                SetIcon(State == VillagerState.Sick ? "+" : "", Color.green);
            }
        }

        private void UpdateFear(GameManager gm)
        {
            if (gm == null || gm.komur == null) return;
            Vector3 catPos = gm.komur.transform.position;
            Vector3 eyes = transform.position + Vector3.up * 0.5f;
            float radius = fearRadius * gm.PanicMultiplier;
            bool near = (catPos - transform.position).sqrMagnitude < radius * radius
                        && gm.komur.Floor == Floor
                        && Sight.Clear(eyes, catPos, Floor);

            fear = Mathf.Clamp(fear + (near ? Time.deltaTime : -Time.deltaTime), 0f, fearBuildTime);
            if (Time.time >= fleeUntil && iconHideAt <= 0f)
                SetIcon(fear > 0.05f ? "!" : "", Color.Lerp(Color.yellow, Color.red, fear / fearBuildTime));

            if (fear >= fearBuildTime)
            {
                fear = 0f;
                fleeUntil = Time.time + fleeDuration;
                Vector2 away = (transform.position - catPos).normalized;
                Vector2 fleeTo = (Vector2)transform.position + away * 4f;
                if (Grid != null && Grid.TryNearestWalkable(fleeTo, 6, out var safe)) fleeTo = safe;
                mover.SetDestination(fleeTo, true);
                GameFeedbacks.Play(FeedbackId.VillagerScared, transform.position, transform);
                ShowIconFor("!", Color.red, fleeDuration);
                if (stonePrefab != null && Time.time >= nextThrowAt)
                {
                    nextThrowAt = Time.time + throwCooldown;
                    var stone = Instantiate(stonePrefab, eyes, Quaternion.identity);
                    stone.Launch(gm.komur, stunDuration);
                }
            }
        }

        private void FixedUpdate()
        {
            var gm = GameManager.I;
            bool playing = gm == null || gm.IsSimulating;
            Vector2 velocity = Vector2.zero;

            if (playing && State == VillagerState.Healthy)
            {
                bool fleeingNow = Time.time < fleeUntil;
                if (!fleeingNow && Time.time >= nextRatCheckAt)
                {
                    nextRatCheckAt = Time.time + 0.3f;
                    nearbyRat = NearestRat(gm);
                    if (nearbyRat != null)
                    {
                        Vector2 away = (transform.position - nearbyRat.transform.position).normalized;
                        Vector2 runTo = (Vector2)transform.position + away * 3f;
                        if (Grid != null && Grid.TryNearestWalkable(runTo, 6, out var safe)) runTo = safe;
                        mover.SetDestination(runTo);
                    }
                }
                if (!fleeingNow && nearbyRat != null)
                {
                    velocity = mover.NextDirection() * ratFleeSpeed;
                }
                else if (!fleeingNow && Time.time >= idleUntil && (!mover.HasDestination || mover.Arrived))
                {
                    if (mover.HasDestination)
                    {
                        mover.Stop();
                        idleUntil = Time.time + Random.Range(1f, 3f);
                    }
                    else if (Grid != null && Grid.TryRandomWalkableNear(home, wanderRadius, out var goal))
                    {
                        if (!mover.SetDestination(goal, true)) mover.Stop();
                    }
                }
                if ((fleeingNow || nearbyRat == null) && (fleeingNow || Time.time >= idleUntil))
                    velocity = mover.NextDirection() * (fleeingNow ? fleeSpeed : walkSpeed);
            }
            else
            {
                mover.Stop();
            }

            body.linearVelocity = velocity;
            UpdateAnimator(velocity);
        }

        private Rat NearestRat(GameManager gm)
        {
            if (gm == null) return null;
            Rat best = null;
            float bestD = ratFearRadius * ratFearRadius;
            int floor = Floor;
            foreach (var rat in gm.rats)
            {
                if (rat.Floor != floor) continue;
                float d = (rat.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = rat; }
            }
            return best;
        }

        private void UpdateAnimator(Vector2 velocity)
        {
            if (animator == null) return;
            bool moving = velocity.sqrMagnitude > 0.01f;
            animator.SetBool("IsMoving", moving);
            if (!moving) return;
            if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y)) animator.SetInteger("Direction", velocity.x > 0f ? 2 : 3);
            else animator.SetInteger("Direction", velocity.y > 0f ? 1 : 0);
        }

        public void Infect(float duration)
        {
            if (State != VillagerState.Healthy) return;
            State = VillagerState.Sick;
            sickDuration = duration;
            sickTimeLeft = sickDuration;
            fear = 0f;
            fleeUntil = 0f;
            if (sickBar != null) { sickBar.SetVisible(true); sickBar.SetFill(1f); }
            SetIcon("+", Color.green);
        }

        public void Cure()
        {
            if (State != VillagerState.Sick) return;
            State = VillagerState.Healthy;
            Friendly = true;
            if (sickBar != null) sickBar.SetVisible(false);
            SetTint(Color.white);
            SetIcon("", Color.white); // the CURED! battle text replaces the old "Thanks!"
            GameFeedbacks.Play(FeedbackId.Cured, transform.position, transform);
            if (GameManager.I != null) GameManager.I.OnVillagerCured();
        }

        private void Die()
        {
            State = VillagerState.Dead;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            if (sickBar != null) sickBar.SetVisible(false);
            SetIcon("", Color.white);
            if (visualRoot != null) visualRoot.SetActive(false);
            if (gravestonePrefab != null)
            {
                var grave = Instantiate(gravestonePrefab, transform.position, Quaternion.identity);
                int layer = LayerMask.NameToLayer(FloorAgent.LayerName(Floor));
                foreach (var t in grave.GetComponentsInChildren<Transform>(true)) if (layer >= 0) t.gameObject.layer = layer;
                foreach (var r in grave.GetComponentsInChildren<Renderer>(true)) r.sortingLayerName = FloorAgent.LayerName(Floor);
            }
            GameFeedbacks.Play(FeedbackId.VillagerDied, transform.position);
            if (GameManager.I != null) GameManager.I.OnVillagerDied();
        }

        private void SetTint(Color color)
        {
            foreach (var sr in tintables) sr.color = color;
        }

        private void SetIcon(string text, Color color)
        {
            if (icon == null) return;
            icon.text = text;
            icon.color = color;
        }

        private void ShowIconFor(string text, Color color, float seconds)
        {
            SetIcon(text, color);
            iconHideAt = Time.time + seconds;
        }
    }
}
