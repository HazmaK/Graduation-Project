using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;

namespace RetroFPS.UI
{
    /// <summary>
    /// Koşu (Run) durumu ve Araf hakkını gösteren, F3 ile hata ayıklama bilgileri açılabilen retro arayüz paneli.
    /// - Sağ üst köşede AmmoHUD ile uyumlu retro/gotik renk ve font temasında çizilir.
    /// - Her zaman görünür: Araf hakkı durumu ("ARAF: HAZIR" / "ARAF: KULLANILDI") ve Araf evresindeyken uyarı etiketi.
    /// - F3 ile açılan hata ayıklama bloğu: Tohum ("SEED: ..."), Kat indeksi ("KAT: ...") ve koşu süresi (mm:ss).
    /// - Sıfır GC: Önbelleğe alınmış StringBuilder, metin önbellekleri ve statik GUIStyle yapıları.
    /// - RunEvents.CurrentRun null ise hiçbir şey çizilmez.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RetroFPS/Run HUD")]
    public class RunHUD : MonoBehaviour
    {
        [Header("Görsel Ayarlar")]
        [SerializeField] private bool showHUD = true;
        [SerializeField] private bool showDebugOnStart = false;
        [SerializeField] private Color arafReadyColor = new Color(0.86f, 0.08f, 0.24f, 1.0f);
        [SerializeField] private Color arafUsedColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        [SerializeField] private Color inArafWarningColor = new Color(1.0f, 0.30f, 0.30f, 1.0f);
        [SerializeField] private Color debugTextColor = new Color(0.75f, 0.85f, 0.95f, 0.95f);

        private const string StrArafReady = "ARAF: HAZIR";
        private const string StrArafUsed = "ARAF: KULLANILDI";
        private const string StrInArafWarning = "ARAF'TASIN — Çıkış odasını bul";

        private const string StrExitFound = "ÇIKIŞ BULUNDU";
        private string _cachedRoomsStr = "ODALAR 0/0";
        private bool _exitRevealed;

        private readonly StringBuilder _sb = new StringBuilder(64);

        private bool _showDebug;
        private RunState _activeRun;
        private float _runStartTime;
        private bool _hasRunStarted;
        private bool _isTrueDeath;
        private float _elapsedSecondsOnDeath;

        private int _lastSeed = -1;
        private int _lastFloor = -1;
        private int _lastTimerSeconds = -1;

        private string _cachedSeedStr = "SEED: -";
        private string _cachedFloorStr = "KAT: 0";
        private string _cachedTimerStr = "SÜRE: 00:00";

        // GUI Önbellekleri
        private static GUIStyle _arafReadyStyle;
        private static GUIStyle _arafUsedStyle;
        private static GUIStyle _inArafStyle;
        private static GUIStyle _debugStyle;
        private static Texture2D _whiteTex;
        private static Texture2D _panelBgTex;

        /// <summary>
        /// Hata ayıklama bloğunun şu anda açık olup olmadığını belirtir.
        /// </summary>
        public bool ShowDebug => _showDebug;

        private void Awake()
        {
            _showDebug = showDebugOnStart;
        }

        private void OnEnable()
        {
            RunEvents.OnTrueDeath += HandleTrueDeath;
            RunEvents.OnPhaseChanged += HandlePhaseChanged;
            RunEvents.OnArafRoomsChanged += HandleArafRoomsChanged;
            ArafLabyrinthDirector director = ArafLabyrinthDirector.Active;
            HandleArafRoomsChanged(director != null ? director.RevealedRooms : 0,
                director != null ? director.TotalRooms : 0, director != null && director.ExitRevealed);
        }

        private void OnDisable()
        {
            RunEvents.OnTrueDeath -= HandleTrueDeath;
            RunEvents.OnPhaseChanged -= HandlePhaseChanged;
            RunEvents.OnArafRoomsChanged -= HandleArafRoomsChanged;
        }

        private void HandleArafRoomsChanged(int revealed, int total, bool exitRevealed)
        {
            _exitRevealed = exitRevealed;
            _sb.Clear();
            _sb.Append("ODALAR ").Append(revealed).Append('/').Append(total);
            _cachedRoomsStr = _sb.ToString();
        }

        private void HandleTrueDeath()
        {
            if (!_isTrueDeath && _hasRunStarted)
            {
                _isTrueDeath = true;
                _elapsedSecondsOnDeath = Mathf.Max(0f, Time.unscaledTime - _runStartTime);
                UpdateTimerString(Mathf.FloorToInt(_elapsedSecondsOnDeath));
            }
        }

        private void HandlePhaseChanged(RunPhase phase)
        {
            if (phase == RunPhase.TrueDeath && !_isTrueDeath && _hasRunStarted)
            {
                _isTrueDeath = true;
                _elapsedSecondsOnDeath = Mathf.Max(0f, Time.unscaledTime - _runStartTime);
                UpdateTimerString(Mathf.FloorToInt(_elapsedSecondsOnDeath));
            }
        }

        private void Update()
        {
            // F3 ile hata ayıklama paneli görünürlüğünü aç/kapat (New Input System)
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                _showDebug = !_showDebug;
            }

            RunState run = RunEvents.CurrentRun;
            if (run == null)
            {
                _hasRunStarted = false;
                _activeRun = null;
                return;
            }

            // Yeni bir koşu örneği algılandıysa zamanlayıcıyı ve önbellekleri sıfırla
            if (!ReferenceEquals(_activeRun, run))
            {
                _activeRun = run;
                _hasRunStarted = true;
                _runStartTime = Time.unscaledTime;
                _isTrueDeath = (run.Phase == RunPhase.TrueDeath);
                _elapsedSecondsOnDeath = 0f;
                _lastSeed = -1;
                _lastFloor = -1;
                _lastTimerSeconds = -1;
            }
            else if (run.Phase == RunPhase.TrueDeath && !_isTrueDeath)
            {
                _isTrueDeath = true;
                _elapsedSecondsOnDeath = Mathf.Max(0f, Time.unscaledTime - _runStartTime);
            }

            // Tohum değiştiğinde dizeyi yeniden oluştur
            if (run.Seed != _lastSeed)
            {
                _lastSeed = run.Seed;
                _sb.Clear();
                _sb.Append("SEED: ").Append(_lastSeed);
                _cachedSeedStr = _sb.ToString();
            }

            // Kat indeksi değiştiğinde dizeyi yeniden oluştur
            if (run.FloorIndex != _lastFloor)
            {
                _lastFloor = run.FloorIndex;
                _sb.Clear();
                _sb.Append("KAT: ").Append(_lastFloor);
                _cachedFloorStr = _sb.ToString();
            }

            // Koşu süresini yalnızca saniye değeri değiştiğinde (saniyede 1 kez) yeniden oluştur
            float elapsed = _isTrueDeath ? _elapsedSecondsOnDeath : Mathf.Max(0f, Time.unscaledTime - _runStartTime);
            int currentSeconds = Mathf.FloorToInt(elapsed);
            if (currentSeconds != _lastTimerSeconds)
            {
                _lastTimerSeconds = currentSeconds;
                UpdateTimerString(currentSeconds);
            }
        }

        private void UpdateTimerString(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _sb.Clear();
            _sb.Append("SÜRE: ");
            if (minutes < 10) _sb.Append('0');
            _sb.Append(minutes).Append(':');
            if (seconds < 10) _sb.Append('0');
            _sb.Append(seconds);
            _cachedTimerStr = _sb.ToString();
        }

        private static void EnsureStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = Texture2D.whiteTexture;
            }

            if (_panelBgTex == null)
            {
                _panelBgTex = new Texture2D(1, 1);
                _panelBgTex.SetPixel(0, 0, new Color(0.04f, 0.04f, 0.05f, 0.85f));
                _panelBgTex.Apply();
            }

            if (_arafReadyStyle == null)
            {
                _arafReadyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_arafUsedStyle == null)
            {
                _arafUsedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_inArafStyle == null)
            {
                _inArafStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_debugStyle == null)
            {
                _debugStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
        }

        private void OnGUI()
        {
            if (!showHUD) return;

            RunState run = RunEvents.CurrentRun;
            if (run == null) return;

            // Matris Ölçekleme: 1920x1080 referans çözünürlük
            float nativeWidth = 1920f;
            float nativeHeight = 1080f;
            Matrix4x4 oldMatrix = GUI.matrix;
            float scaleX = Screen.width / nativeWidth;
            float scaleY = Screen.height / nativeHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            EnsureStyles();

            bool isInAraf = run.Phase == RunPhase.InAraf;
            bool arafUsed = run.ArafChargeUsed;

            _arafReadyStyle.normal.textColor = arafReadyColor;
            _arafUsedStyle.normal.textColor = arafUsedColor;
            _inArafStyle.normal.textColor = inArafWarningColor;
            _debugStyle.normal.textColor = debugTextColor;

            // Panel boyutlandırması ve konumu (Sağ üst köşe)
            float panelW = 360f;
            float contentH = 22f; // Araf durumu satırı
            if (isInAraf) contentH += _exitRevealed ? 60f : 40f;
            if (_showDebug) contentH += 60f; // Seed, Floor, Timer + ayırıcı çizgi

            float panelH = contentH + 16f;
            float x = nativeWidth - panelW - 35f;
            float y = 35f;

            // Panel Arka Planı
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), _panelBgTex);

            // Çerçeve Kenarlığı (AmmoHUD ile uyumlu)
            GUI.color = new Color(0.35f, 0.35f, 0.40f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + panelH - 2, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, 2, panelH), _whiteTex);
            GUI.DrawTexture(new Rect(x + panelW - 2, y, 2, panelH), _whiteTex);

            float curX = x + 12f;
            float curY = y + 8f;
            float innerW = panelW - 24f;

            // 1. Araf Hakkı Göstergesi (Her zaman görünür)
            GUI.Label(new Rect(curX, curY, innerW, 20f), arafUsed ? StrArafUsed : StrArafReady, arafUsed ? _arafUsedStyle : _arafReadyStyle);
            curY += 22f;

            // 2. Araf Evresi Uyarısı (Sadece InAraf durumundayken)
            if (isInAraf)
            {
                GUI.Label(new Rect(curX, curY, innerW, 18f), StrInArafWarning, _inArafStyle);
                curY += 20f;
                GUI.Label(new Rect(curX, curY, innerW, 18f), _cachedRoomsStr, _inArafStyle);
                curY += 20f;
                if (_exitRevealed)
                {
                    GUI.Label(new Rect(curX, curY, innerW, 18f), StrExitFound, _inArafStyle);
                    curY += 20f;
                }
            }

            // 3. F3 Hata Ayıklama Bloğu
            if (_showDebug)
            {
                // Ayırıcı ince çizgi
                GUI.color = new Color(0.35f, 0.35f, 0.40f, 0.45f);
                GUI.DrawTexture(new Rect(curX, curY + 2f, innerW, 1), _whiteTex);
                GUI.color = Color.white;
                curY += 6f;

                GUI.Label(new Rect(curX, curY, innerW, 18f), _cachedSeedStr, _debugStyle);
                curY += 18f;

                GUI.Label(new Rect(curX, curY, innerW, 18f), _cachedFloorStr, _debugStyle);
                curY += 18f;

                GUI.Label(new Rect(curX, curY, innerW, 18f), _cachedTimerStr, _debugStyle);
            }

            GUI.matrix = oldMatrix;
            GUI.color = Color.white;
        }
    }
}
