using NUnit.Framework;
using RetroFPS.Run;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Koşu rastgelelik üretecinin sözleşme testleri.</summary>
    public sealed class RunRandomTests
    {
        /// <summary>Aynı tohum aynı diziyi üretir.</summary>
        [TestCase(0UL)]
        [TestCase(42UL)]
        [TestCase(ulong.MaxValue)]
        public void SameSeedProducesSameSequence(ulong seed)
        {
            var first = new RunRandom(seed);
            var second = new RunRandom(seed);
            for (int i = 0; i < 10000; i++)
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
        }

        /// <summary>Akışlar tekrarlanabilir ve birbirinden bağımsızdır.</summary>
        [Test]
        public void DerivedStreamsAreDistinctAndDeterministic()
        {
            for (int id = 1; id <= 4; id++)
            {
                var first = RunRandom.Derive(-123, (RunStream)id);
                var repeat = RunRandom.Derive(-123, (RunStream)id);
                for (int i = 0; i < 100; i++)
                    Assert.That(first.NextUInt(), Is.EqualTo(repeat.NextUInt()));
                for (int otherId = id + 1; otherId <= 4; otherId++)
                {
                    var left = RunRandom.Derive(-123, (RunStream)id);
                    var right = RunRandom.Derive(-123, (RunStream)otherId);
                    bool differs = false;
                    for (int i = 0; i < 100; i++)
                        differs |= left.NextUInt() != right.NextUInt();
                    Assert.That(differs, Is.True);
                }
            }
        }

        /// <summary>Tamsayı sınırları korunur ve küçük aralıktaki tüm değerler görülür.</summary>
        [Test]
        public void IntegerRangeRespectsBoundsAndCoversSmallRange()
        {
            var random = new RunRandom(42);
            var hits = new bool[7];
            for (int i = 0; i < 10000; i++)
            {
                int value = random.Range(-3, 4);
                Assert.That(value, Is.GreaterThanOrEqualTo(-3).And.LessThan(4));
                hits[value + 3] = true;
                Assert.That(random.Range(int.MinValue, int.MaxValue),
                    Is.GreaterThanOrEqualTo(int.MinValue).And.LessThan(int.MaxValue));
            }
            Assert.That(hits, Is.All.True);
            Assert.That(random.Range(5, 5), Is.EqualTo(5));
            Assert.That(random.Range(5, -5), Is.EqualTo(5));
        }

        /// <summary>Kayan noktalı değerler belirtilen sınırların içinde kalır.</summary>
        [Test]
        public void FloatRangeAndValueRespectBounds()
        {
            var random = new RunRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(random.Value01, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(random.Range(-2.5f, 7.5f), Is.InRange(-2.5f, 7.5f));
                Assert.That(random.Range(-float.MaxValue, float.MaxValue),
                    Is.InRange(-float.MaxValue, float.MaxValue));
            }
            Assert.That(random.Range(2f, 2f), Is.EqualTo(2f));
        }

        /// <summary>Kesin olasılıklar her çekilişte korunur.</summary>
        [Test]
        public void ChanceRespectsCertainOutcomes()
        {
            var random = new RunRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(random.Chance(0f), Is.False);
                Assert.That(random.Chance(1f), Is.True);
            }
        }

        /// <summary>Sıfır ve negatif ağırlıklar seçilmez, count sınırı korunur.</summary>
        [Test]
        public void WeightedIndexIgnoresNonPositiveWeights()
        {
            var random = new RunRandom(42);
            float[] weights = { 0f, -2f, 5f, 0f, 100f };
            for (int i = 0; i < 10000; i++)
                Assert.That(random.WeightedIndex(weights, 4), Is.EqualTo(2));
            Assert.That(random.WeightedIndex(new[] { 0f, 0f }, 2), Is.EqualTo(-1));
            Assert.That(random.WeightedIndex(new[] { -1f, 0f }, 2), Is.EqualTo(-1));
            Assert.That(random.WeightedIndex(weights, 0), Is.EqualTo(-1));
        }

        /// <summary>1:3 ağırlık oranı yüzde on toleransla gözlenir.</summary>
        [Test]
        public void WeightedIndexApproximatelyFollowsWeights()
        {
            var random = new RunRandom(42);
            float[] weights = { 1f, 3f };
            int firstCount = 0;
            for (int i = 0; i < 20000; i++)
            {
                int index = random.WeightedIndex(weights, 2);
                Assert.That(index, Is.InRange(0, 1));
                if (index == 0) firstCount++;
            }
            double ratio = (20000 - firstCount) / (double)firstCount;
            Assert.That(ratio, Is.InRange(2.7d, 3.3d));
        }

        /// <summary>Karıştırma deterministik bir permütasyondur ve count dışını değiştirmez.</summary>
        [Test]
        public void ShuffleIsDeterministicPermutation()
        {
            int[] original = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            int[] first = (int[])original.Clone();
            int[] second = (int[])original.Clone();
            new RunRandom(42).Shuffle(first, first.Length);
            new RunRandom(42).Shuffle(second, second.Length);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EquivalentTo(original));
            int[] partial = (int[])original.Clone();
            new RunRandom(42).Shuffle(partial, 5);
            Assert.That(new[] { partial[0], partial[1], partial[2], partial[3], partial[4] },
                Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
            for (int i = 5; i < partial.Length; i++)
                Assert.That(partial[i], Is.EqualTo(original[i]));
        }
    }
}
