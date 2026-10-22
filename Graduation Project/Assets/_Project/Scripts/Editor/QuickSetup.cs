using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using RetroFPS.Weapons;
using RetroFPS.UI;
using RetroFPS.Combat;

namespace RetroFPS.Editor
{
    /// <summary>
    /// Tek tıkla sahnede Player ve Test Arenasını mükemmel insan ölçeğinde (Human Architecture Scale) kuran araç.
    /// Unity üst menüsünde "RetroFPS -> 1-Click Setup Scene & Player" olarak görünür.
    /// </summary>
    public static class QuickSetup
    {
        [MenuItem("RetroFPS/1-Click Setup Scene & Player", false, 1)]
        public static void SetupSceneAndPlayer()
        {
            // 0. Proje Çözünürlüğünü 1920x1080 Full HD Olarak Yapılandır
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultWebScreenWidth = 1920;
            PlayerSettings.defaultWebScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            ResetGameViewScale();

            // 1. Test Arenasını bul veya oluştur
            GymArenaBuilder arena = Object.FindAnyObjectByType<GymArenaBuilder>();
            if (arena == null)
            {
                GameObject arenaGO = new GameObject("TestArena");
                arena = arenaGO.AddComponent<GymArenaBuilder>();
                Undo.RegisterCreatedObjectUndo(arenaGO, "Create Test Arena");
            }

            // Arenayı editörde hemen inşa et (görsel olarak sahneye yerleşsin)
            arena.BuildArena();

            // 2. Player objesini bul veya oluştur
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                Undo.RegisterCreatedObjectUndo(player, "Create Player");
            }

            // Pozisyonu tam zemin temas noktasına yerleştir (Y = 0.08: ayaklar tam zemine basar, asla düşme olmaz)
            player.transform.position = new Vector3(0f, 0.08f, -10f);
            player.transform.rotation = Quaternion.identity;

            // Varsa CapsuleCollider'ı kaldır (CharacterController yönetecek)
            CapsuleCollider capsuleCol = player.GetComponent<CapsuleCollider>();
            if (capsuleCol != null)
            {
                Object.DestroyImmediate(capsuleCol);
            }

            // MeshRenderer'ı ShadowsOnly yap (kendi kapsülümüz gözümüzün önüne çıkmasın)
            MeshRenderer meshRend = player.GetComponent<MeshRenderer>();
            if (meshRend != null)
            {
                meshRend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            // 3. CameraHolder ve Main Camera Kurulumu
            Transform cameraHolder = player.transform.Find("CameraHolder");
            if (cameraHolder == null)
            {
                GameObject holderGO = new GameObject("CameraHolder");
                holderGO.transform.SetParent(player.transform);
                holderGO.transform.localPosition = new Vector3(0f, 1.55f, 0f); // 1.80m manken ile tam göz seviyesi
                holderGO.transform.localRotation = Quaternion.identity;
                cameraHolder = holderGO.transform;
                Undo.RegisterCreatedObjectUndo(holderGO, "Create CameraHolder");
            }
            else
            {
                cameraHolder.localPosition = new Vector3(0f, 1.55f, 0f);
            }

            // Sahnede Main Camera bul ve CameraHolder altına al
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = Object.FindAnyObjectByType<Camera>();
            }

            if (mainCam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(camGO, "Create Main Camera");
            }

            mainCam.transform.SetParent(cameraHolder);
            mainCam.transform.localPosition = Vector3.zero;
            mainCam.transform.localRotation = Quaternion.identity;
            mainCam.fieldOfView = 65f; // Doğal FPS FOV'u (16:9 ekranda ~95 derece)
            mainCam.nearClipPlane = 0.05f;

            // Main Camera üzerindeki yanlışlıkla eklenmiş fazladan kontrolcüleri temizle
            CharacterController camCC = mainCam.GetComponent<CharacterController>();
            if (camCC != null) Object.DestroyImmediate(camCC);

            PlayerMovement camPM = mainCam.GetComponent<PlayerMovement>();
            if (camPM != null) Object.DestroyImmediate(camPM);

            PlayerLook camPL = mainCam.GetComponent<PlayerLook>();
            if (camPL != null) Object.DestroyImmediate(camPL);

            PlayerDebugHUD camHUD = mainCam.GetComponent<PlayerDebugHUD>();
            if (camHUD != null) Object.DestroyImmediate(camHUD);

            // CameraHolder üzerindeki fazlalıkları temizle
            CharacterController holderCC = cameraHolder.GetComponent<CharacterController>();
            if (holderCC != null) Object.DestroyImmediate(holderCC);

            PlayerMovement holderPM = cameraHolder.GetComponent<PlayerMovement>();
            if (holderPM != null) Object.DestroyImmediate(holderPM);

            // 4. CharacterController Yapılandırması
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1.0f, 0f); // Tabanı Y=0'a denk getirir
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;

            // 5. PlayerMovement Yapılandırması
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            if (pm == null) pm = player.AddComponent<PlayerMovement>();

            SerializedObject soPm = new SerializedObject(pm);
            soPm.FindProperty("cameraHolder").objectReferenceValue = cameraHolder;
            soPm.FindProperty("walkSpeed").floatValue = 8.0f;
            soPm.FindProperty("groundAcceleration").floatValue = 18f;
            soPm.FindProperty("groundFriction").floatValue = 8.5f;
            soPm.FindProperty("airAcceleration").floatValue = 4.0f;
            soPm.FindProperty("airWishSpeed").floatValue = 3.5f;
            soPm.FindProperty("maxAirSpeed").floatValue = 11.5f;
            soPm.FindProperty("jumpForce").floatValue = 7.2f;
            soPm.FindProperty("gravity").floatValue = 25f;
            soPm.FindProperty("dashSpeed").floatValue = 30f;
            soPm.FindProperty("dashDuration").floatValue = 0.25f;
            soPm.FindProperty("slideBoost").floatValue = 13.0f;
            soPm.FindProperty("slideFriction").floatValue = 3.2f;
            soPm.FindProperty("slideDurationMax").floatValue = 0.85f;
            soPm.FindProperty("normalHeight").floatValue = 2.0f;
            soPm.FindProperty("normalCameraY").floatValue = 1.55f;
            soPm.FindProperty("slideHeight").floatValue = 1.0f;
            soPm.FindProperty("slideCameraY").floatValue = 0.65f;
            soPm.ApplyModifiedProperties();

            // 6. PlayerLook Yapılandırması
            PlayerLook pl = player.GetComponent<PlayerLook>();
            if (pl == null) pl = player.AddComponent<PlayerLook>();

            SerializedObject soPl = new SerializedObject(pl);
            soPl.FindProperty("cameraHolder").objectReferenceValue = cameraHolder;
            soPl.FindProperty("playerCamera").objectReferenceValue = mainCam;
            soPl.FindProperty("mouseSensitivity").floatValue = 0.10f;
            soPl.FindProperty("smoothLook").boolValue = true;
            soPl.FindProperty("smoothSpeed").floatValue = 35f;
            soPl.FindProperty("invertY").boolValue = false;
            soPl.FindProperty("baseFOV").floatValue = 65f;
            soPl.FindProperty("maxFOV").floatValue = 78f;
            soPl.ApplyModifiedProperties();

            // 7. PlayerDebugHUD ve CrosshairHUD Ekle
            PlayerDebugHUD hud = player.GetComponent<PlayerDebugHUD>();
            if (hud == null) player.AddComponent<PlayerDebugHUD>();

            CrosshairHUD crosshair = player.GetComponent<CrosshairHUD>();
            if (crosshair == null) player.AddComponent<CrosshairHUD>();

            // 8. PlayerHealth (Oyuncu Canı ve Dash I-Frame Hasar Savuşturma)
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph == null) player.AddComponent<PlayerHealth>();

            // 9. HitstopManager (Mikro Dondurma / Vuruş Hissi Yöneticisi)
            HitstopManager hitstop = Object.FindAnyObjectByType<HitstopManager>();
            if (hitstop == null)
            {
                GameObject hmGO = new GameObject("HitstopManager");
                hitstop = hmGO.AddComponent<HitstopManager>();
                Undo.RegisterCreatedObjectUndo(hmGO, "Create HitstopManager");
            }

            // 10. Silah Görünüm Modelleri (Viewmodels) ve Modüler Silah Kurulumu
            Transform weaponHolder = mainCam.transform.Find("WeaponHolder");
            if (weaponHolder == null)
            {
                GameObject whGO = new GameObject("WeaponHolder");
                whGO.transform.SetParent(mainCam.transform);
                whGO.transform.localPosition = Vector3.zero;
                whGO.transform.localRotation = Quaternion.identity;
                weaponHolder = whGO.transform;
                Undo.RegisterCreatedObjectUndo(whGO, "Create WeaponHolder");
            }

            // Slot 1: Arbalest (Hızlı Menzilli Silah Modeli Proxy)
            Transform arbalest = weaponHolder.Find("Weapon_Crossbow");
            if (arbalest == null)
            {
                GameObject crossGO = CreateCrossbowModelProxy(weaponHolder);
                arbalest = crossGO.transform;
            }
            RepeaterArbalestWeapon arbalestWep = arbalest.GetComponent<RepeaterArbalestWeapon>();
            if (arbalestWep == null) arbalestWep = arbalest.gameObject.AddComponent<RepeaterArbalestWeapon>();

            // Slot 2: İnfaz Kılıcı (Ağır Yakın Dövüş Modeli Proxy)
            Transform greatsword = weaponHolder.Find("Weapon_Greatsword");
            if (greatsword == null)
            {
                GameObject swordGO = CreateGreatswordModelProxy(weaponHolder);
                greatsword = swordGO.transform;
            }
            ExecutionerBladeWeapon bladeWep = greatsword.GetComponent<ExecutionerBladeWeapon>();
            if (bladeWep == null) bladeWep = greatsword.gameObject.AddComponent<ExecutionerBladeWeapon>();

            // Player üzerine WeaponController ekle ve bağla
            WeaponController wc = player.GetComponent<WeaponController>();
            if (wc == null) wc = player.AddComponent<WeaponController>();

            SerializedObject soWc = new SerializedObject(wc);
            soWc.FindProperty("rangedWeaponVisual").objectReferenceValue = arbalest.gameObject;
            soWc.FindProperty("meleeWeaponVisual").objectReferenceValue = greatsword.gameObject;
            Transform firePoint = arbalest.Find("FirePoint");
            if (firePoint != null)
            {
                soWc.FindProperty("firePoint").objectReferenceValue = firePoint;
            }
            soWc.ApplyModifiedProperties();

            // 11. Sol El Taktiksel Savunma Aracı (Spiked Buckler / Parry Kalkanı)
            PlayerParryController parry = player.GetComponent<PlayerParryController>();
            if (parry == null) parry = player.AddComponent<PlayerParryController>();

            // 12. Combat Juice Sistemleri (CombatAudio, CombatVFXManager, DamageNumberSystem, HitstopManager)
            GameObject combatSystems = GameObject.Find("CombatSystems");
            if (combatSystems == null)
            {
                combatSystems = new GameObject("CombatSystems");
                Undo.RegisterCreatedObjectUndo(combatSystems, "Create Combat Systems");
            }
            if (combatSystems.GetComponent<CombatAudio>() == null) combatSystems.AddComponent<CombatAudio>();
            if (combatSystems.GetComponent<CombatVFXManager>() == null) combatSystems.AddComponent<CombatVFXManager>();
            if (combatSystems.GetComponent<DamageNumberSystem>() == null) combatSystems.AddComponent<DamageNumberSystem>();
            if (combatSystems.GetComponent<HitstopManager>() == null) combatSystems.AddComponent<HitstopManager>();

            // Sahneyi kirli olarak işaretle, diske kaydet ve Player'ı seç
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = player;

            Debug.Log("<color=green><b>[RetroFPS QuickSetup]</b></color> Tok Combat Hissiyatı (Juice) ve Savaş Sistemleri Başarıyla Kuruldu!");
            EditorUtility.DisplayDialog(
                "RetroFPS Combat Juice & Savaş Sistemleri Kuruldu!",
                "1. Kinetik Hasar Sayıları: Vurulan hedeflerin üzerinde sıçrayan, yerçekimiyle süzülen 3D hasar sayıları (Normal, CRIT!, PARRY)!\n\n" +
                "2. Kan & Parçalanma VFX: Et darbelerinde yönlü kan spreyleri, kritik vuruşta altın kıvılcımlar ve düşman ölümünde çevreye fırlayan low-poly et parçaları (Gibs)!\n\n" +
                "3. Katmanlı Tok Sesler: Arbalest teli, kılıç savuruşu, et delinmesi, kemik kırma ve tok hitmarker tık sesleri!\n\n" +
                "4. Nişangah Kinetiği: Ateş edildiğinde geri tepmeyle açılıp yaylanan (Bloom) ve vuruş anında sıçrayan nişangah!\n\n" +
                "5. Squash & Stretch: Darbe alan düşmanların organik fiziksel ezilip yaylanma tepkisi!\n\n" +
                "Şimdi Play (▶) butonuna basarak savaşa başlayın!",
                "Harika, Savaşa Başla!"
            );
        }

        private static GameObject CreateCrossbowModelProxy(Transform parent)
        {
            GameObject root = new GameObject("Weapon_Crossbow");
            root.transform.SetParent(parent);
            root.transform.localPosition = new Vector3(0.22f, -0.22f, 0.42f);
            root.transform.localRotation = Quaternion.identity;

            ProceduralViewmodel vm = root.AddComponent<ProceduralViewmodel>();
            vm.SetRestPose(new Vector3(0.22f, -0.22f, 0.42f), Vector3.zero);

            Material darkWood = CreateColoredMaterial("Wep_DarkWood", new Color(0.18f, 0.12f, 0.08f));
            Material darkMetal = CreateColoredMaterial("Wep_DarkMetal", new Color(0.12f, 0.13f, 0.15f));
            Material glowCyan = CreateColoredMaterial("Wep_GlowCyan", new Color(0.0f, 0.9f, 1.0f));

            // Gövde (Stock)
            GameObject stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stock.name = "Stock";
            stock.transform.SetParent(root.transform);
            stock.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            stock.transform.localScale = new Vector3(0.06f, 0.07f, 0.42f);
            stock.GetComponent<Renderer>().material = darkWood;
            Object.DestroyImmediate(stock.GetComponent<Collider>());

            // Yay Kolları (Bow Wings)
            GameObject wings = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wings.name = "BowWings";
            wings.transform.SetParent(root.transform);
            wings.transform.localPosition = new Vector3(0f, 0.02f, 0.22f);
            wings.transform.localScale = new Vector3(0.34f, 0.035f, 0.05f);
            wings.GetComponent<Renderer>().material = darkMetal;
            Object.DestroyImmediate(wings.GetComponent<Collider>());

            // Enerji Çekirdeği (Glow Rail)
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "GlowRail";
            rail.transform.SetParent(root.transform);
            rail.transform.localPosition = new Vector3(0f, 0.045f, 0.10f);
            rail.transform.localScale = new Vector3(0.02f, 0.015f, 0.28f);
            rail.GetComponent<Renderer>().material = glowCyan;
            Object.DestroyImmediate(rail.GetComponent<Collider>());

            // Namlu Ateş Noktası (FirePoint)
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(root.transform);
            fp.transform.localPosition = new Vector3(0f, 0.03f, 0.28f);

            return root;
        }

        private static GameObject CreateGreatswordModelProxy(Transform parent)
        {
            GameObject root = new GameObject("Weapon_Greatsword");
            root.transform.SetParent(parent);
            root.transform.localPosition = new Vector3(0.24f, -0.24f, 0.44f);
            root.transform.localRotation = Quaternion.Euler(-15f, -12f, 20f);

            ProceduralViewmodel vm = root.AddComponent<ProceduralViewmodel>();
            vm.SetRestPose(new Vector3(0.24f, -0.24f, 0.44f), new Vector3(-15f, -12f, 20f));

            Material darkSteel = CreateColoredMaterial("Wep_DarkSteel", new Color(0.22f, 0.24f, 0.26f));
            Material edgeRed = CreateColoredMaterial("Wep_EdgeRed", new Color(0.9f, 0.15f, 0.1f));
            Material hiltMat = CreateColoredMaterial("Wep_HiltGold", new Color(0.75f, 0.55f, 0.15f));

            // Kabza (Hilt)
            GameObject hilt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hilt.name = "Hilt";
            hilt.transform.SetParent(root.transform);
            hilt.transform.localPosition = new Vector3(0f, 0f, -0.10f);
            hilt.transform.localScale = new Vector3(0.032f, 0.12f, 0.032f);
            hilt.GetComponent<Renderer>().material = hiltMat;
            Object.DestroyImmediate(hilt.GetComponent<Collider>());

            // Siperlik (Crossguard)
            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "Guard";
            guard.transform.SetParent(root.transform);
            guard.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            guard.transform.localScale = new Vector3(0.26f, 0.035f, 0.05f);
            guard.GetComponent<Renderer>().material = hiltMat;
            Object.DestroyImmediate(guard.GetComponent<Collider>());

            // Bıçak Gövdesi (Blade Body)
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(root.transform);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.42f);
            blade.transform.localScale = new Vector3(0.075f, 0.015f, 0.78f);
            blade.GetComponent<Renderer>().material = darkSteel;
            Object.DestroyImmediate(blade.GetComponent<Collider>());

            // Kan Oluğu / Kızıl Enerji Hattı
            GameObject fuller = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fuller.name = "FullerRed";
            fuller.transform.SetParent(root.transform);
            fuller.transform.localPosition = new Vector3(0f, 0.009f, 0.38f);
            fuller.transform.localScale = new Vector3(0.02f, 0.005f, 0.65f);
            fuller.GetComponent<Renderer>().material = edgeRed;
            Object.DestroyImmediate(fuller.GetComponent<Collider>());

            root.SetActive(false); // Başlangıçta 1. silah aktif
            return root;
        }

        private static Material CreateColoredMaterial(string matName, Color col)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.name = matName;
            mat.color = col;
            return mat;
        }

        private static void ResetGameViewScale()
        {
            try
            {
                var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null) return;
                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView != null)
                {
                    // 1. Çözünürlüğü Free Aspect (Index 0) yaparak tüm pencereyi doldurmasını sağla
                    var sizeIndexProp = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (sizeIndexProp != null && sizeIndexProp.CanWrite)
                    {
                        sizeIndexProp.SetValue(gameView, 0, null); // 0 = Free Aspect (Kenarlarda boşluk kalmaz)
                    }

                    // 2. Scale değerini 1.0f yap
                    var scaleProp = gameViewType.GetProperty("scale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (scaleProp != null && scaleProp.CanWrite)
                    {
                        scaleProp.SetValue(gameView, 1.0f, null);
                    }
                    gameView.Repaint();
                }
            }
            catch {}
        }

        [MenuItem("RetroFPS/Setup Wave Director & Arena Spawn Points", false, 2)]
        public static void SetupWaveDirectorAndArenaSpawnPoints()
        {
            // 1. SpawnPoints ebeveyn objesini bul veya oluştur
            GameObject spawnPointsRoot = GameObject.Find("Arena_SpawnPoints");
            if (spawnPointsRoot == null)
            {
                spawnPointsRoot = new GameObject("Arena_SpawnPoints");
                Undo.RegisterCreatedObjectUndo(spawnPointsRoot, "Create SpawnPoints Root");

                // 8 Adet Stratejik Doğuş Noktası Yerleştir (Kuzey, Güney, Doğu, Batı, Sütun Köşeleri, Yüksek Platform)
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_North", new Vector3(0f, 0.1f, 12f), Quaternion.Euler(0, 180, 0), true, true, "MainHall");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_South", new Vector3(0f, 0.1f, -12f), Quaternion.identity, true, true, "MainHall");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_East", new Vector3(12f, 0.1f, 0f), Quaternion.Euler(0, -90, 0), true, true, "SideFlank");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_West", new Vector3(-12f, 0.1f, 0f), Quaternion.Euler(0, 90, 0), true, true, "SideFlank");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_Pillar_NE", new Vector3(8f, 0.1f, 8f), Quaternion.Euler(0, -135, 0), true, false, "Pillars");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_Pillar_NW", new Vector3(-8f, 0.1f, 8f), Quaternion.Euler(0, 135, 0), true, false, "Pillars");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_Air_High", new Vector3(0f, 2.8f, 6f), Quaternion.Euler(0, 180, 0), false, true, "Balcony");
                CreateSpawnPointNode(spawnPointsRoot.transform, "SpawnPoint_Air_Flank", new Vector3(-7f, 2.8f, -6f), Quaternion.Euler(0, 45, 0), false, true, "Balcony");
            }

            // 2. WaveSpawnDirector Objesini Bul veya Oluştur
            WaveSpawnDirector director = Object.FindAnyObjectByType<WaveSpawnDirector>();
            if (director == null)
            {
                GameObject directorGO = new GameObject("WaveSpawnDirector");
                director = directorGO.AddComponent<WaveSpawnDirector>();
                Undo.RegisterCreatedObjectUndo(directorGO, "Create WaveSpawnDirector");
            }
            director.AutoStartOnPlay = false;

            // 3. Düşman Prefablarını Hazırla ve Örnek Dalgaları Yapılandır
            GetOrCreateEnemyPrefabs(out GameObject ghoulPrefab, out GameObject eyePrefab);
            if (director.TotalWaves == 0 && ghoulPrefab != null)
            {
                WaveDefinition[] sampleWaves = new WaveDefinition[]
                {
                    new WaveDefinition("Wave 1: Flesh Scavengers", 1.5f, 5, new EnemySpawnEntry[]
                    {
                        new EnemySpawnEntry(ghoulPrefab, 5, 1.0f, false)
                    }),
                    new WaveDefinition("Wave 2: Flying Watchers & Ghouls", 2.0f, 7, new EnemySpawnEntry[]
                    {
                        new EnemySpawnEntry(ghoulPrefab, 5, 0.9f, false),
                        new EnemySpawnEntry(eyePrefab, 3, 1.4f, true)
                    }),
                    new WaveDefinition("Wave 3: Slaughter Swarm (Final Wave)", 2.5f, 8, new EnemySpawnEntry[]
                    {
                        new EnemySpawnEntry(ghoulPrefab, 8, 0.75f, false),
                        new EnemySpawnEntry(eyePrefab, 5, 1.1f, true)
                    })
                };
                director.ConfigureWaves(sampleWaves);
            }

            // 4. PlayerDebugHUD Entegrasyonu (Wave Bilgileri Artık Prototype HUD Altındadır)
            PlayerDebugHUD debugHud = Object.FindAnyObjectByType<PlayerDebugHUD>();
            if (debugHud == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) debugHud = player.AddComponent<PlayerDebugHUD>();
            }

            // 5. Arenadaki Eski Statik Düşmanları Temizle (Dalga sistemi devrede)
            GymArenaBuilder arena = Object.FindAnyObjectByType<GymArenaBuilder>();
            if (arena != null)
            {
                arena.RemoveStaticEnemies();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[RetroFPS]</b> WaveSpawnDirector, 8 Spawn Point ve Prototype HUD (Wave Entegre) başarıyla kuruldu!</color>");
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoadRepair()
        {
            // Editör derlendiğinde düşman materyallerini ve prefablardaki referansları otomatik onar
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                RepairAllEnemyMaterialsAndPrefabs(false);
            };
        }

        [MenuItem("RetroFPS/Repair Enemy Materials & Prefabs", false, 3)]
        public static void RepairAllEnemyMaterialsAndPrefabsMenu()
        {
            RepairAllEnemyMaterialsAndPrefabs(true);
        }

        public static void RepairAllEnemyMaterialsAndPrefabs(bool showDialog)
        {
            const string matFolder = "Assets/_Project/Materials/Enemies";
            Material skin = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Skin", new Color(0.14f, 0.15f, 0.16f), 0.15f);
            Material armor = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Armor", new Color(0.18f, 0.14f, 0.14f), 0.25f, 0.5f);
            Material claw = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Claw", new Color(0.40f, 0.10f, 0.10f), 0.30f);
            Material eyeMat = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Eye", new Color(1.0f, 0.20f, 0.05f), 0f, 0f, true);

            Material sclera = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Sclera", new Color(0.70f, 0.66f, 0.52f), 0.20f);
            Material iris = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Iris", new Color(0.48f, 0.06f, 0.60f), 0f, 0f, true);
            Material pupil = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Pupil", new Color(0.03f, 0.03f, 0.04f), 0.05f);
            Material horn = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Horn", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material tendril = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Tendril", new Color(0.24f, 0.08f, 0.12f), 0.15f);

            // 1. Prefabları eksiksiz 3D anatomi (kollar, bacaklar, pençeler, boynuzlar vb.) ile kalıcı asset olarak üret/güncelle
            GetOrCreateEnemyPrefabs(out GameObject ghoulPrefab, out GameObject eyePrefab);

            // 2. Sahnede bulunan tüm düşmanların eksik kollarını/bacaklarını tamamla ve materyallerini kalıcı assetlere bağla
            BasicEnemyAI[] ghouls = Object.FindObjectsByType<BasicEnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ghoul in ghouls)
            {
                EnsureGhoulLimbsExist(ghoul.transform, skin, armor, claw, eyeMat);

                Renderer[] rends = ghoul.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    string n = r.gameObject.name;
                    if (n.Contains("Eye")) r.sharedMaterial = eyeMat;
                    else if (n.Contains("Armor") || n.Contains("Chest") || n.Contains("Shoulder")) r.sharedMaterial = armor;
                    else if (n.Contains("Claw") || n.Contains("Spike")) r.sharedMaterial = claw;
                    else r.sharedMaterial = skin;
                    EditorUtility.SetDirty(r);
                }
                EditorUtility.SetDirty(ghoul.gameObject);
            }

            FlyingEyeAI[] eyes = Object.FindObjectsByType<FlyingEyeAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var eye in eyes)
            {
                EnsureFlyingEyeFeaturesExist(eye.transform, sclera, iris, pupil, horn, tendril);

                Renderer[] rends = eye.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    string n = r.gameObject.name;
                    if (n.Contains("Pupil") || n.Contains("Head")) r.sharedMaterial = pupil;
                    else if (n.Contains("Iris")) r.sharedMaterial = iris;
                    else if (n.Contains("Horn")) r.sharedMaterial = horn;
                    else if (n.Contains("Tendril")) r.sharedMaterial = tendril;
                    else r.sharedMaterial = sclera;
                    EditorUtility.SetDirty(r);
                }
                EditorUtility.SetDirty(eye.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[RetroFPS]</b> Düşman prefabları ve sahnedeki tüm düşmanlar eksiksiz 3D uzuvlar (kollar, bacaklar, pençeler) ve kalıcı URP materyalleriyle donatıldı!</color>");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Düşman Uzuvları ve Materyalleri Onarıldı!",
                    "Prefablar ve sahnedeki tüm düşmanların kollar, bacaklar, pençeler, omuzlar ve kafaları eksiksiz oluşturuldu ve Assets/_Project/Materials/Enemies altındaki kalıcı materyallere bağlandı.\n\nArtık düşmanlar eksiksiz ve doğal görünecektir!",
                    "Harika!"
                );
            }
        }

        private static Material CreateOrLoadPersistentMaterial(string folder, string matName, Color color, float smoothness = 0.15f, float metallic = 0f, bool unlit = false)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Enemies");
            }

            string path = $"{folder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = unlit 
                    ? Shader.Find("Universal Render Pipeline/Unlit") 
                    : Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader) { name = matName };
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }

        private static void GetOrCreateEnemyPrefabs(out GameObject ghoulPrefab, out GameObject eyePrefab)
        {
            const string folder = "Assets/_Project/Prefabs/Enemies";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Enemies");
            }

            const string matFolder = "Assets/_Project/Materials/Enemies";
            Material skin = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Skin", new Color(0.14f, 0.15f, 0.16f), 0.15f);
            Material armor = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Armor", new Color(0.18f, 0.14f, 0.14f), 0.25f, 0.5f);
            Material claw = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Claw", new Color(0.40f, 0.10f, 0.10f), 0.30f);
            Material eye = CreateOrLoadPersistentMaterial(matFolder, "Mat_Ghoul_Eye", new Color(1.0f, 0.20f, 0.05f), 0f, 0f, true);

            Material sclera = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Sclera", new Color(0.70f, 0.66f, 0.52f), 0.20f);
            Material iris = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Iris", new Color(0.48f, 0.06f, 0.60f), 0f, 0f, true);
            Material pupil = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Pupil", new Color(0.03f, 0.03f, 0.04f), 0.05f);
            Material horn = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Horn", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material tendril = CreateOrLoadPersistentMaterial(matFolder, "Mat_Eye_Tendril", new Color(0.24f, 0.08f, 0.12f), 0.15f);

            string ghoulPath = $"{folder}/Enemy_Ghoul_Stalker.prefab";
            GameObject tempGhoul = CreateGhoulTempObject(skin, armor, claw, eye);
            ghoulPrefab = PrefabUtility.SaveAsPrefabAsset(tempGhoul, ghoulPath);
            Object.DestroyImmediate(tempGhoul);

            string eyePath = $"{folder}/Enemy_FlyingEye_Watcher.prefab";
            GameObject tempEye = CreateFlyingEyeTempObject(sclera, iris, pupil, horn, tendril);
            eyePrefab = PrefabUtility.SaveAsPrefabAsset(tempEye, eyePath);
            Object.DestroyImmediate(tempEye);

            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateGhoulTempObject(Material skin, Material armor, Material claw, Material eye)
        {
            GameObject root = new GameObject("Enemy_Ghoul_Stalker");
            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 2.05f;
            cc.radius = 0.50f;
            cc.center = new Vector3(0f, 1.025f, 0f);
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.08f;

            // Pelvis
            GameObject pelvis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pelvis.name = "Body_Pelvis";
            pelvis.transform.SetParent(root.transform);
            pelvis.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pelvis.transform.localScale = new Vector3(0.50f, 0.45f, 0.35f);
            pelvis.GetComponent<Renderer>().sharedMaterial = skin;
            Object.DestroyImmediate(pelvis.GetComponent<Collider>());

            // Göğüs
            GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Body_Chest";
            chest.transform.SetParent(root.transform);
            chest.transform.localPosition = new Vector3(0f, 1.28f, 0.08f);
            chest.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            chest.transform.localScale = new Vector3(0.72f, 0.65f, 0.48f);
            chest.GetComponent<Renderer>().sharedMaterial = armor;

            // Sırt Dikenleri
            GameObject spineSpike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spineSpike.name = "Spine_Spike";
            spineSpike.transform.SetParent(root.transform);
            spineSpike.transform.localPosition = new Vector3(0f, 1.38f, -0.22f);
            spineSpike.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            spineSpike.transform.localScale = new Vector3(0.10f, 0.45f, 0.12f);
            spineSpike.GetComponent<Renderer>().sharedMaterial = claw;
            Object.DestroyImmediate(spineSpike.GetComponent<Collider>());

            // Kafa (Headshot)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0.22f);
            head.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.36f, 0.40f);
            head.GetComponent<Renderer>().sharedMaterial = skin;

            // Gözler
            CreateEye(head.transform, new Vector3(-0.10f, 0.05f, 0.21f), eye, "Eye_Left");
            CreateEye(head.transform, new Vector3(0.10f, 0.05f, 0.21f), eye, "Eye_Right");

            // Omuzlar
            CreateShoulder(root.transform, new Vector3(-0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, 22f), armor, "Shoulder_L");
            CreateShoulder(root.transform, new Vector3(0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, -22f), armor, "Shoulder_R");

            // Kollar ve Pençeler (UpperArm + ClawBlade)
            CreateArm(root.transform, new Vector3(-0.48f, 1.15f, 0.12f), -1, skin, claw, "Arm_L");
            CreateArm(root.transform, new Vector3(0.48f, 1.15f, 0.12f), 1, skin, claw, "Arm_R");

            // Bacaklar
            CreateLeg(root.transform, new Vector3(-0.20f, 0.36f, 0f), skin, "Leg_L");
            CreateLeg(root.transform, new Vector3(0.20f, 0.36f, 0f), skin, "Leg_R");

            root.AddComponent<BasicEnemyAI>();
            return root;
        }

        private static GameObject CreateFlyingEyeTempObject(Material sclera, Material iris, Material pupil, Material horn, Material tendril)
        {
            GameObject root = new GameObject("Enemy_FlyingEye_Watcher");
            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.1f;
            cc.radius = 0.55f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0.2f;
            cc.skinWidth = 0.06f;

            // Sclera (Gövde)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body_Sclera";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            body.GetComponent<Renderer>().sharedMaterial = sclera;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // Iris (Okült Büyü Halkası)
            GameObject irisGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            irisGO.name = "Eye_Iris";
            irisGO.transform.SetParent(root.transform);
            irisGO.transform.localPosition = new Vector3(0f, 0f, 0.38f);
            irisGO.transform.localScale = new Vector3(0.55f, 0.55f, 0.25f);
            irisGO.GetComponent<Renderer>().sharedMaterial = iris;
            Object.DestroyImmediate(irisGO.GetComponent<Collider>());

            // Pupil (Headshot - 2x Crit)
            GameObject pupilGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupilGO.name = "Pupil_Head";
            pupilGO.transform.SetParent(root.transform);
            pupilGO.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            pupilGO.transform.localScale = new Vector3(0.28f, 0.28f, 0.18f);
            pupilGO.GetComponent<Renderer>().sharedMaterial = pupil;

            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(root.transform);
            fp.transform.localPosition = new Vector3(0f, 0f, 0.65f);

            CreateHorn(root.transform, new Vector3(0f, 0.52f, -0.15f), Quaternion.Euler(-25f, 0f, 0f), horn, "Horn_Top");
            CreateHorn(root.transform, new Vector3(-0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, 35f), horn, "Horn_L");
            CreateHorn(root.transform, new Vector3(0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, -35f), horn, "Horn_R");

            CreateTendril(root.transform, new Vector3(0f, -0.65f, 0f), tendril, "Tendril_Center");
            CreateTendril(root.transform, new Vector3(-0.25f, -0.60f, -0.12f), tendril, "Tendril_L");
            CreateTendril(root.transform, new Vector3(0.25f, -0.60f, -0.12f), tendril, "Tendril_R");

            FlyingEyeAI ai = root.AddComponent<FlyingEyeAI>();
            ai.SetFirePoint(fp.transform);
            return root;
        }

        private static void CreateEye(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = name;
            eye.transform.SetParent(parent);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            eye.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        private static void CreateShoulder(Transform parent, Vector3 localPos, Quaternion localRot, Material mat, string name)
        {
            GameObject shoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shoulder.name = name;
            shoulder.transform.SetParent(parent);
            shoulder.transform.localPosition = localPos;
            shoulder.transform.localRotation = localRot;
            shoulder.transform.localScale = new Vector3(0.24f, 0.26f, 0.32f);
            shoulder.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(shoulder.GetComponent<Collider>());
        }

        private static void CreateArm(Transform parent, Vector3 localPos, int side, Material skinMat, Material clawMat, string name)
        {
            GameObject armRoot = new GameObject(name);
            armRoot.transform.SetParent(parent);
            armRoot.transform.localPosition = localPos;

            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upper.name = "UpperArm";
            upper.transform.SetParent(armRoot.transform);
            upper.transform.localPosition = new Vector3(0f, -0.15f, 0.05f);
            upper.transform.localRotation = Quaternion.Euler(22f, 0f, side * 8f);
            upper.transform.localScale = new Vector3(0.14f, 0.38f, 0.14f);
            upper.GetComponent<Renderer>().sharedMaterial = skinMat;
            Object.DestroyImmediate(upper.GetComponent<Collider>());

            GameObject claw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            claw.name = "ClawBlade";
            claw.transform.SetParent(armRoot.transform);
            claw.transform.localPosition = new Vector3(0f, -0.42f, 0.22f);
            claw.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            claw.transform.localScale = new Vector3(0.12f, 0.44f, 0.09f);
            claw.GetComponent<Renderer>().sharedMaterial = clawMat;
            Object.DestroyImmediate(claw.GetComponent<Collider>());
        }

        private static void CreateLeg(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = name;
            leg.transform.SetParent(parent);
            leg.transform.localPosition = localPos;
            leg.transform.localScale = new Vector3(0.16f, 0.36f, 0.16f);
            leg.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(leg.GetComponent<Collider>());
        }

        private static void CreateHorn(Transform parent, Vector3 localPos, Quaternion localRot, Material mat, string name)
        {
            GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horn.name = name;
            horn.transform.SetParent(parent);
            horn.transform.localPosition = localPos;
            horn.transform.localRotation = localRot;
            horn.transform.localScale = new Vector3(0.14f, 0.32f, 0.14f);
            horn.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(horn.GetComponent<Collider>());
        }

        private static void CreateTendril(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject tendril = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tendril.name = name;
            tendril.transform.SetParent(parent);
            tendril.transform.localPosition = localPos;
            tendril.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
            tendril.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(tendril.GetComponent<Collider>());
        }

        private static void EnsureGhoulLimbsExist(Transform ghoul, Material skin, Material armor, Material claw, Material eye)
        {
            Transform head = ghoul.Find("Head");
            if (head != null)
            {
                if (head.Find("Eye_Left") == null) CreateEye(head, new Vector3(-0.10f, 0.05f, 0.21f), eye, "Eye_Left");
                if (head.Find("Eye_Right") == null) CreateEye(head, new Vector3(0.10f, 0.05f, 0.21f), eye, "Eye_Right");
            }

            if (ghoul.Find("Spine_Spike") == null)
            {
                GameObject spineSpike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spineSpike.name = "Spine_Spike";
                spineSpike.transform.SetParent(ghoul);
                spineSpike.transform.localPosition = new Vector3(0f, 1.38f, -0.22f);
                spineSpike.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                spineSpike.transform.localScale = new Vector3(0.10f, 0.45f, 0.12f);
                spineSpike.GetComponent<Renderer>().sharedMaterial = claw;
                Object.DestroyImmediate(spineSpike.GetComponent<Collider>());
            }

            if (ghoul.Find("Shoulder_L") == null) CreateShoulder(ghoul, new Vector3(-0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, 22f), armor, "Shoulder_L");
            if (ghoul.Find("Shoulder_R") == null) CreateShoulder(ghoul, new Vector3(0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, -22f), armor, "Shoulder_R");

            if (ghoul.Find("Arm_L") == null) CreateArm(ghoul, new Vector3(-0.48f, 1.15f, 0.12f), -1, skin, claw, "Arm_L");
            if (ghoul.Find("Arm_R") == null) CreateArm(ghoul, new Vector3(0.48f, 1.15f, 0.12f), 1, skin, claw, "Arm_R");

            if (ghoul.Find("Leg_L") == null) CreateLeg(ghoul, new Vector3(-0.20f, 0.36f, 0f), skin, "Leg_L");
            if (ghoul.Find("Leg_R") == null) CreateLeg(ghoul, new Vector3(0.20f, 0.36f, 0f), skin, "Leg_R");
        }

        private static void EnsureFlyingEyeFeaturesExist(Transform eyeRoot, Material sclera, Material iris, Material pupil, Material horn, Material tendril)
        {
            if (eyeRoot.Find("Eye_Iris") == null)
            {
                GameObject irisGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                irisGO.name = "Eye_Iris";
                irisGO.transform.SetParent(eyeRoot);
                irisGO.transform.localPosition = new Vector3(0f, 0f, 0.38f);
                irisGO.transform.localScale = new Vector3(0.55f, 0.55f, 0.25f);
                irisGO.GetComponent<Renderer>().sharedMaterial = iris;
                Object.DestroyImmediate(irisGO.GetComponent<Collider>());
            }

            if (eyeRoot.Find("Horn_Top") == null) CreateHorn(eyeRoot, new Vector3(0f, 0.52f, -0.15f), Quaternion.Euler(-25f, 0f, 0f), horn, "Horn_Top");
            if (eyeRoot.Find("Horn_L") == null) CreateHorn(eyeRoot, new Vector3(-0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, 35f), horn, "Horn_L");
            if (eyeRoot.Find("Horn_R") == null) CreateHorn(eyeRoot, new Vector3(0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, -35f), horn, "Horn_R");

            if (eyeRoot.Find("Tendril_Center") == null) CreateTendril(eyeRoot, new Vector3(0f, -0.65f, 0f), tendril, "Tendril_Center");
            if (eyeRoot.Find("Tendril_L") == null) CreateTendril(eyeRoot, new Vector3(-0.25f, -0.60f, -0.12f), tendril, "Tendril_L");
            if (eyeRoot.Find("Tendril_R") == null) CreateTendril(eyeRoot, new Vector3(0.25f, -0.60f, -0.12f), tendril, "Tendril_R");

            if (eyeRoot.Find("FirePoint") == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(eyeRoot);
                fp.transform.localPosition = new Vector3(0f, 0f, 0.65f);
                FlyingEyeAI ai = eyeRoot.GetComponent<FlyingEyeAI>();
                if (ai != null) ai.SetFirePoint(fp.transform);
            }
        }

        private static void CreateSpawnPointNode(Transform parent, string name, Vector3 pos, Quaternion rot, bool ground, bool air, string zoneTag = "")
        {
            GameObject spGO = new GameObject(name);
            spGO.transform.SetParent(parent);
            spGO.transform.position = pos;
            spGO.transform.rotation = rot;
            EncounterSpawnPoint sp = spGO.AddComponent<EncounterSpawnPoint>();

            var allowGroundField = typeof(EncounterSpawnPoint).GetField("allowGroundEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allowAirField = typeof(EncounterSpawnPoint).GetField("allowAirEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var zoneTagField = typeof(EncounterSpawnPoint).GetField("zoneTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (allowGroundField != null) allowGroundField.SetValue(sp, ground);
            if (allowAirField != null) allowAirField.SetValue(sp, air);
            if (zoneTagField != null && !string.IsNullOrEmpty(zoneTag)) zoneTagField.SetValue(sp, zoneTag);
        }

        [MenuItem("GameObject/RetroFPS/Encounter Room (Oda: Manuel Düşman + Dalga)", false, 10)]
        [MenuItem("RetroFPS/Create Encounter Room (Oda: Manuel Düşman + Dalga)", false, 4)]
        public static void CreateEncounterAreaGameObject(MenuCommand menuCommand)
        {
            var existingRooms = Object.FindObjectsByType<EncounterArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string roomName = $"Encounter_Room_{existingRooms.Length + 1:D2}";

            GameObject go = new GameObject(roomName);
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(24f, 5f, 24f);
            col.center = new Vector3(0f, 2.5f, 0f);

            WaveSpawnDirector director = go.AddComponent<WaveSpawnDirector>();
            director.AutoStartOnPlay = false;

            EncounterArea area = go.AddComponent<EncounterArea>();
            area.GenerateSpawnPoints();

            Undo.RegisterCreatedObjectUndo(go, $"Create {roomName}");
            Selection.activeObject = go;

            Debug.Log($"<color=green><b>[RetroFPS]</b> '{roomName}' başarıyla oluşturuldu!</color>\n" +
                      $"• BoxCollider ve Raycast zemin/hava doğuş noktaları hazırlandı.\n" +
                      $"• WaveSpawnDirector ve EncounterArea aynı objede otomatik bağlandı.\n" +
                      $"• Bu alana koyacağınız manuel düşmanların %25'i öldüğünde dalgalar otomatik başlar.");
        }
    }
}
