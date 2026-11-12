using System;
using NUnit.Framework;
using RetroFPS.Settings;

namespace RetroFPS.Tests
{
    /// <summary>Pure sensitivity mapping tests.</summary>
    public sealed class SensitivityMappingTests
    {
        /// <summary>Default bounds retain exact endpoints and neutral midpoint.</summary>
        [TestCase(0f, 0.25f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 4f)]
        public void MapsEndpointsAndMidpoint(float normalized, float expected)
        {
            Assert.That(SensitivityMapping.ToScale(normalized), Is.EqualTo(expected).Within(0.00001f));
        }

        /// <summary>Inspector bounds need not be reciprocal to preserve the exact neutral midpoint.</summary>
        [Test]
        public void CustomBoundsPreserveNeutralAndAreMonotonic()
        {
            Assert.That(SensitivityMapping.ToScale(0f, 0.1f, 3f), Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(SensitivityMapping.ToScale(0.5f, 0.1f, 3f), Is.EqualTo(1f));
            Assert.That(SensitivityMapping.ToScale(1f, 0.1f, 3f), Is.EqualTo(3f).Within(0.00001f));
            float previous = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float current = SensitivityMapping.ToScale(i / 100f, 0.1f, 3f);
                Assert.That(current, Is.GreaterThan(previous));
                previous = current;
            }
        }

        /// <summary>Each half uses exponential spacing.</summary>
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.75f, 2f)]
        public void ExponentialSpacing(float normalized, float expected)
        {
            Assert.That(SensitivityMapping.ToScale(normalized), Is.EqualTo(expected).Within(0.00001f));
        }

        /// <summary>Invalid normalized values are clamped or fall back to neutral.</summary>
        [Test]
        public void ClampsInputAndDefaultsNonFiniteValues()
        {
            Assert.That(SensitivityMapping.ToScale(-1f), Is.EqualTo(0.25f));
            Assert.That(SensitivityMapping.ToScale(2f), Is.EqualTo(4f));
            Assert.That(SensitivityMapping.ToScale(float.NaN), Is.EqualTo(1f));
            Assert.That(SensitivityMapping.ToScale(float.PositiveInfinity), Is.EqualTo(1f));
        }

        /// <summary>Invalid tuning bounds are rejected at configuration time.</summary>
        [TestCase(0f, 4f)]
        [TestCase(2f, 4f)]
        [TestCase(0.25f, 0.5f)]
        public void RejectsInvalidBounds(float min, float max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SensitivityMapping.ToScale(0.5f, min, max));
        }
    }
}
