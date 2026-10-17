using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Komur
{
    public enum GameState { Intro, Playing, Won, Lost }

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
        public float timeBetweenWaves = 4f;
        public float maxWaveDuration = 30f;
        public int maxDeaths = 3;
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

        [Header("Title screen")]
        [Tooltip("Camera size while the title screen shows Kömür sitting in the square.")]
        public float titleCameraSize = 3f;
        public float titleZoomOutTime = 1.4f;

        public GameState State { get; private set; } = GameState.Intro;
        private int playingSinceFrame = int.MaxValue;

        // False on the frame the intro is dismissed, so that Space press doesn't also pounce.
        public bool AcceptsGameplayInput => State == GameState.Playing && Time.frameCount > playingSinceFrame;
        public int WaveNumber { get; private set; }
        public int WaveCount => waves.Count;
        public int Dead { get; private set; }
        public int Cured { get; private set; }
        public float PanicMultiplier => 1f + panicPerDeath * Dead;
        public float CurrentSickDuration => WaveNumber > 0 ? waves[WaveNumber - 1].sickDuration : 25f;

        public readonly List<Villager> villagers = new List<Villager>();
        public readonly List<Rat> rats = new List<Rat>();

        public int Alive => villagers.Count(v => v.State != VillagerState.Dead);
        public int Sick => villagers.Count(v => v.State == VillagerState.Sick);

        public static List<Wave> DefaultWaves()
        {
            return new List<Wave>
            {
                new Wave { rats = 3, spawnInterval = 3.0f, ratSpeed = 1.4f, sickDuration = 30f },
                new Wave { rats = 6, spawnInterval = 2.2f, ratSpeed = 1.6f, sickDuration = 26f },
                new Wave { rats = 9, spawnInterval = 1.6f, ratSpeed = 1.8f, sickDuration = 22f, fastRatChance = 0.25f },
                new Wave { rats = 12, spawnInterval = 1.2f, ratSpeed = 2.0f, sickDuration = 18f, fastRatChance = 0.35f },
                new Wave { rats = 15, spawnInterval = 1.0f, ratSpeed = 2.1f, sickDuration = 15f, fastRatChance = 0.4f, ratKing = true },
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
            if (hud != null) hud.ShowIntro(true);
            CameraFollow2D.ZoomTo(titleCameraSize, 0f);
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

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Intro:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartGame();
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
                bool newHole = OpenRatHoles(WaveNumber);
                if (hud != null) hud.ShowBanner($"Wave {WaveNumber}/{waves.Count}");
                GameFeedbacks.Play(FeedbackId.WaveStart, komur != null ? komur.transform.position : Vector3.zero);
                yield return new WaitForSeconds(1.5f);
                if (newHole && WaveNumber > 1 && hud != null)
                {
                    hud.ShowBanner("A new rat hole appeared!");
                    yield return new WaitForSeconds(1.2f);
                }

                if (wave.ratKing)
                {
                    if (hud != null) hud.ShowBanner(bossPrefab != null ? "The Plague Slime is here!" : "The Rat King is here!");
                    SpawnRat(wave.ratSpeed, RatKind.King);
                    yield return new WaitForSeconds(1.5f);
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

        // Opens holes whose activeFromWave has been reached. Returns true if any opened this wave.
        private bool OpenRatHoles(int wave)
        {
            bool opened = false;
            foreach (var hole in RatHole.All)
            {
                if (hole.IsOpen || hole.activeFromWave > wave) continue;
                hole.SetOpen(true);
                opened = true;
                GameFeedbacks.Play(FeedbackId.WaveStart, hole.transform.position);
            }

            // Safety net: always have at least one open hole.
            if (!RatHole.All.Any(h => h.IsOpen) && RatHole.All.Count > 0)
            {
                RatHole.All.OrderBy(h => h.activeFromWave).First().SetOpen(true);
                opened = true;
            }
            return opened;
        }

        private void SpawnRat(float speed, RatKind kind)
        {
            var prefab = kind == RatKind.King && bossPrefab != null ? bossPrefab : ratPrefab;
            if (prefab == null) return;
            var open = RatHole.All.Where(h => h.IsOpen).ToList();
            if (open.Count == 0) return;
            var hole = open[UnityEngine.Random.Range(0, open.Count)];
            Vector3 pos = hole.SpawnPosition;
            var rat = Instantiate(prefab, pos, Quaternion.identity);
            rat.Init(pos, speed, kind, hole.Floor);
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

        // Nearest healthy villager on the rat's floor that it can actually path to (tries the closest few).
        public Villager FindReachableTarget(Rat rat, PathMover mover)
        {
            candidateBuffer.Clear();
            int floor = rat.Floor;
            foreach (var v in villagers) if (v.CanBeBitten && v.Floor == floor) candidateBuffer.Add(v);
            Vector3 from = rat.transform.position;
            candidateBuffer.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            for (int i = 0; i < candidateBuffer.Count && i < 3; i++)
                if (mover.SetDestination(candidateBuffer[i].transform.position, true)) return candidateBuffer[i];
            return null;
        }

        public void OnVillagerCured()
        {
            Cured++;
        }

        public void OnVillagerDied()
        {
            Dead++;
            if (State == GameState.Playing && Dead >= maxDeaths) End(false);
            else if (State == GameState.Playing && hud != null) hud.ShowBanner("The town panics...");
        }

        private void End(bool won)
        {
            State = won ? GameState.Won : GameState.Lost;
            GameFeedbacks.Play(won ? FeedbackId.Won : FeedbackId.Lost, komur != null ? komur.transform.position : Vector3.zero);
            if (hud != null) hud.ShowEnd(won, Alive, Cured, Dead);
        }
    }
}
