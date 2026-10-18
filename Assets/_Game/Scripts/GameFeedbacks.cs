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
        Lost,
        HoleOpened,
        RatHit,
        // Added later: keep new ids at the end (scenes store them as numbers).
        RatDig,
        BossKilled,
        BossArrived,
        StoneImpact,
        Love
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
            [Tooltip("More sounds for this event: each time it plays, one is picked at random from Sfx and these.")]
            public AudioClip[] sfxVariants = new AudioClip[0];
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0f, 0.3f)] [Tooltip("Random pitch shift (+/-) so repeats sound less identical.")]
            public float pitchVariation;
            public float cameraShake;
            [Tooltip("Floating PixelBattleText word shown above the event (empty = none).")]
            public string battleText;
            public PixelBattleText.TextAnimation battleTextAnimation;
            public float battleTextHeight = 1f;
            public UnityEvent<Vector3> onPlay = new UnityEvent<Vector3>();
        }

        public static GameFeedbacks I { get; private set; }

        public List<Entry> entries = new List<Entry>();
        public string vfxSortingLayer = "Layer 1";
        public int vfxSortingOrder = 50;
        [Tooltip("World units per PixelBattleText pixel (set from hud-layout.json).")]
        public float battleTextPixelSize = 0.018f;

        private AudioSource audioSource;

        private void Awake()
        {
            I = this;
            audioSource = GetComponent<AudioSource>();
        }

        // follow: the character the effect belongs to; floating words stick to it.
        public static void Play(FeedbackId id, Vector3 position, Transform follow = null)
        {
            if (I == null) return;
            I.PlayInternal(id, position, follow);
        }

        private void PlayInternal(FeedbackId id, Vector3 position, Transform follow)
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
                PlaySound(entry);
                if (entry.cameraShake > 0f) CameraFollow2D.Shake(entry.cameraShake, 0.15f);
                if (!string.IsNullOrEmpty(entry.battleText))
                    WorldBattleText.Spawn(entry.battleText, entry.battleTextAnimation, position, follow, entry.battleTextHeight, battleTextPixelSize);
                entry.onPlay?.Invoke(position);
            }
        }

        private readonly List<AudioClip> soundPool = new List<AudioClip>();

        // One clip from Sfx + Sfx Variants, at random; a pitch shift plays it on a spare source.
        private void PlaySound(Entry entry)
        {
            soundPool.Clear();
            if (entry.sfx != null) soundPool.Add(entry.sfx);
            if (entry.sfxVariants != null)
                foreach (var clip in entry.sfxVariants) if (clip != null) soundPool.Add(clip);
            if (soundPool.Count == 0) return;
            var pick = soundPool[UnityEngine.Random.Range(0, soundPool.Count)];
            if (entry.pitchVariation <= 0f)
            {
                audioSource.PlayOneShot(pick, entry.volume);
                return;
            }
            // PlayOneShot shares the source's pitch, so pitched sounds get their own short-lived source.
            var go = new GameObject("Sound " + pick.name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = audioSource.spatialBlend;
            src.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            src.pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariation, entry.pitchVariation);
            src.PlayOneShot(pick, entry.volume);
            Destroy(go, pick.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
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
