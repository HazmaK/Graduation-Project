using System.Text;
using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS
{
    /// <summary>
    /// Her ekran çözünürlüğünde (1080p, 2K, 4K, Tam Ekran veya pencere modu)
    /// kendini otomatik ölçekleyen (Dynamic GUI Matrix) ve sıfır GC çöpü üreten
    /// yüksek kontrastlı Dark Fantasy Retro HUD sistemi.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDebugHUD : MonoBehaviour
    {
        [Header("HUD Ayarları")]
        [SerializeField] private bool showHUD = true;
        [SerializeField] private bool showSpeedBar = true;

        private PlayerMovement _movement;
        private Camera _playerCamera;
        private WeaponController _weapons;
        private PlayerHealth _health;
        private PlayerParryController _parry;

        // Önbelleğe alınmış GUI Stilleri ve Dokular (Sıfır GC tahsisi için)
        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boldLabelStyle;
        private GUIStyle _stateStyle;
        private GUIStyle _controlsStyle;
        private Texture2D _bgTexture;
        private Texture2D _borderTexture;
        private Texture2D _barBgTexture;
        private Texture2D _barFillTexture;

        // Ölüm Ekranı Stilleri ve Dokuları
        private Texture2D _deathOverlayTexture;
        private Texture2D _deathCardTexture;
        private Texture2D _deathBorderTexture;
        private Texture2D _deathBtnTexture;
        private Texture2D _deathBtnHoverTexture;
        private GUIStyle _deathTitleStyle;
        private GUIStyle _deathSubStyle;
        private GUIStyle _deathBtnStyle;
        private GUIStyle _deathHintStyle;

        // Parry Başarı Bildirim Bannerı
        private float _parryBannerTimer = 0f;
        private bool _parryBannerIsProj = false;
        private Texture2D _parryBannerBgTexture;
        private GUIStyle _parryTitleStyle;
        private GUIStyle _parrySubStyle;

        // Hasar Alma Flaşı
        private float _hpFlashTimer = 0f;

        private bool _stylesInitialized = false;

        // GC Çöpünü Önlemek İçin Önbelleğe Alınmış Sabit Metinler ve Dizi Tabloları
        private static readonly string[] DashDisplayCache = new string[]
        {
            "DASH: <color=#555555>◇ ◇ ◇</color> <size=11>(0/3)</size>",
            "DASH: <color=#00E5FF>◆</color> <color=#555555>◇ ◇</color> <size=11>(1/3)</size>",
            "DASH: <color=#00E5FF>◆ ◆</color> <color=#555555>◇</color> <size=11>(2/3)</size>",
            "DASH: <color=#00E5FF>◆ ◆ ◆</color> <size=11>(3/3)</size>"
        };

        private const string LabelWepArbalest = "WEAPON: <b><color=#00E5FF>[1] REPEATER ARBALEST</color></b>";
        private const string LabelWepBlade = "WEAPON: <b><color=#FF5533>[2] EXECUTIONER BLADE</color></b>";

        private const string StateGround = "STATE: <color=#AAAAAA>GROUND</color>";
        private const string StateDash = "STATE: <color=#00E5FF>DASHING (I-FRAME)</color>";
        private const string StateSlide = "STATE: <color=#FF9900>SLIDING</color>";
        private const string StateCrouch = "STATE: <color=#FFCC00>CROUCH WALK</color>";
        private const string StateAir = "STATE: <color=#55FF55>AIR (BHOP)</color>";

        private readonly StringBuilder _sb = new StringBuilder(128);

        private void Awake()
        {
            if (GetComponent<Camera>() != null)
            {
                Destroy(this);
                return;
            }
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (_parry != null)
            {
                _parry.OnParrySuccess -= HandleParrySuccess;
            }

            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void Update()
        {
            if (_parryBannerTimer > 0f)
            {
                _parryBannerTimer -= Time.deltaTime;
            }

            if (_hpFlashTimer > 0f)
            {
                _hpFlashTimer -= Time.deltaTime;
            }
        }

        private void HandleParrySuccess(bool isProjectile)
        {
            _parryBannerTimer = 0.75f;
            _parryBannerIsProj = isProjectile;
        }

        private void HandleDamageTaken(float damage)
        {
            _hpFlashTimer = 0.22f;
        }

        private void ResolveReferences()
        {
            if (_movement == null)
            {
                _movement = GetComponent<PlayerMovement>();
                if (_movement == null)
                    _movement = Object.FindAnyObjectByType<PlayerMovement>();
            }

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
                if (_playerCamera == null)
                    _playerCamera = Object.FindAnyObjectByType<Camera>();
            }

            if (_weapons == null)
            {
                _weapons = GetComponent<WeaponController>();
                if (_weapons == null)
                    _weapons = Object.FindAnyObjectByType<WeaponController>();
            }

            if (_health == null)
            {
                _health = GetComponent<PlayerHealth>();
                if (_health == null)
                    _health = Object.FindAnyObjectByType<PlayerHealth>();

                if (_health != null)
                {
                    _health.OnDamageTaken -= HandleDamageTaken;
                    _health.OnDamageTaken += HandleDamageTaken;
                }
            }

            if (_parry == null)
            {
                _parry = GetComponent<PlayerParryController>();
                if (_parry == null)
                    _parry = Object.FindAnyObjectByType<PlayerParryController>();

                if (_parry != null)
                {
                    _parry.OnParrySuccess -= HandleParrySuccess;
                    _parry.OnParrySuccess += HandleParrySuccess;
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized && _bgTexture != null) return;

            // Yüksek kontrastlı gotik arayüz dokuları
            _bgTexture = MakeSolidTexture(1, 1, new Color(0.06f, 0.07f, 0.09f, 0.92f));
            _borderTexture = MakeSolidTexture(1, 1, new Color(0.85f, 0.65f, 0.20f, 0.85f)); // Gotik Altın Çerçeve
            _barBgTexture = MakeSolidTexture(1, 1, new Color(0.15f, 0.16f, 0.20f, 0.90f));
            _barFillTexture = MakeSolidTexture(1, 1, Color.white);

            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = _bgTexture;
            _panelStyle.padding = new RectOffset(14, 14, 12, 12);

            _headerStyle = new GUIStyle();
            _headerStyle.fontSize = 13;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(0.95f, 0.75f, 0.25f, 1f); // Altın sarısı
            _headerStyle.margin = new RectOffset(0, 0, 0, 4);

            _labelStyle = new GUIStyle();
            _labelStyle.fontSize = 13;
            _labelStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f, 1f);
            _labelStyle.richText = true;

            _boldLabelStyle = new GUIStyle(_labelStyle);
            _boldLabelStyle.fontStyle = FontStyle.Bold;

            _stateStyle = new GUIStyle(_labelStyle);
            _stateStyle.fontStyle = FontStyle.Bold;

            _controlsStyle = new GUIStyle();
            _controlsStyle.fontSize = 10;
            _controlsStyle.normal.textColor = new Color(0.60f, 0.65f, 0.70f, 1f);
            _controlsStyle.richText = true;

            // Ölüm Ekranı Dokuları ve Stilleri
            if (_deathOverlayTexture == null)
            {
                _deathOverlayTexture = MakeSolidTexture(1, 1, new Color(0.08f, 0.01f, 0.02f, 0.85f));
                _deathCardTexture = MakeSolidTexture(1, 1, new Color(0.04f, 0.04f, 0.06f, 0.98f));
                _deathBorderTexture = MakeSolidTexture(1, 1, new Color(0.85f, 0.15f, 0.15f, 0.95f));
                _deathBtnTexture = MakeSolidTexture(1, 1, new Color(0.50f, 0.08f, 0.10f, 1f));
                _deathBtnHoverTexture = MakeSolidTexture(1, 1, new Color(0.80f, 0.16f, 0.16f, 1f));

                _deathTitleStyle = new GUIStyle();
                _deathTitleStyle.fontSize = 28;
                _deathTitleStyle.fontStyle = FontStyle.Bold;
                _deathTitleStyle.alignment = TextAnchor.MiddleCenter;
                _deathTitleStyle.normal.textColor = new Color(1f, 0.22f, 0.22f, 1f);

                _deathSubStyle = new GUIStyle();
                _deathSubStyle.fontSize = 13;
                _deathSubStyle.alignment = TextAnchor.MiddleCenter;
                _deathSubStyle.normal.textColor = new Color(0.80f, 0.82f, 0.85f, 1f);

                _deathBtnStyle = new GUIStyle();
                _deathBtnStyle.fontSize = 15;
                _deathBtnStyle.fontStyle = FontStyle.Bold;
                _deathBtnStyle.alignment = TextAnchor.MiddleCenter;
                _deathBtnStyle.normal.textColor = Color.white;
                _deathBtnStyle.hover.textColor = new Color(1f, 0.95f, 0.45f, 1f);
                _deathBtnStyle.normal.background = _deathBtnTexture;
                _deathBtnStyle.hover.background = _deathBtnHoverTexture;
                _deathBtnStyle.active.background = _deathBtnHoverTexture;
                _deathBtnStyle.border = new RectOffset(0, 0, 0, 0);
                _deathBtnStyle.padding = new RectOffset(10, 10, 8, 8);

                _deathHintStyle = new GUIStyle();
                _deathHintStyle.fontSize = 11;
                _deathHintStyle.alignment = TextAnchor.MiddleCenter;
                _deathHintStyle.normal.textColor = new Color(0.65f, 0.68f, 0.72f, 1f);
            }

            // Parry Banner Dokusu ve Stilleri
            if (_parryBannerBgTexture == null)
            {
                _parryBannerBgTexture = MakeSolidTexture(1, 1, new Color(0.04f, 0.04f, 0.06f, 0.90f));

                _parryTitleStyle = new GUIStyle();
                _parryTitleStyle.fontSize = 28;
                _parryTitleStyle.fontStyle = FontStyle.Bold;
                _parryTitleStyle.alignment = TextAnchor.MiddleCenter;
                _parryTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.25f, 1f); // Altın Sarısı

                _parrySubStyle = new GUIStyle();
                _parrySubStyle.fontSize = 13;
                _parrySubStyle.fontStyle = FontStyle.Bold;
                _parrySubStyle.alignment = TextAnchor.MiddleCenter;
                _parrySubStyle.normal.textColor = new Color(0.2f, 0.9f, 1f, 1f); // Cyan
            }

            _stylesInitialized = true;
        }

        private Texture2D MakeSolidTexture(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.hideFlags = HideFlags.DontSave;
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            if (!showHUD) return;
            if (_movement == null || _health == null) ResolveReferences();

            InitStyles();

            // Ekran ölçekleme (Resolution-Independent Matrix):
            // 1080p referans alınır. 4K, 2K veya küçük pencerelerde kendini orantısal ölçekler.
            float screenH = Screen.height;
            float screenW = Screen.width;
            if (screenH < 100f || screenW < 100f) return;

            // 1080p baz alınarak ölçek katsayısı hesaplanır (0.80x ile 2.5x arası)
            float guiScale = Mathf.Clamp(screenH / 1080f, 0.80f, 2.5f);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));

            // GUI render derinliği: En üstte görünmesi garanti edilir
            GUI.depth = -100;

            float virtualW = screenW / guiScale;
            float virtualH = screenH / guiScale;

            // 1. ÖLÜM EKRANI VE RESTART BUTONU
            if (_health != null && _health.IsDead)
            {
                DrawDeathScreen(virtualW, virtualH);
                GUI.matrix = oldMatrix;
                return;
            }

            if (_movement == null)
            {
                GUI.matrix = oldMatrix;
                return;
            }

            // Sanal koordinatlarda panel boyutu (1080p bazında)
            float panelWidth = 330f;
            float panelHeight = showSpeedBar ? 270f : 235f;
            Rect panelRect = new Rect(20f, 20f, panelWidth, panelHeight);

            // 1. Altın çerçeve sınırı
            GUI.DrawTexture(new Rect(panelRect.x - 2, panelRect.y - 2, panelRect.width + 4, panelRect.height + 4), _borderTexture);
            // 2. Panel arka plan gövdesi
            GUI.DrawTexture(panelRect, _bgTexture);

            // 3. İçerik Alanı
            GUILayout.BeginArea(new Rect(panelRect.x + 12, panelRect.y + 10, panelRect.width - 24, panelRect.height - 20));

            // Başlık
            GUILayout.Label("RETRO FPS // PROTOTYPE HUD", _headerStyle);

            // Can (Health) Barı ve Değeri
            if (_health != null)
            {
                float hp = _health.CurrentHealth;
                float maxHp = _health.MaxHealth;
                float hpRatio = Mathf.Clamp01(hp / maxHp);
                bool isFlashing = _hpFlashTimer > 0f;
                string hpColor = isFlashing ? "#FFFFFF" : (hp > 50f ? "#55FF55" : (hp > 25f ? "#FFAA00" : "#FF3333"));
                GUILayout.Label($"HEALTH: <color={hpColor}><b>{(int)hp} / {(int)maxHp} HP</b></color>", _labelStyle);

                Rect hpBarRect = GUILayoutUtility.GetRect(panelWidth - 24, 6f);
                GUI.DrawTexture(hpBarRect, _barBgTexture);
                Color hpBarColor = isFlashing ? Color.white : (hp > 50f ? new Color(0.2f, 0.95f, 0.35f) : (hp > 25f ? new Color(1f, 0.7f, 0.15f) : new Color(0.95f, 0.2f, 0.2f)));
                GUI.color = hpBarColor;
                GUI.DrawTexture(new Rect(hpBarRect.x, hpBarRect.y, hpBarRect.width * hpRatio, hpBarRect.height), _barFillTexture);
                GUI.color = Color.white;
            }

            // Aktif Silah (Önbellekli sıfır tahsis)
            if (_weapons != null)
            {
                string wepLabel = _weapons.CurrentSlot == WeaponSlot.RepeaterArbalest ? LabelWepArbalest : LabelWepBlade;
                GUILayout.Label(wepLabel, _labelStyle);
            }

            // Sol El Savunma / Parry Durumu
            if (_parry != null)
            {
                if (_parry.IsParrying)
                {
                    GUILayout.Label("DEFENSE [Q]: <color=#00E5FF><b>PARRY ACTIVE!</b></color>", _labelStyle);
                }
                else if (_parry.IsOnCooldown)
                {
                    GUILayout.Label($"DEFENSE [Q]: <color=#FFAA00>COOLDOWN ({_parry.CooldownRemaining:F1}s)</color>", _labelStyle);
                }
                else
                {
                    GUILayout.Label("DEFENSE [Q]: <color=#55FF55><b>SPIKED BUCKLER (READY)</b></color>", _labelStyle);
                }
            }

            // Hız Değeri
            float speed = _movement.CurrentHorizontalSpeed;
            string speedColor = speed > 11.5f ? "#FFAA00" : (speed > 8.0f ? "#55FF55" : "#FFFFFF");
            _sb.Clear();
            _sb.Append("SPEED: <color=").Append(speedColor).Append("><b>").Append(speed.ToString("F1")).Append(" m/s</b></color>");
            GUILayout.Label(_sb.ToString(), _labelStyle);

            // Görsel Hız Barı
            if (showSpeedBar)
            {
                Rect barRect = GUILayoutUtility.GetRect(panelWidth - 24, 7f);
                GUI.DrawTexture(barRect, _barBgTexture);
                float fillRatio = Mathf.Clamp01(speed / 16f);
                Color barColor = speed > 11.5f ? new Color(1f, 0.65f, 0.1f) : new Color(0.2f, 0.8f, 1f);
                GUI.color = barColor;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * fillRatio, barRect.height), _barFillTexture);
                GUI.color = Color.white;
            }

            // Göz Hizası (Gerçek Dünya Kamerası Yüksekliği)
            float eyeHeight = _playerCamera != null ? _playerCamera.transform.position.y : 0f;
            _sb.Clear();
            _sb.Append("EYE HEIGHT: <b>").Append(eyeHeight.ToString("F2")).Append(" m</b> <size=10>(Manken: 1.80m)</size>");
            GUILayout.Label(_sb.ToString(), _labelStyle);

            // Dash Şarj Göstergesi (Önbellekli sıfır tahsis tablosu)
            int charges = Mathf.Clamp(_movement.CurrentDashCharges, 0, 3);
            GUILayout.Label(DashDisplayCache[charges], _labelStyle);

            // Hareket Durumu (Önbellekli sabit metinler)
            string stateLabel = StateGround;
            if (_movement.IsDashing) stateLabel = StateDash;
            else if (_movement.IsSliding) stateLabel = StateSlide;
            else if (_movement.IsCrouching) stateLabel = StateCrouch;
            else if (!_movement.IsGrounded) stateLabel = StateAir;

            GUILayout.Label(stateLabel, _stateStyle);

            // Tuş İpuçları
            GUILayout.Label("<size=10>L-CLICK: Ates/Kes | R-CLICK: Alt-Fire | Q/M-CLICK: Parry\n1-2: Silah | SPACE: Zipla/Bhop | SHIFT: Dash | CTRL: Slide</size>", _controlsStyle);

            GUILayout.EndArea();

            // 4. PARRY BAŞARI BANNERI (Sekiro / Ultrakill Tarzı Anlık Bildirim)
            if (_parryBannerTimer > 0f)
            {
                float bannerW = 440f;
                float bannerH = 72f;
                float bannerX = (virtualW - bannerW) * 0.5f;
                float bannerY = virtualH * 0.24f;
                Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);

                // Altın çerçeve ve koyu arka plan
                GUI.DrawTexture(new Rect(bannerRect.x - 2f, bannerRect.y - 2f, bannerRect.width + 4f, bannerRect.height + 4f), _borderTexture);
                GUI.DrawTexture(bannerRect, _parryBannerBgTexture);

                GUILayout.BeginArea(new Rect(bannerRect.x, bannerRect.y + 8f, bannerRect.width, bannerRect.height - 16f));
                GUILayout.Label("⚡ PARRY! ⚡", _parryTitleStyle);
                string subText = _parryBannerIsProj ? "[MERMİ GERİ YANSITILDI!]" : "[DÜŞMAN SERSEMLETİLDİ — ŞİMDİ VUR!]";
                GUILayout.Label(subText, _parrySubStyle);
                GUILayout.EndArea();
            }

            // Matrisi eski haline geri yükle
            GUI.matrix = oldMatrix;
        }

        private void DrawDeathScreen(float virtualW, float virtualH)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 1. Ekranı kaplayan yarı saydam karanlık/kan perdesi
            GUI.DrawTexture(new Rect(0f, 0f, virtualW, virtualH), _deathOverlayTexture);

            // 2. Ortalanmış Kart Boyutları (1080p sanal matrisinde)
            float cardW = 540f;
            float cardH = 280f;
            float cardX = (virtualW - cardW) * 0.5f;
            float cardY = (virtualH - cardH) * 0.5f;
            Rect cardRect = new Rect(cardX, cardY, cardW, cardH);

            // 3. Kart Çerçevesi (Gotik Kan Kırmızısı)
            GUI.DrawTexture(new Rect(cardRect.x - 3f, cardRect.y - 3f, cardRect.width + 6f, cardRect.height + 6f), _deathBorderTexture);

            // 4. Kart Arka Planı
            GUI.DrawTexture(cardRect, _deathCardTexture);

            // 5. Kart İçeriği
            GUILayout.BeginArea(new Rect(cardRect.x + 20f, cardRect.y + 24f, cardRect.width - 40f, cardRect.height - 48f));

            GUILayout.Label("ÖLDÜN // YOU DIED", _deathTitleStyle);
            GUILayout.Space(8f);

            GUILayout.Label("Karanlık seni yuttu. Bedenin yere serildi.\nRuhun kadim zindanın dehlizlerinde kayboldu.", _deathSubStyle);
            GUILayout.Space(24f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⚔️  YENİDEN BAŞLAT // RESTART", _deathBtnStyle, GUILayout.Width(340f), GUILayout.Height(52f)))
            {
                if (_health != null)
                {
                    _health.RestartGame();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.Label("Kısayol: [R] veya [BOŞLUK] tuşuna basarak da hemen yeniden başlayabilirsin", _deathHintStyle);

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null) Destroy(_bgTexture);
            if (_borderTexture != null) Destroy(_borderTexture);
            if (_barBgTexture != null) Destroy(_barBgTexture);
            if (_barFillTexture != null) Destroy(_barFillTexture);
            if (_deathOverlayTexture != null) Destroy(_deathOverlayTexture);
            if (_deathCardTexture != null) Destroy(_deathCardTexture);
            if (_deathBorderTexture != null) Destroy(_deathBorderTexture);
            if (_deathBtnTexture != null) Destroy(_deathBtnTexture);
            if (_deathBtnHoverTexture != null) Destroy(_deathBtnHoverTexture);
            if (_parryBannerBgTexture != null) Destroy(_parryBannerBgTexture);
        }
    }
}
