using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using RetroFPS.Weapons;
using RetroFPS.Combat;

namespace RetroFPS
{
    /// <summary>
    /// Modüler geliştirici telemetri bölümü arayüzü.
    /// Gelecekte oyuna eklenecek yeni sistemler (AI, Boss, Envanter, Ses vb.)
    /// bu arayüzü uygulayarak veya DebugHUD üzerinden kayıt olarak doğrudan bu HUD'a veri basabilir.
    /// </summary>
    public interface IDebugHUDSection
    {
        string SectionTitle { get; }
        bool IsEnabled { get; set; }
        void DrawSection(StringBuilder sb, GUIStyle headerStyle, GUIStyle labelStyle, float contentWidth);
    }

    /// <summary>
    /// Harici sistemlerin tek satırda HUD'a metrik basabilmesi için sıfır GC yapılı veri yapısı.
    /// </summary>
    public struct DebugMetricEntry
    {
        public string Key;
        public string Value;

        public DebugMetricEntry(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// Tek Çatı Altında Birleştirilmiş Dark Fantasy / Retro Developer Debug HUD.
    /// - F2 tuşuyla anında açılıp kapanır (Unity 6 New Input System).
    /// - Oyuncu telemetrisi (Can barı, Hız barı, Silah, Buckler Parry, Dash, State, Göz boyu).
    /// - Dalga & Karşılaşma telemetrisi (Dalga sayısı, Kalan düşmanlar, Sahada/Kuyrukta dağılım, Süre sayacı ve barı, Eşikler).
    /// - Dinamik Geleceğe Dönük Eklenti Mimarisi (IDebugHUDSection, SetMetric, RegisterCallback).
    /// - Sıfır GC Çöpü (Pre-allocated StringBuilder, önbellek dokuları ve stiller).
    /// - Çözünürlük Bağımsız Dinamik GUI Matrisi (1080p bazlı otomatik ölçekleme).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RetroFPS/Developer Debug HUD (Unified F2)")]
    public class PlayerDebugHUD : MonoBehaviour
    {
        public static PlayerDebugHUD Instance { get; private set; }

        [Header("Görünüm ve Tuş Ayarları")]
        [Tooltip("HUD başlangıçta açık mı olsun?")]
        [SerializeField] private bool showHUD = true;
        [Tooltip("F2 tuşu ile açma/kapama yapılsın mı?")]
        [SerializeField] private bool enableF2Toggle = true;
        [Tooltip("Görsel hız gösterge barı gösterilsin mi?")]
        [SerializeField] private bool showSpeedBar = true;
        [Tooltip("Dalga ve karşılaşma telemetrisi gösterilsin mi?")]
        [SerializeField] private bool showWaveTelemetry = true;
        [Tooltip("Harici sistemlerden eklenen modüler metrikler gösterilsin mi?")]
        [SerializeField] private bool showCustomTelemetry = true;

        [Header("Karşılaşma Direktörü Bağlantısı")]
        [Tooltip("Bağlanılacak WaveSpawnDirector. Boş ise sahnede otomatik aranır.")]
        [SerializeField] private WaveSpawnDirector waveDirector;

        public bool IsVisible => showHUD;

        // Önbelleğe Alınmış Bileşen Referansları
        private PlayerMovement _movement;
        private Camera _playerCamera;
        private WeaponController _weapons;
        private PlayerHealth _health;
        private PlayerParryController _parry;
        private WaveSpawnDirector _waveDirector;

        // Performans ve FPS Ölçüm Sayaçları
        private float _fpsUpdateTimer = 0f;
        private int _fpsFrameCount = 0;
        private float _currentFps = 60f;
        private float _currentFrameMs = 16.6f;

        // Banner Zamanlayıcıları
        private float _parryBannerTimer = 0f;
        private bool _parryBannerIsProj = false;
        private float _waveBannerTimer = 0f;
        private string _waveBannerTitle = string.Empty;
        private string _waveBannerSub = string.Empty;
        private Color _waveBannerColor = Color.white;
        private float _hpFlashTimer = 0f;

        // Panel Yükseklik Takibi (Sıfır Titreme ve Sıfır GC)
        private float _lastPanelHeight = 480f;

        // Önbelleğe Alınmış GUI Stilleri ve Dokular (Sıfır GC)
        private bool _stylesInitialized = false;
        private Texture2D _panelBgTex;
        private Texture2D _borderGoldTex;
        private Texture2D _borderRedTex;
        private Texture2D _borderCyanTex;
        private Texture2D _dividerTex;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;
        private Texture2D _parryBannerBgTex;
        private Texture2D _waveBannerBgTex;

        // Ölüm Ekranı Dokuları
        private Texture2D _deathOverlayTex;
        private Texture2D _deathCardTex;
        private Texture2D _deathBorderTex;
        private Texture2D _deathBtnTex;
        private Texture2D _deathBtnHoverTex;

        // GUI Stilleri
        private GUIStyle _mainHeaderStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _waveSectionHeaderStyle;
        private GUIStyle _customSectionHeaderStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boldLabelStyle;
        private GUIStyle _stateStyle;
        private GUIStyle _controlsStyle;
        private GUIStyle _watermarkStyle;
        private GUIStyle _watermarkBgStyle;
        private GUIStyle _parryTitleStyle;
        private GUIStyle _parrySubStyle;
        private GUIStyle _waveBannerTitleStyle;
        private GUIStyle _waveBannerSubStyle;
        private GUIStyle _deathTitleStyle;
        private GUIStyle _deathSubStyle;
        private GUIStyle _deathBtnStyle;
        private GUIStyle _deathHintStyle;

        // Sabit Metin ve Dizi Önbellekleri (Sıfır GC Tahsisi)
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

        private readonly StringBuilder _sb = new StringBuilder(512);

        // =========================================================================
        // GELECEĞE DÖNÜK MODÜLER TELEMETRİ VERİ HAVUZLARI
        // =========================================================================
        private static readonly List<IDebugHUDSection> CustomSections = new List<IDebugHUDSection>(8);
        private static readonly Dictionary<string, Action<StringBuilder>> CustomCallbacks = new Dictionary<string, Action<StringBuilder>>(8);
        private static readonly Dictionary<string, List<DebugMetricEntry>> CategorizedMetrics = new Dictionary<string, List<DebugMetricEntry>>(8);

        public static void RegisterSection(IDebugHUDSection section)
        {
            if (section != null && !CustomSections.Contains(section))
            {
                CustomSections.Add(section);
            }
        }

        public static void UnregisterSection(IDebugHUDSection section)
        {
            if (section != null) CustomSections.Remove(section);
        }

        public static void RegisterCallback(string category, Action<StringBuilder> callback)
        {
            if (!string.IsNullOrEmpty(category) && callback != null)
            {
                CustomCallbacks[category] = callback;
            }
        }

        public static void UnregisterCallback(string category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                CustomCallbacks.Remove(category);
            }
        }

        public static void SetMetric(string category, string key, string value)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key)) return;

            if (!CategorizedMetrics.TryGetValue(category, out var list))
            {
                list = new List<DebugMetricEntry>(4);
                CategorizedMetrics[category] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == key)
                {
                    list[i] = new DebugMetricEntry(key, value);
                    return;
                }
            }

            list.Add(new DebugMetricEntry(key, value));
        }

        public static void SetMetric(string category, string key, float value, string format = "F2")
        {
            SetMetric(category, key, value.ToString(format));
        }

        public static void RemoveMetric(string category, string key)
        {
            if (CategorizedMetrics.TryGetValue(category, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Key == key)
                    {
                        list.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        public static void ClearCategory(string category)
        {
            if (CategorizedMetrics.TryGetValue(category, out var list))
            {
                list.Clear();
            }
        }

        public void ToggleHUD()
        {
            showHUD = !showHUD;
        }

        private void Awake()
        {
            Instance = this;
            if (GetComponent<Camera>() != null)
            {
                Destroy(this);
                return;
            }
            ResolveReferences();
        }

        private void OnEnable()
        {
            Instance = this;
            ResolveReferences();
            SubscribeEvents();
            WaveSpawnDirector.OnActiveDirectorChanged -= HandleActiveDirectorChanged;
            WaveSpawnDirector.OnActiveDirectorChanged += HandleActiveDirectorChanged;
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            WaveSpawnDirector.OnActiveDirectorChanged -= HandleActiveDirectorChanged;
            if (Instance == this) Instance = null;
        }

        private void HandleActiveDirectorChanged(WaveSpawnDirector newDirector)
        {
            if (_waveDirector == newDirector || newDirector == null) return;
            UnsubscribeWaveDirectorEvents();
            _waveDirector = newDirector;
            SubscribeWaveDirectorEvents();
        }

        private void Update()
        {
            // F2 Tuşu ile Dinamik HUD Açma/Kapatma (Unity 6 New Input System)
            if (enableF2Toggle && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                ToggleHUD();
            }

            // B Tuşu ile Karşılaşmayı Başlatma (STANDBY durumundayken hızlı geliştirici kısayolu)
            if (_waveDirector != null && _waveDirector.State == EncounterState.Inactive)
            {
                if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                {
                    _waveDirector.StartEncounter();
                }
            }

            // FPS ve Çerçeve Süresi Ölçümü
            _fpsFrameCount++;
            _fpsUpdateTimer += Time.unscaledDeltaTime;
            if (_fpsUpdateTimer >= 0.25f)
            {
                _currentFps = _fpsFrameCount / _fpsUpdateTimer;
                _currentFrameMs = (_fpsUpdateTimer / _fpsFrameCount) * 1000f;
                _fpsFrameCount = 0;
                _fpsUpdateTimer = 0f;
            }

            // Geri sayım bannerları
            if (_parryBannerTimer > 0f) _parryBannerTimer -= Time.deltaTime;
            if (_hpFlashTimer > 0f) _hpFlashTimer -= Time.deltaTime;
            if (_waveBannerTimer > 0f) _waveBannerTimer -= Time.deltaTime;
        }

        private void ResolveReferences()
        {
            if (_movement == null)
            {
                _movement = GetComponent<PlayerMovement>();
                if (_movement == null) _movement = FindAnyObjectByType<PlayerMovement>();
            }

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
                if (_playerCamera == null) _playerCamera = FindAnyObjectByType<Camera>();
            }

            if (_weapons == null)
            {
                _weapons = GetComponent<WeaponController>();
                if (_weapons == null) _weapons = FindAnyObjectByType<WeaponController>();
            }

            if (_health == null)
            {
                _health = GetComponent<PlayerHealth>();
                if (_health == null) _health = FindAnyObjectByType<PlayerHealth>();
            }

            if (_parry == null)
            {
                _parry = GetComponent<PlayerParryController>();
                if (_parry == null) _parry = FindAnyObjectByType<PlayerParryController>();
            }

            if (_waveDirector == null)
            {
                _waveDirector = waveDirector != null ? waveDirector : FindAnyObjectByType<WaveSpawnDirector>();
            }
        }

        private void SubscribeEvents()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
                _health.OnDamageTaken += HandleDamageTaken;
            }

            if (_parry != null)
            {
                _parry.OnParrySuccess -= HandleParrySuccess;
                _parry.OnParrySuccess += HandleParrySuccess;
            }

            SubscribeWaveDirectorEvents();
        }

        private void SubscribeWaveDirectorEvents()
        {
            if (_waveDirector != null)
            {
                _waveDirector.OnEncounterStarted -= HandleEncounterStarted;
                _waveDirector.OnEncounterStarted += HandleEncounterStarted;
                _waveDirector.OnWaveStarted -= HandleWaveStarted;
                _waveDirector.OnWaveStarted += HandleWaveStarted;
                _waveDirector.OnWaveCompleted -= HandleWaveCompleted;
                _waveDirector.OnWaveCompleted += HandleWaveCompleted;
                _waveDirector.OnEncounterCompleted -= HandleEncounterCompleted;
                _waveDirector.OnEncounterCompleted += HandleEncounterCompleted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_health != null) _health.OnDamageTaken -= HandleDamageTaken;
            if (_parry != null) _parry.OnParrySuccess -= HandleParrySuccess;
            UnsubscribeWaveDirectorEvents();
        }

        private void UnsubscribeWaveDirectorEvents()
        {
            if (_waveDirector != null)
            {
                _waveDirector.OnEncounterStarted -= HandleEncounterStarted;
                _waveDirector.OnWaveStarted -= HandleWaveStarted;
                _waveDirector.OnWaveCompleted -= HandleWaveCompleted;
                _waveDirector.OnEncounterCompleted -= HandleEncounterCompleted;
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

        private void HandleEncounterStarted()
        {
            ShowWaveBanner("ARENA SEALED", "PURGE ALL HOSTILES!", 2.5f, new Color(0.95f, 0.20f, 0.15f, 1f));
        }

        private void HandleWaveStarted(int waveIndex, int totalWaves, string waveName)
        {
            _sb.Clear();
            _sb.Append("WAVE ").Append(waveIndex).Append(" / ").Append(totalWaves);
            ShowWaveBanner(_sb.ToString(), waveName, 2.2f, new Color(1.0f, 0.88f, 0.35f, 1f));
        }

        private void HandleWaveCompleted(int completedWaveIndex)
        {
            _sb.Clear();
            _sb.Append("WAVE ").Append(completedWaveIndex).Append(" CLEARED!");
            ShowWaveBanner(_sb.ToString(), "PREPARE FOR THE NEXT WAVE...", 2.0f, new Color(0.35f, 1.0f, 0.45f, 1f));
        }

        private void HandleEncounterCompleted()
        {
            ShowWaveBanner("ARENA CLEARED!", "BARRIERS LIFTED - VICTORY!", 3.5f, new Color(1.0f, 0.88f, 0.35f, 1f));
        }

        private void ShowWaveBanner(string title, string sub, float duration, Color color)
        {
            _waveBannerTitle = title;
            _waveBannerSub = sub;
            _waveBannerTimer = duration;
            _waveBannerColor = color;
        }

        private void InitStyles()
        {
            if (_stylesInitialized && _panelBgTex != null) return;

            // Gotik / Cyber-Dark Dokular
            _panelBgTex = MakeSolidTexture(1, 1, new Color(0.05f, 0.06f, 0.08f, 0.94f));
            _borderGoldTex = MakeSolidTexture(1, 1, new Color(0.85f, 0.65f, 0.20f, 0.90f));
            _borderRedTex = MakeSolidTexture(1, 1, new Color(0.92f, 0.20f, 0.18f, 0.90f));
            _borderCyanTex = MakeSolidTexture(1, 1, new Color(0.00f, 0.85f, 1.00f, 0.90f));
            _dividerTex = MakeSolidTexture(1, 1, new Color(0.85f, 0.65f, 0.20f, 0.25f));
            _barBgTex = MakeSolidTexture(1, 1, new Color(0.12f, 0.13f, 0.16f, 0.95f));
            _barFillTex = MakeSolidTexture(1, 1, Color.white);
            _parryBannerBgTex = MakeSolidTexture(1, 1, new Color(0.04f, 0.04f, 0.06f, 0.92f));
            _waveBannerBgTex = MakeSolidTexture(1, 1, new Color(0.04f, 0.04f, 0.06f, 0.94f));

            // Başlıklar
            _mainHeaderStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _mainHeaderStyle.normal.textColor = new Color(0.95f, 0.75f, 0.25f, 1f);

            _subHeaderStyle = new GUIStyle
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            _subHeaderStyle.normal.textColor = new Color(0.65f, 0.70f, 0.75f, 1f);

            _sectionHeaderStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _sectionHeaderStyle.normal.textColor = new Color(0.95f, 0.80f, 0.35f, 1f);

            _waveSectionHeaderStyle = new GUIStyle(_sectionHeaderStyle);
            _waveSectionHeaderStyle.normal.textColor = new Color(1.0f, 0.40f, 0.35f, 1f);

            _customSectionHeaderStyle = new GUIStyle(_sectionHeaderStyle);
            _customSectionHeaderStyle.normal.textColor = new Color(0.20f, 0.85f, 1.0f, 1f);

            // Metinler
            _labelStyle = new GUIStyle
            {
                fontSize = 12,
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f, 1f);

            _boldLabelStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };
            _stateStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };

            _controlsStyle = new GUIStyle
            {
                fontSize = 10,
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };
            _controlsStyle.normal.textColor = new Color(0.60f, 0.65f, 0.70f, 1f);

            // Watermark (Kapalıyken gösterilen F2 bildirim hapı)
            _watermarkStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _watermarkStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.80f);

            _watermarkBgStyle = new GUIStyle
            {
                normal = { background = _panelBgTex },
                padding = new RectOffset(8, 8, 4, 4)
            };

            // Bannerlar
            _parryTitleStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _parryTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.25f, 1f);

            _parrySubStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _parrySubStyle.normal.textColor = new Color(0.2f, 0.9f, 1f, 1f);

            _waveBannerTitleStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _waveBannerSubStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _waveBannerSubStyle.normal.textColor = new Color(0.92f, 0.92f, 0.95f, 1f);

            // Ölüm Ekranı
            _deathOverlayTex = MakeSolidTexture(1, 1, new Color(0.08f, 0.01f, 0.02f, 0.88f));
            _deathCardTex = MakeSolidTexture(1, 1, new Color(0.04f, 0.04f, 0.06f, 0.98f));
            _deathBorderTex = MakeSolidTexture(1, 1, new Color(0.85f, 0.15f, 0.15f, 0.95f));
            _deathBtnTex = MakeSolidTexture(1, 1, new Color(0.50f, 0.08f, 0.10f, 1f));
            _deathBtnHoverTex = MakeSolidTexture(1, 1, new Color(0.80f, 0.16f, 0.16f, 1f));

            _deathTitleStyle = new GUIStyle
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _deathTitleStyle.normal.textColor = new Color(1f, 0.20f, 0.20f, 1f);

            _deathSubStyle = new GUIStyle
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            _deathSubStyle.normal.textColor = new Color(0.80f, 0.82f, 0.85f, 1f);

            _deathBtnStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(10, 10, 8, 8)
            };
            _deathBtnStyle.normal.textColor = Color.white;
            _deathBtnStyle.hover.textColor = new Color(1f, 0.95f, 0.45f, 1f);
            _deathBtnStyle.normal.background = _deathBtnTex;
            _deathBtnStyle.hover.background = _deathBtnHoverTex;
            _deathBtnStyle.active.background = _deathBtnHoverTex;

            _deathHintStyle = new GUIStyle
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            _deathHintStyle.normal.textColor = new Color(0.65f, 0.68f, 0.72f, 1f);

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
            if (_movement == null || _health == null || _waveDirector == null) ResolveReferences();
            InitStyles();

            float screenH = Screen.height;
            float screenW = Screen.width;
            if (screenH < 100f || screenW < 100f) return;

            // 1080p bazında çözünürlükten bağımsız dinamik ölçek matrisi
            float guiScale = Mathf.Clamp(screenH / 1080f, 0.80f, 2.5f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
            GUI.depth = -100;

            float virtualW = screenW / guiScale;
            float virtualH = screenH / guiScale;

            // 1. ÖLÜM EKRANI (HUD kapalı olsa bile gösterilir)
            if (_health != null && _health.IsDead)
            {
                DrawDeathScreen(virtualW, virtualH);
                GUI.matrix = oldMatrix;
                return;
            }

            // 2. HUD KAPALIYSA: Sadece küçük F2 Hatırlatma Bildirimi Çiz
            if (!showHUD)
            {
                DrawWatermarkBadge(20f, 20f);
                DrawCombatBanners(virtualW, virtualH);
                GUI.matrix = oldMatrix;
                return;
            }

            // 3. HUD AÇIKSA: Tek Çatı Altında Birleşik Developer Debug Paneli
            DrawUnifiedDebugHUD(20f, 20f, 380f);

            // 4. SAVAŞ & DALGA MERKEZ BANNERLARI
            DrawCombatBanners(virtualW, virtualH);

            GUI.matrix = oldMatrix;
        }

        /// <summary>
        /// HUD kapalıyken sol üst köşede duran şık ve göze batmayan F2 uyarı hapı.
        /// </summary>
        private void DrawWatermarkBadge(float x, float y)
        {
            float badgeW = 220f;
            float badgeH = 28f;
            Rect rect = new Rect(x, y, badgeW, badgeH);

            GUI.DrawTexture(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), _borderGoldTex);
            GUI.DrawTexture(rect, _panelBgTex);

            GUI.Label(rect, "⚡ [F2] DEBUG HUD (HIDDEN)", _watermarkStyle);
        }

        /// <summary>
        /// Tüm bilgi HUD'larını tek bir mimari altında toplayan ana panel.
        /// </summary>
        private void DrawUnifiedDebugHUD(float x, float y, float width)
        {
            float pad = 12f;
            float contentW = width - (pad * 2f);
            float panelH = Mathf.Max(260f, _lastPanelHeight);

            // 1. Dış Altın Çerçeve ve Arka Plan Gövdesi (İçerikten ÖNCE çizilir, asla yazıları örtmez)
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, width + 4f, panelH + 4f), _borderGoldTex);
            GUI.DrawTexture(new Rect(x, y, width, panelH), _panelBgTex);

            // 2. İçerik Alanı
            GUILayout.BeginArea(new Rect(x + pad, y + pad, contentW, 2000f));
            GUILayout.BeginVertical();

            // =========================================================================
            // BAŞLIK & PERFORMANS DİYAGNOSTİĞİ
            // =========================================================================
            GUILayout.BeginHorizontal();
            GUILayout.Label("⚡ RETRO FPS // DEVELOPER DEBUG HUD", _mainHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#FFCC00><b>[F2]</b></color>", _mainHeaderStyle);
            GUILayout.EndHorizontal();

            _sb.Clear();
            _sb.Append("FPS: <color=#55FF55><b>").Append(_currentFps.ToString("F0")).Append("</b></color> (")
               .Append(_currentFrameMs.ToString("F1")).Append("ms) | ")
               .Append(Screen.width).Append("x").Append(Screen.height).Append(" | Scale: ")
               .Append((Screen.height / 1080f).ToString("F2")).Append("x");
            GUILayout.Label(_sb.ToString(), _subHeaderStyle);

            DrawDivider(contentW);

            // =========================================================================
            // 1. BÖLÜM: OYUNCU & SAVAŞ TELEMETRİSİ (PLAYER TELEMETRY)
            // =========================================================================
            GUILayout.Label("▼ PLAYER & COMBAT TELEMETRY", _sectionHeaderStyle);
            GUILayout.Space(2f);

            // Can (Health) & Bar
            if (_health != null)
            {
                float hp = _health.CurrentHealth;
                float maxHp = _health.MaxHealth;
                float hpRatio = Mathf.Clamp01(hp / maxHp);
                bool isFlashing = _hpFlashTimer > 0f;
                string hpColor = isFlashing ? "#FFFFFF" : (hp > 50f ? "#55FF55" : (hp > 25f ? "#FFAA00" : "#FF3333"));

                _sb.Clear();
                _sb.Append("HEALTH: <color=").Append(hpColor).Append("><b>").Append((int)hp).Append(" / ").Append((int)maxHp).Append(" HP</b></color>");
                GUILayout.Label(_sb.ToString(), _labelStyle);

                Rect hpBarRect = GUILayoutUtility.GetRect(contentW, 6f);
                GUI.DrawTexture(hpBarRect, _barBgTex);
                Color hpBarColor = isFlashing ? Color.white : (hp > 50f ? new Color(0.2f, 0.95f, 0.35f) : (hp > 25f ? new Color(1f, 0.7f, 0.15f) : new Color(0.95f, 0.2f, 0.2f)));
                GUI.color = hpBarColor;
                GUI.DrawTexture(new Rect(hpBarRect.x, hpBarRect.y, hpBarRect.width * hpRatio, hpBarRect.height), _barFillTex);
                GUI.color = Color.white;
            }

            // Silah
            if (_weapons != null)
            {
                string wepLabel = _weapons.CurrentSlot == WeaponSlot.RepeaterArbalest ? LabelWepArbalest : LabelWepBlade;
                GUILayout.Label(wepLabel, _labelStyle);
            }

            // Sol El Savunma / Parry
            if (_parry != null)
            {
                if (_parry.IsParrying)
                {
                    GUILayout.Label("DEFENSE [Q]: <color=#00E5FF><b>PARRY ACTIVE!</b></color>", _labelStyle);
                }
                else if (_parry.IsOnCooldown)
                {
                    _sb.Clear();
                    _sb.Append("DEFENSE [Q]: <color=#FFAA00>COOLDOWN (").Append(_parry.CooldownRemaining.ToString("F1")).Append("s)</color>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);
                }
                else
                {
                    GUILayout.Label("DEFENSE [Q]: <color=#55FF55><b>SPIKED BUCKLER (READY)</b></color>", _labelStyle);
                }
            }

            // Hız & Görsel Hız Barı
            if (_movement != null)
            {
                float speed = _movement.CurrentHorizontalSpeed;
                string speedColor = speed > 11.5f ? "#FFAA00" : (speed > 8.0f ? "#55FF55" : "#FFFFFF");
                _sb.Clear();
                _sb.Append("SPEED: <color=").Append(speedColor).Append("><b>").Append(speed.ToString("F1")).Append(" m/s</b></color>");
                GUILayout.Label(_sb.ToString(), _labelStyle);

                if (showSpeedBar)
                {
                    Rect speedBarRect = GUILayoutUtility.GetRect(contentW, 6f);
                    GUI.DrawTexture(speedBarRect, _barBgTex);
                    float fillRatio = Mathf.Clamp01(speed / 16f);
                    Color barCol = speed > 11.5f ? new Color(1f, 0.65f, 0.1f) : new Color(0.2f, 0.8f, 1f);
                    GUI.color = barCol;
                    GUI.DrawTexture(new Rect(speedBarRect.x, speedBarRect.y, speedBarRect.width * fillRatio, speedBarRect.height), _barFillTex);
                    GUI.color = Color.white;
                }

                // Göz Hizası (Kamera)
                float eyeHeight = _playerCamera != null ? _playerCamera.transform.position.y : 0f;
                _sb.Clear();
                _sb.Append("EYE HEIGHT: <b>").Append(eyeHeight.ToString("F2")).Append(" m</b> <size=10>(Dummy: 1.80m)</size>");
                GUILayout.Label(_sb.ToString(), _labelStyle);

                // Dash Şarjı
                int charges = Mathf.Clamp(_movement.CurrentDashCharges, 0, 3);
                GUILayout.Label(DashDisplayCache[charges], _labelStyle);

                // Hareket Durumu
                string stateLabel = StateGround;
                if (_movement.IsDashing) stateLabel = StateDash;
                else if (_movement.IsSliding) stateLabel = StateSlide;
                else if (_movement.IsCrouching) stateLabel = StateCrouch;
                else if (!_movement.IsGrounded) stateLabel = StateAir;
                GUILayout.Label(stateLabel, _stateStyle);
            }

            // =========================================================================
            // 2. BÖLÜM: DALGA & KARŞILAŞMA TELEMETRİSİ (WAVE ENCOUNTER)
            // =========================================================================
            if (showWaveTelemetry && _waveDirector != null)
            {
                DrawDivider(contentW);
                GUILayout.Label("▼ ENCOUNTER & WAVE TELEMETRY", _waveSectionHeaderStyle);
                GUILayout.Space(2f);

                bool isActive = _waveDirector.State != EncounterState.Inactive;

                if (!isActive)
                {
                    // STATUS & TRIGGER CONTROL
                    if (_waveDirector.HasPreWaveGuards)
                    {
                        int guardsRemaining = _waveDirector.PreWaveGuardsRemaining;
                        int guardsTotal = _waveDirector.PreWaveGuardsTotal;
                        int requiredKills = _waveDirector.PreWaveGuardsRequiredKills;
                        int killed = _waveDirector.PreWaveGuardsKilled;
                        float ratioPct = _waveDirector.PreWaveGuardsDeathRatio * 100f;

                        _sb.Clear();
                        _sb.Append("STATUS: <color=#FFAA00><b>PRE-WAVE GUARDS (").Append(guardsRemaining).Append(" / ").Append(guardsTotal).Append(" REMAINING)</b></color>");
                        GUILayout.Label(_sb.ToString(), _labelStyle);

                        _sb.Clear();
                        _sb.Append("OBJECTIVE: <color=#FFCC00><b>Kill ").Append(requiredKills).Append(" guard(s) (%").Append(ratioPct.ToString("F0")).Append(") to start waves! (").Append(killed).Append("/").Append(requiredKills).Append(" slain)</b></color>");
                        GUILayout.Label(_sb.ToString(), _controlsStyle);
                    }
                    else
                    {
                        GUILayout.Label("STATUS: <color=#FFAA00><b>STANDBY (AWAITING TRIGGER)</b></color>", _labelStyle);
                    }

                    int totalEncounterEnemies = _waveDirector.TotalEnemiesInEncounter;
                    int totalWaves = _waveDirector.TotalWaves;

                    _sb.Clear();
                    _sb.Append("ARENA: <color=#FFCC00><b>").Append(totalWaves).Append(" Waves Configured</b></color> | <color=#00E5FF><b>")
                       .Append(totalEncounterEnemies).Append(" Total Enemies</b></color>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);

                    // Karşılaşmayı Başlatma Butonu
                    if (GUILayout.Button("⚔️  START ENCOUNTER [B]", _deathBtnStyle, GUILayout.Height(28f)))
                    {
                        _waveDirector.StartEncounter();
                    }

                    // TÜM DALGALARIN VE SIRADAKİ DALGANIN DETAYLI ÖNİZLEMESİ
                    if (totalWaves > 0)
                    {
                        var firstWave = _waveDirector.GetWaveDefinition(0);
                        if (firstWave != null)
                        {
                            GUILayout.Space(2f);
                            _sb.Clear();
                            _sb.Append("▶ NEXT (WAVE 1): <color=#55FF55><b>").Append(firstWave.WaveName).Append("</b></color>");
                            GUILayout.Label(_sb.ToString(), _boldLabelStyle);

                            _sb.Clear();
                            _sb.Append("  • Enemy Count: <color=#00E5FF><b>").Append(firstWave.TotalEnemies).Append(" Enemies</b></color>")
                               .Append(" <size=11>(").Append(firstWave.CompositionSummary).Append(")</size>");
                            GUILayout.Label(_sb.ToString(), _labelStyle);

                            _sb.Clear();
                            _sb.Append("  • Threat Rating: <color=#FFAA00><b>").Append(firstWave.DensityRating).Append("</b></color>")
                               .Append(" <size=10>(Max ").Append(firstWave.MaxConcurrentEnemies).Append(" Concurrent | ")
                               .Append(firstWave.GroundEnemies).Append(" Ground, ").Append(firstWave.AirEnemies).Append(" Air)</size>");
                            GUILayout.Label(_sb.ToString(), _labelStyle);
                        }

                        // Varsa diğer dalgaların dökümü
                        if (totalWaves > 1)
                        {
                            for (int w = 1; w < totalWaves && w <= 3; w++)
                            {
                                var otherWave = _waveDirector.GetWaveDefinition(w);
                                if (otherWave != null)
                                {
                                    _sb.Clear();
                                    _sb.Append("  • Wave ").Append(w + 1).Append(": <color=#AAAAAA>")
                                       .Append(otherWave.TotalEnemies).Append(" Enemies (").Append(otherWave.CompositionSummary).Append(")</color>")
                                       .Append(" <size=10>[Max ").Append(otherWave.MaxConcurrentEnemies).Append("]</size>");
                                    GUILayout.Label(_sb.ToString(), _controlsStyle);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // DURUM (Aktif Savaş / Isınma / Mola / Zafer)
                    _sb.Clear();
                    _sb.Append("STATUS: ");
                    if (_waveDirector.State == EncounterState.Warmup)
                        _sb.Append("<color=#FFAA00><b>WARMUP (").Append(_waveDirector.StateTimer.ToString("F1")).Append("s)</b></color>");
                    else if (_waveDirector.State == EncounterState.WaveCooldown)
                        _sb.Append("<color=#55FF55><b>INTERMISSION / COOLDOWN (").Append(_waveDirector.StateTimer.ToString("F1")).Append("s)</b></color>");
                    else if (_waveDirector.State == EncounterState.EncounterCleared)
                        _sb.Append("<color=#55FF55><b>ENCOUNTER CLEARED (VICTORY)</b></color>");
                    else
                        _sb.Append("<color=#00E5FF><b>COMBAT ACTIVE (").Append(_waveDirector.WaveElapsedTime.ToString("F1")).Append("s)</b></color>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);

                    // DALGA VE BAŞLIK
                    int currentWave = _waveDirector.CurrentWaveIndex + 1;
                    int totalWaves = _waveDirector.TotalWaves;
                    int wavesLeft = _waveDirector.WavesRemaining;
                    var waveDef = _waveDirector.CurrentWaveDefinition;

                    _sb.Clear();
                    _sb.Append("WAVE: <color=#FFCC00><b>").Append(currentWave).Append(" / ").Append(totalWaves).Append("</b></color>");
                    if (waveDef != null)
                    {
                        _sb.Append(" - <color=#FFFFFF><b>").Append(waveDef.WaveName).Append("</b></color>");
                    }
                    _sb.Append(" <size=11>(Queued: <b>").Append(wavesLeft).Append("</b> Waves)</size>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);

                    // BU DALGADAKİ DÜŞMANLAR (Kalan / Toplam) + Saha ve Kuyruk
                    int waveRemaining = _waveDirector.EnemiesRemainingInWave;
                    int waveTotal = _waveDirector.TotalEnemiesInCurrentWave;
                    string waveColor = waveRemaining <= 2 ? "#FFAA00" : "#FFFFFF";
                    _sb.Clear();
                    _sb.Append("THIS WAVE: <color=").Append(waveColor).Append("><b>").Append(waveRemaining).Append(" / ").Append(waveTotal).Append(" REMAINING</b></color>")
                       .Append(" <size=10>(Field: <b>").Append(_waveDirector.ActiveEnemiesCount).Append("</b> Alive | Queue: <b>")
                       .Append(_waveDirector.QueuedEnemiesCount).Append("</b> Spawning)</size>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);

                    // Bu dalganın ilerleme barı
                    if (waveTotal > 0)
                    {
                        float waveKilledRatio = Mathf.Clamp01((float)(waveTotal - waveRemaining) / waveTotal);
                        Rect waveBarRect = GUILayoutUtility.GetRect(contentW, 4f);
                        GUI.DrawTexture(waveBarRect, _barBgTex);
                        GUI.color = new Color(0.95f, 0.45f, 0.20f);
                        GUI.DrawTexture(new Rect(waveBarRect.x, waveBarRect.y, waveBarRect.width * waveKilledRatio, waveBarRect.height), _barFillTex);
                        GUI.color = Color.white;
                    }

                    // TÜM KARŞILAŞMADAKİ TOPLAM DÜŞMANLAR
                    int allRemaining = _waveDirector.TotalEnemiesRemainingInEncounter;
                    int allTotal = _waveDirector.TotalEnemiesInEncounter;
                    int allKilled = _waveDirector.TotalEnemiesKilledInEncounter;
                    int upcomingTotal = _waveDirector.UpcomingWavesTotalEnemies;

                    _sb.Clear();
                    _sb.Append("ENCOUNTER TOTAL: <color=#00E5FF><b>").Append(allRemaining).Append(" / ").Append(allTotal).Append(" REMAINING</b></color>")
                       .Append(" <size=10>(Killed: ").Append(allKilled).Append(" | Upcoming Waves: <b>").Append(upcomingTotal).Append("</b> Enemies)</size>");
                    GUILayout.Label(_sb.ToString(), _labelStyle);

                    // =====================================================================
                    // SIRADAKİ DALGA YOĞUNLUĞU VE BİLEŞİMİ (UPCOMING WAVE DENSITY)
                    // =====================================================================
                    if (_waveDirector.HasNextWave)
                    {
                        var nextWave = _waveDirector.NextWaveDefinition;
                        if (nextWave != null)
                        {
                            GUILayout.Space(2f);
                            _sb.Clear();
                            _sb.Append("▶ UPCOMING WAVE [").Append(_waveDirector.NextWaveIndex + 1).Append("/").Append(totalWaves)
                               .Append("]: <color=#FFCC00><b>").Append(nextWave.WaveName).Append("</b></color>");
                            GUILayout.Label(_sb.ToString(), _boldLabelStyle);

                            _sb.Clear();
                            _sb.Append("  • Threat Rating: <color=#FFAA00><b>").Append(nextWave.DensityRating).Append("</b></color>")
                               .Append(" <size=10>(Max ").Append(nextWave.MaxConcurrentEnemies).Append(" Concurrent | ")
                               .Append(nextWave.GroundEnemies).Append(" Ground, ").Append(nextWave.AirEnemies).Append(" Air)</size>");
                            GUILayout.Label(_sb.ToString(), _labelStyle);

                            _sb.Clear();
                            _sb.Append("  • Composition: <color=#55FF55><b>").Append(nextWave.TotalEnemies).Append(" Enemies</b></color>")
                               .Append(" <size=11>(").Append(nextWave.CompositionSummary).Append(")</size>");
                            GUILayout.Label(_sb.ToString(), _labelStyle);
                        }
                    }
                    else if (currentWave == totalWaves)
                    {
                        GUILayout.Label("▶ UPCOMING WAVE: <color=#55FF55><b>NONE (FINAL WAVE ACTIVE!)</b></color>", _controlsStyle);
                    }

                    // SÜRE SAYACI, EŞİK VE GERİ SAYIM BARI
                    float nextTimer = _waveDirector.TimeUntilNextWave;

                    if (nextTimer >= 0f)
                    {
                        string timerColor = nextTimer <= 5f ? "#FF3333" : "#FFAA00";
                        int thresholdPercent = Mathf.RoundToInt((waveDef?.PercentageThreshold ?? 0.75f) * 100f);
                        _sb.Clear();
                        _sb.Append("WAVE TIMER: <color=").Append(timerColor).Append("><b>").Append(nextTimer.ToString("F1")).Append("s</b></color>")
                           .Append(" <size=10>(Or %").Append(thresholdPercent).Append(" Kills)</size>");
                        GUILayout.Label(_sb.ToString(), _labelStyle);

                        if (waveDef != null && waveDef.TimeLimit > 0f)
                        {
                            Rect timeBarRect = GUILayoutUtility.GetRect(contentW, 5f);
                            GUI.DrawTexture(timeBarRect, _barBgTex);
                            float fillRatio = Mathf.Clamp01(nextTimer / waveDef.TimeLimit);
                            GUI.color = nextTimer <= 5f ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.75f, 0.15f);
                            GUI.DrawTexture(new Rect(timeBarRect.x, timeBarRect.y, timeBarRect.width * fillRatio, timeBarRect.height), _barFillTex);
                            GUI.color = Color.white;
                        }
                    }
                    else
                    {
                        if (waveDef != null && waveDef.TransitionMode == WaveTransitionMode.RemainingEnemyThreshold)
                        {
                            _sb.Clear();
                            _sb.Append("THRESHOLD: <color=#FFAA00><b>Starts when <= ").Append(waveDef.RemainingThreshold).Append(" Enemies remain</b></color>");
                            GUILayout.Label(_sb.ToString(), _labelStyle);
                        }
                        else
                        {
                            GUILayout.Label("OBJECTIVE: <color=#FF5555><b>Eliminate All Enemies</b></color>", _labelStyle);
                        }
                    }

                    // Kural ve Devir Politikası
                    if (waveDef != null)
                    {
                        _sb.Clear();
                        _sb.Append("<size=10>RULE: ").Append(waveDef.TransitionMode)
                           .Append(" | CARRY-OVER: <color=").Append(waveDef.CarryOverRemainingEnemies ? "#55FF55>Active" : "#FFAA00>Purged").Append("</color></size>");
                        GUILayout.Label(_sb.ToString(), _controlsStyle);
                    }
                }
            }

            // =========================================================================
            // 3. BÖLÜM: MODÜLER GELİŞTİRİCİ TELEMETRİSİ (CUSTOM & FUTURE-PROOF)
            // =========================================================================
            bool hasCustomMetrics = CategorizedMetrics.Count > 0 || CustomCallbacks.Count > 0 || CustomSections.Count > 0;
            if (showCustomTelemetry && hasCustomMetrics)
            {
                DrawDivider(contentW);
                GUILayout.Label("▼ MODULAR CUSTOM TELEMETRY", _customSectionHeaderStyle);
                GUILayout.Space(2f);

                // 1. Kategorize edilmiş anahtar-değer metrikleri (DebugHUD.SetMetric)
                foreach (var pair in CategorizedMetrics)
                {
                    if (pair.Value.Count == 0) continue;
                    GUILayout.Label($"<b>[{pair.Key}]</b>", _controlsStyle);
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        _sb.Clear();
                        _sb.Append("• ").Append(pair.Value[i].Key).Append(": <color=#00E5FF>").Append(pair.Value[i].Value).Append("</color>");
                        GUILayout.Label(_sb.ToString(), _labelStyle);
                    }
                }

                // 2. Fonksiyonel Callbacks (DebugHUD.RegisterCallback)
                foreach (var cb in CustomCallbacks)
                {
                    _sb.Clear();
                    cb.Value?.Invoke(_sb);
                    if (_sb.Length > 0)
                    {
                        GUILayout.Label($"<b>[{cb.Key}]</b> {_sb.ToString()}", _labelStyle);
                    }
                }

                // 3. Modüler Bölümler (IDebugHUDSection)
                for (int i = 0; i < CustomSections.Count; i++)
                {
                    var sec = CustomSections[i];
                    if (sec != null && sec.IsEnabled)
                    {
                        GUILayout.Label($"<b>[{sec.SectionTitle}]</b>", _customSectionHeaderStyle);
                        _sb.Clear();
                        sec.DrawSection(_sb, _sectionHeaderStyle, _labelStyle, contentW);
                    }
                }
            }

            // =========================================================================
            // ALT BİLGİ & TUŞ İPUÇLARI
            // =========================================================================
            DrawDivider(contentW);
            GUILayout.Label("<size=10><b>[F2]</b> Toggle HUD | <b>[B]</b> Start Waves | <b>1-2</b>: Weapons | <b>Q</b>: Parry | <b>SHIFT</b>: Dash | <b>CTRL</b>: Slide</size>", _controlsStyle);

            GUILayout.Space(pad);
            GUILayout.EndVertical();

            // Gerçek panel yüksekliğini Repaint aşamasında kaydet (Sonraki karelerde mükemmel oturur)
            if (Event.current.type == EventType.Repaint)
            {
                _lastPanelHeight = GUILayoutUtility.GetLastRect().height + (pad * 2f);
            }

            GUILayout.EndArea();
        }

        private void DrawDivider(float width)
        {
            GUILayout.Space(4f);
            Rect r = GUILayoutUtility.GetRect(width, 1f);
            GUI.DrawTexture(r, _dividerTex);
            GUILayout.Space(4f);
        }

        /// <summary>
        /// Parry ve Dalga bildirim bannerları (Ekran ortası gotik juice).
        /// </summary>
        private void DrawCombatBanners(float virtualW, float virtualH)
        {
            // 1. PARRY BAŞARI BANNERI
            if (_parryBannerTimer > 0f)
            {
                float bannerW = 440f;
                float bannerH = 72f;
                float bannerX = (virtualW - bannerW) * 0.5f;
                float bannerY = virtualH * 0.24f;
                Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);

                GUI.DrawTexture(new Rect(bannerRect.x - 2f, bannerRect.y - 2f, bannerRect.width + 4f, bannerRect.height + 4f), _borderGoldTex);
                GUI.DrawTexture(bannerRect, _parryBannerBgTex);

                GUILayout.BeginArea(new Rect(bannerRect.x, bannerRect.y + 8f, bannerRect.width, bannerRect.height - 16f));
                GUILayout.Label("⚡ PARRY! ⚡", _parryTitleStyle);
                string subText = _parryBannerIsProj ? "[PROJECTILE DEFLECTED!]" : "[ENEMY STAGGERED — STRIKE NOW!]";
                GUILayout.Label(subText, _parrySubStyle);
                GUILayout.EndArea();
            }

            // 2. DALGA / KARŞILAŞMA BİLDİRİM BANNERI
            if (_waveBannerTimer > 0f)
            {
                float bannerW = 620f;
                float bannerH = 84f;
                float bannerX = (virtualW - bannerW) * 0.5f;
                float bannerY = virtualH * 0.22f;
                Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);

                GUI.DrawTexture(new Rect(bannerRect.x - 2f, bannerRect.y - 2f, bannerRect.width + 4f, bannerRect.height + 4f), _borderRedTex);
                GUI.DrawTexture(bannerRect, _waveBannerBgTex);

                _waveBannerTitleStyle.normal.textColor = _waveBannerColor;

                GUILayout.BeginArea(new Rect(bannerRect.x + 12f, bannerRect.y + 8f, bannerRect.width - 24f, bannerRect.height - 16f));
                GUILayout.Label(_waveBannerTitle, _waveBannerTitleStyle);
                GUILayout.Label(_waveBannerSub, _waveBannerSubStyle);
                GUILayout.EndArea();
            }
        }

        /// <summary>
        /// Karakter öldüğünde tam ekran gotik kart ve Yeniden Başlat butonu.
        /// </summary>
        private void DrawDeathScreen(float virtualW, float virtualH)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Ekranı kaplayan yarı saydam karanlık/kan perdesi
            GUI.DrawTexture(new Rect(0f, 0f, virtualW, virtualH), _deathOverlayTex);

            float cardW = 540f;
            float cardH = 280f;
            float cardX = (virtualW - cardW) * 0.5f;
            float cardY = (virtualH - cardH) * 0.5f;
            Rect cardRect = new Rect(cardX, cardY, cardW, cardH);

            GUI.DrawTexture(new Rect(cardRect.x - 3f, cardRect.y - 3f, cardRect.width + 6f, cardRect.height + 6f), _deathBorderTex);
            GUI.DrawTexture(cardRect, _deathCardTex);

            GUILayout.BeginArea(new Rect(cardRect.x + 20f, cardRect.y + 24f, cardRect.width - 40f, cardRect.height - 48f));

            GUILayout.Label("YOU DIED", _deathTitleStyle);
            GUILayout.Space(8f);

            GUILayout.Label("The abyss has claimed your flesh.\nYour soul drifts through the ancient crypt.", _deathSubStyle);
            GUILayout.Space(24f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⚔️  RESTART ENCOUNTER", _deathBtnStyle, GUILayout.Width(340f), GUILayout.Height(52f)))
            {
                if (_health != null)
                {
                    _health.RestartGame();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.Label("Shortcut: Press [R] or [SPACE] to restart immediately", _deathHintStyle);

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (_panelBgTex != null) Destroy(_panelBgTex);
            if (_borderGoldTex != null) Destroy(_borderGoldTex);
            if (_borderRedTex != null) Destroy(_borderRedTex);
            if (_borderCyanTex != null) Destroy(_borderCyanTex);
            if (_dividerTex != null) Destroy(_dividerTex);
            if (_barBgTex != null) Destroy(_barBgTex);
            if (_barFillTex != null) Destroy(_barFillTex);
            if (_parryBannerBgTex != null) Destroy(_parryBannerBgTex);
            if (_waveBannerBgTex != null) Destroy(_waveBannerBgTex);
            if (_deathOverlayTex != null) Destroy(_deathOverlayTex);
            if (_deathCardTex != null) Destroy(_deathCardTex);
            if (_deathBorderTex != null) Destroy(_deathBorderTex);
            if (_deathBtnTex != null) Destroy(_deathBtnTex);
            if (_deathBtnHoverTex != null) Destroy(_deathBtnHoverTex);
        }
    }

    /// <summary>
    /// Global Developer Debug HUD Statik Erişim Noktası.
    /// Projedeki herhangi bir sınıftan (AI, Boss, Weapon, Audio vb.) tek satırda HUD'a metrik gönderebilirsiniz:
    /// Örnek: DebugHUD.SetMetric("AI", "Durum", "Takip Ediyor");
    /// </summary>
    public static class DebugHUD
    {
        public static void SetMetric(string category, string key, string value) => PlayerDebugHUD.SetMetric(category, key, value);
        public static void SetMetric(string category, string key, float value, string format = "F2") => PlayerDebugHUD.SetMetric(category, key, value, format);
        public static void RemoveMetric(string category, string key) => PlayerDebugHUD.RemoveMetric(category, key);
        public static void ClearCategory(string category) => PlayerDebugHUD.ClearCategory(category);
        public static void RegisterSection(IDebugHUDSection section) => PlayerDebugHUD.RegisterSection(section);
        public static void UnregisterSection(IDebugHUDSection section) => PlayerDebugHUD.UnregisterSection(section);
        public static void RegisterCallback(string category, Action<StringBuilder> callback) => PlayerDebugHUD.RegisterCallback(category, callback);
        public static void UnregisterCallback(string category) => PlayerDebugHUD.UnregisterCallback(category);
        public static void ToggleHUD() => PlayerDebugHUD.Instance?.ToggleHUD();
        public static bool IsVisible => PlayerDebugHUD.Instance != null && PlayerDebugHUD.Instance.IsVisible;
    }
}
