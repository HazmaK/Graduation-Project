using System;
using UnityEngine;

namespace RetroFPS.Rendering.PostFx
{
    /// <summary>Allocation-free state blending and pulse composition, independent of Volumes.</summary>
    public sealed class PostFxMixer
    {
        private readonly PostFxConfig config;
        private bool postProcessing = true;
        private float brightness;
        private float screenEffects = 1f;
        private bool reduceMotion;
        private float healthFraction = 1f;
        private bool dead, inAraf, bossEngaged, bossPhase2, hitstopActive;
        private Pulse damagePulse, parryCaPulse, parryExposurePulse, dashPulse;
        private float parryExposureCooldown;

        /// <summary>Creates an isolated mixer using the supplied tuning asset.</summary>
        public PostFxMixer(PostFxConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            SnapToTargets();
        }

        /// <summary>Base look weight.</summary>
        public float BaseWeight { get; private set; }
        /// <summary>Current Araf blend weight.</summary>
        public float ArafWeight { get; private set; }
        /// <summary>Current boss blend weight.</summary>
        public float BossWeight { get; private set; }
        /// <summary>Current boss phase two blend weight.</summary>
        public float BossPhase2Weight { get; private set; }
        /// <summary>Current low health blend weight.</summary>
        public float LowHealthWeight { get; private set; }
        /// <summary>Current death blend weight.</summary>
        public float DeathWeight { get; private set; }
        /// <summary>Final chromatic aberration intensity.</summary>
        public float ChromaticAberration { get; private set; }
        /// <summary>Final lens distortion intensity.</summary>
        public float LensDistortion { get; private set; }
        /// <summary>Final exposure offset in EV.</summary>
        public float PostExposure { get; private set; }

        /// <summary>Applies accessibility settings; disabling post processing clears authored weights immediately.</summary>
        public void SetSettings(bool postProcessing, float brightness, float screenEffects, bool reduceMotion)
        {
            bool toggled = this.postProcessing != postProcessing;
            this.postProcessing = postProcessing;
            this.brightness = Mathf.Clamp(brightness, -1f, 1f);
            this.screenEffects = Mathf.Clamp01(screenEffects);
            this.reduceMotion = reduceMotion;
            if (!postProcessing || reduceMotion)
            {
                damagePulse.Clear();
                parryCaPulse.Clear();
                parryExposurePulse.Clear();
                dashPulse.Clear();
            }
            if (!postProcessing || toggled) SnapToTargets();
            else Compose();
        }

        /// <summary>Sets health; an absent or invalid maximum represents full health.</summary>
        public void SetHealth(float current, float max)
            => healthFraction = max > 0f ? Mathf.Clamp01(current / max) : 1f;

        /// <summary>Sets the death target without restarting blends.</summary>
        public void SetDead(bool value) => dead = value;

        /// <summary>Sets area targets; Araf suppresses both boss targets.</summary>
        public void SetArea(bool inAraf, bool bossEngaged, bool bossPhase2)
        {
            this.inAraf = inAraf;
            this.bossEngaged = bossEngaged;
            this.bossPhase2 = bossPhase2;
        }

        /// <summary>Sets the transient hitstop floor input.</summary>
        public void SetHitstopActive(bool value) => hitstopActive = value;

        /// <summary>Starts or max-merges a damage pulse.</summary>
        public void NotifyDamage(float amount)
        {
            if (!PulsesAllowed) return;
            float ratio = config.DamageForMaximumCa > 0f ? amount / config.DamageForMaximumCa : 1f;
            damagePulse.Trigger(Mathf.Lerp(config.DamageCaMinimum, config.DamageCaMaximum,
                Mathf.Clamp01(ratio)), config.DamageDecaySeconds);
            Compose();
        }

        /// <summary>Starts parry feedback, rate limiting only the exposure pulse.</summary>
        public void NotifyParry()
        {
            if (!PulsesAllowed) return;
            parryCaPulse.Trigger(config.ParryCa, config.ParryCaDecaySeconds);
            if (parryExposureCooldown <= 0f)
            {
                parryExposurePulse.Trigger(config.ParryExposure, config.ParryExposureDecaySeconds);
                parryExposureCooldown = config.MinPulseInterval;
            }
            Compose();
        }

        /// <summary>Starts or max-merges the magnitude of a dash distortion pulse.</summary>
        public void NotifyDash()
        {
            if (!PulsesAllowed) return;
            dashPulse.Trigger(Mathf.Abs(config.DashDistortion), config.DashDecaySeconds);
            Compose();
        }

        /// <summary>Advances blends and linear pulse decay by positive unscaled seconds.</summary>
        public void Tick(float unscaledDt)
        {
            if (!(unscaledDt > 0f)) return;
            float areaStep = Mathf.Max(0f, config.AreaBlendSpeed) * unscaledDt;
            BaseWeight = postProcessing ? 1f : 0f;
            ArafWeight = Mathf.MoveTowards(ArafWeight, ArafTarget, areaStep);
            BossWeight = Mathf.MoveTowards(BossWeight, BossTarget, areaStep);
            BossPhase2Weight = Mathf.MoveTowards(BossPhase2Weight, BossPhase2Target, areaStep);
            LowHealthWeight = Mathf.MoveTowards(LowHealthWeight, LowHealthTarget,
                Mathf.Max(0f, config.LowHealthBlendSpeed) * unscaledDt);
            float deathTarget = DeathTarget;
            float deathSpeed = deathTarget > DeathWeight ? config.DeathBlendSpeed : config.DeathReleaseSpeed;
            DeathWeight = Mathf.MoveTowards(DeathWeight, deathTarget, Mathf.Max(0f, deathSpeed) * unscaledDt);
            damagePulse.Tick(unscaledDt);
            parryCaPulse.Tick(unscaledDt);
            parryExposurePulse.Tick(unscaledDt);
            dashPulse.Tick(unscaledDt);
            parryExposureCooldown = Mathf.Max(0f, parryExposureCooldown - unscaledDt);
            Compose();
        }

        /// <summary>Immediately initializes every authored weight from the latest inputs.</summary>
        public void SnapToTargets()
        {
            BaseWeight = postProcessing ? 1f : 0f;
            ArafWeight = ArafTarget;
            BossWeight = BossTarget;
            BossPhase2Weight = BossPhase2Target;
            LowHealthWeight = LowHealthTarget;
            DeathWeight = DeathTarget;
            Compose();
        }

        private bool PulsesAllowed => postProcessing && !reduceMotion;
        private float ArafTarget => postProcessing && inAraf ? 1f : 0f;
        private float BossTarget => postProcessing && !inAraf && bossEngaged ? 1f : 0f;
        private float BossPhase2Target => BossTarget > 0f && bossPhase2 ? 1f : 0f;
        private float DeathTarget => postProcessing && dead ? 1f : 0f;
        private float LowHealthTarget => !postProcessing ? 0f
            : 1f - Mathf.InverseLerp(config.LowHealthFull, config.LowHealthStart, healthFraction);

        private void Compose()
        {
            float staticCa = config.ArafChromaticAberration * ArafWeight
                + config.BossPhase2ChromaticAberration * BossPhase2Weight;
            float transientCa = PulsesAllowed ? Mathf.Max(damagePulse.Value,
                Mathf.Max(hitstopActive ? config.HitstopCaFloor : 0f, parryCaPulse.Value)) : 0f;
            ChromaticAberration = postProcessing
                ? Mathf.Clamp((staticCa + transientCa) * screenEffects, 0f, config.ChromaticAberrationCap) : 0f;
            LensDistortion = postProcessing
                ? Mathf.Clamp((config.ArafLensDistortion * ArafWeight
                    - (PulsesAllowed ? dashPulse.Value : 0f)) * screenEffects, config.LensDistortionMinimum, 0f) : 0f;
            PostExposure = Mathf.Clamp(brightness * config.BrightnessEvRange
                + config.BaseExposure * BaseWeight + config.ArafExposure * ArafWeight
                + config.BossExposure * BossWeight
                + config.DeathExposure * DeathWeight
                + (PulsesAllowed ? parryExposurePulse.Value * screenEffects : 0f),
                config.ExposureMinimum, config.ExposureMaximum);
        }

        private struct Pulse
        {
            private float value, decayRate;
            internal float Value => value;

            internal void Trigger(float peak, float seconds)
            {
                value = Mathf.Max(value, peak);
                if (seconds <= 0f) { Clear(); return; }
                decayRate = value / seconds;
            }

            internal void Tick(float dt)
                => value = value > 0f ? Mathf.Max(0f, value - decayRate * dt) : 0f;
            internal void Clear() { value = 0f; decayRate = 0f; }
        }
    }
}
