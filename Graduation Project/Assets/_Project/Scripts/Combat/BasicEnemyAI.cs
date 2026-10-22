using System.Collections.Generic;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Windup,
        Attack,
        Stagger,
        Dead
    }

    /// <summary>
    /// Karanlık Fantastik Retro Boomer Shooter Düşman Yapay Zekası (Gargoyle / Ghoul Stalker).
    /// - Görüş Açısı (FOV) ve Doğrudan Görüş Hattı (LoS): Sadece oyuncuyu gördüğünde saldırır.
    /// - Devriye ve Gezinme: Oyuncuyu görmediğinde etrafta sakin devriye gezer.
    /// - Sürü Bilinci (Pack Mentality): Bir düşman saldırdığında yakındaki müttefikler de alarma geçer, uzaktakiler etkilenmez.
    /// - NavMesh bağımsız, CharacterController tabanlı akıllı takip ve engelden kaçınma
    /// - Saldırı öncesi net telgraf uyarısı (Windup Flash: Oyuncuya Dash ile kaçma fırsatı verir)
    /// - Ağır hasarlarda tökezleme (Stagger) ve kafa vuruşlarını (Headshot: 2x Hasar) algılama
    /// - Ölümden 4 saniye sonra otomatik yeniden doğma (Kesintisiz arena testi)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BasicEnemyAI : MonoBehaviour, IDamageable, IAlertableEnemy, IStunnable, IEncounterEnemy
    {
        [Header("Sağlık ve Ödül")]
        [SerializeField] private float maxHealth = 150f;
        [SerializeField] private float respawnTime = 4.0f;
        [SerializeField] private bool autoRespawn = true;

        public bool AutoRespawn { get => autoRespawn; set => autoRespawn = value; }

        [Header("Hareket ve Takip")]
        [SerializeField] private float chaseSpeed = 4.8f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = 22f;

        [Header("Görüş ve Algılama")]
        [SerializeField] private float detectionRange = 18f;
        [SerializeField] private float fieldOfView = 110f; // Görüş açısı konisi (derece)
        [SerializeField] private float proximityRange = 2.8f; // Arkadan yaklaşmada duyma/hissetme mesafesi
        [SerializeField] private float loseSightDistance = 24f; // Kovalamayı bırakma mesafesi
        [SerializeField] private float loseSightDuration = 3.5f; // Görüşü kaybettikten sonra arama süresi

        [Header("Devriye ve Gezinme")]
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float patrolRadius = 6.0f;
        [SerializeField] private float idleDurationMin = 1.5f;
        [SerializeField] private float idleDurationMax = 3.0f;

        [Header("Sürü / Grup İletişimi")]
        [SerializeField] private float alertRadius = 10.0f; // Yakındaki müttefikleri uyarma mesafesi

        [Header("Saldırı Telgrafı & Parry Uyarısı")]
        [Tooltip("Saldırı hazırlık süresi (saniye)")]
        [SerializeField] private float windupDuration = 0.58f;
        [Tooltip("Darbe inmeden önce parry flaşının ve sesinin tetikleneceği an (saniye)")]
        [SerializeField] private float parryGlintTiming = 0.20f;
        [SerializeField] private Color glintColor = new Color(1.0f, 0.95f, 0.25f);
        [SerializeField] private AudioClip customGlintSound;

        [Header("Saldırı Parametreleri")]
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float attackCooldown = 1.2f;

        [Header("Görsel Tepkiler")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.20f, 0.24f);
        [SerializeField] private Color windupGlowColor = new Color(1.0f, 0.35f, 0.05f);
        [SerializeField] private Color damageFlashColor = new Color(1.0f, 0.1f, 0.1f);
        [SerializeField] private Color critFlashColor = new Color(1.0f, 0.9f, 0.1f);

        private bool _glintTriggered = false;
        private float _visualTilt = 0f;
        private float _targetVisualTilt = 0f;
        private static AudioClip _cachedGlintClip;

        private CharacterController _controller;
        private Transform _playerTransform;
        private PlayerHealth _playerHealth;

        private EnemyState _currentState = EnemyState.Idle;
        private float _currentHealth;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _initialScale;
        private Vector3 _velocity;

        private Vector3 _patrolTarget;
        private Vector3 _lastKnownPlayerPos;
        private float _loseSightTimer = 0f;

        private float _stateTimer = 0f;
        private float _cooldownTimer = 0f;
        private float _flashTimer = 0f;
        private float _squashTimer = 0f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Ölüm ve Yeniden Doğuş Durum Değişkenleri
        private bool _isGibbed = false;
        private Transform _headTransform;
        private float _deathAnimTimer = 0f;
        private Quaternion _deathStartRot;
        private Quaternion _deathTargetRot;
        private Vector3 _deathStartPos;
        private Vector3 _deathTargetPos;
        private float _respawnPopTimer = 0f;

        public EnemyState CurrentState => _currentState;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public Vector3 Position => transform.position;
        public bool IsDead => _currentState == EnemyState.Dead;

        // IEncounterEnemy Uygulaması
        public event System.Action<IEncounterEnemy> OnEnemyDeath;
        public bool IsAlive => _currentState != EnemyState.Dead;
        public Transform EnemyTransform => transform;
        public GameObject EnemyGameObject => gameObject;

        private void OnEnable()
        {
            EnemyAlertHub.Register(this);
        }

        private void OnDisable()
        {
            EnemyAlertHub.Unregister(this);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _initialScale = transform.localScale;
            _currentHealth = maxHealth;
            _patrolTarget = _spawnPosition;
            _lastKnownPlayerPos = _spawnPosition;

            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            _propBlock = new MaterialPropertyBlock();

            ValidateAndRepairMaterials();

            FindPlayer();
            SetupHitboxes();
            _headTransform = transform.Find("Head");

            // Başlangıçta Idle modunda hafif bekleme ile başla
            _currentState = EnemyState.Idle;
            _stateTimer = Random.Range(idleDurationMin, idleDurationMax);
        }

        private void SetCollidersActive(bool active)
        {
            if (_controller != null) _controller.enabled = active;
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null && _colliders[i] != _controller)
                    {
                        _colliders[i].enabled = active;
                    }
                }
            }
        }

        private void SetRenderersActive(bool active)
        {
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].enabled = active;
                    }
                }
            }
        }

        private void FindPlayer()
        {
            PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null)
            {
                _playerTransform = pm.transform;
                _playerHealth = pm.GetComponent<PlayerHealth>();
            }
        }

        private void SetupHitboxes()
        {
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;

                bool isHead = col.gameObject.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Hitbox hb = col.GetComponent<Hitbox>();
                if (hb == null)
                {
                    hb = col.gameObject.AddComponent<Hitbox>();
                }
                hb.Initialize(this, isHead ? 2.0f : 1.0f, isHead);
            }
        }

        private void Update()
        {
            if (_currentState == EnemyState.Dead)
            {
                _stateTimer -= Time.deltaTime;

                if (!_isGibbed)
                {
                    _deathAnimTimer += Time.deltaTime;

                    // 1. Aşama (0 - 0.45s): Fiziksel geriye yıkılma ve zemin teması
                    if (_deathAnimTimer <= 0.45f)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, _deathAnimTimer / 0.45f);
                        transform.rotation = Quaternion.Slerp(_deathStartRot, _deathTargetRot, t);
                        transform.position = Vector3.Lerp(_deathStartPos, _deathTargetPos, t);
                    }
                    // 2. Aşama (0.45s - 1.20s): Zemin altına batarak okült çözünme
                    else if (_deathAnimTimer <= 1.20f)
                    {
                        float sinkT = (_deathAnimTimer - 0.45f) / 0.75f;
                        transform.position = Vector3.Lerp(_deathTargetPos, _deathTargetPos - Vector3.up * 1.6f, sinkT);

                        if (_deathAnimTimer >= 1.18f)
                        {
                            SetRenderersActive(false);
                            transform.localScale = _initialScale;
                        }
                    }
                }

                if (_stateTimer <= 0f)
                {
                    if (autoRespawn)
                    {
                        Respawn();
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }
                }
                return;
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            UpdateTimers();
            _visualTilt = Mathf.Lerp(_visualTilt, _targetVisualTilt, Time.deltaTime * 12f);

            // Yeniden doğuş organik pop / yaylanma efekti
            if (_respawnPopTimer > 0f)
            {
                _respawnPopTimer -= Time.deltaTime;
                float popT = 1f - (_respawnPopTimer / 0.22f);
                float scaleMod = popT < 0.7f ? Mathf.Lerp(0.1f, 1.14f, popT / 0.7f) : Mathf.Lerp(1.14f, 1.0f, (popT - 0.7f) / 0.3f);
                transform.localScale = _initialScale * scaleMod;
            }
            // Squash & Stretch darbe reaksiyonu (Organik ezilme-yaylanma)
            else if (_squashTimer > 0f)
            {
                _squashTimer -= Time.deltaTime;
                float sqT = _squashTimer / 0.18f;
                transform.localScale = new Vector3(
                    _initialScale.x * (1f + 0.15f * sqT),
                    _initialScale.y * (1f - 0.22f * sqT),
                    _initialScale.z * (1f + 0.15f * sqT)
                );
            }
            else
            {
                transform.localScale = _initialScale;
            }

            ExecuteStateMachine();
            ApplyGravity();
        }

        private void UpdateTimers()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _currentState != EnemyState.Windup)
                {
                    ClearFlash();
                }
            }
        }

        private void ExecuteStateMachine()
        {
            float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

            switch (_currentState)
            {
                case EnemyState.Idle:
                    ExecuteIdle();
                    break;

                case EnemyState.Patrol:
                    ExecutePatrol();
                    break;

                case EnemyState.Chase:
                    ExecuteChase(distToPlayer);
                    break;

                case EnemyState.Windup:
                    ExecuteWindup();
                    break;

                case EnemyState.Attack:
                    ExecuteAttackState();
                    break;

                case EnemyState.Stagger:
                    ExecuteStagger();
                    break;
            }
        }

        private void ExecuteIdle()
        {
            _velocity.x = 0f;
            _velocity.z = 0f;

            // Görüş kontrolü: Oyuncuyu görürse hemen saldır ve yakındaki müttefikleri uyar
            if (CanSeePlayer())
            {
                StartChase(true);
                return;
            }

            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                PickNewPatrolDestination();
            }
        }

        private void ExecutePatrol()
        {
            // Görüş kontrolü: Devriye gezerken oyuncuyu görürse hemen saldır
            if (CanSeePlayer())
            {
                StartChase(true);
                return;
            }

            _stateTimer -= Time.deltaTime;

            Vector3 dirToDest = _patrolTarget - transform.position;
            dirToDest.y = 0f;
            float dist = dirToDest.magnitude;

            if (dist <= 0.8f || _stateTimer <= 0f)
            {
                _currentState = EnemyState.Idle;
                _stateTimer = Random.Range(idleDurationMin, idleDurationMax);
                _velocity.x = 0f;
                _velocity.z = 0f;
                return;
            }

            dirToDest.Normalize();

            // Engelden kaçınma
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayOrigin, dirToDest, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                dirToDest = Quaternion.Euler(0f, 45f, 0f) * dirToDest;
            }

            LookAtPosition(transform.position + dirToDest);

            Vector3 move = dirToDest * patrolSpeed;
            _velocity.x = move.x;
            _velocity.z = move.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void PickNewPatrolDestination()
        {
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _spawnPosition + new Vector3(circle.x, 0f, circle.y);
            _currentState = EnemyState.Patrol;
            _stateTimer = Random.Range(4.0f, 7.0f);
        }

        private void StartChase(bool alertAllies)
        {
            _currentState = EnemyState.Chase;
            _loseSightTimer = loseSightDuration;
            if (_playerTransform != null)
            {
                _lastKnownPlayerPos = _playerTransform.position;
                LookAtPosition(_playerTransform.position);
            }

            if (alertAllies)
            {
                AlertNearbyAllies();
            }
        }

        private void ExecuteChase(float distToPlayer)
        {
            bool canSee = CanSeePlayer();
            if (canSee)
            {
                _loseSightTimer = loseSightDuration;
                _lastKnownPlayerPos = _playerTransform.position;
                LookAtPosition(_playerTransform.position);
            }
            else
            {
                _loseSightTimer -= Time.deltaTime;
                LookAtPosition(_lastKnownPlayerPos);

                // Oyuncu görüşten çok uzun süre kaybolduysa veya aşırı uzaklaştıysa takibi bırak
                if (_loseSightTimer <= 0f || distToPlayer > loseSightDistance)
                {
                    _currentState = EnemyState.Idle;
                    _stateTimer = Random.Range(idleDurationMin, idleDurationMax);
                    _velocity.x = 0f;
                    _velocity.z = 0f;
                    return;
                }
            }

            // Saldırı mesafesine girdiğinde ve bekleme süresi dolmuşsa saldırı hazırlığı
            if (distToPlayer <= attackRange && _cooldownTimer <= 0f)
            {
                StartWindup();
            }
            else
            {
                Vector3 target = canSee ? _playerTransform.position : _lastKnownPlayerPos;
                MoveTowards(target, chaseSpeed);
            }
        }

        private static AudioClip GetOrCreateGlintClip()
        {
            if (_cachedGlintClip != null) return _cachedGlintClip;

            int sampleRate = 44100;
            float duration = 0.16f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 18f);

                // Yüksek frekanslı kristal kılıç parıltısı: 1950 Hz ve 3900 Hz
                float tone1 = Mathf.Sin(2f * Mathf.PI * 1950f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 3900f * t) * 0.5f;

                samples[i] = Mathf.Clamp((tone1 + tone2) * envelope * 0.85f, -1f, 1f);
            }

            _cachedGlintClip = AudioClip.Create("Synthesized_GlintChime", sampleCount, 1, sampleRate, false);
            _cachedGlintClip.SetData(samples, 0);
            return _cachedGlintClip;
        }

        private void ExecuteWindup()
        {
            if (_playerTransform != null)
                LookAtPosition(_playerTransform.position);

            _stateTimer -= Time.deltaTime;

            // Vurmadan 0.20s önce: KRİTİK PARRY UYARI PARILTISI (Sekiro / Ultrakill Tarzı)
            if (_stateTimer <= parryGlintTiming && !_glintTriggered)
            {
                _glintTriggered = true;
                Vector3 eyePos = transform.position + Vector3.up * 1.70f + transform.forward * 0.35f;

                // 1. Gözlerde altın sarısı parry yıldızı flaşı
                ParryVFX.SpawnGlint(eyePos);

                // 2. Kristal kılıç çınlaması sesi (SHING!)
                AudioClip chime = customGlintSound != null ? customGlintSound : GetOrCreateGlintClip();
                if (chime != null)
                {
                    AudioSource.PlayClipAtPoint(chime, eyePos, 1.0f);
                }

                // 3. Gövde uyarısı: Parlak beyaz-sarı flaş
                SetColor(glintColor);

                Debug.Log($"<color=yellow><b>[PARRY TELEGRAPH / GLINT!]</b></color> {gameObject.name} is about to strike — PARRY NOW!");
            }

            if (_stateTimer <= 0f)
            {
                ExecuteAttack();
            }
        }

        private void ExecuteAttackState()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                _targetVisualTilt = 0f;
                _currentState = EnemyState.Chase;
            }
        }

        private void ExecuteStagger()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                _targetVisualTilt = 0f;
                ClearFlash();
                _currentState = EnemyState.Chase;
            }
        }

        private void MoveTowards(Vector3 targetPos, float speed)
        {
            Vector3 dir = (targetPos - transform.position);
            dir.y = 0f;
            dir.Normalize();

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(rayOrigin, dir, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                dir = Quaternion.Euler(0f, 45f, 0f) * dir;
            }

            Vector3 move = dir * speed;
            _velocity.x = move.x;
            _velocity.z = move.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void LookAtPosition(Vector3 targetPos)
        {
            targetPos.y = transform.position.y;
            Vector3 dir = (targetPos - transform.position).normalized;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(_visualTilt, 0f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        public bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;

            // Göz seviyesi
            Vector3 eyePos = transform.position + transform.forward * 0.35f + Vector3.up * 1.70f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.10f;
            Vector3 toPlayer = targetPos - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > detectionRange) return false;

            // 1. Çok yakın mesafe algısı (arkadan yaklaşma)
            if (dist <= proximityRange)
            {
                if (Physics.Raycast(eyePos, toPlayer.normalized, out RaycastHit proxHit, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (proxHit.transform == _playerTransform || proxHit.transform.IsChildOf(_playerTransform))
                    {
                        return true;
                    }
                }
            }

            // 2. Görüş konisi (FOV) kontrolü
            Vector3 dirToPlayer = toPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle > fieldOfView * 0.5f) return false;

            // 3. Line of Sight (Doğrudan görüş hattı) Raycast'i
            if (Physics.Raycast(eyePos, dirToPlayer, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == _playerTransform || hit.transform.IsChildOf(_playerTransform))
                {
                    return true;
                }
            }

            return false;
        }

        public void AlertNearbyAllies()
        {
            EnemyAlertHub.BroadcastAlert(this, transform.position, _playerTransform, alertRadius);
        }

        public void ReceiveAlert(Transform target)
        {
            // Sadece boşta bekleyen (Idle) veya devriye gezen (Patrol) müttefikler uyarılır
            if (_currentState == EnemyState.Idle || _currentState == EnemyState.Patrol)
            {
                _playerTransform = target;
                StartChase(false);
                if (_playerTransform != null)
                {
                    LookAtPosition(_playerTransform.position);
                }
                Debug.Log($"<color=yellow>[Pack Alert]</color> <b>{gameObject.name}</b> alerted by ally!");
            }
        }

        private void StartWindup()
        {
            _currentState = EnemyState.Windup;
            _stateTimer = windupDuration;
            _velocity = Vector3.zero;
            _glintTriggered = false;
            _targetVisualTilt = -16f; // Vuruşa hazırlanırken geriye çekilme (Pullback)

            SetColor(windupGlowColor);
        }

        private void ExecuteAttack()
        {
            _currentState = EnemyState.Attack;
            _stateTimer = 0.25f;
            _cooldownTimer = attackCooldown;
            _targetVisualTilt = 22f; // Sert ileri hamle / vuruş duruşu
            _glintTriggered = false;
            ClearFlash();

            AlertNearbyAllies();

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist <= attackRange + 0.6f && _playerHealth != null)
            {
                Vector3 hitPoint = _playerTransform.position + Vector3.up * 1.0f;
                _playerHealth.TakeDamage(attackDamage, hitPoint, (transform.position - _playerTransform.position).normalized, gameObject);
            }

            Vector3 lunge = transform.forward * 3.5f;
            _controller.Move(lunge * Time.deltaTime);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (_currentState == EnemyState.Dead) return;

            _currentHealth -= amount;
            _flashTimer = isCritical ? 0.14f : 0.08f;
            _squashTimer = 0.18f;
            SetColor(isCritical ? critFlashColor : damageFlashColor);

            // Hasar alındığında oyuncuya kilitlen ve yakındaki müttefikleri savaşa çağır!
            StartChase(true);

            // Ağır hasar veya kafa vuruşu tökezletme (Stagger) tetikler
            if (isCritical || amount >= 40f)
            {
                _currentState = EnemyState.Stagger;
                _stateTimer = 0.35f;
                _velocity = -hitNormal * 3.5f; // Geriye itilme
            }

            // Kritik darbede veya ölümde mikro Hitstop
            if (HitstopManager.Instance != null)
            {
                if (_currentHealth <= 0f)
                    HitstopManager.Instance.TriggerHitstop(0.07f, 0.02f);
                else if (isCritical)
                    HitstopManager.Instance.TriggerHitstop(0.04f, 0.04f);
            }

            Debug.Log($"<color=orange>[Enemy Hit]</color> -{amount:F0} HP | Remaining: <b>{Mathf.Max(0, _currentHealth):F0}/{maxHealth}</b> {(isCritical ? "<color=yellow>CRIT!</color>" : "")}");

            if (_currentHealth <= 0f)
            {
                Die(hitPoint, hitNormal, amount, isCritical);
            }
            else
            {
                // Canlıyken darbe anında kan püskürt ve çevreye kan sıçrat
                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, isCritical, true);
                }

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayFleshImpact(hitPoint, isCritical);
                }
            }
        }

        public void Stun(float duration, Vector3 knockbackDir)
        {
            if (_currentState == EnemyState.Dead) return;

            _currentState = EnemyState.Stagger;
            _stateTimer = duration;
            _cooldownTimer = duration + 0.5f;
            _velocity = knockbackDir.normalized * 5.5f;
            _targetVisualTilt = -25f; // Sersemleme duruşu (geriye yığılma)

            SetColor(new Color(1f, 0.9f, 0.2f));

            // Baş üzerinde dönen altın sersemleme yıldızları efekti
            ParryVFX.SpawnStunEffect(transform, duration);

            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
            }

            Debug.Log($"<color=yellow><b>[ENEMY STUNNED!]</b></color> {gameObject.name} immobilized for {duration:F1}s!");
        }

        private void Die(Vector3 hitPoint, Vector3 hitNormal, float killingDamage, bool isCritical)
        {
            _currentState = EnemyState.Dead;
            _stateTimer = respawnTime;
            _squashTimer = 0f;
            _targetVisualTilt = 0f;
            _visualTilt = 0f;
            ClearFlash();

            OnEnemyDeath?.Invoke(this);

            // 1. Çarpışma kutularını devre dışı bırak (Yerde takılma ve hayalet mermi bloklama yok)
            SetCollidersActive(false);

            // 2. Kafa Kopması (Decapitation) veya Parçalanma (Gibs) Kontrolü
            bool shouldGib = killingDamage >= 50f || _currentHealth <= -25f || (isCritical && killingDamage >= 35f);
            bool isHeadHit = hitPoint.y >= transform.position.y + 1.45f;
            bool shouldDecapitate = !shouldGib && (isCritical || isHeadHit);

            Vector3 blastDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : transform.forward;

            if (shouldGib)
            {
                _isGibbed = true;

                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnFleshGibs(transform.position + Vector3.up * 1.0f, blastDir, 7);
                }

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayGibExplosion(transform.position);
                }

                // Modeli anında gizle, ölçeği orijinal bırak (Yerde yamuk model kalmaz)
                SetRenderersActive(false);
                transform.localScale = _initialScale;

                Debug.Log("<color=red><b>[ENEMY GIBBED!]</b></color> Boomer shooter gore and bone fragments scattered!");
            }
            else if (shouldDecapitate)
            {
                _isGibbed = false;
                _deathAnimTimer = 0f;
                _deathStartRot = transform.rotation;
                _deathStartPos = transform.position;

                // Kafayı gizle ve uçan kafa / boyun kan fıskiyesi başlat
                if (_headTransform == null) _headTransform = transform.Find("Head");
                if (_headTransform != null)
                {
                    _headTransform.gameObject.SetActive(false);
                    if (CombatVFXManager.Instance != null)
                    {
                        CombatVFXManager.Instance.SpawnSeveredHead(_headTransform.position, blastDir);
                    }
                }

                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnNeckBloodFountain(transform.position + Vector3.up * 1.55f);
                }

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayDecapitation(transform.position);
                }

                // Dizüstü çökme ve arkaya yığılma rotasyonu
                Vector3 knockbackDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : -transform.forward;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude < 0.01f) knockbackDir = -transform.forward;
                knockbackDir.Normalize();

                _deathTargetRot = Quaternion.LookRotation(knockbackDir) * Quaternion.Euler(60f, 0f, 0f);
                _deathTargetPos = transform.position + knockbackDir * 0.35f - Vector3.up * 0.40f;

                Debug.Log("<color=red><b>[DECAPITATION!]</b></color> Blood gushes from the neck, head rolls across the floor!");
            }
            else
            {
                _isGibbed = false;
                _deathAnimTimer = 0f;
                _deathStartRot = transform.rotation;
                _deathStartPos = transform.position;

                Vector3 knockbackDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : -transform.forward;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude < 0.01f) knockbackDir = -transform.forward;
                knockbackDir.Normalize();

                // Yere yatma açısı ve zemin teması
                _deathTargetRot = Quaternion.LookRotation(knockbackDir) * Quaternion.Euler(75f, 0f, 0f);
                _deathTargetPos = transform.position + knockbackDir * 0.45f - Vector3.up * 0.35f;

                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(transform.position + Vector3.up * 0.9f, hitNormal, false, true);
                }

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayFleshImpact(transform.position, false);
                }

                Debug.Log("<color=red><b>[ENEMY DOWN]</b></color> Occult dissolution begun.");
            }
        }

        private void Respawn()
        {
            if (_controller != null) _controller.enabled = false;
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            if (_controller != null) _controller.enabled = true;

            transform.localScale = _initialScale;
            _squashTimer = 0f;
            _deathAnimTimer = 0f;
            _isGibbed = false;
            _targetVisualTilt = 0f;
            _visualTilt = 0f;

            if (_headTransform == null) _headTransform = transform.Find("Head");
            if (_headTransform != null) _headTransform.gameObject.SetActive(true);

            _currentHealth = maxHealth;
            _currentState = EnemyState.Idle;
            _velocity = Vector3.zero;
            ClearFlash();

            SetRenderersActive(true);
            SetCollidersActive(true);
            _respawnPopTimer = 0.22f;

            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnHitImpact(transform.position + Vector3.up * 1f, Vector3.up, false, false);
            }

            Debug.Log("<color=green>[Enemy Respawned]</color> Ready for combat!");
        }

        /// <summary>
        /// WaveSpawnDirector tarafından havuzdan çağrıldığında düşmanı hazır konuma getirir.
        /// </summary>
        public void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            autoRespawn = false;
            _spawnPosition = spawnPosition;
            _spawnRotation = spawnRotation;

            if (_controller != null) _controller.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            if (_controller != null) _controller.enabled = true;

            if (_playerTransform == null)
            {
                FindPlayer();
            }

            gameObject.SetActive(true);
            Respawn();
            StartChase(true);
        }

        /// <summary>
        /// WaveSpawnDirector tarafından dalga bittiğinde veya havuz temizliğinde çağrılır.
        /// </summary>
        public void OnDespawnToDirector()
        {
            autoRespawn = false;
            _currentState = EnemyState.Dead;
            gameObject.SetActive(false);
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }
            else
            {
                _velocity.y -= gravity * Time.deltaTime;
            }
        }

        private void SetColor(Color col)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, col);
                _propBlock.SetColor(ColorId, col);
                _renderers[i].SetPropertyBlock(_propBlock);
            }
        }

        private void ClearFlash()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].SetPropertyBlock(null);
            }
        }

        #region Fallback / Eksik Materyal Onarımı (Anti-Purple Defense)

        private static Material _sharedFallbackSkin;
        private static Material _sharedFallbackArmor;
        private static Material _sharedFallbackClaw;
        private static Material _sharedFallbackEye;

        /// <summary>
        /// Model üzerindeki parçaların materyallerini denetler. Eksik veya mor hata shader'ı (Missing Material)
        /// tespit edilirse otomatik olarak URP Dark Fantasy materyali ile anında onarır.
        /// </summary>
        public void ValidateAndRepairMaterials()
        {
            EnsureLimbsExist();

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                Material m = r.sharedMaterial;
                bool isBroken = m == null || m.shader == null || m.shader.name.Contains("InternalError") || m.shader.name.Contains("Error");

                if (isBroken)
                {
                    string n = r.gameObject.name;
                    if (n.Contains("Eye"))
                    {
                        if (_sharedFallbackEye == null)
                            _sharedFallbackEye = CreateFallbackMaterial("Fallback_Ghoul_Eye", new Color(1.0f, 0.20f, 0.05f), true);
                        r.material = _sharedFallbackEye;
                    }
                    else if (n.Contains("Armor") || n.Contains("Chest") || n.Contains("Shoulder"))
                    {
                        if (_sharedFallbackArmor == null)
                            _sharedFallbackArmor = CreateFallbackMaterial("Fallback_Ghoul_Armor", new Color(0.18f, 0.14f, 0.14f), false, 0.25f, 0.5f);
                        r.material = _sharedFallbackArmor;
                    }
                    else if (n.Contains("Claw") || n.Contains("Spike"))
                    {
                        if (_sharedFallbackClaw == null)
                            _sharedFallbackClaw = CreateFallbackMaterial("Fallback_Ghoul_Claw", new Color(0.40f, 0.10f, 0.10f), false, 0.30f);
                        r.material = _sharedFallbackClaw;
                    }
                    else
                    {
                        if (_sharedFallbackSkin == null)
                            _sharedFallbackSkin = CreateFallbackMaterial("Fallback_Ghoul_Skin", new Color(0.14f, 0.15f, 0.16f), false, 0.15f);
                        r.material = _sharedFallbackSkin;
                    }
                }
            }
        }

        /// <summary>
        /// Ghoul modelinin kollarını, bacaklarını, pençelerini ve omuzlarını denetler;
        /// eksik uzuv tespit edilirse anında 3D parçaları tamamlar.
        /// </summary>
        public void EnsureLimbsExist()
        {
            bool needRefresh = false;

            Transform head = transform.Find("Head");
            if (head != null)
            {
                if (head.Find("Eye_Left") == null)
                {
                    CreatePrimitivePart(head, PrimitiveType.Sphere, "Eye_Left", new Vector3(-0.10f, 0.05f, 0.21f), Quaternion.identity, new Vector3(0.08f, 0.08f, 0.08f), _sharedFallbackEye ?? GetOrCreateFallbackEye());
                    needRefresh = true;
                }
                if (head.Find("Eye_Right") == null)
                {
                    CreatePrimitivePart(head, PrimitiveType.Sphere, "Eye_Right", new Vector3(0.10f, 0.05f, 0.21f), Quaternion.identity, new Vector3(0.08f, 0.08f, 0.08f), _sharedFallbackEye ?? GetOrCreateFallbackEye());
                    needRefresh = true;
                }
            }

            if (transform.Find("Spine_Spike") == null)
            {
                CreatePrimitivePart(transform, PrimitiveType.Cube, "Spine_Spike", new Vector3(0f, 1.38f, -0.22f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(0.10f, 0.45f, 0.12f), _sharedFallbackClaw ?? GetOrCreateFallbackClaw());
                needRefresh = true;
            }

            if (transform.Find("Shoulder_L") == null)
            {
                CreatePrimitivePart(transform, PrimitiveType.Cube, "Shoulder_L", new Vector3(-0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, 22f), new Vector3(0.24f, 0.26f, 0.32f), _sharedFallbackArmor ?? GetOrCreateFallbackArmor());
                needRefresh = true;
            }
            if (transform.Find("Shoulder_R") == null)
            {
                CreatePrimitivePart(transform, PrimitiveType.Cube, "Shoulder_R", new Vector3(0.46f, 1.50f, 0.08f), Quaternion.Euler(0f, 0f, -22f), new Vector3(0.24f, 0.26f, 0.32f), _sharedFallbackArmor ?? GetOrCreateFallbackArmor());
                needRefresh = true;
            }

            if (transform.Find("Arm_L") == null)
            {
                CreateArmHierarchy(transform, "Arm_L", new Vector3(-0.48f, 1.15f, 0.12f), -1);
                needRefresh = true;
            }
            if (transform.Find("Arm_R") == null)
            {
                CreateArmHierarchy(transform, "Arm_R", new Vector3(0.48f, 1.15f, 0.12f), 1);
                needRefresh = true;
            }

            if (transform.Find("Leg_L") == null)
            {
                CreatePrimitivePart(transform, PrimitiveType.Cylinder, "Leg_L", new Vector3(-0.20f, 0.36f, 0f), Quaternion.identity, new Vector3(0.16f, 0.36f, 0.16f), _sharedFallbackSkin ?? GetOrCreateFallbackSkin());
                needRefresh = true;
            }
            if (transform.Find("Leg_R") == null)
            {
                CreatePrimitivePart(transform, PrimitiveType.Cylinder, "Leg_R", new Vector3(0.20f, 0.36f, 0f), Quaternion.identity, new Vector3(0.16f, 0.36f, 0.16f), _sharedFallbackSkin ?? GetOrCreateFallbackSkin());
                needRefresh = true;
            }

            if (needRefresh)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private static GameObject CreatePrimitivePart(Transform parent, PrimitiveType type, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = pos;
            obj.transform.localRotation = rot;
            obj.transform.localScale = scale;
            if (mat != null) obj.GetComponent<Renderer>().material = mat;
            Collider c = obj.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
            return obj;
        }

        private static void CreateArmHierarchy(Transform parent, string armName, Vector3 pos, int side)
        {
            GameObject armRoot = new GameObject(armName);
            armRoot.transform.SetParent(parent);
            armRoot.transform.localPosition = pos;
            armRoot.transform.localRotation = Quaternion.identity;

            Material skin = _sharedFallbackSkin ?? GetOrCreateFallbackSkin();
            Material claw = _sharedFallbackClaw ?? GetOrCreateFallbackClaw();

            CreatePrimitivePart(armRoot.transform, PrimitiveType.Cube, "UpperArm", new Vector3(0f, -0.15f, 0.05f), Quaternion.Euler(22f, 0f, side * 8f), new Vector3(0.14f, 0.38f, 0.14f), skin);
            CreatePrimitivePart(armRoot.transform, PrimitiveType.Cube, "ClawBlade", new Vector3(0f, -0.42f, 0.22f), Quaternion.Euler(42f, 0f, 0f), new Vector3(0.12f, 0.44f, 0.09f), claw);
        }

        private static Material GetOrCreateFallbackSkin() => _sharedFallbackSkin ?? (_sharedFallbackSkin = CreateFallbackMaterial("Fallback_Ghoul_Skin", new Color(0.14f, 0.15f, 0.16f), false, 0.15f));
        private static Material GetOrCreateFallbackArmor() => _sharedFallbackArmor ?? (_sharedFallbackArmor = CreateFallbackMaterial("Fallback_Ghoul_Armor", new Color(0.18f, 0.14f, 0.14f), false, 0.25f, 0.5f));
        private static Material GetOrCreateFallbackClaw() => _sharedFallbackClaw ?? (_sharedFallbackClaw = CreateFallbackMaterial("Fallback_Ghoul_Claw", new Color(0.40f, 0.10f, 0.10f), false, 0.30f));
        private static Material GetOrCreateFallbackEye() => _sharedFallbackEye ?? (_sharedFallbackEye = CreateFallbackMaterial("Fallback_Ghoul_Eye", new Color(1.0f, 0.20f, 0.05f), true));

        private static Material CreateFallbackMaterial(string matName, Color color, bool unlit = false, float smoothness = 0.15f, float metallic = 0f)
        {
            Shader shader = unlit
                ? Shader.Find("Universal Render Pipeline/Unlit")
                : Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = matName,
                hideFlags = HideFlags.DontSave
            };

            mat.color = color;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(Shader.PropertyToID("_Smoothness"))) mat.SetFloat(Shader.PropertyToID("_Smoothness"), smoothness);
            if (mat.HasProperty(Shader.PropertyToID("_Metallic"))) mat.SetFloat(Shader.PropertyToID("_Metallic"), metallic);
            return mat;
        }

        #endregion
    }

    /// <summary>
    /// Geriye dönük uyumluluk için Hitbox türevi.
    /// </summary>
    public class EnemyHitbox : Hitbox
    {
    }
}
