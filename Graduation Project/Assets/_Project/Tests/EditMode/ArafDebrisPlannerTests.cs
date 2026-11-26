using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Araf;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Shell geometry, bounded spacing fallback and deterministic buffer contracts.</summary>
    public sealed class ArafDebrisPlannerTests
    {
        /// <summary>Every piece stays in the requested horizontal shell and vertical/scale ranges.</summary>
        [Test]
        public void ShellAndSpacingHoldAcrossSeeds()
        {
            var bounds = new Bounds(new Vector3(17f, 5f, -23f), new Vector3(180f, 12f, 150f));
            var positions = new Vector3[40];
            var rotations = new Quaternion[40];
            var scales = new float[40];
            for (ulong seed = 0; seed < 50; seed++)
            {
                Assert.That(ArafDebrisPlanner.Plan(bounds, 40, 25f, 60f, new RunRandom(seed), positions, rotations, scales), Is.EqualTo(40));
                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 p = positions[i];
                    float dx = Mathf.Max(bounds.min.x - p.x, 0f, p.x - bounds.max.x);
                    float dz = Mathf.Max(bounds.min.z - p.z, 0f, p.z - bounds.max.z);
                    Assert.That(Mathf.Sqrt(dx * dx + dz * dz), Is.InRange(24.999f, 85.001f));
                    Assert.That(p.y, Is.InRange(bounds.min.y - 30f, bounds.max.y + 40f));
                    Assert.That(scales[i], Is.InRange(0.6f, 2.2f));
                    for (int j = 0; j < i; j++)
                        Assert.That((p - positions[j]).sqrMagnitude, Is.GreaterThanOrEqualTo(64f));
                }
            }
        }

        /// <summary>Matching seeds reproduce positions, full rotations and scales exactly.</summary>
        [Test]
        public void SameSeedProducesIdenticalPlan()
        {
            var p1 = new Vector3[40]; var p2 = new Vector3[40];
            var r1 = new Quaternion[40]; var r2 = new Quaternion[40];
            var s1 = new float[40]; var s2 = new float[40];
            var bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            ArafDebrisPlanner.Plan(bounds, 40, 25f, 60f, new RunRandom(42), p1, r1, s1);
            ArafDebrisPlanner.Plan(bounds, 40, 25f, 60f, new RunRandom(42), p2, r2, s2);
            Assert.That(p2, Is.EqualTo(p1));
            Assert.That(r2, Is.EqualTo(r1));
            Assert.That(s2, Is.EqualTo(s1));
        }

        /// <summary>The shortest buffer limits writes; zero/negative counts and missing inputs do not advance RNG.</summary>
        [Test]
        public void BufferBoundsAndEmptyRequestsAreSafe()
        {
            var positions = new Vector3[5];
            var rotations = new Quaternion[2];
            var scales = new float[4];
            positions[2] = Vector3.one * 999f;
            scales[2] = 999f;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var rng = new RunRandom(12);
            var untouched = new RunRandom(12);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 0, 25f, 60f, rng, positions, rotations, scales), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, -1, 25f, 60f, rng, positions, rotations, scales), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 5, 25f, 60f, rng, null, rotations, scales), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 5, 25f, 60f, rng, positions, null, scales), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 5, 25f, 60f, rng, positions, rotations, null), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 5, 25f, 60f, null, positions, rotations, scales), Is.Zero);
            Assert.That(ArafDebrisPlanner.Plan(bounds, 5, 25f, 60f, rng, new Vector3[0], rotations, scales), Is.Zero);
            Assert.That(rng.NextUInt(), Is.EqualTo(untouched.NextUInt()));
            Assert.That(ArafDebrisPlanner.Plan(bounds, int.MaxValue, 25f, 60f, rng, positions, rotations, scales), Is.EqualTo(2));
            Assert.That(positions[2], Is.EqualTo(Vector3.one * 999f));
            Assert.That(scales[2], Is.EqualTo(999f));
        }

        /// <summary>A crowded zero-width shell still terminates and fills buffers after bounded retries.</summary>
        [Test]
        public void CrowdedShellAcceptsFallback()
        {
            var positions = new Vector3[100];
            Assert.That(ArafDebrisPlanner.Plan(new Bounds(Vector3.zero, Vector3.zero), 100, 0f, 0f,
                new RunRandom(7), positions, new Quaternion[100], new float[100]), Is.EqualTo(100));
            bool fallbackUsed = false;
            for (int i = 0; i < positions.Length; i++)
            {
                Assert.That(positions[i].x, Is.Zero);
                Assert.That(positions[i].z, Is.Zero);
                for (int j = 0; j < i; j++)
                    if ((positions[i] - positions[j]).sqrMagnitude < 64f) fallbackUsed = true;
            }
            Assert.That(fallbackUsed, Is.True);
        }
    }
}
