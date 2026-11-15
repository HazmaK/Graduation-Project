using System.Reflection;
using NUnit.Framework;
using RetroFPS.Pause;
using UnityEngine;

namespace RetroFPS.Tests
{
    /// <summary>Real HitstopManager arbitration; engine device/scene activation still requires Play Mode.</summary>
    public sealed class PauseHitstopTests
    {
        private PauseTestWorld world;
        private GameObject hitstopObject;
        private HitstopManager hitstop;
        private HitstopManager previousInstance;

        /// <summary>Creates an enabled hitstop and preserves the editor's previous singleton and globals.</summary>
        [SetUp]
        public void SetUp()
        {
            previousInstance = HitstopManager.Instance;
            SetInstance(null);
            world = new PauseTestWorld();
            Time.timeScale = 0.8f;
            Time.fixedDeltaTime = 0.013f;
            PauseTestWorld.Set(world.Runtime, "normalFixedDeltaTime", 0.013f);
            hitstopObject = new GameObject("Pause hitstop test");
            hitstop = hitstopObject.AddComponent<HitstopManager>();
            if (HitstopManager.Instance != hitstop) PauseTestWorld.Invoke(hitstop, "Awake");
        }

        /// <summary>Destroys only test objects and restores all static/environment state.</summary>
        [TearDown]
        public void TearDown()
        {
            if (hitstopObject != null) Object.DestroyImmediate(hitstopObject);
            world.Dispose();
            SetInstance(previousInstance);
        }

        private static void SetInstance(HitstopManager value) => typeof(HitstopManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, value);

        /// <summary>Half-finished hitstop freezes exactly, rejects new requests and resumes its original baseline.</summary>
        [TestCase(0f)]
        [TestCase(0.04f)]
        public void PauseHalfwayPreservesTimerAndInstantaneousScale(float freezeScale)
        {
            hitstop.TriggerHitstop(0.2f, freezeScale);
            hitstop.AdvanceHitstop(0.1f);
            float instantaneousScale = Time.timeScale;
            float instantaneousStep = Time.fixedDeltaTime;
            world.Sample(1, escape: true, toggleHeld: true);
            Assert.That(GamePause.IsPaused, Is.True);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(instantaneousStep));
            hitstop.AdvanceHitstop(10f);
            hitstop.TriggerHitstop(100f, 0f);
            Assert.That(hitstop.RemainingSeconds, Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(Time.timeScale, Is.Zero);
            world.Runtime.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(instantaneousScale));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(instantaneousStep));
            hitstop.AdvanceHitstop(0.11f);
            Assert.That(hitstop.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.013f));
        }

        /// <summary>Disabling/destroying hitstop revises saved restoration without writing the paused clock.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void DisableOrDestroyDuringPauseDefersUnderlyingRestoration(bool destroy)
        {
            hitstop.TriggerHitstop(0.2f, 0.03f);
            float frozenStep = Time.fixedDeltaTime;
            world.Sample(1, escape: true, toggleHeld: true);
            if (destroy)
            {
                // Runtime-only callbacks are driven explicitly in this EditMode fixture, as is Awake.
                // Exercise OnDestroy directly so its fallback is covered independently of OnDisable.
                PauseTestWorld.Invoke(hitstop, "OnDestroy");
                Assert.That(hitstop.IsActive, Is.False);
                Assert.That(hitstop.RemainingSeconds, Is.Zero);
                Assert.That(HitstopManager.Instance, Is.Null);
                Object.DestroyImmediate(hitstop);
            }
            else
            {
                hitstop.enabled = false;
                PauseTestWorld.Invoke(hitstop, "OnDisable");
            }
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(frozenStep));
            world.Runtime.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(0.8f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.013f));
        }

        /// <summary>Destination cleanup survives hitstop teardown and duplicate pause teardown without stale writes.</summary>
        [Test]
        public void SceneLeaveKeepsNormalPhysicsAndDestinationEnvironment()
        {
            hitstop.TriggerHitstop(0.2f, 0f);
            world.Sample(1, escape: true, toggleHeld: true);
            var owner = (PauseService)world.Runtime.Service;
            Assert.That(owner.PrepareSceneLeave(), Is.True);
            hitstop.CancelForSceneLeave();
            owner.CompleteSceneLeave(0.013f);
            Object.DestroyImmediate(hitstop);
            PauseTestWorld.Invoke(world.Runtime, "OnDisable");
            PauseTestWorld.Invoke(world.Runtime, "OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.013f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(GamePause.IsSimulationBlocked, Is.True);
        }

        /// <summary>The real-time gameplay clock continues through zero-scale hitstop.</summary>
        [Test]
        public void HitstopAloneDoesNotBlockGameplayClock()
        {
            hitstop.TriggerHitstop(0.2f, 0f);
            world.Sample(1, delta: 0.1f);
            Assert.That(GamePause.UnscaledTime, Is.EqualTo(0.1f));
            Assert.That(GamePause.IsPaused, Is.False);
        }
    }
}
