using NUnit.Framework;
using RetroFPS.Combat;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Checks the prefab produced by RetroFPS/Build Flying Eye.</summary>
    public sealed class FlyingEyePrefabTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";
        private GameObject _prefab;

        /// <summary>Loads the asset without invoking runtime AI or creating scene objects.</summary>
        [SetUp]
        public void LoadPrefab()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(_prefab, Is.Not.Null, "The existing Flying Eye prefab must be present.");
        }

        /// <summary>Requires the complete migrated hierarchy.</summary>
        [TestCase("Visual")]
        [TestCase("Visual/Body_Sclera")]
        [TestCase("Visual/Eye_Iris")]
        [TestCase("Visual/Pupil_Visual")]
        [TestCase("Visual/Horn_Top")]
        [TestCase("Visual/Horn_L")]
        [TestCase("Visual/Horn_R")]
        [TestCase("Visual/Tendril_Center")]
        [TestCase("Visual/Tendril_L")]
        [TestCase("Visual/Tendril_R")]
        [TestCase("Hitboxes")]
        [TestCase("Hitboxes/Hitbox_Pupil")]
        [TestCase("Sockets")]
        [TestCase("Sockets/Socket_FirePoint")]
        [TestCase("Sockets/Socket_Telegraph")]
        [TestCase("Sockets/Socket_DeathFx")]
        [TestCase("Telegraph")]
        [TestCase("Telegraph/ChargeOrb")]
        [TestCase("Telegraph/AimLine")]
        public void HierarchyContains(string path)
        {
            Assert.That(_prefab.transform.Find(path), Is.Not.Null, "Run RetroFPS/Build Flying Eye first: " + path);
        }

        /// <summary>Ensures replacement art has no gameplay components.</summary>
        [Test]
        public void VisualContainsOnlyArt()
        {
            Transform visual = Required("Visual");
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        }

        /// <summary>Requires an independent, fully wired pupil critical zone.</summary>
        [Test]
        public void PupilHitboxIsIndependentCriticalSphere()
        {
            Transform pupil = Required("Hitboxes/Hitbox_Pupil");
            SphereCollider sphere = pupil.GetComponent<SphereCollider>();
            Assert.That(sphere, Is.Not.Null);
            Assert.That(sphere.radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(pupil.localPosition, Is.EqualTo(new Vector3(0f, 0f, 0.48f)));
            Assert.That(pupil.localScale, Is.EqualTo(Vector3.one));
            Hitbox hitbox = pupil.GetComponent<Hitbox>();
            Assert.That(hitbox, Is.Not.Null);
            Assert.That(hitbox.IsCriticalZone, Is.True);
            Assert.That(hitbox.DamageMultiplier, Is.EqualTo(2f));
            var data = new SerializedObject(hitbox);
            Assert.That(data.FindProperty("targetEntityComponent").objectReferenceValue, Is.SameAs(_prefab.GetComponent<FlyingEyeAI>()));
        }

        /// <summary>Requires one view and all authored references, including both renderer arrays.</summary>
        [Test]
        public void ViewAndAiReferencesAreAssigned()
        {
            FlyingEyeView[] views = _prefab.GetComponentsInChildren<FlyingEyeView>(true);
            Assert.That(views, Has.Length.EqualTo(1));
            Assert.That(views[0].gameObject, Is.SameAs(_prefab));
            var data = new SerializedObject(views[0]);
            string[] names = { "visualRoot", "fireSocket", "telegraphSocket", "deathFxSocket", "chargeOrb", "chargeOrbRenderer", "aimLine" };
            foreach (string name in names) AssertReference(data, name);
            string[] arrays = { "tintRenderers", "glowRenderers" };
            foreach (string name in arrays)
            {
                SerializedProperty array = data.FindProperty(name);
                Assert.That(array, Is.Not.Null, name);
                Assert.That(array.arraySize, Is.GreaterThan(0), name);
                for (int i = 0; i < array.arraySize; i++)
                {
                    var renderer = array.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    Assert.That(renderer, Is.Not.Null, name);
                    Assert.That(renderer.transform.IsChildOf(Required("Visual")), Is.True, name);
                }
            }
            var aiData = new SerializedObject(_prefab.GetComponent<FlyingEyeAI>());
            Assert.That(aiData.FindProperty("view").objectReferenceValue, Is.SameAs(views[0]));
            Assert.That(aiData.FindProperty("firePoint").objectReferenceValue, Is.SameAs(Required("Sockets/Socket_FirePoint")));
            Assert.That(_prefab.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(_prefab.GetComponent<EnemyHealthDropper>(), Is.Not.Null);
            Assert.That(_prefab.GetComponent<EnemyAmmoDropper>(), Is.Not.Null);
        }

        /// <summary>Requires prebuilt telegraphs with no collider and persistent material assets.</summary>
        [Test]
        public void TelegraphStartsHiddenWithPersistentMaterials()
        {
            Transform orb = Required("Telegraph/ChargeOrb");
            Assert.That(orb.gameObject.activeSelf, Is.False);
            Assert.That(orb.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(orb.GetComponentsInChildren<Collider>(true), Is.Empty);
            AssertPersistentMaterial(orb.GetComponent<Renderer>());
            LineRenderer line = Required("Telegraph/AimLine").GetComponent<LineRenderer>();
            Assert.That(line, Is.Not.Null);
            Assert.That(line.enabled, Is.False);
            Assert.That(line.positionCount, Is.EqualTo(2));
            Assert.That(line.useWorldSpace, Is.True);
            Assert.That(line.startWidth, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(line.endWidth, Is.EqualTo(0.04f).Within(0.0001f));
            AssertPersistentMaterial(line);
        }

        private Transform Required(string path)
        {
            Transform child = _prefab.transform.Find(path);
            Assert.That(child, Is.Not.Null, "Run RetroFPS/Build Flying Eye first: " + path);
            return child;
        }

        private static void AssertReference(SerializedObject data, string name)
        {
            SerializedProperty property = data.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.objectReferenceValue, Is.Not.Null, name);
        }

        private static void AssertPersistentMaterial(Renderer renderer)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Does.StartWith("Assets/_Project/Art/Enemies/FlyingEye/Placeholder/"));
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
        }
    }
}
