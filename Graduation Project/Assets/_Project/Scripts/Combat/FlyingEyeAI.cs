using RetroFPS.Pause;
using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.Combat.Navigation;

namespace RetroFPS
{
    /// <summary>Locomotion and attack phases of the Flying Eye.</summary>
    public enum FlyingEyeState
    {
        /// <summary>Waiting for a patrol or target.</summary>
        Idle,
        /// <summary>Moving between patrol destinations.</summary>
        Patrol,
        /// <summary>Keeping distance and choosing attacks.</summary>
        Combat,
        /// <summary>Stationary attack telegraph.</summary>
        Windup,
        /// <summary>Tangent burst following a projectile launch.</summary>
        Reposition,
        /// <summary>Interrupted or grounded by a stun.</summary>
        Stagger,
        /// <summary>Playing death or waiting for respawn.</summary>
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
    public class FlyingEyeAI : MonoBehaviour, IDamageable, IAlertableEnemy, IStunnable, IEncounterEnemy, IThreatCost, IEliteScalable, IStunState, IReflectPunishable, IHuntableEnemy
    {
        [Header("Sağlık ve Ödül")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float maxHealth = 90f; // Havada olduğu için biraz daha kırılgan
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float respawnTime = 4.0f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private bool autoRespawn = true;

        [Tooltip("Dalga tehdit bütçesindeki ağırlık (Ghoul = 1 referans)")]
        [SerializeField] private float threatCost = 2f;

        /// <summary>Whether this eye respawns itself after death; disabled for director-managed eyes.</summary>
        public bool AutoRespawn { get => autoRespawn; set => autoRespawn = value; }
        /// <summary>Threat budget weight used by the wave director.</summary>
        public float ThreatCost => threatCost;

        [Header("Uçuş ve Hareket")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float flySpeed = 3.8f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float rotationSpeed = 8.0f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float hoverHeight = 2.6f; // Zeminden ideal uçuş yüksekliği
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float maxFlyHeight = 3.6f; // Asla 3.6m üzerine çıkamaz
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float minFlyHeight = 1.8f; // Asla 1.8m altına inemez
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float bobAmplitude = 0.22f; // Havada hafif süzülme dalgalanması
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float bobFrequency = 2.0f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float preferredDistance = 9.5f; // Oyuncuyla tutmak istediği ideal mesafe
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float minDistance = 5.0f; // Çok yaklaşılırsa geri çekilme mesafesi

        [Header("Görüş ve Algılama")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float detectionRange = 22f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float fieldOfView = 125f; // Geniş görüş açısı
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float proximityRange = 3.5f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float loseSightDistance = 26f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float loseSightDuration = 4.0f;

        [Header("Yol Bulma")]
        [Tooltip("NavMesh yol bulma ayarları.")] [SerializeField] private EnemyNavSettings navSettings = new EnemyNavSettings();
        [Tooltip("NavMesh üzerinde görüş ararken takibi sürdürme süresi (sn).")] [SerializeField] private float losSearchDuration = 6f;

        [Header("Menzilli Saldırı")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float attackRange = 20f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float lightProjectileSpeed = 16f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float projectileDamage = 18f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float lightWindup = 0.45f; // Ateş etmeden önce gözün parlayıp şarj olma süresi
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float lightCooldown = 1.6f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private Transform firePoint;

        // Retain the old prefab's serialized tuning verbatim; the shooter uses the new light/heavy settings.
        [HideInInspector, Tooltip("Preserved legacy body tint; presentation now belongs to FlyingEyeView.")] [SerializeField] private Color normalColor = Color.white;
        [HideInInspector, Tooltip("Preserved legacy charge tint; presentation now belongs to FlyingEyeView.")] [SerializeField] private Color windupGlowColor = new Color(1f, 0.15f, 0.9f);

        [Header("Shooter attacks")]
        [Tooltip("Presentation and authored sockets.")] [SerializeField] private FlyingEyeView view;
        [Tooltip("Light launches required before requesting a heavy.")] [SerializeField] private int lightsBeforeHeavy = 2;
        [Tooltip("Orbs reserved during Awake.")] [SerializeField] private int projectilePrewarmCount = 6;
        [Tooltip("Light collision radius in metres.")] [SerializeField] private float lightRadius = 0.35f;
        [Tooltip("Light visual diameter scale.")] [SerializeField] private float lightScale = 0.40f;
        [Tooltip("Light lifetime in seconds.")] [SerializeField] private float lightLifetime = 5f;
        [Tooltip("Fraction of intercept prediction for lights.")] [SerializeField] private float lightLead = 0.6f;
        [Tooltip("Maximum light prediction in seconds.")] [SerializeField] private float lightMaxLead = 0.6f;
        [Tooltip("Reflected light homing degrees per second.")] [SerializeField] private float lightReflectTurnRate = 360f;
        [Tooltip("Reflected light homing cone in degrees.")] [SerializeField] private float lightReflectAngle = 75f;
        [Tooltip("Light bolt tint.")] [SerializeField] private Color lightColor = new Color(0.85f, 0.15f, 0.95f);
        [Tooltip("Heavy windup in seconds.")] [SerializeField] private float heavyWindup = 1.1f;
        [Tooltip("Heavy speed in metres per second.")] [SerializeField] private float heavySpeed = 9f;
        [Tooltip("Heavy damage before elite scaling.")] [SerializeField] private float heavyDamage = 35f;
        [Tooltip("Heavy collision radius in metres.")] [SerializeField] private float heavyRadius = 0.6f;
        [Tooltip("Heavy visual diameter scale.")] [SerializeField] private float heavyScale = 0.9f;
        [Tooltip("Heavy lifetime in seconds.")] [SerializeField] private float heavyLifetime = 7f;
        [Tooltip("Fraction of intercept prediction for heavies.")] [SerializeField] private float heavyLead = 0.9f;
        [Tooltip("Maximum heavy prediction in seconds.")] [SerializeField] private float heavyMaxLead = 0.8f;
        [Tooltip("Reflected heavy homing degrees per second.")] [SerializeField] private float heavyReflectTurnRate = 360f;
        [Tooltip("Reflected heavy homing cone in degrees.")] [SerializeField] private float heavyReflectAngle = 180f;
        [Tooltip("Heavy orb tint.")] [SerializeField] private Color heavyColor = new Color(1f, 0.35f, 0.05f);
        [Tooltip("Cooldown after a heavy launch.")] [SerializeField] private float heavyCooldown = 2.2f;
        [Tooltip("Seconds before heavy launch when direction locks.")] [SerializeField] private float heavyAimLockSeconds = 0.15f;
        [Tooltip("Minimum cooldown after an interrupted windup.")] [SerializeField] private float cancelledAttackCooldown = 1f;
        [Tooltip("Aim offset above the player's position.")] [SerializeField] private float aimHeight = 1f;
        [Tooltip("Forward fire offset without a view or socket.")] [SerializeField] private float fallbackFireOffset = 0.65f;

        [Header("Evasion and reflected heavy punishment")]
        [Tooltip("Combat strafe fraction of fly speed.")] [SerializeField] private float combatStrafeFactor = 0.9f;
        [Tooltip("Post-launch tangent burst duration.")] [SerializeField] private float repositionDuration = 0.55f;
        [Tooltip("Post-launch burst speed multiplier.")] [SerializeField] private float repositionSpeedFactor = 2.4f;
        [Tooltip("Lowest random hover target above minimum height.")] [SerializeField] private float hoverTargetInset = 0.3f;
        [Tooltip("Non-stagger hit jink duration.")] [SerializeField] private float jinkDuration = 0.3f;
        [Tooltip("Non-stagger hit jink speed multiplier.")] [SerializeField] private float jinkSpeedFactor = 2f;
        [Tooltip("Minimum interval between hit jinks.")] [SerializeField] private float jinkCooldown = 1.2f;
        [Tooltip("Maximum-health damage fraction from an elite's reflected heavy.")] [SerializeField] private float eliteHeavyReflectDamageFraction = 0.45f;
        [Tooltip("Elite grounded stun duration in seconds.")] [SerializeField] private float groundedStunDuration = 3f;
        [Tooltip("Grounded height above the ground, within blade reach.")] [SerializeField] private float groundedHeight = 0.7f;
        [Tooltip("Extra attack recovery after stun.")] [SerializeField] private float stunRecoveryCooldown = 0.6f;
        [Tooltip("Damage beyond remaining health for a non-elite reflected heavy.")] [SerializeField] private float reflectedKillMargin = 1f;
        [Tooltip("Gibs from reflected heavy kills.")] [SerializeField] private int reflectedHeavyGibCount = 10;
        [Tooltip("Gibs from ordinary immediate gib kills.")] [SerializeField] private int normalGibCount = 6;
        [Tooltip("Damage required to stagger without a critical hit.")] [SerializeField] private float staggerDamageThreshold = 30f;
        [Tooltip("Stagger duration from damage.")] [SerializeField] private float damageStaggerDuration = 0.3f;
        [Tooltip("Damage converted to knockback speed.")] [SerializeField] private float hitKnockbackFactor = 0.12f;
        [Tooltip("Minimum damage knockback speed.")] [SerializeField] private float minHitKnockback = 2f;
        [Tooltip("Maximum damage knockback speed.")] [SerializeField] private float maxHitKnockback = 9f;
        [Tooltip("Noncritical hit flash duration.")] [SerializeField] private float hitFlashSeconds = 0.06f;
        [Tooltip("Critical hit flash duration.")] [SerializeField] private float criticalFlashSeconds = 0.12f;
        [Tooltip("Accepted stun flash tint.")] [SerializeField] private Color stunColor = new Color(1f, 0.9f, 0.2f);

        [Header("Sersemleme (Stun)")]
        [Tooltip("Stun knockback başlangıç hızı (m/s). 7/s sönümle ~3.5 m itme verir.")]
        [SerializeField] private float stunKnockbackSpeed = 24.5f;

        [Header("Devriye ve Sürü")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float patrolRadius = 7.0f;
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private float alertRadius = 12.0f;

        [Header("Görsel Efekt Renkleri")]
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private Color hitFlashColor = new Color(2.2f, 2.2f, 2.2f, 1f);
        [Tooltip("Flying Eye tuning or authored reference.")] [SerializeField] private Color criticalHitFlashColor = new Color(3.2f, 2.8f, 0.35f, 1f);

        [Header("Legacy movement, hit and death tuning")]
        [Tooltip("Seconds between late player discovery attempts.")] [SerializeField] private float playerSearchInterval = 0.5f;
        [Tooltip("Exponential knockback decay per second.")] [SerializeField] private float knockbackDecay = 7f;
        [Tooltip("Initial idle duration range in seconds.")] [SerializeField] private Vector2 initialIdleDuration = new Vector2(1f, 2.5f);
        [Tooltip("Idle duration range between patrol legs.")] [SerializeField] private Vector2 patrolIdleDuration = new Vector2(1.5f, 3f);
        [Tooltip("Patrol leg timeout range in seconds.")] [SerializeField] private Vector2 patrolDuration = new Vector2(4f, 7f);
        [Tooltip("Distance ending a patrol leg.")] [SerializeField] private float patrolArrivalDistance = 1f;
        [Tooltip("Patrol fraction of fly speed.")] [SerializeField] private float patrolSpeedFactor = 0.6f;
        [Tooltip("Idle duration after losing the player.")] [SerializeField] private float lostSightIdleDuration = 2f;
        [Tooltip("Retreat fraction of fly speed.")] [SerializeField] private float retreatSpeedFactor = 0.9f;
        [Tooltip("Extra distance beyond preferred range before approaching.")] [SerializeField] private float approachDistanceSlack = 2.5f;
        [Tooltip("Seconds between random combat strafe direction choices.")] [SerializeField] private Vector2 strafeDuration = new Vector2(1.2f, 2.4f);
        [Tooltip("Pupil and head hitbox damage multiplier.")] [SerializeField] private float criticalDamageMultiplier = 2f;
        [Tooltip("Ground ray start above the eye.")] [SerializeField] private float groundProbeOffset = 0.5f;
        [Tooltip("Ground ray maximum length.")] [SerializeField] private float groundProbeDistance = 25f;
        [Tooltip("Ground drop below last solid ground treated as void unless sustained.")] [SerializeField] private float voidDropThreshold = 2.5f;
        [Tooltip("Seconds a deeper ground hit must persist before accepting it.")] [SerializeField] private float acceptLowerAfter = 2.0f;
        [Tooltip("Fallback grace delay in seconds when BeginHunt is called without a profile.")] [SerializeField] private float defaultHuntStartDelay = 0.35f;
        [Tooltip("Altitude correction fraction of fly speed.")] [SerializeField] private float hoverSpeedFactor = 2f;
        [Tooltip("Line of sight origin forward from body.")] [SerializeField] private float sightForwardOffset = 0.5f;
        [Tooltip("Line of sight target above player position.")] [SerializeField] private float sightTargetHeight = 1.1f;
        [Tooltip("Falling corpse acceleration.")] [SerializeField] private float deathGravity = 16f;
        [Tooltip("Maximum corpse tumble duration before gibbing.")] [SerializeField] private float delayedGibTime = 0.55f;
        [Tooltip("Ground proximity triggering corpse gibs.")] [SerializeField] private float deathGroundOffset = 0.4f;
        [Tooltip("Gib count after a tumbling death.")] [SerializeField] private int delayedGibCount = 5;
        [Tooltip("Killing damage that always causes gibs.")] [SerializeField] private float gibDamageThreshold = 35f;
        [Tooltip("Health below zero that always causes gibs.")] [SerializeField] private float gibOverkillThreshold = 15f;
        [Tooltip("Ordinary kill probability of immediate gibs.")] [SerializeField] private float gibChance = 0.4f;
        [Tooltip("Minimum tumble angular velocity on each axis.")] [SerializeField] private Vector3 deathSpinMin = new Vector3(350f, 450f, 250f);
        [Tooltip("Maximum tumble angular velocity on each axis.")] [SerializeField] private Vector3 deathSpinMax = new Vector3(650f, 750f, 550f);
        [Tooltip("Corpse initial knockback speed.")] [SerializeField] private float deathKnockbackSpeed = 2.2f;
        [Tooltip("Corpse initial upward speed.")] [SerializeField] private float deathLiftSpeed = 1.4f;
        [Tooltip("Critical hitstop duration and time scale.")] [SerializeField] private Vector2 criticalHitstop = new Vector2(0.04f, 0.03f);
        [Tooltip("Stun hitstop duration and time scale.")] [SerializeField] private Vector2 stunHitstop = new Vector2(0.08f, 0.02f);
        [Tooltip("Death hitstop duration and time scale.")] [SerializeField] private Vector2 deathHitstop = new Vector2(0.07f, 0.03f);
        [Tooltip("Health orb reward tier without a dropper.")] [SerializeField] private int fallbackDropTier = 2;
        [Tooltip("Root scale pop duration on respawn.")] [SerializeField] private float respawnPopDuration = 0.22f;
        [Tooltip("Normalized time of the respawn scale peak.")] [SerializeField] private float respawnPopPeakTime = 0.7f;
        [Tooltip("Initial root scale multiplier during respawn.")] [SerializeField] private float respawnPopStartScale = 0.1f;
        [Tooltip("Peak root scale multiplier during respawn.")] [SerializeField] private float respawnPopPeakScale = 1.15f;

        private readonly RaycastHit[] _sightHits = new RaycastHit[32];
        private EnemyHealthDropper _healthDropper;
        private EnemyAmmoDropper _ammoDropper;
        private CharacterController _controller;
        private EnemyNavigator _nav;
        private bool _hadSightInCombat;
        private Transform _playerTransform;
        private float _playerSearchTimer;

        private FlyingEyeState _currentState = FlyingEyeState.Idle;
        private float _currentHealth;
        private bool _eliteBaseCached;
        private bool _isElite;
        private float _eliteSpeedMultiplier = 1f;
        private float _baseMaxHealth;
        private float _baseProjectileDamage;
        private float _baseFlySpeed;
        private float _baseLightCooldown;
        private float _baseHeavyCooldown;
        private float _baseHoverHeight;
        private float _baseMinFlyHeight;
        private float _baseMaxFlyHeight;
        private bool _isHunting;
        private EnemyAggressionProfile _huntProfile;
        private float _huntGraceTimer;
        private float _huntMoveSpeedMultiplier = 1f;
        private float _huntCooldownMultiplier = 1f;
        private float _eliteHealthMultiplier = 1f;
        private float _difficultyHealthMultiplier = 1f;
        private float _difficultySpeedMultiplier = 1f;
        private float _difficultyCooldownMultiplier = 1f;
        private float _lowerGroundTimer;
        private Vector3 _spawnPosition;
        private float _lastGroundY; // Son bilinen zemin yüksekliği (uçuş sınırları buna GÖRELİ uygulanır)
        private Quaternion _spawnRotation;
        private Vector3 _initialScale;
        private Vector3 _velocity;
        private Vector3 _knockbackVelocity;

        private Vector3 _patrolTarget;
        private Vector3 _lastKnownPlayerPos;
        private float _loseSightTimer = 0f;
        private float _strafeDirection = 1f;
        private float _strafeTimer = 0f;

        private float _stateTimer = 0f;
        private float _cooldownTimer = 0f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private Collider[] _colliders;
        private Collider[] _playerColliders;
        private Transform _playerCollidersOwner;
        private CharacterController _playerController;
        private FlyingEyeAttackPlanner _planner;
        private EnemyProjectileSettings _lightSettings;
        private EnemyProjectileSettings _heavySettings;
        private FlyingEyeAttack _pendingAttack;
        private bool _hasAttackToken;
        private bool _aimLocked;
        private Vector3 _lockedDirection;
        private Vector3 _lockedAimPoint;
        private float _windupDuration;
        private float _hoverTarget;
        private float _jinkTimer;
        private float _jinkCooldown;
        private bool _grounded;
        private bool _recoveringAltitude;
        private bool _reflectedHeavyKill;
        private float _baseHeavyDamage;
        // Ölüm ve Yeniden Doğuş Durum Değişkenleri
        private bool _isGibbed = false;
        private float _deathAnimTimer = 0f;
        private Vector3 _deathSpinSpeed = Vector3.zero;
        private Vector3 _deathFallVelocity = Vector3.zero;
        private float _respawnPopTimer = 0f;

        /// <summary>Current attack or movement phase.</summary>
        public FlyingEyeState CurrentState => _currentState;
        /// <inheritdoc />
        public bool IsStunned => _currentState == FlyingEyeState.Stagger;
        /// <inheritdoc />
        public uint StunSequence => _stunSequence;
        private uint _stunSequence;
        /// <summary>Remaining health after damage and elite scaling.</summary>
        public float CurrentHealth => _currentHealth;
        /// <summary>Maximum health for this spawn, including elite scaling.</summary>
        public float MaxHealth => maxHealth;
        /// <summary>Current world position for targeting and encounters.</summary>
        public Vector3 Position => transform.position;
        /// <summary>Whether the death phase has begun.</summary>
        public bool IsDead => _currentState == FlyingEyeState.Dead;

        /// <summary>Bu doğuşta elit güçlendirmesi uygulanıp uygulanmadığı.</summary>
        public bool IsElite => _isElite;

        /// <summary>Whether this eye is currently in arena hunt mode.</summary>
        public bool IsHunting => _isHunting;

        private void EnsureBaseStatsCached()
        {
            if (_eliteBaseCached) return;
            _baseMaxHealth = maxHealth;
            _baseProjectileDamage = projectileDamage;
            _baseHeavyDamage = heavyDamage;
            _baseFlySpeed = flySpeed;
            _baseLightCooldown = lightCooldown;
            _baseHeavyCooldown = heavyCooldown;
            _baseHoverHeight = hoverHeight;
            _baseMinFlyHeight = minFlyHeight;
            _baseMaxFlyHeight = maxFlyHeight;
            _eliteBaseCached = true;
        }

        /// <summary>Elit, av ve zorluk çarpanlarını temel değerlerle birleştirir (hasar hariç).</summary>
        private void RecomputeScaledStats()
        {
            if (!_eliteBaseCached) return;
            flySpeed = _baseFlySpeed * _eliteSpeedMultiplier * _huntMoveSpeedMultiplier * _difficultySpeedMultiplier;
            maxHealth = _baseMaxHealth * _eliteHealthMultiplier * _difficultyHealthMultiplier;
            float cooldown = _huntCooldownMultiplier * _difficultyCooldownMultiplier;
            lightCooldown = _baseLightCooldown * cooldown;
            heavyCooldown = _baseHeavyCooldown * cooldown;
        }

        /// <summary>Doğuşta zorluk çarpanlarını okur.</summary>
        private void RefreshDifficulty()
        {
            EnsureBaseStatsCached();
            DifficultyModifiers difficulty = GameDifficulty.Current;
            _difficultyHealthMultiplier = difficulty.EnemyHealth;
            _difficultySpeedMultiplier = difficulty.EnemySpeed;
            _difficultyCooldownMultiplier = difficulty.EnemyAttackCooldown;
            RecomputeScaledStats();
        }

        /// <summary>Temel değerleri saklayarak elit çarpanlarını doğuş başına bir kez uygular.</summary>
        public void ApplyEliteScaling(float damageMultiplier, float speedMultiplier, float healthMultiplier)
        {
            if (_isElite) return;
            EnsureBaseStatsCached();

            _eliteSpeedMultiplier = speedMultiplier;
            _eliteHealthMultiplier = healthMultiplier;
            projectileDamage = _baseProjectileDamage * damageMultiplier;
            heavyDamage = _baseHeavyDamage * damageMultiplier;
            RecomputeScaledStats();
            _currentHealth = maxHealth;
            _isElite = true;
            RebuildProjectileSettings();
        }

        private void ResetEliteScaling()
        {
            if (!_eliteBaseCached) return;
            _eliteSpeedMultiplier = 1f;
            _eliteHealthMultiplier = 1f;
            projectileDamage = _baseProjectileDamage;
            heavyDamage = _baseHeavyDamage;
            RecomputeScaledStats();
            _isElite = false;
            RebuildProjectileSettings();
        }

        /// <inheritdoc />
        public void BeginHunt(Transform target, EnemyAggressionProfile profile)
        {
            if (IsDead) return;
            EnsureBaseStatsCached();
            // Av başlangıcındaki bekleme süresi süren bir hazırlıkla atlanmasın.
            if (_currentState == FlyingEyeState.Windup) CancelWindup();

            _isHunting = true;
            _huntProfile = profile;
            _huntGraceTimer = profile != null ? profile.HuntStartDelay : defaultHuntStartDelay;
            _huntMoveSpeedMultiplier = profile != null ? profile.MoveSpeedMultiplier : 1f;

            _huntCooldownMultiplier = profile != null ? profile.AttackCooldownMultiplier : 1f;
            RecomputeScaledStats();

            if (profile != null)
            {
                hoverHeight = profile.EyeHoverHeight;
                minFlyHeight = profile.EyeMinFlyHeight;
                maxFlyHeight = profile.EyeMaxFlyHeight;
            }
            else
            {
                hoverHeight = _baseHoverHeight;
                minFlyHeight = _baseMinFlyHeight;
                maxFlyHeight = _baseMaxFlyHeight;
            }

            if (!_grounded) _hoverTarget = hoverHeight;

            if (target != null)
            {
                _playerTransform = target;
                target.TryGetComponent(out _playerController);
            }
            else if (_playerTransform == null)
            {
                FindPlayer();
            }

            if (_currentState == FlyingEyeState.Idle || _currentState == FlyingEyeState.Patrol)
            {
                StartCombat(false);
            }
            else if (_playerTransform != null)
            {
                _lastKnownPlayerPos = _playerTransform.position;
                _hadSightInCombat = true;
                _nav?.Invalidate();
            }
        }

        private void ClearHunt()
        {
            _isHunting = false;
            _huntProfile = null;
            _huntGraceTimer = 0f;
            _huntMoveSpeedMultiplier = 1f;
            _huntCooldownMultiplier = 1f;
            if (_eliteBaseCached)
            {
                RecomputeScaledStats();
                hoverHeight = _baseHoverHeight;
                minFlyHeight = _baseMinFlyHeight;
                maxFlyHeight = _baseMaxFlyHeight;
            }
        }

        // IEncounterEnemy Uygulaması
        /// <summary>Raised once when the current spawn dies.</summary>
        public event System.Action<IEncounterEnemy> OnEnemyDeath;
        /// <summary>Whether this spawn can participate in an encounter.</summary>
        public bool IsAlive => _currentState != FlyingEyeState.Dead;
        /// <summary>Root transform used by the encounter director.</summary>
        public Transform EnemyTransform => transform;
        /// <summary>Root object used for pooling.</summary>
        public GameObject EnemyGameObject => gameObject;

        private void OnEnable()
        {
            EnemyAlertHub.Register(this);
        }

        private void OnDisable()
        {
            CancelWindup();
            EnemyAlertHub.Unregister(this);
        }

        private void Awake()
        {
            EnsureBaseStatsCached();
            if (navSettings == null) navSettings = new EnemyNavSettings();
            _nav = new EnemyNavigator(new UnityNavMeshQuery(), navSettings, null);
            _controller = GetComponent<CharacterController>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _lastGroundY = _spawnPosition.y - hoverHeight;
            _initialScale = transform.localScale;
            RefreshDifficulty();
            _currentHealth = maxHealth;
            _patrolTarget = _spawnPosition;
            _lastKnownPlayerPos = _spawnPosition;

            if (view == null) view = GetComponent<FlyingEyeView>();
            if (view == null) Debug.LogWarning("FlyingEyeAI has no FlyingEyeView; presentation is disabled.", this);
            _planner = new FlyingEyeAttackPlanner(lightsBeforeHeavy);
            _hoverTarget = hoverHeight;
            RebuildProjectileSettings();
            EyeProjectilePool.Prewarm(projectilePrewarmCount);
            _healthDropper = GetComponent<EnemyHealthDropper>();
            _ammoDropper = GetComponent<EnemyAmmoDropper>();
            _colliders = GetComponentsInChildren<Collider>();

            FindPlayer();
            SetupHitboxes();
            CombatTargetLookup.RegisterHierarchy(this);

            _currentState = FlyingEyeState.Idle;
            _stateTimer = Random.Range(initialIdleDuration.x, initialIdleDuration.y);
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

        private void FindPlayer()
        {
            PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
            if (player == null) return;
            _playerTransform = player.transform;
            // Discovery alone caches components; aiming and firing never look them up.
            player.TryGetComponent(out _playerController);
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
                                  colName.IndexOf("pupil", System.StringComparison.OrdinalIgnoreCase) >= 0;

                Hitbox hb = col.GetComponent<Hitbox>();
                if (hb == null)
                {
                    hb = col.gameObject.AddComponent<Hitbox>();
                }
                hb.Initialize(this, isCritZone ? criticalDamageMultiplier : 1f, isCritZone);
            }
        }

        /// <summary>Sets the projectile origin for existing procedural setup callers.</summary>
        public void SetFirePoint(Transform fp)
        {
            firePoint = fp;
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_currentState == FlyingEyeState.Dead)
            {
                _stateTimer -= Time.deltaTime;

                if (!_isGibbed)
                {
                    _deathAnimTimer += Time.deltaTime;

                    // Havada sönerek yere düşme ve kontrolsüz dönme
                    _deathFallVelocity.y -= deathGravity * Time.deltaTime;
                    transform.position += _deathFallVelocity * Time.deltaTime;
                    transform.Rotate(_deathSpinSpeed * Time.deltaTime);

                    // 0.55 saniye sonra veya zemine çarptığında okült patlama ile parçalan
                    if (_deathAnimTimer >= delayedGibTime || transform.position.y <= _lastGroundY + deathGroundOffset)
                    {
                        _isGibbed = true;
                        if (CombatVFXManager.Instance != null)
                        {
                            CombatVFXManager.Instance.SpawnFleshGibs(view != null ? view.DeathFxPosition : transform.position, Vector3.up, delayedGibCount);
                            CombatVFXManager.Instance.SpawnHitImpact(transform.position, Vector3.up, false, true);
                        }
                        if (CombatAudio.Instance != null)
                        {
                            CombatAudio.Instance.PlayGibExplosion(transform.position);
                        }
                        if (view != null) view.SetVisible(false);
                        transform.localScale = _initialScale;
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

            UpdateTimers();
            if (view != null) view.Tick(Time.deltaTime);

            // Doğuş pop yaylanması
            if (_respawnPopTimer > 0f)
            {
                _respawnPopTimer -= Time.deltaTime;
                float popT = 1f - (_respawnPopTimer / Mathf.Max(respawnPopDuration, Mathf.Epsilon));
                float scaleMod = popT < respawnPopPeakTime ? Mathf.Lerp(respawnPopStartScale, respawnPopPeakScale, popT / Mathf.Max(respawnPopPeakTime, Mathf.Epsilon)) : Mathf.Lerp(respawnPopPeakScale, 1f, (popT - respawnPopPeakTime) / Mathf.Max(1f - respawnPopPeakTime, Mathf.Epsilon));
                transform.localScale = _initialScale * scaleMod;
            }
            else
            {
                transform.localScale = _initialScale;
            }

            if (_playerTransform == null)
            {
                // Oyuncudan önce doğan gözler: seyrek (0.5 sn) yeniden arama.
                _playerSearchTimer -= Time.deltaTime;
                if (_playerSearchTimer <= 0f)
                {
                    _playerSearchTimer = playerSearchInterval;
                    FindPlayer();
                }
            }
            if (_playerTransform == null && _currentState == FlyingEyeState.Windup)
            {
                CancelWindup();
                _currentState = FlyingEyeState.Combat;
            }
            if (_playerTransform != null) ExecuteStateMachine();
            else if (_currentState == FlyingEyeState.Stagger) ExecuteStagger();
            if (_currentState != FlyingEyeState.Windup) ApplyHoverAltitude();
            if (!_grounded && _currentState != FlyingEyeState.Windup && _knockbackVelocity.sqrMagnitude > 0.0001f)
            {
                _controller.Move(_knockbackVelocity * Time.deltaTime);
                _knockbackVelocity *= Mathf.Exp(-knockbackDecay * Time.deltaTime);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }
        }

        private void UpdateTimers()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_jinkCooldown > 0f) _jinkCooldown -= Time.deltaTime;
            if (_huntGraceTimer > 0f) _huntGraceTimer -= Time.deltaTime;
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

                case FlyingEyeState.Reposition:
                    ExecuteReposition();
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

            if (dist <= patrolArrivalDistance || _stateTimer <= 0f)
            {
                _currentState = FlyingEyeState.Idle;
                _stateTimer = Random.Range(patrolIdleDuration.x, patrolIdleDuration.y);
                _velocity.x = 0f;
                _velocity.z = 0f;
                return;
            }

            dirToDest.Normalize();
            LookAtPosition(transform.position + dirToDest);

            Vector3 move = dirToDest * (flySpeed * patrolSpeedFactor);
            _velocity.x = move.x;
            _velocity.z = move.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void PickNewPatrolDestination()
        {
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _spawnPosition + new Vector3(circle.x, 0f, circle.y);
            _currentState = FlyingEyeState.Patrol;
            _stateTimer = Random.Range(patrolDuration.x, patrolDuration.y);
        }

        private void StartCombat(bool alertAllies)
        {
            _currentState = FlyingEyeState.Combat;
            _loseSightTimer = loseSightDuration;
            _hadSightInCombat = true;
            _nav?.Invalidate();
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
            if (_huntGraceTimer > 0f)
            {
                LookAtPosition(_playerTransform.position + Vector3.up * aimHeight);
                _velocity.x = 0f;
                _velocity.z = 0f;
                return;
            }

            bool canSee = CanSeePlayer();
            if (canSee)
            {
                _hadSightInCombat = true;
                _loseSightTimer = loseSightDuration;
                _lastKnownPlayerPos = _playerTransform.position;

                LookAtPosition(_playerTransform.position + Vector3.up * aimHeight);

                if (_jinkTimer > 0f)
                {
                    _jinkTimer -= Time.deltaTime;
                    MoveTangent(jinkSpeedFactor);
                    return;
                }

                // 1. Saldırı Hazırlığı Kontrolü
                if (distToPlayer <= attackRange && _cooldownTimer <= 0f)
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
                    moveDir = -dirToPlayer * (flySpeed * retreatSpeedFactor);
                }
                // Çok uzaksa yaklaş
                else if (distToPlayer > preferredDistance + approachDistanceSlack)
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
                        _strafeTimer = Random.Range(strafeDuration.x, strafeDuration.y);
                    }

                    Vector3 strafeVector = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
                    moveDir = strafeVector * (flySpeed * combatStrafeFactor);
                }

                _velocity.x = moveDir.x;
                _velocity.z = moveDir.z;
                _controller.Move(_velocity * Time.deltaTime);
                return;
            }

            // Yolu izle, görüş ara
            bool lostSightThisFrame = _hadSightInCombat;
            if (lostSightThisFrame)
            {
                _hadSightInCombat = false;
                _nav.Invalidate();
            }

            Vector3 pos = transform.position;
            Vector3 groundSelf = new Vector3(pos.x, _lastGroundY, pos.z);
            Vector3 steer = _nav.Tick(Time.deltaTime, groundSelf, _playerTransform.position, false);

            if (lostSightThisFrame && _nav.Status != NavPathStatus.NoNavMesh)
            {
                _loseSightTimer = losSearchDuration;
            }

            if (!_isHunting)
            {
                _loseSightTimer -= Time.deltaTime;
                if (_loseSightTimer <= 0f || distToPlayer > loseSightDistance)
                {
                    _currentState = FlyingEyeState.Idle;
                    _stateTimer = lostSightIdleDuration;
                    _velocity.x = 0f;
                    _velocity.z = 0f;
                    return;
                }
            }

            Vector3 toSteer = steer - groundSelf;
            toSteer.y = 0f;
            if (_nav.Status != NavPathStatus.NoNavMesh && toSteer.sqrMagnitude > 0.05f * 0.05f)
            {
                LookAtPosition(new Vector3(steer.x, pos.y, steer.z));
            }
            else
            {
                LookAtPosition(_lastKnownPlayerPos);
            }

            if (_jinkTimer > 0f)
            {
                _jinkTimer -= Time.deltaTime;
                MoveTangent(jinkSpeedFactor);
                return;
            }

            float navSpeed = Time.deltaTime > 0f ? Mathf.Min(flySpeed, toSteer.magnitude / Time.deltaTime) : 0f;
            Vector3 navMove = toSteer.sqrMagnitude > 0.05f * 0.05f ? toSteer.normalized * navSpeed : Vector3.zero;
            _velocity.x = navMove.x;
            _velocity.z = navMove.z;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private Vector3 FirePosition => firePoint != null ? firePoint.position :
            view != null ? view.FirePosition : transform.position + transform.forward * fallbackFireOffset;

        private void RebuildProjectileSettings()
        {
            _lightSettings = new EnemyProjectileSettings
            {
                Speed = lightProjectileSpeed, Damage = projectileDamage, CollisionRadius = lightRadius,
                VisualScale = lightScale, Lifetime = lightLifetime, Heavy = false,
                ReflectHomingDegPerSec = lightReflectTurnRate, ReflectHomingMaxAngle = lightReflectAngle,
                Color = lightColor
            };
            _heavySettings = new EnemyProjectileSettings
            {
                Speed = heavySpeed, Damage = heavyDamage, CollisionRadius = heavyRadius,
                VisualScale = heavyScale, Lifetime = heavyLifetime, Heavy = true,
                ReflectHomingDegPerSec = heavyReflectTurnRate, ReflectHomingMaxAngle = heavyReflectAngle,
                Color = heavyColor
            };
        }

        private void ReleaseToken()
        {
            if (!_hasAttackToken) return;
            _hasAttackToken = false;
            EnemyAttackTokens.Release(AttackTokenChannel.RangedHeavy);
        }

        private void CancelWindup()
        {
            if (_currentState == FlyingEyeState.Windup)
            {
                _cooldownTimer = Mathf.Max(_cooldownTimer, cancelledAttackCooldown);
                _currentState = FlyingEyeState.Combat;
                _stateTimer = 0f;
            }
            ReleaseToken();
            _aimLocked = false;
            if (view != null) view.HideWindup();
        }

        private void StartWindup()
        {
            int heavyLimit = _huntProfile != null ? _huntProfile.RangedHeavyTokenLimit : 0;
            _hasAttackToken = _planner.WantsHeavy && EnemyAttackTokens.TryAcquire(AttackTokenChannel.RangedHeavy, heavyLimit);
            _pendingAttack = _hasAttackToken ? FlyingEyeAttack.Heavy : FlyingEyeAttack.Light;
            // Commit only at launch, so cancelled heavies remain wanted.
            _windupDuration = _pendingAttack == FlyingEyeAttack.Heavy ? heavyWindup : lightWindup;
            _stateTimer = _windupDuration;
            _currentState = FlyingEyeState.Windup;
            _velocity = Vector3.zero;
            _knockbackVelocity = Vector3.zero;
            _aimLocked = false;
            _jinkTimer = 0f;
            UpdateWindupView();
        }

        private Vector3 PredictDirection(Vector3 origin)
        {
            bool heavy = _pendingAttack == FlyingEyeAttack.Heavy;
            Vector3 velocity = _playerController != null ? _playerController.velocity : Vector3.zero;
            return ProjectileLead.Direction(origin, _playerTransform.position + Vector3.up * aimHeight,
                velocity, heavy ? heavySpeed : lightProjectileSpeed,
                heavy ? heavyLead : lightLead, heavy ? heavyMaxLead : lightMaxLead);
        }

        private void UpdateWindupView()
        {
            Vector3 origin = FirePosition;
            Vector3 direction = _aimLocked ? _lockedDirection : PredictDirection(origin);
            Vector3 aimPoint = _aimLocked ? _lockedAimPoint :
                origin + direction * Vector3.Distance(origin, _playerTransform.position + Vector3.up * aimHeight);
            if (view != null)
                view.ShowWindup(_pendingAttack, 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(_windupDuration, Mathf.Epsilon)),
                    aimPoint, _aimLocked);
        }

        private void ExecuteWindup()
        {
            LookAtPosition(_playerTransform.position + Vector3.up * aimHeight);
            _stateTimer -= Time.deltaTime;
            if (_pendingAttack == FlyingEyeAttack.Heavy && !_aimLocked && _stateTimer <= heavyAimLockSeconds)
            {
                _lockedDirection = PredictDirection(FirePosition);
                _lockedAimPoint = FirePosition + _lockedDirection *
                    Vector3.Distance(FirePosition, _playerTransform.position + Vector3.up * aimHeight);
                _aimLocked = true;
            }
            UpdateWindupView();
            if (_stateTimer <= 0f) LaunchProjectile();
        }

        private void LaunchProjectile()
        {
            Vector3 origin = FirePosition;
            Vector3 direction = _aimLocked ? _lockedDirection : PredictDirection(origin);
            FlyingEyeAttack attack = _planner.Next(_hasAttackToken);
            if (attack == FlyingEyeAttack.Light) ReleaseToken();
            if (attack == FlyingEyeAttack.Heavy)
                EyeProjectilePool.Launch(transform, origin, direction, in _heavySettings);
            else
                EyeProjectilePool.Launch(transform, origin, direction, in _lightSettings);
            ReleaseToken();
            if (view != null) view.HideWindup();
            _cooldownTimer = attack == FlyingEyeAttack.Heavy ? heavyCooldown : lightCooldown;
            EnemyAlertHub.BroadcastAlert(this, transform.position, _playerTransform, alertRadius);
            _currentState = FlyingEyeState.Reposition;
            _stateTimer = repositionDuration;
            _strafeDirection = Random.value > 0.5f ? 1f : -1f;
            _hoverTarget = Random.Range(Mathf.Min(minFlyHeight + hoverTargetInset, maxFlyHeight), maxFlyHeight);
        }

        private void MoveTangent(float speedFactor)
        {
            Vector3 radial = _playerTransform.position - transform.position;
            radial.y = 0f;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized) * _strafeDirection;
            CollisionFlags flags = _controller.Move(tangent * (flySpeed * speedFactor * Time.deltaTime));
            if ((flags & CollisionFlags.Sides) != 0) _strafeDirection = -_strafeDirection;
        }

        private void ExecuteReposition()
        {
            LookAtPosition(_playerTransform.position + Vector3.up * aimHeight);
            MoveTangent(repositionSpeedFactor);
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f) _currentState = FlyingEyeState.Combat;
        }

        private void ExecuteStagger()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;
            if (_grounded)
            {
                _hoverTarget = hoverHeight;
                _recoveringAltitude = true;
            }
            _grounded = false;
            if (view != null) view.SetGrounded(false);
            _currentState = FlyingEyeState.Combat;
        }
        private bool IsOwnCollider(Collider col)
        {
            if (col == null) return false;
            if (col == _controller) return true;
            if (_colliders == null) return false;
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] == col) return true;
            }
            return false;
        }

        private void ApplyHoverAltitude()
        {
            // Zemin seviyesinde doğan göz de zemini bulabilsin; kendi collider'ları aşağıda elenir.
            Vector3 rayOrigin = transform.position + Vector3.up * groundProbeOffset;
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _groundHits, groundProbeDistance, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);

            bool hasHit = false;
            float hitY = 0f;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                // Kendisine veya kendi çocuk parçalarına çarpışmayı yok say (hit.transform yerine cache'li collider karşılaştırması)
                if (IsOwnCollider(_groundHits[i].collider))
                    continue;

                if (_groundHits[i].distance < bestDist)
                {
                    bestDist = _groundHits[i].distance;
                    hitY = _groundHits[i].point.y;
                    hasHit = true;
                }
            }

            // Zemin bulunamazsa veya boşluk eşiği aşılırsa son bilinen zemini koru
            float groundY = FlyingEyeAltitude.ResolveGround(
                _lastGroundY, hasHit, hitY,
                voidDropThreshold, acceptLowerAfter,
                Time.deltaTime, ref _lowerGroundTimer);
            _lastGroundY = groundY;

            // Hedef Y yüksekliği: Zemin + hoverHeight + dalgalanma. min/maxFlyHeight ZEMİNE GÖRELİDİR (mutlak dünya Y'si değil).
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            float targetY = groundY + Mathf.Clamp(_hoverTarget + (_grounded ? 0f : bob), _grounded ? groundedHeight : minFlyHeight, maxFlyHeight);

            float currentY = transform.position.y;
            float newY = Mathf.MoveTowards(currentY, targetY, flySpeed * Time.deltaTime * hoverSpeedFactor);
            // Recover upward smoothly instead of snapping to the minimum flight height.
            float lowerHeight = _grounded || _recoveringAltitude ? groundedHeight : minFlyHeight;
            newY = Mathf.Clamp(newY, groundY + lowerHeight, groundY + maxFlyHeight);
            if (_recoveringAltitude && newY >= groundY + minFlyHeight) _recoveringAltitude = false;

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

        /// <summary>Tests detection distance, field of view and the existing enemy sight mask.</summary>
        public bool CanSeePlayer()
        {
            if (_playerTransform == null) return false;

            Vector3 eyePos = transform.position + transform.forward * sightForwardOffset;
            Vector3 targetPos = _playerTransform.position + Vector3.up * sightTargetHeight;
            Vector3 toPlayer = targetPos - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > detectionRange) return false;

            if (dist <= proximityRange)
            {
                if (CombatRaycast.Nearest(eyePos, toPlayer.normalized, dist, GameLayers.EnemySightMask, _sightHits, out RaycastHit proxHit))
                {
                    if (IsPlayerCollider(proxHit.collider))
                    {
                        return true;
                    }
                }
            }

            Vector3 dirToPlayer = toPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > fieldOfView * 0.5f) return false;

            if (CombatRaycast.Nearest(eyePos, dirToPlayer, dist, GameLayers.EnemySightMask, _sightHits, out RaycastHit hit))
            {
                if (IsPlayerCollider(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        // RaycastHit.transform allocates a managed wrapper per call; compare colliders by reference instead.
        private bool IsPlayerCollider(Collider candidate)
        {
            if (candidate == null || _playerTransform == null) return false;
            if (!ReferenceEquals(_playerCollidersOwner, _playerTransform) || _playerColliders == null)
            {
                _playerCollidersOwner = _playerTransform;
                _playerColliders = _playerTransform.GetComponentsInChildren<Collider>(true);
            }
            for (int i = 0; i < _playerColliders.Length; i++)
            {
                if (ReferenceEquals(_playerColliders[i], candidate)) return true;
            }
            return false;
        }

        /// <summary>Accepts an allied alert while idle or patrolling.</summary>
        public void ReceiveAlert(Transform target)
        {
            if (_currentState == FlyingEyeState.Idle || _currentState == FlyingEyeState.Patrol)
            {
                _playerTransform = target;
                // An alert can discover a player before the periodic search does.
                if (target != null) target.TryGetComponent(out _playerController);
                StartCombat(false);
            }
        }

        /// <summary>Applies damage, hit reactions, interruptions and death rewards.</summary>
        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (_currentState == FlyingEyeState.Dead) return;

            _currentHealth -= amount;
            if (view != null) view.Flash(isCritical ? criticalHitFlashColor : hitFlashColor, isCritical ? criticalFlashSeconds : hitFlashSeconds);

            bool wasStaggered = _currentState == FlyingEyeState.Stagger;
            bool wasCombat = _currentState == FlyingEyeState.Combat;
            if (_currentState == FlyingEyeState.Idle || _currentState == FlyingEyeState.Patrol) StartCombat(true);

            // Geri tepme sarsıntısı
            if (!_grounded) _knockbackVelocity = -hitNormal.normalized * Mathf.Clamp(amount * hitKnockbackFactor, minHitKnockback, maxHitKnockback);

            if (isCritical || amount >= staggerDamageThreshold)
            {
                CancelWindup();
                _jinkTimer = 0f;
                _currentState = FlyingEyeState.Stagger;
                _stateTimer = wasStaggered ? Mathf.Max(_stateTimer, damageStaggerDuration) : damageStaggerDuration;
                if (!wasStaggered) _stunSequence++;
            }

            if (wasCombat && _currentState == FlyingEyeState.Combat && _jinkCooldown <= 0f)
            {
                _jinkTimer = jinkDuration;
                _jinkCooldown = jinkCooldown;
                _strafeDirection = Random.value > 0.5f ? 1f : -1f;
            }
            if (HitstopManager.Instance != null)
            {
                if (_currentHealth > 0f && isCritical)
                    HitstopManager.Instance.TriggerHitstop(criticalHitstop.x, criticalHitstop.y);
            }

            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, isCritical, true);
            if (CombatAudio.Instance != null)
                CombatAudio.Instance.PlayFleshImpact(hitPoint, isCritical);
            if (_currentHealth <= 0f)
                Die(hitPoint, hitNormal, amount, isCritical);
        }

        /// <summary>Cancels any attack and accepts a timed stun with decaying knockback.</summary>
        public void Stun(float duration, Vector3 knockbackDir)
        {
            if (_currentState == FlyingEyeState.Dead) return;

            CancelWindup();
            _jinkTimer = 0f;
            _currentState = FlyingEyeState.Stagger;
            _stateTimer = _grounded ? Mathf.Max(_stateTimer, duration) : duration;
            _stunSequence++;
            _cooldownTimer = Mathf.Max(_cooldownTimer, duration + stunRecoveryCooldown);
            // knockbackDir bir yöndür; toplam kayma ~ stunKnockbackSpeed / KnockbackDecay (24.5 / 7 = 3.5 m).
            _knockbackVelocity = !_grounded && knockbackDir.sqrMagnitude > 0.0001f
                ? knockbackDir.normalized * stunKnockbackSpeed
                : Vector3.zero;

            if (view != null) view.Flash(stunColor, duration);

            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(stunHitstop.x, stunHitstop.y);
            }

        }

        private void Die(Vector3 hitPoint, Vector3 hitNormal, float killingDamage, bool isCritical)
        {
            if (!IsAlive) return;
            CancelWindup();
            ClearHunt();
            _grounded = false;
            _recoveringAltitude = false;
            if (view != null) view.SetGrounded(false);
            _currentState = FlyingEyeState.Dead;
            _stateTimer = respawnTime;
            if (view != null) view.HideWindup();

            // İnfaz öldürmesi = yakın dövüş silahından gelen son darbe (hasar miktarı değil, kaynak belirler)
            bool isMeleeKill = MeleeDamageScope.IsActive;

            // Can Küresi Düşüşü (Uçan Düşman - Tier 2 Ödülü)
            EnemyHealthDropper dropper = _healthDropper;
            if (dropper != null)
            {
                dropper.TriggerDrop(isCritical, isMeleeKill);
            }
            else if (HealthOrbManager.Instance != null)
            {
                HealthOrbManager.Instance.TryDropOrbs(transform.position, fallbackDropTier, isCritical, isMeleeKill);
            }

            // Cephane Düşüşü (DOOM Kaynak Döngüsü)
            EnemyAmmoDropper ammoDropper = _ammoDropper;
            if (ammoDropper != null)
            {
                ammoDropper.TriggerDrop(isCritical, isMeleeKill);
            }

            // 1. Çarpışma kutularını anında kapat
            SetCollidersActive(false);

            // 2. Parçalanma (Gibs) Kontrolü: Gözbebeği kritik vuruşu veya ağır hasar
            bool shouldGib = _reflectedHeavyKill || isCritical || killingDamage >= gibDamageThreshold || _currentHealth <= -gibOverkillThreshold || Random.value < gibChance;

            if (shouldGib)
            {
                _isGibbed = true;

                if (CombatVFXManager.Instance != null)
                {
                    Vector3 blastDir = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : transform.forward;
                    CombatVFXManager.Instance.SpawnFleshGibs(view != null ? view.DeathFxPosition : transform.position, blastDir, _reflectedHeavyKill ? reflectedHeavyGibCount : normalGibCount);
                    CombatVFXManager.Instance.SpawnHitImpact(transform.position, hitNormal, true, false);
                    if (_reflectedHeavyKill) CombatVFXManager.Instance.SpawnOccultEnergyBurst(view != null ? view.DeathFxPosition : transform.position, true);
                }

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayGibExplosion(transform.position);
                }

                if (view != null) view.SetVisible(false);
                transform.localScale = _initialScale;

            }
            else
            {
                _isGibbed = false;
                _deathAnimTimer = 0f;
                _deathSpinSpeed = new Vector3(Random.Range(deathSpinMin.x, deathSpinMax.x), Random.Range(deathSpinMin.y, deathSpinMax.y), Random.Range(deathSpinMin.z, deathSpinMax.z));
                Vector3 knockback = hitNormal.sqrMagnitude > 0.01f ? -hitNormal : -transform.forward;
                _deathFallVelocity = knockback * deathKnockbackSpeed + Vector3.up * deathLiftSpeed;

            }
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(deathHitstop.x, deathHitstop.y);
            CombatEvents.RaiseEnemyKilled(transform.position, isCritical, _isGibbed);
            OnEnemyDeath?.Invoke(this);
        }

        private void Respawn()
        {
            CancelWindup();
            ClearHunt();
            ResetEliteScaling();
            RefreshDifficulty();
            _planner.Reset();
            _grounded = false;
            _recoveringAltitude = false;
            _reflectedHeavyKill = false;
            _aimLocked = false;
            _hoverTarget = hoverHeight;
            _cooldownTimer = 0f;
            _jinkTimer = 0f;
            _jinkCooldown = 0f;
            _loseSightTimer = 0f;
            _lowerGroundTimer = 0f;
            _hadSightInCombat = false;
            _nav?.Invalidate();
            _strafeTimer = 0f;
            _stateTimer = 0f;
            _lastGroundY = _spawnPosition.y - hoverHeight;
            if (view != null) view.SetGrounded(false);
            if (_controller != null) _controller.enabled = false;
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            if (_controller != null) _controller.enabled = true;

            transform.localScale = _initialScale;
            _deathAnimTimer = 0f;
            _isGibbed = false;
            _deathFallVelocity = Vector3.zero;

            _currentHealth = maxHealth;
            _currentState = FlyingEyeState.Idle;
            _velocity = Vector3.zero;
            _knockbackVelocity = Vector3.zero;
            if (view != null) view.HideWindup();

            if (view != null) view.SetVisible(true);
            SetCollidersActive(true);
            _respawnPopTimer = respawnPopDuration;

            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnHitImpact(transform.position, Vector3.up, false, false);
            }

        }

        /// <summary>
        /// WaveSpawnDirector tarafından havuzdan çağrıldığında düşmanı hazır konuma getirir.
        /// </summary>
        public void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            autoRespawn = false;
            ClearHunt();
            _lowerGroundTimer = 0f;
            _spawnPosition = spawnPosition;
            _spawnRotation = spawnRotation;
            _patrolTarget = spawnPosition;
            _lastKnownPlayerPos = spawnPosition;
            _lastGroundY = spawnPosition.y - hoverHeight;

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
            _nav?.Invalidate();
            StartCombat(true);
        }

        /// <summary>
        /// WaveSpawnDirector tarafından dalga bittiğinde veya havuz temizliğinde çağrılır.
        /// </summary>
        public void OnDespawnToDirector()
        {
            CancelWindup();
            ClearHunt();
            autoRespawn = false;
            _currentState = FlyingEyeState.Dead;
            gameObject.SetActive(false);
        }

        /// <inheritdoc />
        public void OnOwnProjectileReflectedHit(bool heavy, float damage, Vector3 point, Vector3 normal)
        {
            if (IsDead) return;
            if (!heavy)
            {
                TakeDamage(damage, point, normal, true);
                return;
            }

            uint previousStunSequence = _stunSequence;
            _reflectedHeavyKill = true;
            TakeDamage(_isElite ? eliteHeavyReflectDamageFraction * maxHealth :
                Mathf.Max(damage, _currentHealth + reflectedKillMargin), point, normal, true);
            if (IsDead) return;
            _reflectedHeavyKill = false;
            CancelWindup();
            _currentState = FlyingEyeState.Stagger;
            _grounded = true;
            _recoveringAltitude = false;
            _stateTimer = groundedStunDuration;
            // The damage and its grounded punishment represent one accepted stun.
            _stunSequence = previousStunSequence + 1;
            _hoverTarget = groundedHeight;
            _velocity = Vector3.zero;
            _knockbackVelocity = Vector3.zero;
            _jinkTimer = 0f;
            _cooldownTimer = groundedStunDuration + stunRecoveryCooldown;
            if (view != null) view.SetGrounded(true);
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = transform.position + transform.forward * sightForwardOffset;
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

            if (_nav != null && _nav.CornerCount > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < _nav.CornerCount - 1; i++)
                {
                    Gizmos.DrawLine(_nav.GetCorner(i), _nav.GetCorner(i + 1));
                }
            }
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
