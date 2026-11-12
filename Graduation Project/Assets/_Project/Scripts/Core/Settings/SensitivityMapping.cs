using System;

namespace RetroFPS.Settings
{
    /// <summary>Converts the slider to a positive look multiplier with an exact neutral midpoint.</summary>
    public static class SensitivityMapping
    {
        /// <summary>Maps 0 to min, 0.5 to 1 and 1 to max, exponentially on each half.</summary>
        public static float ToScale(float normalized, float minScale = 0.25f, float maxScale = 4f)
        {
            if (float.IsNaN(minScale) || float.IsInfinity(minScale) || minScale <= 0f || minScale > 1f)
                throw new ArgumentOutOfRangeException(nameof(minScale));
            if (float.IsNaN(maxScale) || float.IsInfinity(maxScale) || maxScale < 1f)
                throw new ArgumentOutOfRangeException(nameof(maxScale));
            float value = GameSettingsData.Unit(normalized, 0.5f);
            return value <= 0.5f
                ? (float)Math.Pow(minScale, 1f - 2f * value)
                : (float)Math.Pow(maxScale, 2f * value - 1f);
        }
    }
}
