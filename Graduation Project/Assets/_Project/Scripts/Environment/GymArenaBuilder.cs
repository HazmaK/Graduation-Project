using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Gerçek insan ve gotik mimari metriklerine (Human Architecture Scale) göre ölçeklenmiş 90'lar Dark Fantasy Test Arenası.
    /// - Çizgi film (cartoony) parlak renkler yerine prosedürel 64x64 piksel taş, yosunlu granit, paslı demir ve basalt dokuları.
    /// - Abyssal eksponansiyel sis (fog), soluk ay ışığı ambiyansı ve dinamik gölge üreten meşaleler (GothicTorchFlicker).
    /// - Sahnede zaten mevcut geometri varsa yok etmeden Dark Fantasy materyallerini ve meşalelerini giydirir.
    /// </summary>
    public class GymArenaBuilder : MonoBehaviour
    {
        [Header("Arena Ayarları")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private Vector2 arenaSize = new Vector2(34f, 34f);

        [Header("Dark Fantasy Atmosfer Ayarları")]
        [SerializeField] private bool enableDarkAtmosphere = true;
        [SerializeField] private bool spawnTorches = true;
        [SerializeField] private Color cryptFogColor = new Color(0.045f, 0.038f, 0.058f);
        [SerializeField] private float fogStartDistance = 14f;
        [SerializeField] private float fogEndDistance = 48f;
        [SerializeField] private Color moonlightColor = new Color(0.60f, 0.70f, 0.90f);
        [SerializeField] private float moonlightIntensity = 0.80f;
        [SerializeField] private Color ambientCryptLight = new Color(0.12f, 0.13f, 0.16f);

        // Prosedürel Doku Önbelleği (GC ve performans optimizasyonu)
        private static Texture2D _floorTex;
        private static Texture2D _wallTex;
        private static Texture2D _ironTex;
        private static Texture2D _pillarTex;
        private static Texture2D _tombTex;
        private static Texture2D _woodTex;

        private void Start()
        {
            if (enableDarkAtmosphere)
            {
                ApplyDarkFantasyAtmosphere();
            }

            // Eğer sahnede zaten arena inşa edilmişse (editörde hazırsa),
            // zemini yıkıp oyuncuyu düşürmek yerine Dark Fantasy materyallerini uygula
            Transform existing = transform.Find("Gym_Geometry");
            if (existing == null && buildOnStart)
            {
                BuildArena();
            }
            else if (existing != null)
            {
                ApplyDarkFantasyToExistingGeometry(existing);
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

            // 90'lar Dark Fantasy Prosedürel Dokulu Materyalleri
            Material floorMat = CreateGothicMaterial("Gym_Floor_Gothic", GetOrCreateFloorTexture(), Color.white, 0.10f, 0f, new Vector2(arenaSize.x * 0.5f, arenaSize.y * 0.5f));
            Material wallMat = CreateGothicMaterial("Gym_Wall_Gothic", GetOrCreateWallTexture(), Color.white, 0.12f, 0f, new Vector2(17f, 2f));
            Material rampMat = CreateGothicMaterial("Gym_Ramp_Gothic", GetOrCreateIronTexture(), new Color(0.70f, 0.70f, 0.75f), 0.25f, 0.65f, new Vector2(2f, 4f));
            Material pillarMat = CreateGothicMaterial("Gym_Pillar_Gothic", GetOrCreatePillarTexture(), new Color(0.85f, 0.88f, 0.95f), 0.20f, 0.10f, new Vector2(1f, 3f));
            Material platformMat = CreateGothicMaterial("Gym_Platform_Gothic", GetOrCreateTombTexture(), Color.white, 0.12f, 0f, new Vector2(1.5f, 1.5f));
            Material dummyMat = CreateGothicMaterial("Gym_Dummy_Gothic", GetOrCreateWoodTexture(), Color.white, 0.15f, 0.05f, new Vector2(1f, 2f));

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

            // 6. Gotik Duvar ve Sütun Meşaleleri
            if (spawnTorches)
            {
                SpawnAllTorches(root.transform);
            }

            // 7. 1.80m Boyunda İnsan Referans Mankeni (Pasif DPS / Boyut Kıyaslaması)
            CreateHumanReferenceDummy(root.transform, new Vector3(-3.5f, 0f, -8.5f), dummyMat, "Combat_Dummy_Target");

            // 8. Agresif Gotik Düşmanlar (Ghoul Stalker - Koyu çürük ceset derisi, paslı zırh, kanlı pençeler)
            Material ghoulSkin = CreateColoredMaterial("Ghoul_Skin", new Color(0.12f, 0.13f, 0.14f), 0.15f);
            Material ghoulArmor = CreateColoredMaterial("Ghoul_Armor", new Color(0.16f, 0.12f, 0.12f), 0.25f, 0.5f);
            Material ghoulClaw = CreateColoredMaterial("Ghoul_Claw", new Color(0.38f, 0.08f, 0.08f), 0.30f);
            Material ghoulEye = CreateUnlitMaterial("Ghoul_Eye", new Color(1.0f, 0.15f, 0.02f));

            // Sürü Düşmanları
            CreateGhoulEnemy(root.transform, new Vector3(0f, 0f, 1.0f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_Center");
            CreateGhoulEnemy(root.transform, new Vector3(4.0f, 0f, 0.5f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_Packmate");
            CreateGhoulEnemy(root.transform, new Vector3(-10.5f, 0f, 9.5f), ghoulSkin, ghoulArmor, ghoulClaw, ghoulEye, "Ghoul_Stalker_DistantGuard");

            // 9. Havada Uçan Göz (Occult Watcher - Hastalıklı fildişi sklera, kara büyü moru iris, kömür boynuzlar)
            Material eyeSclera = CreateColoredMaterial("Eye_Sclera", new Color(0.68f, 0.64f, 0.50f), 0.20f);
            Material eyeIris = CreateUnlitMaterial("Eye_Iris", new Color(0.45f, 0.04f, 0.58f));
            Material eyePupil = CreateColoredMaterial("Eye_Pupil", new Color(0.02f, 0.02f, 0.03f), 0.05f);
            Material eyeHorn = CreateColoredMaterial("Eye_Horn", new Color(0.08f, 0.08f, 0.09f), 0.15f);
            Material eyeTendril = CreateColoredMaterial("Eye_Tendril", new Color(0.22f, 0.07f, 0.11f), 0.15f);

            CreateFlyingEyeEnemy(root.transform, new Vector3(2.0f, 2.7f, 4.0f), eyeSclera, eyeIris, eyePupil, eyeHorn, eyeTendril, "FlyingEye_Watcher");

            if (enableDarkAtmosphere)
            {
                ApplyDarkFantasyAtmosphere();
            }

            Debug.Log("[GymArenaBuilder] Gotik Dark Fantasy Test Arenası, atmosferik sis ve meşalelerle başarıyla kuruldu!");
        }

        /// <summary>
        /// Sahneye önceden eklenmiş mevcut arena geometrisini Dark Fantasy materyalleriyle yeniden giydirir.
        /// </summary>
        public void ApplyDarkFantasyToExistingGeometry(Transform root)
        {
            Material floorMat = CreateGothicMaterial("Gym_Floor_Gothic", GetOrCreateFloorTexture(), Color.white, 0.10f, 0f, new Vector2(arenaSize.x * 0.5f, arenaSize.y * 0.5f));
            Material wallMat = CreateGothicMaterial("Gym_Wall_Gothic", GetOrCreateWallTexture(), Color.white, 0.12f, 0f, new Vector2(17f, 2f));
            Material rampMat = CreateGothicMaterial("Gym_Ramp_Gothic", GetOrCreateIronTexture(), new Color(0.70f, 0.70f, 0.75f), 0.25f, 0.65f, new Vector2(2f, 4f));
            Material pillarMat = CreateGothicMaterial("Gym_Pillar_Gothic", GetOrCreatePillarTexture(), new Color(0.85f, 0.88f, 0.95f), 0.20f, 0.10f, new Vector2(1f, 3f));
            Material platformMat = CreateGothicMaterial("Gym_Platform_Gothic", GetOrCreateTombTexture(), Color.white, 0.12f, 0f, new Vector2(1.5f, 1.5f));
            Material dummyMat = CreateGothicMaterial("Gym_Dummy_Gothic", GetOrCreateWoodTexture(), Color.white, 0.15f, 0.05f, new Vector2(1f, 2f));

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                string n = r.gameObject.name;
                if (n == "Floor")
                {
                    r.material = floorMat;
                }
                else if (n.Contains("Wall") || (r.transform.parent != null && r.transform.parent.name.Contains("Wall")))
                {
                    r.material = wallMat;
                }
                else if (n.Contains("Ramp"))
                {
                    r.material = rampMat;
                }
                else if (n.Contains("Pillar"))
                {
                    r.material = pillarMat;
                }
                else if (n.Contains("Step"))
                {
                    r.material = platformMat;
                }
                else if (n.Contains("Dummy") || (r.transform.parent != null && r.transform.parent.name.Contains("Dummy")))
                {
                    r.material = dummyMat;
                }
            }

            // Mevcut veya eski konumu bozuk meşale objesi varsa temizle ve güncel 34m arena konumlarıyla yeniden üret
            Transform oldTorches = root.Find("Gothic_Torches");
            if (oldTorches != null)
            {
                if (Application.isPlaying) Destroy(oldTorches.gameObject);
                else DestroyImmediate(oldTorches.gameObject);
            }

            if (spawnTorches)
            {
                SpawnAllTorches(root);
            }
        }

        /// <summary>
        /// Zindan atmosferi: Doğal derinlik sisi, ay ışığı ve dengeli katakomp ortam aydınlatması.
        /// </summary>
        public void ApplyDarkFantasyAtmosphere()
        {
            // 1. Katakomp Sisi (Linear mod: 14m'ye kadar %0 sis, yakın dövüş berrak; 14-48m arası gotik derinlik)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cryptFogColor;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;

            // 2. Ambiyans Işığı (Soğuk katakomp gölgesi, zifiri karanlık değil)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientCryptLight;

            // 3. Directional Light Ayarı (Gece ay ışığı ve yumuşak gölgeler)
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.color = moonlightColor;
                    l.intensity = moonlightIntensity;
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.85f;
                    break;
                }
            }

            // 4. Unity Scene Reload GI Güncellemesi (LoadScene sonrası ortam ışığının kararmasını önler)
            DynamicGI.UpdateEnvironment();
        }

        private void SpawnAllTorches(Transform root)
        {
            GameObject torchesRoot = new GameObject("Gothic_Torches");
            torchesRoot.transform.SetParent(root);

            float halfX = arenaSize.x * 0.5f;
            float halfZ = arenaSize.y * 0.5f;
            float wallDist = 0.35f;

            // Çevre Duvar Meşaleleri
            // Kuzey Duvarı (Z+)
            SpawnGothicTorch(torchesRoot.transform, new Vector3(-8.5f, 2.2f, halfZ - wallDist), Quaternion.Euler(0f, 180f, 0f));
            SpawnGothicTorch(torchesRoot.transform, new Vector3(8.5f, 2.2f, halfZ - wallDist), Quaternion.Euler(0f, 180f, 0f));

            // Güney Duvarı (Z-)
            SpawnGothicTorch(torchesRoot.transform, new Vector3(-8.5f, 2.2f, -halfZ + wallDist), Quaternion.Euler(0f, 0f, 0f));
            SpawnGothicTorch(torchesRoot.transform, new Vector3(8.5f, 2.2f, -halfZ + wallDist), Quaternion.Euler(0f, 0f, 0f));

            // Doğu Duvarı (X+)
            SpawnGothicTorch(torchesRoot.transform, new Vector3(halfX - wallDist, 2.2f, -8.5f), Quaternion.Euler(0f, -90f, 0f));
            SpawnGothicTorch(torchesRoot.transform, new Vector3(halfX - wallDist, 2.2f, 8.5f), Quaternion.Euler(0f, -90f, 0f));

            // Batı Duvarı (X-)
            SpawnGothicTorch(torchesRoot.transform, new Vector3(-halfX + wallDist, 2.2f, -8.5f), Quaternion.Euler(0f, 90f, 0f));
            SpawnGothicTorch(torchesRoot.transform, new Vector3(-halfX + wallDist, 2.2f, 8.5f), Quaternion.Euler(0f, 90f, 0f));

            // Slalom Sütun Meşaleleri
            for (int i = 0; i < 4; i++)
            {
                float z = -8f + i * 5.5f;
                float x = (i % 2 == 0) ? -1.8f : 1.8f;
                float offset = (i % 2 == 0) ? -0.45f : 0.45f;
                float rotY = (i % 2 == 0) ? -90f : 90f;
                SpawnGothicTorch(torchesRoot.transform, new Vector3(x + offset, 2.1f, z), Quaternion.Euler(0f, rotY, 0f));
            }
        }

        private void SpawnGothicTorch(Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject torch = new GameObject("Gothic_Torch_Sconce");
            torch.transform.SetParent(parent);
            torch.transform.position = position;
            torch.transform.rotation = rotation;

            // 1. Dövme Demir Duvar Braketi
            GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = "Iron_Bracket";
            bracket.transform.SetParent(torch.transform);
            bracket.transform.localPosition = new Vector3(0f, -0.10f, 0.20f);
            bracket.transform.localScale = new Vector3(0.08f, 0.28f, 0.38f);
            DestroyColliderIfAny(bracket);

            // 2. Alev Çanağı (Brazier)
            GameObject cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cup.name = "Brazier_Cup";
            cup.transform.SetParent(torch.transform);
            cup.transform.localPosition = new Vector3(0f, 0.04f, 0.36f);
            cup.transform.localScale = new Vector3(0.20f, 0.10f, 0.20f);
            DestroyColliderIfAny(cup);

            // 3. Yanıcı Kor/Alev Çekirdeği
            GameObject ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ember.name = "Torch_Flame_Ember";
            ember.transform.SetParent(torch.transform);
            ember.transform.localPosition = new Vector3(0f, 0.16f, 0.36f);
            ember.transform.localScale = new Vector3(0.15f, 0.20f, 0.15f);
            DestroyColliderIfAny(ember);

            // Materyal atamaları
            Material ironMat = CreateColoredMaterial("Torch_Iron", new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.7f);
            Material flameMat = CreateUnlitMaterial("Torch_Flame", new Color(1.0f, 0.55f, 0.12f));
            bracket.GetComponent<Renderer>().material = ironMat;
            cup.GetComponent<Renderer>().material = ironMat;
            ember.GetComponent<Renderer>().material = flameMat;

            // 4. Titreşen Dinamik Noktasal Işık (GothicTorchFlicker)
            GameObject lightObj = new GameObject("Torch_PointLight");
            lightObj.transform.SetParent(torch.transform);
            lightObj.transform.localPosition = new Vector3(0f, 0.22f, 0.38f);

            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.55f, 0.18f);
            pLight.intensity = 2.4f;
            pLight.range = 11f;
            pLight.shadows = LightShadows.None; // URP gölge atlasını tıkamayan, akıcı ve parlak alev aydınlatması

            lightObj.AddComponent<GothicTorchFlicker>();
        }

        #region Prosedürel 90'lar Dark Fantasy Dokuları (64x64 / 32x32 Point-Filtered)

        private static Texture2D GetOrCreateFloorTexture()
        {
            if (_floorTex != null && _floorTex) return _floorTex;

            int size = 64;
            _floorTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_CatacombFloor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            int tileSize = 16;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tx = x % tileSize;
                    int ty = y % tileSize;
                    bool isBorder = (tx == 0 || ty == 0 || tx == tileSize - 1 || ty == tileSize - 1);

                    if (isBorder)
                    {
                        // Koyu zindan harcı derzi
                        float mortarNoise = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.05f;
                        pixels[y * size + x] = new Color(0.04f + mortarNoise, 0.035f + mortarNoise, 0.045f + mortarNoise, 1f);
                    }
                    else
                    {
                        // Katakomp taş döşeme
                        float stoneNoise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                        float fineNoise = (Mathf.Sin(x * 12.3f) * Mathf.Cos(y * 8.7f)) * 0.04f;
                        float brightness = 0.18f + stoneNoise * 0.08f + fineNoise;

                        // Kurumuş kan ve rutubet lekeleri
                        float grime = Mathf.PerlinNoise(x * 0.06f + 10f, y * 0.06f + 10f);
                        Color c = new Color(brightness, brightness * 0.95f, brightness * 0.92f, 1f);
                        if (grime > 0.65f)
                        {
                            c = Color.Lerp(c, new Color(0.16f, 0.04f, 0.04f, 1f), (grime - 0.65f) * 2.2f);
                        }
                        pixels[y * size + x] = c;
                    }
                }
            }
            _floorTex.SetPixels(pixels);
            _floorTex.Apply();
            return _floorTex;
        }

        private static Texture2D GetOrCreateWallTexture()
        {
            if (_wallTex != null && _wallTex) return _wallTex;

            int size = 64;
            _wallTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_CryptWall",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            int brickH = 8;
            int brickW = 16;

            for (int y = 0; y < size; y++)
            {
                int row = y / brickH;
                int rowY = y % brickH;
                int offsetX = (row % 2 == 1) ? brickW / 2 : 0;

                for (int x = 0; x < size; x++)
                {
                    int colX = (x + offsetX) % brickW;
                    bool isMortar = (rowY == 0 || rowY == brickH - 1 || colX == 0 || colX == brickW - 1);

                    if (isMortar)
                    {
                        pixels[y * size + x] = new Color(0.03f, 0.025f, 0.035f, 1f);
                    }
                    else
                    {
                        float blockNoise = Mathf.PerlinNoise((x + row * 17f) * 0.2f, y * 0.2f);
                        float crack = (Mathf.PerlinNoise(x * 0.8f, y * 0.8f) > 0.72f) ? -0.06f : 0f;
                        float b = 0.13f + blockNoise * 0.07f + crack;
                        pixels[y * size + x] = new Color(b * 0.95f, b, b * 1.05f, 1f);
                    }
                }
            }
            _wallTex.SetPixels(pixels);
            _wallTex.Apply();
            return _wallTex;
        }

        private static Texture2D GetOrCreateIronTexture()
        {
            if (_ironTex != null && _ironTex) return _ironTex;

            int size = 32;
            _ironTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_DarkIron",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    float rustNoise = Mathf.PerlinNoise(x * 0.18f + 5f, y * 0.18f + 5f);
                    float baseMetal = 0.10f + noise * 0.05f;
                    Color col = new Color(baseMetal, baseMetal * 1.02f, baseMetal * 1.08f, 1f);

                    if (rustNoise > 0.58f)
                    {
                        float rustStrength = (rustNoise - 0.58f) * 1.8f;
                        Color rustCol = new Color(0.24f, 0.07f, 0.04f, 1f);
                        col = Color.Lerp(col, rustCol, rustStrength);
                    }
                    pixels[y * size + x] = col;
                }
            }
            _ironTex.SetPixels(pixels);
            _ironTex.Apply();
            return _ironTex;
        }

        private static Texture2D GetOrCreatePillarTexture()
        {
            if (_pillarTex != null && _pillarTex) return _pillarTex;

            int size = 32;
            _pillarTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_BasaltPillar",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float flute = Mathf.Abs(Mathf.Sin((x / (float)size) * Mathf.PI * 6f));
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.4f) * 0.04f;
                    float b = 0.06f + flute * 0.08f + noise;
                    pixels[y * size + x] = new Color(b * 0.9f, b * 0.95f, b * 1.1f, 1f);
                }
            }
            _pillarTex.SetPixels(pixels);
            _pillarTex.Apply();
            return _pillarTex;
        }

        private static Texture2D GetOrCreateTombTexture()
        {
            if (_tombTex != null && _tombTex) return _tombTex;

            int size = 32;
            _tombTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_TombStone",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    float grain = (Mathf.Sin(x * 35.1f) * Mathf.Cos(y * 42.7f)) * 0.03f;
                    float b = 0.14f + noise * 0.06f + grain;
                    float moss = Mathf.PerlinNoise(x * 0.1f + 2f, y * 0.1f + 2f);
                    Color c = new Color(b, b, b * 0.95f, 1f);
                    if (moss > 0.62f)
                    {
                        c = Color.Lerp(c, new Color(0.08f, 0.13f, 0.07f, 1f), (moss - 0.62f) * 1.5f);
                    }
                    pixels[y * size + x] = c;
                }
            }
            _tombTex.SetPixels(pixels);
            _tombTex.Apply();
            return _tombTex;
        }

        private static Texture2D GetOrCreateWoodTexture()
        {
            if (_woodTex != null && _woodTex) return _woodTex;

            int size = 32;
            _woodTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_TortureWood",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                bool isIronBand = (y >= 6 && y <= 8) || (y >= 23 && y <= 25);
                for (int x = 0; x < size; x++)
                {
                    if (isIronBand)
                    {
                        float ironNoise = Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.05f;
                        pixels[y * size + x] = new Color(0.07f + ironNoise, 0.06f + ironNoise, 0.07f + ironNoise, 1f);
                    }
                    else
                    {
                        float woodGrain = Mathf.Sin(x * 1.2f + Mathf.PerlinNoise(x * 0.15f, y * 0.4f) * 6f) * 0.04f;
                        float b = 0.11f + woodGrain;
                        pixels[y * size + x] = new Color(b * 1.1f, b * 0.9f, b * 0.7f, 1f);
                    }
                }
            }
            _woodTex.SetPixels(pixels);
            _woodTex.Apply();
            return _woodTex;
        }

        #endregion

        #region Geometri ve Varlık Oluşturma Yardımcıları

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
            body.transform.localScale = new Vector3(0.45f, 0.7f, 0.3f);
            body.GetComponent<Renderer>().material = mat;

            // Kafa (0.35m çap)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(dummy.transform);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            head.GetComponent<Renderer>().material = mat;

            dummy.AddComponent<CombatDummy>();
        }

        private void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
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

            CharacterController cc = enemyRoot.AddComponent<CharacterController>();
            cc.height = 2.05f;
            cc.radius = 0.50f;
            cc.center = new Vector3(0f, 1.025f, 0f);
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.08f;

            // Pelvis
            GameObject pelvis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pelvis.name = "Body_Pelvis";
            pelvis.transform.SetParent(enemyRoot.transform);
            pelvis.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pelvis.transform.localScale = new Vector3(0.50f, 0.45f, 0.35f);
            pelvis.GetComponent<Renderer>().material = skinMat;
            DestroyColliderIfAny(pelvis);

            // Göğüs
            GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Body_Chest";
            chest.transform.SetParent(enemyRoot.transform);
            chest.transform.localPosition = new Vector3(0f, 1.28f, 0.08f);
            chest.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            chest.transform.localScale = new Vector3(0.72f, 0.65f, 0.48f);
            chest.GetComponent<Renderer>().material = armorMat;

            // Sırt Dikenleri
            GameObject spineSpike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spineSpike.name = "Spine_Spike";
            spineSpike.transform.SetParent(enemyRoot.transform);
            spineSpike.transform.localPosition = new Vector3(0f, 1.38f, -0.22f);
            spineSpike.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            spineSpike.transform.localScale = new Vector3(0.10f, 0.45f, 0.12f);
            spineSpike.GetComponent<Renderer>().material = clawMat;
            DestroyColliderIfAny(spineSpike);

            // Kafa (Headshot)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(enemyRoot.transform);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0.22f);
            head.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.36f, 0.40f);
            head.GetComponent<Renderer>().material = skinMat;

            // Gözler
            CreateEye(head.transform, new Vector3(-0.10f, 0.05f, 0.21f), eyeMat, "Eye_Left");
            CreateEye(head.transform, new Vector3(0.10f, 0.05f, 0.21f), eyeMat, "Eye_Right");

            // Omuzlar
            CreateShoulder(enemyRoot.transform, new Vector3(-0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, 22f), armorMat, "Shoulder_L");
            CreateShoulder(enemyRoot.transform, new Vector3(0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, -22f), armorMat, "Shoulder_R");

            // Kollar ve Pençeler
            CreateArm(enemyRoot.transform, new Vector3(-0.48f, 1.15f, 0.12f), -1, skinMat, clawMat, "Arm_L");
            CreateArm(enemyRoot.transform, new Vector3(0.48f, 1.15f, 0.12f), 1, skinMat, clawMat, "Arm_R");

            // Bacaklar
            CreateLeg(enemyRoot.transform, new Vector3(-0.20f, 0.36f, 0f), skinMat, "Leg_L");
            CreateLeg(enemyRoot.transform, new Vector3(0.20f, 0.36f, 0f), skinMat, "Leg_R");

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

            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upper.name = "UpperArm";
            upper.transform.SetParent(armRoot.transform);
            upper.transform.localPosition = new Vector3(0f, -0.15f, 0.05f);
            upper.transform.localRotation = Quaternion.Euler(22f, 0f, side * 8f);
            upper.transform.localScale = new Vector3(0.14f, 0.38f, 0.14f);
            upper.GetComponent<Renderer>().material = skinMat;
            DestroyColliderIfAny(upper);

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

        private void CreateFlyingEyeEnemy(Transform parent, Vector3 pos, Material scleraMat, Material irisMat, Material pupilMat, Material hornMat, Material tendrilMat, string name)
        {
            GameObject eyeRoot = new GameObject(name);
            eyeRoot.transform.SetParent(parent);
            eyeRoot.transform.position = pos;

            CharacterController cc = eyeRoot.AddComponent<CharacterController>();
            cc.height = 1.1f;
            cc.radius = 0.55f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0.2f;
            cc.skinWidth = 0.06f;

            // Sclera (Gövde)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body_Sclera";
            body.transform.SetParent(eyeRoot.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            body.GetComponent<Renderer>().material = scleraMat;
            DestroyColliderIfAny(body);

            // Iris (Okült Büyü Halkası)
            GameObject iris = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            iris.name = "Eye_Iris";
            iris.transform.SetParent(eyeRoot.transform);
            iris.transform.localPosition = new Vector3(0f, 0f, 0.38f);
            iris.transform.localScale = new Vector3(0.55f, 0.55f, 0.25f);
            iris.GetComponent<Renderer>().material = irisMat;
            DestroyColliderIfAny(iris);

            // Pupil (Headshot - 2x Crit)
            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil_Head";
            pupil.transform.SetParent(eyeRoot.transform);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            pupil.transform.localScale = new Vector3(0.28f, 0.28f, 0.18f);
            pupil.GetComponent<Renderer>().material = pupilMat;

            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(eyeRoot.transform);
            fp.transform.localPosition = new Vector3(0f, 0f, 0.65f);

            CreateHorn(eyeRoot.transform, new Vector3(0f, 0.52f, -0.15f), Quaternion.Euler(-25f, 0f, 0f), hornMat, "Horn_Top");
            CreateHorn(eyeRoot.transform, new Vector3(-0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, 35f), hornMat, "Horn_L");
            CreateHorn(eyeRoot.transform, new Vector3(0.48f, 0.25f, -0.10f), Quaternion.Euler(-15f, 0f, -35f), hornMat, "Horn_R");

            CreateTendril(eyeRoot.transform, new Vector3(0f, -0.65f, 0f), tendrilMat, "Tendril_Center");
            CreateTendril(eyeRoot.transform, new Vector3(-0.25f, -0.60f, -0.12f), tendrilMat, "Tendril_L");
            CreateTendril(eyeRoot.transform, new Vector3(0.25f, -0.60f, -0.12f), tendrilMat, "Tendril_R");

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

        private void DestroyColliderIfAny(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        private Material CreateGothicMaterial(string matName, Texture2D texture, Color tint, float smoothness = 0.12f, float metallic = 0f, Vector2? tiling = null)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.name = matName;
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            }
            mat.color = tint;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (tiling.HasValue)
            {
                mat.mainTextureScale = tiling.Value;
                if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling.Value);
            }

            return mat;
        }

        private Material CreateColoredMaterial(string matName, Color col, float smoothness = 0.15f, float metallic = 0f)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.name = matName;
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        private Material CreateUnlitMaterial(string matName, Color col)
        {
            Shader uUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (uUnlit == null) uUnlit = Shader.Find("Unlit/Color");

            Material mat = new Material(uUnlit);
            mat.name = matName;
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            return mat;
        }

        #endregion
    }
}
