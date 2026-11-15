using System;
using System.Collections.Generic;
using RetroFPS.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Builds The Judge's generated assets and wires only the Gauntlet boss placeholder.</summary>
    public static class JudgeBossBuilder
    {
        private const string ConfigPath = "Assets/_Project/Data/Boss/JudgeBossConfig.asset";
        private const string MaterialDir = "Assets/_Project/Data/Materials/Boss";
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Boss_Judge.prefab";
        private const string ProjectilePath = "Assets/_Project/Prefabs/Enemies/Sword_Judge.prefab";
        private const string ScenePath = "Assets/Scenes/TestLevel_Gauntlet.unity";
        private const string AmmoCachePath = "Assets/_Project/Prefabs/Enemies/Judge_ArenaAmmoCache.prefab";
        private const float AmmoCacheInset = 3f;

        /// <summary>Creates or updates generated assets and the single Gauntlet encounter in place.</summary>
        [MenuItem("RetroFPS/Build Judge Boss", false, 23)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Judge Boss must run outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Build TestLevel_Gauntlet before building The Judge.");
            Scene tower = SceneManager.GetSceneByPath(ScenePath);
            if (tower.IsValid() && tower.isDirty)
                throw new InvalidOperationException("Save TestLevel_Gauntlet before running the boss builder.");
            if (Shader.Find("Universal Render Pipeline/Lit") == null)
                throw new InvalidOperationException("The Judge requires the URP Lit shader.");

            EnsureFolder("Assets/_Project/Data/Boss");
            EnsureFolder(MaterialDir);
            EnsureFolder("Assets/_Project/Prefabs/Enemies");
            JudgeBossConfig config = AssetDatabase.LoadAssetAtPath<JudgeBossConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<JudgeBossConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            // Existing config tuning is deliberately never overwritten.
            EditorUtility.SetDirty(config);
            Material robe = MaterialAsset("Mat_Judge_Robe", new Color(0.025f, 0.018f, 0.028f),
                new Color(0.001f, 0.001f, 0.001f));
            Material phase2 = MaterialAsset("Mat_Judge_Robe_Phase2", new Color(0.12f, 0.008f, 0.015f),
                new Color(0.8f, 0.008f, 0.02f));
            Material kneeling = MaterialAsset("Mat_Judge_Robe_Kneeling", new Color(0.3f, 0.31f, 0.28f), Color.black);
            Material mask = MaterialAsset("Mat_Judge_Mask", new Color(0.87f, 0.82f, 0.67f),
                new Color(0.3f, 0.27f, 0.2f));
            Material trim = MaterialAsset("Mat_Judge_Trim", new Color(0.45f, 0.34f, 0.14f),
                new Color(0.22f, 0.15f, 0.045f));
            Material steel = MaterialAsset("Mat_Judge_SwordSteel", new Color(0.38f, 0.43f, 0.48f),
                new Color(0.015f, 0.018f, 0.02f), 0.8f);
            Material marker = MaterialAsset("Mat_Judge_HazardMarker", new Color(1f, 0.08f, 0.025f, 1f),
                new Color(2f, 0.12f, 0.03f));

            Scene original = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                EnemyProjectile projectile = BuildProjectile(steel, robe);
                GameObject prefab = BuildPrefab(config, projectile, robe, phase2, kneeling, mask, trim, steel, marker);
                Material bone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Data/Materials/Araf/Mat_Araf_Bone.mat")
                    ?? MaterialAsset("Mat_Judge_Bone", new Color(0.55f, 0.5f, 0.42f), Color.black);
                GameObject cachePrefab = BuildAmmoCachePrefab(bone, mask);
                AssetDatabase.SaveAssets();
                if (!tower.IsValid() || !tower.isLoaded)
                    tower = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                WireScene(tower, prefab, cachePrefab);
                EditorSceneManager.MarkSceneDirty(tower);
                if (!EditorSceneManager.SaveScene(tower))
                    throw new InvalidOperationException("Could not save TestLevel_Gauntlet.");
                Debug.Log("[JudgeBossBuilder] Updated Judge config, materials, sword and boss prefabs, and the Gauntlet boss encounter.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scratch, true);
            }
        }

        private static EnemyProjectile BuildProjectile(Material steel, Material gripMaterial)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(ProjectilePath)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.name = "Sword_Judge";
                root.layer = GameLayers.Projectile;
                root.transform.localScale = new Vector3(0.16f, 0.12f, 1.25f);
                root.GetComponent<Renderer>().sharedMaterial = steel;
                Collider collider = root.GetComponent<Collider>();
                collider.isTrigger = true;
                Ensure<EnemyProjectile>(root);
                // The trail is present before Initialize so launching does not add components.
                TrailRenderer trail = Ensure<TrailRenderer>(root);
                trail.sharedMaterial = steel;
                Primitive(root.transform, "Guard", PrimitiveType.Cube, new Vector3(0f, 0f, -0.36f),
                    new Vector3(3.2f, 1.5f, 0.06f), steel);
                Primitive(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, 0f, -0.56f),
                    new Vector3(0.65f, 0.7f, 0.3f), gripMaterial);
                SetLayers(root.transform, GameLayers.Projectile);
                SerializedObject so = new SerializedObject(root.GetComponent<EnemyProjectile>());
                so.FindProperty("collisionRadius").floatValue = 0.14f;
                so.ApplyModifiedPropertiesWithoutUndo();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
                return saved.GetComponent<EnemyProjectile>();
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPrefab(JudgeBossConfig config, EnemyProjectile projectile,
            Material robe, Material phase2, Material kneeling, Material mask, Material trim, Material steel, Material marker)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Boss_Judge");
            try
            {
                root.name = "Boss_Judge";
                JudgeBossAI boss = Ensure<JudgeBossAI>(root);
                CharacterController controller = Ensure<CharacterController>(root);
                controller.height = 3f;
                controller.radius = 0.75f;
                controller.center = new Vector3(0f, 1.5f, 0f);
                controller.stepOffset = 0.15f;
                Transform model = Child(root.transform, "Body");
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one;
                // Stepped cylinders form a tapered floating robe without a custom mesh asset.
                var body = new List<Renderer>();
                body.Add(Primitive(model, "Robe_Lower", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.48f, 0f), new Vector3(1.85f, 0.48f, 1.35f), robe).GetComponent<Renderer>());
                body.Add(Primitive(model, "Robe_Middle", PrimitiveType.Cylinder,
                    new Vector3(0f, 1.28f, 0f), new Vector3(1.5f, 0.4f, 1.1f), robe).GetComponent<Renderer>());
                body.Add(Primitive(model, "Robe_Upper", PrimitiveType.Cylinder,
                    new Vector3(0f, 1.95f, 0f), new Vector3(1.15f, 0.3f, 0.85f), robe).GetComponent<Renderer>());
                body.Add(Primitive(model, "Shoulders", PrimitiveType.Cube,
                    new Vector3(0f, 2.25f, 0f), new Vector3(2f, 0.35f, 0.85f), robe).GetComponent<Renderer>());
                Transform head = Primitive(model, "Mask_Head", PrimitiveType.Cube,
                    new Vector3(0f, 2.7f, 0.12f), new Vector3(0.75f, 0.6f, 0.4f), mask);
                body.Add(head.GetComponent<Renderer>());
                body.Add(Primitive(model, "Robe_HemTrim", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.08f, 0f), new Vector3(1.9f, 0.04f, 1.4f), trim).GetComponent<Renderer>());
                body.Add(Primitive(model, "Robe_CollarTrim", PrimitiveType.Cube,
                    new Vector3(0f, 2.4f, 0.05f), new Vector3(1.35f, 0.1f, 0.9f), trim).GetComponent<Renderer>());
                BoxCollider headCollider = Ensure<BoxCollider>(head.gameObject);
                headCollider.isTrigger = false;
                Hitbox hitbox = Ensure<Hitbox>(head.gameObject);
                SetReference(hitbox, "targetEntityComponent", boss);
                Primitive(head, "EyeSlit", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.51f),
                    new Vector3(0.7f, 0.08f, 0.04f), robe);

                Transform ringRoot = Child(root.transform, "SwordRing");
                JudgeSwordRing ring = Ensure<JudgeSwordRing>(ringRoot.gameObject);
                int count = Mathf.Max(1, config.RingSwordCount);
                var swords = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    Transform sword = Sword(ringRoot, "RingSword_" + i, steel, robe);
                    float angle = i * Mathf.PI * 2f / count;
                    sword.localPosition = new Vector3(Mathf.Cos(angle) * 2.5f, 1.5f, Mathf.Sin(angle) * 2.5f);
                    swords[i] = sword;
                }
                for (int i = ringRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = ringRoot.GetChild(i);
                    if (child.name.StartsWith("RingSword_", StringComparison.Ordinal)
                        && int.TryParse(child.name.Substring("RingSword_".Length), out int slot) && slot >= count)
                        Object.DestroyImmediate(child.gameObject);
                }
                SetReferences(ring, "swords", swords);
                Transform greatBlade = Sword(root.transform, "GreatBlade", steel, robe);
                greatBlade.localScale = Vector3.one * 2.5f;
                greatBlade.localPosition = new Vector3(1.3f, 1.3f, 0f);
                greatBlade.gameObject.SetActive(false);

                Transform hazards = Child(root.transform, "Hazards");
                var guillotines = new JudgeGuillotineStrike[3];
                for (int i = 0; i < guillotines.Length; i++)
                {
                    Transform hazard = Child(hazards, "Guillotine_" + i);
                    guillotines[i] = Ensure<JudgeGuillotineStrike>(hazard.gameObject);
                    Transform disc = Primitive(hazard, "Marker", PrimitiveType.Cylinder,
                        Vector3.up * 0.025f, new Vector3(1f, 0.025f, 1f), marker);
                    Transform blade = Sword(hazard, "Blade", steel, robe);
                    blade.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    SetReference(guillotines[i], "marker", disc);
                    SetReference(guillotines[i], "blade", blade);
                    disc.gameObject.SetActive(false);
                    blade.gameObject.SetActive(false);
                }
                Transform sweepRoot = Child(hazards, "ScaleSweep");
                JudgeScaleSweep sweep = Ensure<JudgeScaleSweep>(sweepRoot.gameObject);
                Transform bar = Primitive(sweepRoot, "Bar", PrimitiveType.Cube,
                    Vector3.zero, Vector3.one, marker);
                SetReference(sweep, "bar", bar);
                bar.gameObject.SetActive(false);
                Transform repulseRoot = Child(hazards, "RepulseWarning");
                JudgeRepulseWarning repulse = Ensure<JudgeRepulseWarning>(repulseRoot.gameObject);
                Transform fill = Primitive(repulseRoot, "Fill", PrimitiveType.Cylinder,
                    Vector3.zero, Vector3.one, marker);
                Transform boundary = Child(repulseRoot, "Boundary");
                LineRenderer outline = Ensure<LineRenderer>(boundary.gameObject);
                outline.useWorldSpace = false;
                outline.loop = true;
                outline.sharedMaterial = marker;
                outline.startColor = outline.endColor = Color.white;
                outline.widthMultiplier = config.RepulseMarkerThickness;
                outline.positionCount = Mathf.Max(8, config.RepulseMarkerSegments);
                for (int i = 0; i < outline.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / outline.positionCount;
                    outline.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                }
                SetReference(repulse, "fill", fill);
                SetReference(repulse, "outline", outline);
                repulse.Cancel();
                var lines = new JudgeShockLine[2];
                for (int i = 0; i < lines.Length; i++)
                {
                    Transform lineRoot = Child(hazards, "ShockLine_" + i);
                    lines[i] = Ensure<JudgeShockLine>(lineRoot.gameObject);
                    Transform wall = Primitive(lineRoot, "LowWall", PrimitiveType.Cube,
                        Vector3.zero, Vector3.one, marker);
                    SetReference(lines[i], "visual", wall);
                    wall.gameObject.SetActive(false);
                }
                // Same Enemy layer and CharacterController + child Hitbox arrangement as the eye.
                SetLayers(root.transform, GameLayers.Enemy);
                // Decorative swords and hazard visuals have no colliders and must never block queries.
                SetLayers(hazards, LayerMask.NameToLayer("Ignore Raycast"));
                SetReference(boss, "config", config);
                SetReference(boss, "swordRing", ring);
                SetReference(boss, "swordProjectilePrefab", projectile);
                SetReference(boss, "greatBlade", greatBlade);
                SetReference(boss, "hazardsRoot", hazards);
                SetReferences(boss, "guillotines", guillotines);
                SetReference(boss, "sweep", sweep);
                SetReference(boss, "repulseWarning", repulse);
                SetReferences(boss, "shockLines", lines);
                SetReferences(boss, "bodyRenderers", body.ToArray());
                SetReference(boss, "phase2Material", phase2);
                SetReference(boss, "kneelingMaterial", kneeling);
                SetReference(boss, "ammoRewards", Ensure<ArenaAmmoRewards>(root));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void WireScene(Scene scene, GameObject prefab, GameObject cachePrefab)
        {
            Transform placeholder = FindInScene(scene, "BossEncounter_Placeholder");
            if (placeholder == null) throw new InvalidOperationException("Gauntlet boss placeholder is missing.");
            Transform bounds = placeholder.Find("Boss_ArenaBounds");
            Transform anchor = placeholder.Find("Boss_SpawnAnchor");
            Transform checkpoint = placeholder.Find("Boss_PlayerCheckpoint");
            BoxCollider trigger = bounds != null ? bounds.GetComponent<BoxCollider>() : null;
            if (trigger == null || anchor == null || checkpoint == null)
                throw new InvalidOperationException("Boss_ArenaBounds, Boss_SpawnAnchor or Boss_PlayerCheckpoint is missing.");
            trigger.isTrigger = true;
            BossEncounter encounter = Ensure<BossEncounter>(placeholder.gameObject);
            JudgeBossAI boss = placeholder.GetComponentInChildren<JudgeBossAI>(true);
            if (boss == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(placeholder, true);
                boss = instance.GetComponent<JudgeBossAI>();
            }
            else if (PrefabUtility.GetCorrespondingObjectFromSource(boss.gameObject) != prefab)
                throw new InvalidOperationException("Existing boss is not an instance of Boss_Judge; resolve it before rebuilding.");
            boss.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss.transform);
            SetReference(boss, "groundReference", checkpoint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
            SetReference(encounter, "boss", boss);
            SetReference(encounter, "arenaTrigger", trigger);
            SetReference(encounter, "retryCheckpoint", checkpoint);
            SetReference(encounter, "bossSpawnAnchor", anchor);
            var caches = new ArenaAmmoCache[4];
            Bounds arena = trigger.bounds;
            for (int i = 0; i < caches.Length; i++)
            {
                string cacheName = "Boss_AmmoCache_" + i;
                Transform cache = placeholder.Find(cacheName);
                if (cache == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(cachePrefab, scene);
                    instance.name = cacheName;
                    instance.transform.SetParent(placeholder, true);
                    cache = instance.transform;
                }
                // Bounds are inset from the room shell. These points also clear the x=+/-17 pillars.
                cache.position = new Vector3((i & 1) == 0 ? arena.min.x + AmmoCacheInset : arena.max.x - AmmoCacheInset,
                    arena.min.y, (i & 2) == 0 ? arena.min.z + AmmoCacheInset : arena.max.z - AmmoCacheInset);
                cache.rotation = Quaternion.identity;
                caches[i] = cache.GetComponent<ArenaAmmoCache>();
                if (caches[i] == null) throw new InvalidOperationException("Boss ammo cache has no ArenaAmmoCache component.");
                PrefabUtility.RecordPrefabInstancePropertyModifications(cache);
            }
            SetReferences(encounter, "ammoCaches", caches);
            var gates = new List<GameObject>();
            CollectGates(placeholder, gates);
            // TestLevelBuilder currently creates no boss gates. Only consume gates under the boss
            // placeholder or its dedicated geometry; never steal another room's encounter gates.
            Transform geometry = FindInScene(scene, "I_BossRoom");
            if (geometry != null) CollectGates(geometry, gates);
            SetReferences(encounter, "barriers", gates.ToArray());
            EditorUtility.SetDirty(encounter);
        }

        private static GameObject BuildAmmoCachePrefab(Material bone, Material readyMaterial)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoCachePath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(AmmoCachePath) : new GameObject("Judge_ArenaAmmoCache");
            try
            {
                ArenaAmmoCache cache = Ensure<ArenaAmmoCache>(root);
                Primitive(root.transform, "Pedestal", PrimitiveType.Cylinder, Vector3.up * 0.25f,
                    new Vector3(1.2f, 0.25f, 1.2f), bone);
                Transform ready = Primitive(root.transform, "Ready", PrimitiveType.Cylinder, Vector3.up * 0.53f,
                    new Vector3(0.8f, 0.03f, 0.8f), readyMaterial);
                ready.gameObject.SetActive(false);
                SetReference(cache, "readyVisual", ready.gameObject);
                SetLayers(root.transform, LayerMask.NameToLayer("Ignore Raycast"));
                return PrefabUtility.SaveAsPrefabAsset(root, AmmoCachePath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void CollectGates(Transform parent, List<GameObject> result)
        {
            foreach (Transform child in parent)
            {
                if ((child.name.StartsWith("Gate_", StringComparison.Ordinal)
                    || child.name.StartsWith("Boss_Gate", StringComparison.Ordinal)) && !result.Contains(child.gameObject))
                    result.Add(child.gameObject);
                else CollectGates(child, result);
            }
        }

        private static Transform FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDescendant(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform Sword(Transform parent, string name, Material steel, Material grip)
        {
            Transform root = Child(parent, name);
            Primitive(root, "Blade", PrimitiveType.Cube, Vector3.up * 0.55f,
                new Vector3(0.16f, 1.2f, 0.07f), steel);
            Primitive(root, "Guard", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.5f, 0.1f, 0.12f), steel);
            Primitive(root, "Grip", PrimitiveType.Cube, Vector3.down * 0.22f,
                new Vector3(0.1f, 0.35f, 0.1f), grip);
            return root;
        }

        private static Transform Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                GameObject go = GameObject.CreatePrimitive(type);
                go.name = name;
                existing = go.transform;
                existing.SetParent(parent, false);
            }
            existing.localPosition = position;
            existing.localRotation = Quaternion.identity;
            existing.localScale = scale;
            existing.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = existing.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return existing;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetLayers(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayers(child, layer);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences<T>(Object target, string field, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }

        private static Material MaterialAsset(string name, Color color, Color emission,
            float metallic = 0f, bool transparent = false)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", metallic);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                // Rebuilds must also migrate a previously transparent marker to an opaque rim.
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.SetOverrideTag("RenderType", "Opaque");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Geometry;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
