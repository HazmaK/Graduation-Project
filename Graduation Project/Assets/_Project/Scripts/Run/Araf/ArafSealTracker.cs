using System;

namespace RetroFPS.Run.Araf
{
    /// <summary>Tracks collected seals in collection order.</summary>
    public sealed class ArafSealTracker
    {
        /// <summary>Creates an empty tracker, clamping negative totals to zero.</summary>
        public ArafSealTracker(int total) => Total = Math.Max(0, total);

        /// <summary>Total seals required for completion.</summary>
        public int Total { get; }
        /// <summary>Number of seals collected so far.</summary>
        public int Collected { get; private set; }
        /// <summary>Whether every required seal has been collected.</summary>
        public bool IsComplete => Collected >= Total;

        /// <summary>Collects the next seal and returns its zero-based order; returns false when complete.</summary>
        public bool TryCollect(out int sealIndex)
        {
            sealIndex = Collected;
            if (IsComplete) return false;
            Collected++;
            return true;
        }
    }
}
