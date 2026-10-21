using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS
{
    public enum FlyingEyeState
    {
        Idle,
        Patrol,
        Combat,
        Windup,
        Stagger,
        Dead
    }

    /// <summary>
    /// Uçan Göz (Occult Watcher / Beholder) Düşman Yapay Zekası.
    /// - Havada asılı kalır ve süzülerek devriye gezer (Hover & Bobbing).
    /// - Görüş Açısı (FOV & LoS): Sadece oyuncuyu gördüğünde saldırıya geçer.
    /// - İdeal Mesafeyi Koruma (Kiting): Oyuncuya fazla yaklaşmaz, 7-10m mesafeden okült enerji topları fırlatır.
    /// - Sürü Bilinci (EnemyAlertHub): Diğer düşmanlarla koordineli çalışır; birisi saldırınca yakındakiler uyarılır.
    /// - Gözbebeği Kritik Vuruş Noktasıdır (2x Headshot / Crit).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FlyingEyeAI : MonoBehaviour, IDamageable, IAlertableEnemy, IStunnable
    {
        [Header("Sağlık ve Ödül")]
        [SerializeField] private float maxHealth = 90f; // Havada olduğu için biraz daha kırılgan
        [SerializeField] private float respawnTime = 4.0f;

        [Header("Uçuş ve Hareket")]
        [SerializeField] private float flySpeed = 3.8f;
        [SerializeField] private float rotationSpeed = 8.0f;
        [SerializeField] private float hoverHeight = 2.6f; // Zeminden ideal uçuş yüksekliği
        [SerializeField] private float maxFlyHeight = 3.6f; // Asla 3.6m üzerine çıkamaz
        [SerializeField] private float minFlyHeight = 1.8f; // Asla 1.8m altına inemez
        [SerializeField] private float bobAmplitude = 0.22f; // Havada hafif süzülme dalgalanması
        [SerializeField] private float bobFrequency = 2.0f;
        [SerializeField] private float preferredDistance = 9.5f; // Oyuncuyla tutmak istediği ideal mesafe
        [SerializeField] private float minDistance = 5.0f; // Çok yaklaşılırsa geri çekilme mesafesi

        [Header("Görüş ve Algılama")]
        [SerializeField] private float detectionRange = 22f;
        [SerializeField] private float fieldOfView = 125f; // Geniş görüş açısı
        [SerializeField] private float proximityRange = 3.5f;
        [SerializeField] private float loseSightDistance = 26f;
        [SerializeField] private float loseSightDuration = 4.0f;

        [Header("Menzilli Saldırı")]
        [SerializeField] private float attackRange = 20f;
        [SerializeField] private float projectileSpeed = 13.5f;
        [SerializeField] private float projectileDamage = 18f;
        [SerializeField] private float attackWindup = 0.50f; // Ateş etmeden önce gözün parlayıp şarj olma süresi
        [SerializeField] private float attackCooldown = 2.2f;
        [SerializeField] private Transform firePoint;

        [Header("Devriye ve Sürü")]
        [SerializeField] private float patrolRadius = 7.0f;
        [SerializeField] private float alertRadius = 12.0f;

        [Header("Görsel Efekt Renkleri")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color windupGlowColor = new Color(1.0f, 0.15f, 0.9f); // Parlayan büyü moru
        [SerializeField] private Color damageFlashColor = new Color(1.0f, 0.2f, 0.2f);
        [SerializeField] private Color critFlashColor = new Color(1.0f, 0.9f, 0.1f);

        private CharacterController _controller;
        private Transform _playerTransform;
        private PlayerHealth _playerHealth;

        private FlyingEyeState _currentState = FlyingEyeState.Idle;
        private float _currentHealth;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _initialScale;
        private Vector3 _velocity;

        private Vector3 _patrolTarget;
        private Vector3 _lastKnownPlayerPos;
        private float _loseSightTimer = 0f;
        private float _strafeDirection = 1f;
        private float _strafeTimer = 0f;

        private float _stateTimer = 0f;
        private float _cooldownTimer = 0f;
        private float _flashTimer = 0f;
        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public FlyingEyeState CurrentState => _currentState;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public Vector3 Position => transform.position;
        public bool IsDead => _currentState == FlyingEyeState.Dead;

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
            _propBlock = new MaterialPropertyBlock();

            FindPlayer();
            SetupHitboxes();

            _currentState = FlyingEyeState.Idle;
            _stateTimer = Random.Range(1.0f, 2.5f);
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

                // Gözbebeği veya kafa parçası kritik headshot noktasıdır (Sıfır GC string araması)
                string colName = col.gameObject.name;
                bool isCritZone = colName.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  colName.IndexOf("pupil", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  colName.IndexOf("eye", System.StringComparison.OrdinalIgnoreCase) >= 0;

                Hitbox hb = col.GetComponent<Hitbox>();
                if (hb == null)
                {
                    hb = col.gameObject.AddComponent<Hitbox>();
                }
                hb.Initialize(this, isCritZone ? 2.0f : 1.0f, isCritZone);
            }
        }

        public void SetFirePoint(Transform fp)
        {
            firePoint = fp;
        }

        private void Update()
        {
            if (_currentState == FlyingEyeState.Dead)
            {
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    Respawn();
                }
                return;
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            UpdateTimers();
            ExecuteStateMachine();
            ApplyHoverAltitude();
        }

        private void UpdateTimers()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _currentState != FlyingEyeState.Windup)
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
                case FlyingEyeState.Idle:
                    ExecuteIdle();
                    break;

                case FlyingEyeState.Patrol:
                    ExecutePatrol();
                    break;

                case FlyingEyeState.Combat:
                    ExecuteCombat(distToPlayer);
                    break;

                case FlyingEyeState.Windup:
                    ExecuteWindup();
                    break;

                case FlyingEyeState.Stagger:
                    ExecuteStagger();
                    break;
            }
        }

        private void ExecuteIdle()
        {
            _velocity.x = 0f;
            _velocity.z = 0f;

            if (CanSeePlayer())
            {
                StartCombat(true);
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
            if (CanSeePlayer())
            {
                StartCombat(true);
                return;
            }

            _stateTimer -= Time.deltaTime;

            Vector3 dirToDest = _patrolTarget - transform.position;
            dirToDest.y = 0f;
            float dist = dirToDest.magnitude;

            if (dist <= 1.0f || _stateTimer <= 0f)
            {
                _currentState = FlyingEyeState.Idle;
                _stateTimer = Random.Range(1.5f, 3.0f);
                _velocity.x = 0f;
                _velocity.z = 0f;
                return;
            }

            dirToDest.Normalize();
            LookAtPosition(transform.position + dirToDest);

            Vector3 move = dirToDest * (flySpeed * 0.6f);
            _velocity.x = move.x;
            _velocity.z = move.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void PickNewPatrolDestination()
        {
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _spawnPosition + new Vector3(circle.x, 0f, circle.y);
            _currentState = FlyingEyeState.Patrol;
            _stateTimer = Random.Range(4.0f, 7.0f);
        }

        private void StartCombat(bool alertAllies)
        {
            _currentState = FlyingEyeState.Combat;
            _loseSightTimer = loseSightDuration;
            if (_playerTransform != null)
            {
                _lastKnownPlayerPos = _playerTransform.position;
                LookAtPosition(_playerTransform.position);
            }

            if (alertAllies)
            {
                EnemyAlertHub.BroadcastAlert(this, transform.position, _playerTransform, alertRadius);
            }
        }

        private void ExecuteCombat(float distToPlayer)
        {
            bool canSee = CanSeePlayer();
            if (canSee)
            {
                _loseSightTimer = loseSightDuration;
                _lastKnownPlayerPos = _playerTransform.position;
            }
            else
            {
                _loseSightTimer -= Time.deltaTime;
                if (_loseSightTimer <= 0f || distToPlayer > loseSightDistance)
                {
                    _currentState = FlyingEyeState.Idle;
                    _stateTimer = 2.0f;
                    _velocity.x = 0f;
                    _velocity.z = 0f;
                    return;
                }
            }

            Vector3 aimTarget = canSee ? _playerTransform.position + Vector3.up * 1.0f : _lastKnownPlayerPos;
            LookAtPosition(aimTarget);

            // 1. Saldırı Hazırlığı Kontrolü
            if (canSee && distToPlayer <= attackRange && _cooldownTimer <= 0f)
            {
                StartWindup();
                return;
            }

            // 2. Menzil ve Konumlandırma (Kiting)
            Vector3 toPlayerFlat = _playerTransform.position - transform.position;
            toPlayerFlat.y = 0f;
            Vector3 dirToPlayer = toPlayerFlat.normalized;

            Vector3 moveDir = Vector3.zero;

            // Çok yakınsa geri çekil
            if (distToPlayer < minDistance)
            {
                moveDir = -dirToPlayer * (flySpeed * 0.9f);
            }
            // Çok uzaksa yaklaş
            else if (distToPlayer > preferredDistance + 2.5f)
            {
                moveDir = dirToPlayer * flySpeed;
            }
            // İdeal mesafedeyse hafifçe yanlara süzül (strafe)
            else
            {
                _strafeTimer -= Time.deltaTime;
                if (_strafeTimer <= 0f)
                {
                    _strafeDirection = Random.value > 0.5f ? 1f : -1f;
                    _strafeTimer = Random.Range(1.2f, 2.4f);
                }

                Vector3 strafeVector = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
                moveDir = strafeVector * (flySpeed * 0.45f);
            }

            _velocity.x = moveDir.x;
            _velocity.z = moveDir.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void StartWindup()
        {
            _currentState = FlyingEyeState.Windup;
            _stateTimer = attackWindup;
            _velocity.x = 0f;
            _velocity.z = 0f;

            // Görsel şarj telgrafı: Göz parıldamaya başlar
            SetColor(windupGlowColor);
        }

        private void ExecuteWindup()
        {
            if (_playerTransform != null)
            {
                Vector3 aimPos = _playerTransform.position + Vector3.up * 1.0f;
                LookAtPosition(aimPos);
            }

            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                LaunchProjectile();
            }
        }

        private void LaunchProjectile()
        {
            _currentState = FlyingEyeState.Combat;
            _cooldownTimer = attackCooldown;
            ClearFlash();

            EnemyAlertHub.BroadcastAlert(this, transform.position, _playerTransform, alertRadius);

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 0.6f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.0f;
            Vector3 shootDir = (targetPos - spawnPos).normalized;

            // Okült Plazma Mermisi Yarat
            GameObject projGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projGO.name = "OccultEyeOrb";
            projGO.transform.position = spawnPos;
            projGO.transform.rotation = Quaternion.LookRotation(shootDir);
            projGO.transform.localScale = new Vector3(0.40f, 0.40f, 0.40f);

            // Mermi Materyali: Parlayan Okült Mor
            Material orbMat = CreateEmissiveMaterial("Proj_OccultPurple", new Color(0.85f, 0.15f, 0.95f));
            projGO.GetComponent<Renderer>().material = orbMat;

            // Merminin collider'ını trigger yap (fiziksel engelleme yapmasın ama raycast/overlap sorguları bulabilsin)
            Collider col = projGO.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            EnemyProjectile proj = projGO.AddComponent<EnemyProjectile>();
            proj.Initialize(transform, projectileSpeed, projectileDamage);

            Debug.Log("<color=magenta><b>[UÇAN GÖZ SALDIRISI]</b></color> Okült plazma mermisi ateşlendi!");
        }

        private void ExecuteStagger()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                ClearFlash();
                _currentState = FlyingEyeState.Combat;
            }
        }

        private void ApplyHoverAltitude()
        {
            float groundY = 0f;
            // Gözün kendi collider'ına çarpmaması için gözün altından (Y - 0.65m) aşağıya tara
            Vector3 rayOrigin = transform.position - Vector3.up * 0.65f;
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _groundHits, 25f, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                // Kendisine veya kendi çocuk parçalarına çarpışmayı yok say
                if (_groundHits[i].transform == transform || _groundHits[i].transform.IsChildOf(transform))
                    continue;

                groundY = _groundHits[i].point.y;
                break;
            }

            // Hedef Y yüksekliği: Zemin + hoverHeight + dalgalanma (Kesinlikle maxFlyHeight ve minFlyHeight ile sınırlandırılmış)
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            float rawTargetY = groundY + hoverHeight + bob;
            float targetY = Mathf.Clamp(rawTargetY, minFlyHeight, maxFlyHeight);

            float currentY = transform.position.y;
            float newY = Mathf.MoveTowards(currentY, targetY, flySpeed * Time.deltaTime * 2.0f);
            newY = Mathf.Clamp(newY, minFlyHeight, maxFlyHeight);

            float deltaY = newY - currentY;
            _controller.Move(new Vector3(0f, deltaY, 0f));
        }

        private void LookAtPosition(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        public bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;

            Vector3 eyePos = transform.position + transform.forward * 0.5f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.1f;
            Vector3 toPlayer = targetPos - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > detectionRange) return false;

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

            Vector3 dirToPlayer = toPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > fieldOfView * 0.5f) return false;

            if (Physics.Raycast(eyePos, dirToPlayer, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == _playerTransform || hit.transform.IsChildOf(_playerTransform))
                {
                    return true;
                }
            }

            return false;
        }

        public void ReceiveAlert(Transform target)
        {
            if (_currentState == FlyingEyeState.Idle || _currentState == FlyingEyeState.Patrol)
            {
                _playerTransform = target;
                StartCombat(false);
                Debug.Log($"<color=yellow>[Sürü Uyarısı]</color> <b>{gameObject.name} (Uçan Göz)</b> müttefik alarmıyla savaşa dahil oldu!");
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (_currentState == FlyingEyeState.Dead) return;

            _currentHealth -= amount;
            _flashTimer = isCritical ? 0.14f : 0.08f;
            SetColor(isCritical ? critFlashColor : damageFlashColor);

            StartCombat(true);

            // Geri tepme sarsıntısı
            _controller.Move(-hitNormal * 1.8f);

            if (isCritical || amount >= 30f)
            {
                _currentState = FlyingEyeState.Stagger;
                _stateTimer = 0.30f;
            }

            if (HitstopManager.Instance != null)
            {
                if (_currentHealth <= 0f)
                    HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
                else if (isCritical)
                    HitstopManager.Instance.TriggerHitstop(0.04f, 0.03f);
            }

            Debug.Log($"<color=magenta>[Uçan Göz Vuruldu]</color> -{amount:F0} HP | Kalan: <b>{Mathf.Max(0, _currentHealth):F0}/{maxHealth}</b> {(isCritical ? "<color=yellow>CRIT EYE-SHOT!</color>" : "")}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Stun(float duration, Vector3 knockbackDir)
        {
            if (_currentState == FlyingEyeState.Dead) return;

            _currentState = FlyingEyeState.Stagger;
            _stateTimer = duration;
            _cooldownTimer = duration + 0.6f;
            _controller.Move(knockbackDir.normalized * 3.5f);

            SetColor(new Color(1f, 0.9f, 0.2f));

            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
            }

            Debug.Log($"<color=yellow><b>[UÇAN GÖZ SERSEMLETİLDİ / STUN!]</b></color> {gameObject.name} {duration:F1}s boyunca havada felç oldu!");
        }

        private void Die()
        {
            _currentState = FlyingEyeState.Dead;
            _stateTimer = respawnTime;
            _controller.enabled = false;

            // Combat Juice: Uçan Göz parçalanma patlaması (Gibs & Explosion Sound)
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnFleshGibs(transform.position, transform.forward, 4);
            }
            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayGibExplosion(transform.position);
            }

            // Ölüm efekti: Yere düşme ve küçülme
            transform.localScale = new Vector3(_initialScale.x * 0.7f, _initialScale.y * 0.2f, _initialScale.z * 0.7f);
            ClearFlash();

            Debug.Log("<color=red><b>[UÇAN GÖZ İMHA EDİLDİ]</b></color> 4 saniye sonra havada yeniden doğacak.");
        }

        private void Respawn()
        {
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            transform.localScale = _initialScale;

            _currentHealth = maxHealth;
            _currentState = FlyingEyeState.Idle;
            _controller.enabled = true;
            _velocity = Vector3.zero;
            ClearFlash();

            Debug.Log("<color=green>[Uçan Göz Yeniden Doğdu]</color> Havada devriye için hazır!");
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

        private Material CreateEmissiveMaterial(string matName, Color col)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Unlit");
            if (uLit == null) uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.name = matName;
            mat.color = col;
            return mat;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = transform.position + transform.forward * 0.5f;
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.35f);
            Gizmos.DrawWireSphere(eyePos, detectionRange);

            Vector3 leftRay = Quaternion.Euler(0, -fieldOfView * 0.5f, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, fieldOfView * 0.5f, 0) * transform.forward;
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(eyePos, leftRay * detectionRange);
            Gizmos.DrawRay(eyePos, rightRay * detectionRange);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.3f);
            Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;
            Gizmos.DrawWireSphere(center, patrolRadius);
        }
#endif
    }

    /// <summary>
    /// Geriye dönük uyumluluk için Hitbox türevi.
    /// </summary>
    public class FlyingEyeHitbox : Hitbox
    {
    }
}
