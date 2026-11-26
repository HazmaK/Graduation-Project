using RetroFPS.Combat;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>
    /// Spawns corridor patrols, return-path awakening waves and island threats
    /// in response to NavMesh readiness and seal collection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArafThreatDirector : MonoBehaviour
    {
        private const int DefaultCellBufferCapacity = 81;
        private const int DefaultMaxAlivePatrols = 12;
        private const float DefaultMinSpawnDistance = 14f;
        private const float SpawnHeightOffset = 0.1f;
        private const float PlayerLocalHeightTolerance = 25f;

        private static readonly int[] DefaultPatrolsAddedAtLevel = { 2, 2, 3, 3 };
        private static readonly int[] DefaultWaveSizeByLevel = { 0, 3, 4, 5 };

        [SerializeField] private ArafLabyrinthDirector labyrinth;
        [SerializeField] private GameObject patrolEnemyPrefab;
        [SerializeField] private GameObject waveEnemyPrefab;
        [SerializeField] private GameObject islandEyePrefab;
        [SerializeField] private GameObject islandPenitentPrefab;

        private ArafLevel _level;
        private ArafTuning _tuning;
        private GridPos[] _cellBuffer = new GridPos[DefaultCellBufferCapacity];
        private GridPos[] _pathBuffer = new GridPos[DefaultCellBufferCapacity];
        private const int MaxTrackedWaveEnemies = 24;
        private const float WaveRealertInterval = 0.5f;
        private const float WaveSharedCellOffset = 1.5f;

        [Tooltip("Dalga düşmanları oyuncu bu mesafeye girince alarm alır. Ghoul'un loseSightDistance (24 m) değerinden küçük olmalı.")]
        [SerializeField, Min(0f)] private float waveAlertDistance = 22f;

        private GameObject[] _patrolSlots;
        private IEncounterEnemy[] _patrolEnemies;
        private GameObject[] _islandSlots;
        private IEncounterEnemy[] _islandEnemies;
        private int[] _islandChainBuffer;
        private int[] _islandPlatformBuffer;
        // Ghoul'lar uzaktaki hedefi 3.5 s görmeyince kovalamayı bırakır; dalga düşmanları oyuncuyu
        // bulana kadar belirli aralıklarla yeniden uyarılır. Diziler bir kez ayrılır.
        private readonly IAlertableEnemy[] _waveAlertables = new IAlertableEnemy[MaxTrackedWaveEnemies];
        private readonly IEncounterEnemy[] _waveEnemies = new IEncounterEnemy[MaxTrackedWaveEnemies];
        private readonly Transform[] _waveTransforms = new Transform[MaxTrackedWaveEnemies];
        private readonly WaitForSeconds _realertWait = new WaitForSeconds(WaveRealertInterval);
        private Coroutine _realertRoutine;
        private Transform _playerTransform;
        private bool _playerSearched;
        private bool _subscribed;
        private bool _initialPatrolsSpawned;
        private int _nextPatrolSection;

        private void Awake()
        {
            if (labyrinth == null) labyrinth = GetComponent<ArafLabyrinthDirector>();
            _level = labyrinth != null ? labyrinth.GetComponent<ArafLevel>() : GetComponent<ArafLevel>();
            if (_level == null) _level = FindAnyObjectByType<ArafLevel>();
            _tuning = _level != null ? _level.Tuning : null;

            AllocatePatrolSlots(GetMaxAlivePatrols());
            AllocateIslandSlots(GetMaxAliveIslandEnemies());
        }

        private void OnEnable()
        {
            Subscribe();
            if (_realertRoutine == null && HasTrackedWaveEnemies())
                _realertRoutine = StartCoroutine(RealertWaveEnemies());
            if (labyrinth != null && labyrinth.IsNavMeshReady && !_initialPatrolsSpawned)
            {
                HandleNavMeshReady();
            }
        }

        private void Start()
        {
            if (_tuning == null && _level != null)
            {
                _tuning = _level.Tuning;
                int desiredCap = GetMaxAlivePatrols();
                if (_patrolSlots == null || _patrolSlots.Length != desiredCap)
                {
                    AllocatePatrolSlots(desiredCap);
                }
                int islandCap = GetMaxAliveIslandEnemies();
                if (_islandSlots == null || _islandSlots.Length != islandCap)
                    AllocateIslandSlots(islandCap);
            }

            Subscribe();
            if (labyrinth != null && labyrinth.IsNavMeshReady && !_initialPatrolsSpawned)
            {
                HandleNavMeshReady();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            // Bileşen kapatılınca coroutine kendiliğinden durmaz; açıkça durdur.
            if (_realertRoutine != null) StopCoroutine(_realertRoutine);
            _realertRoutine = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || labyrinth == null) return;
            labyrinth.NavMeshReady += HandleNavMeshReady;
            labyrinth.SealTaken += HandleSealTaken;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (labyrinth != null)
            {
                labyrinth.NavMeshReady -= HandleNavMeshReady;
                labyrinth.SealTaken -= HandleSealTaken;
            }
            _subscribed = false;
        }

        private void HandleNavMeshReady()
        {
            if (_initialPatrolsSpawned) return;
            _initialPatrolsSpawned = true;
            SpawnPatrols(0);
        }

        private void HandleSealTaken(int awakeningLevel, int sealSlotIndex)
        {
            SpawnPatrols(awakeningLevel);
            SpawnWave(awakeningLevel, sealSlotIndex);
            SpawnIslandThreats(awakeningLevel);
        }

        private void SpawnIslandThreats(int awakeningLevel)
        {
            if (awakeningLevel <= 0 || labyrinth == null || labyrinth.World == null
                || labyrinth.GeneratedRoot == null || _islandSlots == null) return;
            int eyes = islandEyePrefab == null ? 0 : Mathf.Max(0, _tuning != null
                ? _tuning.GetIslandEyesAddedAtLevel(awakeningLevel)
                : awakeningLevel <= 3 ? (awakeningLevel == 3 ? 3 : 2) : 0);
            int penitents = islandPenitentPrefab == null ? 0 : Mathf.Max(0, _tuning != null
                ? _tuning.GetIslandPenitentsAddedAtLevel(awakeningLevel)
                : awakeningLevel >= 2 && awakeningLevel <= 3 ? 1 : 0);
            if (eyes == 0 && penitents == 0) return;
            int alive = 0;
            for (int i = 0; i < _islandSlots.Length; i++)
                if (IsIslandSlotAlive(i)) alive++;
            int count = (int)System.Math.Min((long)eyes + penitents, _islandSlots.Length - alive);
            RunRandom rng = RunEvents.GetStream(RunStream.ArafThreat);
            Vector3 avoidLocal = GetAvoidLocal(labyrinth.World.Hub.Layout, labyrinth.transform);
            int picked = ArafIslandThreatPlanner.PickPlatforms(labyrinth.World, count, avoidLocal,
                GetMinSpawnDistance(), rng, _islandChainBuffer, _islandPlatformBuffer);
            Transform player = GetPlayerTransform();
            int spawned = 0;
            for (int i = 0; i < picked; i++)
            {
                int slot = -1;
                for (int s = 0; s < _islandSlots.Length; s++)
                    if (!IsIslandSlotAlive(s)) { slot = s; break; }
                if (slot < 0) break;
                bool eye = i < eyes;
                Vector3 local = labyrinth.World.GetChain(_islandChainBuffer[i])
                    .GetPlatform(_islandPlatformBuffer[i]).Center;
                local += Vector3.up * (eye ? (_tuning != null ? _tuning.EyeHoverHeight : 3f) : SpawnHeightOffset);
                Vector3 position = labyrinth.transform.TransformPoint(local);
                GameObject instance = SpawnEnemy(eye ? islandEyePrefab : islandPenitentPrefab,
                    position, GetWaveSpawnRotation(position, player, rng), out IEncounterEnemy enemy);
                if (instance == null) continue;
                _islandSlots[slot] = instance;
                _islandEnemies[slot] = enemy;
                spawned++;
            }
            Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} island enemies spawned: {spawned}/{count} (alive {alive + spawned}/{_islandSlots.Length}).", this);
        }

        private int GetMaxAliveIslandEnemies()
            => _tuning != null ? Mathf.Max(0, _tuning.MaxAliveIslandEnemies) : 10;

        private void AllocateIslandSlots(int capacity)
        {
            _islandSlots = new GameObject[capacity];
            _islandEnemies = new IEncounterEnemy[capacity];
            _islandChainBuffer = new int[capacity];
            _islandPlatformBuffer = new int[capacity];
        }

        private bool IsIslandSlotAlive(int index)
        {
            GameObject slot = _islandSlots[index];
            if (slot == null || !slot.activeInHierarchy) return false;
            IEncounterEnemy enemy = _islandEnemies[index];
            return enemy == null || enemy.IsAlive;
        }

        private void SpawnPatrols(int awakeningLevel)
        {
            if (labyrinth == null || labyrinth.SectionCount <= 0 || labyrinth.GeneratedRoot == null || patrolEnemyPrefab == null)
                return;

            int requested = GetPatrolsAddedAtLevel(awakeningLevel);
            if (requested <= 0 || _patrolSlots == null || _patrolSlots.Length == 0) return;

            int alive = CountAlivePatrols();
            int availableSlots = _patrolSlots.Length - alive;
            int count = Mathf.Min(requested, availableSlots);
            if (count <= 0)
            {
                Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} patrol batch skipped: cap reached ({alive}/{_patrolSlots.Length}).", this);
                return;
            }

            EnsureCellBuffers();
            float minDistance = GetMinSpawnDistance();
            RunRandom rng = RunEvents.GetStream(RunStream.ArafThreat);

            int spawned = 0;
            int sections = labyrinth.SectionCount;
            for (int offset = 0; offset < sections; offset++)
            {
                int section = (_nextPatrolSection + offset) % sections;
                int sectionCount = count / sections + (offset < count % sections ? 1 : 0);
                if (sectionCount <= 0) continue;
                LabyrinthLayout layout = labyrinth.GetSectionLayout(section);
                Transform root = labyrinth.GetSectionRoot(section);
                if (layout == null || root == null) continue;
                Vector3 avoidLocal = GetAvoidLocal(layout, root);
                int picked = ArafThreatPlanner.PickPatrolCells(layout, avoidLocal, minDistance, rng, _cellBuffer, sectionCount);
                for (int i = 0; i < picked; i++)
                {
                    int slotIndex = FindFreePatrolSlot();
                    if (slotIndex < 0) break;

                    GridPos cell = _cellBuffer[i];
                    Vector3 worldPos = CellToWorldSpawn(layout, root, cell);
                    Quaternion rot = root.rotation * Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                    GameObject instance = SpawnEnemy(patrolEnemyPrefab, worldPos, rot, out IEncounterEnemy enemy);
                    if (instance == null) continue;

                    _patrolSlots[slotIndex] = instance;
                    _patrolEnemies[slotIndex] = enemy;
                    spawned++;
                }
            }
            _nextPatrolSection = (_nextPatrolSection + count) % sections;

            Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} patrols spawned: {spawned}/{count} (alive {alive + spawned}/{_patrolSlots.Length}).", this);
        }

        private void SpawnWave(int awakeningLevel, int sealSlotIndex)
        {
            if (labyrinth == null || labyrinth.SectionCount <= 0 || labyrinth.GeneratedRoot == null || waveEnemyPrefab == null)
                return;

            if (sealSlotIndex < 0) return;
            int section = labyrinth.GetSlotSection(sealSlotIndex);
            if (section < 0 || section >= labyrinth.SectionCount) return;
            LabyrinthLayout layout = labyrinth.GetSectionLayout(section);
            Transform root = labyrinth.GetSectionRoot(section);
            int localSlot = labyrinth.GetSlotLocalIndex(sealSlotIndex);
            if (layout == null || root == null || localSlot < 0 || localSlot >= layout.RoomCount) return;

            int waveSize = GetWaveSize(awakeningLevel);
            if (waveSize <= 0) return;

            EnsureCellBuffers();
            int pathLength = ArafThreatPlanner.BuildReturnPath(layout, localSlot, _pathBuffer);
            if (pathLength <= 0)
            {
                Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} wave skipped: empty return path from slot {sealSlotIndex}.", this);
                return;
            }

            Vector3 avoidLocal = GetAvoidLocal(layout, root);
            float minDistance = GetMinSpawnDistance();
            RunRandom rng = RunEvents.GetStream(RunStream.ArafThreat);

            int picked = ArafThreatPlanner.PickWaveCells(layout, _pathBuffer, pathLength, avoidLocal, minDistance, rng, _cellBuffer, waveSize);
            if (picked <= 0)
            {
                Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} wave skipped: no eligible cells on return path from slot {sealSlotIndex}.", this);
                return;
            }
            Transform player = GetPlayerTransform();
            float sharedOffset = Mathf.Min(WaveSharedCellOffset, layout.CellSize * 0.3f);
            int spawned = 0;
            // Kısa yollarda hücre yetmezse kalan düşmanlar aynı hücrelere küçük kaydırmalarla yerleşir.
            for (int i = 0; i < waveSize; i++)
            {
                GridPos cell = _cellBuffer[i % picked];
                Vector3 worldPos = CellToWorldSpawn(layout, root, cell);
                int ring = i / picked;
                if (ring > 0)
                {
                    float angle = (i * 137.5f) * Mathf.Deg2Rad;
                    worldPos += root.rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sharedOffset;
                }
                Quaternion rot = GetWaveSpawnRotation(worldPos, player, rng);
                GameObject instance = SpawnEnemy(waveEnemyPrefab, worldPos, rot, out IEncounterEnemy waveEnemy);
                if (instance == null) continue;

                // Dalga dönüş yolunda pusuda bekler; oyuncu menzile girince alarm alır.
                if (instance.TryGetComponent(out IAlertableEnemy alertable))
                {
                    TrackWaveEnemy(alertable, waveEnemy, instance.transform);
                    if (player != null && IsWithinAlertDistance(instance.transform, player))
                        alertable.ReceiveAlert(player);
                }
                spawned++;
            }

            if (player != null && _realertRoutine == null && isActiveAndEnabled)
            {
                _realertRoutine = StartCoroutine(RealertWaveEnemies());
            }

            Debug.Log($"[ArafThreatDirector] Level {awakeningLevel} wave spawned on return path from slot {sealSlotIndex}: {spawned}/{waveSize}.", this);
        }

        private void TrackWaveEnemy(IAlertableEnemy alertable, IEncounterEnemy enemy, Transform enemyTransform)
        {
            for (int i = 0; i < MaxTrackedWaveEnemies; i++)
            {
                if (_waveAlertables[i] != null && _waveEnemies[i] != null && _waveEnemies[i].IsAlive) continue;
                _waveAlertables[i] = alertable;
                _waveEnemies[i] = enemy;
                _waveTransforms[i] = enemyTransform;
                return;
            }
        }

        private bool IsWithinAlertDistance(Transform enemy, Transform player)
        {
            if (enemy == null || player == null) return false;
            float limit = Mathf.Max(0f, waveAlertDistance);
            return (enemy.position - player.position).sqrMagnitude <= limit * limit;
        }

        private bool HasTrackedWaveEnemies()
        {
            for (int i = 0; i < MaxTrackedWaveEnemies; i++)
                if (_waveAlertables[i] != null && _waveEnemies[i] != null && _waveEnemies[i].IsAlive) return true;
            return false;
        }

        private System.Collections.IEnumerator RealertWaveEnemies()
        {
            while (true)
            {
                yield return _realertWait;
                Transform player = GetPlayerTransform();
                if (player == null) break;
                int alive = 0;
                for (int i = 0; i < MaxTrackedWaveEnemies; i++)
                {
                    IEncounterEnemy enemy = _waveEnemies[i];
                    if (_waveAlertables[i] == null || enemy == null || !enemy.IsAlive || _waveTransforms[i] == null)
                    {
                        _waveAlertables[i] = null;
                        _waveEnemies[i] = null;
                        _waveTransforms[i] = null;
                        continue;
                    }
                    alive++;
                    // Ghoul, oyuncu loseSightDistance (24 m) dışındaysa takibi bırakır; yalnızca menzildekileri uyar.
                    if (IsWithinAlertDistance(_waveTransforms[i], player))
                        _waveAlertables[i].ReceiveAlert(player);
                }
                if (alive == 0) break;
            }
            _realertRoutine = null;
        }

        private GameObject SpawnEnemy(GameObject prefab, Vector3 worldPos, Quaternion rot, out IEncounterEnemy encounterEnemy)
        {
            GameObject instance = Instantiate(prefab, worldPos, rot, labyrinth.GeneratedRoot);
            if (instance == null)
            {
                encounterEnemy = null;
                return null;
            }

            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }

            if (instance.TryGetComponent(out encounterEnemy))
            {
                encounterEnemy.OnSpawnFromDirector(worldPos, rot);
            }
            return instance;
        }

        private Vector3 CellToWorldSpawn(LabyrinthLayout layout, Transform root, GridPos cell)
        {
            Vector3 local = layout.CellToLocal(cell.X, cell.Y);
            return root.TransformPoint(local) + Vector3.up * SpawnHeightOffset;
        }

        private Quaternion GetWaveSpawnRotation(Vector3 spawnWorldPos, Transform player, RunRandom rng)
        {
            if (player != null)
            {
                Vector3 toPlayer = player.position - spawnWorldPos;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    return Quaternion.LookRotation(toPlayer);
                }
            }
            return labyrinth.transform.rotation * Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
        }

        private Vector3 GetAvoidLocal(LabyrinthLayout layout, Transform root)
        {
            Transform player = GetPlayerTransform();
            if (player != null)
            {
                Vector3 playerLocal = root.InverseTransformPoint(player.position);
                // During Araf entry fade, SetContentActive(true) fires before player revive moves the player out of the tower.
                if (Mathf.Abs(playerLocal.y) <= PlayerLocalHeightTolerance)
                {
                    return playerLocal;
                }
            }

            if (_level != null && _level.EntryPoint != null)
            {
                return root.InverseTransformPoint(_level.EntryPoint.position);
            }

            return layout.StartLocalPosition;
        }

        private Transform GetPlayerTransform()
        {
            if (!_playerSearched)
            {
                _playerSearched = true;
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null) _playerTransform = playerHealth.transform;
            }
            return _playerTransform;
        }

        private void EnsureCellBuffers()
        {
            int required = DefaultCellBufferCapacity;
            for (int section = 0; section < labyrinth.SectionCount; section++)
            {
                LabyrinthLayout layout = labyrinth.GetSectionLayout(section);
                if (layout != null) required = Mathf.Max(required, layout.Width * layout.Height);
            }
            if (_cellBuffer.Length < required) _cellBuffer = new GridPos[required];
            if (_pathBuffer.Length < required) _pathBuffer = new GridPos[required];
        }

        private void AllocatePatrolSlots(int capacity)
        {
            int size = Mathf.Max(0, capacity);
            _patrolSlots = new GameObject[size];
            _patrolEnemies = new IEncounterEnemy[size];
        }

        private int CountAlivePatrols()
        {
            int alive = 0;
            for (int i = 0; i < _patrolSlots.Length; i++)
            {
                if (IsPatrolSlotAlive(i)) alive++;
            }
            return alive;
        }

        private int FindFreePatrolSlot()
        {
            for (int i = 0; i < _patrolSlots.Length; i++)
            {
                if (!IsPatrolSlotAlive(i)) return i;
            }
            return -1;
        }

        private bool IsPatrolSlotAlive(int index)
        {
            GameObject slot = _patrolSlots[index];
            if (slot == null || !slot.activeInHierarchy) return false;
            IEncounterEnemy enemy = _patrolEnemies[index];
            return enemy == null || enemy.IsAlive;
        }

        private int GetPatrolsAddedAtLevel(int level)
        {
            if (_tuning != null) return _tuning.GetPatrolsAddedAtLevel(level);
            return level >= 0 && level < DefaultPatrolsAddedAtLevel.Length ? DefaultPatrolsAddedAtLevel[level] : 0;
        }

        private int GetMaxAlivePatrols()
        {
            return _tuning != null ? Mathf.Max(0, _tuning.MaxAlivePatrols) : DefaultMaxAlivePatrols;
        }

        private int GetWaveSize(int level)
        {
            if (_tuning != null) return _tuning.GetWaveSize(level);
            if (level < 0) return DefaultWaveSizeByLevel[0];
            return level < DefaultWaveSizeByLevel.Length
                ? DefaultWaveSizeByLevel[level]
                : DefaultWaveSizeByLevel[DefaultWaveSizeByLevel.Length - 1];
        }

        private float GetMinSpawnDistance()
        {
            return _tuning != null ? _tuning.MinSpawnDistance : DefaultMinSpawnDistance;
        }
    }
}
