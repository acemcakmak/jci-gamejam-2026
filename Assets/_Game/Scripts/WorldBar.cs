using UnityEngine;

namespace Komur
{
    // Small world-space progress bar (sickness timer) built from two sprite renderers.
    public class WorldBar : MonoBehaviour
    {
        public SpriteRenderer background;
        public SpriteRenderer fill;
        public float width = 0.8f;
        public Gradient colors;

        public void SetFill(float t)
        {
            t = Mathf.Clamp01(t);
            fill.transform.localScale = new Vector3(width * t, fill.transform.localScale.y, 1f);
            fill.transform.localPosition = new Vector3(-width * (1f - t) * 0.5f, 0f, 0f);
            if (colors != null) fill.color = colors.Evaluate(t);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
