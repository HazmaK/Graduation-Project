using NUnit.Framework;
using RetroFPS.Run.Araf;
using RetroFPS.Stats;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Temporary wound replacement, removal and stat notification contracts.</summary>
    public sealed class ArafWoundTests
    {
        private ArafTuning _tuning;
        private StatSheet _sheet;

        /// <summary>Creates configured temporary tuning and a fresh stat sheet.</summary>
        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<ArafTuning>();
            _tuning.Configure(25f, 3, 3, null);
            _sheet = new StatSheet();
        }

        /// <summary>Destroys the temporary tuning asset.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        /// <summary>The configured wound subtracts 25 flat health from a base of 100.</summary>
        [Test]
        public void ApplyReducesBaseHundredToSeventyFive()
        {
            Assert.That(ArafWound.Apply(_sheet, _tuning.WoundMaxHealth), Is.True);
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(75f));
        }

        /// <summary>Repeated application replaces the previous source instead of stacking.</summary>
        [Test]
        public void DoubleApplyDoesNotStackAndCanReplaceAmount()
        {
            Assert.That(ArafWound.Apply(_sheet, _tuning.WoundMaxHealth), Is.True);
            Assert.That(ArafWound.Apply(_sheet, _tuning.WoundMaxHealth), Is.True);
            Assert.That(_sheet.Count, Is.EqualTo(1));
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(75f));
            Assert.That(ArafWound.Apply(_sheet, 10f), Is.True);
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(90f));
        }

        /// <summary>Removal restores the base maximum and is safe to repeat.</summary>
        [Test]
        public void RemoveRestoresHundred()
        {
            ArafWound.Apply(_sheet, _tuning.WoundMaxHealth);
            ArafWound.Remove(_sheet);
            ArafWound.Remove(_sheet);
            Assert.That(_sheet.Count, Is.Zero);
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(100f));
        }

        /// <summary>Removing the reserved wound source preserves modifiers from all card sources.</summary>
        [Test]
        public void RemovePreservesCardModifiers()
        {
            _sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 10f));
            _sheet.Add(16, new StatModifier(StatId.DamageDealt, StatOp.Multiply, 2f));
            ArafWound.Apply(_sheet, _tuning.WoundMaxHealth);
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(85f));
            ArafWound.Remove(_sheet);
            Assert.That(_sheet.Count, Is.EqualTo(2));
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(110f));
            Assert.That(_sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(20f));
        }

        /// <summary>Both wound mutations notify listeners after updating the effective maximum.</summary>
        [Test]
        public void ApplyAndRemoveRaiseStatChangeNotifications()
        {
            int calls = 0;
            float observed = 0f;
            _sheet.OnChanged += () => { calls++; observed = _sheet.Evaluate(StatId.MaxHealth, 100f); };
            ArafWound.Apply(_sheet, _tuning.WoundMaxHealth);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(75f));
            ArafWound.Remove(_sheet);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(observed, Is.EqualTo(100f));
        }

        /// <summary>Nonpositive application is a no-op, including when a wound already exists.</summary>
        [TestCase(0f)]
        [TestCase(-25f)]
        public void NonpositiveAmountDoesNotChangeExistingWound(float amount)
        {
            ArafWound.Apply(_sheet, _tuning.WoundMaxHealth);
            int version = _sheet.Version;
            Assert.That(ArafWound.Apply(_sheet, amount), Is.False);
            Assert.That(_sheet.Version, Is.EqualTo(version));
            Assert.That(_sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(75f));
        }

        /// <summary>Missing sheets are safe during lifecycle teardown.</summary>
        [Test]
        public void NullSheetIsIgnored()
        {
            Assert.That(ArafWound.Apply(null, _tuning.WoundMaxHealth), Is.False);
            Assert.DoesNotThrow(() => ArafWound.Remove(null));
        }
    }
}
