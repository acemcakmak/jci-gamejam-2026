using UnityEngine;

namespace Komur
{
    // A keyboard key on the title screen that taps itself now and then, so W-A-S-D roll in sequence and
    // SPACE pounces on a beat. Shows exact 16x16 cells of the key sheet (normal / pressed) through a
    // RawImage's UV rect, so both frames line up pixel for pixel.
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public class KeyPress : MonoBehaviour
    {
        public Rect normalUv;
        public Rect pressedUv;
        public float period = 1.6f;
        public float pressTime = 0.18f;
        [Tooltip("Offset within the period, in seconds.")]
        public float phase;

        private UnityEngine.UI.RawImage image;

        private void Awake()
        {
            image = GetComponent<UnityEngine.UI.RawImage>();
        }

        private void Update()
        {
            if (period <= 0f) return;
            float t = Mathf.Repeat(Time.unscaledTime - phase, period);
            image.uvRect = t < pressTime ? pressedUv : normalUv;
        }
    }
}
