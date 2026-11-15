using System;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Settings;
using RetroFPS.Tests.EditMode;
using RetroFPS.UI.HUD;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetroFPS.Tests
{
    /// <summary>Exercises the boss view and production binding lifecycle without generated prefab dependencies.</summary>
    public sealed class BossBarViewTests
    {
        private LocalizationHudTestContext context;
        private GameObject root, plate;
        private BossBarView view;
        private HudBindings bindings;
        private TMP_Text name;
        private UnityEngine.UI.Image fill, first, second;

        /// <summary>Builds an isolated view with the builder's real bilingual string manifest.</summary>
        [SetUp]
        public void SetUp()
        {
            BossEvents.RaiseBossDisengaged();
            context = new LocalizationHudTestContext();
            root = new GameObject("Boss HUD test"); root.SetActive(false);
            plate = new GameObject("Plate", typeof(RectTransform)); plate.transform.SetParent(root.transform, false);
            name = Child<TextMeshProUGUI>("Name");
            fill = Child<UnityEngine.UI.Image>("Health");
            first = Child<UnityEngine.UI.Image>("First"); second = Child<UnityEngine.UI.Image>("Second");
            fill.type = UnityEngine.UI.Image.Type.Filled;
            view = root.AddComponent<BossBarView>();
            Set(view, "strings", context.Table); Set(view, "plate", plate); Set(view, "nameText", name);
            Set(view, "healthFill", fill); Set(view, "firstPip", first); Set(view, "secondPip", second);
            view.Initialize();
            bindings = root.AddComponent<HudBindings>(); Set(bindings, "bossView", view);
        }

        /// <summary>Removes subscriptions and restores the prior localization composition.</summary>
        [TearDown]
        public void TearDown()
        {
            if (bindings != null) Call(bindings, "OnDisable");
            BossEvents.RaiseBossDisengaged();
            if (root != null) Object.DestroyImmediate(root);
            context?.Dispose();
        }

        /// <summary>Initialization and a boss-free binding snapshot keep the bar hidden.</summary>
        [Test]
        public void HiddenByDefaultAndWithoutEngagedBoss()
        {
            Assert.That(plate.activeSelf, Is.False);
            Call(bindings, "OnEnable");
            Assert.That(plate.activeSelf, Is.False);
        }

        /// <summary>Shows full health, clamps health updates, refills once per new phase and clears on hide.</summary>
        [Test]
        public void ShowHealthPhaseAndHideUpdatePresentation()
        {
            view.Show("boss.judge.name", 100f);
            Assert.That(plate.activeSelf, Is.True); Assert.That(fill.fillAmount, Is.EqualTo(1f));
            Assert.That(first.enabled, Is.True); Assert.That(second.enabled, Is.False);
            view.SetHealth(25f, 100f); Assert.That(fill.fillAmount, Is.EqualTo(0.25f));
            view.SetPhase(2); Assert.That(fill.fillAmount, Is.EqualTo(1f)); Assert.That(second.enabled, Is.True);
            view.SetHealth(30f, 60f); view.SetPhase(2);
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f), "Repeated phase snapshots must not refill damage.");
            view.SetHealth(200f, 100f); Assert.That(fill.fillAmount, Is.EqualTo(1f));
            view.SetHealth(-5f, 100f); Assert.That(fill.fillAmount, Is.Zero);
            view.SetHealth(5f, 0f); Assert.That(fill.fillAmount, Is.Zero);
            view.Hide(); view.SetHealth(100f, 100f); view.SetPhase(2);
            Assert.That(plate.activeSelf, Is.False); Assert.That(fill.fillAmount, Is.Zero);
            Assert.That(first.enabled, Is.False); Assert.That(second.enabled, Is.False);
            Assert.That(name.text, Is.Empty);
        }

        /// <summary>Production event raises update the view and production language refresh preserves health and pips.</summary>
        [Test]
        public void BindingsForwardAllEventsAndRefreshLanguage()
        {
            Call(bindings, "OnEnable");
            BossEvents.RaiseBossEngaged("boss.judge.name", 200f);
            BossEvents.RaiseBossHealthChanged(50f, 200f);
            Assert.That(plate.activeSelf, Is.True); Assert.That(fill.fillAmount, Is.EqualTo(0.25f));
            BossEvents.RaiseBossPhaseChanged(2);
            Assert.That(fill.fillAmount, Is.EqualTo(1f)); Assert.That(second.enabled, Is.True);
            BossEvents.RaiseBossHealthChanged(20f, 80f);
            context.Service.SetLanguage(LanguageSetting.English);
            Assert.That(name.text, Is.EqualTo("THE JUDGE"));
            context.Service.SetLanguage(LanguageSetting.Turkish);
            Assert.That(name.text, Is.EqualTo("YARGI\u00C7"));
            Assert.That(fill.fillAmount, Is.EqualTo(0.25f)); Assert.That(second.enabled, Is.True);
            BossEvents.RaiseBossDisengaged(); Assert.That(plate.activeSelf, Is.False);
        }

        /// <summary>Every boss subscription is removed on disable; reenable snapshots changes missed while disabled.</summary>
        [Test]
        public void DisableUnsubscribesAndReenableRestoresLatestSnapshot()
        {
            Call(bindings, "OnEnable"); Call(bindings, "OnEnable");
            AssertSubscriptions(1);
            BossEvents.RaiseBossEngaged("boss.judge.name", 100f);
            BossEvents.RaiseBossHealthChanged(40f, 100f);
            Call(bindings, "OnDisable"); AssertSubscriptions(0);
            BossEvents.RaiseBossPhaseChanged(2); BossEvents.RaiseBossHealthChanged(15f, 60f);
            Assert.That(fill.fillAmount, Is.EqualTo(0.4f)); Assert.That(second.enabled, Is.False);
            Call(bindings, "OnEnable"); AssertSubscriptions(1);
            Assert.That(fill.fillAmount, Is.EqualTo(0.25f)); Assert.That(second.enabled, Is.True);
            Call(bindings, "OnDisable"); BossEvents.RaiseBossDisengaged();
            Assert.That(plate.activeSelf, Is.True, "Disabled bindings must not receive disengage.");
            Call(bindings, "OnEnable"); Assert.That(plate.activeSelf, Is.False);
        }

        private void AssertSubscriptions(int expected)
        {
            foreach (string eventName in new[] { "OnBossEngaged", "OnBossHealthChanged", "OnBossPhaseChanged", "OnBossDisengaged" })
            {
                var listeners = (Delegate)typeof(BossEvents).GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                int count = 0;
                if (listeners != null)
                    foreach (Delegate listener in listeners.GetInvocationList()) if (ReferenceEquals(listener.Target, bindings)) count++;
                Assert.That(count, Is.EqualTo(expected), eventName);
            }
        }

        private T Child<T>(string label) where T : Component
        {
            var child = new GameObject(label, typeof(RectTransform), typeof(T));
            child.transform.SetParent(plate.transform, false); return child.GetComponent<T>();
        }
        private static void Set(object target, string field, object value) => LocalizationHudTestContext.Set(target, field, value);
        private static void Call(object target, string method) => LocalizationHudTestContext.Call(target, method);
    }
}
