using System;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.Run;
using RetroFPS.UI.HUD;
using RetroFPS.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RetroFPS.Tests
{
    /// <summary>Isolated real adapter fixture; no scene loading, OS input or async activation is simulated.</summary>
    internal sealed class PauseTestWorld : IDisposable
    {
        internal sealed class Sink : IPauseMenuRequestSink
        {
            internal PauseRuntime Runtime { get; set; }
            internal int Opens { get; private set; }
            internal int Backs { get; private set; }
            internal int Resumes { get; private set; }
            internal int Cancels { get; private set; }
            public void Open() => Opens++;
            public void Back() => Backs++;
            public void Resume() { Resumes++; Runtime.Resume(); }
            public void CancelForExclusiveFlow() => Cancels++;
        }

        internal GameObject Root { get; }
        internal PauseRuntime Runtime { get; }
        internal PlayerHealth Health { get; }
        internal Sink Menu { get; }
        private readonly IPauseService previousService;
        private readonly RunState previousRun;
        private readonly PauseSnapshot previousEnvironment;
        private readonly float previousClock;
        private readonly float previousDelta;

        internal PauseTestWorld()
        {
            previousService = GamePause.Service;
            previousRun = RunEvents.CurrentRun;
            previousClock = GamePause.UnscaledTime;
            previousDelta = GamePause.UnscaledDeltaTime;
            previousEnvironment = new PauseSnapshot(Time.timeScale, Time.fixedDeltaTime,
                AudioListener.pause, (int)Cursor.lockState, Cursor.visible);
            GamePause.SetServiceForTests(null);
            RunEvents.SetCurrentRun(null);
            Root = new GameObject("Pause adapter test");
            Root.SetActive(false);
            Health = Root.AddComponent<PlayerHealth>();
            Set(Health, "_currentHealth", 100f);
            Runtime = Root.AddComponent<PauseRuntime>();
            Invoke(Runtime, "Awake");
            Set(Runtime, "runDirector", null);
            Set(Runtime, "gameplayScene", true);
            Menu = new Sink { Runtime = Runtime };
            Runtime.Bind(Health, Menu);
            Invoke(Runtime, "OnEnable");
        }

        internal void Sample(int frame, bool escape = false, bool start = false, bool back = false,
            bool toggleHeld = false, bool actionHeld = false, float delta = 0.1f) =>
            Runtime.ProcessInput(frame, escape, start, back, toggleHeld, actionHeld, delta);

        internal static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        internal static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        internal static void Invoke(object target, string name, params object[] arguments) => target.GetType()
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, arguments);

        public void Dispose()
        {
            Invoke(Runtime, "OnDisable");
            // Complete the retained destination subscription without performing an actual scene load.
            if (Runtime.Service is PauseService leaving && leaving.IsLeaving)
                Invoke(Runtime, "HandleSceneLoaded", default(UnityEngine.SceneManagement.Scene),
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            UnityEngine.Object.DestroyImmediate(Root);
            RunEvents.SetCurrentRun(previousRun);
            GamePause.SetService(previousService);
            typeof(GamePause).GetField("unscaledTime", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, previousClock);
            typeof(GamePause).GetField("unscaledDeltaTime", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, previousDelta);
            Time.timeScale = previousEnvironment.TimeScale;
            Time.fixedDeltaTime = previousEnvironment.FixedDeltaTime;
            AudioListener.pause = previousEnvironment.AudioPaused;
            Cursor.lockState = (CursorLockMode)previousEnvironment.CursorLockMode;
            Cursor.visible = previousEnvironment.CursorVisible;
        }
    }

    /// <summary>A real WeaponBase probe counts emitted shots and ticks without running combat setup.</summary>
    public sealed class PauseWeaponProbe : WeaponBase
    {
        /// <inheritdoc/>
        public override WeaponSlot Slot => WeaponSlot.RepeaterArbalest;
        /// <inheritdoc/>
        public override bool IsFullAuto => true;
        /// <inheritdoc/>
        public override bool CanFirePrimary => true;
        /// <inheritdoc/>
        public override bool CanFireAlt => true;
        /// <summary>Observed primary fire count.</summary>
        public int PrimaryCount { get; private set; }
        /// <summary>Observed alternate fire count.</summary>
        public int AltCount { get; private set; }
        /// <summary>Observed weapon simulation count.</summary>
        public int TickCount { get; private set; }
        /// <inheritdoc/>
        public override void Tick(float deltaTime) => TickCount++;
        /// <inheritdoc/>
        public override void FirePrimary() => PrimaryCount++;
        /// <inheritdoc/>
        public override void FireAlt() => AltCount++;
    }

    /// <summary>Real input adapter, consumer and clock coverage using deterministic injected samples.</summary>
    public sealed class PauseInputAndClockTests
    {
        private const float CounterActiveWindow = 0.18f;
        private const float CounterWhiffRecovery = 0.35f;
        private PauseTestWorld world;
        private Mouse mouse;

        /// <summary>Isolates global state and creates an inactive player fixture.</summary>
        [SetUp]
        public void SetUp() => world = new PauseTestWorld();

        /// <summary>Restores input devices and every mutated global.</summary>
        [TearDown]
        public void TearDown()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            world.Dispose();
        }

        /// <summary>Entry consumes the opening frame; simultaneous commands issue exactly one request.</summary>
        [Test]
        public void OpeningFrameAndSimultaneousCommandsAreConsumedOnce()
        {
            world.Sample(1, escape: true, start: true, back: true, toggleHeld: true);
            Assert.That(GamePause.IsGameplayInputBlocked, Is.True);
            Assert.That(world.Menu.Opens, Is.EqualTo(1));
            Assert.That(world.Menu.Backs + world.Menu.Resumes, Is.Zero);
            world.Sample(1, escape: true, back: true);
            Assert.That(world.Menu.Backs, Is.Zero);
            world.Sample(2, escape: true, back: true);
            world.Sample(2, back: true);
            Assert.That(world.Menu.Backs, Is.EqualTo(1));
        }

        /// <summary>Start resolves the UI once; its entire resume frame remains gated until actions release.</summary>
        [Test]
        public void ResumeFrameAndHeldActionsCannotRelockOrFire()
        {
            world.Sample(1, escape: true, toggleHeld: true);
            world.Sample(2);
            world.Sample(3, start: true, toggleHeld: true, actionHeld: true);
            Assert.That(world.Menu.Resumes, Is.EqualTo(1));
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(GamePause.IsGameplayInputBlocked, Is.True);
            Cursor.lockState = CursorLockMode.None;
            world.Root.AddComponent<PlayerLook>().LockCursor();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            world.Sample(4, actionHeld: true);
            Assert.That(GamePause.IsGameplayInputBlocked, Is.True);
            world.Sample(5);
            Assert.That(GamePause.IsGameplayInputBlocked, Is.False);
        }

        /// <summary>Real weapon Tick/fire remain silent through pause and held resume; a new press fires once.</summary>
        [Test]
        public void RealFullAutoWeaponRequiresReleaseAndFreshPress()
        {
            mouse = InputSystem.AddDevice<Mouse>();
            var controller = world.Root.AddComponent<WeaponController>();
            var probe = world.Root.AddComponent<PauseWeaponProbe>();
            PauseTestWorld.Set(controller, "_activeWeapon", probe);
            world.Sample(1, escape: true, toggleHeld: true);
            PressMouse(true);
            PauseTestWorld.Invoke(controller, "Update");
            Assert.That(probe.TickCount + probe.PrimaryCount + probe.AltCount, Is.Zero);
            world.Sample(2);
            world.Runtime.Resume();
            world.Sample(3, actionHeld: true);
            PauseTestWorld.Invoke(controller, "Update");
            Assert.That(probe.PrimaryCount, Is.Zero);
            PressMouse(false);
            world.Sample(4);
            PauseTestWorld.Invoke(controller, "Update");
            Assert.That(probe.PrimaryCount, Is.Zero);
            PressMouse(true);
            world.Sample(5, actionHeld: true);
            PauseTestWorld.Invoke(controller, "Update");
            Assert.That(probe.PrimaryCount, Is.EqualTo(1));
            Assert.That(probe.AltCount, Is.Zero);
        }

        private void PressMouse(bool held)
        {
            var state = new MouseState();
            if (held) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        /// <summary>Banner deadlines, clock deltas and real RunDirector elapsed exclude all paused duration.</summary>
        [Test]
        public void GameplayClockBannerAndRunElapsedFreezeWithoutCatchUp()
        {
            var run = new RunState(41);
            var director = world.Root.AddComponent<RunDirector>();
            PauseTestWorld.Set(director, "_state", run);
            var banner = new HudBannerQueue();
            world.Sample(1, delta: 0.25f);
            director.AdvanceElapsedTime(0.25f);
            banner.Enqueue(HudBannerKind.ArenaSealed, 0, GamePause.UnscaledTime);
            double remaining = banner.ExpiresAt - GamePause.UnscaledTime;
            world.Sample(2, escape: true, toggleHeld: true, delta: 50f);
            director.AdvanceElapsedTime(50f);
            world.Sample(3, delta: 50f);
            banner.Tick(GamePause.UnscaledTime);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.25f));
            Assert.That(GamePause.UnscaledDeltaTime, Is.Zero);
            Assert.That(run.ElapsedSeconds, Is.EqualTo(0.25f));
            Assert.That(banner.ExpiresAt - GamePause.UnscaledTime, Is.EqualTo(remaining));
            world.Runtime.Resume();
            world.Sample(4, delta: 0.125f);
            director.AdvanceElapsedTime(0.125f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.375f));
            Assert.That(run.ElapsedSeconds, Is.EqualTo(0.375f));
        }

        /// <summary>Paused damage/heal calls and movement/parry updates preserve health and active ability state.</summary>
        [Test]
        public void RealConsumersRejectPausedMutationsWithoutResettingMotion()
        {
            var movement = world.Root.GetComponent<PlayerMovement>();
            PauseTestWorld.Set(movement, "_velocity", new Vector3(2f, 3f, 4f));
            PauseTestWorld.Set(movement, "_isDashing", true);
            var parry = world.Root.AddComponent<PlayerParryController>();
            var counterTimer = new CounterVerdictTimer(CounterActiveWindow, CounterWhiffRecovery);
            counterTimer.TryStart();
            PauseTestWorld.Set(parry, "_timer", counterTimer);
            PauseTestWorld.Set(parry, "enableCounter", true);
            world.Sample(1, escape: true, toggleHeld: true);
            world.Health.TakeDamage(500f, Vector3.zero, Vector3.up);
            Assert.That(world.Health.TakeUnparryableDamage(500f, Vector3.zero, Vector3.up), Is.False);
            Assert.That(world.Health.Heal(25f, true), Is.False);
            PauseTestWorld.Invoke(movement, "Update");
            PauseTestWorld.Invoke(parry, "Update");
            Assert.That(world.Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(movement.Velocity, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(movement.IsDashing, Is.True);
            // Pausing resets the strike window: no stale parry window while paused or after resume.
            Assert.That(parry.IsParrying, Is.False);
            Assert.That(parry.TryParryMelee(Vector3.zero, Vector3.up, 10f), Is.False);
        }

        /// <summary>A Core-only exclusive cancel still hides the injected UI synchronously.</summary>
        [Test]
        public void CoreExclusiveCancellationImmediatelyForwardsToSink()
        {
            world.Sample(1, escape: true, toggleHeld: true);
            world.Runtime.Service.CancelForExclusiveFlow();
            Assert.That(world.Menu.Cancels, Is.EqualTo(1));
            Assert.That(GamePause.IsPaused, Is.False);
        }
    }
}
