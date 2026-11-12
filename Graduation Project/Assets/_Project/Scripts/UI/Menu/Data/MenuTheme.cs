using TMPro;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Direction A palette, typography, timings and ambient motion defaults.</summary>
    [CreateAssetMenu(fileName = "MenuTheme", menuName = "RetroFPS/Menu/Theme")]
    public sealed class MenuTheme : ScriptableObject
    {
        [SerializeField] private int emberMaxParticles = 64;
        [SerializeField] private int smokeMaxParticles = 8;
        [SerializeField] private float smokeRateMin = 2f;
        [SerializeField] private float smokeRateMax = 3f;
        [SerializeField] private Color torchColor = new Color32(255, 140, 42, 255);
        [SerializeField] private Color emberEndColor = new Color32(216, 69, 15, 0);
        /// <summary>Gets the preallocated ember particle capacity.</summary>
        public int EmberMaxParticles => emberMaxParticles;
        /// <summary>Gets the preallocated smoke particle capacity.</summary>
        public int SmokeMaxParticles => smokeMaxParticles;
        /// <summary>Gets the minimum smoke emission per second.</summary>
        public float SmokeRateMin => smokeRateMin;
        /// <summary>Gets the maximum smoke emission per second.</summary>
        public float SmokeRateMax => smokeRateMax;
        /// <summary>Gets the warm torch and initial ember colour.</summary>
        public Color TorchColor => torchColor;
        /// <summary>Gets the final ember colour as it fades out.</summary>
        public Color EmberEndColor => emberEndColor;
        [SerializeField] private Color background = new Color32(12, 10, 10, 255);
        /// <summary>Gets the Background colour token.</summary>
        public Color Background => background;
        [SerializeField] private Color panel = new Color32(20, 15, 16, 255);
        /// <summary>Gets the Panel colour token.</summary>
        public Color Panel => panel;
        [SerializeField] private Color text = new Color32(232, 220, 192, 255);
        /// <summary>Gets the Text colour token.</summary>
        public Color Text => text;
        [SerializeField] private Color textDim = new Color32(160, 150, 132, 255);
        /// <summary>Gets the TextDim colour token.</summary>
        public Color TextDim => textDim;
        [SerializeField] private Color focus = new Color32(255, 244, 220, 255);
        /// <summary>Gets the Focus colour token.</summary>
        public Color Focus => focus;
        [SerializeField] private Color accent = new Color32(219, 20, 61, 255);
        /// <summary>Gets the Accent colour token.</summary>
        public Color Accent => accent;
        [SerializeField] private Color disabled = new Color32(92, 82, 72, 255);
        /// <summary>Gets the Disabled colour token.</summary>
        public Color Disabled => disabled;
        [SerializeField] private Color danger = new Color32(255, 77, 94, 255);
        /// <summary>Gets the Danger colour token.</summary>
        public Color Danger => danger;
        [SerializeField] private Color worstBackground = new Color32(32, 24, 24, 255);
        /// <summary>Gets the WorstBackground colour token.</summary>
        public Color WorstBackground => worstBackground;
        [SerializeField] private Color rowBand = new Color32(30, 23, 24, 255);
        /// <summary>Gets the RowBand colour token.</summary>
        public Color RowBand => rowBand;
        [SerializeField] private Color sliderTrack = new Color32(42, 32, 32, 255);
        /// <summary>Gets the SliderTrack colour token.</summary>
        public Color SliderTrack => sliderTrack;
        [SerializeField] private float titleSize = 132f;
        /// <summary>Gets the TitleSize theme parameter.</summary>
        public float TitleSize => titleSize;
        [SerializeField] private float menuItemSize = 44f;
        /// <summary>Gets the MenuItemSize theme parameter.</summary>
        public float MenuItemSize => menuItemSize;
        [SerializeField] private float settingsTitleSize = 56f;
        /// <summary>Gets the SettingsTitleSize theme parameter.</summary>
        public float SettingsTitleSize => settingsTitleSize;
        [SerializeField] private float settingsLabelSize = 28f;
        /// <summary>Gets the SettingsLabelSize theme parameter.</summary>
        public float SettingsLabelSize => settingsLabelSize;
        [SerializeField] private float groupLabelSize = 24f;
        /// <summary>Gets the GroupLabelSize theme parameter.</summary>
        public float GroupLabelSize => groupLabelSize;
        [SerializeField] private float dialogPromptSize = 32f;
        /// <summary>Gets the DialogPromptSize theme parameter.</summary>
        public float DialogPromptSize => dialogPromptSize;
        [SerializeField] private float dialogButtonSize = 32f;
        /// <summary>Gets the DialogButtonSize theme parameter.</summary>
        public float DialogButtonSize => dialogButtonSize;
        [SerializeField] private float versionSize = 24f;
        /// <summary>Gets the VersionSize theme parameter.</summary>
        public float VersionSize => versionSize;
        [SerializeField] private float minimumFontSize = 24f;
        /// <summary>Gets the MinimumFontSize theme parameter.</summary>
        public float MinimumFontSize => minimumFontSize;
        [SerializeField] private float titleMinimumFontSize = 56f;
        /// <summary>Gets the TitleMinimumFontSize theme parameter.</summary>
        public float TitleMinimumFontSize => titleMinimumFontSize;
        [SerializeField] private float titleTracking = 2f;
        /// <summary>Gets the TitleTracking theme parameter.</summary>
        public float TitleTracking => titleTracking;
        [SerializeField] private float groupTracking = 12f;
        /// <summary>Gets the GroupTracking theme parameter.</summary>
        public float GroupTracking => groupTracking;
        [SerializeField] private float firstLoadSeconds = 0.6f;
        /// <summary>Gets the FirstLoadSeconds theme parameter.</summary>
        public float FirstLoadSeconds => firstLoadSeconds;
        [SerializeField] private float panelSeconds = 0.2f;
        /// <summary>Gets the PanelSeconds theme parameter.</summary>
        public float PanelSeconds => panelSeconds;
        [SerializeField] private float dialogSeconds = 0.2f;
        /// <summary>Gets the DialogSeconds theme parameter.</summary>
        public float DialogSeconds => dialogSeconds;
        [SerializeField] private float pressSeconds = 0.08f;
        /// <summary>Gets the PressSeconds theme parameter.</summary>
        public float PressSeconds => pressSeconds;
        [SerializeField] private float reducedFadeSeconds = 0.1f;
        /// <summary>Gets the ReducedFadeSeconds theme parameter.</summary>
        public float ReducedFadeSeconds => reducedFadeSeconds;
        [SerializeField] private float startFadeSeconds = 0.6f;
        /// <summary>Gets the StartFadeSeconds theme parameter.</summary>
        public float StartFadeSeconds => startFadeSeconds;
        [SerializeField] private float focusSeconds = 0.08f;
        /// <summary>Gets the FocusSeconds theme parameter.</summary>
        public float FocusSeconds => focusSeconds;
        [SerializeField] private float revertSeconds = 10f;
        /// <summary>Gets the RevertSeconds theme parameter.</summary>
        public float RevertSeconds => revertSeconds;
        [SerializeField] private float focusScale = 1.06f;
        /// <summary>Gets the FocusScale theme parameter.</summary>
        public float FocusScale => focusScale;
        [SerializeField] private float panelAlpha = 0.94f;
        /// <summary>Gets the PanelAlpha theme parameter.</summary>
        public float PanelAlpha => panelAlpha;
        [SerializeField] private float dimAlpha = 0.65f;
        /// <summary>Gets the DimAlpha theme parameter.</summary>
        public float DimAlpha => dimAlpha;
        [SerializeField] private float pressedOffset = 3f;
        /// <summary>Gets the PressedOffset theme parameter.</summary>
        public float PressedOffset => pressedOffset;
        [SerializeField] private float audioThrottleSeconds = 0.05f;
        /// <summary>Gets the AudioThrottleSeconds theme parameter.</summary>
        public float AudioThrottleSeconds => audioThrottleSeconds;
        [SerializeField] private float emberRateMin = 6f;
        /// <summary>Gets the EmberRateMin theme parameter.</summary>
        public float EmberRateMin => emberRateMin;
        [SerializeField] private float emberRateMax = 10f;
        /// <summary>Gets the EmberRateMax theme parameter.</summary>
        public float EmberRateMax => emberRateMax;
        [SerializeField] private float emberSpeedMin = 20f;
        /// <summary>Gets the EmberSpeedMin theme parameter.</summary>
        public float EmberSpeedMin => emberSpeedMin;
        [SerializeField] private float emberSpeedMax = 40f;
        /// <summary>Gets the EmberSpeedMax theme parameter.</summary>
        public float EmberSpeedMax => emberSpeedMax;
        [SerializeField] private float emberLifetimeMin = 4f;
        /// <summary>Gets the EmberLifetimeMin theme parameter.</summary>
        public float EmberLifetimeMin => emberLifetimeMin;
        [SerializeField] private float emberLifetimeMax = 7f;
        /// <summary>Gets the EmberLifetimeMax theme parameter.</summary>
        public float EmberLifetimeMax => emberLifetimeMax;
        [SerializeField] private float emberSizeMin = 3f;
        /// <summary>Gets the EmberSizeMin theme parameter.</summary>
        public float EmberSizeMin => emberSizeMin;
        [SerializeField] private float emberSizeMax = 6f;
        /// <summary>Gets the EmberSizeMax theme parameter.</summary>
        public float EmberSizeMax => emberSizeMax;
        [SerializeField] private float smokeSpeedMin = 6f;
        /// <summary>Gets the SmokeSpeedMin theme parameter.</summary>
        public float SmokeSpeedMin => smokeSpeedMin;
        [SerializeField] private float smokeSpeedMax = 10f;
        /// <summary>Gets the SmokeSpeedMax theme parameter.</summary>
        public float SmokeSpeedMax => smokeSpeedMax;
        [SerializeField] private float smokeAlphaMin = 0.1f;
        /// <summary>Gets the SmokeAlphaMin theme parameter.</summary>
        public float SmokeAlphaMin => smokeAlphaMin;
        [SerializeField] private float smokeAlphaMax = 0.18f;
        /// <summary>Gets the SmokeAlphaMax theme parameter.</summary>
        public float SmokeAlphaMax => smokeAlphaMax;
        [SerializeField] private float fogScrollSpeed = 0.004f;
        /// <summary>Gets the FogScrollSpeed theme parameter.</summary>
        public float FogScrollSpeed => fogScrollSpeed;
        [SerializeField] private float torchAlphaMin = 0.85f;
        /// <summary>Gets the TorchAlphaMin theme parameter.</summary>
        public float TorchAlphaMin => torchAlphaMin;
        [SerializeField] private float torchAlphaMax = 1f;
        /// <summary>Gets the TorchAlphaMax theme parameter.</summary>
        public float TorchAlphaMax => torchAlphaMax;
        [SerializeField] private float torchFrequency = 1.5f;
        /// <summary>Gets the TorchFrequency theme parameter.</summary>
        public float TorchFrequency => torchFrequency;
        [SerializeField] private float reducedTorchAlpha = 0.92f;
        /// <summary>Gets the ReducedTorchAlpha theme parameter.</summary>
        public float ReducedTorchAlpha => reducedTorchAlpha;
        [SerializeField] private TMP_FontAsset titleFont;
        /// <summary>Gets the TitleFont asset, assigned by the menu builder.</summary>
        public TMP_FontAsset TitleFont => titleFont;
        [SerializeField] private TMP_FontAsset uiFont;
        /// <summary>Gets the UiFont asset, assigned by the menu builder.</summary>
        public TMP_FontAsset UiFont => uiFont;
        [SerializeField] private TMP_FontAsset fallbackFont;
        /// <summary>Gets the FallbackFont asset, assigned by the menu builder.</summary>
        public TMP_FontAsset FallbackFont => fallbackFont;
    }
}
