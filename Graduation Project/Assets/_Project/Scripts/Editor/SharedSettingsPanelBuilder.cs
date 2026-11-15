using System;
using RetroFPS.UI.Menu;
using RetroFPS.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static RetroFPS.Editor.MainMenuBuilder;

namespace RetroFPS.Editor
{
    /// <summary>Single settings authoring path; extracts existing authored rows without writing any scene.</summary>
    public static class SharedSettingsPanelBuilder
    {
        /// <summary>The nested settings asset used by both menu hosts.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Shared/SettingsPanel.prefab";
        private const string CanvasPath = "Assets/_Project/Prefabs/UI/MainMenu/MainMenuCanvas.prefab";
        private static MenuTheme theme;
        private static StringTable strings;

        /// <summary>Creates or validates the asset, preserving existing GUIDs, copy, styling and row values.</summary>
        public static GameObject Build()
        {
            theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/_Project/Data/UI/MenuTheme_KanVeKemik.asset");
            strings = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/_Project/Data/UI/StringTable_MainMenu_tr.asset");
            if (theme == null || strings == null) throw new InvalidOperationException("Main-menu theme and strings must exist before settings extraction.");
            UseSharedPresentation(theme, strings);
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI/Shared"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/UI", "Shared");
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject root = null;
            bool loaded = existing != null;
            try
            {
                if (loaded) root = PrefabUtility.LoadPrefabContents(PrefabPath);
                else
                {
                    GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
                    Transform inline = canvas != null ? canvas.transform.Find("SafeArea/SettingsPanel") : null;
                    root = inline != null ? Object.Instantiate(inline.gameObject) : AuthorPanel();
                    root.name = "SettingsPanel";
                }
                root.SetActive(false);
                SettingsPanelView view = root.GetComponent<SettingsPanelView>();
                Set(view, "group", root.GetComponent<CanvasGroup>());
                SetArray(view, "orderedSelectables", Ordered(root.transform));
                ValidateSettingsLayout((RectTransform)root.transform);
                root.SetActive(true);
                Save(root, PrefabPath);
            }
            finally
            {
                if (root != null) { if (loaded) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Replaces only the obsolete inline subtree, retaining canvas, root list and dialog local IDs.</summary>
        public static void PatchMainMenuCanvas()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath) == null)
                throw new InvalidOperationException("MainMenuCanvas.prefab is missing.");
            GameObject shared = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (shared == null) throw new InvalidOperationException("Build shared settings first.");
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                Transform safe = root.transform.Find("SafeArea");
                Transform error = safe.Find("LoadError");
                if (error != null)
                {
                    LocalizedText localized = error.GetComponent<LocalizedText>();
                    if (localized != null) Object.DestroyImmediate(localized);
                }
                Transform old = safe.Find("SettingsPanel");
                if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == shared)
                { Save(root, CanvasPath); return; }
                Vector3 position = old != null ? old.localPosition : Vector3.zero;
                int sibling = old != null ? old.GetSiblingIndex() : safe.childCount;
                GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(shared, safe);
                panel.name = "SettingsPanel";
                panel.transform.localPosition = position;
                panel.transform.SetSiblingIndex(sibling);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                Save(root, CanvasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static UnityEngine.UI.Selectable[] Ordered(Transform panel)
        {
            string[] names = { "Language", "Slider0", "Slider1", "Slider2", "Slider3", "Slider4", "Cycle0", "Cycle1", "Cycle2", "Back" };
            var order = new UnityEngine.UI.Selectable[names.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = panel.Find("Content/" + names[i]).GetComponent<UnityEngine.UI.Selectable>();
            return order;
        }
        private static void Save(GameObject root, string path)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Cannot save shared settings: " + path);
        }
        private static GameObject AuthorPanel()
        {
            RectTransform panel = Rect("SettingsPanel", null);
            panel.gameObject.SetActive(false);
            panel.anchorMin = panel.anchorMax = Vector2.zero; panel.pivot = Vector2.zero;
            // At 720p the 1920x1080 canvas scales by 2/3: 36 units retain 24 screen pixels.
            // Safe area is 1728x972 units; this panel ends at y=960, leaving room for Version.
            panel.anchoredPosition = new Vector2(0, 60); Size(panel, 1040, 900);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0; panelGroup.interactable = panelGroup.blocksRaycasts = false;
            var frame = Image(panel, "Frame", MainMenuPlaceholderArt.Sprite("PanelFrame_A"), Color.white, 0, 0, 0, 0);
            Stretch(frame.rectTransform); frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f / 3f;
            var jewel = Image(panel, "Jewel", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSeal"), Color.white, 0, 0, 45, 21);
            jewel.rectTransform.anchorMin = jewel.rectTransform.anchorMax = new Vector2(0.5f, 1); jewel.rectTransform.anchoredPosition = Vector2.zero;
            RectTransform content = Rect("Content", panel); Stretch(content); content.offsetMin = new Vector2(30, 24); content.offsetMax = new Vector2(-30, -24);
            LabelTop(content, "Title", "settings.title", theme.SettingsTitleSize, 0, 54);
            Group(content, "General", "settings.group.general", 86);
            GameObject languageObject = Instantiate("MenuCycleRow", content);
            languageObject.name = "Language";
            Top((RectTransform)languageObject.transform, 130);
            Localize(languageObject.transform.Find("Label").GetComponent<TMP_Text>(), "settings.language");
            var language = languageObject.GetComponent<MenuCycleRow>();
            language.SetOptions(new[] { strings.Get("language.tr", LanguageSetting.Turkish),
                strings.Get("language.en", LanguageSetting.English) });
            Group(content, "Audio", "settings.group.audio", 180);
            string[] sliderKeys = { "settings.master_volume", "settings.music_volume", "settings.sfx_volume", "settings.ui_volume", "settings.mouse_sensitivity" };
            var sliders = new MenuSlider[5]; var values = new TMP_Text[5];
            float[] sliderY = { 224, 280, 336, 392, 488 };
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
            Group(content, "Controls", "settings.group.controls", 444);
            Group(content, "Display", "settings.group.display", 540);
            string[] cycleKeys = { "settings.window_mode", "settings.resolution", "settings.reduce_motion" };
            var cycles = new MenuCycleRow[3];
            for (int i = 0; i < cycles.Length; i++)
            {
                GameObject row = Instantiate("MenuCycleRow", content);
                row.name = "Cycle" + i;
                Top((RectTransform)row.transform, 584 + i * 56);
                Localize(row.transform.Find("Label").GetComponent<TMP_Text>(), cycleKeys[i]);
                cycles[i] = row.GetComponent<MenuCycleRow>();
                TMP_Text value = row.transform.Find("Value").GetComponent<TMP_Text>();
                value.text = string.Empty;
            }
            var back = Button(content, "Back", "settings.back", 0, 0, 980, theme.SettingsLabelSize);
            Top((RectTransform)back.transform, 786);
            var order = new UnityEngine.UI.Selectable[] { language, sliders[0], sliders[1], sliders[2], sliders[3], sliders[4], cycles[0], cycles[1], cycles[2], back };
            Wrap(order);
            var view = panel.gameObject.AddComponent<SettingsPanelView>();
            Set(view, "strings", strings); SetArray(view, "sliders", sliders); SetArray(view, "percentages", values);
            Set(view, "language", language);
            Set(view, "windowMode", cycles[0]); Set(view, "resolution", cycles[1]); Set(view, "reduceMotion", cycles[2]);
            Set(view, "group", panelGroup); SetArray(view, "orderedSelectables", order);
            ValidateSettingsLayout(panel);
            return panel.gameObject;

        }
    }
}
