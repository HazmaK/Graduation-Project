using System;

namespace RetroFPS.UI.Menu
{
    /// <summary>Caller-driven display confirmation timer; expiry means revert.</summary>
    public sealed class RevertCountdown
    {
        private double remaining;
        /// <summary>Gets remaining seconds, suitable for a progress indicator.</summary>
        public float Remaining => (float)remaining;
        /// <summary>Gets the index into cached 0..10 countdown strings.</summary>
        public int WholeSecondsRemaining => (int)Math.Ceiling(remaining);
        /// <summary>Gets whether the countdown is running.</summary>
        public bool Running { get; private set; }
        /// <summary>Gets whether the deadline elapsed without confirmation.</summary>
        public bool Expired { get; private set; }
        /// <summary>Gets whether the displayed time needs danger styling.</summary>
        public bool Danger => Running && remaining <= 3d;
        /// <summary>Starts a new confirmation window, defaulting to ten seconds.</summary>
        public void Start(float seconds = 10f)
        {
            remaining = Math.Max(0f, seconds);
            Expired = remaining <= 0d;
            Running = !Expired;
        }
        /// <summary>Advances the deadline without reading the engine clock.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!Running) return;
            remaining = Math.Max(0d, remaining - Math.Max(0f, deltaSeconds));
            if (remaining > 0d) return;
            Running = false;
            Expired = true;
        }
        /// <summary>Stops the deadline after keeping or explicitly reverting the display.</summary>
        public void Stop() { Running = false; Expired = false; }
    }
}
