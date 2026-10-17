using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Komur
{
    // Walkability grid with A* for one Cainos floor. Rebuilds a few rows every frame so pushed
    // crates/barrels and other moved obstacles are picked up continuously.
    [DefaultExecutionOrder(-90)]
    public class NavGrid : MonoBehaviour
    {
        private static readonly Dictionary<int, NavGrid> grids = new Dictionary<int, NavGrid>();

        // Ground-floor grid (kept for convenience).
        public static NavGrid I => For(1);

        public static NavGrid For(int floor)
        {
            return grids.TryGetValue(floor, out var g) && g != null && g.IsReady ? g : null;
        }

        [Range(1, 3)] public int floor = 1;
        public float cellSize = 0.5f;
        [Tooltip("Half-size of the box checked for obstacles around each cell centre.")]
        public float clearance = 0.3f;
        public int rowsRefreshedPerFrame = 6;
        public int maxSearchNodes = 8000;
        public bool drawGizmos;

        private Vector2 origin;
        private int width, height;
        private bool[] ground;
        private bool[] walkable;
        private bool[] reachable; // connected to Kömür's start at build time; used for snapping/spawning
        private int refreshRow;
        private ContactFilter2D obstacleFilter;
        private ContactFilter2D stairsFilter;
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];

        // A* scratch space, reused between searches.
        private float[] gScore;
        private int[] cameFrom;
        private int[] visitStamp;
        private int[] closedStamp;
        private int stamp;
        private MinHeap open;

        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        public bool IsReady => walkable != null;

        private void OnEnable()
        {
            grids[floor] = this;
        }

        private void OnDisable()
        {
            if (grids.TryGetValue(floor, out var g) && g == this) grids.Remove(floor);
        }

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            Physics2D.SyncTransforms();
            grids[floor] = this;
            string floorLayer = FloorAgent.LayerName(floor);
            obstacleFilter = new ContactFilter2D { useTriggers = false };
            obstacleFilter.SetLayerMask(FloorAgent.ObstacleMask(floor));
            stairsFilter = new ContactFilter2D { useTriggers = true };
            stairsFilter.SetLayerMask(Physics2D.AllLayers);
            walkable = null;

            // Floor = tilemaps on the ground sorting layer that have no collider (walls have one).
            var floors = FindObjectsByType<Tilemap>(FindObjectsSortMode.None)
                .Where(tm => tm.GetComponent<TilemapRenderer>() is TilemapRenderer r
                             && r.sortingLayerName == floorLayer
                             && tm.GetComponent<TilemapCollider2D>() == null)
                .ToArray();

            if (floors.Length == 0)
            {
                if (floor == 1) Debug.LogWarning("[NavGrid] No floor tilemaps found on sorting layer " + floorLayer);
                return;
            }

            Bounds bounds = default;
            bool first = true;
            foreach (var tm in floors)
            {
                tm.CompressBounds();
                var cb = tm.cellBounds;
                var min = tm.CellToWorld(cb.min);
                var max = tm.CellToWorld(cb.max);
                var b = new Bounds((min + max) * 0.5f, max - min);
                if (first) { bounds = b; first = false; }
                else bounds.Encapsulate(b);
            }

            origin = bounds.min;
            width = Mathf.CeilToInt(bounds.size.x / cellSize);
            height = Mathf.CeilToInt(bounds.size.y / cellSize);
            if (width <= 0 || height <= 0) return; // floor has tilemaps but no tiles
            int count = width * height;
            ground = new bool[count];
            walkable = new bool[count];
            reachable = new bool[count];
            gScore = new float[count];
            cameFrom = new int[count];
            visitStamp = new int[count];
            closedStamp = new int[count];
            open = new MinHeap(count);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector3 c = CellCenter(x, y);
                foreach (var tm in floors)
                {
                    if (!tm.HasTile(tm.WorldToCell(c))) continue;
                    ground[x + y * width] = true;
                    break;
                }
            }

            for (int y = 0; y < height; y++) RefreshRow(y);

            // Seeds: everything placed on this floor (villagers, rat holes, apothecary) and Kömür on the ground.
            var seeds = new List<Vector2>();
            foreach (var agent in FindObjectsByType<FloorAgent>(FindObjectsSortMode.None))
                if (agent.floor == floor) seeds.Add(agent.transform.position);
            if (floor == 1)
            {
                var komur = GameManager.I != null && GameManager.I.komur != null
                    ? GameManager.I.komur
                    : FindFirstObjectByType<KomurController>();
                if (komur != null) seeds.Add(komur.transform.position);
                foreach (var apothecary in FindObjectsByType<Apothecary>(FindObjectsSortMode.None))
                    seeds.Add(apothecary.transform.position);
            }
            ComputeReachable(seeds);
        }

        // Flood fill from the seeds so spawns never land in walled-off areas.
        public void ComputeReachable(List<Vector2> seeds)
        {
            System.Array.Clear(reachable, 0, reachable.Length);
            var queue = new Queue<int>();
            foreach (var seed in seeds)
            {
                ToCell(seed, out int sx, out int sy);
                int start = -1;
                for (int ring = 0; ring <= 6 && start < 0; ring++)
                for (int dy = -ring; dy <= ring && start < 0; dy++)
                for (int dx = -ring; dx <= ring && start < 0; dx++)
                    if (Walkable(sx + dx, sy + dy)) start = (sx + dx) + (sy + dy) * width;
                if (start < 0 || reachable[start]) continue;
                reachable[start] = true;
                queue.Enqueue(start);
            }

            if (queue.Count == 0)
            {
                System.Array.Copy(walkable, reachable, walkable.Length);
                return;
            }

            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % width, cy = c / width;
                for (int n = 0; n < 4; n++)
                {
                    int nx = cx + Neighbours[n].x, ny = cy + Neighbours[n].y;
                    if (!Walkable(nx, ny)) continue;
                    int ni = nx + ny * width;
                    if (reachable[ni]) continue;
                    reachable[ni] = true;
                    queue.Enqueue(ni);
                }
            }
        }

        private bool Usable(int x, int y)
        {
            return Walkable(x, y) && reachable[x + y * width];
        }

        private void Update()
        {
            if (!IsReady) return;
            for (int i = 0; i < rowsRefreshedPerFrame; i++)
            {
                RefreshRow(refreshRow);
                refreshRow = (refreshRow + 1) % height;
            }
        }

        private void RefreshRow(int y)
        {
            for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                walkable[i] = ground[i] && !Blocked(CellCenter(x, y));
            }
        }

        private bool Blocked(Vector2 center)
        {
            Vector2 size = Vector2.one * (clearance * 2f);
            int n = Physics2D.OverlapBox(center, size, 0f, obstacleFilter, overlapBuffer);
            for (int i = 0; i < n; i++)
            {
                var col = overlapBuffer[i];
                if (col.isTrigger || IsCharacter(col)) continue;
                return true;
            }

            // Keep NPCs off the stairs so they never switch floors.
            n = Physics2D.OverlapBox(center, size, 0f, stairsFilter, overlapBuffer);
            for (int i = 0; i < n; i++)
                if (overlapBuffer[i].isTrigger && overlapBuffer[i].GetComponent("StairsLayerTrigger") != null) return true;
            return false;
        }

        public static bool IsCharacter(Collider2D col)
        {
            var rb = col.attachedRigidbody;
            if (rb == null) return false;
            return rb.GetComponent<KomurController>() != null
                   || rb.GetComponent<Villager>() != null
                   || rb.GetComponent<Rat>() != null;
        }

        // ---------- Queries ----------

        public Vector3 CellCenter(int x, int y)
        {
            return new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize, 0f);
        }

        private bool ToCell(Vector2 p, out int x, out int y)
        {
            x = Mathf.FloorToInt((p.x - origin.x) / cellSize);
            y = Mathf.FloorToInt((p.y - origin.y) / cellSize);
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private bool Walkable(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height && walkable[x + y * width];
        }

        public bool IsWalkable(Vector2 p)
        {
            return IsReady && ToCell(p, out int x, out int y) && walkable[x + y * width];
        }

        public bool TryNearestWalkable(Vector2 p, int maxRing, out Vector2 result)
        {
            result = p;
            if (!IsReady) return false;
            ToCell(p, out int cx, out int cy);
            for (int ring = 0; ring <= maxRing; ring++)
            {
                float best = float.MaxValue;
                bool found = false;
                for (int dy = -ring; dy <= ring; dy++)
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    if (!Usable(cx + dx, cy + dy)) continue;
                    Vector2 c = CellCenter(cx + dx, cy + dy);
                    float d = (c - p).sqrMagnitude;
                    if (d < best) { best = d; result = c; found = true; }
                }
                if (found) return true;
            }
            return false;
        }

        public Vector2 SnapToWalkable(Vector2 p)
        {
            return TryNearestWalkable(p, 20, out var r) ? r : p;
        }

        public bool TryRandomWalkableNear(Vector2 p, float radius, out Vector2 result)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector2 candidate = p + Random.insideUnitCircle * radius;
                if (ToCell(candidate, out int x, out int y) && Usable(x, y))
                {
                    result = CellCenter(x, y);
                    return true;
                }
            }
            result = p;
            return false;
        }

        // ---------- A* ----------

        public bool FindPath(Vector2 from, Vector2 to, List<Vector2> result)
        {
            result.Clear();
            if (!IsReady) return false;
            if (!TryNearestWalkable(from, 3, out var start)) return false;
            if (!TryNearestWalkable(to, 3, out var goal)) return false;
            ToCell(start, out int sx, out int sy);
            ToCell(goal, out int gx, out int gy);
            int startIdx = sx + sy * width;
            int goalIdx = gx + gy * width;

            stamp++;
            open.Clear();
            gScore[startIdx] = 0f;
            cameFrom[startIdx] = -1;
            visitStamp[startIdx] = stamp;
            open.Push(startIdx, Heuristic(sx, sy, gx, gy));

            int expanded = 0;
            bool found = false;
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closedStamp[current] == stamp) continue;
                closedStamp[current] = stamp;
                if (current == goalIdx) { found = true; break; }
                if (++expanded > maxSearchNodes) break;

                int cx = current % width, cy = current / width;
                for (int n = 0; n < Neighbours.Length; n++)
                {
                    int nx = cx + Neighbours[n].x, ny = cy + Neighbours[n].y;
                    if (!Walkable(nx, ny)) continue;
                    bool diagonal = n >= 4;
                    if (diagonal && (!Walkable(cx + Neighbours[n].x, cy) || !Walkable(cx, cy + Neighbours[n].y))) continue;
                    int ni = nx + ny * width;
                    if (closedStamp[ni] == stamp) continue;
                    float g = gScore[current] + (diagonal ? 1.4142f : 1f);
                    if (visitStamp[ni] == stamp && g >= gScore[ni]) continue;
                    visitStamp[ni] = stamp;
                    gScore[ni] = g;
                    cameFrom[ni] = current;
                    open.Push(ni, g + Heuristic(nx, ny, gx, gy));
                }
            }

            if (!found) return false;

            var cells = new List<int>();
            for (int c = goalIdx; c != -1; c = cameFrom[c]) cells.Add(c);
            cells.Reverse();

            // String-pull: skip waypoints while there is a clear grid line.
            int anchor = 0;
            for (int i = 1; i < cells.Count; i++)
            {
                if (i == cells.Count - 1 || !GridLineClear(cells[anchor], cells[i + 1]))
                {
                    result.Add(CellCenter(cells[i] % width, cells[i] / width));
                    anchor = i;
                }
            }
            if (result.Count == 0) result.Add(goal);
            return true;
        }

        private static float Heuristic(int ax, int ay, int bx, int by)
        {
            int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
        }

        private bool GridLineClear(int a, int b)
        {
            int x0 = a % width, y0 = a / width, x1 = b % width, y1 = b / width;
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                if (!Walkable(x0, y0)) return false;
                if (x0 == x1 && y0 == y1) return true;
                int e2 = 2 * err;
                if (e2 > -dy && e2 < dx)
                {
                    // Diagonal step: both orthogonal neighbours must be free (no corner cutting).
                    if (!Walkable(x0 + sx, y0) || !Walkable(x0, y0 + sy)) return false;
                }
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        [ContextMenu("Rebuild Grid Preview")]
        private void RebuildPreview()
        {
            Build();
        }

        // Green = walkable and reachable, yellow = walkable but cut off from Kömür, red = blocked.
        // In edit mode the grid is built once when gizmos are enabled; use the context menu to refresh it.
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!IsReady && !Application.isPlaying) Build();
            if (!IsReady) return;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                if (!ground[i]) continue;
                if (floor > 1 && !walkable[i]) continue; // keep upper-floor previews readable
                Gizmos.color = !walkable[i] ? new Color(1f, 0f, 0f, 0.35f)
                    : reachable[i] ? new Color(0f, 1f, 0f, 0.25f)
                    : new Color(1f, 0.9f, 0f, 0.35f);
                Gizmos.DrawCube(CellCenter(x, y), Vector3.one * cellSize * 0.9f);
            }
        }

        private class MinHeap
        {
            private readonly int[] items;
            private readonly float[] priorities;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                items = new int[capacity * 8 + 8];
                priorities = new float[capacity * 8 + 8];
            }

            public void Clear() { Count = 0; }

            public void Push(int item, float priority)
            {
                if (Count >= items.Length) return;
                int i = Count++;
                items[i] = item;
                priorities[i] = priority;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (priorities[parent] <= priorities[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                int result = items[0];
                Count--;
                items[0] = items[Count];
                priorities[0] = priorities[Count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < Count && priorities[l] < priorities[smallest]) smallest = l;
                    if (r < Count && priorities[r] < priorities[smallest]) smallest = r;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
                return result;
            }

            private void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
            }
        }
    }
}
