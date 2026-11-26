using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Pure altitude decisions that retain a stable floor when crossing voids.</summary>
    public static class FlyingEyeAltitude
    {
        /// <summary>Accepts nearby ground immediately and deep ground after sustained hits; missing ground resets the timer.</summary>
        public static float ResolveGround(float lastSolidY, bool hasHit, float hitY, float voidDropThreshold,
            float acceptLowerAfter, float dt, ref float lowerTimer)
        {
            if (!hasHit)
            {
                lowerTimer = 0f;
                return lastSolidY;
            }
            if (lastSolidY - hitY > Mathf.Max(0f, voidDropThreshold))
            {
                lowerTimer = Mathf.Max(0f, lowerTimer) + Mathf.Max(0f, dt);
                if (lowerTimer < Mathf.Max(0f, acceptLowerAfter)) return lastSolidY;
            }
            lowerTimer = 0f;
            return hitY;
        }
    }
}
