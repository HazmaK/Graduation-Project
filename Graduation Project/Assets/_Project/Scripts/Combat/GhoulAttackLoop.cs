using System;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Phases of the Ghoul's claw combo and pounce.</summary>
    public enum GhoulAttackPhase { None, ClawWindup, ClawStrike, ClawRecover, LungeWindup, Lunging, LungeRecover }

    /// <summary>Available Ghoul attacks.</summary>
    public enum GhoulAttackKind { None, Claw, Lunge }

    /// <summary>Serialized tuning for the deterministic Stalker attack loop.</summary>
    [Serializable]
    public class GhoulAttackSettings
    {
        [SerializeField] private float clawRange = 2.4f;
        /// <summary>Configured clawRange value.</summary>
        public float ClawRange => clawRange;

        [SerializeField] private float clawHitRange = 2.6f;
        /// <summary>Configured clawHitRange value.</summary>
        public float ClawHitRange => clawHitRange;

        [SerializeField] private float clawHitHalfAngle = 70f;
        /// <summary>Configured clawHitHalfAngle value.</summary>
        public float ClawHitHalfAngle => clawHitHalfAngle;

        [SerializeField] private float clawFirstWindup = 0.38f;
        /// <summary>Configured clawFirstWindup value.</summary>
        public float ClawFirstWindup => clawFirstWindup;

        [SerializeField] private float clawSecondWindup = 0.24f;
        /// <summary>Configured clawSecondWindup value.</summary>
        public float ClawSecondWindup => clawSecondWindup;

        [SerializeField] private float clawGlintLead = 0.16f;
        /// <summary>Configured clawGlintLead value.</summary>
        public float ClawGlintLead => clawGlintLead;

        [SerializeField] private float clawActiveTime = 0.12f;
        /// <summary>Configured clawActiveTime value.</summary>
        public float ClawActiveTime => clawActiveTime;

        [SerializeField] private float clawRecover = 0.55f;
        /// <summary>Configured clawRecover value.</summary>
        public float ClawRecover => clawRecover;

        [SerializeField] private float clawWindupCreepSpeed = 1.6f;
        /// <summary>Configured clawWindupCreepSpeed value.</summary>
        public float ClawWindupCreepSpeed => clawWindupCreepSpeed;

        [SerializeField] private float clawStrikeStepSpeed = 4.0f;
        /// <summary>Configured clawStrikeStepSpeed value.</summary>
        public float ClawStrikeStepSpeed => clawStrikeStepSpeed;

        [SerializeField] private float clawFirstDamageScale = 0.5f;
        /// <summary>Configured clawFirstDamageScale value.</summary>
        public float ClawFirstDamageScale => clawFirstDamageScale;

        [SerializeField] private float clawSecondDamageScale = 0.7f;
        /// <summary>Configured clawSecondDamageScale value.</summary>
        public float ClawSecondDamageScale => clawSecondDamageScale;

        [SerializeField] private float clawCooldown = 0.9f;
        /// <summary>Configured clawCooldown value.</summary>
        public float ClawCooldown => clawCooldown;

        [SerializeField] private float lungeMinRange = 3.2f;
        /// <summary>Configured lungeMinRange value.</summary>
        public float LungeMinRange => lungeMinRange;

        [SerializeField] private float lungeMaxRange = 7.5f;
        /// <summary>Configured lungeMaxRange value.</summary>
        public float LungeMaxRange => lungeMaxRange;

        [SerializeField] private float lungeWindup = 0.45f;
        /// <summary>Configured lungeWindup value.</summary>
        public float LungeWindup => lungeWindup;

        [SerializeField] private float lungeGlintLead = 0.18f;
        /// <summary>Configured lungeGlintLead value.</summary>
        public float LungeGlintLead => lungeGlintLead;

        [SerializeField] private float lungeSpeed = 15f;
        /// <summary>Configured lungeSpeed value.</summary>
        public float LungeSpeed => lungeSpeed;

        [SerializeField] private float lungeMaxDuration = 0.42f;
        /// <summary>Configured lungeMaxDuration value.</summary>
        public float LungeMaxDuration => lungeMaxDuration;

        [SerializeField] private float lungeTurnRate = 110f;
        /// <summary>Configured lungeTurnRate value.</summary>
        public float LungeTurnRate => lungeTurnRate;

        [SerializeField] private float lungeHitRange = 1.7f;
        /// <summary>Configured lungeHitRange value.</summary>
        public float LungeHitRange => lungeHitRange;

        [SerializeField] private float lungeHitHalfAngle = 60f;
        /// <summary>Configured lungeHitHalfAngle value.</summary>
        public float LungeHitHalfAngle => lungeHitHalfAngle;

        [SerializeField] private float lungeDamageScale = 0.9f;
        /// <summary>Configured lungeDamageScale value.</summary>
        public float LungeDamageScale => lungeDamageScale;

        [SerializeField] private float lungeHitRecover = 0.35f;
        /// <summary>Configured lungeHitRecover value.</summary>
        public float LungeHitRecover => lungeHitRecover;

        [SerializeField] private float lungeWhiffRecover = 0.8f;
        /// <summary>Configured lungeWhiffRecover value.</summary>
        public float LungeWhiffRecover => lungeWhiffRecover;

        [SerializeField] private float lungeCooldown = 2.6f;
        /// <summary>Configured lungeCooldown value.</summary>
        public float LungeCooldown => lungeCooldown;

        [SerializeField] private float cooldownJitter = 0.2f;
        /// <summary>Configured cooldownJitter value.</summary>
        public float CooldownJitter => cooldownJitter;

        [SerializeField] private float poise = 70f;
        /// <summary>Configured poise value.</summary>
        public float Poise => poise;

        [SerializeField] private float poiseWindow = 1.0f;
        /// <summary>Configured poiseWindow value.</summary>
        public float PoiseWindow => poiseWindow;

    }

    /// <summary>Pure attack timing, cooldown and poise decisions; driven by the enemy view.</summary>
    public class GhoulAttackLoop
    {
        private readonly GhoulAttackSettings _settings;
        private GhoulAttackPhase _phase;
        private int _swipeIndex;
        private float _remaining;
        private float _clawCooldown;
        private float _lungeCooldown;
        private float _nextJitter;
        private bool _glinted;
        private bool _hit;
        private float _poiseDamage;
        private float _poiseWindowStart;

        /// <summary>Creates a loop using the supplied tuning.</summary>
        public GhoulAttackLoop(GhoulAttackSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Current timed phase.</summary>
        public GhoulAttackPhase Phase => _phase;
        /// <summary>Attack owning the current phase.</summary>
        public GhoulAttackKind Kind => _phase == GhoulAttackPhase.None ? GhoulAttackKind.None
            : (_phase == GhoulAttackPhase.ClawWindup || _phase == GhoulAttackPhase.ClawStrike
                || _phase == GhoulAttackPhase.ClawRecover ? GhoulAttackKind.Claw : GhoulAttackKind.Lunge);
        /// <summary>Whether an attack or its recovery is running.</summary>
        public bool IsActive => _phase != GhoulAttackPhase.None;
        /// <summary>Committed phases resist noncritical staggering.</summary>
        public bool IsCommitted => _phase == GhoulAttackPhase.ClawStrike || _phase == GhoulAttackPhase.Lunging;
        /// <summary>Whether the current swipe or pounce may still hit.</summary>
        public bool HitWindowOpen => IsCommitted && !_hit;
        /// <summary>Zero-based claw swipe index.</summary>
        public int SwipeIndex => _swipeIndex;
        /// <summary>Time until claws can begin again.</summary>
        public float ClawCooldownRemaining => _clawCooldown;
        /// <summary>Time until a pounce can begin again.</summary>
        public float LungeCooldownRemaining => _lungeCooldown;
        /// <summary>Damage multiplier for the current attack and swipe.</summary>
        public float CurrentDamageScale => Kind == GhoulAttackKind.Lunge ? _settings.LungeDamageScale
            : Kind == GhoulAttackKind.Claw ? (_swipeIndex == 0 ? _settings.ClawFirstDamageScale
                : _settings.ClawSecondDamageScale) : 0f;

        /// <summary>Raised once per windup at its glint threshold.</summary>
        public event Action Glint;
        /// <summary>Raised when a swipe or pounce becomes active.</summary>
        public event Action StrikeStarted;
        /// <summary>Raised after entering every phase, including None.</summary>
        public event Action<GhoulAttackPhase> PhaseChanged;

        /// <summary>Advances cooldowns independently of the current phase.</summary>
        public void TickCooldowns(float dt)
        {
            dt = Mathf.Max(0f, dt);
            _clawCooldown = Mathf.Max(0f, _clawCooldown - dt);
            _lungeCooldown = Mathf.Max(0f, _lungeCooldown - dt);
        }

        /// <summary>Queries attack eligibility, preferring claws when both are available.</summary>
        public GhoulAttackKind ChooseAttack(float distance, bool canSee)
        {
            if (!canSee) return GhoulAttackKind.None;
            if (distance <= _settings.ClawRange && _clawCooldown <= 0f) return GhoulAttackKind.Claw;
            if (distance >= _settings.LungeMinRange && distance <= _settings.LungeMaxRange
                && _lungeCooldown <= 0f) return GhoulAttackKind.Lunge;
            return GhoulAttackKind.None;
        }

        /// <summary>Begins the chosen attack if no loop is already running.</summary>
        public void Begin(GhoulAttackKind kind)
        {
            if (IsActive) return;
            _swipeIndex = 0;
            if (kind == GhoulAttackKind.Claw)
                Enter(GhoulAttackPhase.ClawWindup, _settings.ClawFirstWindup);
            else if (kind == GhoulAttackKind.Lunge)
            {
                _clawCooldown = Mathf.Max(_clawCooldown, 0.4f);
                Enter(GhoulAttackPhase.LungeWindup, _settings.LungeWindup);
            }
        }

        /// <summary>Advances at most one phase per call, preserving every phase's events and hit window.</summary>
        public void Tick(float dt)
        {
            if (!IsActive) return;
            _remaining -= Mathf.Max(0f, dt);
            bool windup = _phase == GhoulAttackPhase.ClawWindup || _phase == GhoulAttackPhase.LungeWindup;
            float lead = _phase == GhoulAttackPhase.ClawWindup ? _settings.ClawGlintLead : _settings.LungeGlintLead;
            if (windup && !_glinted && _remaining <= Mathf.Max(0f, lead))
            {
                _glinted = true;
                GhoulAttackPhase before = _phase;
                Glint?.Invoke();
                if (_phase != before) return;
            }
            if (_remaining > 0f) return;
            switch (_phase)
            {
                case GhoulAttackPhase.ClawWindup:
                    Enter(GhoulAttackPhase.ClawStrike, _settings.ClawActiveTime);
                    break;
                case GhoulAttackPhase.ClawStrike:
                    if (_swipeIndex == 0)
                    {
                        _swipeIndex = 1;
                        Enter(GhoulAttackPhase.ClawWindup, _settings.ClawSecondWindup);
                    }
                    else
                    {
                        _clawCooldown = Cooldown(_settings.ClawCooldown);
                        Enter(GhoulAttackPhase.ClawRecover, _settings.ClawRecover);
                    }
                    break;
                case GhoulAttackPhase.LungeWindup:
                    Enter(GhoulAttackPhase.Lunging, _settings.LungeMaxDuration);
                    break;
                case GhoulAttackPhase.Lunging:
                    RecoverLunge(_settings.LungeWhiffRecover);
                    break;
                default:
                    Enter(GhoulAttackPhase.None, 0f);
                    break;
            }
        }

        /// <summary>Consumes this hit window and immediately recovers a successful pounce.</summary>
        public void ReportHit()
        {
            if (!HitWindowOpen) return;
            _hit = true;
            if (_phase == GhoulAttackPhase.Lunging) RecoverLunge(_settings.LungeHitRecover);
        }

        /// <summary>Ends a blocked pounce with its vulnerable whiff recovery.</summary>
        public void ReportLungeBlocked()
        {
            if (_phase == GhoulAttackPhase.Lunging) RecoverLunge(_settings.LungeWhiffRecover);
        }

        /// <summary>Cancels the current attack and applies its cooldown with the supplied jitter.</summary>
        public void Cancel(float jitter01Signed)
        {
            GhoulAttackKind kind = Kind;
            if (kind == GhoulAttackKind.None) return;
            SetCooldownJitter(jitter01Signed);
            if (kind == GhoulAttackKind.Claw) _clawCooldown = Cooldown(_settings.ClawCooldown);
            else _lungeCooldown = Cooldown(_settings.LungeCooldown);
            Enter(GhoulAttackPhase.None, 0f);
        }

        /// <summary>Accumulates windowed damage and evaluates critical hits and committed-phase armor.</summary>
        public bool RegisterDamageForPoise(float amount, bool isCritical, float time)
        {
            if (_poiseDamage <= 0f || time - _poiseWindowStart > _settings.PoiseWindow)
            {
                _poiseDamage = 0f;
                _poiseWindowStart = time;
            }
            _poiseDamage += Mathf.Max(0f, amount);
            if (isCritical)
            {
                if (_phase == GhoulAttackPhase.Lunging) return false;
                _poiseDamage = 0f;
                return true;
            }
            if (IsCommitted) return false;
            if (_poiseDamage < _settings.Poise) return false;
            _poiseDamage = 0f;
            return true;
        }

        /// <summary>Injects signed jitter for the next cooldown that is assigned.</summary>
        public void SetCooldownJitter(float jitterSigned) => _nextJitter = Mathf.Clamp(jitterSigned, -1f, 1f);

        /// <summary>Clears phases, cooldowns, jitter and accumulated poise for a fresh spawn.</summary>
        public void ResetAll()
        {
            _clawCooldown = 0f;
            _lungeCooldown = 0f;
            _nextJitter = 0f;
            _poiseDamage = 0f;
            _poiseWindowStart = 0f;
            _swipeIndex = 0;
            // Zaten boştaysa PhaseChanged'i tekrar tetikleme (Respawn'da Cancel + ResetAll çift olay üretmesin).
            if (_phase != GhoulAttackPhase.None) Enter(GhoulAttackPhase.None, 0f);
        }

        private float Cooldown(float duration)
        {
            float result = Mathf.Max(0f, duration * (1f + _settings.CooldownJitter * _nextJitter));
            _nextJitter = 0f;
            return result;
        }

        private void RecoverLunge(float duration)
        {
            _lungeCooldown = Cooldown(_settings.LungeCooldown);
            Enter(GhoulAttackPhase.LungeRecover, duration);
        }

        private void Enter(GhoulAttackPhase phase, float duration)
        {
            _phase = phase;
            _remaining = Mathf.Max(0f, duration);
            _glinted = false;
            _hit = false;
            PhaseChanged?.Invoke(phase);
            if (_phase == phase && IsCommitted) StrikeStarted?.Invoke();
        }
    }
}