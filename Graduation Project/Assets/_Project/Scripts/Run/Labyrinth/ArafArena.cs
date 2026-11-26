using System;
using System.Collections;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Oyuncu içeri girdiğinde dalgalı arena karşılaşmasını başlatır ve tek kez tamamlar.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafArena : MonoBehaviour
    {
        [SerializeField] private Collider startTrigger;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private GameObject[] spawnTelegraphs;
        [SerializeField] private Transform altarAnchor;

        private enum State { Dormant, Running, Cleared }
        private State _state;
        private ArafRoomRole _role;
        private ArafArenaWaveTable _table;
        private ArafArenaWaveSettings _settings;
        private Func<int> _awakeningProvider;
        private RunRandom _rng;
        private Transform _player;
        private float _eliteDamage;
        private float _eliteSpeed;
        private float _eliteHealth;
        private float _eyeHeight;
        private ArafArenaWave[] _waves;
        private Vector3[] _positions;
        private int[] _pointIndices;
        private Transform[] _enemyTransforms;
        private IEncounterEnemy[] _enemies;
        private IAlertableEnemy[] _alertables;
        private IHuntableEnemy[] _huntables;
        private IEliteScalable[] _elites;
        private EnemyAggressionProfile _profile;
        private int[] _enemyWaves;
        private bool[] _alive;
        private int[] _aliveByWave;
        private int _aliveCount;
        private int _waveIndex = -1;
        private int _waveCount;
        private int _plannedCount;
        private int _candidateCount;
        private bool _telegraphPending;
        private bool _waveSpawned;
        private bool _configured;
        private Action<IEncounterEnemy> _deathHandler;
        private WaitForSeconds _telegraphWait;
        private WaitForSeconds _realertWait;
        private Coroutine _coroutine;

        /// <summary>Arena başladığında kapıları kapatmak için bir kez yükseltilir.</summary>
        public event Action<ArafArena> OnStarted;
        /// <summary>Bütün dalgalar öldüğünde bir kez yükseltilir.</summary>
        public event Action<ArafArena> OnCleared;
        /// <summary>Üretimde atanmış oda rolü.</summary>
        public ArafRoomRole Role => _role;
        /// <summary>Karşılaşma devam ediyor.</summary>
        public bool IsRunning => _state == State.Running;
        /// <summary>Karşılaşma tamamlandı.</summary>
        public bool IsCleared => _state == State.Cleared;
        /// <summary>Bütün dalgalardaki canlı düşman sayısı.</summary>
        public int AliveCount => _aliveCount;
        /// <summary>Mevcut sıfır tabanlı dalga indeksi; başlamadan önce -1.</summary>
        public int WaveIndex => _waveIndex;
        /// <summary>Başlangıç uyanışına göre sabitlenen dalga sayısı.</summary>
        public int WaveCount => _waveCount;
        /// <summary>Ödül sunağının doğuş noktası.</summary>
        public Transform AltarAnchor => altarAnchor;

        private void Awake()
        {
            if (_deathHandler == null) _deathHandler = HandleDeath;
            if (_settings == null) _settings = new ArafArenaWaveSettings();
            if (_waves == null) AllocateBuffers();
            if (startTrigger != null)
            {
                startTrigger.isTrigger = true;
                if (!startTrigger.TryGetComponent(out ArafArenaTriggerRelay relay))
                    relay = startTrigger.gameObject.AddComponent<ArafArenaTriggerRelay>();
                relay.Bind(this);
            }
        }

        // Varsayılan tamponlar Awake'te ayrılır. Farklı tablo kapasitesi yalnızca Configure olayında yeniden ayrılır.
        private void AllocateBuffers()
        {
            int capacity = _settings.MaxAlivePerArena;
            int waves = _settings.MaxWaveCount;
            int points = spawnPoints != null ? spawnPoints.Length : 0;
            _positions = new Vector3[points];
            _pointIndices = new int[capacity];
            _waves = new ArafArenaWave[waves];
            _aliveByWave = new int[waves];
            _enemyTransforms = new Transform[capacity];
            _enemies = new IEncounterEnemy[capacity];
            _alertables = new IAlertableEnemy[capacity];
            _huntables = new IHuntableEnemy[capacity];
            _elites = new IEliteScalable[capacity];
            _enemyWaves = new int[capacity];
            _alive = new bool[capacity];
        }

        /// <summary>Üretim olayında arenayı yapılandırır; uyanış yalnızca oyuncu girdiğinde okunur.</summary>
        public void Configure(ArafRoomRole role, ArafArenaWaveTable table, Func<int> awakeningProvider,
            float eliteDamage, float eliteSpeed, float eliteHealth, RunRandom rng, float eyeHeight)
        {
            if (IsRunning || IsCleared) return;
            _role = role;
            _table = table;
            _awakeningProvider = awakeningProvider;
            _rng = rng;
            _eliteDamage = eliteDamage;
            _eliteSpeed = eliteSpeed;
            _eliteHealth = eliteHealth;
            _eyeHeight = eyeHeight;
            _settings = table != null ? table.Settings : null;
            _configured = _settings != null && rng != null && role != ArafRoomRole.Calm;
            if (!_configured) return;
            // Content kapalıyken Configure, Awake'ten önce çağrılabilir.
            if (_deathHandler == null) _deathHandler = HandleDeath;
            if (_waves == null || _waves.Length != _settings.MaxWaveCount || _enemies.Length != _settings.MaxAlivePerArena)
                AllocateBuffers();
            _telegraphWait = new WaitForSeconds(_settings.TelegraphSeconds);
            _realertWait = new WaitForSeconds(_settings.RealertInterval);
        }

        internal void HandleActivationTriggerEnter(Collider other)
        {
            if (!_configured || _state != State.Dormant || other == null) return;
            PlayerHealth player;
            if (!other.TryGetComponent(out player) && other.attachedRigidbody != null)
                other.attachedRigidbody.TryGetComponent(out player);
            if (player == null) player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead) return;
            _player = player.transform;
            int awakening = _awakeningProvider != null ? _awakeningProvider() : 0;
            _waveCount = ArafArenaPlanner.BuildWaves(_role, awakening, _settings, _waves);
            _profile = _table != null ? _table.GetAggressionProfile(awakening) : null;
            _state = State.Running;
            OnStarted?.Invoke(this);
            if (!isActiveAndEnabled) return;
            _coroutine = StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            while (IsRunning)
            {
                Sweep();
                if (_waveCount == 0 || (_waveSpawned && ArafArenaPlanner.IsCleared(_aliveCount, _waveIndex, _waveCount)))
                {
                    Complete();
                    yield break;
                }
                if (_waveIndex < 0 || (_waveSpawned && ArafArenaPlanner.ShouldAdvance(
                    _aliveByWave[_waveIndex], _waveIndex, _waveCount, _settings.AdvanceWhenAliveAtMost)))
                    PrepareWave();
                if (_telegraphPending)
                {
                    yield return _telegraphWait;
                    // Oyuncu uyarı sırasında hızla yaklaşmışsa yeni güvenli noktaları tekrar haber ver.
                    if (!SpawnPositionsStillSafe())
                    {
                        SetTelegraphs(false);
                        PlanSpawnPoints();
                        continue;
                    }
                    SetTelegraphs(false);
                    _telegraphPending = false;
                    SpawnWave();
                    _waveSpawned = true;
                }
                yield return _realertWait;
            }
        }

        private void PrepareWave()
        {
            _waveIndex++;
            _waveSpawned = false;
            PlanSpawnPoints();
        }

        private void PlanSpawnPoints()
        {
            ArafArenaWave wave = _waves[_waveIndex];
            int points = 0;
            if (spawnPoints != null)
                for (int i = 0; i < spawnPoints.Length; i++)
                    if (spawnPoints[i] != null) _positions[points++] = spawnPoints[i].position;
            int needed = Mathf.Min(_settings.MaxAlivePerArena - _aliveCount, wave.Ghouls + wave.Eyes + wave.Penitents);
            _candidateCount = points;
            _plannedCount = ArafArenaPlanner.PickSpawnPoints(_positions, points,
                _player != null ? _player.position : transform.position, _settings.MinSpawnDistance, needed, _rng, _pointIndices);
            _telegraphPending = true;
            SetTelegraphs(true);
        }

        private bool SpawnPositionsStillSafe()
        {
            if (_plannedCount == 0 || _player == null) return true;
            Vector3 player = _player.position;
            float limit = _settings.MinSpawnDistance * _settings.MinSpawnDistance;
            float farthest = 0f;
            for (int i = 0; i < _candidateCount; i++)
            {
                float x = _positions[i].x - player.x;
                float z = _positions[i].z - player.z;
                farthest = Mathf.Max(farthest, x * x + z * z);
            }
            limit = Mathf.Min(limit, farthest);
            for (int i = 0; i < _plannedCount; i++)
            {
                Vector3 point = _positions[_pointIndices[i]];
                float x = point.x - player.x;
                float z = point.z - player.z;
                if (x * x + z * z < limit) return false;
            }
            return true;
        }

        private int OriginalPointIndex(int compactIndex)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
                if (spawnPoints[i] != null && compactIndex-- == 0) return i;
            return -1;
        }

        private void SetTelegraphs(bool active)
        {
            if (spawnTelegraphs == null) return;
            for (int i = 0; i < _plannedCount; i++)
            {
                int point = OriginalPointIndex(_pointIndices[i]);
                if (point >= 0 && point < spawnTelegraphs.Length && spawnTelegraphs[point] != null)
                    spawnTelegraphs[point].SetActive(active);
            }
        }

        // Doğuş olayı: Instantiate ve bileşen önbellekleme yalnızca burada yapılır, alarm taramasında yapılmaz.
        private void SpawnWave()
        {
            ArafArenaWave wave = _waves[_waveIndex];
            // Kefaretçiler önce doğar: kapasite sınırında bile son dalganın eliti korunur.
            bool elitePending = wave.HasElite;
            int spawn = 0;
            for (int type = 0; type < 3; type++)
            {
                int count = type == 0 ? wave.Penitents : type == 1 ? wave.Ghouls : wave.Eyes;
                GameObject prefab = type == 0 ? _table.PenitentPrefab : type == 1 ? _table.GhoulPrefab : _table.EyePrefab;
                if (prefab == null) continue;
                for (int n = 0; n < count && spawn < _plannedCount; n++)
                {
                    int slot = FreeSlot();
                    if (slot < 0) return;
                    int point = _pointIndices[spawn++];
                    Vector3 position = _positions[point];
                    if (type == 2) position.y += _eyeHeight;
                    GameObject enemy = Instantiate(prefab, position, transform.rotation, transform);
                    enemy.TryGetComponent(out IEncounterEnemy encounter);
                    enemy.TryGetComponent(out IAlertableEnemy alertable);
                    enemy.TryGetComponent(out IHuntableEnemy huntable);
                    enemy.TryGetComponent(out IEliteScalable elite);
                    if (encounter == null)
                    {
                        Destroy(enemy);
                        continue;
                    }
                    _enemies[slot] = encounter;
                    _alertables[slot] = alertable;
                    _huntables[slot] = huntable;
                    _elites[slot] = elite;
                    _enemyTransforms[slot] = enemy.transform;
                    _enemyWaves[slot] = _waveIndex;
                    _alive[slot] = true;
                    _aliveCount++;
                    _aliveByWave[_waveIndex]++;
                    encounter.OnEnemyDeath += _deathHandler;
                    // Sözleşme sırası: OnSpawnFromDirector → elit → BeginHunt.
                    ArafArenaEnemyActivator.Activate(enemy, position, transform.rotation);
                    if (elitePending)
                    {
                        _elites[slot]?.ApplyEliteScaling(_eliteDamage, _eliteSpeed, _eliteHealth);
                        elitePending = false;
                    }
                    ArafArenaEnemyActivator.Hunt(huntable, alertable, _player, _profile);
                }
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < _alive.Length; i++) if (!_alive[i]) return i;
            return -1;
        }

        private void Sweep()
        {
            for (int i = 0; i < _alive.Length; i++)
            {
                if (!_alive[i]) continue;
                if (_enemyTransforms[i] == null || !_enemies[i].IsAlive) Retire(i);
                else ArafArenaEnemyActivator.Hunt(_huntables[i], _alertables[i], _player, _profile);
            }
        }

        private void HandleDeath(IEncounterEnemy enemy)
        {
            for (int i = 0; i < _alive.Length; i++)
                if (_alive[i] && ReferenceEquals(_enemies[i], enemy)) { Retire(i); return; }
        }

        private void Retire(int index)
        {
            _alive[index] = false;
            _aliveCount--;
            _aliveByWave[_enemyWaves[index]]--;
            _enemies[index].OnEnemyDeath -= _deathHandler;
            _enemies[index] = null;
            _alertables[index] = null;
            _huntables[index] = null;
            _elites[index] = null;
            _enemyTransforms[index] = null;
        }

        private void Complete()
        {
            if (!IsRunning) return;
            _state = State.Cleared;
            Unsubscribe();
            _coroutine = null;
            OnCleared?.Invoke(this);
        }

        private void Unsubscribe()
        {
            if (_enemies == null) return;
            for (int i = 0; i < _enemies.Length; i++)
                if (_alive[i]) _enemies[i].OnEnemyDeath -= _deathHandler;
        }

        // Kapanınca dalga/uyarı aşaması korunur; açılınca uyarı süresi yeniden beklenir, düşmanlar tekrar doğmaz.
        private void OnDisable()
        {
            if (_coroutine != null) StopCoroutine(_coroutine);
            _coroutine = null;
            Unsubscribe();
        }

        private void OnEnable()
        {
            if (!IsRunning || _coroutine != null) return;
            for (int i = 0; i < _enemies.Length; i++)
                if (_alive[i]) _enemies[i].OnEnemyDeath += _deathHandler;
            _coroutine = StartCoroutine(RunWaves());
        }
    }
}
