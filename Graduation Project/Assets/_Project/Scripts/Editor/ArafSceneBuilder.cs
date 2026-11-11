using System;
using System.Collections.Generic;
using RetroFPS.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Araf sahnesini ve kuledeki RunDirector prefab bağlantısını kurar.</summary>
    public static class ArafSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Araf.unity";
        private const string TowerPath = "Assets/Scenes/TestLevel_Gauntlet.unity";
        private const string DirectorPath = "Assets/_Project/Prefabs/Systems/RunDirector.prefab";
        private const string MatDir = "Assets/_Project/Data/Materials/Araf";
        private const string GhoulPath = "Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab";
        private const string EyePath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";
        private static readonly Dictionary<Vector4, Mesh> MeshCache = new Dictionary<Vector4, Mesh>();

        [MenuItem("RetroFPS/Build Araf Scene + RunDirector", false, 21)]
        private static void BuildMenu() => BuildAll();

        /// <summary>0: başarılı; -1: Play Mode; -2: kirli sahne; -3: eksik bağımlılık;
        /// -4: kurulum/kayıt hatası; -5: önceki sahneye dönüş hatası.</summary>
        public static int BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[ArafSceneBuilder] Play Mode'da çalışmaz.");
                return -1;
            }

            Scene original = SceneManager.GetActiveScene();
            if (original.isDirty)
            {
                Debug.LogError("[ArafSceneBuilder] Aktif sahnede kaydedilmemiş değişiklik var; önce kaydedin.");
                return -2;
            }

            string originalPath = original.path;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TowerPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(EyePath) == null ||
                Shader.Find("Universal Render Pipeline/Lit") == null)
            {
                Debug.LogError("[ArafSceneBuilder] Kule sahnesi, düşman prefabları veya URP Lit shader bulunamadı.");
                return -3;
            }

            int result = 0;
            try
            {
                // Prefab için geçici sahne: önceki sahneye geçici nesne eklenmez.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder("Assets/_Project/Prefabs/Systems");
                BuildDirectorPrefab();

                EnsureFolder(MatDir);
                Material bone = MaterialAsset("Mat_Araf_Bone", new Color(0.55f, 0.5f, 0.42f));
                Material floor = MaterialAsset("Mat_Araf_Floor", new Color(0.12f, 0.04f, 0.04f));
                Material portal = MaterialAsset("Mat_Araf_Portal", new Color(0.6f, 0f, 0.02f), true);
                // NewScene kullanılmayan assetleri boşaltmadan önce materyalleri kaydet.
                AssetDatabase.SaveAssets();

                Scene araf = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                MeshCache.Clear();
                // Sahne değişiminden sonra asset referanslarını yeniden yükle.
                bone = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Araf_Bone.mat");
                floor = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Araf_Floor.mat");
                portal = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Araf_Portal.mat");
                BuildLevel(bone, floor, portal);
                ApplyAtmosphere();
                EnsureFolder("Assets/Scenes");
                if (!EditorSceneManager.SaveScene(araf, ScenePath))
                    throw new InvalidOperationException("Araf sahnesi kaydedilemedi.");

                AddToBuildSettings();

                Scene tower = EditorSceneManager.OpenScene(TowerPath, OpenSceneMode.Single);
                bool hasDirector = false;
                foreach (GameObject root in tower.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<RunDirector>(true) != null)
                    {
                        hasDirector = true;
                        break;
                    }
                }
                if (!hasDirector)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPath);
                    if (PrefabUtility.InstantiatePrefab(prefab, tower) == null)
                        throw new InvalidOperationException("RunDirector prefab örneği oluşturulamadı.");
                }
                EditorSceneManager.MarkSceneDirty(tower);
                if (!EditorSceneManager.SaveScene(tower))
                    throw new InvalidOperationException("Kule sahnesi kaydedilemedi.");
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArafSceneBuilder] Kurulum başarısız: " + exception);
                result = -4;
            }
            finally
            {
                MeshCache.Clear();
                try
                {
                    if (!string.IsNullOrEmpty(originalPath))
                    {
                        if (SceneManager.GetActiveScene().path != originalPath)
                            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                    }
                    else
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[ArafSceneBuilder] Önceki sahne açılamadı: " + exception);
                    result = -5;
                }
            }

            if (result == 0)
                Debug.Log("<color=green>[ArafSceneBuilder]</color> Araf ve RunDirector kuruldu. Düşmanlar bağımsız prefab örnekleridir; oyuncuyu kendileri bulur.");
            return result;
        }

        private static void BuildDirectorPrefab()
        {
            GameObject director = new GameObject("RunDirector");
            try
            {
                director.AddComponent<RunDirector>();
                director.AddComponent<ArafTransition>();
                PrefabUtility.SaveAsPrefabAsset(director, DirectorPath, out bool success);
                if (!success) throw new InvalidOperationException("RunDirector prefab kaydedilemedi.");
            }
            finally
            {
                Object.DestroyImmediate(director);
            }
        }

        private static Material MaterialAsset(string name, Color color, bool emissive = false)
        {
            string path = MatDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(3f, 0.05f, 0.1f));
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildLevel(Material bone, Material floor, Material portal)
        {
            GameObject root = new GameObject("ArafLevel");
            // Pozitif Y: PlayerMovement Y<0'da oyuncuyu zemine (Y=0.05) geri çeker, FlyingEyeAI de zemini ~Y=0 varsayar.
            root.transform.position = new Vector3(0f, 1000f, 0f);
            ArafLevel level = root.AddComponent<ArafLevel>();
            Transform entry = Child(root.transform, "EntryPoint");
            entry.localPosition = new Vector3(-16f, 0.1f, -16f);
            Transform content = Child(root.transform, "Content");
            SerializedObject settings = new SerializedObject(level);
            settings.FindProperty("entryPoint").objectReferenceValue = entry;
            settings.FindProperty("contentRoot").objectReferenceValue = content.gameObject;
            settings.ApplyModifiedPropertiesWithoutUndo();

            Solid(content, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f), floor);
            Solid(content, "Ceiling", new Vector3(0f, 5.25f, 0f), new Vector3(40f, 0.5f, 40f), bone);
            Solid(content, "Wall_W", new Vector3(-20.3f, 2.5f, 0f), new Vector3(0.6f, 5f, 40f), bone);
            Solid(content, "Wall_E", new Vector3(20.3f, 2.5f, 0f), new Vector3(0.6f, 5f, 40f), bone);
            Solid(content, "Wall_S", new Vector3(0f, 2.5f, -20.3f), new Vector3(41.2f, 5f, 0.6f), bone);
            Solid(content, "Wall_N", new Vector3(0f, 2.5f, 20.3f), new Vector3(41.2f, 5f, 0.6f), bone);

            // Altı segment, üç dönüş: 8 m açıklıklar ve en az 9.4 m ara koridorlar.
            // Kesintisiz rota: (-16,-16) -> (16,-16) -> (16,-5) -> (-16,-5)
            // -> (-16,5) -> (16,5) -> (16,16). Köşe başlangıç koridoru 9.7 m.
            for (int row = 0; row < 3; row++)
            {
                float startX = row == 1 ? -12f : -20f;
                float z = -10f + row * 10f;
                for (int segment = 0; segment < 2; segment++)
                    Solid(content, "MazeWall_" + row + "_" + segment,
                        new Vector3(startX + 8f + segment * 16f, 2.5f, z),
                        new Vector3(16f, 5f, 0.6f), bone);
            }

            Transform bloodPortal = Child(content, "BloodPortal");
            bloodPortal.localPosition = new Vector3(16f, 0f, 16f);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "PortalVisual";
            visual.transform.SetParent(bloodPortal, false);
            visual.transform.localPosition = new Vector3(0f, 2f, 0f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = new Vector3(2.5f, 0.12f, 4f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = portal;
            GameObject trigger = Child(bloodPortal, "EscapeTrigger").gameObject;
            BoxCollider box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 4f, 3f);
            box.center = new Vector3(0f, 2f, 0f);
            trigger.AddComponent<ArafEscapePortal>();
            PointLight(bloodPortal, "Portal_PointLight", new Vector3(0f, 2f, 0f), Color.red, 10f, 3f);

            Vector3[] lights =
            {
                new Vector3(-16f, 3f, -16f), new Vector3(0f, 3f, -16f),
                new Vector3(16f, 3f, -6f), new Vector3(-8f, 3f, -5f),
                new Vector3(-16f, 3f, 5f), new Vector3(8f, 3f, 5f),
                new Vector3(16f, 3f, 12f), new Vector3(-8f, 3f, 16f)
            };
            for (int i = 0; i < lights.Length; i++)
                PointLight(content, "Crimson_PointLight_" + i, lights[i], new Color(0.8f, 0.08f, 0.06f), 9f, 1.5f);

            // BasicEnemyAI ve FlyingEyeAI Awake/Start içinde oyuncuyu bulur ve Idle başlar;
            // WaveSpawnDirector veya NavMesh olmadan kendi devriyelerini yürütürler.
            GameObject ghoul = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
            GameObject eye = AssetDatabase.LoadAssetAtPath<GameObject>(EyePath);
            Enemy(content, ghoul, new Vector3(16f, 0.05f, -5f), 270f);
            Enemy(content, ghoul, new Vector3(-16f, 0.05f, 5f), 90f);
            Enemy(content, ghoul, new Vector3(16f, 0.05f, 12f), 180f);
            Enemy(content, eye, new Vector3(4f, 3f, 5f), 270f);
        }

        private static void Enemy(Transform parent, GameObject prefab, Vector3 position, float yaw)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.transform.localPosition = position;
            enemy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void PointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
        {
            Transform child = Child(parent, name);
            child.localPosition = position;
            Light light = child.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        private static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.18f, 0.02f, 0.03f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 38f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.04f, 0.04f);
        }

        private static void Solid(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            Transform child = Child(parent, name);
            child.localPosition = center;
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, 4f);
            child.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            child.gameObject.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        }

        private static Mesh BoxMesh(Vector3 size, float tile)
        {
            Vector4 key = new Vector4(size.x, size.y, size.z, tile);
            if (MeshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            Vector3 half = size * 0.5f;
            Vector3[] vertices = new Vector3[24];
            Vector3[] normals = new Vector3[24];
            Vector2[] uv = new Vector2[24];
            int[] triangles = new int[36];
            Vector3[] n = { Vector3.back, Vector3.forward, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            Vector3[] u = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.right, Vector3.forward };
            Vector3[] v = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.right };
            for (int face = 0; face < 6; face++)
            {
                float hu = Mathf.Abs(Vector3.Dot(u[face], half));
                float hv = Mathf.Abs(Vector3.Dot(v[face], half));
                Vector3 center = n[face] * Mathf.Abs(Vector3.Dot(n[face], half));
                int i = face * 4;
                vertices[i] = center - u[face] * hu - v[face] * hv;
                vertices[i + 1] = center + u[face] * hu - v[face] * hv;
                vertices[i + 2] = center - u[face] * hu + v[face] * hv;
                vertices[i + 3] = center + u[face] * hu + v[face] * hv;
                uv[i] = Vector2.zero;
                uv[i + 1] = new Vector2(2f * hu / tile, 0f);
                uv[i + 2] = new Vector2(0f, 2f * hv / tile);
                uv[i + 3] = new Vector2(2f * hu / tile, 2f * hv / tile);
                for (int k = 0; k < 4; k++) normals[i + k] = n[face];
                int t = face * 6;
                triangles[t] = i; triangles[t + 1] = i + 2; triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 2; triangles[t + 4] = i + 3; triangles[t + 5] = i + 1;
            }
            Mesh mesh = new Mesh { name = "ArafBox_" + key, vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            MeshCache[key] = mesh;
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new InvalidOperationException("Klasör oluşturulamadı: " + path);
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath) continue;
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
