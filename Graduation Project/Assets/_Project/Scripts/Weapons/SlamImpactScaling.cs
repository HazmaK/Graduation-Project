using System;

namespace RetroFPS.Weapons
{
    /// <summary>Pure landing-height mapping; no scene, clock or Unity object dependencies.</summary>
    public readonly struct SlamImpactScaling
    {
        /// <summary>Nonnegative vertical fall credited to this impact, after any height limit.</summary>
        public float Height { get; }
        /// <summary>Clamped blend between the light and heavy impacts.</summary>
        public float T { get; }
        /// <summary>Base damage before player statistics.</summary>
        public float Damage { get; }
        /// <summary>World-space impact radius.</summary>
        public float Radius { get; }
        /// <summary>Seconds of stun on an unguarded target.</summary>
        public float StunDuration { get; }

        /// <summary>Maps activation and landing heights through configurable impact endpoints and an optional height credit limit.</summary>
        public SlamImpactScaling(float startY, float landY, float minHeight = 1f, float maxHeight = 6f,
            float minDamage = 45f, float maxDamage = 120f, float minRadius = 3f, float maxRadius = 5.5f,
            float minStun = 0.5f, float maxStun = 1.8f, float heightLimit = float.PositiveInfinity)
        {
            Height = Math.Min(Math.Max(0f, startY - landY), Math.Max(0f, heightLimit));
            T = maxHeight <= minHeight ? (Height > minHeight ? 1f : 0f)
                : Math.Max(0f, Math.Min(1f, (Height - minHeight) / (maxHeight - minHeight)));
            Damage = minDamage + (maxDamage - minDamage) * T;
            Radius = minRadius + (maxRadius - minRadius) * T;
            StunDuration = minStun + (maxStun - minStun) * T;
        }
    }
}
