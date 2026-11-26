using System;

namespace RetroFPS.Run.Araf
{
    /// <summary>Bounded awakening counter for an Araf visit.</summary>
    public sealed class ArafAwakening
    {
        /// <summary>Creates a counter at zero, clamping a negative maximum to zero.</summary>
        public ArafAwakening(int maxLevel) => MaxLevel = Math.Max(0, maxLevel);

        /// <summary>Current awakening level.</summary>
        public int Level { get; private set; }
        /// <summary>Maximum supported awakening level.</summary>
        public int MaxLevel { get; }

        /// <summary>Raises awakening by one up to its maximum; returns whether the level changed.</summary>
        public bool Raise()
        {
            if (Level >= MaxLevel) return false;
            Level++;
            return true;
        }
    }
}
