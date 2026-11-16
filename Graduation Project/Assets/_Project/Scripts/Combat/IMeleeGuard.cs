using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Exposes the same directional guard decision used by melee damage reception.</summary>
    public interface IMeleeGuard
    {
        /// <summary>Whether a melee attack from this world position is currently blocked.</summary>
        bool BlocksMeleeFrom(Vector3 attackerPosition);
    }
}
