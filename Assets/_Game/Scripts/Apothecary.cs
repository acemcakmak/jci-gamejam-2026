using UnityEngine;

namespace Komur
{
    public class Apothecary : MonoBehaviour
    {
        public float pickupRange = 1.3f;

        private void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || gm.komur == null) return;
            if (gm.komur.HasMedicine) return;
            if ((gm.komur.transform.position - transform.position).sqrMagnitude <= pickupRange * pickupRange)
                gm.komur.SetMedicine(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
