using System;

namespace RetroFPS.UI.HUD
{
    /// <summary>Caches recovered cooldown fill and binary state without allocations.</summary>
    public sealed class CooldownDisplayCache
    {
        private readonly int steps;
        private bool initialized;
        /// <summary>Creates a cache with a fixed positive bucket count (20 by default).</summary>
        public CooldownDisplayCache(int fillSteps = 20) => steps = Math.Max(1, fillSteps);
        /// <summary>Gets the recovered bucket, from zero through Steps inclusive.</summary>
        public int Bucket { get; private set; }
        /// <summary>Gets the fixed bucket count.</summary>
        public int Steps => steps;
        /// <summary>Gets the quantized recovered fraction.</summary>
        public float Fill => (float)Bucket / steps;
        /// <summary>Gets whether recovery is complete.</summary>
        public bool Ready { get; private set; }
        /// <summary>Gets whether the ability is currently active.</summary>
        public bool Active { get; private set; }
        /// <summary>Gets whether the reserved cell should render its graphics.</summary>
        public bool Visible { get; private set; }

        /// <summary>Accepts recovered fraction (invert remaining fractions at the caller); returns visual dirtiness.</summary>
        public bool Update(float recoveredNormalized, bool active = false, bool visible = true)
        {
            float value = float.IsNaN(recoveredNormalized) ? 0f : Math.Max(0f, Math.Min(1f, recoveredNormalized));
            // Absorb float representation noise at exact bucket boundaries (for example 0.7 * 20).
            int bucket = value >= 1f ? steps : Math.Min(steps - 1, (int)(value * steps + 0.000001d));
            bool ready = value >= 1f;
            bool changed = !initialized || bucket != Bucket || ready != Ready || active != Active || visible != Visible;
            Bucket = bucket;
            Ready = ready;
            Active = active;
            Visible = visible;
            initialized = true;
            return changed;
        }
    }
}
