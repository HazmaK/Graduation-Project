using System.Collections.Generic;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS
{
    /// <summary>
    /// Uçan Göz ve diğer menzilli düşmanların fırlattığı okült enerji / plazma mermisi.
    /// - Havadayken sabit hızla düz hat boyunca uçar.
    /// - Duvara veya oyuncuya çarptığında hasar verip patlar.
    /// - Oyuncunun Dash I-Frame (sıyrılma) ve Spiked Buckler (Parry) mekaniği ile tamamen entegredir.
    /// - Aktif mermi havuzunu (ActiveProjectiles) otomatik yönetir.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        public static readonly List<EnemyProjectile> ActiveProjectiles = new List<EnemyProjectile>();

        [Header("Mermi Parametreleri")]
        [SerializeField] private float speed = 14f;
        [SerializeField] private float damage = 18f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float collisionRadius = 0.35f;

        private Transform _shooter;
        private float _age = 0f;
        private bool _hasImpacted = false;
        private bool _isDeflected = false;
        private Renderer _renderer;
        private TrailRenderer _trail;
        private Vector3 _baseScale;
        private readonly RaycastHit[] _collisionHits = new RaycastHit[32];
        private readonly Collider[] _initialOverlaps = new Collider[32];
        private PlayerParryController[] _playerParries;

        public bool IsDeflected => _isDeflected;
        public float Damage => damage;

        /// <summary>Raised before damage when a reflected projectile impacts a collider.</summary>
        public static event System.Action<EnemyProjectile, Collider> OnDeflectedImpact;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDeflectedImpactSubscribers()
        {
            OnDeflectedImpact = null;
        }

        private void OnEnable()
        {
            if (!ActiveProjectiles.Contains(this))
            {
                ActiveProjectiles.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveProjectiles.Remove(this);
        }

        public void Initialize(Transform shooter, float projectileSpeed, float projectileDamage)
        {
            _shooter = shooter;
            speed = projectileSpeed;
            damage = projectileDamage;
            _age = 0f;
            _hasImpacted = false;
            _isDeflected = false;
            _playerParries = FindObjectsByType<PlayerParryController>(FindObjectsInactive.Exclude);
            _renderer = GetComponent<Renderer>();
            _baseScale = transform.localScale != Vector3.zero ? transform.localScale : new Vector3(0.40f, 0.40f, 0.40f);
            SetupTrail();
        }

        private void SetupTrail()
        {
            if (_trail == null)
            {
                _trail = GetComponent<TrailRenderer>();
                if (_trail == null)
                {
                    _trail = gameObject.AddComponent<TrailRenderer>();
                }
            }

            _trail.time = 0.22f;
            _trail.startWidth = 0.28f;
            _trail.endWidth = 0.02f;
            _trail.minVertexDistance = 0.05f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader != null && _trail.material == null)
            {
                _trail.material = new Material(shader);
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.95f, 0.2f, 1.0f), 0.0f), new GradientColorKey(new Color(0.40f, 0.05f, 0.65f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            _trail.colorGradient = gradient;
        }

        /// <summary>
        /// Kalkan savurması (Parry) anında mermiyi oyuncunun baktığı yöne 2.5x hasarla geri yansıtır.
        /// </summary>
        public void Deflect(Vector3 newDirection, float speedMultiplier = 1.7f, float damageMultiplier = 2.5f)
        {
            if (_hasImpacted) return;

            _isDeflected = true;
            _shooter = null; // Artık düşmana değil oyuncuya ait
            speed *= speedMultiplier;
            damage *= damageMultiplier;
            transform.rotation = Quaternion.LookRotation(newDirection);
            _age = 0f;

            // Görsel Geri Bildirim: Mermi rengini parry elektro maviye dönüştür
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = new Color(0.15f, 0.95f, 1.0f); // Elektro mavi parry rengi
            }

            // Hafif büyütme
            _baseScale = Vector3.one * 0.55f;
            transform.localScale = _baseScale;

            if (_trail != null)
            {
                Gradient deflectGrad = new Gradient();
                deflectGrad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.2f, 0.95f, 1.0f), 0.0f), new GradientColorKey(new Color(1.0f, 0.85f, 0.2f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                _trail.colorGradient = deflectGrad;
                _trail.time = 0.32f;
                _trail.startWidth = 0.36f;
            }

            Debug.Log($"<color=cyan><b>[PARRY DEFLECTION!]</b></color> Projectile deflected with {damage:F0} damage!");
        }

        private void Update()
        {
            if (_hasImpacted) return;

            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Nabız gibi atan okült enerji çekirdeği
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.12f;
            transform.localScale = _baseScale * pulse;

            float stepDistance = speed * Time.deltaTime;
            Vector3 stepMove = transform.forward * stepDistance;

            // Sphere casts omit colliders already overlapping the launch point.
            Collider initial = FindInitialOverlap();
            if (initial != null)
            {
                HandleCollision(initial, transform.position, -transform.forward);
            }
            else if (TryFindCollision(stepDistance, out RaycastHit hit))
            {
                HandleCollision(hit.collider, hit.point, hit.normal);
            }
            else
            {
                transform.position += stepMove;
            }
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

        private Collider FindInitialOverlap()
        {
            // SphereCast ignores colliders it starts inside, so only the player needs this fallback.
            // Walls, arena trigger volumes and other level geometry the sword is born inside (for
            // example the boss arena bounds) must stay ignored, exactly as before the fix.
            if (_isDeflected) return null;
            int count = Physics.OverlapSphereNonAlloc(transform.position, collisionRadius, _initialOverlaps,
                1 << GameLayers.Player, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _initialOverlaps[i];
                if (candidate == null) continue;
                if (!_isDeflected && _shooter != null
                    && (candidate.transform == _shooter || candidate.transform.IsChildOf(_shooter))) continue;
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
                // Unity reports colliders the cast starts inside at distance 0 (boss body, arena
                // bounds trigger, walls). Those are not impacts; the player is handled by
                // FindInitialOverlap, so only genuine sweep hits count here.
                if (candidate.distance <= 0f) continue;
                if (!_isDeflected && _shooter != null
                    && (candidate.transform == _shooter || candidate.transform.IsChildOf(_shooter))) continue;
                // Ignore the source before choosing the nearest hit, so a player immediately
                // behind a boss collider is still tested within this same movement segment.
                nearest = candidate.distance;
                hit = candidate;
            }
            return nearest < float.PositiveInfinity;
        }

        private PlayerParryController ParryFor(Collider collider)
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

            // Okült enerji patlaması VFX (Parry yansıması ise mavi-altın, normal ise karanlık mor)
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnOccultEnergyBurst(point, _isDeflected);
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, point, normal, _isDeflected);

                if (_isDeflected && HitstopManager.Instance != null)
                {
                    HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
                }
            }

            Destroy(gameObject);
        }
    }
}
