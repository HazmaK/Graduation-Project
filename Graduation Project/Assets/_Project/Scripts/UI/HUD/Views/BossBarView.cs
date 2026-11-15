using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Event-driven boss name, current-phase health and two reached-phase pips.</summary>
    public sealed class BossBarView : MonoBehaviour
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private GameObject plate;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private UnityEngine.UI.Image healthFill, firstPip, secondPip;
        private string nameLocKey;
        private bool initialized, visible;
        private int phase;

        private void Awake() => Initialize();

        /// <summary>Prewarms the owned label once and establishes authored hidden state.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            HudText.Prewarm(nameText);
            Hide();
        }

        /// <summary>Shows a fresh phase-one encounter with a localized name and full health.</summary>
        public void Show(string nameLocKey, float max)
        {
            Initialize();
            this.nameLocKey = nameLocKey; visible = true; phase = 0;
            if (plate != null) plate.SetActive(true);
            RefreshLanguage(GameSettings.Current.Language);
            SetPhase(1); SetHealth(max, max);
        }

        /// <summary>Clamps current-phase health into the fill range without formatting text.</summary>
        public void SetHealth(float current, float max)
        {
            if (!visible || healthFill == null) return;
            healthFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        /// <summary>Refills on an actual phase change and lights every reached phase pip.</summary>
        public void SetPhase(int phase)
        {
            if (!visible) return;
            int next = Mathf.Clamp(phase, 1, 2);
            if (this.phase != next && healthFill != null) healthFill.fillAmount = 1f;
            this.phase = next;
            if (firstPip != null) firstPip.enabled = next >= 1;
            if (secondPip != null) secondPip.enabled = next >= 2;
        }

        /// <summary>Clears presentation state and hides the panel.</summary>
        public void Hide()
        {
            visible = false; nameLocKey = null; phase = 0;
            if (plate != null) plate.SetActive(false);
            if (healthFill != null) healthFill.fillAmount = 0f;
            if (firstPip != null) firstPip.enabled = false;
            if (secondPip != null) secondPip.enabled = false;
            if (nameText != null) nameText.text = string.Empty;
        }

        /// <summary>Refreshes owned copy without changing health, phase or panel visibility.</summary>
        public void RefreshLanguage(LanguageSetting language)
        {
            if (!initialized || nameText == null) return;
            nameText.text = nameLocKey == null ? string.Empty :
                strings != null && strings.ContainsKey(nameLocKey) ? strings.Get(nameLocKey, language) : nameLocKey;
        }
    }
}
