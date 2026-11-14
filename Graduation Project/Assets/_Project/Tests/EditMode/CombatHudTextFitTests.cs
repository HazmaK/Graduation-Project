using NUnit.Framework;
using System.Globalization;
using RetroFPS.Settings;
using RetroFPS.Tests.EditMode;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests
{
    /// <summary>
    /// Regression: the health number (48px) sat in a 49px Ellipsis box; TMP hides a line that
    /// does not fit vertically, so the HUD showed no health value at all.
    /// </summary>
    public class CombatHudTextFitTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab";
        private const float MinLineHeightRatio = 1.2f;

        [Test]
        public void ClippingLabels_AreTallEnoughForOneLine()
        {
            // Arrange
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) Assert.Ignore("Run RetroFPS/Build Combat HUD first; the prefab is generated.");
            TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);

            // Act + Assert
            foreach (TMP_Text label in labels)
            {
                bool clips = label.overflowMode == TextOverflowModes.Ellipsis || label.overflowMode == TextOverflowModes.Truncate;
                if (!clips) continue;
                float height = ((RectTransform)label.transform).rect.height;
                Assert.GreaterOrEqual(height, label.fontSize * MinLineHeightRatio,
                    label.name + " under " + label.transform.parent.name + " cannot fit one line and would render blank.");
            }
        }

        /// <summary>Measures authored boxes with real TMP layout for both complete translations.</summary>
        [TestCase(LanguageSetting.Turkish)]
        [TestCase(LanguageSetting.English)]
        public void LocalizedStaticAndDynamicCopyFitsItsAuthoredBox(LanguageSetting language)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "The authored CombatHud prefab is required.");
            var table = LocalizationHudTestContext.CreateTable();
            var canvas = new GameObject("HUD text fit", typeof(RectTransform), typeof(Canvas));
            var textObject = new GameObject("Measured text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvas.transform, false);
            var measured = textObject.GetComponent<TextMeshProUGUI>();
            try
            {
                foreach (LocalizedText localized in prefab.GetComponentsInChildren<LocalizedText>(true))
                {
                    string key = (string)LocalizationHudTestContext.Get(localized, "key");
                    TMP_Text source = (TMP_Text)LocalizationHudTestContext.Get(localized, "label");
                    if (source == null) source = localized.GetComponent<TMP_Text>();
                    // Dynamic labels have no static key and are measured against their templates below.
                    if (string.IsNullOrEmpty(key)) continue;
                    CheckFit(source, table.Get(key, language), measured, key == "hud.araf.warning" ? 2 : 1);
                }
                var ability = prefab.GetComponentInChildren<AbilityHudView>(true);
                CheckFit((TMP_Text)LocalizationHudTestContext.Get(ability, "dashCurrent"),
                    string.Format(CultureInfo.InvariantCulture, table.Get("hud.dash.counter", language), 3, 3), measured, 1);
                FitKeys(ability, "parryState", "hud.parry.ready", "hud.parry.active", "hud.parry.cooldown", "hud.parry.success");
                FitKeys(ability, "hookState", "hud.hook.ready", "hud.hook.cooldown");
                var weapon = prefab.GetComponentInChildren<WeaponAmmoHudView>(true);
                FitKeys(weapon, "weaponName", "hud.weapon.RepeaterArbalest", "hud.weapon.ExecutionerBlade", "hud.weapon.GothicShotgun");
                FitKeys(weapon, "warningText", "hud.ammo.empty", "hud.symbol.warning");
                var encounter = prefab.GetComponentInChildren<EncounterRunHudView>(true);
                FitKeys(encounter, "waveText", "hud.wave.counter"); FitKeys(encounter, "remainingText", "hud.wave.remaining");
                FitKeys(encounter, "roomsText", "hud.araf.rooms"); FitKeys(encounter, "chargeText", "hud.araf.ready", "hud.araf.used");
                var banner = prefab.GetComponentInChildren<HudBannerView>(true);
                FitKeys(banner, "firstLine", "hud.banner.arenaSealed", "hud.banner.arenaCleared", "hud.banner.wave", "hud.banner.waveCleared");
                FitKeys(banner, "secondLine", "hud.banner.wave", "hud.banner.waveCleared");

                void FitKeys(object view, string field, params string[] keys)
                {
                    TMP_Text source = (TMP_Text)LocalizationHudTestContext.Get(view, field);
                    foreach (string key in keys)
                        CheckFit(source, string.Format(CultureInfo.InvariantCulture, table.Get(key, language), 999, 999), measured, 1);
                }
            }
            finally { Object.DestroyImmediate(canvas); Object.DestroyImmediate(table); }
        }

        private static void CheckFit(TMP_Text source, string text, TMP_Text measured, int maxLines)
        {
            Assert.That(source, Is.Not.Null);
            measured.rectTransform.sizeDelta = source.rectTransform.rect.size;
            measured.font = source.font; measured.fontSize = source.fontSize; measured.fontStyle = source.fontStyle;
            measured.enableAutoSizing = false; measured.richText = source.richText;
            measured.characterSpacing = source.characterSpacing; measured.wordSpacing = source.wordSpacing;
            measured.lineSpacing = source.lineSpacing; measured.margin = source.margin;
            measured.textWrappingMode = source.textWrappingMode; measured.overflowMode = TextOverflowModes.Overflow;
            measured.text = text; measured.ForceMeshUpdate(true, true);
            string detail = source.transform.parent.name + "/" + source.name + ": " + text;
            Vector2 preferred = measured.GetPreferredValues(text, source.rectTransform.rect.width, float.PositiveInfinity);
            Assert.That(preferred.x, Is.LessThanOrEqualTo(source.rectTransform.rect.width + 1f), detail + " clips horizontally");
            Assert.That(preferred.y, Is.LessThanOrEqualTo(source.rectTransform.rect.height + 1f), detail + " clips vertically");
            Assert.That(measured.textInfo.lineCount, Is.LessThanOrEqualTo(maxLines), detail + " exceeds its line budget");
            Assert.That(source.fontSize, Is.GreaterThanOrEqualTo(20f), detail + " violates the HUD font floor");
        }
    }
}
