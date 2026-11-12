using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace RetroFPS.Tests
{
    /// <summary>
    /// Regression: the generated menu EventSystem prefab once saved every UI action reference as null,
    /// so the main menu ignored mouse and keyboard entirely.
    /// </summary>
    public class MenuEventSystemPrefabTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/MainMenu/MenuEventSystem.prefab";

        [Test]
        public void GeneratedPrefab_HasEveryUiActionReferenceAssigned()
        {
            // Arrange
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) Assert.Ignore("Run RetroFPS/Build Main Menu first; the prefab is generated.");
            var module = prefab.GetComponent<InputSystemUIInputModule>();

            // Act
            var serialized = new SerializedObject(module);

            // Assert
            Assert.IsNotNull(module, "MenuEventSystem has no InputSystemUIInputModule.");
            string[] fields = { "m_PointAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_LeftClickAction" };
            foreach (string field in fields)
                Assert.IsNotNull(serialized.FindProperty(field).objectReferenceValue, field + " is not assigned.");
        }
    }
}
