using System.Reflection;
using NUnit.Framework;
using RetroFPS.Stats;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Formula, condition, storage and global stat access contracts.</summary>
    public sealed class StatSheetTests : CardTestFixture
    {
        private sealed class Context : IStatContext
        {
            public float HealthFraction { get; private set; }
            public bool IsOverhealed { get; private set; }

            public void Set(float fraction, bool overhealed)
            {
                HealthFraction = fraction;
                IsOverhealed = overhealed;
            }
        }

        /// <summary>Storage order cannot change flat, percent, then multiplier evaluation.</summary>
        [Test]
        public void FormulaUsesFlatThenPercentThenMultipliers()
        {
            var sheet = new StatSheet();
            sheet.Add(1, new StatModifier(StatId.DamageDealt, StatOp.Multiply, 2f));
            sheet.Add(2, new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.5f));
            sheet.Add(3, new StatModifier(StatId.DamageDealt, StatOp.AddFlat, 10f));
            sheet.Add(4, new StatModifier(StatId.DamageDealt, StatOp.Multiply, 0.5f));
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 100f), Is.EqualTo(165f).Within(0.0001f));
            Assert.That(sheet.Evaluate(StatId.MoveSpeed, 100f), Is.EqualTo(100f));
        }

        /// <summary>Percent modifiers from independent sources add rather than multiply.</summary>
        [Test]
        public void PercentBonusesAndPenaltiesStackAdditively()
        {
            var sheet = new StatSheet();
            sheet.Add(1, new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.35f));
            sheet.Add(2, new StatModifier(StatId.FireRate, StatOp.AddPercent, -0.30f));
            Assert.That(sheet.Evaluate(StatId.FireRate, 100f), Is.EqualTo(105f).Within(0.0001f));
        }

        /// <summary>Negative bases, percentage factors and final products clamp at zero.</summary>
        [Test]
        public void NegativeResultsAndPercentageFactorClampToZero()
        {
            var sheet = new StatSheet();
            Assert.That(sheet.Evaluate(StatId.MaxHealth, -10f), Is.Zero);
            sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, -20f));
            Assert.That(sheet.Evaluate(StatId.MaxHealth, 10f), Is.Zero);
            sheet.Clear();
            sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -2f));
            sheet.Add(2, new StatModifier(StatId.MaxHealth, StatOp.Multiply, -3f));
            Assert.That(sheet.Evaluate(StatId.MaxHealth, 10f), Is.Zero);
            sheet.RemoveSource(1);
            Assert.That(sheet.Evaluate(StatId.MaxHealth, 10f), Is.Zero);
        }

        /// <summary>Conditions follow live context changes, including the strict half-health boundary.</summary>
        [Test]
        public void ConditionsAreEvaluatedLiveAndDisabledWithoutContext()
        {
            var sheet = new StatSheet();
            sheet.Add(1, new StatModifier(StatId.DamageDealt, StatOp.AddFlat, 1f));
            sheet.Add(2, new StatModifier(StatId.DamageDealt, StatOp.AddFlat, 2f, StatCondition.HealthBelowHalf));
            sheet.Add(3, new StatModifier(StatId.DamageDealt, StatOp.AddFlat, 4f, StatCondition.Overhealed));
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(11f));
            var context = new Context();
            context.Set(0.49f, false);
            sheet.SetContext(context);
            int version = sheet.Version;
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(13f));
            context.Set(0.5f, false);
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(11f));
            context.Set(1.2f, true);
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(15f));
            Assert.That(sheet.Version, Is.EqualTo(version));
            sheet.SetContext(null);
            Assert.That(sheet.Evaluate(StatId.DamageDealt, 10f), Is.EqualTo(11f));
        }

        /// <summary>Changes notify once after mutation; no-op removals and clears do not advance the revision.</summary>
        [Test]
        public void MutationsUpdateCountVersionAndNotifications()
        {
            var sheet = new StatSheet();
            int calls = 0;
            sheet.OnChanged += () => { calls++; Assert.That(sheet.Version, Is.EqualTo(calls)); };
            Assert.That(sheet.Count, Is.Zero);
            Assert.That(sheet.Version, Is.Zero);
            var context = new Context();
            sheet.SetContext(context);
            sheet.SetContext(context);
            sheet.Add(1, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 1f));
            sheet.Add(2, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 2f));
            sheet.Add(1, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 4f));
            Assert.That(sheet.RemoveSource(1), Is.EqualTo(2));
            Assert.That(sheet.Count, Is.EqualTo(1));
            Assert.That(sheet.Evaluate(StatId.MoveSpeed, 10f), Is.EqualTo(12f));
            Assert.That(sheet.RemoveSource(99), Is.Zero);
            sheet.Clear();
            sheet.Clear();
            sheet.SetContext(null);
            Assert.That(sheet.Count, Is.Zero);
            Assert.That(sheet.Evaluate(StatId.MoveSpeed, 10f), Is.EqualTo(10f));
            Assert.That(sheet.Version, Is.EqualTo(7));
            Assert.That(calls, Is.EqualTo(7));
        }

        /// <summary>The fixed buffer rejects overflow and reuses removed storage.</summary>
        [Test]
        public void CapacityRejectsOverflowAndRecoversAfterRemovalAndClear()
        {
            var sheet = new StatSheet();
            var modifier = new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 1f);
            for (int i = 0; i < StatSheet.MaxModifiers; i++) Assert.That(sheet.Add(i, in modifier), Is.True);
            Assert.That(sheet.Add(100, in modifier), Is.False);
            Assert.That(sheet.Count, Is.EqualTo(64));
            Assert.That(sheet.Version, Is.EqualTo(64));
            Assert.That(sheet.Evaluate(StatId.MoveSpeed, 0f), Is.EqualTo(64f));
            sheet.RemoveSource(10);
            Assert.That(sheet.Add(100, in modifier), Is.True);
            sheet.Clear();
            Assert.That(sheet.Add(100, in modifier), Is.True);
            Assert.That(sheet.Count, Is.EqualTo(1));
        }

        /// <summary>NaN and both infinities are rejected without changing state or sending events.</summary>
        [Test]
        public void NonFiniteModifiersAreRejected()
        {
            var sheet = new StatSheet();
            int calls = 0;
            sheet.OnChanged += () => calls++;
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.That(sheet.Add(1, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, value)), Is.False);
            Assert.That(sheet.Count, Is.Zero);
            Assert.That(sheet.Version, Is.Zero);
            Assert.That(calls, Is.Zero);
        }

        /// <summary>No active run preserves even negative bases; an active run evaluates its sheet.</summary>
        [Test]
        public void PlayerStatsPassesThroughWithoutSheetAndUsesAssignedSheet()
        {
            Assert.That(PlayerStats.Current, Is.Null);
            Assert.That(PlayerStats.Get(StatId.MaxHealth, -5f), Is.EqualTo(-5f));
            var sheet = new StatSheet();
            sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 25f));
            PlayerStats.SetCurrent(sheet);
            Assert.That(PlayerStats.Current, Is.SameAs(sheet));
            Assert.That(PlayerStats.Get(StatId.MaxHealth, 100f), Is.EqualTo(125f));
            PlayerStats.SetCurrent(null);
            Assert.That(PlayerStats.Get(StatId.MaxHealth, 100f), Is.EqualTo(100f));
        }

        /// <summary>Conditional MaxHealth modifiers are rejected to prevent circular evaluation.</summary>
        [Test]
        public void ConditionalMaxHealthIsRejected()
        {
            var sheet = new StatSheet();
            int calls = 0;
            sheet.OnChanged += () => calls++;
            Assert.That(sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 25f, StatCondition.HealthBelowHalf)), Is.False);
            Assert.That(sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.Multiply, 1.5f, StatCondition.Overhealed)), Is.False);
            Assert.That(sheet.Count, Is.Zero);
            Assert.That(sheet.Version, Is.Zero);
            Assert.That(calls, Is.Zero);
            Assert.That(sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 25f, StatCondition.Always)), Is.True);
            Assert.That(sheet.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(125f));
        }

        /// <summary>Batches defer OnChanged until outermost EndBatch and stay silent when nothing mutated.</summary>
        [Test]
        public void BatchNotifiesOnceWhenDirtyAndSupportsNesting()
        {
            var sheet = new StatSheet();
            var context = new Context();
            int calls = 0;
            int versionAtNotify = -1;
            sheet.OnChanged += () => { calls++; versionAtNotify = sheet.Version; };

            sheet.BeginBatch();
            sheet.Add(1, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 2f));
            sheet.BeginBatch();
            sheet.Add(1, new StatModifier(StatId.MoveSpeed, StatOp.AddFlat, 3f));
            sheet.SetContext(context);
            sheet.EndBatch();
            Assert.That(calls, Is.Zero);
            Assert.That(sheet.Version, Is.EqualTo(3));
            Assert.That(sheet.Context, Is.SameAs(context));
            sheet.EndBatch();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(versionAtNotify, Is.EqualTo(3));

            sheet.BeginBatch();
            sheet.RemoveSource(999);
            sheet.Add(1, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 10f, StatCondition.HealthBelowHalf));
            sheet.SetContext(context);
            sheet.EndBatch();
            sheet.EndBatch();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(sheet.Version, Is.EqualTo(3));
        }

        /// <summary>SubsystemRegistration reset clears any previously assigned active sheet.</summary>
        [Test]
        public void PlayerStatsResetStaticsClearsCurrentSheet()
        {
            PlayerStats.SetCurrent(new StatSheet());
            MethodInfo reset = typeof(PlayerStats).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            var attr = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
            reset.Invoke(null, null);
            Assert.That(PlayerStats.Current, Is.Null);
        }
    }
}
