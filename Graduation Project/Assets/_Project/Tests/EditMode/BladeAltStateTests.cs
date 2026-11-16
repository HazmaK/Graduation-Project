using NUnit.Framework;
using RetroFPS.Weapons;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Independent special recovery and height credit across lunge, flight and landing.</summary>
    public sealed class BladeAltStateTests
    {
        private const float LungeCooldown = 2.2f;
        private const float LungeDuration = 0.22f;
        private const float ChainReset = 0.10f;
        private const float SlamSafetyCooldown = 1.25f;
        private const float SlamLandingCooldown = 1f;
        private const float SlamThreshold = -0.42f;
        private const float LookingDown = -1f;
        private const float LevelAim = 0f;
        private const float HeightCap = 4f;
        private const float MaximumHeight = 6f;
        private const float ShortFallHeight = 1f;
        private const float LandingY = 100f;
        private const float Tolerance = 0.00001f;

        /// <summary>The second RMB is legal both during lunge travel and after travel ends.</summary>
        [TestCase(0f)]
        [TestCase(LungeDuration)]
        public void LungeOnCooldown_AirborneDownwardSlamIsReady(float elapsed)
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.Advance(elapsed, false);
            Assert.That(state.CanFire(false, LevelAim, SlamThreshold), Is.False);
            Assert.That(state.CanFire(false, LookingDown, SlamThreshold), Is.True);
            state.StartSlam(SlamSafetyCooldown, elapsed < LungeDuration);
            Assert.That(state.LungeCooldownRemaining, Is.EqualTo(LungeCooldown - elapsed).Within(Tolerance));
            Assert.That(state.SlamCooldownRemaining, Is.EqualTo(SlamSafetyCooldown));
        }

        /// <summary>A landing's slam recovery cannot lock out a grounded or level-aimed lunge.</summary>
        [Test]
        public void SlamOnCooldown_LungeIsReadyAndPreservesSlamRecovery()
        {
            var state = new BladeAltState();
            state.StartSlam(SlamSafetyCooldown, false);
            state.SetSlamCooldown(SlamLandingCooldown);
            Assert.That(state.CanFire(false, LookingDown, SlamThreshold), Is.False);
            Assert.That(state.CanFire(false, LevelAim, SlamThreshold), Is.True);
            Assert.That(state.CanFire(true, LookingDown, SlamThreshold), Is.True);
            state.StartLunge(LungeCooldown);
            Assert.That(state.SlamCooldownRemaining, Is.EqualTo(SlamLandingCooldown));
        }

        /// <summary>Ground contact and the inclusive pitch boundary both select lunge.</summary>
        [TestCase(true, LookingDown, false)]
        [TestCase(false, SlamThreshold, false)]
        [TestCase(false, LevelAim, false)]
        [TestCase(false, LookingDown, true)]
        public void ActionSelection_UsesGroundingAndStrictPitchThreshold(bool grounded, float forwardY, bool slam)
        {
            Assert.That(BladeAltState.WantsSlam(grounded, forwardY, SlamThreshold), Is.EqualTo(slam));
        }

        /// <summary>The shared active/holstered advancement decrements both timers and clamps at zero.</summary>
        [Test]
        public void Advance_RecoversBothSpecialsIndependently()
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.StartSlam(SlamSafetyCooldown, true);
            state.Advance(-LungeDuration, false);
            Assert.That(state.LungeCooldownRemaining, Is.EqualTo(LungeCooldown));
            Assert.That(state.SlamCooldownRemaining, Is.EqualTo(SlamSafetyCooldown));
            state.Advance(SlamSafetyCooldown, false);
            Assert.That(state.SlamCooldownRemaining, Is.Zero);
            Assert.That(state.LungeCooldownRemaining, Is.EqualTo(LungeCooldown - SlamSafetyCooldown).Within(Tolerance));
            state.Advance(LungeCooldown, true);
            Assert.That(state.LungeCooldownRemaining, Is.Zero);
        }

        /// <summary>A kill resets only lunge recovery, while slam landing resets only slam recovery.</summary>
        [Test]
        public void ChainResetAndLanding_ChangeOnlyTheirOwnRecovery()
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.StartSlam(SlamSafetyCooldown, true);
            state.ResetLungeCooldown(ChainReset);
            Assert.That(state.SlamCooldownRemaining, Is.EqualTo(SlamSafetyCooldown));
            state.SetSlamCooldown(SlamLandingCooldown);
            Assert.That(state.LungeCooldownRemaining, Is.EqualTo(ChainReset));
            state.Advance(ChainReset, true);
            Assert.That(state.CanFire(true, LevelAim, SlamThreshold), Is.True);
            Assert.That(state.CanFire(false, LookingDown, SlamThreshold), Is.False);
        }

        /// <summary>The cap applies to an active lunge and to a completed lunge in the same airtime.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void SlamAfterLungeInSameAirtime_CapsAllHeightScaledOutputs(bool stillLunging)
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.Advance(LungeDuration, false);
            state.StartSlam(SlamSafetyCooldown, stillLunging);
            // Landing may be observed by the blade tick before the movement callback runs.
            state.Advance(0f, true);
            var impact = new SlamImpactScaling(LandingY + MaximumHeight, LandingY,
                heightLimit: state.SlamHeightLimit(HeightCap));
            var expected = new SlamImpactScaling(LandingY + HeightCap, LandingY);
            Assert.That(impact.Height, Is.EqualTo(HeightCap));
            Assert.That(impact.Damage, Is.EqualTo(expected.Damage));
            Assert.That(impact.Radius, Is.EqualTo(expected.Radius));
            Assert.That(impact.StunDuration, Is.EqualTo(expected.StunDuration));
        }

        /// <summary>A fresh slam or one after an intervening ground contact keeps full height credit.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void SlamWithoutLungeInCurrentAirtime_KeepsFullHeight(bool previousAirtimeLunge)
        {
            var state = new BladeAltState();
            if (previousAirtimeLunge)
            {
                state.StartLunge(LungeCooldown);
                state.Advance(LungeDuration, true);
            }
            state.StartSlam(SlamSafetyCooldown, false);
            var impact = new SlamImpactScaling(LandingY + MaximumHeight, LandingY,
                heightLimit: state.SlamHeightLimit(HeightCap));
            Assert.That(impact.Height, Is.EqualTo(MaximumHeight));
            Assert.That(impact.T, Is.EqualTo(1f));
        }

        /// <summary>An active blade lunge still counts if the grounded flag was sampled before its launch.</summary>
        [Test]
        public void ActiveLunge_CapsSlamEvenBeforeAirtimeMarkerUpdates()
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.Advance(0f, true);
            state.StartSlam(SlamSafetyCooldown, true);
            Assert.That(state.SlamHeightLimit(HeightCap), Is.EqualTo(HeightCap));
        }

        /// <summary>The configurable cap never increases a short fall and a high value disables it.</summary>
        [TestCase(ShortFallHeight, HeightCap, ShortFallHeight)]
        [TestCase(MaximumHeight, LandingY, MaximumHeight)]
        [TestCase(-HeightCap, HeightCap, 0f)]
        public void HeightLimit_OnlyReducesFallCredit(float fallHeight, float cap, float expectedHeight)
        {
            var state = new BladeAltState();
            state.StartLunge(LungeCooldown);
            state.StartSlam(SlamSafetyCooldown, true);
            var impact = new SlamImpactScaling(LandingY + fallHeight, LandingY,
                heightLimit: state.SlamHeightLimit(cap));
            Assert.That(impact.Height, Is.EqualTo(expectedHeight));
        }
    }
}
