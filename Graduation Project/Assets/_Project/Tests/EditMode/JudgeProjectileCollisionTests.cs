using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Physics regressions for swords crossing overlapping shooter/player movement segments.</summary>
    public sealed class JudgeProjectileCollisionTests
    {
        /// <summary>Volley spawned at the player's position still hits despite SphereCast's initial-overlap limitation.</summary>
        [Test]
        public void Sword_SpawnedInsidePlayer_FindsInitialOverlap()
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            var root = new GameObject("JudgeSwordOverlapTest");
            var player = new GameObject("PlayerCollider");
            try
            {
                root.transform.position = player.transform.position = origin;
                player.layer = GameLayers.Player;
                var collider = player.AddComponent<BoxCollider>();
                var sword = root.AddComponent<EnemyProjectile>();
                Physics.SyncTransforms();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That(typeof(EnemyProjectile).GetMethod("FindInitialOverlap", fields).Invoke(sword, null),
                    Is.SameAs(collider));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
                Physics.SyncTransforms();
            }
        }

        /// <summary>A sword born inside level trigger volumes (boss arena bounds) must not vanish on spawn.</summary>
        [Test]
        public void Sword_SpawnedInsideNonPlayerTrigger_IgnoresIt()
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            var root = new GameObject("JudgeSwordTriggerTest");
            var bounds = new GameObject("ArenaBoundsTrigger");
            try
            {
                root.transform.position = bounds.transform.position = origin;
                var trigger = bounds.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one * 50f;
                var sword = root.AddComponent<EnemyProjectile>();
                Physics.SyncTransforms();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That(typeof(EnemyProjectile).GetMethod("FindInitialOverlap", fields).Invoke(sword, null),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(bounds);
                Physics.SyncTransforms();
            }
        }

        /// <summary>A sword flying inside the arena trigger volume must still reach the player ahead of it.</summary>
        [Test]
        public void Sword_InsideArenaTrigger_SweepSkipsTriggerAndHitsPlayer()
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            var root = new GameObject("JudgeSwordArenaSweepTest");
            var bounds = new GameObject("ArenaBoundsTrigger");
            var player = new GameObject("PlayerCollider");
            try
            {
                root.transform.position = bounds.transform.position = origin;
                var trigger = bounds.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one * 50f;
                player.transform.position = origin + Vector3.forward * 2f;
                player.layer = GameLayers.Player;
                var playerCollider = player.AddComponent<BoxCollider>();
                playerCollider.size = Vector3.one * 0.5f;
                var sword = root.AddComponent<EnemyProjectile>();
                Physics.SyncTransforms();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                object[] arguments = { 5f, default(RaycastHit) };
                bool found = (bool)typeof(EnemyProjectile).GetMethod("TryFindCollision", fields).Invoke(sword, arguments);
                Assert.That(found, Is.True);
                Assert.That(((RaycastHit)arguments[1]).collider, Is.SameAs(playerCollider));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(bounds);
                Object.DestroyImmediate(player);
                Physics.SyncTransforms();
            }
        }

        /// <summary>Ignoring the shooter must not skip a player behind it in the same segment.</summary>
        [Test]
        public void Sword_FiltersShooterBeforeNearestHit_StillFindsNearbyPlayer()
        {
            CheckSegment(false);
        }

        /// <summary>A reflected sword ignores the player while still returning to hit its former shooter.</summary>
        [Test]
        public void ReflectedSword_FiltersPlayer_StillHitsBoss()
        {
            CheckSegment(true);
        }

        private static void CheckSegment(bool reflected)
        {
            // Keep the query independent of whichever scene is open in the Editor.
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            var root = new GameObject("JudgeSwordCollisionTest");
            var shooter = new GameObject("Shooter");
            var player = new GameObject("PlayerCollider");
            try
            {
                root.transform.position = origin;
                shooter.transform.position = origin + Vector3.forward * (reflected ? 3f : 1f);
                shooter.layer = GameLayers.Enemy;
                var shooterCollider = shooter.AddComponent<BoxCollider>();
                shooterCollider.size = Vector3.one * 0.5f;
                player.transform.position = origin + Vector3.forward * 2f;
                player.layer = GameLayers.Player;
                var playerCollider = player.AddComponent<BoxCollider>();
                playerCollider.size = Vector3.one * 0.5f;
                var sword = root.AddComponent<EnemyProjectile>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EnemyProjectile).GetField("_shooter", fields).SetValue(sword, shooter.transform);
                typeof(EnemyProjectile).GetField("_isDeflected", fields).SetValue(sword, reflected);
                Physics.SyncTransforms();
                object[] arguments = { 5f, default(RaycastHit) };
                bool found = (bool)typeof(EnemyProjectile).GetMethod("TryFindCollision", fields).Invoke(sword, arguments);
                Assert.That(found, Is.True);
                Assert.That(((RaycastHit)arguments[1]).collider,
                    Is.SameAs(reflected ? shooterCollider : playerCollider));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shooter);
                Object.DestroyImmediate(player);
                Physics.SyncTransforms();
            }
        }
    }
}
