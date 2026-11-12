using System;
using UnityEngine;

namespace RetroFPS.Settings
{
    /// <summary>Scene-independent access to the settings service.</summary>
    public static class GameSettings
    {
        private static ISettingsService service;
        private static event Action<GameSettingsData> changed;

        /// <summary>Initialized service for menu requests.</summary>
        public static ISettingsService Service
        {
            get { EnsureInitialized(); return service; }
        }

        /// <summary>Current live settings snapshot.</summary>
        public static GameSettingsData Current => Service.Current;

        /// <summary>Raised whenever the service changes live settings.</summary>
        public static event Action<GameSettingsData> Changed
        {
            add { EnsureInitialized(); changed += value; }
            remove { changed -= value; }
        }

        /// <summary>Loads and applies preferences once; called by the single display boot entry point.</summary>
        public static void EnsureInitialized()
        {
            if (service != null) return;
            ResolutionOption desktop = ScreenDisplayApplier.GetDesktopResolution();
            // Only enumerate supported modes at boot when a saved size needs validation.
            ResolutionOption[] options = PlayerPrefs.HasKey("resolution")
                ? ScreenDisplayApplier.GetResolutionOptions() : null;
            service = new SettingsService(new PlayerPrefsSettingsStore(), new ScreenDisplayApplier(),
                new ListenerVolumeApplier(), GameSettingsData.Defaults(desktop.Width, desktop.Height), options);
            service.Changed += ForwardChanged;
        }

        /// <summary>Flushes committed preferences before settings close or application quit.</summary>
        public static void Save() => Service.Save();

        /// <summary>Injects an engine-free service for tests; null clears the override and listeners.</summary>
        public static void SetServiceForTests(ISettingsService replacement)
        {
            if (service != null) service.Changed -= ForwardChanged;
            service = replacement;
            if (service != null) service.Changed += ForwardChanged;
            else changed = null;
        }

        private static void ForwardChanged(GameSettingsData data) => changed?.Invoke(data);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SetServiceForTests(null);
        }
    }
}
