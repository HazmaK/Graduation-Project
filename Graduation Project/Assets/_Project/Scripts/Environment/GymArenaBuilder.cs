using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Gerçek insan ve gotik mimari metriklerine (Human Architecture Scale) göre ölçeklenmiş Greybox Test Arenası.
    /// - Duvar yüksekliği: 3.5 metre
    /// - Sütunlar: 0.8m çap, 3.2m yükseklik (Devasa depo silosu yerine gerçek sütun)
    /// - Zıplama basamakları: 0.4m, 0.85m, 1.35m, 1.85m (İnsan boyu kademeleri)
    /// - Slide rampası: 2.2m yüksekliğinde, yumuşak kayma açısı
    /// - 1.80m Referans Mankenleri: Ölçek kıyaslaması için spawn yanına dikilmiş insan silüeti
    /// </summary>
    public class GymArenaBuilder : MonoBehaviour
    {
        [Header("Ayarlar")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private Vector2 arenaSize = new Vector2(34f, 34f);

        private void Start()
        {
            // Eğer sahnede zaten arena inşa edilmişse (editörde hazırsa),
            // oyun başladığında zemini YIKIP oyuncuyu aşağı düşürme!
            Transform existing = transform.Find("Gym_Geometry");
            if (existing == null && buildOnStart)
            {
                BuildArena();
            }
        }

        [ContextMenu("Build Arena Now")]
        public void BuildArena()
        {
            Transform existing = transform.Find("Gym_Geometry");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            GameObject root = new GameObject("Gym_Geometry");
            root.transform.SetParent(transform);

            // Renkli Prototip Materyalleri
            Material floorMat = CreateColoredMaterial("Gym_Floor", new Color(0.18f, 0.20f, 0.22f));
            Material wallMat = CreateColoredMaterial("Gym_Wall", new Color(0.11f, 0.12f, 0.14f));
            Material rampMat = CreateColoredMaterial("Gym_Ramp", new Color(0.85f, 0.42f, 0.10f));
            Material pillarMat = CreateColoredMaterial("Gym_Pillar", new Color(0.25f, 0.55f, 0.75f));
            Material platformMat = CreateColoredMaterial("Gym_Platform", new Color(0.35f, 0.65f, 0.30f));
            Material dummyMat = CreateColoredMaterial("Gym_HumanDummy", new Color(0.95f, 0.75f, 0.15f));

            // 1. Zemin (Y = 0 üst yüzey)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(arenaSize.x, 0.5f, arenaSize.y);
            floor.GetComponent<Renderer>().material = floorMat;

            // 2. Çevre Duvarları (3.5m yükseklik - gerçek oda/avlu tavan seviyesi)
            float wallH = 3.5f;
            float halfWallH = wallH * 0.5f;
            CreateWall(root.transform, new Vector3(0, halfWallH, arenaSize.y * 0.5f), new Vector3(arenaSize.x, wallH, 0.6f), wallMat);
            CreateWall(root.transform, new Vector3(0, halfWallH, -arenaSize.y * 0.5f), new Vector3(arenaSize.x, wallH, 0.6f), wallMat);
            CreateWall(root.transform, new Vector3(arenaSize.x * 0.5f, halfWallH, 0), new Vector3(0.6f, wallH, arenaSize.y), wallMat);
            CreateWall(root.transform, new Vector3(-arenaSize.x * 0.5f, halfWallH, 0), new Vector3(0.6f, wallH, arenaSize.y), wallMat);

            // 3. Slide Test Rampası (2.2m yüksekliğinde platform ve iniş rampası)
            float rampHeight = 2.2f;
            GameObject rampTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampTop.name = "Ramp_Top_Platform";
            rampTop.transform.SetParent(root.transform);
            rampTop.transform.position = new Vector3(-9f, rampHeight - 0.25f, -2f);
            rampTop.transform.localScale = new Vector3(4.5f, 0.5f, 4f);
            rampTop.GetComponent<Renderer>().material = rampMat;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Slide_Ramp";
            ramp.transform.SetParent(root.transform);
            ramp.transform.position = new Vector3(-9f, 0.95f, 3.8f);
            ramp.transform.rotation = Quaternion.Euler(18f, 0f, 0f); // 18 derece kayma açısı
            ramp.transform.localScale = new Vector3(4.5f, 0.4f, 8.2f);
            ramp.GetComponent<Renderer>().material = rampMat;

            // 4. İnsan Ölçeğinde Zıplama Basamakları (Parkur)
            CreatePlatform(root.transform, new Vector3(9f, 0.20f, -8f), new Vector3(2.5f, 0.40f, 2.5f), platformMat, "Step_0.4m_Shin");
            CreatePlatform(root.transform, new Vector3(9f, 0.425f, -4f), new Vector3(2.5f, 0.85f, 2.5f), platformMat, "Step_0.85m_Waist");
            CreatePlatform(root.transform, new Vector3(9f, 0.675f, 0f), new Vector3(2.5f, 1.35f, 2.5f), platformMat, "Step_1.35m_Chest");
            CreatePlatform(root.transform, new Vector3(9f, 0.925f, 4f), new Vector3(2.5f, 1.85f, 2.5f), platformMat, "Step_1.85m_Head");

            // 5. Gotik Sütunlar (0.8m çap, 3.2m boy - insan ölçeğine uygun slalom)
            for (int i = 0; i < 4; i++)
            {
                float z = -8f + i * 5.5f;
                float x = (i % 2 == 0) ? -1.8f : 1.8f;
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Slalom_Pillar_{i}";
                pillar.transform.SetParent(root.transform);
                pillar.transform.position = new Vector3(x, 1.6f, z);
                pillar.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f); // 1.6 * 2m = 3.2m yükseklik
                pillar.GetComponent<Renderer>().material = pillarMat;
            }

            // 6. 1.80m Boyunda İnsan Referans Mankeni (Pasif DPS / Boyut Kıyaslaması)
            CreateHumanReferenceDummy(root.transform, new Vector3(-3.5f, 0f, -8.5f), dummyMat, "Combat_Dummy_Target");

            // 7. Agresif Gotik Düşmanlar (Ghoul Stalker - 2.05m Boomer Shooter Düşmanları)
            Material ghoulSkin = CreateColoredMaterial("Ghoul_Skin", new Color(0.14f, 0.15f, 0.17f));
            Material ghoulArmor = CreateColoredMaterial("Ghoul_Armor", new Color(0.24f, 0.10f, 0.10f));
            Material ghoulClaw = CreateColoredMaterial("Ghoul_Claw", new Color(0.80f, 0.14f, 0.10f));
            Material ghoulEye = CreateColoredMaterial("Ghoul_Eye", new Color(1.0f, 0.05f, 0.02f));

            // 1. Merkez Düşman ve 2. Yakın Müttefiki (Sürü Mesafesi: ~4.0m - Biri saldırınca ikisi birden saldırır)
            CreateGhoulEnemy(root.transform, new Vector3(0f, 0f, 1.0f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_Center");
            CreateGhoulEnemy(root.transform, new Vector3(4.0f, 0f, 0.5f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_Packmate");

            // 3. Uzak Nöbetçi (Mesafe: ~13.5m - Yakındakiler saldırınca ALARMA GEÇMEZ, sakin devriyesine devam eder)
            CreateGhoulEnemy(root.transform, new Vector3(-10.5f, 0f, 9.5f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_DistantGuard");

            // 4. Havada Uçan Göz (Occult Watcher / Beholder - 3.2m Yükseklikte Menzilli Büyü Düşmanı)
            Material eyeSclera = CreateColoredMaterial("Eye_Sclera", new Color(0.86f, 0.83f, 0.80f));
            Material eyeIris = CreateColoredMaterial("Eye_Iris", new Color(0.55f, 0.08f, 0.65f));
            Material eyePupil = CreateColoredMaterial("Eye_Pupil", new Color(0.06f, 0.04f, 0.08f));
            Material eyeHorn = CreateColoredMaterial("Eye_Horn", new Color(0.12f, 0.12f, 0.14f));
            Material eyeTendril = CreateColoredMaterial("Eye_Tendril", new Color(0.35f, 0.12f, 0.20f));

            CreateFlyingEyeEnemy(root.transform, new Vector3(2.0f, 2.7f, 4.0f), eyeSclera, eyeIris, eyePupil, eyeHorn, eyeTendril, "FlyingEye_Watcher");

            Debug.Log("[GymArenaBuilder] İnsan ölçeğindeki Test Arenası, Ghoul Düşmanları ve Uçan Göz başarıyla kuruldu!");
        }

        private void CreateHumanReferenceDummy(Transform parent, Vector3 pos, Material mat, string name)
        {
            GameObject dummy = new GameObject(name);
            dummy.transform.SetParent(parent);
            dummy.transform.position = pos;

            // Gövde (1.4m yükseklik)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(dummy.transform);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.7f, 0.3f); // 0.7 * 2 = 1.4m boy
            body.GetComponent<Renderer>().material = mat;

            // Kafa (0.35m çap, merkez Y = 1.62m, kafa tepe Y = 1.80m)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(dummy.transform);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            head.GetComponent<Renderer>().material = mat;

            // Combat Dummy bileşeni ekle (Hasar alma, wobble darbe tepkisi, headshot ve flash)
            dummy.AddComponent<CombatDummy>();
        }

        private void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
        }

        private void CreatePlatform(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
        {
            GameObject plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plat.name = name;
            plat.transform.SetParent(parent);
            plat.transform.position = pos;
            plat.transform.localScale = scale;
            plat.GetComponent<Renderer>().material = mat;
        }

        private void CreateGhoulEnemy(Transform parent, Vector3 pos, Material skinMat, Material armorMat, Material clawMat, Material eyeMat, string name)
        {
            GameObject enemyRoot = new GameObject(name);
            enemyRoot.transform.SetParent(parent);
            enemyRoot.transform.position = pos;

            // 1. CharacterController (2.05m heybetli gotik düşman ölçeği)
            CharacterController cc = enemyRoot.AddComponent<CharacterController>();
            cc.height = 2.05f;
            cc.radius = 0.50f;
            cc.center = new Vector3(0f, 1.025f, 0f);
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.08f;

            // 2. Alt Gövde / Bel (Pelvis)
            GameObject pelvis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pelvis.name = "Body_Pelvis";
            pelvis.transform.SetParent(enemyRoot.transform);
            pelvis.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pelvis.transform.localScale = new Vector3(0.50f, 0.45f, 0.35f);
            pelvis.GetComponent<Renderer>().material = skinMat;
            DestroyColliderIfAny(pelvis);

            // 3. Üst Gövde / Göğüs Kafesi (Öne doğru kambur gotik silüet - Vücut Hitbox'ı)
            GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Body_Chest";
            chest.transform.SetParent(enemyRoot.transform);
            chest.transform.localPosition = new Vector3(0f, 1.28f, 0.08f);
            chest.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            chest.transform.localScale = new Vector3(0.72f, 0.65f, 0.48f);
            chest.GetComponent<Renderer>().material = armorMat;

            // 4. Sırt Dikenleri (Spine Spikes)
            GameObject spineSpike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spineSpike.name = "Spine_Spike";
            spineSpike.transform.SetParent(enemyRoot.transform);
            spineSpike.transform.localPosition = new Vector3(0f, 1.38f, -0.22f);
            spineSpike.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            spineSpike.transform.localScale = new Vector3(0.10f, 0.45f, 0.12f);
            spineSpike.GetComponent<Renderer>().material = clawMat;
            DestroyColliderIfAny(spineSpike);

            // 5. Kafa (Öne uzanmış avcı kafatası - Headshot: 2x Kritik Hasar)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(enemyRoot.transform);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0.22f);
            head.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.36f, 0.40f);
            head.GetComponent<Renderer>().material = skinMat;

            // 6. Parlayan Kızıl Gözler (Unlit Crimson Eyes)
            CreateEye(head.transform, new Vector3(-0.10f, 0.05f, 0.21f), eyeMat, "Eye_Left");
            CreateEye(head.transform, new Vector3(0.10f, 0.05f, 0.21f), eyeMat, "Eye_Right");

            // 7. Omuz Zırhları (Spiked Pauldrons)
            CreateShoulder(enemyRoot.transform, new Vector3(-0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, 22f), armorMat, "Shoulder_L");
            CreateShoulder(enemyRoot.transform, new Vector3(0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, -22f), armorMat, "Shoulder_R");

            // 8. Pençe Kolları (Blade Claws)
            CreateArm(enemyRoot.transform, new Vector3(-0.48f, 1.15f, 0.12f), -1, skinMat, clawMat, "Arm_L");
            CreateArm(enemyRoot.transform, new Vector3(0.48f, 1.15f, 0.12f), 1, skinMat, clawMat, "Arm_R");

            // 9. Bacaklar (Stance)
            CreateLeg(enemyRoot.transform, new Vector3(-0.20f, 0.36f, 0f), skinMat, "Leg_L");
            CreateLeg(enemyRoot.transform, new Vector3(0.20f, 0.36f, 0f), skinMat, "Leg_R");

            // 10. BasicEnemyAI Yapay Zekasını Ekle
            enemyRoot.AddComponent<BasicEnemyAI>();
        }

        private void CreateEye(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = name;
            eye.transform.SetParent(parent);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            eye.GetComponent<Renderer>().material = mat;
            DestroyColliderIfAny(eye);
        }

        private void CreateShoulder(Transform parent, Vector3 localPos, Quaternion localRot, Material mat, string name)
        {
            GameObject shoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shoulder.name = name;
            shoulder.transform.SetParent(parent);
            shoulder.transform.localPosition = localPos;
            shoulder.transform.localRotation = localRot;
            shoulder.transform.localScale = new Vector3(0.24f, 0.26f, 0.32f);
            shoulder.GetComponent<Renderer>().material = mat;
            DestroyColliderIfAny(shoulder);
        }

        private void CreateArm(Transform parent, Vector3 localPos, int side, Material skinMat, Material clawMat, string name)
        {
            GameObject armRoot = new GameObject(name);
            armRoot.transform.SetParent(parent);
            armRoot.transform.localPosition = localPos;

            // Üst Kol
            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upper.name = "UpperArm";
            upper.transform.SetParent(armRoot.transform);
            upper.transform.localPosition = new Vector3(0f, -0.15f, 0.05f);
            upper.transform.localRotation = Quaternion.Euler(22f, 0f, side * 8f);
            upper.transform.localScale = new Vector3(0.14f, 0.38f, 0.14f);
            upper.GetComponent<Renderer>().material = skinMat;
            DestroyColliderIfAny(upper);

            // Ön Kol & Pençe Bıçağı
            GameObject claw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            claw.name = "ClawBlade";
            claw.transform.SetParent(armRoot.transform);
            claw.transform.localPosition = new Vector3(0f, -0.42f, 0.22f);
            claw.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            claw.transform.localScale = new Vector3(0.12f, 0.44f, 0.09f);
            claw.GetComponent<Renderer>().material = clawMat;
            DestroyColliderIfAny(claw);
        }

        private void CreateLeg(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = name;
            leg.transform.SetParent(parent);
            leg.transform.localPosition = localPos;
            leg.transform.localScale = new Vector3(0.16f, 0.36f, 0.16f);
            leg.GetComponent<Renderer>().material = mat;
            DestroyColliderIfAny(leg);
        }

        private void DestroyColliderIfAny(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        private Material CreateColoredMaterial(string matName, Color col)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.name = matName;
            mat.color = col;
            return mat;
        }

        private void CreateFlyingEyeEnemy(Transform parent, Vector3 pos, Material scleraMat, Material irisMat, Material pupilMat, Material hornMat, Material tendrilMat, string name)
        {
            GameObject eyeRoot = new GameObject(name);
            eyeRoot.transform.SetParent(parent);
            eyeRoot.transform.position = pos;

            // 1. CharacterController (Havada hareket ve duvar engelleri için)
            CharacterController cc = eyeRoot.AddComponent<CharacterController>();
            cc.height = 1.1f;
            cc.radius = 0.55f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0.2f;
            cc.skinWidth = 0.06f;

            // 2. Göz Küresi (Sclera - Ana Gövde)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body_Sclera";
            body.transform.SetParent(eyeRoot.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            body.GetComponent<Renderer>().material = scleraMat;
            DestroyColliderIfAny(body);

            // 3. İris (Büyülü Mor Halka)
            GameObject iris = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            iris.name = "Eye_Iris";
            iris.transform.SetParent(eyeRoot.transform);
            iris.transform.localPosition = new Vector3(0f, 0f, 0.38f);
            iris.transform.localScale = new Vector3(0.55f, 0.55f, 0.25f);
            iris.GetComponent<Renderer>().material = irisMat;
            DestroyColliderIfAny(iris);

            // 4. Gözbebeği / Kritik Vuruş Noktası (Headshot - 2x Crit!)
            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil_Head";
            pupil.transform.SetParent(eyeRoot.transform);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            pupil.transform.localScale = new Vector3(0.28f, 0.28f, 0.18f);
            pupil.GetComponent<Renderer>().material = pupilMat;

            // 5. Ateş Noktası (Mermilerin çıkış noktası)
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(eyeRoot.transform);
            fp.transform.localPosition = new Vector3(0f, 0f, 0.65f);

            // 6. Gotik Boynuzlar / Taç Dikenleri (Crown Spikes)
            CreateHorn(eyeRoot.transform, new Vector3(0f, 0.52f, -0.15f), Quaternion.Euler(-25f, 0f, 0f), hornMat, "Horn_Top");
            CreateHorn(eyeRoot.transform, new Vector3(-0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, 35f), hornMat, "Horn_L");
            CreateHorn(eyeRoot.transform, new Vector3(0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, -35f), hornMat, "Horn_R");

            // 7. Sarkan Okült Dokunaçlar / Sinir Kökleri (Tendrils)
            CreateTendril(eyeRoot.transform, new Vector3(0f, -0.65f, 0f), tendrilMat, "Tendril_Center");
            CreateTendril(eyeRoot.transform, new Vector3(-0.25f, -0.60f, -0.12f), tendrilMat, "Tendril_L");
            CreateTendril(eyeRoot.transform, new Vector3(0.25f, -0.60f, -0.12f), tendrilMat, "Tendril_R");

            // 8. FlyingEyeAI Bileşeni
            FlyingEyeAI ai = eyeRoot.AddComponent<FlyingEyeAI>();
            ai.SetFirePoint(fp.transform);
        }

        private void CreateHorn(Transform parent, Vector3 localPos, Quaternion localRot, Material mat, string name)
        {
            GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horn.name = name;
            horn.transform.SetParent(parent);
            horn.transform.localPosition = localPos;
            horn.transform.localRotation = localRot;
            horn.transform.localScale = new Vector3(0.14f, 0.32f, 0.14f);
            horn.GetComponent<Renderer>().material = mat;
            DestroyColliderIfAny(horn);
        }

        private void CreateTendril(Transform parent, Vector3 localPos, Material mat, string name)
        {
            GameObject tendril = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tendril.name = name;
            tendril.transform.SetParent(parent);
            tendril.transform.localPosition = localPos;
            tendril.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
            tendril.GetComponent<Renderer>().material = mat;
            DestroyColliderIfAny(tendril);
        }
    }
}
