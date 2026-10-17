using UnityEngine;
using UnityEngine.Rendering;

namespace Komur
{
    // Characters sort as one unit at their feet (the root), not by each sprite's centre: the cat and rat
    // sprites sit in tall frames, so sorting by centre put walls and props at foot level on top of them.
    public static class CharacterSorting
    {
        public static SortingGroup Ensure(GameObject root, SpriteRenderer reference)
        {
            if (!root.TryGetComponent(out SortingGroup group)) group = root.AddComponent<SortingGroup>();
            if (reference != null)
            {
                group.sortingLayerID = reference.sortingLayerID;
                group.sortingOrder = reference.sortingOrder;
            }
            return group;
        }
    }
}
