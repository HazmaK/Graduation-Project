using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Dark Fantasy Retro Boomer Shooter Okült Çağırma Telgrafı (Occult Summoning Telegraph).
    /// Düşman belirmeden 0.6 - 0.8 saniye önce zeminde parlayan kan pentagramı ve yükselen
    /// okült parçacıklar üreterek oyuncuya taktiksel tepki (önceden nişan alma/kaçınma) şansı verir.
    /// Sıfır GC tahsisli nesne havuzu (Object Pool) ile çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class OccultSpawnTelegraph : MonoBehaviour
    {
        [Header("Görsel Ayarlar")]
        [SerializeField] private Color runeColor = new Color(0.95f, 0.15f, 0.10f, 1.0f);
        [SerializeField] private Color flashColor = new Color(1.0f, 0.85f, 0.40f, 1.0f);
        [SerializeField] private float maxDiscRadius = 1.6f;

        private float _duration = 0.70f;
        private float _timer = 0f;
        private bool _isActive = false;
        private System.Action _onCompleteCallback;

        private Transform _discTransform;
        private Renderer _discRenderer;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            // Havuz ısıtılırken ses kaynağını önceden oluştur (oyun sırasında AddComponent yok)
            EnsureCueSource();
            BuildProceduralTelegraphVisuals();
        }

        private void BuildProceduralTelegraphVisuals()
        {
            if (_discTransform != null) return;

            // Zemin Pentagram Diski
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "OccultRuneDisc";
            disc.transform.SetParent(transform);
            disc.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            disc.transform.localScale = new Vector3(maxDiscRadius, 0.01f, maxDiscRadius);

            Collider col = disc.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            _discTransform = disc.transform;
            _discRenderer = disc.GetComponent<Renderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = "Mat_OccultSummonTelegraph",
                hideFlags = HideFlags.DontSave
            };
            mat.color = runeColor;
            _discRenderer.material = mat;
        }

        // Director'ın lambda (heap tahsisi) kullanmadan doğuş bağlamını taşıması için yük alanları
        private System.Action<OccultSpawnTelegraph> _onCompleteWithSelf;
        private GameObject _payloadPrefab;
        private int _payloadWaveIndex;
        private int _payloadGeneration;
        private float _payloadThreat;
        private AudioSource _cueSource;

        public GameObject PayloadPrefab => _payloadPrefab;
        public int PayloadWaveIndex => _payloadWaveIndex;
        public int PayloadGeneration => _payloadGeneration;
        public float PayloadThreat => _payloadThreat;

        /// <summary>
        /// Telgraf tamamlandığında doğurulacak düşman bağlamını (prefab, dalga, nesil, tehdit) telgrafa iliştirir.
        /// </summary>
        public void SetPayload(GameObject prefab, int waveIndex, int generation, float threat = 1f)
        {
            _payloadPrefab = prefab;
            _payloadWaveIndex = waveIndex;
            _payloadGeneration = generation;
            _payloadThreat = threat;
        }

        /// <summary>
        /// Çağırma sesini telgrafın konumunda 3D olarak çalar (oyuncu arkasındaki doğuşu yönüyle duyar).
        /// </summary>
        public void PlayCue(AudioClip clip)
        {
            if (clip == null) return;
            EnsureCueSource();
            _cueSource.PlayOneShot(clip);
        }

        private void EnsureCueSource()
        {
            if (_cueSource != null) return;
            _cueSource = gameObject.AddComponent<AudioSource>();
            _cueSource.playOnAwake = false;
            _cueSource.spatialBlend = 1f;
            _cueSource.rolloffMode = AudioRolloffMode.Linear;
            _cueSource.minDistance = 4f;
            _cueSource.maxDistance = 45f;
        }

        /// <summary>
        /// Telgrafı havuzdan belirli bir konumda başlatır.
        /// </summary>
        public void Activate(Vector3 position, Quaternion rotation, float duration, System.Action onComplete)
        {
            _onCompleteWithSelf = null;
            _onCompleteCallback = onComplete;
            BeginActivation(position, rotation, duration);
        }

        /// <summary>
        /// Sıfır GC varyantı: tamamlandığında telgrafın kendisini parametre olarak veren önbellekli delegeyi çağırır.
        /// </summary>
        public void Activate(Vector3 position, Quaternion rotation, float duration, System.Action<OccultSpawnTelegraph> onCompleteWithSelf)
        {
            _onCompleteCallback = null;
            _onCompleteWithSelf = onCompleteWithSelf;
            BeginActivation(position, rotation, duration);
        }

        /// <summary>
        /// Çalışan telgrafı callback çağırmadan iptal eder ve havuza döndürür.
        /// </summary>
        public void Cancel()
        {
            _isActive = false;
            _isLingering = false;
            _onCompleteCallback = null;
            _onCompleteWithSelf = null;
            if (_cueSource != null) _cueSource.Stop();
            gameObject.SetActive(false);
        }

        private void BeginActivation(Vector3 position, Quaternion rotation, float duration)
        {
            transform.position = position;
            transform.rotation = rotation;
            _duration = duration > 0.1f ? duration : 0.70f;
            _timer = 0f;
            _isActive = true;
            _isLingering = false;
            gameObject.SetActive(true);
            if (_discRenderer != null) _discRenderer.enabled = true;

            if (_discTransform != null)
            {
                _discTransform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
            }
        }

        // Görsel bitti ama 3D çağırma sesi hâlâ çalıyor: obje aktif kalır (ses kesilmesin), havuza ses bitince döner
        private bool _isLingering;

        private void Update()
        {
            if (_isLingering)
            {
                if (_cueSource == null || !_cueSource.isPlaying)
                {
                    _isLingering = false;
                    gameObject.SetActive(false);
                }
                return;
            }

            if (!_isActive) return;

            _timer += Time.deltaTime;
            float progress = Mathf.Clamp01(_timer / _duration);

            // Büyüyen ve nabız gibi atan rün çemberi
            if (_discTransform != null)
            {
                float scale = Mathf.Lerp(0.2f, maxDiscRadius, progress);
                _discTransform.localScale = new Vector3(scale, 0.01f, scale);
                _discTransform.Rotate(Vector3.up, 240f * Time.deltaTime);

                if (_discRenderer != null)
                {
                    Color currentColor = progress > 0.82f ? Color.Lerp(runeColor, flashColor, (progress - 0.82f) / 0.18f) : runeColor;
                    _discRenderer.GetPropertyBlock(_propBlock);
                    _propBlock.SetColor(BaseColorId, currentColor);
                    _discRenderer.SetPropertyBlock(_propBlock);
                }
            }

            if (_timer >= _duration)
            {
                _isActive = false;

                // Kan ve okült darbe efekti (Eğer VFX Manager varsa)
                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(transform.position + Vector3.up * 0.5f, Vector3.up, true, false);
                }

                System.Action callback = _onCompleteCallback;
                System.Action<OccultSpawnTelegraph> callbackWithSelf = _onCompleteWithSelf;
                _onCompleteCallback = null;
                _onCompleteWithSelf = null;

                // Görseli gizle; ses bitmişse hemen havuza dön, bitmediyse ses bitene kadar bekle
                if (_cueSource != null && _cueSource.isPlaying)
                {
                    if (_discRenderer != null) _discRenderer.enabled = false;
                    _isLingering = true;
                }
                else
                {
                    gameObject.SetActive(false);
                }
                callback?.Invoke();
                callbackWithSelf?.Invoke(this);
            }
        }
    }
}
