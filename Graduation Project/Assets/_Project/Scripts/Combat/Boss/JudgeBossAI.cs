using RetroFPS.Pause;
using System;
using System.Collections;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>Scene driver for The Judge's deterministic encounter loop.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class JudgeBossAI : MonoBehaviour, IDamageable, IStunnable, IEncounterEnemy,
        IThreatCost, IStunState, JudgeBossLoop.IActions
    {
        [Header("Assets and hierarchy")]
        [SerializeField] private JudgeBossConfig config;
        [SerializeField] private JudgeSwordRing swordRing;
        [SerializeField] private EnemyProjectile swordProjectilePrefab;
        [SerializeField] private Transform greatBlade;
        [SerializeField] private Transform hazardsRoot;
        [SerializeField] private JudgeGuillotineStrike[] guillotines;
        [SerializeField] private JudgeScaleSweep sweep;
        [SerializeField] private JudgeRepulseWarning repulseWarning;
        [SerializeField] private JudgeShockLine[] shockLines;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Material phase2Material;
        [SerializeField] private Material kneelingMaterial;
        [SerializeField] private Transform groundReference;
        [SerializeField] private ArenaAmmoRewards ammoRewards;
        [SerializeField, Min(0)] private int stunRewardAmmoPickups = 1;
        [SerializeField, Min(0)] private int transitionAmmoPickups = 2;

        [Header("Movement and queries")]
        [SerializeField] private float arenaMargin = 1.5f;
        [SerializeField] private float altitudeSpeed = 6f;
        [SerializeField] private float groundProbeLift = 3f;
        [SerializeField] private float groundProbeDistance = 40f;
        [SerializeField] private float trialDistanceTolerance = 0.8f;
        [SerializeField] private float executionChaseFraction = 0.7f;
        [SerializeField] private float playerChestHeight = 1.1f;
        [SerializeField] private float thrustHitRadius = 1.2f;
        [SerializeField] private float thrustHitHeight = 1f;
        [SerializeField] private float obstacleSkin = 0.1f;
        [SerializeField] private float executionForwardOffset = 2.5f;
        [SerializeField] private float transitionUpSpeed = 3f;
        [SerializeField] private float scaleStartYawOffset = 90f;
        [SerializeField, Range(0f, 1f)] private float reapHitFraction = 0.5f;

        [Header("Volley")]
        [SerializeField] private float volleyHeight = 1.4f;
        [SerializeField] private float volleySpacing = 0.75f;
        [SerializeField] private float swordReturnDelay = 1f;
        [SerializeField, Min(1)] private int projectileCapacity = 128;

        [Header("Feedback")]
        [SerializeField] private Color parryableColor = new Color(3f, 2.5f, 0.1f);
        [SerializeField] private Color unparryableColor = new Color(3f, 0.05f, 0.02f);
        [SerializeField] private Color hitFlashColor = new Color(2.2f, 2.2f, 2.2f);
        [SerializeField] private float hitFlashDuration = 0.08f;
        [SerializeField] private float criticalHitstopDuration = 0.04f;
        [SerializeField] private float deathHitstopDuration = 0.12f;
        [SerializeField] private float hitstopScale = 0.02f;
        [SerializeField] private float signalHeight = 2.8f;
        [SerializeField] private int telegraphParticles = 12;
        [SerializeField] private int stunParticles = 6;
        [SerializeField] private int deathGibs = 12;

        [Header("Great blade poses (local)")]
        [SerializeField] private float bladePoseSpeed = 16f;
        [SerializeField] private Vector3 bladeIdlePosition = new Vector3(1.3f, 1.3f, 0f);
        [SerializeField] private Vector3 bladeIdleEuler = Vector3.zero;
        [SerializeField] private Vector3 bladeReapPosition = new Vector3(0f, 1.2f, 0.8f);
        [SerializeField] private Vector3 bladeReapEuler = new Vector3(90f, -90f, 0f);
        [SerializeField] private float reapArcDegrees = 180f;
        [SerializeField] private Vector3 bladeRaisedPosition = new Vector3(0f, 2.7f, 0f);
        [SerializeField] private Vector3 bladeRaisedEuler = new Vector3(-35f, 0f, 0f);
        [SerializeField] private Vector3 bladeSlamPosition = new Vector3(0f, 0.2f, 1.8f);
        [SerializeField] private Vector3 bladeSlamEuler = new Vector3(140f, 0f, 0f);
        [SerializeField] private Vector3 bladeThrustPosition = new Vector3(0f, 1.2f, 1f);
        [SerializeField] private Vector3 bladeThrustEuler = new Vector3(90f, 0f, 0f);
        [SerializeField] private Vector3 bladeDroppedPosition = new Vector3(1.7f, 0.1f, 0f);
        [SerializeField] private Vector3 bladeDroppedEuler = new Vector3(0f, 0f, 90f);

        private sealed class JudgeRandom : IJudgeRandom
        {
            /// <inheritdoc />
            public float Value01() => UnityEngine.Random.value;
        }

        private struct Shot
        {
            internal Vector3 Position;
            internal Vector3 Direction;
        }

        private readonly RaycastHit[] _rayHits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private JudgeBossLoop _loop;
        private CharacterController _controller;
        private PlayerHealth _playerHealth;
        private PlayerMovement _playerMovement;
        private PlayerHealth[] _repulsePlayers;
        private PlayerMovement[] _repulseMovements;
        private bool[] _repulsePushPending;
        private Transform _player;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private Material[] _originalBodyMaterials;
        private MaterialPropertyBlock _properties;
        private Bounds _arena;
        private bool _hasArena;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private float _groundY;
        private float _hoverGroundY;
        private float _stunSinkSpeed;
        private float _kneelStarted;
        private bool _reapPending;
        private bool _transitionPushPending;
        private float _health;
        private float _maxHealth;
        private float _flashRemaining;
        private float _windupElapsed;
        private float _windupDuration;
        private float _activeStarted;
        private float _reapElapsed;
        private float _dashTravelled;
        private Vector3 _lockedDirection;
        private bool _dash;
        private bool _dashHit;
        private bool _insideMeleeDamage;
        private bool _dead;
        private uint _stunSequence;
        private bool _telegraph;
        private Color _telegraphColor;
        private int _nextSword;
        private Shot[] _shots;
        private int _shotCount;
        private EnemyProjectile[] _launchedSwords;
        private Coroutine _projectileRoutine;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Raised once when the finisher completes this spawn.</summary>
        public event Action<IEncounterEnemy> OnEnemyDeath;
        /// <summary>Whether this spawn has not been finished.</summary>
        public bool IsAlive => !_dead && gameObject.activeInHierarchy;
        /// <inheritdoc />
        public bool IsStunned => _loop != null && _loop.IsStunned
            && (Phase == JudgePhase.Trial || Phase == JudgePhase.Execution);
        /// <inheritdoc />
        public uint StunSequence => _stunSequence;
        /// <summary>Current encounter phase.</summary>
        public JudgePhase Phase => _loop != null ? _loop.Phase : JudgePhase.Dormant;
        /// <summary>Remaining health in the current phase.</summary>
        public float CurrentHealth => _health;
        /// <summary>Maximum health in the current phase.</summary>
        public float MaxHealth => _maxHealth;
        /// <inheritdoc />
        public Transform EnemyTransform => transform;
        /// <inheritdoc />
        public GameObject EnemyGameObject => gameObject;
        /// <inheritdoc />
        public float ThreatCost => config != null ? config.ThreatCost : 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _groundY = GroundY(_spawnPosition, _spawnPosition.y);
            CacheHoverGround();
            // Hazards are world-space siblings; never include them in the boss hit/flash hierarchy.
            if (hazardsRoot != null) hazardsRoot.SetParent(transform.parent, true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _properties = new MaterialPropertyBlock();
            _originalBodyMaterials = new Material[bodyRenderers != null ? bodyRenderers.Length : 0];
            for (int i = 0; i < _originalBodyMaterials.Length; i++)
                if (bodyRenderers[i] != null) _originalBodyMaterials[i] = bodyRenderers[i].sharedMaterial;
            _shots = new Shot[Mathf.Max(1, projectileCapacity)];
            _launchedSwords = new EnemyProjectile[_shots.Length];
            if (config != null)
            {
                _loop = new JudgeBossLoop(config.CreateSettings(), new JudgeRandom(), this);
                _health = _maxHealth = config.Phase1Health;
            }
            else
            {
                Debug.LogError("JudgeBossAI requires a JudgeBossConfig. Run RetroFPS/Build Judge Boss.", this);
                enabled = false;
            }
            CachePlayer();
            CombatTargetLookup.RegisterHierarchy(this);
        }

        private void Start()
        {
            CombatTargetLookup.RegisterHierarchy(this);
            CachePlayer();
            if (swordRing != null) swordRing.SetVisible(true);
            if (greatBlade != null) greatBlade.gameObject.SetActive(false);
            CancelHazards();
        }

        private void OnEnable()
        {
            EnemyProjectile.OnDeflectedImpact += HandleReflectedImpact;
            _projectileRoutine = StartCoroutine(DispatchProjectiles());
        }

        private void OnDisable()
        {
            EnemyProjectile.OnDeflectedImpact -= HandleReflectedImpact;
            if (_projectileRoutine != null) StopCoroutine(_projectileRoutine);
            _projectileRoutine = null;
            CancelHazards();
            ClearProjectiles();
            if (Phase != JudgePhase.Dormant) BossEvents.RaiseBossDisengaged();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && hazardsRoot != null) Destroy(hazardsRoot.gameObject);
        }

        private void CachePlayer()
        {
            _repulsePlayers = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
            _repulseMovements = new PlayerMovement[_repulsePlayers.Length];
            _repulsePushPending = new bool[_repulsePlayers.Length];
            for (int i = 0; i < _repulsePlayers.Length; i++)
                _repulseMovements[i] = _repulsePlayers[i].GetComponent<PlayerMovement>();
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (_playerHealth == null) return;
            _player = _playerHealth.transform;
            _playerMovement = _playerHealth.GetComponent<PlayerMovement>();
        }

        /// <summary>Starts Trial; repeated engagement requests are ignored.</summary>
        public void Engage()
        {
            if (_loop == null || Phase != JudgePhase.Dormant) return;
            if (_playerHealth == null) CachePlayer();
            _loop.Engage();
            BossEvents.RaiseBossEngaged(config.NameLocKey, config.Phase1Health);
            BossEvents.RaiseBossPhaseChanged(1);
        }

        /// <summary>Assigns world-space movement limits and the pose used for retries.</summary>
        public void SetArena(Bounds worldBounds, Transform spawnAnchor)
        {
            _arena = worldBounds;
            _hasArena = true;
            if (ammoRewards != null) ammoRewards.SetArena(worldBounds);
            if (spawnAnchor == null) return;
            _spawnPosition = spawnAnchor.position;
            _spawnRotation = spawnAnchor.rotation;
        }

        /// <summary>Restores a dormant, fully healed boss and removes every outstanding attack.</summary>
        public void ResetBoss()
        {
            if (_loop == null) return;
            _loop.Reset();
            if (ammoRewards != null) ammoRewards.ClearRewards();
            _dead = false;
            _dash = _dashHit = _insideMeleeDamage = _telegraph = false;
            _flashRemaining = _windupElapsed = _dashTravelled = 0f;
            _reapPending = _transitionPushPending = false;
            _kneelStarted = 0f;
            _nextSword = 0;
            _health = _maxHealth = config.Phase1Health;
            CancelHazards();
            ClearProjectiles();
            _controller.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _groundY = GroundY(_spawnPosition, _spawnPosition.y);
            CacheHoverGround();
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = true;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = true;
            for (int i = 0; i < _originalBodyMaterials.Length; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].sharedMaterial = _originalBodyMaterials[i];
            if (swordRing != null) swordRing.SetVisible(true);
            if (greatBlade != null)
            {
                greatBlade.localPosition = bladeIdlePosition;
                greatBlade.localRotation = Quaternion.Euler(bladeIdleEuler);
                greatBlade.gameObject.SetActive(false);
            }
            RefreshColors();
            BossEvents.RaiseBossDisengaged();
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_loop == null || _dead || Phase == JudgePhase.Dormant) return;
            float dt = Time.deltaTime;
            if (_flashRemaining > 0f)
            {
                _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
                if (_flashRemaining == 0f) RefreshColors();
            }
            Vector3 toPlayer = _player != null ? _player.position - transform.position : Vector3.zero;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            var context = new JudgeBossLoop.Context(distance, HasLineOfSight(),
                _maxHealth > 0f ? _health / _maxHealth : 0f, PlayerReachable(toPlayer, distance));
            if (Phase == JudgePhase.Transition) TryTransitionPush();
            TryRepulsePushes();
            TickDriver(dt, in context);
            MoveBoss(toPlayer, distance, dt);
        }

        private void TickDriver(float dt, in JudgeBossLoop.Context context)
        {
            float budget = dt;
            do
            {
                JudgeStep step = _loop.Step;
                JudgePhase phase = Phase;
                JudgeAttack attack = _loop.CurrentAttack;
                bool reap = _reapPending && step == JudgeStep.Active && attack == JudgeAttack.Reap;
                float hitAt = config.ReapActive * reapHitFraction;
                if (reap && _reapElapsed >= hitAt)
                {
                    ApplyReapHit();
                    continue;
                }
                float elapsed = Mathf.Min(budget, _loop.StepTimeRemaining);
                if (reap) elapsed = Mathf.Min(elapsed, hitAt - _reapElapsed);
                _loop.Tick(elapsed, in context, this);
                if (reap) _reapElapsed += elapsed;
                budget -= elapsed;
                if (_reapPending && _loop.Step == JudgeStep.Active && _loop.CurrentAttack == JudgeAttack.Reap
                    && _reapElapsed >= hitAt) ApplyReapHit();
                if (elapsed <= 0f && step == _loop.Step && phase == Phase && attack == _loop.CurrentAttack)
                {
                    // An expired idle with no eligible target still consumes stun immunity time.
                    if (step == JudgeStep.Idle && budget > 0f) _loop.Tick(budget, in context, this);
                    return;
                }
            }
            while (budget > 0f && (Phase == JudgePhase.Trial || Phase == JudgePhase.Execution
                || Phase == JudgePhase.Transition));
        }

        private bool HasLineOfSight()
        {
            if (_player == null) return false;
            Vector3 origin = transform.position + Vector3.up * playerChestHeight;
            Vector3 delta = _player.position + Vector3.up * playerChestHeight - origin;
            return !CombatRaycast.Nearest(origin, delta.normalized, delta.magnitude,
                GameLayers.GroundMask, _rayHits, out _);
        }

        private void MoveBoss(Vector3 toPlayer, float distance, float dt)
        {
            bool combatPhase = Phase == JudgePhase.Trial || Phase == JudgePhase.Execution;
            bool freeToTurn = Phase == JudgePhase.Trial || _loop.Step == JudgeStep.Idle
                || _loop.Step == JudgeStep.Recover || _loop.Step == JudgeStep.Windup;
            if (combatPhase && !_loop.IsStunned && freeToTurn && toPlayer.sqrMagnitude > 0f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(toPlayer), config.TurnSpeedDegrees * dt);
            Vector3 move = Vector3.zero;
            if (Phase == JudgePhase.Trial && !_loop.IsStunned && _player != null)
            {
                float difference = distance - config.PreferredDistanceTrial;
                if (Mathf.Abs(difference) > trialDistanceTolerance)
                    move = toPlayer.normalized * (Mathf.Sign(difference) * config.MoveSpeedTrial * dt);
            }
            else if (Phase == JudgePhase.Execution && !_loop.IsStunned && _player != null
                && (_loop.Step == JudgeStep.Idle || _loop.Step == JudgeStep.Recover))
            {
                Vector3 direction = distance > 0f ? toPlayer / distance : transform.forward;
                if (distance < config.ExecutionMinSeparation)
                    move = -direction * Mathf.Min(config.MoveSpeedExecution * dt,
                        config.ExecutionMinSeparation - distance);
                else if (distance > config.ReapRange * executionChaseFraction)
                    move = direction * Mathf.Min(config.MoveSpeedExecution * dt,
                        distance - Mathf.Max(config.ExecutionMinSeparation, config.ReapRange * executionChaseFraction));
            }
            if (_dash && _loop.Step == JudgeStep.Active && _loop.CurrentAttack == JudgeAttack.Thrust)
            {
                float step = Mathf.Min(config.ThrustSpeed * dt, config.ThrustDistance - _dashTravelled);
                // Clamp the whole segment, so even a long frame cannot tunnel through the stop radius.
                if (_player != null)
                    step = Mathf.Min(step, Mathf.Max(0f, distance - config.ThrustStopDistance));
                move = _lockedDirection * step;
                if (step <= 0f) _dash = false;
            }
            Vector3 next = ClampToArena(transform.position + move);
            _groundY = GroundY(next, _groundY);
            float height = Phase == JudgePhase.Trial && !_loop.IsStunned
                ? config.HoverHeightTrial : config.HoverHeightExecution;
            if (Phase == JudgePhase.Kneeling) height = 0f;
            float floor = GroundY(transform.position, _hoverGroundY);
            float targetGround = Phase == JudgePhase.Execution && _player != null
                ? Mathf.Max(floor, GroundY(_player.position, floor)) : floor;
            // Trial retains its arena-floor altitude; Execution follows the higher supporting ground.
            float targetY = (Phase == JudgePhase.Trial && !_loop.IsStunned ? _hoverGroundY : targetGround) + height;
            Vector3 horizontal = next - transform.position;
            horizontal.y = 0f;
            if (Phase == JudgePhase.Execution && horizontal.sqrMagnitude > 0f)
            {
                // Probe beyond the controller's leading edge so losing the side cast during ascent
                // cannot immediately lower the hover target back into the same ledge.
                Vector3 ahead = transform.position + horizontal.normalized
                    * (horizontal.magnitude + _controller.radius + obstacleSkin);
                float aheadGround = GroundY(ahead, floor);
                if (aheadGround - _hoverGroundY <= config.MaxFloatOverHeight)
                    targetY = Mathf.Max(targetY, aheadGround + height);
            }
            bool ledge = false;
            if (Phase == JudgePhase.Execution && horizontal.sqrMagnitude > 0f
                && MovementObstacle(horizontal.normalized, horizontal.magnitude + obstacleSkin, out RaycastHit obstacle))
            {
                float top = obstacle.collider.bounds.max.y;
                if (top - _hoverGroundY <= config.MaxFloatOverHeight)
                {
                    targetY = Mathf.Max(targetY, top + height + obstacleSkin);
                    ledge = true;
                }
                else
                {
                    horizontal = horizontal.normalized * Mathf.Min(horizontal.magnitude,
                        Mathf.Max(0f, obstacle.distance - obstacleSkin));
                    _dash = false;
                }
            }
            float verticalSpeed = _loop.IsStunned ? _stunSinkSpeed
                : Phase == JudgePhase.Execution ? config.ExecutionClimbSpeed : altitudeSpeed;
            float nextY = Mathf.MoveTowards(transform.position.y, targetY, verticalSpeed * dt);
            // Move vertically first through the controller, preserving ceilings, walls and pillars.
            Vector3 before = transform.position;
            if (!_controller.enabled) return;
            _controller.Move(Vector3.up * (nextY - transform.position.y));
            if (!ledge || transform.position.y >= targetY - obstacleSkin)
                _controller.Move(horizontal);
            if (_dash)
            {
                Vector3 travelled = transform.position - before;
                travelled.y = 0f;
                _dashTravelled += travelled.magnitude;
                if (_dashTravelled >= config.ThrustDistance) _dash = false;
            }
            if (_loop.Step == JudgeStep.Active && _loop.CurrentAttack == JudgeAttack.Thrust && !_dashHit)
                CheckThrustHit();
        }

        private bool PlayerReachable(Vector3 toPlayer, float distance)
        {
            if (_player == null) return false;
            float travel = Mathf.Max(0f, distance - config.ThrustStopDistance);
            if (travel <= 0f) return true;
            Vector3 origin = transform.position + Vector3.up * thrustHitHeight;
            int count = Physics.SphereCastNonAlloc(origin, _controller.radius, toPlayer / distance,
                _rayHits, travel, GameLayers.EnemyObstacleMask | GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            if (count == _rayHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!BelongsToBoss(_rayHits[i].collider)) return false;
            return true;
        }

        private bool MovementObstacle(Vector3 direction, float distance, out RaycastHit nearest)
        {
            Vector3 center = transform.position + _controller.center;
            float halfSegment = Mathf.Max(0f, _controller.height * 0.5f - _controller.radius);
            int count = Physics.CapsuleCastNonAlloc(center - Vector3.up * halfSegment,
                center + Vector3.up * halfSegment, _controller.radius, direction, _rayHits, distance,
                GameLayers.EnemyObstacleMask | GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (BelongsToBoss(_rayHits[i].collider) || _rayHits[i].distance >= best) continue;
                nearest = _rayHits[i];
                best = nearest.distance;
            }
            return best < float.PositiveInfinity;
        }

        private Vector3 ClampToArena(Vector3 point)
        {
            if (!_hasArena) return point;
            float xMargin = Mathf.Min(arenaMargin, _arena.extents.x);
            float zMargin = Mathf.Min(arenaMargin, _arena.extents.z);
            point.x = Mathf.Clamp(point.x, _arena.min.x + xMargin, _arena.max.x - xMargin);
            point.z = Mathf.Clamp(point.z, _arena.min.z + zMargin, _arena.max.z - zMargin);
            return point;
        }

        private float GroundY(Vector3 point, float fallback)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * groundProbeLift, Vector3.down,
                _rayHits, groundProbeDistance, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].distance >= nearest || BelongsToBoss(_rayHits[i].collider)) continue;
                nearest = _rayHits[i].distance;
                fallback = _rayHits[i].point.y;
            }
            return fallback;
        }

        private void CacheHoverGround()
        {
            // Builder wires the checkpoint on the arena floor, away from the one-metre altar.
            Vector3 reference = groundReference != null ? groundReference.position : _spawnPosition;
            _hoverGroundY = GroundY(reference, reference.y);
        }

        private bool BelongsToBoss(Collider hit) => hit != null
            && (hit.transform == transform || hit.transform.IsChildOf(transform));

        /// <inheritdoc />
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (_loop == null || _dead || _loop.IsInvulnerable) return;
            if (Phase == JudgePhase.Kneeling)
            {
                if (Time.time - _kneelStarted < config.KneelGrace) return;
                _loop.NotifyFinisherHit();
                Die(hitPoint, hitNormal, isCritical);
                return;
            }
            _health = Mathf.Max(0f, _health - Mathf.Max(0f, amount));
            _flashRemaining = hitFlashDuration;
            RefreshColors();
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, isCritical, true);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayFleshImpact(hitPoint, isCritical);
            if (isCritical && _health > 0f && HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(criticalHitstopDuration, hitstopScale);
            BossEvents.RaiseBossHealthChanged(_health, _maxHealth);
            if (_health <= 0f) _loop.NotifyPhaseHealthDepleted();
        }

        /// <inheritdoc />
        public void Stun(float duration, Vector3 knockbackDir)
        {
            if (_loop == null) return;
            _loop.RequestStun(_insideMeleeDamage ? JudgeStunSource.MeleeParry : JudgeStunSource.Other,
                _insideMeleeDamage ? config.ParryStunDuration : duration);
        }

        private void HandleReflectedImpact(EnemyProjectile projectile, Collider hit)
        {
            if (_loop != null && projectile != null && projectile.IsDeflected && OwnsSword(projectile) && BelongsToBoss(hit)
                && (Phase == JudgePhase.Trial || Phase == JudgePhase.Execution))
                _loop.RequestStun(JudgeStunSource.ReflectedSword, config.ReflectedStunDuration);
        }

        private bool OwnsSword(EnemyProjectile projectile)
        {
            if (_launchedSwords == null) return false;
            for (int i = 0; i < _launchedSwords.Length; i++)
                if (_launchedSwords[i] == projectile) return true;
            return false;
        }

        /// <inheritdoc />
        public void OnTelegraph(JudgeAttack attack, bool parryable, float duration)
        {
            _windupElapsed = 0f;
            _windupDuration = duration;
            _telegraph = true;
            _telegraphColor = parryable ? parryableColor : unparryableColor;
            RefreshColors();
            Signal(_telegraphColor, telegraphParticles);
            if (attack == JudgeAttack.Volley) FormVolleyLine(0f);
            if (attack == JudgeAttack.Scale && sweep != null)
                sweep.Prepare(transform, config.ScaleLength, transform.eulerAngles.y + scaleStartYawOffset);
            if (attack == JudgeAttack.Repulse && repulseWarning != null)
                repulseWarning.Prepare(RepulseGroundPosition() + Vector3.up * config.RepulseMarkerLift,
                    config.RepulseRadius, config.RepulseMarkerThickness);
        }

        /// <inheritdoc />
        public void OnAttackExecute(JudgeAttack attack, int index)
        {
            _activeStarted = Time.time;
            _telegraph = false;
            RefreshColors();
            _lockedDirection = PlayerDirection();
            switch (attack)
            {
                case JudgeAttack.Verdict:
                    if (_player != null)
                    {
                        Vector3 origin = LaunchPosition(_nextSword);
                        QueueSword(origin, (_player.position + Vector3.up * playerChestHeight - origin).normalized);
                        HideNextSword();
                    }
                    break;
                case JudgeAttack.Volley:
                    FormVolleyLine(1f);
                    for (int i = 0; i < config.VolleyCount; i++)
                    {
                        float fraction = config.VolleyCount > 1 ? (float)i / (config.VolleyCount - 1) : 0.5f;
                        Vector3 direction = Quaternion.AngleAxis((fraction - 0.5f) * config.VolleySpreadDegrees,
                            Vector3.up) * _lockedDirection;
                        Vector3 origin = LaunchPosition(i);
                        origin.y = VolleyCenter().y;
                        QueueSword(origin, direction);
                        if (swordRing != null) swordRing.HideSwordTemporarily(i, swordReturnDelay);
                    }
                    if (swordRing != null) swordRing.ReleaseLine();
                    break;
                case JudgeAttack.Guillotine:
                    if (_player == null || guillotines == null) break;
                    for (int i = 0; i < guillotines.Length; i++)
                    {
                        if (guillotines[i] == null || guillotines[i].IsBusy) continue;
                        guillotines[i].Activate(_player.position, config.GuillotineFallDelay,
                            config.GuillotineRadius, config.GuillotineDamage, config.GuillotineHitHeight);
                        break;
                    }
                    break;
                case JudgeAttack.Scale:
                    if (sweep != null) sweep.Activate(transform, config.ScaleLength,
                        config.ScaleAngularSpeedDegrees, config.ScaleActive, config.ScaleDamage);
                    break;
                case JudgeAttack.Repulse:
                    if (repulseWarning != null) repulseWarning.Cancel();
                    ExecuteRepulse();
                    break;
                case JudgeAttack.Reap:
                    _reapPending = true;
                    _reapElapsed = 0f;
                    break;
                case JudgeAttack.ExecutionStrike:
                    Vector3 impact = transform.position + _lockedDirection * executionForwardOffset;
                    impact.y = GroundY(impact, _groundY);
                    bool circleHit = DamageExecutionCircle(impact);
                    if (shockLines == null || Phase != JudgePhase.Execution || _playerHealth == null || _playerHealth.IsDead) break;
                    for (int i = 0; i < shockLines.Length; i++)
                    {
                        if (shockLines[i] == null || shockLines[i].IsBusy) continue;
                        shockLines[i].Activate(impact + _lockedDirection * config.ExecutionRadius,
                            _lockedDirection, config.ShockLineSpeed,
                            config.ShockLineLength, config.ShockLineDamage, circleHit);
                        break;
                    }
                    break;
                case JudgeAttack.Thrust:
                    transform.rotation = Quaternion.LookRotation(_lockedDirection);
                    _dashTravelled = 0f;
                    _dashHit = false;
                    _dash = true;
                    break;
            }
        }

        private bool DamageExecutionCircle(Vector3 impact)
        {
            int count = Physics.OverlapSphereNonAlloc(impact, config.ExecutionRadius, _overlaps,
                1 << GameLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!IsPlayer(_overlaps[i])) continue;
                return _playerHealth.TakeUnparryableDamage(config.ExecutionDamage, impact, Vector3.up);
            }
            return false;
        }

        private Vector3 RepulseGroundPosition()
        {
            Vector3 position = transform.position;
            position.y = GroundY(position, _groundY);
            return position;
        }

        private void ExecuteRepulse()
        {
            Vector3 center = RepulseGroundPosition();
            float ceiling = transform.position.y + config.RepulseHeightAboveBoss;
            for (int i = 0; i < _repulsePlayers.Length; i++)
            {
                PlayerHealth player = _repulsePlayers[i];
                if (player == null || !player.isActiveAndEnabled || player.IsDead) continue;
                Vector3 delta = player.transform.position - center;
                if (delta.y < 0f || player.transform.position.y > ceiling) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude > config.RepulseRadius * config.RepulseRadius) continue;
                // Preserve existing dash/lunge immunity. The impulse still dislodges a lunging camper.
                player.TakeUnparryableDamage(config.RepulseDamage, center, Vector3.up);
                _repulsePushPending[i] = !player.IsDead;
            }
            TryRepulsePushes();
        }

        private void TryRepulsePushes()
        {
            if (_repulsePushPending == null) return;
            for (int i = 0; i < _repulsePushPending.Length; i++)
            {
                if (!_repulsePushPending[i]) continue;
                PlayerHealth player = _repulsePlayers[i];
                if (player == null || player.IsDead) { _repulsePushPending[i] = false; continue; }
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;
                if (TryPushPlayer(_repulseMovements[i], direction, config.RepulseKnockback, config.RepulseUpSpeed))
                    _repulsePushPending[i] = false;
            }
        }

        private void ApplyReapHit()
        {
            _reapPending = false;
            Vector3 delta = _player != null ? _player.position - transform.position : Vector3.zero;
            delta.y = 0f;
            if (_player != null && delta.magnitude <= config.ReapRange
                && Vector3.Angle(transform.forward, delta) <= config.ReapHalfAngleDegrees)
                DealMelee(config.ReapDamage);
        }

        private void CheckThrustHit()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * thrustHitHeight,
                thrustHitRadius, _overlaps, 1 << GameLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!IsPlayer(_overlaps[i])) continue;
                _dashHit = true;
                DealMelee(config.ThrustDamage);
                break;
            }
        }

        private bool IsPlayer(Collider hit) => _playerHealth != null && hit != null
            && (hit.transform == _player || hit.transform.IsChildOf(_player));

        private void DealMelee(float damage)
        {
            if (_playerHealth == null) return;
            _insideMeleeDamage = true;
            try
            {
                _playerHealth.TakeDamage(damage, _player.position + Vector3.up * playerChestHeight,
                    -transform.forward, attacker: gameObject);
            }
            finally { _insideMeleeDamage = false; }
        }

        /// <inheritdoc />
        public void OnAttackEnd(JudgeAttack attack, bool interrupted)
        {
            _reapPending = false;
            _dash = false;
            if (attack == JudgeAttack.Scale && sweep != null) sweep.Cancel();
            if (attack == JudgeAttack.Repulse && repulseWarning != null) repulseWarning.Cancel();
            _telegraph = false;
            if (swordRing != null) swordRing.ReleaseLine();
            RefreshColors();
        }

        /// <inheritdoc />
        public void OnPhaseChanged(JudgePhase phase)
        {
            if (phase != JudgePhase.Transition) _transitionPushPending = false;
            if (phase == JudgePhase.Transition)
            {
                if (ammoRewards != null) ammoRewards.QueueDrops(transitionAmmoPickups);
                CancelHazards();
                ClearProjectiles();
                _transitionPushPending = true;
                TryTransitionPush();
                if (swordRing != null) swordRing.SetVisible(false);
                if (greatBlade != null) greatBlade.gameObject.SetActive(true);
                for (int i = 0; i < _originalBodyMaterials.Length; i++)
                    if (bodyRenderers[i] != null && phase2Material != null)
                        bodyRenderers[i].sharedMaterial = phase2Material;
            }
            else if (phase == JudgePhase.Execution)
            {
                _health = _maxHealth = config.Phase2Health;
                BossEvents.RaiseBossPhaseChanged(2);
                BossEvents.RaiseBossHealthChanged(_health, _maxHealth);
            }
            else if (phase == JudgePhase.Kneeling)
            {
                _kneelStarted = Time.time;
                CancelHazards();
                ClearProjectiles();
                _telegraph = false;
                _flashRemaining = 0f;
                for (int i = 0; i < _originalBodyMaterials.Length; i++)
                    if (bodyRenderers[i] != null && kneelingMaterial != null)
                        bodyRenderers[i].sharedMaterial = kneelingMaterial;
                RefreshColors();
            }
        }

        /// <inheritdoc />
        public void OnStunStarted(float duration)
        {
            _stunSequence++;
            if (ammoRewards != null) ammoRewards.QueueDrops(stunRewardAmmoPickups);
            _stunSinkSpeed = Mathf.Abs(transform.position.y - (_hoverGroundY + config.HoverHeightExecution))
                / config.StunSinkDuration;
            Signal(parryableColor, stunParticles);
            RefreshColors();
        }

        /// <inheritdoc />
        public void OnStunEnded() => RefreshColors();

        private void TryTransitionPush()
        {
            if (!_transitionPushPending || _playerMovement == null || _playerHealth == null || _playerHealth.IsDead) return;
            if (TryPushPlayer(_playerMovement, PlayerDirection(), config.TransitionPushSpeed, transitionUpSpeed))
                _transitionPushPending = false;
        }

        private static bool TryPushPlayer(PlayerMovement movement, Vector3 direction, float speed, float upSpeed)
        {
            if (movement == null) return true;
            if (movement.IsGrappling) movement.StopGrapple(false);
            if (movement.IsLunging) movement.StopLunge();
            // These modes have no public stop command. Retry the impulse on the next eligible frame.
            if (movement.IsDashing || movement.IsGrappling || movement.IsLunging || movement.IsMeteorSlamming) return false;
            movement.AddImpulse(direction * speed + Vector3.up * upSpeed);
            return true;
        }

        private void Signal(Color color, int particles)
        {
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnSignalBurst(transform.position + Vector3.up * signalHeight,
                    color, particles);
        }

        private void RefreshColors()
        {
            bool flash = _flashRemaining > 0f;
            bool stun = _loop != null && _loop.IsStunned;
            Color color = flash ? hitFlashColor : stun ? parryableColor : _telegraphColor;
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (!flash && !stun && !_telegraph) _renderers[i].SetPropertyBlock(null);
                else
                {
                    _properties.Clear();
                    _properties.SetColor(BaseColorId, color);
                    _properties.SetColor(EmissionId, color);
                    _renderers[i].SetPropertyBlock(_properties);
                }
            }
        }

        private Vector3 PlayerDirection()
        {
            Vector3 direction = _player != null ? _player.position - transform.position : transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;
        }

        private Vector3 VolleyCenter()
        {
            Vector3 center = transform.position;
            center.y = GroundY(_player != null ? _player.position : center, _groundY) + volleyHeight;
            return center;
        }

        private void FormVolleyLine(float blend)
        {
            if (swordRing == null) return;
            Vector3 right = Vector3.Cross(Vector3.up, PlayerDirection());
            swordRing.FormLine(VolleyCenter(), right, volleySpacing, blend);
        }

        private Vector3 LaunchPosition(int index) => swordRing != null
            ? swordRing.GetLaunchPosition(index) : transform.position + Vector3.up * playerChestHeight;

        private void HideNextSword()
        {
            if (swordRing == null || swordRing.Count == 0) return;
            swordRing.HideSwordTemporarily(_nextSword, swordReturnDelay);
            _nextSword = (_nextSword + 1) % swordRing.Count;
        }

        private void QueueSword(Vector3 position, Vector3 direction)
        {
            if (_shotCount >= _shots.Length || swordProjectilePrefab == null) return;
            _shots[_shotCount].Position = position;
            _shots[_shotCount].Direction = direction;
            _shotCount++;
        }

        // EnemyProjectile.Initialize builds its trail and allocates. Dispatch outside Update/LateUpdate;
        // the brain callbacks only fill the fixed launch buffer. There is one coroutine per enabled boss.
        private IEnumerator DispatchProjectiles()
        {
            while (true)
            {
                yield return null;
                if (GamePause.IsSimulationBlocked) continue;
                for (int i = 0; i < _shotCount; i++)
                {
                    int slot = -1;
                    for (int j = 0; j < _launchedSwords.Length; j++)
                        if (_launchedSwords[j] == null) { slot = j; break; }
                    if (slot < 0) break;
                    Shot shot = _shots[i];
                    // TODO: Follow up with a pre-warmed sword projectile pool (same launch mechanism as Flying Eye).
                    EnemyProjectile projectile = Instantiate(swordProjectilePrefab, shot.Position,
                        Quaternion.LookRotation(shot.Direction));
                    projectile.Initialize(transform, config.SwordSpeed, config.SwordDamage);
                    _launchedSwords[slot] = projectile;
                }
                _shotCount = 0;
            }
        }

        private void ClearProjectiles()
        {
            _shotCount = 0;
            if (_launchedSwords == null) return;
            for (int i = 0; i < _launchedSwords.Length; i++)
            {
                if (_launchedSwords[i] == null) continue;
                _launchedSwords[i].gameObject.SetActive(false);
                Destroy(_launchedSwords[i].gameObject);
                _launchedSwords[i] = null;
            }
        }

        private void CancelHazards()
        {
            if (repulseWarning != null) repulseWarning.Cancel();
            if (_repulsePushPending != null) Array.Clear(_repulsePushPending, 0, _repulsePushPending.Length);
            _dash = false;
            _reapPending = false;
            if (guillotines != null)
                for (int i = 0; i < guillotines.Length; i++)
                    if (guillotines[i] != null) guillotines[i].Cancel();
            if (sweep != null) sweep.Cancel();
            if (shockLines != null)
                for (int i = 0; i < shockLines.Length; i++)
                    if (shockLines[i] != null) shockLines[i].Cancel();
        }

        private void LateUpdate()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_loop == null || _dead) return;
            if (_loop.Step == JudgeStep.Windup)
            {
                _windupElapsed += Time.deltaTime;
                float progress = _windupDuration > 0f ? _windupElapsed / _windupDuration : 1f;
                if (_loop.CurrentAttack == JudgeAttack.Volley) FormVolleyLine(progress);
                if (_loop.CurrentAttack == JudgeAttack.Repulse && repulseWarning != null)
                    repulseWarning.SetProgress(RepulseGroundPosition() + Vector3.up * config.RepulseMarkerLift, progress);
            }
            if (greatBlade == null || !greatBlade.gameObject.activeSelf) return;
            Vector3 position = bladeIdlePosition;
            Vector3 euler = bladeIdleEuler;
            if (Phase == JudgePhase.Kneeling) { position = bladeDroppedPosition; euler = bladeDroppedEuler; }
            else if (_loop.CurrentAttack == JudgeAttack.Reap && _loop.Step == JudgeStep.Active)
            {
                float progress = config.ReapActive > 0f
                    ? Mathf.Clamp01((Time.time - _activeStarted) / config.ReapActive) : 1f;
                position = bladeReapPosition;
                euler = bladeReapEuler + Vector3.up * (reapArcDegrees * progress);
            }
            else if (_loop.CurrentAttack == JudgeAttack.ExecutionStrike)
            {
                bool raised = _loop.Step == JudgeStep.Windup;
                position = raised ? bladeRaisedPosition : bladeSlamPosition;
                euler = raised ? bladeRaisedEuler : bladeSlamEuler;
            }
            else if (_loop.CurrentAttack == JudgeAttack.Thrust)
            { position = bladeThrustPosition; euler = bladeThrustEuler; }
            float blend = Mathf.Clamp01(bladePoseSpeed * Time.deltaTime);
            greatBlade.localPosition = Vector3.Lerp(greatBlade.localPosition, position, blend);
            greatBlade.localRotation = Quaternion.Slerp(greatBlade.localRotation, Quaternion.Euler(euler), blend);
        }

        private void Die(Vector3 hitPoint, Vector3 hitNormal, bool critical)
        {
            if (_dead) return;
            _dead = true;
            _health = 0f;
            CancelHazards();
            ClearProjectiles();
            _telegraph = false;
            RefreshColors();
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = false;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = false;
            if (swordRing != null) swordRing.SetVisible(false);
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnFleshGibs(transform.position, -hitNormal, deathGibs);
                CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, true, false);
            }
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayGibExplosion(transform.position);
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(deathHitstopDuration, hitstopScale);
            CombatEvents.RaiseEnemyKilled(transform.position, critical, true);
            BossEvents.RaiseBossDisengaged();
            OnEnemyDeath?.Invoke(this);
        }

        /// <inheritdoc />
        public void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            // Activation may run Awake for a previously inactive pooled instance.
            gameObject.SetActive(true);
            _spawnPosition = spawnPosition;
            _spawnRotation = spawnRotation;
            ResetBoss();
            Engage();
        }

        /// <inheritdoc />
        public void OnDespawnToDirector()
        {
            ResetBoss();
            gameObject.SetActive(false);
        }
    }
}
