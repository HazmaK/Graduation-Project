using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Channel isolation, caller capacities and legacy melee compatibility.</summary>
    public sealed class AttackTokenChannelTests
    {
        [SetUp]
        public void SetUp() => ResetPools();

        [TearDown]
        public void TearDown() => ResetPools();

        private static void ResetPools() => typeof(EnemyAttackTokens)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [Test]
        public void Channels_UseIndependentDefaultBudgets()
        {
            Assert.That(EnemyAttackTokens.MaxOf(AttackTokenChannel.Melee), Is.EqualTo(2));
            Assert.That(EnemyAttackTokens.MaxOf(AttackTokenChannel.Slam), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.MaxOf(AttackTokenChannel.RangedHeavy), Is.EqualTo(2));
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Slam, 0), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Slam, 0), Is.False);
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.Melee), Is.Zero);
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.RangedHeavy), Is.Zero);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 0), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, 0), Is.True);
            EnemyAttackTokens.Release(AttackTokenChannel.Melee);
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.Slam), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.RangedHeavy), Is.EqualTo(1));
        }

        [Test]
        public void PerCallLimit_CanExceedDefaultWithoutChangingGlobalCapacity()
        {
            for (int i = 0; i < 3; i++)
                Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 3), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 3), Is.False);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 0), Is.False);
            Assert.That(EnemyAttackTokens.MaxOf(AttackTokenChannel.Melee), Is.EqualTo(2));
        }

        [Test]
        public void PerCallLimit_CanRestrictDefault()
        {
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, 1), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, 1), Is.False);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, -1), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, 0), Is.False);
        }

        [Test]
        public void LegacyApi_UsesMeleeChannel()
        {
            EnemyAttackTokens.SetMaxConcurrent(4);
            Assert.That(EnemyAttackTokens.MaxConcurrent, Is.EqualTo(EnemyAttackTokens.MaxOf(AttackTokenChannel.Melee)));
            Assert.That(EnemyAttackTokens.TryAcquire(), Is.True);
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.Melee), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.TryAcquire(AttackTokenChannel.Melee, 0), Is.True);
            Assert.That(EnemyAttackTokens.InUse, Is.EqualTo(2));
            EnemyAttackTokens.Release();
            Assert.That(EnemyAttackTokens.InUseOf(AttackTokenChannel.Melee), Is.EqualTo(1));
            EnemyAttackTokens.Release(AttackTokenChannel.Melee);
            Assert.That(EnemyAttackTokens.InUse, Is.Zero);
        }

        [TestCase(AttackTokenChannel.Melee)]
        [TestCase(AttackTokenChannel.Slam)]
        [TestCase(AttackTokenChannel.RangedHeavy)]
        public void CapacityClamps_AndReleaseNeverGoesNegative(AttackTokenChannel channel)
        {
            EnemyAttackTokens.SetMaxConcurrent(channel, -10);
            Assert.That(EnemyAttackTokens.MaxOf(channel), Is.EqualTo(1));
            Assert.That(EnemyAttackTokens.TryAcquire(channel, 0), Is.True);
            Assert.That(EnemyAttackTokens.TryAcquire(channel, 0), Is.False);
            EnemyAttackTokens.Release(channel);
            EnemyAttackTokens.Release(channel);
            Assert.That(EnemyAttackTokens.InUseOf(channel), Is.Zero);
            EnemyAttackTokens.SetMaxConcurrent(channel, 0);
            Assert.That(EnemyAttackTokens.MaxOf(channel), Is.EqualTo(1));
        }

        [Test]
        public void SubsystemReset_RestoresEveryDefaultAndClearsUse()
        {
            for (int i = 0; i < 3; i++)
            {
                var channel = (AttackTokenChannel)i;
                EnemyAttackTokens.SetMaxConcurrent(channel, 7);
                EnemyAttackTokens.TryAcquire(channel, 0);
            }
            ResetPools();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(EnemyAttackTokens.InUseOf((AttackTokenChannel)i), Is.Zero);
                Assert.That(EnemyAttackTokens.MaxOf((AttackTokenChannel)i), Is.EqualTo(i == 1 ? 1 : 2));
            }
        }
    }
}
