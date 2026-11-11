using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Ghoul Stalker saldırı döngüsü, evre zamanlamaları ve saldırı belirteç testleri.</summary>
    public sealed class GhoulAttackLoopTests
    {
        [SetUp]
        public void SetUp() => ResetTokenPool();

        [TearDown]
        public void TearDown() => ResetTokenPool();

        private static void ResetTokenPool()
        {
            EnemyAttackTokens.SetMaxConcurrent(2);
            while (EnemyAttackTokens.InUse > 0)
            {
                EnemyAttackTokens.Release();
            }
        }

        private static void TickUntil(GhoulAttackLoop loop, Func<bool> condition, float step = 0.01f, int maxSteps = 2000)
        {
            int steps = 0;
            while (!condition() && steps++ < maxSteps)
            {
                loop.Tick(step);
            }
            Assert.That(condition(), Is.True, "Zaman adımı güvenlik sınırına ulaştı.");
        }

        private static void AdvanceToPhase(GhoulAttackLoop loop, GhoulAttackPhase targetPhase, float step = 0.01f, int maxSteps = 2000)
        {
            TickUntil(loop, () => loop.Phase == targetPhase, step, maxSteps);
        }

        private static void TickSteps(GhoulAttackLoop loop, int count, float step = 0.01f)
        {
            for (int i = 0; i < count; i++)
            {
                loop.Tick(step);
            }
        }

        /// <summary>Görüş hattı yoksa menzilde olsa bile hiçbir saldırı seçilmez; görüş varken menzile göre seçilir.</summary>
        [Test]
        public void ChooseAttack_ReturnsNoneWhenCannotSee_AndSelectsKindByDistance()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            Assert.That(loop.ChooseAttack(distance: settings.ClawRange, canSee: false), Is.EqualTo(GhoulAttackKind.None));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMinRange, canSee: false), Is.EqualTo(GhoulAttackKind.None));

            Assert.That(loop.ChooseAttack(distance: settings.ClawRange, canSee: true), Is.EqualTo(GhoulAttackKind.Claw));
            Assert.That(loop.ChooseAttack(distance: settings.ClawRange + 0.1f, canSee: true), Is.EqualTo(GhoulAttackKind.None));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMinRange, canSee: true), Is.EqualTo(GhoulAttackKind.Lunge));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMaxRange, canSee: true), Is.EqualTo(GhoulAttackKind.Lunge));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMaxRange + 1.0f, canSee: true), Is.EqualTo(GhoulAttackKind.None));
        }

        /// <summary>Bekleme süreleri saldırı seçimini engeller ve TickCooldowns ile süreler sıfırlanınca saldırı açılır.</summary>
        [Test]
        public void ChooseAttack_PrefersClawAndRespectsCooldowns()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Claw);
            loop.Cancel(0f);
            Assert.That(loop.ClawCooldownRemaining, Is.GreaterThan(0f));
            Assert.That(loop.ChooseAttack(distance: settings.ClawRange, canSee: true), Is.EqualTo(GhoulAttackKind.None));

            loop.TickCooldowns(loop.ClawCooldownRemaining);
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(0f));
            Assert.That(loop.ChooseAttack(distance: settings.ClawRange, canSee: true), Is.EqualTo(GhoulAttackKind.Claw));

            loop.Begin(GhoulAttackKind.Lunge);
            loop.Cancel(0f);
            Assert.That(loop.LungeCooldownRemaining, Is.GreaterThan(0f));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMinRange, canSee: true), Is.EqualTo(GhoulAttackKind.None));

            loop.TickCooldowns(loop.LungeCooldownRemaining);
            Assert.That(loop.LungeCooldownRemaining, Is.EqualTo(0f));
            Assert.That(loop.ChooseAttack(distance: settings.LungeMinRange, canSee: true), Is.EqualTo(GhoulAttackKind.Lunge));
        }

        /// <summary>Pençe kombosu evre sırasını tam tamamlar; Glint ve StrikeStarted tam 2 kez tetiklenir.</summary>
        [Test]
        public void ClawCombo_CompletesFullPhaseSequence_FiresGlintAndStrikeEventsTwice()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);
            var visitedPhases = new List<GhoulAttackPhase>();
            int glintCount = 0;
            int strikeCount = 0;

            loop.PhaseChanged += p => visitedPhases.Add(p);
            loop.Glint += () => glintCount++;
            loop.StrikeStarted += () => strikeCount++;

            loop.Begin(GhoulAttackKind.Claw);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawWindup));
            Assert.That(loop.SwipeIndex, Is.EqualTo(0));

            TickUntil(loop, () => !loop.IsActive);

            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
            Assert.That(glintCount, Is.EqualTo(2), "Glint her savurma öncesi birer kez (toplam 2) tetiklenmeli.");
            Assert.That(strikeCount, Is.EqualTo(2), "StrikeStarted her vuruş evresine girişte (toplam 2) tetiklenmeli.");

            GhoulAttackPhase[] expectedPhases =
            {
                GhoulAttackPhase.ClawWindup,
                GhoulAttackPhase.ClawStrike,
                GhoulAttackPhase.ClawWindup,
                GhoulAttackPhase.ClawStrike,
                GhoulAttackPhase.ClawRecover,
                GhoulAttackPhase.None
            };
            Assert.That(visitedPhases, Is.EqualTo(expectedPhases));
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(settings.ClawCooldown));
        }

        /// <summary>Hasar çarpanı ilk savurma, ikinci savurma ve atılma için ayarlara uygun ölçeklenir.</summary>
        [Test]
        public void DamageScale_MatchesPhaseAndSwipeIndex()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);
            Assert.That(loop.CurrentDamageScale, Is.EqualTo(0f));

            loop.Begin(GhoulAttackKind.Claw);
            Assert.That(loop.SwipeIndex, Is.EqualTo(0));
            Assert.That(loop.CurrentDamageScale, Is.EqualTo(settings.ClawFirstDamageScale));

            TickUntil(loop, () => loop.SwipeIndex == 1);
            Assert.That(loop.CurrentDamageScale, Is.EqualTo(settings.ClawSecondDamageScale));

            loop.ResetAll();
            loop.Begin(GhoulAttackKind.Lunge);
            Assert.That(loop.CurrentDamageScale, Is.EqualTo(settings.LungeDamageScale));

            loop.ResetAll();
            Assert.That(loop.CurrentDamageScale, Is.EqualTo(0f));
        }

        /// <summary>HitWindowOpen yalnızca vuruş evresinde açıktır ve ReportHit sonrasında derhal kapanır.</summary>
        [Test]
        public void HitWindowOpen_ClosesAfterReportHit()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Claw);
            Assert.That(loop.HitWindowOpen, Is.False, "Hazırlık evresinde isabet penceresi kapalı olmalı.");

            AdvanceToPhase(loop, GhoulAttackPhase.ClawStrike);
            Assert.That(loop.HitWindowOpen, Is.True, "ClawStrike başlangıcında isabet penceresi açık olmalı.");

            loop.ReportHit();
            Assert.That(loop.HitWindowOpen, Is.False, "ReportHit çağrısından sonra isabet penceresi kapanmalı.");
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawStrike), "Pençe vuruş evresi süresi boyunca devam etmeli.");
        }

        /// <summary>Atılma saldırısı başladığında pençe bekleme süresini en az 0.4 saniye yapar.</summary>
        [Test]
        public void Lunge_Begin_SetsClawCooldownAtLeastThreshold()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(0f));

            loop.Begin(GhoulAttackKind.Lunge);
            Assert.That(loop.ClawCooldownRemaining, Is.GreaterThanOrEqualTo(0.4f));

            loop.ResetAll();
            loop.Begin(GhoulAttackKind.Claw);
            loop.Cancel(0f);
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(settings.ClawCooldown));

            loop.Begin(GhoulAttackKind.Lunge);
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(settings.ClawCooldown));
        }

        /// <summary>Atılma isabetinde kısa, zaman aşımında (ıskalama) uzun toparlanma süresi uygulanır.</summary>
        [Test]
        public void Lunge_ReportHitVsWhiff_UsesAppropriateRecoverDurations()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            // 1. İsabet toparlanması
            loop.Begin(GhoulAttackKind.Lunge);
            AdvanceToPhase(loop, GhoulAttackPhase.Lunging);
            loop.ReportHit();
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.LungeRecover));

            int hitRecoverSteps = (int)(settings.LungeHitRecover / 0.01f);
            TickSteps(loop, hitRecoverSteps - 1);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.LungeRecover));

            TickSteps(loop, 2);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
            Assert.That(loop.LungeCooldownRemaining, Is.EqualTo(settings.LungeCooldown));

            // 2. Iskalama (zaman aşımı) toparlanması
            loop.ResetAll();
            loop.Begin(GhoulAttackKind.Lunge);
            AdvanceToPhase(loop, GhoulAttackPhase.Lunging);

            AdvanceToPhase(loop, GhoulAttackPhase.LungeRecover);
            TickSteps(loop, hitRecoverSteps + 5);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.LungeRecover),
                "Iskalama toparlanması isabet süresinden uzun sürmeli.");

            TickUntil(loop, () => loop.Phase == GhoulAttackPhase.None);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
        }

        /// <summary>ReportLungeBlocked havadayken çağrıldığında ıskalama toparlanma evresine geçirir.</summary>
        [Test]
        public void ReportLungeBlocked_TransitionsLungingToLungeRecoverWithWhiffDuration()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Lunge);
            AdvanceToPhase(loop, GhoulAttackPhase.Lunging);

            loop.ReportLungeBlocked();
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.LungeRecover));
            Assert.That(loop.LungeCooldownRemaining, Is.EqualTo(settings.LungeCooldown));

            int hitRecoverSteps = (int)(settings.LungeHitRecover / 0.01f);
            TickSteps(loop, hitRecoverSteps + 5);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.LungeRecover));
        }

        /// <summary>Cancel aktif saldırı türünü bekleme süresine alır ve evreyi None yapar.</summary>
        [Test]
        public void Cancel_PutsInProgressKindOnCooldownAndReturnsToNone()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Claw);
            loop.Cancel(0f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
            Assert.That(loop.Kind, Is.EqualTo(GhoulAttackKind.None));
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(settings.ClawCooldown));

            loop.ResetAll();
            loop.Begin(GhoulAttackKind.Lunge);
            loop.Cancel(0.5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
            float expectedLunge = settings.LungeCooldown * (1f + settings.CooldownJitter * 0.5f);
            Assert.That(loop.LungeCooldownRemaining, Is.EqualTo(expectedLunge).Within(0.001f));

            loop.Cancel(0f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
        }

        /// <summary>Denge hasarı zaman penceresi içinde birikir ve eşik aşıldığında sendeleme tetikler.</summary>
        [Test]
        public void Poise_AccumulatesWithinWindow_StaggersWhenThresholdReached()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            Assert.That(loop.RegisterDamageForPoise(40f, isCritical: false, time: 0.1f), Is.False);
            Assert.That(loop.RegisterDamageForPoise(35f, isCritical: false, time: 0.5f), Is.True);
            Assert.That(loop.RegisterDamageForPoise(35f, isCritical: false, time: 0.6f), Is.False);
        }

        /// <summary>Denge zaman penceresi aşıldığında önceki hasar birikimi sıfırlanır.</summary>
        [Test]
        public void Poise_ResetsAccumulator_WhenWindowExpires()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            Assert.That(loop.RegisterDamageForPoise(40f, isCritical: false, time: 0.0f), Is.False);
            Assert.That(loop.RegisterDamageForPoise(35f, isCritical: false, time: 1.5f), Is.False);
            Assert.That(loop.RegisterDamageForPoise(40f, isCritical: false, time: 1.8f), Is.True);
        }

        /// <summary>ClawStrike sırasında süper zırh (hyper-armor) kritik olmayan hasarlarda sendelemeyi engeller.</summary>
        [Test]
        public void Poise_HyperArmorDuringClawStrike_ResistsStagger()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Claw);
            AdvanceToPhase(loop, GhoulAttackPhase.ClawStrike);
            Assert.That(loop.IsCommitted, Is.True);

            Assert.That(loop.RegisterDamageForPoise(100f, isCritical: false, time: 0.1f), Is.False);
        }

        /// <summary>Kritik vuruşlar Lunging hariç her durumda derhal sendeleme üretir.</summary>
        [Test]
        public void Poise_CriticalHitStaggersExceptDuringLunging()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            Assert.That(loop.RegisterDamageForPoise(1f, isCritical: true, time: 0.0f), Is.True);

            loop.Begin(GhoulAttackKind.Claw);
            Assert.That(loop.RegisterDamageForPoise(1f, isCritical: true, time: 0.1f), Is.True);

            loop.ResetAll();
            loop.Begin(GhoulAttackKind.Lunge);
            AdvanceToPhase(loop, GhoulAttackPhase.Lunging);
            Assert.That(loop.RegisterDamageForPoise(100f, isCritical: true, time: 0.2f), Is.False);
        }

        /// <summary>Büyük zaman adımı (ör. 5 saniye) evre geçişlerini ve olayları atlamaz.</summary>
        [Test]
        public void Tick_LargeDeltaTime_DoesNotSkipPhasesOrEvents()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);
            var visitedPhases = new List<GhoulAttackPhase>();
            int glints = 0;
            int strikes = 0;

            loop.PhaseChanged += p => visitedPhases.Add(p);
            loop.Glint += () => glints++;
            loop.StrikeStarted += () => strikes++;

            loop.Begin(GhoulAttackKind.Claw);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawWindup));

            loop.Tick(5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawStrike));
            Assert.That(glints, Is.EqualTo(1));
            Assert.That(strikes, Is.EqualTo(1));

            loop.Tick(5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawWindup));
            Assert.That(loop.SwipeIndex, Is.EqualTo(1));

            loop.Tick(5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawStrike));
            Assert.That(glints, Is.EqualTo(2));
            Assert.That(strikes, Is.EqualTo(2));

            loop.Tick(5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.ClawRecover));

            loop.Tick(5f);
            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));

            GhoulAttackPhase[] expectedPhases =
            {
                GhoulAttackPhase.ClawWindup,
                GhoulAttackPhase.ClawStrike,
                GhoulAttackPhase.ClawWindup,
                GhoulAttackPhase.ClawStrike,
                GhoulAttackPhase.ClawRecover,
                GhoulAttackPhase.None
            };
            Assert.That(visitedPhases, Is.EqualTo(expectedPhases));
        }

        /// <summary>ResetAll evre, bekleme süreleri ve denge birikimini temiz başlangıç durumuna sıfırlar.</summary>
        [Test]
        public void ResetAll_RestoresCleanInitialState()
        {
            var settings = new GhoulAttackSettings();
            var loop = new GhoulAttackLoop(settings);

            loop.Begin(GhoulAttackKind.Claw);
            loop.Tick(0.1f);
            loop.Cancel(0.5f);
            loop.SetCooldownJitter(0.8f);
            loop.RegisterDamageForPoise(50f, isCritical: false, time: 1.0f);

            loop.ResetAll();

            Assert.That(loop.Phase, Is.EqualTo(GhoulAttackPhase.None));
            Assert.That(loop.Kind, Is.EqualTo(GhoulAttackKind.None));
            Assert.That(loop.IsActive, Is.False);
            Assert.That(loop.ClawCooldownRemaining, Is.EqualTo(0f));
            Assert.That(loop.LungeCooldownRemaining, Is.EqualTo(0f));
            Assert.That(loop.SwipeIndex, Is.EqualTo(0));
            Assert.That(loop.RegisterDamageForPoise(40f, isCritical: false, time: 1.1f), Is.False);
        }

        /// <summary>EnemyAttackTokens eşzamanlı sınır uygular, bırakma sayısı sıfırın altına inmez ve kapasite en az 1'dir.</summary>
        [Test]
        public void EnemyAttackTokens_EnforcesLimit_NeverReleasesBelowZero_AndClampsMax()
        {
            EnemyAttackTokens.SetMaxConcurrent(2);
            Assert.That(EnemyAttackTokens.MaxConcurrent, Is.EqualTo(2));
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(0));

            Assert.That(EnemyAttackTokens.TryAcquire(), Is.True);
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(1));

            Assert.That(EnemyAttackTokens.TryAcquire(), Is.True);
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(2));

            Assert.That(EnemyAttackTokens.TryAcquire(), Is.False, "Kapasite doluyken yeni belirteç alınamaz.");
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(2));

            EnemyAttackTokens.Release();
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(1));

            EnemyAttackTokens.Release();
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(0));

            EnemyAttackTokens.Release();
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(0), "Kullanım sayısı asla 0'ın altına düşmez.");

            EnemyAttackTokens.SetMaxConcurrent(0);
            Assert.That(EnemyAttackTokens.MaxConcurrent, Is.EqualTo(1), "Kapasite en az 1'e kenetlenir.");
        }
    }
}
