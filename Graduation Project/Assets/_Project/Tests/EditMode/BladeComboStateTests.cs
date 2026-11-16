using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Pure combo input, wind-up, recovery, cancellation and guard-bounce timing.</summary>
    public sealed class BladeComboStateTests
    {
        private const float LightCooldown = 0.22f;
        private const float FinisherCooldown = 0.44f;
        private const float ResetTimeout = 0.70f;
        private const float BufferWindow = 0.15f;
        private const float Windup = 0.10f;
        private const float BouncePenalty = 0.30f;
        private const float Epsilon = 0.001f;
        private const float Tolerance = 0.00001f;
        private const int First = 1;
        private const int Second = 2;
        private const int Finisher = 3;

        /// <summary>Four legal presses traverse the chain and use each step's recovery.</summary>
        [Test]
        public void LegalPresses_CycleStepsAndUseStepCooldowns()
        {
            var state = new BladeComboState();
            Assert.That(state.Press(out int first), Is.True);
            Assert.That(first, Is.EqualTo(First));
            Assert.That(state.CooldownRemaining, Is.EqualTo(LightCooldown).Within(Tolerance));
            state.Advance(LightCooldown);
            Assert.That(state.Press(out int second), Is.True);
            Assert.That(second, Is.EqualTo(Second));
            Assert.That(state.CooldownRemaining, Is.EqualTo(LightCooldown).Within(Tolerance));
            state.Advance(LightCooldown);
            Assert.That(state.Press(out int finisher), Is.True);
            Assert.That(finisher, Is.EqualTo(Finisher));
            Assert.That(state.CooldownRemaining, Is.EqualTo(FinisherCooldown).Within(Tolerance));
            state.Advance(FinisherCooldown);
            Assert.That(state.TryResolveFinisher(), Is.True);
            Assert.That(state.Press(out int next), Is.True);
            Assert.That(next, Is.EqualTo(First));
        }

        /// <summary>Reset time begins at attack start, not at recovery completion.</summary>
        [Test]
        public void ResetTimeout_IsMeasuredFromAttackStart()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown);
            Assert.That(state.NextStep, Is.EqualTo(Second));
            state.Advance(ResetTimeout - LightCooldown);
            Assert.That(state.NextStep, Is.EqualTo(Second));
            state.Advance(Epsilon);
            Assert.That(state.NextStep, Is.EqualTo(First));
            state.Press(out int step);
            Assert.That(step, Is.EqualTo(First));
        }

        /// <summary>An early fresh press is rejected and never becomes an automatic strike.</summary>
        [Test]
        public void PressOutsideBufferWindow_IsNotRemembered()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown - BufferWindow - Epsilon);
            Assert.That(state.CanAcceptPress, Is.False);
            Assert.That(state.Press(out _), Is.False);
            Assert.That(state.HasBufferedPress, Is.False);
            state.Advance(LightCooldown);
            Assert.That(state.TryStartBuffered(out _), Is.False);
        }

        /// <summary>The inclusive buffer boundary queues one strike at the recovery boundary.</summary>
        [Test]
        public void BufferBoundary_QueuesAndFiresExactlyOnceWhenLegal()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown - BufferWindow);
            Assert.That(state.CanAcceptPress, Is.True);
            Assert.That(state.Press(out _), Is.False);
            Assert.That(state.HasBufferedPress, Is.True);
            state.Advance(BufferWindow - Epsilon);
            Assert.That(state.TryStartBuffered(out _), Is.False);
            state.Advance(Epsilon);
            Assert.That(state.TryStartBuffered(out int step), Is.True);
            Assert.That(step, Is.EqualTo(Second));
            state.Advance(LightCooldown);
            Assert.That(state.TryStartBuffered(out _), Is.False);
        }

        /// <summary>A frame spanning cooldown and buffer expiry still fires at its first legal opportunity.</summary>
        [Test]
        public void LongFrameCrossesReadyAndExpiry_FiresOneBufferedStrike()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown - BufferWindow);
            state.Press(out _);
            state.Advance(BufferWindow + Epsilon);
            Assert.That(state.TryStartBuffered(out int step), Is.True);
            Assert.That(step, Is.EqualTo(Second));
            Assert.That(state.TryStartBuffered(out _), Is.False);
        }

        /// <summary>A held button provides no new edge and cannot create a combo chain.</summary>
        [Test]
        public void AdvanceWithoutFreshPress_NeverAutoRepeats()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown);
            Assert.That(state.TryStartBuffered(out _), Is.False);
            Assert.That(state.NextStep, Is.EqualTo(Second));
        }

        /// <summary>The finisher resolves only at its wind-up boundary and only once.</summary>
        [Test]
        public void Finisher_ResolvesAtWindupDeadlineOnce()
        {
            BladeComboState state = StartFinisher();
            Assert.That(state.HasPendingFinisher, Is.True);
            Assert.That(state.TryResolveFinisher(), Is.False);
            state.Advance(Windup - Epsilon);
            Assert.That(state.TryResolveFinisher(), Is.False);
            state.Advance(Epsilon);
            Assert.That(state.TryResolveFinisher(), Is.True);
            Assert.That(state.TryResolveFinisher(), Is.False);
            Assert.That(state.CooldownRemaining, Is.EqualTo(FinisherCooldown - Windup).Within(Tolerance));
        }

        /// <summary>Wind-up presses remain accepted through the finisher's longer recovery.</summary>
        [Test]
        public void PressDuringFinisherWindup_WaitsForRecoveryThenFires()
        {
            BladeComboState state = StartFinisher();
            Assert.That(state.CanAcceptPress, Is.True);
            Assert.That(state.Press(out _), Is.False);
            state.Advance(Windup);
            Assert.That(state.TryResolveFinisher(), Is.True);
            Assert.That(state.TryStartBuffered(out _), Is.False);
            state.Advance(FinisherCooldown - Windup);
            Assert.That(state.TryStartBuffered(out int step), Is.True);
            Assert.That(step, Is.EqualTo(First));
        }

        /// <summary>With a custom wind-up extending beyond recovery and expiry, the buffered press expires.</summary>
        [Test]
        public void WindupBeyondBufferDeadline_ExpiresRememberedPress()
        {
            BladeComboState state = StartFinisher(ResetTimeout + Windup);
            state.Press(out _);
            state.Advance(FinisherCooldown + Epsilon);
            Assert.That(state.HasBufferedPress, Is.False);
            state.Advance(ResetTimeout);
            Assert.That(state.TryResolveFinisher(), Is.True);
            Assert.That(state.TryStartBuffered(out _), Is.False);
        }

        /// <summary>Holstering cancels both work items while recovery continues to elapse.</summary>
        [Test]
        public void CancelPending_ClearsWindupAndBufferButPreservesRecovery()
        {
            BladeComboState state = StartFinisher();
            state.Press(out _);
            state.CancelPending();
            Assert.That(state.HasPendingFinisher, Is.False);
            Assert.That(state.HasBufferedPress, Is.False);
            Assert.That(state.CooldownRemaining, Is.EqualTo(FinisherCooldown).Within(Tolerance));
            state.Advance(FinisherCooldown);
            Assert.That(state.TryResolveFinisher(), Is.False);
            Assert.That(state.TryStartBuffered(out _), Is.False);
            Assert.That(state.CooldownRemaining, Is.Zero);
        }

        /// <summary>Guard bounce resets the chain, clears queued work and adds recovery to the remaining cooldown.</summary>
        [Test]
        public void GuardBounce_ResetsAndAddsCooldownWithoutBufferedAttack()
        {
            var state = new BladeComboState();
            state.Press(out _);
            state.Advance(LightCooldown - BufferWindow);
            state.Press(out _);
            state.GuardBounce(BouncePenalty);
            Assert.That(state.NextStep, Is.EqualTo(First));
            Assert.That(state.CooldownRemaining, Is.EqualTo(BufferWindow + BouncePenalty).Within(Tolerance));
            Assert.That(state.HasBufferedPress, Is.False);
            state.Advance(BufferWindow + BouncePenalty);
            Assert.That(state.TryStartBuffered(out _), Is.False);
            Assert.That(state.Press(out int step), Is.True);
            Assert.That(step, Is.EqualTo(First));
        }

        /// <summary>Alternate attacks cancel pending primary damage and impose their own recovery.</summary>
        [Test]
        public void DelayPrimary_CancelsWindupAndBufferAndSetsRecovery()
        {
            BladeComboState state = StartFinisher();
            state.Press(out _);
            state.DelayPrimary(BouncePenalty);
            Assert.That(state.HasPendingFinisher, Is.False);
            Assert.That(state.HasBufferedPress, Is.False);
            Assert.That(state.CooldownRemaining, Is.EqualTo(BouncePenalty).Within(Tolerance));
        }

        /// <summary>Injected tuning, rather than defaults, determines recovery and wind-up.</summary>
        [Test]
        public void CustomTuning_IsUsedForEveryStep()
        {
            var state = new BladeComboState(Windup, BufferWindow, BouncePenalty,
                ResetTimeout, BufferWindow, Epsilon);
            state.Press(out _);
            Assert.That(state.CooldownRemaining, Is.EqualTo(Windup).Within(Tolerance));
            state.Advance(Windup);
            state.Press(out _);
            Assert.That(state.CooldownRemaining, Is.EqualTo(BufferWindow).Within(Tolerance));
            state.Advance(BufferWindow);
            state.Press(out _);
            Assert.That(state.CooldownRemaining, Is.EqualTo(BouncePenalty).Within(Tolerance));
            state.Advance(Epsilon);
            Assert.That(state.TryResolveFinisher(), Is.True);
        }

        private static BladeComboState StartFinisher(float windup = Windup)
        {
            var state = new BladeComboState(LightCooldown, LightCooldown, FinisherCooldown,
                ResetTimeout, BufferWindow, windup);
            state.Press(out _);
            state.Advance(LightCooldown);
            state.Press(out _);
            state.Advance(LightCooldown);
            state.Press(out _);
            return state;
        }
    }
}
