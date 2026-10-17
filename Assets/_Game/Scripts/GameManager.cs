using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Komur
{
    public enum GameState { Intro, Playing, Won, Lost }

    // How the night ends: everyone lost, or the rats beaten - and whether the town noticed who saved it.
    public enum Ending { AllDead, Loved, UnknownHero }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class Wave
        {
            public int rats = 4;
            public float spawnInterval = 2f;
            public float ratSpeed = 1.6f;
            [Tooltip("Seconds a bitten villager has before dying, during this wave.")]
            public float sickDuration = 25f;
            [Range(0f, 1f)] public float fastRatChance;
            public bool ratKing;
        }

        public static GameManager I { get; private set; }

        [Header("Waves")]
        public List<Wave> waves = DefaultWaves();
        public float timeBetweenWaves = 6f;
        public float maxWaveDuration = 40f;
        [Range(0f, 1f)]
        [Tooltip("Share of the surviving villagers Kömür must have cured for the town to love the cat.")]
        public float lovedThreshold = 0.5f;

        [Header("New rat hole reveal")]
        public float revealPanTime = 0.8f;
        public float revealHoldTime = 1.2f;
        [Tooltip("Each villager death makes uncured villagers scare this much more easily.")]
        public float panicPerDeath = 0.2f;

        [Header("References")]
        public Rat ratPrefab;
        [Tooltip("Final-wave boss. If empty, a big tinted rat is used.")]
        public Rat bossPrefab;
        [Tooltip("Legacy: plain transforms here are turned into rat holes opening one per wave.")]
        public Transform[] ratSpawns;
        public KomurController komur;
        public Hud hud;
        [Tooltip("Komur/Sprite material that clears the stencil; given to all scenery at start so hidden characters show a silhouette.")]
        public Material sceneryMaterial;

        [Header("Title screen")]
        [Tooltip("Camera size while the title screen shows Kömür sitting in the square.")]
        public float titleCameraSize = 3f;
        public float titleZoomOutTime = 1.4f;

        public GameState State { get; private set; } = GameState.Intro;
        private int playingSinceFrame = int.MaxValue;

        // True while the camera shows a newly opened rat hole: characters freeze.
        public bool IsCinematic { get; private set; }
        public bool IsSimulating => State == GameState.Playing && !IsCinematic && !IsPaused;
        public bool IsPaused { get; private set; }

        // Pause (Esc / P / the pause button): time stops, the overlay shows.
        public void SetPaused(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (hud != null) hud.ShowPause(paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        // False on the frame the intro is dismissed, so that Space press doesn't also pounce.
        public bool AcceptsGameplayInput => IsSimulating && Time.frameCount > playingSinceFrame;
        public int WaveNumber { get; private set; }
        public int WaveCount => waves.Count;
        public int Dead { get; private set; }
        public int Cured { get; private set; }
        public float PanicMultiplier => 1f + panicPerDeath * Dead;
        public float CurrentSickDuration => WaveNumber > 0 ? waves[WaveNumber - 1].sickDuration : 25f;

        public readonly List<Villager> villagers = new List<Villager>();
        public readonly List<Rat> rats = new List<Rat>();

        public int Alive => villagers.Count(v => v.State != VillagerState.Dead);
        public int Total => villagers.Count;
        // Survivors who were cured by Kömür (and so stopped fearing the cat).
        public int Grateful => villagers.Count(v => v.State != VillagerState.Dead && v.Friendly);
        public Ending Ending { get; private set; }
        // The boss takes over the apothecary: no medicine until it is dead.
        public Rat Boss { get; private set; }
        public bool BossAlive => Boss != null && rats.Contains(Boss);
        public int Sick => villagers.Count(v => v.State == VillagerState.Sick);

        public static List<Wave> DefaultWaves()
        {
            return new List<Wave>
            {
                new Wave { rats = 3, spawnInterval = 3.6f, ratSpeed = 1.25f, sickDuration = 38f, fastRatChance = 0.05f },
                new Wave { rats = 5, spawnInterval = 2.9f, ratSpeed = 1.4f, sickDuration = 33f, fastRatChance = 0.1f },
                new Wave { rats = 7, spawnInterval = 2.4f, ratSpeed = 1.5f, sickDuration = 29f, fastRatChance = 0.2f },
                new Wave { rats = 9, spawnInterval = 2.0f, ratSpeed = 1.6f, sickDuration = 25f, fastRatChance = 0.25f },
                new Wave { rats = 12, spawnInterval = 1.7f, ratSpeed = 1.7f, sickDuration = 22f, fastRatChance = 0.3f, ratKing = true },
            };
        }

        private void Awake()
        {
            I = this;
            ConfigurePhysicsLayers();
            EnsureNavGrids();
            ConvertLegacySpawns();
        }

        public static void ConfigurePhysicsLayers()
        {
            // Rats and villagers ("Characters") ignore each other. Which floor's walls they hit is set per
            // character by FloorAgent (Collider2D.excludeLayers).
            int characters = LayerMask.NameToLayer("Characters");
            if (characters < 0) return;
            Physics2D.IgnoreLayerCollision(characters, characters, true);
            for (int f = 1; f <= 3; f++)
            {
                int layer = LayerMask.NameToLayer(FloorAgent.LayerName(f));
                if (layer >= 0) Physics2D.IgnoreLayerCollision(characters, layer, false);
            }
        }

        private void EnsureNavGrids()
        {
            var existing = FindObjectsByType<NavGrid>(FindObjectsSortMode.None);
            for (int f = 1; f <= 3; f++)
            {
                if (existing.Any(g => g.floor == f)) continue;
                var go = new GameObject($"NavGrid Floor {f}");
                go.transform.SetParent(transform, false);
                go.AddComponent<NavGrid>().floor = f;
            }
        }

        private void ConvertLegacySpawns()
        {
            if (ratSpawns == null) return;
            for (int i = 0; i < ratSpawns.Length; i++)
            {
                if (ratSpawns[i] == null || ratSpawns[i].GetComponent<RatHole>() != null) continue;
                var hole = ratSpawns[i].gameObject.AddComponent<RatHole>();
                hole.activeFromWave = i + 1;
                hole.visual = ratSpawns[i].GetComponent<SpriteRenderer>();
            }
        }

        private void Start()
        {
            ApplySceneryMaterial();
            if (skipIntro)
            {
                skipIntro = false;
                CameraFollow2D.ZoomTo(CameraFollow2D.GameplaySize, 0f);
                StartGame();
                return;
            }
            if (hud != null) hud.ShowIntro(true);
            CameraFollow2D.ZoomTo(titleCameraSize, 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => skipIntro = false;

        // Scenery (props, plants, tilemaps) clears the stencil wherever it draws, so a character covered by it
        // shows its silhouette. Ground shadows are left alone (they are see-through).
        private void ApplySceneryMaterial()
        {
            if (sceneryMaterial == null) return;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!(r is SpriteRenderer) && !(r is UnityEngine.Tilemaps.TilemapRenderer)) continue;
                if (r.name.Contains("Shadow")) continue;
                if (r.GetComponentInParent<KomurController>() || r.GetComponentInParent<Villager>() || r.GetComponentInParent<Rat>()
                    || r.GetComponentInParent<RatHole>() || r.GetComponentInParent<ThrownStone>() || r.GetComponentInParent<Canvas>()) continue;
                r.sharedMaterial = sceneryMaterial;
            }
        }

        // Called by the Play button (or Space / Enter on the title screen).
        public void StartGame()
        {
            if (State != GameState.Intro) return;
            if (hud != null) hud.ShowIntro(false);
            State = GameState.Playing;
            playingSinceFrame = Time.frameCount;
            CameraFollow2D.ZoomTo(CameraFollow2D.GameplaySize, titleZoomOutTime);
            StartCoroutine(RunWaves());
        }

        // Restart straight into a new game (skips the title screen once).
        private static bool skipIntro;

        public void Restart()
        {
            skipIntro = true;
            Reload();
        }

        // Back to the title screen.
        public void ReturnToMenu()
        {
            skipIntro = false;
            Reload();
        }

        private void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Intro:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartGame();
                    break;
                case GameState.Playing:
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) TogglePause();
                    break;
                case GameState.Won:
                case GameState.Lost:
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
            }
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Count; i++)
            {
                WaveNumber = i + 1;
                var wave = waves[i];
                if (hud != null) hud.ShowBanner($"Wave {WaveNumber}/{waves.Count}");
                GameFeedbacks.Play(FeedbackId.WaveStart, komur != null ? komur.transform.position : Vector3.zero);
                yield return new WaitForSeconds(1.5f);

                // Show each newly opened hole before any rat comes out of it.
                foreach (var hole in HolesOpeningAt(WaveNumber))
                {
                    if (State != GameState.Playing) yield break;
                    yield return RevealHole(hole);
                }

                if (wave.ratKing)
                {
                    yield return RevealBoss(wave.ratSpeed);
                }

                for (int r = 0; r < wave.rats; r++)
                {
                    if (State != GameState.Playing) yield break;
                    var kind = UnityEngine.Random.value < wave.fastRatChance ? RatKind.Fast : RatKind.Normal;
                    SpawnRat(wave.ratSpeed, kind);
                    yield return new WaitForSeconds(wave.spawnInterval);
                }

                // Next wave comes when this one is cleared, or after a time limit so the pressure keeps building.
                float waveEndsAt = Time.time + maxWaveDuration;
                while (rats.Count > 0 && (Time.time < waveEndsAt || i == waves.Count - 1))
                {
                    if (State != GameState.Playing) yield break;
                    yield return null;
                }

                if (i < waves.Count - 1)
                {
                    if (hud != null) hud.ShowBanner(rats.Count == 0 ? "Wave cleared!" : "More rats are coming...");
                    yield return new WaitForSeconds(timeBetweenWaves);
                }
            }

            // Rats are gone; the outbreak ends once every sick villager is cured or lost.
            while (Sick > 0)
            {
                if (State != GameState.Playing) yield break;
                yield return null;
            }

            if (State == GameState.Playing) End(true);
        }

        // Holes that should open this wave (plus a fallback so at least one is always open).
        private List<RatHole> HolesOpeningAt(int wave)
        {
            var opening = RatHole.All.Where(h => !h.IsOpen && h.activeFromWave <= wave).OrderBy(h => h.activeFromWave).ToList();
            if (opening.Count == 0 && !RatHole.All.Any(h => h.IsOpen) && RatHole.All.Count > 0)
                opening.Add(RatHole.All.OrderBy(h => h.activeFromWave).First());
            return opening;
        }

        // Pan the camera to the hole, pop it open, then pan back to Kömür. Characters freeze meanwhile.
        private IEnumerator RevealHole(RatHole hole)
        {
            IsCinematic = true;
            if (hud != null) hud.ShowBanner("A new rat hole appeared!", revealPanTime + revealHoldTime);
            CameraFollow2D.Focus(hole.transform.position);
            yield return new WaitForSeconds(revealPanTime);
            hole.SetOpen(true);
            GameFeedbacks.Play(FeedbackId.HoleOpened, hole.transform.position, hole.transform);
            yield return new WaitForSeconds(revealHoldTime);
            CameraFollow2D.Focus(null);
            yield return new WaitForSeconds(revealPanTime);
            IsCinematic = false;
        }

#if UNITY_EDITOR
        // Editor-only test: the boss arrives now (full reveal), with the last wave's speed.
        public void DebugSummonBoss()
        {
            if (State == GameState.Intro) StartGame();
            if (State != GameState.Playing || BossAlive || IsCinematic) return;
            if (IsPaused) SetPaused(false);
            float speed = waves.Count > 0 ? waves[waves.Count - 1].ratSpeed : 1.6f;
            StartCoroutine(RevealBoss(speed));
        }

        // Editor-only test: jump straight to an ending (starts the game first if on the title screen).
        public void DebugEnding(Ending ending)
        {
            if (State == GameState.Intro) StartGame();
            if (State != GameState.Playing) return;
            if (IsPaused) SetPaused(false);
            switch (ending)
            {
                case Ending.Loved:
                    foreach (var v in villagers) v.DebugSetFriendly(true);
                    End(true);
                    break;
                case Ending.UnknownHero:
                    foreach (var v in villagers) v.DebugSetFriendly(false);
                    End(true);
                    break;
                case Ending.AllDead:
                    // The real thing: each death plays out, and the last one ends the game.
                    foreach (var v in villagers.ToArray()) v.DebugKill();
                    break;
            }
        }
#endif

        // Like a hole reveal: pan to the apothecary, the boss bursts out of it and roars, pan back.
        private IEnumerator RevealBoss(float speed)
        {
            IsCinematic = true;
            var apothecary = FindFirstObjectByType<Apothecary>();
            if (hud != null) hud.ShowBanner(bossPrefab != null ? "The Plague Slime is here!" : "The Rat King is here!", revealPanTime * 2f + revealHoldTime + 0.5f);
            if (apothecary != null)
            {
                CameraFollow2D.Focus(apothecary.transform.position);
                yield return new WaitForSeconds(revealPanTime);
            }
            var boss = SpawnBoss(speed);
            if (boss != null)
            {
                CameraFollow2D.Focus(boss.transform.position);
                GameFeedbacks.Play(FeedbackId.HoleOpened, boss.transform.position);
                GameFeedbacks.Play(FeedbackId.BossArrived, boss.transform.position, boss.transform);
            }
            yield return new WaitForSeconds(revealHoldTime + 0.5f);
            CameraFollow2D.Focus(null);
            yield return new WaitForSeconds(revealPanTime);
            IsCinematic = false;
        }

        // The boss bursts out of the apothecary (or a rat hole if there is none) and holds it.
        private Rat SpawnBoss(float speed)
        {
            var apothecary = FindFirstObjectByType<Apothecary>();
            var prefab = bossPrefab != null ? bossPrefab : ratPrefab;
            if (apothecary == null || prefab == null) return Boss = SpawnRat(speed, RatKind.King);
            int floor = apothecary.Floor;
            Vector3 pos = apothecary.transform.position;
            var grid = NavGrid.For(floor);
            if (grid != null && grid.TryNearestWalkable(pos, 6, out var walkable)) pos = walkable;
            var rat = Instantiate(prefab, pos, Quaternion.identity);
            rat.Init(pos, speed, RatKind.King, floor);
            Boss = rat;
            apothecary.OnBossArrived();
            return rat;
        }

        private Rat SpawnRat(float speed, RatKind kind)
        {
            var prefab = kind == RatKind.King && bossPrefab != null ? bossPrefab : ratPrefab;
            if (prefab == null) return null;
            var open = RatHole.All.Where(h => h.IsOpen).ToList();
            if (open.Count == 0) return null;
            var hole = open[UnityEngine.Random.Range(0, open.Count)];
            Vector3 pos = hole.SpawnPosition;
            var rat = Instantiate(prefab, pos, Quaternion.identity);
            rat.Init(pos, speed, kind, hole.Floor);
            return rat;
        }

        public Vector3 NearestRatHole(Vector3 from, int floor)
        {
            RatHole best = null;
            float bestDist = float.MaxValue;
            foreach (var hole in RatHole.All)
            {
                if (!hole.IsOpen || hole.Floor != floor) continue;
                float d = (hole.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = hole; }
            }
            return best != null ? best.SpawnPosition : from;
        }

        private readonly List<Villager> candidateBuffer = new List<Villager>();

        // Nearest healthy villager the rat can reach by walking (stairs included; tries the closest few).
        // If none is reachable, returns the nearest one with reachable = false (the rat will tunnel to it).
        public Villager FindReachableTarget(Rat rat, PathMover mover) => FindReachableTarget(rat, mover, out _);

        public Villager FindReachableTarget(Rat rat, PathMover mover, out bool reachable)
        {
            reachable = true;
            candidateBuffer.Clear();
            int floor = rat.Floor;
            foreach (var v in villagers) if (v.CanBeBitten) candidateBuffer.Add(v);
            Vector3 from = rat.transform.position;
            // Nearest first; villagers on other floors count as a bit further away (stairs to climb).
            float Cost(Villager v) => (v.transform.position - from).sqrMagnitude * (v.Floor == floor ? 1f : 2f);
            candidateBuffer.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            for (int i = 0; i < candidateBuffer.Count && i < 5; i++)
                if (mover.SetDestination(candidateBuffer[i].transform.position, true, candidateBuffer[i].Floor)) return candidateBuffer[i];
            reachable = false;
            return candidateBuffer.Count > 0 ? candidateBuffer[0] : null;
        }

        public void OnVillagerCured()
        {
            Cured++;
        }

        public void OnVillagerDied()
        {
            Dead++;
            foreach (var v in villagers) v.LoseTrust();
            if (State == GameState.Playing && Alive == 0) End(false);
            else if (State == GameState.Playing && hud != null) hud.ShowBanner("The town panics...");
        }

        private void End(bool won)
        {
            State = won ? GameState.Won : GameState.Lost;
            Ending = !won ? Ending.AllDead
                : Grateful > 0 && Grateful >= Mathf.CeilToInt(Alive * lovedThreshold + 0.0001f) ? Ending.Loved
                : Ending.UnknownHero;
            GameFeedbacks.Play(won ? FeedbackId.Won : FeedbackId.Lost, komur != null ? komur.transform.position : Vector3.zero, komur != null ? komur.transform : null);
            StartCoroutine(EndSequence());
        }

        [Header("Ending")]
        public Vector2 endCircleRadius = new Vector2(2.1f, 1.35f);
        [Tooltip("How far below the altar's middle Kömür's feet go, so the sitting cat looks centred.")]
        public float komurAltarDrop = 0.3f;
        public float endShowScreenAfter = 1.5f;

        // Everyone gathers: the rats burrow away, Kömür appears on the apothecary, the villagers (and the
        // spirits of the dead) form a circle around the cat and act out the ending, then the end screen.
        private IEnumerator EndSequence()
        {
            if (hud != null) hud.HideGameplay();
            for (int i = rats.Count - 1; i >= 0; i--)
            {
                if (rats[i] == null) continue;
                GameFeedbacks.Play(FeedbackId.RatDig, rats[i].transform.position);
                Destroy(rats[i].gameObject);
            }
            foreach (var stone in FindObjectsByType<ThrownStone>(FindObjectsSortMode.None)) Destroy(stone.gameObject);
            yield return new WaitForSeconds(0.6f);

            var apothecary = FindFirstObjectByType<Apothecary>();
            int floor = apothecary != null ? apothecary.Floor : 1;
            Vector3 centre = apothecary != null ? apothecary.AltarCentre : (komur != null ? komur.transform.position : Vector3.zero);
            var grid = NavGrid.For(floor);
            if (komur != null)
            {
                GameFeedbacks.Play(FeedbackId.RatDig, komur.transform.position);
                // Kömür's feet a little below the middle, so the sitting cat looks centred on the altar.
                komur.EnterEnding(centre + Vector3.down * komurAltarDrop, floor);
                GameFeedbacks.Play(FeedbackId.MedicinePicked, centre);
            }
            CameraFollow2D.Focus(centre);
            CameraFollow2D.ZoomTo(titleCameraSize, 1.2f);
            yield return new WaitForSeconds(0.5f);

            int n = Mathf.Max(1, villagers.Count);
            for (int i = 0; i < villagers.Count; i++)
            {
                var v = villagers[i];
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
                // Slots are feet; the bodies stand ~half a unit higher, so drop the ring to centre the bodies.
                Vector3 slot = centre + new Vector3(Mathf.Cos(angle) * endCircleRadius.x, Mathf.Sin(angle) * endCircleRadius.y - 0.45f, 0f);
                if (v.State != VillagerState.Dead && grid != null && !grid.IsWalkable(slot) && grid.TryNearestWalkable(slot, 1, out var free)) slot = free;
                var mood = Villager.EndMood.Calm;
                if (v.State != VillagerState.Dead)
                {
                    if (Ending == Ending.Loved && v.Friendly) mood = Villager.EndMood.Loving;
                    else if (Ending == Ending.UnknownHero) mood = v.Friendly ? Villager.EndMood.Loving : Villager.EndMood.Angry;
                }
                v.EnterEnding(slot, floor, centre, mood);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(endShowScreenAfter);
            if (hud != null) hud.ShowEnd(Ending, Alive, Grateful, Dead, Total);
        }
    }
}
