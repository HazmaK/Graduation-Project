using System.Collections;
using RetroFPS.Pause;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Slow shield bearer whose fixed parryable area slam launches a surviving player up and down the lane.
    /// Leave the area or parry at impact; jumping does not avoid it.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PenitentAI : MonoBehaviour, IDamageable, IAlertableEnemy, IStunnable, IStunState,
        IEncounterEnemy, IThreatCost, IEliteScalable, IGuardBreakable, IMeleeGuard
    {
        [Header("References")]
        [SerializeField, Tooltip("Optional presentation driver.")] private PenitentView view;
        [SerializeField, Tooltip("Optional health dropper; resolved on this object during initialization if omitted.")]
        private EnemyHealthDropper healthDropper;
        [SerializeField, Tooltip("Optional ammo dropper; resolved on this object during initialization if omitted.")]
        private EnemyAmmoDropper ammoDropper;

        [Header("Health and movement")]
        [SerializeField, Tooltip("Maximum health before elite scaling.")] private float maxHealth = 320f;
        [SerializeField, Tooltip("Threat budget weight.")] private float threatCost = 3f;
        [SerializeField, Tooltip("Advance speed in metres per second.")] private float walkSpeed = 2.6f;
        [SerializeField, Tooltip("Advance turn rate in degrees per second.")] private float turnSpeedAdvance = 110f;
        [SerializeField, Tooltip("Downward acceleration.")] private float gravity = 22f;
        [SerializeField, Tooltip("Downward velocity maintaining ground contact.")] private float groundedVelocity = -2f;
        [SerializeField, Tooltip("Minimum target distance before movement stops.")] private float stoppingDistance = 1.2f;
        [SerializeField, Tooltip("Whether unmanaged enemies automatically respawn.")] private bool autoRespawn = true;
        [SerializeField, Tooltip("Delay before automatic respawn, or deactivation after death.")]
        private float respawnTime = 4f;

        [Header("Detection")]
        [SerializeField, Tooltip("Maximum player detection distance.")] private float sightRange = 18f;
        [SerializeField, Tooltip("Full detection cone in degrees.")] private float fieldOfView = 110f;
        [SerializeField, Tooltip("Detection distance that ignores the field of view.")] private float proximityRange = 2.8f;
        [SerializeField, Tooltip("Seconds of pursuit after losing sight.")] private float loseSightTime = 3.5f;
        [SerializeField, Tooltip("Radius within which seeing the player alerts allies.")] private float alertRadius = 10f;
        [SerializeField, Tooltip("Eye height used when no view is assigned.")] private float eyeHeight = 1.7f;
        [SerializeField, Tooltip("Height above the player's feet used for sight and melee feedback.")]
        private float playerTargetHeight = 1.1f;
        [SerializeField, Tooltip("Seconds between searches when the player is absent.")]
        private float playerSearchInterval = 0.5f;

        [Header("Shield and slam")]
        [SerializeField, Tooltip("Shield arc half-angle in degrees.")] private float guardHalfAngle = 65f;
        [SerializeField, Tooltip("Damage multiplier for precise non-melee hits on the face weak point.")]
        private float weakPointMultiplier = 2f;
        [SerializeField, Tooltip("Slam timing and initiation distance.")]
        private PenitentAttackSettings attackSettings = new PenitentAttackSettings();
        [SerializeField, Tooltip("Length of the fixed slam rectangle in metres.")] private float slamAreaLength = 6.5f;
        [SerializeField, Tooltip("Full width of the fixed slam rectangle in metres.")] private float slamAreaWidth = 4.5f;
        [SerializeField, Tooltip("Forward distance from enemy centre to the near edge.")] private float slamAreaStartOffset = 0.4f;
        [SerializeField, Tooltip("Vertical tolerance above and below the area's ground plane; jumping does not avoid the slam.")]
        private float slamAreaHeight = 2.5f;
        [SerializeField, Tooltip("Parryable area damage before elite scaling.")] private float slamDamage = 35f;
        [SerializeField, Tooltip("Upward velocity added by a landed slam; 9 m/s reaches about 1.62 m above rest at the player's 25 m/s squared gravity.")]
        private float slamLaunchUpSpeed = 9f;
        [SerializeField, Tooltip("Horizontal velocity added along the cached slam lane, in metres per second; unaffected by elite scaling.")]
        private float slamLaunchPushSpeed = 5f;
        [SerializeField, Tooltip("Heavy hitstop duration in real seconds, only when slam damage lands on the player.")]
        private float slamPlayerHitstopDuration = 0.12f;
        [SerializeField, Tooltip("Time scale during a landed slam's heavy hitstop.")]
        private float slamPlayerHitstopScale = 0.03f;
        [SerializeField, Tooltip("Additional camera shake trauma after slam damage lands.")]
        private float slamCameraTrauma = 0.3f;
        [SerializeField, Tooltip("Additional camera pitch and yaw punch in degrees after slam damage lands.")]
        private Vector2 slamCameraPunch = new Vector2(5f, 0f);
        [SerializeField, Tooltip("Additional camera field-of-view kick in degrees after slam damage lands.")]
        private float slamCameraFovKick = 6f;

        [Header("Stun and feedback")]
        [SerializeField, Tooltip("Knockback speed applied to a normalized stun direction.")]
        private float stunKnockbackSpeed = 3f;
        [SerializeField, Tooltip("Exponential knockback damping rate.")] private float knockbackDecay = 8f;
        [SerializeField, Tooltip("Damage needed to stagger while the guard is down.")]
        private float staggerDamageThreshold = 40f;
        [SerializeField, Tooltip("Seconds added to a guard break by a qualifying hit.")] private float staggerExtend = 0.35f;
        [SerializeField, Tooltip("Maximum remaining guard break after damage extensions.")]
        private float staggerMaximum = 4f;
        [SerializeField, Tooltip("Minimum seconds between shield impact feedback.")]
        private float blockFeedbackInterval = 0.08f;
        [SerializeField, Tooltip("Optional shield clang; the existing metallic latch sound is the fallback.")]
        private AudioClip blockSound;
        [SerializeField, Tooltip("Critical hitstop duration.")] private float criticalHitstopDuration = 0.04f;
        [SerializeField, Tooltip("Critical hitstop time scale.")] private float criticalHitstopScale = 0.04f;
        [SerializeField, Tooltip("Stun hitstop duration.")] private float stunHitstopDuration = 0.08f;
        [SerializeField, Tooltip("Stun hitstop time scale.")] private float stunHitstopScale = 0.02f;
        [SerializeField, Tooltip("Death hitstop duration.")] private float deathHitstopDuration = 0.07f;
        [SerializeField, Tooltip("Death hitstop time scale.")] private float deathHitstopScale = 0.03f;
        [SerializeField, Tooltip("Flesh gibs emitted on death.")] private int deathGibCount = 7;
        [SerializeField, Tooltip("Fallback reward orb count when no dropper is present.")]
        private int fallbackHealthOrbCount = 1;
        [SerializeField, Tooltip("Stun signal tint.")] private Color stunColor = new Color(1f, 0.95f, 0.25f);
        [SerializeField, Tooltip("Stun signal particle count.")] private int stunSignalCount = 6;

        private readonly RaycastHit[] _sightHits = new RaycastHit[32];
        private CharacterController _controller;
        private Collider[] _colliders;
        private PlayerHealth _playerHealth;
        private PlayerMovement _playerMovement;
        private PlayerLook _playerLook;
        private Transform _playerTransform;
        private PenitentAttackLoop _loop;
        private Coroutine _cacheRoutine;
        private Vector3 _slamAreaOrigin;
        private Vector3 _slamAreaForward;
        private bool _slamImpactResolved;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _velocity;
        private float _health;
        private float _lostSightRemaining;
        private float _playerSearchTimer;
        private float _blockFeedbackRemaining;
        private float _deathRemaining;
        private bool _alive = true;
        private bool _alerted;
        private bool _hasAttackToken;
        private bool _started;
        private bool _eliteBaseCached;
        private bool _isElite;
        private uint _stunSequence;
        private float _baseHealth;
        private float _baseSlamDamage;
        private float _baseWalkSpeed;

        /// <inheritdoc />
        public event System.Action<IEncounterEnemy> OnEnemyDeath;
        /// <inheritdoc />
        public bool IsAlive => _alive;
        /// <inheritdoc />
        public Transform EnemyTransform => transform;
        /// <inheritdoc />
        public GameObject EnemyGameObject => gameObject;
        /// <inheritdoc />
        public Vector3 Position => transform.position;
        /// <inheritdoc />
        public bool IsDead => !_alive;
        /// <inheritdoc />
        public float ThreatCost => threatCost;
        /// <inheritdoc />
        public bool IsElite => _isElite;
        /// <inheritdoc />
        public bool IsStunned => _alive && _loop != null && _loop.Phase == PenitentAttackPhase.GuardBroken;
        /// <inheritdoc />
        public uint StunSequence => _stunSequence;
        /// <summary>Whether frontal hits are currently blocked.</summary>
        public bool IsGuardUp => _alive && _loop != null && _loop.IsGuardUp;
        /// <summary>Health remaining for this spawn.</summary>
        public float CurrentHealth => _health;
        /// <summary>Maximum health including elite scaling.</summary>
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (view == null) view = GetComponent<PenitentView>();
            if (healthDropper == null) healthDropper = GetComponent<EnemyHealthDropper>();
            if (ammoDropper == null) ammoDropper = GetComponent<EnemyAmmoDropper>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _loop = new PenitentAttackLoop(attackSettings);
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _health = maxHealth;
            FindPlayer();
            CombatTargetLookup.RegisterHierarchy(this);
        }

        private void OnEnable()
        {
            EnemyAlertHub.Register(this);
            if (_started) StartCacheMaintenance();
            SynchronizeView();
        }

        private void Start()
        {
            _started = true;
            CombatTargetLookup.RegisterHierarchy(this);
            FindPlayer();
            if (view != null) view.ResetVisual();
            SynchronizeView();
            StartCacheMaintenance();
        }

        private void OnDisable()
        {
            CancelSlam();
            EnemyAlertHub.Unregister(this);
            if (_cacheRoutine != null) StopCoroutine(_cacheRoutine);
            _cacheRoutine = null;
        }

        private void OnDestroy()
        {
            ReleaseToken();
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            float dt = Time.deltaTime;
            if (!_alive)
            {
                _deathRemaining -= dt;
                if (_deathRemaining <= 0f)
                {
                    if (autoRespawn) Respawn();
                    else gameObject.SetActive(false);
                }
                return;
            }
            _blockFeedbackRemaining = Mathf.Max(0f, _blockFeedbackRemaining - dt);
            Vector3 toPlayer = _playerTransform != null ? _playerTransform.position - transform.position : Vector3.zero;
            float distance = _playerTransform != null ? toPlayer.magnitude : float.PositiveInfinity;
            bool seesPlayer = CanSeePlayer(toPlayer, distance);
            if (seesPlayer)
            {
                if (!_alerted)
                    EnemyAlertHub.BroadcastAlert(this, transform.position, _playerTransform, alertRadius);
                _alerted = true;
                _lostSightRemaining = loseSightTime;
            }
            else
            {
                _lostSightRemaining = Mathf.Max(0f, _lostSightRemaining - dt);
                if (_lostSightRemaining <= 0f) _alerted = false;
            }
            if (!HasLivingPlayer)
            {
                _alerted = false;
                _lostSightRemaining = 0f;
                if (_loop.WantsToken)
                {
                    CancelSlam();
                    SynchronizeView();
                }
            }

            Vector3 flatToPlayer = toPlayer;
            flatToPlayer.y = 0f;
            float targetAngle = Vector3.Angle(transform.forward, flatToPlayer);
            if (_alerted && _loop.CanBeginSlam(distance, seesPlayer, targetAngle) && !_hasAttackToken)
                _hasAttackToken = EnemyAttackTokens.TryAcquire();
            PenitentAttackPhase before = _loop.Phase;
            _loop.Tick(dt, distance, _alerted && seesPlayer, _hasAttackToken, targetAngle, Random.Range(-1f, 1f));
            if (_loop.Phase != before) OnPhaseChanged();
            if (_loop.StrikeStartedThisTick) PerformSlam();
            if (!_alive || !isActiveAndEnabled) return;
            if (!_loop.WantsToken || _loop.SlamEndedThisTick) ReleaseToken();

            MoveAndTurn(toPlayer, dt);
        }

        private void FindPlayer()
        {
            _playerSearchTimer = playerSearchInterval;
            PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
            if (player == _playerHealth && _playerTransform != null) return;
            _playerHealth = player;
            _playerTransform = _playerHealth != null ? _playerHealth.transform : null;
            _playerMovement = _playerHealth != null ? _playerHealth.GetComponent<PlayerMovement>() : null;
            _playerLook = _playerHealth != null ? _playerHealth.GetComponent<PlayerLook>() : null;
        }

        private bool CanSeePlayer(Vector3 toPlayer, float distance)
        {
            if (!HasLivingPlayer || distance > sightRange) return false;
            Vector3 flat = toPlayer;
            flat.y = 0f;
            if (distance > proximityRange && Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f)
                return false;
            Vector3 eye = view != null ? view.HeadPosition : transform.position + Vector3.up * eyeHeight;
            Vector3 ray = _playerTransform.position + Vector3.up * playerTargetHeight - eye;
            // Enemy prefabs share the Default layer, so the ray starts behind our own shield and hitboxes;
            // those must not count as blockers or the Penitent could never see (and slam) the player.
            int count = Physics.RaycastNonAlloc(eye, ray.normalized, _sightHits, ray.magnitude,
                GameLayers.EnemySightMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Transform nearestTransform = null;
            for (int i = 0; i < count; i++)
            {
                Transform t = _sightHits[i].transform;
                if (t == null || t == transform || t.IsChildOf(transform)) continue;
                if (_sightHits[i].distance >= nearest) continue;
                nearest = _sightHits[i].distance;
                nearestTransform = t;
            }
            return nearestTransform != null
                && (nearestTransform == _playerTransform || nearestTransform.IsChildOf(_playerTransform));
        }

        private bool HasLivingPlayer => _playerTransform != null && _playerHealth != null && !_playerHealth.IsDead;

        private void MoveAndTurn(Vector3 toPlayer, float dt)
        {
            PenitentAttackPhase phase = _loop.Phase;
            if (phase == PenitentAttackPhase.SlamWindup || phase == PenitentAttackPhase.SlamStrike
                || phase == PenitentAttackPhase.SlamRecover)
            {
                _velocity = Vector3.zero;
                if (view != null) view.SetMoveSpeed01(0f);
                return;
            }
            toPlayer.y = 0f;
            float turnRate = phase == PenitentAttackPhase.Advance ? turnSpeedAdvance : 0f;
            if (_alerted && toPlayer.sqrMagnitude > 0.000001f && turnRate > 0f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), turnRate * dt);
            bool walking = _alerted && phase == PenitentAttackPhase.Advance
                && toPlayer.sqrMagnitude > stoppingDistance * stoppingDistance;
            if (phase != PenitentAttackPhase.GuardBroken)
            {
                Vector3 horizontal = walking ? toPlayer.normalized * walkSpeed : Vector3.zero;
                _velocity.x = horizontal.x;
                _velocity.z = horizontal.z;
            }
            else
            {
                float decay = Mathf.Exp(-knockbackDecay * dt);
                _velocity.x *= decay;
                _velocity.z *= decay;
            }
            if (_controller != null && _controller.enabled)
            {
                if (_controller.isGrounded && _velocity.y < 0f) _velocity.y = groundedVelocity;
                else _velocity.y -= gravity * dt;
                _controller.Move(_velocity * dt);
            }
            if (view != null) view.SetMoveSpeed01(walking ? 1f : 0f);
        }

        private void OnPhaseChanged()
        {
            if (_loop.Phase == PenitentAttackPhase.SlamWindup)
            {
                _slamImpactResolved = false;
                _slamAreaForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                _slamAreaOrigin = transform.position + _slamAreaForward * slamAreaStartOffset;
                if (view != null)
                {
                    view.BeginSlamTelegraph(attackSettings.SlamWindup, _slamAreaOrigin, _slamAreaForward,
                        slamAreaLength, slamAreaWidth);
                    _slamAreaOrigin.y = view.SlamAreaGroundHeight;
                }
            }
            if (!_loop.WantsToken) ReleaseToken();
            SynchronizeView();
        }

        private void SynchronizeView()
        {
            if (view == null || _loop == null) return;
            if (_loop.Phase != PenitentAttackPhase.SlamWindup) view.EndSlamTelegraph();
            view.SetGuardLowered(!IsGuardUp);
            view.SetStunned(IsStunned);
            view.SetElite(_isElite);
        }

        private void PerformSlam()
        {
            if (GamePause.IsSimulationBlocked || _slamImpactResolved || !HasLivingPlayer
                || _loop.Phase != PenitentAttackPhase.SlamStrike) return;
            _slamImpactResolved = true;
            if (view != null)
            {
                view.EndSlamTelegraph();
                view.PlaySlamStrike();
                view.PlaySlamImpact();
            }
            Vector3 impact = view != null ? view.SlamImpactPosition : _slamAreaOrigin;
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(impact, Vector3.up, true, false);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSlam(impact);
            Vector3 playerPosition = _playerTransform.position;
            if (!PenitentAttackLoop.IsInsideSlamArea(playerPosition, _slamAreaOrigin, _slamAreaForward,
                slamAreaLength, slamAreaWidth, slamAreaHeight)) return;
            Vector3 towardEnemy = transform.position - playerPosition;
            float healthBefore = _playerHealth.CurrentHealth;
            _playerHealth.TakeDamage(slamDamage, playerPosition + Vector3.up * playerTargetHeight,
                towardEnemy.sqrMagnitude > 0.000001f ? towardEnemy.normalized : -_slamAreaForward, gameObject);
            // Parry and invulnerability return without reducing health; Stun alone cannot detect i-frames.
            if (_playerHealth == null || _playerHealth.CurrentHealth >= healthBefore) return;
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(slamPlayerHitstopDuration, slamPlayerHitstopScale);
            if (_playerLook != null)
            {
                _playerLook.AddTrauma(slamCameraTrauma);
                _playerLook.AddViewPunch(slamCameraPunch.x, slamCameraPunch.y);
                _playerLook.DoFovKick(slamCameraFovKick);
            }
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(playerPosition, Vector3.up, true, false);
            // Distinct crunch from the lid's BladeSlam clip; TakeDamage already plays player hurt audio.
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayFleshImpact(playerPosition, true);
            if (_playerHealth.IsDead || _playerMovement == null) return;
            if (_playerMovement.IsGrappling) _playerMovement.StopGrapple(false);
            if (_playerMovement.IsLunging) _playerMovement.StopLunge();
            // Mirror JudgeBossAI: these modes have no public stop API. Do not queue another slam impact.
            if (_playerMovement.IsDashing || _playerMovement.IsGrappling || _playerMovement.IsLunging
                || _playerMovement.IsMeteorSlamming) return;
            _playerMovement.AddImpulse(PenitentSlamLaunch.Compute(_slamAreaForward, slamLaunchUpSpeed, slamLaunchPushSpeed));
        }

        private void StartCacheMaintenance()
        {
            if (_cacheRoutine == null) _cacheRoutine = StartCoroutine(MaintainCaches());
        }

        private IEnumerator MaintainCaches()
        {
            // The iterator is allocated at activation; player searches run on coroutine resumption.
            while (true)
            {
                if (!GamePause.IsSimulationBlocked)
                {
                    if (_playerHealth == null || _playerTransform == null)
                    {
                        _playerSearchTimer -= Time.deltaTime;
                        if (_playerSearchTimer <= 0f) FindPlayer();
                    }
                }
                yield return null;
            }
        }

        private void CancelSlam()
        {
            _loop?.Cancel();
            ReleaseToken();
            if (view != null) view.EndSlamTelegraph();
        }

        private void ReleaseToken()
        {
            if (!_hasAttackToken) return;
            _hasAttackToken = false;
            EnemyAttackTokens.Release();
        }

        /// <inheritdoc />
        public void ReceiveAlert(Transform target)
        {
            if (!_alive || target == null || !HasLivingPlayer) return;
            _playerTransform = target;
            _alerted = true;
            _lostSightRemaining = loseSightTime;
        }

        /// <summary>Receives a shield collider hit using the same ranged arc and melee attacker checks as body hits.</summary>
        public void OnShieldHit(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
        {
            TakeDamage(amount, hitPoint, hitNormal, isCritical);
        }

        /// <summary>Applies precise ranged face hits as critical damage without opening a raised guard.</summary>
        public void OnWeakPointHit(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
        {
            if (!_alive) return;
            if (MeleeDamageScope.IsActive)
            {
                TakeDamage(amount, hitPoint, hitNormal, isCritical);
                return;
            }
            ApplyDamage(amount * Mathf.Max(0f, weakPointMultiplier), hitPoint, hitNormal, true);
        }

        /// <inheritdoc />
        public bool BlocksMeleeFrom(Vector3 attackerPosition)
            => BlocksMeleeDirection(attackerPosition - transform.position);

        private bool BlocksMeleeDirection(Vector3 attackerDirection)
            => PenitentGuard.IsBlocked(transform.forward, attackerDirection, guardHalfAngle, IsGuardUp);

        /// <inheritdoc />
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (!_alive) return;
            bool blocked = MeleeDamageScope.IsActive
                ? BlocksMeleeDirection(hitNormal)
                : PenitentGuard.IsBlocked(transform.forward, transform.position, hitPoint, hitNormal, guardHalfAngle, IsGuardUp);
            if (blocked)
            {
                BlockHit(hitPoint, hitNormal);
                return;
            }
            ApplyDamage(amount, hitPoint, hitNormal, isCritical);
        }

        private void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
        {
            _health -= Mathf.Max(0f, amount);
            if (view != null) view.FlashHit(isCritical);
            ReceiveAlert(_playerTransform);
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, isCritical, true);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayFleshImpact(hitPoint, isCritical);
            if (_health <= 0f)
            {
                Die(hitNormal, isCritical);
                return;
            }
            if (isCritical && HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(criticalHitstopDuration, criticalHitstopScale);
            if (!IsGuardUp && (isCritical || amount >= staggerDamageThreshold))
            {
                bool wasStunned = IsStunned;
                _loop.ExtendGuardBreak(staggerExtend, staggerMaximum);
                if (!wasStunned) _stunSequence++;
                ReleaseToken();
                SynchronizeView();
            }
        }

        private void BlockHit(Vector3 point, Vector3 normal)
        {
            if (_blockFeedbackRemaining > 0f) return;
            _blockFeedbackRemaining = blockFeedbackInterval;
            if (view != null) view.PlayBlockFeedback(point, normal);
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(point, normal, false, false);
            if (CombatAudio.Instance != null)
            {
                if (blockSound != null) CombatAudio.Instance.PlayClipAt(blockSound, point);
                else CombatAudio.Instance.PlayGrappleHookLatch(point);
            }
        }

        /// <inheritdoc />
        public void Stun(float duration, Vector3 knockbackDir)
        {
            if (!_alive || duration <= 0f) return;
            EnterGuardBreak(duration);
            knockbackDir.y = 0f;
            Vector3 knockback = knockbackDir.sqrMagnitude > 0.000001f
                ? knockbackDir.normalized * stunKnockbackSpeed : Vector3.zero;
            _velocity.x = knockback.x;
            _velocity.z = knockback.z;
        }

        /// <inheritdoc />
        public void BreakGuard(float duration)
        {
            if (!_alive || duration <= 0f) return;
            EnterGuardBreak(duration);
            _velocity.x = 0f;
            _velocity.z = 0f;
        }

        private void EnterGuardBreak(float duration)
        {
            _loop.BreakGuard(duration);
            ReleaseToken();
            _stunSequence++;
            SynchronizeView();
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnSignalBurst(view != null ? view.HeadPosition
                    : transform.position + Vector3.up * eyeHeight, stunColor, stunSignalCount);
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(stunHitstopDuration, stunHitstopScale);
        }

        private void Die(Vector3 normal, bool critical)
        {
            if (!_alive) return;
            CancelSlam();
            _alive = false;
            _deathRemaining = respawnTime;
            _velocity = Vector3.zero;
            bool melee = MeleeDamageScope.IsActive;
            Vector3 position = view != null ? view.DeathFxPosition : transform.position + Vector3.up;
            if (healthDropper != null) healthDropper.TriggerDrop(critical, melee);
            else if (HealthOrbManager.Instance != null)
                HealthOrbManager.Instance.TryDropOrbs(position, fallbackHealthOrbCount, critical, melee);
            if (ammoDropper != null) ammoDropper.TriggerDrop(critical, melee);
            SetCollidersActive(false);
            if (view != null)
            {
                view.SetStunned(false);
                view.PlayDeath();
            }
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnFleshGibs(position,
                    normal.sqrMagnitude > 0.000001f ? -normal : transform.forward, deathGibCount);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayGibExplosion(position);
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(deathHitstopDuration, deathHitstopScale);
            CombatEvents.RaiseEnemyKilled(transform.position, critical, true);
            OnEnemyDeath?.Invoke(this);
        }

        private void SetCollidersActive(bool active)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = active;
        }

        /// <inheritdoc />
        public void ApplyEliteScaling(float damageMultiplier, float speedMultiplier, float healthMultiplier)
        {
            if (_isElite) return;
            if (!_eliteBaseCached)
            {
                _baseHealth = maxHealth;
                _baseSlamDamage = slamDamage;
                _baseWalkSpeed = walkSpeed;
                _eliteBaseCached = true;
            }
            maxHealth = _baseHealth * healthMultiplier;
            slamDamage = _baseSlamDamage * damageMultiplier;
            walkSpeed = _baseWalkSpeed * speedMultiplier;
            _health = maxHealth;
            _isElite = true;
            if (view != null) view.SetElite(true);
        }

        private void ResetEliteScaling()
        {
            if (_eliteBaseCached)
            {
                maxHealth = _baseHealth;
                slamDamage = _baseSlamDamage;
                walkSpeed = _baseWalkSpeed;
            }
            _isElite = false;
            if (view != null) view.SetElite(false);
        }

        /// <summary>Restores a complete, idle spawn at the stored director or initial placement.</summary>
        public void Respawn()
        {
            CancelSlam();
            _loop.Reset();
            ResetEliteScaling();
            if (_controller != null) _controller.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _alive = true;
            _health = maxHealth;
            _velocity = Vector3.zero;
            _alerted = false;
            _lostSightRemaining = 0f;
            _playerSearchTimer = 0f;
            _blockFeedbackRemaining = 0f;
            _deathRemaining = 0f;
            // StunSequence remains monotonic across pooled spawns, as required by IStunState.
            SetCollidersActive(true);
            if (view != null) view.ResetVisual();
            SynchronizeView();
        }

        /// <inheritdoc />
        public void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            autoRespawn = false;
            gameObject.SetActive(true);
            _spawnPosition = spawnPosition;
            _spawnRotation = spawnRotation;
            Respawn();
            if (_playerHealth == null) FindPlayer();
        }

        /// <inheritdoc />
        public void OnDespawnToDirector()
        {
            CancelSlam();
            autoRespawn = false;
            _alive = false;
            gameObject.SetActive(false);
        }
    }
}
