using System;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Movement, slam and vulnerable phases of the Penitent.</summary>
    public enum PenitentAttackPhase
    {
        /// <summary>May advance with its shield raised.</summary>
        Advance,
        /// <summary>Telegraphs the impending slam with its guard raised.</summary>
        SlamWindup,
        /// <summary>Resolves one parryable area impact.</summary>
        SlamStrike,
        /// <summary>Recovers with its guard lowered.</summary>
        SlamRecover,
        /// <summary>Cannot move or attack and has no guard.</summary>
        GuardBroken
    }

    /// <summary>Serialized tuning for the deterministic Penitent attack loop.</summary>
    [Serializable]
    public sealed class PenitentAttackSettings
    {
        [SerializeField, Tooltip("Maximum distance at which a slam can start.")]
        private float slamTriggerRange = 5.5f;
        [SerializeField, Range(0f, 180f), Tooltip("Maximum horizontal angle from forward at which a slam can start.")]
        private float slamTriggerHalfAngle = 35f;
        [SerializeField, Tooltip("Slam telegraph duration in seconds.")]
        private float slamWindup = 1.1f;
        [SerializeField, Tooltip("Committed strike duration in seconds.")]
        private float slamStrike = 0.2f;
        [SerializeField, Tooltip("Vulnerable recovery duration in seconds.")]
        private float slamRecover = 1.1f;
        [SerializeField, Tooltip("Cooldown assigned when a strike ends or a slam is cancelled.")]
        private float slamCooldown = 3.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum fractional variation in the slam cooldown.")]
        private float cooldownJitter = 0.2f;

        /// <summary>Maximum slam initiation distance.</summary>
        public float SlamTriggerRange => slamTriggerRange;
        /// <summary>Maximum horizontal initiation angle from forward.</summary>
        public float SlamTriggerHalfAngle => slamTriggerHalfAngle;
        /// <summary>Telegraph duration.</summary>
        public float SlamWindup => slamWindup;
        /// <summary>Strike duration.</summary>
        public float SlamStrike => slamStrike;
        /// <summary>Recovery duration.</summary>
        public float SlamRecover => slamRecover;
        /// <summary>Base cooldown.</summary>
        public float SlamCooldown => slamCooldown;
        /// <summary>Maximum fractional cooldown jitter.</summary>
        public float CooldownJitter => cooldownJitter;
    }

    /// <summary>Allocation-free attack decisions without scene or Unity object dependencies.</summary>
    public sealed class PenitentAttackLoop
    {
        private readonly PenitentAttackSettings _settings;
        private PenitentAttackPhase _phase;
        private float _phaseTime;
        private float _remaining;
        private float _cooldown;
        private float _jitter;
        private float _cooldownScale = 1f;
        private bool _strikeStarted;
        private bool _slamEnded;

        /// <summary>Creates a loop with the supplied tuning and an initially ready slam.</summary>
        public PenitentAttackLoop(PenitentAttackSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Current phase.</summary>
        public PenitentAttackPhase Phase => _phase;
        /// <summary>Elapsed seconds in the current phase.</summary>
        public float PhaseTime => _phaseTime;
        /// <summary>Seconds remaining in the current timed phase.</summary>
        public float PhaseRemaining => _remaining;
        /// <summary>Seconds until another slam is eligible.</summary>
        public float CooldownRemaining => _cooldown;
        /// <summary>Only advancing and winding up protect frontal hits.</summary>
        public bool IsGuardUp => _phase == PenitentAttackPhase.Advance || _phase == PenitentAttackPhase.SlamWindup;
        /// <summary>Whether this phase requires ownership of an attack token.</summary>
        public bool WantsToken => _phase == PenitentAttackPhase.SlamWindup || _phase == PenitentAttackPhase.SlamStrike;
        /// <summary>True only on the tick that enters the strike phase.</summary>
        public bool StrikeStartedThisTick => _strikeStarted;
        /// <summary>True on strike completion or cancellation, when the token may be released.</summary>
        public bool SlamEndedThisTick => _slamEnded;

        /// <summary>Tests feet against the cached ground rectangle and vertical tolerance. Jumping does not avoid it;
        /// leave the area or parry at impact. Elevated balconies outside the tolerance are safe.</summary>
        public static bool IsInsideSlamArea(Vector3 feet, Vector3 origin, Vector3 forward,
            float length, float width, float height)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.000001f) return false;
            forward.Normalize();
            Vector3 delta = feet - origin;
            float along = Vector3.Dot(delta, forward);
            float across = Vector3.Dot(delta, Vector3.Cross(Vector3.up, forward));
            return along >= 0f && along <= Mathf.Max(0f, length)
                && Mathf.Abs(across) <= Mathf.Max(0f, width) * 0.5f
                && Mathf.Abs(delta.y) <= Mathf.Max(0f, height);
        }

        /// <summary>Checks eligibility before the driver attempts to acquire a shared token.</summary>
        public bool CanBeginSlam(float distanceToTarget, bool hasLineOfSight, float targetAngle)
        {
            return _phase == PenitentAttackPhase.Advance && _cooldown <= 0f && hasLineOfSight
                && distanceToTarget >= 0f && distanceToTarget <= _settings.SlamTriggerRange
                && Mathf.Abs(targetAngle) <= _settings.SlamTriggerHalfAngle;
        }

        /// <summary>Begins a valid slam using externally supplied signed cooldown jitter.</summary>
        public bool TryBeginSlam(float distanceToTarget, bool hasLineOfSight, bool hasToken, float targetAngle, float jitterSigned = 0f)
        {
            if (!hasToken || !CanBeginSlam(distanceToTarget, hasLineOfSight, targetAngle)) return false;
            _jitter = Mathf.Clamp(jitterSigned, -1f, 1f);
            _strikeStarted = false;
            _slamEnded = false;
            Enter(PenitentAttackPhase.SlamWindup, _settings.SlamWindup);
            return true;
        }

        /// <summary>Advances at most one phase, preserving the strike event even with a large time step.</summary>
        public void Tick(float dt, float distanceToTarget, bool hasLineOfSight, bool hasToken, float targetAngle, float jitterSigned = 0f)
        {
            _strikeStarted = false;
            _slamEnded = false;
            dt = Mathf.Max(0f, dt);
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            if (WantsToken && !hasToken)
            {
                Cancel();
                return;
            }
            if (_phase == PenitentAttackPhase.Advance)
            {
                _phaseTime += dt;
                TryBeginSlam(distanceToTarget, hasLineOfSight, hasToken, targetAngle, jitterSigned);
                return;
            }
            _phaseTime += dt;
            _remaining = Mathf.Max(0f, _remaining - dt);
            if (_remaining > 0f) return;
            switch (_phase)
            {
                case PenitentAttackPhase.SlamWindup:
                    Enter(PenitentAttackPhase.SlamStrike, _settings.SlamStrike);
                    _strikeStarted = true;
                    break;
                case PenitentAttackPhase.SlamStrike:
                    AssignCooldown();
                    _slamEnded = true;
                    Enter(PenitentAttackPhase.SlamRecover, _settings.SlamRecover);
                    break;
                default:
                    Enter(PenitentAttackPhase.Advance, 0f);
                    break;
            }
        }

        /// <summary>Cancels a slam, drops token intent and returns to advancing.</summary>
        public void Cancel()
        {
            bool hadSlam = WantsToken;
            if (hadSlam) AssignCooldown();
            _strikeStarted = false;
            _slamEnded = hadSlam;
            Enter(PenitentAttackPhase.Advance, 0f);
        }

        /// <summary>Cancels any slam and opens the guard, preserving a longer existing break.</summary>
        public void BreakGuard(float duration)
        {
            float remaining = _phase == PenitentAttackPhase.GuardBroken ? _remaining : 0f;
            Cancel();
            Enter(PenitentAttackPhase.GuardBroken, Mathf.Max(remaining, Mathf.Max(0f, duration)));
        }

        /// <summary>Extends vulnerability after damage, capped without shortening an existing break.</summary>
        public void ExtendGuardBreak(float extension, float maximumDuration)
        {
            float remaining = _phase == PenitentAttackPhase.GuardBroken ? _remaining : 0f;
            BreakGuard(Mathf.Max(remaining, Mathf.Min(Mathf.Max(0f, maximumDuration),
                remaining + Mathf.Max(0f, extension))));
        }

        /// <summary>Scales future slam cooldowns (difficulty); survives <see cref="Reset"/>.</summary>
        public void SetCooldownScale(float scale) => _cooldownScale = Mathf.Max(0f, scale);

        /// <summary>Current slam cooldown scale.</summary>
        public float CooldownScale => _cooldownScale;

        /// <summary>Clears all timing, flags and cooldowns for a new spawn.</summary>
        public void Reset()
        {
            _cooldown = 0f;
            _jitter = 0f;
            _strikeStarted = false;
            _slamEnded = false;
            Enter(PenitentAttackPhase.Advance, 0f);
        }

        private void AssignCooldown()
        {
            _cooldown = Mathf.Max(0f, _settings.SlamCooldown * _cooldownScale * (1f + _settings.CooldownJitter * _jitter));
            _jitter = 0f;
        }

        private void Enter(PenitentAttackPhase phase, float duration)
        {
            _phase = phase;
            _phaseTime = 0f;
            _remaining = Mathf.Max(0f, duration);
        }
    }
}
