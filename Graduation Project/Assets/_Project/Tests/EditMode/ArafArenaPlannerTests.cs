using System;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Saf arena planlamasının rollerini, dalgalarını ve doğuş sınırlarını doğrular.</summary>
    public sealed class ArafArenaPlannerTests
    {
        private readonly int[] _optional = { 2, 2, 3, 3 };
        private readonly int[] _seal = { 3, 3, 3, 3 };
        private readonly int[] _ghouls = { 3, 3, 4, 4 };
        private readonly int[] _eyes = { 0, 1, 1, 2 };
        private readonly int[] _penitents = { 0, 0, 1, 1 };
        private const int SealExtra = 1;
        private const int AdvanceThreshold = 1;
        private const int Capacity = 12;
        private const float MinimumDistance = 4f;

        private ArafArenaWaveSettings Settings()
            => new ArafArenaWaveSettings(_optional, _seal, _ghouls, _eyes, _penitents,
                SealExtra, AdvanceThreshold, 0.6f, MinimumDistance, 0.5f, Capacity);

        /// <summary>Merkezde arena olmaz; her kanatta mühür ile ayrı isteğe bağlı yuvalar seçilir.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(99)]
        public void AssignRolesRespectsHubSealAndWingCounts(int optionalPerWing)
        {
            int[] offsets = { 0, 2, 6, 9 };
            int[] counts = { 2, 4, 3, 1 };
            bool[] hubs = { true, false, false, false };
            bool[] seals = { true, false, false, true, false, false, true, false, false, true };
            for (ulong seed = 0; seed < 64; seed++)
            {
                var roles = new ArafRoomRole[seals.Length];
                ArafArenaPlanner.AssignRoles(offsets, counts, hubs, seals, optionalPerWing, new RunRandom(seed), roles);
                for (int section = 0; section < offsets.Length; section++)
                {
                    int optionalCount = 0;
                    for (int i = offsets[section]; i < offsets[section] + counts[section]; i++)
                    {
                        if (hubs[section]) Assert.That(roles[i], Is.EqualTo(ArafRoomRole.Calm));
                        else if (seals[i]) Assert.That(roles[i], Is.EqualTo(ArafRoomRole.SealArena));
                        else if (roles[i] == ArafRoomRole.OptionalArena) optionalCount++;
                        else Assert.That(roles[i], Is.EqualTo(ArafRoomRole.Calm));
                    }
                    Assert.That(optionalCount, Is.EqualTo(hubs[section] ? 0 : Math.Min(optionalPerWing, counts[section] - 1)));
                }
            }
        }

        /// <summary>Aynı tohum aynı rolleri üretir; yeniden kullanım eski sonuçları temizler.</summary>
        [Test]
        public void AssignRolesIsDeterministicAndClearsPreviousOutput()
        {
            int[] offsets = { 0, 1 };
            int[] counts = { 1, 4 };
            bool[] hubs = { true, false };
            bool[] seals = { false, false, true, false, false };
            var first = new ArafRoomRole[seals.Length];
            var second = new ArafRoomRole[seals.Length];
            for (ulong seed = 0; seed < 64; seed++)
            {
                ArafArenaPlanner.AssignRoles(offsets, counts, hubs, seals, 1, new RunRandom(seed), first);
                ArafArenaPlanner.AssignRoles(offsets, counts, hubs, seals, 1, new RunRandom(seed), second);
                Assert.That(second, Is.EqualTo(first));
                Assert.That(first[2], Is.EqualTo(ArafRoomRole.SealArena));
            }
            ArafArenaPlanner.AssignRoles(offsets, counts, hubs, seals, -1, new RunRandom(1), first);
            for (int i = 0; i < first.Length; i++)
                Assert.That(first[i], Is.EqualTo(seals[i] ? ArafRoomRole.SealArena : ArafRoomRole.Calm));
        }

        /// <summary>Eksik, kısa veya yanlış toplamlı diziler açık hata verir.</summary>
        [Test]
        public void AssignRolesRejectsInvalidBuffers()
        {
            var rng = new RunRandom(1);
            var output = new ArafRoomRole[1];
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(null, new[] { 1 }, new[] { false }, new[] { true }, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, null, new[] { false }, new[] { true }, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 1 }, null, new[] { true }, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 1 }, new[] { false }, null, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 1 }, new[] { false }, new[] { true }, 1, rng, null));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0, 1 }, new[] { 1 }, new[] { false, false }, new[] { true }, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 1 }, Array.Empty<bool>(), new[] { true }, 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 1 }, new[] { false }, Array.Empty<bool>(), 1, rng, output));
            Assert.Throws<ArgumentException>(() => ArafArenaPlanner.AssignRoles(new[] { 0 }, new[] { 2 }, new[] { false }, new[] { true, false }, 1, rng, output));
        }

        /// <summary>Her iki rolün sayıları uyanış tablosundan gelir; yalnızca son dalgada kefaretçi ve mühür eliti vardır.</summary>
        [TestCase(ArafRoomRole.OptionalArena)]
        [TestCase(ArafRoomRole.SealArena)]
        public void BuildWavesMatchesSettingsAndClampsAwakening(ArafRoomRole role)
        {
            var settings = Settings();
            var output = new ArafArenaWave[settings.MaxWaveCount];
            int[] levels = { -1, 0, 1, 2, 3, int.MaxValue };
            foreach (int level in levels)
            {
                int a = Mathf.Clamp(level, 0, _optional.Length - 1);
                int count = ArafArenaPlanner.BuildWaves(role, level, settings, output);
                Assert.That(count, Is.EqualTo(role == ArafRoomRole.SealArena ? _seal[a] : _optional[a]));
                for (int i = 0; i < count; i++)
                {
                    bool final = i == count - 1;
                    bool elite = final && role == ArafRoomRole.SealArena;
                    Assert.That(output[i].Ghouls, Is.EqualTo(_ghouls[a]));
                    Assert.That(output[i].Eyes, Is.EqualTo(_eyes[a]));
                    Assert.That(output[i].Penitents, Is.EqualTo(final ? _penitents[a] + (elite ? SealExtra : 0) : 0));
                    Assert.That(output[i].HasElite, Is.EqualTo(elite));
                }
            }
        }

        /// <summary>Sakin odalarda dalga yoktur; kısa tamponun son dalgası son dalga kurallarını alır.</summary>
        [Test]
        public void BuildWavesHandlesCalmEmptyAndShortBuffers()
        {
            var settings = Settings();
            Assert.That(ArafArenaPlanner.BuildWaves(ArafRoomRole.Calm, 0, settings, new ArafArenaWave[settings.MaxWaveCount]), Is.Zero);
            Assert.That(ArafArenaPlanner.BuildWaves(ArafRoomRole.SealArena, 0, settings, Array.Empty<ArafArenaWave>()), Is.Zero);
            var one = new ArafArenaWave[1];
            Assert.That(ArafArenaPlanner.BuildWaves(ArafRoomRole.SealArena, 0, settings, one), Is.EqualTo(one.Length));
            Assert.That(one[0].HasElite, Is.True);
            Assert.That(one[0].Penitents, Is.EqualTo(_penitents[0] + SealExtra));
        }

        /// <summary>Ayarlar dış dizileri kopyalar ve her dizinin kendi sınırını kullanır.</summary>
        [Test]
        public void SettingsCopyArraysAndClampEachTableIndependently()
        {
            int[] waves = { 2, 4 };
            int[] eyes = { 7 };
            var settings = new ArafArenaWaveSettings(waves, waves, null, eyes, Array.Empty<int>(),
                SealExtra, AdvanceThreshold, 0.6f, MinimumDistance, 0.5f, Capacity);
            waves[0] = 99;
            eyes[0] = 99;
            Assert.That(settings.OptionalWaves(-1), Is.EqualTo(2));
            Assert.That(settings.SealWaves(int.MaxValue), Is.EqualTo(4));
            Assert.That(settings.Eyes(int.MaxValue), Is.EqualTo(7));
            Assert.That(settings.Ghouls(0), Is.Zero);
            Assert.That(settings.FinalWavePenitents(0), Is.Zero);
            var defaults = new ArafArenaWaveSettings();
            Assert.That(defaults.MaxAlivePerArena, Is.EqualTo(Capacity));
            for (int a = 0; a < _optional.Length; a++)
            {
                Assert.That(defaults.OptionalWaves(a), Is.EqualTo(_optional[a]));
                Assert.That(defaults.SealWaves(a), Is.EqualTo(_seal[a]));
                Assert.That(defaults.Ghouls(a), Is.EqualTo(_ghouls[a]));
                Assert.That(defaults.Eyes(a), Is.EqualTo(_eyes[a]));
                Assert.That(defaults.FinalWavePenitents(a), Is.EqualTo(_penitents[a]));
            }
        }

        /// <summary>Erken geçiş son dalgada kapalıdır; temizlenme bütün canlıları ve son doğuşu bekler.</summary>
        [Test]
        public void AdvanceAndClearRespectBoundaries()
        {
            Assert.That(ArafArenaPlanner.ShouldAdvance(2, 0, 3, 1), Is.False);
            Assert.That(ArafArenaPlanner.ShouldAdvance(1, 0, 3, 1), Is.True);
            Assert.That(ArafArenaPlanner.ShouldAdvance(0, 1, 3, 1), Is.True);
            Assert.That(ArafArenaPlanner.ShouldAdvance(0, 2, 3, 1), Is.False);
            Assert.That(ArafArenaPlanner.ShouldAdvance(0, -1, 3, 1), Is.False);
            Assert.That(ArafArenaPlanner.IsCleared(0, 2, 3), Is.True);
            Assert.That(ArafArenaPlanner.IsCleared(1, 2, 3), Is.False);
            Assert.That(ArafArenaPlanner.IsCleared(0, 1, 3), Is.False);
            Assert.That(ArafArenaPlanner.IsCleared(0, -1, 0), Is.False);
            Assert.That(ArafArenaPlanner.IsCleared(0, 0, 1), Is.True);
        }

        /// <summary>Yükseklik mesafeyi etkilemez; uygun noktalar karıştırılıp döngüsel kullanılır.</summary>
        [Test]
        public void PickSpawnPointsRespectsHorizontalDistanceAndReusesRoundRobin()
        {
            Vector3[] points = { new Vector3(0f, 100f, 0f), Vector3.right * MinimumDistance, Vector3.forward * (MinimumDistance + 1f) };
            var output = new int[Capacity];
            int count = ArafArenaPlanner.PickSpawnPoints(points, points.Length, Vector3.zero, MinimumDistance, output.Length, new RunRandom(1), output);
            Assert.That(count, Is.EqualTo(output.Length));
            Assert.That(output[0], Is.Not.EqualTo(output[1]));
            for (int i = 0; i < count; i++)
            {
                Assert.That(output[i], Is.Not.Zero);
                Vector3 point = points[output[i]];
                Assert.That(point.x * point.x + point.z * point.z, Is.GreaterThanOrEqualTo(MinimumDistance * MinimumDistance));
                Assert.That(output[i], Is.EqualTo(output[i % 2]));
            }
        }

        /// <summary>Uygun nokta yoksa yükseklikten bağımsız en uzak noktalar seçilir.</summary>
        [Test]
        public void PickSpawnPointsFallsBackToFarthestAndHonorsCandidateCount()
        {
            Vector3[] points = { new Vector3(1f, 99f, 0f), Vector3.right * 2f, Vector3.left * 2f, Vector3.right * 100f };
            var output = new int[Capacity];
            int count = ArafArenaPlanner.PickSpawnPoints(points, points.Length - 1, Vector3.zero, MinimumDistance, Capacity, new RunRandom(1), output);
            Assert.That(count, Is.EqualTo(Capacity));
            Assert.That(output[0], Is.Not.EqualTo(output[1]));
            for (int i = 0; i < count; i++) Assert.That(output[i], Is.InRange(1, 2));
            Assert.That(ArafArenaPlanner.PickSpawnPoints(points, 1, Vector3.zero, MinimumDistance, Capacity, new RunRandom(1), output), Is.EqualTo(Capacity));
            for (int i = 0; i < output.Length; i++) Assert.That(output[i], Is.Zero);
        }

        /// <summary>Aynı tohum aynı seçimi verir; kısa tamponda benzersizlik korunur.</summary>
        [Test]
        public void PickSpawnPointsIsDeterministicAndCapsOutput()
        {
            var points = new Vector3[Capacity];
            for (int i = 0; i < points.Length; i++) points[i] = Vector3.right * (MinimumDistance + i);
            var first = new int[Capacity / 2];
            var second = new int[first.Length];
            for (ulong seed = 0; seed < 64; seed++)
            {
                Assert.That(ArafArenaPlanner.PickSpawnPoints(points, points.Length, Vector3.zero, MinimumDistance, Capacity, new RunRandom(seed), first), Is.EqualTo(first.Length));
                ArafArenaPlanner.PickSpawnPoints(points, points.Length, Vector3.zero, MinimumDistance, Capacity, new RunRandom(seed), second);
                Assert.That(second, Is.EqualTo(first));
                for (int i = 0; i < first.Length; i++)
                    for (int j = 0; j < i; j++) Assert.That(first[i], Is.Not.EqualTo(first[j]));
            }
        }

        /// <summary>Boş aday veya çıktı dizileri hiçbir indeks yazmaz.</summary>
        [Test]
        public void PickSpawnPointsHandlesEmptyInputs()
        {
            var rng = new RunRandom(1);
            var output = new int[Capacity];
            Assert.That(ArafArenaPlanner.PickSpawnPoints(Array.Empty<Vector3>(), 0, Vector3.zero, MinimumDistance, Capacity, rng, output), Is.Zero);
            Assert.That(ArafArenaPlanner.PickSpawnPoints(null, 0, Vector3.zero, MinimumDistance, Capacity, rng, output), Is.Zero);
            Assert.That(ArafArenaPlanner.PickSpawnPoints(new[] { Vector3.zero }, 0, Vector3.zero, MinimumDistance, Capacity, rng, output), Is.Zero);
            Assert.That(ArafArenaPlanner.PickSpawnPoints(new[] { Vector3.zero }, 1, Vector3.zero, MinimumDistance, Capacity, rng, Array.Empty<int>()), Is.Zero);
        }
    }
}
