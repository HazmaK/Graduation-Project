using RetroFPS.Pause;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Switches the top-right plate between exploration, encounter and labyrinth context.</summary>
    public sealed class EncounterRunHudView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private RectTransform plate;
        [SerializeField] private GameObject waveGroup, arafGroup, runGroup, exitGroup;
        [SerializeField] private CanvasGroup waveOpacity;
        [SerializeField] private TMP_Text waveText, remainingText, chargeText, roomsText;
        [SerializeField] private UnityEngine.UI.Image remainingFill, remainingUnderline, sigilFill;
        private readonly char[] buffer = new char[256];
        private readonly MenuTween completedFade = new MenuTween();
        private string ready, used, waveFormat, remainingFormat, sealsFormat;
        private bool initialized, inAraf, encounterVisible, hiding, reduceMotion, runPresent;
        private int wave = -1, waveTotal = -1, remaining = -1, enemyTotal = -1, seals = -1, sealTotal = -1;
        private bool portalOpen, chargeConsumed;
        private bool waveTextDirty, remainingTextDirty, sealsTextDirty;
        private float sealFlashUntil;

        /// <summary>Resolves state copy and prewarms numeric pieces before gameplay events.</summary>
        public void Initialize()
        {
            if (initialized || strings == null) return;
            initialized = true;
            RefreshLanguage(GameSettings.Current.Language);
            HudText.Prewarm(waveText); HudText.Prewarm(remainingText);
            HudText.Prewarm(roomsText); HudText.Prewarm(chargeText, used);
            waveTextDirty = remainingTextDirty = sealsTextDirty = true;
        }

        /// <summary>Refreshes counters and charge copy while retaining fades, seal flashes and context.</summary>
        public void RefreshLanguage(LanguageSetting language)
        {
            if (!initialized || strings == null) return;
            waveFormat = strings.Get("hud.wave.counter", language); remainingFormat = strings.Get("hud.wave.remaining", language);
            sealsFormat = strings.Get("hud.araf.seals", language);
            ready = strings.Get("hud.araf.ready", language); used = strings.Get("hud.araf.used", language);
            waveTextDirty = remainingTextDirty = sealsTextDirty = true;
            if (wave >= 0) SetWave(wave, waveTotal);
            if (remaining >= 0) SetRemaining(remaining, enemyTotal);
            if (seals >= 0) SetSeals(seals, sealTotal);
            SetCharge(chargeConsumed);
        }

        /// <summary>Snapshots run/phase without reconstructing true death from a phase value.</summary>
        public void SetRun(RunState run)
        {
            runPresent = run != null;
            runGroup.SetActive(runPresent);
            if (run == null) { inAraf = false; ApplyContext(); return; }
            SetCharge(run.ArafChargeUsed); SetPhase(run.Phase);
        }

        /// <summary>Preserves labyrinth context during exit travel until Tower returns.</summary>
        public void SetPhase(RunPhase phase)
        {
            if (phase == RunPhase.Tower) inAraf = false;
            else if (phase == RunPhase.InAraf || phase == RunPhase.EscapingAraf) inAraf = true;
            ApplyContext();
        }

        /// <summary>Changes the sigil fill and its redundant ready/used wording.</summary>
        public void SetCharge(bool consumed)
        {
            chargeConsumed = consumed;
            if (chargeText != null) chargeText.text = consumed ? used : ready;
            if (sigilFill != null) sigilFill.enabled = !consumed;
        }

        /// <summary>Applies an already one-based wave payload, or a converted getter snapshot.</summary>
        public void SetWave(int current, int total)
        {
            if (waveTextDirty || wave != current || waveTotal != total)
            {
                waveTextDirty = false;
                wave = current; waveTotal = total;
                HudText.Template(waveText, waveFormat, current, total, buffer);
            }
        }

        /// <summary>Displays the remaining wave population and low-count underline.</summary>
        public void SetRemaining(int current, int total)
        {
            if (!remainingTextDirty && remaining == current && enemyTotal == total) return;
            remainingTextDirty = false;
            remaining = current; enemyTotal = total;
            HudText.Template(remainingText, remainingFormat, current, 0, buffer);
            remainingFill.fillAmount = total > 0 ? Mathf.Clamp01((float)current / total) : 0f;
            remainingUnderline.enabled = total > 0 && current <= config.LowEnemiesEmphasis;
        }

        /// <summary>Shows or immediately hides the encounter context, cancelling an old completion fade.</summary>
        public void SetEncounter(bool visible)
        {
            hiding = false; encounterVisible = visible;
            if (waveOpacity != null) waveOpacity.alpha = 1f;
            ApplyContext();
        }

        /// <summary>Fades the completed block for 300 ms; reduced motion hides it immediately.</summary>
        public void EncounterCompleted()
        {
            if (reduceMotion) { SetEncounter(false); return; }
            hiding = true; completedFade.Start(config.EncounterCompletedHideMs * 0.001f);
        }

        /// <summary>Snapshots labyrinth seal counters, including seals collected before the HUD was bound.</summary>
        public void SetSeals(int collected, int total)
        {
            if (sealsTextDirty || seals != collected || sealTotal != total)
            {
                sealsTextDirty = false;
                bool flash = seals >= 0 && collected > seals;
                seals = collected; sealTotal = total;
                HudText.Template(roomsText, sealsFormat, collected, total, buffer);
                if (flash && !reduceMotion) { sealFlashUntil = GamePause.UnscaledTime + 0.2f; roomsText.color = theme.MenuTheme.Focus; }
            }
            portalOpen = total > 0 && collected >= total;
            exitGroup.SetActive(inAraf && portalOpen);
        }

        /// <summary>Finishes any completion fade and seal flash when motion is reduced.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value;
            if (value && hiding) SetEncounter(false);
            if (value && roomsText != null && theme != null) { roomsText.color = theme.MenuTheme.Text; sealFlashUntil = 0f; }
        }

        private void ApplyContext()
        {
            if (theme == null || plate == null) return;
            bool combat = encounterVisible && !inAraf;
            waveGroup.SetActive(combat); arafGroup.SetActive(runPresent && inAraf);
            exitGroup.SetActive(runPresent && inAraf && portalOpen);
            Vector2 size = plate.sizeDelta;
            size.y = inAraf && runPresent ? theme.ArafPlateHeight : combat ? theme.CombatPlateHeight : theme.ExplorationPlateHeight;
            plate.sizeDelta = size;
            plate.gameObject.SetActive(runPresent || combat);
        }

        private void Update()
        {
            if (hiding)
            {
                completedFade.Tick(GamePause.UnscaledDeltaTime); waveOpacity.alpha = 1f - completedFade.Value;
                if (completedFade.Complete) SetEncounter(false);
            }
            if (sealFlashUntil > 0f && GamePause.UnscaledTime >= sealFlashUntil)
            { sealFlashUntil = 0f; roomsText.color = theme.MenuTheme.Text; }
        }
    }
}
