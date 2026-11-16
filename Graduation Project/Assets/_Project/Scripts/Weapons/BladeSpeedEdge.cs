using System;

namespace RetroFPS.Weapons
{
    /// <summary>Pure horizontal momentum damage bonus for primary blade swings.</summary>
    public static class BladeSpeedEdge
    {
        /// <summary>Returns one below the minimum speed, then linearly reaches the capped bonus.</summary>
        public static float Multiplier(float horizontalSpeed, float minSpeed = 8f,
            float maxSpeed = 13f, float maxBonus = 0.35f)
        {
            if (horizontalSpeed <= minSpeed) return 1f;
            float bonus = Math.Max(0f, maxBonus);
            if (horizontalSpeed >= maxSpeed || maxSpeed <= minSpeed) return 1f + bonus;
            return 1f + (horizontalSpeed - minSpeed) / (maxSpeed - minSpeed) * bonus;
        }
    }
}
