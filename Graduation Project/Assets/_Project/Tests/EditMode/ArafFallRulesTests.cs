using NUnit.Framework;
using RetroFPS.Run.Araf;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Nonlethal void fall damage contracts.</summary>
    public sealed class ArafFallRulesTests
    {
        [TestCase(100f, 20f, 20f)]
        [TestCase(15f, 20f, 14f)]
        [TestCase(1f, 20f, 0f)]
        [TestCase(0.5f, 20f, 0f)]
        [TestCase(100f, -20f, 0f)]
        public void FallDamageIsNonnegativeAndLeavesOneHealth(float health, float damage, float expected)
        {
            Assert.That(ArafFallRules.ComputeFallDamage(health, damage), Is.EqualTo(expected));
        }
    }
}
