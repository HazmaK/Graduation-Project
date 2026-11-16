using NUnit.Framework;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic strike and whiff recovery rules without Unity state or clocks.</summary>
    public sealed class CounterVerdictTimerTests
    {
        private const float ActiveWindow = 0.25f;
        private const float Recovery = 0.5f;
        private const float HalfWindow = ActiveWindow / 2f;
        private const float HalfRecovery = Recovery / 2f;
        private const float Zero = 0f;
        private const float One = 1f;
        private const float Half = 0.5f;
        private const float NegativeDuration = -ActiveWindow;

        /// <summary>A new timer is idle and ready, with no recovery progress.</summary>
        [Test]
        public void ConstructedTimer_IsReadyAndIdle()
        {
            // Arrange / Act
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);

            // Assert
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>Starting opens the active window without beginning recovery.</summary>
        [Test]
        public void ReadyStrike_StartsActiveWindow()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);

            // Act
            bool started = timer.TryStart();

            // Assert
            Assert.That(started, Is.True);
            Assert.That(timer.IsActive, Is.True);
            Assert.That(timer.IsReady, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>A rejected press does not extend the original window.</summary>
        [Test]
        public void UnsuccessfulActiveStrike_RejectsRestartWithoutExtendingWindow()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(HalfWindow);

            // Act
            bool restarted = timer.TryStart();
            timer.Tick(HalfWindow);

            // Assert
            Assert.That(restarted, Is.False);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
        }

        /// <summary>A whiff enters a full recovery precisely when its window expires.</summary>
        [Test]
        public void WhiffWindowExpires_EntersFullRecovery()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.Tick(ActiveWindow);

            // Assert
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.IsReady, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(One));
            Assert.That(timer.TryStart(), Is.False);
        }

        /// <summary>Recovery exposes its remaining seconds and fraction.</summary>
        [Test]
        public void HalfRecoveryElapsed_ReportsHalfRemaining()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Act
            timer.Tick(HalfRecovery);

            // Assert
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(HalfRecovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Half));
            Assert.That(timer.IsReady, Is.False);
        }

        /// <summary>Recovery expiry permits a new strike and clears recovery metrics.</summary>
        [Test]
        public void RecoveryExpires_ReturnsToReady()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Act
            timer.Tick(Recovery);

            // Assert
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
            Assert.That(timer.TryStart(), Is.True);
        }

        /// <summary>Success immediately unlocks another strike while preserving the parry window.</summary>
        [Test]
        public void SuccessDuringWindow_BecomesReadyWithoutClosingWindow()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.NotifySuccess();

            // Assert
            Assert.That(timer.IsActive, Is.True);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>A successful window ends directly in idle.</summary>
        [Test]
        public void SuccessfulWindowExpires_SkipsWhiffRecovery()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.NotifySuccess();

            // Act
            timer.Tick(ActiveWindow);

            // Assert
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>Overshooting a successful window never enters whiff recovery.</summary>
        [Test]
        public void SuccessfulWindowOvershot_RemainsReadyAndCanRestart()
        {
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.NotifySuccess();

            timer.Tick(ActiveWindow + HalfRecovery);

            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.Zero);
            Assert.That(timer.TryStart(), Is.True);
        }

        /// <summary>Several projectile successes share one window and do not extend it.</summary>
        [Test]
        public void RepeatedSuccesses_PreserveOriginalWindowDeadline()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.NotifySuccess();
            timer.Tick(HalfWindow);

            // Act
            timer.NotifySuccess();
            bool stillActive = timer.IsActive;
            timer.Tick(HalfWindow);

            // Assert
            Assert.That(stillActive, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsRecovering, Is.False);
        }

        /// <summary>A re-strike starts a full window and must earn its own success.</summary>
        [Test]
        public void SuccessfulStrikeRestarts_ResetsWindowAndClearsSuccess()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.NotifySuccess();
            timer.Tick(HalfWindow);

            // Act
            bool restarted = timer.TryStart();
            timer.Tick(HalfWindow);
            bool activeAfterOldDeadline = timer.IsActive;
            bool readyWithoutNewSuccess = timer.IsReady;
            timer.Tick(HalfWindow);

            // Assert
            Assert.That(restarted, Is.True);
            Assert.That(activeAfterOldDeadline, Is.True);
            Assert.That(readyWithoutNewSuccess, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(One));
        }

        /// <summary>Zero and negative windows expire immediately into whiff recovery.</summary>
        [TestCase(Zero)]
        [TestCase(NegativeDuration)]
        public void NonPositiveActiveDuration_StartsDirectlyInRecovery(float duration)
        {
            // Arrange
            var timer = new CounterVerdictTimer(duration, Recovery);

            // Act
            bool started = timer.TryStart();

            // Assert
            Assert.That(started, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(One));
        }

        /// <summary>Zero and negative recovery durations do not lock out the next strike.</summary>
        [TestCase(Zero)]
        [TestCase(NegativeDuration)]
        public void NonPositiveRecoveryDuration_WhiffReturnsDirectlyToReady(float duration)
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, duration);
            timer.TryStart();

            // Act
            timer.Tick(ActiveWindow);

            // Assert
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>A zero-length strike and recovery leave no persistent state.</summary>
        [Test]
        public void BothDurationsNonPositive_StartsAndImmediatelyReturnsToIdle()
        {
            // Arrange
            var timer = new CounterVerdictTimer(NegativeDuration, NegativeDuration);

            // Act
            bool started = timer.TryStart();

            // Assert
            Assert.That(started, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>Non-positive deltas cannot move an active window or reverse time.</summary>
        [TestCase(Zero)]
        [TestCase(NegativeDuration)]
        public void NonPositiveTickDuringWindow_DoesNotChangeDeadline(float deltaTime)
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.Tick(deltaTime);
            bool remainedActive = timer.IsActive;
            timer.Tick(ActiveWindow);

            // Assert
            Assert.That(remainedActive, Is.True);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
        }

        /// <summary>Non-positive deltas cannot alter recovery seconds or progress.</summary>
        [TestCase(Zero)]
        [TestCase(NegativeDuration)]
        public void NonPositiveTickDuringRecovery_DoesNothing(float deltaTime)
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Act
            timer.Tick(deltaTime);

            // Assert
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.IsReady, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(One));
        }

        /// <summary>Low frame rates consume elapsed time across both phases.</summary>
        [Test]
        public void TickCrossesWindowBoundary_ConsumesExcessTimeFromRecovery()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.Tick(ActiveWindow + HalfRecovery);

            // Assert
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(HalfRecovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Half));
        }

        /// <summary>Overshooting both phases clamps recovery at zero.</summary>
        [Test]
        public void TickExceedsBothDurations_ReturnsToIdleWithoutNegativeRecovery()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.Tick(ActiveWindow + Recovery + HalfWindow);

            // Assert
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }

        /// <summary>Stale notifications cannot rescue an already missed strike.</summary>
        [Test]
        public void SuccessDuringRecovery_DoesNotUnlockStrike()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Act
            timer.NotifySuccess();

            // Assert
            Assert.That(timer.IsReady, Is.False);
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Recovery));
        }

        /// <summary>A success while idle cannot carry into the next strike.</summary>
        [Test]
        public void SuccessWhileIdle_DoesNotPreapproveNextStrike()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);

            // Act
            timer.NotifySuccess();
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Assert
            Assert.That(timer.IsRecovering, Is.True);
            Assert.That(timer.IsReady, Is.False);
        }

        /// <summary>Reset cancels active or recovering state and clears success.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void ResetDuringStrikeOrRecovery_ReturnsToIdleAndClearsSuccess(bool recovering)
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            if (recovering) timer.Tick(ActiveWindow);
            else timer.NotifySuccess();

            // Act
            timer.Reset();

            // Assert
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(Zero));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
            timer.TryStart();
            Assert.That(timer.IsReady, Is.False);
            timer.Tick(ActiveWindow);
            Assert.That(timer.IsRecovering, Is.True);
        }

        /// <summary>Reconfiguration resets the old phase and uses the replacement durations.</summary>
        [Test]
        public void ConfigureWithNewDurations_ResetsStateAndUsesNewDurations()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();
            timer.Tick(ActiveWindow);

            // Act
            timer.Configure(HalfWindow, HalfRecovery);
            bool readyAfterConfigure = timer.IsReady;
            timer.TryStart();
            timer.Tick(HalfWindow);

            // Assert
            Assert.That(readyAfterConfigure, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.RecoveryRemaining, Is.EqualTo(HalfRecovery));
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(One));
        }

        /// <summary>Configure applies the same duration clamping as construction.</summary>
        [Test]
        public void ConfigureWithNegativeDurations_ClampsBothDurationsToZero()
        {
            // Arrange
            var timer = new CounterVerdictTimer(ActiveWindow, Recovery);
            timer.TryStart();

            // Act
            timer.Configure(NegativeDuration, NegativeDuration);
            bool started = timer.TryStart();

            // Assert
            Assert.That(started, Is.True);
            Assert.That(timer.IsReady, Is.True);
            Assert.That(timer.IsActive, Is.False);
            Assert.That(timer.IsRecovering, Is.False);
            Assert.That(timer.RecoveryNormalized, Is.EqualTo(Zero));
        }
    }
}
