using NUnit.Framework;
using RetroFPS.Run.Araf;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Bounded awakening progression contracts.</summary>
    public sealed class ArafAwakeningTests
    {
        private ArafTuning _tuning;

        /// <summary>Creates configured temporary tuning.</summary>
        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<ArafTuning>();
            _tuning.Configure(25f, 3, 3, null);
        }

        /// <summary>Destroys the temporary tuning asset.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        /// <summary>Raises exactly once per level, stopping at the configured maximum.</summary>
        [Test]
        public void RaisesUntilMaximumAndThenStaysThere()
        {
            var awakening = new ArafAwakening(_tuning.MaxAwakening);
            Assert.That(awakening.Level, Is.Zero);
            Assert.That(awakening.MaxLevel, Is.EqualTo(3));
            for (int i = 1; i <= awakening.MaxLevel; i++)
            {
                Assert.That(awakening.Raise(), Is.True);
                Assert.That(awakening.Level, Is.EqualTo(i));
            }
            Assert.That(awakening.Raise(), Is.False);
            Assert.That(awakening.Raise(), Is.False);
            Assert.That(awakening.Level, Is.EqualTo(3));
        }

        /// <summary>A nonpositive maximum cannot raise awakening.</summary>
        [TestCase(0)]
        [TestCase(-3)]
        public void NonpositiveMaximumRemainsZero(int maxLevel)
        {
            var awakening = new ArafAwakening(maxLevel);
            Assert.That(awakening.MaxLevel, Is.Zero);
            Assert.That(awakening.Raise(), Is.False);
            Assert.That(awakening.Level, Is.Zero);
        }
    }
}
