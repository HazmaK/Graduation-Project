using NUnit.Framework;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests
{
    /// <summary>
    /// Regression: the ammo warning chip's text had both a LocalizedText ("!") and WeaponAmmoHudView
    /// writing BOŞ/EMPTY; every language change let LocalizedText overwrite the word with "!".
    /// A text that a HUD view writes itself must not also carry a LocalizedText.
    /// </summary>
    public class HudTextOwnershipTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab";

        [Test]
        public void AmmoWarningText_IsOwnedOnlyByWeaponAmmoView()
        {
            // Arrange
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) Assert.Ignore("Run RetroFPS/Build Combat HUD first; the prefab is generated.");
            var view = prefab.GetComponentInChildren<WeaponAmmoHudView>(true);
            Assert.IsNotNull(view, "CombatHud has no WeaponAmmoHudView.");

            // Act
            var warning = new SerializedObject(view).FindProperty("warningText").objectReferenceValue as TMP_Text;

            // Assert
            Assert.IsNotNull(warning, "WeaponAmmoHudView.warningText is not wired.");
            Assert.IsNull(warning.GetComponent<LocalizedText>(),
                "The view-owned ammo warning text also has a LocalizedText, which overwrites EMPTY/BOŞ on language change.");
        }
    }
}
