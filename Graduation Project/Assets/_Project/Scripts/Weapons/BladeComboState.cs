using System;

namespace RetroFPS.Weapons
{
    /// <summary>Allocation-free combo clock. Each press is an edge, never a held-button repeat.</summary>
    public sealed class BladeComboState
    {
        private const double Tolerance = 0.0000001;
        private readonly float _firstCooldown;
        private readonly float _secondCooldown;
        private readonly float _finisherCooldown;
        private readonly float _resetTimeout;
        private readonly float _bufferWindow;
        private readonly float _windup;
        private double _time;
        private double _readyAt;
        private double _resetAt;
        private double _finisherAt;
        private double _bufferExpiresAt;
        private bool _buffered;
        private bool _pendingFinisher;
        private int _nextStep = 1;

        /// <summary>Creates a clock with the supplied cooldown, reset, buffer and wind-up tuning.</summary>
        public BladeComboState(float firstCooldown = 0.22f, float secondCooldown = 0.22f,
            float finisherCooldown = 0.44f, float resetTimeout = 0.70f,
            float bufferWindow = 0.15f, float windup = 0.10f)
        {
            _firstCooldown = Math.Max(0f, firstCooldown);
            _secondCooldown = Math.Max(0f, secondCooldown);
            _finisherCooldown = Math.Max(0f, finisherCooldown);
            _resetTimeout = Math.Max(0f, resetTimeout);
            _bufferWindow = Math.Max(0f, bufferWindow);
            _windup = Math.Max(0f, windup);
        }

        /// <summary>Next attack in the one-based three-step chain.</summary>
        public int NextStep => _nextStep;
        /// <summary>Seconds until the primary attack becomes legal.</summary>
        public float CooldownRemaining => (float)Math.Max(0d, _readyAt - _time);
        /// <summary>Whether the overhead hit still awaits resolution.</summary>
        public bool HasPendingFinisher => _pendingFinisher;
        /// <summary>Whether a new input edge can fire or enter the buffer.</summary>
        public bool CanAcceptPress => _pendingFinisher || CooldownRemaining <= _bufferWindow + Tolerance;
        /// <summary>Whether one fresh input edge is remembered.</summary>
        public bool HasBufferedPress => _buffered;

        /// <summary>Advances timers without starting or resolving any attack.</summary>
        public void Advance(float deltaTime)
        {
            _time += Math.Max(0f, deltaTime);
            if (_time > _resetAt + Tolerance) _nextStep = 1;
            if (_buffered && _time > _bufferExpiresAt + Tolerance
                && (_readyAt > _bufferExpiresAt + Tolerance
                    || (_pendingFinisher && _finisherAt > _bufferExpiresAt + Tolerance))) _buffered = false;
        }

        /// <summary>Starts a legal press immediately, otherwise remembers an eligible edge.</summary>
        public bool Press(out int startedStep)
        {
            startedStep = 0;
            if (!_pendingFinisher && CooldownRemaining <= Tolerance)
            {
                startedStep = StartAttack();
                return true;
            }
            if (!CanAcceptPress) return false;
            _buffered = true;
            // Wind-up presses are explicitly accepted even when recovery exceeds the normal buffer.
            _bufferExpiresAt = _time + (_pendingFinisher
                ? Math.Max(_bufferWindow, CooldownRemaining) : _bufferWindow);
            return false;
        }

        /// <summary>Consumes the delayed overhead hit exactly once when its wind-up has elapsed.</summary>
        public bool TryResolveFinisher()
        {
            if (!_pendingFinisher || _time + Tolerance < _finisherAt) return false;
            _pendingFinisher = false;
            return true;
        }

        /// <summary>Starts the remembered press on the first legal frame, including a long frame.</summary>
        public bool TryStartBuffered(out int startedStep)
        {
            startedStep = 0;
            if (!_buffered || _pendingFinisher || CooldownRemaining > Tolerance) return false;
            _buffered = false;
            if (_readyAt > _bufferExpiresAt + Tolerance) return false;
            startedStep = StartAttack();
            return true;
        }

        /// <summary>Cancels delayed damage and buffered input, retaining cooldown and chain timing.</summary>
        public void CancelPending()
        {
            _pendingFinisher = false;
            _buffered = false;
        }

        /// <summary>Interrupts pending primary work and imposes an alternate attack's recovery.</summary>
        public void DelayPrimary(float duration)
        {
            CancelPending();
            _readyAt = _time + Math.Max(0f, duration);
        }

        /// <summary>Restarts the chain and adds recovery after an entirely guarded swing.</summary>
        public void GuardBounce(float additionalCooldown)
        {
            CancelPending();
            _nextStep = 1;
            _readyAt = Math.Max(_time, _readyAt) + Math.Max(0f, additionalCooldown);
        }

        private int StartAttack()
        {
            if (_time > _resetAt + Tolerance) _nextStep = 1;
            int step = _nextStep;
            _nextStep = step == 3 ? 1 : step + 1;
            _readyAt = _time + (step == 1 ? _firstCooldown : step == 2 ? _secondCooldown : _finisherCooldown);
            _resetAt = _time + _resetTimeout;
            _buffered = false;
            if (step == 3)
            {
                _pendingFinisher = true;
                _finisherAt = _time + _windup;
            }
            return step;
        }
    }
}
