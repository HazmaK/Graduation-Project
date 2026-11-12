using RetroFPS.Combat;
using RetroFPS.Presentation;
using RetroFPS.UI.Menu;
using RetroFPS.Weapons;
using TMPro;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Shows localized slot identity and the equipped resource; melee has no ammo controls.</summary>
    public sealed class WeaponAmmoHudView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private TMP_Text weaponName, slotText, currentText, warningText, meleeText;
        [SerializeField] private GameObject ammoGroup, warningChip;
        [SerializeField] private UnityEngine.UI.Image glyph, glyphOutline, fill, warningHighlight;
        private readonly char[] digits = new char[256];
        private string[] names;
        private string empty, warning;
        private PlayerAmmoInventory inventory;
        private AmmoType ammoType;
        private int previousCurrent = -1, previousMaximum = -1;
        private bool low, reduceMotion, textDirty;
        private float pickupUntil;

        /// <summary>Caches names, warning text and TMP capacity before binding events.</summary>
        public void Initialize(PlayerAmmoInventory source)
        {
            inventory = source;
            if (names != null || strings == null) return;
            names = new[] { strings.Get("hud.weapon.RepeaterArbalest"), strings.Get("hud.weapon.ExecutionerBlade"), strings.Get("hud.weapon.GothicShotgun") };
            empty = strings.Get("hud.ammo.empty");
            warning = strings.Get("hud.symbol.warning");
            HudText.Prewarm(weaponName, names[0]); HudText.Prewarm(slotText);
            HudText.Prewarm(currentText); HudText.Prewarm(warningText, empty);
            textDirty = true;
        }

        /// <summary>Updates name, silhouette and type colour, then snapshots the correct inventory pool.</summary>
        public void SetWeapon(WeaponBase weapon)
        {
            if (theme == null || names == null) return;
            gameObject.SetActive(weapon != null);
            if (weapon == null) return;
            int slot = (int)weapon.Slot;
            pickupUntil = 0f;
            weaponName.text = slot >= 0 && slot < names.Length ? names[slot] : string.Empty;
            HudText.Template(slotText, "[{0}]", slot + 1, 0, digits);
            ammoType = weapon.RequiredAmmoType;
            bool melee = ammoType == AmmoType.None;
            ammoGroup.SetActive(!melee && inventory != null); meleeText.gameObject.SetActive(melee);
            glyph.sprite = melee ? theme.WeaponBlade : ammoType == AmmoType.Bolts ? theme.WeaponBolt : theme.WeaponShell;
            if (glyphOutline != null) glyphOutline.sprite = glyph.sprite;
            glyph.color = melee ? theme.MenuTheme.Text : ammoType == AmmoType.Bolts ? theme.TypeBolts : theme.TypeShells;
            fill.color = glyph.color;
            previousCurrent = previousMaximum = -1;
            if (inventory != null && !melee) AmmoChanged(ammoType, inventory.GetCurrentAmmo(ammoType), inventory.GetMaxAmmo(ammoType));
            else { low = false; warningChip.SetActive(false); }
        }

        /// <summary>Ignores the inactive ammo pool and writes only changed values.</summary>
        public void AmmoChanged(AmmoType type, int current, int maximum)
        {
            if (type != ammoType || type == AmmoType.None || config == null) return;
            if (!textDirty && previousCurrent == current && previousMaximum == maximum) return;
            textDirty = false;
            previousCurrent = current; previousMaximum = maximum;
            HudText.Template(currentText, "{0}<size=24> / {1}</size>", current, maximum, digits);
            fill.fillAmount = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            low = HudLowStateRules.IsLowAmmo(current, maximum, config.LowAmmoRatio);
            warningChip.SetActive(low);
            warningText.text = HudLowStateRules.IsEmptyAmmo(current, maximum) ? empty : warning;
        }

        /// <summary>Starts a short glyph highlight only for an actual pickup of the equipped resource.</summary>
        public void AmmoCollected(AmmoType type, int added)
        {
            if (type == ammoType && added > 0 && !reduceMotion) pickupUntil = Time.unscaledTime + 0.15f;
        }

        /// <summary>Keeps warning backing opaque and makes its outline static under reduced motion.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value; pickupUntil = 0f;
            if (warningHighlight != null && theme != null) warningHighlight.color = theme.MenuTheme.Text;
            if (glyph != null && theme != null)
                glyph.color = ammoType == AmmoType.None ? theme.MenuTheme.Text : ammoType == AmmoType.Bolts ? theme.TypeBolts : theme.TypeShells;
        }

        private void Update()
        {
            if (theme == null || config == null) return;
            if (warningHighlight != null && low && !reduceMotion)
            {
                Color color = theme.MenuTheme.Text;
                color.a = 0.775f + 0.225f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f * config.LowPulseHz);
                warningHighlight.color = color;
            }
            if (pickupUntil > 0f)
            {
                glyph.color = Time.unscaledTime < pickupUntil ? theme.MenuTheme.Focus : ammoType == AmmoType.Bolts ? theme.TypeBolts : theme.TypeShells;
                if (Time.unscaledTime >= pickupUntil) pickupUntil = 0f;
            }
        }
    }
}
