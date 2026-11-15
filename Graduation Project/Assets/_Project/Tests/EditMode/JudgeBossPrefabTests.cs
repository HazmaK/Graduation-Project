using NUnit.Framework;
using RetroFPS.Combat;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests
{
    /// <summary>Checks generated, scene-independent Judge assets without invoking their builder.</summary>
    public sealed class JudgeBossPrefabTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Boss_Judge.prefab";
        private const string ConfigPath = "Assets/_Project/Data/Boss/JudgeBossConfig.asset";
        private const string ProjectilePath = "Assets/_Project/Prefabs/Enemies/Sword_Judge.prefab";
        private const string MaterialDir = "Assets/_Project/Data/Materials/Boss/";

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                Assert.Ignore("Run RetroFPS/Build Judge Boss first; Boss_Judge.prefab has not been built yet.");
            return prefab;
        }

        private static Object Reference(Object owner, string field)
        {
            Assert.IsNotNull(owner, "Missing owner for " + field);
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, "Missing serialized field " + field);
            Object value = property.objectReferenceValue;
            Assert.IsNotNull(value, "Unwired serialized field " + field);
            return value;
        }

        private static void ArrayReferences(Object owner, string field, int expectedCount, Transform parent)
        {
            SerializedProperty array = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(array, field);
            Assert.AreEqual(expectedCount, array.arraySize, field);
            for (int i = 0; i < array.arraySize; i++)
            {
                var component = array.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                Assert.IsNotNull(component, field + "[" + i + "]");
                Assert.IsTrue(component.transform.IsChildOf(parent), field + " hierarchy");
            }
        }

        /// <summary>The boss is hittable through the existing damage, stun and encounter interfaces.</summary>
        [Test]
        public void Boss_HasCombatInterfacesAndHittableColliders()
        {
            GameObject prefab = Prefab();
            JudgeBossAI boss = prefab.GetComponent<JudgeBossAI>();
            Assert.IsNotNull(boss);
            Assert.IsInstanceOf<IDamageable>(boss);
            Assert.IsInstanceOf<IStunnable>(boss);
            Assert.IsInstanceOf<IEncounterEnemy>(boss);
            Assert.IsInstanceOf<IThreatCost>(boss);
            Assert.IsInstanceOf<JudgeBossLoop.IActions>(boss);
            Assert.AreEqual(GameLayers.Enemy, prefab.layer);
            CharacterController controller = prefab.GetComponent<CharacterController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.enabled);
            Assert.GreaterOrEqual(controller.height, 3f);
            Hitbox head = prefab.GetComponentInChildren<Hitbox>(true);
            Assert.IsNotNull(head);
            Assert.AreSame(boss, Reference(head, "targetEntityComponent"));
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                Assert.AreEqual(GameLayers.Enemy, collider.gameObject.layer);
                Assert.IsFalse(collider.isTrigger, "Boss hitboxes must be hittable by Ignore-trigger weapon queries.");
            }
        }

        /// <summary>Config, materials, swords and hazard pools survive loading the prefab from disk.</summary>
        [Test]
        public void Boss_AllRequiredReferencesAndPoolsAreWired()
        {
            GameObject prefab = Prefab();
            JudgeBossAI boss = prefab.GetComponent<JudgeBossAI>();
            var config = (JudgeBossConfig)Reference(boss, "config");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<JudgeBossConfig>(ConfigPath), config);
            var ring = (JudgeSwordRing)Reference(boss, "swordRing");
            Assert.AreEqual(config.RingSwordCount, ring.Count);
            ArrayReferences(ring, "swords", config.RingSwordCount, ring.transform);
            var blade = (Transform)Reference(boss, "greatBlade");
            Assert.IsFalse(blade.gameObject.activeSelf);
            var hazards = (Transform)Reference(boss, "hazardsRoot");
            Assert.AreEqual("Hazards", hazards.name);
            Assert.AreSame(prefab.transform, hazards.parent);
            ArrayReferences(boss, "guillotines", 3, hazards);
            ArrayReferences(boss, "shockLines", 2, hazards);
            var sweep = (JudgeScaleSweep)Reference(boss, "sweep");
            Assert.IsTrue(sweep.transform.IsChildOf(hazards));
            Assert.IsFalse(((Transform)Reference(sweep, "bar")).gameObject.activeSelf);
            Assert.AreEqual(3, hazards.GetComponentsInChildren<JudgeGuillotineStrike>(true).Length);
            Assert.AreEqual(1, hazards.GetComponentsInChildren<JudgeScaleSweep>(true).Length);
            var repulse = (JudgeRepulseWarning)Reference(boss, "repulseWarning");
            Assert.IsTrue(repulse.transform.IsChildOf(hazards));
            Assert.AreEqual(1, hazards.GetComponentsInChildren<JudgeRepulseWarning>(true).Length);
            var fill = (Transform)Reference(repulse, "fill");
            var outline = (LineRenderer)Reference(repulse, "outline");
            Assert.IsFalse(fill.gameObject.activeSelf);
            Assert.IsFalse(outline.gameObject.activeSelf);
            Assert.IsTrue(outline.loop);
            Assert.IsFalse(outline.useWorldSpace);
            Assert.AreEqual(Mathf.Max(8, config.RepulseMarkerSegments), outline.positionCount);
            Material redMarker = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "Mat_Judge_HazardMarker.mat");
            Assert.AreSame(redMarker, fill.GetComponent<Renderer>().sharedMaterial);
            Assert.AreSame(redMarker, outline.sharedMaterial);
            Assert.AreEqual(2, hazards.GetComponentsInChildren<JudgeShockLine>(true).Length);
            foreach (JudgeGuillotineStrike guillotine in hazards.GetComponentsInChildren<JudgeGuillotineStrike>(true))
            {
                Assert.IsFalse(((Transform)Reference(guillotine, "marker")).gameObject.activeSelf);
                Assert.IsFalse(((Transform)Reference(guillotine, "blade")).gameObject.activeSelf);
            }
            foreach (JudgeShockLine line in hazards.GetComponentsInChildren<JudgeShockLine>(true))
                Assert.IsFalse(((Transform)Reference(line, "visual")).gameObject.activeSelf);
            Assert.AreEqual(0, hazards.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, ring.GetComponentsInChildren<Collider>(true).Length);
            Assert.IsNotNull(Reference(boss, "phase2Material"));
            SerializedProperty bodies = new SerializedObject(boss).FindProperty("bodyRenderers");
            Assert.Greater(bodies.arraySize, 0);
            for (int i = 0; i < bodies.arraySize; i++)
            {
                var renderer = bodies.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(renderer.sharedMaterial);
            }
        }

        /// <summary>Projectile and all material assets exist independently of an open Gauntlet scene.</summary>
        [Test]
        public void Boss_ProjectileAndUrpMaterialsArePersistentAssets()
        {
            JudgeBossAI boss = Prefab().GetComponent<JudgeBossAI>();
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            Assert.IsNotNull(projectile);
            Assert.AreSame(projectile.GetComponent<EnemyProjectile>(), Reference(boss, "swordProjectilePrefab"));
            Assert.AreEqual(GameLayers.Projectile, projectile.layer);
            Assert.IsTrue(projectile.GetComponent<Collider>().isTrigger);
            Assert.IsNotNull(projectile.GetComponent<TrailRenderer>());
            string[] materialNames = { "Mat_Judge_Robe", "Mat_Judge_Robe_Phase2", "Mat_Judge_Mask",
                "Mat_Judge_SwordSteel", "Mat_Judge_HazardMarker" };
            foreach (string name in materialNames)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
                Assert.IsNotNull(material, name);
                Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), name + " must support telegraph glow");
                Assert.IsTrue(EditorUtility.IsPersistent(material));
            }
            Material marker = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "Mat_Judge_HazardMarker.mat");
            // The builder now emits an opaque emissive marker so it stays readable on the dark floor.
            Assert.AreEqual(0f, marker.GetFloat("_Surface"));
        }
    }
}
