using UnityEngine;

namespace Komur
{
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;
        [Tooltip("Orthographic size during play. 0 = keep the camera's size from the scene.")]
        public float gameplaySize;

        private static CameraFollow2D instance;
        private Vector3 velocity;
        private float shakeAmplitude;
        private float shakeTimeLeft;
        private Camera cam;
        private float zoomFrom, zoomTo, zoomStart, zoomDuration;

        private void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
            if (cam != null && gameplaySize <= 0f) gameplaySize = cam.orthographicSize;
        }

        public static float GameplaySize => instance != null ? instance.gameplaySize : 5f;

        // Smoothly change the orthographic size (used by the title screen zoom).
        public static void ZoomTo(float size, float duration)
        {
            if (instance == null || instance.cam == null) return;
            instance.zoomFrom = instance.cam.orthographicSize;
            instance.zoomTo = size;
            instance.zoomStart = Time.unscaledTime;
            instance.zoomDuration = Mathf.Max(0.0001f, duration);
            if (duration <= 0f) instance.cam.orthographicSize = size;
        }

        public static void Shake(float amplitude, float duration)
        {
            if (instance == null) return;
            instance.shakeAmplitude = Mathf.Max(instance.shakeAmplitude, amplitude);
            instance.shakeTimeLeft = Mathf.Max(instance.shakeTimeLeft, duration);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            Vector3 pos = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);

            if (shakeTimeLeft > 0f)
            {
                shakeTimeLeft -= Time.unscaledDeltaTime;
                pos += (Vector3)(Random.insideUnitCircle * shakeAmplitude);
                if (shakeTimeLeft <= 0f) shakeAmplitude = 0f;
            }

            transform.position = pos;

            if (cam != null && zoomDuration > 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - zoomStart) / zoomDuration);
                cam.orthographicSize = Mathf.Lerp(zoomFrom, zoomTo, t * t * (3f - 2f * t));
                if (t >= 1f) zoomDuration = 0f;
            }
        }
    }
}
