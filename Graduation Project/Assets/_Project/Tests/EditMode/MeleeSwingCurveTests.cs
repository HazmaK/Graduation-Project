using NUnit.Framework;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Verifies the melee swing curve reaches each key pose at its phase boundary.</summary>
    public sealed class MeleeSwingCurveTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Vector3 Rest = new Vector3(0.25f, -0.28f, 0.45f);
        private static readonly Quaternion RestRot = Quaternion.Euler(-40f, -10f, 0f);
        private static readonly Vector3 From = new Vector3(0.3f, -0.3f, 0.4f);
        private static readonly Quaternion FromRot = Quaternion.Euler(-30f, 5f, 3f);

        private static SwingProfile Profile() => new SwingProfile(
            new SwingKey(new Vector3(0.1f, 0.1f, -0.1f), new Vector3(1f, 0.4f, 0.1f)),
            new SwingKey(new Vector3(-0.1f, 0f, 0.1f), Vector3.forward),
            new SwingKey(new Vector3(-0.3f, -0.1f, 0f), new Vector3(-1f, -0.4f, 0.2f)),
            0.2f, 0.3f, 0.1f);

        private static void Eval(float t, out Vector3 pos, out Quaternion rot)
        {
            SwingProfile p = Profile();
            MeleeSwingCurve.Evaluate(in p, t, From, FromRot, Rest, RestRot, out pos, out rot);
        }

        private static void AssertPose(Vector3 expectedPos, Quaternion expectedRot, Vector3 pos, Quaternion rot)
        {
            Assert.That(Vector3.Distance(expectedPos, pos), Is.LessThan(Tolerance));
            Assert.That(Quaternion.Angle(expectedRot, rot), Is.LessThan(0.1f));
        }

        /// <summary>The swing starts exactly at the pose it was triggered from, so it never snaps.</summary>
        [Test]
        public void EvaluateAtZero_ReturnsTriggerPose()
        {
            Eval(0f, out Vector3 pos, out Quaternion rot);
            AssertPose(From, FromRot, pos, rot);
        }

        /// <summary>The wind-up ends on the chamber pose.</summary>
        [Test]
        public void EvaluateAtWindupEnd_ReturnsChamberPose()
        {
            SwingProfile p = Profile();
            Eval(0.2f, out Vector3 pos, out Quaternion rot);
            AssertPose(Rest + p.Chamber.Position, p.ChamberRotation, pos, rot);
        }

        /// <summary>The strike path passes through the contact pose halfway.</summary>
        [Test]
        public void PathAtHalf_ReturnsContactPose()
        {
            SwingProfile p = Profile();
            MeleeSwingCurve.EvaluatePath(in p, 0.5f, Rest, out Vector3 pos, out Quaternion rot);
            AssertPose(Rest + p.Contact.Position, p.ContactRotation, pos, rot);
        }

        /// <summary>The follow-through pose is held between the strike and the recovery.</summary>
        [Test]
        public void EvaluateDuringHold_ReturnsFollowPose()
        {
            SwingProfile p = Profile();
            Eval(0.55f, out Vector3 pos, out Quaternion rot);
            AssertPose(Rest + p.Follow.Position, p.FollowRotation, pos, rot);
        }

        /// <summary>The swing ends back at the rest pose.</summary>
        [Test]
        public void EvaluateAtOne_ReturnsRestPose()
        {
            Eval(1f, out Vector3 pos, out Quaternion rot);
            AssertPose(Rest, RestRot, pos, rot);
        }

        /// <summary>The blade points along its direction and its cutting edge (local +X) leads the motion.</summary>
        [Test]
        public void BladeRotation_PointsBladeAndTurnsEdgeIntoMotion()
        {
            // Arrange
            Vector3 direction = new Vector3(-0.7f, 0.2f, 0.68f).normalized;
            Vector3 motion = new Vector3(-0.3f, -0.9f, -0.2f);

            // Act
            Quaternion rot = MeleeSwingCurve.BladeRotation(direction, motion);

            // Assert
            Vector3 edgeExpected = Vector3.ProjectOnPlane(motion, direction).normalized;
            Assert.That(Vector3.Angle(direction, rot * Vector3.forward), Is.LessThan(0.1f));
            Assert.That(Vector3.Angle(edgeExpected, rot * Vector3.right), Is.LessThan(0.1f));
        }
    }
}
