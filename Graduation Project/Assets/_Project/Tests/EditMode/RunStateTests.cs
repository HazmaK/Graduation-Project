using NUnit.Framework;
using RetroFPS.Run;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Koşu durumunun akış ve evre sözleşme testleri.</summary>
    public sealed class RunStateTests
    {
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
