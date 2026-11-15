using System;
using RetroFPS.Pause;

namespace RetroFPS.UI.HUD
{
    /// <summary>Exclusive card input gate layered over the existing pause owner.</summary>
    public sealed class CardOfferPauseService : IPauseService
    {
        private readonly IPauseService previous;
        /// <summary>Captures the facade owner without changing its internal input latches.</summary>
        public CardOfferPauseService(IPauseService previous) { this.previous = previous; }
        /// <inheritdoc/>
        public bool IsPaused => false;
        /// <inheritdoc/>
        public bool SimulationBlocked => true;
        /// <inheritdoc/>
        public bool GameplayInputBlocked => true;
        /// <inheritdoc/>
        public event Action Changed { add { if (previous != null) previous.Changed += value; } remove { if (previous != null) previous.Changed -= value; } }
        /// <inheritdoc/>
        public bool TryPause(bool gameplayScene, bool playerAlive, bool trueDeathOpen, bool sceneLoadInProgress, bool phaseAllowsPause) => false;
        /// <inheritdoc/>
        public void Resume() { }
        /// <inheritdoc/>
        public void CancelForExclusiveFlow() => previous?.CancelForExclusiveFlow();
        /// <inheritdoc/>
        public bool PrepareSceneLeave() => false;
    }
}
