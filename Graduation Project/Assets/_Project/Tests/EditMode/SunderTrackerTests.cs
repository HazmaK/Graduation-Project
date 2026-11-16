using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic Sunder lifetimes, reference identity and fixed-capacity eviction.</summary>
    public sealed class SunderTrackerTests
    {
        private const float StartTime = 10f;
        private const float Duration = 4f;
        private const float HalfDuration = Duration / 2f;
        private const float Multiplier = 1.25f;
        private const float StrongerMultiplier = 1.5f;
        private const int Capacity = 32;

        /// <summary>An unknown key has no amplification or lifetime.</summary>
        [Test]
        public void AbsentKey_ReturnsOneAndNoRemainingTime()
        {
            var tracker = new SunderTracker();
            var key = new object();
            Assert.That(tracker.GetMultiplier(key, StartTime), Is.EqualTo(1f));
            Assert.That(tracker.TryGetRemaining(key, StartTime, out float remaining), Is.False);
            Assert.That(remaining, Is.Zero);
        }

        /// <summary>A mark amplifies damage throughout its active duration.</summary>
        [Test]
        public void Apply_ReturnsMultiplierAndRemainingTimeUntilExpiry()
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime), Is.EqualTo(Multiplier));
            Assert.That(tracker.GetMultiplier(key, StartTime + HalfDuration), Is.EqualTo(Multiplier));
            Assert.That(tracker.TryGetRemaining(key, StartTime + HalfDuration, out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(HalfDuration));
        }

        /// <summary>The expiry instant itself is outside the active interval.</summary>
        [Test]
        public void ExpiryBoundary_ReturnsOneAndNoRemainingTime()
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime + Duration), Is.EqualTo(1f));
            Assert.That(tracker.TryGetRemaining(key, StartTime + Duration, out float remaining), Is.False);
            Assert.That(remaining, Is.Zero);
        }

        /// <summary>Reapplying gives a full fresh duration past the old deadline.</summary>
        [Test]
        public void Reapply_ExtendsExpiry()
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, Multiplier);
            tracker.Apply(key, StartTime + HalfDuration, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime + Duration), Is.EqualTo(Multiplier));
            Assert.That(tracker.TryGetRemaining(key, StartTime + Duration, out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(HalfDuration));
            Assert.That(tracker.GetMultiplier(key, StartTime + Duration + HalfDuration), Is.EqualTo(1f));
        }

        /// <summary>Refreshing retains the strongest active multiplier in either application order.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Reapply_KeepsHigherMultiplier(bool strongerFirst)
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, strongerFirst ? StrongerMultiplier : Multiplier);
            tracker.Apply(key, StartTime + HalfDuration, Duration, strongerFirst ? Multiplier : StrongerMultiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime + Duration), Is.EqualTo(StrongerMultiplier));
        }

        /// <summary>Expired strength does not carry into a new mark on the same key.</summary>
        [Test]
        public void ReapplyAfterExpiry_UsesNewMultiplier()
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, StrongerMultiplier);
            tracker.Apply(key, StartTime + Duration, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime + Duration), Is.EqualTo(Multiplier));
        }

        /// <summary>Different keys maintain independent strengths and deadlines.</summary>
        [Test]
        public void IndependentKeys_DoNotShareMarks()
        {
            var tracker = new SunderTracker();
            var first = new object();
            var second = new object();
            tracker.Apply(first, StartTime, HalfDuration, Multiplier);
            tracker.Apply(second, StartTime, Duration, StrongerMultiplier);
            Assert.That(tracker.GetMultiplier(first, StartTime + HalfDuration), Is.EqualTo(1f));
            Assert.That(tracker.GetMultiplier(second, StartTime + HalfDuration), Is.EqualTo(StrongerMultiplier));
        }

        /// <summary>Value equality cannot merge two separate target references.</summary>
        [Test]
        public void ValueEqualKeys_StillUseReferenceIdentity()
        {
            var tracker = new SunderTracker();
            object first = new string('x', 1);
            object second = new string('x', 1);
            tracker.Apply(first, StartTime, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(second, StartTime), Is.EqualTo(1f));
        }

        /// <summary>Null cannot consume a slot or acquire amplification.</summary>
        [Test]
        public void NullKey_IsIgnored()
        {
            var tracker = new SunderTracker();
            tracker.Apply(null, StartTime, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(null, StartTime), Is.EqualTo(1f));
            Assert.That(tracker.TryGetRemaining(null, StartTime, out float remaining), Is.False);
            Assert.That(remaining, Is.Zero);
        }

        /// <summary>A full tracker replaces the earliest expiry even when it is not the first slot.</summary>
        [Test]
        public void CapacityOverflow_ReplacesSoonestExpiring()
        {
            var tracker = new SunderTracker();
            var keys = new object[Capacity];
            int soonestIndex = Capacity / 2;
            for (int i = 0; i < Capacity; i++)
            {
                keys[i] = new object();
                tracker.Apply(keys[i], StartTime, i == soonestIndex ? HalfDuration : Duration, Multiplier);
            }
            var replacement = new object();
            tracker.Apply(replacement, StartTime, Duration, StrongerMultiplier);
            for (int i = 0; i < Capacity; i++)
                Assert.That(tracker.GetMultiplier(keys[i], StartTime), Is.EqualTo(i == soonestIndex ? 1f : Multiplier));
            Assert.That(tracker.GetMultiplier(replacement, StartTime), Is.EqualTo(StrongerMultiplier));
        }

        /// <summary>Expired slots are reused before any active entry is evicted.</summary>
        [Test]
        public void ExpiredSlots_AreReclaimedBeforeEvictingLiveMarks()
        {
            var tracker = new SunderTracker();
            var liveKey = new object();
            tracker.Apply(liveKey, StartTime, Duration, Multiplier);
            for (int i = 1; i < Capacity; i++)
                tracker.Apply(new object(), StartTime, HalfDuration, Multiplier);
            var replacement = new object();
            tracker.Apply(replacement, StartTime + HalfDuration, Duration, StrongerMultiplier);
            Assert.That(tracker.GetMultiplier(liveKey, StartTime + HalfDuration), Is.EqualTo(Multiplier));
            Assert.That(tracker.GetMultiplier(replacement, StartTime + HalfDuration), Is.EqualTo(StrongerMultiplier));
        }

        /// <summary>Removing clears only the requested mark and can succeed only once.</summary>
        [Test]
        public void Remove_ActiveKeyClearsMultiplierAndLifetimeOnlyForThatKey()
        {
            var tracker = new SunderTracker();
            var first = new object();
            var second = new object();
            tracker.Apply(first, StartTime, Duration, Multiplier);
            tracker.Apply(second, StartTime, Duration, StrongerMultiplier);
            Assert.That(tracker.Remove(first), Is.True);
            Assert.That(tracker.GetMultiplier(first, StartTime), Is.EqualTo(1f));
            Assert.That(tracker.TryGetRemaining(first, StartTime, out float remaining), Is.False);
            Assert.That(remaining, Is.Zero);
            Assert.That(tracker.GetMultiplier(second, StartTime), Is.EqualTo(StrongerMultiplier));
            Assert.That(tracker.Remove(first), Is.False);
        }

        /// <summary>Absent and null removal cannot disturb a stored reference-equal key.</summary>
        [Test]
        public void Remove_NullAndValueEqualUnknownKeyReturnFalse()
        {
            var tracker = new SunderTracker();
            object first = new string('x', 1);
            object equalKey = new string('x', 1);
            tracker.Apply(first, StartTime, Duration, Multiplier);
            Assert.That(tracker.Remove(null), Is.False);
            Assert.That(tracker.Remove(equalKey), Is.False);
            Assert.That(tracker.GetMultiplier(first, StartTime), Is.EqualTo(Multiplier));
        }

        /// <summary>A removed slot is reusable at capacity without evicting a surviving mark.</summary>
        [Test]
        public void Remove_AtCapacityReusesSlotWithoutEviction()
        {
            var tracker = new SunderTracker();
            var keys = new object[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                keys[i] = new object();
                tracker.Apply(keys[i], StartTime, Duration, StrongerMultiplier);
            }
            int removedIndex = Capacity / 2;
            Assert.That(tracker.Remove(keys[removedIndex]), Is.True);
            var replacement = new object();
            tracker.Apply(replacement, StartTime, Duration, Multiplier);
            for (int i = 0; i < Capacity; i++)
                Assert.That(tracker.GetMultiplier(keys[i], StartTime),
                    Is.EqualTo(i == removedIndex ? 1f : StrongerMultiplier));
            Assert.That(tracker.GetMultiplier(replacement, StartTime), Is.EqualTo(Multiplier));
        }

        /// <summary>Removing permits the same pooled target reference to receive a fresh strength.</summary>
        [Test]
        public void Remove_ReapplySameKeyDoesNotRetainOldStrength()
        {
            var tracker = new SunderTracker();
            var key = new object();
            tracker.Apply(key, StartTime, Duration, StrongerMultiplier);
            tracker.Remove(key);
            tracker.Apply(key, StartTime + HalfDuration, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(key, StartTime + HalfDuration), Is.EqualTo(Multiplier));
            Assert.That(tracker.TryGetRemaining(key, StartTime + HalfDuration, out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(Duration));
        }

        /// <summary>Clearing removes all marks and permits reuse of the tracker.</summary>
        [Test]
        public void Clear_RemovesAllMarksAndAllowsReuse()
        {
            var tracker = new SunderTracker();
            var first = new object();
            var second = new object();
            tracker.Apply(first, StartTime, Duration, Multiplier);
            tracker.Apply(second, StartTime, Duration, StrongerMultiplier);
            tracker.Clear();
            Assert.That(tracker.GetMultiplier(first, StartTime), Is.EqualTo(1f));
            Assert.That(tracker.GetMultiplier(second, StartTime), Is.EqualTo(1f));
            Assert.That(tracker.TryGetRemaining(first, StartTime, out _), Is.False);
            tracker.Apply(first, StartTime, Duration, Multiplier);
            Assert.That(tracker.GetMultiplier(first, StartTime), Is.EqualTo(Multiplier));
        }
    }
}
