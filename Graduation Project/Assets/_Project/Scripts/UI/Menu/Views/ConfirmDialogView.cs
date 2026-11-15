using System.Globalization;
using RetroFPS.Settings;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Two-choice dialog presentation; navigation and decisions belong to the controller.</summary>
    public sealed class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Button affirmative;
        [SerializeField] private UnityEngine.UI.Button negative;
        [SerializeField] private TMP_Text countdownLabel;
        [SerializeField] private StringTable strings;
        [SerializeField] private MenuTheme theme;
        private string[] countdownStrings;
        private readonly string[][] localeCountdowns = new string[2][];
        private int shownSeconds = -1;
        private RevertCountdown activeCountdown;
        private LanguageSetting cachedLanguage;
        private bool hasLanguage;
        private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

        /// <summary>Dialog opacity and input boundary.</summary>
        public CanvasGroup Group => group;
        /// <summary>Explicit confirmation choice.</summary>
        public UnityEngine.UI.Button Affirmative => affirmative;
        /// <summary>Safe default choice.</summary>
        public UnityEngine.UI.Button Negative => negative;

        private void Awake()
        {
            for (int locale = 0; locale < 2; locale++)
            {
                LanguageSetting language = locale == 0 ? LanguageSetting.Turkish : LanguageSetting.English;
                CultureInfo culture = locale == 0 ? TurkishCulture : EnglishCulture;
                string format = strings.Get("display.revert_countdown_format", language);
                var values = new string[11];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = string.Format(culture, format, i);
                    if (countdownLabel == null) continue;
                    countdownLabel.vertexBufferAutoSizeReduction = false;
                    countdownLabel.text = values[i];
                    countdownLabel.ForceMeshUpdate(true);
                }
                localeCountdowns[locale] = values;
            }
            if (countdownLabel != null) countdownLabel.text = string.Empty;
            RefreshLanguage(GameSettings.Current);
        }

        private void OnEnable()
        {
            GameSettings.Changed += RefreshLanguage;
            RefreshLanguage(GameSettings.Current);
            if (activeCountdown != null) ShowCountdown(activeCountdown);
        }
        private void OnDisable() => GameSettings.Changed -= RefreshLanguage;

        private void RefreshLanguage(GameSettingsData data)
        {
            if (hasLanguage && cachedLanguage == data.Language) return;
            cachedLanguage = data.Language;
            hasLanguage = true;
            countdownStrings = localeCountdowns[data.Language == LanguageSetting.Turkish ? 0 : 1];
            shownSeconds = -1;
            if (activeCountdown != null) ShowCountdown(activeCountdown);
        }

        /// <summary>Uses a prebuilt string and numeric urgency for the display deadline.</summary>
        public void ShowCountdown(RevertCountdown countdown)
        {
            activeCountdown = countdown;
            if (countdownLabel == null) return;
            int seconds = Mathf.Clamp(countdown.WholeSecondsRemaining, 0, 10);
            if (shownSeconds != seconds)
            {
                shownSeconds = seconds;
                countdownLabel.text = countdownStrings[seconds];
            }
            countdownLabel.color = countdown.Danger ? theme.Danger : theme.Text;
        }
    }
}
