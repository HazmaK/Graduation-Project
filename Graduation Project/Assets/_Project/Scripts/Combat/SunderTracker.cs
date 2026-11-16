namespace RetroFPS.Combat
{
    /// <summary>Fixed-capacity, reference-keyed Sunder state with an externally supplied clock.</summary>
    public sealed class SunderTracker
    {
        internal const int Capacity = 32;
        private readonly object[] _keys = new object[Capacity];
        private readonly float[] _expiresAt = new float[Capacity];
        private readonly float[] _multipliers = new float[Capacity];

        /// <summary>Refreshes a mark, retaining the stronger multiplier while it is active.</summary>
        public void Apply(object key, float now, float duration, float multiplier)
        {
            ReclaimExpired(now);
            if (ReferenceEquals(key, null) || !(duration > 0f)) return;
            int available = -1;
            int soonest = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (ReferenceEquals(_keys[i], key))
                {
                    _expiresAt[i] = now + duration;
                    if (multiplier > _multipliers[i]) _multipliers[i] = multiplier;
                    return;
                }
                if (ReferenceEquals(_keys[i], null) && available < 0) available = i;
                if (_expiresAt[i] < _expiresAt[soonest]) soonest = i;
            }
            int index = available >= 0 ? available : soonest;
            _keys[index] = key;
            _expiresAt[index] = now + duration;
            _multipliers[index] = multiplier > 1f ? multiplier : 1f;
        }

        /// <summary>Returns the active multiplier, or one for an absent, expired or null key.</summary>
        public float GetMultiplier(object key, float now)
        {
            ReclaimExpired(now);
            if (ReferenceEquals(key, null)) return 1f;
            for (int i = 0; i < Capacity; i++)
                if (ReferenceEquals(_keys[i], key)) return _multipliers[i];
            return 1f;
        }

        /// <summary>Returns positive time remaining for an active mark.</summary>
        public bool TryGetRemaining(object key, float now, out float remaining)
        {
            ReclaimExpired(now);
            remaining = 0f;
            if (ReferenceEquals(key, null)) return false;
            for (int i = 0; i < Capacity; i++)
            {
                if (!ReferenceEquals(_keys[i], key)) continue;
                remaining = _expiresAt[i] - now;
                return true;
            }
            return false;
        }

        /// <summary>Removes a reference-keyed mark, returning whether it was stored.</summary>
        public bool Remove(object key)
        {
            if (ReferenceEquals(key, null)) return false;
            for (int i = 0; i < Capacity; i++)
            {
                if (!ReferenceEquals(_keys[i], key)) continue;
                _keys[i] = null;
                _expiresAt[i] = 0f;
                _multipliers[i] = 1f;
                return true;
            }
            return false;
        }

        /// <summary>Releases every key and removes every mark.</summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _keys[i] = null;
                _expiresAt[i] = 0f;
                _multipliers[i] = 1f;
            }
        }

        private void ReclaimExpired(float now)
        {
            for (int i = 0; i < Capacity; i++)
                if (now >= _expiresAt[i]) _keys[i] = null;
        }
    }
}
