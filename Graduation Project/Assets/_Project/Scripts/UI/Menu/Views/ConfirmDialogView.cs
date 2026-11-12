using System.Globalization;
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
        private int shownSeconds = -1;

        /// <summary>Dialog opacity and input boundary.</summary>
        public CanvasGroup Group => group;
        /// <summary>Explicit confirmation choice.</summary>
        public UnityEngine.UI.Button Affirmative => affirmative;
        /// <summary>Safe default choice.</summary>
        public UnityEngine.UI.Button Negative => negative;

        private void Awake()
        {
            countdownStrings = new string[11];
            string format = strings.Get("display.revert_countdown_format");
            for (int i = 0; i < countdownStrings.Length; i++)
                countdownStrings[i] = string.Format(CultureInfo.GetCultureInfo("tr-TR"), format, i);
        }

        /// <summary>Uses a prebuilt string and numeric urgency for the display deadline.</summary>
        public void ShowCountdown(RevertCountdown countdown)
        {
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
