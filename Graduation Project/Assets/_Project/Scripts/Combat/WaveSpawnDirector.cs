using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.Combat
{
    public enum EncounterState
    {
        Inactive,
        Warmup,
        WaveSpawningAndCombat,
        WaveCooldown,
        EncounterCleared
    }

    /// <summary>
    /// Sektör standardı dalga geçiş ve tempo kontrolü kuralı (DOOM Eternal / Ultrakill / Quake Pacing).
    /// </summary>
    public enum WaveTransitionMode
    {
        [Tooltip("Dalgadaki tüm düşmanlar ve kuyruk tamamen yok edilene kadar bekle (Klasik / Boss dalgası modu)")]
        StrictKillAll,

        [Tooltip("Canlı kalan düşman sayısı bu eşiğe (veya altına) indiğinde bir sonraki dalgayı başlat")]
        RemainingEnemyThreshold,

        [Tooltip("Dalgadaki toplam düşmanların belirli bir yüzdesi (%60, %75 vb.) öldüğünde yeni dalgayı getir")]
        PercentageKilled,

        [Tooltip("Belirli bir saniye sonra zorunlu olarak sonraki dalgaya geç (Tempo koruyucu zaman aşımı)")]
        TimeLimit,

        [Tooltip("DOOM Pacing Standardı: Yüzde eşiği VEYA Süre aşımı (Hangisi önce dolarsa sonraki dalga başlar)")]
        PercentageOrTimeLimit
    }

    /// <summary>
    /// Bir dalga içerisinde doğurulacak belirli bir düşman grubu konfigürasyonu.
    /// </summary>
    [System.Serializable]
    public struct EnemySpawnEntry
    {
        [Tooltip("Doğurulacak düşman prefabı (IEncounterEnemy veya BasicEnemyAI/FlyingEyeAI içermelidir)")]
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("Bu grupta toplam kaç adet düşman doğacak")]
        [SerializeField] private int count;

        [Tooltip("Düşmanlar arası doğma aralığı (saniye)")]
        [SerializeField] private float spawnInterval;

        [Tooltip("Uçan bir düşman mı? (Doğuş noktası hava filtresi için)")]
        [SerializeField] private bool isAirEnemy;

        [Tooltip("Doğacağı özel bölge etiketi (Örn: 'Balkon', 'Meydan'). Boş bırakılırsa tüm uygun noktalar kullanılır.")]
        [SerializeField] private string targetZoneTag;

        public GameObject EnemyPrefab => enemyPrefab;
        public int Count => Mathf.Max(1, count);
        public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
        public bool IsAirEnemy => isAirEnemy;
        public string TargetZoneTag => targetZoneTag;

        public EnemySpawnEntry(GameObject prefab, int count, float interval, bool isAir = false, string zone = "")
        {
            this.enemyPrefab = prefab;
            this.count = count;
            this.spawnInterval = interval;
            this.isAirEnemy = isAir;
            this.targetZoneTag = zone ?? string.Empty;
        }
    }

    /// <summary>
    /// Tek bir karşılaşma dalgası (Wave) tanımı.
    /// </summary>
    [System.Serializable]
    public class WaveDefinition
    {
        [Tooltip("Dalga Başlığı (Örn: 1. Dalga: Leş Yiyicilerin Çıkışı)")]
        [SerializeField] private string waveName = "Wave 1";

        [Tooltip("Bu dalga başlamadan önceki hazırlık süresi (saniye)")]
        [SerializeField] private float preWaveDelay = 2.0f;

        [Tooltip("Aynı anda sahada canlı kalabilecek maksimum düşman sayısı (Performans & Yoğunluk Bütçelemesi)")]
        [SerializeField] private int maxConcurrentEnemies = 6;

        [Header("Dalga Geçiş & Tempo Kriteri (Sektör Standardı)")]
        [Tooltip("Bu dalgadan sonraki dalgaya nasıl geçileceğini belirler")]
        [SerializeField] private WaveTransitionMode transitionMode = WaveTransitionMode.PercentageOrTimeLimit;

        [Tooltip("RemainingEnemyThreshold modunda: Sahada bu sayı veya daha az canlı kaldığında sonraki dalga başlar")]
        [SerializeField] private int remainingThreshold = 2;

        [Tooltip("PercentageKilled veya PercentageOrTimeLimit modunda: Düşmanların yüzde kaçı öldüğünde tetiklensin? (0.75 = %75)")]
        [Range(0.2f, 1.0f)]
        [SerializeField] private float percentageThreshold = 0.75f;

        [Tooltip("TimeLimit veya PercentageOrTimeLimit modunda: Kaç saniye sonra zorunlu sonraki dalga başlasın? (0 = Sınırsız)")]
        [SerializeField] private float timeLimit = 40.0f;

        [Tooltip("Sonraki dalga başladığında bu dalgadan sağ kalan düşmanlar ne olsun? (True = Sahada kalıp yeni dalgaya katılırlar; False = Temizlenirler)")]
        [SerializeField] private bool carryOverRemainingEnemies = true;

        [Header("Düşman Grupları")]
        [Tooltip("Bu dalgada sırayla sahaya sürülecek düşman grupları")]
        [SerializeField] private EnemySpawnEntry[] spawnEntries;

        public WaveDefinition() { }

        public WaveDefinition(string waveName, float preWaveDelay, int maxConcurrentEnemies, EnemySpawnEntry[] spawnEntries,
            WaveTransitionMode transitionMode = WaveTransitionMode.PercentageOrTimeLimit, int remainingThreshold = 2,
            float percentageThreshold = 0.75f, float timeLimit = 40.0f, bool carryOver = true)
        {
            this.waveName = waveName;
            this.preWaveDelay = preWaveDelay;
            this.maxConcurrentEnemies = maxConcurrentEnemies;
            this.spawnEntries = spawnEntries;
            this.transitionMode = transitionMode;
            this.remainingThreshold = remainingThreshold;
            this.percentageThreshold = percentageThreshold;
            this.timeLimit = timeLimit;
            this.carryOverRemainingEnemies = carryOver;
        }

        public string WaveName => waveName;
        public float PreWaveDelay => preWaveDelay;
        public int MaxConcurrentEnemies => Mathf.Max(1, maxConcurrentEnemies);
        public WaveTransitionMode TransitionMode => transitionMode;
        public int RemainingThreshold => Mathf.Max(0, remainingThreshold);
        public float PercentageThreshold => Mathf.Clamp01(percentageThreshold);
        public float TimeLimit => timeLimit;
        public bool CarryOverRemainingEnemies => carryOverRemainingEnemies;
        public EnemySpawnEntry[] SpawnEntries => spawnEntries;

        [System.NonSerialized] private int _cachedTotalEnemies = -1;
        [System.NonSerialized] private int _cachedAirEnemies = -1;
        [System.NonSerialized] private int _cachedGroundEnemies = -1;
        [System.NonSerialized] private string _cachedCompositionSummary = null;
        [System.NonSerialized] private string _cachedDensityRating = null;

        public void InvalidateCache()
        {
            _cachedTotalEnemies = -1;
            _cachedAirEnemies = -1;
            _cachedGroundEnemies = -1;
            _cachedCompositionSummary = null;
            _cachedDensityRating = null;
        }

        public int TotalEnemies
        {
            get
            {
                if (_cachedTotalEnemies < 0) CalculateMetrics();
                return _cachedTotalEnemies;
            }
        }

        public int AirEnemies
        {
            get
            {
                if (_cachedAirEnemies < 0) CalculateMetrics();
                return _cachedAirEnemies;
            }
        }

        public int GroundEnemies
        {
            get
            {
                if (_cachedGroundEnemies < 0) CalculateMetrics();
                return _cachedGroundEnemies;
            }
        }

        public string CompositionSummary
        {
            get
            {
                if (_cachedCompositionSummary == null) CalculateMetrics();
                return _cachedCompositionSummary;
            }
        }

        public string DensityRating
        {
            get
            {
                if (_cachedDensityRating == null) CalculateMetrics();
                return _cachedDensityRating;
            }
        }

        public void CalculateMetrics()
        {
            int total = 0;
            int air = 0;
            int ground = 0;

            if (spawnEntries != null && spawnEntries.Length > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
                for (int i = 0; i < spawnEntries.Length; i++)
                {
                    int c = spawnEntries[i].Count;
                    total += c;
                    if (spawnEntries[i].IsAirEnemy) air += c;
                    else ground += c;

                    if (i > 0) sb.Append(", ");
                    sb.Append(c).Append("x ");

                    string name = "Enemy";
                    if (spawnEntries[i].EnemyPrefab != null)
                    {
                        name = CleanEnemyName(spawnEntries[i].EnemyPrefab.name);
                    }
                    sb.Append(name);
                }
                _cachedCompositionSummary = sb.ToString();
            }
            else
            {
                _cachedCompositionSummary = "Empty Wave";
            }

            _cachedTotalEnemies = total;
            _cachedAirEnemies = air;
            _cachedGroundEnemies = ground;

            int cap = MaxConcurrentEnemies;
            if (total >= 12 || cap >= 8) _cachedDensityRating = "Extreme";
            else if (total >= 7 || cap >= 6) _cachedDensityRating = "High";
            else if (total >= 4 || cap >= 4) _cachedDensityRating = "Medium";
            else _cachedDensityRating = "Low";
        }

        private static string CleanEnemyName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Enemy";
            string s = raw;
            if (s.StartsWith("Enemy_")) s = s.Substring(6);
            int pooledIdx = s.IndexOf("_Pooled");
            if (pooledIdx > 0) s = s.Substring(0, pooledIdx);
            return s.Replace('_', ' ');
        }

        public int CalculateTotalEnemies()
        {
            return TotalEnemies;
        }
    }

    /// <summary>
    /// İç kuyrukta bekleyen tek bir doğuş talebi (Sıfır GC için struct).
    /// </summary>
    public struct QueuedSpawnRequest
    {
        public GameObject Prefab;
        public bool IsAirEnemy;
        public float Interval;
        public string TargetZoneTag;

        public QueuedSpawnRequest(GameObject prefab, bool isAir, float interval, string targetZone = "")
        {
            Prefab = prefab;
            IsAirEnemy = isAir;
            Interval = interval;
            TargetZoneTag = targetZone ?? string.Empty;
        }
    }

    /// <summary>
    /// Karşılaşma ve Dalga Direktörü (WaveSpawnDirector).
    /// Ultrakill, DOOM ve retro boomer shooter dinamiklerine göre optimize edilmiştir.
    /// - Sıfır GC Tahsisi (Önceden ısıtılmış Nesne Havuzu / Object Pool)
    /// - Eşzamanlılık Bütçelemesi (Max Concurrent Alive Cap ile FPS düşüşlerini önler)
    /// - Akıllı Doğuş Noktası Seçimi (Mesafe puanlaması, LoS ve fiziksel engelsizlik denetimi)
    /// - Okült Çağırma Telgrafı (Occult Telegraph Marker ile oyuncuya adil tepki süresi)
    /// - Arena Karantinası (Bariyer/kapı kilitleme ve zaferde açma entegrasyonu)
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveSpawnDirector : MonoBehaviour
    {
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
        [Tooltip("Bu karşılaşmayı başlatacak olan EncounterArea (varsa). Otomatik algılanır.")]
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
        [SerializeField] private WaveDefinition[] waves;

        [Header("Doğuş Noktaları ve Konumlandırma")]
        [Tooltip("Arenadaki doğuş noktaları. Boş bırakılırsa sahnede otomatik aranır.")]
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

        [Header("Ses Efektleri")]
        [SerializeField] private AudioClip encounterStartSound;
        [SerializeField] private AudioClip waveStartSound;
        [SerializeField] private AudioClip waveClearedSound;
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip summonTelegraphSound;

        // Karşılaşma Durum Bilgileri
        private EncounterState _state = EncounterState.Inactive;
        private int _currentWaveIndex = -1;
        private float _stateTimer = 0f;
        private float _spawnCooldownTimer = 0f;
        private float _waveElapsedTime = 0f;
        private float _stragglerAlertTimer = 0f;

        private int _totalEnemiesInCurrentWave = 0;
        private int _enemiesKilledInCurrentWave = 0;
        private int _totalEnemiesInAllWaves = 0;
        private int _totalEnemiesKilledInEncounter = 0;

        // Önbelleğe Alınmış Referanslar
        private Transform _playerTransform;
        private AudioSource _audioSource;
        private Transform _poolContainer;

        // Sıfır GC Listeleri ve Havuz Veri Yapıları
        private readonly List<IEncounterEnemy> _activeEnemies = new List<IEncounterEnemy>(32);
        private readonly List<QueuedSpawnRequest> _spawnQueue = new List<QueuedSpawnRequest>(64);
        private readonly List<EncounterSpawnPoint> _candidatePoints = new List<EncounterSpawnPoint>(32);

        // Prefab -> Pre-warmed Nesne Havuzu
        private readonly Dictionary<GameObject, List<GameObject>> _enemyPools = new Dictionary<GameObject, List<GameObject>>();
        private readonly List<OccultSpawnTelegraph> _telegraphPool = new List<OccultSpawnTelegraph>(8);

        // Kamu Olay Delegeleri (Decoupled Observer Pattern)
        public event System.Action OnEncounterStarted;
        public event System.Action<int, int, string> OnWaveStarted; // (waveIndex, totalWaves, waveName)
        public event System.Action<int, int> OnEnemiesRemainingChanged; // (remainingEnemies, totalInWave)
        public event System.Action<int> OnWaveCompleted; // (completedWaveIndex)
        public event System.Action OnEncounterCompleted;

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
                if (_state == EncounterState.Inactive)
                {
                    return TotalEnemiesInEncounter;
                }
                if (_state == EncounterState.EncounterCleared)
                {
                    return 0;
                }
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

        public int QueuedEnemiesCount => _spawnQueue.Count;
        public float WaveElapsedTime => _waveElapsedTime;
        public float StateTimer => _stateTimer;

        public WaveDefinition GetWaveDefinition(int index)
        {
            if (waves != null && index >= 0 && index < waves.Length)
            {
                return waves[index];
            }
            return null;
        }

        public WaveDefinition NextWaveDefinition
        {
            get
            {
                if (waves == null || waves.Length == 0) return null;
                int nextIndex = _currentWaveIndex + 1;
                if (nextIndex >= 0 && nextIndex < waves.Length)
                {
                    return waves[nextIndex];
                }
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
                if (waves != null && _currentWaveIndex >= 0 && _currentWaveIndex < waves.Length)
                {
                    return waves[_currentWaveIndex];
                }
                return null;
            }
        }

        private void Awake()
        {
            if (_activeDirector == null)
            {
                SetActiveDirector(this);
            }

            SetupAudio();
            FindPlayerReference();
            FindSpawnPointsIfEmpty();
            FindEncounterAreaIfEmpty();
            ValidateAndRepairWaveEntries();
            InitializeObjectPools();
            _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
        }

        private void FindEncounterAreaIfEmpty()
        {
            if (encounterArea == null)
            {
                encounterArea = GetComponent<EncounterArea>();
                if (encounterArea == null) encounterArea = GetComponentInChildren<EncounterArea>();
                if (encounterArea == null) encounterArea = Object.FindAnyObjectByType<EncounterArea>();
            }
        }

        private void ValidateAndRepairWaveEntries()
        {
#if UNITY_EDITOR
            GameObject defaultGhoul = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab");
            GameObject defaultEye = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab");

            if (waves == null || waves.Length == 0)
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
                            wave.SpawnEntries[e] = new EnemySpawnEntry(fixedPrefab, entry.Count, entry.SpawnInterval, entry.IsAirEnemy, entry.TargetZoneTag);
                            Debug.LogWarning($"<color=yellow>[WaveSpawnDirector]</color> Dalga {w + 1} Grup {e + 1} düşman prefab referansı sahne objesi veya boştu; kalıcı prefab ({fixedPrefab.name}) ile otomatik onarıldı!");
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
                    Debug.Log("<color=yellow>[WaveSpawnDirector] Dalga listesi boştu; varsayılan 3 aşamalı karşılaşma dalgaları otomatik oluşturuldu!</color>");
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

        private void Start()
        {
            SetBarriersActive(false);

            if (autoStartOnPlay)
            {
                StartEncounter();
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
            PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null)
            {
                _playerTransform = pm.transform;
            }
        }

        private void FindSpawnPointsIfEmpty()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                if (encounterArea != null)
                {
                    var areaPoints = encounterArea.GetSpawnPoints();
                    if (areaPoints != null && areaPoints.Length > 0)
                    {
                        spawnPoints = areaPoints;
                        return;
                    }
                }
                spawnPoints = Object.FindObjectsByType<EncounterSpawnPoint>(FindObjectsInactive.Exclude);
            }
        }

        public void ConfigureSpawnPoints(EncounterSpawnPoint[] newPoints)
        {
            spawnPoints = newPoints;
        }

        public EncounterSpawnPoint[] SpawnPoints => spawnPoints;

        /// <summary>
        /// Karşılaşmadaki tüm dalgaları analiz eder ve ihtiyaç duyulacak maksimum düşman sayısı
        /// kadar GameObject'i oyun başlamadan önce ısıtarak (Pre-warm) belleğe yerleştirir.
        /// Oyun sırasında SIFIR Instantiate ve Destroy gerçekleşir.
        /// </summary>
        private void InitializeObjectPools()
        {
            GameObject poolRoot = new GameObject("Encounter_ObjectPool_Container");
            poolRoot.transform.SetParent(transform);
            _poolContainer = poolRoot.transform;

            if (waves == null) return;

            // Her bir prefab için gereken maksimum eşzamanlı havuz kapasitesini hesapla
            Dictionary<GameObject, int> requiredCapacities = new Dictionary<GameObject, int>();

            for (int w = 0; w < waves.Length; w++)
            {
                WaveDefinition wave = waves[w];
                if (wave?.SpawnEntries == null) continue;

                for (int g = 0; g < wave.SpawnEntries.Length; g++)
                {
                    GameObject prefab = wave.SpawnEntries[g].EnemyPrefab;
                    if (prefab == null) continue;

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

            // Okült Telgraf Havuzunu Hazırla (Maksimum 8 adet yeterlidir)
            for (int i = 0; i < 8; i++)
            {
                GameObject telGo = new GameObject($"OccultTelegraph_Pooled_{i}");
                telGo.transform.SetParent(_poolContainer);
                OccultSpawnTelegraph tel = telGo.AddComponent<OccultSpawnTelegraph>();
                telGo.SetActive(false);
                _telegraphPool.Add(tel);
            }
        }

        /// <summary>
        /// Karşılaşmayı dışarıdan (Trigger Volume veya Tetikleyici Olay) başlatır.
        /// </summary>
        public void StartEncounter()
        {
            if (_state != EncounterState.Inactive) return;

            SetActiveDirector(this);

            if (_playerTransform == null)
            {
                FindPlayerReference();
            }

            _state = EncounterState.Warmup;
            _stateTimer = warmupDuration;
            _currentWaveIndex = -1;
            _totalEnemiesInAllWaves = CalculateTotalEnemiesAllWaves();
            _totalEnemiesKilledInEncounter = 0;

            SetBarriersActive(true);
            PlaySound(encounterStartSound);

            OnEncounterStarted?.Invoke();
            Debug.Log("<color=red><b>[ENCOUNTER STARTED!]</b></color> Arena sealed, preparing wave.");
        }

        private void Update()
        {
            switch (_state)
            {
                case EncounterState.Warmup:
                    UpdateWarmup();
                    break;

                case EncounterState.WaveSpawningAndCombat:
                    UpdateWaveCombat();
                    break;

                case EncounterState.WaveCooldown:
                    UpdateWaveCooldown();
                    break;
            }
        }

        private void UpdateWarmup()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                StartNextWave();
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
            _spawnCooldownTimer = 0f;
            _waveElapsedTime = 0f;
            _stragglerAlertTimer = 0f;

            _totalEnemiesInCurrentWave = wave.CalculateTotalEnemies();
            _enemiesKilledInCurrentWave = 0;

            // Sıfır GC ile doğuş kuyruğunu doldur
            _spawnQueue.Clear();
            if (wave.SpawnEntries != null)
            {
                for (int i = 0; i < wave.SpawnEntries.Length; i++)
                {
                    EnemySpawnEntry entry = wave.SpawnEntries[i];
                    if (entry.EnemyPrefab == null) continue;

                    for (int c = 0; c < entry.Count; c++)
                    {
                        _spawnQueue.Add(new QueuedSpawnRequest(entry.EnemyPrefab, entry.IsAirEnemy, entry.SpawnInterval, entry.TargetZoneTag));
                    }
                }
            }

            PlaySound(waveStartSound);
            OnWaveStarted?.Invoke(_currentWaveIndex + 1, waves.Length, wave.WaveName);
            OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);

            Debug.Log($"<color=orange><b>[WAVE STARTED]</b></color> {wave.WaveName} ({_totalEnemiesInCurrentWave} Enemies | Mode: {wave.TransitionMode})");
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

            // 2. Doğuş Bütçelemesi ve Kuyruktan Çıkarma
            if (_spawnCooldownTimer > 0f)
            {
                _spawnCooldownTimer -= Time.deltaTime;
            }

            if (_spawnQueue.Count > 0 && _spawnCooldownTimer <= 0f && _activeEnemies.Count < currentWave.MaxConcurrentEnemies)
            {
                QueuedSpawnRequest request = _spawnQueue[0];
                EncounterSpawnPoint bestPoint = FindBestSpawnPoint(request.IsAirEnemy, request.TargetZoneTag);

                if (bestPoint != null)
                {
                    _spawnQueue.RemoveAt(0);
                    _spawnCooldownTimer = request.Interval;
                    bestPoint.MarkAsSpawned();

                    SpawnEnemyWithTelegraph(request.Prefab, bestPoint.Position, bestPoint.Rotation);
                }
            }

            // 3. Straggler (Kayıp Düşman) Hızlandırma: Son 1-2 düşman saklanıyorsa oyuncuya yönlendir
            if (_spawnQueue.Count == 0 && _activeEnemies.Count > 0 && _activeEnemies.Count <= 2)
            {
                _stragglerAlertTimer += Time.deltaTime;
                if (_stragglerAlertTimer >= 3.5f)
                {
                    _stragglerAlertTimer = 0f;
                    AlertAllActiveEnemies();
                }
            }

            // 4. Sektör Standardı Dalga Geçiş Kontrolü (Pacing Threshold / TimeLimit / KillAll)
            if (ShouldTransitionToNextWave(currentWave))
            {
                CompleteCurrentWave();
            }
        }

        /// <summary>
        /// DOOM Eternal ve modern retro shooter standartlarında dalga tamamlama mantığı.
        /// Straggler bekleme çilesini ortadan kaldırır.
        /// </summary>
        private bool ShouldTransitionToNextWave(WaveDefinition wave)
        {
            bool queueEmpty = _spawnQueue.Count == 0;
            bool isFinalWave = (_currentWaveIndex == waves.Length - 1);

            // KURAL: Son wave %100 bitmeden ASLA tamamlanamaz!
            // Ara dalgalarda yüzde veya süre eşikleri geçiş sağlayabilir; ancak final dalgada
            // kuyruktaki tüm düşmanlar doğmuş ve sahadaki TÜM aktif düşmanlar öldürülmüş olmalıdır.
            if (isFinalWave)
            {
                return queueEmpty && _activeEnemies.Count == 0 && _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave;
            }

            switch (wave.TransitionMode)
            {
                case WaveTransitionMode.StrictKillAll:
                    return queueEmpty && _activeEnemies.Count == 0 && _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave;

                case WaveTransitionMode.RemainingEnemyThreshold:
                    // Kuyruk bitmiş ve sahada kalan aktif düşman sayısı belirlenen eşiğin altına inmişse
                    return queueEmpty && (_activeEnemies.Count <= wave.RemainingThreshold || _enemiesKilledInCurrentWave >= _totalEnemiesInCurrentWave);

                case WaveTransitionMode.PercentageKilled:
                    if (_totalEnemiesInCurrentWave <= 0) return true;
                    float killPercent = (float)_enemiesKilledInCurrentWave / _totalEnemiesInCurrentWave;
                    return killPercent >= wave.PercentageThreshold;

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
                var enemy = _activeEnemies[i];
                if (enemy is IAlertableEnemy alertable)
                {
                    alertable.ReceiveAlert(_playerTransform);
                }
            }
        }

        private void CompleteCurrentWave()
        {
            WaveDefinition completedWave = waves[_currentWaveIndex];
            _state = EncounterState.WaveCooldown;
            _stateTimer = 2.0f; // Dalgadan sonra kısa nefes alma payı

            // Eğer kalan düşmanların devredilmesi istenmiyorsa, önceki dalgadan kalanları temizle
            if (!completedWave.CarryOverRemainingEnemies)
            {
                DespawnRemainingEnemies();
            }

            PlaySound(waveClearedSound);
            OnWaveCompleted?.Invoke(_currentWaveIndex + 1);

            Debug.Log($"<color=green><b>[WAVE TRANSITION]</b></color> Wave {_currentWaveIndex + 1} completed. (Time: {_waveElapsedTime:F1}s | Active Enemies: {_activeEnemies.Count})");
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
        }

        private void UpdateWaveCooldown()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                StartNextWave();
            }
        }

        private void CompleteEncounter()
        {
            _state = EncounterState.EncounterCleared;
            SetBarriersActive(false);

            PlaySound(victorySound);
            OnEncounterCompleted?.Invoke();

            Debug.Log("<color=gold><b>[ARENA VICTORY!]</b></color> All waves purged, barriers lifted!");
        }

        /// <summary>
        /// Doğuş noktalarını bölge etiketi (Zone Tag), oyuncuya mesafe, bekleme süresi ve fiziksel
        /// engellere göre filtreleyerek en uygun noktayı sıfır GC tahsisiyle seçer.
        /// </summary>
        private EncounterSpawnPoint FindBestSpawnPoint(bool isAirEnemy, string targetZoneTag)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            _candidatePoints.Clear();
            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : transform.position;
            bool hasZoneFilter = !string.IsNullOrEmpty(targetZoneTag);

            // 1. Aşama: Bölge etiketi eşleşen ve tüm kriterleri sağlayan adayları topla
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EncounterSpawnPoint sp = spawnPoints[i];
                if (sp == null || sp.IsOnCooldown) continue;

                if (isAirEnemy && !sp.AllowAirEnemies) continue;
                if (!isAirEnemy && !sp.AllowGroundEnemies) continue;

                if (hasZoneFilter && !sp.ZoneTag.Equals(targetZoneTag, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float dist = Vector3.Distance(playerPos, sp.Position);
                if (dist < minSpawnDistance || dist > maxSpawnDistance) continue;
                if (!sp.IsPhysicallyClear(obstacleLayerMask)) continue;

                _candidatePoints.Add(sp);
            }

            // 2. Aşama: Eğer belirtilen bölge etiketinde uygun nokta kalmadıysa genel noktalara geri dön
            if (_candidatePoints.Count == 0 && hasZoneFilter)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    EncounterSpawnPoint sp = spawnPoints[i];
                    if (sp == null || sp.IsOnCooldown) continue;
                    if (isAirEnemy && !sp.AllowAirEnemies) continue;
                    if (!isAirEnemy && !sp.AllowGroundEnemies) continue;

                    float dist = Vector3.Distance(playerPos, sp.Position);
                    if (dist < minSpawnDistance || dist > maxSpawnDistance) continue;
                    if (!sp.IsPhysicallyClear(obstacleLayerMask)) continue;

                    _candidatePoints.Add(sp);
                }
            }

            if (_candidatePoints.Count == 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    EncounterSpawnPoint sp = spawnPoints[i];
                    if (sp != null && (isAirEnemy ? sp.AllowAirEnemies : sp.AllowGroundEnemies))
                    {
                        return sp;
                    }
                }
                return spawnPoints[0];
            }

            int randomIndex = Random.Range(0, _candidatePoints.Count);
            return _candidatePoints[randomIndex];
        }

        /// <summary>
        /// Telgraf çemberini tetikler, süre bitince havuzdaki düşmanı uyandırır.
        /// </summary>
        private void SpawnEnemyWithTelegraph(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            OccultSpawnTelegraph telegraph = GetPooledTelegraph();
            if (telegraph != null)
            {
                PlaySound(summonTelegraphSound);
                telegraph.Activate(position, rotation, summonTelegraphDuration, () =>
                {
                    MaterializeEnemyFromPool(prefab, position, rotation);
                });
            }
            else
            {
                MaterializeEnemyFromPool(prefab, position, rotation);
            }
        }

        private void MaterializeEnemyFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError("<color=red>[WaveSpawnDirector]</color> MaterializeEnemyFromPool: Doğurulacak düşman prefabı null! Spawn işlemi iptal edildi.");
                return;
            }

            GameObject enemyGo = GetPooledEnemy(prefab);
            if (enemyGo == null)
            {
                enemyGo = Instantiate(prefab, position, rotation);
            }

            IEncounterEnemy encounterEnemy = enemyGo.GetComponent<IEncounterEnemy>();
            if (encounterEnemy != null)
            {
                encounterEnemy.OnEnemyDeath -= HandleEnemyDeath;
                encounterEnemy.OnEnemyDeath += HandleEnemyDeath;
                encounterEnemy.OnSpawnFromDirector(position, rotation);
                _activeEnemies.Add(encounterEnemy);
            }
            else
            {
                // IEncounterEnemy içermiyorsa varsayılan aktif etme
                enemyGo.transform.position = position;
                enemyGo.transform.rotation = rotation;
                enemyGo.SetActive(true);
            }
        }

        private void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            if (enemy == null) return;

            enemy.OnEnemyDeath -= HandleEnemyDeath;
            _activeEnemies.Remove(enemy);
            _enemiesKilledInCurrentWave++;
            _totalEnemiesKilledInEncounter++;

            OnEnemiesRemainingChanged?.Invoke(EnemiesRemainingInWave, _totalEnemiesInCurrentWave);
        }

        private GameObject GetPooledEnemy(GameObject prefab)
        {
            if (!_enemyPools.TryGetValue(prefab, out List<GameObject> poolList)) return null;

            for (int i = 0; i < poolList.Count; i++)
            {
                GameObject obj = poolList[i];
                if (obj != null && !obj.activeInHierarchy)
                {
                    return obj;
                }
            }

            // Havuz yetersiz kaldıysa dinamik genişlet
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

            // Genişlet
            GameObject go = new GameObject($"OccultTelegraph_Pooled_Extra");
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
        /// Karşılaşmayı sıfırlar ve tüm düşmanları anında havuza çeker.
        /// </summary>
        public void ResetEncounter()
        {
            _state = EncounterState.Inactive;
            _currentWaveIndex = -1;
            _totalEnemiesKilledInEncounter = 0;
            _spawnQueue.Clear();

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

            SetBarriersActive(false);
        }

        /// <summary>
        /// Kod veya editör üzerinden dalga konfigürasyonunu toplu atar.
        /// </summary>
        public void ConfigureWaves(WaveDefinition[] newWaves)
        {
            waves = newWaves;
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
            if (!Application.isPlaying) return;
            _spawnQueue.Clear();
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

        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EncounterSpawnPoint sp = spawnPoints[i];
                if (sp == null) continue;

                Gizmos.color = sp.IsOnCooldown ? Color.gray : new Color(0.2f, 0.85f, 1f, 0.5f);
                Gizmos.DrawLine(transform.position, sp.Position);
            }
        }
#endif

        private void OnDestroy()
        {
            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                if (_activeEnemies[i] != null)
                {
                    _activeEnemies[i].OnEnemyDeath -= HandleEnemyDeath;
                }
            }
        }
    }
}
