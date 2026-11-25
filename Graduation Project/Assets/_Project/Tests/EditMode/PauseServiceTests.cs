using System;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Pause;

namespace RetroFPS.Tests
{
    /// <summary>Deterministic ownership/input/clock tests; no Unity environment is mutated.</summary>
    public sealed class PauseServiceTests
    {
        private sealed class FakeEnvironment : IPauseEnvironment
        {
            internal PauseSnapshot Current { get; set; }
            internal int CaptureCount { get; private set; }
            internal int ApplyCount { get; private set; }

            internal FakeEnvironment(PauseSnapshot initial) { Current = initial; }
            public PauseSnapshot Capture() { CaptureCount++; return Current; }
            public void Apply(PauseSnapshot snapshot) { ApplyCount++; Current = snapshot; }
        }

        private FakeEnvironment environment;
        private PauseService service;
        private PauseSnapshot initial;

        /// <summary>Isolates the environment and static clock before each case.</summary>
        [SetUp]
        public void SetUp()
        {
            initial = new PauseSnapshot(0.65f, 0.013f, false, 1, false);
            environment = new FakeEnvironment(initial);
            service = new PauseService(environment);
            GamePause.SetServiceForTests(service);
        }

        /// <summary>Releases only the fake owner and clears static test state.</summary>
        [TearDown]
        public void TearDown()
        {
            service.Release();
            GamePause.SetServiceForTests(null);
        }

        private bool Pause() => service.TryPause(true, true, false, false, true);

        /// <summary>Exclusive departure ignores held/consumed toggle input and never captures the modal's zero clock.</summary>
        [Test]
        public void ExclusiveDepartureUsesSuppliedBaselineAndRecovers()
        {
            Assert.That(Pause(), Is.True);
            service.Resume();
            environment.Current = new PauseSnapshot(0f, initial.FixedDeltaTime, true, 0, true);
            int captures = environment.CaptureCount;
            Assert.That(Pause(), Is.False);
            Assert.That(service.PrepareSceneLeave(initial), Is.True);
            Assert.That(service.PrepareSceneLeave(initial), Is.False);
            Assert.That(environment.CaptureCount, Is.EqualTo(captures));
            Assert.That(service.IsLeaving, Is.True);
            service.RecoverSceneLeave();
            service.Resume();
            Assert.That(environment.Current.TimeScale, Is.EqualTo(initial.TimeScale));
            Assert.That(environment.Current.FixedDeltaTime, Is.EqualTo(initial.FixedDeltaTime));
            Assert.That(environment.Current.AudioPaused, Is.EqualTo(initial.AudioPaused));
        }

        /// <summary>Gameplay and menu departures share normal time/audio but have distinct cursor ownership.</summary>
        [TestCase(true, 1, false)]
        [TestCase(false, 0, true)]
        public void SceneLeaveUsesDestinationCursor(bool gameplay, int cursorMode, bool visible)
        {
            Pause();
            service.PrepareSceneLeave();
            service.CompleteSceneLeave(0.017f, gameplay);
            var expected = new PauseSnapshot(1f, 0.017f, false, cursorMode, visible);
            AssertSnapshot(environment.Current, expected);
            AssertSnapshot(PauseSnapshot.SceneLeave(0.017f, gameplay), expected);
            service.CompleteSceneLeave(0.033f, !gameplay);
            AssertSnapshot(environment.Current, expected);
            Assert.That(service.SimulationBlocked && service.GameplayInputBlocked, Is.True);
            service.RecoverSceneLeave();
            Assert.That(service.IsPaused, Is.True);
            service.Resume();
            AssertSnapshot(environment.Current, new PauseSnapshot(1f, 0.017f,
                initial.AudioPaused, initial.CursorLockMode, initial.CursorVisible));
        }

        private static void AssertSnapshot(PauseSnapshot actual, PauseSnapshot expected)
        {
            Assert.That(actual.TimeScale, Is.EqualTo(expected.TimeScale));
            Assert.That(actual.FixedDeltaTime, Is.EqualTo(expected.FixedDeltaTime));
            Assert.That(actual.AudioPaused, Is.EqualTo(expected.AudioPaused));
            Assert.That(actual.CursorLockMode, Is.EqualTo(expected.CursorLockMode));
            Assert.That(actual.CursorVisible, Is.EqualTo(expected.CursorVisible));
        }

        /// <summary>Construction does not take environment ownership.</summary>
        [Test]
        public void ConstructionIsInertAndRejectsMissingEnvironment()
        {
            Assert.That(service.IsPaused, Is.False);
            Assert.That(service.SimulationBlocked, Is.False);
            Assert.That(service.GameplayInputBlocked, Is.False);
            Assert.That(environment.CaptureCount, Is.Zero);
            Assert.That(environment.ApplyCount, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => new PauseService(null));
        }

        /// <summary>Pause never normalizes unusual clock values or modifies the physics step.</summary>
        [TestCase(0.65f, 0.013f)]
        [TestCase(0f, 0.007f)]
        [TestCase(2.5f, 0.033333f)]
        [TestCase(1f, 0f)]
        public void ExactClockSnapshotSurvivesPauseAndResume(float scale, float step)
        {
            environment.Current = new PauseSnapshot(scale, step, false, 1, false);
            PauseSnapshot expected = environment.Current;
            Assert.That(Pause(), Is.True);
            Assert.That(service.IsPaused && service.SimulationBlocked && service.GameplayInputBlocked, Is.True);
            AssertSnapshot(environment.Current, new PauseSnapshot(0f, step, true, 0, true));
            service.Resume();
            AssertSnapshot(environment.Current, expected);
            Assert.That(service.IsPaused || service.SimulationBlocked, Is.False);
            Assert.That(service.GameplayInputBlocked, Is.True);
        }

        /// <summary>Restoration preserves preexisting audio and all cursor ownership variants.</summary>
        [TestCase(false, 1, false)]
        [TestCase(true, 1, false)]
        [TestCase(true, 2, true)]
        [TestCase(false, 0, true)]
        public void AudioAndCursorAreRestoredExactly(bool audio, int cursorMode, bool visible)
        {
            PauseSnapshot expected = new PauseSnapshot(0.4f, 0.017f, audio, cursorMode, visible);
            environment.Current = expected;
            Pause();
            service.Resume();
            AssertSnapshot(environment.Current, expected);
        }

        /// <summary>Duplicate calls cannot overwrite the original snapshot or republish ownership.</summary>
        [Test]
        public void DuplicatePauseResumeAndTeardownAreIdempotent()
        {
            int changed = 0;
            service.Changed += () => changed++;
            service.Resume();
            Assert.That(Pause(), Is.True);
            Assert.That(Pause(), Is.False);
            service.UpdateInput(1, false, false);
            Assert.That(Pause(), Is.False);
            Assert.That(environment.CaptureCount, Is.EqualTo(1));
            service.Resume();
            service.Resume();
            service.Release();
            AssertSnapshot(environment.Current, initial);
            Assert.That(environment.ApplyCount, Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(2));
        }

        /// <summary>Each primitive eligibility exclusion denies without touching the environment.</summary>
        [TestCase(false, true, false, false, true)]
        [TestCase(true, false, false, false, true)]
        [TestCase(true, true, true, false, true)]
        [TestCase(true, true, false, true, true)]
        [TestCase(true, true, false, false, false)]
        public void IneligibleRequestIsDiscarded(bool gameplay, bool alive, bool death, bool loading, bool phase)
        {
            Assert.That(service.TryPause(gameplay, alive, death, loading, phase), Is.False);
            Assert.That(environment.CaptureCount, Is.Zero);
            Assert.That(environment.ApplyCount, Is.Zero);
            Assert.That(service.GameplayInputBlocked, Is.False);
            service.UpdateInput(1, true, false);
            Assert.That(Pause(), Is.False, "Eligibility returning cannot accept a held toggle.");
            service.UpdateInput(2, false, false);
            Assert.That(service.IsPaused, Is.False, "Release never executes a queued request.");
            service.UpdateInput(3, true, false);
            Assert.That(Pause(), Is.True);
        }

        /// <summary>Ordinary disable restores once and consumes input for the closing frame.</summary>
        [Test]
        public void OrdinaryReleaseRestoresTheCapturedEnvironment()
        {
            Pause();
            service.Release();
            service.Release();
            AssertSnapshot(environment.Current, initial);
            Assert.That(environment.ApplyCount, Is.EqualTo(2));
            Assert.That(service.GameplayInputBlocked, Is.True);
        }

        /// <summary>Death/transition handoff restores clocks/audio without overriding the next owner's cursor.</summary>
        [TestCase(0, true)]
        [TestCase(2, false)]
        public void ExclusiveCancelPreservesNewOwnersCursor(int cursorMode, bool visible)
        {
            Pause();
            environment.Current = new PauseSnapshot(0f, initial.FixedDeltaTime, true, cursorMode, visible);
            service.CancelForExclusiveFlow();
            AssertSnapshot(environment.Current, new PauseSnapshot(initial.TimeScale, initial.FixedDeltaTime,
                initial.AudioPaused, cursorMode, visible));
            Assert.That(service.SimulationBlocked, Is.False);
            Assert.That(service.GameplayInputBlocked, Is.True);
            PauseSnapshot nextOwner = new PauseSnapshot(0.15f, 0.003f, true, 0, true);
            environment.Current = nextOwner;
            service.CancelForExclusiveFlow();
            service.Release();
            AssertSnapshot(environment.Current, nextOwner);
            Assert.That(environment.ApplyCount, Is.EqualTo(2));
        }

        /// <summary>Synchronous exclusive ownership beats a just-accepted request.</summary>
        [Test]
        public void ExclusiveOwnerCanCancelDuringChangedNotification()
        {
            service.Changed += () => { if (service.IsPaused) service.CancelForExclusiveFlow(); };
            Assert.That(Pause(), Is.False);
            Assert.That(service.IsPaused, Is.False);
            Assert.That(environment.Current.TimeScale, Is.EqualTo(initial.TimeScale));
            Assert.That(environment.Current.CursorVisible, Is.True);
        }

        /// <summary>Hitstop cancellation revises only saved time values until ownership is released.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void RestorationTargetCanChangeWithoutUnpausing(bool leaving)
        {
            Pause();
            if (leaving) service.PrepareSceneLeave();
            PauseSnapshot paused = environment.Current;
            service.SetRestorationTime(0.9f, 0.011f);
            AssertSnapshot(environment.Current, paused);
            Assert.That(environment.ApplyCount, Is.EqualTo(1));
            if (leaving) service.RecoverSceneLeave();
            service.Resume();
            AssertSnapshot(environment.Current, new PauseSnapshot(0.9f, 0.011f,
                initial.AudioPaused, initial.CursorLockMode, initial.CursorVisible));
        }

        /// <summary>A stale hitstop callback cannot modify a later acquisition.</summary>
        [Test]
        public void RestorationRevisionOutsideOwnershipIsIgnored()
        {
            service.SetRestorationTime(9f, 0.1f);
            Pause();
            service.Resume();
            AssertSnapshot(environment.Current, initial);
        }

        /// <summary>Departure requires an acquired pause and every inactive departure operation is inert.</summary>
        [Test]
        public void SceneLeaveWithoutOwnershipDoesNothing()
        {
            Assert.That(service.PrepareSceneLeave(), Is.False);
            service.CompleteSceneLeave(0.016f);
            service.RecoverSceneLeave();
            service.CancelForExclusiveFlow();
            Assert.That(environment.ApplyCount, Is.Zero);
            Assert.That(service.IsLeaving, Is.False);
        }

        /// <summary>The leave latch survives cleanup, repeated requests and player teardown.</summary>
        [Test]
        public void SceneLeaveKeepsGatesThroughFadeCleanupAndTeardown()
        {
            Pause();
            Assert.That(service.PrepareSceneLeave(), Is.True);
            Assert.That(service.PrepareSceneLeave(), Is.False);
            service.Resume();
            service.Release();
            service.CancelForExclusiveFlow();
            Assert.That(service.IsPaused, Is.True);
            Assert.That(environment.ApplyCount, Is.EqualTo(1));
            service.CompleteSceneLeave(0.016f);
            PauseSnapshot destination = new PauseSnapshot(1f, 0.016f, false, 0, true);
            AssertSnapshot(environment.Current, destination);
            Assert.That(service.IsPaused, Is.False);
            Assert.That(service.IsLeaving && service.SimulationBlocked && service.GameplayInputBlocked, Is.True);
            service.UpdateInput(10, false, false);
            service.CompleteSceneLeave(0.04f);
            service.SetRestorationTime(0f, 0.001f);
            service.Release();
            service.CancelForExclusiveFlow();
            Assert.That(Pause(), Is.False);
            AssertSnapshot(environment.Current, destination);
            Assert.That(environment.ApplyCount, Is.EqualTo(2));
            Assert.That(service.SimulationBlocked && service.GameplayInputBlocked, Is.True);
        }

        /// <summary>Invalid normal baselines cannot partially clean up an owned pause.</summary>
        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SceneLeaveRejectsInvalidNormalPhysicsStep(float step)
        {
            Pause();
            service.PrepareSceneLeave();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.CompleteSceneLeave(step));
            Assert.That(service.IsPaused, Is.True);
            Assert.That(environment.ApplyCount, Is.EqualTo(1));
            service.CompleteSceneLeave(0.012f);
            Assert.That(environment.Current.FixedDeltaTime, Is.EqualTo(0.012f));
        }

        /// <summary>Failed departure restores pause, retaining original targets until hitstop was cancelled for loading.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void SceneLeaveFailureRestoresPauseAndCanResumeOrRetry(bool cleanedUp)
        {
            Pause();
            service.PrepareSceneLeave();
            if (cleanedUp) service.CompleteSceneLeave(0.016f);
            service.RecoverSceneLeave();
            int writes = environment.ApplyCount;
            service.RecoverSceneLeave();
            Assert.That(environment.ApplyCount, Is.EqualTo(writes));
            Assert.That(service.IsPaused, Is.True);
            Assert.That(service.IsLeaving, Is.False);
            AssertSnapshot(environment.Current, new PauseSnapshot(0f,
                cleanedUp ? 0.016f : initial.FixedDeltaTime, true, 0, true));
            Assert.That(service.PrepareSceneLeave(), Is.True, "A failure permits an explicit retry.");
            service.RecoverSceneLeave();
            service.Resume();
            AssertSnapshot(environment.Current, new PauseSnapshot(cleanedUp ? 1f : initial.TimeScale,
                cleanedUp ? 0.016f : initial.FixedDeltaTime, initial.AudioPaused,
                initial.CursorLockMode, initial.CursorVisible));
        }

        /// <summary>Release sampled during the closing frame cannot expose that frame's UI input to gameplay.</summary>
        [Test]
        public void ResumeFrameStaysBlockedRegardlessOfInputSamplingOrder()
        {
            service.UpdateInput(10, true, false);
            Pause();
            service.UpdateInput(11, false, false);
            service.Resume();
            Assert.That(service.GameplayInputBlocked, Is.True);
            service.UpdateInput(11, false, false);
            Assert.That(service.GameplayInputBlocked, Is.True);
            Assert.That(Pause(), Is.False, "The same frame cannot reopen pause.");
            service.UpdateInput(12, false, false);
            Assert.That(service.GameplayInputBlocked, Is.False);
        }

        /// <summary>Held actions remain suppressed across multiple frames until a sampled release.</summary>
        [Test]
        public void HeldGameplayActionsMustBeReleasedAfterResume()
        {
            Pause();
            service.UpdateInput(1, false, true);
            service.Resume();
            for (int frame = 2; frame < 8; frame++)
            {
                service.UpdateInput(frame, false, true);
                Assert.That(service.GameplayInputBlocked, Is.True);
                Assert.That(service.SimulationBlocked, Is.False);
            }
            service.UpdateInput(8, false, false);
            Assert.That(service.GameplayInputBlocked, Is.False);
            service.UpdateInput(9, false, true);
            Assert.That(service.GameplayInputBlocked, Is.False, "Fresh gameplay presses are allowed.");
        }

        /// <summary>A closing toggle cannot reopen pause until released and freshly requested.</summary>
        [Test]
        public void HeldToggleCannotReopenPauseAfterResume()
        {
            Pause();
            service.Resume();
            service.UpdateInput(1, true, false);
            Assert.That(Pause(), Is.False);
            service.UpdateInput(2, false, false);
            Assert.That(service.IsPaused, Is.False);
            service.UpdateInput(3, true, false);
            Assert.That(Pause(), Is.True);
        }

        /// <summary>Observers see applied environment and are notified when release unblocks input.</summary>
        [Test]
        public void ChangedPublishesAppliedStateAndGateChangesOnly()
        {
            int notifications = 0;
            service.Changed += () =>
            {
                notifications++;
                Assert.That(environment.Current.TimeScale, Is.EqualTo(service.IsPaused ? 0f : initial.TimeScale));
            };
            Pause();
            service.UpdateInput(1, false, false);
            service.Resume();
            service.UpdateInput(2, false, true);
            Assert.That(notifications, Is.EqualTo(2));
            service.UpdateInput(3, false, false);
            Assert.That(notifications, Is.EqualTo(3));
            service.UpdateInput(4, false, false);
            Assert.That(notifications, Is.EqualTo(3));
        }

        /// <summary>Pause duration is excluded; zero-scale hitstop remains presentation time.</summary>
        [TestCase(0f)]
        [TestCase(0.1f)]
        public void UnscaledClockFreezesWithoutCatchUpAndContinuesThroughHitstop(float scale)
        {
            environment.Current = new PauseSnapshot(scale, 0.012f, false, 1, false);
            GamePause.AdvanceClock(0.25f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.25f));
            Pause();
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero, "Entry immediately hides the previous delta.");
            GamePause.AdvanceClock(20f);
            GamePause.AdvanceClock(30f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.25f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
            service.Resume();
            GamePause.AdvanceClock(0.125f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.375f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.EqualTo(0.125f));
            Assert.That(environment.Current.TimeScale, Is.EqualTo(scale));
        }

        /// <summary>Destination cleanup cannot restart the clock while simulation is still latched.</summary>
        [Test]
        public void SceneLeaveKeepsClockFrozenAfterCleanup()
        {
            GamePause.AdvanceClock(0.5f);
            Pause();
            service.PrepareSceneLeave();
            service.CompleteSceneLeave(0.016f);
            GamePause.AdvanceClock(40f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.5f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(GamePause.IsSimulationBlocked && GamePause.IsGameplayInputBlocked, Is.True);
        }

        /// <summary>Invalid adapter deltas cannot poison absolute presentation timestamps.</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidClockDeltasAreIgnored(float delta)
        {
            GamePause.AdvanceClock(0.5f);
            GamePause.AdvanceClock(delta);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.5f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
        }

        /// <summary>Additive owner rebinding preserves timestamps; test injection clears all clock values.</summary>
        [Test]
        public void StaticBindingAndTestInjectionHaveExplicitClockLifetimes()
        {
            GamePause.AdvanceClock(0.75f);
            GamePause.SetService(null);
            Assert.That(GamePause.Service, Is.Null);
            Assert.That(GamePause.IsPaused || GamePause.IsSimulationBlocked || GamePause.IsGameplayInputBlocked, Is.False);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.75f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
            GamePause.SetService(service);
            Assert.That(GamePause.Service, Is.SameAs(service));
            GamePause.SetServiceForTests(service);
            Assert.That(GamePause.UnscaledTime, Is.Zero);
        }

        /// <summary>The domain-reload-disabled reset drops the owner and both clock values.</summary>
        [Test]
        public void SubsystemResetClearsStaticState()
        {
            GamePause.AdvanceClock(3f);
            Pause();
            MethodInfo reset = typeof(GamePause).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
            Assert.That(GamePause.Service, Is.Null);
            Assert.That(GamePause.UnscaledTime, Is.Zero);
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
            Assert.That(GamePause.IsSimulationBlocked || GamePause.IsGameplayInputBlocked, Is.False);
        }
    }
}
