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
        [Tooltip("Sick villagers still shuffle about, slowly.")]
        public float sickWalkSpeed = 0.6f;
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
        [Tooltip("Minifolk sheets; each villager gets a different one. Empty = keep the visual as built.")]
        public VillagerLooks looks;
        public WorldBar sickBar;
        public TextMeshPro icon;
        public GameObject gravestonePrefab;
        [Tooltip("Ghost look for the villager's spirit (All In 1 'Ghost'); without it a gravestone is left.")]
        public Material ghostMaterial;
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
            if (visualRoot != null)
            {
                visualSprite = visualRoot.GetComponent<SpriteRenderer>();
                if (looks != null && visualRoot.TryGetComponent(out flipbook))
                {
                    var look = looks.Draw();
                    if (look != null)
                    {
                        flipbook.clips = looks.Clips(look);
                        if (visualSprite != null) visualSprite.sprite = flipbook.clips[0].frames[0];
                        if (animator != null) animator.enabled = false;
                    }
                    else flipbook = null;
                }
            }
            CharacterSorting.Ensure(gameObject, visualSprite);
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

        // ---------- End of the game: the circle around Kömür ----------

        public enum EndMood { Calm, Angry, Loving }

        private bool inEnding;
        private EndMood endMood;
        private Vector3? endSlot;
        private Vector3 endLookAt;
        private float nextEndActionAt;
        private VillagerGhost spirit;

        // Stand at the given spot facing Kömür (spirits float there instead) and act out the ending.
        public void EnterEnding(Vector3 slot, int floor, Vector3 lookAt, EndMood mood)
        {
            inEnding = true;
            endMood = mood;
            endSlot = slot;
            endLookAt = lookAt;
            nextEndActionAt = Time.time + Random.Range(0.6f, 1.6f);
            if (State == VillagerState.Dead)
            {
                if (spirit != null) spirit.MoveTo(slot);     // otherwise BecomeGrave puts it there
                return;
            }
            mover.Stop();
            body.linearVelocity = Vector2.zero;
            body.position = slot;
            transform.position = slot;
            if (TryGetComponent(out FloorAgent agent)) agent.SetFloor(floor);
            if (visualSprite != null) visualSprite.flipX = lookAt.x < slot.x;
            if (flipbook != null) flipbook.Play("Idle");
            fleeUntil = 0f;
            fear = 0f;
            shownAnger = -1f;
            GameFeedbacks.Play(FeedbackId.RatDig, slot);      // a little poof as they arrive
        }

        private void UpdateEnding()
        {
            if (State == VillagerState.Dead || Time.time < nextEndActionAt) return;
            switch (endMood)
            {
                case EndMood.Angry:
                    nextEndActionAt = Time.time + Random.Range(1.4f, 2.6f);
                    var gm = GameManager.I;
                    if (stonePrefab != null && gm != null && gm.komur != null)
                    {
                        if (flipbook != null) PlayOnce("Hurt");
                        var stone = Instantiate(stonePrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                        stone.Launch(gm.komur, 0.4f, Floor, visualSprite != null ? visualSprite.sharedMaterial : null);
                    }
                    break;
                case EndMood.Loving:
                    nextEndActionAt = Time.time + Random.Range(0.7f, 1.4f);
                    GameFeedbacks.Play(FeedbackId.Love, transform.position + new Vector3(Random.Range(-0.25f, 0.25f), 0.9f, 0f));
                    break;
            }
        }

        private void Update()
        {
            var gm = GameManager.I;
            bool playing = gm == null || gm.IsSimulating;
            if (inEnding)
            {
                UpdateEnding();
                UpdateAnger();
                return;
            }

            if (State == VillagerState.Sick && playing)
            {
                sickTimeLeft -= Time.deltaTime;
                float t = sickTimeLeft / sickDuration;
                if (sickBar != null) sickBar.SetFill(t);
                SetTint(Color.Lerp(sickTint * 0.8f, sickTint, Mathf.PingPong(Time.time * (t < 0.3f ? 6f : 2f), 1f)));
                if (sickTimeLeft <= 0f) Die();
            }

            if (State == VillagerState.Healthy && playing && fearEnabled && !Friendly) UpdateFear(gm);
            UpdateAnger();

            if (iconHideAt > 0f && Time.time >= iconHideAt)
            {
                iconHideAt = 0f;
                SetIcon("", Color.green);
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

            if (fear >= fearBuildTime)
            {
                fear = 0f;
                fleeUntil = Time.time + fleeDuration;
                Vector2 away = (transform.position - catPos).normalized;
                Vector2 fleeTo = (Vector2)transform.position + away * 4f;
                if (Grid != null && Grid.TryNearestWalkable(fleeTo, 6, out var safe)) fleeTo = safe;
                mover.SetDestination(fleeTo, true);
                GameFeedbacks.Play(FeedbackId.VillagerScared, transform.position, transform);
                if (stonePrefab != null && Time.time >= nextThrowAt)
                {
                    nextThrowAt = Time.time + throwCooldown;
                    var stone = Instantiate(stonePrefab, eyes, Quaternion.identity);
                    stone.Launch(gm.komur, stunDuration, Floor, visualSprite != null ? visualSprite.sharedMaterial : null);
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
                    else if (PickWanderGoal(out var goal, out int goalFloor))
                    {
                        if (!mover.SetDestination(goal, true, goalFloor)) mover.Stop();
                    }
                }
                if ((fleeingNow || nearbyRat == null) && (fleeingNow || Time.time >= idleUntil))
                    velocity = mover.NextDirection() * (fleeingNow ? fleeSpeed : walkSpeed);
            }
            else if (playing && State == VillagerState.Sick)
            {
                // Feverish shuffling between short rests (no fear of cats or rats any more).
                if (Time.time >= idleUntil && (!mover.HasDestination || mover.Arrived))
                {
                    if (mover.HasDestination)
                    {
                        mover.Stop();
                        idleUntil = Time.time + Random.Range(1.5f, 3.5f);
                    }
                    else if (Grid != null && Grid.TryRandomWalkableNear(home, wanderRadius, out var goal))
                    {
                        if (!mover.SetDestination(goal, true)) mover.Stop();
                    }
                }
                if (Time.time >= idleUntil) velocity = mover.NextDirection() * sickWalkSpeed;
            }
            else
            {
                mover.Stop();
            }

            body.linearVelocity = velocity;
            UpdateAnimator(velocity);
        }

        [Header("Wander between floors")]
        [Range(0f, 1f)] public float otherFloorChance = 0.3f;

        // Mostly stroll near home; sometimes take the stairs to another floor nearby.
        private bool PickWanderGoal(out Vector2 goal, out int goalFloor)
        {
            goalFloor = Floor;
            if (Random.value < otherFloorChance)
            {
                int other = Random.Range(1, 4);
                var otherGrid = NavGrid.For(other);
                if (other != Floor && otherGrid != null && otherGrid.TryRandomWalkableNear(home, wanderRadius * 3f, out goal))
                {
                    goalFloor = other;
                    return true;
                }
            }
            goal = home;
            return Grid != null && Grid.TryRandomWalkableNear(home, wanderRadius, out goal);
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

        [Header("Anger outline")]
        public Color angerColor = new Color(1f, 0.15f, 0.1f, 1f);
        [Tooltip("Subtle outline + wash while this villager likes Kömür (cured by the cat).")]
        public Color fondColor = new Color(1f, 0.55f, 0.8f, 0.7f);

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int HitColorId = Shader.PropertyToID("_HitEffectColor");
        private static readonly int HitBlendId = Shader.PropertyToID("_HitEffectBlend");
        private MaterialPropertyBlock angerBlock;
        private float shownAnger = -1f;

        // Red outline (and a faint red wash) that grows while the villager glares at the cat and
        // pulses while they flee and throw.
        private void UpdateAnger()
        {
            if (visualSprite == null) return;
            float anger = 0f;
            // Sick green wins over the pink: fondness shows again once they are cured.
            bool fond = Friendly && State == VillagerState.Healthy;
            if (inEnding)
            {
                fond = endMood == EndMood.Loving && State == VillagerState.Healthy;
                if (endMood == EndMood.Angry && State == VillagerState.Healthy) anger = 0.75f + 0.25f * Mathf.Sin(Time.time * 18f);
            }
            else if (State == VillagerState.Healthy && !Friendly)
            {
                if (Time.time < fleeUntil) anger = 0.75f + 0.25f * Mathf.Sin(Time.time * 18f);
                else anger = fearBuildTime > 0f ? fear / fearBuildTime : 0f;
            }
            // Fondness breathes gently; anger takes over the same channels.
            float key = fond ? 2f + Mathf.Round(Mathf.Sin(Time.time * 2f) * 10f) / 10f : anger;
            if (Mathf.Abs(key - shownAnger) < 0.01f) return;
            shownAnger = key;
            angerBlock ??= new MaterialPropertyBlock();
            visualSprite.GetPropertyBlock(angerBlock);
            Color outline, wash;
            float washAmount, width = 1f;
            if (fond)
            {
                float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
                outline = fondColor;
                outline.a = fondColor.a * Mathf.Lerp(0.6f, 1f, breathe);
                wash = fondColor;
                washAmount = 0.08f;
            }
            else
            {
                outline = angerColor;
                outline.a = anger > 0.02f ? Mathf.Lerp(0.45f, 1f, anger) : 0f;
                wash = angerColor;
                washAmount = anger * 0.22f;
                if (anger > 0.6f) width = 2f;
            }
            angerBlock.SetColor(OutlineColorId, outline);
            angerBlock.SetFloat(OutlineWidthId, width);
            angerBlock.SetColor(HitColorId, wash);
            angerBlock.SetFloat(HitBlendId, washAmount);
            visualSprite.SetPropertyBlock(angerBlock);
        }

        private SpriteFlipbook flipbook;
        private SpriteRenderer visualSprite;
        private float oneShotUntil;

        private void UpdateAnimator(Vector2 velocity)
        {
            if (flipbook != null)
            {
                if (State == VillagerState.Dead || Time.time < oneShotUntil) return;
                bool walking = velocity.sqrMagnitude > 0.01f;
                flipbook.Play(walking ? "Walk" : "Idle");
                if (visualSprite != null && Mathf.Abs(velocity.x) > 0.05f) visualSprite.flipX = velocity.x < 0f;
                return;
            }
            if (animator == null) return;
            bool moving = velocity.sqrMagnitude > 0.01f;
            animator.SetBool("IsMoving", moving);
            if (!moving) return;
            if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y)) animator.SetInteger("Direction", velocity.x > 0f ? 2 : 3);
            else animator.SetInteger("Direction", velocity.y > 0f ? 1 : 0);
        }

        // Someone died: the town blames the black cat again, even those it saved.
        public void LoseTrust()
        {
            if (!Friendly || State == VillagerState.Dead) return;
            Friendly = false;
            fear = 0f;
            shownAnger = -1f;
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
            SetIcon("", Color.green);     // the green tint already says "sick"
            PlayOnce("Hurt");
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
            GameFeedbacks.Play(FeedbackId.VillagerDied, transform.position);
            if (GameManager.I != null) GameManager.I.OnVillagerDied();
            StartCoroutine(BecomeGrave(PlayOnce("Death")));
        }

        // Plays a one-shot clip (hurt, death); returns its length (0 if there is none).
        private float PlayOnce(string clip)
        {
            if (flipbook == null) return 0f;
            float length = flipbook.ClipLength(clip);
            if (length <= 0f) return 0f;
            flipbook.Play(clip, true);
            oneShotUntil = Time.time + length + 0.15f;
            return length;
        }

        private System.Collections.IEnumerator BecomeGrave(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay + 0.6f);
            bool flip = visualSprite != null && visualSprite.flipX;
            if (visualRoot != null) visualRoot.SetActive(false);
            var idle = flipbook != null && flipbook.clips != null && flipbook.clips.Length > 0 ? flipbook.clips[0].frames : null;
            if (ghostMaterial != null && idle != null)
            {
                spirit = VillagerGhost.Spawn(idle, flip, endSlot ?? transform.position, ghostMaterial, Floor);
            }
            else if (gravestonePrefab != null)
            {
                var grave = Instantiate(gravestonePrefab, transform.position, Quaternion.identity);
                int layer = LayerMask.NameToLayer(FloorAgent.LayerName(Floor));
                foreach (var t in grave.GetComponentsInChildren<Transform>(true)) if (layer >= 0) t.gameObject.layer = layer;
                foreach (var r in grave.GetComponentsInChildren<Renderer>(true)) r.sortingLayerName = FloorAgent.LayerName(Floor);
                var scenery = GameManager.I != null ? GameManager.I.sceneryMaterial : null;
                if (scenery != null)
                    foreach (var sr in grave.GetComponentsInChildren<SpriteRenderer>(true))
                        if (!sr.name.Contains("Shadow")) sr.sharedMaterial = scenery;
            }
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
