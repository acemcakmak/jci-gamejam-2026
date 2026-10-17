using System.Collections.Generic;
using UnityEngine;

namespace Komur
{
    // Routes across floors. Every Cainos staircase (StairsLayerTrigger) links a cell at its foot on the
    // lower floor to a cell at its top on the upper floor; a route is a Dijkstra search over those links,
    // with NavGrid A* for the walking legs on each floor.
    //
    // A StairsLayerTrigger is a line across the foot of its stairs: crossing it from the 'direction' side
    // puts you on the upper floor, and the stairs rise away from that side. South stairs go straight up
    // (~2.9 units); the side stairs (E 02 / W 02) are a diagonal band rising ~2.55 units sideways and
    // ~1.15 up, walled on both sides by edge colliders, so climbs follow that band exactly.
    public static class FloorRouter
    {
        private class Node
        {
            public Vector2 pos;
            public int floor;
            public int partner = -1;     // the other end of the same staircase
            public Vector2 stairFoot;    // the trigger line's centre
            public Vector2 stairTop;     // where the stairs themselves end (climbs: foot cell -> stairFoot -> stairTop -> top cell)
        }

        private static readonly List<Node> nodes = new List<Node>();
        private static int builtForScene = -1;
        private static readonly List<Vector2> scratch = new List<Vector2>();
        private static readonly Dictionary<long, float> stairLegCache = new Dictionary<long, float>();

        // Fresh links every play session, also when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            builtForScene = -1;
            nodes.Clear();
            stairLegCache.Clear();
        }

        public static int PortalCount { get { EnsureBuilt(); return nodes.Count / 2; } }

        private static void EnsureBuilt()
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene == builtForScene) return;
            if (NavGrid.For(1) == null) return;      // grids not built yet; try again on the next request
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

                // enum Direction { North, South, West, East }
                int direction = System.Convert.ToInt32(type.GetField("direction")?.GetValue(mb) ?? 1);
                Vector2 up, rise;   // 'up' = the way you step off the stairs at the top; 'rise' = trigger -> stairs' end
                switch (direction)
                {
                    case 2: up = Vector2.right; rise = new Vector2(2.55f, 1.15f); break;   // West: foot on the west side
                    case 3: up = Vector2.left; rise = new Vector2(-2.55f, 1.15f); break;   // East: foot on the east side
                    case 0: up = Vector2.down; rise = new Vector2(0f, -2.9f); break;
                    default: up = Vector2.up; rise = new Vector2(0f, 2.9f); break;         // South
                }
                Vector2 trigger = mb.transform.position;
                Vector2 stairTop = trigger + rise;
                if (!gl.TryNearestWalkable(trigger - up * 0.6f, 4, out var foot))
                {
                    Debug.LogWarning($"[FloorRouter] {mb.transform.parent?.name}: no floor {fl} cell at the foot of the stairs");
                    continue;
                }
                if (!gu.TryNearestWalkable(stairTop + up * 0.3f, 5, out var top))
                {
                    Debug.LogWarning($"[FloorRouter] {mb.transform.parent?.name}: no floor {fu} cell at the top of the stairs");
                    continue;
                }

                int a = nodes.Count;
                nodes.Add(new Node { pos = foot, floor = fl, partner = a + 1, stairFoot = trigger, stairTop = stairTop });
                nodes.Add(new Node { pos = top, floor = fu, partner = a, stairFoot = trigger, stairTop = stairTop });
            }
            Debug.Log($"[FloorRouter] {nodes.Count / 2} staircases linked");

            // Only areas connected to something placed or to a staircase count as walkable goals.
            for (int f = 1; f <= 3; f++)
            {
                var grid = NavGrid.For(f);
                if (grid == null) continue;
                var ends = new List<Vector2>();
                foreach (var node in nodes) if (node.floor == f) ends.Add(node.pos);
                grid.RestrictToConnected(ends);
            }
        }

        private static int FloorFromName(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return -1;
            for (int f = 1; f <= 3; f++) if (layer == FloorAgent.LayerName(f)) return f;
            return -1;
        }

        // Fills result with waypoints from (from, fromFloor) to (to, toFloor); climbIndices gets the indices of
        // waypoints reached by climbing or descending a staircase. Returns false if unreachable.
        public static bool Route(Vector2 from, int fromFloor, Vector2 to, int toFloor, List<Vector2> result, List<int> climbIndices = null)
        {
            result.Clear();
            climbIndices?.Clear();
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
            if (prev[1] < 0)
            {
                LogFailure(from, fromFloor, to, toFloor);
                return false;
            }

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
                    // Climb along the stairs' own axis: foot cell <-> trigger <-> end of the stairs <-> top cell.
                    var stairs = nodes[a - 2];
                    bool goingUp = Floor(b) > Floor(a);
                    climbIndices?.Add(result.Count);
                    result.Add(goingUp ? stairs.stairFoot : stairs.stairTop);
                    climbIndices?.Add(result.Count);
                    result.Add(goingUp ? stairs.stairTop : stairs.stairFoot);
                    climbIndices?.Add(result.Count);
                    result.Add(Pos(b));
                }
            }
            if (result.Count == 0) LogFailure(from, fromFloor, to, toFloor);
            return result.Count > 0;
        }

        private static float nextLogAt;

        private static void LogFailure(Vector2 from, int fromFloor, Vector2 to, int toFloor)
        {
            if (Time.time < nextLogAt) return;
            nextLogAt = Time.time + 2f;
            Debug.LogWarning($"[FloorRouter] no route from {from} (floor {fromFloor}) to {to} (floor {toFloor}); {nodes.Count / 2} staircases known");
        }

        // Draws every staircase link (bottom -> top) for the NavGrid gizmos.
        public static void DrawGizmos()
        {
            if (!Application.isPlaying) return;
            EnsureBuilt();
            for (int i = 0; i + 1 < nodes.Count; i += 2)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(nodes[i].pos, nodes[i].stairFoot);
                Gizmos.DrawLine(nodes[i].stairFoot, nodes[i].stairTop);
                Gizmos.DrawLine(nodes[i].stairTop, nodes[i + 1].pos);
                Gizmos.DrawWireSphere(nodes[i].pos, 0.2f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(nodes[i + 1].pos, 0.2f);
            }
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
