using NUnit.Framework;
using RetroFPS.Combat.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace RetroFPS.Tests.EditMode.Navigation
{
    /// <summary>Real synchronous collider baking and query integration, isolated above the scene.</summary>
    public sealed class RuntimeNavMeshBakerTests
    {
        private const float TestHeight = 1000f;
        private GameObject root;
        private RuntimeNavMeshBaker baker;

        /// <summary>Creates a ten-metre collider platform away from other navigation data.</summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Runtime navigation test root");
            root.transform.position = Vector3.up * TestHeight;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = new Vector3(10f, 1f, 10f);
            baker = root.AddComponent<RuntimeNavMeshBaker>();
            Physics.SyncTransforms();
        }

        /// <summary>Unregisters data and destroys the test hierarchy even after a failed assertion.</summary>
        [TearDown]
        public void TearDown()
        {
            if (baker != null)
                baker.Clear();
            if (root != null)
                Object.DestroyImmediate(root);
        }

        /// <summary>Baking creates registered data and a complete path across the cube.</summary>
        [Test]
        public void Bake_ColliderHierarchySupportsCompletePath()
        {
            var settings = new EnemyNavSettings();
            Assert.That(baker.Root, Is.EqualTo(root.transform));
            Assert.That(baker.HasData, Is.False);
            int bakedEvents = 0;
            baker.Baked += source =>
            {
                Assert.That(source, Is.SameAs(baker));
                bakedEvents++;
            };
            baker.Bake();
            Assert.That(baker.HasData, Is.True);
            Assert.That(bakedEvents, Is.EqualTo(1));
            Vector3 top = new Vector3(0f, TestHeight + 0.5f, 0f);
            Assert.That(NavMesh.SamplePosition(top, out NavMeshHit hit, settings.SampleMaxDistance, NavMesh.AllAreas), Is.True);
            Assert.That(Mathf.Abs(hit.position.y - top.y), Is.LessThanOrEqualTo(settings.CornerReachDistance));
            var query = new UnityNavMeshQuery();
            Assert.That(query.SamplePosition(top - Vector3.right * settings.PartialEndAttackRadius,
                settings.SampleMaxDistance, out Vector3 from), Is.True);
            Assert.That(query.SamplePosition(top + Vector3.right * settings.PartialEndAttackRadius,
                settings.SampleMaxDistance, out Vector3 to), Is.True);
            var corners = new Vector3[EnemyNavigator.MaxCorners];
            Assert.That(query.CalculatePath(from, to, corners, out int count), Is.EqualTo(NavPathStatus.Complete));
            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            baker.Clear();
            Assert.That(baker.HasData, Is.False);
            Assert.That(NavMesh.SamplePosition(top, out _, settings.SampleMaxDistance, NavMesh.AllAreas), Is.False);
        }

        /// <summary>The explicit-root overload bakes the requested hierarchy.</summary>
        [Test]
        public void Bake_ExplicitRootSetsEffectiveRoot()
        {
            Transform child = root.transform.GetChild(0);
            baker.Bake(child);
            Assert.That(baker.Root, Is.EqualTo(child));
            Assert.That(baker.HasData, Is.True);
        }
    }
}
