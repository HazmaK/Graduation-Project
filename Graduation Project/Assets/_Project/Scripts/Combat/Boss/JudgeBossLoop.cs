using System;

namespace RetroFPS.Combat
{
    /// <summary>Encounter phases of The Judge.</summary>
    public enum JudgePhase
    {
        /// <summary>Waiting for engagement.</summary>
        Dormant,
        /// <summary>First health pool and ranged attack pool.</summary>
        Trial,
        /// <summary>Invulnerable change between health pools.</summary>
        Transition,
        /// <summary>Second health pool and execution attack pool.</summary>
        Execution,
        /// <summary>Awaiting the player's finisher.</summary>
        Kneeling,
        /// <summary>Encounter completed.</summary>
        Dead
    }
    /// <summary>Attacks available to The Judge.</summary>
    public enum JudgeAttack
    {
        /// <summary>No attack currently owns the timeline.</summary>
        None,
        /// <summary>Parryable single sword attack.</summary>
        Verdict,
        /// <summary>Parryable sword volley.</summary>
        Volley,
        /// <summary>Unparryable sequence of falling strikes.</summary>
        Guillotine,
        /// <summary>Unparryable rotating area attack.</summary>
        Scale,
        /// <summary>Parryable close-range sweep.</summary>
        Reap,
        /// <summary>Unparryable execution slam.</summary>
        ExecutionStrike,
        /// <summary>Parryable gap-closing thrust.</summary>
        Thrust,
        /// <summary>Unparryable Trial response to sustained close-range pressure.</summary>
        Repulse
    }
    /// <summary>Current attack or stun timing stage.</summary>
    public enum JudgeStep
    {
        /// <summary>Waiting for an idle gap or eligible target.</summary>
        Idle,
        /// <summary>Telegraphing the selected attack.</summary>
        Windup,
        /// <summary>Executing the selected attack.</summary>
        Active,
        /// <summary>Recovering or waiting between Last Word links.</summary>
        Recover,
        /// <summary>Temporarily interrupted by an accepted stun.</summary>
        Stunned
    }
    /// <summary>Origins of a requested stun.</summary>
    public enum JudgeStunSource
    {
        /// <summary>A successful melee parry.</summary>
        MeleeParry,
        /// <summary>A reflected sword hitting the boss.</summary>
        ReflectedSword,
        /// <summary>Any unsupported stun source.</summary>
        Other
    }

    /// <summary>Injectable random source, independent of Unity's global random state.</summary>
    public interface IJudgeRandom
    {
        /// <summary>Returns a sample in the inclusive range zero to one.</summary>
        float Value01();
    }

    /// <summary>Allocation-free encounter decisions and timing, driven by a separate view.</summary>
    public sealed class JudgeBossLoop
    {
        /// <summary>Immutable timing and selection tuning copied from JudgeBossConfig.</summary>
        public readonly struct Settings
        {
            /// <summary>Creates tuning; finite nonnegative values are clamped, and idle minima must be positive.</summary>
            public Settings(
                float verdictWindup,
                float verdictActive,
                float verdictRecover,
                float volleyWindup,
                float volleyActive,
                float volleyRecover,
                float guillotineWindup,
                float guillotineStrikeInterval,
                int guillotineStrikes,
                float guillotineRecover,
                float scaleWindup,
                float scaleActive,
                float scaleRecover,
                float reapWindup,
                float reapActive,
                float reapRecover,
                float executionStrikeWindup,
                float executionStrikeActive,
                float executionStrikeRecover,
                float thrustWindup,
                float thrustActive,
                float thrustRecover,
                float trialIdleGapMin,
                float trialIdleGapMax,
                float executionIdleGapMin,
                float executionIdleGapMax,
                float scaleMaxRange,
                float thrustMinRange,
                float guillotineReturnFraction,
                float lastWordFraction,
                float lastWordChainGap,
                float lastWordFinalRecover,
                float transitionDuration,
                float stunImmunity,
                float verdictWeight,
                float volleyWeight,
                float trialGuillotineWeight,
                float scaleWeight,
                float reapWeight,
                float executionStrikeWeight,
                float executionGuillotineWeight,
                float engageDelay,
                float executionIdleWatchdog = 4f,
                float repulseTriggerRadius = 6f,
                float repulseCloseTime = 1.2f,
                float repulseCooldown = 3.5f,
                float repulseTelegraph = 0.7f,
                float repulseRecover = 0.6f)
            {
                VerdictWindup = NonNegative(verdictWindup);
                VerdictActive = NonNegative(verdictActive);
                VerdictRecover = NonNegative(verdictRecover);
                VolleyWindup = NonNegative(volleyWindup);
                VolleyActive = NonNegative(volleyActive);
                VolleyRecover = NonNegative(volleyRecover);
                GuillotineWindup = NonNegative(guillotineWindup);
                GuillotineStrikeInterval = NonNegative(guillotineStrikeInterval);
                GuillotineStrikes = Math.Max(1, guillotineStrikes);
                GuillotineRecover = NonNegative(guillotineRecover);
                ScaleWindup = NonNegative(scaleWindup);
                ScaleActive = NonNegative(scaleActive);
                ScaleRecover = NonNegative(scaleRecover);
                ReapWindup = NonNegative(reapWindup);
                ReapActive = NonNegative(reapActive);
                ReapRecover = NonNegative(reapRecover);
                ExecutionStrikeWindup = NonNegative(executionStrikeWindup);
                ExecutionStrikeActive = NonNegative(executionStrikeActive);
                ExecutionStrikeRecover = NonNegative(executionStrikeRecover);
                ThrustWindup = NonNegative(thrustWindup);
                ThrustActive = NonNegative(thrustActive);
                ThrustRecover = NonNegative(thrustRecover);
                TrialIdleGapMin = NonNegative(trialIdleGapMin);
                TrialIdleGapMax = Math.Max(TrialIdleGapMin, NonNegative(trialIdleGapMax));
                ExecutionIdleGapMin = NonNegative(executionIdleGapMin);
                ExecutionIdleGapMax = Math.Max(ExecutionIdleGapMin, NonNegative(executionIdleGapMax));
                ScaleMaxRange = NonNegative(scaleMaxRange);
                ThrustMinRange = NonNegative(thrustMinRange);
                GuillotineReturnFraction = Math.Min(1f, NonNegative(guillotineReturnFraction));
                LastWordFraction = Math.Min(1f, NonNegative(lastWordFraction));
                LastWordChainGap = NonNegative(lastWordChainGap);
                LastWordFinalRecover = NonNegative(lastWordFinalRecover);
                TransitionDuration = NonNegative(transitionDuration);
                StunImmunity = NonNegative(stunImmunity);
                VerdictWeight = NonNegative(verdictWeight);
                VolleyWeight = NonNegative(volleyWeight);
                TrialGuillotineWeight = NonNegative(trialGuillotineWeight);
                ScaleWeight = NonNegative(scaleWeight);
                ReapWeight = NonNegative(reapWeight);
                ExecutionStrikeWeight = NonNegative(executionStrikeWeight);
                ExecutionGuillotineWeight = NonNegative(executionGuillotineWeight);
                EngageDelay = NonNegative(engageDelay);
                ExecutionIdleWatchdog = Math.Max(0.01f, NonNegative(executionIdleWatchdog));
                RepulseTriggerRadius = NonNegative(repulseTriggerRadius);
                RepulseCloseTime = Math.Max(0.01f, NonNegative(repulseCloseTime));
                RepulseCooldown = NonNegative(repulseCooldown);
                RepulseTelegraph = NonNegative(repulseTelegraph);
                RepulseRecover = NonNegative(repulseRecover);
                if (TrialIdleGapMin <= 0f) throw new ArgumentOutOfRangeException(nameof(trialIdleGapMin));
                if (ExecutionIdleGapMin <= 0f) throw new ArgumentOutOfRangeException(nameof(executionIdleGapMin));
            }

            /// <summary>Configured verdictWindup value.</summary>
            public float VerdictWindup { get; }
            /// <summary>Configured verdictActive value.</summary>
            public float VerdictActive { get; }
            /// <summary>Configured verdictRecover value.</summary>
            public float VerdictRecover { get; }
            /// <summary>Configured volleyWindup value.</summary>
            public float VolleyWindup { get; }
            /// <summary>Configured volleyActive value.</summary>
            public float VolleyActive { get; }
            /// <summary>Configured volleyRecover value.</summary>
            public float VolleyRecover { get; }
            /// <summary>Configured guillotineWindup value.</summary>
            public float GuillotineWindup { get; }
            /// <summary>Configured guillotineStrikeInterval value.</summary>
            public float GuillotineStrikeInterval { get; }
            /// <summary>Configured guillotineStrikes value.</summary>
            public int GuillotineStrikes { get; }
            /// <summary>Configured guillotineRecover value.</summary>
            public float GuillotineRecover { get; }
            /// <summary>Configured scaleWindup value.</summary>
            public float ScaleWindup { get; }
            /// <summary>Configured scaleActive value.</summary>
            public float ScaleActive { get; }
            /// <summary>Configured scaleRecover value.</summary>
            public float ScaleRecover { get; }
            /// <summary>Configured reapWindup value.</summary>
            public float ReapWindup { get; }
            /// <summary>Configured reapActive value.</summary>
            public float ReapActive { get; }
            /// <summary>Configured reapRecover value.</summary>
            public float ReapRecover { get; }
            /// <summary>Configured executionStrikeWindup value.</summary>
            public float ExecutionStrikeWindup { get; }
            /// <summary>Configured executionStrikeActive value.</summary>
            public float ExecutionStrikeActive { get; }
            /// <summary>Configured executionStrikeRecover value.</summary>
            public float ExecutionStrikeRecover { get; }
            /// <summary>Configured thrustWindup value.</summary>
            public float ThrustWindup { get; }
            /// <summary>Configured thrustActive value.</summary>
            public float ThrustActive { get; }
            /// <summary>Configured thrustRecover value.</summary>
            public float ThrustRecover { get; }
            /// <summary>Configured trialIdleGapMin value.</summary>
            public float TrialIdleGapMin { get; }
            /// <summary>Configured trialIdleGapMax value.</summary>
            public float TrialIdleGapMax { get; }
            /// <summary>Configured executionIdleGapMin value.</summary>
            public float ExecutionIdleGapMin { get; }
            /// <summary>Configured executionIdleGapMax value.</summary>
            public float ExecutionIdleGapMax { get; }
            /// <summary>Configured scaleMaxRange value.</summary>
            public float ScaleMaxRange { get; }
            /// <summary>Configured thrustMinRange value.</summary>
            public float ThrustMinRange { get; }
            /// <summary>Configured guillotineReturnFraction value.</summary>
            public float GuillotineReturnFraction { get; }
            /// <summary>Configured lastWordFraction value.</summary>
            public float LastWordFraction { get; }
            /// <summary>Configured lastWordChainGap value.</summary>
            public float LastWordChainGap { get; }
            /// <summary>Configured lastWordFinalRecover value.</summary>
            public float LastWordFinalRecover { get; }
            /// <summary>Configured transitionDuration value.</summary>
            public float TransitionDuration { get; }
            /// <summary>Configured stunImmunity value.</summary>
            public float StunImmunity { get; }
            /// <summary>Configured verdictWeight value.</summary>
            public float VerdictWeight { get; }
            /// <summary>Configured volleyWeight value.</summary>
            public float VolleyWeight { get; }
            /// <summary>Configured trialGuillotineWeight value.</summary>
            public float TrialGuillotineWeight { get; }
            /// <summary>Configured scaleWeight value.</summary>
            public float ScaleWeight { get; }
            /// <summary>Configured reapWeight value.</summary>
            public float ReapWeight { get; }
            /// <summary>Configured executionStrikeWeight value.</summary>
            public float ExecutionStrikeWeight { get; }
            /// <summary>Configured executionGuillotineWeight value.</summary>
            public float ExecutionGuillotineWeight { get; }
            /// <summary>Delay before the first Trial telegraph after engagement.</summary>
            public float EngageDelay { get; }
            /// <summary>Maximum Execution idle time before forcing Guillotine.</summary>
            public float ExecutionIdleWatchdog { get; }
            /// <summary>Horizontal proximity threshold in metres.</summary>
            public float RepulseTriggerRadius { get; }
            /// <summary>Accumulated close time required before Repulse.</summary>
            public float RepulseCloseTime { get; }
            /// <summary>Cooldown measured from the end or interruption of Repulse.</summary>
            public float RepulseCooldown { get; }
            /// <summary>Unparryable warning duration.</summary>
            public float RepulseTelegraph { get; }
            /// <summary>Recovery after the instantaneous burst.</summary>
            public float RepulseRecover { get; }
        }

        /// <summary>Player observations for one tick.</summary>
        public readonly struct Context
        {
            /// <summary>Creates observations using horizontal distance and current-phase health.</summary>
            public Context(float distanceToPlayer, bool hasLineOfSight, float phaseHealthFraction, bool playerReachable = true)
            {
                DistanceToPlayer = distanceToPlayer;
                HasLineOfSight = hasLineOfSight;
                PhaseHealthFraction = phaseHealthFraction;
                PlayerReachable = playerReachable;
            }

            /// <summary>Horizontal distance to the player in metres.</summary>
            public float DistanceToPlayer { get; }
            /// <summary>Whether the player is visible.</summary>
            public bool HasLineOfSight { get; }
            /// <summary>Whether a Thrust can reach its stop distance without hitting geometry.</summary>
            public bool PlayerReachable { get; }
            /// <summary>Remaining health fraction of the current encounter phase.</summary>
            public float PhaseHealthFraction { get; }
        }

        /// <summary>Effects implemented by the encounter driver.</summary>
        public interface IActions
        {
            /// <summary>Reports entry into an encounter phase.</summary>
            void OnPhaseChanged(JudgePhase phase);
            /// <summary>Begins a windup with its parry cue and duration.</summary>
            void OnTelegraph(JudgeAttack attack, bool parryable, float duration);
            /// <summary>Executes a hit with its zero-based strike index.</summary>
            void OnAttackExecute(JudgeAttack attack, int index);
            /// <summary>Ends an active attack, or cancels an unfinished windup or active attack.</summary>
            void OnAttackEnd(JudgeAttack attack, bool interrupted);
            /// <summary>Begins an accepted stun.</summary>
            void OnStunStarted(float duration);
            /// <summary>Ends or cancels a stun.</summary>
            void OnStunEnded();
        }

        private const int LastWordLinkCount = 3;
        private readonly Settings _settings;
        private readonly IJudgeRandom _random;
        private IActions _actions;
        private JudgePhase _phase;
        private JudgeStep _step;
        private JudgeAttack _attack;
        private JudgeAttack _previousAttack;
        private float _remaining;
        private float _idleElapsed;
        private float _stunImmunityRemaining;
        private float _repulseCloseElapsed;
        private float _repulseCooldownRemaining;
        private int _strikeIndex;
        private int _chainIndex;
        private bool _lastWord;
        private bool _lastPickWasChain;
        private bool _attackOpen;
        private int _revision;

        /// <summary>Creates a dormant loop; actions also receive notifications from commands between ticks.</summary>
        public JudgeBossLoop(Settings settings, IJudgeRandom random, IActions actions)
        {
            if (settings.TrialIdleGapMin <= 0f || settings.ExecutionIdleGapMin <= 0f)
                throw new ArgumentException("Idle gaps must be positive. Construct Settings explicitly.", nameof(settings));
            _settings = settings;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>Current encounter phase.</summary>
        public JudgePhase Phase => _phase;
        /// <summary>Current attack or stun timing stage.</summary>
        public JudgeStep Step => _step;
        /// <summary>Attack owning the windup, active or recovery stage.</summary>
        public JudgeAttack CurrentAttack => _attack;
        /// <summary>Whether phase damage must be ignored.</summary>
        public bool IsInvulnerable => _phase == JudgePhase.Dormant || _phase == JudgePhase.Transition || _phase == JudgePhase.Dead;
        /// <summary>Whether an accepted stun is currently running.</summary>
        public bool IsStunned => _step == JudgeStep.Stunned;
        /// <summary>Seconds of stun immunity still remaining.</summary>
        public float StunImmunityRemaining => _stunImmunityRemaining;
        /// <summary>Whether the Last Word chain, including its final recovery, is running.</summary>
        public bool IsLastWordActive => _lastWord;
        /// <summary>Seconds until the current timeline stage ends, for driver substeps.</summary>
        public float StepTimeRemaining => _remaining;

        /// <summary>Starts Trial with the configured engage delay; ignored after engagement.</summary>
        public void Engage()
        {
            if (_phase != JudgePhase.Dormant) return;
            _revision++;
            _phase = JudgePhase.Trial;
            EnterIdle();
            _remaining = _settings.EngageDelay;
            _actions.OnPhaseChanged(_phase);
        }

        /// <summary>Cancels an attack for a parry or reflected sword, unless stunned or immune.</summary>
        public bool RequestStun(JudgeStunSource source, float duration)
        {
            if ((source != JudgeStunSource.MeleeParry && source != JudgeStunSource.ReflectedSword)
                || !IsCombatPhase || IsStunned || _stunImmunityRemaining > 0f) return false;
            int revision = ++_revision;
            JudgeAttack cancelled = _attackOpen ? _attack : JudgeAttack.None;
            ClearAttack();
            if (cancelled == JudgeAttack.Repulse) _repulseCooldownRemaining = _settings.RepulseCooldown;
            _step = JudgeStep.Stunned;
            _remaining = NonNegative(duration);
            if (cancelled != JudgeAttack.None) _actions.OnAttackEnd(cancelled, true);
            if (_revision == revision) _actions.OnStunStarted(_remaining);
            return true;
        }

        /// <summary>Moves Trial into its invulnerable transition, or Execution into Kneeling.</summary>
        public void NotifyPhaseHealthDepleted()
        {
            if (!IsCombatPhase) return;
            ChangePhase(_phase == JudgePhase.Trial ? JudgePhase.Transition : JudgePhase.Kneeling);
        }

        /// <summary>Completes the encounter when a finisher hits the kneeling boss.</summary>
        public void NotifyFinisherHit()
        {
            if (_phase == JudgePhase.Kneeling) ChangePhase(JudgePhase.Dead);
        }

        /// <summary>Returns to Dormant, cancelling effects and clearing all history and timers.</summary>
        public void Reset()
        {
            if (_phase == JudgePhase.Dormant) return;
            ChangePhase(JudgePhase.Dormant);
        }

        /// <summary>
        /// Consumes elapsed time without dropping attack events. Actions becomes the command callback sink.
        /// Stun immunity and the normal idle gap elapse independently after stun.
        /// </summary>
        public void Tick(float deltaTime, in Context context, IActions actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            _actions = actions;
            float budget = NonNegative(deltaTime);
            int revision = _revision;
            while (IsCombatPhase || _phase == JudgePhase.Transition)
            {
                if (TryStartRepulse())
                {
                    if (_revision != revision || budget <= 0f) return;
                    continue;
                }
                float elapsed = Math.Min(budget, _remaining);
                if (_phase == JudgePhase.Trial && _step == JudgeStep.Idle)
                    elapsed = Math.Min(elapsed, TimeUntilRepulse(in context));
                bool executionIdle = _phase == JudgePhase.Execution && _step == JudgeStep.Idle;
                if (executionIdle)
                    elapsed = Math.Min(elapsed, Math.Max(0f, _settings.ExecutionIdleWatchdog - _idleElapsed));
                _remaining -= elapsed;
                if (executionIdle) _idleElapsed += elapsed;
                _stunImmunityRemaining = Math.Max(0f, _stunImmunityRemaining - elapsed);
                AdvanceRepulse(elapsed, in context);
                budget -= elapsed;
                if (TryStartRepulse())
                {
                    if (_revision != revision || budget <= 0f) return;
                    continue;
                }
                if (executionIdle && _idleElapsed >= _settings.ExecutionIdleWatchdog)
                {
                    _lastPickWasChain = false;
                    StartAttack(JudgeAttack.Guillotine);
                    if (_revision != revision || budget <= 0f) return;
                    continue;
                }
                if (_remaining > 0f) return;

                if (_phase == JudgePhase.Transition)
                {
                    _phase = JudgePhase.Execution;
                    EnterIdle();
                    _actions.OnPhaseChanged(_phase);
                }
                else if (_step == JudgeStep.Stunned)
                {
                    _stunImmunityRemaining = _settings.StunImmunity;
                    EnterIdle();
                    _actions.OnStunEnded();
                }
                else if (_step == JudgeStep.Idle)
                {
                    if (!TryStartAttack(in context))
                    {
                        // Range/visibility may leave no eligible attack after the idle timer expires.
                        // Immunity must still consume the unused tick budget while waiting for a target.
                        if (executionIdle && budget > 0f)
                        {
                            // Keep consuming Idle time even after its normal gap has expired.
                            float waiting = Math.Min(budget, Math.Max(0f, _settings.ExecutionIdleWatchdog - _idleElapsed));
                            _idleElapsed += waiting;
                            budget -= waiting;
                            _stunImmunityRemaining = Math.Max(0f, _stunImmunityRemaining - waiting);
                            if (_idleElapsed >= _settings.ExecutionIdleWatchdog)
                            {
                                _lastPickWasChain = false;
                                StartAttack(JudgeAttack.Guillotine);
                                if (_revision != revision || budget <= 0f) return;
                                continue;
                            }
                        }
                        float waitingForRepulse = Math.Min(budget, TimeUntilRepulse(in context));
                        AdvanceRepulse(waitingForRepulse, in context);
                        _stunImmunityRemaining = Math.Max(0f, _stunImmunityRemaining - waitingForRepulse);
                        budget -= waitingForRepulse;
                        if (TryStartRepulse() && _revision == revision && budget > 0f) continue;
                        return;
                    }
                }
                else if (_step == JudgeStep.Windup)
                {
                    _step = JudgeStep.Active;
                    _strikeIndex = 0;
                    _remaining = _attack == JudgeAttack.Guillotine ? _settings.GuillotineStrikeInterval : ActiveDuration(_attack);
                    _actions.OnAttackExecute(_attack, _strikeIndex);
                    if (_revision != revision) return;
                    if (_attack == JudgeAttack.Guillotine && _settings.GuillotineStrikes == 1) FinishActive();
                }
                else if (_step == JudgeStep.Active)
                {
                    if (_attack == JudgeAttack.Guillotine)
                    {
                        _strikeIndex++;
                        _remaining = _settings.GuillotineStrikeInterval;
                        _actions.OnAttackExecute(_attack, _strikeIndex);
                        if (_revision != revision) return;
                        if (_strikeIndex == _settings.GuillotineStrikes - 1) FinishActive();
                    }
                    else FinishActive();
                }
                else if (_step == JudgeStep.Recover)
                {
                    if (_lastWord && _chainIndex < LastWordLinkCount - 1)
                    {
                        _chainIndex++;
                        StartAttack(_chainIndex == LastWordLinkCount - 1 ? JudgeAttack.ExecutionStrike : JudgeAttack.Reap);
                    }
                    else
                    {
                        ClearAttack();
                        EnterIdle();
                        TryStartRepulse();
                    }
                }

                // Driver callbacks may synchronously stun, reset or deplete health.
                if (_revision != revision || budget <= 0f) return;
            }
        }

        private bool IsCombatPhase => _phase == JudgePhase.Trial || _phase == JudgePhase.Execution;

        private void AdvanceRepulse(float elapsed, in Context context)
        {
            _repulseCooldownRemaining = Math.Max(0f, _repulseCooldownRemaining - elapsed);
            if (_phase != JudgePhase.Trial || IsStunned) return;
            _repulseCloseElapsed = context.DistanceToPlayer < _settings.RepulseTriggerRadius
                ? Math.Min(_settings.RepulseCloseTime, _repulseCloseElapsed + elapsed)
                : Math.Max(0f, _repulseCloseElapsed - elapsed);
        }

        private float TimeUntilRepulse(in Context context)
        {
            if (_phase != JudgePhase.Trial || IsStunned
                || context.DistanceToPlayer >= _settings.RepulseTriggerRadius) return float.PositiveInfinity;
            return Math.Max(_repulseCooldownRemaining, _settings.RepulseCloseTime - _repulseCloseElapsed);
        }

        private bool TryStartRepulse()
        {
            if (_phase != JudgePhase.Trial || _step != JudgeStep.Idle
                || _repulseCloseElapsed < _settings.RepulseCloseTime || _repulseCooldownRemaining > 0f) return false;
            _repulseCloseElapsed = 0f;
            StartAttack(JudgeAttack.Repulse);
            return true;
        }

        private void ChangePhase(JudgePhase phase)
        {
            int revision = ++_revision;
            JudgeAttack cancelled = _attackOpen ? _attack : JudgeAttack.None;
            bool wasStunned = IsStunned;
            ClearAttack();
            _phase = phase;
            _step = JudgeStep.Idle;
            _idleElapsed = 0f;
            _remaining = phase == JudgePhase.Transition ? _settings.TransitionDuration : 0f;
            _stunImmunityRemaining = 0f;
            _repulseCloseElapsed = _repulseCooldownRemaining = 0f;
            if (phase == JudgePhase.Dormant)
            {
                _previousAttack = JudgeAttack.None;
                _lastPickWasChain = false;
            }
            if (cancelled != JudgeAttack.None) _actions.OnAttackEnd(cancelled, true);
            if (_revision != revision) return;
            if (wasStunned) _actions.OnStunEnded();
            if (_revision == revision) _actions.OnPhaseChanged(phase);
        }

        private void ClearAttack()
        {
            _attack = JudgeAttack.None;
            _attackOpen = false;
            _lastWord = false;
            _chainIndex = 0;
            _strikeIndex = 0;
        }

        private void EnterIdle()
        {
            _idleElapsed = 0f;
            _step = JudgeStep.Idle;
            _attack = JudgeAttack.None;
            float min = _phase == JudgePhase.Trial ? _settings.TrialIdleGapMin : _settings.ExecutionIdleGapMin;
            float max = _phase == JudgePhase.Trial ? _settings.TrialIdleGapMax : _settings.ExecutionIdleGapMax;
            _remaining = min + (max - min) * Random01();
        }

        private bool TryStartAttack(in Context context)
        {
            if (_phase == JudgePhase.Execution && !_lastPickWasChain && context.DistanceToPlayer <= _settings.ThrustMinRange
                && context.PhaseHealthFraction < _settings.LastWordFraction)
            {
                _lastWord = true;
                _lastPickWasChain = true;
                _chainIndex = 0;
                StartAttack(JudgeAttack.Reap);
                return true;
            }
            JudgeAttack attack = SelectAttack(in context);
            if (attack == JudgeAttack.None) return false;
            _lastPickWasChain = false;
            StartAttack(attack);
            return true;
        }

        private JudgeAttack SelectAttack(in Context context)
        {
            // Occlusion must not leave Trial permanently idle after its only eligible attack.
            if (_phase == JudgePhase.Trial && !context.HasLineOfSight) return JudgeAttack.Guillotine;
            if (_phase == JudgePhase.Execution && context.DistanceToPlayer > _settings.ThrustMinRange)
            {
                if (!context.PlayerReachable) return JudgeAttack.Guillotine;
                return _previousAttack == JudgeAttack.Thrust ? JudgeAttack.None : JudgeAttack.Thrust;
            }

            float total = 0f;
            JudgeAttack lastEligible = JudgeAttack.None;
            for (JudgeAttack attack = JudgeAttack.Verdict; attack <= JudgeAttack.Thrust; attack++)
            {
                float weight = EligibleWeight(attack, in context);
                total += weight;
                if (weight > 0f) lastEligible = attack;
            }
            if (total <= 0f) return JudgeAttack.None;
            float sample = Random01() * total;
            for (JudgeAttack attack = JudgeAttack.Verdict; attack <= JudgeAttack.Thrust; attack++)
            {
                float weight = EligibleWeight(attack, in context);
                if (weight <= 0f) continue;
                if (sample < weight) return attack;
                sample -= weight;
            }
            return lastEligible; // Inclusive random endpoint selects the final eligible bucket.
        }

        private float EligibleWeight(JudgeAttack attack, in Context context)
        {
            if (attack == _previousAttack) return 0f;
            if (_phase == JudgePhase.Trial)
            {
                if (!context.HasLineOfSight && attack != JudgeAttack.Guillotine) return 0f;
                switch (attack)
                {
                    case JudgeAttack.Verdict: return _settings.VerdictWeight;
                    case JudgeAttack.Volley: return _settings.VolleyWeight;
                    case JudgeAttack.Guillotine: return _settings.TrialGuillotineWeight;
                    case JudgeAttack.Scale: return context.DistanceToPlayer <= _settings.ScaleMaxRange ? _settings.ScaleWeight : 0f;
                    default: return 0f;
                }
            }
            switch (attack)
            {
                case JudgeAttack.Reap: return _settings.ReapWeight;
                case JudgeAttack.ExecutionStrike: return _settings.ExecutionStrikeWeight;
                case JudgeAttack.Guillotine: return !context.PlayerReachable || context.PhaseHealthFraction < _settings.GuillotineReturnFraction ? _settings.ExecutionGuillotineWeight : 0f;
                default: return 0f;
            }
        }

        private void StartAttack(JudgeAttack attack)
        {
            _attack = attack;
            _previousAttack = attack;
            _attackOpen = true;
            _step = JudgeStep.Windup;
            _remaining = WindupDuration(attack);
            bool parryable = attack == JudgeAttack.Verdict || attack == JudgeAttack.Volley
                || attack == JudgeAttack.Reap || attack == JudgeAttack.Thrust;
            _actions.OnTelegraph(attack, parryable, _remaining);
        }

        private void FinishActive()
        {
            if (_attack == JudgeAttack.Repulse) _repulseCooldownRemaining = _settings.RepulseCooldown;
            _attackOpen = false;
            _step = JudgeStep.Recover;
            _remaining = _lastWord
                ? (_chainIndex == LastWordLinkCount - 1 ? _settings.LastWordFinalRecover : _settings.LastWordChainGap)
                : RecoverDuration(_attack);
            _actions.OnAttackEnd(_attack, false);
        }

        private float WindupDuration(JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return _settings.VerdictWindup;
                case JudgeAttack.Volley: return _settings.VolleyWindup;
                case JudgeAttack.Guillotine: return _settings.GuillotineWindup;
                case JudgeAttack.Scale: return _settings.ScaleWindup;
                case JudgeAttack.Reap: return _settings.ReapWindup;
                case JudgeAttack.ExecutionStrike: return _settings.ExecutionStrikeWindup;
                case JudgeAttack.Thrust: return _settings.ThrustWindup;
                case JudgeAttack.Repulse: return _settings.RepulseTelegraph;
                default: return 0f;
            }
        }

        private float ActiveDuration(JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return _settings.VerdictActive;
                case JudgeAttack.Volley: return _settings.VolleyActive;
                case JudgeAttack.Scale: return _settings.ScaleActive;
                case JudgeAttack.Reap: return _settings.ReapActive;
                case JudgeAttack.ExecutionStrike: return _settings.ExecutionStrikeActive;
                case JudgeAttack.Thrust: return _settings.ThrustActive;
                default: return 0f;
            }
        }

        private float RecoverDuration(JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return _settings.VerdictRecover;
                case JudgeAttack.Volley: return _settings.VolleyRecover;
                case JudgeAttack.Guillotine: return _settings.GuillotineRecover;
                case JudgeAttack.Scale: return _settings.ScaleRecover;
                case JudgeAttack.Reap: return _settings.ReapRecover;
                case JudgeAttack.ExecutionStrike: return _settings.ExecutionStrikeRecover;
                case JudgeAttack.Thrust: return _settings.ThrustRecover;
                case JudgeAttack.Repulse: return _settings.RepulseRecover;
                default: return 0f;
            }
        }

        private float Random01() => Math.Min(1f, NonNegative(_random.Value01()));
        private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
    }
}
