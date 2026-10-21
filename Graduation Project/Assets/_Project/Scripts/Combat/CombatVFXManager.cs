using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Sektör standardı, sıfır GC tahsisi üreten Havuzlanmış Dövüş Görsel Efekt Yöneticisi (Combat VFX Manager).
    /// - Kan Püskürmesi (Blood Splatter Spray)
    /// - Kritik Darbe Altın Kıvılcımları (Crit Sparks)
    /// - Çevre / Duvar Sekme Kıvılcımları & Toz Pufu (Wall Ricochet & Dust)
    /// - Düşman Ölümünde Düşük Poligonlu Et & Kemik Parçalanması (Low-Poly Flesh Gibs)
    /// - Namlu Parlaması (Muzzle Flash & Dynamic Light Pulse)
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
        [SerializeField] private Color bloodColor = new Color(0.65f, 0.04f, 0.06f, 0.95f);
        [SerializeField] private Color bloodDarkColor = new Color(0.25f, 0.01f, 0.02f, 0.95f);
        [SerializeField] private int bloodDropCount = 20;

        [Header("Kritik & Kıvılcım Efektleri")]
        [SerializeField] private bool enableCritSparks = true;
        [SerializeField] private Color critSparkColor = new Color(1f, 0.88f, 0.20f, 1f);
        [SerializeField] private int critSparkCount = 28;

        [Header("Duvar & Çevre Sekme Efektleri")]
        [SerializeField] private bool enableWallSparks = true;
        [SerializeField] private Color wallSparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color dustColor = new Color(0.55f, 0.52f, 0.48f, 0.7f);

        [Header("Parçalanma (Gibs & Gore)")]
        [SerializeField] private bool enableFleshGibs = true;
        [SerializeField] private int gibsPerDeath = 5;
        [SerializeField] private float gibExplosionForce = 7.5f;

        // Havuzlanmış Parçacık Sistemleri
        private ParticleSystem _bloodParticleSystem;
        private ParticleSystem _sparkParticleSystem;
        private ParticleSystem _dustParticleSystem;

        // Havuzlanmış Et Parçaları (Gibs)
        private const int MaxGibs = 32;
        private readonly GameObject[] _gibPool = new GameObject[MaxGibs];
        private readonly Rigidbody[] _gibBodies = new Rigidbody[MaxGibs];
        private readonly float[] _gibDespawnTimers = new float[MaxGibs];
        private int _nextGibIndex = 0;

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

            InitializeParticlePools();
            InitializeGibPool();
            InitializeMuzzleLight();
        }

        private void InitializeParticlePools()
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Unlit/Color");

            // 1. Kan Parçacık Sistemi
            GameObject bloodObj = new GameObject("VFX_Pool_Blood");
            bloodObj.transform.SetParent(transform);
            _bloodParticleSystem = bloodObj.AddComponent<ParticleSystem>();
            SetupParticleSystem(_bloodParticleSystem, bloodColor, bloodDarkColor, 0.12f, 12f, true, particleShader);

            // 2. Kıvılcım Parçacık Sistemi
            GameObject sparkObj = new GameObject("VFX_Pool_Sparks");
            sparkObj.transform.SetParent(transform);
            _sparkParticleSystem = sparkObj.AddComponent<ParticleSystem>();
            SetupParticleSystem(_sparkParticleSystem, critSparkColor, Color.white, 0.06f, 18f, true, particleShader);

            // 3. Çevre Tozu Sistemi
            GameObject dustObj = new GameObject("VFX_Pool_Dust");
            dustObj.transform.SetParent(transform);
            _dustParticleSystem = dustObj.AddComponent<ParticleSystem>();
            SetupParticleSystem(_dustParticleSystem, dustColor, dustColor * 0.7f, 0.25f, 3.5f, false, particleShader);
        }

        private void SetupParticleSystem(ParticleSystem ps, Color colorMin, Color colorMax, float startSize, float speed, bool useGravity, Shader shader)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.5f, startSize);
            main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
            main.gravityModifier = useGravity ? 2.5f : 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.08f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            renderer.material = mat;
        }

        private void InitializeGibPool()
        {
            Shader gibShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");

            Material gibMat = new Material(gibShader);
            Color goreCol = new Color(0.45f, 0.05f, 0.06f);
            if (gibMat.HasProperty("_BaseColor")) gibMat.SetColor("_BaseColor", goreCol);
            if (gibMat.HasProperty("_Color")) gibMat.color = goreCol;

            GameObject gibRoot = new GameObject("VFX_Pool_Gibs");
            gibRoot.transform.SetParent(transform);

            for (int i = 0; i < MaxGibs; i++)
            {
                // Düşük poligonlu gotik et/kemik parçası
                GameObject gib = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gib.name = $"Gib_Chunk_{i}";
                gib.transform.SetParent(gibRoot.transform);
                gib.transform.localScale = new Vector3(
                    Random.Range(0.12f, 0.22f),
                    Random.Range(0.08f, 0.16f),
                    Random.Range(0.12f, 0.25f)
                );

                gib.GetComponent<Renderer>().material = gibMat;

                Rigidbody rb = gib.AddComponent<Rigidbody>();
                rb.mass = 1.2f;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                _gibPool[i] = gib;
                _gibBodies[i] = rb;
                _gibDespawnTimers[i] = 0f;
                gib.SetActive(false);
            }
        }

        private void InitializeMuzzleLight()
        {
            GameObject lightObj = new GameObject("VFX_MuzzlePointLight");
            lightObj.transform.SetParent(transform);
            _muzzlePointLight = lightObj.AddComponent<Light>();
            _muzzlePointLight.type = LightType.Point;
            _muzzlePointLight.range = 7f;
            _muzzlePointLight.color = new Color(0.2f, 0.95f, 1f); // Neon arbalest cyan/gold
            _muzzlePointLight.intensity = 0f;
            _muzzlePointLight.enabled = false;
        }

        private void Update()
        {
            // Namlu ışığının anlık sönmesi
            if (_muzzleLightTimer > 0f)
            {
                _muzzleLightTimer -= Time.deltaTime;
                if (_muzzleLightTimer <= 0f && _muzzlePointLight != null)
                {
                    _muzzlePointLight.intensity = 0f;
                    _muzzlePointLight.enabled = false;
                }
            }

            // Et parçalarının (Gibs) ömür takibi
            for (int i = 0; i < MaxGibs; i++)
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

        #region Public VFX Tetikleyicileri

        /// <summary>
        /// Mermi veya kılıç bir hedefe çarptığında uygun etki parçacıklarını fırlatır.
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
        /// Düşman öldüğünde veya aşırı hasar aldığında low-poly et parçalarını çevreye fırlatır.
        /// </summary>
        public void SpawnFleshGibs(Vector3 origin, Vector3 impactDir, int count = -1)
        {
            if (!enableFleshGibs) return;

            int spawnCount = count > 0 ? count : gibsPerDeath;

            for (int k = 0; k < spawnCount; k++)
            {
                int index = _nextGibIndex;
                _nextGibIndex = (_nextGibIndex + 1) % MaxGibs;

                GameObject gib = _gibPool[index];
                Rigidbody rb = _gibBodies[index];

                if (gib == null || rb == null) continue;

                gib.transform.position = origin + Random.insideUnitSphere * 0.35f;
                gib.transform.rotation = Random.rotation;
                gib.SetActive(true);

                // Fiziksel fırlatma darbesi
                Vector3 blastDir = (impactDir.normalized + Random.insideUnitSphere * 0.85f + Vector3.up * 0.75f).normalized;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(blastDir * (gibExplosionForce * Random.Range(0.8f, 1.4f)), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 25f, ForceMode.Impulse);

                _gibDespawnTimers[index] = Random.Range(3.5f, 5.5f);
            }
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
