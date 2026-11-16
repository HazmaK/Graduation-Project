using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Routes precise face shots to the Penitent's guarded weak point damage policy.</summary>
    [DisallowMultipleComponent]
    public sealed class PenitentWeakPoint : MonoBehaviour, IDamageable
    {
        [SerializeField, Tooltip("Penitent receiving hits on this tiny face collider.")]
        private PenitentAI owner;

        /// <summary>Forwards the hit to its owner; melee receives the ordinary guard check.</summary>
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (owner != null) owner.OnWeakPointHit(amount, hitPoint, hitNormal, isCritical);
        }
    }
}
