using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Pure harvest decisions, independent of weapon presentation and Unity physics.</summary>
    public sealed class BloodHarvestTrackerTests
    {
        private const int LedgerCapacity = 2;
        private const int RepeatedHits = 20;
        private const uint FirstSequence = 1;
        private const uint NextSequence = FirstSequence + 1;

        /// <summary>Idle, recovery, transition and kneel grace hits have no genuine stun or death edge.</summary>
        [Test]
        public void LivingUnstunnedTarget_NeverGrantsHarvest()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            var target = new object();
            for (int i = 0; i < RepeatedHits; i++)
                Assert.That(tracker.TryHarvest(target, true, true, false, 0), Is.False);
        }

        /// <summary>Cooldown expiry alone cannot harvest the same accepted stun twice.</summary>
        [Test]
        public void Stun_HarvestsOnceUntilANewAcceptedStun()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            var target = new object();
            Assert.That(tracker.TryHarvest(target, true, true, true, FirstSequence), Is.True);
            Assert.That(tracker.TryHarvest(target, true, true, true, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(target, true, true, false, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(target, true, true, true, NextSequence), Is.True);
            Assert.That(tracker.TryHarvest(target, true, true, true, NextSequence), Is.False);
        }

        /// <summary>Different targets have independent ledgers even when stun sequence numbers match.</summary>
        [Test]
        public void TwoTargets_DoNotShareStunConsumption()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            Assert.That(tracker.TryHarvest(new object(), true, true, true, FirstSequence), Is.True);
            Assert.That(tracker.TryHarvest(new object(), true, true, true, FirstSequence), Is.True);
        }

        /// <summary>A plain kill never grants Blood Harvest without an existing stun.</summary>
        [Test]
        public void UnstunnedKill_DoesNotGrantHarvest()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            var target = new object();
            Assert.That(tracker.TryHarvest(target, true, false, false, 0), Is.False);
            Assert.That(tracker.TryHarvest(target, false, false, false, 0), Is.False);
        }

        /// <summary>A killing hit on an unconsumed existing stun rewards once.</summary>
        [Test]
        public void StunnedKill_HarvestsOnlyAnUnconsumedStun()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            var target = new object();
            Assert.That(tracker.TryHarvest(target, true, false, true, FirstSequence), Is.True);
            Assert.That(tracker.TryHarvest(target, false, false, true, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(target, true, false, true, FirstSequence), Is.False);
        }

        /// <summary>Killing a target cannot reward a stun that already healed on an earlier lunge.</summary>
        [Test]
        public void PreviouslyHarvestedStun_KillDoesNotGrantAgain()
        {
            var tracker = new BloodHarvestTracker(LedgerCapacity);
            var target = new object();
            Assert.That(tracker.TryHarvest(target, true, true, true, FirstSequence), Is.True);
            Assert.That(tracker.TryHarvest(target, true, false, true, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(target, true, false, true, NextSequence), Is.True);
        }

        /// <summary>A full ledger does not allocate or forget consumed stuns, including killing hits.</summary>
        [Test]
        public void Capacity_DoesNotReenableAnOldStun()
        {
            var tracker = new BloodHarvestTracker(1);
            var first = new object();
            var second = new object();
            Assert.That(tracker.TryHarvest(first, true, true, true, FirstSequence), Is.True);
            Assert.That(tracker.TryHarvest(second, true, true, true, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(first, true, true, true, FirstSequence), Is.False);
            Assert.That(tracker.TryHarvest(second, true, false, false, 0), Is.False);
            Assert.That(tracker.TryHarvest(second, true, false, true, FirstSequence), Is.False);
        }
    }
}
