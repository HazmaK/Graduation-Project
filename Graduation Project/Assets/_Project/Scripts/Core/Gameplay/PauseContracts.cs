using System;

namespace RetroFPS.Pause
{
    /// <summary>Value-only environment state; cursor mode uses the adapter's integer representation.</summary>
    public readonly struct PauseSnapshot
    {
        /// <summary>Exact simulation scale, including zero during hitstop.</summary>
        public float TimeScale { get; }
        /// <summary>Exact authored or instantaneous physics step.</summary>
        public float FixedDeltaTime { get; }
        /// <summary>Whether gameplay audio was already paused.</summary>
        public bool AudioPaused { get; }
        /// <summary>Cursor lock mode: 0 free, 1 locked, 2 confined.</summary>
        public int CursorLockMode { get; }
        /// <summary>Whether the cursor is visible.</summary>
        public bool CursorVisible { get; }

        /// <summary>Captures plain values without normalizing restoration targets.</summary>
        public PauseSnapshot(float timeScale, float fixedDeltaTime, bool audioPaused,
            int cursorLockMode, bool cursorVisible)
        {
            TimeScale = timeScale;
            FixedDeltaTime = fixedDeltaTime;
            AudioPaused = audioPaused;
            CursorLockMode = cursorLockMode;
            CursorVisible = cursorVisible;
        }
    }

    /// <summary>Engine boundary implemented only by the gameplay lifecycle adapter.</summary>
    public interface IPauseEnvironment
    {
        /// <summary>Reads the current time, audio and cursor values.</summary>
        PauseSnapshot Capture();
        /// <summary>Applies all values exactly, without changing settings or audio volume.</summary>
        void Apply(PauseSnapshot snapshot);
    }

    /// <summary>Core pause ownership contract, independent of scenes, run phases and UI types.</summary>
    public interface IPauseService
    {
        /// <summary>Whether the pause menu owns the environment, including its leaving fade.</summary>
        bool IsPaused { get; }
        /// <summary>Blocks simulation during pause and throughout scene departure.</summary>
        bool SimulationBlocked { get; }
        /// <summary>Also blocks the resume frame and held gameplay actions until release.</summary>
        bool GameplayInputBlocked { get; }
        /// <summary>Raised synchronously after ownership or observable blocking gates change.</summary>
        event Action Changed;
        /// <summary>
        /// Attempts immediate acquisition using freshly sampled eligibility. No run is mapped to
        /// phaseAllowsPause=true; MainMenu must always map to gameplayScene=false.
        /// Denied requests are discarded and require toggle release before a fresh attempt.
        /// </summary>
        bool TryPause(bool gameplayScene, bool playerAlive, bool trueDeathOpen,
            bool sceneLoadInProgress, bool phaseAllowsPause);
        /// <summary>Restores the snapshot for a still-live owner; the adapter rechecks eligibility first.</summary>
        void Resume();
        /// <summary>Releases time/audio to an exclusive owner, preserving its current cursor state.</summary>
        void CancelForExclusiveFlow();
        /// <summary>Latches departure once while keeping pause ownership through the fade.</summary>
        bool PrepareSceneLeave();
    }

    /// <summary>Injected UI request boundary; the early input adapter coalesces commands per frame.</summary>
    public interface IPauseMenuRequestSink
    {
        /// <summary>Shows the root only after the adapter successfully acquires pause.</summary>
        void Open();
        /// <summary>Routes Esc, B or right-click one level back in an open menu.</summary>
        void Back();
        /// <summary>Resolves pending preview/settings and resumes from any non-leaving menu state.</summary>
        void Resume();
        /// <summary>Cancels pending UI work, reverts previews and hides for an exclusive owner.</summary>
        void CancelForExclusiveFlow();
    }
}
