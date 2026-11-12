using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Exercises production spawning and pickup collection, inspecting the actual private popup pool.</summary>
    public sealed class DamageNumberLabelRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject _popupObject;
        private GameObject _inventoryObject;
        private GameObject _pickupObject;
        private DamageNumberSystem _system;
        private object _previousPopup;
        private object _previousPopupQuitting;
        private object _previousAudio;
        private object _previousAudioQuitting;

        /// <summary>Initializes real production caches without relying on EditMode lifecycle callbacks.</summary>
        [SetUp]
        public void SetUp()
        {
            _previousPopup = StaticField(typeof(DamageNumberSystem), "_instance").GetValue(null);
            _previousPopupQuitting = StaticField(typeof(DamageNumberSystem), "_isQuitting").GetValue(null);
            _previousAudio = StaticField(typeof(CombatAudio), "_instance").GetValue(null);
            _previousAudioQuitting = StaticField(typeof(CombatAudio), "_isQuitting").GetValue(null);
            StaticField(typeof(DamageNumberSystem), "_instance").SetValue(null, null);
            StaticField(typeof(DamageNumberSystem), "_isQuitting").SetValue(null, false);
            // Audio is outside this regression's scope; prevent lazy creation and scene pollution.
            StaticField(typeof(CombatAudio), "_instance").SetValue(null, null);
            StaticField(typeof(CombatAudio), "_isQuitting").SetValue(null, true);
            _popupObject = new GameObject("Popup regression");
            _popupObject.SetActive(false);
            _system = _popupObject.AddComponent<DamageNumberSystem>();
            Invoke(_system, "Awake");
        }

        /// <summary>Restores singleton state and removes every test object.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_pickupObject != null) Object.DestroyImmediate(_pickupObject);
            if (_inventoryObject != null) Object.DestroyImmediate(_inventoryObject);
            if (_popupObject != null) Object.DestroyImmediate(_popupObject);
            StaticField(typeof(DamageNumberSystem), "_instance").SetValue(null, _previousPopup);
            StaticField(typeof(DamageNumberSystem), "_isQuitting").SetValue(null, _previousPopupQuitting);
            StaticField(typeof(CombatAudio), "_instance").SetValue(null, _previousAudio);
            StaticField(typeof(CombatAudio), "_isQuitting").SetValue(null, _previousAudioQuitting);
        }

        /// <summary>Perfect-parry critical flags must produce PARRY, including zero-damage parries.</summary>
        [TestCase(0)]
        [TestCase(125)]
        [TestCase(4096)]
        public void ReflectedCriticalSpawnIsParryWithoutCritScale(int amount)
        {
            _system.SpawnDamageNumber(Vector3.zero, amount, true, true);
            Assert.That(Label(0), Is.EqualTo("PARRY " + amount));
            Assert.That(Entry<bool>(0, "IsCrit"), Is.False, "Crit scaling must be disabled on reflected entries.");
            Assert.That(Entry<bool>(0, "IsReflected"), Is.True);
            Assert.That(Entry<DamagePopupKind>(0, "Kind"), Is.EqualTo(DamagePopupKind.Reflected));
            var style = (GUIStyle)Field(_system, "_styleReflected");
            Assert.That(style.normal.textColor, Is.EqualTo((Color)Field(_system, "reflectedDamageColor")));
        }

        /// <summary>Ordinary critical hits keep their label and resolved critical scale/style kind.</summary>
        [Test]
        public void NormalCriticalSpawnRemainsCritical()
        {
            _system.SpawnDamageNumber(Vector3.zero, 42, true);
            Assert.That(Label(0), Is.EqualTo("CRIT! 42"));
            Assert.That(Entry<bool>(0, "IsCrit"), Is.True);
            Assert.That(Entry<DamagePopupKind>(0, "Kind"), Is.EqualTo(DamagePopupKind.Critical));
        }

        /// <summary>Large heals preserve the full digit fallback.</summary>
        [TestCase(4096)]
        [TestCase(123456)]
        public void LargeHealSpawnKeepsHealthSuffix(int amount)
        {
            _system.SpawnHealNumber(Vector3.zero, amount);
            Assert.That(Label(0), Is.EqualTo("+" + amount + " HP"));
            Assert.That(Entry<bool>(0, "IsHeal"), Is.True);
        }

        /// <summary>A reused slot cannot retain a reflected, critical, heal, ammo or multipart flag.</summary>
        [Test]
        public void PoolReuseClearsAllPreviousKindsAndLabelParts()
        {
            _system.SpawnDamageNumber(Vector3.zero, 4096, true, true);
            for (int i = 0; i < 31; i++) _system.SpawnHealNumber(Vector3.zero, 4096);
            _system.SpawnAmmoNumber(Vector3.zero, 7, AmmoType.Bolts);
            Assert.That(Label(0), Is.EqualTo("+7 OK"));
            Assert.That(Entry<bool>(0, "IsCrit"), Is.False);
            Assert.That(Entry<bool>(0, "IsReflected"), Is.False);
            Assert.That(Entry<bool>(0, "IsHeal"), Is.False);
            Assert.That(Entry<int>(0, "PartCount"), Is.Zero);
            for (int i = 0; i < 31; i++) _system.SpawnAmmoNumber(Vector3.zero, 9, AmmoType.Shells);
            _system.SpawnDamageNumber(Vector3.zero, 2, false);
            Assert.That(Label(0), Is.EqualTo("2"));
            Assert.That(Entry<DamagePopupKind>(0, "Kind"), Is.EqualTo(DamagePopupKind.Damage));
        }

        /// <summary>Ammo digit fallback supports the entire integer range and its own suffix.</summary>
        [TestCase(AmmoType.Bolts, "+2147483647 OK")]
        [TestCase(AmmoType.Shells, "+2147483647 SAÇMA")]
        public void LargeAmmoSpawnKeepsWholeValue(AmmoType type, string expected)
        {
            _system.SpawnAmmoNumber(Vector3.zero, int.MaxValue, type);
            Assert.That(Label(0), Is.EqualTo(expected));
        }

        /// <summary>The real pickup Collect path shows only space actually filled and never a damage/heal label.</summary>
        [TestCase(AmmoType.Bolts, "+2 OK", DamagePopupKind.Bolts)]
        [TestCase(AmmoType.Shells, "+2 SAÇMA", DamagePopupKind.Shells)]
        public void PickupCollectSpawnsActualAmmoAmount(AmmoType type, string expected, DamagePopupKind kind)
        {
            PlayerAmmoInventory inventory = CreateInventory();
            inventory.AddAmmo(type, int.MaxValue);
            inventory.TryConsumeAmmo(type, 2);
            CollectPickup(inventory, type);
            Assert.That(Label(0), Is.EqualTo(expected));
            Assert.That(Entry<int>(0, "Amount"), Is.EqualTo(2));
            Assert.That(Entry<bool>(0, "IsCrit"), Is.False);
            Assert.That(Entry<bool>(0, "IsHeal"), Is.False);
            Assert.That(Entry<DamagePopupKind>(0, "Kind"), Is.EqualTo(kind));
        }

        /// <summary>Collecting against a full inventory never spawns a popup.</summary>
        [TestCase(AmmoType.Bolts)]
        [TestCase(AmmoType.Shells)]
        public void FullInventoryPickupSpawnsNothing(AmmoType type)
        {
            PlayerAmmoInventory inventory = CreateInventory();
            inventory.AddAmmo(type, int.MaxValue);
            CollectPickup(inventory, type);
            Assert.That((int)Field(_system, "_nextIndex"), Is.Zero);
            Assert.That(Entry<bool>(0, "Active"), Is.False);
        }

        private PlayerAmmoInventory CreateInventory()
        {
            _inventoryObject = new GameObject("Ammo regression inventory");
            _inventoryObject.SetActive(false);
            var inventory = _inventoryObject.AddComponent<PlayerAmmoInventory>();
            Invoke(inventory, "Awake");
            return inventory;
        }

        private void CollectPickup(PlayerAmmoInventory inventory, AmmoType type)
        {
            _pickupObject = new GameObject("Ammo regression pickup");
            _pickupObject.SetActive(false);
            var pickup = _pickupObject.AddComponent<AmmoPickup>();
            SetField(pickup, "ammoType", type);
            SetField(pickup, "ammoAmount", 10);
            SetField(pickup, "_playerInventory", inventory);
            // Production schedules destruction in Play Mode; EditMode logs this one expected diagnostic.
            LogAssert.Expect(LogType.Error, new Regex("Destroy.*(edit mode|DestroyImmediate)", RegexOptions.Singleline));
            Invoke(pickup, "Collect");
        }

        private T Entry<T>(int index, string name)
        {
            object entry = ((Array)Field(_system, "_pool")).GetValue(index);
            return (T)entry.GetType().GetProperty(name, PrivateInstance | BindingFlags.Public).GetValue(entry);
        }

        private string Label(int index)
        {
            int count = Entry<int>(index, "PartCount");
            if (count == 0) return ((GUIContent[])Field(_system, "_contents"))[index].text;
            var parts = (GUIContent[])Field(_system, "_largeLabelParts");
            var result = new StringBuilder();
            for (int i = 0; i < count; i++) result.Append(parts[index * 12 + i].text);
            return result.ToString();
        }

        private static FieldInfo StaticField(Type type, string name) => type.GetField(name, PrivateStatic);
        private static object Field(object target, string name) => target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
    }
}
