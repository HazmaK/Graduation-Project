using System.Globalization;
using RetroFPS.Settings;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Binds five sliders and three cycles to scene-independent settings requests.</summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private MenuSlider[] sliders;
        [SerializeField] private TMP_Text[] percentages;
        [SerializeField] private MenuCycleRow windowMode;
        [SerializeField] private MenuCycleRow resolution;
        [SerializeField] private MenuCycleRow reduceMotion;
        private MainMenuController controller;
        private ISettingsService service;
        private ResolutionOption[] resolutions;
        private string[] percentStrings;
        private string[] resolutionStrings;
        private string[] modeStrings;
        private string[] motionStrings;
        private int resolutionCount = -1;

        private void Awake()
        {
            service = GameSettings.Service;
            percentStrings = new string[21];
            string format = strings.Get("settings.percent_format");
            for (int i = 0; i < percentStrings.Length; i++)
                percentStrings[i] = string.Format(CultureInfo.GetCultureInfo("tr-TR"), format, i * 5);
            modeStrings = new[] { strings.Get("settings.window_mode.fullscreen"),
                strings.Get("settings.window_mode.borderless"), strings.Get("settings.window_mode.windowed") };
            motionStrings = new[] { strings.Get("settings.off"), strings.Get("settings.on") };
            windowMode.SetOptions(modeStrings);
            reduceMotion.SetOptions(motionStrings);
            RefreshResolutionOptions();
            sliders[0].onValueChanged.AddListener(MasterChanged);
            sliders[1].onValueChanged.AddListener(MusicChanged);
            sliders[2].onValueChanged.AddListener(SfxChanged);
            sliders[3].onValueChanged.AddListener(UiChanged);
            sliders[4].onValueChanged.AddListener(SensitivityChanged);
            windowMode.Changed += ModeChanged;
            resolution.Changed += ResolutionChanged;
            reduceMotion.Changed += MotionChanged;
        }

        private void OnEnable()
        {
            GameSettings.Changed += Refresh;
            Display.onDisplaysUpdated += RefreshResolutionOptions;
        }
        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            Display.onDisplaysUpdated -= RefreshResolutionOptions;
        }
        private void OnDestroy()
        {
            sliders[0].onValueChanged.RemoveListener(MasterChanged);
            sliders[1].onValueChanged.RemoveListener(MusicChanged);
            sliders[2].onValueChanged.RemoveListener(SfxChanged);
            sliders[3].onValueChanged.RemoveListener(UiChanged);
            sliders[4].onValueChanged.RemoveListener(SensitivityChanged);
            windowMode.Changed -= ModeChanged;
            resolution.Changed -= ResolutionChanged;
            reduceMotion.Changed -= MotionChanged;
        }

        /// <summary>Injects the controller gate without coupling the settings service to UI.</summary>
        public void Bind(MainMenuController owner) => controller = owner;

        /// <summary>Reads the current live snapshot using cached display options.</summary>
        public void Open() => Refresh(service.Current);

        private void RefreshResolutionOptions()
        {
            // Screen.resolutions allocates an array, so query only at startup or a display notification.
            Resolution[] supported = Screen.resolutions;
            if (resolutionCount == supported.Length) return;
            resolutionCount = supported.Length;
            var sizes = new ResolutionOption[supported.Length];
            for (int i = 0; i < supported.Length; i++)
                sizes[i] = new ResolutionOption(supported[i].width, supported[i].height);
            resolutions = ResolutionOptions.Build(sizes, ScreenDisplayApplier.GetDesktopResolution());
            resolutionStrings = new string[resolutions.Length];
            for (int i = 0; i < resolutions.Length; i++)
                resolutionStrings[i] = resolutions[i].Width.ToString(CultureInfo.InvariantCulture) + "x"
                    + resolutions[i].Height.ToString(CultureInfo.InvariantCulture);
            resolution.SetOptions(resolutionStrings);
            Refresh(service.Current);
        }

        /// <summary>Commits or restores the pending display snapshot after the state machine decides.</summary>
        public void ResolveDisplay(bool keep)
        {
            if (keep) service.ConfirmDisplay();
            else service.RevertDisplay();
        }

        private void Refresh(GameSettingsData data)
        {
            SetSlider(0, data.MasterVolume);
            SetSlider(1, data.MusicVolume);
            SetSlider(2, data.SfxVolume);
            SetSlider(3, data.UiVolume);
            SetSlider(4, data.MouseSensitivity);
            windowMode.SetIndex((int)data.WindowMode);
            if (resolutions != null)
                resolution.SetIndex(ResolutionOptions.FindNearestIndex(resolutions, data.ResolutionWidth, data.ResolutionHeight));
            reduceMotion.SetIndex(data.ReduceMotion ? 1 : 0);
        }

        private void SetSlider(int index, float normalized)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(normalized * 20f), 0, 20);
            sliders[index].SetValueWithoutNotify(step);
            percentages[index].text = percentStrings[step];
        }
        private bool CanEdit => controller != null && controller.CanEditSettings;
        private void MasterChanged(float value) => Volume("masterVolume", value);
        private void MusicChanged(float value) => Volume("musicVolume", value);
        private void SfxChanged(float value) => Volume("sfxVolume", value);
        private void UiChanged(float value) => Volume("uiVolume", value);
        private void Volume(string key, float value)
        {
            if (!CanEdit) { Refresh(service.Current); return; }
            service.SetVolume(key, value * 0.05f);
            MenuEvents.RaiseUiAudio(UiAudioEventType.SliderStep);
            MenuEvents.RaiseUiAudio(UiAudioEventType.VolumePreview);
        }
        private void SensitivityChanged(float value)
        {
            if (!CanEdit) { Refresh(service.Current); return; }
            service.SetSensitivity(value * 0.05f);
            MenuEvents.RaiseUiAudio(UiAudioEventType.SliderStep);
        }
        private void ModeChanged(int index)
        {
            if (controller == null || !controller.CanPreviewDisplay) { Refresh(service.Current); return; }
            GameSettingsData data = service.Current;
            Preview((WindowModeSetting)index, data.ResolutionWidth, data.ResolutionHeight, MenuFocusTarget.WindowMode);
        }
        private void ResolutionChanged(int index)
        {
            if (controller == null || !controller.CanPreviewDisplay) { Refresh(service.Current); return; }
            ResolutionOption size = resolutions[index];
            Preview(service.Current.WindowMode, size.Width, size.Height, MenuFocusTarget.Resolution);
        }
        private void Preview(WindowModeSetting mode, int width, int height, MenuFocusTarget opener)
        {
            service.RequestDisplay(mode, width, height);
            if (!controller.OpenDisplayPrompt(opener)) service.RevertDisplay();
        }
        private void MotionChanged(int index)
        {
            if (CanEdit) service.SetReduceMotion(index != 0);
            else Refresh(service.Current);
        }
    }
}
