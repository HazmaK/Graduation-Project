using System.Globalization;
using UnityEngine;

namespace RetroFPS.Settings
{
    /// <summary>PlayerPrefs persistence using the exact UX keys.</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private string cachedResolution;
        private int cachedWidth;
        private int cachedHeight;
        private bool resolutionWritten;
        /// <inheritdoc/>
        public GameSettingsData Load(GameSettingsData defaults)
        {
            int width = defaults.ResolutionWidth, height = defaults.ResolutionHeight;
            string resolution = PlayerPrefs.GetString("resolution", string.Empty);
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
            int motion = PlayerPrefs.GetInt("reduceMotion", defaults.ReduceMotion ? 1 : 0);
            return new GameSettingsData(
                PlayerPrefs.GetFloat("masterVolume", defaults.MasterVolume),
                PlayerPrefs.GetFloat("musicVolume", defaults.MusicVolume),
                PlayerPrefs.GetFloat("sfxVolume", defaults.SfxVolume),
                PlayerPrefs.GetFloat("uiVolume", defaults.UiVolume),
                PlayerPrefs.GetFloat("mouseSensitivity", defaults.MouseSensitivity),
                (WindowModeSetting)PlayerPrefs.GetInt("windowMode", (int)defaults.WindowMode),
                width, height, motion == 0 ? false : motion == 1 ? true : defaults.ReduceMotion);
        }

        /// <inheritdoc/>
        public void Write(GameSettingsData data)
        {
            PlayerPrefs.SetFloat("masterVolume", data.MasterVolume);
            PlayerPrefs.SetFloat("musicVolume", data.MusicVolume);
            PlayerPrefs.SetFloat("sfxVolume", data.SfxVolume);
            PlayerPrefs.SetFloat("uiVolume", data.UiVolume);
            PlayerPrefs.SetFloat("mouseSensitivity", data.MouseSensitivity);
            PlayerPrefs.SetInt("windowMode", (int)data.WindowMode);
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
                PlayerPrefs.SetString("resolution", cachedResolution);
                resolutionWritten = true;
            }
            PlayerPrefs.SetInt("reduceMotion", data.ReduceMotion ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Save() => PlayerPrefs.Save();
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
