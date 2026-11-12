using UnityEngine;

namespace RetroFPS.Presentation
{
    /// <summary>Shared scalar settings for combat presentation. Time suffixes denote unscaled milliseconds or seconds.</summary>
    [CreateAssetMenu(fileName = "HudConfig", menuName = "RetroFPS/UI/HUD Config")]
    public sealed class HudConfig : ScriptableObject
    {
        private const float DefaultLowHealthThreshold = 0.30f;

        /// <summary>Fallback inclusive health boundary when no config is assigned.</summary>
        public static float DefaultLowHealthPercent => DefaultLowHealthThreshold;

        [SerializeField, Range(0.1f, 0.5f)] private float lowHealthPercent = DefaultLowHealthPercent;
        [SerializeField, Range(0.1f, 0.5f)] private float lowAmmoRatio = 0.25f;
        [SerializeField, Range(1, 10)] private int lowEnemiesEmphasis = 3;
        [SerializeField, Range(800, 4000)] private int bannerHoldMs = 1800;
        [SerializeField, Range(800, 4000)] private int arenaClearedHoldMs = 2200;
        [SerializeField, Range(0, 600)] private int bannerFadeInMs = 150;
        [SerializeField, Range(0, 600)] private int bannerFadeOutMs = 300;
        [SerializeField, Range(100, 1000)] private int bannerMergeWindowMs = 500;
        [SerializeField, Range(0.88f, 0.95f)] private float plateAlpha = 0.90f;
        [SerializeField, Range(3f, 10f)] private float safeInsetPercent = 5f;
        [SerializeField, Range(0.75f, 1.5f)] private float hudScale = 1f;
        [SerializeField, Range(0.5f, 1f)] private float lowPulseHz = 1f;
        [SerializeField, Range(0, 800)] private int deathFadeMs = 400;
        [SerializeField, Min(0f)] private float deathArmSeconds = 0.6f;
        [SerializeField, Range(8, 60)] private int cooldownFillSteps = 20;
        [SerializeField, Min(0)] private int parrySuccessMs = 250;
        [SerializeField, Min(0)] private int encounterCompletedHideMs = 300;
        [SerializeField, Min(0)] private int deathConfirmMs = 80;

        /// <summary>Inclusive low-health boundary shared with gameplay feedback.</summary>
        public float LowHealthPercent => lowHealthPercent;
        /// <summary>Exclusive low-ammo boundary.</summary>
        public float LowAmmoRatio => lowAmmoRatio;
        /// <summary>Remaining enemy count that receives emphasis.</summary>
        public int LowEnemiesEmphasis => lowEnemiesEmphasis;
        /// <summary>Default banner hold duration.</summary>
        public int BannerHoldMs => bannerHoldMs;
        /// <summary>Arena-cleared hold duration.</summary>
        public int ArenaClearedHoldMs => arenaClearedHoldMs;
        /// <summary>Banner entrance duration.</summary>
        public int BannerFadeInMs => bannerFadeInMs;
        /// <summary>Banner exit duration.</summary>
        public int BannerFadeOutMs => bannerFadeOutMs;
        /// <summary>Window for adjacent banner merging and replacement.</summary>
        public int BannerMergeWindowMs => bannerMergeWindowMs;
        /// <summary>Gameplay plate opacity.</summary>
        public float PlateAlpha => plateAlpha;
        /// <summary>Screen inset in percentage points.</summary>
        public float SafeInsetPercent => safeInsetPercent;
        /// <summary>Scale applied to the HUD root.</summary>
        public float HudScale => hudScale;
        /// <summary>Low-state pulse frequency, capped at one hertz.</summary>
        public float LowPulseHz => Mathf.Clamp(lowPulseHz, 0f, 1f);
        /// <summary>Death-card fade duration.</summary>
        public int DeathFadeMs => deathFadeMs;
        /// <summary>Minimum unscaled seconds before death confirmation, including under reduced motion.</summary>
        public float DeathArmSeconds => Mathf.Max(0f, deathArmSeconds);
        /// <summary>Number of cooldown fill buckets.</summary>
        public int CooldownFillSteps => cooldownFillSteps;
        /// <summary>Parry-success word duration.</summary>
        public int ParrySuccessMs => parrySuccessMs;
        /// <summary>Delay before hiding the completed encounter block.</summary>
        public int EncounterCompletedHideMs => encounterCompletedHideMs;
        /// <summary>Confirmation lock retained under reduced motion.</summary>
        public int DeathConfirmMs => deathConfirmMs;
    }
}
