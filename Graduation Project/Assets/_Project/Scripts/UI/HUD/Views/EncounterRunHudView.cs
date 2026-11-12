using RetroFPS.Presentation;
using RetroFPS.Run;
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
        private string ready, used, waveFormat, remainingFormat, roomsFormat;
        private bool initialized, inAraf, encounterVisible, hiding, reduceMotion, runPresent;
        private int wave = -1, waveTotal = -1, remaining = -1, enemyTotal = -1, rooms = -1, roomTotal = -1;
        private bool exit;
        private bool waveTextDirty, remainingTextDirty, roomsTextDirty;
        private float roomFlashUntil;

        /// <summary>Resolves state copy and prewarms numeric pieces before gameplay events.</summary>
        public void Initialize()
        {
            if (initialized || strings == null) return;
            initialized = true;
            waveFormat = strings.Get("hud.wave.counter"); remainingFormat = strings.Get("hud.wave.remaining");
            roomsFormat = strings.Get("hud.araf.rooms");
            ready = strings.Get("hud.araf.ready"); used = strings.Get("hud.araf.used");
            HudText.Prewarm(waveText); HudText.Prewarm(remainingText);
            HudText.Prewarm(roomsText); HudText.Prewarm(chargeText, used);
            waveTextDirty = remainingTextDirty = roomsTextDirty = true;
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

        /// <summary>Snapshots labyrinth counters, including rooms revealed before the HUD was bound.</summary>
        public void SetRooms(int revealed, int total, bool exitRevealed)
        {
            if (roomsTextDirty || rooms != revealed || roomTotal != total)
            {
                roomsTextDirty = false;
                bool flash = rooms >= 0 && revealed > rooms;
                rooms = revealed; roomTotal = total;
                HudText.Template(roomsText, roomsFormat, revealed, total, buffer);
                if (flash && !reduceMotion) { roomFlashUntil = Time.unscaledTime + 0.2f; roomsText.color = theme.MenuTheme.Focus; }
            }
            exit = exitRevealed;
            exitGroup.SetActive(inAraf && exit);
        }

        /// <summary>Finishes any completion fade and room flash when motion is reduced.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value;
            if (value && hiding) SetEncounter(false);
            if (value && roomsText != null && theme != null) { roomsText.color = theme.MenuTheme.Text; roomFlashUntil = 0f; }
        }

        private void ApplyContext()
        {
            if (theme == null || plate == null) return;
            bool combat = encounterVisible && !inAraf;
            waveGroup.SetActive(combat); arafGroup.SetActive(runPresent && inAraf);
            exitGroup.SetActive(runPresent && inAraf && exit);
            Vector2 size = plate.sizeDelta;
            size.y = inAraf && runPresent ? theme.ArafPlateHeight : combat ? theme.CombatPlateHeight : theme.ExplorationPlateHeight;
            plate.sizeDelta = size;
            plate.gameObject.SetActive(runPresent || combat);
        }

        private void Update()
        {
            if (hiding)
            {
                completedFade.Tick(Time.unscaledDeltaTime); waveOpacity.alpha = 1f - completedFade.Value;
                if (completedFade.Complete) SetEncounter(false);
            }
            if (roomFlashUntil > 0f && Time.unscaledTime >= roomFlashUntil)
            { roomFlashUntil = 0f; roomsText.color = theme.MenuTheme.Text; }
        }
    }
}
