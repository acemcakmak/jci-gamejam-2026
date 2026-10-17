using System;
using System.Collections;
using System.Collections.Generic;
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
        }

        public static GameManager I { get; private set; }

        [Header("Waves")]
        public List<Wave> waves = new List<Wave>
        {
            new Wave { rats = 4, spawnInterval = 2.0f, ratSpeed = 1.5f },
            new Wave { rats = 7, spawnInterval = 1.5f, ratSpeed = 1.7f },
            new Wave { rats = 10, spawnInterval = 1.2f, ratSpeed = 1.9f },
            new Wave { rats = 14, spawnInterval = 1.0f, ratSpeed = 2.1f },
            new Wave { rats = 18, spawnInterval = 0.8f, ratSpeed = 2.3f },
        };
        public float timeBetweenWaves = 4f;
        public int maxDeaths = 3;

        [Header("References")]
        public Rat ratPrefab;
        public Transform[] ratSpawns;
        public KomurController komur;
        public Hud hud;

        public float maxWaveDuration = 30f;

        public GameState State { get; private set; } = GameState.Intro;
        private int playingSinceFrame = int.MaxValue;

        // False on the frame the intro is dismissed, so that Space press doesn't also pounce.
        public bool AcceptsGameplayInput => State == GameState.Playing && Time.frameCount > playingSinceFrame;
        public int WaveNumber { get; private set; }
        public int WaveCount => waves.Count;
        public int Dead { get; private set; }
        public int Cured { get; private set; }

        public readonly List<Villager> villagers = new List<Villager>();
        public readonly List<Rat> rats = new List<Rat>();

        public int Alive
        {
            get
            {
                int count = 0;
                foreach (var v in villagers) if (v.State != VillagerState.Dead) count++;
                return count;
            }
        }

        public int Sick
        {
            get
            {
                int count = 0;
                foreach (var v in villagers) if (v.State == VillagerState.Sick) count++;
                return count;
            }
        }

        private void Awake()
        {
            I = this;
            ConfigurePhysicsLayers();
            if (NavGrid.I == null && FindFirstObjectByType<NavGrid>() == null) gameObject.AddComponent<NavGrid>();
        }

        public static void ConfigurePhysicsLayers()
        {
            // Rats and villagers ("Characters") ignore each other and the upper floors; they collide with ground walls/props.
            int characters = LayerMask.NameToLayer("Characters");
            if (characters < 0) return;
            Physics2D.IgnoreLayerCollision(characters, characters, true);
            foreach (var upper in new[] { "Layer 2", "Layer 3" })
            {
                int layer = LayerMask.NameToLayer(upper);
                if (layer >= 0) Physics2D.IgnoreLayerCollision(characters, layer, true);
            }
        }

        private void Start()
        {
            if (hud != null) hud.ShowIntro(true);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Intro:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    {
                        if (hud != null) hud.ShowIntro(false);
                        State = GameState.Playing;
                        playingSinceFrame = Time.frameCount;
                        StartCoroutine(RunWaves());
                    }
                    break;
                case GameState.Won:
                case GameState.Lost:
                    if (Input.GetKeyDown(KeyCode.R))
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

                for (int r = 0; r < wave.rats; r++)
                {
                    if (State != GameState.Playing) yield break;
                    SpawnRat(wave.ratSpeed);
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
                    if (hud != null) hud.ShowBanner("Wave cleared!");
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

        private void SpawnRat(float speed)
        {
            if (ratPrefab == null || ratSpawns == null || ratSpawns.Length == 0) return;
            var spawn = ratSpawns[UnityEngine.Random.Range(0, ratSpawns.Length)];
            Vector3 pos = NavGrid.I != null ? (Vector3)NavGrid.I.SnapToWalkable(spawn.position) : spawn.position;
            var rat = Instantiate(ratPrefab, pos, Quaternion.identity);
            rat.Init(pos, speed);
        }

        public Vector3 NearestRatSpawn(Vector3 from)
        {
            Vector3 best = from;
            float bestDist = float.MaxValue;
            foreach (var s in ratSpawns)
            {
                float d = (s.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s.position; }
            }
            return NavGrid.I != null ? (Vector3)NavGrid.I.SnapToWalkable(best) : best;
        }

        public Villager NearestBiteable(Vector3 from)
        {
            Villager best = null;
            float bestDist = float.MaxValue;
            foreach (var v in villagers)
            {
                if (!v.CanBeBitten) continue;
                float d = (v.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = v; }
            }
            return best;
        }

        private readonly List<Villager> candidateBuffer = new List<Villager>();

        // Nearest healthy villager the rat can actually path to (tries the closest few).
        public Villager FindReachableTarget(Rat rat, PathMover mover)
        {
            candidateBuffer.Clear();
            foreach (var v in villagers) if (v.CanBeBitten) candidateBuffer.Add(v);
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
        }

        private void End(bool won)
        {
            State = won ? GameState.Won : GameState.Lost;
            GameFeedbacks.Play(won ? FeedbackId.Won : FeedbackId.Lost, komur != null ? komur.transform.position : Vector3.zero);
            if (hud != null) hud.ShowEnd(won, Alive, Cured, Dead);
        }
    }
}
