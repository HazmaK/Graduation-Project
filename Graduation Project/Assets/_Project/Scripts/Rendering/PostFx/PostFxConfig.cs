using UnityEngine;

namespace RetroFPS.Rendering.PostFx
{
    /// <summary>Authored blend rates, accessibility ranges and transient effect tuning.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/PostFx Config")]
    public sealed class PostFxConfig : ScriptableObject
    {
        [Header("Blending (weight per unscaled second)")]
        [SerializeField, Min(0f)] private float areaBlendSpeed = 1.5f;
        [SerializeField, Min(0f)] private float lowHealthBlendSpeed = 3f;
        [SerializeField, Min(0f)] private float deathBlendSpeed = 1.6f;
        [SerializeField, Min(0f)] private float deathReleaseSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float lowHealthStart = 0.30f;
        [SerializeField, Range(0f, 1f)] private float lowHealthFull = 0.12f;

        [Header("Static area effects")]
        [SerializeField, Min(0f)] private float arafChromaticAberration = 0.10f;
        [SerializeField, Min(0f)] private float bossPhase2ChromaticAberration = 0.07f;
        [SerializeField, Range(-1f, 0f)] private float arafLensDistortion = -0.05f;

        [Header("Pulses")]
        [SerializeField, Min(0f)] private float damageCaMinimum = 0.25f;
        [SerializeField, Min(0f)] private float damageCaMaximum = 0.75f;
        [SerializeField, Min(0.001f)] private float damageForMaximumCa = 40f;
        [SerializeField, Min(0f)] private float damageDecaySeconds = 0.30f;
        [SerializeField, Min(0f)] private float hitstopCaFloor = 0.22f;
        [SerializeField, Min(0f)] private float parryCa = 0.35f;
        [SerializeField, Min(0f)] private float parryCaDecaySeconds = 0.18f;
        [SerializeField, Min(0f)] private float parryExposure = 0.25f;
        [SerializeField, Min(0f)] private float parryExposureDecaySeconds = 0.14f;
        [SerializeField, Range(-1f, 0f)] private float dashDistortion = -0.16f;
        [SerializeField, Min(0f)] private float dashDecaySeconds = 0.28f;
        [SerializeField, Min(0f)] private float minPulseInterval = 0.35f;

        [Header("Caps and exposure")]
        [SerializeField, Range(0f, 1f)] private float chromaticAberrationCap = 0.9f;
        [SerializeField, Range(-1f, 0f)] private float lensDistortionMinimum = -0.3f;
        [SerializeField] private float exposureMinimum = -2f;
        [SerializeField] private float exposureMaximum = 2f;
        [SerializeField, Min(0f)] private float brightnessEvRange = 1f;
        [SerializeField] private float deathExposure = -0.6f;

        [Header("Grade exposure compensation (EV, scaled by blend weight)")]
        [SerializeField] private float baseExposure = 0.45f;
        [SerializeField] private float arafExposure = 0.60f;
        [SerializeField] private float bossExposure = 0.15f;

        /// <summary>Area weight units per second.</summary>
        public float AreaBlendSpeed => areaBlendSpeed;
        /// <summary>Low health weight units per second.</summary>
        public float LowHealthBlendSpeed => lowHealthBlendSpeed;
        /// <summary>Death weight rise per second.</summary>
        public float DeathBlendSpeed => deathBlendSpeed;
        /// <summary>Death weight release per second.</summary>
        public float DeathReleaseSpeed => deathReleaseSpeed;
        /// <summary>Health fraction at which low health starts.</summary>
        public float LowHealthStart => lowHealthStart;
        /// <summary>Health fraction at which low health reaches full weight.</summary>
        public float LowHealthFull => lowHealthFull;
        /// <summary>Chromatic aberration at full Araf weight.</summary>
        public float ArafChromaticAberration => arafChromaticAberration;
        /// <summary>Chromatic aberration at full boss phase two weight.</summary>
        public float BossPhase2ChromaticAberration => bossPhase2ChromaticAberration;
        /// <summary>Lens distortion at full Araf weight.</summary>
        public float ArafLensDistortion => arafLensDistortion;
        /// <summary>Damage chromatic aberration minimum peak.</summary>
        public float DamageCaMinimum => damageCaMinimum;
        /// <summary>Damage chromatic aberration maximum peak.</summary>
        public float DamageCaMaximum => damageCaMaximum;
        /// <summary>Damage amount that reaches maximum chromatic aberration.</summary>
        public float DamageForMaximumCa => damageForMaximumCa;
        /// <summary>Damage pulse decay duration.</summary>
        public float DamageDecaySeconds => damageDecaySeconds;
        /// <summary>Minimum transient chromatic aberration during hitstop.</summary>
        public float HitstopCaFloor => hitstopCaFloor;
        /// <summary>Parry chromatic aberration peak.</summary>
        public float ParryCa => parryCa;
        /// <summary>Parry chromatic aberration decay duration.</summary>
        public float ParryCaDecaySeconds => parryCaDecaySeconds;
        /// <summary>Parry exposure peak in EV.</summary>
        public float ParryExposure => parryExposure;
        /// <summary>Parry exposure decay duration.</summary>
        public float ParryExposureDecaySeconds => parryExposureDecaySeconds;
        /// <summary>Dash lens distortion peak.</summary>
        public float DashDistortion => dashDistortion;
        /// <summary>Dash distortion decay duration.</summary>
        public float DashDecaySeconds => dashDecaySeconds;
        /// <summary>Minimum time between parry exposure pulse starts.</summary>
        public float MinPulseInterval => minPulseInterval;
        /// <summary>Maximum final chromatic aberration.</summary>
        public float ChromaticAberrationCap => chromaticAberrationCap;
        /// <summary>Minimum final lens distortion.</summary>
        public float LensDistortionMinimum => lensDistortionMinimum;
        /// <summary>Minimum final exposure in EV.</summary>
        public float ExposureMinimum => exposureMinimum;
        /// <summary>Maximum final exposure in EV.</summary>
        public float ExposureMaximum => exposureMaximum;
        /// <summary>EV magnitude for either brightness endpoint.</summary>
        public float BrightnessEvRange => brightnessEvRange;
        /// <summary>Exposure offset at full death weight.</summary>
        public float DeathExposure => deathExposure;
        /// <summary>Exposure compensation (EV) at full base weight.</summary>
        public float BaseExposure => baseExposure;
        /// <summary>Exposure compensation (EV) at full Araf weight.</summary>
        public float ArafExposure => arafExposure;
        /// <summary>Exposure compensation (EV) at full boss weight.</summary>
        public float BossExposure => bossExposure;
    }
}
