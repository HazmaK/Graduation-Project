using RetroFPS.Pause;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Card acquisition presentation backed by the standard HUD banner queue.</summary>
    public sealed class CardAcquiredBannerView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private StringTable strings;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        private CardAcquiredBannerModel model;
        private LanguageSetting language;
        private string displayed;
        private void Awake() => Initialize();
        private void OnEnable()
        {
            Initialize();
            LanguageSetting current = GameSettings.Current.Language;
            if (language != current) { language = current; model?.RefreshLanguage(strings, language); Render(); }
            GameSettings.Changed += SettingsChanged; RunEvents.OnTrueDeath += Clear;
        }
        private void OnDisable() { GameSettings.Changed -= SettingsChanged; RunEvents.OnTrueDeath -= Clear; Clear(); }
        private void Initialize()
        {
            if (model != null || config == null) return;
            model = new CardAcquiredBannerModel(config.BannerHoldMs * 0.001d, config.BannerFadeInMs * 0.001d, config.BannerFadeOutMs * 0.001d);
            language = GameSettings.Current.Language;
            Render();
        }
        /// <summary>Formats and queues only successful acquisitions.</summary>
        public void Enqueue(CurseBlessingDefinition card)
        {
            Initialize(); model?.Enqueue(card, strings, GameSettings.Current.Language, GamePause.UnscaledTime); Render();
        }
        /// <summary>Removes active and pending card banners.</summary>
        public void Clear() { model?.Clear(); Render(); }
        private void SettingsChanged(GameSettingsData data)
        {
            if (language == data.Language) return;
            language = data.Language; model?.RefreshLanguage(strings, language); Render();
        }
        private void Update() { model?.Tick(GamePause.UnscaledTime); Render(); }
        private void Render()
        {
            if (group != null) group.alpha = model != null && model.Visible ? 1f : 0f;
            string text = model != null ? model.Text : string.Empty;
            if (displayed == text) return;
            displayed = text;
            if (label != null) label.text = text;
        }
    }
}
