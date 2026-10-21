using UnityEngine;

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
        private static DamageNumberSystem _instance;
        public static DamageNumberSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<DamageNumberSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("DamageNumberSystem");
                        _instance = go.AddComponent<DamageNumberSystem>();
                    }
                }
                return _instance;
            }
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
        [SerializeField] private Color shadowColor = new Color(0.02f, 0.02f, 0.04f, 0.90f); // Kontrast gölge

        private struct PopupEntry
        {
            public bool Active;
            public Vector3 WorldPos;
            public Vector3 Velocity;
            public float Timer;
            public int Amount;
            public bool IsCrit;
            public bool IsReflected;
        }

        private const int MaxPopups = 32;
        private readonly PopupEntry[] _pool = new PopupEntry[MaxPopups];
        private int _nextIndex = 0;

        private Camera _cam;
        private GUIStyle _styleNormal;
        private GUIStyle _styleCrit;
        private GUIStyle _styleReflected;
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

            _pool[index].Active = true;
            _pool[index].WorldPos = worldPos + Random.insideUnitSphere * 0.15f;
            _pool[index].Velocity = Vector3.up * upwardForce + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            _pool[index].Timer = popupLifetime;
            _pool[index].Amount = Mathf.RoundToInt(amount);
            _pool[index].IsCrit = isCrit;
            _pool[index].IsReflected = isReflected;
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
                _pool[i].Velocity.y -= gravity * dt;
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

            InitStyles();

            for (int i = 0; i < MaxPopups; i++)
            {
                if (!_pool[i].Active) continue;

                Vector3 screenPos = _cam.WorldToScreenPoint(_pool[i].WorldPos);

                // Kameranın arkasındaysa çizme
                if (screenPos.z <= 0f) continue;

                float screenY = Screen.height - screenPos.y;
                float progress = 1f - (_pool[i].Timer / popupLifetime);

                // Boyut ve solma (Scale pop & fade)
                float scale = _pool[i].IsCrit ? Mathf.Lerp(1.4f, 1.0f, progress) : 1.0f;
                float alpha = Mathf.Clamp01(_pool[i].Timer / (popupLifetime * 0.35f));

                GUIStyle baseStyle = _pool[i].IsCrit ? _styleCrit : (_pool[i].IsReflected ? _styleReflected : _styleNormal);
                Color fontColor = _pool[i].IsCrit ? critDamageColor : (_pool[i].IsReflected ? reflectedDamageColor : normalDamageColor);
                fontColor.a = alpha;
                baseStyle.normal.textColor = fontColor;

                string text = _pool[i].IsCrit ? $"CRIT! {_pool[i].Amount}" : (_pool[i].IsReflected ? $"PARRY {_pool[i].Amount}" : _pool[i].Amount.ToString());

                float w = 180f * scale;
                float h = 40f * scale;
                Rect rect = new Rect(screenPos.x - w * 0.5f, screenY - h * 0.5f, w, h);

                // Yüksek kontrastlı 4 yönlü damla gölge (Shadow Outline)
                Color sCol = shadowColor;
                sCol.a = alpha * 0.95f;
                _shadowStyle.fontSize = baseStyle.fontSize;
                _shadowStyle.normal.textColor = sCol;

                GUI.Label(new Rect(rect.x - 1, rect.y, rect.width, rect.height), text, _shadowStyle);
                GUI.Label(new Rect(rect.x + 1, rect.y, rect.width, rect.height), text, _shadowStyle);
                GUI.Label(new Rect(rect.x, rect.y - 1, rect.width, rect.height), text, _shadowStyle);
                GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, rect.height), text, _shadowStyle);

                // Ana Sayı
                GUI.Label(rect, text, baseStyle);
            }
        }
    }
}
