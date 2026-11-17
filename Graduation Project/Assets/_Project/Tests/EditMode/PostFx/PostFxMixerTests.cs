using NUnit.Framework;
using RetroFPS.Rendering.PostFx;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic state blending, accessibility and pulse composition coverage.</summary>
    public sealed class PostFxMixerTests
    {
        private const float Tolerance = 0.00001f;
        private PostFxConfig config;
        private PostFxMixer mixer;

        /// <summary>Creates independent default tuning and mixer state.</summary>
        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<PostFxConfig>();
            // Grade compensation is covered by its own tests; zero it so the other exposure arithmetic stays isolated.
            SetExposure(0f, 0f, 0f);
            mixer = new PostFxMixer(config);
        }

        private void SetExposure(float baseEv, float arafEv, float bossEv)
        {
            var so = new SerializedObject(config);
            so.FindProperty("baseExposure").floatValue = baseEv;
            so.FindProperty("arafExposure").floatValue = arafEv;
            so.FindProperty("bossExposure").floatValue = bossEv;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Authored defaults compensate the grade per blend weight and vanish with post-processing off.</summary>
        [Test]
        public void GradeExposure_ScalesWithWeightsAndVanishesWhenOff()
        {
            SetExposure(0.45f, 0.40f, 0.15f);
            mixer.SetArea(false, false, false); mixer.SnapToTargets();
            Assert.That(mixer.PostExposure, Is.EqualTo(0.45f).Within(Tolerance));
            mixer.SetArea(true, true, false); mixer.SnapToTargets();
            Assert.That(mixer.PostExposure, Is.EqualTo(0.85f).Within(Tolerance));
            mixer.SetArea(false, true, false); mixer.SnapToTargets();
            Assert.That(mixer.PostExposure, Is.EqualTo(0.60f).Within(Tolerance));
            mixer.SetSettings(true, 0.2f, 0f, false);
            Assert.That(mixer.PostExposure, Is.EqualTo(0.80f).Within(Tolerance), "not scaled by ScreenEffects");
            mixer.SetSettings(false, 0.2f, 1f, false);
            Assert.That(mixer.PostExposure, Is.EqualTo(0.2f).Within(Tolerance));
        }

        /// <summary>Releases the temporary tuning asset.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        /// <summary>Low health maps endpoints, midpoint and clamped outliers.</summary>
        [TestCase(30f, 100f, 0f)]
        [TestCase(12f, 100f, 1f)]
        [TestCase(21f, 100f, 0.5f)]
        [TestCase(200f, 100f, 0f)]
        [TestCase(-10f, 100f, 1f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, -100f, 0f)]
        public void Health_SnapsToExpectedWeight(float current, float max, float expected)
        {
            mixer.SetHealth(current, max);
            mixer.SnapToTargets();
            Assert.That(mixer.LowHealthWeight, Is.EqualTo(expected).Within(Tolerance));
        }

        /// <summary>Areas use unscaled rate-limited movement and do not overshoot.</summary>
        [Test]
        public void AreaWeights_MoveAtConfiguredSpeed()
        {
            mixer.SetArea(true, false, false);
            mixer.Tick(0.2f);
            Assert.That(mixer.ArafWeight, Is.EqualTo(0.3f).Within(Tolerance));
            mixer.Tick(100f);
            Assert.That(mixer.ArafWeight, Is.EqualTo(1f));
            mixer.SetArea(false, true, true);
            mixer.Tick(0.2f);
            Assert.That(mixer.ArafWeight, Is.EqualTo(0.7f).Within(Tolerance));
            Assert.That(mixer.BossWeight, Is.EqualTo(0.3f).Within(Tolerance));
            Assert.That(mixer.BossPhase2Weight, Is.EqualTo(0.3f).Within(Tolerance));
        }

        /// <summary>Araf suppresses both boss targets even if the boss remains engaged.</summary>
        [Test]
        public void Araf_SuppressesBossTargets()
        {
            mixer.SetArea(false, true, true);
            mixer.SnapToTargets();
            mixer.SetArea(true, true, true);
            mixer.Tick(100f);
            Assert.That(mixer.ArafWeight, Is.EqualTo(1f));
            Assert.That(mixer.BossWeight, Is.Zero);
            Assert.That(mixer.BossPhase2Weight, Is.Zero);
        }

        /// <summary>Phase two requires an engaged boss.</summary>
        [Test]
        public void PhaseTwo_WithoutBoss_HasZeroTarget()
        {
            mixer.SetArea(false, false, true);
            mixer.SnapToTargets();
            Assert.That(mixer.BossPhase2Weight, Is.Zero);
        }

        /// <summary>Low health blends independently at its configured speed.</summary>
        [Test]
        public void LowHealth_UsesItsBlendSpeed()
        {
            mixer.SetHealth(0f, 100f);
            mixer.Tick(0.1f);
            Assert.That(mixer.LowHealthWeight, Is.EqualTo(0.3f).Within(Tolerance));
            mixer.SetHealth(100f, 100f);
            mixer.Tick(0.1f);
            Assert.That(mixer.LowHealthWeight, Is.Zero.Within(Tolerance));
        }

        /// <summary>Death rises and releases at separate rates.</summary>
        [Test]
        public void Death_UsesDistinctUpAndDownSpeeds()
        {
            mixer.SetDead(true);
            mixer.Tick(0.25f);
            Assert.That(mixer.DeathWeight, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(mixer.PostExposure, Is.EqualTo(-0.24f).Within(Tolerance));
            mixer.SetDead(false);
            mixer.Tick(0.05f);
            Assert.That(mixer.DeathWeight, Is.EqualTo(0.2f).Within(Tolerance));
            mixer.Tick(10f);
            Assert.That(mixer.DeathWeight, Is.Zero);
        }

        /// <summary>Snap initializes all weights without requiring a positive tick.</summary>
        [Test]
        public void SnapToTargets_InitializesEveryAuthoredWeight()
        {
            mixer.SetArea(false, true, true);
            mixer.SetHealth(21f, 100f);
            mixer.SetDead(true);
            mixer.SnapToTargets();
            Assert.That(mixer.BaseWeight, Is.EqualTo(1f));
            Assert.That(mixer.ArafWeight, Is.Zero);
            Assert.That(mixer.BossWeight, Is.EqualTo(1f));
            Assert.That(mixer.BossPhase2Weight, Is.EqualTo(1f));
            Assert.That(mixer.LowHealthWeight, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(mixer.DeathWeight, Is.EqualTo(1f));
        }

        /// <summary>Nonpositive delta does not advance weights, pulses or cooldowns.</summary>
        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonpositiveDelta_HoldsOutputs(float dt)
        {
            mixer.NotifyDamage(40f);
            mixer.NotifyParry();
            mixer.NotifyDash();
            mixer.SetArea(true, false, false);
            mixer.SetDead(true);
            mixer.Tick(dt);
            Assert.That(mixer.ArafWeight, Is.Zero);
            Assert.That(mixer.DeathWeight, Is.Zero);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.16f).Within(Tolerance));
            Assert.That(mixer.PostExposure, Is.EqualTo(0.25f).Within(Tolerance));
        }

        /// <summary>Damage magnitude interpolates and clamps the peak.</summary>
        [TestCase(-10f, 0.25f)]
        [TestCase(0f, 0.25f)]
        [TestCase(20f, 0.5f)]
        [TestCase(40f, 0.75f)]
        [TestCase(400f, 0.75f)]
        public void Damage_MapsMagnitudeToPeak(float damage, float expected)
        {
            mixer.NotifyDamage(damage);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(expected).Within(Tolerance));
        }

        /// <summary>Damage decays linearly to zero at its authored duration.</summary>
        [Test]
        public void Damage_DecaysLinearly()
        {
            mixer.NotifyDamage(40f);
            mixer.Tick(0.15f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.375f).Within(Tolerance));
            mixer.Tick(0.15f);
            Assert.That(mixer.ChromaticAberration, Is.Zero.Within(Tolerance));
            mixer.Tick(100f);
            Assert.That(mixer.ChromaticAberration, Is.Zero);
        }

        /// <summary>Retriggering never reduces the current pulse or adds peaks together.</summary>
        [Test]
        public void Damage_MaxMergesAndRestartsDecayFromMergedPeak()
        {
            mixer.NotifyDamage(40f);
            mixer.Tick(0.06f);
            mixer.NotifyDamage(1f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.6f).Within(Tolerance));
            mixer.Tick(0.15f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.3f).Within(Tolerance));
            mixer.NotifyDamage(40f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.75f).Within(Tolerance));
        }

        /// <summary>Simultaneous transient CA sources use their maximum, then add the area contribution.</summary>
        [Test]
        public void ChromaticAberration_AddsStaticToMaximumTransient()
        {
            mixer.SetArea(true, false, false);
            mixer.SnapToTargets();
            mixer.SetHitstopActive(true);
            mixer.NotifyDamage(20f);
            mixer.NotifyParry();
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.6f).Within(Tolerance));
            mixer.Tick(1f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.32f).Within(Tolerance));
            mixer.SetHitstopActive(false);
            mixer.Tick(0.01f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.1f).Within(Tolerance));
        }

        /// <summary>Area CA and distortion track the current weight during a transition.</summary>
        [Test]
        public void StaticAreaEffects_FollowCurrentWeights()
        {
            mixer.SetArea(true, false, false);
            mixer.Tick(0.2f);
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.03f).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.015f).Within(Tolerance));
            mixer.SetArea(false, true, true);
            mixer.SnapToTargets();
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.07f).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.Zero);
        }

        /// <summary>Parry CA and exposure each use their own linear decay duration.</summary>
        [Test]
        public void Parry_DecaysAtIndependentDurations()
        {
            mixer.NotifyParry();
            mixer.Tick(0.07f);
            Assert.That(mixer.PostExposure, Is.EqualTo(0.125f).Within(Tolerance));
            mixer.Tick(0.07f);
            Assert.That(mixer.PostExposure, Is.Zero.Within(Tolerance));
            Assert.That(mixer.ChromaticAberration,
                Is.EqualTo(0.35f * (1f - 0.14f / 0.18f)).Within(Tolerance));
            mixer.Tick(0.04f);
            Assert.That(mixer.ChromaticAberration, Is.Zero.Within(Tolerance));
        }

        /// <summary>Parry exposure cannot restart before the minimum interval; CA can.</summary>
        [Test]
        public void ParryExposure_EnforcesMinimumStartInterval()
        {
            mixer.NotifyParry();
            mixer.Tick(0.2f);
            mixer.NotifyParry();
            Assert.That(mixer.PostExposure, Is.Zero.Within(Tolerance));
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.35f).Within(Tolerance));
            mixer.Tick(0.149f);
            mixer.NotifyParry();
            Assert.That(mixer.PostExposure, Is.Zero.Within(Tolerance));
            mixer.Tick(0.002f);
            mixer.NotifyParry();
            Assert.That(mixer.PostExposure, Is.EqualTo(0.25f).Within(Tolerance));
        }

        /// <summary>Dash distortion decays linearly and max-merges retriggers.</summary>
        [Test]
        public void Dash_DecaysAndMaxMerges()
        {
            mixer.NotifyDash();
            mixer.NotifyDash();
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.16f).Within(Tolerance));
            mixer.Tick(0.14f);
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.08f).Within(Tolerance));
            mixer.Tick(0.14f);
            Assert.That(mixer.LensDistortion, Is.Zero.Within(Tolerance));
        }

        /// <summary>Reduce motion removes every transient source but retains static area effects and weights.</summary>
        [Test]
        public void ReduceMotion_SuppressesAllPulsesAndHitstop()
        {
            mixer.SetArea(true, false, false);
            mixer.SetDead(true);
            mixer.SnapToTargets();
            mixer.NotifyDamage(40f);
            mixer.NotifyParry();
            mixer.NotifyDash();
            mixer.SetSettings(true, 0.5f, 1f, true);
            mixer.SetHitstopActive(true);
            mixer.NotifyDamage(40f);
            mixer.NotifyParry();
            mixer.NotifyDash();
            mixer.Tick(0.01f);
            Assert.That(mixer.ArafWeight, Is.EqualTo(1f));
            Assert.That(mixer.DeathWeight, Is.EqualTo(1f));
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.1f).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.05f).Within(Tolerance));
            Assert.That(mixer.PostExposure, Is.EqualTo(-0.1f).Within(Tolerance));
        }

        /// <summary>Screen effects scales static effects and pulses, preserving weights, brightness and death EV.</summary>
        [TestCase(0f)]
        [TestCase(0.5f)]
        public void ScreenEffects_ScalesOnlyEffectIntensities(float strength)
        {
            mixer.SetSettings(true, 0.5f, strength, false);
            mixer.SetArea(true, false, false);
            mixer.SetDead(true);
            mixer.SetHealth(0f, 100f);
            mixer.SnapToTargets();
            mixer.NotifyDamage(40f);
            mixer.NotifyParry();
            mixer.NotifyDash();
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.85f * strength).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.21f * strength).Within(Tolerance));
            Assert.That(mixer.PostExposure, Is.EqualTo(-0.1f + 0.25f * strength).Within(Tolerance));
            Assert.That(mixer.ArafWeight, Is.EqualTo(1f));
            Assert.That(mixer.DeathWeight, Is.EqualTo(1f));
            Assert.That(mixer.LowHealthWeight, Is.EqualTo(1f));
        }

        /// <summary>Disabling post processing snaps every authored weight off and leaves brightness operational.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void PostProcessingOff_SnapsAllWeightsAndPreservesBrightness(bool araf)
        {
            mixer.SetArea(araf, true, true);
            mixer.SetHealth(0f, 100f);
            mixer.SetDead(true);
            mixer.SnapToTargets();
            mixer.NotifyDamage(40f);
            mixer.NotifyParry();
            mixer.NotifyDash();
            mixer.SetSettings(false, 0.6f, 1f, false);
            AssertAuthoredWeightsZero();
            Assert.That(mixer.ChromaticAberration, Is.Zero);
            Assert.That(mixer.LensDistortion, Is.Zero);
            Assert.That(mixer.PostExposure, Is.EqualTo(0.6f).Within(Tolerance));
            mixer.SetHitstopActive(true);
            mixer.NotifyParry();
            mixer.NotifyDash();
            mixer.NotifyDamage(40f);
            mixer.Tick(10f);
            AssertAuthoredWeightsZero();
            Assert.That(mixer.PostExposure, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(mixer.ChromaticAberration, Is.Zero);
            Assert.That(mixer.LensDistortion, Is.Zero);
        }

        /// <summary>Re-enabling snaps to retained targets at once (no Tick, as when paused) without restoring old pulses.</summary>
        [Test]
        public void PostProcessingOn_SnapsToTargetsWithoutOldPulses()
        {
            mixer.SetArea(true, false, false);
            mixer.NotifyDamage(40f);
            mixer.SetSettings(false, 0f, 1f, false);
            mixer.SetSettings(true, 0f, 1f, false);
            Assert.That(mixer.BaseWeight, Is.EqualTo(1f));
            Assert.That(mixer.ArafWeight, Is.EqualTo(1f));
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.1f).Within(Tolerance));
        }

        /// <summary>Accessibility input clamps to its supported ranges.</summary>
        [TestCase(4f, 4f, 1f, 0.75f)]
        [TestCase(-4f, -4f, -1f, 0f)]
        public void Settings_ClampBrightnessAndEffectStrength(float brightness, float effects,
            float expectedExposure, float expectedCa)
        {
            mixer.SetSettings(true, brightness, effects, false);
            mixer.NotifyDamage(40f);
            Assert.That(mixer.PostExposure, Is.EqualTo(expectedExposure).Within(Tolerance));
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(expectedCa).Within(Tolerance));
        }

        /// <summary>Final caps hold even when authored peaks and brightness range exceed them.</summary>
        [Test]
        public void Outputs_RespectFinalCaps()
        {
            Tune("damageCaMaximum", 3f);
            Tune("dashDistortion", -0.8f);
            Tune("brightnessEvRange", 4f);
            mixer.SetSettings(true, 1f, 1f, false);
            mixer.NotifyDamage(40f);
            mixer.NotifyDash();
            mixer.NotifyParry();
            Assert.That(mixer.ChromaticAberration, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(mixer.LensDistortion, Is.EqualTo(-0.3f).Within(Tolerance));
            Assert.That(mixer.PostExposure, Is.EqualTo(2f).Within(Tolerance));
            mixer.SetSettings(true, -1f, 1f, false);
            mixer.SetDead(true);
            mixer.SnapToTargets();
            Assert.That(mixer.PostExposure, Is.EqualTo(-2f).Within(Tolerance));
        }

        private void AssertAuthoredWeightsZero()
        {
            Assert.That(mixer.BaseWeight, Is.Zero);
            Assert.That(mixer.ArafWeight, Is.Zero);
            Assert.That(mixer.BossWeight, Is.Zero);
            Assert.That(mixer.BossPhase2Weight, Is.Zero);
            Assert.That(mixer.LowHealthWeight, Is.Zero);
            Assert.That(mixer.DeathWeight, Is.Zero);
        }

        private void Tune(string field, float value)
        {
            var data = new SerializedObject(config);
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
