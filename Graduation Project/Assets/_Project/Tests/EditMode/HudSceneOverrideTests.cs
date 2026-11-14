using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace RetroFPS.Tests
{
    /// <summary>
    /// Regression: rebuilding the HUD inside Player.prefab once left TestLevel_Gauntlet with scene-level
    /// m_overflowMode overrides on the nested HUD labels, forcing Ellipsis back over the builder's
    /// Overflow choice (which had blanked the 48px health/ammo numbers before).
    /// </summary>
    public class HudSceneOverrideTests
    {
        private static readonly string[] GameplayScenes =
        {
            "Assets/Scenes/TestLevel_Gauntlet.unity",
            "Assets/Scenes/SampleScene.unity",
        };

        [Test]
        public void GameplayScenes_DoNotOverridePlayerHudTextOverflow()
        {
            // Arrange
            string playerGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Prefabs/Player/Player.prefab");
            Assert.IsNotEmpty(playerGuid, "Player.prefab not found.");

            foreach (string scene in GameplayScenes)
            {
                if (!File.Exists(scene)) continue;

                // Act
                string[] lines = File.ReadAllLines(scene);

                // Assert
                for (int i = 1; i < lines.Length; i++)
                {
                    if (!lines[i].Contains("propertyPath: m_overflowMode")) continue;
                    Assert.IsFalse(lines[i - 1].Contains(playerGuid),
                        scene + " overrides a Player.prefab HUD label's overflow mode at line " + (i + 1) +
                        "; revert the scene override so the builder's value applies.");
                }
            }
        }
    }
}
