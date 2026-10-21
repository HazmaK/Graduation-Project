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
    }
}
