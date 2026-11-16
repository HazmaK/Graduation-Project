using NUnit.Framework;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Horizontal shield arc and degenerate hit direction coverage.</summary>
    public sealed class PenitentGuardTests
    {
        private const float HalfAngle = 65f;
        private const float OutsideArcOffset = 0.1f;
        private const float HitDistance = 2f;
        private const float NearCentreDistance = 0.04f;
        private const float VerticalOffset = 1000f;
        private const float FrontAngle = 0f;
        private const float SideAngle = 90f;
        private const float BackAngle = 180f;

        /// <summary>Hits on either inclusive arc boundary and in front are blocked.</summary>
        [TestCase(FrontAngle)]
        [TestCase(HalfAngle)]
        [TestCase(-HalfAngle)]
        public void RaisedGuard_BlocksFrontAndInclusiveBoundaries(float angle)
        {
            Vector3 position = Vector3.up * VerticalOffset;
            Vector3 direction = Direction(angle);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, position,
                position + direction * HitDistance, Vector3.zero, HalfAngle, true), Is.True);
        }

        /// <summary>Hits immediately outside the arc, at the side or behind bypass the shield.</summary>
        [TestCase(HalfAngle + OutsideArcOffset)]
        [TestCase(-HalfAngle - OutsideArcOffset)]
        [TestCase(SideAngle)]
        [TestCase(BackAngle)]
        public void RaisedGuard_DoesNotBlockOutsideArc(float angle)
        {
            Vector3 direction = Direction(angle);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero,
                direction * HitDistance, Vector3.zero, HalfAngle, true), Is.False);
        }

        /// <summary>A lowered shield never blocks, regardless of angle or normal fallback.</summary>
        [TestCase(FrontAngle)]
        [TestCase(HalfAngle)]
        [TestCase(BackAngle)]
        public void LoweredGuard_NeverBlocks(float angle)
        {
            Vector3 direction = Direction(angle);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero,
                direction * HitDistance, -Vector3.forward, HalfAngle, false), Is.False);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero,
                Vector3.zero, -Vector3.forward, HalfAngle, false), Is.False);
        }

        /// <summary>Near-centre XZ hits use the attacker-facing normal, including vertically offset hits.</summary>
        [Test]
        public void CentrePoint_UsesAttackerFacingNormal()
        {
            Vector3 position = Vector3.up * VerticalOffset;
            Vector3 point = position + Vector3.back * NearCentreDistance + Vector3.up * HitDistance;
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, position, point,
                Vector3.forward, HalfAngle, true), Is.True);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, position, point,
                Vector3.back, HalfAngle, true), Is.False);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, position, position,
                Vector3.forward, HalfAngle, true), Is.True);
        }

        /// <summary>Melee blocks depend only on the horizontal attacker direction, including arc boundaries.</summary>
        [TestCase(FrontAngle, true)]
        [TestCase(HalfAngle, true)]
        [TestCase(-HalfAngle, true)]
        [TestCase(HalfAngle + OutsideArcOffset, false)]
        [TestCase(BackAngle, false)]
        public void DirectionOnly_UsesAttackerArc(float angle, bool expected)
        {
            Vector3 attacker = Direction(angle) + Vector3.up * VerticalOffset;
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, attacker, HalfAngle, true), Is.EqualTo(expected));
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, attacker, HalfAngle, false), Is.False);
        }

        /// <summary>A frontal melee hit on a rear collider blocks; a rear melee hit on a front collider does not.</summary>
        [Test]
        public void DirectionOnly_DoesNotUseTouchedColliderPosition()
        {
            Vector3 rearPoint = Vector3.back * HitDistance;
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero, rearPoint,
                Vector3.forward, HalfAngle, true), Is.False, "Ranged rear point bypasses the guard.");
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.forward, HalfAngle, true), Is.True,
                "Frontal melee still blocks when the touched collider is behind the centre.");
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.back, HalfAngle, true), Is.False);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero, Vector3.forward * HitDistance,
                Vector3.back, HalfAngle, true), Is.True, "Ranged hits continue to use the point.");
        }

        /// <summary>Height and forward pitch do not change the horizontal guard decision.</summary>
        [Test]
        public void Guard_FlattensForwardAndHitDirection()
        {
            Vector3 forward = Vector3.forward + Vector3.up * HitDistance;
            Vector3 point = Vector3.forward * HitDistance + Vector3.down * VerticalOffset;
            Assert.That(PenitentGuard.IsBlocked(forward, Vector3.zero, point,
                Vector3.forward, HalfAngle, true), Is.True);
        }

        /// <summary>Without a usable horizontal forward or hit direction there is no block.</summary>
        [Test]
        public void DegenerateDirection_DoesNotBlock()
        {
            Assert.That(PenitentGuard.IsBlocked(Vector3.up, Vector3.zero, Vector3.forward,
                Vector3.zero, HalfAngle, true), Is.False);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.zero, Vector3.zero,
                Vector3.up, HalfAngle, true), Is.False);
            Assert.That(PenitentGuard.IsBlocked(Vector3.forward, Vector3.up, HalfAngle, true), Is.False);
        }

        private static Vector3 Direction(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}
