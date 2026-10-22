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

        /// <summary>
        /// Telgrafı havuzdan belirli bir konumda başlatır.
        /// </summary>
        public void Activate(Vector3 position, Quaternion rotation, float duration, System.Action onComplete)
        {
            transform.position = position;
            transform.rotation = rotation;
            _duration = duration > 0.1f ? duration : 0.70f;
            _timer = 0f;
            _onCompleteCallback = onComplete;
            _isActive = true;
            gameObject.SetActive(true);

            if (_discTransform != null)
            {
                _discTransform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
            }
        }

        private void Update()
        {
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
                _onCompleteCallback = null;
                callback?.Invoke();

                gameObject.SetActive(false);
            }
        }
    }
}
