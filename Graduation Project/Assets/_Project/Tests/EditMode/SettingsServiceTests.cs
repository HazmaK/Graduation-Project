using System;
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
    }
}
