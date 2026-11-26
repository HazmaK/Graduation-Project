using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Pure damage rules for a nonlethal fall into the Araf void.</summary>
    public static class ArafFallRules
    {
        /// <summary>Caps fall damage so at least one health point remains.</summary>
        public static float ComputeFallDamage(float currentHealth, float fallDamage)
            => Mathf.Max(0f, Mathf.Min(fallDamage, currentHealth - 1f));
    }
}
