using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Settings;

namespace RetroFPS.Tests
{
    /// <summary>Settings behavior using in-memory boundaries only.</summary>
    public sealed class SettingsServiceTests
    {
        private sealed class FakeStore : ISettingsStore
        {
            internal GameSettingsData? Stored;
            internal int Writes;
            internal int Saves;
            public GameSettingsData Load(GameSettingsData defaults) => Stored ?? defaults;
            public void Write(GameSettingsData data) { Stored = data; Writes++; }
            public void Save() { Saves++; }
        }

        private sealed class FakePreferences : ISettingsPreferences
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public float GetFloat(string key, float fallback) => values.TryGetValue(key, out object value) ? (float)value : fallback;
            public int GetInt(string key, int fallback) => values.TryGetValue(key, out object value) ? (int)value : fallback;
            public string GetString(string key, string fallback) => values.TryGetValue(key, out object value) ? (string)value : fallback;
            public void SetFloat(string key, float value) => values[key] = value;
            public void SetInt(string key, int value) => values[key] = value;
            public void SetString(string key, string value) => values[key] = value;
            public void Save() { }
        }

        private sealed class FakeDisplay : IDisplayApplier
        {
            internal WindowModeSetting Mode;
            internal int Width;
            internal int Height;
            internal int Calls;
            public void Apply(WindowModeSetting mode, int width, int height)
            { Mode = mode; Width = width; Height = height; Calls++; }
        }

        private sealed class FakeAudio : IAudioVolumeApplier
        {
            internal GameSettingsData Applied;
            internal int Calls;
            public void Apply(GameSettingsData data) { Applied = data; Calls++; }
        }

        private FakeStore store;
        private FakeDisplay display;
        private FakeAudio audio;
        private GameSettingsData defaults;

        /// <summary>Creates fresh fake dependencies.</summary>
        [SetUp]
        public void SetUp()
        {
            store = new FakeStore();
            display = new FakeDisplay();
            audio = new FakeAudio();
            defaults = GameSettingsData.Defaults(2560, 1440);
        }

        private SettingsService Create() => new SettingsService(store, display, audio, defaults);

        /// <summary>Each new preference persists and publishes its live snapshot exactly once per request.</summary>
        [Test]
        public void PostFxSettersPersistNotifyAndRoundTrip()
        {
            SettingsService service = Create();
            int events = 0;
            GameSettingsData notified = default;
            service.Changed += data => { events++; notified = data; };
            service.SetPostProcessing(false);
            Assert.That(notified.PostProcessing, Is.False);
            service.SetBrightness(-0.6f);
            Assert.That(notified.Brightness, Is.EqualTo(-0.6f));
            service.SetScreenEffects(0.4f);
            Assert.That(notified.ScreenEffects, Is.EqualTo(0.4f));
            Assert.That(events, Is.EqualTo(3));
            Assert.That(store.Writes, Is.EqualTo(3));
            Assert.That(store.Saves, Is.Zero);
            Assert.That(display.Calls, Is.EqualTo(1));
            Assert.That(audio.Calls, Is.EqualTo(1));
            service.Save();
            GameSettingsData reloaded = Create().Current;
            Assert.That(reloaded.PostProcessing, Is.False);
            Assert.That(reloaded.Brightness, Is.EqualTo(-0.6f));
            Assert.That(reloaded.ScreenEffects, Is.EqualTo(0.4f));
            Assert.That(store.Saves, Is.EqualTo(1));
        }

        /// <summary>Both load validation and setters clamp finite values and repair non-finite values.</summary>
        [TestCase(-2f, -1f, 0f)]
        [TestCase(2f, 1f, 1f)]
        [TestCase(float.NaN, 0f, 1f)]
        [TestCase(float.PositiveInfinity, 0f, 1f)]
        [TestCase(float.NegativeInfinity, 0f, 1f)]
        public void PostFxClampsAtLoadAndAtSet(float input, float brightness, float effects)
        {
            store.Stored = new GameSettingsData(1, 0.7f, 1, 0.8f, 0.5f,
                WindowModeSetting.Borderless, 2560, 1440, false, LanguageSetting.English, true, input, input);
            SettingsService service = Create();
            Assert.That(service.Current.Brightness, Is.EqualTo(brightness));
            Assert.That(service.Current.ScreenEffects, Is.EqualTo(effects));
            service.SetBrightness(input);
            service.SetScreenEffects(input);
            Assert.That(store.Stored.Value.Brightness, Is.EqualTo(brightness));
            Assert.That(store.Stored.Value.ScreenEffects, Is.EqualTo(effects));
        }

        /// <summary>Existing setters and display commit/revert preserve every new preference.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void PostFxSurvivesEveryCopyAndDisplayTransaction(bool confirm)
        {
            SettingsService service = Create();
            service.SetPostProcessing(false);
            service.SetBrightness(0.6f);
            service.SetScreenEffects(0.3f);
            service.RequestDisplay(WindowModeSetting.Windowed, 1280, 720);
            foreach (string key in new[] { "masterVolume", "musicVolume", "sfxVolume", "uiVolume" })
                service.SetVolume(key, 0.2f);
            service.SetSensitivity(0.8f);
            service.SetReduceMotion(true);
            service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(store.Stored.Value.ResolutionWidth, Is.EqualTo(2560));
            if (confirm) service.ConfirmDisplay(); else service.RevertDisplay();
            service.Save();
            GameSettingsData data = Create().Current;
            Assert.That(data.PostProcessing, Is.False);
            Assert.That(data.Brightness, Is.EqualTo(0.6f));
            Assert.That(data.ScreenEffects, Is.EqualTo(0.3f));
            Assert.That(data.ResolutionWidth, Is.EqualTo(confirm ? 1280 : 2560));
            Assert.That(data.ReduceMotion, Is.True);
        }

        /// <summary>The shared facade publishes each post-processing setting for runtime consumers.</summary>
        [Test]
        public void FacadeForwardsAllPostFxSettings()
        {
            SettingsService service = Create();
            try
            {
                GameSettings.SetServiceForTests(service);
                int events = 0;
                GameSettings.Changed += _ => events++;
                service.SetPostProcessing(false);
                service.SetBrightness(0.5f);
                service.SetScreenEffects(0.2f);
                Assert.That(events, Is.EqualTo(3));
                Assert.That(GameSettings.Current.PostProcessing, Is.False);
                Assert.That(GameSettings.Current.Brightness, Is.EqualTo(0.5f));
                Assert.That(GameSettings.Current.ScreenEffects, Is.EqualTo(0.2f));
            }
            finally { GameSettings.SetServiceForTests(null); }
        }

        /// <summary>Legacy keys use explicit new defaults; typed persistence round-trips exact contract keys.</summary>
        [Test]
        public void PreferenceAdapterMissingKeysAndRoundTripUseContractDefaults()
        {
            var preferences = new FakePreferences();
            preferences.SetFloat("musicVolume", 0.2f);
            preferences.SetInt("reduceMotion", 1);
            var adapter = new PlayerPrefsSettingsStore(preferences);
            GameSettingsData legacy = adapter.Load(defaults);
            Assert.That(legacy.PostProcessing, Is.True);
            Assert.That(legacy.Brightness, Is.Zero);
            Assert.That(legacy.ScreenEffects, Is.EqualTo(1f));
            Assert.That(legacy.MusicVolume, Is.EqualTo(0.2f));
            Assert.That(legacy.ReduceMotion, Is.True);
            var service = new SettingsService(adapter, display, audio, defaults);
            service.SetPostProcessing(false);
            service.SetBrightness(-0.8f);
            service.SetScreenEffects(0.2f);
            service.Save();
            Assert.That(preferences.GetInt("postProcessing", -1), Is.Zero);
            Assert.That(preferences.GetFloat("brightness", 9f), Is.EqualTo(-0.8f));
            Assert.That(preferences.GetFloat("screenEffects", 9f), Is.EqualTo(0.2f));
            GameSettingsData loaded = new PlayerPrefsSettingsStore(preferences).Load(defaults);
            Assert.That(loaded.PostProcessing, Is.False);
            Assert.That(loaded.Brightness, Is.EqualTo(-0.8f));
            Assert.That(loaded.ScreenEffects, Is.EqualTo(0.2f));
            preferences.SetInt("postProcessing", 99);
            Assert.That(adapter.Load(defaults).PostProcessing, Is.True);
        }

        /// <summary>No saved values means borderless at desktop size.</summary>
        [Test]
        public void EmptyStoreAppliesDesktopBorderlessDefaults()
        {
            SettingsService service = Create();
            Assert.That(service.Current.MasterVolume, Is.EqualTo(1f));
            Assert.That(service.Current.MusicVolume, Is.EqualTo(0.7f));
            Assert.That(service.Current.SfxVolume, Is.EqualTo(1f));
            Assert.That(service.Current.UiVolume, Is.EqualTo(0.8f));
            Assert.That(service.Current.MouseSensitivity, Is.EqualTo(0.5f));
            Assert.That(service.Current.ReduceMotion, Is.False);
            Assert.That(service.Current.PostProcessing, Is.True);
            Assert.That(service.Current.Brightness, Is.Zero);
            Assert.That(service.Current.ScreenEffects, Is.EqualTo(1f));
            Assert.That(display.Mode, Is.EqualTo(WindowModeSetting.Borderless));
            Assert.That(display.Width, Is.EqualTo(2560));
            Assert.That(display.Height, Is.EqualTo(1440));
            Assert.That(audio.Applied.MasterVolume, Is.EqualTo(1f));
            Assert.That(service.HasPendingDisplay, Is.False);
            Assert.That(store.Writes, Is.Zero);
        }

        /// <summary>Untrusted preferences cannot introduce invalid application values.</summary>
        [Test]
        public void CorruptValuesAreClampedOrDefaulted()
        {
            store.Stored = new GameSettingsData(-1, 2, float.NaN, float.PositiveInfinity,
                float.NegativeInfinity, (WindowModeSetting)99, -10, 0, true);
            GameSettingsData data = Create().Current;
            Assert.That(data.MasterVolume, Is.Zero);
            Assert.That(data.MusicVolume, Is.EqualTo(1f));
            Assert.That(data.SfxVolume, Is.EqualTo(1f));
            Assert.That(data.UiVolume, Is.EqualTo(0.8f));
            Assert.That(data.MouseSensitivity, Is.EqualTo(0.5f));
            Assert.That(data.WindowMode, Is.EqualTo(WindowModeSetting.Borderless));
            Assert.That(data.ResolutionWidth, Is.EqualTo(2560));
            Assert.That(data.ResolutionHeight, Is.EqualTo(1440));
            Assert.That(data.ReduceMotion, Is.True);
        }

        /// <summary>Each volume request reaches persistence, audio and listeners.</summary>
        [TestCase("masterVolume")]
        [TestCase("musicVolume")]
        [TestCase("sfxVolume")]
        [TestCase("uiVolume")]
        public void VolumeAppliesPersistsAndNotifies(string key)
        {
            SettingsService service = Create();
            int notifications = 0;
            GameSettingsData notified = default;
            service.Changed += data => { notifications++; notified = data; };
            service.SetVolume(key, 0.35f);
            Assert.That(Volume(service.Current, key), Is.EqualTo(0.35f));
            Assert.That(Volume(audio.Applied, key), Is.EqualTo(0.35f));
            Assert.That(Volume(store.Stored.Value, key), Is.EqualTo(0.35f));
            Assert.That(Volume(notified, key), Is.EqualTo(0.35f));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(audio.Calls, Is.EqualTo(2));
            Assert.That(store.Saves, Is.Zero);
        }

        private static float Volume(GameSettingsData data, string key)
        {
            switch (key)
            {
                case "masterVolume": return data.MasterVolume;
                case "musicVolume": return data.MusicVolume;
                case "sfxVolume": return data.SfxVolume;
                default: return data.UiVolume;
            }
        }

        /// <summary>Sensitivity and motion requests persist and notify without changing display.</summary>
        [Test]
        public void SensitivityAndMotionPersistAndNotify()
        {
            SettingsService service = Create();
            int notifications = 0;
            service.Changed += _ => notifications++;
            service.SetSensitivity(2f);
            service.SetReduceMotion(true);
            Assert.That(service.Current.MouseSensitivity, Is.EqualTo(1f));
            Assert.That(store.Stored.Value.MouseSensitivity, Is.EqualTo(1f));
            Assert.That(store.Stored.Value.ReduceMotion, Is.True);
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(display.Calls, Is.EqualTo(1));
            service.Save();
            Assert.That(store.Saves, Is.EqualTo(1));
        }

        /// <summary>Display previews do not reach the store until Keep.</summary>
        [Test]
        public void DisplayIsPendingUntilConfirmed()
        {
            SettingsService service = Create();
            int notifications = 0;
            service.Changed += _ => notifications++;
            service.RequestDisplay(WindowModeSetting.Windowed, 1600, 900);
            Assert.That(service.HasPendingDisplay, Is.True);
            Assert.That(display.Mode, Is.EqualTo(WindowModeSetting.Windowed));
            Assert.That(display.Width, Is.EqualTo(1600));
            Assert.That(display.Height, Is.EqualTo(900));
            Assert.That(store.Writes, Is.Zero);
            service.ConfirmDisplay();
            Assert.That(service.HasPendingDisplay, Is.False);
            Assert.That(store.Stored.Value.WindowMode, Is.EqualTo(WindowModeSetting.Windowed));
            Assert.That(store.Stored.Value.ResolutionWidth, Is.EqualTo(1600));
            Assert.That(store.Stored.Value.ResolutionHeight, Is.EqualTo(900));
            Assert.That(store.Saves, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(2));
        }

        /// <summary>Repeated previews always revert to committed values, not the previous preview.</summary>
        [Test]
        public void RevertRestoresCommittedDisplayAndPreservesOtherChanges()
        {
            SettingsService service = Create();
            service.RequestDisplay(WindowModeSetting.Windowed, 1600, 900);
            service.ConfirmDisplay();
            service.RequestDisplay(WindowModeSetting.Fullscreen, 1920, 1080);
            service.RequestDisplay(WindowModeSetting.Borderless, 3840, 2160);
            service.SetVolume("sfxVolume", 0.2f);
            service.SetReduceMotion(true);
            service.Save();
            Assert.That(store.Stored.Value.ResolutionWidth, Is.EqualTo(1600));
            Assert.That(store.Stored.Value.WindowMode, Is.EqualTo(WindowModeSetting.Windowed));
            // A crash/reload during the preview sees only committed display values.
            SettingsService reloaded = Create();
            Assert.That(reloaded.Current.ResolutionWidth, Is.EqualTo(1600));
            service.RevertDisplay();
            Assert.That(service.HasPendingDisplay, Is.False);
            Assert.That(service.Current.ResolutionWidth, Is.EqualTo(1600));
            Assert.That(service.Current.ResolutionHeight, Is.EqualTo(900));
            Assert.That(display.Mode, Is.EqualTo(WindowModeSetting.Windowed));
            Assert.That(service.Current.SfxVolume, Is.EqualTo(0.2f));
            Assert.That(service.Current.ReduceMotion, Is.True);
        }

        /// <summary>Every request, including an unchanged size or a mode-only change, requires confirmation.</summary>
        [Test]
        public void EveryDisplayRequestBecomesPendingAndRevertNotifies()
        {
            SettingsService service = Create();
            int notifications = 0;
            service.Changed += _ => notifications++;
            service.RequestDisplay(defaults.WindowMode, 2560, 1440);
            Assert.That(service.HasPendingDisplay, Is.True);
            service.RevertDisplay();
            service.RequestDisplay(WindowModeSetting.Fullscreen, 2560, 1440);
            Assert.That(service.HasPendingDisplay, Is.True);
            service.RevertDisplay();
            Assert.That(notifications, Is.EqualTo(4));
            Assert.That(store.Writes, Is.Zero);
            service.ConfirmDisplay();
            service.RevertDisplay();
            Assert.That(notifications, Is.EqualTo(4));
        }

        /// <summary>Unsupported saved sizes resolve deterministically to the nearest available size.</summary>
        [Test]
        public void SavedResolutionUsesNearestSupportedSize()
        {
            store.Stored = new GameSettingsData(1, 0.7f, 1, 0.8f, 0.5f,
                WindowModeSetting.Windowed, 1900, 1000, false);
            var options = new[] { new ResolutionOption(1280, 720), new ResolutionOption(1920, 1080) };
            var service = new SettingsService(store, display, audio, defaults, options);
            Assert.That(service.Current.ResolutionWidth, Is.EqualTo(1920));
            Assert.That(service.Current.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(display.Width, Is.EqualTo(1920));
        }

        /// <summary>Missing preferences retain the exact desktop size even if not enumerated.</summary>
        [Test]
        public void DesktopDefaultIsNotSnappedToSupportedList()
        {
            var service = new SettingsService(store, display, audio, defaults,
                new[] { new ResolutionOption(1920, 1080) });
            Assert.That(service.Current.ResolutionWidth, Is.EqualTo(2560));
            Assert.That(service.Current.ResolutionHeight, Is.EqualTo(1440));
        }

        /// <summary>Invalid requests fail without modifying live values or persistence.</summary>
        [Test]
        public void InvalidRequestsHaveNoSideEffects()
        {
            SettingsService service = Create();
            Assert.Throws<ArgumentException>(() => service.SetVolume("unknown", 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.RequestDisplay((WindowModeSetting)9, 1280, 720));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.RequestDisplay(WindowModeSetting.Windowed, 0, 720));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.RequestDisplay(WindowModeSetting.Windowed, 1280, -1));
            Assert.That(service.HasPendingDisplay, Is.False);
            Assert.That(store.Writes, Is.Zero);
            Assert.That(display.Calls, Is.EqualTo(1));
        }

        /// <summary>The static facade can be used and replaced entirely through fake services.</summary>
        [Test]
        public void FacadeForwardsOnceAndUnhooksReplacedService()
        {
            SettingsService first = Create();
            SettingsService second = Create();
            int notifications = 0;
            Action<GameSettingsData> listener = _ => notifications++;
            try
            {
                GameSettings.SetServiceForTests(first);
                GameSettings.Changed += listener;
                first.SetSensitivity(0.2f);
                GameSettings.SetServiceForTests(second);
                first.SetSensitivity(0.3f);
                second.SetSensitivity(0.4f);
                Assert.That(notifications, Is.EqualTo(2));
                Assert.That(GameSettings.Service, Is.SameAs(second));
                Assert.That(GameSettings.Current.MouseSensitivity, Is.EqualTo(0.4f));
                GameSettings.Save();
                Assert.That(store.Saves, Is.EqualTo(1));
            }
            finally
            {
                GameSettings.Changed -= listener;
                GameSettings.SetServiceForTests(null);
            }
        }
        /// <summary>Language changes once, stages persistence, and leaves display/audio untouched.</summary>
        [Test]
        public void LanguageChangesOnceWithoutApplyingOtherSettings()
        {
            SettingsService service = Create();
            int events = 0;
            service.Changed += data => { events++; Assert.That(data.Language, Is.EqualTo(LanguageSetting.Turkish)); };
            service.SetLanguage(LanguageSetting.Turkish);
            service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(store.Saves, Is.Zero);
            Assert.That(store.Stored.Value.Language, Is.EqualTo(LanguageSetting.Turkish));
            Assert.That(Create().Current.Language, Is.EqualTo(LanguageSetting.Turkish));
            Assert.That(display.Calls, Is.EqualTo(2));
            Assert.That(audio.Calls, Is.EqualTo(2));
            Assert.That(service.Current.MasterVolume, Is.EqualTo(defaults.MasterVolume));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetLanguage((LanguageSetting)99));
            Assert.That(events, Is.EqualTo(1));
        }

        /// <summary>All setters, saves, reload and display outcomes preserve language and preview isolation.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void LanguageSurvivesEverySettingAndDisplayTransaction(bool confirm)
        {
            SettingsService service = Create();
            service.SetLanguage(LanguageSetting.Turkish);
            service.RequestDisplay(WindowModeSetting.Windowed, 1280, 720);
            service.SetLanguage(LanguageSetting.English);
            service.SetLanguage(LanguageSetting.Turkish);
            foreach (string key in new[] { "masterVolume", "musicVolume", "sfxVolume", "uiVolume" })
            {
                service.SetVolume(key, 0.3f);
                Assert.That(service.Current.Language, Is.EqualTo(LanguageSetting.Turkish));
            }
            service.SetSensitivity(0.2f);
            service.SetReduceMotion(true);
            service.Save();
            Assert.That(store.Stored.Value.Language, Is.EqualTo(LanguageSetting.Turkish));
            Assert.That(store.Stored.Value.ResolutionWidth, Is.EqualTo(2560));
            Assert.That(Create().Current.ResolutionWidth, Is.EqualTo(2560));
            if (confirm) service.ConfirmDisplay(); else service.RevertDisplay();
            Assert.That(service.Current.Language, Is.EqualTo(LanguageSetting.Turkish));
            Assert.That(service.Current.ResolutionWidth, Is.EqualTo(confirm ? 1280 : 2560));
            service.Save();
            Assert.That(Create().Current.Language, Is.EqualTo(LanguageSetting.Turkish));
        }

        /// <summary>The facade forwards exactly one language event and exposes the new snapshot immediately.</summary>
        [Test]
        public void FacadeForwardsLanguageOnce()
        {
            var service = Create();
            try
            {
                GameSettings.SetServiceForTests(service);
                int events = 0;
                GameSettings.Changed += _ => events++;
                service.SetLanguage(LanguageSetting.Turkish);
                service.SetLanguage(LanguageSetting.Turkish);
                Assert.That(events, Is.EqualTo(1));
                Assert.That(GameSettings.Current.Language, Is.EqualTo(LanguageSetting.Turkish));
            }
            finally { GameSettings.SetServiceForTests(null); }
        }

    }
}
