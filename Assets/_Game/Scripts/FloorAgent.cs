using UnityEngine;

namespace Komur
{
    // Which Cainos floor ("Layer 1/2/3") a villager, rat or rat hole lives on.
    // Sets sorting layers for all renderers and makes colliders ignore the other floors' walls.
    [DisallowMultipleComponent]
    public class FloorAgent : MonoBehaviour
    {
        [Range(1, 3)] public int floor = 1;

        public static string LayerName(int floor) => "Layer " + floor;

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            ApplySorting();
        }

        public void SetFloor(int newFloor)
        {
            floor = newFloor;
            Apply();
        }

        public void Apply()
        {
            ApplySorting();
            ApplyCollision();
        }

        private void ApplySorting()
        {
            int id = SortingLayer.NameToID(LayerName(floor));
            if (!SortingLayer.IsValid(id)) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.sortingLayerID = id;
        }

        private void ApplyCollision()
        {
            int mask = 0;
            for (int f = 1; f <= 3; f++)
            {
                if (f == floor) continue;
                int layer = LayerMask.NameToLayer(LayerName(f));
                if (layer >= 0) mask |= 1 << layer;
            }
            foreach (var col in GetComponents<Collider2D>()) col.excludeLayers = mask;
        }

        public static int FloorOf(GameObject go)
        {
            if (go.TryGetComponent(out FloorAgent agent)) return agent.floor;
            for (int f = 1; f <= 3; f++)
                if (go.layer == LayerMask.NameToLayer(LayerName(f))) return f;
            return 1;
        }

        // Physics layers that count as walls/obstacles on a floor.
        public static int ObstacleMask(int floor)
        {
            return floor == 1
                ? LayerMask.GetMask("Default", LayerName(1))
                : LayerMask.GetMask(LayerName(floor));
        }
    }
}
