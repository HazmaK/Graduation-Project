using System.Globalization;
using UnityEngine;

namespace RetroFPS.Settings
{
    /// <summary>Unity system locale boundary for first-launch defaults.</summary>
    public static class UnityLanguageDefaults
    {
        /// <summary>Turkish alone maps to Turkish; every other value maps to English.</summary>
        public static LanguageSetting FromSystemLanguage(SystemLanguage language) =>
            LanguageDefaults.FromSystemLanguage(language == SystemLanguage.Turkish);
    }

    /// <summary>PlayerPrefs persistence using the exact UX keys.</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private sealed class UnityPreferences : ISettingsPreferences
        {
            /// <inheritdoc/>
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            /// <inheritdoc/>
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            /// <inheritdoc/>
            public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
            /// <inheritdoc/>
            public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
            /// <inheritdoc/>
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            /// <inheritdoc/>
            public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
            /// <inheritdoc/>
            public void Save() => PlayerPrefs.Save();
        }

        private readonly ISettingsPreferences preferences;
        private string cachedResolution;
        private int cachedWidth;
        private int cachedHeight;
        private bool resolutionWritten;
        /// <summary>Creates the Unity PlayerPrefs adapter.</summary>
        public PlayerPrefsSettingsStore() : this(new UnityPreferences()) { }

        /// <summary>Creates a preference store with an injectable typed backend.</summary>
        public PlayerPrefsSettingsStore(ISettingsPreferences preferences)
        {
            this.preferences = preferences ?? throw new System.ArgumentNullException(nameof(preferences));
        }

        /// <inheritdoc/>
        public GameSettingsData Load(GameSettingsData defaults)
        {
            int width = defaults.ResolutionWidth, height = defaults.ResolutionHeight;
            string resolution = preferences.GetString("resolution", string.Empty);
            int separator = resolution.IndexOf('x');
            if (separator > 0 &&
                int.TryParse(resolution.Substring(0, separator), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int savedWidth) &&
                int.TryParse(resolution.Substring(separator + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int savedHeight) && savedWidth > 0 && savedHeight > 0)
            {
                width = savedWidth;
                height = savedHeight;
                resolutionWritten = true;
            }
            else resolutionWritten = false;
            cachedWidth = width;
            cachedHeight = height;
            cachedResolution = resolutionWritten ? resolution
                : width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            int motion = preferences.GetInt("reduceMotion", defaults.ReduceMotion ? 1 : 0);
            int postProcessing = preferences.GetInt("postProcessing", defaults.PostProcessing ? 1 : 0);
            return new GameSettingsData(
                preferences.GetFloat("masterVolume", defaults.MasterVolume),
                preferences.GetFloat("musicVolume", defaults.MusicVolume),
                preferences.GetFloat("sfxVolume", defaults.SfxVolume),
                preferences.GetFloat("uiVolume", defaults.UiVolume),
                preferences.GetFloat("mouseSensitivity", defaults.MouseSensitivity),
                (WindowModeSetting)preferences.GetInt("windowMode", (int)defaults.WindowMode),
                width, height, motion == 0 ? false : motion == 1 ? true : defaults.ReduceMotion,
                LanguageDefaults.FromCode(preferences.GetString("language", string.Empty), defaults.Language),
                postProcessing == 0 ? false : postProcessing == 1 ? true : defaults.PostProcessing,
                preferences.GetFloat("brightness", defaults.Brightness),
                preferences.GetFloat("screenEffects", defaults.ScreenEffects));
        }

        /// <inheritdoc/>
        public void Write(GameSettingsData data)
        {
            preferences.SetFloat("masterVolume", data.MasterVolume);
            preferences.SetFloat("musicVolume", data.MusicVolume);
            preferences.SetFloat("sfxVolume", data.SfxVolume);
            preferences.SetFloat("uiVolume", data.UiVolume);
            preferences.SetFloat("mouseSensitivity", data.MouseSensitivity);
            preferences.SetInt("windowMode", (int)data.WindowMode);
            if (cachedResolution == null || cachedWidth != data.ResolutionWidth || cachedHeight != data.ResolutionHeight)
            {
                cachedWidth = data.ResolutionWidth;
                cachedHeight = data.ResolutionHeight;
                cachedResolution = cachedWidth.ToString(CultureInfo.InvariantCulture)
                    + "x" + cachedHeight.ToString(CultureInfo.InvariantCulture);
                resolutionWritten = false;
            }
            if (!resolutionWritten)
            {
                preferences.SetString("resolution", cachedResolution);
                resolutionWritten = true;
            }
            preferences.SetInt("reduceMotion", data.ReduceMotion ? 1 : 0);
            preferences.SetInt("postProcessing", data.PostProcessing ? 1 : 0);
            preferences.SetFloat("brightness", data.Brightness);
            preferences.SetFloat("screenEffects", data.ScreenEffects);
            preferences.SetString("language", LanguageDefaults.ToCode(data.Language));
        }

        /// <inheritdoc/>
        public void Save() => preferences.Save();
    }

    /// <summary>Unity display application and supported-size discovery.</summary>
    public sealed class ScreenDisplayApplier : IDisplayApplier
    {
        /// <inheritdoc/>
        public void Apply(WindowModeSetting mode, int width, int height)
        {
            FullScreenMode unityMode = mode == WindowModeSetting.Fullscreen ? FullScreenMode.ExclusiveFullScreen
                : mode == WindowModeSetting.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, unityMode);
        }

        /// <summary>Reads the desktop size without relying on the game window's current dimensions.</summary>
        public static ResolutionOption GetDesktopResolution()
        {
            Resolution desktop = Screen.currentResolution;
            if (desktop.width > 0 && desktop.height > 0)
                return new ResolutionOption(desktop.width, desktop.height);
            DisplayInfo info = Screen.mainWindowDisplayInfo;
            return info.width > 0 && info.height > 0
                ? new ResolutionOption(info.width, info.height) : new ResolutionOption(1920, 1080);
        }

        /// <summary>Reads and allocates supported sizes on demand, outside the per-frame path.</summary>
        public static ResolutionOption[] GetResolutionOptions()
        {
            Resolution[] supported = Screen.resolutions;
            var sizes = new ResolutionOption[supported.Length];
            for (int i = 0; i < supported.Length; i++)
                sizes[i] = new ResolutionOption(supported[i].width, supported[i].height);
            return ResolutionOptions.Build(sizes, GetDesktopResolution());
        }
    }

    /// <summary>Applies master volume globally; individual sources consume the remaining gains.</summary>
    public sealed class ListenerVolumeApplier : IAudioVolumeApplier
    {
        /// <inheritdoc/>
        public void Apply(GameSettingsData data) => AudioListener.volume = data.MasterVolume;
    }
}
