using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Island threat placement boundaries, distribution and deterministic selection.</summary>
    public sealed class ArafIslandThreatPlannerTests
    {
        /// <summary>Generated worlds preserve distance, endpoint, spread and distinctness constraints.</summary>
        [Test]
        public void GeneratedWorldsRespectDistanceEndpointsSpreadAndDistinctness()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                ArafWorldLayout world = ArafWorldGenerator.Generate(new ArafWorldSettings(), new RunRandom(seed));
                var chains = new int[32];
                var platforms = new int[32];
                Vector3 avoid = world.GetChain(0).GetPlatform(1).Center;
                const float distance = 10f;
                int candidates = 0;
                for (int c = 0; c < world.ChainCount; c++)
                for (int p = 1; p < world.GetChain(c).PlatformCount - 1; p++)
                    if ((world.GetChain(c).GetPlatform(p).Center - avoid).sqrMagnitude >= distance * distance)
                        candidates++;
                int picked = ArafIslandThreatPlanner.PickPlatforms(world, 32, avoid, distance,
                    new RunRandom(seed), chains, platforms);
                Assert.That(picked, Is.EqualTo(32));
                for (int i = 0; i < picked; i++)
                {
                    ArafIslandChain chain = world.GetChain(chains[i]);
                    Assert.That(platforms[i], Is.InRange(1, chain.PlatformCount - 2));
                    Assert.That((chain.GetPlatform(platforms[i]).Center - avoid).sqrMagnitude,
                        Is.GreaterThanOrEqualTo(distance * distance));
                    if (i < candidates)
                        for (int j = 0; j < i; j++)
                            Assert.That(chains[i] == chains[j] && platforms[i] == platforms[j], Is.False);
                }
                picked = ArafIslandThreatPlanner.PickPlatforms(world, world.ChainCount, Vector3.zero, 0f,
                    new RunRandom(seed), chains, platforms);
                Assert.That(picked, Is.EqualTo(world.ChainCount));
                for (int i = 0; i < picked; i++)
                for (int j = 0; j < i; j++) Assert.That(chains[i], Is.Not.EqualTo(chains[j]));
            }
        }

        /// <summary>Identical random streams produce identical results and consume identical randomness.</summary>
        [Test]
        public void SelectionIsDeterministic()
        {
            var world = ArafWorldGenerator.Generate(new ArafWorldSettings(), new RunRandom(10));
            var left = new RunRandom(42);
            var right = new RunRandom(42);
            var lc = new int[30]; var lp = new int[30];
            var rc = new int[30]; var rp = new int[30];
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 30, Vector3.zero, 14f, left, lc, lp),
                Is.EqualTo(ArafIslandThreatPlanner.PickPlatforms(world, 30, Vector3.zero, 14f, right, rc, rp)));
            Assert.That(rc, Is.EqualTo(lc));
            Assert.That(rp, Is.EqualTo(lp));
            Assert.That(right.NextUInt(), Is.EqualTo(left.NextUInt()));
        }

        /// <summary>Zero requests, missing inputs, no candidates and mismatched buffers are bounded.</summary>
        [Test]
        public void EmptyRequestsAndBufferBoundsAreSafe()
        {
            var world = ArafWorldGenerator.Generate(new ArafWorldSettings(), new RunRandom(10));
            var rng = new RunRandom(42);
            var chains = new[] { -1, -1, -1 };
            var platforms = new[] { -1, -1 };
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 0, Vector3.zero, 0f, rng, chains, platforms), Is.Zero);
            Assert.That(chains[0], Is.EqualTo(-1));
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 100, Vector3.zero, 0f, rng, chains, platforms), Is.EqualTo(2));
            Assert.That(chains[2], Is.EqualTo(-1));
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 100, Vector3.zero, 0f, rng, platforms, chains), Is.EqualTo(2));
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 1, Vector3.zero, 10000f, rng, chains, platforms), Is.Zero);
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 1, Vector3.zero, 0f, rng, new int[0], platforms), Is.Zero);
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(null, 1, Vector3.zero, 0f, rng, chains, platforms), Is.Zero);
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 1, Vector3.zero, 0f, null, chains, platforms), Is.Zero);
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 1, Vector3.zero, 0f, rng, null, platforms), Is.Zero);
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, 1, Vector3.zero, 0f, rng, chains, null), Is.Zero);
        }

        /// <summary>Chains with one or two platforms retain their endpoints as candidates.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public void ShortChainsRetainPlatforms(int platformCount)
        {
            var defaults = new ArafWorldSettings();
            var settings = new ArafWorldSettings(defaults.Hub, defaults.Wing,
                wingHeightOffsets: new float[0], platformsMin: platformCount, platformsMax: platformCount);
            var world = ArafWorldGenerator.Generate(settings, new RunRandom(10));
            int distinct = platformCount * world.ChainCount;
            var chains = new int[distinct + 1]; var platforms = new int[distinct + 1];
            Assert.That(ArafIslandThreatPlanner.PickPlatforms(world, chains.Length, Vector3.zero, 0f,
                new RunRandom(42), chains, platforms), Is.EqualTo(chains.Length));
            for (int i = 0; i < distinct; i++)
            {
                Assert.That(platforms[i], Is.InRange(0, platformCount - 1));
                for (int j = 0; j < i; j++)
                    Assert.That(chains[i] == chains[j] && platforms[i] == platforms[j], Is.False);
            }
        }
    }
}
