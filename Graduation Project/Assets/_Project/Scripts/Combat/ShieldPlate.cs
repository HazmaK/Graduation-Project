using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Routes shield collider damage to its owning Penitent's guard decision.</summary>
    [DisallowMultipleComponent]
    public sealed class ShieldPlate : MonoBehaviour, IDamageable
    {
        [Tooltip("Penitent whose guard receives hits on this collider.")]
        [SerializeField] private PenitentAI owner;

        /// <summary>Forwards the unmodified hit; an unassigned owner ignores damage.</summary>
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (owner != null) owner.OnShieldHit(amount, hitPoint, hitNormal, isCritical);
        }
    }
}
