using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RetroFPS.Run;

namespace RetroFPS.Pause
{
    /// <summary>Early gameplay owner of pause input, Unity environment state and scene departure.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class PauseRuntime : MonoBehaviour, IPauseEnvironment
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private RunDirector runDirector;

        private PauseService service;
        private IPauseMenuRequestSink menu;
        private RunState run;
        private RunPhase phase;
        private bool gameplayScene;
        private bool trueDeathOpen;
        private bool externalSceneLoad;
        private bool loadIssued;
        private bool cancelling;
        private bool ownsService;
        private bool hasProcessedFrame;
        private int processedFrame;
        private float normalFixedDeltaTime;

        /// <summary>Core owner for UI subscriptions; the UI never writes Unity time or audio.</summary>
        public IPauseService Service => service;
        /// <summary>Whether preload, external loading or pause departure currently owns the run.</summary>
        public bool IsSceneLoadInProgress => externalSceneLoad || loadIssued
            || (service != null && service.IsLeaving)
            || (runDirector != null && runDirector.IsSceneLoadInProgress);
        /// <summary>Fresh primitive eligibility, independent of whether the menu is already open.</summary>
        public bool CanPause => EvaluateEligibility(gameplayScene, player != null && !player.IsDead,
            trueDeathOpen, RunEvents.IsCardOfferOpen, IsSceneLoadInProgress, runDirector != null && runDirector.IsTransitioning,
            run != null, phase);
        /// <summary>Raised after load/preflight failure has restored visible pause ownership.</summary>
        public event Action SceneLeaveFailed;

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerHealth>();
            if (runDirector == null) runDirector = FindAnyObjectByType<RunDirector>();
            normalFixedDeltaTime = HitstopManager.Instance != null
                ? HitstopManager.Instance.NormalFixedDeltaTime : Time.fixedDeltaTime;
            service = new PauseService(this);
            service.Changed += HandleServiceChanged;
            CacheScene(SceneManager.GetActiveScene());
        }

        private void OnEnable()
        {
            if (GamePause.Service != null && !ReferenceEquals(GamePause.Service, service))
            {
                enabled = false;
                return;
            }
            ownsService = true;
            GamePause.SetService(service);
            HandleRunChanged(RunEvents.CurrentRun);
            RunEvents.OnRunChanged += HandleRunChanged;
            RunEvents.OnPhaseChanged += HandlePhaseChanged;
            RunEvents.OnTrueDeath += HandleTrueDeath;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (player != null) player.OnDied += HandleEligibilityChanged;
            if (runDirector != null) runDirector.PauseEligibilityChanged += HandleEligibilityChanged;
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy()
        {
            Cleanup();
            if (service != null) service.Changed -= HandleServiceChanged;
        }

        private void Cleanup()
        {
            if (!ownsService) return;
            RunEvents.OnRunChanged -= HandleRunChanged;
            RunEvents.OnPhaseChanged -= HandlePhaseChanged;
            RunEvents.OnTrueDeath -= HandleTrueDeath;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (player != null) player.OnDied -= HandleEligibilityChanged;
            if (runDirector != null) runDirector.PauseEligibilityChanged -= HandleEligibilityChanged;
            bool exclusive = !CanPause;
            cancelling = true;
            try { menu?.CancelForExclusiveFlow(); }
            finally { cancelling = false; }
            if (service.IsLeaving)
            {
                // A disabled/destroyed player must never restore pause or hitstop into the destination.
                if (HitstopManager.Instance != null) HitstopManager.Instance.CancelForSceneLeave();
                service.CompleteSceneLeave(normalFixedDeltaTime);
                // Retain only the destination callback and Core departure gates until Single activation.
            }
            else
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                if (exclusive) service.CancelForExclusiveFlow();
                else service.Release();
                if (ReferenceEquals(GamePause.Service, service)) GamePause.SetService(null);
                // A card modal torn down after this runtime holds a service that wraps ours; drop it so the
                // next scene's OnEnable never sees a foreign owner (the modal's own Close tolerates this).
                else if (GamePause.Service != null && RunEvents.IsCardOfferOpen) GamePause.SetService(null);
            }
            ownsService = false;
        }

        /// <summary>Injects the player and the Core UI sink from the enclosing Player prefab.</summary>
        public void Bind(PlayerHealth health, IPauseMenuRequestSink requestSink)
        {
            if (ownsService && player != null) player.OnDied -= HandleEligibilityChanged;
            player = health;
            menu = requestSink;
            if (ownsService && player != null) player.OnDied += HandleEligibilityChanged;
            HandleEligibilityChanged();
        }

        /// <summary>Detaches only the matching UI, reverting any open pause first.</summary>
        public void Unbind(IPauseMenuRequestSink requestSink)
        {
            if (!ReferenceEquals(menu, requestSink)) return;
            CancelForExclusiveFlow();
            menu = null;
        }

        /// <summary>Maps gameplay-only run phases without introducing Gameplay dependencies into Core.</summary>
        public static bool EvaluateEligibility(bool isGameplayScene, bool playerAlive, bool deathCardOpen,
            bool cardOfferOpen, bool sceneLoadInProgress, bool transitioning, bool hasRun, RunPhase runPhase)
        {
            return isGameplayScene && playerAlive && !deathCardOpen && !cardOfferOpen && !sceneLoadInProgress
                && !transitioning && (!hasRun || runPhase == RunPhase.Tower || runPhase == RunPhase.InAraf);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;
            bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool startPressed = gamepad != null && gamepad.startButton.wasPressedThisFrame;
            bool backPressed = (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            bool toggleHeld = (keyboard != null && keyboard.escapeKey.isPressed)
                || (gamepad != null && gamepad.startButton.isPressed);
            bool actionHeld = (mouse != null && (mouse.leftButton.isPressed
                    || mouse.rightButton.isPressed || mouse.middleButton.isPressed))
                || (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed
                    || keyboard.qKey.isPressed || keyboard.leftShiftKey.isPressed
                    || keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed
                    || keyboard.rKey.isPressed || keyboard.digit1Key.isPressed
                    || keyboard.digit2Key.isPressed || keyboard.digit3Key.isPressed))
                || (gamepad != null && (gamepad.buttonSouth.isPressed || gamepad.buttonEast.isPressed
                    || gamepad.buttonWest.isPressed || gamepad.buttonNorth.isPressed
                    || gamepad.leftTrigger.isPressed || gamepad.rightTrigger.isPressed
                    || gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed));
            ProcessInput(Time.frameCount, escapePressed, startPressed, backPressed, toggleHeld,
                actionHeld, Time.unscaledDeltaTime);
        }

        /// <summary>Processes one injected device sample per frame; Start wins simultaneous Back requests.</summary>
        public void ProcessInput(int frameIndex, bool escapePressed, bool startPressed, bool backPressed,
            bool toggleHeld, bool gameplayActionHeld, float realUnscaledDeltaTime)
        {
            if (!ownsService || (hasProcessedFrame && processedFrame == frameIndex)) return;
            hasProcessedFrame = true;
            processedFrame = frameIndex;
            service.UpdateInput(frameIndex, toggleHeld, gameplayActionHeld);
            HandleEligibilityChanged();
            if (!service.IsLeaving)
            {
                if (service.IsPaused)
                {
                    if (startPressed)
                    {
                        if (menu != null) menu.Resume();
                        else Resume();
                    }
                    else if (escapePressed || backPressed)
                    {
                        if (menu != null) menu.Back();
                        else Resume();
                    }
                }
                else if (escapePressed || startPressed)
                {
                    if (TryPause()) menu?.Open();
                }
            }
            GamePause.AdvanceClock(realUnscaledDeltaTime);
        }

        /// <summary>Immediately acquires ownership after rechecking live phase, player, card and load state.</summary>
        public bool TryPause()
        {
            if (!ownsService) return false;
            return service.TryPause(gameplayScene, player != null && !player.IsDead, trueDeathOpen,
                IsSceneLoadInProgress, !RunEvents.IsCardOfferOpen && (runDirector == null || !runDirector.IsTransitioning)
                    && (run == null || phase == RunPhase.Tower || phase == RunPhase.InAraf));
        }

        /// <summary>Restores a still-live gameplay owner after the UI has saved/reverted settings.</summary>
        public void Resume()
        {
            if (!CanPause) CancelForExclusiveFlow();
            else service.Resume();
        }

        /// <summary>Synchronously hides/reverts pause before death, transition or external loading takes focus.</summary>
        public void CancelForExclusiveFlow()
        {
            if (cancelling || service == null || !service.IsPaused || service.IsLeaving) return;
            cancelling = true;
            try
            {
                menu?.CancelForExclusiveFlow();
                service.CancelForExclusiveFlow();
            }
            finally { cancelling = false; }
        }

        /// <summary>Receives TrueDeathView.IsOpen through bindings; an open card cancels pause immediately.</summary>
        public void SetTrueDeathOpen(bool isOpen)
        {
            trueDeathOpen = isOpen;
            HandleEligibilityChanged();
        }

        /// <summary>External loaders must publish ownership before starting a scene operation.</summary>
        public void SetSceneLoadInProgress(bool inProgress)
        {
            externalSceneLoad = inProgress;
            HandleEligibilityChanged();
        }

        /// <summary>Preflights MainMenu and latches the frozen leaving fade exactly once.</summary>
        public bool TryPrepareSceneLeave()
        {
            if (service == null || service.IsLeaving || !service.IsPaused) return false;
            if (!CanPause) { CancelForExclusiveFlow(); return false; }
            if (!Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneLeaveFailed?.Invoke();
                return false;
            }
            return service.PrepareSceneLeave();
        }

        /// <summary>Called once after the UI reaches black; keeps simulation blocked through Single loading.</summary>
        public void LoadMainMenu()
        {
            if (service == null || !service.IsLeaving || loadIssued) return;
            loadIssued = true;
            LoadMainMenuAsync();
        }

        private async void LoadMainMenuAsync()
        {
            try
            {
                if (runDirector != null) await runDirector.PrepareForSceneLeaveAsync();
                // Ownership can be torn down while the additive load drains.
                if (this == null || !ownsService) return;
                if (!Application.CanStreamedLevelBeLoaded("MainMenu"))
                {
                    RecoverSceneLeave();
                    return;
                }
                if (HitstopManager.Instance != null) HitstopManager.Instance.CancelForSceneLeave();
                service.CompleteSceneLeave(normalFixedDeltaTime);
                AsyncOperation operation = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
                if (operation == null) { RecoverSceneLeave(); return; }
                await operation;
            }
            catch (Exception)
            {
                if (this != null && ownsService) RecoverSceneLeave();
            }
        }

        /// <summary>Restores pause/input ownership on preflight or asynchronous load failure.</summary>
        public void RecoverSceneLeave()
        {
            if (service == null || !service.IsLeaving) return;
            loadIssued = false;
            if (runDirector != null) runDirector.RecoverSceneLeave();
            service.RecoverSceneLeave();
            SceneLeaveFailed?.Invoke();
        }

        /// <inheritdoc/>
        public PauseSnapshot Capture() => new PauseSnapshot(Time.timeScale, Time.fixedDeltaTime,
            AudioListener.pause, (int)Cursor.lockState, Cursor.visible);

        /// <inheritdoc/>
        public void Apply(PauseSnapshot snapshot)
        {
            Time.timeScale = snapshot.TimeScale;
            Time.fixedDeltaTime = snapshot.FixedDeltaTime;
            AudioListener.pause = snapshot.AudioPaused;
            Cursor.lockState = (CursorLockMode)snapshot.CursorLockMode;
            Cursor.visible = snapshot.CursorVisible;
        }

        private void HandleRunChanged(RunState state)
        {
            run = state;
            phase = state != null ? state.Phase : RunPhase.Tower;
            if (state != null) trueDeathOpen = phase == RunPhase.TrueDeath;
            HandleEligibilityChanged();
        }

        private void HandlePhaseChanged(RunPhase value)
        {
            phase = value;
            HandleEligibilityChanged();
        }

        private void HandleTrueDeath()
        {
            trueDeathOpen = true;
            CancelForExclusiveFlow();
        }

        private void HandleEligibilityChanged()
        {
            if (service != null && service.IsPaused && !service.IsLeaving && !CanPause)
                CancelForExclusiveFlow();
        }

        private void HandleServiceChanged()
        {
            // Core-only exclusive owners (including TrueDeathView) must hide pause synchronously.
            if (cancelling || service.IsPaused || service.IsLeaving) return;
            cancelling = true;
            try { menu?.CancelForExclusiveFlow(); }
            finally { cancelling = false; }
        }

        private void CacheScene(Scene scene) => gameplayScene = scene.IsValid() && scene.name != "MainMenu";
        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            CacheScene(next);
            HandleEligibilityChanged();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ownsService)
            {
                if (mode == LoadSceneMode.Single)
                {
                    // Old transition OnDisable callbacks may write their own defaults during unload.
                    // Reassert the saved destination baseline after that scene's final teardown.
                    if (ReferenceEquals(GamePause.Service, service))
                    {
                        Apply(new PauseSnapshot(1f, normalFixedDeltaTime, false, 0, true));
                        GamePause.SetService(null);
                    }
                    SceneManager.sceneLoaded -= HandleSceneLoaded;
                }
                return;
            }
            if (runDirector == null)
            {
                runDirector = FindAnyObjectByType<RunDirector>();
                if (runDirector != null) runDirector.PauseEligibilityChanged += HandleEligibilityChanged;
            }
            CacheScene(SceneManager.GetActiveScene());
            HandleEligibilityChanged();
        }
    }
}
