using System;
using UnityEngine;

namespace RetroFPS.Settings
{
    /// <summary>Supported desktop window modes.</summary>
    public enum WindowModeSetting
    {
        /// <summary>Exclusive fullscreen.</summary>
        Fullscreen,
        /// <summary>Borderless desktop window.</summary>
        Borderless,
        /// <summary>Decorated window.</summary>
        Windowed
    }

    /// <summary>A value snapshot of the user's settings.</summary>
    [Serializable]
    public struct GameSettingsData
    {
        [SerializeField] private float masterVolume;
        [SerializeField] private float musicVolume;
        [SerializeField] private float sfxVolume;
        [SerializeField] private float uiVolume;
        [SerializeField] private float mouseSensitivity;
        [SerializeField] private WindowModeSetting windowMode;
        [SerializeField] private int resolutionWidth;
        [SerializeField] private int resolutionHeight;
        [SerializeField] private bool reduceMotion;
        [SerializeField] private bool postProcessing;
        [SerializeField] private float brightness;
        [SerializeField] private float screenEffects;
        [SerializeField] private LanguageSetting language;
        [SerializeField] private DifficultySetting difficulty;

        /// <summary>Global audio gain.</summary>
        public float MasterVolume => masterVolume;
        /// <summary>Music gain.</summary>
        public float MusicVolume => musicVolume;
        /// <summary>Sound effect gain.</summary>
        public float SfxVolume => sfxVolume;
        /// <summary>Interface sound gain.</summary>
        public float UiVolume => uiVolume;
        /// <summary>Normalized mouse sensitivity.</summary>
        public float MouseSensitivity => mouseSensitivity;
        /// <summary>Requested window mode.</summary>
        public WindowModeSetting WindowMode => windowMode;
        /// <summary>Requested display width in pixels.</summary>
        public int ResolutionWidth => resolutionWidth;
        /// <summary>Requested display height in pixels.</summary>
        public int ResolutionHeight => resolutionHeight;
        /// <summary>Whether optional motion should be suppressed.</summary>
        public bool ReduceMotion => reduceMotion;
        /// <summary>Whether camera post-processing is enabled.</summary>
        public bool PostProcessing => postProcessing;
        /// <summary>Brightness adjustment in the range -1 to 1.</summary>
        public float Brightness => brightness;
        /// <summary>Screen effect intensity in the range 0 to 1.</summary>
        public float ScreenEffects => screenEffects;
        /// <summary>Current language authority.</summary>
        public LanguageSetting Language => language;
        /// <summary>Selected difficulty; Normal reproduces the untuned game.</summary>
        public DifficultySetting Difficulty => difficulty;

        /// <summary>Creates a settings snapshot; use Clamp before applying untrusted data.</summary>
        public GameSettingsData(float masterVolume, float musicVolume, float sfxVolume,
            float uiVolume, float mouseSensitivity, WindowModeSetting windowMode,
            int resolutionWidth, int resolutionHeight, bool reduceMotion)
            : this(masterVolume, musicVolume, sfxVolume, uiVolume, mouseSensitivity,
                windowMode, resolutionWidth, resolutionHeight, reduceMotion, LanguageSetting.English) { }

        /// <summary>Creates a settings snapshot including its language.</summary>
        public GameSettingsData(float masterVolume, float musicVolume, float sfxVolume,
            float uiVolume, float mouseSensitivity, WindowModeSetting windowMode,
            int resolutionWidth, int resolutionHeight, bool reduceMotion, LanguageSetting language,
            bool postProcessing = true, float brightness = 0f, float screenEffects = 1f,
            DifficultySetting difficulty = DifficultySetting.Normal)
        {
            this.masterVolume = masterVolume;
            this.musicVolume = musicVolume;
            this.sfxVolume = sfxVolume;
            this.uiVolume = uiVolume;
            this.mouseSensitivity = mouseSensitivity;
            this.windowMode = windowMode;
            this.resolutionWidth = resolutionWidth;
            this.resolutionHeight = resolutionHeight;
            this.reduceMotion = reduceMotion;
            this.postProcessing = postProcessing;
            this.brightness = brightness;
            this.screenEffects = screenEffects;
            this.language = language;
            this.difficulty = difficulty;
        }

        /// <summary>Creates first-boot defaults using the supplied desktop size.</summary>
        public static GameSettingsData Defaults(int desktopWidth = 1920, int desktopHeight = 1080)
            => Defaults(desktopWidth, desktopHeight, LanguageSetting.English);

        /// <summary>Creates first-boot defaults with the platform-derived language.</summary>
        public static GameSettingsData Defaults(int desktopWidth, int desktopHeight, LanguageSetting language)
        {
            return new GameSettingsData(1f, 0.7f, 1f, 0.8f, 0.5f,
                WindowModeSetting.Borderless, desktopWidth > 0 ? desktopWidth : 1920,
                desktopHeight > 0 ? desktopHeight : 1080, false,
                LanguageDefaults.IsValid(language) ? language : LanguageSetting.English);
        }

        /// <summary>Returns valid values, replacing non-finite or invalid data with defaults.</summary>
        public GameSettingsData Clamp(GameSettingsData defaults)
        {
            return new GameSettingsData(Unit(masterVolume, defaults.MasterVolume),
                Unit(musicVolume, defaults.MusicVolume), Unit(sfxVolume, defaults.SfxVolume),
                Unit(uiVolume, defaults.UiVolume), Unit(mouseSensitivity, defaults.MouseSensitivity),
                ValidMode(windowMode) ? windowMode : defaults.WindowMode,
                resolutionWidth > 0 && resolutionHeight > 0 ? resolutionWidth : defaults.ResolutionWidth,
                resolutionWidth > 0 && resolutionHeight > 0 ? resolutionHeight : defaults.ResolutionHeight,
                reduceMotion, LanguageDefaults.IsValid(language) ? language : defaults.Language,
                postProcessing, SignedUnit(brightness, defaults.Brightness), Unit(screenEffects, defaults.ScreenEffects),
                DifficultyDefaults.IsValid(difficulty) ? difficulty : defaults.Difficulty);
        }

        internal static float Unit(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        internal static bool ValidMode(WindowModeSetting mode)
        {
            return mode >= WindowModeSetting.Fullscreen && mode <= WindowModeSetting.Windowed;
        }

        internal static float SignedUnit(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return value < -1f ? -1f : value > 1f ? 1f : value;
        }

        internal GameSettingsData WithPostFx(bool enabled, float adjustment, float intensity)
        {
            return new GameSettingsData(masterVolume, musicVolume, sfxVolume, uiVolume,
                mouseSensitivity, windowMode, resolutionWidth, resolutionHeight, reduceMotion, language,
                enabled, adjustment, intensity, difficulty);
        }

        internal GameSettingsData WithDisplay(WindowModeSetting mode, int width, int height)
        {
            return new GameSettingsData(masterVolume, musicVolume, sfxVolume, uiVolume,
                mouseSensitivity, mode, width, height, reduceMotion, language, postProcessing, brightness, screenEffects, difficulty);
        }

        internal GameSettingsData WithDifficulty(DifficultySetting selected)
        {
            return new GameSettingsData(masterVolume, musicVolume, sfxVolume, uiVolume,
                mouseSensitivity, windowMode, resolutionWidth, resolutionHeight, reduceMotion, language,
                postProcessing, brightness, screenEffects, selected);
        }
    }
}
