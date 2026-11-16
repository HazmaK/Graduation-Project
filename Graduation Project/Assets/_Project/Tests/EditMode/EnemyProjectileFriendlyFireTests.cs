using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Enemy identity and damage probe, independent of live AI and timing.</summary>
    public class EyeProjectileEnemyProbe : MonoBehaviour, IEncounterEnemy, IDamageable
    {
        private bool _alive = true;
        /// <summary>Number of ordinary damage deliveries.</summary>
        public int DamageCalls { get; private set; }
        /// <summary>Last ordinary damage amount.</summary>
        public float LastDamage { get; private set; }
        /// <inheritdoc/>
        public bool IsAlive => _alive && gameObject.activeInHierarchy;
        /// <inheritdoc/>
        public Transform EnemyTransform => transform;
        /// <inheritdoc/>
        public GameObject EnemyGameObject => gameObject;
        /// <inheritdoc/>
        public event Action<IEncounterEnemy> OnEnemyDeath;
        /// <inheritdoc/>
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            DamageCalls++;
            LastDamage = amount;
        }
        /// <inheritdoc/>
        public void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            _alive = true;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
        /// <inheritdoc/>
        public void OnDespawnToDirector() => gameObject.SetActive(false);

        internal void Die()
        {
            _alive = false;
            OnEnemyDeath?.Invoke(this);
        }
    }

    /// <summary>Records reflected punishment separately from ordinary damage.</summary>
    public sealed class EyeProjectileReflectProbe : EyeProjectileEnemyProbe, IReflectPunishable
    {
        /// <summary>Number of reflected punishment deliveries.</summary>
        public int PunishCalls { get; private set; }
        /// <summary>Heavy flag delivered by the projectile.</summary>
        public bool ReflectedHeavy { get; private set; }
        /// <summary>Damage delivered by the reflected projectile.</summary>
        public float ReflectedDamage { get; private set; }
        /// <summary>Point delivered by the reflected projectile.</summary>
        public Vector3 ReflectedPoint { get; private set; }
        /// <summary>Normal delivered by the reflected projectile.</summary>
        public Vector3 ReflectedNormal { get; private set; }
        /// <inheritdoc/>
        public void OnOwnProjectileReflectedHit(bool heavy, float damage, Vector3 point, Vector3 normal)
        {
            PunishCalls++;
            ReflectedHeavy = heavy;
            ReflectedDamage = damage;
            ReflectedPoint = point;
            ReflectedNormal = normal;
        }
    }

    /// <summary>Real physics sweeps through reflection helpers, matching JudgeProjectileCollisionTests.</summary>
    public sealed class EnemyProjectileFriendlyFireTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly Vector3 _origin = new Vector3(10000f, 10000f, 10000f);
        private GameObject _shooter;
        private EyeProjectileReflectProbe _shooterProbe;
        private object _previousVfxInstance;
        private object _previousVfxQuitting;
        private object _previousHitstop;

        /// <summary>Creates a shooter and prewarms orbs while isolating scene feedback singletons.</summary>
        [SetUp]
        public void SetUp()
        {
            _previousVfxInstance = typeof(CombatVFXManager).GetField("_instance", PrivateStatic).GetValue(null);
            _previousVfxQuitting = typeof(CombatVFXManager).GetField("_isQuitting", PrivateStatic).GetValue(null);
            typeof(CombatVFXManager).GetField("_instance", PrivateStatic).SetValue(null, null);
            typeof(CombatVFXManager).GetField("_isQuitting", PrivateStatic).SetValue(null, true);
            _previousHitstop = typeof(HitstopManager).GetField("<Instance>k__BackingField", PrivateStatic).GetValue(null);
            typeof(HitstopManager).GetField("<Instance>k__BackingField", PrivateStatic).SetValue(null, null);
            ResetPool();
            _shooter = CreateObject("Eye shooter", _origin - Vector3.forward * 4f);
            _shooterProbe = _shooter.AddComponent<EyeProjectileReflectProbe>();
            EyeProjectilePool.Prewarm(2);
        }

        /// <summary>Destroys the isolated pool and restores scene feedback references.</summary>
        [TearDown]
        public void TearDown()
        {
            ResetPool();
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            typeof(CombatVFXManager).GetField("_instance", PrivateStatic).SetValue(null, _previousVfxInstance);
            typeof(CombatVFXManager).GetField("_isQuitting", PrivateStatic).SetValue(null, _previousVfxQuitting);
            typeof(HitstopManager).GetField("<Instance>k__BackingField", PrivateStatic).SetValue(null, _previousHitstop);
            Physics.SyncTransforms();
        }

        /// <summary>Default-layer enemies are skipped before nearest-hit selection, leaving the target reachable.</summary>
        [Test]
        public void UnreflectedOrb_PassesThroughSecondEnemyAndReachesTarget()
        {
            EyeProjectileEnemyProbe enemy = CreateEnemy("Friendly enemy", 1.5f, out Collider enemyCollider);
            GameObject target = CreateObject("Target beyond enemy", _origin + Vector3.forward * 3f);
            Collider targetCollider = target.AddComponent<BoxCollider>();
            EnemyProjectile orb = Launch();
            Assert.That(enemy.gameObject.layer, Is.Zero);
            Assert.That(Sweep(orb, 5f, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(targetCollider));
            Assert.That(hit.collider, Is.Not.SameAs(enemyCollider));
            Assert.That(enemy.DamageCalls, Is.Zero);
            Assert.That(orb.gameObject.activeSelf, Is.True);
            Assert.That(Get<bool>(orb, "_hasImpacted"), Is.False);
            Assert.That(orb.IsDeflected, Is.False);
            Collide(orb, hit);
            Assert.That(enemy.DamageCalls, Is.Zero);
            Assert.That(orb.gameObject.activeSelf, Is.False);
        }

        /// <summary>A segment containing only another enemy remains clear and cannot absorb the orb.</summary>
        [Test]
        public void UnreflectedOrb_OnlyFriendlyEnemy_NoCollisionOrDamage()
        {
            EyeProjectileEnemyProbe enemy = CreateEnemy("Friendly enemy", 1.5f, out _);
            EnemyProjectile orb = Launch();
            Assert.That(Sweep(orb, 5f, out _), Is.False);
            Assert.That(enemy.DamageCalls, Is.Zero);
            Assert.That(Get<bool>(orb, "_hasImpacted"), Is.False);
            Assert.That(orb.gameObject.activeSelf, Is.True);
        }

        /// <summary>Reflection restores ordinary enemy collision and damage.</summary>
        [Test]
        public void ReflectedOrb_HitsAndDamagesSecondEnemy()
        {
            EyeProjectileEnemyProbe enemy = CreateEnemy("Enemy after reflection", 1.5f, out Collider collider);
            EnemyProjectile orb = Launch();
            orb.Deflect(Vector3.forward);
            Assert.That(Sweep(orb, 5f, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(collider));
            Collide(orb, hit);
            Assert.That(enemy.DamageCalls, Is.EqualTo(1));
            Assert.That(enemy.LastDamage, Is.EqualTo(25f));
            Assert.That(orb.gameObject.activeSelf, Is.False);
        }

        /// <summary>The original shooter's child collider receives punishment, not ordinary damage, exactly once.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void OwnReflectedOrb_CallsPunishInsteadOfDamage(bool heavy)
        {
            _shooter.transform.position = _origin + Vector3.forward * 3f;
            GameObject child = CreateObject("Shooter child collider", _shooter.transform.position);
            child.transform.SetParent(_shooter.transform, true);
            Collider collider = child.AddComponent<BoxCollider>();
            EnemyProjectile orb = Launch(heavy);
            orb.Deflect(Vector3.forward);
            Assert.That(orb.OriginalShooter, Is.SameAs(_shooter.transform));
            Assert.That(Get<Transform>(orb, "_shooter"), Is.Null);
            Assert.That(Sweep(orb, 5f, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(collider));
            int events = 0;
            Action<EnemyProjectile, Collider> onImpact = (projectile, impacted) =>
            {
                if (projectile != orb) return;
                events++;
                Assert.That(impacted, Is.SameAs(collider));
                Assert.That(_shooterProbe.PunishCalls, Is.Zero);
            };
            EnemyProjectile.OnDeflectedImpact += onImpact;
            try
            {
                Collide(orb, hit);
                Collide(orb, hit);
            }
            finally
            {
                EnemyProjectile.OnDeflectedImpact -= onImpact;
            }
            Assert.That(events, Is.EqualTo(1));
            Assert.That(_shooterProbe.PunishCalls, Is.EqualTo(1));
            Assert.That(_shooterProbe.DamageCalls, Is.Zero);
            Assert.That(_shooterProbe.ReflectedHeavy, Is.EqualTo(heavy));
            Assert.That(_shooterProbe.ReflectedDamage, Is.EqualTo(25f));
            Assert.That(_shooterProbe.ReflectedPoint, Is.EqualTo(hit.point));
            Assert.That(_shooterProbe.ReflectedNormal, Is.EqualTo(hit.normal));
            Assert.That(orb.gameObject.activeSelf, Is.False);
        }

        /// <summary>Reuse restores settings, tint, scale and reflection state without new materials.</summary>
        [Test]
        public void PooledOrb_ReusesObjectAndSharedMaterialsAndResetsSettings()
        {
            EnemyProjectile orb = Launch(true);
            Material orbMaterial = orb.GetComponent<Renderer>().sharedMaterial;
            Material trailMaterial = orb.GetComponent<TrailRenderer>().sharedMaterial;
            orb.Deflect(Vector3.forward);
            Assert.That(orb.transform.localScale.x, Is.EqualTo(0.8f * 1.15f).Within(0.00001f));
            typeof(EnemyProjectile).GetMethod("Release", PrivateInstance).Invoke(orb, null);
            EnemyProjectile reused = Launch();
            Assert.That(reused, Is.SameAs(orb));
            Assert.That(reused.IsDeflected, Is.False);
            Assert.That(reused.IsHeavy, Is.False);
            Assert.That(reused.Damage, Is.EqualTo(10f));
            Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one * 0.4f));
            Assert.That(Get<float>(reused, "collisionRadius"), Is.EqualTo(0.2f));
            Assert.That(Get<float>(reused, "lifetime"), Is.EqualTo(5f));
            Assert.That(reused.GetComponent<Renderer>().sharedMaterial, Is.SameAs(orbMaterial));
            Assert.That(reused.GetComponent<TrailRenderer>().sharedMaterial, Is.SameAs(trailMaterial));
            var properties = new MaterialPropertyBlock();
            reused.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetColor(Shader.PropertyToID("_BaseColor")), Is.EqualTo(Color.magenta));
            Assert.That(reused.GetComponent<TrailRenderer>().positionCount, Is.Zero);
        }

        /// <summary>Destroyed inactive entries cannot be returned by the pool.</summary>
        [Test]
        public void Pool_DropsDestroyedEntriesBeforeReuse()
        {
            EnemyProjectile first = Launch();
            Object.DestroyImmediate(first.gameObject);
            EnemyProjectile replacement = Launch();
            Assert.That(replacement != null, Is.True);
            Assert.That(replacement.gameObject.activeSelf, Is.True);
        }

        /// <summary>Reflected homing turns at the configured rate, respecting angle, life and activity gates.</summary>
        [Test]
        public void ReflectedHoming_RespectsTurnRateConeLifeAndActivity()
        {
            _shooter.transform.position = _origin + Vector3.right * 10f;
            EnemyProjectile orb = Launch();
            orb.Deflect(Vector3.forward);
            MethodInfo homing = typeof(EnemyProjectile).GetMethod("UpdateReflectHoming", PrivateInstance);
            homing.Invoke(orb, new object[] { 0.5f });
            Assert.That(Vector3.Angle(Vector3.forward, orb.transform.forward), Is.EqualTo(45f).Within(0.001f));
            Set(orb, "reflectHomingMaxAngle", 10f);
            Quaternion before = orb.transform.rotation;
            homing.Invoke(orb, new object[] { 0.5f });
            Assert.That(orb.transform.rotation, Is.EqualTo(before));
            Set(orb, "reflectHomingMaxAngle", 180f);
            _shooter.SetActive(false);
            homing.Invoke(orb, new object[] { 0.5f });
            Assert.That(orb.transform.rotation, Is.EqualTo(before));
            _shooter.SetActive(true);
            _shooterProbe.Die();
            homing.Invoke(orb, new object[] { 0.5f });
            Assert.That(orb.transform.rotation, Is.EqualTo(before));
        }

        /// <summary>The existing launch API always disables homing.</summary>
        [Test]
        public void LegacyInitialize_LeavesHomingDisabled()
        {
            GameObject root = CreateObject("Legacy sword", _origin);
            EnemyProjectile sword = root.AddComponent<EnemyProjectile>();
            sword.Initialize(_shooter.transform, 14f, 18f);
            Assert.That(Get<float>(sword, "reflectHomingDegPerSec"), Is.Zero);
            Assert.That(Get<bool>(sword, "_isPooled"), Is.False);
            Assert.That(sword.IsHeavy, Is.False);
        }

        private EnemyProjectile Launch(bool heavy = false)
        {
            var settings = new EnemyProjectileSettings
            {
                Speed = 10f, Damage = 10f, CollisionRadius = 0.2f,
                VisualScale = heavy ? 0.8f : 0.4f, Lifetime = 5f, Heavy = heavy,
                ReflectHomingDegPerSec = 90f, ReflectHomingMaxAngle = 180f, Color = Color.magenta
            };
            return EyeProjectilePool.Launch(_shooter.transform, _origin, Vector3.forward, in settings);
        }

        private GameObject CreateObject(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            _objects.Add(root);
            return root;
        }

        private EyeProjectileEnemyProbe CreateEnemy(string name, float distance, out Collider collider)
        {
            GameObject root = CreateObject(name, _origin + Vector3.forward * distance);
            var enemy = root.AddComponent<EyeProjectileEnemyProbe>();
            GameObject child = CreateObject("Enemy child collider", root.transform.position);
            child.transform.SetParent(root.transform, true);
            collider = child.AddComponent<BoxCollider>();
            return enemy;
        }

        private static bool Sweep(EnemyProjectile orb, float distance, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            object[] arguments = { distance, default(RaycastHit) };
            bool found = (bool)typeof(EnemyProjectile).GetMethod("TryFindCollision", PrivateInstance).Invoke(orb, arguments);
            hit = (RaycastHit)arguments[1];
            return found;
        }

        private static void Collide(EnemyProjectile orb, RaycastHit hit)
        {
            typeof(EnemyProjectile).GetMethod("HandleCollision", PrivateInstance)
                .Invoke(orb, new object[] { hit.collider, hit.point, hit.normal });
        }

        private static T Get<T>(EnemyProjectile orb, string name) =>
            (T)typeof(EnemyProjectile).GetField(name, PrivateInstance).GetValue(orb);

        private static void Set(EnemyProjectile orb, string name, object value) =>
            typeof(EnemyProjectile).GetField(name, PrivateInstance).SetValue(orb, value);

        private static void ResetPool() =>
            typeof(EyeProjectilePool).GetMethod("ResetStatics", PrivateStatic).Invoke(null, null);
    }
}
