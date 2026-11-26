using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Nonallocating ground checks for self-driven horizontal movement.</summary>
    public static class LedgeProbe
    {
        private const float RayStartHeight = 0.5f;

        /// <summary>
        /// Checks for ground within maxDrop below the look-ahead point, ignoring the caller's own colliders.
        /// Compares colliders by reference instead of reading <c>RaycastHit.transform</c>, which allocated in profiling.
        /// Zero horizontal direction is safe.
        /// </summary>
        public static bool HasGroundAhead(Vector3 feet, Vector3 horizontalDir, float lookAhead, float maxDrop,
            RaycastHit[] buffer, Collider[] ownColliders)
        {
            horizontalDir.y = 0f;
            if (horizontalDir.sqrMagnitude <= 0f) return true;
            if (buffer == null || buffer.Length == 0) return false;
            Vector3 origin = feet + horizontalDir.normalized * Mathf.Max(0f, lookAhead) + Vector3.up * RayStartHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, buffer,
                RayStartHeight + Mathf.Max(0f, maxDrop), RetroFPS.GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = buffer[i].collider;
                if (hit != null && !IsOwn(hit, ownColliders)) return true;
            }
            return false;
        }

        private static bool IsOwn(Collider hit, Collider[] ownColliders)
        {
            if (ownColliders == null) return false;
            for (int i = 0; i < ownColliders.Length; i++)
                if (ReferenceEquals(ownColliders[i], hit)) return true;
            return false;
        }
    }
}
