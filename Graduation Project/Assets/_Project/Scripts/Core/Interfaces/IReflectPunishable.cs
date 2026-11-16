using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Receives a shooter's own reflected projectile instead of ordinary damage.</summary>
    public interface IReflectPunishable
    {
        /// <summary>Applies the reflected light or heavy projectile's punishment.</summary>
        void OnOwnProjectileReflectedHit(bool heavy, float damage, Vector3 point, Vector3 normal);
    }
}
