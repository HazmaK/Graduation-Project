using RetroFPS.Combat;
using RetroFPS.Pause;
using RetroFPS.Run;
using RetroFPS.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroFPS.Rendering.PostFx
{
    /// <summary>Connects gameplay snapshots to the pure mixer and owned URP Volumes.</summary>
    [DisallowMultipleComponent]
    public sealed class PostFxDirector : MonoBehaviour
    {
        private const float WeightEpsilon = 0.001f;
        private const float ParameterEpsilon = 0.0005f;
        private const int SecondBossPhase = 2;

        [SerializeField] private PostFxConfig config;
        [SerializeField] private Volume baseVolume;
        [SerializeField] private Volume arafVolume;
        [SerializeField] private Volume bossVolume;
        [SerializeField] private Volume bossPhase2Volume;
        [SerializeField] private Volume lowHealthVolume;
        [SerializeField] private Volume deathVolume;
        [SerializeField] private Volume runtimeVolume;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerParryController playerParry;

        private PostFxMixer mixer;
        private PostFxConfig fallbackConfig;
        private VolumeProfile runtimeProfile;
        private ChromaticAberration runtimeCa;
        private LensDistortion runtimeDistortion;
        private ColorAdjustments runtimeColor;
        private bool inAraf, bossEngaged, bossPhase2;

        /// <summary>Pure mixer for presentation inspection.</summary>
        public PostFxMixer Mixer => mixer;
        /// <summary>Current Araf weight, including transitions.</summary>
        public float CurrentArafWeight => mixer != null ? mixer.ArafWeight : 0f;
        /// <summary>Current boss weight, including transitions.</summary>
        public float CurrentBossWeight => mixer != null ? mixer.BossWeight : 0f;
        /// <summary>Current boss phase two weight.</summary>
        public float CurrentBossPhase2Weight => mixer != null ? mixer.BossPhase2Weight : 0f;
        /// <summary>Current base weight.</summary>
        public float CurrentBaseWeight => mixer != null ? mixer.BaseWeight : 0f;
        /// <summary>Current low health weight.</summary>
        public float CurrentLowHealthWeight => mixer != null ? mixer.LowHealthWeight : 0f;
        /// <summary>Current death weight.</summary>
        public float CurrentDeathWeight => mixer != null ? mixer.DeathWeight : 0f;
        /// <summary>Current chromatic aberration intensity.</summary>
        public float CurrentChromaticAberration => mixer != null ? mixer.ChromaticAberration : 0f;
        /// <summary>Current lens distortion intensity.</summary>
        public float CurrentLensDistortion => mixer != null ? mixer.LensDistortion : 0f;
        /// <summary>Current exposure offset in EV.</summary>
        public float CurrentPostExposure => mixer != null ? mixer.PostExposure : 0f;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
            if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();
            if (playerParry == null) playerParry = GetComponentInParent<PlayerParryController>();
            if (config == null)
            {
                fallbackConfig = ScriptableObject.CreateInstance<PostFxConfig>();
                fallbackConfig.hideFlags = HideFlags.DontSave;
            }
            mixer = new PostFxMixer(config != null ? config : fallbackConfig);
            if (runtimeVolume == null) return;

            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "PostFx_Runtime";
            runtimeProfile.hideFlags = HideFlags.DontSave;
            runtimeCa = runtimeProfile.Add<ChromaticAberration>(false);
            runtimeDistortion = runtimeProfile.Add<LensDistortion>(false);
            runtimeColor = runtimeProfile.Add<ColorAdjustments>(false);
            runtimeCa.hideFlags = runtimeDistortion.hideFlags = runtimeColor.hideFlags = HideFlags.DontSave;
            runtimeCa.intensity.overrideState = true;
            runtimeDistortion.intensity.overrideState = true;
            runtimeColor.postExposure.overrideState = true;
            runtimeVolume.profile = runtimeProfile;
        }

        private void OnEnable()
        {
            if (mixer == null) return;
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken += HandleDamage;
                playerHealth.OnHealthChanged += HandleHealth;
                playerHealth.OnDied += HandleDeath;
                playerHealth.OnRevived += HandleRevived;
                mixer.SetHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                mixer.SetDead(playerHealth.IsDead);
            }
            else
            {
                mixer.SetHealth(1f, 1f);
                mixer.SetDead(false);
            }
            if (playerMovement != null) playerMovement.OnDashStarted += HandleDash;
            if (playerParry != null) playerParry.OnParrySuccess += HandleParry;
            RunEvents.OnPhaseChanged += HandlePhase;
            RunEvents.OnRunChanged += HandleRunChanged;
            BossEvents.OnBossEngaged += HandleBossEngaged;
            BossEvents.OnBossPhaseChanged += HandleBossPhase;
            BossEvents.OnBossDisengaged += HandleBossDisengaged;
            GameSettings.Changed += HandleSettings;

            inAraf = RunEvents.CurrentRun != null && RunEvents.CurrentRun.Phase == RunPhase.InAraf;
            bossEngaged = BossEvents.IsEngaged;
            bossPhase2 = BossEvents.Phase >= SecondBossPhase;
            UpdateArea();
            ReadSettings();
            HitstopManager hitstop = HitstopManager.Instance;
            mixer.SetHitstopActive(hitstop != null && hitstop.IsActive);
            mixer.SnapToTargets();
            Apply();
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken -= HandleDamage;
                playerHealth.OnHealthChanged -= HandleHealth;
                playerHealth.OnDied -= HandleDeath;
                playerHealth.OnRevived -= HandleRevived;
            }
            if (playerMovement != null) playerMovement.OnDashStarted -= HandleDash;
            if (playerParry != null) playerParry.OnParrySuccess -= HandleParry;
            RunEvents.OnPhaseChanged -= HandlePhase;
            RunEvents.OnRunChanged -= HandleRunChanged;
            BossEvents.OnBossEngaged -= HandleBossEngaged;
            BossEvents.OnBossPhaseChanged -= HandleBossPhase;
            BossEvents.OnBossDisengaged -= HandleBossDisengaged;
            GameSettings.Changed -= HandleSettings;
        }

        private void Update()
        {
            if (GamePause.IsPaused || mixer == null) return;
            HitstopManager hitstop = HitstopManager.Instance;
            mixer.SetHitstopActive(hitstop != null && hitstop.IsActive);
            mixer.Tick(Time.unscaledDeltaTime);
            Apply();
        }

        private void HandleDamage(float amount) => mixer.NotifyDamage(amount);
        private void HandleHealth(float current, float max) => mixer.SetHealth(current, max);
        private void HandleDeath() => mixer.SetDead(true);
        private void HandleRevived() => mixer.SetDead(false);
        private void HandleDash() => mixer.NotifyDash();
        private void HandleParry(bool projectile) => mixer.NotifyParry();

        private void HandlePhase(RunPhase phase)
        {
            inAraf = phase == RunPhase.InAraf;
            UpdateArea();
            if ((phase == RunPhase.Tower || phase == RunPhase.InAraf)
                && playerHealth != null && playerHealth.CurrentHealth > 0f)
                mixer.SetDead(false);
        }

        private void HandleRunChanged(RunState run)
        {
            inAraf = run != null && run.Phase == RunPhase.InAraf;
            UpdateArea();
        }

        private void HandleBossEngaged(string nameKey, float maxHealth)
        {
            bossEngaged = true;
            bossPhase2 = BossEvents.Phase >= SecondBossPhase;
            UpdateArea();
        }

        private void HandleBossPhase(int phase)
        {
            bossPhase2 = phase >= SecondBossPhase;
            UpdateArea();
        }

        private void HandleBossDisengaged()
        {
            bossEngaged = bossPhase2 = false;
            UpdateArea();
        }

        private void UpdateArea() => mixer.SetArea(inAraf, bossEngaged, bossPhase2);
        private void HandleSettings(GameSettingsData settings)
        {
            ReadSettings();
            // User settings apply immediately in the pause menu; blend and pulse clocks stay frozen.
            Apply();
        }

        private void ReadSettings() => mixer.SetSettings(GameSettings.Current.PostProcessing,
            GameSettings.Current.Brightness, GameSettings.Current.ScreenEffects, GameSettings.Current.ReduceMotion);

        private void Apply()
        {
            ApplyWeight(baseVolume, mixer.BaseWeight);
            ApplyWeight(arafVolume, mixer.ArafWeight);
            ApplyWeight(bossVolume, mixer.BossWeight);
            ApplyWeight(bossPhase2Volume, mixer.BossPhase2Weight);
            ApplyWeight(lowHealthVolume, mixer.LowHealthWeight);
            ApplyWeight(deathVolume, mixer.DeathWeight);
            ApplyWeight(runtimeVolume, 1f);
            if (runtimeCa != null) ApplyParameter(runtimeCa.intensity, mixer.ChromaticAberration);
            if (runtimeDistortion != null) ApplyParameter(runtimeDistortion.intensity, mixer.LensDistortion);
            if (runtimeColor != null) ApplyParameter(runtimeColor.postExposure, mixer.PostExposure);
        }

        private static void ApplyWeight(Volume volume, float weight)
        {
            if (volume == null) return;
            if (Mathf.Abs(volume.weight - weight) > WeightEpsilon)
                volume.weight = weight;
            bool shouldEnable = weight > 0f;
            if (volume.enabled != shouldEnable) volume.enabled = shouldEnable;
        }

        private static void ApplyParameter(VolumeParameter<float> parameter, float value)
        {
            if (Mathf.Abs(parameter.value - value) > ParameterEpsilon) parameter.value = value;
        }

        private void OnDestroy()
        {
            if (runtimeVolume != null && runtimeProfile != null) runtimeVolume.profile = null;
            if (runtimeProfile != null)
            {
                for (int i = 0; i < runtimeProfile.components.Count; i++)
                    Destroy(runtimeProfile.components[i]);
                Destroy(runtimeProfile);
            }
            if (fallbackConfig != null) Destroy(fallbackConfig);
        }
    }
}
