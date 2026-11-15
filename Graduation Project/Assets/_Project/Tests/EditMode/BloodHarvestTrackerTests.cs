using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Pure harvest decisions, independent of weapon presentation and Unity physics.</summary>
    public sealed class BloodHarvestTrackerTests
    {
        /// <summary>Idle, recovery, transition and kneel grace hits have no genuine stun or death edge.</summary>
        [Test]
        public void LivingUnstunnedTarget_NeverGrantsHarvest()
        {
            var tracker = new BloodHarvestTracker(2);
            var target = new object();
            for (int i = 0; i < 20; i++)
                Assert.That(tracker.TryHarvest(target, true, true, false, 0), Is.False);
        }

        /// <summary>Cooldown expiry alone cannot harvest the same accepted stun twice.</summary>
        [Test]
        public void Stun_HarvestsOnceUntilANewAcceptedStun()
        {
            var tracker = new BloodHarvestTracker(2);
            var target = new object();
            Assert.That(tracker.TryHarvest(target, true, true, true, 1), Is.True);
            Assert.That(tracker.TryHarvest(target, true, true, true, 1), Is.False);
            Assert.That(tracker.TryHarvest(target, true, true, false, 1), Is.False);
            Assert.That(tracker.TryHarvest(target, true, true, true, 2), Is.True);
            Assert.That(tracker.TryHarvest(target, true, true, true, 2), Is.False);
        }

        /// <summary>Different targets have independent ledgers even when stun sequence numbers match.</summary>
        [Test]
        public void TwoTargets_DoNotShareStunConsumption()
        {
            var tracker = new BloodHarvestTracker(2);
            Assert.That(tracker.TryHarvest(new object(), true, true, true, 1), Is.True);
            Assert.That(tracker.TryHarvest(new object(), true, true, true, 1), Is.True);
        }

        /// <summary>A finisher or ordinary lunge kill rewards the alive-to-dead edge once.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Kill_HarvestsOnceIncludingPreviouslyHarvestedStun(bool stunned)
        {
            var tracker = new BloodHarvestTracker(2);
            var target = new object();
            if (stunned) Assert.That(tracker.TryHarvest(target, true, true, true, 1), Is.True);
            Assert.That(tracker.TryHarvest(target, true, false, stunned, 1), Is.True);
            Assert.That(tracker.TryHarvest(target, false, false, stunned, 1), Is.False);
        }

        /// <summary>A full ledger does not allocate or forget consumed stuns; kills still work.</summary>
        [Test]
        public void Capacity_DoesNotReenableAnOldStun()
        {
            var tracker = new BloodHarvestTracker(1);
            var first = new object();
            var second = new object();
            Assert.That(tracker.TryHarvest(first, true, true, true, 1), Is.True);
            Assert.That(tracker.TryHarvest(second, true, true, true, 1), Is.False);
            Assert.That(tracker.TryHarvest(first, true, true, true, 1), Is.False);
            Assert.That(tracker.TryHarvest(second, true, false, false, 0), Is.True);
        }
    }
}
