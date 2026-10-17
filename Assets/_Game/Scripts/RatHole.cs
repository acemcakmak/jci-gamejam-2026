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

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (visual != null) visual.enabled = open;
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
