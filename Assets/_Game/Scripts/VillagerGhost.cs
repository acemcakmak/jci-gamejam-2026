using UnityEngine;

namespace Komur
{
    // What a villager leaves behind: their spirit rises from where they fell and keeps floating there,
    // see-through and drawn above everything. No collider, so a death never walls off part of the map.
    public class VillagerGhost : MonoBehaviour
    {
        public Color tint = new Color(0.7f, 0.95f, 1f, 0.75f);
        public float rise = 0.7f;
        public float riseTime = 1.6f;
        public float bob = 0.12f;
        public float fps = 4f;

        private SpriteRenderer sr;
        private Sprite[] frames;
        private Vector3 origin;
        private float bornAt;
        private float phase;

        public static VillagerGhost Spawn(Sprite[] frames, bool flipX, Vector3 position, Material material, int floor)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("Spirit");
            go.transform.position = position;
            var ghost = go.AddComponent<VillagerGhost>();
            ghost.sr = go.AddComponent<SpriteRenderer>();
            ghost.sr.sprite = frames[0];
            ghost.sr.flipX = flipX;
            if (material != null) ghost.sr.sharedMaterial = material;
            // Spirits drift above the rooftops: always visible, over every floor.
            ghost.sr.sortingLayerName = "Layer 3";
            ghost.sr.sortingOrder = 150 + floor;
            ghost.frames = frames;
            ghost.origin = position;
            ghost.bornAt = Time.time;
            ghost.phase = Random.value * 10f;
            return ghost;
        }

        private void Update()
        {
            float age = Time.time - bornAt;
            float up = Mathf.SmoothStep(0f, rise, Mathf.Clamp01(age / riseTime));
            float t = Time.time + phase;
            transform.position = origin + new Vector3(Mathf.Sin(t * 0.9f) * 0.08f, up + Mathf.Sin(t * 1.7f) * bob, 0f);
            var c = tint;
            c.a = tint.a * Mathf.Clamp01(age / 0.6f) * Mathf.Lerp(0.75f, 1f, 0.5f + 0.5f * Mathf.Sin(t * 2.3f));
            sr.color = c;
            sr.sprite = frames[(int)(t * fps) % frames.Length];
        }
    }
}
