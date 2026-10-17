using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // A rat spawn point that opens at a given wave.
    [RequireComponent(typeof(FloorAgent))]
    public class RatHole : MonoBehaviour
    {
        public static readonly List<RatHole> All = new List<RatHole>();

        [Min(1)] public int activeFromWave = 1;
        public SpriteRenderer visual;

        public bool IsOpen { get; private set; }
        public int Floor => FloorAgent.FloorOf(gameObject);

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Start()
        {
            SetOpen(false);
        }

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        public void SetOpen(bool open)
        {
            bool wasOpen = IsOpen;
            IsOpen = open;
            if (visual != null) visual.enabled = open;
            if (open && !wasOpen && Application.isPlaying) StartCoroutine(Pop());
        }

        // The hole bursts open: grows past its size and settles back.
        private System.Collections.IEnumerator Pop()
        {
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float overshoot = 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k);
                transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f)) * overshoot;
                yield return null;
            }
            transform.localScale = baseScale;
        }

        public Vector3 SpawnPosition
        {
            get
            {
                var grid = NavGrid.For(Floor);
                return grid != null ? (Vector3)grid.SnapToWalkable(transform.position) : transform.position;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.Lerp(Color.yellow, Color.red, (activeFromWave - 1) / 4f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
