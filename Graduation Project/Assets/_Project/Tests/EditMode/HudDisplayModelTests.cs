using NUnit.Framework;
using RetroFPS.UI.HUD;

namespace RetroFPS.Tests
{
    /// <summary>Deterministic numeric decomposition and quantized cooldown contracts.</summary>
    public sealed class HudDisplayModelTests
    {
        /// <summary>Health preserves large values and clamps only the base fill.</summary>
        [TestCase(0f, 100f, 0, 0, 0f)]
        [TestCase(100f, 100f, 100, 0, 1f)]
        [TestCase(125f, 100f, 100, 25, 1f)]
        [TestCase(4096f, 2000f, 2000, 2096, 1f)]
        [TestCase(25f, 100f, 25, 0, 0.25f)]
        [TestCase(0f, 0f, 0, 0, 0f)]
        [TestCase(25f, 0f, 0, 25, 0f)]
        [TestCase(-5f, -10f, 0, 0, 0f)]
        public void HealthDecomposesWithoutTruncation(float current, float maximum, int baseHealth, int overflow, float fill)
        {
            var model = new HealthDisplayModel();
            Assert.That(model.Update(current, maximum), Is.True);
            Assert.That(model.Base, Is.EqualTo(baseHealth));
            Assert.That(model.Overflow, Is.EqualTo(overflow));
            Assert.That(model.Current, Is.EqualTo(baseHealth + overflow));
            Assert.That(model.BaseFill, Is.EqualTo(fill).Within(0.00001f));
        }

        /// <summary>Sub-integer decay dirties the fill while keeping cached numeric output unchanged.</summary>
        [Test]
        public void HealthNumbersAndFillHaveIndependentDirtyChecks()
        {
            var model = new HealthDisplayModel();
            model.Update(80f, 100f);
            Assert.That(model.Update(80f, 100f), Is.False);
            Assert.That(model.NumbersChanged, Is.False);
            Assert.That(model.FillChanged, Is.False);
            Assert.That(model.Update(79.9f, 100f), Is.True);
            Assert.That(model.Current, Is.EqualTo(80));
            Assert.That(model.NumbersChanged, Is.False);
            Assert.That(model.FillChanged, Is.True);
            model.Update(79.5f, 100f);
            Assert.That(model.Current, Is.EqualTo(80));
            model.Update(79.49f, 100f);
            Assert.That(model.Current, Is.EqualTo(79));
            Assert.That(model.NumbersChanged, Is.True);
            model.Update(4096f, 2000f);
            Assert.That(model.Update(4096f, 2000f), Is.False);
            Assert.That(model.Current, Is.EqualTo(4096));
            model.Update(4096f, 3000f);
            Assert.That(model.NumbersChanged, Is.True);
            Assert.That(model.FillChanged, Is.False);
        }

        /// <summary>Recovery is clamped and bucketed, with exact readiness at one.</summary>
        [TestCase(-1f, 0, false)]
        [TestCase(0f, 0, false)]
        [TestCase(0.049f, 0, false)]
        [TestCase(0.051f, 1, false)]
        [TestCase(0.5f, 10, false)]
        [TestCase(0.7f, 14, false)]
        [TestCase(0.999f, 19, false)]
        [TestCase(1f, 20, true)]
        [TestCase(2f, 20, true)]
        public void CooldownBoundaries(float recovered, int bucket, bool ready)
        {
            var cache = new CooldownDisplayCache();
            Assert.That(cache.Update(recovered), Is.True);
            Assert.That(cache.Bucket, Is.EqualTo(bucket));
            Assert.That(cache.Ready, Is.EqualTo(ready));
            Assert.That(cache.Fill, Is.EqualTo(bucket / 20f));
            Assert.That(cache.Update(recovered), Is.False);
        }

        /// <summary>Binary active and visibility changes cannot be lost inside a bucket.</summary>
        [Test]
        public void CooldownDirtyStateIncludesBinaryStates()
        {
            var cache = new CooldownDisplayCache();
            cache.Update(0.501f);
            Assert.That(cache.Update(0.52f), Is.False);
            Assert.That(cache.Update(0.52f, true), Is.True);
            Assert.That(cache.Active, Is.True);
            Assert.That(cache.Update(0.52f, true, false), Is.True);
            Assert.That(cache.Visible, Is.False);
            Assert.That(cache.Update(0.52f, true, false), Is.False);
            var guarded = new CooldownDisplayCache(0);
            guarded.Update(float.NaN);
            Assert.That(guarded.Steps, Is.EqualTo(1));
            Assert.That(guarded.Fill, Is.Zero);
        }

        /// <summary>Repeated resource ticks allocate no managed memory after construction.</summary>
        [Test]
        public void WarmResourceTicksDoNotAllocate()
        {
            var health = new HealthDisplayModel();
            var cooldown = new CooldownDisplayCache();
            health.Update(4096f, 100f);
            cooldown.Update(0.5f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                health.Update(4096f - i, 100f);
                cooldown.Update((i % 21) / 20f);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
    }
}
