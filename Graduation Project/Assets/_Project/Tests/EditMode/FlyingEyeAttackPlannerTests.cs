using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Pure, deterministic coverage of the Flying Eye's attack cadence.</summary>
    public sealed class FlyingEyeAttackPlannerTests
    {
        /// <summary>Two lights separate every permitted heavy.</summary>
        [Test]
        public void TwoLightsBeforeHeavy_RepeatsLightLightHeavy()
        {
            var planner = new FlyingEyeAttackPlanner(2);
            FlyingEyeAttack[] expected =
            {
                FlyingEyeAttack.Light, FlyingEyeAttack.Light, FlyingEyeAttack.Heavy,
                FlyingEyeAttack.Light, FlyingEyeAttack.Light, FlyingEyeAttack.Heavy
            };
            for (int i = 0; i < expected.Length; i++) Assert.That(planner.Next(true), Is.EqualTo(expected[i]));
            Assert.That(planner.LightsSinceHeavy, Is.Zero);
        }

        /// <summary>A blocked heavy commits lights and stays due until it is allowed.</summary>
        [Test]
        public void BlockedHeavy_ContinuesLightsAndKeepsWantingHeavy()
        {
            var planner = new FlyingEyeAttackPlanner(2);
            planner.Next(true);
            planner.Next(true);
            Assert.That(planner.WantsHeavy, Is.True);
            Assert.That(planner.WantsHeavy, Is.True);
            Assert.That(planner.LightsSinceHeavy, Is.EqualTo(2));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(planner.Next(false), Is.EqualTo(FlyingEyeAttack.Light));
                Assert.That(planner.WantsHeavy, Is.True);
                Assert.That(planner.LightsSinceHeavy, Is.EqualTo(3 + i));
            }
            Assert.That(planner.Next(true), Is.EqualTo(FlyingEyeAttack.Heavy));
            Assert.That(planner.WantsHeavy, Is.False);
            Assert.That(planner.LightsSinceHeavy, Is.Zero);
        }

        /// <summary>Reset starts a fresh cadence.</summary>
        [Test]
        public void Reset_ClearsCountAndHeavyRequest()
        {
            var planner = new FlyingEyeAttackPlanner(2);
            planner.Next(false);
            planner.Next(false);
            planner.Reset();
            Assert.That(planner.LightsSinceHeavy, Is.Zero);
            Assert.That(planner.WantsHeavy, Is.False);
            Assert.That(planner.Next(true), Is.EqualTo(FlyingEyeAttack.Light));
        }

        /// <summary>Nonpositive cadence settings still require one light before a heavy.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NonpositiveLightCount_ClampsToOne(int lights)
        {
            var planner = new FlyingEyeAttackPlanner(lights);
            Assert.That(planner.WantsHeavy, Is.False);
            Assert.That(planner.Next(true), Is.EqualTo(FlyingEyeAttack.Light));
            Assert.That(planner.WantsHeavy, Is.True);
            Assert.That(planner.Next(true), Is.EqualTo(FlyingEyeAttack.Heavy));
        }
    }
}
