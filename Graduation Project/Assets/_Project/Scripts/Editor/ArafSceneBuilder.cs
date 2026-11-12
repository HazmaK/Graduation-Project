using System;
using System.Collections.Generic;
using RetroFPS.Combat;
using RetroFPS.Run;
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
        private const string ArenaGateMatPath = "Assets/_Project/Data/Materials/Mat_ArenaGate.mat";
        private const string KitPrefabDir = "Assets/_Project/Prefabs/Araf/Kit";
        private const string RoomPrefabDir = "Assets/_Project/Prefabs/Araf/Rooms";
        private const string ArafDataDir = "Assets/_Project/Data/Araf";
        private const string RoomDefDir = "Assets/_Project/Data/Araf/Rooms";
        private const string KitAssetPath = ArafDataDir + "/LabyrinthKit_Araf.asset";
        private const string RoomTableAssetPath = ArafDataDir + "/ArafRoomTable_Default.asset";
        private const string GhoulPath = "Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab";
        private const string EyePath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";

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
                // Prefab üretimi için geçici boş sahne.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EnsureFolder("Assets/_Project/Prefabs/Systems");
                BuildDirectorPrefab();

                EnsureFolder(MatDir);
                Material bone = MaterialAsset("Mat_Araf_Bone", new Color(0.55f, 0.5f, 0.42f));
                Material floor = MaterialAsset("Mat_Araf_Floor", new Color(0.12f, 0.04f, 0.04f));
                Material portal = MaterialAsset("Mat_Araf_Portal", new Color(0.6f, 0f, 0.02f), true);
                Material veil = VeilMaterialAsset("Mat_Araf_Veil", new Color(0.01f, 0f, 0f, 0.92f));
                Material gate = AssetDatabase.LoadAssetAtPath<Material>(ArenaGateMatPath);
                if (gate == null) gate = portal;

                EnsureFolder(KitPrefabDir);
                EnsureFolder(RoomPrefabDir);
                EnsureFolder(ArafDataDir);
                EnsureFolder(RoomDefDir);

                BuildKitAndAsset(bone, floor, veil, gate);

                GameObject ghoulPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
                GameObject eyePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EyePath);
                BuildRoomsAndTable(bone, portal, ghoulPrefab, eyePrefab);

                // Yeni sahneye geçmeden önce tüm prefab ve ScriptableObject varlıklarını diske yaz.
                AssetDatabase.SaveAssets();

                Scene araf = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                LabyrinthKit kitAsset = AssetDatabase.LoadAssetAtPath<LabyrinthKit>(KitAssetPath);
                ArafRoomTable tableAsset = AssetDatabase.LoadAssetAtPath<ArafRoomTable>(RoomTableAssetPath);
                BuildLevel(kitAsset, tableAsset);
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
                Debug.Log("<color=green>[ArafSceneBuilder]</color> Araf labirenti, kit/oda prefabları ve RunDirector kuruldu.");
            return result;
        }

        private static void BuildDirectorPrefab()
        {
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

        private static Material VeilMaterialAsset(string name, Color color)
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
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildKitAndAsset(Material bone, Material floor, Material veil, Material gate)
        {
            GameObject wallPrefab = BuildWallPrefab(KitPrefabDir + "/Kit_Wall.prefab", bone);
            GameObject floorPrefab = BuildSlabPrefab(KitPrefabDir + "/Kit_Floor.prefab", "Kit_Floor", floor, -0.1f);
            GameObject ceilingPrefab = BuildSlabPrefab(KitPrefabDir + "/Kit_Ceiling.prefab", "Kit_Ceiling", floor, 0.1f);
            GameObject pillarPrefab = BuildPillarPrefab(KitPrefabDir + "/Kit_Pillar.prefab", bone, 5f);
            GameObject veilPrefab = BuildVeilPrefab(KitPrefabDir + "/Kit_Veil.prefab", veil);
            GameObject barrierPrefab = BuildWallPrefab(KitPrefabDir + "/Kit_DoorBarrier.prefab", gate, "Kit_DoorBarrier");
            GameObject torchPrefab = BuildTorchPrefab(KitPrefabDir + "/Kit_Torch.prefab", bone);

            LabyrinthKit kit = LoadOrCreateAsset<LabyrinthKit>(KitAssetPath);
            SerializedObject so = new SerializedObject(kit);
            so.FindProperty("wallSegment").objectReferenceValue = wallPrefab;
            so.FindProperty("floorTile").objectReferenceValue = floorPrefab;
            so.FindProperty("ceilingTile").objectReferenceValue = ceilingPrefab;
            so.FindProperty("pillar").objectReferenceValue = pillarPrefab;
            so.FindProperty("veil").objectReferenceValue = veilPrefab;
            so.FindProperty("doorBarrier").objectReferenceValue = barrierPrefab;
            so.FindProperty("torch").objectReferenceValue = torchPrefab;
            so.FindProperty("wallHeight").floatValue = 5f;
            so.FindProperty("wallThickness").floatValue = 0.6f;
            so.FindProperty("torchEveryNCells").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kit);
        }

        private static GameObject BuildWallPrefab(string path, Material mat, string name = "Kit_Wall")
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            try
            {
                root.GetComponent<Renderer>().sharedMaterial = mat;
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
                cube.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);
                cube.transform.localScale = new Vector3(0.9f, wallHeight, 0.9f);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
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

                Color torchColor = new Color(1.0f, 0.36f, 0.12f);
                Light light = PointLight(root.transform, "TorchLight", new Vector3(0f, 0.15f, 0.25f), torchColor, 7f, 2.2f);
                GothicTorchFlicker flicker = light.gameObject.AddComponent<GothicTorchFlicker>();
                SerializedObject flickerSo = new SerializedObject(flicker);
                flickerSo.FindProperty("baseIntensity").floatValue = 2.2f;
                flickerSo.FindProperty("flameColor").colorValue = torchColor;
                flickerSo.FindProperty("emberColor").colorValue = new Color(0.85f, 0.15f, 0.06f);
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
            GameObject roomCombatGhouls = BuildCombatGhoulsRoom(bone, ghoulPrefab);
            GameObject roomCombatMixed = BuildCombatMixedRoom(bone, ghoulPrefab, eyePrefab);
            GameObject roomAmbush = BuildAmbushRoom(bone, ghoulPrefab);
            GameObject roomShrine = BuildShrineRoom(bone, portalMat);
            GameObject roomOssuary = BuildOssuaryRoom(bone);
            GameObject roomExit = BuildExitRoom(bone, portalMat, ghoulPrefab);

            ArafRoomDefinition defCombatGhouls = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Combat_Ghouls.asset",
                "Combat_Ghouls", ArafRoomKind.Combat, roomCombatGhouls, 3f, 0, 99, true);
            ArafRoomDefinition defCombatMixed = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Combat_Mixed.asset",
                "Combat_Mixed", ArafRoomKind.Combat, roomCombatMixed, 2f, 0, 99, true);
            ArafRoomDefinition defAmbush = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Ambush.asset",
                "Ambush", ArafRoomKind.Ambush, roomAmbush, 1.5f, 2, 99, true);
            ArafRoomDefinition defShrine = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Shrine.asset",
                "Shrine", ArafRoomKind.Shrine, roomShrine, 1f, 0, 1, false);
            ArafRoomDefinition defOssuary = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Ossuary.asset",
                "Ossuary", ArafRoomKind.Ossuary, roomOssuary, 1.5f, 0, 99, false);
            ArafRoomDefinition defExit = SaveRoomDefinition(
                RoomDefDir + "/ArafRoom_Exit.asset",
                "Exit", ArafRoomKind.Exit, roomExit, 1f, 3, 1, true);

            ArafRoomTable table = LoadOrCreateAsset<ArafRoomTable>(RoomTableAssetPath);
            SerializedObject tableSo = new SerializedObject(table);
            SetReferences(tableSo.FindProperty("rooms"), new Object[]
            {
                defCombatGhouls, defCombatMixed, defAmbush, defShrine, defOssuary
            });
            tableSo.FindProperty("exitRoom").objectReferenceValue = defExit;
            tableSo.FindProperty("exitBaseChance").floatValue = 0.10f;
            tableSo.FindProperty("exitChancePerReveal").floatValue = 0.15f;
            tableSo.FindProperty("exitMinDepth").intValue = 3;
            tableSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
        }

        private static GameObject BuildCombatGhoulsRoom(Material bone, GameObject ghoulPrefab)
        {
            GameObject root = CreateRoomRoot("Room_Combat_Ghouls", bone);
            try
            {
                PropCube(root.transform, "BonePillar_NW", new Vector3(-2.6f, 1.5f, 2.6f), new Vector3(0.8f, 3f, 0.8f), bone);
                PropCube(root.transform, "BonePillar_SE", new Vector3(2.6f, 1.5f, -2.6f), new Vector3(0.8f, 3f, 0.8f), bone);

                GameObject[] enemies =
                {
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(-2.2f, 0.05f, -1.8f), 45f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(2.2f, 0.05f, 1.8f), 225f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(-1.8f, 0.05f, 2.0f), 135f)
                };

                ConfigureRoom(root.GetComponent<ArafRoom>(), enemies, null, null, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Combat_Ghouls.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildCombatMixedRoom(Material bone, GameObject ghoulPrefab, GameObject eyePrefab)
        {
            GameObject root = CreateRoomRoot("Room_Combat_Mixed", bone);
            try
            {
                PropCube(root.transform, "BoneObelisk_W", new Vector3(-2.5f, 1.8f, 0f), new Vector3(0.7f, 3.6f, 0.7f), bone);
                PropCube(root.transform, "BoneObelisk_E", new Vector3(2.5f, 1.8f, 0f), new Vector3(0.7f, 3.6f, 0.7f), bone);

                GameObject[] enemies =
                {
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(-2.0f, 0.05f, -2.0f), 30f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(2.0f, 0.05f, -2.0f), 330f),
                    RoomEnemy(root.transform, eyePrefab, new Vector3(0f, 2.5f, 2.0f), 180f)
                };

                ConfigureRoom(root.GetComponent<ArafRoom>(), enemies, null, null, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Combat_Mixed.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildAmbushRoom(Material bone, GameObject ghoulPrefab)
        {
            GameObject root = CreateRoomRoot("Room_Ambush", bone);
            try
            {
                PropCube(root.transform, "BoneCover_1", new Vector3(-2.8f, 1.0f, -2.8f), new Vector3(1.1f, 2.0f, 0.5f), bone);
                PropCube(root.transform, "BoneCover_2", new Vector3(2.8f, 1.0f, 2.8f), new Vector3(1.1f, 2.0f, 0.5f), bone);

                BoxCollider trigger = TriggerBox(root.transform, "AmbushTrigger", new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 6f));
                GameObject[] enemies =
                {
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(-2.4f, 0.05f, 2.2f), 135f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(2.4f, 0.05f, 2.2f), 225f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(0f, 0.05f, -2.4f), 0f)
                };

                ConfigureRoom(root.GetComponent<ArafRoom>(), enemies, null, trigger, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Ambush.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        private static GameObject BuildExitRoom(Material bone, Material portalMat, GameObject ghoulPrefab)
        {
            GameObject root = CreateRoomRoot("Room_Exit", bone);
            try
            {
                PropCube(root.transform, "ExitPillar_L", new Vector3(-2.6f, 2f, 1.5f), new Vector3(0.8f, 4f, 0.8f), bone);
                PropCube(root.transform, "ExitPillar_R", new Vector3(2.6f, 2f, 1.5f), new Vector3(0.8f, 4f, 0.8f), bone);

                Transform bloodPortal = Child(root.transform, "PortalRoot");
                bloodPortal.localPosition = new Vector3(0f, 0f, 1.5f);
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "PortalVisual";
                visual.transform.SetParent(bloodPortal, false);
                visual.transform.localPosition = new Vector3(0f, 2f, 0f);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(2.5f, 0.12f, 4f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = portalMat;

                GameObject trigger = Child(bloodPortal, "EscapeTrigger").gameObject;
                BoxCollider box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(3f, 4f, 3f);
                box.center = new Vector3(0f, 2f, 0f);
                ArafEscapePortal escapePortal = trigger.AddComponent<ArafEscapePortal>();
                Light portalLight = PointLight(bloodPortal, "Portal_PointLight", new Vector3(0f, 2f, 0f), Color.red, 10f, 3f);

                SerializedObject portalSettings = new SerializedObject(escapePortal);
                portalSettings.FindProperty("startSealed").boolValue = true;
                SetReferences(portalSettings.FindProperty("visuals"), new Object[] { visual.GetComponent<Renderer>() });
                portalSettings.FindProperty("portalLight").objectReferenceValue = portalLight;
                portalSettings.ApplyModifiedPropertiesWithoutUndo();

                GameObject[] enemies =
                {
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(-2.2f, 0.05f, -1.8f), 30f),
                    RoomEnemy(root.transform, ghoulPrefab, new Vector3(2.2f, 0.05f, -1.8f), 330f)
                };

                ConfigureRoom(root.GetComponent<ArafRoom>(), enemies, escapePortal, null, 0f);
                return SavePrefab(root, RoomPrefabDir + "/Room_Exit.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        private static void BuildLevel(LabyrinthKit kitAsset, ArafRoomTable tableAsset)
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
            levelSo.ApplyModifiedPropertiesWithoutUndo();

            ArafLabyrinthDirector director = root.AddComponent<ArafLabyrinthDirector>();
            SerializedObject dirSo = new SerializedObject(director);
            SerializedProperty settingsProp = dirSo.FindProperty("settings");
            settingsProp.FindPropertyRelative("width").intValue = 9;
            settingsProp.FindPropertyRelative("height").intValue = 9;
            settingsProp.FindPropertyRelative("cellSize").floatValue = 6f;
            settingsProp.FindPropertyRelative("roomSlotCount").intValue = 7;
            settingsProp.FindPropertyRelative("braidChance").floatValue = 0.15f;
            settingsProp.FindPropertyRelative("minRoomGap").intValue = 1;
            settingsProp.FindPropertyRelative("doorwaysPerRoomMax").intValue = 2;
            dirSo.FindProperty("kit").objectReferenceValue = kitAsset;
            dirSo.FindProperty("roomTable").objectReferenceValue = tableAsset;
            dirSo.FindProperty("level").objectReferenceValue = level;
            dirSo.FindProperty("eliteDamage").floatValue = 1.4f;
            dirSo.FindProperty("eliteSpeed").floatValue = 1.4f;
            dirSo.FindProperty("eliteHealth").floatValue = 1.25f;
            dirSo.FindProperty("logGeneration").boolValue = true;
            dirSo.ApplyModifiedPropertiesWithoutUndo();
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
