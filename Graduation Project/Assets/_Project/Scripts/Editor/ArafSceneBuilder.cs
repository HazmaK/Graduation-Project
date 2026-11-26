using System;
using System.Collections.Generic;
using System.IO;
using RetroFPS.Combat;
using RetroFPS.Combat.Navigation;
using RetroFPS.Run;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Cards;
using RetroFPS.Run.Labyrinth;
using RetroFPS.UI;
using RetroFPS.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Araf labirent sahnesini, kit/oda prefablarını ve kuledeki RunDirector bağlantısını kurar.</summary>
    public static class ArafSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Araf.unity";
        private const string TowerPath = "Assets/Scenes/TestLevel_Gauntlet.unity";
        private const string DirectorPath = "Assets/_Project/Prefabs/Systems/RunDirector.prefab";
        private const string MatDir = "Assets/_Project/Data/Materials/Araf";
        private const string ArafPrefabDir = "Assets/_Project/Prefabs/Araf";
        private const string KitPrefabDir = ArafPrefabDir + "/Kit";
        private const string RoomPrefabDir = ArafPrefabDir + "/Rooms";
        private const string HubPortalPrefabPath = ArafPrefabDir + "/Araf_HubPortal.prefab";
        private const string SealBeaconPrefabPath = ArafPrefabDir + "/Araf_SealBeacon.prefab";
        private const string SealLightPillarPrefabPath = ArafPrefabDir + "/Araf_SealLightPillar.prefab";
        private const string DebrisArchPrefabPath = KitPrefabDir + "/Debris_Arch.prefab";
        private const string DebrisPillarPrefabPath = KitPrefabDir + "/Debris_Pillar.prefab";
        private const string DebrisMonolithPrefabPath = KitPrefabDir + "/Debris_Monolith.prefab";
        private const string SkyMatPath = MatDir + "/Mat_Araf_Sky.mat";
        private const string ArafDataDir = "Assets/_Project/Data/Araf";
        private const string SkyTexPath = ArafDataDir + "/Tex_Araf_Sky.png";
        private const string ArafAudioDir = "Assets/_Project/Art/Audio/Araf";
        private const string SealBeaconClipPath = ArafAudioDir + "/Sfx_Araf_SealBeacon.wav";
        private const string AwakeningStingClipPath = ArafAudioDir + "/Sfx_Araf_AwakeningSting.wav";
        private const bool RegenerateSky = false;
        private const float SunAzimuthDeg = 200f;
        private const float SunElevationDeg = 25f;
        private const string RoomDefDir = ArafDataDir + "/Rooms";
        private const string KitAssetPath = ArafDataDir + "/LabyrinthKit_Araf.asset";
        private const string RoomTableAssetPath = ArafDataDir + "/ArafRoomTable_Default.asset";
        private const string TuningAssetPath = ArafDataDir + "/ArafTuning_Default.asset";
        private const string GhoulPath = "Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab";
        private const string EyePath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";
        private const string PenitentPath = "Assets/_Project/Prefabs/Enemies/Enemy_Penitent_Kefaretci.prefab";

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
                AssetDatabase.LoadAssetAtPath<GameObject>(PenitentPath) == null ||
                Shader.Find("Universal Render Pipeline/Lit") == null)
            {
                Debug.LogError("[ArafSceneBuilder] Kule sahnesi, düşman prefabları veya URP Lit shader bulunamadı.");
                return -3;
            }

            int result = 0;
            try
            {
                // Prefab üretimi için geçici boş sahne.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EnsureFolder("Assets/_Project/Prefabs/Systems");
                BuildDirectorPrefab();

                EnsureFolder(MatDir);
                Material bone = MaterialAsset("Mat_Araf_Bone", new Color(0.42f, 0.38f, 0.50f));
                Material floor = MaterialAsset("Mat_Araf_Floor", new Color(0.07f, 0.04f, 0.11f));
                Material portal = MaterialAsset("Mat_Araf_Portal", new Color(0.6f, 0f, 0.02f), true);
                Material sealBeam = MaterialAsset("Mat_Araf_SealBeam", new Color(1f, 0.08f, 0.05f), true, new Color(4f, 0.32f, 0.2f));
                Material veil = VeilMaterialAsset("Mat_Araf_Veil", new Color(0.02f, 0f, 0.04f, 0.92f));
                Material doorStone = MaterialAsset("Mat_Araf_DoorStone", new Color(0.22f, 0.19f, 0.28f));
                Material rune = MaterialAsset("Mat_Araf_Rune", new Color(1f, 0.08f, 0.05f), true, new Color(4f, 0.32f, 0.2f));
                // Mat_Araf_Bone kuledeki Judge_ArenaAmmoCache'te de kullanılıyor; kuleye dokunmamak için
                // Araf kit/oda parçaları dokulu ayrı Mat_Araf_Wall kullanır, Bone düz kalır.
                Material wall = MaterialAsset("Mat_Araf_Wall", new Color(0.42f, 0.38f, 0.50f));
                // Yer tutucu dokular: var olan PNG'lere dokunmaz, yalnızca shader/doku bağlar.
                ArafPlaceholderTextures.Apply(wall, floor, doorStone);
                bone = wall;

                EnsureFolder(ArafPrefabDir);
                EnsureFolder(KitPrefabDir);
                EnsureFolder(RoomPrefabDir);
                EnsureFolder(ArafDataDir);
                EnsureFolder(RoomDefDir);

                MoveAssetIfPresent(RoomPrefabDir + "/Room_Exit.prefab", RoomPrefabDir + "/Room_Seal.prefab");
                MoveAssetIfPresent(RoomDefDir + "/ArafRoom_Exit.asset", RoomDefDir + "/ArafRoom_Seal.asset");

                BuildKitAndAsset(bone, floor, veil, doorStone, rune);
                BuildDebrisPrefabs(bone);

                GameObject ghoulPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
                GameObject eyePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EyePath);
                BuildRoomsAndTable(bone, portal, ghoulPrefab, eyePrefab);
                ArafEscapePortal hubPortalPrefab = BuildHubPortalPrefab(portal);
                ArafSealBeacon sealBeaconPrefab = BuildSealBeaconPrefab();
                ArafSealLightPillar sealLightPillarPrefab = BuildSealLightPillarPrefab(sealBeam);
                ArafTuning tuningAsset = BuildTuningAsset();

                // Yeni sahneye geçmeden önce tüm prefab ve ScriptableObject varlıklarını diske yaz.
                AssetDatabase.SaveAssets();

                Scene araf = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                LabyrinthKit kitAsset = AssetDatabase.LoadAssetAtPath<LabyrinthKit>(KitAssetPath);
                ArafRoomTable tableAsset = AssetDatabase.LoadAssetAtPath<ArafRoomTable>(RoomTableAssetPath);
                // NewScene kullanılmayan varlıkları boşaltır: referansları sahne açıldıktan sonra yeniden yükle.
                tuningAsset = AssetDatabase.LoadAssetAtPath<ArafTuning>(TuningAssetPath);
                hubPortalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HubPortalPrefabPath).GetComponent<ArafEscapePortal>();
                sealBeaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SealBeaconPrefabPath).GetComponent<ArafSealBeacon>();
                sealLightPillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SealLightPillarPrefabPath).GetComponent<ArafSealLightPillar>();
                ghoulPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
                eyePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EyePath);
                GameObject penitentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PenitentPath);
                GameObject[] debrisPrefabs =
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>(DebrisArchPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(DebrisPillarPrefabPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(DebrisMonolithPrefabPath)
                };
                BuildLevel(
                    kitAsset,
                    tableAsset,
                    tuningAsset,
                    hubPortalPrefab,
                    sealBeaconPrefab,
                    sealLightPillarPrefab,
                    ghoulPrefab,
                    eyePrefab,
                    penitentPrefab,
                    debrisPrefabs);
                Texture2D skyTex = EnsureSkyTexture();
                ApplyAtmosphere(skyTex);
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

            if (result == 0) CurseCardBuilder.WireArafScene();
            if (result == 0)
                Debug.Log("<color=green>[ArafSceneBuilder]</color> Araf labirenti, kit/oda prefabları ve RunDirector kuruldu.");
            return result;
        }

        private static void BuildDirectorPrefab()
        {
            // Var olan prefabı baştan üretmek, sonradan eklenen bileşenleri (ör. RunRecordRecorder)
            // ve Inspector ayarlarını siler. Bu yüzden yalnızca eksik bileşenleri ekle.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPath) != null)
            {
                EnsureDirectorComponents();
                return;
            }

            GameObject director = new GameObject("RunDirector");
            try
            {
                RunDirector runDirector = director.AddComponent<RunDirector>();
                director.AddComponent<ArafTransition>();
                RunRestartHandler restartHandler = director.AddComponent<RunRestartHandler>();
                SerializedObject restartSettings = new SerializedObject(restartHandler);
                restartSettings.FindProperty("director").objectReferenceValue = runDirector;
                restartSettings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(director, DirectorPath, out bool success);
                if (!success) throw new InvalidOperationException("RunDirector prefab kaydedilemedi.");
            }
            finally
            {
                Object.DestroyImmediate(director);
            }
        }

        private static void EnsureDirectorComponents()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DirectorPath);
            try
            {
                bool changed = false;
                if (!root.TryGetComponent(out RunDirector runDirector))
                {
                    runDirector = root.AddComponent<RunDirector>();
                    changed = true;
                }
                if (!root.TryGetComponent(out ArafTransition _))
                {
                    root.AddComponent<ArafTransition>();
                    changed = true;
                }
                if (!root.TryGetComponent(out RunRestartHandler restartHandler))
                {
                    restartHandler = root.AddComponent<RunRestartHandler>();
                    changed = true;
                }
                SerializedObject restartSettings = new SerializedObject(restartHandler);
                SerializedProperty directorRef = restartSettings.FindProperty("director");
                if (directorRef != null && directorRef.objectReferenceValue == null)
                {
                    directorRef.objectReferenceValue = runDirector;
                    restartSettings.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, DirectorPath, out bool success);
                if (!success) throw new InvalidOperationException("RunDirector prefab güncellenemedi.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material MaterialAsset(string name, Color color, bool emissive = false, Color? emissionColor = null)
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
                material.SetColor("_EmissionColor", emissionColor ?? new Color(3f, 0.05f, 0.1f));
                // GI bayrağı EmissiveIsBlack kalırsa URP doğrulaması _EMISSION anahtarını kaydederken düşürür.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material VeilMaterialAsset(string name, Color color, CullMode cullMode = CullMode.Off)
        {
            string path = MatDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)cullMode);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildKitAndAsset(Material bone, Material floor, Material veil, Material doorStone, Material rune)
        {
            GameObject wallPrefab = BuildWallPrefab(KitPrefabDir + "/Kit_Wall.prefab", bone);
            GameObject floorPrefab = BuildSlabPrefab(KitPrefabDir + "/Kit_Floor.prefab", "Kit_Floor", floor, -0.1f);
            GameObject ceilingPrefab = BuildSlabPrefab(KitPrefabDir + "/Kit_Ceiling.prefab", "Kit_Ceiling", floor, 0.1f);
            GameObject pillarPrefab = BuildPillarPrefab(KitPrefabDir + "/Kit_Pillar.prefab", bone, 5f);
            GameObject veilPrefab = BuildVeilPrefab(KitPrefabDir + "/Kit_Veil.prefab", veil);
            GameObject barrierPrefab = BuildArenaDoorPrefab(doorStone, rune);
            GameObject torchPrefab = BuildTorchPrefab(KitPrefabDir + "/Kit_Torch.prefab", bone);
            GameObject islandPrefab = BuildWallPrefab(KitPrefabDir + "/Kit_IslandPlatform.prefab", bone, "Kit_IslandPlatform");

            LabyrinthKit kit = LoadOrCreateAsset<LabyrinthKit>(KitAssetPath);
            SerializedObject so = new SerializedObject(kit);
            so.FindProperty("wallSegment").objectReferenceValue = wallPrefab;
            so.FindProperty("floorTile").objectReferenceValue = floorPrefab;
            so.FindProperty("ceilingTile").objectReferenceValue = ceilingPrefab;
            so.FindProperty("pillar").objectReferenceValue = pillarPrefab;
            so.FindProperty("veil").objectReferenceValue = veilPrefab;
            so.FindProperty("doorBarrier").objectReferenceValue = barrierPrefab;
            so.FindProperty("torch").objectReferenceValue = torchPrefab;
            so.FindProperty("islandPlatform").objectReferenceValue = islandPrefab;
            so.FindProperty("wallHeight").floatValue = 5f;
            so.FindProperty("wallThickness").floatValue = 0.6f;
            so.FindProperty("islandThickness").floatValue = 1.2f;
            so.FindProperty("torchEveryNCells").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kit);
        }

        private static void BuildDebrisPrefabs(Material bone)
        {
            BuildDebrisArchPrefab(DebrisArchPrefabPath, bone);
            BuildDebrisBlockPrefab(DebrisPillarPrefabPath, "Debris_Pillar", new Vector3(1.2f, 10f, 1.2f), bone);
            BuildDebrisBlockPrefab(DebrisMonolithPrefabPath, "Debris_Monolith", new Vector3(3f, 8f, 1f), bone);
        }

        private static void BuildDebrisArchPrefab(string path, Material mat)
        {
            GameObject root = new GameObject("Debris_Arch");
            try
            {
                DebrisPart(root.transform, "Post_L", new Vector3(-2.5f, -0.5f, 0f), new Vector3(1f, 6f, 1f), mat);
                DebrisPart(root.transform, "Post_R", new Vector3(2.5f, -0.5f, 0f), new Vector3(1f, 6f, 1f), mat);
                DebrisPart(root.transform, "Lintel", new Vector3(0f, 3f, 0f), new Vector3(6f, 1f, 1f), mat);
                SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildDebrisBlockPrefab(string path, string name, Vector3 scale, Material mat)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            try
            {
                root.transform.localScale = scale;
                Object.DestroyImmediate(root.GetComponent<Collider>());
                root.GetComponent<Renderer>().sharedMaterial = mat;
                SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void DebrisPart(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static GameObject BuildArenaDoorPrefab(Material stone, Material rune)
        {
            GameObject root = new GameObject("Kit_ArenaDoor");
            try
            {
                ArafArenaDoor door = root.AddComponent<ArafArenaDoor>();
                BoxCollider blocker = root.AddComponent<BoxCollider>();
                blocker.size = Vector3.one;
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = stone;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mark.name = side < 0 ? "Rune_Back" : "Rune_Front";
                    mark.transform.SetParent(visual.transform, false);
                    mark.transform.localPosition = new Vector3(0f, 0f, side * 0.51f);
                    mark.transform.localScale = new Vector3(0.12f, 0.4f, 0.025f);
                    Object.DestroyImmediate(mark.GetComponent<Collider>());
                    mark.GetComponent<Renderer>().sharedMaterial = rune;
                }
                SerializedObject so = new SerializedObject(door);
                so.FindProperty("visual").objectReferenceValue = visual.transform;
                so.FindProperty("blocker").objectReferenceValue = blocker;
                // Kök duvar yüksekliğine ölçeklenir: 5.2 dünya metresi / 5 m yükseklik.
                so.FindProperty("raisedHeight").floatValue = 5.2f / 5f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EnsureGroundLayer(root);
                return SavePrefab(root, KitPrefabDir + "/Kit_ArenaDoor.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject BuildWallPrefab(string path, Material mat, string name = "Kit_Wall")
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            try
            {
                root.GetComponent<Renderer>().sharedMaterial = mat;
                EnsureGroundLayer(root);
                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildSlabPrefab(string path, string name, Material mat, float childOffsetY)
        {
            GameObject root = new GameObject(name);
            try
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Slab";
                cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = new Vector3(0f, childOffsetY, 0f);
                cube.transform.localScale = new Vector3(1f, 0.2f, 1f);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                EnsureGroundLayer(root);
                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPillarPrefab(string path, Material mat, float wallHeight)
        {
            GameObject root = new GameObject("Kit_Pillar");
            try
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "PillarMesh";
                cube.transform.SetParent(root.transform, false);
                // Yönetici sütunu (kalınlık, duvar yüksekliği, kalınlık) ile ölçekleyip ortasına yerleştirir;
                // prefab birim ve merkezli olmalı, yoksa sütun tavanın 5 kat üstüne taşar.
                cube.transform.localPosition = Vector3.zero;
                cube.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                EnsureGroundLayer(root);
                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureGroundLayer(GameObject root)
        {
            int groundMask = GameLayers.GroundMask;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (((1 << t.gameObject.layer) & groundMask) == 0)
                {
                    t.gameObject.layer = 0;
                }
            }
        }

        private static GameObject BuildVeilPrefab(string path, Material veilMat)
        {
            GameObject root = new GameObject("Kit_Veil");
            try
            {
                GameObject front = GameObject.CreatePrimitive(PrimitiveType.Quad);
                front.name = "Veil_Front";
                front.transform.SetParent(root.transform, false);
                front.transform.localRotation = Quaternion.identity;
                Object.DestroyImmediate(front.GetComponent<Collider>());
                front.GetComponent<Renderer>().sharedMaterial = veilMat;

                GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
                back.name = "Veil_Back";
                back.transform.SetParent(root.transform, false);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Object.DestroyImmediate(back.GetComponent<Collider>());
                back.GetComponent<Renderer>().sharedMaterial = veilMat;

                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildTorchPrefab(string path, Material bracketMat)
        {
            GameObject root = new GameObject("Kit_Torch");
            try
            {
                GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bracket.name = "Bracket";
                bracket.transform.SetParent(root.transform, false);
                bracket.transform.localPosition = new Vector3(0f, -0.1f, 0.1f);
                bracket.transform.localScale = new Vector3(0.14f, 0.35f, 0.14f);
                Object.DestroyImmediate(bracket.GetComponent<Collider>());
                bracket.GetComponent<Renderer>().sharedMaterial = bracketMat;

                Color torchColor = new Color(0.62f, 0.48f, 1.0f);
                Light light = PointLight(root.transform, "TorchLight", new Vector3(0f, 0.15f, 0.25f), torchColor, 7f, 2.2f);
                GothicTorchFlicker flicker = light.gameObject.AddComponent<GothicTorchFlicker>();
                SerializedObject flickerSo = new SerializedObject(flicker);
                flickerSo.FindProperty("baseIntensity").floatValue = 2.2f;
                flickerSo.FindProperty("flameColor").colorValue = torchColor;
                flickerSo.FindProperty("emberColor").colorValue = new Color(0.32f, 0.18f, 0.62f);
                flickerSo.ApplyModifiedPropertiesWithoutUndo();

                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildRoomsAndTable(Material bone, Material portalMat, GameObject ghoulPrefab, GameObject eyePrefab)
        {
            string[] retired = { "Combat_Ghouls", "Combat_Mixed", "Ambush" };
            for (int i = 0; i < retired.Length; i++)
            {
                DeleteAssetIfPresent(RoomPrefabDir + "/Room_" + retired[i] + ".prefab");
                DeleteAssetIfPresent(RoomDefDir + "/ArafRoom_" + retired[i] + ".asset");
            }
            Material telegraph = MaterialAsset("Mat_Araf_Telegraph", new Color(1f, 0.04f, 0.02f), true,
                new Color(4f, 0.16f, 0.08f));
            GameObject roomShrine = BuildShrineRoom(bone, portalMat);
            GameObject roomOssuary = BuildOssuaryRoom(bone);
            GameObject roomArena = BuildArenaRoom(bone, telegraph);
            GameObject roomSeal = BuildSealRoom(bone, portalMat, telegraph);
            ArafRoomDefinition defShrine = SaveRoomDefinition(RoomDefDir + "/ArafRoom_Shrine.asset",
                "Shrine", ArafRoomKind.Shrine, roomShrine, 1f, 0, 1, false);
            ArafRoomDefinition defOssuary = SaveRoomDefinition(RoomDefDir + "/ArafRoom_Ossuary.asset",
                "Ossuary", ArafRoomKind.Ossuary, roomOssuary, 1.5f, 0, 99, false);
            ArafRoomDefinition defArena = SaveRoomDefinition(RoomDefDir + "/ArafRoom_Arena.asset",
                "Arena", ArafRoomKind.Arena, roomArena, 0f, 0, 99, false);
            ArafRoomDefinition defSeal = SaveRoomDefinition(RoomDefDir + "/ArafRoom_Seal.asset",
                "Seal", ArafRoomKind.Seal, roomSeal, 1f, 0, 3, true);
            ArafArenaWaveTable waves = LoadOrCreateAsset<ArafArenaWaveTable>(ArafDataDir + "/ArafArenaWaves_Default.asset");
            EnemyAggressionProfile[] profiles = new EnemyAggressionProfile[4];
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = AssetDatabase.LoadAssetAtPath<EnemyAggressionProfile>(ArafDataDir + "/AI/AggressionProfile_Araf_U" + i + ".asset");
            waves.Configure(ghoulPrefab, eyePrefab, AssetDatabase.LoadAssetAtPath<GameObject>(PenitentPath), profiles);
            EditorUtility.SetDirty(waves);
            ArafRoomTable table = LoadOrCreateAsset<ArafRoomTable>(RoomTableAssetPath);
            SerializedObject tableSo = new SerializedObject(table);
            SetReferences(tableSo.FindProperty("rooms"), new Object[] { defShrine, defOssuary });
            tableSo.FindProperty("sealRoom").objectReferenceValue = defSeal;
            tableSo.FindProperty("arenaRoom").objectReferenceValue = defArena;
            tableSo.FindProperty("arenaWaves").objectReferenceValue = waves;
            tableSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException("Eski oda varlığı silinemedi: " + path);
        }

        private static GameObject BuildArenaRoom(Material bone, Material telegraph)
        {
            GameObject root = CreateRoomRoot("Room_Arena", bone);
            try
            {
                PropCube(root.transform, "Cover_L", new Vector3(-2.6f, 0.45f, -1.8f), new Vector3(1.2f, 0.9f, 1.6f), bone);
                PropCube(root.transform, "Cover_R", new Vector3(2.6f, 0.45f, 1.8f), new Vector3(1.2f, 0.9f, 1.6f), bone);
                PropCube(root.transform, "Cover_C", new Vector3(0f, 0.4f, -2.8f), new Vector3(1.4f, 0.8f, 0.8f), bone);
                SetupArena(root, Child(root.transform, "AltarAnchor"), telegraph);
                ConfigureRoom(root.GetComponent<ArafRoom>(), Array.Empty<GameObject>(), null, null, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Arena.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ArafArena SetupArena(GameObject root, Transform altarAnchor, Material telegraph)
        {
            ArafArena arena = root.AddComponent<ArafArena>();
            BoxCollider trigger = TriggerBox(root.transform, "ArenaStartTrigger", new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 8f));
            trigger.gameObject.AddComponent<ArafArenaTriggerRelay>().Bind(arena);
            Vector3[] positions =
            {
                new Vector3(-4.5f, 0.05f, -4.5f), new Vector3(-4.5f, 0.05f, 4.5f),
                new Vector3(4.5f, 0.05f, -4.5f), new Vector3(4.5f, 0.05f, 4.5f),
                new Vector3(0f, 0.05f, -4.5f), new Vector3(0f, 0.05f, 4.5f),
                new Vector3(-4.5f, 0.05f, 0f), new Vector3(4.5f, 0.05f, 0f)
            };
            Transform[] points = new Transform[positions.Length];
            GameObject[] telegraphs = new GameObject[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                points[i] = Child(root.transform, "SpawnPoint_" + i);
                points[i].localPosition = positions[i];
                GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mark.name = "Telegraph";
                mark.transform.SetParent(points[i], false);
                mark.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.GetComponent<Renderer>().sharedMaterial = telegraph;
                mark.SetActive(false);
                telegraphs[i] = mark;
            }
            SerializedObject so = new SerializedObject(arena);
            so.FindProperty("startTrigger").objectReferenceValue = trigger;
            SetReferences(so.FindProperty("spawnPoints"), points);
            SetReferences(so.FindProperty("spawnTelegraphs"), telegraphs);
            so.FindProperty("altarAnchor").objectReferenceValue = altarAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arena;
        }

        private static GameObject BuildShrineRoom(Material bone, Material emissiveMat)
        {
            GameObject root = CreateRoomRoot("Room_Shrine", bone);
            try
            {
                PropCube(root.transform, "AltarBase", new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 1.8f), bone);
                PropCube(root.transform, "AltarCore", new Vector3(0f, 1.35f, 0f), new Vector3(0.85f, 1.1f, 0.85f), emissiveMat);
                BoxCollider trigger = TriggerBox(root.transform, "ShrineTrigger", new Vector3(0f, 2f, 0f), new Vector3(4f, 4f, 4f));

                ConfigureRoom(root.GetComponent<ArafRoom>(), Array.Empty<GameObject>(), null, trigger, 50f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Shrine.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildOssuaryRoom(Material bone)
        {
            GameObject root = CreateRoomRoot("Room_Ossuary", bone);
            try
            {
                PropCube(root.transform, "OssuaryPlinth_L", new Vector3(-1.8f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 1.1f), bone);
                PropCube(root.transform, "OssuaryPlinth_R", new Vector3(1.8f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 1.1f), bone);
                PropCube(root.transform, "OssuaryPlinth_C", new Vector3(0f, 0.35f, 1.8f), new Vector3(1.1f, 0.7f, 1.1f), bone);

                CreateRoomAmmoPickup(root.transform, "AmmoPickup_Shells_1", new Vector3(-1.8f, 0.85f, 0f), AmmoType.Shells, 8, bone);
                CreateRoomAmmoPickup(root.transform, "AmmoPickup_Bolts_1", new Vector3(1.8f, 0.85f, 0f), AmmoType.Bolts, 10, bone);
                CreateRoomAmmoPickup(root.transform, "AmmoPickup_Shells_2", new Vector3(0f, 0.85f, 1.8f), AmmoType.Shells, 6, bone);

                ConfigureRoom(root.GetComponent<ArafRoom>(), Array.Empty<GameObject>(), null, null, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Ossuary.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildSealRoom(Material bone, Material sealMat, Material telegraph)
        {
            GameObject root = CreateRoomRoot("Room_Seal", bone);
            try
            {
                PropCube(root.transform, "SealPillar_L", new Vector3(-2.6f, 2f, 1.5f), new Vector3(0.8f, 4f, 0.8f), bone);
                PropCube(root.transform, "SealPillar_R", new Vector3(2.6f, 2f, 1.5f), new Vector3(0.8f, 4f, 0.8f), bone);
                PropCube(root.transform, "SealPedestal", new Vector3(0f, 0.4f, 1.6f), new Vector3(1.4f, 0.8f, 1.4f), bone);

                GameObject sealVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sealVisual.name = "SealVisual";
                sealVisual.transform.SetParent(root.transform, false);
                sealVisual.transform.localPosition = new Vector3(0f, 1.35f, 1.6f);
                sealVisual.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);
                Object.DestroyImmediate(sealVisual.GetComponent<Collider>());
                sealVisual.GetComponent<Renderer>().sharedMaterial = sealMat;
                PointLight(sealVisual.transform, "Seal_PointLight", new Vector3(0f, 0.4f, 0f), new Color(0.9f, 0.08f, 0.08f), 8f, 2.5f);

                Transform altarAnchor = Child(root.transform, "AltarAnchor");
                altarAnchor.localPosition = Vector3.zero;

                ArafRoom arafRoom = root.GetComponent<ArafRoom>();
                ConfigureRoom(arafRoom, Array.Empty<GameObject>(), null, null, 0f);
                ArafArena arena = SetupArena(root, altarAnchor, telegraph);

                ArafSealRoom sealRoom = root.AddComponent<ArafSealRoom>();
                SerializedObject sealSo = new SerializedObject(sealRoom);
                sealSo.FindProperty("room").objectReferenceValue = arafRoom;
                sealSo.FindProperty("arena").objectReferenceValue = arena;
                sealSo.FindProperty("sealVisual").objectReferenceValue = sealVisual;
                sealSo.FindProperty("altarAnchor").objectReferenceValue = altarAnchor;
                sealSo.ApplyModifiedPropertiesWithoutUndo();

                return SavePrefab(root, RoomPrefabDir + "/Room_Seal.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ArafEscapePortal BuildHubPortalPrefab(Material portalMat)
        {
            GameObject root = new GameObject("Araf_HubPortal");
            try
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "PortalVisual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 2f, 0f);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(2.5f, 0.12f, 4f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = portalMat;

                BoxCollider box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(3f, 4f, 3f);
                box.center = new Vector3(0f, 2f, 0f);
                ArafEscapePortal escapePortal = root.AddComponent<ArafEscapePortal>();
                Light portalLight = PointLight(root.transform, "Portal_PointLight", new Vector3(0f, 2f, 0f), Color.red, 10f, 3f);

                SerializedObject portalSettings = new SerializedObject(escapePortal);
                portalSettings.FindProperty("startSealed").boolValue = true;
                SetReferences(portalSettings.FindProperty("visuals"), new Object[] { visual.GetComponent<Renderer>() });
                portalSettings.FindProperty("portalLight").objectReferenceValue = portalLight;
                portalSettings.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = SavePrefab(root, HubPortalPrefabPath);
                return saved.GetComponent<ArafEscapePortal>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ArafSealBeacon BuildSealBeaconPrefab()
        {
            GameObject root = new GameObject("Araf_SealBeacon");
            try
            {
                AudioSource source = root.AddComponent<AudioSource>();
                source.clip = EnsureSynthClip(SealBeaconClipPath, true);
                source.volume = 0.8f;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 4f;
                source.maxDistance = 60f;

                root.AddComponent<ArafSealBeacon>();
                GameObject saved = SavePrefab(root, SealBeaconPrefabPath);
                return saved.GetComponent<ArafSealBeacon>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ArafSealLightPillar BuildSealLightPillarPrefab(Material sealBeamMat)
        {
            GameObject root = new GameObject("Araf_SealLightPillar");
            try
            {
                ArafSealLightPillar pillar = root.AddComponent<ArafSealLightPillar>();

                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Beam";
                beam.transform.SetParent(root.transform, false);
                beam.transform.localPosition = new Vector3(0f, 30f, 0f);
                beam.transform.localScale = new Vector3(0.9f, 60f, 0.9f);
                Object.DestroyImmediate(beam.GetComponent<Collider>());
                Renderer beamRenderer = beam.GetComponent<Renderer>();
                beamRenderer.sharedMaterial = sealBeamMat;
                beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
                beamRenderer.receiveShadows = false;

                Light glow = PointLight(root.transform, "Glow", new Vector3(0f, 1.5f, 0f), new Color(1f, 0.08f, 0.05f), 14f, 3f);

                SerializedObject pillarSo = new SerializedObject(pillar);
                pillarSo.FindProperty("beam").objectReferenceValue = beam.transform;
                pillarSo.FindProperty("glow").objectReferenceValue = glow;
                pillarSo.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = SavePrefab(root, SealLightPillarPrefabPath);
                return saved.GetComponent<ArafSealLightPillar>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ArafTuning BuildTuningAsset()
        {
            ArafTuning tuning = LoadOrCreateAsset<ArafTuning>(TuningAssetPath);
            tuning.Configure(
                25f,
                3,
                3,
                new[]
                {
                    new CardRarityMultipliers(1f, 0f, 0f),
                    new CardRarityMultipliers(1f, 1f, 0f),
                    new CardRarityMultipliers(0.5f, 1.5f, 1f)
                });
            tuning.ConfigureArenas(1, new[]
            {
                new CardRarityMultipliers(1f, 0f, 0f), new CardRarityMultipliers(1f, 0.5f, 0f),
                new CardRarityMultipliers(1f, 1f, 0f), new CardRarityMultipliers(0.5f, 1.5f, 0.5f)
            });
            tuning.ConfigureThreat(
                new[] { 2, 2, 3, 3 },
                12,
                new[] { 0, 3, 4, 5 },
                14f);
            tuning.ConfigureIslandThreat(
                new[] { 0, 2, 2, 3 },
                new[] { 0, 0, 1, 1 },
                10,
                3f);
            tuning.ConfigureAtmosphere(
                new[] { 20f, 16f, 12f, 8f },
                new[] { 140f, 115f, 90f, 70f },
                new Color(0.10f, 0.05f, 0.16f),
                40,
                25f,
                60f);
            EditorUtility.SetDirty(tuning);
            return tuning;
        }

        private static void MoveAssetIfPresent(string oldPath, string newPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null) return;
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null) return;
            string error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[ArafSceneBuilder] Asset taşınamadı ({oldPath} -> {newPath}): {error}");
        }

        private static GameObject CreateRoomRoot(string name, Material bone)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<ArafRoom>();
            Color crimson = new Color(0.82f, 0.08f, 0.06f);
            PointLight(root.transform, "RoomLight_A", new Vector3(-2.5f, 3.2f, 2.5f), crimson, 10f, 1.8f);
            PointLight(root.transform, "RoomLight_B", new Vector3(2.5f, 3.2f, -2.5f), crimson, 10f, 1.8f);
            PropCube(root.transform, "FloorTrim_C", new Vector3(0f, 0.03f, 0f), new Vector3(3.2f, 0.06f, 3.2f), bone);
            return root;
        }

        private static void ConfigureRoom(ArafRoom room, GameObject[] enemies, ArafEscapePortal portal, Collider activationTrigger, float shrineHeal)
        {
            SerializedObject so = new SerializedObject(room);
            SetReferences(so.FindProperty("enemies"), enemies);
            so.FindProperty("portal").objectReferenceValue = portal;
            so.FindProperty("activationTrigger").objectReferenceValue = activationTrigger;
            so.FindProperty("shrineHealAmount").floatValue = shrineHeal;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ArafRoomDefinition SaveRoomDefinition(
            string path,
            string displayName,
            ArafRoomKind kind,
            GameObject prefab,
            float weight,
            int minDepth,
            int maxPerRun,
            bool sealDoors)
        {
            ArafRoomDefinition def = LoadOrCreateAsset<ArafRoomDefinition>(path);
            SerializedObject so = new SerializedObject(def);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("minDepth").intValue = minDepth;
            so.FindProperty("maxPerRun").intValue = maxPerRun;
            so.FindProperty("sealDoorsUntilCleared").boolValue = sealDoors;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void BuildLevel(
            LabyrinthKit kitAsset,
            ArafRoomTable tableAsset,
            ArafTuning tuningAsset,
            ArafEscapePortal hubPortalPrefab,
            ArafSealBeacon sealBeaconPrefab,
            ArafSealLightPillar sealLightPillarPrefab,
            GameObject ghoulPrefab,
            GameObject eyePrefab,
            GameObject penitentPrefab,
            GameObject[] debrisPrefabs)
        {
            GameObject root = new GameObject("ArafLevel");
            // Pozitif Y: PlayerMovement Y<0'da oyuncuyu zemine (Y=0.05) geri çeker.
            root.transform.position = new Vector3(0f, 1000f, 0f);
            ArafLevel level = root.AddComponent<ArafLevel>();
            Transform entry = Child(root.transform, "EntryPoint");
            entry.localPosition = new Vector3(0f, 0.1f, 0f);
            Transform content = Child(root.transform, "Content");
            content.gameObject.SetActive(false);

            SerializedObject levelSo = new SerializedObject(level);
            levelSo.FindProperty("entryPoint").objectReferenceValue = entry;
            levelSo.FindProperty("contentRoot").objectReferenceValue = content.gameObject;
            levelSo.FindProperty("tuning").objectReferenceValue = tuningAsset;
            levelSo.ApplyModifiedPropertiesWithoutUndo();

            RuntimeNavMeshBaker navMeshBaker = root.AddComponent<RuntimeNavMeshBaker>();
            SerializedObject bakerSo = new SerializedObject(navMeshBaker);
            bakerSo.FindProperty("bakeOnStart").boolValue = false;
            bakerSo.ApplyModifiedPropertiesWithoutUndo();

            ArafLabyrinthDirector director = root.AddComponent<ArafLabyrinthDirector>();
            SerializedObject dirSo = new SerializedObject(director);
            SerializedProperty worldProp = dirSo.FindProperty("world");
            if (worldProp != null)
            {
                ConfigureLabyrinthSettings(worldProp.FindPropertyRelative("hub"), 7, 7, 6f, 2, 0.15f, 1, 2);
                ConfigureLabyrinthSettings(worldProp.FindPropertyRelative("wing"), 7, 7, 6f, 3, 0.15f, 1, 2);
                worldProp.FindPropertyRelative("wingCount").intValue = 3;
                SerializedProperty offsetsProp = worldProp.FindPropertyRelative("wingHeightOffsets");
                if (offsetsProp != null)
                {
                    float[] offsets = { 2.4f, -3.2f, 4f };
                    offsetsProp.arraySize = offsets.Length;
                    for (int i = 0; i < offsets.Length; i++)
                        offsetsProp.GetArrayElementAtIndex(i).floatValue = offsets[i];
                }
                worldProp.FindPropertyRelative("platformsMin").intValue = 4;
                worldProp.FindPropertyRelative("platformsMax").intValue = 6;
                worldProp.FindPropertyRelative("platformSize").floatValue = 5f;
                worldProp.FindPropertyRelative("gapMin").floatValue = 2f;
                worldProp.FindPropertyRelative("gapMax").floatValue = 3.5f;
                worldProp.FindPropertyRelative("maxRisePerHop").floatValue = 0.8f;
                worldProp.FindPropertyRelative("lateralJitter").floatValue = 1.5f;
            }
            dirSo.FindProperty("kit").objectReferenceValue = kitAsset;
            dirSo.FindProperty("roomTable").objectReferenceValue = tableAsset;
            dirSo.FindProperty("level").objectReferenceValue = level;
            dirSo.FindProperty("navMeshBaker").objectReferenceValue = navMeshBaker;
            dirSo.FindProperty("hubPortalPrefab").objectReferenceValue = hubPortalPrefab;
            dirSo.FindProperty("sealBeaconPrefab").objectReferenceValue = sealBeaconPrefab;
            dirSo.FindProperty("sealLightPillarPrefab").objectReferenceValue = sealLightPillarPrefab;
            dirSo.FindProperty("eliteDamage").floatValue = 1.4f;
            dirSo.FindProperty("eliteSpeed").floatValue = 1.4f;
            dirSo.FindProperty("eliteHealth").floatValue = 1.25f;
            dirSo.FindProperty("logGeneration").boolValue = true;
            dirSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<ArafSafeGround>();
            root.AddComponent<ArafVoidFall>();

            ArafThreatDirector threatDirector = root.AddComponent<ArafThreatDirector>();
            SerializedObject threatSo = new SerializedObject(threatDirector);
            threatSo.FindProperty("labyrinth").objectReferenceValue = director;
            threatSo.FindProperty("patrolEnemyPrefab").objectReferenceValue = ghoulPrefab;
            threatSo.FindProperty("waveEnemyPrefab").objectReferenceValue = ghoulPrefab;
            threatSo.FindProperty("islandEyePrefab").objectReferenceValue = eyePrefab;
            threatSo.FindProperty("islandPenitentPrefab").objectReferenceValue = penitentPrefab;
            threatSo.ApplyModifiedPropertiesWithoutUndo();

            AudioSource stingSource = root.AddComponent<AudioSource>();
            stingSource.playOnAwake = false;
            stingSource.spatialBlend = 0f;
            stingSource.volume = 0.9f;

            ArafAtmosphereDirector atmosphereDirector = root.AddComponent<ArafAtmosphereDirector>();
            SerializedObject atmosSo = new SerializedObject(atmosphereDirector);
            atmosSo.FindProperty("labyrinth").objectReferenceValue = director;
            atmosSo.FindProperty("stingSource").objectReferenceValue = stingSource;
            atmosSo.FindProperty("awakeningSting").objectReferenceValue = EnsureSynthClip(AwakeningStingClipPath, false);
            atmosSo.ApplyModifiedPropertiesWithoutUndo();

            ArafFloatingDebris floatingDebris = root.AddComponent<ArafFloatingDebris>();
            SerializedObject debrisSo = new SerializedObject(floatingDebris);
            debrisSo.FindProperty("labyrinth").objectReferenceValue = director;
            SetReferences(debrisSo.FindProperty("debrisPrefabs"), Array.ConvertAll(debrisPrefabs, p => (Object)p));
            debrisSo.ApplyModifiedPropertiesWithoutUndo();

            // Content altında: Araf sahnesi kule oynanırken de additive yüklü durur ve yönlü ışık
            // konumdan bağımsızdır; kök altında olursa kuleyi de mora boyar.
            BuildVioletRim(content);
        }

        private static void BuildVioletRim(Transform contentRoot)
        {
            Vector3 sunDir = SunDirection();

            GameObject rimGo = new GameObject("VioletRim");
            rimGo.transform.SetParent(contentRoot, false);
            rimGo.transform.localPosition = Vector3.zero;
            rimGo.transform.localRotation = Quaternion.LookRotation(-sunDir, Vector3.up);
            Light rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.55f, 0.42f, 0.9f);
            rimLight.intensity = 0.35f;
            rimLight.shadows = LightShadows.None;
        }

        private static Vector3 SunDirection()
        {
            float az = SunAzimuthDeg * Mathf.Deg2Rad;
            float el = SunElevationDeg * Mathf.Deg2Rad;
            float cosEl = Mathf.Cos(el);
            return new Vector3(cosEl * Mathf.Sin(az), Mathf.Sin(el), cosEl * Mathf.Cos(az)).normalized;
        }

        private static Vector3 DirectionFromEquirectangular(float u, float v)
        {
            float el = (v - 0.5f) * Mathf.PI;
            float cosEl = Mathf.Cos(el);
            float lon = (0.5f - u) * (2f * Mathf.PI);
            return new Vector3(cosEl * Mathf.Cos(lon), Mathf.Sin(el), cosEl * Mathf.Sin(lon));
        }

        private static void ConfigureLabyrinthSettings(
            SerializedProperty prop,
            int width,
            int height,
            float cellSize,
            int roomSlotCount,
            float braidChance,
            int minRoomGap,
            int doorwaysPerRoomMax)
        {
            if (prop == null) return;
            prop.FindPropertyRelative("width").intValue = width;
            prop.FindPropertyRelative("height").intValue = height;
            prop.FindPropertyRelative("cellSize").floatValue = cellSize;
            prop.FindPropertyRelative("roomSlotCount").intValue = roomSlotCount;
            prop.FindPropertyRelative("braidChance").floatValue = braidChance;
            prop.FindPropertyRelative("minRoomGap").intValue = minRoomGap;
            prop.FindPropertyRelative("doorwaysPerRoomMax").intValue = doorwaysPerRoomMax;
        }

        private static GameObject RoomEnemy(Transform parent, GameObject prefab, Vector3 position, float yaw)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.transform.localPosition = position;
            enemy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            IEncounterEnemy encounterEnemy = enemy.GetComponent<IEncounterEnemy>();
            if (encounterEnemy is Object component)
            {
                SerializedObject enemySettings = new SerializedObject(component);
                SerializedProperty autoRespawn = enemySettings.FindProperty("autoRespawn");
                if (autoRespawn != null) autoRespawn.boolValue = false;
                enemySettings.ApplyModifiedPropertiesWithoutUndo();
            }
            enemy.SetActive(false);
            return enemy;
        }

        private static void CreateRoomAmmoPickup(Transform parent, string name, Vector3 position, AmmoType type, int amount, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = type == AmmoType.Shells
                ? new Vector3(0.28f, 0.22f, 0.38f)
                : new Vector3(0.20f, 0.18f, 0.45f);
            go.layer = GameLayers.Pickup;
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            AmmoPickup pickup = go.AddComponent<AmmoPickup>();
            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("ammoType").enumValueIndex = (int)type;
            so.FindProperty("ammoAmount").intValue = amount;
            so.FindProperty("lifetime").floatValue = 600f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PropCube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static BoxCollider TriggerBox(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            Transform child = Child(parent, name);
            child.localPosition = localPos;
            BoxCollider box = child.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null)
                throw new InvalidOperationException("Prefab kaydedilemedi: " + path);
            return saved;
        }

        private static void SetReferences(SerializedProperty property, Object[] references)
        {
            property.arraySize = references.Length;
            for (int i = 0; i < references.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Light PointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
        {
            Transform child = Child(parent, name);
            child.localPosition = position;
            Light light = child.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            return light;
        }

        // GLSL smoothstep eşdeğeri: Unity'nin Mathf.SmoothStep'i bir eşik değil ara değer (from, to, t) hesaplar.
        private static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static AudioClip EnsureSynthClip(string path, bool beacon)
        {
            if (File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureClipImporter(path);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            EnsureFolder(ArafAudioDir);
            float seconds = beacon ? ArafToneSynth.BeaconSeconds : ArafToneSynth.StingSeconds;
            float[] samples = new float[ArafToneSynth.SampleCount(seconds)];
            if (beacon) ArafToneSynth.FillBeacon(samples);
            else ArafToneSynth.FillSting(samples);
            File.WriteAllBytes(path, ArafToneSynth.EncodeWav16(samples, ArafToneSynth.SampleRate));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureClipImporter(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void ConfigureClipImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool dirty = !importer.forceToMono
                || importer.loadInBackground
                || settings.loadType != AudioClipLoadType.DecompressOnLoad
                || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || settings.quality != 0.7f
                || !settings.preloadAudioData;
            if (!dirty) return;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static Texture2D EnsureSkyTexture()
        {
            if (!RegenerateSky && File.Exists(SkyTexPath))
            {
                // Var olan doku da aynı içe aktarma ayarlarından geçer.
                AssetDatabase.ImportAsset(SkyTexPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSkyImporter();
                Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexPath);
                if (existing != null) return existing;
            }

            const int width = 2048;
            const int height = 1024;
            Color[] pixels = new Color[width * height];

            Color horizonCol = new Color(0.20f, 0.10f, 0.32f);
            Color zenithCol = new Color(0.03f, 0.01f, 0.06f);
            Color nadirCol = new Color(0.01f, 0.00f, 0.02f);
            Color nebulaViolet = new Color(0.36f, 0.14f, 0.58f);
            Color nebulaMagenta = new Color(0.55f, 0.14f, 0.48f);

            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                float el = (v - 0.5f) * Mathf.PI;
                float cosEl = Mathf.Cos(el);
                float dirY = Mathf.Sin(el);

                Color baseSky = dirY >= 0f
                    ? Color.Lerp(horizonCol, zenithCol, Mathf.Pow(dirY, 0.45f))
                    : Color.Lerp(horizonCol, nadirCol, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-dirY / 0.15f)));

                float horizonBand = Mathf.Exp(-Mathf.Pow((dirY - 0.10f) / 0.28f, 2f))
                                    * Smoothstep(-0.08f, 0.05f, dirY);

                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float lon = (0.5f - u) * (2f * Mathf.PI);
                    float dirX = cosEl * Mathf.Cos(lon);
                    float dirZ = cosEl * Mathf.Sin(lon);

                    float n1 = Mathf.PerlinNoise(dirX * 2.2f + dirZ * 0.8f + 11.3f, dirY * 2.2f + dirZ * 1.3f + 5.7f);
                    float n2 = Mathf.PerlinNoise(dirX * 4.4f + dirZ * 1.6f + 23.1f, dirY * 4.4f + dirZ * 2.7f + 17.9f);
                    float n3 = Mathf.PerlinNoise(dirX * 8.8f + dirZ * 3.2f + 41.7f, dirY * 8.8f + dirZ * 5.3f + 31.4f);
                    float noise = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;

                    float tintMix = Mathf.PerlinNoise(dirX * 1.7f + 71.2f, dirZ * 1.7f + dirY * 1.2f + 19.4f);
                    Color nebulaCol = Color.Lerp(nebulaViolet, nebulaMagenta, tintMix);
                    float cloudAlpha = Smoothstep(0.40f, 0.78f, noise) * horizonBand * 0.55f;

                    pixels[py * width + px] = Color.Lerp(baseSky, nebulaCol, cloudAlpha);
                }
            }

            System.Random rng = new System.Random(198307);
            const int starCount = 450;
            for (int s = 0; s < starCount; s++)
            {
                float y = Mathf.Lerp(0.03f, 0.995f, (float)rng.NextDouble());
                float az = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float cosY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 starDir = new Vector3(cosY * Mathf.Sin(az), y, cosY * Mathf.Cos(az));

                float starLon = Mathf.Atan2(starDir.z, starDir.x);
                float su = Mathf.Repeat(0.5f - starLon / (2f * Mathf.PI), 1f);
                float sv = 1f - Mathf.Acos(Mathf.Clamp(starDir.y, -1f, 1f)) / Mathf.PI;

                int cx = Mathf.FloorToInt(su * width);
                int cy = Mathf.Clamp(Mathf.FloorToInt(sv * height), 0, height - 1);
                float starRadiusDeg = Mathf.Lerp(0.08f, 0.16f, (float)rng.NextDouble());
                float starBrightness = Mathf.Lerp(0.25f, 0.90f, (float)rng.NextDouble()) * Smoothstep(0.02f, 0.16f, y);
                Color starTint = Color.Lerp(new Color(0.85f, 0.80f, 1.0f), new Color(1.0f, 0.88f, 0.96f), (float)rng.NextDouble());

                for (int dy = -2; dy <= 2; dy++)
                {
                    int py = cy + dy;
                    if (py < 0 || py >= height) continue;
                    float v = (py + 0.5f) / height;
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int px = (cx + dx + width) % width;
                        float u = (px + 0.5f) / width;
                        Vector3 pDir = DirectionFromEquirectangular(u, v);
                        float dot = Mathf.Clamp(Vector3.Dot(pDir, starDir), -1f, 1f);
                        float angDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;
                        if (angDeg >= starRadiusDeg) continue;

                        float falloff = 1f - Smoothstep(0f, starRadiusDeg, angDeg);
                        pixels[py * width + px] += starTint * (starBrightness * falloff);
                    }
                }
            }

            Vector3 sunDir = SunDirection();
            float sunLon = Mathf.Atan2(sunDir.z, sunDir.x);
            float sunU = Mathf.Repeat(0.5f - sunLon / (2f * Mathf.PI), 1f);
            float sunV = 1f - Mathf.Acos(Mathf.Clamp(sunDir.y, -1f, 1f)) / Mathf.PI;
            int sunCx = Mathf.FloorToInt(sunU * width);
            int sunCy = Mathf.Clamp(Mathf.FloorToInt(sunV * height), 0, height - 1);

            const float sunRadiusDeg = 6f;
            const float coronaWidthDeg = 3f;
            Color coronaCol = new Color(1.0f, 0.45f, 1.0f);

            for (int dy = -80; dy <= 80; dy++)
            {
                int py = sunCy + dy;
                if (py < 0 || py >= height) continue;
                float v = (py + 0.5f) / height;
                for (int dx = -100; dx <= 100; dx++)
                {
                    int px = (sunCx + dx + width) % width;
                    float u = (px + 0.5f) / width;
                    Vector3 pDir = DirectionFromEquirectangular(u, v);
                    float dot = Mathf.Clamp(Vector3.Dot(pDir, sunDir), -1f, 1f);
                    float angDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;
                    if (angDeg > sunRadiusDeg + coronaWidthDeg) continue;

                    int idx = py * width + px;
                    float coronaT = Mathf.Clamp01(Mathf.Max(0f, angDeg - sunRadiusDeg) / coronaWidthDeg);
                    float corona = Mathf.Pow(1f - coronaT, 2.8f);
                    Color col = Color.Lerp(pixels[idx], coronaCol, corona);

                    float discMask = 1f - Smoothstep(sunRadiusDeg - 0.2f, sunRadiusDeg, angDeg);
                    pixels[idx] = Color.Lerp(col, Color.black, discMask);
                }
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                tex.SetPixels(pixels);
                tex.Apply(false, false);
                File.WriteAllBytes(SkyTexPath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(SkyTexPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureSkyImporter();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexPath);
        }

        private static void ConfigureSkyImporter()
        {
            if (!(AssetImporter.GetAtPath(SkyTexPath) is TextureImporter importer)) return;
            bool dirty = importer.textureType != TextureImporterType.Default
                || importer.textureShape != TextureImporterShape.Texture2D
                || !importer.sRGBTexture
                || importer.alphaSource != TextureImporterAlphaSource.None
                || importer.wrapModeU != TextureWrapMode.Repeat
                || importer.wrapModeV != TextureWrapMode.Clamp
                || importer.maxTextureSize != 2048;
            if (!dirty) return;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ApplyAtmosphere(Texture2D skyTex)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.10f, 0.05f, 0.16f);
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.06f, 0.14f);

            Shader skyShader = Shader.Find("Skybox/Panoramic");
            if (skyShader != null)
            {
                Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
                if (skyMat == null)
                {
                    skyMat = new Material(skyShader) { name = "Mat_Araf_Sky" };
                    AssetDatabase.CreateAsset(skyMat, SkyMatPath);
                }
                skyMat.shader = skyShader;
                skyMat.SetTexture("_MainTex", skyTex);
                skyMat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                skyMat.SetFloat("_Exposure", 1.0f);
                skyMat.SetFloat("_Rotation", 0f);
                skyMat.SetFloat("_Mapping", 1f);
                skyMat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
                skyMat.SetFloat("_ImageType", 0f);
                skyMat.SetFloat("_MirrorOnBack", 0f);
                skyMat.SetFloat("_Layout", 0f);
                EditorUtility.SetDirty(skyMat);
                AssetDatabase.SaveAssetIfDirty(skyMat);
                RenderSettings.skybox = skyMat;
            }
            else
            {
                Debug.LogWarning("[ArafSceneBuilder] Skybox/Panoramic shader bulunamadı.");
            }

            DynamicGI.UpdateEnvironment();
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
