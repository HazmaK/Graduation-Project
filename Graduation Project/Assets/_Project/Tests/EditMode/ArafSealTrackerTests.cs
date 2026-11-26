using NUnit.Framework;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Cards;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Seal collection order, completion and shared tuning contracts.</summary>
    public sealed class ArafSealTrackerTests
    {
        private ArafTuning _tuning;

        /// <summary>Creates configured temporary tuning for each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<ArafTuning>();
            _tuning.Configure(25f, 3, 3, new[] { new CardRarityMultipliers(1f, 0f, 0f),
                new CardRarityMultipliers(1f, 1f, 0f), new CardRarityMultipliers(0.5f, 1.5f, 1f) });
        }

        /// <summary>Destroys the temporary tuning asset.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        /// <summary>Each collection returns its order and completion prevents further changes.</summary>
        [Test]
        public void CollectsInOrderUntilComplete()
        {
            var tracker = new ArafSealTracker(_tuning.SealCount);
            Assert.That(tracker.Total, Is.EqualTo(3));
            Assert.That(tracker.Collected, Is.Zero);
            Assert.That(tracker.IsComplete, Is.False);
            for (int i = 0; i < tracker.Total; i++)
            {
                Assert.That(tracker.TryCollect(out int index), Is.True);
                Assert.That(index, Is.EqualTo(i));
                Assert.That(tracker.Collected, Is.EqualTo(i + 1));
                Assert.That(tracker.IsComplete, Is.EqualTo(i + 1 == tracker.Total));
            }
            Assert.That(tracker.TryCollect(out _), Is.False);
            Assert.That(tracker.TryCollect(out _), Is.False);
            Assert.That(tracker.Collected, Is.EqualTo(3));
        }

        /// <summary>Zero and negative totals start complete without accepting a seal.</summary>
        [TestCase(0)]
        [TestCase(-3)]
        public void NonpositiveTotalIsImmediatelyComplete(int total)
        {
            var tracker = new ArafSealTracker(total);
            Assert.That(tracker.Total, Is.Zero);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(tracker.TryCollect(out _), Is.False);
            Assert.That(tracker.Collected, Is.Zero);
        }

        /// <summary>Seal-order tuning clamps indices at both ends.</summary>
        [Test]
        public void SealRarityClampsIndices()
        {
            Assert.That(_tuning.GetSealRarity(-1).Common, Is.EqualTo(1f));
            Assert.That(_tuning.GetSealRarity(-1).Rare, Is.Zero);
            Assert.That(_tuning.GetSealRarity(1).Rare, Is.EqualTo(1f));
            Assert.That(_tuning.GetSealRarity(1).Relic, Is.Zero);
            Assert.That(_tuning.GetSealRarity(99).Common, Is.EqualTo(0.5f));
            Assert.That(_tuning.GetSealRarity(99).Rare, Is.EqualTo(1.5f));
            Assert.That(_tuning.GetSealRarity(99).Relic, Is.EqualTo(1f));
        }

        /// <summary>Missing rarity entries preserve the default catalog distribution.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void EmptyRarityUsesNeutralMultipliers(bool useNull)
        {
            _tuning.Configure(25f, 3, 3, useNull ? null : new CardRarityMultipliers[0]);
            var rarity = _tuning.GetSealRarity(99);
            Assert.That(rarity.Common, Is.EqualTo(1f));
            Assert.That(rarity.Rare, Is.EqualTo(1f));
            Assert.That(rarity.Relic, Is.EqualTo(1f));
        }
    }
}
