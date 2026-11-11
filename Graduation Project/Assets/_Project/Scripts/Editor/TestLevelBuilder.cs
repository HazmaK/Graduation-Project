using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using RetroFPS.Combat;

namespace RetroFPS.Editor
{
    /// <summary>
    /// "Gauntlet" test seviyesini prosedürel olarak kurar: Başlangıç salonu → koridorlar → 3 savaş odası → boss odası.
    /// Tüm geometri eksen-hizalı kutulardır (dünya-ölçekli UV, Point filtreli retro dokular); odalar
    /// Encounter_* nesneleri olarak (BoxCollider + EncounterArea + WaveSpawnDirector + SpawnPoints + Gate'ler) izole kurulur.
    /// Menü: RetroFPS ▸ Build Test Level (Gauntlet). Çıktı: Assets/Scenes/TestLevel_Gauntlet.unity
    /// </summary>
    public static class TestLevelBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestLevel_Gauntlet.unity";
        private const string MatDir = "Assets/_Project/Data/Materials/Level";
        private const string TexDir = "Assets/_Project/Data/Textures/Level";
        private const string WaveDir = "Assets/_Project/Data/Waves";
        private const string GhoulPath = "Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab";
        private const string EyePath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string SystemsPath = "Assets/_Project/Prefabs/Systems/CombatSystems.prefab";
        private const string GatePath = "Assets/_Project/Data/Materials/Mat_ArenaGate.mat";

        private const float WallT = 0.6f;     // duvar kalınlığı
        private const float DoorW = 5f;       // kapı/koridor genişliği
        private const float DoorH = 4.5f;     // kapı/koridor yüksekliği

        // Materyaller
        private static Material _floor, _wall, _ceil, _plat, _trim, _bossFloor, _gate, _iron, _flame;
        private static GameObject _ghoul, _eye;
        private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        [MenuItem("RetroFPS/Build Test Level (Gauntlet)", false, 20)]
        private static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildGauntlet();
        }

        /// <summary>Seviyeyi kurar ve kaydeder. 0 = başarılı, negatif = hata.</summary>
        public static int BuildGauntlet()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[TestLevelBuilder] Play Mode'da çalışmaz."); return -1; }
            Scene cur = SceneManager.GetActiveScene();
            if (cur.isDirty && cur.path != ScenePath) { Debug.LogError("[TestLevelBuilder] Aktif sahnede kaydedilmemiş değişiklik var; önce kaydedin."); return -2; }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Asset referansları NewScene sonrasında yüklenmeli (NewScene kullanılmayan asset'leri boşaltır)
            _ghoul = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
            _eye = AssetDatabase.LoadAssetAtPath<GameObject>(EyePath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject systemsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath);
            if (_ghoul == null || _eye == null || playerPrefab == null) { Debug.LogError("[TestLevelBuilder] Gerekli prefablar bulunamadı."); return -3; }

            MeshCache.Clear();
            EnsureFolders();
            LoadMaterials();

            WaveSet set1 = MakeWaveSet("WaveSet_Gauntlet_R1_Crypt",
                "Oda 1 (3 faz, yalnız Ghoul). Hareket ve silah ısınması: tempo yüksek, tehdit düşük.", WavesR1());
            WaveSet set2 = MakeWaveSet("WaveSet_Gauntlet_R2_BoneHall",
                "Oda 2 (4 faz). Uçan Göz tanıtımı + yan platformlardan flank; dikey hareket gerektirir.", WavesR2());
            WaveSet set3 = MakeWaveSet("WaveSet_Gauntlet_R3_Ossuary",
                "Oda 3 (5 faz). Boss öncesi sınav: köprü/balkon, kalabalık ve hava baskısı birlikte.", WavesR3());

            ApplyAtmosphere();

            GameObject root = new GameObject("TestLevel_Gauntlet");
            Transform geo = Child(root.transform, "Geometry");
            Transform enc = Child(root.transform, "Encounters");

            BuildStartHall(geo);
            BuildCorridorC1(geo);
            BuildRoomPit(geo, enc, set1);
            BuildCorridorC2(geo);
            BuildRoomBoneHall(geo, enc, set2);
            BuildCorridorC3(geo);
            BuildRoomOssuary(geo, enc, set3);
            BuildCorridorC4(geo);
            BuildBossRoom(geo, root.transform);

            // Oyuncu + savaş sistemleri
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.08f, -4f), Quaternion.identity);
            if (systemsPrefab != null) PrefabUtility.InstantiatePrefab(systemsPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[TestLevelBuilder]</color> Gauntlet kuruldu: " + ScenePath);
            return 0;
        }

        // ───────────────────────────── ALANLAR ─────────────────────────────

        private static void BuildStartHall(Transform geo)
        {
            Transform g = Child(geo, "A_StartHall");
            RoomShell(g, "Shell", -7, 7, -7, 7, 6f, null, (float?)0f, _wall, _floor, _ceil);
            TorchRow(g, true, -7f, -5f, 5f, 5f, 2.8f, 0f, null, false);
            TorchRow(g, true, 7f, -5f, 5f, 5f, 2.8f, 180f, new[] { 0f }, false);
            TorchRow(g, false, -7f, -4f, 4f, 8f, 2.8f, 90f, null, false);
            TorchRow(g, false, 7f, -4f, 4f, 8f, 2.8f, -90f, null, false);
            Pillar(g, "Pillar_L", -4.5f, 4.5f, 6f, 1.2f);
            Pillar(g, "Pillar_R", 4.5f, 4.5f, 6f, 1.2f);
            Dummy(g, new Vector3(-3f, 0f, 1.5f));
            Dummy(g, new Vector3(3f, 0f, 1.5f));
        }

        private static void BuildCorridorC1(Transform geo)
        {
            Transform g = Child(geo, "B_Corridor1_SprintLane");
            StraightCorridor(g, 0f, 7f, 25f, DoorH);
            for (int i = 0; i < 3; i++)
            {
                float z = 12f + i * 4.5f;
                Solid(g, "Hurdle_" + i, new Vector3(0f, 0.3f, z), new Vector3(DoorW, 0.6f, 0.6f), _trim);
            }
            CorridorTorches(g, 0f, 9f, 24f, 2.6f, 9f);
        }

        private static void BuildRoomPit(Transform geo, Transform enc, WaveSet set)
        {
            Transform g = Child(geo, "C_Room1_CryptPit");
            RoomShell(g, "Shell", -14, 14, 25, 53, 7f, 0f, 0f, _wall, _floor, _ceil);
            RoomTorches(g, -14, 14, 25, 53, 3.4f, 9f, 0f, 0f, false);

            Pillar(g, "Pillar_NW", -6f, 44f, 7f, 1.8f);
            Pillar(g, "Pillar_NE", 6f, 44f, 7f, 1.8f);
            Pillar(g, "Pillar_SW", -6f, 34f, 7f, 1.8f);
            Pillar(g, "Pillar_SE", 6f, 34f, 7f, 1.8f);
            Solid(g, "Sarcophagus", new Vector3(0f, 0.5f, 39f), new Vector3(5f, 1f, 3.5f), _plat, null, 2f);
            Solid(g, "Cover_W", new Vector3(-10.5f, 0.4f, 39f), new Vector3(3f, 0.8f, 3f), _plat, null, 2f);
            Solid(g, "Cover_E", new Vector3(10.5f, 0.4f, 39f), new Vector3(3f, 0.8f, 3f), _plat, null, 2f);

            List<SpawnDef> pts = new List<SpawnDef>
            {
                G(-12, 28), G(12, 28), G(-12, 39), G(12, 39), G(-12, 50), G(12, 50), G(-5, 50.5f), G(5, 50.5f),
                A(-8, 5f, 45), A(8, 5f, 45), A(0, 5.5f, 33)
            };
            BuildEncounter(enc, "Encounter_Room01_CryptPit", "Oda 1: Mezar Çukuru", set,
                new Vector3(0f, 3.5f, 39f), new Vector3(24f, 7f, 24f), pts, new Vector3(0f, 0f, 39f),
                0f, 25f, 0f, 53f, new Vector3(0f, 0.08f, 29f));
        }

        private static void BuildCorridorC2(Transform geo)
        {
            Transform g = Child(geo, "D_Corridor2_Dogleg");
            float h = DoorH;
            FloorCeil(g, "A", -2.5f, 2.5f, 53f, 64f, h);
            FloorCeil(g, "B", -2.5f, 18.5f, 64f, 69f, h);
            FloorCeil(g, "C", 13.5f, 18.5f, 69f, 80f, h);
            WZ(g, "W1", -2.5f, -1, 53f, 69.6f, 0f, h, _wall);
            WZ(g, "W2", 2.5f, 1, 53f, 64f, 0f, h, _wall);
            WX(g, "W3", 64f, -1, 2.5f, 19.1f, 0f, h, _wall);
            WX(g, "W4", 69f, 1, -3.1f, 13.5f, 0f, h, _wall);
            WZ(g, "W5", 13.5f, -1, 69f, 80f, 0f, h, _wall);
            WZ(g, "W6", 18.5f, 1, 64f, 80f, 0f, h, _wall);
            Torch(g, new Vector3(-2.5f, 2.6f, 59f), 90f, false);
            Torch(g, new Vector3(2.5f, 2.6f, 57f), -90f, false);
            Torch(g, new Vector3(6f, 2.6f, 69f), 180f, false);
            Torch(g, new Vector3(12f, 2.6f, 64f), 0f, false);
            Torch(g, new Vector3(13.5f, 2.6f, 75f), 90f, false);
            Torch(g, new Vector3(18.5f, 2.6f, 72f), -90f, false);
        }

        private static void BuildRoomBoneHall(Transform geo, Transform enc, WaveSet set)
        {
            Transform g = Child(geo, "E_Room2_BoneHall");
            RoomShell(g, "Shell", 2, 30, 80, 112, 9f, 16f, 16f, _wall, _floor, _ceil);
            RoomTorches(g, 2, 30, 80, 112, 3.6f, 9f, 16f, 16f, false);

            // Yan balkonlar (y=3) + rampalar: dikey hareket
            Solid(g, "Balcony_W", new Vector3(5f, 1.5f, 103f), new Vector3(6f, 3f, 14f), _plat, null, 2f);
            Solid(g, "Balcony_E", new Vector3(27f, 1.5f, 103f), new Vector3(6f, 3f, 14f), _plat, null, 2f);
            Ramp(g, "Ramp_W", 2f, 8f, 88f, 96f, 0f, 3f, _trim);
            Ramp(g, "Ramp_E", 24f, 30f, 88f, 96f, 0f, 3f, _trim);
            Pillar(g, "Pillar_W", 11f, 100f, 9f, 1.8f);
            Pillar(g, "Pillar_E", 21f, 100f, 9f, 1.8f);
            Solid(g, "Sarcophagus", new Vector3(16f, 0.5f, 106f), new Vector3(4f, 1f, 4f), _plat, null, 2f);

            List<SpawnDef> pts = new List<SpawnDef>
            {
                G(6, 83.5f), G(26, 83.5f), G(12, 91), G(20, 91), G(12, 108), G(20, 108), G(16, 109.5f),
                G(5, 3.05f, 100), G(5, 3.05f, 106), G(27, 3.05f, 100), G(27, 3.05f, 106),
                A(10, 6.5f, 100), A(22, 6.5f, 100), A(16, 7f, 88), A(16, 7f, 108)
            };
            BuildEncounter(enc, "Encounter_Room02_BoneHall", "Oda 2: Kemik Salonu", set,
                new Vector3(16f, 4.5f, 96f), new Vector3(24f, 9f, 28f), pts, new Vector3(16f, 0f, 96f),
                16f, 80f, 16f, 112f, new Vector3(16f, 0.08f, 84f));
        }

        private static void BuildCorridorC3(Transform geo)
        {
            Transform g = Child(geo, "F_Corridor3_Dogleg");
            float h = DoorH;
            FloorCeil(g, "A", 13.5f, 18.5f, 112f, 121f, h);
            FloorCeil(g, "B", -2.5f, 18.5f, 121f, 126f, h);
            FloorCeil(g, "C", -2.5f, 2.5f, 126f, 140f, h);
            WZ(g, "W1", 13.5f, -1, 112f, 121f, 0f, h, _wall);
            WZ(g, "W2", 18.5f, 1, 112f, 126.6f, 0f, h, _wall);
            WX(g, "W3", 121f, -1, -3.1f, 13.5f, 0f, h, _wall);
            WZ(g, "W4", -2.5f, -1, 120.4f, 140f, 0f, h, _wall);
            WX(g, "W5", 126f, 1, 2.5f, 19.1f, 0f, h, _wall);
            WZ(g, "W6", 2.5f, 1, 126f, 140f, 0f, h, _wall);
            Torch(g, new Vector3(13.5f, 2.6f, 116f), 90f, false);
            Torch(g, new Vector3(18.5f, 2.6f, 118f), -90f, false);
            Torch(g, new Vector3(10f, 2.6f, 126f), 180f, false);
            Torch(g, new Vector3(4f, 2.6f, 121f), 0f, false);
            Torch(g, new Vector3(-2.5f, 2.6f, 132f), 90f, false);
            Torch(g, new Vector3(2.5f, 2.6f, 135f), -90f, false);
        }

        private static void BuildRoomOssuary(Transform geo, Transform enc, WaveSet set)
        {
            Transform g = Child(geo, "G_Room3_Ossuary");
            RoomShell(g, "Shell", -16, 16, 140, 176, 11f, 0f, 0f, _wall, _floor, _ceil);
            RoomTorches(g, -16, 16, 140, 176, 4.2f, 9f, 0f, 0f, false);

            Solid(g, "Balcony_W", new Vector3(-13f, 2f, 166f), new Vector3(6f, 4f, 20f), _plat, null, 2f);
            Solid(g, "Balcony_E", new Vector3(13f, 2f, 166f), new Vector3(6f, 4f, 20f), _plat, null, 2f);
            Ramp(g, "Ramp_W", -16f, -10f, 146f, 156f, 0f, 4f, _trim);
            Ramp(g, "Ramp_E", 10f, 16f, 146f, 156f, 0f, 4f, _trim);
            Solid(g, "Bridge", new Vector3(0f, 3.7f, 166f), new Vector3(20f, 0.6f, 4f), _trim, null, 2f);
            Pillar(g, "Pillar_SW", -6f, 150f, 11f, 1.8f);
            Pillar(g, "Pillar_SE", 6f, 150f, 11f, 1.8f);
            Pillar(g, "Pillar_MW", -6f, 158f, 11f, 1.8f);
            Pillar(g, "Pillar_ME", 6f, 158f, 11f, 1.8f);
            Solid(g, "Dais", new Vector3(0f, 0.5f, 152f), new Vector3(6f, 1f, 6f), _plat, null, 2f);

            List<SpawnDef> pts = new List<SpawnDef>
            {
                G(-6, 143), G(6, 143), G(-6, 171), G(6, 171), G(0, 172.5f), G(-8.5f, 154), G(8.5f, 154),
                G(-13, 4.05f, 160), G(-13, 4.05f, 172), G(13, 4.05f, 160), G(13, 4.05f, 172),
                A(-8, 8f, 150), A(8, 8f, 150), A(0, 8f, 171), A(-8, 8f, 164), A(8, 8f, 164)
            };
            BuildEncounter(enc, "Encounter_Room03_Ossuary", "Oda 3: Kemikhane", set,
                new Vector3(0f, 5.5f, 158f), new Vector3(28f, 11f, 32f), pts, new Vector3(0f, 0f, 158f),
                0f, 140f, 0f, 176f, new Vector3(0f, 0.08f, 144f));
        }

        private static void BuildCorridorC4(Transform geo)
        {
            Transform g = Child(geo, "H_Corridor4_ToBoss");
            StraightCorridor(g, 0f, 176f, 200f, 5f);
            for (int i = 0; i < 4; i++)
            {
                float z = 181f + i * 6f;
                Torch(g, new Vector3(i % 2 == 0 ? -2.5f : 2.5f, 3f, z), i % 2 == 0 ? 90f : -90f, true);
            }
        }

        private static void BuildBossRoom(Transform geo, Transform root)
        {
            Transform g = Child(geo, "I_BossRoom");
            RoomShell(g, "Shell", -24, 24, 200, 248, 16f, 0f, null, _wall, _bossFloor, _ceil);
            RoomTorches(g, -24, 24, 200, 248, 5.5f, 12f, 0f, null, true);

            // Sunak (iki kademe) + dev sütunlar
            Solid(g, "Dais_Tier1", new Vector3(0f, 0.2f, 230f), new Vector3(24f, 0.4f, 20f), _plat, null, 3f);
            Solid(g, "Dais_Tier2", new Vector3(0f, 0.6f, 232f), new Vector3(16f, 0.8f, 14f), _plat, null, 3f);
            for (int i = 0; i < 3; i++)
            {
                float z = 208f + i * 16f;
                Pillar(g, "Pillar_L" + i, -17f, z, 16f, 2.4f);
                Pillar(g, "Pillar_R" + i, 17f, z, 16f, 2.4f);
                Torch(g, new Vector3(-15.8f, 4f, z), -90f, true);
                Torch(g, new Vector3(15.8f, 4f, z), 90f, true);
            }
            // Kapı çerçevesi
            Pillar(g, "DoorFrame_L", -4.2f, 200.8f, 8f, 1.6f);
            Pillar(g, "DoorFrame_R", 4.2f, 200.8f, 8f, 1.6f);

            // Boss placeholder: şimdilik boss yok; ileride bu sınır + çapa kullanılarak direktör bağlanır.
            GameObject bossRoot = Child(root, "BossEncounter_Placeholder").gameObject;
            GameObject bounds = new GameObject("Boss_ArenaBounds");
            bounds.transform.SetParent(bossRoot.transform, false);
            bounds.transform.position = new Vector3(0f, 8f, 224f);
            BoxCollider bc = bounds.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(44f, 16f, 44f);
            GameObject anchor = new GameObject("Boss_SpawnAnchor");
            anchor.transform.SetParent(bossRoot.transform, false);
            anchor.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 234f), Quaternion.Euler(0f, 180f, 0f));
            GameObject cp = new GameObject("Boss_PlayerCheckpoint");
            cp.transform.SetParent(bossRoot.transform, false);
            cp.transform.position = new Vector3(0f, 0.08f, 205f);
        }

        // ───────────────────────────── ENCOUNTER ─────────────────────────────

        private struct SpawnDef { public Vector3 pos; public bool air; }
        private static SpawnDef G(float x, float z) { return new SpawnDef { pos = new Vector3(x, 0.05f, z), air = false }; }
        private static SpawnDef G(float x, float y, float z) { return new SpawnDef { pos = new Vector3(x, y, z), air = false }; }
        private static SpawnDef A(float x, float y, float z) { return new SpawnDef { pos = new Vector3(x, y, z), air = true }; }

        private static void BuildEncounter(Transform enc, string goName, string display, WaveSet set,
            Vector3 areaCenter, Vector3 areaSize, List<SpawnDef> points, Vector3 lookTarget,
            float doorX, float entryZ, float exitDoorX, float exitZ, Vector3 checkpointPos)
        {
            GameObject room = new GameObject(goName);
            room.transform.SetParent(enc, false);
            room.transform.position = areaCenter;

            BoxCollider box = room.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = areaSize;
            EncounterArea area = room.AddComponent<EncounterArea>();
            WaveSpawnDirector director = room.AddComponent<WaveSpawnDirector>();

            Transform spRoot = Child(room.transform, "SpawnPoints");
            List<EncounterSpawnPoint> spList = new List<EncounterSpawnPoint>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                GameObject go = new GameObject((points[i].air ? "Air_" : "Ground_") + (i + 1).ToString("D2"));
                go.transform.SetParent(spRoot, false);
                go.transform.position = points[i].pos;
                Vector3 look = lookTarget - points[i].pos; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(look);
                EncounterSpawnPoint sp = go.AddComponent<EncounterSpawnPoint>();
                sp.Configure(!points[i].air, points[i].air, string.Empty, points[i].air ? 2.5f : 1.6f);
                spList.Add(sp);
            }

            Transform gates = Child(room.transform, "Gates");
            GameObject gateIn = Gate(gates, "Gate_Entry", new Vector3(doorX, DoorH * 0.5f, entryZ - WallT * 0.5f));
            GameObject gateOut = Gate(gates, "Gate_Exit", new Vector3(exitDoorX, DoorH * 0.5f, exitZ + WallT * 0.5f));

            Transform cp = Child(room.transform, "RetryCheckpoint");
            cp.position = checkpointPos;

            SerializedObject soArea = new SerializedObject(area);
            soArea.FindProperty("waveDirector").objectReferenceValue = director;
            soArea.FindProperty("autoDetectEnemiesInBounds").boolValue = false;
            soArea.FindProperty("autoGenerateSpawnPointsIfEmpty").boolValue = false;
            soArea.FindProperty("delayBeforeWaveStart").floatValue = 0.5f;
            soArea.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject so = new SerializedObject(director);
            so.FindProperty("waveSet").objectReferenceValue = set;
            so.FindProperty("encounterArea").objectReferenceValue = area;
            so.FindProperty("roomDisplayName").stringValue = display;
            so.FindProperty("autoStartOnPlay").boolValue = false;
            so.FindProperty("minSpawnDistance").floatValue = 7f;
            so.FindProperty("maxSpawnDistance").floatValue = 70f;
            so.FindProperty("retryCheckpoint").objectReferenceValue = cp;
            SerializedProperty bar = so.FindProperty("arenaBarriers");
            bar.arraySize = 2;
            bar.GetArrayElementAtIndex(0).objectReferenceValue = gateIn;
            bar.GetArrayElementAtIndex(1).objectReferenceValue = gateOut;
            SerializedProperty sps = so.FindProperty("spawnPoints");
            sps.arraySize = spList.Count;
            for (int i = 0; i < spList.Count; i++) sps.GetArrayElementAtIndex(i).objectReferenceValue = spList[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Gate(Transform parent, string name, Vector3 pos)
        {
            GameObject g = Solid(parent, name, pos, new Vector3(DoorW + 0.2f, DoorH, 0.8f), _gate, null, 2f);
            g.isStatic = false;
            return g;
        }

        // ───────────────────────────── DALGA VERİSİ ─────────────────────────────

        private class Grp { public GameObject prefab; public int count; public float interval; public bool air; public float delay; public int batch = 1; public int pattern; public bool parallel; }
        private class Wv { public string name; public float pre = 1f; public int cap = 6; public float budget; public int mode; public int remaining = 2; public float pct = 0.75f; public float time = 30f; public int hp = 1; public int ammo = 1; public Grp[] groups; }

        private static Grp Gh(int count, float interval, int batch, int pattern, float delay = 0f, bool parallel = false)
        { return new Grp { prefab = _ghoul, count = count, interval = interval, batch = batch, pattern = pattern, delay = delay, parallel = parallel }; }
        private static Grp Ey(int count, float interval, int pattern, float delay, bool parallel = true)
        { return new Grp { prefab = _eye, count = count, interval = interval, air = true, pattern = pattern, delay = delay, parallel = parallel }; }

        // mode: 0 KillAll, 1 RemainingThreshold, 2 Percentage, 3 Time, 4 PercentageOrTime | pattern: 0 Random, 1 RoundRobin, 2 Farthest
        private static Wv[] WavesR1()
        {
            return new[]
            {
                new Wv { name = "Faz 1: Uyanış", pre = 1f, cap = 4, mode = 1, remaining = 2, groups = new[] { Gh(5, 0.9f, 1, 1) } },
                new Wv { name = "Faz 2: Sürü", pre = 0.8f, cap = 6, mode = 4, pct = 0.65f, time = 25f, groups = new[] { Gh(8, 1f, 2, 0) } },
                new Wv { name = "Faz 3: Kapanış (Final)", pre = 0.8f, cap = 8, mode = 0, hp = 2, ammo = 2,
                    groups = new[] { Gh(6, 1.2f, 3, 0), Gh(4, 1f, 4, 0, 6f, true) } }
            };
        }

        private static Wv[] WavesR2()
        {
            return new[]
            {
                new Wv { name = "Faz 1: Gökyüzü Gözleri", pre = 1f, cap = 6, budget = 8, mode = 2, pct = 0.7f, groups = new[] { Gh(6, 1f, 2, 1), Ey(2, 2.5f, 2, 3f) } },
                new Wv { name = "Faz 2: Kuşatma", pre = 0.8f, cap = 8, budget = 10, mode = 4, pct = 0.7f, time = 28f, groups = new[] { Gh(8, 0.9f, 2, 0), Ey(3, 2f, 2, 2f) } },
                new Wv { name = "Faz 3: Balkon Baskısı", pre = 0.8f, cap = 10, budget = 12, mode = 4, pct = 0.75f, time = 30f, hp = 2, groups = new[] { Gh(10, 1.1f, 3, 0), Ey(4, 1.8f, 0, 2f) } },
                new Wv { name = "Faz 4: Katliam (Final)", pre = 1f, cap = 12, budget = 14, mode = 0, hp = 2, ammo = 2,
                    groups = new[] { Gh(10, 1f, 3, 1), Ey(5, 1.5f, 2, 2f), Gh(4, 1f, 4, 0, 8f, true) } }
            };
        }

        private static Wv[] WavesR3()
        {
            return new[]
            {
                new Wv { name = "Faz 1: Kemik Yağmuru", pre = 1f, cap = 8, budget = 10, mode = 1, remaining = 2, groups = new[] { Gh(8, 0.8f, 2, 1), Ey(2, 2f, 2, 2f) } },
                new Wv { name = "Faz 2: Köprü Savaşı", pre = 0.8f, cap = 10, budget = 12, mode = 4, pct = 0.7f, time = 25f, groups = new[] { Gh(10, 0.9f, 3, 0), Ey(3, 1.6f, 2, 2f) } },
                new Wv { name = "Faz 3: Ruhların Dansı", pre = 0.8f, cap = 12, budget = 14, mode = 4, pct = 0.7f, time = 28f, hp = 2, groups = new[] { Gh(12, 1f, 3, 1), Ey(5, 1.4f, 0, 1f) } },
                new Wv { name = "Faz 4: Kıyamet Yaklaşıyor", pre = 0.8f, cap = 14, budget = 16, mode = 4, pct = 0.75f, time = 30f, hp = 2, ammo = 2, groups = new[] { Gh(12, 1f, 4, 0), Ey(6, 1.2f, 2, 1f) } },
                new Wv { name = "Faz 5: Eşik (Final)", pre = 1f, cap = 16, budget = 18, mode = 0, hp = 3, ammo = 3,
                    groups = new[] { Gh(12, 1.1f, 4, 0), Ey(6, 1.2f, 0, 1f), Gh(6, 1f, 6, 1, 6f, true) } }
            };
        }

        private static WaveSet MakeWaveSet(string assetName, string notes, Wv[] waves)
        {
            string path = WaveDir + "/" + assetName + ".asset";
            WaveSet set = AssetDatabase.LoadAssetAtPath<WaveSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<WaveSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            SerializedObject so = new SerializedObject(set);
            so.FindProperty("designNotes").stringValue = notes;
            SerializedProperty wp = so.FindProperty("waves");
            wp.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                Wv w = waves[i];
                SerializedProperty e = wp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("waveName").stringValue = w.name;
                e.FindPropertyRelative("preWaveDelay").floatValue = w.pre;
                e.FindPropertyRelative("maxConcurrentEnemies").intValue = w.cap;
                e.FindPropertyRelative("threatBudget").floatValue = w.budget;
                e.FindPropertyRelative("transitionMode").enumValueIndex = w.mode;
                e.FindPropertyRelative("remainingThreshold").intValue = w.remaining;
                e.FindPropertyRelative("percentageThreshold").floatValue = w.pct;
                e.FindPropertyRelative("timeLimit").floatValue = w.time;
                e.FindPropertyRelative("carryOverRemainingEnemies").boolValue = true;
                e.FindPropertyRelative("rewardHealthOrbs").intValue = w.hp;
                e.FindPropertyRelative("rewardAmmoPickups").intValue = w.ammo;
                SerializedProperty se = e.FindPropertyRelative("spawnEntries");
                se.arraySize = w.groups.Length;
                for (int g = 0; g < w.groups.Length; g++)
                {
                    Grp gr = w.groups[g];
                    SerializedProperty s = se.GetArrayElementAtIndex(g);
                    s.FindPropertyRelative("enemyPrefab").objectReferenceValue = gr.prefab;
                    s.FindPropertyRelative("count").intValue = gr.count;
                    s.FindPropertyRelative("spawnInterval").floatValue = gr.interval;
                    s.FindPropertyRelative("isAirEnemy").boolValue = gr.air;
                    s.FindPropertyRelative("targetZoneTag").stringValue = string.Empty;
                    s.FindPropertyRelative("delayFromWaveStart").floatValue = gr.delay;
                    s.FindPropertyRelative("batchSize").intValue = gr.batch;
                    s.FindPropertyRelative("spawnPattern").enumValueIndex = gr.pattern;
                    s.FindPropertyRelative("runInParallel").boolValue = gr.parallel;
                    s.FindPropertyRelative("threatCostOverride").floatValue = 0f;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        // ───────────────────────────── GEOMETRİ YARDIMCILARI ─────────────────────────────

        private static Transform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Kutu geometri: dünya-ölçekli UV'li mesh + BoxCollider.</summary>
        private static GameObject Solid(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Quaternion? rot = null, float tile = 4f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            go.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            return go;
        }

        private static void FloorCeil(Transform p, string n, float x0, float x1, float z0, float z1, float h)
        {
            Solid(p, n + "_Floor", new Vector3((x0 + x1) * 0.5f, -0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), _floor);
            Solid(p, n + "_Ceiling", new Vector3((x0 + x1) * 0.5f, h + 0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), _ceil);
        }

        /// <summary>X ekseni boyunca duvar; iç yüz z'de, dış taraf = side (±1).</summary>
        private static void WX(Transform p, string n, float z, int side, float xa, float xb, float y0, float y1, Material m)
        {
            Solid(p, n, new Vector3((xa + xb) * 0.5f, (y0 + y1) * 0.5f, z + side * WallT * 0.5f), new Vector3(xb - xa, y1 - y0, WallT), m);
        }

        /// <summary>Z ekseni boyunca duvar; iç yüz x'te, dış taraf = side (±1).</summary>
        private static void WZ(Transform p, string n, float x, int side, float za, float zb, float y0, float y1, Material m)
        {
            Solid(p, n, new Vector3(x + side * WallT * 0.5f, (y0 + y1) * 0.5f, (za + zb) * 0.5f), new Vector3(WallT, y1 - y0, zb - za), m);
        }

        private static void WXGap(Transform p, string n, float z, int side, float xa, float xb, float h, float? door, Material m)
        {
            if (!door.HasValue) { WX(p, n, z, side, xa, xb, 0f, h, m); return; }
            float a = door.Value - DoorW * 0.5f, b = door.Value + DoorW * 0.5f;
            WX(p, n + "_L", z, side, xa, a, 0f, h, m);
            WX(p, n + "_R", z, side, b, xb, 0f, h, m);
            if (h > DoorH + 0.01f) WX(p, n + "_Lintel", z, side, a, b, DoorH, h, m);
        }

        private static void RoomShell(Transform parent, string n, float x0, float x1, float z0, float z1, float h,
            float? doorS, float? doorN, Material wall, Material floor, Material ceil)
        {
            Transform g = Child(parent, n);
            Solid(g, "Floor", new Vector3((x0 + x1) * 0.5f, -0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), floor);
            Solid(g, "Ceiling", new Vector3((x0 + x1) * 0.5f, h + 0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), ceil);
            WZ(g, "Wall_W", x0, -1, z0, z1, 0f, h, wall);
            WZ(g, "Wall_E", x1, 1, z0, z1, 0f, h, wall);
            WXGap(g, "Wall_S", z0, -1, x0 - WallT, x1 + WallT, h, doorS, wall);
            WXGap(g, "Wall_N", z1, 1, x0 - WallT, x1 + WallT, h, doorN, wall);
        }

        private static void StraightCorridor(Transform g, float cx, float z0, float z1, float h)
        {
            FloorCeil(g, "Seg", cx - DoorW * 0.5f, cx + DoorW * 0.5f, z0, z1, h);
            WZ(g, "Wall_W", cx - DoorW * 0.5f, -1, z0, z1, 0f, h, _wall);
            WZ(g, "Wall_E", cx + DoorW * 0.5f, 1, z0, z1, 0f, h, _wall);
        }

        private static void Pillar(Transform p, string n, float x, float z, float h, float size)
        {
            Solid(p, n, new Vector3(x, h * 0.5f, z), new Vector3(size, h, size), _trim, null, 2f);
        }

        private static void Ramp(Transform p, string n, float x0, float x1, float zA, float zB, float yA, float yB, Material m)
        {
            float run = zB - zA, rise = yB - yA, t = 0.5f;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run);
            Vector3 mid = new Vector3((x0 + x1) * 0.5f, (yA + yB) * 0.5f, (zA + zB) * 0.5f);
            Vector3 normal = new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang));
            Solid(p, n, mid - normal * (t * 0.5f), new Vector3(x1 - x0, t, len), m, Quaternion.Euler(-ang * Mathf.Rad2Deg, 0f, 0f), 2f);
        }

        private static void Dummy(Transform parent, Vector3 pos)
        {
            GameObject d = new GameObject("Combat_Dummy_Target");
            d.transform.SetParent(parent, false);
            d.transform.position = pos;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body"; body.transform.SetParent(d.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f); body.transform.localScale = new Vector3(0.45f, 0.7f, 0.3f);
            body.GetComponent<Renderer>().sharedMaterial = _plat;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(d.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f); head.transform.localScale = Vector3.one * 0.35f;
            head.GetComponent<Renderer>().sharedMaterial = _plat;
            d.AddComponent<CombatDummy>();
        }

        // ───────────────────────────── MEŞALELER ─────────────────────────────

        private static void RoomTorches(Transform p, float x0, float x1, float z0, float z1, float y, float spacing, float? doorS, float? doorN, bool boss)
        {
            Transform g = Child(p, "Torches");
            float[] skipS = doorS.HasValue ? new[] { doorS.Value } : null;
            float[] skipN = doorN.HasValue ? new[] { doorN.Value } : null;
            TorchRow(g, true, z0, x0 + 3f, x1 - 3f, spacing, y, 0f, skipS, boss);
            TorchRow(g, true, z1, x0 + 3f, x1 - 3f, spacing, y, 180f, skipN, boss);
            TorchRow(g, false, x0, z0 + 3f, z1 - 3f, spacing, y, 90f, null, boss);
            TorchRow(g, false, x1, z0 + 3f, z1 - 3f, spacing, y, -90f, null, boss);
        }

        private static void CorridorTorches(Transform p, float cx, float za, float zb, float y, float spacing)
        {
            Transform g = Child(p, "Torches");
            TorchRow(g, false, cx - DoorW * 0.5f, za, zb, spacing, y, 90f, null, false);
            TorchRow(g, false, cx + DoorW * 0.5f, za + spacing * 0.5f, zb, spacing, y, -90f, null, false);
        }

        private static void TorchRow(Transform p, bool alongX, float fixedC, float a, float b, float spacing, float y, float yaw, float[] skip, bool boss)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a) / spacing));
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? (a + b) * 0.5f : a + (b - a) * (i + 0.5f) / n;
                if (skip != null && IsNear(t, skip, 3.6f)) continue;
                Vector3 pos = alongX ? new Vector3(t, y, fixedC) : new Vector3(fixedC, y, t);
                Torch(p, pos, yaw, boss);
            }
        }

        private static bool IsNear(float v, float[] c, float d)
        {
            for (int i = 0; i < c.Length; i++) if (Mathf.Abs(v - c[i]) < d) return true;
            return false;
        }

        private static void Torch(Transform parent, Vector3 pos, float yaw, bool boss)
        {
            GameObject t = new GameObject("Torch");
            t.transform.SetParent(parent, false);
            t.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (boss) t.transform.localScale = Vector3.one * 1.6f;

            Prim(PrimitiveType.Cube, "Bracket", t.transform, new Vector3(0f, -0.10f, 0.20f), new Vector3(0.08f, 0.28f, 0.38f), _iron);
            Prim(PrimitiveType.Cylinder, "Cup", t.transform, new Vector3(0f, 0.04f, 0.36f), new Vector3(0.20f, 0.10f, 0.20f), _iron);
            Prim(PrimitiveType.Sphere, "Flame", t.transform, new Vector3(0f, 0.16f, 0.36f), new Vector3(0.15f, 0.20f, 0.15f), _flame);

            GameObject lo = new GameObject("Torch_PointLight");
            lo.transform.SetParent(t.transform, false);
            lo.transform.localPosition = new Vector3(0f, 0.22f, 0.38f);
            Light l = lo.AddComponent<Light>();
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            Color col = boss ? new Color(1f, 0.28f, 0.12f) : new Color(1f, 0.55f, 0.18f);
            l.color = col;
            l.intensity = boss ? 5.5f : 3.4f;
            l.range = boss ? 20f : 14f;

            GothicTorchFlicker fl = lo.AddComponent<GothicTorchFlicker>();
            SerializedObject so = new SerializedObject(fl);
            so.FindProperty("baseIntensity").floatValue = l.intensity;
            so.FindProperty("flickerIntensity").floatValue = boss ? 0.9f : 0.5f;
            so.FindProperty("flameColor").colorValue = col;
            so.FindProperty("emberColor").colorValue = boss ? new Color(0.8f, 0.12f, 0.05f) : new Color(0.95f, 0.35f, 0.08f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Prim(PrimitiveType type, string name, Transform parent, Vector3 lp, Vector3 ls, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = ls;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ───────────────────────────── ATMOSFER ─────────────────────────────

        private static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.03f, 0.045f);
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 60f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.21f);
        }

        // ───────────────────────────── MESH / MATERYAL / DOKU ─────────────────────────────

        private static Mesh BoxMesh(Vector3 s, float tile)
        {
            string key = s.x.ToString("F3") + "_" + s.y.ToString("F3") + "_" + s.z.ToString("F3") + "_" + tile;
            if (MeshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            Vector3 h = s * 0.5f;
            Vector3[] v = new Vector3[24]; Vector3[] nrm = new Vector3[24]; Vector2[] uv = new Vector2[24];
            int[] tri = new int[36];
            // n, u, v eksenleri: cross(u,v) = -n (Unity saat yönü sarma)
            Vector3[] N = { Vector3.back, Vector3.forward, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            Vector3[] U = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.right, Vector3.forward };
            Vector3[] V = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.right };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = N[f], u = U[f], w = V[f];
                float hu = Mathf.Abs(Vector3.Dot(u, h)), hv = Mathf.Abs(Vector3.Dot(w, h)), hn = Mathf.Abs(Vector3.Dot(n, h));
                Vector3 c = n * hn;
                int i = f * 4;
                v[i] = c - u * hu - w * hv; v[i + 1] = c + u * hu - w * hv; v[i + 2] = c - u * hu + w * hv; v[i + 3] = c + u * hu + w * hv;
                float uu = 2f * hu / tile, vv = 2f * hv / tile;
                uv[i] = new Vector2(0, 0); uv[i + 1] = new Vector2(uu, 0); uv[i + 2] = new Vector2(0, vv); uv[i + 3] = new Vector2(uu, vv);
                for (int k = 0; k < 4; k++) nrm[i + k] = n;
                int t = f * 6;
                tri[t] = i; tri[t + 1] = i + 2; tri[t + 2] = i + 1; tri[t + 3] = i + 2; tri[t + 4] = i + 3; tri[t + 5] = i + 1;
            }
            Mesh m = new Mesh { name = "Box_" + key, vertices = v, normals = nrm, uv = uv, triangles = tri };
            m.RecalculateBounds();
            m.RecalculateTangents();
            MeshCache[key] = m;
            return m;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Data/Materials/Level"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Data/Textures/Level"));
            AssetDatabase.Refresh();
        }

        private static void LoadMaterials()
        {
            Texture2D floorT = Tex("Tex_L_Floor", 64, (x, y) => TexFloor(x, y, 1.7f, false));
            Texture2D wallT = Tex("Tex_L_Wall", 64, TexWall);
            Texture2D ceilT = Tex("Tex_L_Ceil", 64, (x, y) => TexFloor(x, y, 1.0f, false));
            Texture2D platT = Tex("Tex_L_Plat", 32, TexTomb);
            Texture2D trimT = Tex("Tex_L_Trim", 32, TexIron);
            Texture2D bossT = Tex("Tex_L_BossFloor", 64, (x, y) => TexFloor(x, y, 1.3f, true));

            _floor = Mat("Mat_L_Floor", floorT, 0.10f, 0f);
            _wall = Mat("Mat_L_Wall", wallT, 0.10f, 0f);
            _ceil = Mat("Mat_L_Ceil", ceilT, 0.05f, 0f);
            _plat = Mat("Mat_L_Plat", platT, 0.12f, 0f);
            _trim = Mat("Mat_L_Trim", trimT, 0.25f, 0.5f);
            _bossFloor = Mat("Mat_L_BossFloor", bossT, 0.25f, 0f);
            _iron = Mat("Mat_L_TorchIron", null, 0.3f, 0.7f, new Color(0.08f, 0.08f, 0.09f));
            _flame = Mat("Mat_L_TorchFlame", null, 0f, 0f, new Color(1f, 0.55f, 0.12f), true);
            _gate = AssetDatabase.LoadAssetAtPath<Material>(GatePath);
            if (_gate == null) _gate = Mat("Mat_L_Gate", null, 0.3f, 0.5f, new Color(0.5f, 0.05f, 0.05f));
        }

        private static Material Mat(string name, Texture2D tex, float smooth, float metal, Color? tint = null, bool unlit = false)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            Color c = tint ?? Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D Tex(string name, int size, System.Func<int, int, Color> px)
        {
            string assetPath = TexDir + "/" + name + ".png";
            string full = Path.Combine(Application.dataPath, "..", assetPath);
            if (!File.Exists(full))
            {
                Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] c = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Color p = px(x, y); p.a = 1f; c[y * size + x] = p;
                    }
                t.SetPixels(c); t.Apply();
                File.WriteAllBytes(full, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Color TexFloor(int x, int y, float gain, bool boss)
        {
            int tx = x % 16, ty = y % 16;
            if (tx == 0 || ty == 0)
            {
                float mn = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.05f;
                return new Color(0.04f + mn, 0.035f + mn, 0.045f + mn);
            }
            float s = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
            float b = (0.18f + s * 0.08f + Mathf.Sin(x * 12.3f) * Mathf.Cos(y * 8.7f) * 0.04f) * gain;
            Color c = new Color(b, b * 0.95f, b * 0.92f);
            if (boss)
            {
                c = new Color(b * 0.55f, b * 0.45f, b * 0.5f);
                float vein = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * 0.09f + 3f, y * 0.09f + 3f) - 1f);
                if (vein > 0.9f) c = Color.Lerp(c, new Color(0.7f, 0.06f, 0.05f), (vein - 0.9f) * 9f);
            }
            else
            {
                float grime = Mathf.PerlinNoise(x * 0.06f + 10f, y * 0.06f + 10f);
                if (grime > 0.65f) c = Color.Lerp(c, new Color(0.18f, 0.04f, 0.04f), (grime - 0.65f) * 2.2f);
            }
            return c;
        }

        private static Color TexWall(int x, int y)
        {
            int row = y / 8, rowY = y % 8;
            int ox = (row % 2 == 1) ? 8 : 0;
            int colX = (x + ox) % 16;
            if (rowY == 0 || colX == 0) return new Color(0.03f, 0.025f, 0.035f);
            float bn = Mathf.PerlinNoise((x + row * 17f) * 0.2f, y * 0.2f);
            float crack = Mathf.PerlinNoise(x * 0.8f, y * 0.8f) > 0.72f ? -0.06f : 0f;
            float b = (0.13f + bn * 0.07f + crack) * 1.6f;
            return new Color(b * 0.95f, b, b * 1.05f);
        }

        private static Color TexTomb(int x, int y)
        {
            float n = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
            float grain = Mathf.Sin(x * 35.1f) * Mathf.Cos(y * 42.7f) * 0.03f;
            float b = (0.14f + n * 0.06f + grain) * 1.6f;
            Color c = new Color(b, b, b * 0.95f);
            float moss = Mathf.PerlinNoise(x * 0.1f + 2f, y * 0.1f + 2f);
            if (moss > 0.62f) c = Color.Lerp(c, new Color(0.08f, 0.14f, 0.07f), (moss - 0.62f) * 1.5f);
            return c;
        }

        private static Color TexIron(int x, int y)
        {
            float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
            float rust = Mathf.PerlinNoise(x * 0.18f + 5f, y * 0.18f + 5f);
            float b = (0.10f + n * 0.05f) * 1.7f;
            Color c = new Color(b, b * 1.02f, b * 1.08f);
            if (rust > 0.58f) c = Color.Lerp(c, new Color(0.26f, 0.08f, 0.04f), (rust - 0.58f) * 1.8f);
            return c;
        }

        private static void AddToBuildSettings()
        {
            EditorBuildSettingsScene[] cur = EditorBuildSettings.scenes;
            for (int i = 0; i < cur.Length; i++) if (cur[i].path == ScenePath) return;
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(cur) { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
