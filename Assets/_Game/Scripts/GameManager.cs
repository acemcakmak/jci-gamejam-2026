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
            new Wave { rats = 3, spawnInterval = 2.5f, ratSpeed = 1.4f },
            new Wave { rats = 5, spawnInterval = 2.0f, ratSpeed = 1.6f },
            new Wave { rats = 7, spawnInterval = 1.6f, ratSpeed = 1.8f },
            new Wave { rats = 9, spawnInterval = 1.3f, ratSpeed = 2.0f },
            new Wave { rats = 12, spawnInterval = 1.0f, ratSpeed = 2.2f },
        };
        public float timeBetweenWaves = 4f;
        public int maxDeaths = 3;

        [Header("References")]
        public Rat ratPrefab;
        public Transform[] ratSpawns;
        public KomurController komur;
        public Hud hud;

        public GameState State { get; private set; } = GameState.Intro;
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
            int characters = LayerMask.NameToLayer("Characters");
            if (characters >= 0) Physics2D.IgnoreLayerCollision(characters, characters, true);
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

                while (rats.Count > 0)
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
            var rat = Instantiate(ratPrefab, spawn.position, Quaternion.identity);
            rat.Init(spawn.position, speed);
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
            return best;
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
