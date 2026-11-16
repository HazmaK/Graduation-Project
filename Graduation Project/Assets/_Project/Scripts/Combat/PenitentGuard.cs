using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Pure frontal shield decisions on the horizontal plane.</summary>
    public static class PenitentGuard
    {
        private const float CentreTolerance = 0.05f;
        private const float DirectionEpsilon = 0.000001f;
        private const float BoundaryTolerance = 0.000001f;

        /// <summary>Tests a hit point against the shield arc, using the attacker-facing normal for centre hits.</summary>
        public static bool IsBlocked(Vector3 forward, Vector3 enemyPosition, Vector3 hitPoint,
            Vector3 hitNormal, float halfAngleDeg, bool guardUp)
        {
            if (!guardUp) return false;
            Vector3 toHit = hitPoint - enemyPosition;
            toHit.y = 0f;
            if (toHit.sqrMagnitude < CentreTolerance * CentreTolerance)
            {
                toHit = hitNormal;
            }
            return IsBlocked(forward, toHit, halfAngleDeg, guardUp);
        }

        /// <summary>Tests only the direction toward the attacker, ignoring overlap collider positions.</summary>
        public static bool IsBlocked(Vector3 forward, Vector3 attackerDirection, float halfAngleDeg, bool guardUp)
        {
            if (!guardUp) return false;
            forward.y = 0f;
            Vector3 toHit = attackerDirection;
            toHit.y = 0f;
            if (forward.sqrMagnitude < DirectionEpsilon || toHit.sqrMagnitude < DirectionEpsilon) return false;
            float threshold = Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward.normalized, toHit.normalized) + BoundaryTolerance >= threshold;
        }
    }
}
