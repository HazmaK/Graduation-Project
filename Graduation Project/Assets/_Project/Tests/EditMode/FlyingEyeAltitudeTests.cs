using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Floor acceptance and void-crossing timer coverage without physics.</summary>
    public sealed class FlyingEyeAltitudeTests
    {
        [TestCase(12f)]
        [TestCase(10f)]
        [TestCase(9f)]
        [TestCase(7.5f)]
        public void NearbyOrHigherGround_IsAcceptedImmediatelyAndClearsTimer(float hitY)
        {
            float timer = 1f;
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, hitY, 2.5f, 2f, 0.1f, ref timer), Is.EqualTo(hitY));
            Assert.That(timer, Is.Zero);
        }

        [Test]
        public void DeepGround_IsHeldUntilPersistenceThreshold_ThenAccepted()
        {
            float timer = 0f;
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 2f, 2.5f, 2f, 0.75f, ref timer), Is.EqualTo(10f));
            Assert.That(timer, Is.EqualTo(0.75f));
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 2f, 2.5f, 2f, 0.75f, ref timer), Is.EqualTo(10f));
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 2f, 2.5f, 2f, 0.5f, ref timer), Is.EqualTo(2f));
            Assert.That(timer, Is.Zero);
        }

        [Test]
        public void MissingGround_KeepsLastFloorAndRestartsPersistence()
        {
            float timer = 1.9f;
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, false, -100f, 2.5f, 2f, 5f, ref timer), Is.EqualTo(10f));
            Assert.That(timer, Is.Zero);
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 0f, 2.5f, 2f, 0.2f, ref timer), Is.EqualTo(10f));
            Assert.That(timer, Is.EqualTo(0.2f));
        }

        [Test]
        public void SolidGround_InterruptsPreviousDeepGroundTimer()
        {
            float timer = 1.9f;
            FlyingEyeAltitude.ResolveGround(10f, true, 10f, 2.5f, 2f, 0.1f, ref timer);
            Assert.That(timer, Is.Zero);
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 0f, 2.5f, 2f, 0.1f, ref timer), Is.EqualTo(10f));
        }

        [Test]
        public void ZeroDelay_AcceptsDeepGroundImmediately_AndNegativeTimeDoesNotAdvanceTimer()
        {
            float timer = 0f;
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 0f, 2.5f, 0f, 0f, ref timer), Is.Zero);
            Assert.That(timer, Is.Zero);
            Assert.That(FlyingEyeAltitude.ResolveGround(10f, true, 0f, 2.5f, 2f, -1f, ref timer), Is.EqualTo(10f));
            Assert.That(timer, Is.Zero);
        }
    }
}
