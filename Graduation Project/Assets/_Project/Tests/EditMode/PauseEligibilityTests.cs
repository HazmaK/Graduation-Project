using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.Run;

namespace RetroFPS.Tests
{
    /// <summary>Actual runtime mapping and live run/load/death adapter coverage.</summary>
    public sealed class PauseEligibilityTests
    {
        /// <summary>Both sceneLoaded orders retain the enabled successor and its environment after a Single load.</summary>
        [TestCase(true, false, false)]
        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, true)]
        [TestCase(false, false, true)]
        public void LeavingRuntimeHandsOffWithoutLateWrites(bool oldCallbackFirst, bool lateTeardown,
            bool cleanupBeforeEnable)
        {
            using (var world = new PauseTestWorld())
            {
                world.Sample(1, escape: true, toggleHeld: true);
                Assert.That(world.Runtime.Service.PrepareSceneLeave(), Is.True);
                PauseTestWorld.Set(world.Runtime, "leavingForNewRun", true);
                ((PauseService)world.Runtime.Service).CompleteSceneLeave(0.017f, true);
                if (!lateTeardown) PauseTestWorld.Invoke(world.Runtime, "OnDisable");
                if (cleanupBeforeEnable) SceneLoaded(world.Runtime);
                var root = new UnityEngine.GameObject("Successor pause runtime");
                root.SetActive(false);
                var next = root.AddComponent<PauseRuntime>();
                try
                {
                    PauseTestWorld.Invoke(next, "Awake");
                    PauseTestWorld.Set(next, "runDirector", null);
                    PauseTestWorld.Invoke(next, "OnEnable");
                    Assert.That(next.enabled, Is.True);
                    Assert.That(GamePause.Service, Is.SameAs(next.Service));
                    // A scene may establish its own environment after Awake; the old owner cannot overwrite it.
                    next.Apply(new PauseSnapshot(0.75f, 0.019f, true, 0, false));
                    PauseSnapshot destination = next.Capture();
                    if (lateTeardown) PauseTestWorld.Invoke(world.Runtime, "OnDisable");
                    SceneLoaded(oldCallbackFirst ? world.Runtime : next);
                    SceneLoaded(oldCallbackFirst ? next : world.Runtime);
                    PauseTestWorld.Invoke(world.Runtime, "OnDestroy");
                    Assert.That(next.enabled, Is.True);
                    Assert.That(PauseTestWorld.Read<bool>(next, "ownsService"), Is.True);
                    Assert.That(GamePause.Service, Is.SameAs(next.Service));
                    Assert.That(next.Capture(), Is.EqualTo(destination));
                    Assert.That(GamePause.IsSimulationBlocked, Is.False);
                    next.Bind(world.Health, null);
                    PauseTestWorld.Set(next, "runDirector", null);
                    PauseTestWorld.Set(next, "gameplayScene", true);
                    Assert.That(next.TryPause(), Is.True, "Fresh run must accept pause again.");
                }
                finally
                {
                    PauseTestWorld.Invoke(next, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>Cleanup before successor creation uses the requested destination, then releases the service.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void DepartureCleanupBeforeSuccessorUsesDestinationBaseline(bool newRun)
        {
            using (var world = new PauseTestWorld())
            {
                world.Sample(1, escape: true, toggleHeld: true);
                world.Runtime.Service.PrepareSceneLeave();
                PauseTestWorld.Set(world.Runtime, "leavingForNewRun", newRun);
                PauseTestWorld.Set(world.Runtime, "normalFixedDeltaTime", 0.017f);
                PauseTestWorld.Invoke(world.Runtime, "OnDisable");
                // Simulate an unload callback changing the global environment after OnDisable.
                world.Runtime.Apply(new PauseSnapshot(0.5f, 0.009f, true, 0, true));
                SceneLoaded(world.Runtime);
                Assert.That(GamePause.Service, Is.Null);
                Assert.That(world.Runtime.Capture(), Is.EqualTo(PauseSnapshot.SceneLeave(0.017f, newRun)));
            }
        }

        /// <summary>True-death restart uses ordinary teardown, leaving no retained service to reject the next player.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TrueDeathTeardownAllowsFreshRuntime(bool oldCallbackFirst)
        {
            using (var world = new PauseTestWorld())
            {
                RunEvents.RaiseTrueDeath();
                world.Runtime.SetSceneLoadInProgress(true);
                PauseTestWorld.Invoke(world.Runtime, "OnDisable");
                Assert.That(GamePause.Service, Is.Null);
                var root = new UnityEngine.GameObject("Restart pause runtime");
                root.SetActive(false);
                var next = root.AddComponent<PauseRuntime>();
                try
                {
                    RunEvents.SetCurrentRun(new RunState(42));
                    PauseTestWorld.Invoke(next, "Awake");
                    PauseTestWorld.Set(next, "runDirector", null);
                    PauseTestWorld.Invoke(next, "OnEnable");
                    SceneLoaded(oldCallbackFirst ? world.Runtime : next);
                    SceneLoaded(oldCallbackFirst ? next : world.Runtime);
                    Assert.That(next.enabled, Is.True);
                    Assert.That(PauseTestWorld.Read<bool>(next, "ownsService"), Is.True);
                    Assert.That(GamePause.Service, Is.SameAs(next.Service));
                    next.Bind(world.Health, null);
                    PauseTestWorld.Set(next, "runDirector", null);
                    PauseTestWorld.Set(next, "gameplayScene", true);
                    Assert.That(next.TryPause(), Is.True);
                }
                finally
                {
                    PauseTestWorld.Invoke(next, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void SceneLoaded(PauseRuntime runtime) => PauseTestWorld.Invoke(runtime,
            "HandleSceneLoaded", default(UnityEngine.SceneManagement.Scene),
            UnityEngine.SceneManagement.LoadSceneMode.Single);

        /// <summary>A live owner still rejects duplicate players; only departing ownership can be replaced.</summary>
        [Test]
        public void NonLeavingOwnerStillRejectsDuplicateRuntime()
        {
            using (var world = new PauseTestWorld())
            {
                var root = new UnityEngine.GameObject("Duplicate pause runtime");
                root.SetActive(false);
                var duplicate = root.AddComponent<PauseRuntime>();
                try
                {
                    PauseTestWorld.Invoke(duplicate, "Awake");
                    PauseTestWorld.Invoke(duplicate, "OnEnable");
                    Assert.That(duplicate.enabled, Is.False);
                    Assert.That(PauseTestWorld.Read<bool>(duplicate, "ownsService"), Is.False);
                    Assert.That(GamePause.Service, Is.SameAs(world.Runtime.Service));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        /// <summary>Pause restart keeps the saved tower target and admits only settled live phases.</summary>
        [TestCase(RunPhase.Tower, true)]
        [TestCase(RunPhase.InAraf, true)]
        [TestCase(RunPhase.FallingToAraf, false)]
        [TestCase(RunPhase.EscapingAraf, false)]
        [TestCase(RunPhase.TrueDeath, false)]
        public void PauseRestartUsesSavedTowerAndLivePhaseGuards(RunPhase phase, bool allowed)
        {
            using (var world = new PauseTestWorld())
            {
                var director = world.Root.AddComponent<RunDirector>();
                var run = new RunState(51); run.SetPhase(phase);
                PauseTestWorld.Set(director, "_state", run);
                PauseTestWorld.Set(director, "towerSceneName", "SavedTower");
                PauseTestWorld.Set(world.Runtime, "runDirector", director);
                RunEvents.SetCurrentRun(run);
                world.Sample(1, escape: true, toggleHeld: true);
                Assert.That(GamePause.IsPaused, Is.EqualTo(allowed));
                Assert.That(director.TowerSceneName, Is.EqualTo("SavedTower"));
                Assert.That(director.CanRestartRunFromPause, Is.EqualTo(allowed));
                PauseTestWorld.Set(director, "towerSceneName", "");
                Assert.That(director.CanRestartRunFromPause, Is.False);
            }
        }

        /// <summary>Every load/transition latch independently blocks a second restart.</summary>
        [TestCase("_isTransitioning")]
        [TestCase("_preloadingAraf")]
        [TestCase("_restartRequested")]
        [TestCase("_sceneDeparture")]
        public void PauseRestartRejectsBusyDirector(string guard)
        {
            using (var world = new PauseTestWorld())
            {
                var director = world.Root.AddComponent<RunDirector>();
                PauseTestWorld.Set(director, "_state", new RunState(52));
                PauseTestWorld.Set(director, "towerSceneName", "SavedTower");
                Assert.That(director.CanRestartRunFromPause, Is.True);
                PauseTestWorld.Set(director, guard, true);
                Assert.That(director.CanRestartRunFromPause, Is.False);
                PauseTestWorld.Set(director, guard, false);
                Assert.That(director.CanRestartRunFromPause, Is.True);
            }
        }

        /// <summary>No director means reload the active test scene; preflight itself never issues a load.</summary>
        [Test]
        public void StandaloneNewRunPreflightsTheActiveSceneAndRequiresPreparedDeparture()
        {
            using (var world = new PauseTestWorld())
            {
                world.Runtime.StartNewRun();
                Assert.That(PauseTestWorld.Read<bool>(world.Runtime, "loadIssued"), Is.False);
                world.Sample(1, escape: true, toggleHeld: true);
                world.Runtime.StartNewRun();
                Assert.That(PauseTestWorld.Read<bool>(world.Runtime, "loadIssued"), Is.False);
                world.Runtime.TryPrepareSceneLeave(true);
                Assert.That(PauseTestWorld.Read<string>(world.Runtime, "leaveSceneName"),
                    Is.EqualTo(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
                Assert.That(PauseTestWorld.Read<bool>(world.Runtime, "loadIssued"), Is.False);
            }
        }

        /// <summary>A failed asynchronous-path preflight restores visible pause and emits one recovery event.</summary>
        [Test]
        public void FailedNewRunLoadRecoversPauseAndBusyLatchRejectsDuplicateCalls()
        {
            using (var world = new PauseTestWorld())
            {
                world.Sample(1, escape: true, toggleHeld: true);
                var service = (PauseService)world.Runtime.Service;
                Assert.That(service.PrepareSceneLeave(), Is.True);
                PauseTestWorld.Set(world.Runtime, "leavingForNewRun", true);
                PauseTestWorld.Set(world.Runtime, "leaveSceneName", "__MissingPauseRestartScene__");
                int failures = 0; world.Runtime.SceneLeaveFailed += () => failures++;
                PauseTestWorld.Set(world.Runtime, "loadIssued", true);
                world.Runtime.StartNewRun(); world.Runtime.StartNewRun(); world.Runtime.LoadMainMenu();
                Assert.That(failures, Is.Zero, "An issued operation cannot be issued again.");
                Assert.That(service.IsLeaving, Is.True);
                PauseTestWorld.Set(world.Runtime, "loadIssued", false);
                world.Runtime.LoadMainMenu();
                Assert.That(failures, Is.Zero, "The other destination cannot consume this confirmation.");
                world.Runtime.StartNewRun(); world.Runtime.StartNewRun();
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(service.IsLeaving, Is.False);
                Assert.That(service.IsPaused, Is.True);
                Assert.That(PauseTestWorld.Read<bool>(world.Runtime, "loadIssued"), Is.False);
                Assert.That(UnityEngine.Time.timeScale, Is.Zero);
                Assert.That(UnityEngine.AudioListener.pause, Is.True);
            }
        }

        /// <summary>Only Tower and InAraf permit a pause when a run exists.</summary>
        [TestCase(RunPhase.Tower, true)]
        [TestCase(RunPhase.InAraf, true)]
        [TestCase(RunPhase.FallingToAraf, false)]
        [TestCase(RunPhase.EscapingAraf, false)]
        [TestCase(RunPhase.TrueDeath, false)]
        public void AdapterMapsExactRunPhases(RunPhase phase, bool expected)
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, false, false, false, false, true, phase),
                Is.EqualTo(expected));
        }

        /// <summary>An open card offer denies pause through the explicit parameter.</summary>
        [Test]
        public void CardOfferFlagDeniesPause()
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, true, false, false, false, true,
                RunPhase.Tower), Is.False);
        }

        /// <summary>An open floor summary independently denies ordinary pause.</summary>
        [Test]
        public void FloorSummaryFlagDeniesPause()
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, false, true, false, false, true,
                RunPhase.Tower), Is.False);
        }

        /// <summary>MainMenu, dead player, death card, preload/load and transitions independently deny entry.</summary>
        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, false, false, true)]
        public void AdapterDeniesEveryExclusiveOwner(bool gameplay, bool alive, bool card, bool load, bool transition)
        {
            Assert.That(PauseRuntime.EvaluateEligibility(gameplay, alive, card, false, false, load, transition,
                true, RunPhase.Tower), Is.False);
        }

        /// <summary>A live standalone test remains eligible, while MainMenu always denies even without a run.</summary>
        [Test]
        public void NoRunDoesNotRequireRunDirectorAndNeverAdmitsMainMenu()
        {
            Assert.That(PauseRuntime.EvaluateEligibility(true, true, false, false, false, false, false, false,
                RunPhase.Tower), Is.True);
            Assert.That(PauseRuntime.EvaluateEligibility(false, true, false, false, false, false, false, false,
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
