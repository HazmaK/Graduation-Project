using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using RetroFPS.Localization;
using RetroFPS.Run;
using RetroFPS.Settings;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using RetroFPS.Weapons;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using SettingsService = RetroFPS.Settings.SettingsService;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Exercises real view refresh through the production settings event, including hidden children.</summary>
    public sealed class LocalizationHudSwitchTests
    {
        private LocalizationHudTestContext context;
        private GameObject root, hud;
        private HudBindings bindings;
        private AbilityHudView ability;
        private EncounterRunHudView encounter;
        private WeaponAmmoHudView weapon;
        private HudBannerView banner;
        private BossBarView boss;
        private TrueDeathView death;
        private float timeScale, fixedDelta;
        private CursorLockMode cursorLock;
        private bool cursorVisible;

        /// <summary>Composes real prefab views with isolated, memory-only bilingual data.</summary>
        [SetUp]
        public void SetUp()
        {
            timeScale = Time.timeScale; fixedDelta = Time.fixedDeltaTime;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            context = new LocalizationHudTestContext();
            root = new GameObject("Localization HUD tests"); root.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab");
            Assert.That(prefab, Is.Not.Null, "The authored CombatHud prefab is required.");
            hud = Object.Instantiate(prefab, root.transform);
            // TMP needs its edit-mode Awake before a hidden label can be prewarmed.
            Transform[] nodes = hud.GetComponentsInChildren<Transform>(true);
            var visibility = new bool[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) visibility[i] = nodes[i].gameObject.activeSelf;
            root.SetActive(true);
            for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.SetActive(true);
            root.SetActive(false);
            for (int i = nodes.Length - 1; i >= 0; i--) nodes[i].gameObject.SetActive(visibility[i]);
            ability = hud.GetComponentInChildren<AbilityHudView>(true);
            encounter = hud.GetComponentInChildren<EncounterRunHudView>(true);
            weapon = hud.GetComponentInChildren<WeaponAmmoHudView>(true);
            banner = hud.GetComponentInChildren<HudBannerView>(true);
            boss = hud.GetComponentInChildren<BossBarView>(true);
            Assert.That(boss, Is.Not.Null, "Rebuild Combat HUD to include BossBarView.");
            death = hud.GetComponentInChildren<TrueDeathView>(true);
            foreach (object view in new object[] { ability, encounter, weapon, banner, boss })
                LocalizationHudTestContext.Set(view, "strings", context.Table);
            ability.Initialize(null, null); encounter.Initialize(); weapon.Initialize(null); banner.Initialize();
            boss.Initialize();
            bindings = root.AddComponent<HudBindings>();
            LocalizationHudTestContext.Set(bindings, "abilityView", ability);
            LocalizationHudTestContext.Set(bindings, "encounterView", encounter);
            LocalizationHudTestContext.Set(bindings, "weaponView", weapon);
            LocalizationHudTestContext.Set(bindings, "bannerView", banner);
            LocalizationHudTestContext.Set(bindings, "bossView", boss);
            LocalizationHudTestContext.Set(bindings, "deathView", death);
            LocalizationHudTestContext.Call(bindings, "OnEnable");
        }

        /// <summary>Removes view subscriptions and restores the original global composition and timing.</summary>
        [TearDown]
        public void TearDown()
        {
            if (bindings != null) LocalizationHudTestContext.Call(bindings, "OnDisable");
            if (root != null) Object.DestroyImmediate(root);
            context?.Dispose();
            Time.timeScale = timeScale; Time.fixedDeltaTime = fixedDelta;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }

        /// <summary>Hidden and visible dynamic words refresh with no reset of their current semantic state.</summary>
        [Test]
        public void LiveHudSwitchPreservesCountersChargeSuccessAndFeedbackTimers()
        {
            boss.Show("boss.judge.name", 100f); boss.SetPhase(2); boss.SetHealth(30f, 100f);
            ability.SetDash(1, 3); ability.ParrySucceeded(true);
            encounter.SetRun(new RunState(7)); encounter.SetPhase(RunPhase.InAraf);
            encounter.SetCharge(true); encounter.SetWave(2, 5); encounter.SetRemaining(1, 8);
            encounter.SetRooms(2, 10, false); encounter.SetRooms(3, 10, true);
            encounter.EncounterCompleted();
            // Exercise production weapon selection once. Refresh must never call SetWeapon again.
            var source = root.AddComponent<RepeaterArbalestWeapon>();
            weapon.SetWeapon(source); weapon.AmmoChanged(AmmoType.Bolts, 0, 20); weapon.AmmoCollected(AmmoType.Bolts, 1);
            object successUntil = LocalizationHudTestContext.Get(ability, "successUntil");
            object roomUntil = LocalizationHudTestContext.Get(encounter, "roomFlashUntil");
            object pickupUntil = LocalizationHudTestContext.Get(weapon, "pickupUntil");
            object fade = LocalizationHudTestContext.Get(encounter, "completedFade");
            context.Service.SetLanguage(LanguageSetting.English);
            Assert.That(Text(boss, "nameText"), Is.EqualTo("THE JUDGE"));
            Assert.That(((UnityEngine.UI.Image)LocalizationHudTestContext.Get(boss, "healthFill")).fillAmount, Is.EqualTo(0.3f));
            Assert.That(Text(ability, "dashCurrent"), Is.EqualTo("DASH 1/3"));
            Assert.That(Text(ability, "parryState"), Is.EqualTo("PARRIED"));
            Assert.That(Text(encounter, "waveText"), Is.EqualTo("WAVE 2/5"));
            Assert.That(Text(encounter, "remainingText"), Is.EqualTo("LEFT 1"));
            Assert.That(Text(encounter, "roomsText"), Is.EqualTo("ROOMS 3/10"));
            Assert.That(Text(encounter, "chargeText"), Is.EqualTo("PURGATORY: USED"));
            Assert.That(Text(weapon, "weaponName"), Is.EqualTo("ARBALEST"));
            Assert.That(Text(weapon, "warningText"), Is.EqualTo("EMPTY"));
            Assert.That(LocalizationHudTestContext.Get(ability, "successUntil"), Is.EqualTo(successUntil));
            Assert.That(LocalizationHudTestContext.Get(encounter, "roomFlashUntil"), Is.EqualTo(roomUntil));
            Assert.That(LocalizationHudTestContext.Get(encounter, "completedFade"), Is.SameAs(fade));
            Assert.That(LocalizationHudTestContext.Get(encounter, "hiding"), Is.True);
            Assert.That(LocalizationHudTestContext.Get(weapon, "pickupUntil"), Is.EqualTo(pickupUntil));
            Assert.That(LocalizationHudTestContext.Get(weapon, "ammoType"), Is.EqualTo(AmmoType.Bolts));
            context.Service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(Text(boss, "nameText"), Is.EqualTo("YARGI\u00C7"));
            Assert.That(Text(ability, "parryState"), Is.EqualTo("PARRY!"));
            Assert.That(Text(weapon, "warningText"), Is.EqualTo("BOŞ"));
        }

        /// <summary>The Counter-Verdict label switches languages while its reserved cell stays hidden.</summary>
        [Test]
        public void CounterVerdictLabelSwitchesBetweenHukumAndCounterWithoutChangingVisibility()
        {
            var cell = (CanvasGroup)LocalizationHudTestContext.Get(ability, "parryCell");
            var localized = cell.transform.Find("Label").GetComponent<LocalizedText>();
            Assert.That(localized, Is.Not.Null);
            Assert.That(LocalizationHudTestContext.Get(localized, "key"), Is.EqualTo("hud.parry.label"));
            LocalizationHudTestContext.Set(localized, "strings", context.Table);
            LocalizationHudTestContext.Call(localized, "OnEnable");
            try
            {
                var label = localized.GetComponent<TMP_Text>();
                float alpha = cell.alpha;
                Assert.That(label.text, Is.EqualTo("HÜKÜM"));
                context.Service.SetLanguage(LanguageSetting.English);
                Assert.That(label.text, Is.EqualTo("PARRY"));
                Assert.That(cell.alpha, Is.EqualTo(alpha));
                context.Service.SetLanguage(LanguageSetting.Turkish);
                Assert.That(label.text, Is.EqualTo("HÜKÜM"));
                Assert.That(cell.alpha, Is.EqualTo(alpha));
            }
            finally { LocalizationHudTestContext.Call(localized, "OnDisable"); }
        }

        /// <summary>Both live lines and the eventual pending line use the new language at the original deadline.</summary>
        [Test]
        public void MergedCurrentAndPendingBannerSwitchWithoutResettingQueueOrClock()
        {
            banner.Enqueue(HudBannerKind.ArenaSealed); banner.Enqueue(HudBannerKind.Wave, 1);
            banner.Enqueue(HudBannerKind.WaveCleared, 1);
            var queue = (HudBannerQueue)LocalizationHudTestContext.Get(banner, "queue");
            HudBanner current = queue.Current, pending = queue.Pending;
            double deadline = queue.ExpiresAt;
            Assert.That(current.IsMerged, Is.True); Assert.That(queue.HasPending, Is.True);
            context.Service.SetLanguage(LanguageSetting.English);
            Assert.That(Text(banner, "firstLine"), Is.EqualTo("ARENA SEALED"));
            Assert.That(Text(banner, "secondLine"), Is.EqualTo("WAVE 1"));
            Assert.That(queue.Current, Is.EqualTo(current)); Assert.That(queue.Pending, Is.EqualTo(pending));
            Assert.That(queue.ExpiresAt, Is.EqualTo(deadline));
            queue.Tick(deadline);
            LocalizationHudTestContext.Call(banner, "Render");
            Assert.That(Text(banner, "firstLine"), Is.EqualTo("WAVE 1 CLEARED"));
            context.Service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(Text(banner, "firstLine"), Is.EqualTo("DALGA 1 TEMİZLENDİ"));
        }

        /// <summary>Unchanged language events leave caches alone and reenable catches missed language changes.</summary>
        [Test]
        public void OtherSettingsAndRepeatedLanguageDoNotRebuildCachesOrResetFeedback()
        {
            LocalizationHudTestContext.Call(bindings, "OnEnable");
            Assert.That(BindingSubscriptions(), Is.EqualTo(1));
            banner.Enqueue(HudBannerKind.Wave, 1);
            string cached = ((string[])LocalizationHudTestContext.Get(banner, "waves"))[1];
            LocalizationHudTestContext.Set(ability, "dashFlashUntil", 12345f);
            LocalizationHudTestContext.Set(weapon, "pickupUntil", 12345f);
            context.Service.SetVolume("masterVolume", 0.4f); context.Service.SetSensitivity(0.4f);
            context.Service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(((string[])LocalizationHudTestContext.Get(banner, "waves"))[1], Is.SameAs(cached));
            Assert.That(LocalizationHudTestContext.Get(ability, "dashFlashUntil"), Is.EqualTo(12345f));
            Assert.That(LocalizationHudTestContext.Get(weapon, "pickupUntil"), Is.EqualTo(12345f));
            LocalizationHudTestContext.Call(bindings, "OnDisable");
            Assert.That(BindingSubscriptions(), Is.Zero);
            context.Service.SetLanguage(LanguageSetting.English);
            LocalizationHudTestContext.Call(bindings, "OnEnable");
            Assert.That(BindingSubscriptions(), Is.EqualTo(1));
            Assert.That(((string[])LocalizationHudTestContext.Get(banner, "waves"))[1], Is.EqualTo("WAVE 1"));
        }

        /// <summary>Both fade phases retain semantic captions, opacity and timing when the language changes.</summary>
        [TestCase(ArafTransition.MessageKind.Enter, "ARAF", "PURGATORY")]
        [TestCase(ArafTransition.MessageKind.Escape, "KAÇIŞ", "ESCAPE")]
        public void TransitionCaptionSwitchKeepsFadeColorsAndSlowMotion(ArafTransition.MessageKind kind, string tr, string en)
        {
            var transition = root.AddComponent<ArafTransition>();
            LocalizationHudTestContext.Call(transition, "Awake");
            LocalizationHudTestContext.Call(transition, "OnEnable");
            try
            {
                // Cancel at the first await boundary, retaining the real semantic caption during each phase.
                Assert.Throws<OperationCanceledException>(() => transition.FadeOut(Color.black, kind,
                    new CancellationToken(true)).GetAwaiter().GetResult());
                var caption = (UnityEngine.UI.Text)LocalizationHudTestContext.Get(transition, "_messageText");
                var overlay = (UnityEngine.UI.Image)LocalizationHudTestContext.Get(transition, "_overlayImage");
                caption.color = new Color(1f, 1f, 1f, 0.37f); overlay.color = new Color(0f, 0f, 0f, 0.37f);
                transition.BeginSlowMo();
                Assert.That(caption.text, Is.EqualTo(tr));
                context.Service.SetLanguage(LanguageSetting.English);
                Assert.That(caption.text, Is.EqualTo(en));
                Assert.That(caption.color.a, Is.EqualTo(0.37f)); Assert.That(overlay.color.a, Is.EqualTo(0.37f));
                Assert.That(Time.timeScale, Is.EqualTo(transition.FallTimeScale));
                Assert.Throws<OperationCanceledException>(() => transition.FadeIn(new CancellationToken(true)).GetAwaiter().GetResult());
                context.Service.SetLanguage(LanguageSetting.Turkish);
                Assert.That(caption.text, Is.EqualTo(tr)); Assert.That(caption.color.a, Is.EqualTo(0.37f));
                Assert.That(LocalizationHudTestContext.Get(transition, "_messageKind"), Is.EqualTo(kind));
                transition.Hide(); context.Service.SetLanguage(LanguageSetting.English);
                Assert.That(caption.text, Is.Empty, "Never restore the dormant death instruction.");
            }
            finally { LocalizationHudTestContext.Call(transition, "OnDisable"); }
        }

        /// <summary>One language event updates a real countdown without restarting its deadline or death lock.</summary>
        [Test]
        public void CountdownAndArmedDeathLatchSurviveTheSameLanguageEvent()
        {
            var menuTable = ScriptableObject.CreateInstance<StringTable>();
            menuTable.PopulateBilingualDefaults();
            var dialog = root.AddComponent<ConfirmDialogView>();
            var labelObject = new GameObject("Countdown", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TMP_Text>();
            LocalizationHudTestContext.Set(dialog, "strings", menuTable);
            LocalizationHudTestContext.Set(dialog, "theme", LocalizationHudTestContext.Get(death, "theme") is HudTheme hudTheme ? hudTheme.MenuTheme : null);
            LocalizationHudTestContext.Set(dialog, "countdownLabel", label);
            LocalizationHudTestContext.Call(dialog, "Awake"); LocalizationHudTestContext.Call(dialog, "OnEnable");
            try
            {
                var countdown = new RevertCountdown(); countdown.Start(); countdown.Tick(2.25f); dialog.ShowCountdown(countdown);
                death.Initialize(); death.Show();
                var gate = (TrueDeathGate)LocalizationHudTestContext.Get(death, "gate");
                double confirmAt = Time.unscaledTimeAsDouble + 1d;
                gate.ObserveInputs(false, false, false, false, true);
                Assert.That(gate.TryConfirm(confirmAt), Is.True);
                var card = (GameObject)LocalizationHudTestContext.Get(death, "deathLayer");
                var opacity = (CanvasGroup)LocalizationHudTestContext.Get(death, "cardGroup");
                float alpha = opacity.alpha;
                object armAt = LocalizationHudTestContext.Get(gate, "armAt");
                var events = UnityEngine.EventSystems.EventSystem.current;
                GameObject focus = events != null ? events.currentSelectedGameObject : null;
                context.Service.SetLanguage(LanguageSetting.English);
                Assert.That(label.text, Is.EqualTo("Reverting in 8 seconds"));
                Assert.That(countdown.Remaining, Is.EqualTo(7.75f)); Assert.That(countdown.Running, Is.True);
                Assert.That(card.activeSelf, Is.True); Assert.That(opacity.alpha, Is.EqualTo(alpha));
                Assert.That(LocalizationHudTestContext.Get(gate, "armAt"), Is.EqualTo(armAt));
                if (events != null) Assert.That(events.currentSelectedGameObject, Is.SameAs(focus));
                Assert.That(gate.Confirmed, Is.True); Assert.That(gate.TryConsumeRequest(confirmAt + 0.079d), Is.False);
                Assert.That(gate.TryConsumeRequest(confirmAt + 0.081d), Is.True); Assert.That(gate.TryConsumeRequest(confirmAt + 1d), Is.False);
            }
            finally { LocalizationHudTestContext.Call(dialog, "OnDisable"); Object.DestroyImmediate(menuTable); }
        }

        private static string Text(object view, string field) => ((TMP_Text)LocalizationHudTestContext.Get(view, field)).text;

        private int BindingSubscriptions()
        {
            var handlers = (Delegate)typeof(GameSettings).GetField("changed", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            int count = 0;
            if (handlers != null)
                foreach (Delegate handler in handlers.GetInvocationList())
                    if (ReferenceEquals(handler.Target, bindings)) count++;
            return count;
        }
    }

    /// <summary>Isolated settings/provider composition and production builder seeding; never saves assets.</summary>
    internal sealed class LocalizationHudTestContext : IDisposable
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly ISettingsService previousService;
        private readonly ILocalizedStringProvider previousProvider;
        private readonly object previousListeners;
        internal StringTable Table { get; }
        internal SettingsService Service { get; }

        internal LocalizationHudTestContext()
        {
            previousService = (ISettingsService)typeof(GameSettings).GetField("service", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            previousListeners = typeof(GameSettings).GetField("changed", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            previousProvider = LocalizedStrings.Provider;
            Table = CreateTable();
            Service = new SettingsService(new Store(), new Display(), new Audio(),
                GameSettingsData.Defaults(1920, 1080, LanguageSetting.Turkish));
            GameSettings.SetServiceForTests(null);
            GameSettings.SetServiceForTests(Service); LocalizedStrings.Register(Table);
        }

        internal static StringTable CreateTable()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            Type builder = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                builder = assembly.GetType("RetroFPS.Editor.CombatHudBuilder");
                if (builder != null) break;
            }
            Assert.That(builder, Is.Not.Null, "CombatHudBuilder must be loaded to test the production manifest.");
            FieldInfo field = builder.GetField("strings", BindingFlags.NonPublic | BindingFlags.Static);
            object previous = field.GetValue(null);
            try
            {
                field.SetValue(null, table);
                builder.GetMethod("PopulateStrings", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            finally { field.SetValue(null, previous); }
            return table;
        }

        /// <summary>Restores the previously composed service, provider and event listeners.</summary>
        public void Dispose()
        {
            GameSettings.SetServiceForTests(null);
            if (previousService != null) GameSettings.SetServiceForTests(previousService);
            typeof(GameSettings).GetField("changed", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, previousListeners);
            LocalizedStrings.Reset(); if (previousProvider != null) LocalizedStrings.Register(previousProvider);
            Object.DestroyImmediate(Table);
        }

        internal static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        internal static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        private sealed class Store : ISettingsStore
        {
            public GameSettingsData Load(GameSettingsData defaults) => defaults;
            public void Write(GameSettingsData data) { }
            public void Save() { }
        }
        private sealed class Display : IDisplayApplier { public void Apply(WindowModeSetting mode, int width, int height) { } }
        private sealed class Audio : IAudioVolumeApplier { public void Apply(GameSettingsData data) { } }
    }
}
