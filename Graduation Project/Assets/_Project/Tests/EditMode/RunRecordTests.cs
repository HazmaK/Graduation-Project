using System;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Run statistics, immutable summaries, and floor-clear lifecycle contracts.</summary>
    public sealed class RunRecordTests : CardTestFixture
    {
        /// <summary>Leaves no active run or open summary after each test.</summary>
        [TearDown]
        public void ClearCurrentRun() => RunEvents.SetCurrentRun(null);

        /// <summary>Kills split by location while critical and gib flags count independently.</summary>
        [Test]
        public void RecordKillSplitsLocationsAndCountsFlags()
        {
            var record = new RunRecord();
            record.RecordKill(false, true, false);
            record.RecordKill(false, false, true);
            record.RecordKill(true, true, true);
            record.RecordKill(true, false, false);
            Assert.That(record.TowerKills, Is.EqualTo(2));
            Assert.That(record.ArafKills, Is.EqualTo(2));
            Assert.That(record.TotalKills, Is.EqualTo(4));
            Assert.That(record.CriticalKills, Is.EqualTo(2));
            Assert.That(record.GibKills, Is.EqualTo(2));
        }

        /// <summary>Invalid damage values cannot contaminate the accumulated damage.</summary>
        [Test]
        public void RecordDamageIgnoresNonPositiveAndNonFiniteValues()
        {
            var record = new RunRecord();
            record.RecordDamage(12.5f);
            record.RecordDamage(0f);
            record.RecordDamage(-1f);
            record.RecordDamage(float.NaN);
            record.RecordDamage(float.PositiveInfinity);
            record.RecordDamage(float.NegativeInfinity);
            record.RecordDamage(2.5f);
            Assert.That(record.DamageTaken, Is.EqualTo(15f));
        }

        /// <summary>Only falling, in-Araf, and escaping phases accumulate Araf duration.</summary>
        [TestCase(RunPhase.Tower, 0f)]
        [TestCase(RunPhase.FallingToAraf, 2f)]
        [TestCase(RunPhase.InAraf, 2f)]
        [TestCase(RunPhase.EscapingAraf, 2f)]
        [TestCase(RunPhase.TrueDeath, 0f)]
        public void AdvanceTimeUsesOnlyValidDeltasInArafPhases(RunPhase phase, float expected)
        {
            var record = new RunRecord();
            record.AdvanceTime(2f, phase);
            record.AdvanceTime(0f, phase);
            record.AdvanceTime(-1f, phase);
            record.AdvanceTime(float.NaN, phase);
            record.AdvanceTime(float.PositiveInfinity, phase);
            record.AdvanceTime(float.NegativeInfinity, phase);
            Assert.That(record.ArafSeconds, Is.EqualTo(expected));
        }

        /// <summary>The run forwards accepted deltas and freezes both clocks at true death.</summary>
        [Test]
        public void AdvanceElapsedTimeForwardsArafTimeAndStopsAtTrueDeath()
        {
            var run = new RunState(123);
            run.AdvanceElapsedTime(10f);
            run.SetPhase(RunPhase.FallingToAraf);
            run.AdvanceElapsedTime(1f);
            run.SetPhase(RunPhase.InAraf);
            run.AdvanceElapsedTime(2f);
            run.SetPhase(RunPhase.EscapingAraf);
            run.AdvanceElapsedTime(3f);
            run.AdvanceElapsedTime(0f);
            run.AdvanceElapsedTime(-1f);
            run.AdvanceElapsedTime(float.NaN);
            run.AdvanceElapsedTime(float.PositiveInfinity);
            run.AdvanceElapsedTime(float.NegativeInfinity);
            run.SetPhase(RunPhase.TrueDeath);
            run.AdvanceElapsedTime(100f);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(16f));
            Assert.That(run.Record.ArafSeconds, Is.EqualTo(6f));
        }

        /// <summary>Each run owns independent statistics.</summary>
        [Test]
        public void NewRunsDoNotShareRecords()
        {
            var first = new RunState(1);
            var second = new RunState(1);
            first.Record.RecordKill(false, true, true);
            first.Record.RecordDamage(5f);
            Assert.That(second.Record, Is.Not.SameAs(first.Record));
            Assert.That(second.Record.TotalKills, Is.Zero);
            Assert.That(second.Record.DamageTaken, Is.Zero);
        }

        /// <summary>Summary values and card membership remain fixed as the run progresses.</summary>
        [Test]
        public void CreateSummaryCapturesAllValuesAndCardSnapshot()
        {
            var run = new RunState(123);
            var first = CreateCard("first");
            var second = CreateCard("second");
            Assert.That(run.Cards.TryAcquire(first), Is.True);
            run.AdvanceFloor();
            run.AdvanceElapsedTime(10f);
            run.Record.RecordKill(false, true, false);
            run.ConsumeArafCharge();
            run.SetPhase(RunPhase.InAraf);
            run.AdvanceElapsedTime(4f);
            run.Record.RecordKill(true, false, true);
            run.Record.RecordDamage(12.5f);
            RunSummary summary = run.CreateSummary();
            Assert.That(run.Cards.TryAcquire(second), Is.True);
            run.Record.RecordKill(true, true, true);
            run.Record.RecordDamage(1f);
            run.AdvanceElapsedTime(1f);
            run.AdvanceFloor();
            Assert.That(summary.FloorNumber, Is.EqualTo(2));
            Assert.That(summary.TotalSeconds, Is.EqualTo(14f));
            Assert.That(summary.ArafSeconds, Is.EqualTo(4f));
            Assert.That(summary.TotalKills, Is.EqualTo(2));
            Assert.That(summary.TowerKills, Is.EqualTo(1));
            Assert.That(summary.ArafKills, Is.EqualTo(1));
            Assert.That(summary.CriticalKills, Is.EqualTo(1));
            Assert.That(summary.GibKills, Is.EqualTo(1));
            Assert.That(summary.DamageTaken, Is.EqualTo(12.5f));
            Assert.That(summary.ArafChargeUsed, Is.True);
            Assert.That(summary.CardCount, Is.EqualTo(1));
            Assert.That(summary.GetCard(0), Is.SameAs(first));
            Assert.Throws<ArgumentOutOfRangeException>(() => summary.GetCard(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => summary.GetCard(1));
        }

        /// <summary>The public preview constructor copies cards instead of retaining the caller's array.</summary>
        [Test]
        public void PublicSummaryConstructorCopiesCardsAndSupportsEmptyDefault()
        {
            var first = CreateCard("first");
            var cards = new[] { first };
            var summary = new RunSummary(1, 10f, 2f, 3, 2, 1, 1, 1, 5f, true, cards);
            cards[0] = null;
            Assert.That(summary.GetCard(0), Is.SameAs(first));
            RunSummary empty = default;
            Assert.That(empty.CardCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.GetCard(0));
        }

        /// <summary>A clear opens once, callbacks see committed state, and continue advances once.</summary>
        [Test]
        public void FloorClearAndContinueAreOnceOnlyWhileOpen()
        {
            var run = new RunState(123);
            int cleared = 0;
            int continued = 0;
            RunEvents.SetCurrentRun(run);
            RunEvents.OnFloorCleared += summary =>
            {
                cleared++;
                Assert.That(RunEvents.IsFloorSummaryOpen, Is.True);
                Assert.That(summary.FloorNumber, Is.EqualTo(run.FloorIndex + 1));
                Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.EqualTo(summary.FloorNumber));
                RunEvents.RaiseFloorCleared();
            };
            RunEvents.OnFloorContinued += () =>
            {
                continued++;
                Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
                Assert.That(run.FloorIndex, Is.EqualTo(continued));
                RunEvents.ContinueAfterFloorClear();
            };
            RunEvents.ContinueAfterFloorClear();
            RunEvents.RaiseFloorCleared();
            RunEvents.RaiseFloorCleared();
            Assert.That(cleared, Is.EqualTo(1));
            Assert.That(run.FloorIndex, Is.Zero);
            RunEvents.ContinueAfterFloorClear();
            RunEvents.ContinueAfterFloorClear();
            Assert.That(continued, Is.EqualTo(1));
            Assert.That(run.FloorIndex, Is.EqualTo(1));
            RunEvents.RaiseFloorCleared();
            Assert.That(cleared, Is.EqualTo(2));
            Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.EqualTo(2));
        }

        /// <summary>Only Tower clears can open a summary or emit a floor-cleared event.</summary>
        [TestCase(RunPhase.FallingToAraf)]
        [TestCase(RunPhase.InAraf)]
        [TestCase(RunPhase.EscapingAraf)]
        [TestCase(RunPhase.TrueDeath)]
        public void FloorClearOutsideTowerIsNoOp(RunPhase phase)
        {
            var run = new RunState(123);
            run.SetPhase(phase);
            RunEvents.SetCurrentRun(run);
            int calls = 0;
            RunEvents.OnFloorCleared += summary => calls++;
            RunEvents.RaiseFloorCleared();
            RunEvents.ContinueAfterFloorClear();
            Assert.That(calls, Is.Zero);
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
            Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.Zero);
            Assert.That(run.FloorIndex, Is.Zero);
        }

        /// <summary>Closing is idempotent, retains the summary, and never continues or advances the floor.</summary>
        [Test]
        public void CloseFloorSummaryClearsWithoutAdvancing()
        {
            var run = new RunState(123);
            RunEvents.SetCurrentRun(run);
            RunEvents.RaiseFloorCleared();
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.True);
            int continued = 0;
            RunEvents.OnFloorContinued += () => continued++;
            RunEvents.CloseFloorSummary();
            RunEvents.CloseFloorSummary();
            RunEvents.ContinueAfterFloorClear();
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
            Assert.That(run.FloorIndex, Is.Zero);
            Assert.That(continued, Is.Zero);
            Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.EqualTo(1));
        }

        /// <summary>Death listeners see a closed summary and cannot advance the cleared floor.</summary>
        [Test]
        public void TrueDeathClearsFloorSummaryBeforeNotification()
        {
            var run = new RunState(123);
            RunEvents.SetCurrentRun(run);
            RunEvents.RaiseFloorCleared();
            bool notified = false;
            RunEvents.OnTrueDeath += () =>
            {
                notified = true;
                Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
                RunEvents.ContinueAfterFloorClear();
            };
            RunEvents.RaiseTrueDeath();
            Assert.That(notified, Is.True);
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
            Assert.That(run.FloorIndex, Is.Zero);
        }

        /// <summary>Clearing without a run has no effect.</summary>
        [Test]
        public void FloorClearWithoutCurrentRunIsNoOp()
        {
            int calls = 0;
            RunEvents.SetCurrentRun(null);
            RunEvents.OnFloorCleared += summary => calls++;
            RunEvents.RaiseFloorCleared();
            RunEvents.ContinueAfterFloorClear();
            Assert.That(calls, Is.Zero);
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
            Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.Zero);
        }

        /// <summary>Replacing or clearing a run discards the previous summary and its open state.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void SetCurrentRunClearsFloorSummary(bool clearRun)
        {
            RunEvents.SetCurrentRun(new RunState(1));
            RunEvents.RaiseFloorCleared();
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.True);
            var replacement = clearRun ? null : new RunState(2);
            RunEvents.SetCurrentRun(replacement);
            Assert.That(RunEvents.IsFloorSummaryOpen, Is.False);
            Assert.That(RunEvents.LastFloorSummary.FloorNumber, Is.Zero);
            Assert.That(RunEvents.LastFloorSummary.CardCount, Is.Zero);
            RunEvents.ContinueAfterFloorClear();
            if (replacement != null) Assert.That(replacement.FloorIndex, Is.Zero);
        }
    }
}
