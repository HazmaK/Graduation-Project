namespace RetroFPS.UI.HUD
{
    /// <summary>Latches only the true-death event and yields one restart request after an unscaled 80 ms lock.</summary>
    public sealed class TrueDeathGate
    {
        private double requestAt;
        private double armAt;
        private int releasedInputs;
        private bool fadeFinished;
        /// <summary>Gets the card's latched visibility; ordinary death and Araf never set it.</summary>
        public bool Visible { get; private set; }
        /// <summary>Gets whether confirmation was accepted and the button must be disabled.</summary>
        public bool Confirmed { get; private set; }
        /// <summary>Gets whether the caller already consumed the sole request.</summary>
        public bool RequestConsumed { get; private set; }
        /// <summary>Handles OnTrueDeath only; returns true exactly once for this card instance.</summary>
        public bool OnTrueDeath(double unscaledSeconds = 0d, double armSeconds = 0.6d)
        {
            if (Visible) return false;
            Visible = true;
            armAt = unscaledSeconds + System.Math.Max(0d, armSeconds);
            releasedInputs = 0;
            fadeFinished = false;
            return true;
        }
        /// <summary>Records each confirm control observed up after opening; absent devices count as released.</summary>
        public void ObserveInputs(bool enterHeld, bool spaceHeld, bool southHeld, bool mouseHeld, bool fadeComplete)
        {
            if (!Visible || Confirmed) return;
            if (!enterHeld) releasedInputs |= 1;
            if (!spaceHeld) releasedInputs |= 2;
            if (!southHeld) releasedInputs |= 4;
            if (!mouseHeld) releasedInputs |= 8;
            fadeFinished = fadeComplete;
        }
        /// <summary>Requires the completed fade, minimum unscaled reading time and all four release observations.</summary>
        public bool CanConfirm(double unscaledSeconds) => Visible && !Confirmed && fadeFinished && releasedInputs == 15 &&
            !double.IsNaN(unscaledSeconds) && !double.IsInfinity(unscaledSeconds) && unscaledSeconds >= armAt;
        /// <summary>Accepts one submit at an absolute unscaled timestamp in seconds.</summary>
        public bool TryConfirm(double unscaledSeconds)
        {
            if (!CanConfirm(unscaledSeconds)) return false;
            Confirmed = true;
            requestAt = unscaledSeconds + 0.08d;
            return true;
        }
        /// <summary>Returns true once at or after the lock expires; the caller sends the request event.</summary>
        public bool TryConsumeRequest(double unscaledSeconds)
        {
            if (!Confirmed || RequestConsumed || double.IsNaN(unscaledSeconds) || unscaledSeconds < requestAt) return false;
            RequestConsumed = true;
            return true;
        }
    }
}
