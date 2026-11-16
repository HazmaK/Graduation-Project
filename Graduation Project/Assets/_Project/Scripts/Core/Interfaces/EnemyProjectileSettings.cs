using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Allocation-free launch settings shared by enemy shooters and the orb pool.</summary>
    public struct EnemyProjectileSettings
    {
        /// <summary>Travel speed in metres per second.</summary>
        public float Speed;
        /// <summary>Damage before reflection multipliers.</summary>
        public float Damage;
        /// <summary>Swept collision radius in metres.</summary>
        public float CollisionRadius;
        /// <summary>Uniform visual scale before pulsing or reflection.</summary>
        public float VisualScale;
        /// <summary>Maximum flight duration in seconds.</summary>
        public float Lifetime;
        /// <summary>Whether this is a heavy attack.</summary>
        public bool Heavy;
        /// <summary>Reflected turn rate in degrees per second; zero disables homing.</summary>
        public float ReflectHomingDegPerSec;
        /// <summary>Maximum angle to the original shooter for reflected homing.</summary>
        public float ReflectHomingMaxAngle;
        /// <summary>Orb tint before reflection.</summary>
        public Color Color;
    }
}
