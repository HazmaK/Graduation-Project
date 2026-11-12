using System;

namespace RetroFPS.UI.Menu
{
    /// <summary>Turns consecutive navigation repeats into accelerated five-percent steps.</summary>
    public sealed class HoldAccelerator
    {
        private readonly float resetGapSeconds;
        private readonly float accelerateAfterSeconds;
        private readonly int maximumMultiplier;
        private float heldSeconds;
        private int direction;
        /// <summary>Creates a hold policy; a 400 ms first-repeat delay remains consecutive.</summary>
        public HoldAccelerator(float resetGapSeconds = 0.5f, float accelerateAfterSeconds = 0.6f, int maximumMultiplier = 4)
        {
            this.resetGapSeconds = Math.Max(0f, resetGapSeconds);
            this.accelerateAfterSeconds = Math.Max(0.001f, accelerateAfterSeconds);
            this.maximumMultiplier = Math.Max(1, maximumMultiplier);
        }
        /// <summary>Records a repeat, passing seconds since the previous repeat and its direction.</summary>
        public int Repeat(float secondsSincePrevious, int repeatDirection = 1)
        {
            if (repeatDirection == 0) { Reset(); return 0; }
            int sign = repeatDirection > 0 ? 1 : -1;
            if (direction != sign || secondsSincePrevious > resetGapSeconds) heldSeconds = 0f;
            else heldSeconds += Math.Max(0f, secondsSincePrevious);
            direction = sign;
            return 1 + (int)Math.Min(maximumMultiplier - 1, heldSeconds / accelerateAfterSeconds);
        }
        /// <summary>Clears acceleration on release or focus change.</summary>
        public void Reset() { heldSeconds = 0f; direction = 0; }
    }
}
