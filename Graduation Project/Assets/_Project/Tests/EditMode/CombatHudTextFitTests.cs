using NUnit.Framework;
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
    }
}
