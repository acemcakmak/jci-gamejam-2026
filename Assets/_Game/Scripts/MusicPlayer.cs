using UnityEngine;

namespace Komur
{
    // Background music: plays the tracks one after another (shuffled), fading between them, and dips
    // under the victory / defeat sound when the game ends.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        public AudioClip[] tracks;
        [Range(0f, 1f)] public float volume = 0.35f;
        public bool shuffle = true;
        public float fadeTime = 1.5f;
        [Range(0f, 1f)] [Tooltip("Music level (relative) once the game is won or lost.")]
        public float endVolume = 0.35f;

        private AudioSource source;
        private int index = -1;
        private float fade = 1f;
        private bool heardPlaying;     // WebGL starts audio only after the first click: wait for that

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = tracks != null && tracks.Length == 1;
        }

        private void Start()
        {
            if (shuffle && tracks != null && tracks.Length > 1) index = Random.Range(0, tracks.Length) - 1;
            Next();
        }

        private void Next()
        {
            if (tracks == null || tracks.Length == 0) return;
            index = (index + 1) % tracks.Length;
            if (shuffle && tracks.Length > 2 && Random.value < 0.5f) index = (index + 1) % tracks.Length;
            source.clip = tracks[index];
            source.Play();
            fade = 0f;
            heardPlaying = false;
        }

        private void Update()
        {
            if (source.clip == null) return;
            float dt = Time.unscaledDeltaTime;
            float remaining = source.clip.length - source.time;
            bool ending = !source.loop && remaining < fadeTime;
            fade = Mathf.MoveTowards(fade, ending ? 0f : 1f, dt / Mathf.Max(0.01f, fadeTime));

            var gm = GameManager.I;
            bool over = gm != null && (gm.State == GameState.Won || gm.State == GameState.Lost);
            source.volume = volume * fade * (over ? endVolume : 1f) * (gm != null && gm.IsPaused ? 0.5f : 1f);

            if (source.isPlaying) heardPlaying = true;
            else if (!source.loop && heardPlaying) Next();
        }
    }
}
