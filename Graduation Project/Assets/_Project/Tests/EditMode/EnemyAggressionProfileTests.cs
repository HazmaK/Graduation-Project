using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Profile defaults and editor validation using real ScriptableObject instances.</summary>
    public sealed class EnemyAggressionProfileTests
    {
        private EnemyAggressionProfile _profile;

        [SetUp]
        public void SetUp() => _profile = ScriptableObject.CreateInstance<EnemyAggressionProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [Test]
        public void Defaults_MatchHuntContract()
        {
            Assert.That(_profile.HuntStartDelay, Is.EqualTo(0.35f));
            Assert.That(_profile.MeleeTokenLimit, Is.EqualTo(3));
            Assert.That(_profile.SlamTokenLimit, Is.EqualTo(1));
            Assert.That(_profile.RangedHeavyTokenLimit, Is.EqualTo(2));
            Assert.That(_profile.ReleaseMeleeTokenOnRecover, Is.True);
            Assert.That(_profile.SurroundRadiusMin, Is.EqualTo(2.8f));
            Assert.That(_profile.SurroundRadiusMax, Is.EqualTo(3.5f));
            Assert.That(_profile.FeintChancePerSecond, Is.EqualTo(0.35f));
            Assert.That(_profile.FeintDuration, Is.EqualTo(0.22f));
            Assert.That(_profile.FeintSpeed, Is.EqualTo(5.5f));
            Assert.That(_profile.MoveSpeedMultiplier, Is.EqualTo(1.15f));
            Assert.That(_profile.TurnSpeedMultiplier, Is.EqualTo(1.45f));
            Assert.That(_profile.AttackCooldownMultiplier, Is.EqualTo(0.8f));
            Assert.That(_profile.EyeHoverHeight, Is.EqualTo(3.0f));
            Assert.That(_profile.EyeMinFlyHeight, Is.EqualTo(2.0f));
            Assert.That(_profile.EyeMaxFlyHeight, Is.EqualTo(4.0f));
        }

        [Test]
        public void OnValidate_ClampsEveryNumericFieldToNonnegative()
        {
            Set("huntStartDelay", -3f);
            Set("meleeTokenLimit", -3);
            Set("slamTokenLimit", -3);
            Set("rangedHeavyTokenLimit", -3);
            Set("surroundRadiusMin", -3f);
            Set("surroundRadiusMax", -3f);
            Set("feintChancePerSecond", -3f);
            Set("feintDuration", -3f);
            Set("feintSpeed", -3f);
            Set("moveSpeedMultiplier", -3f);
            Set("turnSpeedMultiplier", -3f);
            Set("attackCooldownMultiplier", -3f);
            Set("eyeHoverHeight", -3f);
            Set("eyeMinFlyHeight", -3f);
            Set("eyeMaxFlyHeight", -3f);
            Validate();
            Assert.That(_profile.HuntStartDelay, Is.Zero);
            Assert.That(_profile.MeleeTokenLimit, Is.Zero);
            Assert.That(_profile.SlamTokenLimit, Is.Zero);
            Assert.That(_profile.RangedHeavyTokenLimit, Is.Zero);
            Assert.That(_profile.SurroundRadiusMin, Is.Zero);
            Assert.That(_profile.SurroundRadiusMax, Is.Zero);
            Assert.That(_profile.FeintChancePerSecond, Is.Zero);
            Assert.That(_profile.FeintDuration, Is.Zero);
            Assert.That(_profile.FeintSpeed, Is.Zero);
            Assert.That(_profile.MoveSpeedMultiplier, Is.Zero);
            Assert.That(_profile.TurnSpeedMultiplier, Is.Zero);
            Assert.That(_profile.AttackCooldownMultiplier, Is.Zero);
            Assert.That(_profile.EyeHoverHeight, Is.Zero);
            Assert.That(_profile.EyeMinFlyHeight, Is.Zero);
            Assert.That(_profile.EyeMaxFlyHeight, Is.Zero);
        }

        [Test]
        public void OnValidate_OrdersRangesAndClampsHoverWithinThem()
        {
            Set("surroundRadiusMin", 6f);
            Set("surroundRadiusMax", 2f);
            Set("eyeMinFlyHeight", 5f);
            Set("eyeMaxFlyHeight", 1f);
            Set("eyeHoverHeight", 20f);
            Validate();
            Assert.That(_profile.SurroundRadiusMin, Is.EqualTo(6f));
            Assert.That(_profile.SurroundRadiusMax, Is.EqualTo(6f));
            Assert.That(_profile.EyeMinFlyHeight, Is.EqualTo(5f));
            Assert.That(_profile.EyeMaxFlyHeight, Is.EqualTo(5f));
            Assert.That(_profile.EyeHoverHeight, Is.EqualTo(5f));
            Set("eyeMaxFlyHeight", 8f);
            Set("eyeHoverHeight", 1f);
            Validate();
            Assert.That(_profile.EyeHoverHeight, Is.EqualTo(5f));
            Set("eyeHoverHeight", 7f);
            Validate();
            Assert.That(_profile.EyeHoverHeight, Is.EqualTo(7f));
        }

        [Test]
        public void OnValidate_PreservesValidValuesIncludingDisabledFeintsAndDefaultTokenLimits()
        {
            Set("huntStartDelay", 0f);
            Set("meleeTokenLimit", 0);
            Set("slamTokenLimit", 0);
            Set("rangedHeavyTokenLimit", 0);
            Set("feintChancePerSecond", 0f);
            Set("releaseMeleeTokenOnRecover", false);
            Set("moveSpeedMultiplier", 2f);
            Set("attackCooldownMultiplier", 1.2f);
            Validate();
            Assert.That(_profile.HuntStartDelay, Is.Zero);
            Assert.That(_profile.MeleeTokenLimit, Is.Zero);
            Assert.That(_profile.SlamTokenLimit, Is.Zero);
            Assert.That(_profile.RangedHeavyTokenLimit, Is.Zero);
            Assert.That(_profile.FeintChancePerSecond, Is.Zero);
            Assert.That(_profile.ReleaseMeleeTokenOnRecover, Is.False);
            Assert.That(_profile.MoveSpeedMultiplier, Is.EqualTo(2f));
            Assert.That(_profile.AttackCooldownMultiplier, Is.EqualTo(1.2f));
        }

        private void Set(string name, object value) => typeof(EnemyAggressionProfile)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_profile, value);

        private void Validate() => typeof(EnemyAggressionProfile)
            .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_profile, null);
    }
}

