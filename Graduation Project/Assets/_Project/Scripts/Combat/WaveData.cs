using UnityEngine;
using UnityEngine.Events;

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
    /// Bir düşman grubunun doğuş noktası seçme deseni.
    /// </summary>
    public enum SpawnPattern
    {
        [Tooltip("Rastgele; direktörde flank açıksa oyuncunun yan/arkasındaki noktalar ağırlıklı seçilir")]
        Random = 0,

        [Tooltip("Noktaları sırayla dolaşır (çevreleme / kuşatma hissi)")]
        SequentialRoundRobin = 1,

        [Tooltip("Oyuncuya en uzak uygun nokta (uzaktan yaklaşan baskı / keskin nişancı)")]
        FarthestFromPlayer = 2
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

        [Tooltip("Doğuş partileri arası bekleme (saniye)")]
        [SerializeField] private float spawnInterval;

        [Tooltip("Uçan bir düşman mı? (Doğuş noktası hava filtresi için)")]
        [SerializeField] private bool isAirEnemy;

        [Tooltip("Doğacağı özel bölge etiketi (Örn: 'Balkon', 'Meydan'). Boş bırakılırsa tüm uygun noktalar kullanılır.")]
        [SerializeField] private string targetZoneTag;

        [Header("Zamanlama ve Desen")]
        [Tooltip("Bu grubun dalga başlangıcından en erken kaç saniye sonra doğmaya başlayacağı")]
        [SerializeField] private float delayFromWaveStart;

        [Tooltip("Her doğuş anında aynı anda kaç düşman çıksın (0 veya 1 = tek tek)")]
        [SerializeField] private int batchSize;

        [Tooltip("Doğuş noktası seçme deseni")]
        [SerializeField] private SpawnPattern spawnPattern;

        [Tooltip("Kapalı: önceki grubun tamamı doğduktan sonra başlar (sıralı). Açık: önceki grubu beklemeden, kendi gecikmesiyle paralel başlar.")]
        [SerializeField] private bool runInParallel;

        [Tooltip("Tehdit puanı (0 = prefab'daki IThreatCost değeri, o da yoksa 1). Dalgada Threat Budget kullanılıyorsa geçerlidir.")]
        [SerializeField] private float threatCostOverride;

        public GameObject EnemyPrefab => enemyPrefab;
        public int Count => Mathf.Max(1, count);
        public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
        public bool IsAirEnemy => isAirEnemy;
        public string TargetZoneTag => targetZoneTag;
        public float DelayFromWaveStart => Mathf.Max(0f, delayFromWaveStart);
        public int BatchSize => Mathf.Max(1, batchSize);
        public SpawnPattern Pattern => spawnPattern;
        public bool RunInParallel => runInParallel;
        public float ThreatCostOverride => threatCostOverride;

        public EnemySpawnEntry(GameObject prefab, int count, float interval, bool isAir = false, string zone = "")
        {
            this.enemyPrefab = prefab;
            this.count = count;
            this.spawnInterval = interval;
            this.isAirEnemy = isAir;
            this.targetZoneTag = zone ?? string.Empty;
            this.delayFromWaveStart = 0f;
            this.batchSize = 1;
            this.spawnPattern = SpawnPattern.Random;
            this.runInParallel = false;
            this.threatCostOverride = 0f;
        }

        /// <summary>
        /// Aynı grubu farklı bir prefab ile kopyalar (diğer tüm ayarlar korunur). Editör onarımı için.
        /// </summary>
        public EnemySpawnEntry WithPrefab(GameObject prefab)
        {
            EnemySpawnEntry copy = this;
            copy.enemyPrefab = prefab;
            return copy;
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

        [Tooltip("Aynı anda sahada canlı kalabilecek maksimum düşman sayısı (Threat Budget > 0 ise yok sayılır)")]
        [SerializeField] private int maxConcurrentEnemies = 6;

        [Tooltip("Eşzamanlı tehdit bütçesi. 0 = kapalı (adet sınırı kullanılır). Örn: 10 → sahada aynı anda 10 Ghoul veya 5 Flying Eye.")]
        [SerializeField] private float threatBudget = 0f;

        [Header("Dalga Geçiş & Tempo Kriteri (Sektör Standardı)")]
        [Tooltip("Bu dalgadan sonraki dalgaya nasıl geçileceğini belirler. Son dalga her zaman 'hepsini öldür' ile biter.")]
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

        [Header("Dalga Sonu Ödülü (Pacing)")]
        [Tooltip("Dalga bittiğinde oyuncunun çevresine garanti düşen can küresi sayısı")]
        [SerializeField] private int rewardHealthOrbs = 1;

        [Tooltip("Dalga bittiğinde oyuncunun çevresine garanti düşen cephane paketi sayısı (en az olan cephane türü seçilir)")]
        [SerializeField] private int rewardAmmoPickups = 1;

        [Header("Düşman Grupları")]
        [Tooltip("Bu dalgada sahaya sürülecek düşman grupları")]
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
        public float ThreatBudget => Mathf.Max(0f, threatBudget);
        public WaveTransitionMode TransitionMode => transitionMode;
        public int RemainingThreshold => Mathf.Max(0, remainingThreshold);
        public float PercentageThreshold => Mathf.Clamp01(percentageThreshold);
        public float TimeLimit => timeLimit;
        public bool CarryOverRemainingEnemies => carryOverRemainingEnemies;
        public int RewardHealthOrbs => Mathf.Max(0, rewardHealthOrbs);
        public int RewardAmmoPickups => Mathf.Max(0, rewardAmmoPickups);
        public EnemySpawnEntry[] SpawnEntries => spawnEntries;

        [System.NonSerialized] private int _cachedTotalEnemies = -1;
        [System.NonSerialized] private int _cachedAirEnemies = -1;
        [System.NonSerialized] private int _cachedGroundEnemies = -1;
        [System.NonSerialized] private float _cachedTotalThreat = -1f;
        [System.NonSerialized] private string _cachedCompositionSummary = null;
        [System.NonSerialized] private string _cachedDensityRating = null;

        public void InvalidateCache()
        {
            _cachedTotalEnemies = -1;
            _cachedAirEnemies = -1;
            _cachedGroundEnemies = -1;
            _cachedTotalThreat = -1f;
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

        /// <summary>Dalgadaki tüm düşmanların tehdit puanı toplamı.</summary>
        public float TotalThreat
        {
            get
            {
                if (_cachedTotalThreat < 0f) CalculateMetrics();
                return _cachedTotalThreat;
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

        /// <summary>
        /// Bir grubun tehdit puanı: grup override'ı → prefab'daki IThreatCost → 1.
        /// Prefab üzerinde GetComponent yapar; yalnızca dalga başında / editörde çağrılır.
        /// </summary>
        public static float ResolveThreatCost(EnemySpawnEntry entry)
        {
            if (entry.ThreatCostOverride > 0f) return entry.ThreatCostOverride;
            if (entry.EnemyPrefab != null)
            {
                IThreatCost threat = entry.EnemyPrefab.GetComponent<IThreatCost>();
                if (threat != null && threat.ThreatCost > 0f) return threat.ThreatCost;
            }
            return 1f;
        }

        public void CalculateMetrics()
        {
            int total = 0;
            int air = 0;
            int ground = 0;
            float threat = 0f;

            if (spawnEntries != null && spawnEntries.Length > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
                for (int i = 0; i < spawnEntries.Length; i++)
                {
                    int c = spawnEntries[i].Count;
                    total += c;
                    threat += c * ResolveThreatCost(spawnEntries[i]);
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
            _cachedTotalThreat = threat;

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
    /// Belirli bir dalganın başlangıç/bitişine bağlanan sahne olayları (kapı, ışık, müzik, tuzak).
    /// Direktör üzerinde durur; böylece paylaşılan WaveSet asset'i sahne referansı taşımak zorunda kalmaz.
    /// </summary>
    [System.Serializable]
    public class WaveEventBinding
    {
        [Tooltip("Hangi dalga (1'den başlar). 0 = her dalga.")]
        [SerializeField] private int waveNumber = 1;

        [SerializeField] private UnityEvent onWaveStart = new UnityEvent();
        [SerializeField] private UnityEvent onWaveEnd = new UnityEvent();

        public int WaveNumber => waveNumber;
        public UnityEvent OnWaveStart => onWaveStart;
        public UnityEvent OnWaveEnd => onWaveEnd;

        public bool Matches(int oneBasedWave) => waveNumber == 0 || waveNumber == oneBasedWave;
    }
}
