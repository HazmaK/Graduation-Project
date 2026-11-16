using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Speed Edge boundaries, interpolation and configurable endpoints.</summary>
    public sealed class BladeSpeedEdgeTests
    {
        private const float MinimumSpeed = 8f;
        private const float MaximumSpeed = 13f;
        private const float MaximumBonus = 0.35f;
        private const float Midpoint = (MinimumSpeed + MaximumSpeed) / 2f;
        private const float Tolerance = 0.00001f;

        /// <summary>Stationary, negative and minimum-speed movement grants no bonus.</summary>
        [TestCase(0f)]
        [TestCase(-MinimumSpeed)]
        [TestCase(MinimumSpeed)]
        public void AtOrBelowMinimum_ReturnsOne(float speed)
            => Assert.That(BladeSpeedEdge.Multiplier(speed), Is.EqualTo(1f));

        /// <summary>The cap is inclusive and cannot be exceeded.</summary>
        [TestCase(MaximumSpeed)]
        [TestCase(MaximumSpeed + MinimumSpeed)]
        public void AtOrAboveMaximum_ReturnsCappedBonus(float speed)
            => Assert.That(BladeSpeedEdge.Multiplier(speed), Is.EqualTo(1f + MaximumBonus).Within(Tolerance));

        /// <summary>Halfway speed grants exactly half of the configured maximum bonus.</summary>
        [Test]
        public void Midpoint_InterpolatesLinearly()
            => Assert.That(BladeSpeedEdge.Multiplier(Midpoint), Is.EqualTo(1f + MaximumBonus / 2f).Within(Tolerance));

        /// <summary>Zero bonus disables damage amplification.</summary>
        [Test]
        public void ZeroBonus_DisablesSpeedEdge()
            => Assert.That(BladeSpeedEdge.Multiplier(MaximumSpeed, MinimumSpeed, MaximumSpeed, 0f), Is.EqualTo(1f));

        /// <summary>Caller-provided endpoints determine interpolation.</summary>
        [Test]
        public void CustomTuning_UsesSuppliedEndpoints()
        {
            float bonus = MaximumBonus / 2f;
            Assert.That(BladeSpeedEdge.Multiplier(MinimumSpeed / 2f, 0f, MinimumSpeed, bonus),
                Is.EqualTo(1f + bonus / 2f).Within(Tolerance));
        }
    }
}
