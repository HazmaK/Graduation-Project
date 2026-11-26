using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Optional arena aggression overrides; enemies retain their prefab defaults without a profile.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Combat/Enemy Aggression Profile", fileName = "AggressionProfile")]
    public sealed class EnemyAggressionProfile : ScriptableObject
    {
        [SerializeField, Tooltip("Grace period before movement and attacks.")]
        private float huntStartDelay = 0.35f;
        [SerializeField, Tooltip("Concurrent melee attacks; zero uses the channel default.")]
        private int meleeTokenLimit = 3;
        [SerializeField, Tooltip("Concurrent slams; zero uses the channel default.")]
        private int slamTokenLimit = 1;
        [SerializeField, Tooltip("Concurrent heavy ranged attacks; zero uses the channel default.")]
        private int rangedHeavyTokenLimit = 2;
        [SerializeField, Tooltip("Release melee ownership when recovery begins.")]
        private bool releaseMeleeTokenOnRecover = true;
        [SerializeField, Tooltip("Minimum surround radius in metres.")]
        private float surroundRadiusMin = 2.8f;
        [SerializeField, Tooltip("Maximum surround radius in metres.")]
        private float surroundRadiusMax = 3.5f;
        [SerializeField, Tooltip("Feint rate while waiting; zero disables feints.")]
        private float feintChancePerSecond = 0.35f;
        [SerializeField, Tooltip("Feint duration in seconds.")]
        private float feintDuration = 0.22f;
        [SerializeField, Tooltip("Feint movement speed in metres per second.")]
        private float feintSpeed = 5.5f;
        [SerializeField, Tooltip("Multiplier applied to hunt movement speed.")]
        private float moveSpeedMultiplier = 1.15f;
        [SerializeField, Tooltip("Multiplier applied to hunt turn speed.")]
        private float turnSpeedMultiplier = 1.45f;
        [SerializeField, Tooltip("Multiplier applied to attack cooldowns.")]
        private float attackCooldownMultiplier = 0.8f;
        [SerializeField, Tooltip("Desired eye height above ground in metres.")]
        private float eyeHoverHeight = 3.0f;
        [SerializeField, Tooltip("Minimum eye height above ground in metres.")]
        private float eyeMinFlyHeight = 2.0f;
        [SerializeField, Tooltip("Maximum eye height above ground in metres.")]
        private float eyeMaxFlyHeight = 4.0f;

        /// <summary>Grace period before movement and attacks.</summary>
        public float HuntStartDelay => huntStartDelay;

        /// <summary>Concurrent melee attacks; zero uses the channel default.</summary>
        public int MeleeTokenLimit => meleeTokenLimit;

        /// <summary>Concurrent slams; zero uses the channel default.</summary>
        public int SlamTokenLimit => slamTokenLimit;

        /// <summary>Concurrent heavy ranged attacks; zero uses the channel default.</summary>
        public int RangedHeavyTokenLimit => rangedHeavyTokenLimit;

        /// <summary>Release melee ownership when recovery begins.</summary>
        public bool ReleaseMeleeTokenOnRecover => releaseMeleeTokenOnRecover;

        /// <summary>Minimum surround radius in metres.</summary>
        public float SurroundRadiusMin => surroundRadiusMin;

        /// <summary>Maximum surround radius in metres.</summary>
        public float SurroundRadiusMax => surroundRadiusMax;

        /// <summary>Feint rate while waiting; zero disables feints.</summary>
        public float FeintChancePerSecond => feintChancePerSecond;

        /// <summary>Feint duration in seconds.</summary>
        public float FeintDuration => feintDuration;

        /// <summary>Feint movement speed in metres per second.</summary>
        public float FeintSpeed => feintSpeed;

        /// <summary>Multiplier applied to hunt movement speed.</summary>
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        /// <summary>Multiplier applied to hunt turn speed.</summary>
        public float TurnSpeedMultiplier => turnSpeedMultiplier;

        /// <summary>Multiplier applied to attack cooldowns.</summary>
        public float AttackCooldownMultiplier => attackCooldownMultiplier;

        /// <summary>Desired eye height above ground in metres.</summary>
        public float EyeHoverHeight => eyeHoverHeight;

        /// <summary>Minimum eye height above ground in metres.</summary>
        public float EyeMinFlyHeight => eyeMinFlyHeight;

        /// <summary>Maximum eye height above ground in metres.</summary>
        public float EyeMaxFlyHeight => eyeMaxFlyHeight;

        private void OnValidate()
        {
            huntStartDelay = Mathf.Max(0f, huntStartDelay);
            meleeTokenLimit = Mathf.Max(0, meleeTokenLimit);
            slamTokenLimit = Mathf.Max(0, slamTokenLimit);
            rangedHeavyTokenLimit = Mathf.Max(0, rangedHeavyTokenLimit);
            surroundRadiusMin = Mathf.Max(0f, surroundRadiusMin);
            surroundRadiusMax = Mathf.Max(0f, surroundRadiusMax);
            feintChancePerSecond = Mathf.Max(0f, feintChancePerSecond);
            feintDuration = Mathf.Max(0f, feintDuration);
            feintSpeed = Mathf.Max(0f, feintSpeed);
            moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
            turnSpeedMultiplier = Mathf.Max(0f, turnSpeedMultiplier);
            attackCooldownMultiplier = Mathf.Max(0f, attackCooldownMultiplier);
            eyeHoverHeight = Mathf.Max(0f, eyeHoverHeight);
            eyeMinFlyHeight = Mathf.Max(0f, eyeMinFlyHeight);
            eyeMaxFlyHeight = Mathf.Max(0f, eyeMaxFlyHeight);
            surroundRadiusMax = Mathf.Max(surroundRadiusMin, surroundRadiusMax);
            eyeMaxFlyHeight = Mathf.Max(eyeMinFlyHeight, eyeMaxFlyHeight);
            eyeHoverHeight = Mathf.Clamp(eyeHoverHeight, eyeMinFlyHeight, eyeMaxFlyHeight);
        }
    }
}
