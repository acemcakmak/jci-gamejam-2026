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
        private int destinationFloor = -1;
        private bool hasDestination;
        private float nextRepathAt;
        private Vector3 lastProgressPos;
        private float progressCheckAt;
        private float sidestepUntil;
        private float sidestepSign = 1f;
        private int stuckCount;

        public bool HasDestination => hasDestination;
        public bool Arrived => hasDestination && index >= path.Count;
        public Vector2 Destination => destination;

        // Returns false if no route exists right now. targetFloor < 0 means "my own floor";
        // other floors are reached via stairs (FloorRouter).
        public bool SetDestination(Vector2 target, bool force = false, int targetFloor = -1)
        {
            if (!force && hasDestination && (target - destination).sqrMagnitude < 0.25f && targetFloor == destinationFloor && Time.time < nextRepathAt)
                return true;
            destination = target;
            destinationFloor = targetFloor;
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
            int myFloor = FloorAgent.FloorOf(gameObject);
            var grid = NavGrid.For(myFloor);
            if (grid == null)
            {
                path.Clear();
                path.Add(destination);
                return true;
            }
            int toFloor = destinationFloor < 0 ? myFloor : destinationFloor;
            if (FloorRouter.Route(transform.position, myFloor, destination, toFloor, path)) return true;
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
                progressCheckAt = Time.time + 0.5f;
                if (stuck)
                {
                    // Re-plan, and slide sideways for a moment to get off whatever corner we're caught on.
                    nextRepathAt = 0f;
                    stuckCount++;
                    sidestepSign = stuckCount % 2 == 0 ? 1f : -1f;
                    sidestepUntil = Time.time + 0.35f;
                }
                else stuckCount = 0;
            }
            if (Time.time >= nextRepathAt) Repath();

            Vector2 pos = transform.position;
            while (index < path.Count && (path[index] - pos).sqrMagnitude <= waypointReachedDistance * waypointReachedDistance)
                index++;
            if (index >= path.Count) return Vector2.zero;
            Vector2 dir = (path[index] - pos).normalized;
            if (Time.time < sidestepUntil) dir = (dir * 0.4f + new Vector2(-dir.y, dir.x) * sidestepSign).normalized;
            return dir;
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
