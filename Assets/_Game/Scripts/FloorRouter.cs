using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Routes across floors. Every Cainos staircase (StairsLayerTrigger) links a cell at its bottom on the
    // lower floor to a cell at its top on the upper floor; a route is a Dijkstra search over those links,
    // with NavGrid A* for the walking legs on each floor.
    public static class FloorRouter
    {
        private class Node
        {
            public Vector2 pos;
            public int floor;
            public int partner = -1;     // the other end of the same staircase
        }

        private static readonly List<Node> nodes = new List<Node>();
        private static int builtForScene = -1;
        private static readonly List<Vector2> scratch = new List<Vector2>();
        private static readonly Dictionary<long, float> stairLegCache = new Dictionary<long, float>();

        public static int PortalCount { get { EnsureBuilt(); return nodes.Count / 2; } }

        private static void EnsureBuilt()
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene == builtForScene && nodes.Count > 0) return;
            builtForScene = scene;
            nodes.Clear();
            stairLegCache.Clear();

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "StairsLayerTrigger") continue;
                var type = mb.GetType();
                string upper = type.GetField("layerUpper")?.GetValue(mb) as string;
                string lower = type.GetField("layerLower")?.GetValue(mb) as string;
                int fu = FloorFromName(upper), fl = FloorFromName(lower);
                if (fu < 1 || fl < 1 || fu == fl) continue;
                var gl = NavGrid.For(fl);
                var gu = NavGrid.For(fu);
                if (gl == null || gu == null) continue;

                // Bottom: the lower-floor cell nearest the trigger (it sits at the foot of the stairs).
                // Top: the upper-floor cell nearest the staircase root (the top of the stairs).
                Vector2 trigger = mb.transform.position;
                Vector2 root = mb.transform.parent != null ? (Vector2)mb.transform.parent.position : trigger;
                if (!gl.TryNearestWalkable(trigger, 8, out var bottom)) continue;
                if (!gu.TryNearestWalkable(root, 10, out var top)) continue;

                int a = nodes.Count;
                nodes.Add(new Node { pos = bottom, floor = fl, partner = a + 1 });
                nodes.Add(new Node { pos = top, floor = fu, partner = a });
            }
            Debug.Log($"[FloorRouter] {nodes.Count / 2} staircases linked");
        }

        private static int FloorFromName(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return -1;
            for (int f = 1; f <= 3; f++) if (layer == FloorAgent.LayerName(f)) return f;
            return -1;
        }

        // Fills result with waypoints from (from, fromFloor) to (to, toFloor). Returns false if unreachable.
        public static bool Route(Vector2 from, int fromFloor, Vector2 to, int toFloor, List<Vector2> result)
        {
            result.Clear();
            EnsureBuilt();

            // Same floor and a direct path: done.
            if (fromFloor == toFloor)
            {
                var grid = NavGrid.For(fromFloor);
                if (grid != null && grid.FindPath(from, to, result)) return true;
            }

            // Dijkstra over: 0 = start, 1 = goal, 2.. = staircase ends.
            int n = nodes.Count + 2;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[0] = 0f;

            Vector2 Pos(int i) => i == 0 ? from : i == 1 ? to : nodes[i - 2].pos;
            int Floor(int i) => i == 0 ? fromFloor : i == 1 ? toFloor : nodes[i - 2].floor;

            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == 1) break;
                done[u] = true;

                // Climb or descend the staircase this end belongs to.
                if (u >= 2)
                {
                    int partner = nodes[u - 2].partner + 2;
                    float c = dist[u] + Vector2.Distance(Pos(u), Pos(partner));
                    if (c < dist[partner]) { dist[partner] = c; prev[partner] = u; }
                }
                // Walk to anything else on the same floor.
                for (int v = 1; v < n; v++)
                {
                    if (done[v] || Floor(v) != Floor(u) || v == u) continue;
                    float len;
                    if (u >= 2 && v >= 2)
                    {
                        // Staircase-to-staircase legs never change much: cache them.
                        long key = ((long)u << 32) | (uint)v;
                        if (!stairLegCache.TryGetValue(key, out len)) stairLegCache[key] = len = PathLength(Floor(u), Pos(u), Pos(v));
                    }
                    else len = PathLength(Floor(u), Pos(u), Pos(v));
                    if (len < 0f) continue;
                    float c = dist[u] + len;
                    if (c < dist[v]) { dist[v] = c; prev[v] = u; }
                }
            }
            if (prev[1] < 0) return false;

            // Rebuild: walking legs via A*, staircase legs as a straight climb.
            var chain = new List<int>();
            for (int i = 1; i != -1; i = prev[i]) chain.Add(i);
            chain.Reverse();
            for (int k = 1; k < chain.Count; k++)
            {
                int a = chain[k - 1], b = chain[k];
                if (Floor(a) == Floor(b) && !(a >= 2 && b >= 2 && nodes[a - 2].partner == b - 2))
                {
                    var grid = NavGrid.For(Floor(a));
                    if (grid != null && grid.FindPath(Pos(a), Pos(b), scratch)) result.AddRange(scratch);
                    else result.Add(Pos(b));
                }
                else
                {
                    result.Add(Pos(b));
                }
            }
            return result.Count > 0;
        }

        private static float PathLength(int floor, Vector2 a, Vector2 b)
        {
            var grid = NavGrid.For(floor);
            if (grid == null || !grid.FindPath(a, b, scratch)) return -1f;
            float len = 0f;
            Vector2 prev = a;
            foreach (var p in scratch) { len += Vector2.Distance(prev, p); prev = p; }
            return len;
        }
    }
}
