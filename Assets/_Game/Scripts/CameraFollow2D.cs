using UnityEngine;

namespace Komur
{
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;

        private static CameraFollow2D instance;
        private Vector3 velocity;
        private float shakeAmplitude;
        private float shakeTimeLeft;

        private void Awake()
        {
            instance = this;
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
        }
    }
}
