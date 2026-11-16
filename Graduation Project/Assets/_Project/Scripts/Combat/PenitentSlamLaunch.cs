using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Pure velocity calculation for the Penitent's upward, down-lane slam launch.</summary>
    public static class PenitentSlamLaunch
    {
        /// <summary>Flattens and normalizes the cached lane direction before adding the upward speed.
        /// A degenerate horizontal direction contributes no push.</summary>
        public static Vector3 Compute(Vector3 areaForward, float upSpeed, float pushSpeed)
        {
            areaForward.y = 0f;
            Vector3 horizontal = areaForward.sqrMagnitude > 0.000001f
                ? areaForward.normalized * pushSpeed : Vector3.zero;
            return horizontal + Vector3.up * upSpeed;
        }
    }
}
