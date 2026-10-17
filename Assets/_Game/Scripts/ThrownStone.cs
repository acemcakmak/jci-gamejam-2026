using UnityEngine;

namespace Komur
{
    // Thrown by a frightened villager at the spot Kömür was standing; flies in an arc with a ground
    // shadow and can be dodged.
    public class ThrownStone : MonoBehaviour
    {
        public float speed = 6f;
        public float hitRadius = 0.45f;
        public float spinSpeed = 540f;
        public float arcHeight = 0.9f;
        [Tooltip("The rock sprite; lifted along the arc while the root slides along the ground.")]
        public Transform visual;
        public Transform shadow;

        private KomurController target;
        private Vector3 start;
        private Vector3 destination;
        private float stunDuration;
        private float travelled;
        private float distance;

        public void Launch(KomurController komur, float stun)
        {
            target = komur;
            start = transform.position;
            destination = komur.transform.position;
            distance = Mathf.Max(0.1f, Vector3.Distance(start, destination));
            stunDuration = stun;
            Destroy(gameObject, 4f);
        }

        private void Update()
        {
            if (target == null) return;
            travelled += speed * Time.deltaTime;
            float t = Mathf.Clamp01(travelled / distance);
            transform.position = Vector3.Lerp(start, destination, t);

            float lift = arcHeight * 4f * t * (1f - t);
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, lift, 0f);
                visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
            if (shadow != null) shadow.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, lift / Mathf.Max(0.01f, arcHeight));

            // Only hits once it's coming down near the ground.
            if (t > 0.6f && (target.transform.position - transform.position).sqrMagnitude <= hitRadius * hitRadius)
            {
                target.Stun(stunDuration);
                Destroy(gameObject);
            }
            else if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
