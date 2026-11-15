using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic encounter timing, selection and interruption tests for The Judge.</summary>
    public sealed class JudgeBossLoopTests
    {
        private const float BoundaryOffset = 0.125f;
        private const float Half = 0.5f;
        private const int SelectionCycles = 32;
        private const int ExpectedGuillotineStrikes = 3;

        /// <summary>Engagement immediately reports Trial, then observes the initial idle gap.</summary>
        [Test]
        public void Engage_EntersTrial_WithInitialIdleGap()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            loop.Engage();
            loop.Engage();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Trial));
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
            Assert.That(loop.IsInvulnerable, Is.False);
            Assert.That(actions.Phases, Is.EqualTo(new[] { JudgePhase.Trial }));
            Assert.That(settings.EngageDelay, Is.GreaterThan(settings.TrialIdleGapMax));
            loop.Tick(settings.EngageDelay - BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
        }

        /// <summary>Each attack announces the correct cue and waits its entire windup before executing.</summary>
        [TestCase(JudgeAttack.Verdict, true)]
        [TestCase(JudgeAttack.Volley, true)]
        [TestCase(JudgeAttack.Guillotine, false)]
        [TestCase(JudgeAttack.Scale, false)]
        [TestCase(JudgeAttack.Reap, true)]
        [TestCase(JudgeAttack.ExecutionStrike, false)]
        [TestCase(JudgeAttack.Thrust, true)]
        public void Windup_PrecedesExecute_WithCorrectParryFlag(JudgeAttack attack, bool parryable)
        {
            var settings = CreateSettings(attack);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            bool execution = attack == JudgeAttack.Reap || attack == JudgeAttack.ExecutionStrike || attack == JudgeAttack.Thrust;
            var context = new JudgeBossLoop.Context(attack == JudgeAttack.Thrust ? settings.ThrustMinRange + BoundaryOffset : 0f, true, 1f);
            StartAttack(loop, settings, actions, in context, execution);
            Assert.That(loop.CurrentAttack, Is.EqualTo(attack));
            Assert.That(actions.Telegraphs[0].Attack, Is.EqualTo(attack));
            Assert.That(actions.Telegraphs[0].Flag, Is.EqualTo(parryable));
            float windup = Windup(settings, attack);
            Assert.That(actions.Telegraphs[0].Duration, Is.EqualTo(windup));
            Assert.That(actions.Executions.Count, Is.Zero);
            loop.Tick(windup - BoundaryOffset, in context, actions);
            Assert.That(actions.Executions.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(actions.Executions.Count, Is.EqualTo(1));
            Assert.That(actions.Executions[0].Attack, Is.EqualTo(attack));
            Assert.That(actions.Executions[0].Index, Is.Zero);
        }

        /// <summary>Regular selections never repeat their predecessor in either combat phase.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Selection_NeverRepeatsPreviousAttack(bool execution)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context, execution);
            for (int index = 0; index < SelectionCycles; index++)
            {
                JudgeAttack previous = loop.CurrentAttack;
                CompleteAttack(loop, settings, actions, in context);
                loop.Tick(execution ? settings.ExecutionIdleGapMin : settings.TrialIdleGapMin, in context, actions);
                Assert.That(loop.CurrentAttack, Is.Not.EqualTo(previous));
                Assert.That(loop.CurrentAttack, Is.Not.EqualTo(JudgeAttack.None));
            }
        }

        /// <summary>Scale's range boundary is inclusive and excludes it immediately outside.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Scale_RespectsMaximumRange(bool outside)
        {
            var settings = CreateSettings(JudgeAttack.Scale);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(settings.ScaleMaxRange + (outside ? BoundaryOffset : 0f), true, 1f);
            StartAttack(loop, settings, actions, in context);
            Assert.That(loop.CurrentAttack, Is.EqualTo(outside ? JudgeAttack.None : JudgeAttack.Scale));
        }

        /// <summary>Occlusion repeats the safe fallback rather than leaving Trial permanently idle.</summary>
        [Test]
        public void Trial_WithoutLineOfSight_RepeatsGuillotineRatherThanStalling()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var hidden = new JudgeBossLoop.Context(0f, false, 1f);
            StartAttack(loop, settings, actions, in hidden);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            CompleteAttack(loop, settings, actions, in hidden);
            loop.Tick(settings.TrialIdleGapMax, in hidden, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
        }

        /// <summary>All configured strikes occur at the interval, ending Active on the last strike.</summary>
        [Test]
        public void Guillotine_ExecutesExactlyThreeStrikes_AtConfiguredIntervals()
        {
            var settings = CreateSettings(JudgeAttack.Guillotine);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            loop.Tick(settings.GuillotineWindup, in context, actions);
            Assert.That(settings.GuillotineStrikes, Is.EqualTo(ExpectedGuillotineStrikes));
            for (int index = 1; index < settings.GuillotineStrikes; index++)
            {
                loop.Tick(settings.GuillotineStrikeInterval - BoundaryOffset, in context, actions);
                Assert.That(actions.Executions.Count, Is.EqualTo(index));
                Assert.That(actions.Ends.Count, Is.Zero);
                loop.Tick(BoundaryOffset, in context, actions);
                Assert.That(actions.Executions[index].Index, Is.EqualTo(index));
            }
            Assert.That(actions.Executions.Count, Is.EqualTo(ExpectedGuillotineStrikes));
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Recover));
            Assert.That(actions.Ends.Count, Is.EqualTo(1));
            Assert.That(actions.Ends[0].Flag, Is.False);
            loop.Tick(settings.GuillotineRecover, in context, actions);
            Assert.That(actions.Executions.Count, Is.EqualTo(ExpectedGuillotineStrikes));
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
        }

        /// <summary>A large tick preserves every Guillotine strike and its single completion callback.</summary>
        [Test]
        public void LargeTick_DoesNotDropGuillotineStrikes()
        {
            var settings = CreateSettings(JudgeAttack.Guillotine);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            loop.Tick(settings.GuillotineWindup + settings.GuillotineStrikeInterval * (settings.GuillotineStrikes - 1), in context, actions);
            Assert.That(actions.Executions.Count, Is.EqualTo(settings.GuillotineStrikes));
            for (int index = 0; index < settings.GuillotineStrikes; index++)
                Assert.That(actions.Executions[index].Index, Is.EqualTo(index));
            Assert.That(actions.Ends.Count, Is.EqualTo(1));
        }

        /// <summary>Only the two specified sources may cancel a windup or active attack for stun.</summary>
        [TestCase(JudgeStunSource.MeleeParry, false)]
        [TestCase(JudgeStunSource.ReflectedSword, false)]
        [TestCase(JudgeStunSource.MeleeParry, true)]
        [TestCase(JudgeStunSource.ReflectedSword, true)]
        public void Stun_AcceptsParryAndReflection_CancelsAttack(JudgeStunSource source, bool active)
        {
            var settings = CreateSettings(JudgeAttack.Verdict);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            if (active) loop.Tick(settings.VerdictWindup, in context, actions);
            Assert.That(loop.RequestStun(JudgeStunSource.Other, settings.VerdictRecover), Is.False);
            Assert.That(loop.RequestStun(source, settings.VerdictRecover), Is.True);
            Assert.That(loop.IsStunned, Is.True);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.None));
            Assert.That(actions.Ends.Count, Is.EqualTo(1));
            Assert.That(actions.Ends[0].Attack, Is.EqualTo(JudgeAttack.Verdict));
            Assert.That(actions.Ends[0].Flag, Is.True);
            Assert.That(actions.StunDurations, Is.EqualTo(new[] { settings.VerdictRecover }));
            Assert.That(loop.RequestStun(source, settings.VerdictRecover), Is.False);
        }

        /// <summary>Immunity begins when stun ends, expires independently, then permits another stun.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Stun_NormalIdleGapWhileImmunityCountsDown(bool execution)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = new JudgeBossLoop(settings, new FixedRandom(1f), actions);
            var context = NearContext();
            loop.Engage();
            if (execution)
            {
                loop.NotifyPhaseHealthDepleted();
                loop.Tick(settings.TransitionDuration, in context, actions);
            }
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.True);
            loop.Tick(settings.VerdictRecover - BoundaryOffset, in context, actions);
            Assert.That(loop.IsStunned, Is.True);
            Assert.That(loop.StunImmunityRemaining, Is.Zero);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(loop.IsStunned, Is.False);
            Assert.That(actions.StunEndCount, Is.EqualTo(1));
            Assert.That(loop.StunImmunityRemaining, Is.EqualTo(settings.StunImmunity));
            Assert.That(loop.RequestStun(JudgeStunSource.ReflectedSword, settings.VerdictRecover), Is.False);
            float gap = execution ? settings.ExecutionIdleGapMax : settings.TrialIdleGapMax;
            loop.Tick(gap - BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1), "Stun must not add immunity to the idle gap.");
            Assert.That(loop.StunImmunityRemaining, Is.GreaterThan(0f));
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.False);
            loop.Tick(settings.StunImmunity - gap - BoundaryOffset, in context, actions);
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.False);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(loop.StunImmunityRemaining, Is.Zero);
            Assert.That(loop.RequestStun(JudgeStunSource.ReflectedSword, settings.VerdictRecover), Is.True);
        }

        /// <summary>Immunity elapses even when no attack can be chosen after the normal idle gap expires.</summary>
        [Test]
        public void Stun_ImmunityExpiresWhileNoAttackIsEligible()
        {
            var settings = CreateSettings(JudgeAttack.Verdict);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.True);
            loop.Tick(settings.VerdictRecover, in context, actions);
            loop.Tick(settings.StunImmunity, in context, actions);
            Assert.That(loop.StunImmunityRemaining, Is.Zero);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1), "The only weighted attack cannot repeat itself.");
            Assert.That(loop.RequestStun(JudgeStunSource.ReflectedSword, settings.VerdictRecover), Is.True);
        }

        /// <summary>Trial depletion interrupts the attack and waits the invulnerable transition duration.</summary>
        [Test]
        public void TrialDepleted_TransitionsThenAutomaticallyEntersExecution()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            loop.NotifyPhaseHealthDepleted();
            loop.NotifyPhaseHealthDepleted();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Transition));
            Assert.That(loop.IsInvulnerable, Is.True);
            Assert.That(actions.Ends.Count, Is.EqualTo(1));
            Assert.That(actions.Ends[0].Flag, Is.True);
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.False);
            loop.Tick(settings.TransitionDuration - BoundaryOffset, in context, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Transition));
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Execution));
            Assert.That(loop.IsInvulnerable, Is.False);
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
            Assert.That(actions.Phases, Is.EqualTo(new[] { JudgePhase.Trial, JudgePhase.Transition, JudgePhase.Execution }));
        }

        /// <summary>The shared Guillotine attack cannot repeat across a phase transition.</summary>
        [Test]
        public void Transition_PreservesPreviousAttackExclusion()
        {
            var settings = CreateSettings(JudgeAttack.Guillotine);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(0f, true,
                (settings.LastWordFraction + settings.GuillotineReturnFraction) * Half);
            StartAttack(loop, settings, actions, in context);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            loop.NotifyPhaseHealthDepleted();
            loop.Tick(settings.TransitionDuration + settings.ExecutionIdleGapMin, in context, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Execution));
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.None));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
        }

        /// <summary>A phase change ends a stun once and clears its timers.</summary>
        [Test]
        public void PhaseChange_CancelsStun()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            loop.Engage();
            loop.RequestStun(JudgeStunSource.ReflectedSword, settings.StunImmunity);
            loop.NotifyPhaseHealthDepleted();
            Assert.That(loop.IsStunned, Is.False);
            Assert.That(actions.StunEndCount, Is.EqualTo(1));
            Assert.That(loop.StunImmunityRemaining, Is.Zero);
        }

        /// <summary>Thrust overrides the execution pool only beyond its strict distance boundary.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Execution_ThrustBeyondMinimumRange_WithoutLineOfSight(bool beyond)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(settings.ThrustMinRange + (beyond ? BoundaryOffset : 0f), false, 1f);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.CurrentAttack, Is.EqualTo(beyond ? JudgeAttack.Thrust : JudgeAttack.Reap));
            if (!beyond) return;
            CompleteAttack(loop, settings, actions, in context);
            loop.Tick(settings.ExecutionIdleGapMax, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1), "A forced Thrust must not repeat itself.");
        }

        /// <summary>Execution Guillotine is eligible strictly below its health fraction threshold.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Execution_GuillotineOnlyBelowReturnFraction(bool below)
        {
            var settings = CreateSettings(JudgeAttack.Guillotine);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            float fraction = below ? (settings.LastWordFraction + settings.GuillotineReturnFraction) * Half : settings.GuillotineReturnFraction;
            var context = new JudgeBossLoop.Context(0f, false, fraction);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.CurrentAttack, Is.EqualTo(below ? JudgeAttack.Guillotine : JudgeAttack.None));
        }

        /// <summary>Last Word chains two Reaps into ExecutionStrike with dedicated gaps and final recovery.</summary>
        [Test]
        public void LastWord_UsesOrderedLinks_ChainGaps_AndFinalRecover()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(settings.ThrustMinRange, false, settings.LastWordFraction - BoundaryOffset);
            StartAttack(loop, settings, actions, in context, true);
            JudgeAttack[] expected = { JudgeAttack.Reap, JudgeAttack.Reap, JudgeAttack.ExecutionStrike };
            Assert.That(loop.IsLastWordActive, Is.True);
            for (int link = 0; link < expected.Length; link++)
            {
                JudgeAttack attack = expected[link];
                Assert.That(loop.CurrentAttack, Is.EqualTo(attack));
                Assert.That(actions.Telegraphs[link].Attack, Is.EqualTo(attack));
                loop.Tick(Windup(settings, attack), in context, actions);
                loop.Tick(Active(settings, attack), in context, actions);
                Assert.That(actions.Executions[link].Attack, Is.EqualTo(attack));
                Assert.That(actions.Ends[link].Flag, Is.False);
                bool final = link == expected.Length - 1;
                float recover = final ? settings.LastWordFinalRecover : settings.LastWordChainGap;
                loop.Tick(recover - BoundaryOffset, in context, actions);
                Assert.That(loop.Step, Is.EqualTo(JudgeStep.Recover));
                Assert.That(actions.Telegraphs.Count, Is.EqualTo(link + 1));
                loop.Tick(BoundaryOffset, in context, actions);
            }
            Assert.That(loop.IsLastWordActive, Is.False);
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.None));
        }

        /// <summary>The Last Word health boundary is strict and a distant player still forces Thrust.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void LastWord_RequiresLowHealthAndNearPlayer(bool distant)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(distant ? settings.ThrustMinRange + BoundaryOffset : 0f,
                true, distant ? 0f : settings.LastWordFraction);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.IsLastWordActive, Is.False);
            if (distant) Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Thrust));
        }

        /// <summary>Stun cancels the remaining links and prevents their execution.</summary>
        [Test]
        public void LastWord_StunCancelsWholeChain()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(0f, true, 0f);
            StartAttack(loop, settings, actions, in context, true);
            loop.Tick(settings.ReapWindup, in context, actions);
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.ReapRecover), Is.True);
            Assert.That(loop.IsLastWordActive, Is.False);
            Assert.That(actions.Ends[0].Flag, Is.True);
            loop.Tick(settings.ReapRecover, in context, actions);
            Assert.That(actions.Executions.Count, Is.EqualTo(1));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
            loop.Tick(settings.ExecutionIdleGapMin, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
            Assert.That(loop.IsLastWordActive, Is.False, "An interrupted chain also requires a normal pick.");
        }

        /// <summary>A full chain must be followed by one normal pool pick before another chain is allowed.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void LastWord_AlternatesWithNormalExecutionPick(bool distantAfterChain)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var near = new JudgeBossLoop.Context(0f, true, 0f);
            StartAttack(loop, settings, actions, in near, true);
            float chainDuration = 2f * (settings.ReapWindup + settings.ReapActive + settings.LastWordChainGap)
                + settings.ExecutionStrikeWindup + settings.ExecutionStrikeActive + settings.LastWordFinalRecover;
            loop.Tick(chainDuration, in near, actions);
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
            var next = new JudgeBossLoop.Context(distantAfterChain ? settings.ThrustMinRange + BoundaryOffset : 0f, true, 0f);
            loop.Tick(settings.ExecutionIdleGapMin, in next, actions);
            Assert.That(loop.IsLastWordActive, Is.False);
            Assert.That(loop.CurrentAttack, Is.EqualTo(distantAfterChain ? JudgeAttack.Thrust : JudgeAttack.Guillotine));
            CompleteAttack(loop, settings, actions, in next);
            loop.Tick(settings.ExecutionIdleGapMin, in near, actions);
            Assert.That(loop.IsLastWordActive, Is.True);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Reap));
        }

        /// <summary>Depleted Execution kneels without further attacks, and only a finisher makes it Dead.</summary>
        [Test]
        public void ExecutionDepleted_KneelsUntilFinisher_ThenDead()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            loop.NotifyFinisherHit();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Dormant));
            StartAttack(loop, settings, actions, in context, true);
            loop.NotifyFinisherHit();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Execution));
            loop.NotifyPhaseHealthDepleted();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Kneeling));
            Assert.That(loop.IsInvulnerable, Is.False);
            Assert.That(actions.Ends[0].Flag, Is.True);
            Assert.That(loop.RequestStun(JudgeStunSource.ReflectedSword, settings.ReapRecover), Is.False);
            int callbacks = actions.CallbackCount;
            loop.Tick(settings.StunImmunity + settings.TransitionDuration, in context, actions);
            loop.NotifyPhaseHealthDepleted();
            Assert.That(actions.CallbackCount, Is.EqualTo(callbacks));
            loop.NotifyFinisherHit();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Dead));
            Assert.That(loop.IsInvulnerable, Is.True);
            callbacks = actions.CallbackCount;
            loop.Tick(settings.StunImmunity, in context, actions);
            loop.NotifyFinisherHit();
            loop.NotifyPhaseHealthDepleted();
            Assert.That(actions.CallbackCount, Is.EqualTo(callbacks));
        }

        /// <summary>Dormant commands and ticks produce no callbacks.</summary>
        [Test]
        public void Dormant_ProducesNoCallbacks()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = NearContext();
            loop.Tick(settings.TransitionDuration, in context, actions);
            loop.NotifyPhaseHealthDepleted();
            loop.NotifyFinisherHit();
            loop.Reset();
            Assert.That(loop.RequestStun(JudgeStunSource.MeleeParry, settings.VerdictRecover), Is.False);
            Assert.That(actions.CallbackCount, Is.Zero);
            Assert.That(loop.IsInvulnerable, Is.True);
        }

        /// <summary>Reset clears a running chain, stun or immunity and restores a fresh Trial selection.</summary>
        [TestCase(JudgeStep.Windup)]
        [TestCase(JudgeStep.Stunned)]
        [TestCase(JudgeStep.Idle)]
        public void Reset_ClearsTimersAndHistory(JudgeStep resetDuring)
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(0f, true, 0f);
            StartAttack(loop, settings, actions, in context, true);
            if (resetDuring != JudgeStep.Windup)
                loop.RequestStun(JudgeStunSource.ReflectedSword, settings.ReapRecover);
            if (resetDuring == JudgeStep.Idle)
                loop.Tick(settings.ReapRecover, in context, actions);
            loop.Reset();
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Dormant));
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.None));
            Assert.That(loop.IsLastWordActive, Is.False);
            Assert.That(loop.IsStunned, Is.False);
            Assert.That(loop.StunImmunityRemaining, Is.Zero);
            int callbacks = actions.CallbackCount;
            loop.Tick(settings.StunImmunity, in context, actions);
            Assert.That(actions.CallbackCount, Is.EqualTo(callbacks));
            loop.Engage();
            loop.Tick(settings.EngageDelay, in context, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Verdict));
        }

        /// <summary>Reentrant phase depletion during a strike cannot emit further Guillotine strikes.</summary>
        [Test]
        public void ExecuteCallback_CanSynchronouslyDepletePhase()
        {
            var settings = CreateSettings(JudgeAttack.Guillotine);
            JudgeBossLoop loop = null;
            var actions = new RecordingActions(() => loop.NotifyPhaseHealthDepleted());
            loop = CreateLoop(settings, actions);
            var context = NearContext();
            StartAttack(loop, settings, actions, in context);
            loop.Tick(settings.GuillotineWindup + settings.GuillotineStrikeInterval * settings.GuillotineStrikes, in context, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Transition));
            Assert.That(actions.Executions.Count, Is.EqualTo(1));
            Assert.That(actions.Ends.Count, Is.EqualTo(1));
            Assert.That(actions.Ends[0].Flag, Is.True);
        }

        /// <summary>The inclusive random endpoint chooses a valid bucket; subsequent idle gaps use their maximum.</summary>
        [Test]
        public void RandomEndpoint_UsesMaximumGapAndFinalEligibleBucket()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = new JudgeBossLoop(settings, new FixedRandom(1f), actions);
            var context = NearContext();
            loop.Engage();
            loop.Tick(settings.EngageDelay - BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Scale));
            CompleteAttack(loop, settings, actions, in context);
            loop.Tick(settings.TrialIdleGapMax - BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
            loop.Tick(BoundaryOffset, in context, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
        }

        private static JudgeBossLoop CreateLoop(JudgeBossLoop.Settings settings, RecordingActions actions)
            => new JudgeBossLoop(settings, new FixedRandom(0f), actions);

        /// <summary>A blocked dais approach uses Guillotine even above its normal health gate.</summary>
        [Test]
        public void Execution_UnreachableDistantPlayer_UsesGuillotineAtFullHealth()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(12.7f, true, 1f, false);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            CompleteAttack(loop, settings, actions, in context);
            loop.Tick(settings.ExecutionIdleGapMin, in context, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
        }

        /// <summary>Blocked approaches retain the ordinary melee pool when the player is near.</summary>
        [Test]
        public void Execution_UnreachableNearbyPlayer_CanUseMelee()
        {
            var settings = CreateSettings();
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(0f, true, 1f, false);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            CompleteAttack(loop, settings, actions, in context);
            loop.Tick(settings.ExecutionIdleGapMin, in context, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Reap));
        }

        /// <summary>The old repeated-Thrust dead end is bounded by total Idle time, including expired gaps.</summary>
        [TestCase(false, 4f)]
        [TestCase(true, 4f)]
        [TestCase(false, 1.25f)]
        [TestCase(true, 1.25f)]
        public void Execution_IdleWatchdog_ForcesGuillotineAfterThrust(bool singleLargeTick, float watchdog)
        {
            var settings = CreateSettings(executionIdleWatchdog: watchdog);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var context = new JudgeBossLoop.Context(12.7f, true, 1f);
            StartAttack(loop, settings, actions, in context, true);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Thrust));
            CompleteAttack(loop, settings, actions, in context);
            if (singleLargeTick) loop.Tick(settings.ExecutionIdleWatchdog, in context, actions);
            else
            {
                loop.Tick(settings.ExecutionIdleGapMin, in context, actions);
                loop.Tick(settings.ExecutionIdleWatchdog - settings.ExecutionIdleGapMin - BoundaryOffset, in context, actions);
                Assert.That(loop.Step, Is.EqualTo(JudgeStep.Idle));
                loop.Tick(BoundaryOffset, in context, actions);
            }
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Guillotine));
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Windup));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
        }

        private static JudgeBossLoop.Context NearContext() => new JudgeBossLoop.Context(0f, true, 1f);

        private static void StartAttack(JudgeBossLoop loop, JudgeBossLoop.Settings settings, RecordingActions actions,
            in JudgeBossLoop.Context context, bool execution = false)
        {
            loop.Engage();
            if (execution)
            {
                loop.NotifyPhaseHealthDepleted();
                loop.Tick(settings.TransitionDuration, in context, actions);
            }
            loop.Tick(execution ? settings.ExecutionIdleGapMin : settings.EngageDelay, in context, actions);
        }

        private static void CompleteAttack(JudgeBossLoop loop, JudgeBossLoop.Settings settings, RecordingActions actions,
            in JudgeBossLoop.Context context)
        {
            JudgeAttack attack = loop.CurrentAttack;
            loop.Tick(Windup(settings, attack), in context, actions);
            loop.Tick(Active(settings, attack), in context, actions);
            loop.Tick(Recover(settings, attack), in context, actions);
        }

        private static float Windup(JudgeBossLoop.Settings settings, JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return settings.VerdictWindup;
                case JudgeAttack.Volley: return settings.VolleyWindup;
                case JudgeAttack.Guillotine: return settings.GuillotineWindup;
                case JudgeAttack.Scale: return settings.ScaleWindup;
                case JudgeAttack.Reap: return settings.ReapWindup;
                case JudgeAttack.ExecutionStrike: return settings.ExecutionStrikeWindup;
                case JudgeAttack.Thrust: return settings.ThrustWindup;
                default: throw new ArgumentOutOfRangeException(nameof(attack));
            }
        }

        private static float Active(JudgeBossLoop.Settings settings, JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return settings.VerdictActive;
                case JudgeAttack.Volley: return settings.VolleyActive;
                case JudgeAttack.Guillotine: return settings.GuillotineStrikeInterval * (settings.GuillotineStrikes - 1);
                case JudgeAttack.Scale: return settings.ScaleActive;
                case JudgeAttack.Reap: return settings.ReapActive;
                case JudgeAttack.ExecutionStrike: return settings.ExecutionStrikeActive;
                case JudgeAttack.Thrust: return settings.ThrustActive;
                default: throw new ArgumentOutOfRangeException(nameof(attack));
            }
        }

        private static float Recover(JudgeBossLoop.Settings settings, JudgeAttack attack)
        {
            switch (attack)
            {
                case JudgeAttack.Verdict: return settings.VerdictRecover;
                case JudgeAttack.Volley: return settings.VolleyRecover;
                case JudgeAttack.Guillotine: return settings.GuillotineRecover;
                case JudgeAttack.Scale: return settings.ScaleRecover;
                case JudgeAttack.Reap: return settings.ReapRecover;
                case JudgeAttack.ExecutionStrike: return settings.ExecutionStrikeRecover;
                case JudgeAttack.Thrust: return settings.ThrustRecover;
                default: throw new ArgumentOutOfRangeException(nameof(attack));
            }
        }

        /// <summary>Close pressure interrupts the idle gap exactly at the configured threshold.</summary>
        [Test]
        public void Repulse_TriggersAtCloseTime_NotBefore_AndCannotBeParried()
        {
            var settings = CreateSettings(repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            loop.Engage();
            loop.Tick(settings.RepulseCloseTime - BoundaryOffset, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in close, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Repulse));
            Assert.That(actions.Telegraphs[0].Flag, Is.False);
            Assert.That(actions.Telegraphs[0].Duration, Is.EqualTo(settings.RepulseTelegraph));
            loop.Tick(settings.RepulseTelegraph - BoundaryOffset, in close, actions);
            Assert.That(actions.Executions.Count, Is.Zero);
            loop.Tick(BoundaryOffset, in close, actions);
            Assert.That(actions.Executions[0].Attack, Is.EqualTo(JudgeAttack.Repulse));
            loop.Tick(0f, in close, actions);
            Assert.That(actions.Executions.Count, Is.EqualTo(1));
        }

        /// <summary>The proximity boundary is strict and uses horizontal observations.</summary>
        [Test]
        public void Repulse_DoesNotAccumulateAtTriggerRadius()
        {
            var settings = CreateSettings(JudgeAttack.Repulse, repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var outside = new JudgeBossLoop.Context(settings.RepulseTriggerRadius, true, 1f);
            loop.Engage();
            loop.Tick(10f, in outside, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
        }

        /// <summary>Leaving proximity subtracts elapsed time rather than preserving or instantly clearing it.</summary>
        [Test]
        public void Repulse_CloseAccumulatorDecaysAtSameRate()
        {
            var settings = CreateSettings(JudgeAttack.Repulse, repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            var far = new JudgeBossLoop.Context(20f, true, 1f);
            loop.Engage();
            loop.Tick(0.75f, in close, actions);
            loop.Tick(0.5f, in far, actions);
            loop.Tick(0.875f, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(0.125f, in close, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Repulse));
        }

        /// <summary>Repulse starts at recovery completion, before the next normal attack or idle gap.</summary>
        [Test]
        public void Repulse_WaitsForCurrentAttack_ThenTakesPriority()
        {
            var settings = CreateSettings(repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var far = new JudgeBossLoop.Context(20f, true, 1f);
            var close = new JudgeBossLoop.Context(0f, false, 1f);
            StartAttack(loop, settings, actions, in far);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Verdict));
            loop.Tick(settings.VerdictWindup + settings.VerdictActive, in close, actions);
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Recover));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
            loop.Tick(settings.VerdictRecover, in close, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Repulse));
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
        }

        /// <summary>Cooldown starts at burst end, includes recovery, and cannot be bypassed by staying close.</summary>
        [Test]
        public void Repulse_CooldownIsMeasuredFromEnd_NotStart()
        {
            var settings = CreateSettings(JudgeAttack.Repulse, repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            loop.Engage();
            loop.Tick(settings.RepulseCloseTime, in close, actions);
            loop.Tick(settings.RepulseTelegraph, in close, actions);
            loop.Tick(0f, in close, actions);
            Assert.That(loop.Step, Is.EqualTo(JudgeStep.Recover));
            loop.Tick(settings.RepulseCooldown - BoundaryOffset, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
            loop.Tick(BoundaryOffset, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Repulse));
        }

        /// <summary>Neither transition nor Execution can accumulate or select Repulse.</summary>
        [Test]
        public void Repulse_NeverDuringTransitionOrExecution()
        {
            var settings = CreateSettings(repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            loop.Engage();
            loop.Tick(0.75f, in close, actions);
            loop.NotifyPhaseHealthDepleted();
            loop.Tick(settings.TransitionDuration - BoundaryOffset, in close, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Transition));
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(30f, in close, actions);
            Assert.That(loop.Phase, Is.EqualTo(JudgePhase.Execution));
            foreach (var telegraph in actions.Telegraphs)
                Assert.That(telegraph.Attack, Is.Not.EqualTo(JudgeAttack.Repulse));
        }

        /// <summary>Stun freezes close accumulation and prevents a warning until a safe idle step.</summary>
        [Test]
        public void Repulse_NeverWhileStunned_AndStunTimeDoesNotAccumulate()
        {
            var settings = CreateSettings(JudgeAttack.Repulse, repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            loop.Engage();
            loop.Tick(0.75f, in close, actions);
            Assert.That(loop.RequestStun(JudgeStunSource.ReflectedSword, 2f), Is.True);
            loop.Tick(2f, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(0.375f, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.Zero);
            loop.Tick(0.125f, in close, actions);
            Assert.That(loop.CurrentAttack, Is.EqualTo(JudgeAttack.Repulse));
            Assert.That(loop.StunImmunityRemaining, Is.EqualTo(settings.StunImmunity - 0.5f));
            loop.Tick(settings.RepulseTelegraph, in close, actions);
            Assert.That(loop.IsStunned, Is.False);
            Assert.That(loop.StunImmunityRemaining, Is.EqualTo(settings.StunImmunity - 0.5f - settings.RepulseTelegraph));
        }

        /// <summary>Encounter reset removes stored proximity and cooldown history.</summary>
        [Test]
        public void Repulse_ResetClearsCloseHistory()
        {
            var settings = CreateSettings(JudgeAttack.Repulse, repulse: true);
            var actions = new RecordingActions();
            var loop = CreateLoop(settings, actions);
            var close = new JudgeBossLoop.Context(0f, true, 1f);
            loop.Engage();
            loop.Tick(settings.RepulseCloseTime, in close, actions);
            loop.Reset();
            loop.Engage();
            loop.Tick(settings.RepulseCloseTime - BoundaryOffset, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(1));
            loop.Tick(BoundaryOffset, in close, actions);
            Assert.That(actions.Telegraphs.Count, Is.EqualTo(2));
        }

        // Binary-exact timing values keep boundary assertions independent of floating-point rounding.
        private static JudgeBossLoop.Settings CreateSettings(JudgeAttack onlyAttack = JudgeAttack.None,
            float executionIdleWatchdog = 4f, bool repulse = false)
        {
            return new JudgeBossLoop.Settings(
                verdictWindup: 0.5f,
                verdictActive: 0.25f,
                verdictRecover: 0.5f,
                volleyWindup: 0.75f,
                volleyActive: 0.25f,
                volleyRecover: 0.75f,
                guillotineWindup: 0.5f,
                guillotineStrikeInterval: 0.5f,
                guillotineStrikes: 3,
                guillotineRecover: 0.5f,
                scaleWindup: 1.0f,
                scaleActive: 3.0f,
                scaleRecover: 0.75f,
                reapWindup: 0.5f,
                reapActive: 0.25f,
                reapRecover: 0.5f,
                executionStrikeWindup: 0.75f,
                executionStrikeActive: 0.25f,
                executionStrikeRecover: 1.5f,
                thrustWindup: 0.5f,
                thrustActive: 0.5f,
                thrustRecover: 0.75f,
                trialIdleGapMin: 1.0f,
                trialIdleGapMax: 1.5f,
                executionIdleGapMin: 0.5f,
                executionIdleGapMax: 1.0f,
                scaleMaxRange: 14f,
                thrustMinRange: 9f,
                guillotineReturnFraction: 0.5f,
                lastWordFraction: 0.25f,
                lastWordChainGap: 0.25f,
                lastWordFinalRecover: 2.0f,
                transitionDuration: 3.0f,
                stunImmunity: 6.0f,
                verdictWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Verdict ? 3f : 0f,
                volleyWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Volley ? 2f : 0f,
                trialGuillotineWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Guillotine ? 2f : 0f,
                scaleWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Scale ? 1.5f : 0f,
                reapWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Reap ? 3f : 0f,
                executionStrikeWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.ExecutionStrike ? 2f : 0f,
                executionGuillotineWeight: onlyAttack == JudgeAttack.None || onlyAttack == JudgeAttack.Guillotine ? 1.5f : 0f,
                engageDelay: 3f,
                executionIdleWatchdog: executionIdleWatchdog,
                repulseTriggerRadius: repulse ? 6f : 0f,
                repulseCloseTime: 1.25f,
                repulseCooldown: 3.5f,
                repulseTelegraph: 0.75f,
                repulseRecover: 0.5f);
        }

        private sealed class FixedRandom : IJudgeRandom
        {
            private readonly float _value;
            /// <summary>Creates a deterministic random source.</summary>
            public FixedRandom(float value) => _value = value;
            /// <summary>Returns the configured sample.</summary>
            public float Value01() => _value;
        }

        private readonly struct AttackEvent
        {
            /// <summary>Records the supplied callback payload.</summary>
            public AttackEvent(JudgeAttack attack, bool flag = false, float duration = 0f, int index = 0)
            {
                Attack = attack;
                Flag = flag;
                Duration = duration;
                Index = index;
            }
            /// <summary>Attack associated with this callback.</summary>
            public JudgeAttack Attack { get; }
            /// <summary>Parryability for telegraphs or interruption for endings.</summary>
            public bool Flag { get; }
            /// <summary>Telegraph duration.</summary>
            public float Duration { get; }
            /// <summary>Strike index.</summary>
            public int Index { get; }
        }

        private sealed class RecordingActions : JudgeBossLoop.IActions
        {
            private readonly List<JudgePhase> _phases = new List<JudgePhase>();
            private readonly List<AttackEvent> _telegraphs = new List<AttackEvent>();
            private readonly List<AttackEvent> _executions = new List<AttackEvent>();
            private readonly List<AttackEvent> _ends = new List<AttackEvent>();
            private readonly List<float> _stunDurations = new List<float>();
            private readonly Action _executeCallback;
            private int _stunEndCount;
            /// <summary>Creates a recorder with an optional synchronous execute hook.</summary>
            public RecordingActions(Action executeCallback = null) => _executeCallback = executeCallback;
            /// <summary>Recorded phase transitions.</summary>
            public IReadOnlyList<JudgePhase> Phases => _phases;
            /// <summary>Recorded windup starts.</summary>
            public IReadOnlyList<AttackEvent> Telegraphs => _telegraphs;
            /// <summary>Recorded strike executions.</summary>
            public IReadOnlyList<AttackEvent> Executions => _executions;
            /// <summary>Recorded attack endings.</summary>
            public IReadOnlyList<AttackEvent> Ends => _ends;
            /// <summary>Recorded stun durations.</summary>
            public IReadOnlyList<float> StunDurations => _stunDurations;
            /// <summary>Number of completed or cancelled stuns.</summary>
            public int StunEndCount => _stunEndCount;
            /// <summary>Total callbacks received.</summary>
            public int CallbackCount => _phases.Count + _telegraphs.Count + _executions.Count + _ends.Count + _stunDurations.Count + _stunEndCount;
            /// <summary>Records the new phase.</summary>
            public void OnPhaseChanged(JudgePhase phase) => _phases.Add(phase);
            /// <summary>Records a windup and its parry flag.</summary>
            public void OnTelegraph(JudgeAttack attack, bool parryable, float duration) => _telegraphs.Add(new AttackEvent(attack, parryable, duration));
            /// <summary>Records a strike, then invokes the optional synchronous hook.</summary>
            public void OnAttackExecute(JudgeAttack attack, int index)
            {
                _executions.Add(new AttackEvent(attack, index: index));
                _executeCallback?.Invoke();
            }
            /// <summary>Records whether an attack was interrupted.</summary>
            public void OnAttackEnd(JudgeAttack attack, bool interrupted) => _ends.Add(new AttackEvent(attack, interrupted));
            /// <summary>Records the accepted stun duration.</summary>
            public void OnStunStarted(float duration) => _stunDurations.Add(duration);
            /// <summary>Counts completed or cancelled stuns.</summary>
            public void OnStunEnded() => _stunEndCount++;
        }
    }
}
