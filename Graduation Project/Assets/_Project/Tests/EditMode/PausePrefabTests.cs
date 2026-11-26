using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.Presentation;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace RetroFPS.Tests
{
    public sealed class PausePrefabTests
    {
        private const string PausePath = "Assets/_Project/Prefabs/UI/Pause/PauseMenu.prefab";
        private const string SharedPath = "Assets/_Project/Prefabs/UI/Shared/SettingsPanel.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string HudPath = "Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab";
        private static GameObject Pause()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePath);
            if (prefab == null) Assert.Ignore("Pause prefab has not been generated yet. Run RetroFPS/Build Pause Menu, then rerun; no Ignore is allowed after generation.");
            return prefab;
        }
        [Test]
        public void BothMenusNestTheExactSharedAssetAndHaveOneOwner()
        {
            GameObject pause = Pause();
            GameObject shared = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPath);
            GameObject main = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MainMenu/MainMenuCanvas.prefab");
            Assert.That(shared, Is.Not.Null); Assert.That(main, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(main.transform.Find("SafeArea/SettingsPanel").gameObject), Is.EqualTo(shared));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(pause.transform.Find("SafeArea/SettingsPanel").gameObject), Is.EqualTo(shared));
            Assert.That(pause.GetComponentsInChildren<PauseMenuController>(true).Length, Is.EqualTo(1));
            Assert.That(pause.GetComponentsInChildren<MainMenuController>(true), Is.Empty);
            Assert.That(pause.GetComponentsInChildren<PauseRuntime>(true), Is.Empty);
            Assert.That(pause.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(pause.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
            Assert.That(pause.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            var scaler = pause.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            var router = new SerializedObject(pause.GetComponent<MenuInputRouter>());
            Assert.That(router.FindProperty("pauseHost").boolValue, Is.True);
            Assert.That(router.FindProperty("controller").objectReferenceValue, Is.EqualTo(pause.GetComponent<PauseMenuController>()));
            Assert.That(router.FindProperty("cancel").objectReferenceValue, Is.Null);
            Assert.That(router.FindProperty("rightClick").objectReferenceValue, Is.Null);
        }
        [Test]
        public void PlayerHasOnePauseHudAndPersistentInputReferencesAfterReload()
        {
            Pause();
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Assert.That(player.GetComponents<PauseRuntime>().Length, Is.EqualTo(1));
                Assert.That(player.GetComponents<PauseBindings>().Length, Is.EqualTo(1));
                Assert.That(player.GetComponents<HudBindings>().Length, Is.EqualTo(1));
                Assert.That(player.GetComponentsInChildren<PauseMenuController>(true).Length, Is.EqualTo(1));
                Assert.That(player.GetComponentsInChildren<HudEventSystemGuard>(true).Length, Is.EqualTo(1));
                Assert.That(player.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
                var bindings = new SerializedObject(player.GetComponent<PauseBindings>());
                foreach (string field in new[] { "runtime", "pauseMenu", "health", "eventSystemGuard", "trueDeath" })
                    Assert.That(bindings.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                InputSystemUIInputModule module = player.GetComponentInChildren<InputSystemUIInputModule>(true);
                Assert.That(module, Is.Not.Null); Assert.That(module.deselectOnBackgroundClick, Is.False);
                var data = new SerializedObject(module);
                Assert.That(data.FindProperty("m_ActionsAsset").objectReferenceValue, Is.Not.Null);
                foreach (string field in new[] { "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_ScrollWheelAction" })
                {
                    var reference = data.FindProperty(field).objectReferenceValue as InputActionReference;
                    Assert.That(reference, Is.Not.Null, field);
                    Assert.That(AssetDatabase.Contains(reference), Is.True, field);
                    Assert.That(reference.action, Is.Not.Null, field);
                    Assert.That(reference.action.actionMap.name, Is.EqualTo("UI"));
                }
                MenuInputRouter router = player.GetComponentInChildren<PauseMenuController>(true).GetComponent<MenuInputRouter>();
                Assert.That(new SerializedObject(router).FindProperty("eventSystem").objectReferenceValue, Is.EqualTo(module.GetComponent<EventSystem>()));
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        [TestCase("CombatHudBuilder", "WirePlayer")]
        [TestCase("PauseMenuBuilder", "EnsurePlayerPause")]
        public void PlayerWiringTwiceRemovesDuplicatesAndPreservesOriginalReferences(string builderName, string methodName)
        {
            GameObject pausePrefab = Pause();
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Assert.That(hudPrefab, Is.Not.Null);
            string copyPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Tests/EditMode/PauseWiringTest.prefab");
            GameObject player = null;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(PlayerPath, copyPath), Is.True);
                string guid = AssetDatabase.AssetPathToGUID(copyPath);
                player = PrefabUtility.LoadPrefabContents(copyPath);
                var originalBindings = player.GetComponent<HudBindings>();
                var originalRuntime = player.GetComponent<PauseRuntime>();
                var originalPauseBindings = player.GetComponent<PauseBindings>();
                var originalPause = player.GetComponentInChildren<PauseMenuController>(true);
                var originalGuard = player.GetComponentInChildren<HudEventSystemGuard>(true);
                var originalHud = (GameObject)new SerializedObject(originalBindings).FindProperty("hud").objectReferenceValue;
                HudConfig originalConfig = originalBindings.Config;
                PrefabUtility.InstantiatePrefab(hudPrefab, player.transform);
                PrefabUtility.InstantiatePrefab(pausePrefab, player.transform);
                var debris = new GameObject("DuplicateBindings");
                debris.SetActive(false);
                debris.transform.SetParent(player.transform, false);
                debris.AddComponent<HudBindings>();
                debris.AddComponent<PauseRuntime>();
                debris.AddComponent<PauseBindings>();
                System.Type builder = System.Type.GetType("RetroFPS.Editor." + builderName + ", RetroFPS.Editor", true);
                var wire = builder.GetMethod(methodName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                Assert.That(wire, Is.Not.Null);
                for (int pass = 0; pass < 2; pass++)
                {
                    wire.Invoke(null, new object[] { player });
                    AssertSinglePlayerOwners(player);
                    Assert.That(player.GetComponent<HudBindings>(), Is.EqualTo(originalBindings));
                    Assert.That(player.GetComponent<PauseRuntime>(), Is.EqualTo(originalRuntime));
                    Assert.That(player.GetComponent<PauseBindings>(), Is.EqualTo(originalPauseBindings));
                    Assert.That(player.GetComponentInChildren<PauseMenuController>(true), Is.EqualTo(originalPause));
                    Assert.That(player.GetComponentInChildren<HudEventSystemGuard>(true), Is.EqualTo(originalGuard));
                    Assert.That(new SerializedObject(originalBindings).FindProperty("hud").objectReferenceValue, Is.EqualTo(originalHud));
                    Assert.That(originalBindings.Config, Is.EqualTo(originalConfig));
                    var bindings = new SerializedObject(originalPauseBindings);
                    Assert.That(bindings.FindProperty("pauseMenu").objectReferenceValue, Is.EqualTo(originalPause));
                    Assert.That(bindings.FindProperty("eventSystemGuard").objectReferenceValue, Is.EqualTo(originalGuard));
                }
                PrefabUtility.SaveAsPrefabAsset(player, copyPath, out bool saved);
                Assert.That(saved, Is.True);
                PrefabUtility.UnloadPrefabContents(player);
                player = null;
                player = PrefabUtility.LoadPrefabContents(copyPath);
                AssertSinglePlayerOwners(player);
                Assert.That(AssetDatabase.AssetPathToGUID(copyPath), Is.EqualTo(guid));
                Assert.That(player.GetComponent<HudBindings>().Config, Is.EqualTo(originalConfig));
                var reloaded = new SerializedObject(player.GetComponent<PauseBindings>());
                Assert.That(reloaded.FindProperty("runtime").objectReferenceValue, Is.EqualTo(player.GetComponent<PauseRuntime>()));
                Assert.That(reloaded.FindProperty("pauseMenu").objectReferenceValue, Is.EqualTo(player.GetComponentInChildren<PauseMenuController>(true)));
                Assert.That(reloaded.FindProperty("eventSystemGuard").objectReferenceValue, Is.EqualTo(player.GetComponentInChildren<HudEventSystemGuard>(true)));
            }
            finally
            {
                if (player != null) PrefabUtility.UnloadPrefabContents(player);
                AssetDatabase.DeleteAsset(copyPath);
            }
        }

        private static void AssertSinglePlayerOwners(GameObject player)
        {
            Assert.That(player.GetComponentsInChildren<HudBindings>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<PauseRuntime>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<PauseBindings>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<PauseMenuController>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<HudEventSystemGuard>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
            foreach (string path in new[] { HudPath, PausePath })
            {
                int count = 0;
                foreach (Transform child in player.transform)
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == path) count++;
                Assert.That(count, Is.EqualTo(1), path);
            }
        }
        /// <summary>Legacy migration adds one button and preserves every existing button/dialog reference on reruns.</summary>
        [Test]
        public void NewRunUpgradeIsIdempotentAndPreservesExistingObjects()
        {
            Pause();
            GameObject root = PrefabUtility.LoadPrefabContents(PausePath);
            StringTable table = null;
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var builder = System.Type.GetType("RetroFPS.Editor.PauseMenuBuilder, RetroFPS.Editor", true);
            var mainBuilder = System.Type.GetType("RetroFPS.Editor.MainMenuBuilder, RetroFPS.Editor", true);
            object oldTheme = builder.GetField("theme", flags).GetValue(null);
            object oldMainTheme = mainBuilder.GetField("theme", flags).GetValue(null);
            object oldMainStrings = mainBuilder.GetField("strings", flags).GetValue(null);
            string guid = AssetDatabase.AssetPathToGUID(PausePath);
            try
            {
                var controller = root.GetComponent<PauseMenuController>();
                var data = new SerializedObject(controller);
                var theme = (MenuTheme)data.FindProperty("theme").objectReferenceValue;
                table = Object.Instantiate((StringTable)data.FindProperty("strings").objectReferenceValue);
                builder.GetMethod("UpsertStrings").Invoke(null, new object[] { table });
                builder.GetField("theme", flags).SetValue(null, theme);
                mainBuilder.GetMethod("UseSharedPresentation", flags).Invoke(null, new object[] { theme, table });
                Transform menu = root.transform.Find("SafeArea/Root/MenuList");
                Object[] originals = { menu.Find("Resume").GetComponent<UnityEngine.UI.Selectable>(),
                    menu.Find("Settings").GetComponent<UnityEngine.UI.Selectable>(), menu.Find("MainMenu").GetComponent<UnityEngine.UI.Selectable>() };
                Object dialog = data.FindProperty("mainMenuDialog").objectReferenceValue;
                Transform newRun = menu.Find("NewRun");
                if (newRun != null) Object.DestroyImmediate(newRun.gameObject);
                var items = data.FindProperty("rootItems"); items.arraySize = 3;
                for (int i = 0; i < 3; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = originals[i];
                data.FindProperty("sceneLeavePrompt").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                var upgrade = builder.GetMethod("UpgradeNewRunMenu", flags);
                Assert.That(upgrade.Invoke(null, new object[] { root }), Is.EqualTo(true));
                Object added = menu.Find("NewRun").GetComponent<UnityEngine.UI.Selectable>();
                Assert.That(upgrade.Invoke(null, new object[] { root }), Is.EqualTo(false));
                data.Update(); items = data.FindProperty("rootItems");
                Assert.That(items.arraySize, Is.EqualTo(4));
                Assert.That(menu.childCount, Is.EqualTo(4));
                Assert.That(items.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(originals[0]));
                Assert.That(items.GetArrayElementAtIndex(1).objectReferenceValue, Is.EqualTo(added));
                Assert.That(items.GetArrayElementAtIndex(2).objectReferenceValue, Is.EqualTo(originals[1]));
                Assert.That(items.GetArrayElementAtIndex(3).objectReferenceValue, Is.EqualTo(originals[2]));
                Assert.That(data.FindProperty("mainMenuDialog").objectReferenceValue, Is.EqualTo(dialog));
                Assert.That(AssetDatabase.AssetPathToGUID(PausePath), Is.EqualTo(guid));
            }
            finally
            {
                builder.GetField("theme", flags).SetValue(null, oldTheme);
                mainBuilder.GetField("theme", flags).SetValue(null, oldMainTheme);
                mainBuilder.GetField("strings", flags).SetValue(null, oldMainStrings);
                PrefabUtility.UnloadPrefabContents(root);
                if (table != null) Object.DestroyImmediate(table);
            }
        }
        /// <summary>The shared legacy upgrade preserves controls and serializes identically on its second pass.</summary>
        [Test]
        public void PostFxUpgradePreservesRowsFitsPanelAndIsIdempotent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPath);
            if (prefab == null) Assert.Ignore("Generate shared settings before testing the prefab upgrade.");
            string copyPath = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Tests/EditMode/PostFxSettingsUpgrade.prefab");
            string tablePath = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Tests/EditMode/PostFxSettingsUpgradeStrings.asset");
            GameObject root = null;
            StringTable table = null;
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var builder = System.Type.GetType("RetroFPS.Editor.SharedSettingsPanelBuilder, RetroFPS.Editor", true);
            var mainBuilder = System.Type.GetType("RetroFPS.Editor.MainMenuBuilder, RetroFPS.Editor", true);
            object oldMainTheme = mainBuilder.GetField("theme", flags).GetValue(null);
            object oldMainStrings = mainBuilder.GetField("strings", flags).GetValue(null);
            try
            {
                Assert.That(AssetDatabase.CopyAsset(SharedPath, copyPath), Is.True);
                root = PrefabUtility.LoadPrefabContents(copyPath);
                root.SetActive(false);
                SettingsPanelView view = root.GetComponent<SettingsPanelView>();
                var data = new SerializedObject(view);
                table = Object.Instantiate((StringTable)data.FindProperty("strings").objectReferenceValue);
                table.PopulateBilingualDefaults();
                AssetDatabase.CreateAsset(table, tablePath);
                MenuTheme theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/_Project/Data/UI/MenuTheme_KanVeKemik.asset");
                mainBuilder.GetMethod("UseSharedPresentation", flags).Invoke(null, new object[] { theme, table });
                Transform content = root.transform.Find("Content");
                Object[] original = { content.Find("Language").gameObject, content.Find("Cycle2").gameObject, content.Find("Back").gameObject };
                foreach (string name in new[] { "Cycle3", "Slider5", "Slider6" })
                {
                    Transform added = content.Find(name);
                    if (added != null) Object.DestroyImmediate(added.gameObject);
                }
                var upgrade = builder.GetMethod("UpgradePostFxRows", flags);
                var save = builder.GetMethod("Save", flags);
                upgrade.Invoke(null, new object[] { root });
                save.Invoke(null, new object[] { root, copyPath });
                string first = System.IO.File.ReadAllText(copyPath);
                Object addedCycle = content.Find("Cycle3").gameObject;
                upgrade.Invoke(null, new object[] { root });
                save.Invoke(null, new object[] { root, copyPath });
                Assert.That(System.IO.File.ReadAllText(copyPath), Is.EqualTo(first));
                Assert.That(content.Find("Language").gameObject, Is.EqualTo(original[0]));
                Assert.That(content.Find("Cycle2").gameObject, Is.EqualTo(original[1]));
                Assert.That(content.Find("Back").gameObject, Is.EqualTo(original[2]));
                Assert.That(content.Find("Cycle3").gameObject, Is.EqualTo(addedCycle));
                string[] names = { "Language", "Difficulty", "Slider0", "Slider1", "Slider2", "Slider3", "Slider4", "Cycle0", "Cycle1", "Cycle2", "Cycle3", "Slider5", "Slider6", "Back" };
                Assert.That(view.OrderedSelectables.Length, Is.EqualTo(names.Length));
                float previousBottom = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    var item = view.OrderedSelectables[i];
                    Assert.That(item.name, Is.EqualTo(names[i]));
                    Assert.That(item.navigation.selectOnDown, Is.EqualTo(view.OrderedSelectables[(i + 1) % names.Length]));
                    Assert.That(item.navigation.selectOnUp, Is.EqualTo(view.OrderedSelectables[(i + names.Length - 1) % names.Length]));
                    RectTransform rect = (RectTransform)item.transform;
                    float top = -rect.anchoredPosition.y - rect.rect.height * 0.5f;
                    float bottom = top + rect.rect.height;
                    Assert.That(top, Is.GreaterThanOrEqualTo(previousBottom));
                    Assert.That(bottom, Is.LessThanOrEqualTo(((RectTransform)content).rect.height));
                    previousBottom = bottom;
                }
                data.Update();
                foreach (string field in new[] { "difficulty", "postProcessing", "brightness", "brightnessValue", "screenEffects", "screenEffectsValue" })
                    Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                Assert.That(content.Find("Slider5").GetComponent<MenuSlider>().maxValue, Is.EqualTo(20));
                Assert.That(content.Find("Slider6").GetComponent<MenuSlider>().maxValue, Is.EqualTo(10));
                string[] rowNames = { "Difficulty", "Cycle3", "Slider5", "Slider6" };
                string[] keys = { "settings.difficulty", "settings.post_processing", "settings.brightness", "settings.screen_effects" };
                for (int i = 0; i < rowNames.Length; i++)
                {
                    var label = new SerializedObject(content.Find(rowNames[i] + "/Label").GetComponent<LocalizedText>());
                    Assert.That(label.FindProperty("key").stringValue, Is.EqualTo(keys[i]));
                }
            }
            finally
            {
                mainBuilder.GetField("theme", flags).SetValue(null, oldMainTheme);
                mainBuilder.GetField("strings", flags).SetValue(null, oldMainStrings);
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.DeleteAsset(copyPath);
                if (AssetDatabase.LoadAssetAtPath<StringTable>(tablePath) != null) AssetDatabase.DeleteAsset(tablePath);
                else if (table != null) Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void ModalNavigationWrapsAndSettingsStartAtLanguage()
        {
            GameObject pause = Pause();
            var controller = new SerializedObject(pause.GetComponent<PauseMenuController>());
            SerializedProperty roots = controller.FindProperty("rootItems");
            string[] names = { "Resume", "NewRun", "Settings", "MainMenu" };
            string[] keys = { "pause.resume", "pause.new_run", "menu.settings", "pause.main_menu" };
            Assert.That(roots.arraySize, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                var item = (UnityEngine.UI.Selectable)roots.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(item.name, Is.EqualTo(names[i]));
                Assert.That(item.transform.GetSiblingIndex(), Is.EqualTo(i));
                var localized = new SerializedObject(item.transform.Find("Label").GetComponent<LocalizedText>());
                Assert.That(localized.FindProperty("key").stringValue, Is.EqualTo(keys[i]));
                Assert.That(item.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.Explicit));
                Assert.That(item.navigation.selectOnDown, Is.EqualTo(roots.GetArrayElementAtIndex((i + 1) % 4).objectReferenceValue));
                Assert.That(item.navigation.selectOnUp, Is.EqualTo(roots.GetArrayElementAtIndex((i + 3) % 4).objectReferenceValue));
            }
            var prompt = (TMP_Text)controller.FindProperty("sceneLeavePrompt").objectReferenceValue;
            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.GetComponent<LocalizedText>(), Is.Null, "Shared confirmation copy is controller-owned.");
            Assert.That(pause.GetComponentsInChildren<ConfirmDialogView>(true).Length, Is.EqualTo(2), "Reuse the existing confirmation card.");
            foreach (ConfirmDialogView dialog in pause.GetComponentsInChildren<ConfirmDialogView>(true))
            {
                Assert.That(dialog.Negative.navigation.selectOnDown, Is.EqualTo(dialog.Affirmative));
                Assert.That(dialog.Negative.navigation.selectOnRight, Is.EqualTo(dialog.Affirmative));
                Assert.That(dialog.Affirmative.navigation.selectOnUp, Is.EqualTo(dialog.Negative));
            }
            SettingsPanelView settings = pause.GetComponentInChildren<SettingsPanelView>(true);
            Assert.That(settings.OrderedSelectables.Length, Is.EqualTo(14));
            Assert.That(settings.OrderedSelectables[0].name, Is.EqualTo("Language"));
            Assert.That(settings.OrderedSelectables[settings.OrderedSelectables.Length - 1].navigation.selectOnDown, Is.EqualTo(settings.OrderedSelectables[0]));
        }
        [Test]
        public void TextFloorDynamicLabelsAndUiAudioExceptionAreValid()
        {
            GameObject pause = Pause();
            foreach (TMP_Text label in pause.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24f), label.name);
                if (label.enableAutoSizing) Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(24f), label.name);
                if (label.name == "Value" || label.name == "Countdown" || label.name == "LoadError")
                    Assert.That(label.GetComponent<LocalizedText>(), Is.Null, label.name + " is view-owned.");
            }
            var sources = pause.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.EqualTo(1));
            Assert.That(sources[0].playOnAwake, Is.False);
            Assert.That(sources[0].spatialBlend, Is.Zero);
            Assert.That(pause.GetComponentsInChildren<MenuAudioPlayer>(true).Length, Is.EqualTo(1));
            var image = pause.transform.Find("Scrim").GetComponent<UnityEngine.UI.Image>();
            MenuTheme theme = (MenuTheme)new SerializedObject(pause.GetComponent<PauseMenuController>()).FindProperty("theme").objectReferenceValue;
            var audioObject = new GameObject("PauseAudioTest", typeof(AudioSource));
            audioObject.SetActive(false);
            try
            {
                var audio = audioObject.AddComponent<MenuAudioPlayer>();
                var data = new SerializedObject(audio);
                data.FindProperty("theme").objectReferenceValue = theme;
                data.ApplyModifiedPropertiesWithoutUndo();
                AudioSource source = audioObject.GetComponent<AudioSource>();
                source.ignoreListenerPause = false;
                typeof(MenuAudioPlayer).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(audio, null);
                Assert.That(source.ignoreListenerPause, Is.True);
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.Zero);
            }
            finally { Object.DestroyImmediate(audioObject); }
            Color expected = theme.Background; expected.a = theme.PanelAlpha;
            Assert.That(image.color, Is.EqualTo(expected)); Assert.That(image.raycastTarget, Is.True);
        }
    }
}
