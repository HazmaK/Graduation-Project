using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Shared, bounded attack slots for melee enemies.</summary>
    public static class EnemyAttackTokens
    {
        private static int _maxConcurrent = 2;
        private static int _inUse;

        /// <summary>Maximum simultaneous attacks.</summary>
        public static int MaxConcurrent => _maxConcurrent;
        /// <summary>Currently occupied attack slots.</summary>
        public static int InUse => _inUse;

        /// <summary>Sets the capacity, clamped to at least one.</summary>
        public static void SetMaxConcurrent(int value) => _maxConcurrent = Mathf.Max(1, value);

        /// <summary>Acquires one slot if capacity is available.</summary>
        public static bool TryAcquire()
        {
            if (_inUse >= _maxConcurrent) return false;
            _inUse++;
            return true;
        }

        /// <summary>Releases one slot without allowing a negative count.</summary>
        public static void Release() => _inUse = Mathf.Max(0, _inUse - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _inUse = 0;
            _maxConcurrent = 2;
        }
    }
}
