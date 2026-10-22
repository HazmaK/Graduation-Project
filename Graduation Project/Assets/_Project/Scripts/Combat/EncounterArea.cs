using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Level Designer Karşılaşma Alanı (Encounter Area & Pre-Wave Zone).
    /// Hiyerarşiye kolayca eklenen bir BoxCollider hacmidir.
    /// - Bu alan içerisine harita tasarımcısı tarafından elle yerleştirilen (manually placed) düşmanları otomatik algılar.
    /// - Alandaki tüm manuel düşmanlar yok edildiğinde (ve oyuncu alana girdiğinde) WaveSpawnDirector'ı tetikler.
    /// - Oyuncu odaya girdiğinde muhafızları otomatik alarma geçirir.
    /// - Scene View üzerinde renkli sınır kutusu ve düşman bağlantı çizgileri (Gizmos) çizer.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RetroFPS/Encounter Area (Manuel Düşman + Dalga Alanı)")]
    public class EncounterArea : MonoBehaviour
    {
        [Header("Karşılaşma Direktörü Bağlantısı")]
        [Tooltip("Tetiklenecek WaveSpawnDirector. Boş ise aynı objede, ebeveynde veya sahnede otomatik bulunur.")]
        [SerializeField] private WaveSpawnDirector waveDirector;

        [Header("Manuel Düşman Algılama Ayarları")]
        [Tooltip("Bu BoxCollider alanının içine elle koyulmuş tüm düşmanlar otomatik olarak ön muhafız sayılsın mı?")]
        [SerializeField] private bool autoDetectEnemiesInBounds = true;

        [Tooltip("Manuel olarak elle seçmek istediğiniz düşmanlar varsa buraya ekleyebilirsiniz. Boşsa alandakiler otomatik taranır.")]
        [SerializeField] private List<GameObject> specificManualEnemies = new List<GameObject>();

        [Header("Tetikleme ve Pacing Kuralları")]
        [Tooltip("Alandaki manuel muhafızların yüzde kaçı öldürüldüğünde dalgalar başlasın? (0.25 = %25)")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float guardDeathRatioToTriggerWaves = 0.25f;

        [Tooltip("Tüm ön muhafızlar öldükten sonra dalgaların başlaması için oyuncunun da bu alanın içinde olması zorunlu mu?")]
        [SerializeField] private bool requirePlayerInsideToStartWaves = true;

        [Tooltip("Oyuncu bu alana adım attığında alandaki tüm muhafızlar hemen oyuncuyu kovalamaya başlasın mı?")]
        [SerializeField] private bool alertGuardsWhenPlayerEnters = true;

        [Tooltip("Gereken muhafız yüzdesi öldükten sonra dalga başlamadan önceki kısa nefes payı (saniye)")]
        [SerializeField] private float delayBeforeWaveStart = 1.0f;

        [Header("Otomatik Doğuş Noktaları (Prosedürel Spawn Points)")]
        [Tooltip("Alanın içinde doğuş noktaları yoksa, zemin geometrisini tarayarak otomatik spawn point'ler üretsin mi?")]
        [SerializeField] private bool autoGenerateSpawnPointsIfEmpty = true;

        [Tooltip("Otomatik üretilecek zemin doğuş noktası sayısı")]
        [SerializeField] private int groundSpawnPointCount = 6;

        [Tooltip("Otomatik üretilecek hava doğuş noktası sayısı (Uçan düşmanlar için)")]
        [SerializeField] private int airSpawnPointCount = 2;

        [Tooltip("Zemini ve engelleri tespit eden fizik katman maskesi")]
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Header("Ses Efektleri")]
        [Tooltip("Tüm muhafızlar temizlendiğinde çalacak arena mühürlenme / dalga alarm sesi")]
        [SerializeField] private AudioClip guardsClearedSound;

        // Çalışma Zamanı Takip Verileri
        private BoxCollider _areaCollider;
        private readonly List<IEncounterEnemy> _trackedGuards = new List<IEncounterEnemy>(16);
        private int _initialGuardCount = 0;
        private int _killedGuardsCount = 0;
        private bool _isPlayerInside = false;
        private bool _hasTriggeredWaves = false;
        private bool _isEncounterCompleted = false;
        private bool _waitingForPlayerToTrigger = false;
        private AudioSource _audioSource;

        // Olay Bildirimleri (Observer Pattern)
        public event System.Action<int, int> OnGuardCountChanged; // (remaining, total)
        public event System.Action OnAllGuardsCleared;
        public event System.Action OnPlayerEnteredArea;

        public WaveSpawnDirector WaveDirector => waveDirector;
        public int InitialGuardCount => _initialGuardCount;
        public int RemainingGuardCount => _trackedGuards.Count;
        public int KilledGuardsCount => _killedGuardsCount;
        public float GuardDeathRatio => guardDeathRatioToTriggerWaves;
        public int RequiredGuardKills => Mathf.Max(1, Mathf.CeilToInt(_initialGuardCount * guardDeathRatioToTriggerWaves));
        public bool HasGuards => _trackedGuards.Count > 0;
        public bool IsPlayerInside => _isPlayerInside;
        public bool HasTriggeredWaves => _hasTriggeredWaves;
        public bool IsEncounterCompleted => _isEncounterCompleted;

        private void Awake()
        {
            _areaCollider = GetComponent<BoxCollider>();
            if (_areaCollider != null)
            {
                _areaCollider.isTrigger = true;
            }

            // Eğer alanda spawn noktası yoksa ve otomatik üretim açıksa başlatmada otomatik üret
            if (autoGenerateSpawnPointsIfEmpty)
            {
                EncounterSpawnPoint[] currentPoints = GetComponentsInChildren<EncounterSpawnPoint>(true);
                if (currentPoints == null || currentPoints.Length == 0)
                {
                    GenerateSpawnPoints();
                }
            }

            ResolveDirector();
            SetupAudio();
        }

        private void Start()
        {
            ScanAndRegisterManualEnemies();
        }

        private void ResolveDirector()
        {
            if (waveDirector == null)
            {
                waveDirector = GetComponent<WaveSpawnDirector>();
                if (waveDirector == null) waveDirector = GetComponentInParent<WaveSpawnDirector>();
                if (waveDirector == null) waveDirector = Object.FindAnyObjectByType<WaveSpawnDirector>();
            }

            if (waveDirector != null)
            {
                waveDirector.RegisterEncounterArea(this);
                waveDirector.OnEncounterCompleted -= HandleDirectorEncounterCompleted;
                waveDirector.OnEncounterCompleted += HandleDirectorEncounterCompleted;

                var points = GetComponentsInChildren<EncounterSpawnPoint>(true);
                if (points != null && points.Length > 0 && (waveDirector.SpawnPoints == null || waveDirector.SpawnPoints.Length == 0))
                {
                    waveDirector.ConfigureSpawnPoints(points);
                }
            }
        }

        private void HandleDirectorEncounterCompleted()
        {
            _isEncounterCompleted = true;
            Debug.Log($"<color=green>[EncounterArea]</color> '{gameObject.name}' odasındaki tüm dalgalar zaferle tamamlandı! Bu karşılaşma alanı tamamen kapandı.");
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Alan içerisindeki veya elle atanmış manuel düşmanları bularak muhafız olarak kaydeder.
        /// </summary>
        public void ScanAndRegisterManualEnemies()
        {
            _trackedGuards.Clear();

            // 1. Elle atanmış spesifik düşmanlar varsa önce onları ekle
            if (specificManualEnemies != null && specificManualEnemies.Count > 0)
            {
                for (int i = 0; i < specificManualEnemies.Count; i++)
                {
                    GameObject go = specificManualEnemies[i];
                    if (go == null) continue;
                    IEncounterEnemy enemy = go.GetComponent<IEncounterEnemy>();
                    if (enemy != null && !_trackedGuards.Contains(enemy))
                    {
                        RegisterGuard(enemy);
                    }
                }
            }

            // 2. Alan içi otomatik tarama aktifse, BoxCollider bounds içindeki tüm düşmanları topla
            if (autoDetectEnemiesInBounds && _areaCollider != null)
            {
                Bounds bounds = _areaCollider.bounds;

                // Sahnede aktif olan tüm IEncounterEnemy bileşenlerini tara
                MonoBehaviour[] allMonos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allMonos.Length; i++)
                {
                    if (allMonos[i] is IEncounterEnemy enemy && enemy.EnemyGameObject != null)
                    {
                        // Nesne havuzu içinde pasif bekleyen düşmanları atla
                        if (!enemy.EnemyGameObject.activeInHierarchy) continue;

                        Vector3 pos = enemy.EnemyTransform.position;
                        if (bounds.Contains(pos) && !_trackedGuards.Contains(enemy))
                        {
                            RegisterGuard(enemy);
                        }
                    }
                }
            }

            _initialGuardCount = _trackedGuards.Count;
            OnGuardCountChanged?.Invoke(_trackedGuards.Count, _initialGuardCount);

            if (_initialGuardCount > 0)
            {
                Debug.Log($"<color=orange>[EncounterArea]</color> '{gameObject.name}' alanında <b>{_initialGuardCount} adet manuel muhafız</b> tespit edildi. Tümü öldürüldüğünde dalga başlayacak!");
            }
        }

        private void RegisterGuard(IEncounterEnemy guard)
        {
            _trackedGuards.Add(guard);

            // Muhafız öldüğünde dalganın bozulmaması için otomatik yeniden doğmasını kapat
            if (guard is BasicEnemyAI ghoul)
            {
                ghoul.AutoRespawn = false;
            }
            else if (guard is FlyingEyeAI eye)
            {
                eye.AutoRespawn = false;
            }

            guard.OnEnemyDeath -= HandleGuardDeath;
            guard.OnEnemyDeath += HandleGuardDeath;
        }

        private void HandleGuardDeath(IEncounterEnemy deadGuard)
        {
            if (deadGuard == null || _isEncounterCompleted) return;
            deadGuard.OnEnemyDeath -= HandleGuardDeath;
            _trackedGuards.Remove(deadGuard);
            _killedGuardsCount++;

            int remaining = _trackedGuards.Count;
            OnGuardCountChanged?.Invoke(remaining, _initialGuardCount);

            int requiredKills = RequiredGuardKills;
            Debug.Log($"<color=orange>[EncounterArea]</color> Muhafız yok edildi! ({_killedGuardsCount}/{requiredKills} ölüm - Kalan Canlı: {remaining}/{_initialGuardCount})");

            // KURAL: Manuel düşmanların %25'i öldüğünde dalga başlar!
            if (!_hasTriggeredWaves && _killedGuardsCount >= requiredKills)
            {
                CheckAndTriggerWaves();
            }

            if (remaining == 0)
            {
                HandleAllGuardsDead();
            }
        }

        private void CheckAndTriggerWaves()
        {
            if (_hasTriggeredWaves || _isEncounterCompleted) return;

            // Eğer oyuncunun alanda olması gerekiyorsa ve oyuncu henüz odaya girmediyse bekle
            if (requirePlayerInsideToStartWaves && !_isPlayerInside)
            {
                _waitingForPlayerToTrigger = true;
                Debug.Log($"<color=yellow>[EncounterArea]</color> Muhafızların %{guardDeathRatioToTriggerWaves * 100f:F0}'i öldü! Dalganın başlaması için oyuncunun odaya girmesi bekleniyor.");
                return;
            }

            TriggerWaveEncounter();
        }

        private void HandleAllGuardsDead()
        {
            OnAllGuardsCleared?.Invoke();

            if (guardsClearedSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(guardsClearedSound);
            }

            if (!_hasTriggeredWaves)
            {
                CheckAndTriggerWaves();
            }
        }

        private void TriggerWaveEncounter()
        {
            if (_hasTriggeredWaves || _isEncounterCompleted) return;
            _hasTriggeredWaves = true;
            _waitingForPlayerToTrigger = false;

            StartCoroutine(DelayedWaveStartRoutine());
        }

        private IEnumerator DelayedWaveStartRoutine()
        {
            if (delayBeforeWaveStart > 0f)
            {
                yield return new WaitForSeconds(delayBeforeWaveStart);
            }

            if (waveDirector != null && waveDirector.State == EncounterState.Inactive)
            {
                Debug.Log($"<color=red><b>[EncounterArea -> WaveSpawnDirector]</b></color> Manuel muhafızların %{guardDeathRatioToTriggerWaves * 100f:F0}'i öldü! Dalga Karşılaşması başlatılıyor!");
                waveDirector.StartEncounter();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isEncounterCompleted) return;

            // Oyuncu alana girdiğinde
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
            {
                _isPlayerInside = true;
                OnPlayerEnteredArea?.Invoke();

                // Bu odayı global aktif karşılaşma alanı olarak duyur
                if (waveDirector != null)
                {
                    WaveSpawnDirector.SetActiveDirector(waveDirector);
                }

                // 1. Alandaki tüm muhafızları hemen alarma geçirip oyuncuya yönlendir
                if (alertGuardsWhenPlayerEnters && _trackedGuards.Count > 0)
                {
                    Transform playerT = other.transform;
                    for (int i = 0; i < _trackedGuards.Count; i++)
                    {
                        var g = _trackedGuards[i];
                        if (g is IAlertableEnemy alertable)
                        {
                            alertable.ReceiveAlert(playerT);
                        }
                    }
                }

                // 2. Eğer muhafızlar önceden dışarıdan vurulmuş ve gereken %25 eşiğine ulaşılmışsa hemen başlat
                if (_waitingForPlayerToTrigger && !_hasTriggeredWaves)
                {
                    TriggerWaveEncounter();
                }
                // 3. Eğer alanda hiç muhafız yoksa ve direktör beklemedeyse direkt başlat
                else if (_initialGuardCount == 0 && !_hasTriggeredWaves)
                {
                    TriggerWaveEncounter();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
            {
                _isPlayerInside = false;
            }
        }

        /// <summary>
        /// Bu alana ait veya çocuk objeleri olan tüm EncounterSpawnPoint bileşenlerini döner.
        /// Eğer henüz hiç yoksa ve autoGenerateSpawnPointsIfEmpty aktifse otomatik üretir.
        /// </summary>
        public EncounterSpawnPoint[] GetSpawnPoints()
        {
            EncounterSpawnPoint[] points = GetComponentsInChildren<EncounterSpawnPoint>(true);
            if ((points == null || points.Length == 0) && autoGenerateSpawnPointsIfEmpty)
            {
                GenerateSpawnPoints();
                points = GetComponentsInChildren<EncounterSpawnPoint>(true);
            }
            return points;
        }

        /// <summary>
        /// Alanın BoxCollider sınırları ve zemin geometrisini fizik taraması (Raycast) ile analiz ederek
        /// zemin ve hava doğuş noktalarını odaya bakacak şekilde prosedürel olarak otomatik üretir.
        /// </summary>
        [ContextMenu("Doğuş Noktalarını Otomatik Üret (Prosedürel)")]
        public void GenerateSpawnPoints()
        {
            ClearSpawnPoints();

            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) return;

            Transform spawnGroup = transform.Find("SpawnPoints");
            if (spawnGroup == null)
            {
                GameObject groupGo = new GameObject("SpawnPoints");
                groupGo.transform.SetParent(transform, false);
                spawnGroup = groupGo.transform;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(groupGo, "Create SpawnPoints Group");
                }
#endif
            }

            Vector3 boxCenterLocal = col.center;
            Vector3 halfSize = col.size * 0.5f;
            Vector3 roomCenterWorld = transform.TransformPoint(boxCenterLocal);

            float rayHeight = (col.size.y * transform.lossyScale.y) + 10f;

            // 1. Zemin Doğuş Noktaları (Odanın çevresine dağıtılmış, odaya bakan)
            for (int i = 0; i < groundSpawnPointCount; i++)
            {
                float angle = i * (Mathf.PI * 2f / Mathf.Max(1, groundSpawnPointCount));
                // Duvarlara yapışmayı önlemek için %72 çevre marjı
                float localX = Mathf.Cos(angle) * (halfSize.x * 0.72f);
                float localZ = Mathf.Sin(angle) * (halfSize.z * 0.72f);

                Vector3 sampleOriginLocal = boxCenterLocal + new Vector3(localX, halfSize.y, localZ);
                Vector3 sampleOriginWorld = transform.TransformPoint(sampleOriginLocal);

                Vector3 targetWorldPos;
                if (Physics.Raycast(sampleOriginWorld, Vector3.down, out RaycastHit hit, rayHeight, groundLayerMask, QueryTriggerInteraction.Ignore))
                {
                    targetWorldPos = hit.point + Vector3.up * 0.05f;
                }
                else
                {
                    targetWorldPos = transform.TransformPoint(boxCenterLocal + new Vector3(localX, -halfSize.y + 0.1f, localZ));
                }

                // Merkeze doğru bakış yönü
                Vector3 lookDir = (roomCenterWorld - targetWorldPos);
                lookDir.y = 0f;
                Quaternion lookRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                GameObject spGO = new GameObject($"SpawnPoint_Ground_{i + 1:D2}");
                spGO.transform.SetParent(spawnGroup, false);
                spGO.transform.position = targetWorldPos;
                spGO.transform.rotation = lookRot;

                EncounterSpawnPoint sp = spGO.AddComponent<EncounterSpawnPoint>();
                sp.Configure(allowGround: true, allowAir: false, tag: gameObject.name, cooldown: 2.0f);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(spGO, "Create Ground Spawn Point");
                }
#endif
            }

            // 2. Hava Doğuş Noktaları (Uçan düşmanlar için yükseltilmiş noktalar)
            for (int i = 0; i < airSpawnPointCount; i++)
            {
                float angle = (i * (Mathf.PI * 2f / Mathf.Max(1, airSpawnPointCount))) + (Mathf.PI / 4f);
                float localX = Mathf.Cos(angle) * (halfSize.x * 0.45f);
                float localZ = Mathf.Sin(angle) * (halfSize.z * 0.45f);

                Vector3 sampleOriginLocal = boxCenterLocal + new Vector3(localX, halfSize.y, localZ);
                Vector3 sampleOriginWorld = transform.TransformPoint(sampleOriginLocal);

                Vector3 groundHitPos;
                if (Physics.Raycast(sampleOriginWorld, Vector3.down, out RaycastHit hit, rayHeight, groundLayerMask, QueryTriggerInteraction.Ignore))
                {
                    groundHitPos = hit.point;
                }
                else
                {
                    groundHitPos = transform.TransformPoint(boxCenterLocal + new Vector3(localX, -halfSize.y, localZ));
                }

                float airElevation = Mathf.Clamp(col.size.y * transform.lossyScale.y * 0.60f, 2.5f, 5.0f);
                Vector3 airPos = groundHitPos + Vector3.up * airElevation;

                Vector3 lookDir = (roomCenterWorld - airPos);
                lookDir.y = 0f;
                Quaternion lookRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                GameObject spGO = new GameObject($"SpawnPoint_Air_{i + 1:D2}");
                spGO.transform.SetParent(spawnGroup, false);
                spGO.transform.position = airPos;
                spGO.transform.rotation = lookRot;

                EncounterSpawnPoint sp = spGO.AddComponent<EncounterSpawnPoint>();
                sp.Configure(allowGround: false, allowAir: true, tag: gameObject.name, cooldown: 2.5f);

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(spGO, "Create Air Spawn Point");
                }
#endif
            }

            Debug.Log($"<color=green>[EncounterArea]</color> '{gameObject.name}' için <b>{groundSpawnPointCount} zemin</b> + <b>{airSpawnPointCount} hava</b> doğuş noktası zemin geometrisine oturtularak otomatik üretildi!");

            // WaveDirector'a da bildir
            if (waveDirector != null)
            {
                waveDirector.ConfigureSpawnPoints(GetComponentsInChildren<EncounterSpawnPoint>(true));
            }
        }

        /// <summary>
        /// Alandaki üretilmiş doğuş noktalarını temizler.
        /// </summary>
        [ContextMenu("Tüm Doğuş Noktalarını Temizle")]
        public void ClearSpawnPoints()
        {
            Transform spawnGroup = transform.Find("SpawnPoints");
            if (spawnGroup != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnGroup.gameObject);
                }
                else
                {
                    DestroyImmediate(spawnGroup.gameObject);
                }
            }

            // Doğrudan child olarak eklenmiş olabilecek spawn point'ler varsa onları da temizle
            EncounterSpawnPoint[] directPoints = GetComponentsInChildren<EncounterSpawnPoint>(true);
            for (int i = directPoints.Length - 1; i >= 0; i--)
            {
                if (directPoints[i] != null)
                {
                    if (Application.isPlaying) Destroy(directPoints[i].gameObject);
                    else DestroyImmediate(directPoints[i].gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            if (waveDirector != null)
            {
                waveDirector.OnEncounterCompleted -= HandleDirectorEncounterCompleted;
            }

            for (int i = 0; i < _trackedGuards.Count; i++)
            {
                if (_trackedGuards[i] != null)
                {
                    _trackedGuards[i].OnEnemyDeath -= HandleGuardDeath;
                }
            }
            _trackedGuards.Clear();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) return;

            Matrix4x4 oldMat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Color boxColor;
            if (_hasTriggeredWaves)
            {
                boxColor = new Color(0.95f, 0.20f, 0.15f, 0.18f); // Kırmızı: Dalgalar başladı
            }
            else if (_trackedGuards.Count > 0 || (specificManualEnemies != null && specificManualEnemies.Count > 0))
            {
                boxColor = new Color(1.0f, 0.65f, 0.10f, 0.20f); // Turuncu/Amber: Muhafızlar bekliyor
            }
            else
            {
                boxColor = new Color(0.20f, 0.85f, 0.40f, 0.15f); // Yeşil: Alan hazır
            }

            Gizmos.color = boxColor;
            Gizmos.DrawCube(col.center, col.size);

            Color wireColor = boxColor;
            wireColor.a = 0.90f;
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(col.center, col.size);

            Gizmos.matrix = oldMat;

            // Alandaki muhafızlara doğru net bağlantı çizgileri çiz
            if (Application.isPlaying && _trackedGuards != null)
            {
                for (int i = 0; i < _trackedGuards.Count; i++)
                {
                    var g = _trackedGuards[i];
                    if (g != null && g.EnemyTransform != null)
                    {
                        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                        Gizmos.DrawLine(transform.position, g.EnemyTransform.position);
                        Gizmos.DrawWireSphere(g.EnemyTransform.position + Vector3.up * 1f, 0.4f);
                    }
                }
            }
            else if (!Application.isPlaying && _areaCollider != null)
            {
                Bounds b = _areaCollider.bounds;
                var allMonos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allMonos.Length; i++)
                {
                    if (allMonos[i] is IEncounterEnemy enemy && enemy.EnemyGameObject != null)
                    {
                        if (b.Contains(enemy.EnemyTransform.position))
                        {
                            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.75f);
                            Gizmos.DrawLine(transform.position, enemy.EnemyTransform.position);
                            Gizmos.DrawWireSphere(enemy.EnemyTransform.position + Vector3.up * 1f, 0.35f);
                        }
                    }
                }
            }
        }
#endif
    }
}
