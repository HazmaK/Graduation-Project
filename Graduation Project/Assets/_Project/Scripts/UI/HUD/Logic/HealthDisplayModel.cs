using System;

namespace RetroFPS.UI.HUD
{
    /// <summary>Decomposes health into numeric pieces and an independently dirty base fill.</summary>
    public sealed class HealthDisplayModel
    {
        private bool initialized;
        /// <summary>Gets nonnegative current health rounded to the nearest integer, ties upward.</summary>
        public int Current { get; private set; }
        /// <summary>Gets the rounded nonnegative base maximum.</summary>
        public int Maximum { get; private set; }
        /// <summary>Gets current health inside the base maximum.</summary>
        public int Base { get; private set; }
        /// <summary>Gets the remaining numeric health; no source-independent overheal cap is assumed.</summary>
        public int Overflow { get; private set; }
        /// <summary>Gets the clamped base fill, or zero for a nonpositive maximum.</summary>
        public float BaseFill { get; private set; }
        /// <summary>Gets whether the last update changed any numeric piece.</summary>
        public bool NumbersChanged { get; private set; }
        /// <summary>Gets whether the last update changed the continuous fill.</summary>
        public bool FillChanged { get; private set; }

        /// <summary>Updates without formatting or allocation; returns whether any displayed value changed.</summary>
        public bool Update(float current, float maximum)
        {
            current = Nonnegative(current);
            maximum = Nonnegative(maximum);
            int nextCurrent = Round(current);
            int nextMaximum = Round(maximum);
            float nextFill = maximum > 0f ? Math.Min(1f, current / maximum) : 0f;
            NumbersChanged = !initialized || nextCurrent != Current || nextMaximum != Maximum;
            FillChanged = !initialized || nextFill != BaseFill;
            Current = nextCurrent;
            Maximum = nextMaximum;
            Base = Math.Min(Current, Maximum);
            Overflow = Current - Base;
            BaseFill = nextFill;
            initialized = true;
            return NumbersChanged || FillChanged;
        }

        private static float Nonnegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(float.MaxValue, value));
        private static int Round(float value) => value >= int.MaxValue ? int.MaxValue : (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
