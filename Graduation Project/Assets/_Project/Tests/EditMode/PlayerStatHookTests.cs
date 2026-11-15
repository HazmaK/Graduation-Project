using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Pause;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;
using RetroFPS.Weapons;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Stat integration decisions exercised without frame loops or weapon presentation.</summary>
    public sealed class PlayerStatHookTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private StatSheet _sheet;
        private PlayerHealth _health;
        private IPauseService _previousPause;
        private object _previousAudio;
        private object _previousNumbers;
        private object _previousAudioQuitting;
        private object _previousNumbersQuitting;

        [SetUp]
        public void SetUp()
        {
            _previousPause = GamePause.Service;
            GamePause.SetService(null);
            _previousAudio = GetStatic(typeof(CombatAudio), "_instance");
            _previousNumbers = GetStatic(typeof(DamageNumberSystem), "_instance");
            _previousAudioQuitting = GetStatic(typeof(CombatAudio), "_isQuitting");
            _previousNumbersQuitting = GetStatic(typeof(DamageNumberSystem), "_isQuitting");
            SetStatic(typeof(CombatAudio), "_instance", null);
            SetStatic(typeof(DamageNumberSystem), "_instance", null);
            SetStatic(typeof(CombatAudio), "_isQuitting", true);
            SetStatic(typeof(DamageNumberSystem), "_isQuitting", true);
            _sheet = new StatSheet();
            PlayerStats.SetCurrent(_sheet);
            _health = Create<PlayerHealth>();
            // Keep objects inactive: these tests exercise health logic without feedback initialization.
            Set(_health, "_currentHealth", 100f);
            Set(_health, "_previousMaxHealth", 100f);
            Invoke(_health, "BindStats");
        }

        [TearDown]
        public void TearDown()
        {
            if (_health != null) Invoke(_health, "UnbindStats");
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            PlayerStats.SetCurrent(null);
            GamePause.SetService(_previousPause);
            SetStatic(typeof(CombatAudio), "_instance", _previousAudio);
            SetStatic(typeof(DamageNumberSystem), "_instance", _previousNumbers);
            SetStatic(typeof(CombatAudio), "_isQuitting", _previousAudioQuitting);
            SetStatic(typeof(DamageNumberSystem), "_isQuitting", _previousNumbersQuitting);
        }

        [Test]
        public void NoSheetPreservesHealthAndMovementBases()
        {
            PlayerStats.SetCurrent(null);
            Invoke(_health, "BindStats");
            _health.TakeDamage(20f, Vector3.zero, Vector3.up);
            Assert.That(_health.Heal(5f), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(85f));
            Assert.That(_health.MaxHealth, Is.EqualTo(_health.BaseMaxHealth));
            Assert.That(_health.HealthFraction, Is.EqualTo(0.85f));
            Assert.That(_health.IsOverhealed, Is.False);
            Assert.That(_health.GetComponent<PlayerMovement>().WalkSpeed, Is.EqualTo(8f));
        }

        [TestCase(80f, 50f)]
        [TestCase(125f, 75f)]
        public void MaxReductionPreservesOnlyExistingOverhealAndNotifiesHud(float current, float expected)
        {
            Set(_health, "_currentHealth", current);
            float notifiedCurrent = -1f;
            float notifiedMax = -1f;
            _health.OnHealthChanged += (value, maximum) => { notifiedCurrent = value; notifiedMax = maximum; };
            Add(StatId.MaxHealth, StatOp.Multiply, 0.5f);
            Assert.That(_health.CurrentHealth, Is.EqualTo(expected));
            Assert.That(_health.MaxHealth, Is.EqualTo(50f));
            Assert.That(_health.BaseMaxHealth, Is.EqualTo(100f));
            Assert.That(notifiedCurrent, Is.EqualTo(expected));
            Assert.That(notifiedMax, Is.EqualTo(50f));
            Assert.That(_health.HealthFraction, Is.EqualTo(expected / 50f));
            Assert.That(_health.IsOverhealed, Is.EqualTo(expected > 50f));
        }

        [Test]
        public void MaxIncreaseDoesNotHealAndZeroMaxDoesNotKill()
        {
            Set(_health, "_currentHealth", 40f);
            Add(StatId.MaxHealth, StatOp.AddFlat, 50f);
            Assert.That(_health.CurrentHealth, Is.EqualTo(40f));
            Assert.That(_health.MaxHealth, Is.EqualTo(150f));
            Add(StatId.MaxHealth, StatOp.Multiply, 0f);
            Assert.That(_health.MaxHealth, Is.EqualTo(1f));
            Assert.That(_health.CurrentHealth, Is.EqualTo(1f));
            Assert.That(_health.IsDead, Is.False);
            Assert.That(_health.HealthFraction, Is.EqualTo(1f));
            Assert.That(_health.HealthPercent, Is.EqualTo(1f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DamageTakenScalesBothDamageEntrypoints(bool unparryable)
        {
            Add(StatId.DamageTaken, StatOp.Multiply, 0.5f);
            if (unparryable) _health.TakeUnparryableDamage(20f, Vector3.zero, Vector3.up);
            else _health.TakeDamage(20f, Vector3.zero, Vector3.up);
            Assert.That(_health.CurrentHealth, Is.EqualTo(90f));
        }

        [Test]
        public void DashInvulnerabilityPrecedesDamageScaling()
        {
            var movement = _health.GetComponent<PlayerMovement>();
            Set(_health, "_movement", movement);
            Set(movement, "_isInvincible", true);
            Add(StatId.DamageTaken, StatOp.AddFlat, 500f);
            Assert.That(_health.TakeUnparryableDamage(20f, Vector3.zero, Vector3.up), Is.False);
            _health.TakeDamage(20f, Vector3.zero, Vector3.up);
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void HealingUsesReceivedMultiplierEffectiveMaxAndOverhealCap()
        {
            Add(StatId.MaxHealth, StatOp.AddFlat, 20f);
            Add(StatId.HealReceived, StatOp.Multiply, 0.5f);
            Assert.That(_health.Heal(60f), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(120f));
            Add(StatId.OverhealCap, StatOp.AddFlat, 15f);
            Assert.That(_health.Heal(100f, true, 125f), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(140f));
            Assert.That(_health.IsOverhealed, Is.True);
            Assert.That(_health.Heal(10f, true, 125f), Is.False);
        }

        [Test]
        public void OverhealCapNeverDropsBelowEffectiveMaxHealth()
        {
            Add(StatId.MaxHealth, StatOp.AddFlat, 50f);
            Add(StatId.OverhealCap, StatOp.Multiply, 0.5f);
            Assert.That(_health.MaxHealth, Is.EqualTo(150f));
            Assert.That(_health.Heal(40f, true, 125f), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(140f));
            Assert.That(_health.Heal(40f, true, 125f), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(150f));
            Assert.That(_health.Heal(10f, true, 125f), Is.False);
        }

        [Test]
        public void CardWithTwoMaxHealthModifiersNetsWithoutIntermediateClamp()
        {
            var card = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            try
            {
                card.Configure("netMax", "n", "b", "c", "f", Color.red, new[]
                {
                    new StatModifier(StatId.MaxHealth, StatOp.AddFlat, -80f),
                    new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 90f)
                });
                int calls = 0;
                float notifiedCurrent = -1f;
                float notifiedMax = -1f;
                _health.OnHealthChanged += (current, maximum) => { calls++; notifiedCurrent = current; notifiedMax = maximum; };
                Assert.That(new CardInventory(_sheet).TryAcquire(card), Is.True);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(_health.MaxHealth, Is.EqualTo(110f));
                Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
                Assert.That(notifiedCurrent, Is.EqualTo(100f));
                Assert.That(notifiedMax, Is.EqualTo(110f));
            }
            finally
            {
                Object.DestroyImmediate(card);
            }
        }

        [Test]
        public void ReviveBindsReplacementSheetAndDoesNotScaleAsHealing()
        {
            var previous = _sheet;
            _sheet = new StatSheet();
            Add(StatId.MaxHealth, StatOp.AddFlat, 40f);
            Add(StatId.HealReceived, StatOp.Multiply, 0f);
            Add(StatId.DamageDealt, StatOp.Multiply, 2f, StatCondition.HealthBelowHalf);
            PlayerStats.SetCurrent(_sheet);
            _health.Revive(Vector3.zero, Quaternion.identity);
            Assert.That(_health.CurrentHealth, Is.EqualTo(140f));
            Set(_health, "_currentHealth", 40f);
            Assert.That(PlayerStats.Get(StatId.DamageDealt, 1f), Is.EqualTo(2f));
            previous.Add(10, new StatModifier(StatId.MaxHealth, StatOp.Multiply, 0f));
            Assert.That(_health.CurrentHealth, Is.EqualTo(40f));
        }

        [Test]
        public void UnbindClearsOwnContextButPreservesAReplacementContext()
        {
            Add(StatId.DamageDealt, StatOp.Multiply, 2f, StatCondition.HealthBelowHalf);
            Set(_health, "_currentHealth", 25f);
            Assert.That(_sheet.Context, Is.SameAs(_health));
            Assert.That(PlayerStats.Get(StatId.DamageDealt, 1f), Is.EqualTo(2f));
            Invoke(_health, "UnbindStats");
            Assert.That(_sheet.Context, Is.Null);
            Assert.That(PlayerStats.Get(StatId.DamageDealt, 1f), Is.EqualTo(1f));
            Invoke(_health, "BindStats");
            var replacement = new LowHealthContext();
            _sheet.SetContext(replacement);
            Invoke(_health, "UnbindStats");
            Assert.That(_sheet.Context, Is.SameAs(replacement));
            Assert.That(PlayerStats.Get(StatId.DamageDealt, 1f), Is.EqualTo(2f));
        }

        [Test]
        public void DisabledHealthUnsubscribesFromSheetChanges()
        {
            int notifications = 0;
            _health.OnHealthChanged += (current, maximum) => notifications++;
            Invoke(_health, "OnDisable");
            Add(StatId.MaxHealth, StatOp.Multiply, 0.5f);
            Assert.That(notifications, Is.Zero);
            Assert.That(_health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ZeroHealingModifierDoesNotMutateHealthOrNotify()
        {
            Set(_health, "_currentHealth", 50f);
            Add(StatId.HealReceived, StatOp.Multiply, 0f);
            int notifications = 0;
            _health.OnHealed += amount => notifications++;
            Assert.That(_health.Heal(20f), Is.False);
            Assert.That(_health.CurrentHealth, Is.EqualTo(50f));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void MovementAndDashHudReadEffectiveValuesWithoutMutatingBases()
        {
            var movement = _health.GetComponent<PlayerMovement>();
            Add(StatId.MoveSpeed, StatOp.Multiply, 2f);
            Add(StatId.DashRechargeTime, StatOp.Multiply, 0.5f);
            Set(movement, "_currentDashCharges", 1);
            Set(movement, "_dashRechargeTimer", 0.3f);
            Assert.That(movement.WalkSpeed, Is.EqualTo(16f));
            Assert.That(movement.DashRechargeNormalized, Is.EqualTo(0.5f).Within(0.00001f));
            Set(movement, "_isLunging", true);
            Set(movement, "_lungeDirection", Vector3.forward);
            movement.StopLunge();
            Assert.That(movement.Velocity.z, Is.EqualTo(16f * 1.35f));
            Assert.That(Get(movement, "walkSpeed"), Is.EqualTo(8f));
            Assert.That(Get(movement, "dashRechargeTime"), Is.EqualTo(1.2f));
        }

        [TestCase(0f, 4, 1)]
        [TestCase(0.6f, 4, 2)]
        [TestCase(2f, 4, 8)]
        [TestCase(2f, 0, 0)]
        public void PickupCollectionRoundsAndFloorsPositiveBaseAmounts(float multiplier, int amount, int expected)
        {
            Add(StatId.AmmoPickupAmount, StatOp.Multiply, multiplier);
            var inventory = Create<PlayerAmmoInventory>();
            Set(inventory, "_currentShells", 0);
            var pickup = Create<AmmoPickup>();
            Set(pickup, "_playerInventory", inventory);
            Set(pickup, "ammoAmount", amount);
            Invoke(pickup, "Collect");
            Assert.That(inventory.CurrentShells, Is.EqualTo(expected));
        }

        [Test]
        public void ZeroOrbMultiplierSuppressesEvenMaximumPityAndMeleeBonuses()
        {
            Add(StatId.OrbDropChance, StatOp.Multiply, 0f);
            var manager = Create<HealthOrbManager>();
            Set(manager, "_playerHealth", _health);
            Set(manager, "_consecutiveNonDrops", 100);
            manager.TryDropOrbs(Vector3.zero, 3, true, true);
            Assert.That(Get(manager, "_consecutiveNonDrops"), Is.EqualTo(101));
            Assert.That(((ICollection)Get(manager, "_allOrbs")).Count, Is.Zero);
            Assert.That(manager.transform.childCount, Is.Zero);
        }

        [TestCase(2f)]
        [TestCase(0f)]
        public void RangedDryFireCooldownsUseFireRateWithItsMinimum(float rate)
        {
            Add(StatId.FireRate, StatOp.Multiply, rate);
            var inventory = Create<PlayerAmmoInventory>();
            Set(inventory, "_currentShells", 0);
            Set(inventory, "_currentBolts", 0);
            var camera = Create<Camera>();
            var arbalest = Create<RepeaterArbalestWeapon>();
            var shotgun = Create<GothicShotgunWeapon>();
            Set(arbalest, "_cam", camera);
            Set(arbalest, "_ammoInventory", inventory);
            Set(shotgun, "_cam", camera);
            Set(shotgun, "_ammoInventory", inventory);
            arbalest.FirePrimary();
            arbalest.FireAlt();
            shotgun.FirePrimary();
            float divisor = Mathf.Max(0.05f, rate);
            Assert.That(Get(arbalest, "_primaryCooldownTimer"), Is.EqualTo(0.22f / divisor));
            Assert.That(Get(arbalest, "_altCooldownTimer"), Is.EqualTo(0.30f / divisor));
            Assert.That(Get(shotgun, "_primaryCooldownTimer"), Is.EqualTo(0.25f / divisor));
        }

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name + " stat test");
            go.SetActive(false);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private void Add(StatId stat, StatOp op, float value, StatCondition condition = StatCondition.Always)
        {
            Assert.That(_sheet.Add(_sheet.Count + 1, new StatModifier(stat, op, value, condition)), Is.True);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
        private static object GetStatic(System.Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static void SetStatic(System.Type type, string name, object value) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);

        private sealed class LowHealthContext : IStatContext
        {
            public float HealthFraction => 0.25f;
            public bool IsOverhealed => false;
        }
    }
}
