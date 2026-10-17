using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Follows NavGrid paths. Re-plans periodically so moved obstacles are respected.
    public class PathMover : MonoBehaviour
    {
        public float repathInterval = 0.5f;
        public float waypointReachedDistance = 0.15f;

        private readonly List<Vector2> path = new List<Vector2>();
        private int index;
        private Vector2 destination;
        private bool hasDestination;
        private float nextRepathAt;
        private Vector3 lastProgressPos;
        private float progressCheckAt;

        public bool HasDestination => hasDestination;
        public bool Arrived => hasDestination && index >= path.Count;
        public Vector2 Destination => destination;

        // Returns false if no path exists right now.
        public bool SetDestination(Vector2 target, bool force = false)
        {
            if (!force && hasDestination && (target - destination).sqrMagnitude < 0.25f && Time.time < nextRepathAt)
                return true;
            destination = target;
            hasDestination = true;
            return Repath();
        }

        public void Stop()
        {
            hasDestination = false;
            path.Clear();
            index = 0;
        }

        private bool Repath()
        {
            nextRepathAt = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
            index = 0;
            var grid = NavGrid.For(FloorAgent.FloorOf(gameObject));
            if (grid == null)
            {
                path.Clear();
                path.Add(destination);
                return true;
            }
            if (grid.FindPath(transform.position, destination, path)) return true;
            // No path on the grid: head straight for it rather than standing still (walls still block physically).
            path.Clear();
            path.Add(destination);
            return false;
        }

        public Vector2 NextDirection()
        {
            if (!hasDestination) return Vector2.zero;

            // Re-plan on a timer, or sooner if we've stopped making progress (e.g. a crate got pushed in the way).
            if (Time.time >= progressCheckAt)
            {
                bool stuck = (transform.position - lastProgressPos).sqrMagnitude < 0.01f && index < path.Count;
                lastProgressPos = transform.position;
                progressCheckAt = Time.time + 0.6f;
                if (stuck) nextRepathAt = 0f;
            }
            if (Time.time >= nextRepathAt) Repath();

            Vector2 pos = transform.position;
            while (index < path.Count && (path[index] - pos).sqrMagnitude <= waypointReachedDistance * waypointReachedDistance)
                index++;
            if (index >= path.Count) return Vector2.zero;
            return (path[index] - pos).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = transform.position;
            for (int i = index; i < path.Count; i++)
            {
                Gizmos.DrawLine(prev, path[i]);
                prev = path[i];
            }
        }
    }
}
