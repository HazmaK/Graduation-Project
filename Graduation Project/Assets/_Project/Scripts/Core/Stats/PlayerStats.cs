using UnityEngine;

namespace RetroFPS.Stats
{
    /// <summary>Global access to the active run's statistics.</summary>
    public static class PlayerStats
    {
        private static StatSheet _current;

        /// <summary>Active sheet, or null outside a run.</summary>
        public static StatSheet Current => _current;

        /// <summary>Assigns or clears the active sheet.</summary>
        public static void SetCurrent(StatSheet sheet) => _current = sheet;

        /// <summary>Evaluates a statistic, preserving the base value when no sheet is active.</summary>
        public static float Get(StatId stat, float baseValue)
            => _current != null ? _current.Evaluate(stat, baseValue) : baseValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;
    }
}
