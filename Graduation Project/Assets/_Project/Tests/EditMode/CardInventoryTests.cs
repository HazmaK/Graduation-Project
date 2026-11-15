using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Temporary card assets and isolated run statics for card contract tests.</summary>
    public abstract class CardTestFixture
    {
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        /// <summary>Starts each test without an active run or stat sheet.</summary>
        [SetUp]
        public void SetUpCardState() => ResetState();

        /// <summary>Destroys temporary assets and clears all run events after every test.</summary>
        [TearDown]
        public void TearDownCardState()
        {
            ResetState();
            for (int i = 0; i < _assets.Count; i++) Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        /// <summary>Creates a configured temporary card.</summary>
        protected CurseBlessingDefinition CreateCard(string id, params StatModifier[] modifiers)
        {
            var card = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            _assets.Add(card);
            card.Configure(id, id + ".name", id + ".boon", id + ".curse", id + ".flavor", Color.red, modifiers);
            return card;
        }

        /// <summary>Creates a configured temporary catalog.</summary>
        protected CardCatalog CreateCatalog(params CurseBlessingDefinition[] cards)
        {
            var catalog = ScriptableObject.CreateInstance<CardCatalog>();
            _assets.Add(catalog);
            catalog.Configure(cards);
            return catalog;
        }

        private static void ResetState()
        {
            PlayerStats.SetCurrent(null);
            typeof(RunEvents).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
    }

    /// <summary>Card ownership, capacity, presentation and modifier application contracts.</summary>
    public sealed class CardInventoryTests : CardTestFixture
    {
        /// <summary>Acquisition applies all modifiers before notifying and uses slot plus one as the source.</summary>
        [Test]
        public void AcquisitionAppliesModifiersAndRaisesOneEvent()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            var first = CreateCard("first", new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.5f),
                new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, -2f));
            var second = CreateCard("second", new StatModifier(StatId.DamageDealt, StatOp.AddFlat, 10f));
            int calls = 0;
            inventory.OnCardAcquired += card =>
            {
                calls++;
                Assert.That(inventory.Has(card), Is.True);
                Assert.That(sheet.Count, Is.EqualTo(calls == 1 ? 2 : 3));
            };
            Assert.That(inventory.TryAcquire(first), Is.True);
            Assert.That(inventory.TryAcquire(second), Is.True);
            Assert.That(inventory.Count, Is.EqualTo(2));
            Assert.That(inventory.Get(0), Is.SameAs(first));
            Assert.That(inventory.Get(1), Is.SameAs(second));
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 100f), Is.EqualTo(165f));
            Assert.That(sheet.Evaluate(StatId.MoveSpeed, 10f), Is.EqualTo(8f));
            Assert.That(sheet.RemoveSource(1), Is.EqualTo(2));
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 100f), Is.EqualTo(110f));
            Assert.That(sheet.RemoveSource(2), Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(2));
        }

        /// <summary>Nulls and a second definition with the same identity cannot be acquired.</summary>
        [Test]
        public void NullAndDuplicateIdentitiesAreRejected()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            var card = CreateCard("same");
            var duplicate = CreateCard("same");
            int calls = 0;
            inventory.OnCardAcquired += acquired => calls++;
            Assert.That(inventory.TryAcquire(null), Is.False);
            Assert.That(inventory.Has(null), Is.False);
            Assert.That(inventory.TryAcquire(card), Is.True);
            Assert.That(inventory.TryAcquire(card), Is.False);
            Assert.That(inventory.TryAcquire(duplicate), Is.False);
            Assert.That(inventory.Has(duplicate), Is.True);
            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>All sixteen slots are usable and the seventeenth card has no effect.</summary>
        [Test]
        public void FullInventoryRejectsAnotherCard()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            for (int i = 0; i < CardInventory.MaxCards; i++)
                Assert.That(inventory.TryAcquire(CreateCard("card" + i)), Is.True);
            var overflow = CreateCard("overflow", new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 10f));
            Assert.That(inventory.TryAcquire(overflow), Is.False);
            Assert.That(inventory.Count, Is.EqualTo(16));
            Assert.That(inventory.Has(overflow), Is.False);
            Assert.That(sheet.Count, Is.Zero);
        }

        /// <summary>Insufficient sheet capacity cannot leave a partially applied card.</summary>
        [Test]
        public void InsufficientModifierCapacityRejectsWholeCard()
        {
            var sheet = new StatSheet();
            for (int i = 0; i < StatSheet.MaxModifiers - 1; i++)
                sheet.Add(100, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 1f));
            var inventory = new CardInventory(sheet);
            var card = CreateCard("large", new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 2f),
                new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 3f));
            Assert.That(inventory.TryAcquire(card), Is.False);
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(sheet.Count, Is.EqualTo(63));
            Assert.That(sheet.Version, Is.EqualTo(63));
        }

        /// <summary>A bad modifier rejects the whole card before a valid prefix reaches the sheet.</summary>
        [Test]
        public void NonFiniteCardModifierRejectsWholeCard()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            var card = CreateCard("invalid", new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 2f),
                new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, float.NaN));
            Assert.That(inventory.TryAcquire(card), Is.False);
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(sheet.Count, Is.Zero);
        }

        /// <summary>A conditional MaxHealth modifier rejects the whole card before any prefix reaches the sheet.</summary>
        [Test]
        public void ConditionalMaxHealthCardModifierRejectsWholeCard()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            var card = CreateCard("invalidMax",
                new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 2f),
                new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 20f, StatCondition.HealthBelowHalf));
            Assert.That(inventory.TryAcquire(card), Is.False);
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(sheet.Count, Is.Zero);
            Assert.That(sheet.Version, Is.Zero);
        }

        /// <summary>Acquiring a card batches its sheet mutations into a single OnChanged notification.</summary>
        [Test]
        public void AcquisitionBatchesSheetNotifications()
        {
            var sheet = new StatSheet();
            var inventory = new CardInventory(sheet);
            int sheetCalls = 0;
            sheet.OnChanged += () => { sheetCalls++; Assert.That(sheet.Count, Is.EqualTo(2)); };
            var empty = CreateCard("empty");
            var pair = CreateCard("pair",
                new StatModifier(StatId.MaxHealth, StatOp.AddFlat, -50f),
                new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 60f));
            Assert.That(inventory.TryAcquire(empty), Is.True);
            Assert.That(sheetCalls, Is.Zero);
            Assert.That(inventory.TryAcquire(pair), Is.True);
            Assert.That(sheetCalls, Is.EqualTo(1));
            Assert.That(sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(110f));
        }

        /// <summary>Configured presentation and null collections are exposed safely.</summary>
        [Test]
        public void DefinitionAndCatalogExposeConfiguredData()
        {
            var card = CreateCard("test", new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 10f));
            Assert.That(card.Id, Is.EqualTo("test"));
            Assert.That(card.NameKey, Is.EqualTo("test.name"));
            Assert.That(card.BoonKey, Is.EqualTo("test.boon"));
            Assert.That(card.CurseKey, Is.EqualTo("test.curse"));
            Assert.That(card.FlavorKey, Is.EqualTo("test.flavor"));
            Assert.That(card.AccentColor, Is.EqualTo(Color.red));
            Assert.That(card.ModifierCount, Is.EqualTo(1));
            Assert.That(card.GetModifier(0).Value, Is.EqualTo(10f));
            var catalog = CreateCatalog(card, null);
            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.Get(0), Is.SameAs(card));
            Assert.That(catalog.Get(1), Is.Null);
            catalog.Configure(null);
            Assert.That(catalog.Count, Is.Zero);
            card.Configure("test", null, null, null, null, Color.white, null);
            Assert.That(card.ModifierCount, Is.Zero);
        }
    }
}
