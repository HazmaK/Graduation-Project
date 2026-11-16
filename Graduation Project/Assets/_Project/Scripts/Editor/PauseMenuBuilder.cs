using System;
using RetroFPS.Pause;
using RetroFPS.Settings;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static RetroFPS.Editor.MainMenuBuilder;

namespace RetroFPS.Editor
{
    /// <summary>Idempotent, prefab-only pause integration. Never invokes the broad scene-writing builders.</summary>
    public static class PauseMenuBuilder
    {
        /// <summary>Pause canvas nested once in Player.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Pause/PauseMenu.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string TablePath = "Assets/_Project/Data/UI/StringTable_MainMenu_tr.asset";
        private static MenuTheme theme;
        private static StringTable strings;

        [MenuItem("RetroFPS/Build Pause Menu", false, 24)]
        private static void BuildMenu() => BuildAll();

        /// <summary>Shared settings → canvas-only patch → pause prefab → Player wiring. Returns zero on success.</summary>
        public static int BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return -1;
            Scene previous = SceneManager.GetActiveScene();
            // Additive scratch construction keeps loaded font assets alive and never saves an open scene.
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/_Project/Data/UI/MenuTheme_KanVeKemik.asset");
                strings = AssetDatabase.LoadAssetAtPath<StringTable>(TablePath);
                if (theme == null || strings == null || theme.UiFont == null)
                    throw new InvalidOperationException("Existing main-menu theme, strings and leaf prefabs are required.");
                UpsertStrings(strings);
                WarmPauseGlyphs(strings, theme);
                UseSharedPresentation(theme, strings);
                SharedSettingsPanelBuilder.Build();
                SharedSettingsPanelBuilder.PatchMainMenuCanvas();
                BuildPausePrefab();
                GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
                try
                {
                    // Build missing HUD assets only; EnsurePlayerPause always repairs existing Player wiring.
                    if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab") == null)
                        CombatHudBuilder.EnsurePlayerHud(player);
                    EnsurePlayerPause(player);
                    Save(player, PlayerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(player); }
                AssetDatabase.SaveAssets();
                ValidateSavedPlayer();
                Debug.Log("[PauseMenuBuilder] Shared settings, main-menu canvas patch, pause and Player prefabs saved. No scenes written.");
                return 0;
            }
            catch (Exception exception) { Debug.LogException(exception); return -4; }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scratch, true);
            }
        }

        /// <summary>Non-destructive bilingual seeds; authored cells and unrelated settings keys survive reruns.</summary>
        public static void UpsertStrings(StringTable table)
        {
            if (table.UpsertEntries(new[]
            {
                new StringTable.Translation("pause.resume", "DEVAM", "RESUME"),
                new StringTable.Translation("pause.new_run", "YENİ KOŞU", "NEW RUN"),
                new StringTable.Translation("pause.new_run_prompt", "Yeni koşu başlatılsın mı? Bu koşudaki ilerleme kaybolacak.", "Start a new run? This run's progress will be lost."),
                new StringTable.Translation("pause.new_run_load_error", "Yeni koşu başlatılamadı. Tekrar deneyin.", "Could not start a new run. Try again."),
                new StringTable.Translation("pause.main_menu", "ANA MENÜ", "MAIN MENU"),
                new StringTable.Translation("pause.main_menu_prompt", "Ana menüye dönülsün mü? Bu koşudaki ilerleme kaybolacak.", "Return to the main menu? This run's progress will be lost."),
                new StringTable.Translation("pause.main_menu_load_error", "Ana menü yüklenemedi. Tekrar deneyin.", "Could not load the main menu. Try again.")
            })) EditorUtility.SetDirty(table);
            table.ValidateEntries();
        }
        private static void WarmPauseGlyphs(StringTable table, MenuTheme palette)
        {
            var entries = new SerializedObject(table).FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (!entry.FindPropertyRelative("key").stringValue.StartsWith("pause.", StringComparison.Ordinal)) continue;
                WarmGlyphs(palette.UiFont, entry.FindPropertyRelative("value").stringValue);
                WarmGlyphs(palette.UiFont, entry.FindPropertyRelative("en").stringValue);
            }
            if (palette.FallbackFont != null) EditorUtility.SetDirty(palette.FallbackFont);
        }
        private static void WarmGlyphs(TMP_FontAsset font, string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (!char.IsWhiteSpace(value[i]) && !font.HasCharacter(value[i], true, true))
                    throw new InvalidOperationException("Missing pause glyph: " + value[i]);
            EditorUtility.SetDirty(font);
        }
        private static void BuildPausePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                GameObject existing = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    if (UpgradeNewRunMenu(existing)) Save(existing, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                ValidatePause(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI/Pause"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/UI", "Pause");
            GameObject root = Rect("PauseMenu", null).gameObject;
            root.SetActive(false);
            try
            {
                var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
                var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
                root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Color color = theme.Background; color.a = theme.PanelAlpha;
                var scrim = Image(root.transform, "Scrim", null, color, 0, 0, 0, 0); Stretch(scrim.rectTransform); scrim.raycastTarget = true;
                CanvasGroup scrimGroup = Group(scrim.gameObject);
                RectTransform safe = Rect("SafeArea", root.transform); Stretch(safe);
                safe.anchorMin = Vector2.one * 0.05f; safe.anchorMax = Vector2.one * 0.95f;
                RectTransform list = Rect("Root", safe); Stretch(list);
                CanvasGroup rootGroup = Group(list.gameObject);
                RectTransform menu = Rect("MenuList", list);
                menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(0, 0.5f);
                menu.anchoredPosition = Vector2.zero; menu.sizeDelta = new Vector2(700, 326);
                var layout = menu.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.spacing = 18; layout.childControlHeight = layout.childControlWidth = false;
                layout.childForceExpandHeight = layout.childForceExpandWidth = false;
                var resume = Button(menu, "Resume", "pause.resume", 0, 0, 700, theme.MenuItemSize);
                var newRun = Button(menu, "NewRun", "pause.new_run", 0, 0, 700, theme.MenuItemSize);
                var settings = Button(menu, "Settings", "menu.settings", 0, 0, 700, theme.MenuItemSize);
                var main = Button(menu, "MainMenu", "pause.main_menu", 0, 0, 700, theme.MenuItemSize);
                var order = new UnityEngine.UI.Selectable[] { resume, newRun, settings, main }; Wrap(order);
                GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SharedSettingsPanelBuilder.PrefabPath), safe);
                panel.name = "SettingsPanel";
                TMP_Text error = DynamicText("LoadError", safe, 1000, 100);
                error.rectTransform.anchorMin = error.rectTransform.anchorMax = error.rectTransform.pivot = Vector2.zero;
                error.rectTransform.anchoredPosition = new Vector2(0, 90); error.color = theme.Danger;
                error.gameObject.SetActive(false);
                RectTransform dialogs = Rect("DialogLayer", root.transform); Stretch(dialogs);
                var dim = Image(dialogs, "DimOverlay", null, new Color(0, 0, 0, theme.DimAlpha), 0, 0, 0, 0); Stretch(dim.rectTransform); dim.raycastTarget = true;
                CanvasGroup dimGroup = Group(dim.gameObject);
                GameObject confirm = Instantiate("ConfirmDialog", dialogs); confirm.name = "ConfirmMainMenu";
                var confirmRect = (RectTransform)confirm.transform; Centre(confirmRect); Size(confirmRect, 1040, 440);
                TMP_Text prompt = confirm.transform.Find("Prompt").GetComponent<TMP_Text>();
                LocalizedText localizedPrompt = prompt.GetComponent<LocalizedText>();
                if (localizedPrompt != null) Object.DestroyImmediate(localizedPrompt);
                prompt.text = strings.Get("pause.main_menu_prompt", LanguageSetting.Turkish);
                Size(prompt.rectTransform, 960, 170); prompt.rectTransform.anchoredPosition = new Vector2(40, 105);
                prompt.textWrappingMode = TextWrappingModes.Normal;
                prompt.enableAutoSizing = false; prompt.fontSize = Mathf.Max(24f, theme.DialogPromptSize);
                prompt.fontSizeMin = 24f;
                ValidatePrompt(prompt);
                confirm.transform.Find("Affirmative").GetComponent<RectTransform>().anchoredPosition = new Vector2(100, -120);
                confirm.transform.Find("Negative").GetComponent<RectTransform>().anchoredPosition = new Vector2(620, -120);
                GameObject revert = Instantiate("ConfirmDialog", dialogs); revert.name = "RevertPrompt"; Centre((RectTransform)revert.transform);
                Localize(revert.transform.Find("Prompt").GetComponent<TMP_Text>(), "display.revert_title");
                Localize(revert.transform.Find("Affirmative/Label").GetComponent<TMP_Text>(), "display.keep");
                Localize(revert.transform.Find("Negative/Label").GetComponent<TMP_Text>(), "display.revert");
                RectTransform fade = Rect("FadeLayer", root.transform); Stretch(fade);
                var fadeImage = Image(fade, "FadeOverlay", null, Color.black, 0, 0, 0, 0); Stretch(fadeImage.rectTransform); fadeImage.raycastTarget = true;
                CanvasGroup fadeGroup = Group(fadeImage.gameObject);
                var fader = fade.gameObject.AddComponent<ScreenFader>(); Set(fader, "theme", theme); Set(fader, "overlay", fadeGroup);
                var controller = root.AddComponent<PauseMenuController>();
                var router = root.AddComponent<MenuInputRouter>(); Set(router, "controller", controller); SetBool(router, "pauseHost", true); router.enabled = false;
                Set(controller, "theme", theme); Set(controller, "strings", strings); Set(controller, "scrimGroup", scrimGroup);
                Set(controller, "rootGroup", rootGroup); Set(controller, "dimGroup", dimGroup); SetArray(controller, "rootItems", order);
                Set(controller, "settingsPanel", panel.GetComponent<SettingsPanelView>());
                Set(controller, "mainMenuDialog", confirm.GetComponent<ConfirmDialogView>()); Set(controller, "revertDialog", revert.GetComponent<ConfirmDialogView>());
                Set(controller, "sceneLeavePrompt", prompt);
                Set(controller, "fader", fader); Set(controller, "fadeGroup", fadeGroup); Set(controller, "safeArea", safe);
                Set(controller, "loadError", error); Set(controller, "inputRouter", router);
                var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0f; source.ignoreListenerPause = true;
                var audio = root.AddComponent<MenuAudioPlayer>(); Set(audio, "source", source); Set(audio, "theme", theme);
                // Preserve authored main-menu clips; unassigned slots remain silent placeholders.
                GameObject systems = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MainMenu/MainMenuSystems.prefab");
                MenuAudioPlayer authored = systems != null ? systems.GetComponent<MenuAudioPlayer>() : null;
                var clips = new AudioClip[(int)UiAudioEventType.Error + 1];
                if (authored != null)
                {
                    SerializedProperty existing = new SerializedObject(authored).FindProperty("clips");
                    for (int i = 0; i < Math.Min(existing.arraySize, clips.Length); i++) clips[i] = existing.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                }
                SetArray(audio, "clips", clips);
                root.SetActive(true);
                Save(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            ValidatePause(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        // Upgrade in place: retain the existing dialog, three buttons, authored styling and all file IDs.
        internal static bool UpgradeNewRunMenu(GameObject root)
        {
            var controller = root.GetComponent<PauseMenuController>();
            var data = new SerializedObject(controller);
            RectTransform menu = (RectTransform)root.transform.Find("SafeArea/Root/MenuList");
            bool changed = false;
            Transform newRun = menu.Find("NewRun");
            if (newRun == null)
            {
                newRun = Button(menu, "NewRun", "pause.new_run", 0, 0, menu.sizeDelta.x, theme.MenuItemSize).transform;
                changed = true;
                var layout = menu.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                float requiredHeight = 0f;
                for (int i = 0; i < menu.childCount; i++) requiredHeight += ((RectTransform)menu.GetChild(i)).sizeDelta.y;
                requiredHeight += layout.spacing * (menu.childCount - 1) + layout.padding.vertical;
                menu.sizeDelta = new Vector2(menu.sizeDelta.x, Mathf.Max(menu.sizeDelta.y, requiredHeight));
            }
            if (newRun.GetSiblingIndex() != 1) { newRun.SetSiblingIndex(1); changed = true; }
            var order = new UnityEngine.UI.Selectable[]
            {
                menu.Find("Resume").GetComponent<UnityEngine.UI.Selectable>(), newRun.GetComponent<UnityEngine.UI.Selectable>(),
                menu.Find("Settings").GetComponent<UnityEngine.UI.Selectable>(), menu.Find("MainMenu").GetComponent<UnityEngine.UI.Selectable>()
            };
            SerializedProperty items = data.FindProperty("rootItems");
            bool orderChanged = items.arraySize != order.Length;
            for (int i = 0; !orderChanged && i < order.Length; i++)
                orderChanged = items.GetArrayElementAtIndex(i).objectReferenceValue != order[i];
            if (orderChanged) { SetArray(controller, "rootItems", order); changed = true; }
            for (int i = 0; i < order.Length; i++)
            {
                var nav = order[i].navigation;
                if (nav.mode != UnityEngine.UI.Navigation.Mode.Explicit || nav.selectOnUp != order[(i + 3) % 4]
                    || nav.selectOnDown != order[(i + 1) % 4])
                { Wrap(order); changed = true; break; }
            }
            var dialog = (ConfirmDialogView)data.FindProperty("mainMenuDialog").objectReferenceValue;
            TMP_Text prompt = dialog.transform.Find("Prompt").GetComponent<TMP_Text>();
            LocalizedText localized = prompt.GetComponent<LocalizedText>();
            if (localized != null) { Object.DestroyImmediate(localized); changed = true; }
            if (data.FindProperty("sceneLeavePrompt").objectReferenceValue != prompt)
            { Set(controller, "sceneLeavePrompt", prompt); changed = true; }
            return changed;
        }
        /// <summary>Wires only the supplied prefab/player; safe for CombatHudBuilder to call after HUD creation.</summary>
        public static void EnsurePlayerPause(GameObject player)
        {
            CombatHudBuilder.EnsurePlayerHudWiring(player);
            EnsurePlayerPauseWiring(player);
        }

        internal static void EnsurePlayerPauseWiring(GameObject player)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return; // HUD can be built before pause; the pause build completes this later.
            HudEventSystemGuard guard = player.GetComponentInChildren<HudEventSystemGuard>(true);
            if (guard == null) throw new InvalidOperationException("Player must have its combat HUD before pause wiring.");
            GameObject pause = CombatHudBuilder.SingleNestedPrefab(player, prefab);
            pause.name = "PauseMenu";
            var runtime = CombatHudBuilder.SinglePlayerComponent<PauseRuntime>(player);
            var bindings = CombatHudBuilder.SinglePlayerComponent<PauseBindings>(player);
            Set(runtime, "player", player.GetComponent<PlayerHealth>());
            Set(bindings, "runtime", runtime); Set(bindings, "health", player.GetComponent<PlayerHealth>());
            Set(bindings, "pauseMenu", pause.GetComponent<PauseMenuController>()); Set(bindings, "eventSystemGuard", guard);
            Set(bindings, "trueDeath", player.GetComponentInChildren<TrueDeathView>(true));
            Set(pause.GetComponent<MenuInputRouter>(), "eventSystem", guard.BundledSystem);
        }
        private static CanvasGroup Group(GameObject root)
        {
            CanvasGroup group = root.AddComponent<CanvasGroup>(); group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false; return group;
        }
        private static void Centre(RectTransform rect)
        { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = Vector2.zero; }
        private static TMP_Text DynamicText(string name, Transform parent, float width, float height)
        {
            RectTransform rect = Rect(name, parent); Size(rect, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = theme.UiFont;
            label.fontSize = Mathf.Max(24f, theme.SettingsLabelSize); label.fontSizeMin = 24f;
            label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
            label.text = string.Empty; label.vertexBufferAutoSizeReduction = false; return label;
        }
        private static void ValidatePrompt(TMP_Text prompt)
        {
            foreach (string key in new[] { "pause.main_menu_prompt", "pause.new_run_prompt" })
            for (int i = 0; i < 2; i++)
            {
                string copy = strings.Get(key, i == 0 ? LanguageSetting.Turkish : LanguageSetting.English);
                if (prompt.GetPreferredValues(copy, prompt.rectTransform.rect.width, float.PositiveInfinity).y > prompt.rectTransform.rect.height)
                    throw new InvalidOperationException("Pause confirmation copy overflows at the authored font size.");
            }
        }
        private static void ValidatePause(GameObject prefab)
        {
            if (prefab.GetComponent<Canvas>().sortingOrder != 200 || prefab.GetComponentInChildren<MainMenuController>(true) != null
                || prefab.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length != 0
                || prefab.GetComponentsInChildren<AudioSource>(true).Length != 1)
                throw new InvalidOperationException("Pause prefab ownership/order is invalid.");
            foreach (TMP_Text label in prefab.GetComponentsInChildren<TMP_Text>(true))
                if (label.fontSize < 24f || (label.enableAutoSizing && label.fontSizeMin < 24f))
                    throw new InvalidOperationException("Pause text is below 24px: " + label.name);
            AudioSource source = prefab.GetComponent<AudioSource>();
            // AudioSource.ignoreListenerPause is runtime-only (never serialized), so a saved prefab always reads
            // false; MenuAudioPlayer sets it on initialization. Validate the serialized half and that wiring instead.
            MenuAudioPlayer pauseAudio = prefab.GetComponent<MenuAudioPlayer>();
            if (source == null || source.playOnAwake || source.spatialBlend != 0f || pauseAudio == null
                || new SerializedObject(pauseAudio).FindProperty("source").objectReferenceValue != source)
                throw new InvalidOperationException("Pause UI audio must be a 2D, non-autoplay source driven by MenuAudioPlayer.");
            var controller = new SerializedObject(prefab.GetComponent<PauseMenuController>());
            string[] fields = { "theme", "strings", "scrimGroup", "rootGroup", "dimGroup", "settingsPanel", "mainMenuDialog", "sceneLeavePrompt", "revertDialog", "fader", "fadeGroup", "safeArea", "loadError", "inputRouter" };
            for (int i = 0; i < fields.Length; i++)
                if (controller.FindProperty(fields[i]).objectReferenceValue == null)
                    throw new InvalidOperationException("Missing pause reference: " + fields[i]);
            SerializedProperty items = controller.FindProperty("rootItems");
            string[] names = { "Resume", "NewRun", "Settings", "MainMenu" };
            if (items.arraySize != names.Length) throw new InvalidOperationException("Pause needs four root items.");
            for (int i = 0; i < names.Length; i++)
            {
                var item = (UnityEngine.UI.Selectable)items.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item == null || item.name != names[i] || item.transform.GetSiblingIndex() != i
                    || item.navigation.mode != UnityEngine.UI.Navigation.Mode.Explicit
                    || item.navigation.selectOnDown != items.GetArrayElementAtIndex((i + 1) % 4).objectReferenceValue
                    || item.navigation.selectOnUp != items.GetArrayElementAtIndex((i + 3) % 4).objectReferenceValue)
                    throw new InvalidOperationException("Pause root order/navigation is invalid.");
            }
            TMP_Text prompt = (TMP_Text)controller.FindProperty("sceneLeavePrompt").objectReferenceValue;
            if (prompt.GetComponent<LocalizedText>() != null) throw new InvalidOperationException("Pause confirmation is view-owned.");
            ValidatePrompt(prompt);
        }
        private static void ValidateSavedPlayer()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (player.GetComponents<PauseRuntime>().Length != 1 || player.GetComponents<PauseBindings>().Length != 1
                || player.GetComponentsInChildren<HudBindings>(true).Length != 1
                || player.GetComponentsInChildren<PauseMenuController>(true).Length != 1
                || player.GetComponentsInChildren<HudEventSystemGuard>(true).Length != 1)
                throw new InvalidOperationException("Player pause/HUD ownership validation failed.");
            var modules = player.GetComponentsInChildren<InputSystemUIInputModule>(true);
            if (modules.Length != 1) throw new InvalidOperationException("Player needs one compatible HUD EventSystem.");
            string[] fields = { "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_ScrollWheelAction" };
            var data = new SerializedObject(modules[0]);
            for (int i = 0; i < fields.Length; i++)
            {
                Object reference = data.FindProperty(fields[i]).objectReferenceValue;
                if (reference == null || !AssetDatabase.Contains(reference)) throw new InvalidOperationException("Nonpersistent input reference: " + fields[i]);
            }
        }
        private static void SetBool(Object target, string property, bool value)
        { var so = new SerializedObject(target); so.FindProperty(property).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Save(GameObject root, string path)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                { PrefabUtility.RecordPrefabInstancePropertyModifications(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject); }
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Cannot save pause integration: " + path);
        }
    }
}
