using RetroFPS.Presentation;
using RetroFPS.UI.Menu;
using RetroFPS.Weapons;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Renders prebuilt dash pips and quantized parry/hook readiness in reserved cells.</summary>
    public sealed class AbilityHudView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private RectTransform pipRoot;
        [SerializeField] private UnityEngine.UI.Image pipTemplate;
        [SerializeField] private TMP_Text dashCurrent, parryState, hookState;
        [SerializeField] private CanvasGroup parryCell, hookCell;
        [SerializeField] private UnityEngine.UI.Image parryFill, hookFill, activeChip;
        private PlayerMovement movement;
        private PlayerParryController parry;
        private GothicShotgunWeapon shotgun;
        private UnityEngine.UI.Image[] pips;
        private CooldownDisplayCache dashCache, parryCache, hookCache;
        private readonly char[] digits = new char[256];
        private string ready, active, cooldown, success, hookReady, hookCooldown, dashFormat;
        private int charges = -1, maximum = -1;
        private float successUntil, dashFlashUntil;
        private bool reduceMotion, showingSuccess, dashTextDirty, stateTextDirty;

        /// <summary>Creates exactly the authored number of pips before any recharge/dash event.</summary>
        public void Initialize(PlayerMovement source, PlayerParryController shield)
        {
            movement = source; parry = shield;
            if (config == null || theme == null || strings == null) return;
            if (pipRoot != null) pipRoot.gameObject.SetActive(movement != null);
            int count = movement != null ? Mathf.Max(0, movement.MaxDashCharges) : 0;
            if (pips != null && pips.Length == count) return;
            dashFormat = strings.Get("hud.dash.counter");
            ready = strings.Get("hud.parry.ready"); active = strings.Get("hud.parry.active");
            cooldown = strings.Get("hud.parry.cooldown"); success = strings.Get("hud.parry.success");
            hookReady = strings.Get("hud.hook.ready"); hookCooldown = strings.Get("hud.hook.cooldown");
            dashCache = new CooldownDisplayCache(config.CooldownFillSteps);
            parryCache = new CooldownDisplayCache(config.CooldownFillSteps);
            hookCache = new CooldownDisplayCache(config.CooldownFillSteps);
            HudText.Prewarm(dashCurrent);
            HudText.Prewarm(parryState, success); HudText.Prewarm(hookState, hookCooldown);
            dashTextDirty = stateTextDirty = true;
            pips = new UnityEngine.UI.Image[count];
            for (int i = 0; i < count; i++)
            {
                UnityEngine.UI.Image pip = Instantiate(pipTemplate, pipRoot);
                pip.gameObject.SetActive(true);
                // Fit authored N in the reserved cell; labels retain the complete numeric count.
                float pitch = Mathf.Min(theme.AbilityGlyphSize.x + theme.PipGap, 104f / Mathf.Max(1, count));
                Vector2 position = pip.rectTransform.anchoredPosition; position.x = i * pitch;
                pip.rectTransform.anchoredPosition = position;
                pips[i] = pip.transform.Find("Fill").GetComponent<UnityEngine.UI.Image>();
            }
            if (pipTemplate != null) pipTemplate.gameObject.SetActive(false);
        }

        /// <summary>Sets a cached shotgun reference only on weapon changes.</summary>
        public void SetWeapon(WeaponBase weapon) { shotgun = weapon as GothicShotgunWeapon; Poll(); }

        /// <summary>Snapshots charges without creating or moving any layout cells.</summary>
        public void SetDash(int current, int max)
        {
            if (!dashTextDirty && current == charges && max == maximum) return;
            dashTextDirty = false;
            if (charges >= 0 && current != charges && !reduceMotion) dashFlashUntil = Time.unscaledTime + 0.12f;
            charges = current; maximum = max;
            HudText.Template(dashCurrent, dashFormat, current, max, digits);
            RenderPips();
        }

        /// <summary>Coalesces the success word in the parry slot for 250 unscaled milliseconds.</summary>
        public void ParrySucceeded(bool projectile)
        {
            if (config == null) return;
            successUntil = Time.unscaledTime + config.ParrySuccessMs * 0.001f;
            showingSuccess = true;
            if (parryState != null) { parryState.text = success; parryState.color = theme.MenuTheme.Text; }
            if (activeChip != null) activeChip.enabled = false;
            if (parryFill != null && !reduceMotion) parryFill.color = theme.MenuTheme.Focus;
        }

        /// <summary>Removes flashes immediately; the success word keeps its original expiry.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value; dashFlashUntil = 0f;
            if (parryFill != null && theme != null) parryFill.color = parry != null && parry.IsParrying ? theme.MenuTheme.Focus : theme.MenuTheme.Text;
            RenderPips();
        }

        private void Update() => Poll();
        private void Poll()
        {
            if (dashCache == null) return;
            if (movement != null && dashCache.Update(movement.DashRechargeNormalized)) RenderPips();
            if (dashFlashUntil > 0f && Time.unscaledTime >= dashFlashUntil) { dashFlashUntil = 0f; RenderPips(); }
            bool expired = showingSuccess && Time.unscaledTime >= successUntil;
            if (expired) showingSuccess = false;
            bool visible = parry != null && parry.ShieldEnabled;
            bool changed = parryCache.Update(parry != null && parry.IsOnCooldown ? 1f - parry.CooldownNormalized : 1f,
                parry != null && parry.IsParrying, visible);
            if (stateTextDirty || changed || expired)
            {
                if (parryCell != null) parryCell.alpha = visible ? 1f : 0f;
                if (!showingSuccess)
                {
                    if (parryState != null)
                    {
                        parryState.text = parryCache.Active ? active : parryCache.Ready ? ready : cooldown;
                        parryState.color = parryCache.Active ? theme.MenuTheme.Background : theme.MenuTheme.Text;
                    }
                    if (activeChip != null) activeChip.enabled = parryCache.Active;
                    if (parryFill != null) parryFill.color = parryCache.Active ? theme.MenuTheme.Focus : theme.MenuTheme.Text;
                }
                if (parryFill != null) parryFill.fillAmount = parryCache.Active ? 1f : parryCache.Fill;
            }
            bool hookChanged = hookCache.Update(shotgun != null ? 1f - shotgun.HookCooldownNormalized : 1f,
                false, shotgun != null);
            if (stateTextDirty || hookChanged)
            {
                if (hookCell != null) hookCell.alpha = shotgun != null ? 1f : 0f;
                if (hookState != null) hookState.text = shotgun != null && shotgun.CanFireAlt ? hookReady : hookCooldown;
                if (hookFill != null) hookFill.fillAmount = hookCache.Fill;
            }
            stateTextDirty = false;
        }

        private void RenderPips()
        {
            if (pips == null || theme == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].fillAmount = i < charges ? 1f : i == charges && charges < maximum && dashCache != null ? dashCache.Fill : 0f;
                pips[i].color = !reduceMotion && dashFlashUntil > Time.unscaledTime ? theme.MenuTheme.Focus : theme.MenuTheme.Text;
            }
        }
    }
}
