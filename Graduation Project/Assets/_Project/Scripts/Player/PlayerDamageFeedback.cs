using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli Oyuncu Darbe ve Hasar Hissiyatı (Player Damage Game Feel / Juice) Sistemi.
    /// - Kenar Kan Perdesi & Acı Flaşı (Blood Vignette & Pain Flash)
    /// - Kritik Can Durumunda Ritmik Kalp Atışı Uyarısı (Low-Health Pulsing Vignette)
    /// - 360 Derece Yönlü Darbe Göstergeleri (Directional Hit Arc Indicators)
    /// - Yönlü Kamera Sarsıntısı ve Silah Geri Tepmesi (Directional Camera & Viewmodel Flinch)
    /// - Tok Beden Darbesi ve Acı Sesleri (Combat Audio Integration)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDamageFeedback : MonoBehaviour
    {
        [Header("Kan Perdesi & Acı Flaşı (Vignette)")]
        [SerializeField] private bool enableBloodVignette = true;
        [SerializeField] private Color vignetteColor = new Color(0.75f, 0.04f, 0.06f, 1f);
        [SerializeField] private float vignetteDecaySpeed = 2.4f;

        [Header("Gotik Ortam Karartması (Dark Fantasy Ambient Vignette)")]
        [SerializeField] private bool enableAmbientVignette = true;
        [Range(0f, 0.4f)]
        [SerializeField] private float ambientVignetteIntensity = 0.12f;
        [SerializeField] private Color ambientVignetteColor = new Color(0.01f, 0.01f, 0.02f, 1f);

        [Header("Kritik Can Nabız Uyarısı (Low Health)")]
        [SerializeField] private float lowHealthThreshold = 30f;
        [SerializeField] private float lowHealthPulseSpeed = 6.5f;

        [Header("Yönlü Darbe Göstergeleri (Directional Indicators)")]
        [SerializeField] private bool enableDirectionalIndicators = true;
        [SerializeField] private float indicatorRadius = 100f;
        [SerializeField] private float indicatorDuration = 0.70f;
        [SerializeField] private Color indicatorColor = new Color(1f, 0.15f, 0.15f, 0.95f);

        [Header("Kamera & Silah Flinch Kuvveti")]
        [SerializeField] private bool enableFlinch = true;
        [SerializeField] private float flinchIntensityMultiplier = 1.0f;

        private PlayerHealth _health;
        private PlayerLook _look;
        private Camera _cam;
        private ProceduralViewmodel _viewmodel;

        // Kan Perdesi Dokusu (Sıfır GC tahsisi için dinamik önbellek)
        private Texture2D _vignetteTexture;
        private Texture2D _indicatorTexture;
        private float _currentPainAlpha = 0f;

        // Yönlü Darbe Havuzu (Maksimum 4 eşzamanlı yönlü darbe)
        private struct DirectionalHit
        {
            public bool Active;
            public float Angle; // Derece cinsinden: 0 = Ön, 90 = Sağ, 180 = Arka, -90 = Sol
            public float Timer;
            public float MaxTimer;
        }

        private const int MaxHits = 4;
        private readonly DirectionalHit[] _hits = new DirectionalHit[MaxHits];
        private int _nextHitIndex = 0;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _look = GetComponent<PlayerLook>();
            _cam = Camera.main;
            _viewmodel = GetComponentInChildren<ProceduralViewmodel>();
            _currentPainAlpha = 0f;

            CreateVignetteTexture();
            CreateIndicatorTexture();
        }

        private void OnEnable()
        {
            _currentPainAlpha = 0f;
            for (int i = 0; i < MaxHits; i++)
            {
                _hits[i].Active = false;
            }
        }

        private void Start()
        {
            if (_cam == null) _cam = Camera.main;
            if (_viewmodel == null) _viewmodel = GetComponentInChildren<ProceduralViewmodel>();
        }

        private void CreateVignetteTexture()
        {
            if (_vignetteTexture != null) return;

            int size = 128;
            _vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _vignetteTexture.hideFlags = HideFlags.DontSave;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // Kenarlara doğru yumuşak kavisli opaklık
                    float alpha = Mathf.SmoothStep(0.25f, 1.0f, dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _vignetteTexture.SetPixels(pixels);
            _vignetteTexture.Apply();
        }

        private void CreateIndicatorTexture()
        {
            if (_indicatorTexture != null) return;

            // Keskin gotik darbe oku (Chevron/Wedge)
            int w = 32;
            int h = 18;
            _indicatorTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _indicatorTexture.hideFlags = HideFlags.DontSave;
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    float v = (float)y / h;

                    // Sivri V şeklindeki kavisli ok geometrisi
                    float distFromCenter = Mathf.Abs(u - 0.5f) * 2f;
                    float expectedV = distFromCenter * 0.85f;
                    bool inWedge = v >= expectedV && v <= expectedV + 0.35f;

                    pixels[y * w + x] = inWedge ? Color.white : Color.clear;
                }
            }

            _indicatorTexture.SetPixels(pixels);
            _indicatorTexture.Apply();
        }

        /// <summary>
        /// Karakter hasar aldığında tüm game feel tepkilerini (VFX, Audio, Flinch, HUD) eşzamanlı ateşler.
        /// </summary>
        public void OnPlayerDamaged(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject attacker)
        {
            if (_cam == null) _cam = Camera.main;

            // 1. Acı Flaşı Opaklığını Artır
            float impactRatio = Mathf.Clamp01(amount / 30f);
            _currentPainAlpha = Mathf.Min(0.85f, _currentPainAlpha + 0.35f + impactRatio * 0.50f);

            // 2. Darbenin Geldiği Yönü Hesapla (360 derece yatay açı)
            Vector3 hitSourcePos = attacker != null ? attacker.transform.position : hitPoint;
            Vector3 toSource = hitSourcePos - transform.position;
            toSource.y = 0f;

            Vector3 localDir = Vector3.forward;
            if (_cam != null && toSource.sqrMagnitude > 0.01f)
            {
                Vector3 camForwardHoriz = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                float signedAngle = Vector3.SignedAngle(camForwardHoriz, toSource.normalized, Vector3.up);

                // Yönlü darbe göstergesini kaydet
                AddDirectionalHit(signedAngle);

                // Yerel uzay darbe yönü (Kamera flinch için)
                localDir = _cam.transform.InverseTransformDirection(toSource.normalized);
            }

            // 3. Yönlü Kamera ve Silah Flinch Sarsıntısı
            if (enableFlinch)
            {
                float intensity = (0.7f + impactRatio * 0.6f) * flinchIntensityMultiplier;

                if (_look != null)
                {
                    _look.AddDirectionalFlinch(localDir, intensity);
                }

                if (_viewmodel == null) _viewmodel = GetComponentInChildren<ProceduralViewmodel>();
                if (_viewmodel != null)
                {
                    _viewmodel.AddDamageFlinch(intensity);
                }
            }

            // 4. Beden Darbesi ve Acı Sesleri (Combat Audio)
            if (Combat.CombatAudio.Instance != null)
            {
                bool isLowHealth = _health != null && _health.CurrentHealth <= lowHealthThreshold;
                Combat.CombatAudio.Instance.PlayPlayerHurt(amount, isLowHealth);
            }
        }

        private void AddDirectionalHit(float angle)
        {
            if (!enableDirectionalIndicators) return;

            int index = _nextHitIndex;
            _nextHitIndex = (_nextHitIndex + 1) % MaxHits;

            _hits[index].Active = true;
            _hits[index].Angle = angle;
            _hits[index].Timer = indicatorDuration;
            _hits[index].MaxTimer = indicatorDuration;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Kan perdesi acı flaşının sönmesi
            if (_currentPainAlpha > 0f)
            {
                _currentPainAlpha = Mathf.MoveTowards(_currentPainAlpha, 0f, dt * vignetteDecaySpeed);
            }

            // Yönlü darbe göstergelerinin ömür takibi
            for (int i = 0; i < MaxHits; i++)
            {
                if (_hits[i].Active)
                {
                    _hits[i].Timer -= dt;
                    if (_hits[i].Timer <= 0f)
                    {
                        _hits[i].Active = false;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (_health != null && _health.IsDead) return;

            float screenW = Screen.width;
            float screenH = Screen.height;
            if (screenW < 100f || screenH < 100f) return;

            Color oldCol = GUI.color;

            // 0. Gotik Ortam Karartması (Dark Fantasy Ambient Vignette)
            if (enableAmbientVignette && _vignetteTexture != null)
            {
                Color ambCol = ambientVignetteColor;
                ambCol.a = ambientVignetteIntensity;
                GUI.color = ambCol;
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _vignetteTexture);
                GUI.color = oldCol;
            }

            // 1. Kenar Kan Perdesi & Acı Flaşı (Vignette)
            if (enableBloodVignette && _vignetteTexture != null)
            {
                float finalAlpha = _currentPainAlpha;

                // Kritik Can Durumunda Ritmik Kalp Atışı Pulsasyonu
                if (_health != null && _health.CurrentHealth <= lowHealthThreshold)
                {
                    float pulse = (Mathf.Sin(Time.time * lowHealthPulseSpeed) * 0.5f + 0.5f);
                    float lowHpAlpha = Mathf.Lerp(0.20f, 0.58f, pulse);
                    finalAlpha = Mathf.Max(finalAlpha, lowHpAlpha);
                }

                if (finalAlpha > 0.01f)
                {
                    Color vCol = vignetteColor;
                    vCol.a = finalAlpha;
                    GUI.color = vCol;
                    GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _vignetteTexture);
                }
            }

            // 2. Yönlü Darbe Göstergeleri (360 Derece Radial Hit Indicators)
            if (enableDirectionalIndicators && _indicatorTexture != null)
            {
                float cx = screenW * 0.5f;
                float cy = screenH * 0.5f;

                for (int i = 0; i < MaxHits; i++)
                {
                    if (!_hits[i].Active) continue;

                    float t = _hits[i].Timer / _hits[i].MaxTimer;
                    float alpha = Mathf.Clamp01(t * 1.5f);

                    Color indCol = indicatorColor;
                    indCol.a = alpha;
                    GUI.color = indCol;

                    Matrix4x4 oldMatrix = GUI.matrix;

                    // Nişangah etrafında darbenin açısına göre döndür
                    GUIUtility.RotateAroundPivot(_hits[i].Angle, new Vector2(cx, cy));

                    float indW = 48f;
                    float indH = 24f;
                    // Açının yönünde indicatorRadius kadar ileri yerleştir (yukarı doğru ötelenir)
                    Rect indRect = new Rect(cx - indW * 0.5f, cy - indicatorRadius - indH, indW, indH);

                    GUI.DrawTexture(indRect, _indicatorTexture);

                    GUI.matrix = oldMatrix;
                }
            }

            GUI.color = oldCol;
        }

        private void OnDestroy()
        {
            if (_vignetteTexture != null) Destroy(_vignetteTexture);
            if (_indicatorTexture != null) Destroy(_indicatorTexture);
        }
    }
}
