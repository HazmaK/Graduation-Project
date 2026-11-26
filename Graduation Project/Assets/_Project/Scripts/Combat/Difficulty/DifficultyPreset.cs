using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Tuning for one difficulty level. The Normal asset must keep identity values.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Combat/Difficulty Preset", fileName = "DifficultyPreset")]
    public sealed class DifficultyPreset : ScriptableObject
    {
        [Header("Oyuncu")]
        [SerializeField, Tooltip("Multiplier on all damage the player receives (after perks).")]
        private float playerDamageTakenMultiplier = 1f;

        [Header("Düşman")]
        [SerializeField, Tooltip("Multiplier on enemy max health; composes with elite scaling.")]
        private float enemyHealthMultiplier = 1f;
        [SerializeField, Tooltip("Multiplier on enemy movement speed; composes with elite and hunt multipliers.")]
        private float enemySpeedMultiplier = 1f;
        [SerializeField, Tooltip("Multiplier on enemy attack cooldowns; composes with the aggression profile.")]
        private float enemyAttackCooldownMultiplier = 1f;

        [Header("Saldırı Jetonları (ek kapasite)")]
        [SerializeField, Tooltip("Added to the melee capacity (default 2, or the Araf profile limit).")]
        private int meleeTokenOffset;
        [SerializeField, Tooltip("Added to the slam capacity (default 1).")]
        private int slamTokenOffset;
        [SerializeField, Tooltip("Added to the heavy ranged capacity (default 2).")]
        private int rangedHeavyTokenOffset;

        [Header("Dalga Temposu")]
        [SerializeField, Tooltip("Multiplier on wave spawn intervals; composes with dynamic difficulty.")]
        private float waveSpawnIntervalMultiplier = 1f;

        /// <summary>Immutable snapshot used by gameplay code.</summary>
        public DifficultyModifiers Modifiers => new DifficultyModifiers(playerDamageTakenMultiplier,
            enemyHealthMultiplier, enemySpeedMultiplier, enemyAttackCooldownMultiplier,
            meleeTokenOffset, slamTokenOffset, rangedHeavyTokenOffset, waveSpawnIntervalMultiplier);

        private void OnValidate()
        {
            playerDamageTakenMultiplier = Mathf.Max(0f, playerDamageTakenMultiplier);
            enemyHealthMultiplier = Mathf.Max(0.05f, enemyHealthMultiplier);
            enemySpeedMultiplier = Mathf.Max(0f, enemySpeedMultiplier);
            enemyAttackCooldownMultiplier = Mathf.Max(0f, enemyAttackCooldownMultiplier);
            waveSpawnIntervalMultiplier = Mathf.Max(0.05f, waveSpawnIntervalMultiplier);
        }
    }
}
