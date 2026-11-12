using NUnit.Framework;
using RetroFPS.Run;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Koşu durumunun akış ve evre sözleşme testleri.</summary>
    public sealed class RunStateTests
    {
        /// <summary>Araf charge sends exactly one false-to-true notification.</summary>
        [Test]
        public void ChargeChangedIsRaisedOnlyOnce()
        {
            var state = new RunState(42);
            int calls = 0;
            bool used = false;
            state.OnArafChargeChanged += value => { calls++; used = value; };
            state.ConsumeArafCharge();
            state.ConsumeArafCharge();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(used, Is.True);
        }

        /// <summary>Run replacement detaches its predecessor and publishes run and charge snapshots in order.</summary>
        [Test]
        public void SwitchingRunsDetachesOldChargeAndSnapshotsNewRun()
        {
            RunState previous = RunEvents.CurrentRun;
            var first = new RunState(1);
            var second = new RunState(2);
            second.ConsumeArafCharge();
            var events = new System.Collections.Generic.List<string>();
            System.Action<RunState> onRun = run => events.Add(run == null ? "null" : "run:" + run.Seed);
            System.Action<bool> onCharge = used => events.Add(used ? "used" : "ready");
            RunEvents.OnRunChanged += onRun;
            RunEvents.OnArafChargeChanged += onCharge;
            try
            {
                RunEvents.SetCurrentRun(first);
                RunEvents.SetCurrentRun(second);
                first.ConsumeArafCharge();
                second.ConsumeArafCharge();
                RunEvents.SetCurrentRun(null);
                Assert.That(events, Is.EqualTo(new[] { "run:1", "ready", "run:2", "used", "null", "ready" }));
            }
            finally
            {
                RunEvents.OnRunChanged -= onRun;
                RunEvents.OnArafChargeChanged -= onCharge;
                RunEvents.SetCurrentRun(previous);
            }
        }

        /// <summary>Reassigning a state never duplicates charge forwarding.</summary>
        [Test]
        public void SameRunReassignmentForwardsConsumptionOnce()
        {
            RunState previous = RunEvents.CurrentRun;
            var state = new RunState(1);
            int calls = 0;
            System.Action<bool> handler = used => { if (used) calls++; };
            RunEvents.OnArafChargeChanged += handler;
            try
            {
                RunEvents.SetCurrentRun(state);
                RunEvents.SetCurrentRun(state);
                state.ConsumeArafCharge();
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                RunEvents.OnArafChargeChanged -= handler;
                RunEvents.SetCurrentRun(previous);
            }
        }

        /// <summary>All travel phases count time; negative time and true death do not.</summary>
        [Test]
        public void ElapsedTimeIncludesArafAndFreezesAtTrueDeath()
        {
            var state = new RunState(42);
            RunPhase[] phases = { RunPhase.Tower, RunPhase.FallingToAraf, RunPhase.InAraf, RunPhase.EscapingAraf };
            foreach (RunPhase phase in phases)
            {
                state.SetPhase(phase);
                state.AdvanceElapsedTime(1.25f);
            }
            state.AdvanceElapsedTime(-100f);
            state.AdvanceElapsedTime(float.NaN);
            state.AdvanceElapsedTime(float.PositiveInfinity);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(5f));
            state.SetPhase(RunPhase.TrueDeath);
            state.AdvanceElapsedTime(100f);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(5f));
            state.ConsumeArafCharge();
            var fresh = new RunState(42);
            Assert.That(fresh.ElapsedSeconds, Is.Zero);
            Assert.That(fresh.ArafChargeUsed, Is.False);
        }

        /// <summary>Her akış aynı örneği döndürür ve diğer akışlardan etkilenmez.</summary>
        [Test]
        public void GetStreamCachesEachStreamAndUsesRunSeed()
        {
            var state = new RunState(-42);
            var repeat = new RunState(-42);
            for (int id = 1; id <= 4; id++)
            {
                RunStream stream = (RunStream)id;
                RunRandom random = state.GetStream(stream);
                Assert.That(state.GetStream(stream), Is.SameAs(random));
                RunRandom expected = RunRandom.Derive(state.Seed, stream);
                for (int i = 0; i < 100; i++)
                {
                    uint value = random.NextUInt();
                    Assert.That(value, Is.EqualTo(expected.NextUInt()));
                    Assert.That(value, Is.EqualTo(repeat.GetStream(stream).NextUInt()));
                }
            }
            Assert.That(state.GetStream(RunStream.Encounter),
                Is.Not.SameAs(state.GetStream(RunStream.Loot)));
        }

        /// <summary>Araf hakkı tüketildikten sonra tekrar kullanılamaz.</summary>
        [Test]
        public void ConsumeArafChargePreventsReentry()
        {
            var state = new RunState(42);
            Assert.That(state.CanEnterAraf, Is.True);
            Assert.That(state.ArafChargeUsed, Is.False);
            state.ConsumeArafCharge();
            Assert.That(state.ArafChargeUsed, Is.True);
            Assert.That(state.CanEnterAraf, Is.False);
            state.SetPhase(RunPhase.InAraf);
            state.SetPhase(RunPhase.Tower);
            state.ConsumeArafCharge();
            Assert.That(state.CanEnterAraf, Is.False);
        }

        /// <summary>Evre güncellemeleri ve Araf giriş koşulu korunur.</summary>
        [Test]
        public void PhaseTransitionsUpdateStateAndArafEligibility()
        {
            var state = new RunState(42);
            Assert.That(state.Seed, Is.EqualTo(42));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Tower));
            RunPhase[] phases =
            {
                RunPhase.FallingToAraf, RunPhase.InAraf,
                RunPhase.EscapingAraf, RunPhase.Tower, RunPhase.TrueDeath
            };
            foreach (RunPhase phase in phases)
            {
                state.SetPhase(phase);
                Assert.That(state.Phase, Is.EqualTo(phase));
                Assert.That(state.CanEnterAraf, Is.EqualTo(phase == RunPhase.Tower));
            }
            Assert.That(state.FloorIndex, Is.EqualTo(0));
            state.AdvanceFloor();
            Assert.That(state.FloorIndex, Is.EqualTo(1));
        }
    }
}
