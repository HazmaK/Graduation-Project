using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Difficulty setting persistence, token composition and preset data.</summary>
    public sealed class DifficultyTests
    {
        private sealed class FakeStore : ISettingsStore
        {
            internal GameSettingsData? Stored;
            public GameSettingsData Load(GameSettingsData defaults) => Stored ?? defaults;
            public void Write(GameSettingsData data) => Stored = data;
            public void Save() { }
        }

        private sealed class FakePreferences : ISettingsPreferences
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public float GetFloat(string key, float fallback) => values.TryGetValue(key, out object v) ? (float)v : fallback;
            public int GetInt(string key, int fallback) => values.TryGetValue(key, out object v) ? (int)v : fallback;
            public string GetString(string key, string fallback) => values.TryGetValue(key, out object v) ? (string)v : fallback;
            public void SetFloat(string key, float value) => values[key] = value;
            public void SetInt(string key, int value) => values[key] = value;
            public void SetString(string key, string value) => values[key] = value;
            public void Save() { }
        }

        private sealed class NullDisplay : IDisplayApplier { public void Apply(WindowModeSetting m, int w, int h) { } }
        private sealed class NullAudio : IAudioVolumeApplier { public void Apply(GameSettingsData d) { } }

        private static readonly GameSettingsData Defaults = GameSettingsData.Defaults(1920, 1080);

        [SetUp]
        public void SetUp() => ResetStatics();

        [TearDown]
        public void TearDown() => ResetStatics();

        private static void ResetStatics()
        {
            GameDifficulty.ResetForTests();
            typeof(EnemyAttackTokens).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        [Test]
        public void Defaults_AreNormal()
        {
            Assert.That(Defaults.Difficulty, Is.EqualTo(DifficultySetting.Normal));
        }

        [Test]
        public void SetDifficulty_PersistsAndRaisesChanged_OtherSettersPreserveIt()
        {
            var store = new FakeStore();
            var service = new SettingsService(store, new NullDisplay(), new NullAudio(), Defaults);
            int raised = 0;
            service.Changed += _ => raised++;

            service.SetDifficulty(DifficultySetting.Hard);
            Assert.That(store.Stored.Value.Difficulty, Is.EqualTo(DifficultySetting.Hard));
            Assert.That(raised, Is.EqualTo(1));

            service.SetVolume("masterVolume", 0.3f);
            service.SetSensitivity(0.2f);
            service.SetReduceMotion(true);
            service.SetBrightness(0.5f);
            service.SetLanguage(LanguageSetting.Turkish);
            service.RequestDisplay(WindowModeSetting.Windowed, 1280, 720);
            service.ConfirmDisplay();
            Assert.That(service.Current.Difficulty, Is.EqualTo(DifficultySetting.Hard));
            Assert.That(store.Stored.Value.Difficulty, Is.EqualTo(DifficultySetting.Hard));
        }

        [Test]
        public void SetDifficulty_SameValue_DoesNotRaise()
        {
            var service = new SettingsService(new FakeStore(), new NullDisplay(), new NullAudio(), Defaults);
            int raised = 0;
            service.Changed += _ => raised++;
            service.SetDifficulty(DifficultySetting.Normal);
            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void SetDifficulty_InvalidValue_Throws()
        {
            var service = new SettingsService(new FakeStore(), new NullDisplay(), new NullAudio(), Defaults);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => service.SetDifficulty((DifficultySetting)7));
        }

        [Test]
        public void PlayerPrefsStore_RoundTripsDifficulty_AndRepairsInvalid()
        {
            var preferences = new FakePreferences();
            var store = new PlayerPrefsSettingsStore(preferences);
            store.Write(new GameSettingsData(1f, 0.7f, 1f, 0.8f, 0.5f, WindowModeSetting.Borderless,
                1920, 1080, false, LanguageSetting.English, true, 0f, 1f, DifficultySetting.Easy));
            Assert.That(new PlayerPrefsSettingsStore(preferences).Load(Defaults).Difficulty, Is.EqualTo(DifficultySetting.Easy));

            preferences.SetInt("difficulty", 42);
            var service = new SettingsService(new PlayerPrefsSettingsStore(preferences), new NullDisplay(), new NullAudio(), Defaults);
            Assert.That(service.Current.Difficulty, Is.EqualTo(DifficultySetting.Normal));
        }

        [Test]
        public void DefaultModifiers_BehaveAsIdentity()
        {
            DifficultyModifiers none = default;
            Assert.That(none.PlayerDamageTaken, Is.EqualTo(1f));
            Assert.That(none.EnemyHealth, Is.EqualTo(1f));
            Assert.That(none.EnemySpeed, Is.EqualTo(1f));
            Assert.That(none.EnemyAttackCooldown, Is.EqualTo(1f));
            Assert.That(none.WaveSpawnInterval, Is.EqualTo(1f));
            Assert.That(none.MeleeTokenOffset, Is.Zero);
        }

        [Test]
        public void TokenOffset_ComposesWithDefaultAndProfileLimits()
        {
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Melee, 0), Is.EqualTo(2));
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Melee, 3), Is.EqualTo(3));

            GameDifficulty.SetForTests(new DifficultyModifiers(1f, 1f, 1f, 1f, 1, 0, -1, 1f), DifficultySetting.Hard);
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Melee, 0), Is.EqualTo(3));
            // Araf hunt profile limit 3 stays a base, difficulty adds on top.
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Melee, 3), Is.EqualTo(4));
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Slam, 0), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.RangedHeavy, 0), Is.EqualTo(1));

            for (int i = 0; i < 3; i++) Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 0), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 0), Is.False);
        }

        [Test]
        public void TokenOffset_NeverDropsCapacityBelowOne()
        {
            GameDifficulty.SetForTests(new DifficultyModifiers(1f, 1f, 1f, 1f, -5, -5, -5, 1f), DifficultySetting.Easy);
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Melee, 0), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.CapacityOf(AttackTokenChannel.Slam, 1), Is.EqualTo(1));
        }

        [Test]
        public void ResetForTests_RestoresIdentityAndClearsOffsets()
        {
            GameDifficulty.SetForTests(new DifficultyModifiers(2f, 2f, 2f, 2f, 1, 1, 1, 2f), DifficultySetting.Hard);
            GameDifficulty.ResetForTests();
            Assert.That(GameDifficulty.Current.EnemyHealth, Is.EqualTo(1f));
            Assert.That(EnemyAttackTokens.LimitOffsetOf(AttackTokenChannel.Melee), Is.Zero);
        }

        [Test]
        public void PresetTable_NormalIsIdentity_EasyAndHardPullOppositeWays()
        {
            var table = Resources.Load<DifficultyPresetTable>(DifficultyPresetTable.ResourcesPath);
            Assert.That(table, Is.Not.Null, "Resources/" + DifficultyPresetTable.ResourcesPath + " is missing.");

            DifficultyModifiers normal = table.Get(DifficultySetting.Normal);
            Assert.That(normal.PlayerDamageTaken, Is.EqualTo(1f));
            Assert.That(normal.EnemyHealth, Is.EqualTo(1f));
            Assert.That(normal.EnemySpeed, Is.EqualTo(1f));
            Assert.That(normal.EnemyAttackCooldown, Is.EqualTo(1f));
            Assert.That(normal.WaveSpawnInterval, Is.EqualTo(1f));
            Assert.That(normal.MeleeTokenOffset, Is.Zero);
            Assert.That(normal.SlamTokenOffset, Is.Zero);
            Assert.That(normal.RangedHeavyTokenOffset, Is.Zero);

            DifficultyModifiers easy = table.Get(DifficultySetting.Easy);
            DifficultyModifiers hard = table.Get(DifficultySetting.Hard);
            Assert.That(easy.PlayerDamageTaken, Is.LessThan(1f));
            Assert.That(hard.PlayerDamageTaken, Is.GreaterThan(1f));
            Assert.That(easy.EnemyHealth, Is.LessThan(1f));
            Assert.That(hard.EnemyHealth, Is.GreaterThan(1f));
            Assert.That(easy.EnemyAttackCooldown, Is.GreaterThan(1f));
            Assert.That(hard.EnemyAttackCooldown, Is.LessThan(1f));
            Assert.That(easy.WaveSpawnInterval, Is.GreaterThan(1f));
            Assert.That(hard.WaveSpawnInterval, Is.LessThan(1f));
        }
    }
}
