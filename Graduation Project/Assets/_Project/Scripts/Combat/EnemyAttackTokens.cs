using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Independent, bounded attack budgets with a legacy melee facade.</summary>
    public static class EnemyAttackTokens
    {
        private static readonly int[] _maxConcurrent = { 2, 1, 2 };
        private static readonly int[] _inUse = new int[3];
        private static readonly int[] _limitOffset = new int[3];

        /// <summary>Maximum simultaneous attacks.</summary>
        public static int MaxConcurrent => MaxOf(AttackTokenChannel.Melee);
        /// <summary>Currently occupied attack slots.</summary>
        public static int InUse => InUseOf(AttackTokenChannel.Melee);

        /// <summary>Sets the capacity, clamped to at least one.</summary>
        public static void SetMaxConcurrent(int value) => SetMaxConcurrent(AttackTokenChannel.Melee, value);

        /// <summary>Sets a channel's default capacity, clamped to at least one.</summary>
        public static void SetMaxConcurrent(AttackTokenChannel channel, int value) =>
            _maxConcurrent[(int)channel] = Mathf.Max(1, value);

        /// <summary>Returns the channel's default capacity.</summary>
        public static int MaxOf(AttackTokenChannel channel) => _maxConcurrent[(int)channel];

        /// <summary>Difficulty offset added to every capacity of a channel (default or caller limit), result at least one.</summary>
        public static void SetLimitOffset(AttackTokenChannel channel, int offset) =>
            _limitOffset[(int)channel] = offset;

        /// <summary>Returns the channel's difficulty offset.</summary>
        public static int LimitOffsetOf(AttackTokenChannel channel) => _limitOffset[(int)channel];

        /// <summary>Returns the effective capacity for a caller limit (nonpositive uses the channel default).</summary>
        public static int CapacityOf(AttackTokenChannel channel, int limit)
        {
            int index = (int)channel;
            return Mathf.Max(1, (limit > 0 ? limit : _maxConcurrent[index]) + _limitOffset[index]);
        }

        /// <summary>Returns the number of occupied slots in a channel.</summary>
        public static int InUseOf(AttackTokenChannel channel) => _inUse[(int)channel];

        /// <summary>Acquires one slot if capacity is available.</summary>
        public static bool TryAcquire() => TryAcquire(AttackTokenChannel.Melee, 0);

        /// <summary>Acquires using a caller limit, or the channel default when limit is nonpositive; the difficulty offset is added.</summary>
        public static bool TryAcquire(AttackTokenChannel channel, int limit)
        {
            int index = (int)channel;
            if (_inUse[index] >= CapacityOf(channel, limit)) return false;
            _inUse[index]++;
            return true;
        }

        /// <summary>Releases one slot without allowing a negative count.</summary>
        public static void Release() => Release(AttackTokenChannel.Melee);

        /// <summary>Releases a channel slot without allowing a negative count.</summary>
        public static void Release(AttackTokenChannel channel)
        {
            int index = (int)channel;
            if (_inUse[index] > 0) _inUse[index]--;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < _inUse.Length; i++) { _inUse[i] = 0; _limitOffset[i] = 0; }
            _maxConcurrent[0] = 2;
            _maxConcurrent[1] = 1;
            _maxConcurrent[2] = 2;
        }
    }
}
