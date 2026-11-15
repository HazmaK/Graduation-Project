using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Six pre-created, event-driven acquired card rows.</summary>
    public sealed class ActiveCardsHudView : MonoBehaviour
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private GameObject list;
        [SerializeField] private TMP_Text[] rows;
        [SerializeField] private UnityEngine.UI.Image[] markers;
        [SerializeField] private RectTransform plate;
        [SerializeField] private float headerHeight = 32f, rowHeight = 28f, padding = 8f;
        [SerializeField] private Color bodyColor = new Color(0.9098039f, 0.8627451f, 0.7529412f, 1f);
        [SerializeField] private CardAcquiredBannerView banner;
        private LanguageSetting language;
        private void OnEnable()
        {
            language = GameSettings.Current.Language;
            RunEvents.OnCardAcquired += Acquired;
            RunEvents.OnRunChanged += RunChanged;
            GameSettings.Changed += SettingsChanged;
            RefreshSnapshot();
        }
        private void OnDisable()
        {
            RunEvents.OnCardAcquired -= Acquired;
            RunEvents.OnRunChanged -= RunChanged;
            GameSettings.Changed -= SettingsChanged;
        }
        private void Acquired(CurseBlessingDefinition card) { RefreshSnapshot(); banner?.Enqueue(card); }
        private void RunChanged(RunState run) { RefreshSnapshot(); banner?.Clear(); }
        private void SettingsChanged(GameSettingsData data)
        {
            if (language == data.Language) return;
            language = data.Language; RefreshSnapshot();
        }
        /// <summary>Rebuilds only in response to initialization, acquisition, run or language events.</summary>
        public void RefreshSnapshot()
        {
            CardInventory cards = RunEvents.CurrentRun?.Cards;
            int count = cards != null ? cards.Count : 0;
            if (list != null) list.SetActive(count > 0);
            if (plate != null)
            {
                int visible = rows != null ? Mathf.Min(count, rows.Length) : 0;
                plate.sizeDelta = new Vector2(plate.sizeDelta.x, padding * 2f + headerHeight + visible * rowHeight);
            }
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                TMP_Text row = rows[i];
                if (row == null) continue;
                row.gameObject.SetActive(i < count);
                UnityEngine.UI.Image marker = markers != null && i < markers.Length ? markers[i] : null;
                if (marker != null) marker.gameObject.SetActive(i < count);
                if (i >= count) continue;
                CurseBlessingDefinition card = cards.Get(i);
                row.color = bodyColor;
                if (marker != null) marker.color = card.AccentColor;
                row.text = strings != null ? strings.Get(card.NameKey, GameSettings.Current.Language) : string.Empty;
            }
        }
    }
}
