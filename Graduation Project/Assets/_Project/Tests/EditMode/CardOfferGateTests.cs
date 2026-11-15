using NUnit.Framework;
using RetroFPS.Run.Cards;
using RetroFPS.UI.HUD;
using UnityEngine;

namespace RetroFPS.Tests
{
    public sealed class CardOfferGateTests
    {
        private CurseBlessingDefinition first, second;
        private CardOfferGate gate;
        [SetUp]
        public void SetUp()
        {
            first = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            second = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            gate = new CardOfferGate();
        }
        [TearDown]
        public void TearDown() { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        [Test]
        public void ConfirmRequiresFullUnscaledArmDelayAndConsumesOfferOnce()
        {
            gate.Open(new CardOffer(first, second));
            Assert.That(gate.State, Is.EqualTo(CardOfferGate.GateState.Arming));
            Assert.That(gate.Selection, Is.Zero);
            Assert.That(gate.TryConfirm(out _), Is.False);
            gate.Tick(0.399d); Assert.That(gate.TryConfirm(out _), Is.False);
            gate.Tick(0.0011d); Assert.That(gate.State, Is.EqualTo(CardOfferGate.GateState.Ready));
            Assert.That(gate.TryConfirm(out var chosen), Is.True); Assert.That(chosen, Is.SameAs(first));
            Assert.That(gate.TryConfirm(out _), Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void NavigationWrapsOverCardsAndRefusalAndPointerSelectionClamps(bool twoCards)
        {
            int count = twoCards ? 2 : 1;
            gate.Open(new CardOffer(first, twoCards ? second : null));
            gate.Move(-1); Assert.That(gate.Selection, Is.EqualTo(count));
            gate.Move(1); Assert.That(gate.Selection, Is.Zero);
            for (int i = 0; i <= count; i++) gate.Move(1);
            Assert.That(gate.Selection, Is.Zero);
            gate.Select(100); Assert.That(gate.Selection, Is.EqualTo(count));
            gate.Select(-100); Assert.That(gate.Selection, Is.Zero);
        }
        [Test]
        public void SecondCardAndRefusalResolveDistinctly()
        {
            gate.Open(new CardOffer(first, second)); gate.Tick(0.4d); gate.Move(1);
            Assert.That(gate.TryConfirm(out var chosen), Is.True); Assert.That(chosen, Is.SameAs(second));
            gate.Open(new CardOffer(first, null)); gate.Tick(0.4d); gate.Move(1);
            Assert.That(gate.TryConfirm(out chosen), Is.True); Assert.That(chosen, Is.Null);
        }
        [Test]
        public void ClosedGateIgnoresAllInputAndReopeningRearms()
        {
            gate.Tick(10d); gate.Move(1); gate.Select(10);
            Assert.That(gate.State, Is.EqualTo(CardOfferGate.GateState.Closed));
            Assert.That(gate.Selection, Is.Zero); Assert.That(gate.TryConfirm(out _), Is.False);
            gate.Open(new CardOffer(first, null)); gate.Tick(1d); gate.Close(); gate.Tick(1d);
            Assert.That(gate.TryConfirm(out _), Is.False);
            gate.Open(new CardOffer(first, second)); Assert.That(gate.TryConfirm(out _), Is.False);
        }
        [Test]
        public void EmptyOfferOnlyAllowsDeliberateRefusal()
        {
            gate.Open(default); gate.Move(-1); gate.Tick(0.4d);
            Assert.That(gate.Selection, Is.Zero); Assert.That(gate.TryConfirm(out var chosen), Is.True);
            Assert.That(chosen, Is.Null);
        }
        [Test]
        public void InvalidDeltaCannotArmTheGate()
        {
            gate.Open(new CardOffer(first, null)); gate.Tick(double.NaN); gate.Tick(-10d);
            Assert.That(gate.TryConfirm(out _), Is.False);
        }
    }
}
