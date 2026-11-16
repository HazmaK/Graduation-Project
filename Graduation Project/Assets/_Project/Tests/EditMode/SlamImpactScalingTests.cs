using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Height-relative, clamped slam impact scaling without physics or scenes.</summary>
    public sealed class SlamImpactScalingTests
    {
        private const float MinimumHeight = 1f;
        private const float MaximumHeight = 6f;
        private const float MidpointHeight = (MinimumHeight + MaximumHeight) / 2f;
        private const float LightDamage = 45f;
        private const float HeavyDamage = 120f;
        private const float LightRadius = 3f;
        private const float HeavyRadius = 5.5f;
        private const float LightStun = 0.5f;
        private const float HeavyStun = 1.8f;
        private const float WorldOffset = 100f;
        private const float Tolerance = 0.00001f;

        /// <summary>A rise or tiny fall uses the light impact and clamps negative height.</summary>
        [TestCase(-MinimumHeight)]
        [TestCase(0f)]
        [TestCase(MinimumHeight)]
        public void AtOrBelowMinimum_UsesLightImpact(float height)
        {
            var impact = new SlamImpactScaling(height, 0f);
            Assert.That(impact.Height, Is.GreaterThanOrEqualTo(0f));
            Assert.That(impact.T, Is.Zero);
            Assert.That(impact.Damage, Is.EqualTo(LightDamage));
            Assert.That(impact.Radius, Is.EqualTo(LightRadius));
            Assert.That(impact.StunDuration, Is.EqualTo(LightStun));
        }

        /// <summary>The maximum fall and larger falls use identical capped impact values.</summary>
        [TestCase(MaximumHeight)]
        [TestCase(MaximumHeight + MinimumHeight)]
        public void AtOrAboveMaximum_UsesHeavyImpact(float height)
        {
            var impact = new SlamImpactScaling(height, 0f);
            Assert.That(impact.T, Is.EqualTo(1f));
            Assert.That(impact.Damage, Is.EqualTo(HeavyDamage));
            Assert.That(impact.Radius, Is.EqualTo(HeavyRadius));
            Assert.That(impact.StunDuration, Is.EqualTo(HeavyStun).Within(Tolerance));
        }

        /// <summary>All three impact outputs interpolate at the same normalized height.</summary>
        [Test]
        public void Midpoint_InterpolatesDamageRadiusAndStun()
        {
            var impact = new SlamImpactScaling(MidpointHeight, 0f);
            Assert.That(impact.T, Is.EqualTo(0.5f));
            Assert.That(impact.Damage, Is.EqualTo((LightDamage + HeavyDamage) / 2f).Within(Tolerance));
            Assert.That(impact.Radius, Is.EqualTo((LightRadius + HeavyRadius) / 2f).Within(Tolerance));
            Assert.That(impact.StunDuration, Is.EqualTo((LightStun + HeavyStun) / 2f).Within(Tolerance));
        }

        /// <summary>Absolute world elevation does not change a fall's strength.</summary>
        [Test]
        public void WorldHeightOffset_DoesNotChangeImpact()
        {
            var impact = new SlamImpactScaling(WorldOffset + MidpointHeight, WorldOffset);
            Assert.That(impact.Height, Is.EqualTo(MidpointHeight));
            Assert.That(impact.T, Is.EqualTo(0.5f));
        }

        /// <summary>The mapping uses the configured minimum and maximum values.</summary>
        [Test]
        public void CustomEndpoints_UseSuppliedTuning()
        {
            var impact = new SlamImpactScaling(MidpointHeight, 0f, 0f, MidpointHeight,
                0f, LightDamage, 0f, LightRadius, 0f, LightStun);
            Assert.That(impact.Damage, Is.EqualTo(LightDamage));
            Assert.That(impact.Radius, Is.EqualTo(LightRadius));
            Assert.That(impact.StunDuration, Is.EqualTo(LightStun));
        }
    }
}
