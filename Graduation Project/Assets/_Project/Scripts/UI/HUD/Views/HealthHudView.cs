using RetroFPS.Presentation;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Displays base health and source-dependent overheal as a separate numeric piece.</summary>
    public sealed class HealthHudView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private TMP_Text currentText, overflowText;
        [SerializeField] private GameObject overflowGroup, warningChip;
        [SerializeField] private UnityEngine.UI.Image fill, leadingTick, lowBorder, damageEdge;
        private readonly HealthDisplayModel model = new HealthDisplayModel();
        private readonly char[] digits = new char[256];
        private bool low, reduceMotion, initialized, textDirty;
        private float damageUntil;

        /// <summary>Preallocates the TMP buffers before the initial gameplay snapshot.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            HudText.Prewarm(currentText); HudText.Prewarm(overflowText);
            textDirty = true;
            if (damageEdge != null) damageEdge.enabled = false;
        }

        /// <summary>Applies a health mutation without formatting or allocations.</summary>
        public void Render(float current, float maximum)
        {
            if (config == null || theme == null) return;
            model.Update(current, maximum);
            if (textDirty || model.NumbersChanged)
            {
                textDirty = false;
                HudText.Template(currentText, "{0}<size=20> / {1}</size>", model.Base, model.Maximum, digits);

                HudText.Template(overflowText, "+{0}", model.Overflow, 0, digits);
                if (overflowGroup != null) overflowGroup.SetActive(model.Overflow > 0);
            }
            if (model.FillChanged)
            {
                float old = fill != null ? fill.fillAmount : 0f;
                if (fill != null) fill.fillAmount = model.BaseFill;
                if (leadingTick != null)
                {
                    RectTransform tick = leadingTick.rectTransform;
                    Vector2 anchor = tick.anchorMin; anchor.x = model.BaseFill;
                    tick.anchorMin = tick.anchorMax = anchor;
                }
                if (!reduceMotion && model.BaseFill < old && damageEdge != null)
                {
                    RectTransform edge = damageEdge.rectTransform;
                    Vector2 min = edge.anchorMin; min.x = model.BaseFill;
                    Vector2 max = edge.anchorMax; max.x = old;
                    edge.anchorMin = min; edge.anchorMax = max;
                    edge.offsetMin = edge.offsetMax = Vector2.zero;
                    damageEdge.enabled = true; damageUntil = Time.unscaledTime + 0.12f;
                }
            }
            low = HudLowStateRules.IsLowHealth(current, maximum, config.LowHealthPercent);
            if (warningChip != null) warningChip.SetActive(low);
            if (lowBorder != null) lowBorder.enabled = low;
        }

        /// <summary>Immediately removes motion while preserving the low-state warning.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value;
            if (value && damageEdge != null) damageEdge.enabled = false;
            if (lowBorder != null && theme != null) lowBorder.color = theme.MenuTheme.Danger;
        }

        private void Update()
        {
            if (low && lowBorder != null && theme != null && config != null && !reduceMotion)
            {
                Color color = theme.MenuTheme.Danger;
                color.a = 0.775f + 0.225f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f * config.LowPulseHz);
                lowBorder.color = color;
            }
            if (damageEdge != null && damageEdge.enabled && Time.unscaledTime >= damageUntil) damageEdge.enabled = false;
        }
    }
}
