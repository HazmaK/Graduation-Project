using System;

namespace RetroFPS
{
    /// <summary>Allocation-free strike window and whiff recovery state, independent of Unity.</summary>
    public sealed class CounterVerdictTimer
    {
        private float _activeWindow;
        private float _whiffRecovery;
        private float _activeRemaining;
        private float _recoveryRemaining;
        private bool _succeeded;

        /// <summary>Creates an idle timer with non-negative window and recovery durations.</summary>
        public CounterVerdictTimer(float activeWindow, float whiffRecovery)
        {
            Configure(activeWindow, whiffRecovery);
        }

        /// <summary>Whether the current strike can still parry attacks.</summary>
        public bool IsActive => _activeRemaining > 0f;
        /// <summary>Whether an unsuccessful strike is recovering.</summary>
        public bool IsRecovering => _recoveryRemaining > 0f;
        /// <summary>Whether another strike can start, including during a successful window.</summary>
        public bool IsReady => !IsRecovering && (!IsActive || _succeeded);
        /// <summary>Seconds remaining in whiff recovery; zero outside recovery.</summary>
        public float RecoveryRemaining => _recoveryRemaining;
        /// <summary>Recovery fraction, from one at entry to zero when ready.</summary>
        public float RecoveryNormalized => IsRecovering ? _recoveryRemaining / _whiffRecovery : 0f;

        /// <summary>Starts or restarts a ready strike and clears its previous success.</summary>
        public bool TryStart()
        {
            if (!IsReady) return false;
            _succeeded = false;
            _activeRemaining = _activeWindow;
            _recoveryRemaining = IsActive ? 0f : _whiffRecovery;
            return true;
        }

        /// <summary>Advances both phases, carrying time beyond the window into whiff recovery.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (IsActive)
            {
                if (deltaTime < _activeRemaining)
                {
                    _activeRemaining -= deltaTime;
                    return;
                }

                deltaTime -= _activeRemaining;
                _activeRemaining = 0f;
                _recoveryRemaining = _succeeded ? 0f : _whiffRecovery;
            }

            _recoveryRemaining = Math.Max(0f, _recoveryRemaining - deltaTime);
        }

        /// <summary>Makes an active strike immediately ready again without closing its window.</summary>
        public void NotifySuccess()
        {
            if (IsActive) _succeeded = true;
        }

        /// <summary>Clears the current strike and recovery, returning to idle.</summary>
        public void Reset()
        {
            _activeRemaining = 0f;
            _recoveryRemaining = 0f;
            _succeeded = false;
        }

        /// <summary>Clamps new durations to zero and resets the timer to idle.</summary>
        public void Configure(float activeWindow, float whiffRecovery)
        {
            _activeWindow = Math.Max(0f, activeWindow);
            _whiffRecovery = Math.Max(0f, whiffRecovery);
            Reset();
        }
    }
}
