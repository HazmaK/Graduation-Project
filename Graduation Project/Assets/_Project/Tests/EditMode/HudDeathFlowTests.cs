using NUnit.Framework;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    /// <summary>Event-only true-death visibility and the one-shot unscaled confirmation contract.</summary>
    public sealed class HudDeathFlowTests
    {
        /// <summary>Zero health and time passing cannot reconstruct a death card without the true-death event.</summary>
        [Test]
        public void OrdinaryDeathAndArafDoNotOpenWithoutTrueDeathEvent()
        {
            var health = new HealthDisplayModel();
            var gate = new TrueDeathGate();
            health.Update(0f, 100f);
            Assert.That(gate.TryConfirm(0d), Is.False);
            Assert.That(gate.TryConsumeRequest(100d), Is.False);
            health.Update(100f, 100f); // Araf revival remains unrelated to the card latch.
            Assert.That(gate.Visible, Is.False);
            Assert.That(gate.Confirmed, Is.False);
        }

        /// <summary>Repeated true-death events do not reopen or rearm the same card.</summary>
        [Test]
        public void TrueDeathOpensOnceAndRepeatedSubmitCannotPostponeRequest()
        {
            var gate = new TrueDeathGate();
            Assert.That(gate.OnTrueDeath(), Is.True);
            Assert.That(gate.OnTrueDeath(), Is.False);
            Assert.That(gate.Visible, Is.True);
            gate.ObserveInputs(false, false, false, false, true);
            Assert.That(gate.TryConsumeRequest(10d), Is.False);
            Assert.That(gate.TryConfirm(10d), Is.True);
            Assert.That(gate.TryConfirm(10.04d), Is.False);
            Assert.That(gate.TryConsumeRequest(10.079d), Is.False);
            Assert.That(gate.TryConsumeRequest(10.08d), Is.True);
            Assert.That(gate.RequestConsumed, Is.True);
            Assert.That(gate.OnTrueDeath(), Is.False);
            Assert.That(gate.TryConfirm(11d), Is.False);
            Assert.That(gate.TryConsumeRequest(11d), Is.False);
        }

        /// <summary>Snapping reduced-motion visuals cannot bypass arming or the logical 80 ms lock.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void ReducedMotionPreservesConfirmationGate(bool reduceMotion)
        {
            var gate = new TrueDeathGate();
            var visual = new MenuTween();
            gate.OnTrueDeath();
            visual.Start(reduceMotion ? 0f : 0.4f);
            visual.Tick(0.4f);
            gate.ObserveInputs(false, false, false, false, visual.Complete);
            Assert.That(gate.TryConfirm(0.599d), Is.False);
            Assert.That(gate.TryConfirm(0.6d), Is.True);
            Assert.That(gate.TryConsumeRequest(0.679d), Is.False);
            Assert.That(gate.TryConsumeRequest(0.681d), Is.True);
            Assert.That(gate.TryConsumeRequest(0.682d), Is.False);
        }

        /// <summary>Early presses cannot confirm or restart the arming deadline; the fade is an independent gate.</summary>
        [Test]
        public void ConfirmRequiresMinimumArmingTimeAndCompletedFade()
        {
            var gate = new TrueDeathGate();
            gate.OnTrueDeath(10d, 0.6d);
            gate.ObserveInputs(false, false, false, false, true);
            Assert.That(gate.TryConfirm(10.1d), Is.False);
            Assert.That(gate.TryConfirm(10.599d), Is.False);
            gate.ObserveInputs(false, false, false, false, false);
            Assert.That(gate.TryConfirm(10.6d), Is.False);
            Assert.That(gate.TryConsumeRequest(20d), Is.False);
            gate.ObserveInputs(false, false, false, false, true);
            Assert.That(gate.TryConfirm(10.6d), Is.True);
            Assert.That(gate.TryConfirm(10.7d), Is.False);
            Assert.That(gate.TryConsumeRequest(10.681d), Is.True);
            Assert.That(gate.TryConsumeRequest(11d), Is.False);
        }

        /// <summary>Every confirm control must be seen released after opening, including while time has already elapsed.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void HeldConfirmIsIgnoredUntilReleasedAndArmed(int heldControl)
        {
            var gate = new TrueDeathGate();
            // Observations before opening cannot arm the card.
            gate.ObserveInputs(false, false, false, false, true);
            gate.OnTrueDeath(5d, 0.6d);
            Assert.That(gate.TryConfirm(6d), Is.False);
            gate.ObserveInputs(heldControl == 0, heldControl == 1, heldControl == 2, heldControl == 3, true);
            Assert.That(gate.TryConfirm(6d), Is.False);
            Assert.That(gate.TryConsumeRequest(7d), Is.False);
            gate.ObserveInputs(false, false, false, false, true);
            Assert.That(gate.TryConfirm(7d), Is.True);
            Assert.That(gate.TryConfirm(7.04d), Is.False);
            Assert.That(gate.TryConsumeRequest(7.081d), Is.True);
            Assert.That(gate.TryConsumeRequest(8d), Is.False);
        }

        /// <summary>Release observations are tracked independently rather than requiring a simultaneous all-up frame.</summary>
        [Test]
        public void EachControlCanBeReleasedOnADifferentFrame()
        {
            var gate = new TrueDeathGate();
            gate.OnTrueDeath();
            gate.ObserveInputs(false, true, true, true, true);
            gate.ObserveInputs(true, false, true, true, true);
            gate.ObserveInputs(true, true, false, true, true);
            Assert.That(gate.TryConfirm(1d), Is.False);
            gate.ObserveInputs(true, true, true, false, true);
            Assert.That(gate.TryConfirm(1d), Is.True);
        }
    }
}
