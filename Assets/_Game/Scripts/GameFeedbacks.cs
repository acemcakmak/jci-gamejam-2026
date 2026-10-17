using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Komur
{
    public enum FeedbackId
    {
        Pounce,
        RatKilled,
        Bite,
        Cured,
        VillagerDied,
        Stunned,
        MedicinePicked,
        VillagerScared,
        WaveStart,
        Won,
        Lost
    }

    // One place to hook juice to game events: drop an Epic Toon FX prefab into vfxPrefab,
    // a sound into sfx, and a Feel MMF_Player.PlayFeedbacks(Vector3) into onPlay.
    [RequireComponent(typeof(AudioSource))]
    public class GameFeedbacks : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public FeedbackId id;
            public GameObject vfxPrefab;
            public float vfxLifetime = 2f;
            public float vfxScale = 0.5f;
            public AudioClip sfx;
            [Range(0f, 1f)] public float volume = 1f;
            public float cameraShake;
            public UnityEvent<Vector3> onPlay = new UnityEvent<Vector3>();
        }

        public static GameFeedbacks I { get; private set; }

        public List<Entry> entries = new List<Entry>();
        public string vfxSortingLayer = "Layer 1";
        public int vfxSortingOrder = 50;

        private AudioSource audioSource;

        private void Awake()
        {
            I = this;
            audioSource = GetComponent<AudioSource>();
        }

        public static void Play(FeedbackId id, Vector3 position)
        {
            if (I == null) return;
            I.PlayInternal(id, position);
        }

        private void PlayInternal(FeedbackId id, Vector3 position)
        {
            foreach (var entry in entries)
            {
                if (entry.id != id) continue;

                if (entry.vfxPrefab != null)
                {
                    var vfx = Instantiate(entry.vfxPrefab, position, entry.vfxPrefab.transform.rotation);
                    vfx.transform.localScale *= entry.vfxScale;
                    foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                    {
                        var main = ps.main;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    }
                    foreach (var r in vfx.GetComponentsInChildren<Renderer>())
                    {
                        r.sortingLayerName = vfxSortingLayer;
                        r.sortingOrder = vfxSortingOrder;
                    }
                    if (entry.vfxLifetime > 0f) Destroy(vfx, entry.vfxLifetime);
                }
                if (entry.sfx != null) audioSource.PlayOneShot(entry.sfx, entry.volume);
                if (entry.cameraShake > 0f) CameraFollow2D.Shake(entry.cameraShake, 0.15f);
                entry.onPlay?.Invoke(position);
            }
        }

        public void EnsureAllEntries()
        {
            foreach (FeedbackId id in Enum.GetValues(typeof(FeedbackId)))
            {
                if (entries.Exists(e => e.id == id)) continue;
                entries.Add(new Entry { id = id });
            }
        }
    }
}
