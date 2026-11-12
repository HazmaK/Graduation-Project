using NUnit.Framework;
using RetroFPS.UI.HUD;

namespace RetroFPS.Tests
{
    /// <summary>Fixed-slot banner arbitration, timing and lifecycle regression coverage.</summary>
    public sealed class HudBannerQueueTests
    {
        /// <summary>Replacement discards older waiting waves of equal or lower importance.</summary>
        [TestCase(HudBannerKind.WaveCleared, HudBannerKind.Wave)]
        [TestCase(HudBannerKind.Wave, HudBannerKind.Wave)]
        [TestCase(HudBannerKind.WaveCleared, HudBannerKind.ArenaCleared)]
        public void ReplacementDiscardsOutdatedPending(HudBannerKind pendingKind, HudBannerKind replacementKind)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(pendingKind, 1, 0.6d);
            Assert.That(queue.HasPending, Is.True);
            queue.Enqueue(replacementKind, 2, 2d);
            Assert.That(queue.Current.Kind, Is.EqualTo(replacementKind));
            Assert.That(queue.HasPending, Is.False);
            queue.Tick(queue.ExpiresAt);
            Assert.That(queue.HasCurrent, Is.False);
        }

        /// <summary>Merged pending entries use their newest wave and retain higher importance.</summary>
        [TestCase(HudBannerKind.Wave, false)]
        [TestCase(HudBannerKind.WaveCleared, true)]
        public void ReplacementRespectsMergedPendingPriority(HudBannerKind replacementKind, bool keepsPending)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.WaveCleared, 1, 0.1d);
            queue.Enqueue(HudBannerKind.Wave, 2, 0.6d);
            Assert.That(queue.Pending.IsMerged, Is.True);
            queue.Enqueue(replacementKind, 3, 2d);
            Assert.That(queue.Current.Kind, Is.EqualTo(replacementKind));
            Assert.That(queue.HasPending, Is.EqualTo(keepsPending));
        }

        /// <summary>A same-wave or future pending event remains relevant after replacement.</summary>
        [TestCase(2)]
        [TestCase(3)]
        public void ReplacementKeepsRelevantPending(int pendingWave)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.WaveCleared, pendingWave, 0.6d);
            queue.Enqueue(HudBannerKind.Wave, 2, 2d);
            Assert.That(queue.HasPending, Is.True);
            queue.Tick(queue.ExpiresAt);
            Assert.That(queue.Current.Wave, Is.EqualTo(pendingWave));
        }

        /// <summary>The configured merge window applies inclusively to both current and pending slots.</summary>
        [TestCase(0.2d, 0.2d, true)]
        [TestCase(0.2d, 0.201d, false)]
        [TestCase(0.8d, 0.7d, true)]
        public void ConfiguredMergeWindow(double window, double delay, bool merged)
        {
            var queue = new HudBannerQueue(mergeWindowSeconds: window);
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, delay);
            Assert.That(queue.Current.IsMerged, Is.EqualTo(merged));
            queue.Reset();
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0d);
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, delay);
            Assert.That(queue.Current.Kind, Is.EqualTo(HudBannerKind.ArenaCleared));
            Assert.That(queue.Pending.IsMerged, Is.EqualTo(merged));
        }

        /// <summary>The configured replacement window is strict and never preempts arena clear.</summary>
        [TestCase(0.2d, 0.3d, false)]
        [TestCase(0.8d, 0.7d, true)]
        [TestCase(0.75d, 0.75d, false)]
        public void ConfiguredRemainingTimeWindow(double window, double remaining, bool replaces)
        {
            var queue = new HudBannerQueue(mergeWindowSeconds: window);
            queue.Enqueue(HudBannerKind.Wave, 1, 0d);
            queue.Enqueue(HudBannerKind.WaveCleared, 1, queue.ExpiresAt - remaining);
            Assert.That(queue.Current.Kind, Is.EqualTo(replaces ? HudBannerKind.WaveCleared : HudBannerKind.Wave));
            queue.Reset();
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, queue.ExpiresAt - 0.1d);
            Assert.That(queue.Current.Kind, Is.EqualTo(HudBannerKind.ArenaCleared));
        }

        /// <summary>More important messages replace immediately, and arena clear never yields early.</summary>
        [Test]
        public void PrioritiesAndNeverPreemptArenaClear()
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.WaveCleared, 1, 0d);
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0.1d);
            Assert.That(queue.Current.Priority, Is.EqualTo(2));
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0.2d);
            Assert.That(queue.Current.Priority, Is.EqualTo(1));
            double end = queue.ExpiresAt;
            queue.Enqueue(HudBannerKind.Wave, 2, end - 0.001d);
            Assert.That(queue.Current.Kind, Is.EqualTo(HudBannerKind.ArenaCleared));
            Assert.That(queue.Pending.Wave, Is.EqualTo(2));
            queue.Tick(end);
            Assert.That(queue.Current.Kind, Is.EqualTo(HudBannerKind.Wave));
        }

        /// <summary>The exception is strictly less than 500 ms of total display time remaining.</summary>
        [TestCase(0.499d, true)]
        [TestCase(0.500d, false)]
        public void RemainingTimePreemptionBoundary(double remaining, bool replaces)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.Wave, 1, 0d);
            queue.Enqueue(HudBannerKind.WaveCleared, 1, queue.ExpiresAt - remaining);
            Assert.That(queue.Current.Kind, Is.EqualTo(replaces ? HudBannerKind.WaveCleared : HudBannerKind.Wave));
            Assert.That(queue.HasPending, Is.EqualTo(!replaces));
        }

        /// <summary>Sealed plus wave merges through the inclusive 500 ms event interval.</summary>
        [TestCase(0.499d, true)]
        [TestCase(0.500d, true)]
        [TestCase(0.501d, false)]
        public void SealedWaveMergeBoundary(double delay, bool merged)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, delay);
            Assert.That(queue.Current.IsMerged, Is.EqualTo(merged));
            Assert.That(queue.HasPending, Is.EqualTo(!merged));
            if (merged) Assert.That(queue.Current.SecondWave, Is.EqualTo(1));
        }

        /// <summary>Only the adjacent next wave merges into a cleared banner.</summary>
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void ClearedNextWaveMerge(int nextWave, bool merged)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.WaveCleared, 1, 0d);
            queue.Enqueue(HudBannerKind.Wave, nextWave, 1d);
            Assert.That(queue.Current.IsMerged, Is.EqualTo(merged));
            Assert.That(queue.Current.Priority, Is.EqualTo(2));
            if (merged)
            {
                Assert.That(queue.Current.Kind, Is.EqualTo(HudBannerKind.WaveCleared));
                Assert.That(queue.Current.SecondKind, Is.EqualTo(HudBannerKind.Wave));
                Assert.That(queue.Current.SecondWave, Is.EqualTo(2));
            }
        }

        /// <summary>Pending storage is one slot, supports merging, and the newest waiting event wins.</summary>
        [Test]
        public void OnePendingSlotMergesAndReplaces()
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0d);
            queue.Enqueue(HudBannerKind.WaveCleared, 1, 0.1d);
            queue.Enqueue(HudBannerKind.Wave, 2, 0.2d);
            Assert.That(queue.Pending.IsMerged, Is.True);
            queue.Enqueue(HudBannerKind.WaveCleared, 2, 0.3d);
            Assert.That(queue.Pending.IsMerged, Is.False);
            Assert.That(queue.Pending.Kind, Is.EqualTo(HudBannerKind.WaveCleared));
            Assert.That(queue.Pending.Wave, Is.EqualTo(2));
            queue.Tick(queue.ExpiresAt);
            Assert.That(queue.Current.Wave, Is.EqualTo(2));
            Assert.That(queue.HasPending, Is.False);
        }

        /// <summary>Repeated events neither restart timers nor replace pending entries, even after expiry.</summary>
        [Test]
        public void DuplicateEventsAreSuppressedUntilReset()
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.Wave, 1, 0d);
            double end = queue.ExpiresAt;
            Assert.That(queue.Enqueue(HudBannerKind.Wave, 1, 0.2d), Is.False);
            Assert.That(queue.ExpiresAt, Is.EqualTo(end));
            Assert.That(queue.HasPending, Is.False);
            queue.Tick(end);
            Assert.That(queue.Enqueue(HudBannerKind.Wave, 1, end + 1d), Is.False);
            Assert.That(queue.HasCurrent, Is.False);
        }

        /// <summary>The lifecycle adapter uses the same reset for every clearing event.</summary>
        [TestCase("retry")]
        [TestCase("death")]
        [TestCase("scene")]
        [TestCase("Araf")]
        public void LifecycleResetClearsSlotsAndAllowsWaveOneAgain(string reason)
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, 0.1d);
            queue.Reset();
            Assert.That(queue.HasCurrent, Is.False, reason);
            Assert.That(queue.HasPending, Is.False, reason);
            Assert.That(queue.ExpiresAt, Is.Zero);
            Assert.That(queue.Enqueue(HudBannerKind.Wave, 1, 1d), Is.True);
            Assert.That(queue.Enqueue(HudBannerKind.Wave, 1, 1.1d), Is.False);
        }

        /// <summary>Hold and fade timings expire at their exact boundary, including long idle ticks.</summary>
        [Test]
        public void DurationsAndLongTickAreDeterministic()
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.Wave, 1, 0d);
            Assert.That(queue.ExpiresAt, Is.EqualTo(2.25d).Within(1e-9d));
            Assert.That(queue.Tick(queue.ExpiresAt - 0.001d), Is.False);
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 3d);
            Assert.That(queue.ExpiresAt, Is.EqualTo(5.65d).Within(1e-9d));
            queue.Enqueue(HudBannerKind.Wave, 2, 3.1d);
            queue.Tick(100d);
            Assert.That(queue.HasCurrent, Is.False);
            Assert.That(queue.HasPending, Is.False);
        }

        /// <summary>Event arbitration, merges, resets and expiration need no heap storage.</summary>
        [Test]
        public void WarmBannerEventsDoNotAllocate()
        {
            var queue = new HudBannerQueue();
            queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
            queue.Enqueue(HudBannerKind.Wave, 1, 0.1d);
            queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0.2d);
            queue.Enqueue(HudBannerKind.WaveCleared, 1, 0.3d);
            queue.Tick(100d);
            queue.Reset();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                queue.Reset();
                queue.Enqueue(HudBannerKind.ArenaSealed, 0, 0d);
                queue.Enqueue(HudBannerKind.Wave, 1, 0.1d);
                queue.Enqueue(HudBannerKind.ArenaCleared, 0, 0.2d);
                queue.Enqueue(HudBannerKind.WaveCleared, 1, 0.3d);
                queue.Tick(100d);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
    }
}
