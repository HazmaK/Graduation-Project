using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Gib parçasının zeminle temasında kan lekesi ve ses tetikleyen sıfır tahsisli fizik habercisi.
    /// </summary>
    public class GibPhysicsReporter : MonoBehaviour
    {
        private bool _hasSplattered = false;
        private bool _isHead = false;

        public void Setup(bool isHead)
        {
            _isHead = isHead;
            _hasSplattered = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasSplattered) return;

            Collider other = collision.collider;
            if (other == null) return;

            // Karakterlere, düşmanlara veya diğer havada uçan gib parçalarına çarpınca leke BIRAKMA!
            if (other.GetComponentInParent<IDamageable>() != null) return;
            if (other.GetComponentInParent<CharacterController>() != null) return;
            if (other.GetComponentInParent<PlayerMovement>() != null) return;
            if (other.GetComponentInParent<GibPhysicsReporter>() != null) return;
            string otherName = other.name;
            if (otherName.StartsWith("Gib_") || otherName.StartsWith("Arrow_") || otherName.StartsWith("Blood_")) return;

            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);

                // Yalnızca gerçek zemine (%100 yatay) veya gerçek dikey duvara çarpınca leke damgala
                if (contact.normal.y >= 0.55f)
                {
                    _hasSplattered = true;
                    if (BloodDecalManager.Instance != null)
                    {
                        BloodDecalType decalType = _isHead ? BloodDecalType.Pool : BloodDecalType.Drip;
                        float scale = _isHead ? 1.3f : Random.Range(0.6f, 0.95f);
                        BloodDecalManager.Instance.StampDecal(contact.point, Vector3.up, decalType, scale);
                    }

                    if (CombatAudio.Instance != null)
                    {
                        CombatAudio.Instance.PlayGibBounce(contact.point);
                    }
                }
                else if (Mathf.Abs(contact.normal.y) <= 0.35f)
                {
                    _hasSplattered = true;
                    Vector3 wallNormal = new Vector3(contact.normal.x, 0f, contact.normal.z).normalized;
                    if (BloodDecalManager.Instance != null)
                    {
                        BloodDecalType decalType = _isHead ? BloodDecalType.Burst : BloodDecalType.Drip;
                        BloodDecalManager.Instance.StampDecal(contact.point, wallNormal, decalType, 0.75f);
                    }

                    if (CombatAudio.Instance != null)
                    {
                        CombatAudio.Instance.PlayGibBounce(contact.point);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli Gelişmiş Dövüş Görsel Efekt Yöneticisi (Combat VFX Manager).
    /// - Şeffaf ve organik kan damlaları (Soft Fluid Droplets)
    /// - Hıza göre uzayan parıltılı kıvılcım iğneleri (Stretched Radiant Sparks)
    /// - Kılıç savurmasında beliren gotik hilal biçme şeridi (Blade Slash Ribbon)
    /// - Okült enerji mermisi patlama ve sis halkası (Occult Energy Burst)
    /// - Anatomik Düşük Poligonlu Et, Kemik ve Kafatası Parçalanması
    /// - Kalıcı Zemin ve Duvar Kan İzi Entegrasyonu
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatVFXManager : MonoBehaviour
    {
        private static CombatVFXManager _instance;
        public static CombatVFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<CombatVFXManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CombatVFXManager");
                        _instance = go.AddComponent<CombatVFXManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Kan & Et Efektleri")]
        [SerializeField] private bool enableBloodVFX = true;
        [SerializeField] private Color bloodColor = new Color(0.55f, 0.03f, 0.05f, 0.98f);
        [SerializeField] private Color bloodDarkColor = new Color(0.20f, 0.01f, 0.02f, 0.98f);
        [SerializeField] private int bloodDropCount = 20;

        [Header("Kritik & Kıvılcım Efektleri")]
        [SerializeField] private bool enableCritSparks = true;
        [SerializeField] private Color critSparkColor = new Color(1.0f, 0.85f, 0.25f, 1f);
        [SerializeField] private int critSparkCount = 26;

        [Header("Duvar & Çevre Sekme Efektleri")]
        [SerializeField] private bool enableWallSparks = true;
        [SerializeField] private Color wallSparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color dustColor = new Color(0.55f, 0.52f, 0.48f, 0.65f);

        [Header("Parçalanma (Gibs & Gore)")]
        [SerializeField] private bool enableFleshGibs = true;
        [SerializeField] private int gibsPerDeath = 6;
        [SerializeField] private float gibExplosionForce = 7.5f;

        // Prosedürel Alfa Maskeli Parçacık Dokuları
        private static Texture2D _texBloodParticle;
        private static Texture2D _texSparkParticle;
        private static Texture2D _texSmokeParticle;

        // Havuzlanmış Parçacık Sistemleri
        private ParticleSystem _bloodParticleSystem;
        private ParticleSystem _sparkParticleSystem;
        private ParticleSystem _dustParticleSystem;
        private ParticleSystem _neckFountainSystem;
        private ParticleSystem _occultBurstSystem;

        // Havuzlanmış Anatomik Et ve Kemik Parçaları (Gibs)
        private const int TotalGibs = 36;
        private const int HeadGibsCount = 4;
        private const int BoneGibsCount = 12;

        private readonly GameObject[] _gibPool = new GameObject[TotalGibs];
        private readonly Rigidbody[] _gibBodies = new Rigidbody[TotalGibs];
        private readonly GibPhysicsReporter[] _gibReporters = new GibPhysicsReporter[TotalGibs];
        private readonly float[] _gibDespawnTimers = new float[TotalGibs];
        private int _nextGeneralGibIndex = HeadGibsCount;
        private int _nextHeadGibIndex = 0;

        // Namlu Işığı (Muzzle Light)
        private Light _muzzlePointLight;
        private float _muzzleLightTimer = 0f;
        private float _muzzleLightDuration = 0.04f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            GenerateVFXTextures();
            InitializeParticlePools();
            InitializeAnatomicalGibPool();
            InitializeMuzzleLight();
        }

        private void GenerateVFXTextures()
        {
            if (_texBloodParticle == null) _texBloodParticle = CreateBloodDropTexture();
            if (_texSparkParticle == null) _texSparkParticle = CreateGlowSparkTexture();
            if (_texSmokeParticle == null) _texSmokeParticle = CreateSmokePuffTexture();
        }

        private static Texture2D CreateBloodDropTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Blood_Drop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pix = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float maxR = s * 0.44f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float alpha = dist < maxR ? Mathf.SmoothStep(1f, 0f, dist / maxR) : 0f;
                    pix[y * s + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateGlowSparkTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Glow_Spark",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pix = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float maxR = s * 0.5f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c) / maxR;
                    float glow = Mathf.Exp(-dist * 4.2f);
                    pix[y * s + x] = new Color(1f, 1f, 1f, glow);
                }
            }

            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSmokePuffTexture()
        {
            int s = 32;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "Tex_Smoke_Puff",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pix = new Color[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float maxR = s * 0.46f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float baseMask = dist < maxR ? Mathf.SmoothStep(1f, 0f, dist / maxR) : 0f;
                    float noise = Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.45f + 0.55f;
                    pix[y * s + x] = new Color(1f, 1f, 1f, baseMask * noise);
                }
            }

            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Material CreateParticleMaterial(Texture2D tex, Shader shader)
        {
            Material mat = new Material(shader)
            {
                mainTexture = tex,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15
            };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0);
            return mat;
        }

        private void InitializeParticlePools()
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Unlit/Transparent");

            // 1. Kan Damlaları (Fluid Blood Droplets)
            GameObject bloodObj = new GameObject("VFX_Pool_Blood");
            bloodObj.transform.SetParent(transform);
            _bloodParticleSystem = bloodObj.AddComponent<ParticleSystem>();
            SetupFluidParticleSystem(_bloodParticleSystem, _texBloodParticle, bloodColor, bloodDarkColor, 0.14f, 11f, true, particleShader);

            // 2. Hıza Göre Uzayan Parıltılı Kıvılcım İğneleri (Stretched Radiant Sparks)
            GameObject sparkObj = new GameObject("VFX_Pool_Sparks");
            sparkObj.transform.SetParent(transform);
            _sparkParticleSystem = sparkObj.AddComponent<ParticleSystem>();
            SetupSparkParticleSystem(_sparkParticleSystem, _texSparkParticle, critSparkColor, Color.white, 0.08f, 18f, particleShader);

            // 3. Çevre Tozu ve Duman Sistemi (Soft Billowing Smoke)
            GameObject dustObj = new GameObject("VFX_Pool_Dust");
            dustObj.transform.SetParent(transform);
            _dustParticleSystem = dustObj.AddComponent<ParticleSystem>();
            SetupSmokeParticleSystem(_dustParticleSystem, _texSmokeParticle, dustColor, 0.28f, 2.5f, particleShader);

            // 4. Kafa Kopması Boyun Kan Fıskiyesi (Neck Blood Fountain)
            GameObject neckObj = new GameObject("VFX_Pool_NeckFountain");
            neckObj.transform.SetParent(transform);
            _neckFountainSystem = neckObj.AddComponent<ParticleSystem>();
            SetupNeckFountainSystem(_neckFountainSystem, _texBloodParticle, bloodColor, bloodDarkColor, particleShader);

            // 5. Okült Enerji Patlama Sistemi (Occult Void Energy Burst)
            GameObject occultObj = new GameObject("VFX_Pool_OccultBurst");
            occultObj.transform.SetParent(transform);
            _occultBurstSystem = occultObj.AddComponent<ParticleSystem>();
            SetupOccultBurstSystem(_occultBurstSystem, _texSparkParticle, particleShader);
        }

        private void SetupFluidParticleSystem(ParticleSystem ps, Texture2D tex, Color colorMin, Color colorMax, float startSize, float speed, bool useGravity, Shader shader)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.6f, startSize);
            main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
            main.gravityModifier = useGravity ? 3.0f : 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.06f;

            // Ömür boyu küçülme (Doğal sıvı damlası kuruması)
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1.0f);
            sizeCurve.AddKey(0.6f, 0.85f);
            sizeCurve.AddKey(1f, 0.25f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(tex, shader);
        }

        private void SetupSparkParticleSystem(ParticleSystem ps, Texture2D tex, Color colorMin, Color colorMax, float startSize, float speed, Shader shader)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.5f, startSize);
            main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            // Hıza göre uzayan kıvılcım iğneleri (Stretched Billboard)
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
            renderer.velocityScale = 0.08f;
            renderer.material = CreateParticleMaterial(tex, shader);
        }

        private void SetupSmokeParticleSystem(ParticleSystem ps, Texture2D tex, Color col, float startSize, float speed, Shader shader)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.7f, startSize * 1.3f);
            main.startColor = col;
            main.gravityModifier = -0.05f; // Hafifçe yukarı yükselme
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(tex, shader);
        }

        private void SetupNeckFountainSystem(ParticleSystem ps, Texture2D tex, Color colorMin, Color colorMax, Shader shader)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
            main.gravityModifier = 2.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.05f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(tex, shader);
        }

        private void SetupOccultBurstSystem(ParticleSystem ps, Texture2D tex, Shader shader)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 0.12f, 0.95f), new Color(0.25f, 0.95f, 1.0f));
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.0f;
            renderer.velocityScale = 0.06f;
            renderer.material = CreateParticleMaterial(tex, shader);
        }

        private void InitializeAnatomicalGibPool()
        {
            Shader gibShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");

            Material fleshMat = new Material(gibShader) { name = "Mat_Gib_Flesh" };
            Color fleshCol = new Color(0.38f, 0.04f, 0.06f);
            if (fleshMat.HasProperty("_BaseColor")) fleshMat.SetColor("_BaseColor", fleshCol);
            if (fleshMat.HasProperty("_Color")) fleshMat.color = fleshCol;

            Material boneMat = new Material(gibShader) { name = "Mat_Gib_Bone" };
            Color boneCol = new Color(0.82f, 0.78f, 0.68f);
            if (boneMat.HasProperty("_BaseColor")) boneMat.SetColor("_BaseColor", boneCol);
            if (boneMat.HasProperty("_Color")) boneMat.color = boneCol;

            Material skullMat = new Material(gibShader) { name = "Mat_Gib_Skull" };
            Color skullCol = new Color(0.22f, 0.18f, 0.19f);
            if (skullMat.HasProperty("_BaseColor")) skullMat.SetColor("_BaseColor", skullCol);
            if (skullMat.HasProperty("_Color")) skullMat.color = skullCol;

            GameObject gibRoot = new GameObject("VFX_Pool_AnatomicalGibs");
            gibRoot.transform.SetParent(transform);

            for (int i = 0; i < TotalGibs; i++)
            {
                GameObject gib;
                bool isHead = i < HeadGibsCount;
                bool isBone = i >= HeadGibsCount && i < HeadGibsCount + BoneGibsCount;

                if (isHead)
                {
                    gib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gib.name = $"Gib_SeveredHead_{i}";
                    gib.transform.localScale = new Vector3(0.32f, 0.32f, 0.35f);
                    gib.GetComponent<Renderer>().material = skullMat;
                }
                else if (isBone)
                {
                    gib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    gib.name = $"Gib_BoneFragment_{i}";
                    gib.transform.localScale = new Vector3(0.08f, Random.Range(0.20f, 0.38f), 0.08f);
                    gib.GetComponent<Renderer>().material = boneMat;
                }
                else
                {
                    gib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gib.name = $"Gib_FleshChunk_{i}";
                    gib.transform.localScale = new Vector3(
                        Random.Range(0.14f, 0.26f),
                        Random.Range(0.10f, 0.18f),
                        Random.Range(0.14f, 0.28f)
                    );
                    gib.GetComponent<Renderer>().material = fleshMat;
                }

                gib.transform.SetParent(gibRoot.transform);
                gib.layer = 2; // Raycast taramalarından gizle

                Rigidbody rb = gib.AddComponent<Rigidbody>();
                rb.mass = isHead ? 2.5f : (isBone ? 0.8f : 1.2f);
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                GibPhysicsReporter reporter = gib.AddComponent<GibPhysicsReporter>();

                _gibPool[i] = gib;
                _gibBodies[i] = rb;
                _gibReporters[i] = reporter;
                _gibDespawnTimers[i] = 0f;
                gib.SetActive(false);
            }

            // Gib parçalarının havada patlama anında birbirine çarpıp leke oluşturmasını engelle
            for (int a = 0; a < TotalGibs; a++)
            {
                Collider colA = _gibPool[a] != null ? _gibPool[a].GetComponent<Collider>() : null;
                if (colA == null) continue;
                for (int b = a + 1; b < TotalGibs; b++)
                {
                    Collider colB = _gibPool[b] != null ? _gibPool[b].GetComponent<Collider>() : null;
                    if (colB != null) Physics.IgnoreCollision(colA, colB, true);
                }
            }
        }

        private void InitializeMuzzleLight()
        {
            GameObject lightObj = new GameObject("VFX_MuzzlePointLight");
            lightObj.transform.SetParent(transform);
            _muzzlePointLight = lightObj.AddComponent<Light>();
            _muzzlePointLight.type = LightType.Point;
            _muzzlePointLight.range = 7f;
            _muzzlePointLight.color = new Color(0.2f, 0.95f, 1f);
            _muzzlePointLight.intensity = 0f;
            _muzzlePointLight.enabled = false;
        }

        private void Update()
        {
            if (_muzzleLightTimer > 0f)
            {
                _muzzleLightTimer -= Time.deltaTime;
                if (_muzzleLightTimer <= 0f && _muzzlePointLight != null)
                {
                    _muzzlePointLight.intensity = 0f;
                    _muzzlePointLight.enabled = false;
                }
            }

            for (int i = 0; i < TotalGibs; i++)
            {
                if (_gibDespawnTimers[i] > 0f)
                {
                    _gibDespawnTimers[i] -= Time.deltaTime;
                    if (_gibDespawnTimers[i] <= 0f)
                    {
                        if (_gibPool[i] != null) _gibPool[i].SetActive(false);
                    }
                }
            }
        }

        #region Public VFX & Gore Tetikleyicileri

        /// <summary>
        /// Kılıç savurulduğunda kamera önünde beliren hilal şerit efekti (Kullanıcı tercihi doğrultusunda kaldırıldı).
        /// </summary>
        public void SpawnBladeSlashArc(Vector3 position, Quaternion rotation, bool isCrit)
        {
            // Kullanıcı tercihi doğrultusunda ekran önündeki kırmızı görsel efekt devre dışı bırakıldı.
        }

        /// <summary>
        /// Kılıç saplama darbesinde ileriye doğru sivri bir aerodinamik şok dalgası fırlatır.
        /// </summary>
        public void SpawnBladeThrustVFX(Vector3 position, Vector3 forward)
        {
            if (_sparkParticleSystem != null)
            {
                _sparkParticleSystem.transform.position = position;
                _sparkParticleSystem.transform.rotation = Quaternion.LookRotation(forward);
                _sparkParticleSystem.Emit(14);
            }

            if (_dustParticleSystem != null)
            {
                _dustParticleSystem.transform.position = position;
                _dustParticleSystem.transform.rotation = Quaternion.LookRotation(forward);
                _dustParticleSystem.Emit(4);
            }
        }

        /// <summary>
        /// Mermi veya kılıç bir hedefe çarptığında kan püskürtür ve çevreye kalıcı kan lekesi damgalar.
        /// </summary>
        public void SpawnHitImpact(Vector3 hitPoint, Vector3 hitNormal, bool isCrit, bool isFlesh = true)
        {
            Quaternion rot = Quaternion.LookRotation(hitNormal);

            if (isFlesh && enableBloodVFX && _bloodParticleSystem != null)
            {
                _bloodParticleSystem.transform.position = hitPoint;
                _bloodParticleSystem.transform.rotation = rot;
                int count = isCrit ? bloodDropCount * 2 : bloodDropCount;
                _bloodParticleSystem.Emit(count);

                // Arkadaki duvara veya altındaki zemine kan lekesi damgala
                if (BloodDecalManager.Instance != null)
                {
                    BloodDecalType decalType = isCrit ? BloodDecalType.Burst : BloodDecalType.Drip;
                    if (!BloodDecalManager.Instance.TrySplatterWall(hitPoint, -hitNormal, 4.5f, decalType, isCrit ? 1.3f : 0.9f))
                    {
                        BloodDecalManager.Instance.TrySplatterGround(hitPoint, 3.5f, BloodDecalType.Drip, 0.85f);
                    }
                }
            }

            if (isCrit && enableCritSparks && _sparkParticleSystem != null)
            {
                _sparkParticleSystem.transform.position = hitPoint;
                _sparkParticleSystem.transform.rotation = rot;
                _sparkParticleSystem.Emit(critSparkCount);
            }

            if (!isFlesh && enableWallSparks)
            {
                if (_sparkParticleSystem != null)
                {
                    _sparkParticleSystem.transform.position = hitPoint;
                    _sparkParticleSystem.transform.rotation = rot;
                    _sparkParticleSystem.Emit(12);
                }

                if (_dustParticleSystem != null)
                {
                    _dustParticleSystem.transform.position = hitPoint;
                    _dustParticleSystem.transform.rotation = rot;
                    _dustParticleSystem.Emit(6);
                }
            }
        }

        /// <summary>
        /// Kılıç savurmasında yatay kan çizgisi damgalar ve kan sisi püskürtür.
        /// </summary>
        public void SpawnBladeSlashImpact(Vector3 hitPoint, Vector3 slashDir, bool isCrit)
        {
            SpawnHitImpact(hitPoint, slashDir, isCrit, true);

            if (BloodDecalManager.Instance != null)
            {
                if (!BloodDecalManager.Instance.TrySplatterWall(hitPoint, slashDir, 4.5f, BloodDecalType.Slash, isCrit ? 1.5f : 1.1f))
                {
                    BloodDecalManager.Instance.TrySplatterGround(hitPoint, 3.5f, BloodDecalType.Slash, 1.2f);
                }
            }
        }

        /// <summary>
        /// Okült enerji mermisi (Uçan Göz) duvara veya hedefe çarptığında yayılan mor/mavi enerji patlaması.
        /// </summary>
        public void SpawnOccultEnergyBurst(Vector3 position, bool isDeflected)
        {
            if (_occultBurstSystem != null)
            {
                _occultBurstSystem.transform.position = position;
                _occultBurstSystem.transform.rotation = Quaternion.LookRotation(Random.onUnitSphere);
                _occultBurstSystem.Emit(isDeflected ? 24 : 16);
            }

            if (_dustParticleSystem != null)
            {
                _dustParticleSystem.transform.position = position;
                _dustParticleSystem.transform.rotation = Quaternion.identity;
                _dustParticleSystem.Emit(5);
            }
        }

        /// <summary>
        /// Düşman aşırı hasarla patladığında anatomik et, kemik ve kafa parçalarını çevreye fırlatır.
        /// </summary>
        public void SpawnFleshGibs(Vector3 origin, Vector3 impactDir, int count = -1)
        {
            if (!enableFleshGibs) return;

            int spawnCount = count > 0 ? count : gibsPerDeath;

            if (BloodDecalManager.Instance != null)
            {
                BloodDecalManager.Instance.TrySplatterGround(origin, 4.0f, BloodDecalType.Pool, 1.45f);
                BloodDecalManager.Instance.TrySplatterWall(origin, impactDir, 5.0f, BloodDecalType.Burst, 1.35f);
            }

            for (int k = 0; k < spawnCount; k++)
            {
                int index = _nextGeneralGibIndex;
                _nextGeneralGibIndex++;
                if (_nextGeneralGibIndex >= TotalGibs) _nextGeneralGibIndex = HeadGibsCount;

                GameObject gib = _gibPool[index];
                Rigidbody rb = _gibBodies[index];
                GibPhysicsReporter reporter = _gibReporters[index];

                if (gib == null || rb == null) continue;

                gib.transform.position = origin + Random.insideUnitSphere * 0.35f;
                gib.transform.rotation = Random.rotation;
                if (reporter != null) reporter.Setup(false);
                gib.SetActive(true);

                Vector3 blastDir = (impactDir.normalized + Random.insideUnitSphere * 0.9f + Vector3.up * 0.85f).normalized;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(blastDir * (gibExplosionForce * Random.Range(0.85f, 1.45f)), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 35f, ForceMode.Impulse);

                _gibDespawnTimers[index] = Random.Range(4.5f, 7.0f);
            }
        }

        /// <summary>
        /// Kafa kopması (Decapitation) durumunda kopan kafayı havaya fırlatır.
        /// </summary>
        public void SpawnSeveredHead(Vector3 headPosition, Vector3 impactDir)
        {
            if (!enableFleshGibs) return;

            int index = _nextHeadGibIndex;
            _nextHeadGibIndex = (_nextHeadGibIndex + 1) % HeadGibsCount;

            GameObject headGib = _gibPool[index];
            Rigidbody rb = _gibBodies[index];
            GibPhysicsReporter reporter = _gibReporters[index];

            if (headGib == null || rb == null) return;

            headGib.transform.position = headPosition;
            headGib.transform.rotation = Random.rotation;
            if (reporter != null) reporter.Setup(true);
            headGib.SetActive(true);

            Vector3 headLaunch = (impactDir.normalized * 0.8f + Vector3.up * 1.5f).normalized;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(headLaunch * Random.Range(6.0f, 8.5f), ForceMode.Impulse);
            rb.AddTorque(new Vector3(Random.Range(20f, 40f), Random.Range(15f, 30f), Random.Range(20f, 40f)), ForceMode.Impulse);

            _gibDespawnTimers[index] = Random.Range(7.0f, 11.0f);

            if (BloodDecalManager.Instance != null)
            {
                BloodDecalManager.Instance.TrySplatterGround(headPosition, 4.0f, BloodDecalType.Pool, 1.1f);
            }
        }

        /// <summary>
        /// Kafa koptuğunda boyundan dikey fışkıran basınçlı kan fıskiyesi (Neck Blood Fountain).
        /// </summary>
        public void SpawnNeckBloodFountain(Vector3 neckPosition)
        {
            if (!enableBloodVFX || _neckFountainSystem == null) return;

            _neckFountainSystem.transform.position = neckPosition;
            _neckFountainSystem.transform.rotation = Quaternion.LookRotation(Vector3.up);
            _neckFountainSystem.Emit(22);
        }

        /// <summary>
        /// Namlu ucunda anlık parlayan ışık darbesi (Muzzle Flash).
        /// </summary>
        public void TriggerMuzzleFlash(Vector3 position, Color flashColor, float intensity = 3.2f)
        {
            if (_muzzlePointLight == null) return;

            _muzzlePointLight.transform.position = position;
            _muzzlePointLight.color = flashColor;
            _muzzlePointLight.intensity = intensity;
            _muzzlePointLight.enabled = true;
            _muzzleLightTimer = _muzzleLightDuration;
        }

        #endregion
    }
}
