using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Sektör standardı, sıfır GC tahsisi üreten Kinetik Hasar Sayıları Sistemi (Floating Damage Numbers).
    /// - 32'lik önbellekli struct havuzu ile çalışır (Zero GC Allocation).
    /// - Vurulan hedefin üzerinde sıçrayarak yerçekimiyle süzülen 3D uzay projeksiyonlu sayılar.
    /// - Normal vuruş, Kritik (Headshot) ve Parry yansıtma hasarı için dinamik renk ve boyut kodlaması.
    /// - Inspector üzerinden tamamen özelleştirilebilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageNumberSystem : MonoBehaviour
    {
        [SerializeField] private Color boltsColor = new Color(0.1f, 0.85f, 1f);
        [SerializeField] private Color shellsColor = new Color(1f, 0.7f, 0.15f);
        private static DamageNumberSystem _instance;
        private static bool _isQuitting;

        /// <summary>
        /// Tembel (lazy) erişim: sahne-yükleme ön ısıtmasından önce (ör. Awake içinde) veya additive
        /// sahnelerde de null dönmez. Sıcak yolda yalnızca tek bir null kontrolü maliyeti vardır.
        /// </summary>
        public static DamageNumberSystem Instance
        {
            get
            {
                if (_instance == null && !_isQuitting) EnsureInstance();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
            _isQuitting = false;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= EnsureSceneInstance;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += EnsureSceneInstance;
        }

        private static void HandleQuitting() => _isQuitting = true;

        private static void EnsureSceneInstance(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (_instance != null || _isQuitting) return;
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            _instance = Object.FindAnyObjectByType<DamageNumberSystem>();
            if (_instance == null) _instance = new GameObject("DamageNumberSystem").AddComponent<DamageNumberSystem>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [Header("Görsel & Sistem Ayarları")]
        [SerializeField] private bool showDamageNumbers = true;
        [SerializeField] private float popupLifetime = 0.70f;
        [SerializeField] private float upwardForce = 2.8f;
        [SerializeField] private float gravity = 5.5f;

        [Header("Renk Kodlaması")]
        [SerializeField] private Color normalDamageColor = new Color(1f, 0.90f, 0.65f, 1f); // Fildişi sarı
        [SerializeField] private Color critDamageColor = new Color(1f, 0.82f, 0.12f, 1f);   // Altın sarısı
        [SerializeField] private Color reflectedDamageColor = new Color(0.2f, 0.95f, 1f, 1f); // Cyan mavi
        [SerializeField] private Color healColor = new Color(0.20f, 1.0f, 0.45f, 1f);       // Zümrüt yeşili
        [SerializeField] private Color shadowColor = new Color(0.02f, 0.02f, 0.04f, 0.90f); // Kontrast gölge

        private struct PopupEntry
        {
            internal bool Active { get; set; }
            internal Vector3 WorldPos { get; set; }
            internal Vector3 Velocity { get; set; }
            internal float Timer { get; set; }
            internal int Amount { get; set; }
            internal bool IsCrit { get; set; }
            internal bool IsReflected { get; set; }
            internal bool IsHeal { get; set; }
            internal DamagePopupKind Kind { get; set; }
            internal string Label { get; set; }
            internal int PartCount { get; set; }
        }

        private const int MaxPopups = 32;
        private const int CachedAmounts = 4096;
        private const int MaxLabelParts = 12;
        private readonly string[][] _labels = new string[6][];
        private readonly GUIContent[] _contents = new GUIContent[MaxPopups];
        private readonly GUIContent[] _largeLabelParts = new GUIContent[MaxPopups * MaxLabelParts];
        private readonly string[] _digits = new string[10];
        private readonly PopupEntry[] _pool = new PopupEntry[MaxPopups];
        private int _nextIndex = 0;

        private Camera _cam;
        private GUIStyle _styleNormal;
        private GUIStyle _styleCrit;
        private GUIStyle _styleReflected;
        private GUIStyle _styleHeal;
        private GUIStyle _shadowStyle;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            _cam = Camera.main;
            InitializeLabels();
            InitStyles();
        }

        private void InitializeLabels()
        {
            for (int kind = 0; kind < _labels.Length; kind++) _labels[kind] = new string[CachedAmounts];
            for (int amount = 0; amount < CachedAmounts; amount++)
            {
                string number = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _labels[0][amount] = number;
                _labels[1][amount] = "CRIT! " + number;
                _labels[2][amount] = "PARRY " + number;
                _labels[3][amount] = "+" + number + " HP";
                _labels[4][amount] = "+" + number + " OK";
                _labels[5][amount] = "+" + number + " SAÇMA";
                if (amount < 10) _digits[amount] = number;
            }
            for (int i = 0; i < _contents.Length; i++) _contents[i] = new GUIContent();
            for (int i = 0; i < _largeLabelParts.Length; i++) _largeLabelParts[i] = new GUIContent();
        }

        private void SetLabel(int index, int kind)
        {
            int amount = Mathf.Max(0, _pool[index].Amount);
            if (amount < CachedAmounts)
            {
                _pool[index].Label = _labels[kind][amount];
                _contents[index].text = _pool[index].Label;
                _pool[index].PartCount = 0;
                return;
            }

            // Preserve arbitrary integer totals without allocating strings on a hit.
            int offset = index * MaxLabelParts;
            int count = 0;
            string prefix = kind == 1 ? "CRIT! " : kind == 2 ? "PARRY " : kind >= 3 ? "+" : "";
            _pool[index].Label = prefix;
            if (prefix.Length > 0) _largeLabelParts[offset + count++].text = prefix;
            int divisor = 1;
            while (amount / divisor >= 10 && divisor <= 100000000) divisor *= 10;
            while (divisor > 0)
            {
                _largeLabelParts[offset + count++].text = _digits[amount / divisor % 10];
                divisor /= 10;
            }
            if (kind == 3) _largeLabelParts[offset + count++].text = " HP";
            else if (kind == 4) _largeLabelParts[offset + count++].text = " OK";
            else if (kind == 5) _largeLabelParts[offset + count++].text = " SAÇMA";
            _pool[index].PartCount = count;
        }

        private void DrawLabel(Rect rect, int index, GUIStyle style)
        {
            int count = _pool[index].PartCount;
            if (count == 0)
            {
                GUI.Label(rect, _contents[index], style);
                return;
            }
            int offset = index * MaxLabelParts;
            float width = 0f;
            for (int i = 0; i < count; i++) width += style.CalcSize(_largeLabelParts[offset + i]).x;
            float x = rect.center.x - width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                GUIContent content = _largeLabelParts[offset + i];
                float partWidth = style.CalcSize(content).x;
                GUI.Label(new Rect(x, rect.y, partWidth, rect.height), content, style);
                x += partWidth;
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _styleNormal = new GUIStyle();
            _styleNormal.fontSize = 17;
            _styleNormal.fontStyle = FontStyle.Bold;
            _styleNormal.alignment = TextAnchor.MiddleCenter;
            _styleNormal.normal.textColor = normalDamageColor;

            _styleCrit = new GUIStyle();
            _styleCrit.fontSize = 24;
            _styleCrit.fontStyle = FontStyle.Bold;
            _styleCrit.alignment = TextAnchor.MiddleCenter;
            _styleCrit.normal.textColor = critDamageColor;

            _styleReflected = new GUIStyle();
            _styleReflected.fontSize = 21;
            _styleReflected.fontStyle = FontStyle.Bold;
            _styleReflected.alignment = TextAnchor.MiddleCenter;
            _styleReflected.normal.textColor = reflectedDamageColor;

            _styleHeal = new GUIStyle();
            _styleHeal.fontSize = 20;
            _styleHeal.fontStyle = FontStyle.Bold;
            _styleHeal.alignment = TextAnchor.MiddleCenter;
            _styleHeal.normal.textColor = healColor;

            _shadowStyle = new GUIStyle(_styleNormal);
            _shadowStyle.normal.textColor = shadowColor;

            _stylesInitialized = true;
        }

        /// <summary>
        /// Hedef vurulduğunda 3D dünyada sıçrayan hasar sayısı oluşturur. Sıfır GC üretir.
        /// </summary>
        public void SpawnDamageNumber(Vector3 worldPos, float amount, bool isCrit, bool isReflected = false)
        {
            if (!showDamageNumbers) return;

            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % MaxPopups;

            _pool[index] = default;
            DamagePopupKind kind = DamagePopupLabelPolicy.Resolve(isCrit, isReflected);
            _pool[index].Kind = kind;
            _pool[index].Active = true;
            _pool[index].WorldPos = worldPos + Random.insideUnitSphere * 0.15f;
            _pool[index].Velocity = Vector3.up * upwardForce + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            _pool[index].Timer = popupLifetime;
            _pool[index].Amount = Mathf.RoundToInt(amount);
            _pool[index].IsCrit = kind == DamagePopupKind.Critical;
            _pool[index].IsReflected = kind == DamagePopupKind.Reflected;
            _pool[index].IsHeal = false;
            SetLabel(index, (int)kind);
        }

        /// <summary>
        /// Can küresi toplandığında zümrüt yeşili kinetik iyileşme sayısı oluşturur (+X HP). Sıfır GC üretir.
        /// </summary>
        public void SpawnHealNumber(Vector3 worldPos, float amount)
        {
            if (!showDamageNumbers) return;

            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % MaxPopups;

            _pool[index] = default;
            _pool[index].Kind = DamagePopupKind.Heal;
            _pool[index].Active = true;
            _pool[index].WorldPos = worldPos + Vector3.up * 0.4f + Random.insideUnitSphere * 0.1f;
            _pool[index].Velocity = Vector3.up * (upwardForce * 1.15f) + new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f));
            _pool[index].Timer = popupLifetime * 1.1f;
            _pool[index].Amount = Mathf.RoundToInt(amount);
            _pool[index].IsCrit = false;
            _pool[index].IsReflected = false;
            _pool[index].IsHeal = true;
            SetLabel(index, 3);
        }

        /// <summary>Shows the actual collected ammunition as +n OK or +n SAÇMA, without damage or heal flags.</summary>
        public void SpawnAmmoNumber(Vector3 worldPos, int amount, AmmoType type)
        {
            if (!showDamageNumbers || amount <= 0 || (type != AmmoType.Bolts && type != AmmoType.Shells)) return;
            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % MaxPopups;
            _pool[index] = default;
            _pool[index].Active = true;
            _pool[index].WorldPos = worldPos + Random.insideUnitSphere * 0.1f;
            _pool[index].Velocity = Vector3.up * upwardForce;
            _pool[index].Timer = popupLifetime;
            _pool[index].Amount = amount;
            _pool[index].Kind = DamagePopupLabelPolicy.ResolveAmmo(type);
            SetLabel(index, (int)_pool[index].Kind);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < MaxPopups; i++)
            {
                if (!_pool[i].Active) continue;

                _pool[i].Timer -= dt;
                if (_pool[i].Timer <= 0f)
                {
                    _pool[i].Active = false;
                    continue;
                }

                // Fiziksel süzülme ve yerçekimi
                Vector3 velocity = _pool[i].Velocity;
                velocity.y -= gravity * dt;
                _pool[i].Velocity = velocity;
                _pool[i].WorldPos += _pool[i].Velocity * dt;
            }
        }

        private void OnGUI()
        {
            if (!showDamageNumbers) return;

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            for (int i = 0; i < MaxPopups; i++)
            {
                if (!_pool[i].Active) continue;

                Vector3 screenPos = _cam.WorldToScreenPoint(_pool[i].WorldPos);

                // Kameranın arkasındaysa çizme
                if (screenPos.z <= 0f) continue;

                float screenY = Screen.height - screenPos.y;
                float progress = 1f - (_pool[i].Timer / popupLifetime);

                // Boyut ve solma (Scale pop & fade)
                float scale = _pool[i].Kind == DamagePopupKind.Critical ? Mathf.Lerp(1.4f, 1.0f, progress) : 1.0f;
                float alpha = Mathf.Clamp01(_pool[i].Timer / (popupLifetime * 0.35f));

                GUIStyle baseStyle;
                Color fontColor;

                if (_pool[i].Kind == DamagePopupKind.Heal)
                {
                    baseStyle = _styleHeal;
                    fontColor = healColor;
                }
                else if (_pool[i].Kind == DamagePopupKind.Critical)
                {
                    baseStyle = _styleCrit;
                    fontColor = critDamageColor;
                }
                else if (_pool[i].Kind == DamagePopupKind.Reflected)
                {
                    baseStyle = _styleReflected;
                    fontColor = reflectedDamageColor;
                }
                else
                {
                    baseStyle = _styleNormal;
                    fontColor = _pool[i].Kind == DamagePopupKind.Bolts ? boltsColor :
                        _pool[i].Kind == DamagePopupKind.Shells ? shellsColor : normalDamageColor;
                }

                fontColor.a = alpha;
                baseStyle.normal.textColor = fontColor;

                float w = 180f * scale;
                float h = 40f * scale;
                Rect rect = new Rect(screenPos.x - w * 0.5f, screenY - h * 0.5f, w, h);

                // Yüksek kontrastlı 4 yönlü damla gölge (Shadow Outline)
                Color sCol = shadowColor;
                sCol.a = alpha * 0.95f;
                _shadowStyle.fontSize = baseStyle.fontSize;
                _shadowStyle.normal.textColor = sCol;

                DrawLabel(new Rect(rect.x - 1, rect.y, rect.width, rect.height), i, _shadowStyle);
                DrawLabel(new Rect(rect.x + 1, rect.y, rect.width, rect.height), i, _shadowStyle);
                DrawLabel(new Rect(rect.x, rect.y - 1, rect.width, rect.height), i, _shadowStyle);
                DrawLabel(new Rect(rect.x, rect.y + 1, rect.width, rect.height), i, _shadowStyle);

                // Ana Sayı
                DrawLabel(rect, i, baseStyle);
            }
        }
    }
}
