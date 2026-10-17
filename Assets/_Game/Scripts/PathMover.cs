using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Follows routes from FloorRouter (NavGrid A* on each floor, stairs between floors). Re-plans
    // periodically so moved obstacles are respected - but never while climbing a staircase.
    public class PathMover : MonoBehaviour
    {
        public float repathInterval = 0.5f;
        public float waypointReachedDistance = 0.15f;

        private readonly List<Vector2> path = new List<Vector2>();
        private readonly List<int> climbs = new List<int>();
        private int index;
        private Vector2 destination;
        private int destinationFloor = -1;
        private bool hasDestination;
        private bool replanAfterClimb;
        private float nextRepathAt;
        private Vector3 lastProgressPos;
        private float progressCheckAt;
        private float sidestepUntil;
        private float sidestepSign = 1f;
        private int stuckCount;

        public bool HasDestination => hasDestination;
        public bool Arrived => hasDestination && index >= path.Count;
        public Vector2 Destination => destination;

        // Heading up or down a staircase right now (the current waypoint is the other end of the stairs).
        public bool Climbing => index < path.Count && climbs.Contains(index);

        // Returns false if no route exists right now. targetFloor < 0 means "my own floor";
        // other floors are reached via stairs.
        public bool SetDestination(Vector2 target, bool force = false, int targetFloor = -1)
        {
            if (!force && hasDestination && (target - destination).sqrMagnitude < 0.25f && targetFloor == destinationFloor && Time.time < nextRepathAt)
                return true;
            destination = target;
            destinationFloor = targetFloor;
            if (hasDestination && Climbing)
            {
                // Finish the climb first; the new goal is planned from the other end.
                replanAfterClimb = true;
                return true;
            }
            hasDestination = true;
            return Repath();
        }

        public void Stop()
        {
            hasDestination = false;
            path.Clear();
            climbs.Clear();
            index = 0;
        }

        private bool Repath()
        {
            nextRepathAt = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
            replanAfterClimb = false;
            index = 0;
            int myFloor = FloorAgent.FloorOf(gameObject);
            if (NavGrid.For(myFloor) == null)
            {
                path.Clear();
                climbs.Clear();
                path.Add(destination);
                return true;
            }
            int toFloor = destinationFloor < 0 ? myFloor : destinationFloor;
            if (FloorRouter.Route(transform.position, myFloor, destination, toFloor, path, climbs)) return true;
            // No route: head straight for it rather than standing still (walls still block physically).
            path.Clear();
            climbs.Clear();
            path.Add(destination);
            return false;
        }

        public Vector2 NextDirection()
        {
            if (!hasDestination) return Vector2.zero;
            Vector2 pos = transform.position;

            if (Climbing)
            {
                // Walk the stairs straight to their other end; no re-planning, no sidestepping.
                if ((path[index] - pos).sqrMagnitude <= 0.3f * 0.3f)
                {
                    index++;
                    if (replanAfterClimb && !Climbing) Repath();
                }
                if (index >= path.Count) return Vector2.zero;
                return (path[index] - pos).normalized;
            }

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

            while (index < path.Count && !Climbing && (path[index] - pos).sqrMagnitude <= waypointReachedDistance * waypointReachedDistance)
                index++;
            if (index >= path.Count) return Vector2.zero;
            Vector2 dir = (path[index] - pos).normalized;
            if (!Climbing && Time.time < sidestepUntil) dir = (dir * 0.4f + new Vector2(-dir.y, dir.x) * sidestepSign).normalized;
            return dir;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 prev = transform.position;
            for (int i = index; i < path.Count; i++)
            {
                Gizmos.color = climbs.Contains(i) ? Color.magenta : Color.cyan;
                Gizmos.DrawLine(prev, path[i]);
                prev = path[i];
            }
        }
    }
}
