using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Immutable difficulty snapshot. Every value composes with existing systems
    /// (elite scaling, Araf hunt profiles, dynamic difficulty) instead of replacing them.
    /// </summary>
    public readonly struct DifficultyModifiers
    {
        private readonly float playerDamageTaken;
        private readonly float enemyHealth;
        private readonly float enemySpeed;
        private readonly float enemyAttackCooldown;
        private readonly int meleeTokenOffset;
        private readonly int slamTokenOffset;
        private readonly int rangedHeavyTokenOffset;
        private readonly float waveSpawnInterval;
        private readonly bool initialized;

        /// <summary>Identity values: Normal reproduces the untuned game exactly.</summary>
        public static DifficultyModifiers Identity => new DifficultyModifiers(1f, 1f, 1f, 1f, 0, 0, 0, 1f);

        /// <summary>Creates a snapshot; multipliers are clamped to be non-negative and finite.</summary>
        public DifficultyModifiers(float playerDamageTaken, float enemyHealth, float enemySpeed,
            float enemyAttackCooldown, int meleeTokenOffset, int slamTokenOffset,
            int rangedHeavyTokenOffset, float waveSpawnInterval)
        {
            this.playerDamageTaken = Sanitize(playerDamageTaken);
            this.enemyHealth = Sanitize(enemyHealth);
            this.enemySpeed = Sanitize(enemySpeed);
            this.enemyAttackCooldown = Sanitize(enemyAttackCooldown);
            this.meleeTokenOffset = meleeTokenOffset;
            this.slamTokenOffset = slamTokenOffset;
            this.rangedHeavyTokenOffset = rangedHeavyTokenOffset;
            this.waveSpawnInterval = Sanitize(waveSpawnInterval);
            initialized = true;
        }

        // default(DifficultyModifiers) must behave as Normal, never as "everything x0".
        /// <summary>Multiplier on all damage the player receives, after perk stats.</summary>
        public float PlayerDamageTaken => initialized ? playerDamageTaken : 1f;
        /// <summary>Multiplier on enemy max health, composed with elite scaling.</summary>
        public float EnemyHealth => initialized ? enemyHealth : 1f;
        /// <summary>Multiplier on enemy movement speed, composed with elite and hunt multipliers.</summary>
        public float EnemySpeed => initialized ? enemySpeed : 1f;
        /// <summary>Multiplier on enemy attack cooldowns, composed with the aggression profile's.</summary>
        public float EnemyAttackCooldown => initialized ? enemyAttackCooldown : 1f;
        /// <summary>Added to the melee token capacity (channel default or profile limit).</summary>
        public int MeleeTokenOffset => meleeTokenOffset;
        /// <summary>Added to the slam token capacity.</summary>
        public int SlamTokenOffset => slamTokenOffset;
        /// <summary>Added to the heavy ranged token capacity.</summary>
        public int RangedHeavyTokenOffset => rangedHeavyTokenOffset;
        /// <summary>Multiplier on wave spawn intervals, composed with the dynamic difficulty multiplier.</summary>
        public float WaveSpawnInterval => initialized ? waveSpawnInterval : 1f;

        /// <summary>Returns the token offset for a channel.</summary>
        public int TokenOffset(AttackTokenChannel channel) => channel == AttackTokenChannel.Melee ? meleeTokenOffset
            : channel == AttackTokenChannel.Slam ? slamTokenOffset : rangedHeavyTokenOffset;

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0f, value);
    }
}
