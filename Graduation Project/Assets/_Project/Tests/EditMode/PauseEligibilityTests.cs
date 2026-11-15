using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.Run;

namespace RetroFPS.Tests
{
    /// <summary>Actual runtime mapping and live run/load/death adapter coverage.</summary>
    public sealed class PauseEligibilityTests
    {
        /// <summary>Only Tower and InAraf permit a pause when a run exists.</summary>
        [TestCase(RunPhase.Tower, true)]
        [TestCase(RunPhase.InAraf, true)]
        [TestCase(RunPhase.FallingToAraf, false)]
        [TestCase(RunPhase.EscapingAraf, false)]
        [TestCase(RunPhase.TrueDeath, false)]
        public void AdapterMapsExactRunPhases(RunPhase phase, bool expected)
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, false, false, true, phase),
                Is.EqualTo(expected));
        }

        /// <summary>MainMenu, dead player, death card, preload/load and transitions independently deny entry.</summary>
        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, false, false, true)]
        public void AdapterDeniesEveryExclusiveOwner(bool gameplay, bool alive, bool card, bool load, bool transition)
        {
            Assert.That(PauseRuntime.EvaluateEligibility(gameplay, alive, card, load, transition,
                true, RunPhase.Tower), Is.False);
        }

        /// <summary>A live standalone test remains eligible, while MainMenu always denies even without a run.</summary>
        [Test]
        public void NoRunDoesNotRequireRunDirectorAndNeverAdmitsMainMenu()
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, false, false, false,
                RunPhase.Tower), Is.True);
            Assert.That(PauseRuntime.EvaluateEligibility(false, true, false, false, false, false,
                RunPhase.Tower), Is.False);
        }

        /// <summary>Binding snapshots CurrentRun, denied presses are discarded, and held toggles cannot queue.</summary>
        [Test]
        public void RunSnapshotAndTransitionRequireFreshToggleRelease()
        {
            using (var world = new PauseTestWorld())
            {
                var run = new RunState(11);
                run.SetPhase(RunPhase.FallingToAraf);
                RunEvents.SetCurrentRun(run);
                world.Sample(1, escape: true, toggleHeld: true);
                Assert.That(world.Runtime.CanPause, Is.False);
                run.SetPhase(RunPhase.InAraf);
                RunEvents.RaisePhaseChanged(RunPhase.InAraf);
                world.Sample(2, escape: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.Zero);
                world.Sample(3);
                world.Sample(4, start: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.EqualTo(1));
                run.SetPhase(RunPhase.EscapingAraf);
                RunEvents.RaisePhaseChanged(RunPhase.EscapingAraf);
                Assert.That(GamePause.IsPaused, Is.False);
                Assert.That(world.Menu.Cancels, Is.EqualTo(1));
            }
        }

        /// <summary>Card/load flags cancel synchronously and never relock the exclusive owner's cursor.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void CardAndExternalLoadWinSameFrameOwnership(bool deathCard)
        {
            using (var world = new PauseTestWorld())
            {
                world.Sample(1, escape: true, toggleHeld: true);
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
                if (deathCard) world.Runtime.SetTrueDeathOpen(true);
                else world.Runtime.SetSceneLoadInProgress(true);
                Assert.That(GamePause.IsPaused, Is.False);
                Assert.That(world.Menu.Cancels, Is.EqualTo(1));
                Assert.That(UnityEngine.Cursor.lockState, Is.EqualTo(UnityEngine.CursorLockMode.None));
                world.Sample(2);
                world.Sample(3, start: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.EqualTo(1));
            }
        }

        /// <summary>The real director's active additive preload denies opening independently of phase.</summary>
        [Test]
        public void ActualArafPreloadFlagBlocksAdapter()
        {
            using (var world = new PauseTestWorld())
            {
                var director = world.Root.AddComponent<RunDirector>();
                PauseTestWorld.Set(director, "_preloadingAraf", true);
                PauseTestWorld.Set(world.Runtime, "runDirector", director);
                world.Sample(1, escape: true, toggleHeld: true);
                Assert.That(world.Runtime.IsSceneLoadInProgress, Is.True);
                Assert.That(world.Menu.Opens, Is.Zero);
                PauseTestWorld.Set(director, "_preloadingAraf", false);
                world.Sample(2);
                world.Sample(3, escape: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.EqualTo(1));
            }
        }

        /// <summary>True death remains latched until a new run; dead health separately denies standalone entry.</summary>
        [Test]
        public void TrueDeathAndDeadPlayerCannotOpenStandalonePause()
        {
            using (var world = new PauseTestWorld())
            {
                RunEvents.RaiseTrueDeath();
                world.Sample(1, start: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.Zero);
                RunEvents.SetCurrentRun(new RunState(22));
                PauseTestWorld.Set(world.Health, "_isDead", true);
                world.Sample(2);
                world.Sample(3, escape: true, toggleHeld: true);
                Assert.That(world.Menu.Opens, Is.Zero);
            }
        }
    }
}
