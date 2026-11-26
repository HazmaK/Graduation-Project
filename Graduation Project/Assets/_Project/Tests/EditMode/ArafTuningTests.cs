using NUnit.Framework;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Cards;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Threat tuning defaults, boundaries and editor configuration contracts.</summary>
    public sealed class ArafTuningTests
    {
        /// <summary>Atmosphere defaults and boundaries match the shared builder API.</summary>
        [Test]
        public void AtmosphereDefaultsMatchContract()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                float[] starts = { 20f, 16f, 12f, 8f };
                float[] ends = { 140f, 115f, 90f, 70f };
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(tuning.GetFogStart(i), Is.EqualTo(starts[i]));
                    Assert.That(tuning.GetFogEnd(i), Is.EqualTo(ends[i]));
                }
                foreach (int level in new[] { -1, 4, int.MinValue, int.MaxValue })
                {
                    Assert.That(tuning.GetFogStart(level), Is.EqualTo(8f));
                    Assert.That(tuning.GetFogEnd(level), Is.EqualTo(70f));
                }
                Assert.That(tuning.FogColor, Is.EqualTo(new Color(0.10f, 0.05f, 0.16f)));
                Assert.That(tuning.DebrisCount, Is.EqualTo(40));
                Assert.That(tuning.DebrisMargin, Is.EqualTo(25f));
                Assert.That(tuning.DebrisShellThickness, Is.EqualTo(60f));
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        /// <summary>Configuration copies both fog arrays and leaves other tuning intact.</summary>
        [Test]
        public void AtmosphereConfigurationCopiesArraysAndHandlesEmptySettings()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                var starts = new[] { 21f, 9f }; var ends = new[] { 150f, 75f };
                tuning.ConfigureAtmosphere(starts, ends, Color.blue, 12, 30f, 50f);
                starts[0] = ends[0] = 999f;
                Assert.That(tuning.GetFogStart(0), Is.EqualTo(21f));
                Assert.That(tuning.GetFogEnd(0), Is.EqualTo(150f));
                Assert.That(tuning.GetFogStart(-1), Is.EqualTo(9f));
                Assert.That(tuning.GetFogEnd(10), Is.EqualTo(75f));
                Assert.That(tuning.FogColor, Is.EqualTo(Color.blue));
                Assert.That(tuning.DebrisCount, Is.EqualTo(12));
                Assert.That(tuning.DebrisMargin, Is.EqualTo(30f));
                Assert.That(tuning.DebrisShellThickness, Is.EqualTo(50f));
                Assert.That(tuning.WoundMaxHealth, Is.EqualTo(25f));
                Assert.That(tuning.MaxAlivePatrols, Is.EqualTo(12));
                Assert.That(tuning.MaxAliveIslandEnemies, Is.EqualTo(10));
                tuning.ConfigureAtmosphere(null, null, Color.black, 0, 0f, 0f);
                Assert.That(tuning.GetFogStart(0), Is.Zero);
                Assert.That(tuning.GetFogEnd(-1), Is.Zero);
                tuning.ConfigureAtmosphere(new float[0], new float[0], Color.black, 0, 0f, 0f);
                Assert.That(tuning.GetFogStart(int.MaxValue), Is.Zero);
                Assert.That(tuning.GetFogEnd(0), Is.Zero);
            }
            finally { Object.DestroyImmediate(tuning); }
        }
        /// <summary>Island defaults and out-of-range additions match the shared builder API.</summary>
        [Test]
        public void IslandDefaultsAndOutOfRangeLevelsMatchContract()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                int[] eyes = { 0, 2, 2, 3 };
                int[] penitents = { 0, 0, 1, 1 };
                for (int level = 0; level < 4; level++)
                {
                    Assert.That(tuning.GetIslandEyesAddedAtLevel(level), Is.EqualTo(eyes[level]));
                    Assert.That(tuning.GetIslandPenitentsAddedAtLevel(level), Is.EqualTo(penitents[level]));
                }
                Assert.That(tuning.MaxAliveIslandEnemies, Is.EqualTo(10));
                Assert.That(tuning.EyeHoverHeight, Is.EqualTo(3f));
                foreach (int level in new[] { -1, 4, int.MinValue, int.MaxValue })
                {
                    Assert.That(tuning.GetIslandEyesAddedAtLevel(level), Is.Zero);
                    Assert.That(tuning.GetIslandPenitentsAddedAtLevel(level), Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        /// <summary>Island configuration copies arrays and preserves the pre-existing threat settings.</summary>
        [Test]
        public void ConfigureIslandThreatCopiesArraysAndPreservesOtherSettings()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                tuning.ConfigureThreat(new[] { 7 }, 20, new[] { 6 }, 9f);
                var eyes = new[] { 0, 4 }; var penitents = new[] { 0, 2 };
                tuning.ConfigureIslandThreat(eyes, penitents, 15, 5f);
                eyes[1] = penitents[1] = 99;
                Assert.That(tuning.GetIslandEyesAddedAtLevel(1), Is.EqualTo(4));
                Assert.That(tuning.GetIslandPenitentsAddedAtLevel(1), Is.EqualTo(2));
                Assert.That(tuning.MaxAliveIslandEnemies, Is.EqualTo(15));
                Assert.That(tuning.EyeHoverHeight, Is.EqualTo(5f));
                Assert.That(tuning.GetPatrolsAddedAtLevel(0), Is.EqualTo(7));
                Assert.That(tuning.GetWaveSize(0), Is.EqualTo(6));
                Assert.That(tuning.MaxAlivePatrols, Is.EqualTo(20));
                Assert.That(tuning.MinSpawnDistance, Is.EqualTo(9f));
                tuning.ConfigureIslandThreat(null, null, 0, 0f);
                Assert.That(tuning.GetIslandEyesAddedAtLevel(0), Is.Zero);
                Assert.That(tuning.GetIslandPenitentsAddedAtLevel(0), Is.Zero);
                tuning.ConfigureIslandThreat(new int[0], new int[0], 0, 0f);
                Assert.That(tuning.GetIslandEyesAddedAtLevel(-1), Is.Zero);
                Assert.That(tuning.GetIslandPenitentsAddedAtLevel(-1), Is.Zero);
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void ThreatDefaultsAndOutOfRangeLevelsMatchContract()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                int[] patrols = { 2, 2, 3, 3 };
                int[] waves = { 0, 3, 4, 5 };
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(tuning.GetPatrolsAddedAtLevel(i), Is.EqualTo(patrols[i]));
                    Assert.That(tuning.GetWaveSize(i), Is.EqualTo(waves[i]));
                }
                Assert.That(tuning.MaxAlivePatrols, Is.EqualTo(12));
                Assert.That(tuning.MinSpawnDistance, Is.EqualTo(14f));
                foreach (int level in new[] { -1, 4, int.MinValue, int.MaxValue })
                {
                    Assert.That(tuning.GetPatrolsAddedAtLevel(level), Is.Zero);
                    Assert.That(tuning.GetWaveSize(level), Is.EqualTo(5));
                }
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void ConfigureThreatCopiesArraysAndPreservesExistingConfiguration()
        {
            var tuning = ScriptableObject.CreateInstance<ArafTuning>();
            try
            {
                tuning.Configure(30f, 2, 4, new[] { CardRarityMultipliers.One });
                int[] patrols = { 7, 8 };
                int[] waves = { 1, 6 };
                tuning.ConfigureThreat(patrols, 20, waves, 9f);
                patrols[0] = waves[1] = 99;
                Assert.That(tuning.GetPatrolsAddedAtLevel(0), Is.EqualTo(7));
                Assert.That(tuning.GetWaveSize(1), Is.EqualTo(6));
                Assert.That(tuning.MaxAlivePatrols, Is.EqualTo(20));
                Assert.That(tuning.MinSpawnDistance, Is.EqualTo(9f));
                Assert.That(tuning.WoundMaxHealth, Is.EqualTo(30f));
                Assert.That(tuning.SealCount, Is.EqualTo(2));
                Assert.That(tuning.MaxAwakening, Is.EqualTo(4));
                tuning.ConfigureThreat(null, 0, null, 0f);
                Assert.That(tuning.GetPatrolsAddedAtLevel(0), Is.Zero);
                Assert.That(tuning.GetWaveSize(0), Is.Zero);
                tuning.ConfigureThreat(new int[0], 0, new int[0], 0f);
                Assert.That(tuning.GetPatrolsAddedAtLevel(-1), Is.Zero);
                Assert.That(tuning.GetWaveSize(-1), Is.Zero);
            }
            finally { Object.DestroyImmediate(tuning); }
        }
    }
}
