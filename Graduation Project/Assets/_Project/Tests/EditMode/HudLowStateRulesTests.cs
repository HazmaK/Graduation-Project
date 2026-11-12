using System;
using NUnit.Framework;
using RetroFPS.Presentation;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Boundary and complete-number contracts shared by gameplay and HUD.</summary>
    public sealed class HudLowStateRulesTests
    {
        /// <summary>Missing presentation config retains the inclusive default without requiring a player component.</summary>
        [TestCase(30f, 100f, true)]
        [TestCase(30.01f, 100f, false)]
        [TestCase(60f, 200f, true)]
        [TestCase(0f, 0f, false)]
        public void MissingConfigUsesDefaultHealthBoundary(float current, float maximum, bool expected)
            => Assert.That(HudLowStateRules.IsLowHealth(current, maximum, (HudConfig)null), Is.EqualTo(expected));

        /// <summary>Health is inclusive and absent health is guarded.</summary>
        [TestCase(30f, 100f, true)]
        [TestCase(30.01f, 100f, false)]
        [TestCase(60f, 200f, true)]
        [TestCase(125f, 100f, false)]
        [TestCase(0f, 100f, true)]
        [TestCase(0f, 0f, false)]
        [TestCase(0f, -1f, false)]
        public void HealthUsesInclusivePercentage(float current, float maximum, bool expected)
            => Assert.That(HudLowStateRules.IsLowHealth(current, maximum, 0.30f), Is.EqualTo(expected));

        /// <summary>Low ammo is strict while empty and absent resources have separate states.</summary>
        [Test]
        public void AmmoBoundariesAndZeroMaximaAreGuarded()
        {
            Assert.That(HudLowStateRules.IsLowAmmo(25, 100, 0.25f), Is.False);
            Assert.That(HudLowStateRules.IsLowAmmo(24, 100, 0.25f), Is.True);
            Assert.That(HudLowStateRules.IsLowAmmo(0, 100, 0.25f), Is.True);
            Assert.That(HudLowStateRules.IsEmptyAmmo(0, 100), Is.True);
            Assert.That(HudLowStateRules.IsEmptyAmmo(1, 100), Is.False);
            Assert.That(HudLowStateRules.IsLowAmmo(0, 0, 0.25f), Is.False);
            Assert.That(HudLowStateRules.IsEmptyAmmo(0, 0), Is.False);
            Assert.That(HudLowStateRules.IsEmptyAmmo(0, -1), Is.False);
        }

        /// <summary>Cached strings retain identity and time pieces are padded.</summary>
        [Test]
        public void NumbersAreCachedAndTimePiecesArePadded()
        {
            HudNumberCache.Prewarm();
            Assert.That(HudNumberCache.GetInteger(999), Is.SameAs(HudNumberCache.GetInteger(999)));
            Assert.That(HudNumberCache.GetDigit(0), Is.EqualTo("0"));
            Assert.That(HudNumberCache.GetTwoDigits(0), Is.EqualTo("00"));
            Assert.That(HudNumberCache.GetTwoDigits(59), Is.EqualTo("59"));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberCache.GetInteger(1000));
        }

        /// <summary>Fallback handles arbitrary signed totals without truncation and respects the destination bounds.</summary>
        [TestCase(0, "0")]
        [TestCase(1000, "1000")]
        [TestCase(4096, "4096")]
        [TestCase(int.MaxValue, "2147483647")]
        [TestCase(int.MinValue, "-2147483648")]
        public void IntegerWriterPreservesWholeValue(int value, string expected)
        {
            var buffer = new char[13];
            buffer[0] = '!';
            int count = HudNumberCache.WriteInteger(value, buffer, 1);
            Assert.That(new string(buffer, 1, count), Is.EqualTo(expected));
            Assert.That(buffer[0], Is.EqualTo('!'));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberCache.WriteInteger(value, new char[0]));
        }
        /// <summary>Authored counters preserve exact spacing, signed values and rich size suffixes.</summary>
        [TestCase("DALGA {0}/{1}", 1, 5, "DALGA 1/5")]
        [TestCase("KALAN {0}", 10, 0, "KALAN 10")]
        [TestCase("ODALAR {0}/{1}", 3, 12, "ODALAR 3/12")]
        [TestCase("ATILMA {0}/{1}", 3, 3, "ATILMA 3/3")]
        [TestCase("[{0}]", 2, 0, "[2]")]
        [TestCase("{0} / {1}", 60, 120, "60 / 120")]
        [TestCase("{0}<size=20> / {1}</size>", 100, 100, "100<size=20> / 100</size>")]
        [TestCase("{0}<size=24> / {1}</size>", 60, 120, "60<size=24> / 120</size>")]
        [TestCase("{0}/{1}", int.MinValue, int.MaxValue, "-2147483648/2147483647")]
        public void CounterWriterPreservesAuthoredSpacing(string format, int first, int second, string expected)
        {
            var buffer = new char[256];
            int length = HudNumberCache.WriteTemplate(format, first, second, buffer);
            Assert.That(new string(buffer, 0, length), Is.EqualTo(expected));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberCache.WriteTemplate(format, first, second, new char[1]));
        }

        /// <summary>Counter events reuse caller storage without managed allocations after warmup.</summary>
        [Test]
        public void WarmCounterWritesDoNotAllocate()
        {
            var buffer = new char[256];
            HudNumberCache.WriteTemplate("DALGA {0}/{1}", 1, 5, buffer);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) HudNumberCache.WriteTemplate("DALGA {0}/{1}", i, 1000, buffer);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
    }
}
