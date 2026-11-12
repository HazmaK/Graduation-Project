using System;
using System.Collections.Generic;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Builds the complete, replaceable Direction A main menu without editing gameplay assets.</summary>
    public static class MainMenuBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/TestLevel_Gauntlet.unity";
        private const string Prefabs = "Assets/_Project/Prefabs/UI/MainMenu/";
        private const string Data = "Assets/_Project/Data/UI/";
        private const string Materials = "Assets/_Project/Data/Materials/UI/";
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        private static MenuTheme theme;
        private static StringTable strings;

        [MenuItem("RetroFPS/Build Main Menu", false, 22)]
        private static void BuildMenu() => BuildAll();

        /// <summary>Returns 0 on success; -1 play mode, -2 dirty scene, -3 dependency/import,
        /// -4 build failure, or -5 previous scene restoration failure. TMP import requires a second run.</summary>
        public static int BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Error(-1, "Exit Play Mode before building.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return Error(-2, "Save all open scenes before building.");
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.Log("[MainMenuBuilder] TMP resources are importing. Run RetroFPS/Build Main Menu again after import/reload.");
                return -3;
            }
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (actions == null || font == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayPath) == null
                || Shader.Find("Universal Render Pipeline/Particles/Unlit") == null)
                return Error(-3, "Input actions, LiberationSans SDF, gameplay scene or URP particles shader is missing.");
            // Validate persistent importer-owned references before generating any prefabs.
            string[] required = { "Navigate", "Submit", "Cancel", "Point", "Click", "RightClick", "ScrollWheel" };
            for (int i = 0; i < required.Length; i++)
                if (ActionReference(required[i]) == null) return Error(-3, "Missing UI action reference: " + required[i]);

            SceneSetup[] original = EditorSceneManager.GetSceneManagerSetup();
            int result = 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder(Prefabs.TrimEnd('/'));
                EnsureFolder(Data.TrimEnd('/'));
                EnsureFolder(Materials.TrimEnd('/'));
                EnsureFolder("Assets/Scenes");
                BuildData(font);
                MainMenuPlaceholderArt.Generate();
                BuildLeaves();
                BuildCanvas();
                BuildArt(EnsureMenuArtLayer());
                BuildEventSystem(actions);
                BuildSystems();
                AssetDatabase.SaveAssets();

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Instantiate("MainMenuArt", null);
                GameObject canvas = Instantiate("MainMenuCanvas", null);
                GameObject events = Instantiate("MenuEventSystem", null);
                GameObject systems = Instantiate("MainMenuSystems", null);
                WireScene(canvas, events, systems);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save MainMenu scene.");
                UpdateBuildSettings();
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = -4;
            }
            finally
            {
                try
                {
                    // Restore saved multi-scene setups; untitled clean scenes are restored as an empty scene.
                    bool hasSavedScene = false;
                    for (int i = 0; i < original.Length; i++) hasSavedScene |= !string.IsNullOrEmpty(original[i].path);
                    if (hasSavedScene) EditorSceneManager.RestoreSceneManagerSetup(original);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                catch (Exception exception) { Debug.LogException(exception); result = -5; }
            }
            if (result == 0) Debug.Log("[MainMenuBuilder] MainMenu built with four prefab instances. Open Assets/Scenes/MainMenu.unity to preview.");
            return result;
        }

        private static int Error(int code, string message) { Debug.LogError("[MainMenuBuilder] " + message); return code; }

        private static void BuildData(TMP_FontAsset font)
        {
            theme = Asset<MenuTheme>(Data + "MenuTheme_KanVeKemik.asset", out bool themeCreated);
            strings = Asset<StringTable>(Data + "StringTable_MainMenu_tr.asset", out bool stringsCreated);
            strings.RebuildCache();
            // A previously failed run can leave an empty table asset behind; refill it.
            if (stringsCreated || !strings.ContainsKey("menu.start"))
            {
                strings.PopulateTurkishDefaults();
                strings.RebuildCache();
            }
            // NewScene(Single) unloads unreferenced assets, so the font loaded before it may be destroyed.
            if (font == null)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            TMP_FontAsset fallback = theme.FallbackFont;
            if (fallback == null)
            {
                fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Data + "MainMenu_DynamicFallback.asset");
                if (fallback == null)
                {
                    Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                    if (source == null) throw new InvalidOperationException("TMP LiberationSans.ttf is missing.");
                    fallback = TMP_FontAsset.CreateFontAsset(source, 90, 9,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                    fallback.name = "MainMenu_DynamicFallback";
                    AssetDatabase.CreateAsset(fallback, Data + "MainMenu_DynamicFallback.asset");
                    AssetDatabase.AddObjectToAsset(fallback.material, fallback);
                    for (int i = 0; i < fallback.atlasTextures.Length; i++)
                        AssetDatabase.AddObjectToAsset(fallback.atlasTextures[i], fallback);
                    fallback.TryAddCharacters("ÇçĞğİıÖöŞşÜü%<>…", out string missing);
                    if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[MainMenuBuilder] Missing Turkish fallback glyphs: " + missing);
                    EditorUtility.SetDirty(fallback);
                }
            }
            if (themeCreated || theme.TitleFont == null) Set(theme, "titleFont", font);
            if (themeCreated || theme.UiFont == null) Set(theme, "uiFont", font);
            Set(theme, "fallbackFont", fallback);
            AddFallback(theme.TitleFont != null ? theme.TitleFont : font, fallback);
            AddFallback(theme.UiFont != null ? theme.UiFont : font, fallback);
            EditorUtility.SetDirty(theme);
            EditorUtility.SetDirty(strings);
        }
        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null)
            {
                Debug.LogWarning("[MainMenuBuilder] Skipping font fallback: " + (font == null ? "font" : "fallback") + " is null.");
                return;
            }
            if (font == fallback) return;
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(font);
        }

        private static void BuildLeaves()
        {
            GameObject button = Row("MenuButton", 650, 68);
            var buttonComponent = button.AddComponent<UnityEngine.UI.Button>();
            Style(buttonComponent);
            TMP_Text label = Label(button.transform, "Label", "menu.start", 44, 48, 0, 550, 64);
            Focus(buttonComponent, label, false, null);
            SavePrefab(button, "MenuButton");

            GameObject sliderObject = Row("MenuSliderRow", 700, 56);
            var slider = sliderObject.AddComponent<MenuSlider>();
            Style(slider);
            Set(slider, "navigate", ActionReference("Navigate"));
            slider.minValue = 0; slider.maxValue = 20; slider.wholeNumbers = true; slider.value = 20;
            TMP_Text sliderLabel = Label(sliderObject.transform, "Label", "settings.master_volume", theme.SettingsLabelSize, 48, 0, 256, 54);
            sliderLabel.overflowMode = TextOverflowModes.Ellipsis;
            var track = Image(sliderObject.transform, "Track", null, theme.SliderTrack, 325, 0, 235, 6);
            RectTransform fillArea = Rect("FillArea", track.transform); Stretch(fillArea);
            var fill = Image(fillArea, "Fill", null, theme.TextDim, 0, 0, 0, 6); Stretch(fill.rectTransform);
            RectTransform handleArea = Rect("HandleArea", track.transform); Stretch(handleArea);
            var handle = Image(handleArea, "Thumb", null, theme.Text, 0, 0, 9, 15);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(1, 0.5f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            Label(sliderObject.transform, "Value", null, theme.SettingsLabelSize, 590, 0, 100, 54);
            Focus(slider, sliderLabel, true, fill);
            SavePrefab(sliderObject, "MenuSliderRow");

            GameObject cycleObject = Row("MenuCycleRow", 700, 56);
            var cycle = cycleObject.AddComponent<MenuCycleRow>();
            Style(cycle);
            Set(cycle, "theme", theme);
            // Reserve space for the focused label's 1.06 scale and the arrow's press nudge.
            TMP_Text cycleLabel = Label(cycleObject.transform, "Label", "settings.window_mode", theme.SettingsLabelSize, 48, 0, 236, 54);
            cycleLabel.overflowMode = TextOverflowModes.Ellipsis;
            var previous = Arrow(cycleObject.transform, "Previous", "<", 304);
            var next = Arrow(cycleObject.transform, "Next", ">", 644);
            TMP_Text value = Label(cycleObject.transform, "Value", null, theme.SettingsLabelSize, 362, 0, 276, 54);
            value.overflowMode = TextOverflowModes.Ellipsis;
            value.alignment = TextAlignmentOptions.Center;
            Set(cycle, "valueLabel", value); Set(cycle, "previous", previous); Set(cycle, "next", next);
            Focus(cycle, cycleLabel, true, null);
            SavePrefab(cycleObject, "MenuCycleRow");

            GameObject dialogObject = Rect("ConfirmDialog", null).gameObject;
            Size((RectTransform)dialogObject.transform, 800, 320);
            var group = dialogObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            var frame = Image(dialogObject.transform, "Frame", MainMenuPlaceholderArt.Sprite("DialogFrame"), Color.white, 0, 0, 0, 0);
            Stretch(frame.rectTransform); frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f / 3f;
            frame.raycastTarget = true;
            Label(dialogObject.transform, "Prompt", "quit.prompt", theme.DialogPromptSize, 40, 92, 720, 90);
            TMP_Text countdown = Label(dialogObject.transform, "Countdown", null, 40, 40, 16, 720, 65);
            countdown.alignment = TextAlignmentOptions.Center;
            var yes = Button(dialogObject.transform, "Affirmative", "quit.yes", 50, -100, 320, theme.DialogButtonSize);
            var no = Button(dialogObject.transform, "Negative", "quit.no", 430, -100, 320, theme.DialogButtonSize);
            Wrap(new UnityEngine.UI.Selectable[] { no, yes }, true);
            var dialog = dialogObject.AddComponent<ConfirmDialogView>();
            Set(dialog, "group", group); Set(dialog, "affirmative", yes); Set(dialog, "negative", no);
            Set(dialog, "countdownLabel", countdown); Set(dialog, "theme", theme); Set(dialog, "strings", strings);
            SavePrefab(dialogObject, "ConfirmDialog");
        }

        private static void BuildCanvas()
        {
            GameObject root = Rect("MainMenuCanvas", null).gameObject;
            Canvas canvas = Canvas(root, RenderMode.ScreenSpaceOverlay, 10, true);
            RectTransform safe = Rect("SafeArea", root.transform);
            safe.anchorMin = Vector2.one * 0.05f; safe.anchorMax = Vector2.one * 0.95f;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            RectTransform rootView = Rect("Root", safe); Stretch(rootView);
            rootView.gameObject.AddComponent<CanvasGroup>();
            TMP_Text title = Label(rootView, "Title", "menu.title", theme.TitleSize, 0, 0, 740, 170);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0, 1);
            title.rectTransform.pivot = new Vector2(0, 1); title.rectTransform.anchoredPosition = Vector2.zero;
            title.font = theme.TitleFont; title.fontSizeMin = theme.TitleMinimumFontSize;
            title.characterSpacing = theme.TitleTracking; title.fontStyle = FontStyles.Bold;
            Image(rootView, "TitleRule", null, theme.Accent, 0, 0, 600, 3).rectTransform.anchoredPosition = new Vector2(0, 300);
            RectTransform menu = Rect("MenuList", rootView);
            menu.anchorMin = menu.anchorMax = Vector2.zero; menu.pivot = Vector2.zero;
            menu.anchoredPosition = new Vector2(0, 70); Size(menu, 650, 260);
            var layout = menu.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 18; layout.childControlHeight = false; layout.childControlWidth = false;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = false;
            var start = Button(menu, "Start", "menu.start", 0, 0, 650);
            var settings = Button(menu, "Settings", "menu.settings", 0, 0, 650);
            var quit = Button(menu, "Quit", "menu.quit", 0, 0, 650);
            Wrap(new UnityEngine.UI.Selectable[] { start, settings, quit });

            RectTransform panel = Rect("SettingsPanel", safe);
            panel.anchorMin = panel.anchorMax = Vector2.zero; panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(0, 65); Size(panel, 760, 820);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0; panelGroup.interactable = panelGroup.blocksRaycasts = false;
            var frame = Image(panel, "Frame", MainMenuPlaceholderArt.Sprite("PanelFrame_A"), Color.white, 0, 0, 0, 0);
            Stretch(frame.rectTransform); frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f / 3f;
            var jewel = Image(panel, "Jewel", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSeal"), Color.white, 0, 0, 45, 21);
            jewel.rectTransform.anchorMin = jewel.rectTransform.anchorMax = new Vector2(0.5f, 1); jewel.rectTransform.anchoredPosition = Vector2.zero;
            RectTransform content = Rect("Content", panel); Stretch(content); content.offsetMin = new Vector2(30, 24); content.offsetMax = new Vector2(-30, -24);
            LabelTop(content, "Title", "settings.title", theme.SettingsTitleSize, 0, 54);
            Group(content, "Audio", "settings.group.audio", 80);
            string[] sliderKeys = { "settings.master_volume", "settings.music_volume", "settings.sfx_volume", "settings.ui_volume", "settings.mouse_sensitivity" };
            var sliders = new MenuSlider[5]; var values = new TMP_Text[5];
            float[] sliderY = { 128, 184, 240, 296, 402 };
            for (int i = 0; i < sliders.Length; i++)
            {
                GameObject row = Instantiate("MenuSliderRow", content);
                row.name = "Slider" + i;
                Top((RectTransform)row.transform, sliderY[i]);
                Localize(row.transform.Find("Label").GetComponent<TMP_Text>(), sliderKeys[i]);
                sliders[i] = row.GetComponent<MenuSlider>();
                values[i] = row.transform.Find("Value").GetComponent<TMP_Text>();
                values[i].text = string.Empty;
            }
            Group(content, "Controls", "settings.group.controls", 354);
            Group(content, "Display", "settings.group.display", 460);
            string[] cycleKeys = { "settings.window_mode", "settings.resolution", "settings.reduce_motion" };
            var cycles = new MenuCycleRow[3];
            for (int i = 0; i < cycles.Length; i++)
            {
                GameObject row = Instantiate("MenuCycleRow", content);
                row.name = "Cycle" + i;
                Top((RectTransform)row.transform, 508 + i * 56);
                Localize(row.transform.Find("Label").GetComponent<TMP_Text>(), cycleKeys[i]);
                cycles[i] = row.GetComponent<MenuCycleRow>();
                TMP_Text value = row.transform.Find("Value").GetComponent<TMP_Text>();
                value.text = string.Empty;
            }
            var back = Button(content, "Back", "settings.back", 0, 0, 700, theme.SettingsLabelSize);
            Top((RectTransform)back.transform, 700);
            var order = new UnityEngine.UI.Selectable[] { sliders[0], sliders[1], sliders[2], sliders[3], sliders[4], cycles[0], cycles[1], cycles[2], back };
            Wrap(order);
            var view = panel.gameObject.AddComponent<SettingsPanelView>();
            Set(view, "strings", strings); SetArray(view, "sliders", sliders); SetArray(view, "percentages", values);
            Set(view, "windowMode", cycles[0]); Set(view, "resolution", cycles[1]); Set(view, "reduceMotion", cycles[2]);

            TMP_Text version = Label(safe, "Version", null, theme.VersionSize, 0, 0, 650, 40);
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = Vector2.zero;
            version.rectTransform.pivot = Vector2.zero; version.rectTransform.anchoredPosition = Vector2.zero;
            version.color = theme.TextDim;
            TMP_Text error = Label(safe, "LoadError", "menu.load_error", theme.SettingsLabelSize, 0, 0, 700, 50);
            error.rectTransform.anchorMin = error.rectTransform.anchorMax = Vector2.zero;
            error.rectTransform.anchoredPosition = new Vector2(0, 370); error.color = theme.Danger;
            error.gameObject.SetActive(false);

            RectTransform dialogs = Rect("DialogLayer", root.transform); Stretch(dialogs);
            Canvas(dialogs.gameObject, RenderMode.ScreenSpaceOverlay, 20, true, true);
            var dim = Image(dialogs, "DimOverlay", null, new Color(0, 0, 0, theme.DimAlpha), 0, 0, 0, 0);
            Stretch(dim.rectTransform); dim.raycastTarget = true;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>(); dimGroup.alpha = 0; dimGroup.blocksRaycasts = false;
            GameObject quitDialog = Instantiate("ConfirmDialog", dialogs); quitDialog.name = "QuitConfirm"; Centre((RectTransform)quitDialog.transform);
            GameObject revert = Instantiate("ConfirmDialog", dialogs); revert.name = "RevertPrompt"; Centre((RectTransform)revert.transform);
            Localize(revert.transform.Find("Prompt").GetComponent<TMP_Text>(), "display.revert_title");
            Localize(revert.transform.Find("Affirmative/Label").GetComponent<TMP_Text>(), "display.keep");
            Localize(revert.transform.Find("Negative/Label").GetComponent<TMP_Text>(), "display.revert");

            RectTransform fade = Rect("FadeLayer", root.transform); Stretch(fade);
            Canvas(fade.gameObject, RenderMode.ScreenSpaceOverlay, 100, true, true);
            var fadeImage = Image(fade, "FadeOverlay", null, Color.black, 0, 0, 0, 0); Stretch(fadeImage.rectTransform); fadeImage.raycastTarget = true;
            var fadeGroup = fadeImage.gameObject.AddComponent<CanvasGroup>(); fadeGroup.alpha = 1; fadeGroup.blocksRaycasts = true;
            var fader = fade.gameObject.AddComponent<ScreenFader>(); Set(fader, "theme", theme); Set(fader, "overlay", fadeGroup);
            SavePrefab(root, "MainMenuCanvas");
        }

        private static void BuildArt(int layer)
        {
            GameObject root = new GameObject("MainMenuArt");
            GameObject cameraObject = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false); cameraObject.transform.localPosition = new Vector3(0, 0, -100);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 540; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = theme.Background; camera.cullingMask = 1 << layer;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 200; camera.allowHDR = false; camera.allowMSAA = false;
            RectTransform art = Rect("ArtCanvas", root.transform);
            Canvas canvas = Canvas(art.gameObject, RenderMode.ScreenSpaceCamera, 0, false);
            canvas.worldCamera = camera; canvas.planeDistance = 10;
            var sky = Image(art, "ArtSky", MainMenuPlaceholderArt.Sprite("ArtSky"), Color.white, 0, 0, 0, 0); Stretch(sky.rectTransform);
            var far = Image(art, "ArtTowerFar", MainMenuPlaceholderArt.Sprite("ArtTowerFar"), Color.white, 0, 0, 1920, 1080); Centre(far.rectTransform);
            RectTransform fogRect = Rect("ArtFog", art);
            fogRect.anchorMin = new Vector2(0, 0); fogRect.anchorMax = new Vector2(1, 0.45f); fogRect.offsetMin = fogRect.offsetMax = Vector2.zero;
            var fog = fogRect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); fog.texture = MainMenuPlaceholderArt.Texture("ArtFog"); fog.raycastTarget = false;
            var near = Image(art, "ArtTowerNear", MainMenuPlaceholderArt.Sprite("ArtTowerNear"), Color.white, 0, 0, 1920, 1080); Centre(near.rectTransform);
            Material emberMat = ParticleMaterial("MenuEmbers", "Ember", true);
            Material smokeMat = ParticleMaterial("MenuSmoke", "ArtTorchGlow", false);
            Material glowMat = ParticleMaterial("MenuGlow", "ArtTorchGlow", true);
            var glows = new UnityEngine.UI.Image[2];
            for (int i = 0; i < glows.Length; i++)
            {
                glows[i] = Image(art, "ArtTorchGlow" + i, MainMenuPlaceholderArt.Sprite("ArtTorchGlow"), theme.TorchColor, 0, 0, 150, 150);
                Centre(glows[i].rectTransform); glows[i].rectTransform.anchoredPosition = new Vector2(450 + i * 96, -228);
                glows[i].material = glowMat;
            }
            ParticleSystem embers = Particles(art, "Embers", emberMat, true);
            ParticleSystem smoke = Particles(art, "Smoke", smokeMat, false);
            RectTransform scrimLayer = Rect("ScrimLayer", art); Stretch(scrimLayer);
            Canvas scrimCanvas = scrimLayer.gameObject.AddComponent<Canvas>(); scrimCanvas.overrideSorting = true; scrimCanvas.sortingOrder = 3;
            var scrim = Image(scrimLayer, "LeftScrim", MainMenuPlaceholderArt.Sprite("LeftScrim"), Color.white, 0, 0, 0, 0); Stretch(scrim.rectTransform);
            var ambient = root.AddComponent<MenuAmbientMotion>();
            Set(ambient, "theme", theme); Set(ambient, "fog", fog); SetArray(ambient, "torchGlows", glows);
            SetArray(ambient, "particles", new[] { embers, smoke });
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            SavePrefab(root, "MainMenuArt");
        }

        private static ParticleSystem Particles(Transform parent, string name, Material material, bool ember)
        {
            RectTransform rect = Rect(name, parent); Centre(rect);
            rect.anchoredPosition = new Vector2(480, -250); Size(rect, 400, 100);
            var particles = rect.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.prewarm = true; main.duration = 7;
            main.playOnAwake = true; main.useUnscaledTime = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = ember ? theme.EmberMaxParticles : theme.SmokeMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(ember ? theme.EmberLifetimeMin : 5, ember ? theme.EmberLifetimeMax : 7);
            main.startSpeed = new ParticleSystem.MinMaxCurve(ember ? theme.EmberSpeedMin : theme.SmokeSpeedMin, ember ? theme.EmberSpeedMax : theme.SmokeSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(ember ? theme.EmberSizeMin : 90, ember ? theme.EmberSizeMax : 140);
            main.startColor = ember ? theme.TorchColor : new Color(0.4f, 0.34f, 0.33f, theme.SmokeAlphaMax);
            var emission = particles.emission; emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                ember ? theme.EmberRateMin : theme.SmokeRateMin, ember ? theme.EmberRateMax : theme.SmokeRateMax);
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(240, 70, 0); shape.rotation = new Vector3(ember ? -90 : 0, ember ? 0 : 90, 0);
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(ember ? theme.TorchColor : theme.TextDim, 0),
                new GradientColorKey(ember ? theme.EmberEndColor : theme.TextDim, 1) },
                new[] { new GradientAlphaKey(ember ? 1 : theme.SmokeAlphaMin, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = rect.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingOrder = ember ? 1 : 2;
            return particles;
        }

        private static Material ParticleMaterial(string name, string texture, bool additive)
        {
            string path = Materials + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            material.SetTexture("_BaseMap", MainMenuPlaceholderArt.Texture(texture));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildEventSystem(InputActionAsset actions)
        {
            GameObject root = new GameObject("MenuEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = root.GetComponent<InputSystemUIInputModule>();
            module.deselectOnBackgroundClick = false; module.moveRepeatDelay = 0.4f; module.moveRepeatRate = 0.1f;
            // The module's property setters swap in runtime-created references that serialize as {fileID: 0},
            // which leaves the saved prefab with no input at all. Write the persistent sub-asset references directly.
            Set(module, "m_ActionsAsset", actions);
            Set(module, "m_MoveAction", ActionReference("Navigate"));
            Set(module, "m_SubmitAction", ActionReference("Submit"));
            Set(module, "m_CancelAction", ActionReference("Cancel"));
            Set(module, "m_PointAction", ActionReference("Point"));
            Set(module, "m_LeftClickAction", ActionReference("Click"));
            Set(module, "m_RightClickAction", ActionReference("RightClick"));
            Set(module, "m_ScrollWheelAction", ActionReference("ScrollWheel"));
            SavePrefab(root, "MenuEventSystem");
        }

        private static void BuildSystems()
        {
            GameObject root = new GameObject("MainMenuSystems");
            var controller = root.AddComponent<MainMenuController>(); Set(controller, "theme", theme); Set(controller, "strings", strings);
            var router = root.AddComponent<MenuInputRouter>(); Set(router, "controller", controller);
            Set(router, "cancel", ActionReference("Cancel")); Set(router, "rightClick", ActionReference("RightClick"));
            root.AddComponent<MenuLifecycle>();
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            var audio = root.AddComponent<MenuAudioPlayer>(); Set(audio, "theme", theme); Set(audio, "source", source);
            SetArray(audio, "clips", new AudioClip[(int)UiAudioEventType.Error + 1]);
            SavePrefab(root, "MainMenuSystems");
        }

        private static void WireScene(GameObject canvas, GameObject events, GameObject systems)
        {
            Transform ui = canvas.transform;
            var controller = systems.GetComponent<MainMenuController>();
            var eventSystem = events.GetComponent<EventSystem>();
            Set(controller, "eventSystem", eventSystem);
            Set(controller, "rootGroup", ui.Find("SafeArea/Root").GetComponent<CanvasGroup>());
            Set(controller, "settingsGroup", ui.Find("SafeArea/SettingsPanel").GetComponent<CanvasGroup>());
            Set(controller, "dimGroup", ui.Find("DialogLayer/DimOverlay").GetComponent<CanvasGroup>());
            Set(controller, "quitDialog", ui.Find("DialogLayer/QuitConfirm").GetComponent<ConfirmDialogView>());
            Set(controller, "revertDialog", ui.Find("DialogLayer/RevertPrompt").GetComponent<ConfirmDialogView>());
            Set(controller, "settingsPanel", ui.Find("SafeArea/SettingsPanel").GetComponent<SettingsPanelView>());
            Set(controller, "fader", ui.Find("FadeLayer").GetComponent<ScreenFader>());
            Set(controller, "versionLabel", ui.Find("SafeArea/Version").GetComponent<TMP_Text>());
            Set(controller, "loadError", ui.Find("SafeArea/LoadError").GetComponent<TMP_Text>());
            var roots = new UnityEngine.UI.Selectable[3];
            string[] names = { "Start", "Settings", "Quit" };
            for (int i = 0; i < roots.Length; i++) roots[i] = ui.Find("SafeArea/Root/MenuList/" + names[i]).GetComponent<UnityEngine.UI.Button>();
            SetArray(controller, "rootItems", roots);
            Transform content = ui.Find("SafeArea/SettingsPanel/Content");
            var rows = new UnityEngine.UI.Selectable[9];
            for (int i = 0; i < 5; i++) rows[i] = content.Find("Slider" + i).GetComponent<MenuSlider>();
            for (int i = 0; i < 3; i++) rows[5 + i] = content.Find("Cycle" + i).GetComponent<MenuCycleRow>();
            rows[8] = content.Find("Back").GetComponent<UnityEngine.UI.Button>(); SetArray(controller, "settingsItems", rows);
            Set(systems.GetComponent<MenuInputRouter>(), "eventSystem", eventSystem);
            eventSystem.firstSelectedGameObject = roots[0].gameObject;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(eventSystem);
            PrefabUtility.RecordPrefabInstancePropertyModifications(systems.GetComponent<MenuInputRouter>());
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            bool hasGameplay = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath) continue;
                bool gameplay = scene.path == GameplayPath; hasGameplay |= gameplay;
                scenes.Add(new EditorBuildSettingsScene(scene.path, gameplay || scene.enabled));
            }
            if (!hasGameplay) scenes.Add(new EditorBuildSettingsScene(GameplayPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static int EnsureMenuArtLayer()
        {
            int existing = LayerMask.NameToLayer("MenuArt");
            if (existing >= 0) return existing;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = manager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = "MenuArt"; manager.ApplyModifiedPropertiesWithoutUndo(); return i;
            }
            throw new InvalidOperationException("No free user layer for MenuArt.");
        }

        private static InputActionReference ActionReference(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ActionsPath))
                if (asset is InputActionReference reference && reference.action != null
                    && reference.action.actionMap.name == "UI" && reference.action.name == name) return reference;
            return null;
        }
        private static T Asset<T>(string path, out bool created) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); created = asset == null;
            if (created) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        private static GameObject Instantiate(string name, Transform parent)
        {
            var result = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab"));
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }
        private static void SavePrefab(GameObject root, string name)
        {
            try
            {
                // Explicitly capture direct UI property setters on nested leaf instances too.
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                        if (component is Transform) PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
                    }
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab", out bool success);
                if (!success) throw new InvalidOperationException("Cannot save prefab " + name);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Size(RectTransform rect, float width, float height) => rect.sizeDelta = new Vector2(width, height);
        private static void Centre(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = Vector2.zero;
        }
        private static void Top(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(0, -y);
        }
        private static Canvas Canvas(GameObject root, RenderMode mode, int order, bool raycaster, bool nested = false)
        {
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = mode; canvas.sortingOrder = order;
            canvas.pixelPerfect = true; canvas.overrideSorting = nested;
            if (!nested)
            {
                var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            }
            if (raycaster) root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return canvas;
        }
        private static UnityEngine.UI.Image Image(Transform parent, string name, Sprite sprite, Color color,
            float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f); rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(x, y); Size(rect, width, height);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }
        private static GameObject Row(string name, float width, float height)
        {
            RectTransform rect = Rect(name, null); Size(rect, width, height);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.clear; image.raycastTarget = true;
            return rect.gameObject;
        }
        private static void Style(UnityEngine.UI.Selectable selectable)
        {
            selectable.transition = UnityEngine.UI.Selectable.Transition.None;
            selectable.targetGraphic = selectable.GetComponent<UnityEngine.UI.Image>();
            selectable.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit };
        }
        private static TMP_Text Label(Transform parent, string name, string key, float size,
            float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f); rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(x, y); Size(rect, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = theme.UiFont; label.fontSize = size; label.color = theme.Text;
            label.enableAutoSizing = true; label.fontSizeMin = theme.MinimumFontSize; label.fontSizeMax = size;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
            if (key != null) Localize(label, key); else label.text = string.Empty;
            return label;
        }
        private static void Localize(TMP_Text label, string key)
        {
            LocalizedText localized = label.GetComponent<LocalizedText>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
            Set(localized, "strings", strings); Set(localized, "key", key); Set(localized, "label", label);
            label.text = strings.Get(key);
        }
        private static void LabelTop(Transform parent, string name, string key, float size, float y, float height)
        {
            TMP_Text label = Label(parent, name, key, size, 0, 0, 700, height); Top(label.rectTransform, y + height / 2);
        }
        private static void Group(Transform parent, string name, string key, float y)
        {
            TMP_Text label = Label(parent, name + "Group", key, theme.GroupLabelSize, 0, 0, 700, 28);
            Top(label.rectTransform, y); label.color = theme.TextDim; label.fontStyle = FontStyles.Bold; label.characterSpacing = theme.GroupTracking;
            var divider = Image(parent, name + "Divider", null, new Color(theme.TextDim.r, theme.TextDim.g, theme.TextDim.b, 0.4f), 0, 0, 700, 3);
            Top(divider.rectTransform, y + 20);
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string key,
            float x, float y, float width, float size = 44)
        {
            GameObject root = Instantiate("MenuButton", parent); root.name = name;
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f); rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(x, y); Size(rect, width, 68);
            TMP_Text label = root.transform.Find("Label").GetComponent<TMP_Text>();
            Size(label.rectTransform, width - 70, 64); label.fontSizeMax = size; label.fontSize = size; Localize(label, key);
            return root.GetComponent<UnityEngine.UI.Button>();
        }
        private static UnityEngine.UI.Button Arrow(Transform parent, string name, string glyph, float x)
        {
            GameObject row = Row(name, 56, 56); row.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)row.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f); rect.anchoredPosition = new Vector2(x, 0);
            var button = row.AddComponent<UnityEngine.UI.Button>(); Style(button);
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            TMP_Text label = Label(row.transform, "Glyph", null, 28, 0, 0, 56, 56); label.text = glyph; label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static void Focus(UnityEngine.UI.Selectable selectable, TMP_Text label, bool band, UnityEngine.UI.Image fill)
        {
            var sigil = Image(selectable.transform, "Sigil", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSeal"), Color.white, 5, 0, 27, 33);
            var underline = Image(label.transform, "Underline", MainMenuPlaceholderArt.Sprite("FocusUnderline"), Color.white, 0, 0, 180, 6);
            underline.rectTransform.anchorMin = underline.rectTransform.anchorMax = Vector2.zero;
            underline.rectTransform.anchoredPosition = new Vector2(0, -3); underline.type = UnityEngine.UI.Image.Type.Sliced;
            underline.pixelsPerUnitMultiplier = 1f / 3f; sigil.enabled = underline.enabled = false;
            UnityEngine.UI.Image rowBand = null;
            if (band)
            {
                rowBand = Image(selectable.transform, "FocusBand", null, theme.RowBand, 0, 0, 0, 0);
                Stretch(rowBand.rectTransform); rowBand.transform.SetAsFirstSibling(); rowBand.enabled = false;
            }
            var visual = selectable.gameObject.AddComponent<MenuFocusVisual>();
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = theme.Accent; shadow.effectDistance = new Vector2(0, -3); shadow.enabled = false;
            Set(visual, "theme", theme); Set(visual, "selectable", selectable); Set(visual, "label", label);
            Set(visual, "sigil", sigil); Set(visual, "underline", underline); Set(visual, "rowBand", rowBand); Set(visual, "sliderFill", fill);
            Set(visual, "pressedSigil", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSealPressed")); Set(visual, "pressShadow", shadow);
        }
        private static void Wrap(UnityEngine.UI.Selectable[] items, bool horizontal = false)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = items[(i + items.Length - 1) % items.Length], selectOnDown = items[(i + 1) % items.Length]
                };
                if (horizontal) { navigation.selectOnLeft = navigation.selectOnUp; navigation.selectOnRight = navigation.selectOnDown; }
                items[i].navigation = navigation;
            }
        }
        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string property, string value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray<T>(Object target, string property, T[] values) where T : Object
        {
            var serialized = new SerializedObject(target); SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
