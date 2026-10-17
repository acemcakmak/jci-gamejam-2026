using UnityEngine;

namespace Komur
{
    // Line-of-sight test: walls, props and crates block sight; characters don't.
    public static class Sight
    {
        private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        public static bool Clear(Vector2 from, Vector2 to, int floor)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(FloorAgent.ObstacleMask(floor));

            int n = Physics2D.Linecast(from, to, filter, hits);
            for (int i = 0; i < n; i++)
            {
                if (NavGrid.IsCharacter(hits[i].collider)) continue;
                return false;
            }
            return true;
        }
    }
}
