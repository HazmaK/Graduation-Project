using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Pause;
using System.Reflection;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic timing, token intent, vulnerability and reset coverage.</summary>
    public sealed class PenitentAttackLoopTests
    {
        private const float Zero = 0f;
        private const float Epsilon = 0.0001f;
        private const float Step = 0.01f;
        private const float BreakDuration = 2f;
        private const float LargeStep = 100f;
        private const float MinimumJitter = -1f;
        private const float MaximumJitter = 1f;
        private const float BeyondJitterBounds = 5f;
        private const float Extension = 0.5f;
        private const float ExtensionCap = 3f;
        private const int MaximumSteps = 2000;

        /// <summary>Every phase lasts its configured duration and resets its elapsed clock.</summary>
        [Test]
        public void Slam_VisitsTimedPhasesInOrder()
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f), Is.True);
            AssertPhaseDuration(loop, PenitentAttackPhase.SlamWindup, settings.SlamWindup, PenitentAttackPhase.SlamStrike);
            Assert.That(loop.StrikeStartedThisTick, Is.True);
            AssertPhaseDuration(loop, PenitentAttackPhase.SlamStrike, settings.SlamStrike, PenitentAttackPhase.SlamRecover);
            Assert.That(loop.SlamEndedThisTick, Is.True);
            Assert.That(loop.WantsToken, Is.False);
            Assert.That(loop.CooldownRemaining, Is.EqualTo(settings.SlamCooldown).Within(Epsilon));
            AssertPhaseDuration(loop, PenitentAttackPhase.SlamRecover, settings.SlamRecover, PenitentAttackPhase.Advance);
        }

        /// <summary>A strike flag is present for one tick only, with no skipped phases on long frames.</summary>
        [Test]
        public void StrikeFlag_FiresExactlyOnce_AndLargeStepsPreserveEveryPhase()
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f);
            int strikes = 0;
            Tick(loop, LargeStep);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.SlamStrike));
            if (loop.StrikeStartedThisTick) strikes++;
            Tick(loop, Zero);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
            Tick(loop, LargeStep);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.SlamRecover));
            if (loop.StrikeStartedThisTick) strikes++;
            Assert.That(loop.SlamEndedThisTick, Is.True);
            Tick(loop, Zero);
            Assert.That(loop.SlamEndedThisTick, Is.False);
            Tick(loop, LargeStep);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(strikes, Is.EqualTo(1));
        }

        /// <summary>Only Advance and SlamWindup block frontal damage.</summary>
        [TestCase(PenitentAttackPhase.Advance, true)]
        [TestCase(PenitentAttackPhase.SlamWindup, true)]
        [TestCase(PenitentAttackPhase.SlamStrike, false)]
        [TestCase(PenitentAttackPhase.SlamRecover, false)]
        [TestCase(PenitentAttackPhase.GuardBroken, false)]
        public void GuardUp_IsLimitedToAdvanceAndWindup(PenitentAttackPhase phase, bool expected)
        {
            var loop = CreateAtPhase(phase);
            Assert.That(loop.IsGuardUp, Is.EqualTo(expected));
            Assert.That(loop.WantsToken, Is.EqualTo(phase == PenitentAttackPhase.SlamWindup
                || phase == PenitentAttackPhase.SlamStrike));
        }

        /// <summary>A token, a target in range and line of sight are all required to begin.</summary>
        [Test]
        public void Begin_RejectsMissingTokenRangeOrSight()
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, true, false, 0f), Is.False);
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange + Step, true, true, 0f), Is.False);
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, false, true, 0f), Is.False);
            loop.Tick(Step, settings.SlamTriggerRange, true, false, 0f);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(loop.WantsToken, Is.False);
            loop.Tick(Step, settings.SlamTriggerRange, true, true, 0f);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.SlamWindup));
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f), Is.False);
        }

        /// <summary>Completed or cancelled attacks must wait for their cooldown.</summary>
        [Test]
        public void Cooldown_PreventsAnotherSlamUntilExpired()
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f);
            loop.Cancel();
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f), Is.False);
            Tick(loop, settings.SlamCooldown - Step);
            Assert.That(loop.CanBeginSlam(settings.SlamTriggerRange, true, 0f), Is.False);
            Tick(loop, Step + Epsilon);
            Assert.That(loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f), Is.True);
        }

        /// <summary>Cancelling a telegraph returns to Advance and relinquishes token intent.</summary>
        [Test]
        public void Cancel_DuringWindupClearsStrikeAndTokenIntent()
        {
            var loop = CreateAtPhase(PenitentAttackPhase.SlamWindup);
            Tick(loop, Step);
            loop.Cancel();
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(loop.PhaseTime, Is.EqualTo(Zero));
            Assert.That(loop.WantsToken, Is.False);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
            Assert.That(loop.SlamEndedThisTick, Is.True);
            Tick(loop, LargeStep);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
            Assert.That(loop.SlamEndedThisTick, Is.False);
        }

        /// <summary>Losing an already owned token cancels both windup and strike.</summary>
        [TestCase(PenitentAttackPhase.SlamWindup)]
        [TestCase(PenitentAttackPhase.SlamStrike)]
        public void Tick_WithoutOwnedTokenCancelsCommittedSlam(PenitentAttackPhase phase)
        {
            var loop = CreateAtPhase(phase);
            loop.Tick(Step, Zero, true, false, 0f);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(loop.WantsToken, Is.False);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
        }

        /// <summary>An external guard break interrupts every possible phase and later restores Advance.</summary>
        [TestCase(PenitentAttackPhase.Advance)]
        [TestCase(PenitentAttackPhase.SlamWindup)]
        [TestCase(PenitentAttackPhase.SlamStrike)]
        [TestCase(PenitentAttackPhase.SlamRecover)]
        [TestCase(PenitentAttackPhase.GuardBroken)]
        public void BreakGuard_FromEveryPhaseOpensGuardAndReturnsToAdvance(PenitentAttackPhase phase)
        {
            var loop = CreateAtPhase(phase);
            loop.BreakGuard(BreakDuration);
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.GuardBroken));
            Assert.That(loop.IsGuardUp, Is.False);
            Assert.That(loop.WantsToken, Is.False);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
            Assert.That(loop.TryBeginSlam(Zero, true, true, 0f), Is.False);
            AssertPhaseDuration(loop, PenitentAttackPhase.GuardBroken, BreakDuration, PenitentAttackPhase.Advance);
            Assert.That(loop.IsGuardUp, Is.True);
        }

        /// <summary>Cooldown jitter is deterministic and clamped to its configured bounds.</summary>
        [TestCase(MinimumJitter, MinimumJitter)]
        [TestCase(Zero, Zero)]
        [TestCase(MaximumJitter, MaximumJitter)]
        [TestCase(BeyondJitterBounds, MaximumJitter)]
        [TestCase(-BeyondJitterBounds, MinimumJitter)]
        public void Jitter_UsesInjectedClampedValue(float supplied, float clamped)
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f, supplied);
            Tick(loop, settings.SlamWindup);
            Tick(loop, settings.SlamStrike);
            float expected = settings.SlamCooldown * (1f + settings.CooldownJitter * clamped);
            Assert.That(loop.CooldownRemaining, Is.EqualTo(expected).Within(Epsilon));
            Assert.That(loop.CooldownRemaining, Is.InRange(settings.SlamCooldown * (1f - settings.CooldownJitter),
                settings.SlamCooldown * (1f + settings.CooldownJitter)));
        }

        /// <summary>Damage extensions are bounded and never shorten an accepted external stun.</summary>
        [Test]
        public void GuardBreakExtension_IsCappedAndPreservesLongerExistingBreak()
        {
            var loop = CreateAtPhase(PenitentAttackPhase.SlamRecover);
            loop.ExtendGuardBreak(Extension, ExtensionCap);
            Assert.That(loop.PhaseRemaining, Is.EqualTo(Extension));
            for (int i = 0; i < MaximumSteps; i++) loop.ExtendGuardBreak(Extension, ExtensionCap);
            Assert.That(loop.PhaseRemaining, Is.EqualTo(ExtensionCap));
            loop.BreakGuard(LargeStep);
            loop.ExtendGuardBreak(Extension, ExtensionCap);
            Assert.That(loop.PhaseRemaining, Is.EqualTo(LargeStep));
        }

        /// <summary>Reset removes phase timers, cooldown, flags and jitter from any phase.</summary>
        [TestCase(PenitentAttackPhase.Advance)]
        [TestCase(PenitentAttackPhase.SlamWindup)]
        [TestCase(PenitentAttackPhase.SlamStrike)]
        [TestCase(PenitentAttackPhase.SlamRecover)]
        [TestCase(PenitentAttackPhase.GuardBroken)]
        public void Reset_RestoresReadyGuardedSpawn(PenitentAttackPhase phase)
        {
            var loop = CreateAtPhase(phase);
            loop.Reset();
            Assert.That(loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
            Assert.That(loop.PhaseTime, Is.EqualTo(Zero));
            Assert.That(loop.PhaseRemaining, Is.EqualTo(Zero));
            Assert.That(loop.CooldownRemaining, Is.EqualTo(Zero));
            Assert.That(loop.IsGuardUp, Is.True);
            Assert.That(loop.WantsToken, Is.False);
            Assert.That(loop.StrikeStartedThisTick, Is.False);
            Assert.That(loop.SlamEndedThisTick, Is.False);
            Assert.That(loop.TryBeginSlam(Zero, true, true, 0f), Is.True);
        }

        /// <summary>The new charge starts at 5.5 metres after a 1.1 second telegraph.</summary>
        [Test]
        public void Defaults_MatchAreaSlamDesign()
        {
            var settings = new PenitentAttackSettings();
            Assert.That(settings.SlamTriggerRange, Is.EqualTo(5.5f));
            Assert.That(settings.SlamWindup, Is.EqualTo(1.1f));
            Assert.That(settings.SlamTriggerHalfAngle, Is.EqualTo(35f));
        }

        /// <summary>Both signed angular boundaries work; targets outside the cone cannot consume a slam.</summary>
        [TestCase(35f, true)]
        [TestCase(-35f, true)]
        [TestCase(35.01f, false)]
        [TestCase(-35.01f, false)]
        [TestCase(180f, false)]
        public void Trigger_RequiresForwardCone(float angle, bool expected)
        {
            var loop = new PenitentAttackLoop(new PenitentAttackSettings());
            Assert.That(loop.CanBeginSlam(5f, true, angle), Is.EqualTo(expected));
            Assert.That(loop.TryBeginSlam(5f, true, true, angle), Is.EqualTo(expected));
            loop.Reset();
            loop.Tick(0f, 5f, true, true, angle);
            Assert.That(loop.Phase, Is.EqualTo(expected ? PenitentAttackPhase.SlamWindup : PenitentAttackPhase.Advance));
        }

        /// <summary>Cached XZ boundaries are inclusive; jump height is hit but balconies are excluded.</summary>
        [TestCase(0f, 0f, 0f, true)]
        [TestCase(2.25f, 0f, 6.5f, true)]
        [TestCase(-2.25f, -2.5f, 6.5f, true)]
        [TestCase(0f, 2.5f, 3f, true)]
        [TestCase(0f, 3f, 3f, false)]
        [TestCase(0f, -3f, 3f, false)]
        [TestCase(2.251f, 0f, 3f, false)]
        [TestCase(0f, 0f, -0.001f, false)]
        [TestCase(0f, 0f, 6.501f, false)]
        public void AreaRectangle_UsesCachedFrameAndHeight(float x, float y, float z, bool expected)
        {
            Vector3 origin = new Vector3(10f, 4f, -6f);
            Vector3 forward = Vector3.right;
            Vector3 right = Vector3.back;
            Vector3 feet = origin + right * x + Vector3.up * y + forward * z;
            Assert.That(PenitentAttackLoop.IsInsideSlamArea(feet, origin, forward, 6.5f, 4.5f, 2.5f), Is.EqualTo(expected));
        }

        private static PenitentAttackLoop CreateAtPhase(PenitentAttackPhase phase)
        {
            var settings = new PenitentAttackSettings();
            var loop = new PenitentAttackLoop(settings);
            if (phase == PenitentAttackPhase.Advance) return loop;
            if (phase == PenitentAttackPhase.GuardBroken)
            {
                loop.BreakGuard(BreakDuration);
                return loop;
            }
            loop.TryBeginSlam(settings.SlamTriggerRange, true, true, 0f, MaximumJitter);
            if (phase == PenitentAttackPhase.SlamWindup) return loop;
            Tick(loop, settings.SlamWindup);
            if (phase == PenitentAttackPhase.SlamStrike) return loop;
            Tick(loop, settings.SlamStrike);
            return loop;
        }

        private static void Tick(PenitentAttackLoop loop, float dt)
        {
            // No sight prevents automatic initiation; active attacks retain their owned token.
            loop.Tick(dt, Zero, false, loop.WantsToken, 0f);
        }

        private static void AssertPhaseDuration(PenitentAttackLoop loop, PenitentAttackPhase phase,
            float duration, PenitentAttackPhase next)
        {
            Assert.That(loop.Phase, Is.EqualTo(phase));
            Assert.That(loop.PhaseTime, Is.EqualTo(Zero));
            Tick(loop, duration - Step);
            Assert.That(loop.Phase, Is.EqualTo(phase));
            Assert.That(loop.PhaseTime, Is.EqualTo(duration - Step).Within(Epsilon));
            Tick(loop, Step + Epsilon);
            Assert.That(loop.Phase, Is.EqualTo(next));
            Assert.That(loop.PhaseTime, Is.EqualTo(Zero));
        }
    }

    /// <summary>Driver regressions for damage routing, fixed slam commitment and dead-player cancellation.</summary>
    public sealed class PenitentDriverTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private PenitentAI _ai;
        private PenitentAttackLoop _loop;

        /// <summary>Creates an inactive driver with isolated combat state and no scene dependencies.</summary>
        [SetUp]
        public void CreateDriver()
        {
            _root = new GameObject("PenitentDriverTest");
            _root.SetActive(false);
            _ai = _root.AddComponent<PenitentAI>();
            _loop = new PenitentAttackLoop(new PenitentAttackSettings());
            Set(_ai, "_loop", _loop);
            Set(_ai, "_health", _ai.MaxHealth);
        }

        /// <summary>Destroys the isolated driver.</summary>
        [TearDown]
        public void DestroyDriver()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>Both body and shield routes ignore a touched collider's position for melee damage.</summary>
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void Melee_UsesAttackerDirection(bool shield, bool frontal)
        {
            Vector3 point = frontal ? Vector3.back : Vector3.forward;
            Vector3 normal = frontal ? Vector3.forward : Vector3.back;
            MeleeDamageScope.Begin();
            try
            {
                if (shield) _ai.OnShieldHit(10f, point, normal, false);
                else _ai.TakeDamage(10f, point, normal);
            }
            finally { MeleeDamageScope.End(); }
            Assert.That(_ai.CurrentHealth, Is.EqualTo(_ai.MaxHealth - (frontal ? 0f : 10f)));
        }

        /// <summary>Precise ranged hits multiply damage and keep a raised guard closed, without a stagger.</summary>
        [Test]
        public void RangedWeakPoint_DealsCriticalDamageWithoutGuardBreak()
        {
            _ai.OnWeakPointHit(30f, Vector3.forward, Vector3.forward, false);
            Assert.That(_ai.CurrentHealth, Is.EqualTo(_ai.MaxHealth - 60f));
            Assert.That(_ai.IsGuardUp, Is.True);
            Assert.That(_ai.IsStunned, Is.False);
            Assert.That(_ai.StunSequence, Is.Zero);
        }

        /// <summary>Melee cannot claim the ranged weak point multiplier or bypass its frontal guard.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void MeleeWeakPoint_UsesOrdinaryDamagePolicy(bool frontal)
        {
            MeleeDamageScope.Begin();
            try { _ai.OnWeakPointHit(10f, Vector3.forward, frontal ? Vector3.forward : Vector3.back, false); }
            finally { MeleeDamageScope.End(); }
            Assert.That(_ai.CurrentHealth, Is.EqualTo(_ai.MaxHealth - (frontal ? 0f : 10f)));
        }

        /// <summary>Missing health and a dead player both cancel a committed windup and release its actual token.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void InvalidPlayer_CancelsWindupAndReleasesToken(bool missingHealth)
        {
            GameObject player = new GameObject("DeadPlayerTest");
            player.SetActive(false);
            IPauseService previousPause = GamePause.Service;
            GamePause.SetService(null);
            int tokensBefore = EnemyAttackTokens.InUse;
            int capacityBefore = EnemyAttackTokens.MaxConcurrent;
            EnemyAttackTokens.SetMaxConcurrent(tokensBefore + 1);
            try
            {
                if (!missingHealth)
                {
                    PlayerHealth health = player.AddComponent<PlayerHealth>();
                    Set(health, "_isDead", true);
                    Set(_ai, "_playerHealth", health);
                }
                Set(_ai, "_playerTransform", player.transform);
                Set(_ai, "_alerted", true);
                Set(_ai, "_lostSightRemaining", 3f);
                Assert.That(EnemyAttackTokens.TryAcquire(), Is.True);
                Set(_ai, "_hasAttackToken", true);
                Assert.That(_loop.TryBeginSlam(1f, true, true, 0f), Is.True);
                typeof(PenitentAI).GetMethod("Update", PrivateInstance).Invoke(_ai, null);
                Assert.That(_loop.Phase, Is.EqualTo(PenitentAttackPhase.Advance));
                Assert.That(_loop.WantsToken, Is.False);
                Assert.That(_loop.StrikeStartedThisTick, Is.False);
                Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(tokensBefore));
                Assert.That(Get<bool>(_ai, "_alerted"), Is.False);
                Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                typeof(PenitentAI).GetMethod("CancelSlam", PrivateInstance).Invoke(_ai, null);
                EnemyAttackTokens.SetMaxConcurrent(capacityBefore);
                GamePause.SetService(previousPause);
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>Committed phases freeze facing and position, including recovery and residual velocity.</summary>
        [TestCase(PenitentAttackPhase.SlamWindup)]
        [TestCase(PenitentAttackPhase.SlamStrike)]
        [TestCase(PenitentAttackPhase.SlamRecover)]
        public void CommittedSlam_LocksMovementAndFacing(PenitentAttackPhase phase)
        {
            _loop.TryBeginSlam(1f, true, true, 0f);
            if (phase != PenitentAttackPhase.SlamWindup) _loop.Tick(2f, 1f, true, true, 0f);
            if (phase == PenitentAttackPhase.SlamRecover) _loop.Tick(1f, 1f, true, true, 0f);
            Set(_ai, "_alerted", true);
            Set(_ai, "_velocity", new Vector3(5f, 5f, 5f));
            typeof(PenitentAI).GetMethod("MoveAndTurn", PrivateInstance).Invoke(_ai, new object[] { Vector3.right * 4f, 1f });
            Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(_root.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(Get<Vector3>(_ai, "_velocity"), Is.EqualTo(Vector3.zero));
        }

        /// <summary>Elite scaling applies to the single area damage value and resets on respawn.</summary>
        [Test]
        public void Elite_ScalesAreaDamageAndResets()
        {
            _ai.ApplyEliteScaling(2f, 1.5f, 2f);
            Assert.That(Get<float>(_ai, "slamDamage"), Is.EqualTo(70f));
            _ai.Respawn();
            Assert.That(Get<float>(_ai, "slamDamage"), Is.EqualTo(35f));
        }

        /// <summary>The cached rectangle survives root rotation and delivers damage only once at impact.</summary>
        [TestCase(0f, 0f, 3f, true)]
        [TestCase(0f, 2f, 3f, true)]
        [TestCase(0f, 3f, 3f, false)]
        [TestCase(3f, 0f, 3f, false)]
        [TestCase(0f, 0f, -1f, false)]
        public void AreaImpact_UsesCachedFrameAndDamagesOnce(float x, float y, float z, bool hit)
        {
            GameObject player = new GameObject("AreaTarget");
            player.SetActive(false);
            IPauseService previousPause = GamePause.Service;
            GamePause.SetService(null);
            try
            {
                PlayerHealth health = player.AddComponent<PlayerHealth>();
                Set(health, "_currentHealth", 100f);
                Set(_ai, "_playerHealth", health);
                Set(_ai, "_playerTransform", player.transform);
                _loop.TryBeginSlam(1f, true, true, 0f);
                typeof(PenitentAI).GetMethod("OnPhaseChanged", PrivateInstance).Invoke(_ai, null);
                _root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                player.transform.position = new Vector3(x, y, z);
                _loop.Tick(2f, 1f, true, true, 0f);
                MethodInfo impact = typeof(PenitentAI).GetMethod("PerformSlam", PrivateInstance);
                impact.Invoke(_ai, null);
                impact.Invoke(_ai, null);
                Assert.That(health.CurrentHealth, Is.EqualTo(hit ? 65f : 100f));
            }
            finally
            {
                GamePause.SetService(previousPause);
                Object.DestroyImmediate(player);
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }
}
