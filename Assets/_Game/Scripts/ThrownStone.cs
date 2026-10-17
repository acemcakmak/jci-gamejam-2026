using UnityEngine;

namespace Komur
{
    // Thrown by a frightened villager at the spot Kömür was standing; dodgeable.
    public class ThrownStone : MonoBehaviour
    {
        public float speed = 6f;
        public float hitRadius = 0.4f;
        public float spinSpeed = 720f;

        private KomurController target;
        private Vector3 destination;
        private float stunDuration;

        public void Launch(KomurController komur, float stun)
        {
            target = komur;
            destination = komur.transform.position;
            stunDuration = stun;
            Destroy(gameObject, 3f);
        }

        private void Update()
        {
            if (target == null) return;
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            if ((target.transform.position - transform.position).sqrMagnitude <= hitRadius * hitRadius)
            {
                target.Stun(stunDuration);
                Destroy(gameObject);
            }
            else if ((destination - transform.position).sqrMagnitude < 0.0001f)
            {
                Destroy(gameObject);
            }
        }
    }
}
