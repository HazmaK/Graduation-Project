using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using SettingsService = RetroFPS.Settings.SettingsService;

namespace RetroFPS.Tests
{
    /// <summary>Real TMP menu refresh, lifecycle and warmed allocation regressions.</summary>
    public sealed class LocalizationMenuSwitchTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public GameSettingsData Load(GameSettingsData defaults) => defaults;
            public void Write(GameSettingsData data) { }
            public void Save() { }
        }
        private sealed class DisplayStub : IDisplayApplier
        {
            public void Apply(WindowModeSetting mode, int width, int height) { }
        }
        private sealed class AudioStub : IAudioVolumeApplier
        {
            public void Apply(GameSettingsData data) { }
        }

        private GameObject root;
        private StringTable strings;
        private TMP_FontAsset font;
        private MenuTheme theme;
        private SettingsService service;
        private readonly List<MonoBehaviour> lifecycleViews = new List<MonoBehaviour>();

        [SetUp]
        public void SetUp()
        {
            GameSettings.SetServiceForTests(null);
            service = new SettingsService(new MemoryStore(), new DisplayStub(), new AudioStub(),
                GameSettingsData.Defaults(1280, 720, LanguageSetting.Turkish));
            GameSettings.SetServiceForTests(service);
            strings = ScriptableObject.CreateInstance<StringTable>();
            strings.PopulateBilingualDefaults();
            theme = ScriptableObject.CreateInstance<MenuTheme>();
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            Assert.That(source, Is.Not.Null, "TMP Essentials font must be present.");
            font = TMP_FontAsset.CreateFontAsset(source);
            root = new GameObject("LocalizationMenuTests", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = lifecycleViews.Count - 1; i >= 0; i--)
            {
                MonoBehaviour view = lifecycleViews[i];
                if (view == null) continue;
                if (view.isActiveAndEnabled) Call(view, "OnDisable");
                if (view is SettingsPanelView) Call(view, "OnDestroy");
            }
            lifecycleViews.Clear();
            if (root != null) Object.DestroyImmediate(root);
            GameSettings.SetServiceForTests(null);
            if (strings != null) Object.DestroyImmediate(strings);
            if (theme != null) Object.DestroyImmediate(theme);
            if (font == null) return;
            Texture2D[] atlases = font.atlasTextures;
            Material material = font.material;
            Object.DestroyImmediate(font);
            if (material != null) Object.DestroyImmediate(material);
            for (int i = 0; i < atlases.Length; i++)
                if (atlases[i] != null) Object.DestroyImmediate(atlases[i]);
        }

        [Test]
        public void VisibleLabelSwitchesBothWaysWithoutReenable()
        {
            TMP_Text label = Text("Start");
            LocalizedText localized = Localize(label, "menu.start");
            ActivateFixture();
            Assert.That(label.text, Is.EqualTo("BAŞLA"));
            service.SetLanguage(LanguageSetting.English);
            Assert.That(label.text, Is.EqualTo("START"));
            Assert.That(localized.isActiveAndEnabled, Is.True);
            service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(label.text, Is.EqualTo("BAŞLA"));
        }

        [Test]
        public void DisabledLabelCatchesUpOnEnable()
        {
            TMP_Text label = Text("Start");
            LocalizedText localized = Localize(label, "menu.start");
            ActivateFixture();
            label.gameObject.SetActive(false);
            Call(localized, "OnDisable");
            service.SetLanguage(LanguageSetting.English);
            Assert.That(label.text, Is.EqualTo("BAŞLA"));
            label.gameObject.SetActive(true);
            Call(localized, "OnEnable");
            Assert.That(label.text, Is.EqualTo("START"));
        }

        [Test]
        public void RepeatedEnableDisableHasExactlyOneOrNoSubscriptions()
        {
            LocalizedText localized = Localize(Text("Start"), "menu.start");
            ActivateFixture();
            for (int i = 0; i < 8; i++)
            {
                Assert.That(Subscriptions(localized), Is.EqualTo(1));
                localized.enabled = false;
                Call(localized, "OnDisable");
                Assert.That(Subscriptions(localized), Is.Zero);
                service.SetLanguage(i % 2 == 0 ? LanguageSetting.English : LanguageSetting.Turkish);
                localized.enabled = true;
                Call(localized, "OnEnable");
            }
            Call(localized, "OnDisable");
            Object.DestroyImmediate(localized);
            Assert.That(Subscriptions(localized), Is.Zero);
        }

        [Test]
        public void WarmedLanguageEventsRefreshAndMeshUpdatesAllocateZeroBytes()
        {
            TMP_Text label = Text("Start");
            LocalizedText localized = Localize(label, "menu.start");
            ActivateFixture();
            for (int i = 0; i < 64; i++)
            {
                service.SetLanguage(i % 2 == 0 ? LanguageSetting.English : LanguageSetting.Turkish);
                localized.Refresh();
                label.ForceMeshUpdate();
            }
            // All setup, reflection, assertions and JIT warm-up stay outside the measured interval.
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                service.SetLanguage(i % 2 == 0 ? LanguageSetting.English : LanguageSetting.Turkish);
                localized.Refresh();
                label.ForceMeshUpdate();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Only the warmed label path is measured, not formatted menu/HUD caches.");
            Assert.That(label.text, Is.EqualTo("BAŞLA"));
        }

        [Test]
        public void UnchangedLanguageSettingsEventLeavesLabelMeshClean()
        {
            TMP_Text label = Text("Start");
            Localize(label, "menu.start");
            ActivateFixture();
            label.ForceMeshUpdate();
            Assert.That(label.havePropertiesChanged, Is.False);
            service.SetSensitivity(0.75f);
            Assert.That(label.havePropertiesChanged, Is.False);
        }

        [Test]
        public void CountdownSwitchRedrawsCurrentSecondWithoutChangingDeadline()
        {
            TMP_Text label = Text("Countdown");
            ConfirmDialogView dialog = Child("Dialog").AddComponent<ConfirmDialogView>();
            Set(dialog, "strings", strings); Set(dialog, "theme", theme); Set(dialog, "countdownLabel", label);
            ActivateFixture();
            var countdown = new RevertCountdown();
            countdown.Start(); countdown.Tick(3.25f);
            dialog.ShowCountdown(countdown);
            Assert.That(label.text, Is.EqualTo("7 saniye içinde geri alınacak"));
            float remaining = countdown.Remaining;
            object cache = Get(dialog, "countdownStrings");
            service.SetLanguage(LanguageSetting.English);
            Assert.That(label.text, Is.EqualTo("Reverting in 7 seconds"));
            Assert.That(countdown.Remaining, Is.EqualTo(remaining));
            Assert.That(countdown.Running, Is.True);
            Assert.That(Get(dialog, "countdownStrings"), Is.SameAs(cache));
            string entry = ((string[])cache)[7];
            service.SetSensitivity(0.8f);
            Assert.That(((string[])cache)[7], Is.SameAs(entry));
            service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(label.text, Is.EqualTo("7 saniye içinde geri alınacak"));
            Assert.That(countdown.Remaining, Is.EqualTo(remaining));
            dialog.gameObject.SetActive(false);
            Call(dialog, "OnDisable");
            service.SetLanguage(LanguageSetting.English);
            dialog.gameObject.SetActive(true);
            Call(dialog, "OnEnable");
            Assert.That(label.text, Is.EqualTo("Reverting in 7 seconds"));
            Assert.That(countdown.Remaining, Is.EqualTo(remaining));
        }

        [Test]
        public void SettingsSwitchKeepsValuesFocusAndLanguageNamesWithoutDisplayPrompt()
        {
            SettingsPanelView panel = Child("Settings").AddComponent<SettingsPanelView>();
            var sliders = new MenuSlider[5];
            var percentages = new TMP_Text[5];
            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i] = Child("Slider" + i).AddComponent<MenuSlider>();
                sliders[i].maxValue = 20; sliders[i].wholeNumbers = true;
                percentages[i] = Text("Percent" + i);
            }
            MenuCycleRow language = Cycle("Language");
            MenuCycleRow mode = Cycle("Mode");
            MenuCycleRow resolution = Cycle("Resolution");
            MenuCycleRow motion = Cycle("Motion");
            Set(panel, "strings", strings); Set(panel, "sliders", sliders); Set(panel, "percentages", percentages);
            Set(panel, "language", language); Set(panel, "windowMode", mode);
            Set(panel, "resolution", resolution); Set(panel, "reduceMotion", motion);
            // The inactive controller is only the request gate; its scene composition is outside this fixture.
            MainMenuController controller = Child("ControllerGate").AddComponent<MainMenuController>();
            controller.gameObject.SetActive(false);
            var machine = new MainMenuStateMachine();
            machine.Tick(1); machine.OpenSettings(); machine.Tick(0.08f);
            Set(controller, "machine", machine);
            Set(controller, "fader", Child("Fader").AddComponent<ScreenFader>());
            panel.Bind(controller);
            EventSystem events = Child("Events").AddComponent<EventSystem>();
            // Reduced motion also keeps the isolated cycle's presentation independent of arrow geometry.
            service.SetReduceMotion(true);
            ActivateFixture();
            EventSystem.current = events;
            events.SetSelectedGameObject(language.gameObject);
            Assert.That(language.Index, Is.Zero);
            Assert.That(CycleText(language), Is.EqualTo("Türkçe"));
            Assert.That(percentages[0].text, Is.EqualTo("%100"));
            object cache = Get(panel, "percentStrings");
            string oldPercent = ((string[])cache)[20];
            service.SetSensitivity(0.75f);
            Assert.That(((string[])cache)[20], Is.SameAs(oldPercent));
            language.Next();
            Assert.That(service.Current.Language, Is.EqualTo(LanguageSetting.English));
            Assert.That(CycleText(language), Is.EqualTo("English"));
            Assert.That(percentages[0].text, Is.EqualTo("100%"));
            Assert.That(percentages[4].text, Is.EqualTo("75%"));
            Assert.That(sliders[4].value, Is.EqualTo(15));
            Assert.That(CycleText(mode), Is.EqualTo("Borderless"));
            Assert.That(CycleText(motion), Is.EqualTo("ON"));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(language.gameObject));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Language));
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Settings));
            Assert.That(service.HasPendingDisplay, Is.False);
            Assert.That(Get(panel, "percentStrings"), Is.SameAs(cache));
            language.Previous();
            Assert.That(CycleText(language), Is.EqualTo("Türkçe"));
            Assert.That(percentages[4].text, Is.EqualTo("%75"));
            Assert.That(CycleText(mode), Is.EqualTo("Çerçevesiz"));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(language.gameObject));
        }

        private void ActivateFixture()
        {
            // These runtime-only behaviours do not receive lifecycle callbacks in EditMode.
            // Drive the same Awake -> OnEnable sequence after all serialized dependencies are wired.
            // TMP and Selectable already execute in edit mode; leave their lifecycle to Unity.
            foreach (MonoBehaviour view in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (view is LocalizedText || view is ConfirmDialogView || view is SettingsPanelView)
                    Call(view, "Awake");
                else if (!(view is EventSystem)) continue;
                lifecycleViews.Add(view);
            }
            root.SetActive(true);
            foreach (MonoBehaviour view in lifecycleViews)
                if (view.isActiveAndEnabled) Call(view, "OnEnable");
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private GameObject Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private TMP_Text Text(string name)
        {
            TMP_Text label = Child(name).AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 36; label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(1000, 100);
            return label;
        }

        private LocalizedText Localize(TMP_Text label, string key)
        {
            var localized = label.gameObject.AddComponent<LocalizedText>();
            Set(localized, "strings", strings); Set(localized, "key", key); Set(localized, "label", label);
            return localized;
        }

        private MenuCycleRow Cycle(string name)
        {
            var cycle = Child(name).AddComponent<MenuCycleRow>();
            Set(cycle, "valueLabel", Text(name + "Value"));
            return cycle;
        }

        private static string CycleText(MenuCycleRow cycle) => ((TMP_Text)Get(cycle, "valueLabel")).text;
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Get(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static int Subscriptions(object target)
        {
            var handlers = (Delegate)typeof(GameSettings).GetField("changed", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (handlers == null) return 0;
            int count = 0;
            foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, target)) count++;
            return count;
        }
    }
}
