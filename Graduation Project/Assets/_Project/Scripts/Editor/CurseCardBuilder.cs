using System;
using RetroFPS.Run.Cards;
using RetroFPS.Run.Labyrinth;
using RetroFPS.Stats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Creates the fifteen curse cards, altar prefab and Araf scene references in place.</summary>
    public static class CurseCardBuilder
    {
        private const string CardDir = "Assets/_Project/Data/Araf/Cards";
        private const string CatalogPath = CardDir + "/CardCatalog_Default.asset";
        private const string PrefabPath = "Assets/_Project/Prefabs/Araf/CardAltar.prefab";
        private const string MaterialDir = "Assets/_Project/Data/Materials/Araf";
        private const string ScenePath = "Assets/Scenes/Araf.unity";

        /// <summary>Updates card assets and the altar, then wires the Araf scene.</summary>
        [MenuItem("RetroFPS/Build Curse Cards")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Curse Cards must run outside Play Mode.");
            Material bone = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Mat_Araf_Bone.mat");
            if (bone == null) throw new InvalidOperationException("Build Araf first: its Bone material is required.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Curse cards require URP Lit.");
            EnsureFolder(CardDir);
            EnsureFolder("Assets/_Project/Prefabs/Araf");

            CurseBlessingDefinition[] cards =
            {
                Card("blood_debt", CardRarity.Common, new Color(0.44f, 0.22f, 0.24f),
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.3f),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.25f)),
                Card("starving_steel", CardRarity.Rare, new Color(0.24f, 0.48f, 0.88f),
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.45f),
                    new StatModifier(StatId.HarvestHeal, StatOp.Multiply, 1.75f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0f)),
                Card("last_breath", CardRarity.Rare, new Color(0.65f, 0.28f, 0.85f),
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.5f, StatCondition.HealthBelowHalf),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.2f, StatCondition.HealthBelowHalf),
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, -0.35f)),
                Card("bone_wings", CardRarity.Common, new Color(0.64f, 0.6f, 0.48f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.8f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.12f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, 0.25f)),
                Card("brimming_chalice", CardRarity.Common, new Color(0.56f, 0.43f, 0.28f),
                    new StatModifier(StatId.OverhealCap, StatOp.AddFlat, 50f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.35f, StatCondition.Overhealed),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.15f, StatCondition.HealthBelowHalf)),
                Card("glutton_barrel", CardRarity.Common, new Color(0.6f, 0.34f, 0.24f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.4f),
                    new StatModifier(StatId.AmmoPickupAmount, StatOp.AddPercent, -0.5f)),
                Card("iron_hide", CardRarity.Common, new Color(0.39f, 0.43f, 0.44f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.2f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.1f)),
                Card("carrion_crow", CardRarity.Common, new Color(0.34f, 0.32f, 0.4f),
                    new StatModifier(StatId.AmmoPickupAmount, StatOp.AddPercent, 0.75f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0.75f)),
                Card("pilgrims_flask", CardRarity.Common, new Color(0.4f, 0.51f, 0.41f),
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, 0.35f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 1.5f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, -0.12f)),
                Card("gilded_vein", CardRarity.Rare, new Color(0.89f, 0.48f, 0.12f),
                    new StatModifier(StatId.OverhealCap, StatOp.AddFlat, 75f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.25f, StatCondition.Overhealed),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.2f)),
                Card("executioners_oath", CardRarity.Rare, new Color(0.78f, 0.12f, 0.22f),
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.75f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.8f),
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, -0.2f)),
                Card("bell_tower", CardRarity.Rare, new Color(0.22f, 0.58f, 0.79f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.1f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.15f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 1.2f)),
                Card("judges_scale", CardRarity.Relic, new Color(0.95f, 0.76f, 0.3f),
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.6f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, 0.4f)),
                Card("hollow_saint", CardRarity.Relic, new Color(0.94f, 0.9f, 0.77f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.625f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.25f),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.25f)),
                Card("red_hour", CardRarity.Relic, new Color(0.88f, 0.66f, 0.3f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.15f),
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, -0.25f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0.5f))
            };
            CardCatalog catalog = LoadOrCreate<CardCatalog>(CatalogPath);
            catalog.Configure(cards, 10f, 5f, 1.5f);
            EditorUtility.SetDirty(catalog);
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Mat_Araf_CardGlow.mat");
            if (glow == null)
            {
                glow = new Material(shader);
                AssetDatabase.CreateAsset(glow, MaterialDir + "/Mat_Araf_CardGlow.mat");
            }
            glow.shader = shader;
            glow.SetColor("_BaseColor", new Color(0.32f, 0.025f, 0.035f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1.5f, 0.045f, 0.025f));
            // EmissiveIsBlack would let URP validation strip _EMISSION on save.
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow);

            Scene original = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                BuildAltar(bone, glow);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scratch, true);
            }
            AssetDatabase.SaveAssets();
            WireArafScene();
            Debug.Log("[CurseCardBuilder] Updated fifteen curse cards, default catalog, altar prefab and Araf wiring.");
        }

        private static CurseBlessingDefinition Card(string id, CardRarity rarity, Color accent, params StatModifier[] modifiers)
        {
            CurseBlessingDefinition card = LoadOrCreate<CurseBlessingDefinition>(CardDir + "/Card_" + id + ".asset");
            string key = "hud.card." + id + ".";
            card.Configure(id, key + "name", key + "boon", key + "curse", key + "flavor", accent, modifiers, rarity);
            EditorUtility.SetDirty(card);
            return card;
        }

        private static void BuildAltar(Material bone, Material glow)
        {
            GameObject root = new GameObject("CardAltar");
            try
            {
                SphereCollider trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.6f;
                trigger.center = Vector3.up;
                ArafCardAltar altar = root.AddComponent<ArafCardAltar>();
                Primitive(root.transform, "BonePedestal", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.5f, 0f), new Vector3(0.85f, 0.5f, 0.85f), bone, true);
                GameObject active = new GameObject("ActiveVisual");
                active.transform.SetParent(root.transform, false);
                GameObject left = Primitive(active.transform, "Card_Left", PrimitiveType.Cube,
                    new Vector3(-0.3f, 1.7f, 0f), new Vector3(0.38f, 0.75f, 0.045f), glow, false);
                left.transform.localRotation = Quaternion.Euler(0f, -20f, -10f);
                GameObject right = Primitive(active.transform, "Card_Right", PrimitiveType.Cube,
                    new Vector3(0.3f, 1.7f, 0f), new Vector3(0.38f, 0.75f, 0.045f), glow, false);
                right.transform.localRotation = Quaternion.Euler(0f, 20f, 10f);
                GameObject lightObject = new GameObject("CardLight");
                lightObject.transform.SetParent(active.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 2f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.8f, 0.035f, 0.025f);
                light.intensity = 1.2f;
                light.range = 4f;
                light.shadows = LightShadows.None;
                // The shared bone pedestal remains when the glowing cards and light go out.
                SerializedObject settings = new SerializedObject(altar);
                settings.FindProperty("activeVisual").objectReferenceValue = active;
                settings.FindProperty("spentVisual").objectReferenceValue = null;
                settings.FindProperty("armDelay").floatValue = 0.75f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("Could not save CardAltar prefab.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material, bool solid)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(visual.GetComponent<Collider>());
            return visual;
        }

        /// <summary>Wires existing card assets into Araf, preserving the previous open scene setup.</summary>
        public static void WireArafScene()
        {
            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (catalog == null || prefab == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Wire Araf cards must run outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[CurseCardBuilder] Araf scene not found; build Araf first.");
                return;
            }
            Scene original = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded && scene.isDirty)
                throw new InvalidOperationException("Save Araf before wiring curse cards.");
            try
            {
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                ArafLabyrinthDirector director = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    director = root.GetComponentInChildren<ArafLabyrinthDirector>(true);
                    if (director != null) break;
                }
                if (director == null)
                {
                    Debug.LogError("[CurseCardBuilder] ArafLabyrinthDirector not found in Araf.");
                    return;
                }
                SerializedObject settings = new SerializedObject(director);
                settings.FindProperty("cardCatalog").objectReferenceValue = catalog;
                settings.FindProperty("cardAltarPrefab").objectReferenceValue = prefab.GetComponent<ArafCardAltar>();
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Araf card wiring.");
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new InvalidOperationException("Could not create folder: " + path);
        }
    }
}
