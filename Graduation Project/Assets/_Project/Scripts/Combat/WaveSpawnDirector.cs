using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Karşılaşma ve Dalga Direktörü (WaveSpawnDirector). Bir ODA'nın dalgalarını yönetir.
    /// Ultrakill, DOOM ve retro boomer shooter dinamiklerine göre optimize edilmiştir.
    /// - Sıfır GC Tahsisi (Önceden ısıtılmış Nesne Havuzu / Object Pool, önbellekli delegeler)
    /// - Grup akışları (Spawn Streams): gecikme, parti (batch), desen (Random/RoundRobin/Farthest), sıralı/paralel
    /// - Eşzamanlılık bütçesi: adet sınırı veya tehdit bütçesi (IThreatCost)
    /// - Flank baskısı: oyuncunun yan/arkasındaki noktalar ağırlıklı seçilir, telgraf sesi 3D çalınır
    /// - Dalga sonu ödülü (can/cephane), UnityEvent kancaları, dinamik zorluk ve checkpoint/retry
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveSpawnDirector : MonoBehaviour, IRetryableEncounter
    {
        /// <summary>
        /// Bir düşman grubunun çalışma zamanı akışı (struct: sıfır GC).
        /// </summary>
        private struct SpawnStream
        {
            public GameObject Prefab;
            public bool IsAir;
            public string ZoneTag;
            public float Interval;
            public int BatchSize;
            public SpawnPattern Pattern;
            public float Threat;
            public float Delay;
            public bool Started;
            public int Remaining;
            public float NextTime;
            public float LastSpawnTime;
            public int RoundRobinCursor;
        }

        [Header("Karşılaşma Başlangıç Ayarları")]
        [Tooltip("Oyun başlar başlamaz karşılaşmayı otomatik başlat")]
        [SerializeField] private bool autoStartOnPlay = false;

        public bool AutoStartOnPlay { get => autoStartOnPlay; set => autoStartOnPlay = value; }

        // Çoklu Oda / Arena Takibi (Multi-Room Architecture)
        private static WaveSpawnDirector _activeDirector;
        public static WaveSpawnDirector ActiveDirector => _activeDirector;
        public static event System.Action<WaveSpawnDirector> OnActiveDirectorChanged;

        public static void SetActiveDirector(WaveSpawnDirector director)
        {
            if (_activeDirector != director)
            {
                _activeDirector = director;
                OnActiveDirectorChanged?.Invoke(director);
            }
        }

        [Header("Alan ve Ön Muhafız Entegrasyonu (Encounter Area)")]
        [Tooltip("Bu karşılaşmayı başlatacak olan EncounterArea (varsa). Yalnızca bu odanın hiyerarşisinde otomatik algılanır.")]
        [SerializeField] private EncounterArea encounterArea;

        public EncounterArea EncounterArea => encounterArea;
        public bool HasPreWaveGuards => encounterArea != null && encounterArea.HasGuards;
        public int PreWaveGuardsRemaining => encounterArea != null ? encounterArea.RemainingGuardCount : 0;
        public int PreWaveGuardsTotal => encounterArea != null ? encounterArea.InitialGuardCount : 0;
        public int PreWaveGuardsRequiredKills => encounterArea != null ? encounterArea.RequiredGuardKills : 0;
        public int PreWaveGuardsKilled => encounterArea != null ? encounterArea.KilledGuardsCount : 0;
        public float PreWaveGuardsDeathRatio => encounterArea != null ? encounterArea.GuardDeathRatio : 0.25f;

        public void RegisterEncounterArea(EncounterArea area)
        {
            encounterArea = area;
        }

        [Tooltip("İlk dalgadan önceki genel arena ısınma/kilitlenme süresi (saniye)")]
        [SerializeField] private float warmupDuration = 1.5f;

        [Header("Dalga Tanımları")]
        [Tooltip("Atanırsa her run başında havuzdan seed'li rastgele bir WaveSet seçilir; 'waveSet' alanı yok sayılır.")]
        [SerializeField] private WaveSetPool waveSetPool;

        [Tooltip("Atanırsa dalga listesi bu WaveSet preset'inden okunur ve aşağıdaki satır-içi 'waves' listesi yok sayılır. Aynı preset birden fazla arenada paylaşılabilir.")]
        [SerializeField] private WaveSet waveSet;

        [Tooltip("Satır-içi dalga listesi (WaveSet atanmamışsa kullanılır). NOT: Son dalga, geçiş modundan bağımsız olarak her zaman 'hepsini öldür' kuralıyla biter.")]
        [SerializeField] private WaveDefinition[] waves;

        [Header("Tempo ve Bütçe Ayarları")]
        [Tooltip("Dalgalar arası nefes alma süresi (saniye)")]
        [SerializeField] private float waveCooldownDuration = 2.0f;

        [Tooltip("Sahadaki TÜM aktif düşmanlar için mutlak üst sınır (önceki dalgadan taşan düşmanlar dahil). Performans koruması. 0 = sınırsız (yalnızca dalga başına sınır geçerli).")]
        [SerializeField] private int absoluteMaxAlive = 0;

        [Tooltip("Kuyruk bittiğinde, sahada bu sayı veya daha az düşman kalırsa Straggler (kayıp düşman) alarmı devreye girer")]
        [SerializeField] private int stragglerAlertThreshold = 2;

        [Tooltip("Straggler alarmının tekrar etme aralığı (saniye)")]
        [SerializeField] private float stragglerAlertInterval = 3.5f;

        [Header("Doğuş Noktası Seçimi (Flank Baskısı)")]
        [Tooltip("Random desenli gruplarda oyuncunun yan/arkasındaki noktalara ağırlık ver")]
        [SerializeField] private bool preferOutOfViewSpawns = true;

        [Tooltip("Kamera ileri yönü ile noktaya yön arasındaki dot bu değerin ÜSTÜNDEYSE nokta 'önde' sayılır (0.5 ≈ 60°)")]
        [Range(-0.5f, 0.95f)]
        [SerializeField] private float outOfViewDotThreshold = 0.5f;

        [Tooltip("Dot bu değerin ALTINDAYSA nokta 'arkada' sayılır (-0.25 ≈ 105°). İkisinin arası 'yan'dır.")]
        [Range(-0.95f, 0.5f)]
        [SerializeField] private float rearDotThreshold = -0.25f;

        [Tooltip("Önde (görüş içinde) kalan noktaların seçilme ağırlığı")]
        [SerializeField] private float frontSpawnWeight = 1f;

        [Tooltip("Yandaki noktaların seçilme ağırlığı")]
        [SerializeField] private float sideSpawnWeight = 2.5f;

        [Tooltip("Arkadaki (kör nokta) noktaların seçilme ağırlığı")]
        [SerializeField] private float rearSpawnWeight = 4f;

        [Tooltip("Uygun doğuş noktası bulunamazsa bu süre sonra (saniye) kısıtlar gevşetilir ve dalganın kilitlenmesi önlenir")]
        [SerializeField] private float spawnBlockedTimeout = 2.0f;

        [Tooltip("Telgraf süresine eklenen güvenlik payı (sn). Bir noktaya doğan düşman ortaya çıkıp uzaklaşana kadar aynı noktaya ikinci düşman konmaz; üst üste doğan düşmanlar birbirini duvardan dışarı itip dünyadan düşebiliyordu.")]
        [SerializeField] private float spawnReservationPadding = 0.75f;

        [Header("Kayıp Düşman Kurtarma (Güvenlik Ağı)")]
        [Tooltip("Düşman oda zemininin bu kadar metre altına düşerse (dünyadan düştü) havuza alınıp yeniden doğurulur")]
        [SerializeField] private float fallRescueDepth = 8f;

        [Tooltip("Düşman oda sınırlarının (EncounterArea) dışında bu kadar saniye kalırsa yeniden doğurulur. 0 = kapalı")]
        [SerializeField] private float outOfRoomRescueDelay = 6f;

        [Tooltip("Oda sınırına eklenen tolerans (metre); kapı eşiğindeki düşmanlar hemen 'dışarıda' sayılmasın")]
        [SerializeField] private float outOfRoomMargin = 3f;

        [Header("Oda Kimliği")]
        [Tooltip("Log ve HUD'da görünen oda adı. Boşsa GameObject adı kullanılır.")]
        [SerializeField] private string roomDisplayName;

        [Header("Doğuş Noktaları ve Konumlandırma")]
        [Tooltip("Bu odanın doğuş noktaları. Boş bırakılırsa yalnızca bu odanın hiyerarşisinden toplanır.")]
        [SerializeField] private EncounterSpawnPoint[] spawnPoints;

        [Tooltip("Oyuncuya olan minimum doğuş mesafesi (Kameranın hemen dibinde belirmeyi önler)")]
        [SerializeField] private float minSpawnDistance = 6.0f;

        [Tooltip("Oyuncuya olan maksimum doğuş mesafesi (Çok uzakta boşlukta doğmayı önler)")]
        [SerializeField] private float maxSpawnDistance = 35.0f;

        [Tooltip("Doğuş alanının dolu olup olmadığını denetleyen fiziksel engel katmanları")]
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Okült Çağırma Telgrafı")]
        [Tooltip("Düşman belirmeden önce yerde beliren çağırma rünü süresi (saniye)")]
        [SerializeField] private float summonTelegraphDuration = 0.65f;

        [Header("Arena Karantinası ve Bariyerler")]
        [Tooltip("Savaş başladığında açılan (aktifleşen), zaferde kapanan arena bariyerleri/kapıları")]
        [SerializeField] private GameObject[] arenaBarriers;

        [Header("Dalga Sonu Ödülü")]
        [Tooltip("Ödül can küresinin iyileştirme miktarı")]
        [SerializeField] private float rewardOrbHealAmount = 15f;

        [Tooltip("Ödül cephane paketi: saçma (Shells) miktarı")]
        [SerializeField] private int rewardShellAmount = 6;

        [Tooltip("Ödül cephane paketi: ok (Bolts) miktarı")]
        [SerializeField] private int rewardBoltAmount = 20;

        [Tooltip("Ödüllerin oyuncu etrafına saçılma yarıçapı (metre)")]
        [SerializeField] private float rewardScatterRadius = 2.5f;

        [Header("Dinamik Zorluk")]
        [SerializeField] private DynamicDifficultySettings dynamicDifficulty = new DynamicDifficultySettings();

        [Header("Checkpoint / Retry")]
        [Tooltip("Oyuncu bu odanın savaşı sırasında ölürse sahneyi yeniden yüklemek yerine karşılaşmayı checkpoint'ten yeniden başlat")]
        [SerializeField] private bool enableCheckpointRetry = true;

        [Tooltip("Retry'da oyuncunun doğacağı nokta. Boşsa karşılaşma başladığı andaki oyuncu konumu kullanılır.")]
        [SerializeField] private Transform retryCheckpoint;

        [Tooltip("Retry sonrası ilk dalgadan önceki hazırlık süresi (saniye)")]
        [SerializeField] private float retryWarmupDuration = 2.5f;

        [Header("Olaylar (Kod yazmadan level scripting)")]
        [SerializeField] private UnityEvent onEncounterStarted = new UnityEvent();
        [SerializeField] private UnityEvent onEncounterCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onEncounterRetried = new UnityEvent();

        [Tooltip("Belirli dalgaların başında/sonunda tetiklenecek olaylar (kapı, ışık, müzik, tuzak)")]
        [SerializeField] private WaveEventBinding[] waveEvents;

        [Header("Ses Efektleri")]
        [SerializeField] private AudioClip encounterStartSound;
        [SerializeField] private AudioClip waveStartSound;
        [SerializeField] private AudioClip waveClearedSound;
        [SerializeField] private AudioClip victorySound;
        [Tooltip("Telgraf noktasında 3D olarak çalınır (oyuncu arkasındaki doğuşu duyabilsin)")]
        [SerializeField] private AudioClip summonTelegraphSound;

        public WaveSetPool WaveSetPoolAsset => waveSetPool;
        public WaveSet WaveSetAsset => _runtimeWaveSet != null ? _runtimeWaveSet : waveSet;
        public string RoomName => string.IsNullOrEmpty(roomDisplayName) ? name : roomDisplayName;

        /// <summary>WaveSet atanmışsa (veya havuzdan seçilmişse) onun dalgaları, değilse satır-içi liste (Editor doğrulaması ve araçlar için).</summary>
        public WaveDefinition[] EffectiveWaves
        {
            get
            {
                WaveSet activeSet = WaveSetAsset;
                return activeSet != null && activeSet.WaveCount > 0 ? activeSet.Waves : waves;
            }
        }
        public GameObject[] ArenaBarriers => arenaBarriers;
        public float MinSpawnDistance => minSpawnDistance;
        public float MaxSpawnDistance => maxSpawnDistance;
        public int AbsoluteMaxAlive => absoluteMaxAlive;
        public LayerMask ObstacleLayerMask => obstacleLayerMask;
        public bool CheckpointRetryEnabled => enableCheckpointRetry;
        public WaveEventBinding[] WaveEvents => waveEvents;
        public EncounterDifficultyModel Difficulty => _difficultyModel;

        // Karşılaşma Durum Bilgileri
        private EncounterState _state = EncounterState.Inactive;
        private int _currentWaveIndex = -1;
        private float _stateTimer = 0f;
        private float _waveElapsedTime = 0f;
        private float _stragglerAlertTimer = 0f;
        private float _spawnBlockedTimer = 0f;
        private bool _forceEmergencyPick = false;
        private bool _warnedSpawnBlocked = false;
        private bool _usingWaveSet = false;
        private float _fallbackFloorY = 0f; // EncounterArea yoksa "dünyadan düştü" eşiği için en alçak doğuş noktası
        private WaveSet _runtimeWaveSet;

        // Telgraf bekleyen (henüz doğmamış) düşmanlar ve Reset sonrası eski callback'leri eleyen nesil sayacı
        private int _pendingSpawns = 0;
        private float _pendingThreat = 0f;
        private int _encounterGeneration = 0;
        private System.Action<OccultSpawnTelegraph> _onTelegraphCompleted;
        private System.Func<bool> _retryHandler;

        private int _totalEnemiesInCurrentWave = 0;
        private int _enemiesKilledInCurrentWave = 0;
        private int _totalEnemiesInAllWaves = 0;
        private int _totalEnemiesKilledInEncounter = 0;
        private int _queuedRemaining = 0;

        // Checkpoint
        private Vector3 _checkpointPosition;
        private Quaternion _checkpointRotation = Quaternion.identity;
        private bool _hasCheckpoint = false;
        private bool _suspendedForPurgatory = false;

        // Önbelleğe Alınmış Referanslar
        private Transform _playerTransform;
        private Camera _playerCamera;
        private PlayerHealth _playerHealth;
        private PlayerAmmoInventory _ammoInventory;
        private AudioSource _audioSource;
        private Transform _poolContainer;
        private EncounterDifficultyModel _difficultyModel;

        // Sıfır GC Listeleri ve Havuz Veri Yapıları
        // _activeEnemies / _activeEnemyWave / _activeEnemyThreat birebir paraleldir.
        private readonly List<IEncounterEnemy> _activeEnemies = new List<IEncounterEnemy>(32);
        private readonly List<int> _activeEnemyWave = new List<int>(32);
        private readonly List<float> _activeEnemyThreat = new List<float>(32);
        private readonly List<GameObject> _activeEnemyPrefab = new List<GameObject>(32);
        private readonly List<bool> _activeEnemyIsAir = new List<bool>(32);
        private readonly List<float> _activeEnemyOutsideTime = new List<float>(32);

        private const float RescueCheckInterval = 0.5f;
        private float _rescueCheckTimer;
        private int _rescuedEnemyCount;

        /// <summary>Bu karşılaşmada güvenlik ağının kurtardığı (düşen / odadan çıkan / kaybolan) düşman sayısı.</summary>
        public int RescuedEnemyCount => _rescuedEnemyCount;
        private readonly List<SpawnStream> _streams = new List<SpawnStream>(16);
        private readonly List<EncounterSpawnPoint> _candidatePoints = new List<EncounterSpawnPoint>(32);
        private readonly List<int> _candidateIndices = new List<int>(32);
        private readonly List<float> _candidateWeights = new List<float>(32);

        // Prefab -> Pre-warmed Nesne Havuzu ve tehdit puanı önbelleği
        private readonly Dictionary<GameObject, List<GameObject>> _enemyPools = new Dictionary<GameObject, List<GameObject>>();
        private readonly Dictionary<GameObject, float> _prefabThreat = new Dictionary<GameObject, float>();
        private readonly List<OccultSpawnTelegraph> _telegraphPool = new List<OccultSpawnTelegraph>(8);

        // Kamu Olay Delegeleri (Decoupled Observer Pattern)
        public event System.Action OnEncounterStarted;
        public event System.Action<int, int, string> OnWaveStarted; // (waveIndex, totalWaves, waveName)
        public event System.Action<int, int> OnEnemiesRemainingChanged; // (remainingEnemies, totalInWave)
        public event System.Action<int> OnWaveCompleted; // (completedWaveIndex)
        public event System.Action OnEncounterCompleted;
        public event System.Action OnEncounterRetried;

        public EncounterState State => _state;
        public int CurrentWaveIndex => _currentWaveIndex;
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int WavesRemaining => Mathf.Max(0, TotalWaves - (_currentWaveIndex + 1));
        public int ActiveEnemiesCount => _activeEnemies.Count;
        public int EnemiesRemainingInWave => Mathf.Max(0, _totalEnemiesInCurrentWave - _enemiesKilledInCurrentWave);
        public int TotalEnemiesInCurrentWave => _totalEnemiesInCurrentWave;

        public int TotalEnemiesInEncounter
        {
            get
            {
                if (_totalEnemiesInAllWaves <= 0)
                {
                    _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
                }
                return _totalEnemiesInAllWaves;
            }
        }

        public int TotalEnemiesKilledInEncounter => _totalEnemiesKilledInEncounter;

        public int TotalEnemiesRemainingInEncounter
        {
            get
            {
                if (_state == EncounterState.Inactive) return TotalEnemiesInEncounter;
                if (_state == EncounterState.EncounterCleared) return 0;
                return EnemiesRemainingInWave + UpcomingWavesTotalEnemies;
            }
        }

        public int UpcomingWavesTotalEnemies
        {
            get
            {
                if (waves == null) return 0;
                int sum = 0;
                int start = Mathf.Max(0, _currentWaveIndex + 1);
                for (int i = start; i < waves.Length; i++)
                {
                    if (waves[i] != null) sum += waves[i].TotalEnemies;
                }
                return sum;
            }
        }

        public int QueuedEnemiesCount => _queuedRemaining;
        public float WaveElapsedTime => _waveElapsedTime;
        public float StateTimer => _stateTimer;
        public float CurrentWaveThreatAlive => ThreatAliveInWave(_currentWaveIndex) + _pendingThreat;

        public WaveDefinition GetWaveDefinition(int index)
        {
            if (waves != null && index >= 0 && index < waves.Length) return waves[index];
            return null;
        }

        public WaveDefinition NextWaveDefinition
        {
            get
            {
                if (waves == null || waves.Length == 0) return null;
                int nextIndex = _currentWaveIndex + 1;
                if (nextIndex >= 0 && nextIndex < waves.Length) return waves[nextIndex];
                return null;
            }
        }

        public int NextWaveIndex => _currentWaveIndex + 1;
        public bool HasNextWave => waves != null && (_currentWaveIndex + 1) < waves.Length;

        public float TimeUntilNextWave
        {
            get
            {
                if (_state == EncounterState.Warmup || _state == EncounterState.WaveCooldown)
                {
                    return Mathf.Max(0f, _stateTimer);
                }

                if (_state == EncounterState.WaveSpawningAndCombat && _currentWaveIndex >= 0 && waves != null && _currentWaveIndex < waves.Length)
                {
                    WaveDefinition wave = waves[_currentWaveIndex];
                    if ((wave.TransitionMode == WaveTransitionMode.TimeLimit || wave.TransitionMode == WaveTransitionMode.PercentageOrTimeLimit) && wave.TimeLimit > 0f)
                    {
                        return Mathf.Max(0f, wave.TimeLimit - _waveElapsedTime);
                    }
                }

                return -1f;
            }
        }

        public WaveDefinition CurrentWaveDefinition
        {
            get
            {
                if (waves != null && _currentWaveIndex >= 0 && _currentWaveIndex < waves.Length) return waves[_currentWaveIndex];
                return null;
            }
        }

        // ───────────────────────────── Yaşam Döngüsü ─────────────────────────────

        private void Awake()
        {
            if (_activeDirector == null)
            {
                SetActiveDirector(this);
            }

            _onTelegraphCompleted = HandleTelegraphCompleted;
            _retryHandler = HandleRetryRequest;
            _difficultyModel = new EncounterDifficultyModel(dynamicDifficulty);

            _runtimeWaveSet = null;
            if (waveSetPool != null)
            {
                int seed = RetroFPS.Run.RunEvents.CurrentRun != null ? RetroFPS.Run.RunEvents.CurrentRun.Seed : 0;
                WaveSet picked = waveSetPool.Pick(RetroFPS.Run.RunEvents.GetStream(RetroFPS.Run.RunStream.Encounter), seed);
                if (picked != null && picked.WaveCount > 0)
                {
                    _runtimeWaveSet = picked;
                    waves = _runtimeWaveSet.Waves;
                    _usingWaveSet = true;
                    Debug.Log($"<color=cyan>[WaveSpawnDirector]</color> '{RoomName}': '{waveSetPool.name}' havuzundan '{_runtimeWaveSet.name}' dalga seti seçildi (Seed: {seed}).");
                }
            }

            if (!_usingWaveSet && waveSet != null && waveSet.WaveCount > 0)
            {
                waves = waveSet.Waves;
                _usingWaveSet = true;
            }

            SetupAudio();
            FindPlayerReference();
            // Sıra önemli: önce oda alanı, sonra o alana ait doğuş noktaları (oda izolasyonu)
            FindEncounterAreaIfEmpty();
            FindSpawnPointsIfEmpty();
            ValidateAndRepairWaveEntries();
            InitializeObjectPools();
            CacheFallbackFloor();
            _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
        }

        private void Start()
        {
            SetBarriersActive(false);

            if (autoStartOnPlay)
            {
                StartEncounter();
            }
        }

        private void OnDestroy()
        {
            if (_activeDirector == this)
            {
                _activeDirector = null;
                OnActiveDirectorChanged?.Invoke(null);
            }

            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken -= HandlePlayerDamaged;
                ReleaseRetryInterceptor();
            }

            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                if (_activeEnemies[i] != null)
                {
                    _activeEnemies[i].OnEnemyDeath -= HandleEnemyDeath;
                }
            }
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D Arena Genel Efekti
            }
        }

        private void FindPlayerReference()
        {
            if (_playerTransform == null)
            {
                PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
                if (pm != null)
                {
                    _playerTransform = pm.transform;
                }
            }

            if (_playerTransform != null && _playerHealth == null)
            {
                _playerHealth = _playerTransform.GetComponent<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _playerHealth.OnDamageTaken -= HandlePlayerDamaged;
                    _playerHealth.OnDamageTaken += HandlePlayerDamaged;
                }
            }

            if (_ammoInventory == null)
            {
                _ammoInventory = _playerTransform != null ? _playerTransform.GetComponentInChildren<PlayerAmmoInventory>() : null;
                if (_ammoInventory == null) _ammoInventory = Object.FindAnyObjectByType<PlayerAmmoInventory>();
            }

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }
        }

        private void FindEncounterAreaIfEmpty()
        {
            // Oda izolasyonu: yalnızca kendi hiyerarşisinde (aynı obje / ebeveyn / çocuk) aranır.
            if (encounterArea == null)
            {
                encounterArea = GetComponent<EncounterArea>();
                if (encounterArea == null) encounterArea = GetComponentInParent<EncounterArea>();
                if (encounterArea == null) encounterArea = GetComponentInChildren<EncounterArea>(true);
            }
        }

        private void FindSpawnPointsIfEmpty()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = GetRoomSpawnPoints();
            }
        }

        /// <summary>
        /// Yan etkisiz (nokta ÜRETMEYEN) sürüm: Editor doğrulaması ve araçları için.
        /// Öncelik: Inspector'daki liste, sonra alan çocukları, sonra kendi çocukları.
        /// </summary>
        public EncounterSpawnPoint[] PeekRoomSpawnPoints()
        {
            if (spawnPoints != null && spawnPoints.Length > 0) return spawnPoints;
            return CollectHierarchySpawnPoints();
        }

        private EncounterSpawnPoint[] CollectHierarchySpawnPoints()
        {
            EncounterArea area = encounterArea;
            if (area == null) area = GetComponent<EncounterArea>();
            if (area == null) area = GetComponentInParent<EncounterArea>();
            if (area == null) area = GetComponentInChildren<EncounterArea>(true);

            if (area != null)
            {
                EncounterSpawnPoint[] areaPoints = area.GetComponentsInChildren<EncounterSpawnPoint>(true);
                if (areaPoints.Length > 0) return areaPoints;
            }

            return GetComponentsInChildren<EncounterSpawnPoint>(true);
        }

        /// <summary>
        /// Inspector'daki doğuş noktası listesini bu odanın noktalarıyla doldurur (Editor aracı).
        /// </summary>
        public void BindRoomSpawnPoints()
        {
            // Mevcut (muhtemelen yanlış) listeyi yok sayıp yalnızca bu odanın hiyerarşisinden yeniden topla
            spawnPoints = CollectHierarchySpawnPoints();
        }

        /// <summary>
        /// Bu odaya ait doğuş noktalarını döner: önce EncounterArea'nın noktaları (gerekirse prosedürel üretir),
        /// yoksa bu direktörün kendi hiyerarşisindeki noktalar. Sahne genelinde ARAMA YAPMAZ (oda izolasyonu).
        /// </summary>
        public EncounterSpawnPoint[] GetRoomSpawnPoints()
        {
            FindEncounterAreaIfEmpty();

            if (encounterArea != null)
            {
                EncounterSpawnPoint[] areaPoints = encounterArea.GetSpawnPoints();
                if (areaPoints != null && areaPoints.Length > 0) return areaPoints;
            }

            EncounterSpawnPoint[] ownPoints = GetComponentsInChildren<EncounterSpawnPoint>(true);
            if (ownPoints != null && ownPoints.Length > 0) return ownPoints;

            return System.Array.Empty<EncounterSpawnPoint>();
        }

        public void ConfigureSpawnPoints(EncounterSpawnPoint[] newPoints)
        {
            spawnPoints = newPoints;
        }

        public EncounterSpawnPoint[] SpawnPoints => spawnPoints;

        private void ValidateAndRepairWaveEntries()
        {
            if (waves == null) waves = System.Array.Empty<WaveDefinition>();

            // WaveSet preset'i paylaşılan bir asset'tir; sahne tarafında onarım yazmayız, sadece metrikleri hesaplarız.
            if (_usingWaveSet)
            {
                for (int i = 0; i < waves.Length; i++) waves[i]?.CalculateMetrics();
                return;
            }
#if UNITY_EDITOR
            GameObject defaultGhoul = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab");
            GameObject defaultEye = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab");

            if (waves.Length == 0)
            {
                EnsureDefaultWavesIfEmpty();
                return;
            }

            for (int w = 0; w < waves.Length; w++)
            {
                WaveDefinition wave = waves[w];
                if (wave?.SpawnEntries == null) continue;

                for (int e = 0; e < wave.SpawnEntries.Length; e++)
                {
                    var entry = wave.SpawnEntries[e];
                    bool isSceneObject = entry.EnemyPrefab != null && !UnityEditor.EditorUtility.IsPersistent(entry.EnemyPrefab);
                    if (entry.EnemyPrefab == null || isSceneObject)
                    {
                        GameObject fixedPrefab = entry.IsAirEnemy ? (defaultEye != null ? defaultEye : defaultGhoul) : defaultGhoul;
                        if (fixedPrefab != null)
                        {
                            wave.SpawnEntries[e] = entry.WithPrefab(fixedPrefab);
                            Debug.LogWarning($"<color=yellow>[WaveSpawnDirector]</color> '{RoomName}' Dalga {w + 1} Grup {e + 1} düşman prefab referansı sahne objesi veya boştu; kalıcı prefab ({fixedPrefab.name}) ile otomatik onarıldı!");
                        }
                    }
                }
                wave.CalculateMetrics();
            }
#endif
        }

        private void EnsureDefaultWavesIfEmpty()
        {
#if UNITY_EDITOR
            if (waves == null || waves.Length == 0)
            {
                GameObject ghoul = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab");
                GameObject eye = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab");

                if (ghoul != null)
                {
                    waves = new WaveDefinition[]
                    {
                        new WaveDefinition("1. Dalga: Leş Yiyiciler", 1.5f, 5, new EnemySpawnEntry[]
                        {
                            new EnemySpawnEntry(ghoul, 5, 1.0f, false)
                        }),
                        new WaveDefinition("2. Dalga: Gökyüzü Gözleri & Leşçiler", 2.0f, 7, new EnemySpawnEntry[]
                        {
                            new EnemySpawnEntry(ghoul, 5, 0.9f, false),
                            new EnemySpawnEntry(eye != null ? eye : ghoul, 3, 1.4f, true)
                        }),
                        new WaveDefinition("3. Dalga: Katliam Sürüsü (Final Wave)", 2.5f, 8, new EnemySpawnEntry[]
                        {
                            new EnemySpawnEntry(ghoul, 8, 0.75f, false),
                            new EnemySpawnEntry(eye != null ? eye : ghoul, 5, 1.1f, true)
                        })
                    };
                    Debug.Log($"<color=yellow>[WaveSpawnDirector] '{RoomName}' dalga listesi boştu; varsayılan 3 aşamalı karşılaşma dalgaları otomatik oluşturuldu (yalnızca Editor)!</color>");
                }
            }
#endif
        }

        public int CalculateTotalEnemiesAllWaves()
        {
            if (waves == null) return 0;
            int total = 0;
            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] != null) total += waves[i].CalculateTotalEnemies();
            }
            return total;
        }

        /// <summary>
        /// Karşılaşmadaki tüm dalgaları analiz eder ve ihtiyaç duyulacak maksimum düşman sayısı
        /// kadar GameObject'i oyun başlamadan önce ısıtarak (Pre-warm) belleğe yerleştirir.
        /// Tehdit puanlarını da burada önbelleğe alır (oyun döngüsünde GetComponent yok).
        /// </summary>
        private void InitializeObjectPools()
        {
            GameObject poolRoot = new GameObject("Encounter_ObjectPool_Container");
            poolRoot.transform.SetParent(transform);
            _poolContainer = poolRoot.transform;

            if (waves != null)
            {
                Dictionary<GameObject, int> requiredCapacities = new Dictionary<GameObject, int>();

                for (int w = 0; w < waves.Length; w++)
                {
                    WaveDefinition wave = waves[w];
                    if (wave?.SpawnEntries == null) continue;

                    for (int g = 0; g < wave.SpawnEntries.Length; g++)
                    {
                        GameObject prefab = wave.SpawnEntries[g].EnemyPrefab;
                        if (prefab == null) continue;

                        CacheThreat(prefab);

                        int count = wave.SpawnEntries[g].Count;
                        int maxAllowed = Mathf.Min(count, wave.MaxConcurrentEnemies + 2);

                        if (!requiredCapacities.TryGetValue(prefab, out int currentMax) || maxAllowed > currentMax)
                        {
                            requiredCapacities[prefab] = maxAllowed;
                        }
                    }
                }

                foreach (var kvp in requiredCapacities)
                {
                    GameObject prefab = kvp.Key;
                    int countToPool = Mathf.Max(2, kvp.Value);

                    List<GameObject> poolList = new List<GameObject>(countToPool);
                    for (int i = 0; i < countToPool; i++)
                    {
                        GameObject instance = Instantiate(prefab, _poolContainer);
                        instance.name = $"{prefab.name}_Pooled_{i}";
                        instance.SetActive(false);
                        poolList.Add(instance);
                    }
                    _enemyPools[prefab] = poolList;
                }
            }

            // Okült Telgraf Havuzunu Hazırla
            for (int i = 0; i < 8; i++)
            {
                GameObject telGo = new GameObject($"OccultTelegraph_Pooled_{i}");
                telGo.transform.SetParent(_poolContainer);
                OccultSpawnTelegraph tel = telGo.AddComponent<OccultSpawnTelegraph>();
                telGo.SetActive(false);
                _telegraphPool.Add(tel);
            }
        }

        private float CacheThreat(GameObject prefab)
        {
            if (prefab == null) return 1f;
            if (_prefabThreat.TryGetValue(prefab, out float cached)) return cached;

            float cost = 1f;
            IThreatCost threat = prefab.GetComponent<IThreatCost>();
            if (threat != null && threat.ThreatCost > 0f) cost = threat.ThreatCost;
            _prefabThreat[prefab] = cost;
            return cost;
        }

        private float ResolveStreamThreat(EnemySpawnEntry entry)
        {
            return entry.ThreatCostOverride > 0f ? entry.ThreatCostOverride : CacheThreat(entry.EnemyPrefab);
        }

        // ───────────────────────────── Karşılaşma Akışı ─────────────────────────────

        /// <summary>
        /// Karşılaşmayı dışarıdan (Trigger Volume veya Tetikleyici Olay) başlatır.
        /// </summary>
        public void StartEncounter()
        {
            StartEncounterInternal(warmupDuration, false);
        }

        private void StartEncounterInternal(float warmup, bool isRetry)
        {
            if (_state != EncounterState.Inactive) return;
            if (!Application.isPlaying) return; // Edit mode'da (Awake çalışmamışken) başlatılamaz

            SetActiveDirector(this);

            if (_playerTransform == null || _playerHealth == null)
            {
                FindPlayerReference();
            }

            if (!isRetry)
            {
                _difficultyModel.ResetAll();
                _rescuedEnemyCount = 0;
                RecordCheckpoint();
            }

            _state = EncounterState.Warmup;
            _stateTimer = warmup;
            _currentWaveIndex = -1;
            _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
            _totalEnemiesKilledInEncounter = 0;

            if (enableCheckpointRetry && _playerHealth != null)
            {
                _playerHealth.RetryInterceptor = _retryHandler;
                RetroFPS.Run.RunEvents.RegisterActiveEncounter(this);
            }

            SetBarriersActive(true);
            PlaySound(encounterStartSound);

            OnEncounterStarted?.Invoke();
            onEncounterStarted?.Invoke();
            Debug.Log($"<color=red><b>[ENCOUNTER {(isRetry ? "RETRY" : "STARTED")}!]</b></color> '{RoomName}' arena sealed, preparing wave.");
        }

        private void RecordCheckpoint()
        {
            if (retryCheckpoint != null)
            {
                _checkpointPosition = retryCheckpoint.position;
                _checkpointRotation = Quaternion.Euler(0f, retryCheckpoint.eulerAngles.y, 0f);
                _hasCheckpoint = true;
            }
            else if (_playerTransform != null)
            {
                _checkpointPosition = _playerTransform.position;
                _checkpointRotation = Quaternion.Euler(0f, _playerTransform.eulerAngles.y, 0f);
                _hasCheckpoint = true;
            }
        }

        private void Update()
        {
            switch (_state)
            {
                case EncounterState.Warmup:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f) StartNextWave();
                    break;

                case EncounterState.WaveSpawningAndCombat:
                    UpdateWaveCombat();
                    break;

                case EncounterState.WaveCooldown:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f) StartNextWave();
                    break;
            }
        }

        private void StartNextWave()
        {
            _currentWaveIndex++;

            if (waves == null || _currentWaveIndex >= waves.Length)
            {
                CompleteEncounter();
                return;
            }

            WaveDefinition wave = waves[_currentWaveIndex];
            _state = EncounterState.WaveSpawningAndCombat;
            _stateTimer = wave.PreWaveDelay;
            _waveElapsedTime = 0f;
            _stragglerAlertTimer = 0f;
            _spawnBlockedTimer = 0f;
            _forceEmergencyPick = false;
            _warnedSpawnBlocked = false;
            _enemiesKilledInCurrentWave = 0;

            BuildStreams(wave);

            // Dinamik zorluk: oyuncu rahatsa en kalabalık gruba ek düşman ekle
            int bonus = _difficultyModel.GetBonusEnemiesForNextWave();
            if (bonus > 0 && _streams.Count > 0)
            {
                int largest = 0;
                for (int i = 1; i < _streams.Count; i++)
                {
                    if (_streams[i].Remaining > _streams[largest].Remaining) largest = i;
                }
                SpawnStream s = _streams[largest];
                s.Remaining += bonus;
                _streams[largest] = s;
                _queuedRemaining += bonus;
                Debug.Log($"<color=orange>[WaveSpawnDirector]</color> '{RoomName}': dinamik zorluk +{bonus} ek düşman.");
            }

            // Toplam, gerçekten kuyruğa giren düşmanlardır (prefabı boş gruplar sayılmaz → kill-all kilitlenmez)
            _totalEnemiesInCurrentWave = _queuedRemaining;
            _difficultyModel.BeginWave();

            PlaySound(waveStartSound);
            OnWaveStarted?.Invoke(_currentWaveIndex + 1, waves.Length, wave.WaveName);
            OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);
            InvokeWaveEvents(_currentWaveIndex + 1, true);

            Debug.Log($"<color=orange><b>[WAVE STARTED]</b></color> '{RoomName}' {wave.WaveName} ({_totalEnemiesInCurrentWave} Enemies | Mode: {wave.TransitionMode})");
        }

        private void BuildStreams(WaveDefinition wave)
        {
            _streams.Clear();
            _queuedRemaining = 0;
            if (wave.SpawnEntries == null) return;

            for (int i = 0; i < wave.SpawnEntries.Length; i++)
            {
                EnemySpawnEntry entry = wave.SpawnEntries[i];
                if (entry.EnemyPrefab == null) continue;

                SpawnStream s = new SpawnStream
                {
                    Prefab = entry.EnemyPrefab,
                    IsAir = entry.IsAirEnemy,
                    ZoneTag = entry.TargetZoneTag,
                    Interval = entry.SpawnInterval,
                    BatchSize = entry.BatchSize,
                    Pattern = entry.Pattern,
                    Threat = ResolveStreamThreat(entry),
                    Delay = entry.DelayFromWaveStart,
                    // İlk grup ve paralel gruplar hemen kendi gecikmeleriyle başlar; diğerleri öncekini bekler
                    Started = _streams.Count == 0 || entry.RunInParallel,
                    Remaining = entry.Count,
                    NextTime = entry.DelayFromWaveStart,
                    LastSpawnTime = 0f,
                    RoundRobinCursor = 0
                };

                _streams.Add(s);
                _queuedRemaining += entry.Count;
            }
        }

        private void UpdateWaveCombat()
        {
            WaveDefinition currentWave = waves[_currentWaveIndex];

            // 1. Dalga öncesi bekleme süresi
            if (_stateTimer > 0f)
            {
                _stateTimer -= Time.deltaTime;
                return;
            }

            _waveElapsedTime += Time.deltaTime;

            // 2. Grup akışlarından doğuş
            UpdateSpawning(currentWave);

            // 2b. Güvenlik ağı: dünyadan düşen / odadan çıkan / kaybolan düşmanları kurtar
            UpdateLostEnemyRescue();

            // 3. Straggler (Kayıp Düşman) Hızlandırma: Son birkaç düşman saklanıyorsa oyuncuya yönlendir
            if (_queuedRemaining == 0 && _pendingSpawns == 0 && _activeEnemies.Count > 0 && _activeEnemies.Count <= stragglerAlertThreshold)
            {
                _stragglerAlertTimer += Time.deltaTime;
                if (_stragglerAlertTimer >= stragglerAlertInterval)
                {
                    _stragglerAlertTimer = 0f;
                    AlertAllActiveEnemies();
                }
            }

            // 4. Dalga geçiş kontrolü
            if (ShouldTransitionToNextWave(currentWave))
            {
                CompleteCurrentWave();
            }
        }

        /// <summary>
        /// Her grup akışını kendi zamanlamasıyla ilerletir: sıralı/paralel başlangıç, gecikme, parti boyutu,
        /// desen ve eşzamanlılık (adet veya tehdit bütçesi). Tahsissizdir.
        /// </summary>
        private void UpdateSpawning(WaveDefinition wave)
        {
            if (_queuedRemaining <= 0) return;

            float healthPct = _playerHealth != null ? _playerHealth.HealthPercent : 1f;
            float intervalMul = _difficultyModel.GetSpawnIntervalMultiplier(healthPct);
            bool spawnedAny = false;
            bool blocked = false;

            for (int i = 0; i < _streams.Count; i++)
            {
                SpawnStream s = _streams[i];
                if (s.Remaining <= 0) continue;

                if (!s.Started)
                {
                    // Sıralı grup: bir önceki grup başlamış ve tamamen doğmuş olmalı
                    SpawnStream prev = _streams[i - 1];
                    if (!prev.Started || prev.Remaining > 0) continue;

                    s.Started = true;
                    s.NextTime = Mathf.Max(prev.LastSpawnTime + prev.Interval * intervalMul, s.Delay);
                }

                if (_waveElapsedTime < s.NextTime)
                {
                    _streams[i] = s;
                    continue;
                }

                int spawnedNow = 0;
                while (spawnedNow < s.BatchSize && s.Remaining > 0 && HasCapacityFor(wave, s.Threat))
                {
                    EncounterSpawnPoint point = FindPointForStream(ref s);
                    if (point == null)
                    {
                        if (_forceEmergencyPick && !HasAnyStaticallyValidPoint(s.IsAir))
                        {
                            DropStream(ref s); // Bu tip için geometri dışında kalan hiç uyumlu nokta yok
                        }
                        else
                        {
                            blocked = true; // Noktalar geçici olarak dolu/rezerve: bekle (parti sonraki karelere bölünür)
                        }
                        break;
                    }

                    point.MarkAsSpawned();
                    SpawnEnemyWithTelegraph(s.Prefab, point.Position, point.Rotation, s.Threat, s.IsAir);
                    s.Remaining--;
                    _queuedRemaining--;
                    spawnedNow++;
                }

                if (spawnedNow > 0)
                {
                    s.LastSpawnTime = _waveElapsedTime;
                    s.NextTime = _waveElapsedTime + s.Interval * intervalMul;
                    spawnedAny = true;
                }

                _streams[i] = s;
            }

            if (spawnedAny)
            {
                _spawnBlockedTimer = 0f;
                _forceEmergencyPick = false;
            }
            else if (blocked)
            {
                _spawnBlockedTimer += Time.deltaTime;
                if (_spawnBlockedTimer >= spawnBlockedTimeout && !_forceEmergencyPick)
                {
                    _forceEmergencyPick = true;
                    if (!_warnedSpawnBlocked)
                    {
                        _warnedSpawnBlocked = true;
                        Debug.LogWarning($"<color=yellow>[WaveSpawnDirector]</color> '{RoomName}': uygun doğuş noktası bulunamadı (mesafe/engel/cooldown). Kısıtlar gevşetildi; spawn point sayısını veya min/max mesafeyi gözden geçirin.", this);
                    }
                }
            }
        }

        private void DropStream(ref SpawnStream s)
        {
            Debug.LogError($"<color=red>[WaveSpawnDirector]</color> '{RoomName}': {(s.IsAir ? "hava" : "zemin")} düşmanı için uyumlu EncounterSpawnPoint yok! {s.Remaining} adet {(s.Prefab != null ? s.Prefab.name : "null")} iptal edildi.", this);
            _queuedRemaining -= s.Remaining;
            _totalEnemiesInCurrentWave = Mathf.Max(_enemiesKilledInCurrentWave, _totalEnemiesInCurrentWave - s.Remaining);
            s.Remaining = 0;
            s.LastSpawnTime = _waveElapsedTime;
            _spawnBlockedTimer = 0f;
            OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);
        }

        /// <summary>
        /// Eşzamanlılık kontrolü: dalgada tehdit bütçesi varsa bütçe, yoksa adet sınırı; ayrıca mutlak tavan.
        /// Yalnızca MEVCUT dalgadan canlı + telgrafta bekleyenler sayılır (taşan düşmanlar yeni dalgayı kilitlemez).
        /// </summary>
        private bool HasCapacityFor(WaveDefinition wave, float threat)
        {
            if (absoluteMaxAlive > 0 && _activeEnemies.Count + _pendingSpawns >= absoluteMaxAlive) return false;

            if (wave.ThreatBudget > 0f)
            {
                float used = ThreatAliveInWave(_currentWaveIndex) + _pendingThreat;
                // Sahada hiç tehdit yoksa bütçeden büyük tek bir düşmana da izin ver (kilitlenmeyi önler)
                return used <= 0.0001f || used + threat <= wave.ThreatBudget + 0.0001f;
            }

            return CountAliveInWave(_currentWaveIndex) + _pendingSpawns < wave.MaxConcurrentEnemies;
        }

        /// <summary>
        /// DOOM Eternal ve modern retro shooter standartlarında dalga tamamlama mantığı.
        /// </summary>
        private bool ShouldTransitionToNextWave(WaveDefinition wave)
        {
            // Telgrafta bekleyen (henüz doğmamış) düşmanlar da "kuyruk dolu" sayılır
            bool queueEmpty = _queuedRemaining == 0 && _pendingSpawns == 0;
            bool isFinalWave = (_currentWaveIndex == waves.Length - 1);

            // KURAL: Son wave %100 bitmeden ASLA tamamlanamaz!
            if (isFinalWave)
            {
                return queueEmpty && _activeEnemies.Count == 0 && _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave;
            }

            switch (wave.TransitionMode)
            {
                case WaveTransitionMode.StrictKillAll:
                    return queueEmpty && _activeEnemies.Count == 0 && _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave;

                case WaveTransitionMode.RemainingEnemyThreshold:
                    return queueEmpty && (_activeEnemies.Count <= wave.RemainingThreshold || _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave);

                case WaveTransitionMode.PercentageKilled:
                    if (_totalEnemiesInCurrentWave <= 0) return true;
                    return (float)_enemiesKilledInCurrentWave / _totalEnemiesInCurrentWave >= wave.PercentageThreshold;

                case WaveTransitionMode.TimeLimit:
                    return wave.TimeLimit > 0f && _waveElapsedTime >= wave.TimeLimit;

                case WaveTransitionMode.PercentageOrTimeLimit:
                    if (_totalEnemiesInCurrentWave <= 0) return true;
                    float ratio = (float)_enemiesKilledInCurrentWave / _totalEnemiesInCurrentWave;
                    bool ratioMet = ratio >= wave.PercentageThreshold;
                    bool timeMet = wave.TimeLimit > 0f && _waveElapsedTime >= wave.TimeLimit;
                    return ratioMet || timeMet || (queueEmpty && _activeEnemies.Count == 0);

                default:
                    return queueEmpty && _activeEnemies.Count == 0;
            }
        }

        private void AlertAllActiveEnemies()
        {
            if (_playerTransform == null) return;
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                if (_activeEnemies[i] is IAlertableEnemy alertable)
                {
                    alertable.ReceiveAlert(_playerTransform);
                }
            }
        }

        private void CompleteCurrentWave()
        {
            WaveDefinition completedWave = waves[_currentWaveIndex];
            _state = EncounterState.WaveCooldown;
            _stateTimer = waveCooldownDuration;

            // Süre/yüzde ile erken geçilen dalgada doğmamış grup kalmış olabilir: dalga toplamından düşülsün
            if (_queuedRemaining > 0)
            {
                _totalEnemiesInCurrentWave = Mathf.Max(_enemiesKilledInCurrentWave, _totalEnemiesInCurrentWave - _queuedRemaining);
                OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);
            }
            _streams.Clear();
            _queuedRemaining = 0;

            if (!completedWave.CarryOverRemainingEnemies)
            {
                DespawnRemainingEnemies();
            }

            // Dinamik zorluk değerlendirmesi ve pacing ödülü
            if (_playerHealth != null)
            {
                _difficultyModel.EvaluateWaveEnd(_playerHealth.HealthPercent, _playerHealth.MaxHealth);
            }
            SpawnWaveRewards(completedWave);

            PlaySound(waveClearedSound);
            OnWaveCompleted?.Invoke(_currentWaveIndex + 1);
            InvokeWaveEvents(_currentWaveIndex + 1, false);

            Debug.Log($"<color=green><b>[WAVE TRANSITION]</b></color> '{RoomName}' Wave {_currentWaveIndex + 1} completed. (Time: {_waveElapsedTime:F1}s | Active Enemies: {_activeEnemies.Count} | Bonus Level: {_difficultyModel.BonusLevel})");
        }

        private void DespawnRemainingEnemies()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null)
                {
                    enemy.OnEnemyDeath -= HandleEnemyDeath;
                    enemy.OnDespawnToDirector();
                }
            }
            _activeEnemies.Clear();
            _activeEnemyWave.Clear();
            _activeEnemyThreat.Clear();
            _activeEnemyPrefab.Clear();
            _activeEnemyIsAir.Clear();
            _activeEnemyOutsideTime.Clear();
        }

        /// <summary>Aktif düşman kaydını tüm paralel listelerden birlikte siler.</summary>
        private void RemoveActiveAt(int index)
        {
            _activeEnemies.RemoveAt(index);
            _activeEnemyWave.RemoveAt(index);
            _activeEnemyThreat.RemoveAt(index);
            _activeEnemyPrefab.RemoveAt(index);
            _activeEnemyIsAir.RemoveAt(index);
            _activeEnemyOutsideTime.RemoveAt(index);
        }

        // ───────────────────────────── Kayıp Düşman Kurtarma (Güvenlik Ağı) ─────────────────────────────

        /// <summary>
        /// Yarım saniyede bir aktif düşmanları denetler. Dalganın "herkes öldü ama bitmiyor" durumuna takılmasını önler:
        /// - Ölmüş ama ölümü bildirilmemiş kayıt → öldürülmüş sayılır.
        /// - Yok edilmiş / dışarıdan kapatılmış kayıt → yeniden doğurulur.
        /// - Oda zemininin altına düşmüş veya uzun süre oda dışında kalmış düşman → havuza alınıp yeniden doğurulur.
        /// Kurtarılan düşman öldürülmüş SAYILMAZ; kuyruğa geri girer, yani oyuncu yine öldürmek zorundadır. Tahsissizdir.
        /// </summary>
        private void UpdateLostEnemyRescue()
        {
            _rescueCheckTimer -= Time.deltaTime;
            if (_rescueCheckTimer > 0f) return;
            _rescueCheckTimer = RescueCheckInterval;

            bool hasArea = encounterArea != null;
            Bounds roomBounds = hasArea ? encounterArea.WorldBounds : default;
            float floorY = hasArea ? roomBounds.min.y : _fallbackFloorY;
            if (hasArea) roomBounds.Expand(outOfRoomMargin * 2f);

            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                IEncounterEnemy enemy = _activeEnemies[i];
                bool destroyed = (enemy as Object) == null || enemy.EnemyGameObject == null;

                if (!destroyed && !enemy.IsAlive)
                {
                    // Öldü ama OnEnemyDeath bize ulaşmadı: öldürülmüş say
                    Debug.LogWarning($"<color=yellow>[WaveSpawnDirector]</color> '{RoomName}': '{enemy.EnemyGameObject.name}' ölü ama ölümü bildirilmemişti; öldürülmüş sayıldı.", this);
                    HandleEnemyDeath(enemy);
                    continue;
                }

                if (destroyed || !enemy.EnemyGameObject.activeInHierarchy)
                {
                    RescueEnemy(i, destroyed ? "yok edilmiş" : "dışarıdan kapatılmış");
                    continue;
                }

                Vector3 p = enemy.EnemyTransform.position;
                if (p.y < floorY - fallRescueDepth)
                {
                    RescueEnemy(i, $"dünyadan düştü (y={p.y:F0})");
                    continue;
                }

                if (hasArea && outOfRoomRescueDelay > 0f && !roomBounds.Contains(p))
                {
                    float outside = _activeEnemyOutsideTime[i] + RescueCheckInterval;
                    _activeEnemyOutsideTime[i] = outside;
                    if (outside >= outOfRoomRescueDelay)
                    {
                        RescueEnemy(i, $"{outside:F0} sn oda dışında kaldı ({p})");
                    }
                }
                else
                {
                    _activeEnemyOutsideTime[i] = 0f;
                }
            }
        }

        /// <summary>
        /// Kayıp düşmanı havuza alır; mevcut dalgaya aitse aynı prefabı tek kişilik bir akışla yeniden kuyruğa koyar.
        /// Dalga toplamı ve öldürme sayısı değişmez (oyuncu yeniden doğanı öldürünce dalga normal biter).
        /// </summary>
        private void RescueEnemy(int index, string reason)
        {
            IEncounterEnemy enemy = _activeEnemies[index];
            int wave = _activeEnemyWave[index];
            float threat = _activeEnemyThreat[index];
            GameObject prefab = _activeEnemyPrefab[index];
            bool isAir = _activeEnemyIsAir[index];

            RemoveActiveAt(index);
            _rescuedEnemyCount++;

            string enemyName = prefab != null ? prefab.name : "?";
            if ((enemy as Object) != null)
            {
                enemyName = enemy.EnemyGameObject.name;
                enemy.OnEnemyDeath -= HandleEnemyDeath;
                enemy.OnDespawnToDirector();
            }

            bool requeue = wave == _currentWaveIndex && prefab != null && _state == EncounterState.WaveSpawningAndCombat;
            if (requeue)
            {
                _streams.Add(new SpawnStream
                {
                    Prefab = prefab,
                    IsAir = isAir,
                    ZoneTag = null,
                    Interval = 0.5f,
                    BatchSize = 1,
                    Pattern = SpawnPattern.Random,
                    Threat = threat,
                    Delay = 0f,
                    Started = true,
                    Remaining = 1,
                    NextTime = _waveElapsedTime,
                    LastSpawnTime = _waveElapsedTime,
                    RoundRobinCursor = 0
                });
                _queuedRemaining++;
            }

            Debug.LogWarning($"<color=yellow>[WaveSpawnDirector]</color> '{RoomName}': kayıp düşman kurtarıldı → '{enemyName}' {reason}. {(requeue ? "Yeniden doğurulacak." : "Önceki dalgaya aitti, kaldırıldı.")}", this);
        }

        private void CompleteEncounter()
        {
            _state = EncounterState.EncounterCleared;
            SetBarriersActive(false);
            ReleaseRetryInterceptor();

            PlaySound(victorySound);
            OnEncounterCompleted?.Invoke();
            onEncounterCompleted?.Invoke();

            Debug.Log($"<color=gold><b>[ARENA VICTORY!]</b></color> '{RoomName}' all waves purged, barriers lifted!");
        }

        private void InvokeWaveEvents(int oneBasedWave, bool isStart)
        {
            if (waveEvents == null) return;
            for (int i = 0; i < waveEvents.Length; i++)
            {
                WaveEventBinding b = waveEvents[i];
                if (b == null || !b.Matches(oneBasedWave)) continue;

                UnityEvent evt = isStart ? b.OnWaveStart : b.OnWaveEnd;
                evt?.Invoke();
            }
        }

        // ───────────────────────────── Ödül (Pacing Kancası) ─────────────────────────────

        private void SpawnWaveRewards(WaveDefinition wave)
        {
            if (_playerTransform == null) return;

            int orbs = wave.RewardHealthOrbs;
            int ammo = wave.RewardAmmoPickups;
            int total = orbs + ammo;
            if (total <= 0) return;

            int slot = 0;
            if (HealthOrbManager.Instance != null)
            {
                for (int i = 0; i < orbs; i++)
                {
                    HealthOrbManager.Instance.SpawnOrb(GetRewardDropPosition(slot++, total), rewardOrbHealAmount);
                }
            }
            else
            {
                slot += orbs;
            }

            for (int i = 0; i < ammo; i++)
            {
                AmmoType type = ChooseRewardAmmoType();
                int amount = type == AmmoType.Shells ? rewardShellAmount : rewardBoltAmount;
                AmmoPickup.CreatePickupGameObject(GetRewardDropPosition(slot++, total), type, amount);
            }
        }

        private Vector3 GetRewardDropPosition(int slot, int total)
        {
            Vector3 origin = _playerTransform.position + Vector3.up;
            float angle = (360f / Mathf.Max(1, total)) * slot + _playerTransform.eulerAngles.y;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            float dist = rewardScatterRadius;
            if (Physics.Raycast(origin, dir, out RaycastHit wallHit, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                dist = Mathf.Max(0.3f, wallHit.distance - 0.5f);
            }

            Vector3 p = origin + dir * dist;
            if (Physics.Raycast(p, Vector3.down, out RaycastHit groundHit, 5f, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                p = groundHit.point + Vector3.up * 0.6f;
            }
            return p;
        }

        private AmmoType ChooseRewardAmmoType()
        {
            if (_ammoInventory == null) return Random.value > 0.5f ? AmmoType.Shells : AmmoType.Bolts;

            float shells = _ammoInventory.GetAmmoRatio(AmmoType.Shells);
            float bolts = _ammoInventory.GetAmmoRatio(AmmoType.Bolts);
            if (shells < bolts) return AmmoType.Shells;
            if (bolts < shells) return AmmoType.Bolts;
            return Random.value > 0.5f ? AmmoType.Shells : AmmoType.Bolts;
        }

        // ───────────────────────────── Dinamik Zorluk & Retry ─────────────────────────────

        private void HandlePlayerDamaged(float amount)
        {
            if (_state == EncounterState.WaveSpawningAndCombat || _state == EncounterState.WaveCooldown)
            {
                _difficultyModel.RegisterDamage(amount);
            }
        }

        /// <inheritdoc />
        public bool CanResumeAfterDeath =>
            enableCheckpointRetry &&
            _playerHealth != null &&
            _hasCheckpoint &&
            _state != EncounterState.Inactive &&
            _state != EncounterState.EncounterCleared;

        /// <inheritdoc />
        public void SuspendForPurgatory()
        {
            if (!CanResumeAfterDeath) return;

            _difficultyModel.RegisterPlayerDeath();
            ResetEncounter();
            _suspendedForPurgatory = true;
        }

        /// <inheritdoc />
        public void ResumeAfterPurgatory()
        {
            if (!_suspendedForPurgatory) return;

            _suspendedForPurgatory = false;
            _playerHealth.Revive(_checkpointPosition, _checkpointRotation);

            OnEncounterRetried?.Invoke();
            onEncounterRetried?.Invoke();

            StartEncounterInternal(retryWarmupDuration, true);
        }

        /// <summary>
        /// Oyuncu öldükten sonra yeniden başlatma istediğinde PlayerHealth tarafından çağrılır.
        /// Bu oda savaştaysa karşılaşmayı sıfırlar, oyuncuyu checkpoint'te diriltir ve dalgaları baştan başlatır.
        /// </summary>
        /// <returns>Yeniden deneme bu oda tarafından üstlenildiyse true (sahne yeniden yüklenmez).</returns>
        private bool HandleRetryRequest()
        {
            if (!CanResumeAfterDeath) return false;

            SuspendForPurgatory();
            ResumeAfterPurgatory();
            return true;
        }

        private void ReleaseRetryInterceptor()
        {
            if (_playerHealth != null && _playerHealth.RetryInterceptor == _retryHandler)
            {
                _playerHealth.RetryInterceptor = null;
            }
            RetroFPS.Run.RunEvents.UnregisterActiveEncounter(this);
        }

        // ───────────────────────────── Doğuş Noktası Seçimi ─────────────────────────────

        /// <summary>
        /// Kademeli gevşetme (Tier) ile adayları toplar ve grubun desenine göre birini seçer.
        /// Fiziksel engel kontrolü her kademede korunur. Hiç aday yoksa ve zaman aşımı dolduysa
        /// yalnızca tip uyumuna bakan acil seçim yapılır.
        /// </summary>
        private EncounterSpawnPoint FindPointForStream(ref SpawnStream s)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : transform.position;
            bool hasZoneFilter = !string.IsNullOrEmpty(s.ZoneTag);

            //   Tier 0: Bölge etiketi + mesafe + cooldown + engel
            //   Tier 1: Tüm bölgeler + mesafe + cooldown + engel
            //   Tier 2: Mesafe sınırı gevşer
            //   Tier 3: Cooldown da gevşer
            for (int tier = hasZoneFilter ? 0 : 1; tier <= 3; tier++)
            {
                CollectCandidates(s.IsAir, tier == 0 ? s.ZoneTag : null, tier <= 1, tier <= 2, playerPos);
                if (_candidatePoints.Count > 0)
                {
                    return PickByPattern(ref s, playerPos);
                }
            }

            return _forceEmergencyPick ? FindAnyCompatiblePoint(s.IsAir) : null;
        }

        private void CollectCandidates(bool isAirEnemy, string zoneFilter, bool respectDistance, bool respectCooldown, Vector3 playerPos)
        {
            _candidatePoints.Clear();
            _candidateIndices.Clear();
            bool hasZoneFilter = !string.IsNullOrEmpty(zoneFilter);
            float minSqr = minSpawnDistance * minSpawnDistance;
            float maxSqr = maxSpawnDistance * maxSpawnDistance;
            float reservation = ReservationWindow;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EncounterSpawnPoint sp = spawnPoints[i];
                if (sp == null) continue;
                // Rezervasyon hiçbir kademede gevşetilmez (aynı noktaya üst üste doğuşu engeller)
                if (sp.IsReserved(reservation)) continue;
                if (respectCooldown && sp.IsOnCooldown) continue;

                if (isAirEnemy ? !sp.AllowAirEnemies : !sp.AllowGroundEnemies) continue;

                if (hasZoneFilter && !sp.ZoneTag.Equals(zoneFilter, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (respectDistance)
                {
                    float sqr = (playerPos - sp.Position).sqrMagnitude;
                    if (sqr < minSqr || sqr > maxSqr) continue;
                }

                if (!sp.IsPhysicallyClear(obstacleLayerMask)) continue;

                _candidatePoints.Add(sp);
                _candidateIndices.Add(i);
            }
        }

        private EncounterSpawnPoint PickByPattern(ref SpawnStream s, Vector3 playerPos)
        {
            switch (s.Pattern)
            {
                case SpawnPattern.SequentialRoundRobin:
                {
                    // İmleçten sonraki ilk aday (yoksa baştan sar)
                    int best = -1;
                    int wrap = -1;
                    for (int i = 0; i < _candidateIndices.Count; i++)
                    {
                        int idx = _candidateIndices[i];
                        if (idx >= s.RoundRobinCursor && (best < 0 || idx < _candidateIndices[best])) best = i;
                        if (wrap < 0 || idx < _candidateIndices[wrap]) wrap = i;
                    }
                    int chosen = best >= 0 ? best : wrap;
                    s.RoundRobinCursor = _candidateIndices[chosen] + 1;
                    return _candidatePoints[chosen];
                }

                case SpawnPattern.FarthestFromPlayer:
                {
                    int best = 0;
                    float bestSqr = -1f;
                    for (int i = 0; i < _candidatePoints.Count; i++)
                    {
                        float sqr = (_candidatePoints[i].Position - playerPos).sqrMagnitude;
                        if (sqr > bestSqr)
                        {
                            bestSqr = sqr;
                            best = i;
                        }
                    }
                    return _candidatePoints[best];
                }

                default:
                    return PickFlankWeighted();
            }
        }

        /// <summary>
        /// Flank ağırlıklı rastgele seçim: arkadaki noktalar &gt; yandakiler &gt; öndekiler.
        /// </summary>
        private EncounterSpawnPoint PickFlankWeighted()
        {
            if (!preferOutOfViewSpawns || _candidatePoints.Count == 1)
            {
                return _candidatePoints[Random.Range(0, _candidatePoints.Count)];
            }

            Transform viewT = _playerCamera != null ? _playerCamera.transform : _playerTransform;
            if (viewT == null)
            {
                return _candidatePoints[Random.Range(0, _candidatePoints.Count)];
            }

            Vector3 viewPos = viewT.position;
            Vector3 viewFwd = viewT.forward;
            viewFwd.y = 0f;
            if (viewFwd.sqrMagnitude < 0.0001f) viewFwd = Vector3.forward;
            viewFwd.Normalize();

            _candidateWeights.Clear();
            float totalWeight = 0f;
            for (int i = 0; i < _candidatePoints.Count; i++)
            {
                float w = GetFlankWeight(_candidatePoints[i].Position, viewPos, viewFwd);
                _candidateWeights.Add(w);
                totalWeight += w;
            }

            float r = Random.value * totalWeight;
            for (int i = 0; i < _candidateWeights.Count; i++)
            {
                r -= _candidateWeights[i];
                if (r <= 0f) return _candidatePoints[i];
            }
            return _candidatePoints[_candidatePoints.Count - 1];
        }

        private float GetFlankWeight(Vector3 pointPos, Vector3 viewPos, Vector3 viewFwdFlat)
        {
            Vector3 to = pointPos - viewPos;
            to.y = 0f;
            float mag = to.magnitude;
            if (mag < 0.01f) return Mathf.Max(0.01f, frontSpawnWeight);

            float dot = Vector3.Dot(viewFwdFlat, to / mag);
            if (dot >= outOfViewDotThreshold) return Mathf.Max(0.01f, frontSpawnWeight);
            if (dot <= rearDotThreshold) return Mathf.Max(0.01f, rearSpawnWeight);
            return Mathf.Max(0.01f, sideSpawnWeight);
        }

        /// <summary>
        /// Son çare: yalnızca tip uyumluluğuna (zemin/hava) bakar. Hiç uyumlu nokta yoksa null döner.
        /// </summary>
        private EncounterSpawnPoint FindAnyCompatiblePoint(bool isAirEnemy)
        {
            if (spawnPoints == null) return null;

            // Acil seçim de iki şeyi ASLA gevşetmez: rezervasyon (üst üste doğuş) ve statik geometri
            // (duvar/siper içindeki nokta). Aksi halde düşman duvarın içinde doğup dünyadan düşebiliyordu.
            float reservation = ReservationWindow;
            _candidatePoints.Clear();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EncounterSpawnPoint sp = spawnPoints[i];
                if (sp == null) continue;
                if (isAirEnemy ? !sp.AllowAirEnemies : !sp.AllowGroundEnemies) continue;
                if (sp.IsReserved(reservation)) continue;
                if (!sp.IsClearOfStaticGeometry(obstacleLayerMask)) continue;
                _candidatePoints.Add(sp);
            }

            return _candidatePoints.Count > 0 ? _candidatePoints[Random.Range(0, _candidatePoints.Count)] : null;
        }

        private void CacheFallbackFloor()
        {
            _fallbackFloorY = transform.position.y;
            if (spawnPoints == null) return;

            bool found = false;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                float y = spawnPoints[i].Position.y;
                if (!found || y < _fallbackFloorY)
                {
                    _fallbackFloorY = y;
                    found = true;
                }
            }
        }

        private float ReservationWindow => summonTelegraphDuration + Mathf.Max(0f, spawnReservationPadding);

        /// <summary>
        /// Bu tip (zemin/hava) için statik geometrinin içinde KALMAYAN en az bir nokta var mı?
        /// Yoksa grup kalıcı olarak doğurulamaz ve düşürülür; varsa noktalar yalnızca geçici olarak doludur.
        /// </summary>
        private bool HasAnyStaticallyValidPoint(bool isAirEnemy)
        {
            if (spawnPoints == null) return false;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EncounterSpawnPoint sp = spawnPoints[i];
                if (sp == null) continue;
                if (isAirEnemy ? !sp.AllowAirEnemies : !sp.AllowGroundEnemies) continue;
                if (sp.IsClearOfStaticGeometry(obstacleLayerMask)) return true;
            }
            return false;
        }

        private int CountAliveInWave(int waveIndex)
        {
            int count = 0;
            for (int i = 0; i < _activeEnemyWave.Count; i++)
            {
                if (_activeEnemyWave[i] == waveIndex) count++;
            }
            return count;
        }

        private float ThreatAliveInWave(int waveIndex)
        {
            float sum = 0f;
            for (int i = 0; i < _activeEnemyWave.Count; i++)
            {
                if (_activeEnemyWave[i] == waveIndex) sum += _activeEnemyThreat[i];
            }
            return sum;
        }

        // ───────────────────────────── Doğuş & Havuz ─────────────────────────────

        /// <summary>
        /// Telgraf çemberini tetikler, süre bitince havuzdaki düşmanı uyandırır.
        /// </summary>
        private void SpawnEnemyWithTelegraph(GameObject prefab, Vector3 position, Quaternion rotation, float threat, bool isAir)
        {
            OccultSpawnTelegraph telegraph = GetPooledTelegraph();
            if (telegraph != null)
            {
                // Telgraf bitene kadar bu doğuş sınıra "bekleyen" olarak sayılır; lambda yerine önbellekli delege (Sıfır GC)
                _pendingSpawns++;
                _pendingThreat += threat;
                telegraph.SetPayload(prefab, _currentWaveIndex, _encounterGeneration, threat, isAir);
                telegraph.Activate(position, rotation, summonTelegraphDuration, _onTelegraphCompleted);
                telegraph.PlayCue(summonTelegraphSound);
            }
            else
            {
                MaterializeEnemyFromPool(prefab, position, rotation, _currentWaveIndex, threat, isAir);
            }
        }

        private void HandleTelegraphCompleted(OccultSpawnTelegraph telegraph)
        {
            // Reset/yeniden başlatma sonrası eski nesil telgraflar yok sayılır (sayaçlar zaten sıfırlanmıştır)
            if (telegraph.PayloadGeneration != _encounterGeneration) return;

            _pendingSpawns = Mathf.Max(0, _pendingSpawns - 1);
            _pendingThreat = Mathf.Max(0f, _pendingThreat - telegraph.PayloadThreat);

            int waveIndex = telegraph.PayloadWaveIndex;
            bool waveCarriesOver = waves != null && waveIndex >= 0 && waveIndex < waves.Length
                ? (waveIndex == _currentWaveIndex || waves[waveIndex].CarryOverRemainingEnemies)
                : true;

            if (_state == EncounterState.Inactive || !waveCarriesOver) return;

            MaterializeEnemyFromPool(telegraph.PayloadPrefab, telegraph.transform.position, telegraph.transform.rotation, waveIndex, telegraph.PayloadThreat, telegraph.PayloadIsAir);
        }

        private void MaterializeEnemyFromPool(GameObject prefab, Vector3 position, Quaternion rotation, int waveIndex, float threat, bool isAir)
        {
            if (prefab == null)
            {
                Debug.LogError("<color=red>[WaveSpawnDirector]</color> MaterializeEnemyFromPool: Doğurulacak düşman prefabı null! Spawn işlemi iptal edildi.");
                return;
            }

            GameObject enemyGo = GetPooledEnemy(prefab);
            if (enemyGo == null)
            {
                enemyGo = Instantiate(prefab, position, rotation, _poolContainer);
            }

            IEncounterEnemy encounterEnemy = enemyGo.GetComponent<IEncounterEnemy>();
            if (encounterEnemy != null)
            {
                encounterEnemy.OnEnemyDeath -= HandleEnemyDeath;
                encounterEnemy.OnEnemyDeath += HandleEnemyDeath;
                encounterEnemy.OnSpawnFromDirector(position, rotation);
                _activeEnemies.Add(encounterEnemy);
                _activeEnemyWave.Add(waveIndex);
                _activeEnemyThreat.Add(threat);
                _activeEnemyPrefab.Add(prefab);
                _activeEnemyIsAir.Add(isAir);
                _activeEnemyOutsideTime.Add(0f);
            }
            else
            {
                enemyGo.transform.position = position;
                enemyGo.transform.rotation = rotation;
                enemyGo.SetActive(true);
            }
        }

        private void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            if (enemy == null) return;

            enemy.OnEnemyDeath -= HandleEnemyDeath;

            // Ölümü doğduğu dalgaya yaz: önceki dalgadan taşan bir düşmanın ölümü, yeni dalganın yüzde sayacını şişirmesin.
            int spawnedWave = _currentWaveIndex;
            int idx = _activeEnemies.IndexOf(enemy);
            if (idx >= 0)
            {
                spawnedWave = _activeEnemyWave[idx];
                RemoveActiveAt(idx);
            }

            if (spawnedWave == _currentWaveIndex)
            {
                _enemiesKilledInCurrentWave++;
            }
            _totalEnemiesKilledInEncounter++;

            OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);
        }

        private GameObject GetPooledEnemy(GameObject prefab)
        {
            if (!_enemyPools.TryGetValue(prefab, out List<GameObject> poolList))
            {
                poolList = new List<GameObject>(4);
                _enemyPools[prefab] = poolList;
            }

            for (int i = 0; i < poolList.Count; i++)
            {
                GameObject obj = poolList[i];
                if (obj != null && !obj.activeInHierarchy)
                {
                    return obj;
                }
            }

            // Havuz yetersiz kaldıysa dinamik genişlet (dinamik zorluk ek düşmanları vb.)
            GameObject extra = Instantiate(prefab, _poolContainer);
            extra.name = $"{prefab.name}_Pooled_Extra";
            extra.SetActive(false);
            poolList.Add(extra);
            return extra;
        }

        private OccultSpawnTelegraph GetPooledTelegraph()
        {
            for (int i = 0; i < _telegraphPool.Count; i++)
            {
                OccultSpawnTelegraph t = _telegraphPool[i];
                if (t != null && !t.gameObject.activeInHierarchy)
                {
                    return t;
                }
            }

            GameObject go = new GameObject("OccultTelegraph_Pooled_Extra");
            go.transform.SetParent(_poolContainer);
            OccultSpawnTelegraph extra = go.AddComponent<OccultSpawnTelegraph>();
            go.SetActive(false);
            _telegraphPool.Add(extra);
            return extra;
        }

        private void SetBarriersActive(bool active)
        {
            if (arenaBarriers == null) return;
            for (int i = 0; i < arenaBarriers.Length; i++)
            {
                if (arenaBarriers[i] != null)
                {
                    arenaBarriers[i].SetActive(active);
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Karşılaşmayı sıfırlar ve tüm düşmanları anında havuza çeker. Dinamik zorluk geçmişi korunur.
        /// </summary>
        public void ResetEncounter()
        {
            _state = EncounterState.Inactive;
            _currentWaveIndex = -1;
            _stateTimer = 0f;
            _waveElapsedTime = 0f;
            _totalEnemiesKilledInEncounter = 0;
            _enemiesKilledInCurrentWave = 0;
            _totalEnemiesInCurrentWave = 0;
            _streams.Clear();
            _queuedRemaining = 0;
            _spawnBlockedTimer = 0f;
            _forceEmergencyPick = false;

            // Havadaki telgrafları iptal et ve eski nesil callback'lerini geçersiz kıl
            _encounterGeneration++;
            _pendingSpawns = 0;
            _pendingThreat = 0f;
            for (int i = 0; i < _telegraphPool.Count; i++)
            {
                if (_telegraphPool[i] != null) _telegraphPool[i].Cancel();
            }

            DespawnRemainingEnemies();

            if (spawnPoints != null)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] != null) spawnPoints[i].ResetCooldown();
                }
            }

            ReleaseRetryInterceptor();
            SetBarriersActive(false);
        }

        /// <summary>
        /// Kod veya editör üzerinden dalga konfigürasyonunu toplu atar.
        /// </summary>
        public void ConfigureWaves(WaveDefinition[] newWaves)
        {
            waves = newWaves;
            _runtimeWaveSet = null;
            _usingWaveSet = false;
            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    waves[i]?.CalculateMetrics();
                }
            }
            _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Kill All Active Enemies")]
        public void DebugKillAllActiveEnemies()
        {
            if (!Application.isPlaying) return;
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy != null && enemy.EnemyGameObject != null)
                {
                    IDamageable d = enemy.EnemyGameObject.GetComponent<IDamageable>();
                    if (d != null)
                    {
                        d.TakeDamage(9999f, enemy.EnemyTransform.position, Vector3.up, true);
                    }
                    else
                    {
                        enemy.OnDespawnToDirector();
                    }
                }
            }
        }

        [ContextMenu("Debug: Skip To Next Wave")]
        public void DebugSkipToNextWave()
        {
            if (!Application.isPlaying || _state != EncounterState.WaveSpawningAndCombat) return;
            DebugKillAllActiveEnemies();
            CompleteCurrentWave();
        }

        [ContextMenu("Debug: Restart Encounter")]
        public void DebugRestartEncounter()
        {
            if (!Application.isPlaying) return;
            ResetEncounter();
            StartEncounter();
        }

        [ContextMenu("Debug: Simulate Player Death Retry")]
        public void DebugSimulateRetry()
        {
            if (!Application.isPlaying) return;
            if (!HandleRetryRequest())
            {
                Debug.LogWarning($"[WaveSpawnDirector] '{RoomName}' retry üstlenmedi (savaşta değil, retry kapalı veya checkpoint yok).", this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnPoints != null)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    EncounterSpawnPoint sp = spawnPoints[i];
                    if (sp == null) continue;

                    Gizmos.color = sp.IsOnCooldown ? Color.gray : new Color(0.2f, 0.85f, 1f, 0.5f);
                    Gizmos.DrawLine(transform.position, sp.Position);
                }
            }

            if (retryCheckpoint != null)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
                Gizmos.DrawWireSphere(retryCheckpoint.position + Vector3.up, 0.5f);
                Gizmos.DrawLine(retryCheckpoint.position + Vector3.up, retryCheckpoint.position + Vector3.up + retryCheckpoint.forward * 1.5f);
            }
        }
#endif
    }
}
