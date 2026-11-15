using System;

namespace RetroFPS.Pause
{
    /// <summary>Engine-free, single-owner pause logic. All ticking and snapshot operations allocate no heap memory.</summary>
    public sealed class PauseService : IPauseService
    {
        private readonly IPauseEnvironment environment;
        private PauseSnapshot restoration;
        private bool paused;
        private bool leaving;
        private bool departureApplied;
        private bool requireActionRelease;
        private bool requireToggleRelease;
        private bool hasConsumedFrame;
        private int frame;
        private int consumedFrame;

        /// <summary>Creates an unpaused service; construction does not read or write the environment.</summary>
        public PauseService(IPauseEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc/>
        public bool IsPaused => paused;
        /// <inheritdoc/>
        public bool SimulationBlocked => paused || leaving;
        /// <inheritdoc/>
        public bool GameplayInputBlocked => SimulationBlocked || requireActionRelease
            || (hasConsumedFrame && consumedFrame == frame);
        /// <summary>Whether departure is latched, including after destination cleanup.</summary>
        public bool IsLeaving => leaving;
        /// <inheritdoc/>
        public event Action Changed;

        /// <summary>
        /// Called early every frame, even while ineligible. toggleHeld aggregates Esc/Start;
        /// gameplayActionHeld aggregates fire, alt-fire, parry, dash, jump, grapple and relock/submit
        /// buttons. A release on the consumed frame never unblocks that frame. This method does
        /// not issue commands; the adapter supplies press edges and discards ineligible requests.
        /// </summary>
        public void UpdateInput(int frameIndex, bool toggleHeld, bool gameplayActionHeld)
        {
            bool wasBlocked = GameplayInputBlocked;
            frame = frameIndex;
            if (!toggleHeld) requireToggleRelease = false;
            if (!SimulationBlocked && (!hasConsumedFrame || consumedFrame != frame)
                && !gameplayActionHeld) requireActionRelease = false;
            if (wasBlocked != GameplayInputBlocked) Changed?.Invoke();
        }

        /// <inheritdoc/>
        public bool TryPause(bool gameplayScene, bool playerAlive, bool trueDeathOpen,
            bool sceneLoadInProgress, bool phaseAllowsPause)
        {
            if (paused || leaving || requireToggleRelease
                || (hasConsumedFrame && consumedFrame == frame)) return false;
            requireToggleRelease = true;
            if (!gameplayScene || !playerAlive || trueDeathOpen || sceneLoadInProgress || !phaseAllowsPause)
                return false;

            restoration = environment.Capture();
            paused = true;
            ConsumeInput();
            ApplyPaused();
            Changed?.Invoke();
            // A synchronous death/transition subscriber may have won ownership during notification.
            return paused && !leaving;
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (!paused || leaving) return;
            paused = false;
            ConsumeInput();
            // A normal live gameplay snapshot is locked/hidden; preserve unusual test/owner states too.
            environment.Apply(restoration);
            Changed?.Invoke();
        }

        /// <inheritdoc/>
        public void CancelForExclusiveFlow()
        {
            if (!paused || leaving) return;
            PauseSnapshot current = environment.Capture();
            paused = false;
            ConsumeInput();
            environment.Apply(new PauseSnapshot(restoration.TimeScale, restoration.FixedDeltaTime,
                restoration.AudioPaused, current.CursorLockMode, current.CursorVisible));
            // The exclusive adapter owns dead-player controls; this service never enables components.
            Changed?.Invoke();
        }

        /// <summary>Idempotent ordinary teardown; scene-leaving teardown must retain destination cleanup and gates.</summary>
        public void Release()
        {
            Resume();
        }

        /// <summary>Revises saved hitstop restoration without writing the currently paused environment.</summary>
        public void SetRestorationTime(float timeScale, float fixedDeltaTime)
        {
            if (!paused || departureApplied) return;
            restoration = new PauseSnapshot(timeScale, fixedDeltaTime, restoration.AudioPaused,
                restoration.CursorLockMode, restoration.CursorVisible);
        }

        /// <inheritdoc/>
        public bool PrepareSceneLeave()
        {
            if (!paused || leaving) return false;
            leaving = true;
            ConsumeInput();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// After black and hitstop cancellation, applies destination cleanup once. The adapter supplies
        /// the positive normal physics baseline captured before hitstop; both gates remain latched
        /// until successful unload replaces/unbinds the service or RecoverSceneLeave is called.
        /// Recovery subsequently resumes at this normal baseline, never at a cancelled hitstop scale.
        /// </summary>
        public void CompleteSceneLeave(float normalFixedDeltaTime)
        {
            if (!leaving || departureApplied) return;
            if (normalFixedDeltaTime <= 0f || float.IsNaN(normalFixedDeltaTime)
                || float.IsInfinity(normalFixedDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(normalFixedDeltaTime));
            departureApplied = true;
            paused = false;
            restoration = new PauseSnapshot(1f, normalFixedDeltaTime, restoration.AudioPaused,
                restoration.CursorLockMode, restoration.CursorVisible);
            environment.Apply(new PauseSnapshot(1f, normalFixedDeltaTime, false, 0, true));
            Changed?.Invoke();
        }

        /// <summary>Recovers a preflight/load failure to visible pause ownership without recapturing cleaned-up values.</summary>
        public void RecoverSceneLeave()
        {
            if (!leaving) return;
            leaving = false;
            departureApplied = false;
            paused = true;
            ConsumeInput();
            ApplyPaused();
            Changed?.Invoke();
        }

        private void ApplyPaused()
        {
            environment.Apply(new PauseSnapshot(0f, restoration.FixedDeltaTime, true, 0, true));
        }

        private void ConsumeInput()
        {
            consumedFrame = frame;
            hasConsumedFrame = true;
            requireActionRelease = true;
            requireToggleRelease = true;
        }
    }
}
