using RetroFPS.Settings;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Refreshes pre-cased copy live, retaining warmed TMP capacity for both languages.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private string key;
        [SerializeField] private TMP_Text label;

        private LanguageSetting shownLanguage;
        private bool hasLanguage;
        private StringTable cachedTable;
        private string cachedKey;
        private TMP_Text cachedLabel;
        private TMP_FontAsset cachedFont;
        private string turkish;
        private string english;

        private void Awake() { if (label == null) label = GetComponent<TMP_Text>(); }
        private void OnEnable()
        {
            GameSettings.Changed += OnSettingsChanged;
            Refresh();
        }
        private void OnDisable() => GameSettings.Changed -= OnSettingsChanged;

        private void OnSettingsChanged(GameSettingsData data)
        {
            if (!hasLanguage || shownLanguage != data.Language) Refresh();
        }

        /// <summary>Refreshes the label after a table edit or locale change.</summary>
        public void Refresh()
        {
            if (label == null || strings == null || string.IsNullOrEmpty(key)) return;
            string tr = strings.Get(key, LanguageSetting.Turkish);
            string en = strings.Get(key, LanguageSetting.English);
            if (cachedTable != strings || cachedKey != key || cachedLabel != label || cachedFont != label.font
                || !ReferenceEquals(turkish, tr) || !ReferenceEquals(english, en))
            {
                cachedTable = strings;
                cachedKey = key;
                cachedLabel = label;
                cachedFont = label.font;
                turkish = tr;
                english = en;
                // Warm glyph fallback, parsing and mesh arrays before the first visible switch.
                label.vertexBufferAutoSizeReduction = false;
                label.text = turkish.Length >= english.Length ? turkish : english;
                label.ForceMeshUpdate(true);
                label.text = turkish.Length >= english.Length ? english : turkish;
                label.ForceMeshUpdate(true);
            }
            shownLanguage = GameSettings.Current.Language;
            hasLanguage = true;
            label.text = shownLanguage == LanguageSetting.Turkish ? turkish : english;
        }
    }
}
