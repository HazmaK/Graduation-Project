using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Kan lekesi türleri (Darbe, kılıç kesiği, patlama ve damla).
    /// </summary>
    public enum BloodDecalType
    {
        Pool,       // Büyük göllenme / birikinti
        Slash,      // Kılıç savurması çizgisel sıçraması
        Burst,      // Yüksek basınçlı radyal mermi püskürmesi
        Drip        // Ufak kan damlaları / et sıçramaları
    }

    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli Kalıcı Kan ve Çevre Sıçrama Yöneticisi (Persistent Blood Decal System).
    /// - 64 elemanlı dairesel havuz (Circular Buffer Pool) ile sabit bellek ve sıfır çöp üretimi.
    /// - Yalnızca gerçek statik zemin ve duvar yüzeylerine tutunur; karakter, düşman veya havada leke oluşturmaz.
    /// - Zemin lekeleri %100 yatay (Vector3.up), duvar lekeleri tam dikey yüzeye hizalanır.
    /// - Z-fighting'i önlemek için yüzeyden 3.5 milimetre kaldırılır.
    /// </summary>
    [DisallowMultipleComponent]
    public class BloodDecalManager : MonoBehaviour
    {
        private static BloodDecalManager _instance;
        public static BloodDecalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<BloodDecalManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BloodDecalManager");
                        _instance = go.AddComponent<BloodDecalManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Havuz Ayarları")]
        [SerializeField] private int maxDecals = 64;
        [SerializeField] private LayerMask surfaceLayerMask = ~0;

        [Header("Gotik Kan Renkleri")]
        [SerializeField] private Color darkCrimsonBlood = new Color(0.28f, 0.02f, 0.04f, 0.94f);
        [SerializeField] private Color deepVenousBlood = new Color(0.18f, 0.01f, 0.02f, 0.96f);

        // Prosedürel Kan Dokuları
        private static Texture2D _texPool;
        private static Texture2D _texSlash;
        private static Texture2D _texBurst;
        private static Texture2D _texDrip;

        private GameObject[] _decalObjects;
        private Transform[] _decalTransforms;
        private Renderer[] _decalRenderers;
        private MaterialPropertyBlock _propBlock;
        private int _currentIndex = 0;

        // GC Çöpünü Önlemek İçin Önbelleğe Alınmış Raycast Tamponu
        private static readonly RaycastHit[] HitResults = new RaycastHit[16];
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _propBlock = new MaterialPropertyBlock();
            GenerateProceduralTextures();
            BuildDecalPool();
        }

        private void GenerateProceduralTextures()
        {
            if (_texPool == null) _texPool = CreatePoolTexture();
            if (_texSlash == null) _texSlash = CreateSlashTexture();
            if (_texBurst == null) _texBurst = CreateBurstTexture();
            if (_texDrip == null) _texDrip = CreateDripTexture();
        }

        private static Texture2D CreatePoolTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Blood_Pool",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] p = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float maxR = s * 0.44f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float angle = Mathf.Atan2(y - c.y, x - c.x);
                    float lobe = Mathf.Sin(angle * 5f) * 0.12f + Mathf.Cos(angle * 3f) * 0.08f;
                    float edge = maxR * (1f + lobe);

                    float alpha = dist < edge ? Mathf.SmoothStep(1f, 0.35f, dist / edge) : 0f;
                    p[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(p);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSlashTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Blood_Slash",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] p = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (float)x / s;
                    float v = (float)y / s;

                    float targetV = 0.5f + Mathf.Sin(u * Mathf.PI) * 0.22f;
                    float distY = Mathf.Abs(v - targetV);
                    float width = Mathf.Lerp(0.06f, 0.18f, Mathf.Sin(u * Mathf.PI));

                    float alpha = distY < width ? Mathf.SmoothStep(1f, 0f, distY / width) : 0f;
                    p[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(p);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBurstTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Blood_Burst",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] p = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float angle = Mathf.Atan2(y - c.y, x - c.x);
                    float spike = Mathf.Abs(Mathf.Sin(angle * 7f)) * 0.35f + Mathf.Abs(Mathf.Cos(angle * 4f)) * 0.25f;
                    float maxR = (s * 0.32f) * (0.6f + spike);

                    float alpha = dist < maxR ? Mathf.SmoothStep(1f, 0.2f, dist / maxR) : 0f;
                    p[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(p);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateDripTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Blood_Drip",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] p = new Color[s * s];
            Vector2[] spots = new Vector2[]
            {
                new Vector2(16, 16),
                new Vector2(8, 22),
                new Vector2(24, 10),
                new Vector2(22, 23),
                new Vector2(9, 9)
            };
            float[] rads = new float[] { 5.5f, 3.2f, 3.8f, 2.8f, 3.0f };

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float maxAlpha = 0f;
                    Vector2 pt = new Vector2(x, y);
                    for (int k = 0; k < spots.Length; k++)
                    {
                        float d = Vector2.Distance(pt, spots[k]);
                        if (d < rads[k])
                        {
                            float a = Mathf.SmoothStep(1f, 0.1f, d / rads[k]);
                            if (a > maxAlpha) maxAlpha = a;
                        }
                    }
                    p[y * s + x] = new Color(1f, 1f, 1f, maxAlpha);
                }
            }
            tex.SetPixels(p);
            tex.Apply();
            return tex;
        }

        private void BuildDecalPool()
        {
            _decalObjects = new GameObject[maxDecals];
            _decalTransforms = new Transform[maxDecals];
            _decalRenderers = new Renderer[maxDecals];

            Shader decalShader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Sprites/Default")
                              ?? Shader.Find("Unlit/Transparent");

            Material sharedDecalMat = new Material(decalShader)
            {
                name = "Mat_BloodDecal_Shared",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15
            };

            if (sharedDecalMat.HasProperty("_Surface")) sharedDecalMat.SetFloat("_Surface", 1);
            if (sharedDecalMat.HasProperty("_Blend")) sharedDecalMat.SetFloat("_Blend", 0);
            if (sharedDecalMat.HasProperty("_ZWrite")) sharedDecalMat.SetFloat("_ZWrite", 0);

            Mesh quadMesh = CreateQuadMesh();
            GameObject poolRoot = new GameObject("Blood_Decal_Pool");
            poolRoot.transform.SetParent(transform);

            for (int i = 0; i < maxDecals; i++)
            {
                GameObject decal = new GameObject($"Blood_Decal_{i}");
                decal.transform.SetParent(poolRoot.transform);

                // Raycast'lerin kendi decal'lerine çarpmasını engelle (Layer 2 = Ignore Raycast)
                decal.layer = 2;

                MeshFilter mf = decal.AddComponent<MeshFilter>();
                mf.sharedMesh = quadMesh;

                MeshRenderer mr = decal.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sharedDecalMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                _decalObjects[i] = decal;
                _decalTransforms[i] = decal.transform;
                _decalRenderers[i] = mr;

                decal.SetActive(false);
            }
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh { name = "Decal_Quad_Mesh" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.normals = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }

        /// <summary>
        /// Sadece gerçek statik çevre yüzeylerini (Zemin, Duvar, Sütun) tespit eden filtreli Raycast tarayıcısı.
        /// Karakterleri, düşmanları, uzuvları ve havada uçan gib'leri tamamen yoksayar.
        /// </summary>
        private bool RaycastEnvironment(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = default;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, HitResults, maxDistance, surfaceLayerMask, QueryTriggerInteraction.Ignore);
            if (count == 0) return false;

            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit h = HitResults[i];
                Collider col = h.collider;
                if (col == null) continue;

                // 1. Karakter, Düşman ve Mankenleri filtrele (Havada leke oluşmasını engeller)
                if (col.GetComponentInParent<IDamageable>() != null) continue;
                if (col.GetComponentInParent<CharacterController>() != null) continue;
                if (col.GetComponentInParent<PlayerMovement>() != null) continue;
                if (col.GetComponentInParent<GibPhysicsReporter>() != null) continue;

                // 2. Gib, Mermi ve Decal objelerini filtrele
                string n = col.name;
                if (n.StartsWith("Gib_") || n.StartsWith("Arrow_") || n.StartsWith("Blood_")) continue;

                // 3. En yakın geçerli statik yüzeyi seç
                if (h.distance < closestDist && h.distance > 0.005f)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Yüzey normaline ve noktasına kesin temas eden bir kan lekesi damgalar.
        /// Normal yukarı bakıyorsa lekenin %100 yatay zemin pozisyonu alması garanti edilir.
        /// </summary>
        public void StampDecal(Vector3 position, Vector3 normal, BloodDecalType type, float scaleMultiplier = 1f)
        {
            if (_decalObjects == null || _decalObjects.Length == 0) return;

            // Zemin normalini saf Vector3.up olarak zorla (Yere yapışmayı ve yataylığı %100 garanti eder)
            if (normal.y >= 0.55f)
            {
                normal = Vector3.up;
            }
            else if (Mathf.Abs(normal.y) <= 0.35f)
            {
                // Dikey duvar normalini yatay düzlemde normalize et
                normal = new Vector3(normal.x, 0f, normal.z).normalized;
            }

            int idx = _currentIndex;
            _currentIndex = (_currentIndex + 1) % maxDecals;

            GameObject obj = _decalObjects[idx];
            Transform trans = _decalTransforms[idx];
            Renderer rend = _decalRenderers[idx];

            if (obj == null || trans == null || rend == null) return;

            // Z-fighting'i önlemek için yüzeyden 3.5 milimetre kaldır
            trans.position = position + normal * 0.0035f;

            // Yüzey normaline göre döndür ve rastgele kendi ekseninde çevir
            Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion randomSpin = Quaternion.AngleAxis(Random.Range(0f, 360f), normal);
            trans.rotation = randomSpin * surfaceRot;

            // Boyut ölçeklendirmesi
            float baseSize = (type == BloodDecalType.Pool ? Random.Range(1.1f, 1.8f) :
                              type == BloodDecalType.Slash ? Random.Range(1.3f, 2.2f) :
                              type == BloodDecalType.Burst ? Random.Range(0.85f, 1.4f) :
                              Random.Range(0.55f, 0.95f)) * scaleMultiplier;

            trans.localScale = new Vector3(baseSize, 1f, baseSize);

            Texture2D tex = type == BloodDecalType.Pool ? _texPool :
                            type == BloodDecalType.Slash ? _texSlash :
                            type == BloodDecalType.Burst ? _texBurst : _texDrip;

            Color bloodColor = Color.Lerp(darkCrimsonBlood, deepVenousBlood, Random.value);
            rend.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(BaseMapId, tex);
            _propBlock.SetTexture(MainTexId, tex);
            _propBlock.SetColor(BaseColorId, bloodColor);
            _propBlock.SetColor(ColorId, bloodColor);
            rend.SetPropertyBlock(_propBlock);

            obj.SetActive(true);
        }

        /// <summary>
        /// Karakterlerin içinden geçip yalnızca gerçek zemin yüzeyine yatay leke bırakır.
        /// </summary>
        public bool TrySplatterGround(Vector3 origin, float maxDistance = 6.0f, BloodDecalType type = BloodDecalType.Pool, float scale = 1f)
        {
            if (RaycastEnvironment(origin, Vector3.down, maxDistance, out RaycastHit hit))
            {
                if (hit.normal.y >= 0.55f)
                {
                    StampDecal(hit.point, Vector3.up, type, scale);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Karakterlerin içinden geçip yalnızca arkadaki gerçek duvara dikey leke püskürtür.
        /// Duvar yoksa havada ASLA leke oluşturmaz.
        /// </summary>
        public bool TrySplatterWall(Vector3 origin, Vector3 shootDir, float maxDistance = 5.5f, BloodDecalType type = BloodDecalType.Slash, float scale = 1f)
        {
            if (shootDir.sqrMagnitude < 0.01f) return false;

            if (RaycastEnvironment(origin, shootDir, maxDistance, out RaycastHit hit))
            {
                // Dikey duvar kontrolü
                if (Mathf.Abs(hit.normal.y) <= 0.35f)
                {
                    Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                    StampDecal(hit.point, wallNormal, type, scale);
                    return true;
                }
                // Duvara değil de zemine çarptıysa zemine yatay damgala
                else if (hit.normal.y >= 0.55f)
                {
                    StampDecal(hit.point, Vector3.up, type, scale);
                    return true;
                }
            }
            return false;
        }
    }
}
