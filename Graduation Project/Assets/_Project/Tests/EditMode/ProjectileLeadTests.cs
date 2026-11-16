using NUnit.Framework;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic interception and prediction bounds without scene or clock dependencies.</summary>
    public sealed class ProjectileLeadTests
    {
        /// <summary>A stationary target needs no lead.</summary>
        [Test]
        public void StationaryTarget_ReturnsDirectAim()
        {
            Vector3 origin = new Vector3(2f, 3f, 4f);
            Vector3 target = new Vector3(12f, 5f, 14f);
            AssertDirection(ProjectileLead.Direction(origin, target, Vector3.zero, 10f, 1f, 5f),
                (target - origin).normalized);
        }

        /// <summary>Zero lead ignores target velocity.</summary>
        [Test]
        public void ZeroLead_ReturnsDirectAim()
        {
            Vector3 target = new Vector3(0f, 0f, 10f);
            AssertDirection(ProjectileLead.Direction(Vector3.zero, target, Vector3.right * 3f, 10f, 0f, 5f),
                Vector3.forward);
        }

        /// <summary>Full lead intercepts a perpendicular mover within five centimetres.</summary>
        [Test]
        public void FullLead_InterceptsPerpendicularMover()
        {
            Vector3 target = new Vector3(0f, 0f, 10f);
            Vector3 velocity = Vector3.right * 3f;
            Vector3 direction = ProjectileLead.Direction(Vector3.zero, target, velocity, 10f, 1f, 5f);
            float closest = float.PositiveInfinity;
            const float step = 0.001f;
            for (int i = 0; i <= 2000; i++)
            {
                float time = i * step;
                closest = Mathf.Min(closest, Vector3.Distance(direction * (10f * time), target + velocity * time));
            }
            Assert.That(closest, Is.LessThan(0.05f));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.00001f));
        }

        /// <summary>Partial prediction lies strictly between direct and full intercept aim.</summary>
        [Test]
        public void HalfLead_LiesBetweenDirectAndFull()
        {
            Vector3 target = Vector3.forward * 10f;
            Vector3 velocity = Vector3.right * 3f;
            Vector3 full = ProjectileLead.Direction(Vector3.zero, target, velocity, 10f, 1f, 5f);
            Vector3 half = ProjectileLead.Direction(Vector3.zero, target, velocity, 10f, 0.5f, 5f);
            float fullAngle = Vector3.Angle(Vector3.forward, full);
            float halfAngle = Vector3.Angle(Vector3.forward, half);
            Assert.That(halfAngle, Is.GreaterThan(0f).And.LessThan(fullAngle));
            float interceptTime = Mathf.Sqrt(100f / 91f);
            AssertDirection(half, (target + velocity * (interceptTime * 0.5f)).normalized);
        }

        /// <summary>The prediction horizon is clamped before applying partial lead.</summary>
        [TestCase(1f)]
        [TestCase(0.5f)]
        public void Prediction_ClampsToMaximumSeconds(float leadFactor)
        {
            Vector3 target = Vector3.forward * 100f;
            Vector3 velocity = Vector3.right * 3f;
            Vector3 result = ProjectileLead.Direction(Vector3.zero, target, velocity, 10f, leadFactor, 0.25f);
            AssertDirection(result, (target + velocity * (0.25f * leadFactor)).normalized);
        }

        /// <summary>Zero speed and unreachable targets fall back without producing NaNs.</summary>
        [TestCase(0f, 3f)]
        [TestCase(10f, 20f)]
        [TestCase(10f, 10f)]
        public void NoIntercept_ReturnsFiniteDirectAim(float speed, float perpendicularSpeed)
        {
            Vector3 result = ProjectileLead.Direction(Vector3.zero, Vector3.forward * 10f,
                Vector3.right * perpendicularSpeed, speed, 1f, 10f);
            AssertDirection(result, Vector3.forward);
            Assert.That(float.IsNaN(result.x) || float.IsNaN(result.y) || float.IsNaN(result.z), Is.False);
        }

        /// <summary>Equal-speed approach uses the linear intercept solution.</summary>
        [Test]
        public void EqualSpeedApproachingTarget_UsesLinearSolution()
        {
            Vector3 result = ProjectileLead.Direction(Vector3.zero, Vector3.forward * 10f,
                Vector3.back * 10f, 10f, 1f, 10f);
            AssertDirection(result, Vector3.forward);
        }

        /// <summary>Coincident targets have a finite normalized fallback.</summary>
        [Test]
        public void CoincidentTarget_ReturnsFiniteNormalizedDirection()
        {
            AssertDirection(ProjectileLead.Direction(Vector3.zero, Vector3.zero, Vector3.zero, 10f, 1f, 5f),
                Vector3.forward);
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
        }
    }
}
