using System.Collections.Generic;
using RetroFPS.Combat;
using RetroFPS.Pause;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroFPS
{
    /// <summary>Enemy projectile with swept collision, parrying and optional pooled Eye-orb settings.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField, Tooltip("Travel speed in metres per second.")] private float speed = 14f;
        [SerializeField, Tooltip("Damage before reflection multipliers.")] private float damage = 18f;
        [SerializeField, Tooltip("Flight lifetime in seconds.")] private float lifetime = 5f;
        [SerializeField, Tooltip("Swept collision radius in metres.")] private float collisionRadius = 0.35f;
        [SerializeField, Tooltip("Uniform scale supplied by a pooled launch.")] private float visualScale = 0.40f;
        [SerializeField, Tooltip("Whether this launch is a heavy orb.")] private bool heavy;
        [SerializeField, Tooltip("Reflected homing turn rate in degrees per second.")] private float reflectHomingDegPerSec;
        [SerializeField, Tooltip("Maximum angle to the shooter permitting homing; 180 permits all directions.")]
        private float reflectHomingMaxAngle = 180f;

        [Header("Appearance")]
        [SerializeField, Tooltip("Fallback scale for legacy projectiles with zero local scale.")] private float fallbackScale = 0.40f;
        [SerializeField, Tooltip("Scale of a reflected light orb or legacy sword.")] private float deflectedScale = 0.55f;
        [SerializeField, Tooltip("Scale multiplier for a reflected heavy orb.")] private float heavyDeflectScaleMultiplier = 1.15f;
        [SerializeField, Tooltip("Orb pulse angular frequency.")] private float pulseFrequency = 18f;
        [SerializeField, Tooltip("Orb pulse scale amplitude.")] private float pulseAmplitude = 0.12f;
        [SerializeField, Tooltip("Reflected orb tint.")] private Color deflectedColor = new Color(0.15f, 0.95f, 1f);
        [SerializeField, Tooltip("Unreflected trail duration in seconds.")] private float trailTime = 0.22f;
        [SerializeField, Tooltip("Unreflected trail starting width.")] private float trailStartWidth = 0.28f;
        [SerializeField, Tooltip("Trail ending width.")] private float trailEndWidth = 0.02f;
        [SerializeField, Tooltip("Minimum distance between trail vertices.")] private float trailMinVertexDistance = 0.05f;
        [SerializeField, Tooltip("Reflected trail duration in seconds.")] private float deflectedTrailTime = 0.32f;
        [SerializeField, Tooltip("Reflected trail starting width.")] private float deflectedTrailStartWidth = 0.36f;

        [Header("Reflected hit feedback")]
        [SerializeField, Tooltip("Reflected impact hitstop duration in seconds.")] private float reflectedHitstopDuration = 0.08f;
        [SerializeField, Tooltip("Reflected impact hitstop time scale.")] private float reflectedHitstopTimeScale = 0.02f;
        [SerializeField, Tooltip("Guard break duration caused by a reflected heavy orb.")]
        private float reflectedGuardBreakDuration = 3f;

        private static readonly List<EnemyProjectile> _activeProjectiles = new List<EnemyProjectile>(128);
        private static PlayerParryController[] _playerParries;
        private static readonly Dictionary<Transform, ShooterBinding> ShooterBindings = new Dictionary<Transform, ShooterBinding>(128);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Gradient NormalTrailGradient = CreateTrailGradient(false);
        private static readonly Gradient DeflectedTrailGradient = CreateTrailGradient(true);

        private Transform _shooter;
        private Transform _originalShooter;
        private IEncounterEnemy _originalEnemy;
        private IReflectPunishable _reflectPunishable;
        private float _age;
        private bool _hasImpacted;
        private bool _isDeflected;
        private bool _isPooled;
        private Renderer _renderer;
        private TrailRenderer _trail;
        private MaterialPropertyBlock _properties;
        private Vector3 _baseScale;
        private readonly RaycastHit[] _collisionHits = new RaycastHit[32];
        private readonly Collider[] _initialOverlaps = new Collider[32];

        private readonly struct ShooterBinding
        {
            internal IEncounterEnemy Enemy { get; }
            internal IReflectPunishable Punishable { get; }

            internal ShooterBinding(IEncounterEnemy enemy, IReflectPunishable punishable)
            {
                Enemy = enemy;
                Punishable = punishable;
            }

            internal bool HasDestroyedReference =>
                (Enemy is Object enemy && enemy == null) || (Punishable is Object punishable && punishable == null);
        }

        /// <summary>Currently enabled projectiles, shared with the player's parry scan.</summary>
        public static List<EnemyProjectile> ActiveProjectiles => _activeProjectiles;
        /// <summary>Whether this projectile has been reflected.</summary>
        public bool IsDeflected => _isDeflected;
        /// <summary>Current damage including any reflection multiplier.</summary>
        public float Damage => damage;
        /// <summary>Whether this launch is a heavy attack.</summary>
        public bool IsHeavy => heavy;
        /// <summary>Original shooter, retained after reflection.</summary>
        public Transform OriginalShooter => _originalShooter;

        /// <summary>Raised before damage when a reflected projectile impacts a collider.</summary>
        public static event System.Action<EnemyProjectile, Collider> OnDeflectedImpact;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDeflectedImpactSubscribers()
        {
            OnDeflectedImpact = null;
            _activeProjectiles.Clear();
            _playerParries = null;
            ShooterBindings.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) ShooterBindings.Clear();
            CacheSceneShooters();
        }

        // Initialization only: also covers disabled enemies before their first pooled launch.
        internal static void WarmLaunchCaches(int capacity)
        {
            if (_activeProjectiles.Capacity < capacity) _activeProjectiles.Capacity = capacity;
            RefreshParryCache();
            CacheSceneShooters();
        }

        private static void CacheSceneShooters()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                IEncounterEnemy enemy = behaviour as IEncounterEnemy;
                IReflectPunishable punishable = behaviour as IReflectPunishable;
                if (enemy == null && punishable == null) continue;
                Transform owner = behaviour.transform;
                ShooterBindings.TryGetValue(owner, out ShooterBinding previous);
                ShooterBindings[owner] = new ShooterBinding(enemy ?? previous.Enemy, punishable ?? previous.Punishable);
            }
        }

        private static void RefreshParryCache()
        {
            bool refresh = _playerParries == null || _playerParries.Length == 0;
            if (!refresh)
            {
                for (int i = 0; i < _playerParries.Length; i++)
                {
                    if (_playerParries[i] != null) continue;
                    refresh = true;
                    break;
                }
            }
            if (refresh) _playerParries = FindObjectsByType<PlayerParryController>(FindObjectsInactive.Exclude);
        }

        private void OnEnable()
        {
            if (!_activeProjectiles.Contains(this)) _activeProjectiles.Add(this);
        }

        private void OnDisable() => _activeProjectiles.Remove(this);

        /// <summary>Initializes a legacy projectile without reflected homing or pooling.</summary>
        public void Initialize(Transform shooter, float projectileSpeed, float projectileDamage)
        {
            speed = projectileSpeed;
            damage = projectileDamage;
            heavy = false;
            reflectHomingDegPerSec = 0f;
            _baseScale = transform.localScale != Vector3.zero ? transform.localScale : Vector3.one * fallbackScale;
            CacheVisuals();
            BeginLaunch(shooter);
        }

        internal void PrepareForPool(Renderer orbRenderer, TrailRenderer trail)
        {
            _isPooled = true;
            _renderer = orbRenderer;
            _trail = trail;
            _properties = new MaterialPropertyBlock();
            SetupTrail();
        }

        internal void InitializePooled(Transform shooter, in EnemyProjectileSettings settings)
        {
            speed = settings.Speed;
            damage = settings.Damage;
            collisionRadius = settings.CollisionRadius;
            lifetime = settings.Lifetime;
            visualScale = settings.VisualScale;
            heavy = settings.Heavy;
            reflectHomingDegPerSec = settings.ReflectHomingDegPerSec;
            reflectHomingMaxAngle = settings.ReflectHomingMaxAngle;
            _baseScale = Vector3.one * visualScale;
            transform.localScale = _baseScale;
            BeginLaunch(shooter);
            SetTint(settings.Color);
        }

        private void BeginLaunch(Transform shooter)
        {
            _shooter = _originalShooter = shooter;
            _originalEnemy = null;
            _reflectPunishable = null;
            if (shooter != null)
            {
                Transform owner = shooter;
                while (owner != null)
                {
                    if (ShooterBindings.TryGetValue(owner, out ShooterBinding binding) && !binding.HasDestroyedReference)
                    {
                        _originalEnemy = _originalEnemy ?? binding.Enemy;
                        _reflectPunishable = _reflectPunishable ?? binding.Punishable;
                    }
                    owner = owner.parent;
                }
                // Legacy callers and newly instantiated shooters may precede scene/prewarm registration.
                // Resolve each new shooter once; subsequent launches use cached interfaces.
                if (!ShooterBindings.ContainsKey(shooter))
                {
                    _originalEnemy = _originalEnemy ?? shooter.GetComponentInParent<IEncounterEnemy>();
                    _reflectPunishable = _reflectPunishable ?? shooter.GetComponentInParent<IReflectPunishable>();
                    ShooterBindings[shooter] = new ShooterBinding(_originalEnemy, _reflectPunishable);
                }
            }
            _age = 0f;
            _hasImpacted = false;
            _isDeflected = false;
            RefreshParryCache();
            SetupTrail();
            _trail.Clear();
        }

        private void CacheVisuals()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_properties == null) _properties = new MaterialPropertyBlock();
            if (_trail == null) _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
        }

        private void SetupTrail()
        {
            _trail.time = trailTime;
            _trail.startWidth = trailStartWidth;
            _trail.endWidth = trailEndWidth;
            _trail.minVertexDistance = trailMinVertexDistance;
            if (_trail.sharedMaterial == null) _trail.sharedMaterial = EyeProjectilePool.SharedTrailMaterial;
            _trail.colorGradient = NormalTrailGradient;
        }

        private static Gradient CreateTrailGradient(bool deflected)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(deflected ? new Color(0.2f, 0.95f, 1f) : new Color(0.95f, 0.2f, 1f), 0f),
                    new GradientColorKey(deflected ? new Color(1f, 0.85f, 0.2f) : new Color(0.40f, 0.05f, 0.65f), 1f)
                },
                new[] { new GradientAlphaKey(deflected ? 0.95f : 0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private void SetTint(Color color)
        {
            if (_renderer == null || _properties == null) return;
            _properties.Clear();
            _properties.SetColor(BaseColorId, color);
            _properties.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_properties);
        }

        /// <summary>Reflects the projectile with the player's direction, speed and damage multipliers.</summary>
        public void Deflect(Vector3 newDirection, float speedMultiplier = 1.7f, float damageMultiplier = 2.5f)
        {
            if (_hasImpacted) return;
            _isDeflected = true;
            _shooter = null;
            speed *= speedMultiplier;
            damage *= damageMultiplier;
            if (newDirection.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(newDirection);
            _age = 0f;
            SetTint(deflectedColor);
            _baseScale = heavy ? _baseScale * heavyDeflectScaleMultiplier : Vector3.one * deflectedScale;
            transform.localScale = _baseScale;
            if (_trail != null)
            {
                _trail.colorGradient = DeflectedTrailGradient;
                _trail.time = deflectedTrailTime;
                _trail.startWidth = deflectedTrailStartWidth;
            }
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_hasImpacted) return;
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                Release();
                return;
            }

            UpdateReflectHoming(Time.deltaTime);
            float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency) * pulseAmplitude;
            transform.localScale = _baseScale * pulse;
            float stepDistance = speed * Time.deltaTime;
            Vector3 stepMove = transform.forward * stepDistance;
            Collider initial = FindInitialOverlap();
            if (initial != null) HandleCollision(initial, transform.position, -transform.forward);
            else if (TryFindCollision(stepDistance, out RaycastHit hit)) HandleCollision(hit.collider, hit.point, hit.normal);
            else transform.position += stepMove;
        }

        private void UpdateReflectHoming(float deltaTime)
        {
            if (!_isDeflected || reflectHomingDegPerSec <= 0f || _originalShooter == null
                || !_originalShooter.gameObject.activeInHierarchy || _originalEnemy == null
                || (_originalEnemy is Object enemy && enemy == null) || !_originalEnemy.IsAlive) return;
            Vector3 direction = _originalShooter.position - transform.position;
            if (direction.sqrMagnitude <= 0f) return;
            if (Vector3.Angle(transform.forward, direction) > reflectHomingMaxAngle) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                reflectHomingDegPerSec * deltaTime);
        }

        private void HandleCollision(Collider collider, Vector3 point, Vector3 normal)
        {
            PlayerParryController parry = !_isDeflected ? ParryFor(collider) : null;
            if (parry != null && parry.IsParrying && parry.IsFacingTarget(transform.position))
            {
                Deflect(parry.GetAimDirection());
                parry.NotifyProjectileParried();
                return;
            }
            Impact(point, normal, collider);
        }

        private bool ShouldIgnore(Collider collider)
        {
            if (_isDeflected) return false;
            if (_shooter != null && (collider.transform == _shooter || collider.transform.IsChildOf(_shooter))) return true;
            return CombatTargetLookup.Get(collider).Enemy != null;
        }

        private Collider FindInitialOverlap()
        {
            // Preserve the Judge's player-only fallback: non-player initial overlaps are never impacts.
            if (_isDeflected) return null;
            int count = Physics.OverlapSphereNonAlloc(transform.position, collisionRadius, _initialOverlaps,
                1 << GameLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _initialOverlaps[i];
                if (candidate == null || ShouldIgnore(candidate)) continue;
                return candidate;
            }
            return null;
        }

        private bool TryFindCollision(float distance, out RaycastHit hit)
        {
            int mask = GameLayers.EnemyProjectileMask;
            if (_isDeflected) mask &= ~(1 << GameLayers.Player);
            int count = Physics.SphereCastNonAlloc(transform.position, collisionRadius, transform.forward,
                _collisionHits, distance, mask, QueryTriggerInteraction.Collide);
            hit = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _collisionHits[i];
                if (candidate.collider == null || candidate.distance >= nearest) continue;
                // Launch overlaps have zero distance. Only the player fallback handles those.
                if (candidate.distance <= 0f || ShouldIgnore(candidate.collider)) continue;
                nearest = candidate.distance;
                hit = candidate;
            }
            return nearest < float.PositiveInfinity;
        }

        private static PlayerParryController ParryFor(Collider collider)
        {
            if (_playerParries == null) return null;
            for (int i = 0; i < _playerParries.Length; i++)
            {
                PlayerParryController parry = _playerParries[i];
                if (parry != null && (collider.transform == parry.transform || collider.transform.IsChildOf(parry.transform)))
                    return parry;
            }
            return null;
        }

        private void Impact(Vector3 point, Vector3 normal, Collider hitCollider)
        {
            if (_hasImpacted) return;
            _hasImpacted = true;
            if (_isDeflected) OnDeflectedImpact?.Invoke(this, hitCollider);
            if (CombatVFXManager.Instance != null) CombatVFXManager.Instance.SpawnOccultEnergyBurst(point, _isDeflected);
            bool punish = _isDeflected && _originalShooter != null && _reflectPunishable != null
                && !(_reflectPunishable is Object punishable && punishable == null)
                && (hitCollider.transform == _originalShooter || hitCollider.transform.IsChildOf(_originalShooter));
            IDamageable damageable = CombatTargetLookup.Get(hitCollider).Damageable;
            if (_isDeflected && heavy)
            {
                IGuardBreakable guard = hitCollider.GetComponentInParent<IGuardBreakable>();
                if (guard != null) guard.BreakGuard(reflectedGuardBreakDuration);
            }
            if (punish) _reflectPunishable.OnOwnProjectileReflectedHit(heavy, damage, point, normal);
            else if (damageable != null) damageable.TakeDamage(damage, point, normal, _isDeflected);
            if (_isDeflected && (punish || damageable != null) && HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(reflectedHitstopDuration, reflectedHitstopTimeScale);
            Release();
        }

        private void Release()
        {
            if (_isPooled) gameObject.SetActive(false);
            else Destroy(gameObject);
        }
    }
}
