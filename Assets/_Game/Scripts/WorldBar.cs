using UnityEngine;

namespace Komur
{
    // Small world-space bar (sickness timer, rat health) built from sprite renderers.
    // With a chip renderer it shows the classic "damage drain": the fill drops at once and a pale
    // chip trails behind it, and the bar punches up in size on every hit.
    public class WorldBar : MonoBehaviour
    {
        public SpriteRenderer background;
        public SpriteRenderer fill;
        [Tooltip("Optional trailing segment that drains after the fill drops.")]
        public SpriteRenderer chip;
        public float width = 0.8f;
        public Gradient colors;
        public float chipDelay = 0.15f;
        public float chipSpeed = 2f;

        private float value = 1f;
        private float chipValue = 1f;
        private float chipStartAt;
        private float punchUntil;
        private Vector3 baseScale = Vector3.one;
        private bool scaleCaptured;

        public void SetFill(float t)
        {
            t = Mathf.Clamp01(t);
            if (!scaleCaptured) { baseScale = transform.localScale; scaleCaptured = true; }
            if (t < value - 0.001f)
            {
                chipStartAt = Time.time + chipDelay;
                punchUntil = Time.time + 0.15f;
            }
            else if (t > value)
            {
                chipValue = t;
            }
            value = t;
            Apply(fill, value);
            if (colors != null) fill.color = colors.Evaluate(value);
            if (chip != null) Apply(chip, chipValue);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (chip != null && chipValue > value && Time.time >= chipStartAt)
            {
                chipValue = Mathf.MoveTowards(chipValue, value, chipSpeed * Time.deltaTime);
                Apply(chip, chipValue);
            }
            if (scaleCaptured)
            {
                float punch = Mathf.Clamp01((punchUntil - Time.time) / 0.15f);
                transform.localScale = baseScale * (1f + 0.25f * punch);
            }
        }

        private void Apply(SpriteRenderer r, float t)
        {
            if (r == null) return;
            r.transform.localScale = new Vector3(width * t, r.transform.localScale.y, 1f);
            r.transform.localPosition = new Vector3(-width * (1f - t) * 0.5f, 0f, 0f);
        }
    }
}
