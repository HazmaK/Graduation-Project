using System.Globalization;
using RetroFPS.Presentation;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Renders the fixed two-slot banner queue with cached lines and unscaled opacity.</summary>
    public sealed class HudBannerView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform plate;
        [SerializeField] private TMP_Text firstLine, secondLine;
        [SerializeField] private UnityEngine.UI.Image rule;
        private HudBannerQueue queue;
        private string sealedText, clearedText, waveTemplate, clearedTemplate;
        private string[] waves, clearedWaves;
        private readonly char[] firstBuffer = new char[256], secondBuffer = new char[256];
        private HudBanner displayed;
        private double displayEnd;
        private double queueTimeScale = 1d;
        private bool reduceMotion;

        /// <summary>Prebuilds bounded wave variants and separate line buffers before combat.</summary>
        public void Initialize()
        {
            if (queue != null || config == null || strings == null) return;
            // Part B fixes its semantic merge/replacement window at 0.5 seconds. Scale its entire
            // clock and durations together so the shared config knob still controls that window.
            queueTimeScale = 500d / Mathf.Max(1, config.BannerMergeWindowMs);
            queue = new HudBannerQueue(config.ArenaClearedHoldMs * 0.001d * queueTimeScale, config.BannerHoldMs * 0.001d * queueTimeScale,
                config.BannerFadeInMs * 0.001d * queueTimeScale, config.BannerFadeOutMs * 0.001d * queueTimeScale);
            sealedText = strings.Get("hud.banner.arenaSealed"); clearedText = strings.Get("hud.banner.arenaCleared");
            waveTemplate = strings.Get("hud.banner.wave"); clearedTemplate = strings.Get("hud.banner.waveCleared");
            waves = new string[1000]; clearedWaves = new string[1000];
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = string.Format(CultureInfo.InvariantCulture, waveTemplate, HudNumberCache.GetInteger(i));
                clearedWaves[i] = string.Format(CultureInfo.InvariantCulture, clearedTemplate, HudNumberCache.GetInteger(i));
            }
            HudText.Prewarm(firstLine, clearedTemplate); HudText.Prewarm(secondLine, clearedTemplate);
            Clear();
        }

        /// <summary>Enqueues an event without constructing a string or collection.</summary>
        public void Enqueue(HudBannerKind kind, int oneBasedWave = 0)
        {
            if (queue == null) return;
            queue.Enqueue(kind, oneBasedWave, Time.unscaledTimeAsDouble * queueTimeScale);
            Render();
        }

        /// <summary>Clears active and pending content on retry, rebind, Araf, death or scene changes.</summary>
        public void Clear()
        {
            queue?.Reset(); displayed = default; displayEnd = 0d;
            if (group != null) group.alpha = 0f;
        }

        /// <summary>Snaps current fades under reduced motion without changing message holds.</summary>
        public void SetReduceMotion(bool value) { reduceMotion = value; Render(); }

        private void Update() { if (queue == null) return; queue.Tick(Time.unscaledTimeAsDouble * queueTimeScale); Render(); }
        private void Render()
        {
            if (queue == null || group == null) return;
            if (!queue.HasCurrent) { group.alpha = 0f; displayed = default; displayEnd = 0d; return; }
            HudBanner banner = queue.Current;
            if (banner.Kind != displayed.Kind || banner.Wave != displayed.Wave || banner.SecondKind != displayed.SecondKind ||
                banner.SecondWave != displayed.SecondWave || displayEnd != queue.ExpiresAt)
            {
                displayed = banner; displayEnd = queue.ExpiresAt;
                Line(firstLine, banner.Kind, banner.Wave, firstBuffer);
                secondLine.gameObject.SetActive(banner.IsMerged);
                if (banner.IsMerged) Line(secondLine, banner.SecondKind, banner.SecondWave, secondBuffer);
                plate.sizeDelta = banner.IsMerged ? theme.MergedBannerSize : theme.BannerSize;
                firstLine.color = banner.Kind == HudBannerKind.ArenaCleared ? theme.MenuTheme.Focus : theme.MenuTheme.Text;
                Vector2 position = firstLine.rectTransform.anchoredPosition;
                position.y = banner.IsMerged ? -25f : -30f; firstLine.rectTransform.anchoredPosition = position;
                Vector2 size = rule.rectTransform.sizeDelta;
                size.y = banner.Kind == HudBannerKind.ArenaCleared ? theme.ArenaClearedRuleHeight : theme.BannerRuleHeight;
                rule.rectTransform.sizeDelta = size;
            }
            double now = Time.unscaledTimeAsDouble;
            double end = queue.ExpiresAt / queueTimeScale;
            double hold = banner.Kind == HudBannerKind.ArenaCleared ? config.ArenaClearedHoldMs * 0.001d : config.BannerHoldMs * 0.001d;
            double fadeIn = config.BannerFadeInMs * 0.001d, fadeOut = config.BannerFadeOutMs * 0.001d;
            double start = end - hold - fadeIn - fadeOut;
            float opacity = 1f;
            if (!reduceMotion)
            {
                if (fadeIn > 0d && now < start + fadeIn) opacity = (float)((now - start) / fadeIn);
                else if (fadeOut > 0d && now > end - fadeOut) opacity = (float)((end - now) / fadeOut);
            }
            group.alpha = MenuTween.Evaluate(opacity, MenuEase.Linear);
        }

        private void Line(TMP_Text label, HudBannerKind kind, int wave, char[] buffer)
        {
            if (kind == HudBannerKind.ArenaSealed) label.text = sealedText;
            else if (kind == HudBannerKind.ArenaCleared) label.text = clearedText;
            else if ((uint)wave < waves.Length) label.text = kind == HudBannerKind.Wave ? waves[wave] : clearedWaves[wave];
            else HudText.Template(label, kind == HudBannerKind.Wave ? waveTemplate : clearedTemplate, wave, 0, buffer);
        }
    }
}
