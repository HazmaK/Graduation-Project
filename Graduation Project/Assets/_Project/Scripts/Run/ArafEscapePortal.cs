using UnityEngine;

namespace RetroFPS.Run
{
    /// <summary>
    /// Araf'tan kuleye geri dönüşü tetikleyen kan portalı (Trigger Collider).
    /// Canlı oyuncu içine girdiğinde <see cref="RunEvents.RaiseEscapePortalReached"/> olayını yükseltir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class ArafEscapePortal : MonoBehaviour
    {
        private bool _triggered;

        private void OnEnable()
        {
            _triggered = false;
        }

        private void Reset()
        {
            if (TryGetComponent(out Collider col))
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || other == null) return;

            PlayerHealth player = null;
            if (!other.TryGetComponent(out player) && other.attachedRigidbody != null)
            {
                other.attachedRigidbody.TryGetComponent(out player);
            }
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerHealth>();
            }

            if (player == null || player.IsDead) return;

            _triggered = true;
            RunEvents.RaiseEscapePortalReached();
        }
    }
}
