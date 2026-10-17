using System;
using UnityEngine;

namespace Komur
{
    // Code-driven sprite animation so sprite strips can be used without building Animator Controllers.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbook : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 10f;
            public bool loop = true;
        }

        public Clip[] clips;
        public string defaultClip = "Idle";

        private SpriteRenderer spriteRenderer;
        private Clip current;
        private float time;

        public string CurrentClip => current != null ? current.name : null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Play(defaultClip);
        }

        public void Play(string clipName, bool restart = false)
        {
            if (!restart && current != null && current.name == clipName) return;
            var clip = Array.Find(clips, c => c.name == clipName);
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;
            current = clip;
            time = 0f;
            spriteRenderer.sprite = clip.frames[0];
        }

        public float ClipLength(string clipName)
        {
            var clip = Array.Find(clips, c => c.name == clipName);
            return clip == null || clip.frames == null || clip.fps <= 0f ? 0f : clip.frames.Length / clip.fps;
        }

        private void Update()
        {
            if (current == null) return;
            time += Time.deltaTime;
            int frame = Mathf.FloorToInt(time * current.fps);
            frame = current.loop ? frame % current.frames.Length : Mathf.Min(frame, current.frames.Length - 1);
            spriteRenderer.sprite = current.frames[frame];
        }
    }
}
