using System.Collections.Generic;
using UnityEngine;

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

        public bool IsDeflected => _isDeflected;
        public float Damage => damage;

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
            _renderer = GetComponent<Renderer>();
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
            transform.localScale = Vector3.one * 0.55f;

            Debug.Log($"<color=cyan><b>[PARRY DEFLECTION!]</b></color> Mermi {damage:F0} hasarla geri püskürtüldü!");
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

            float stepDistance = speed * Time.deltaTime;
            Vector3 stepMove = transform.forward * stepDistance;

            // Çarpışma testi (SphereCast)
            if (Physics.SphereCast(transform.position, collisionRadius, transform.forward, out RaycastHit hit, stepDistance, ~0, QueryTriggerInteraction.Collide))
            {
                // 1. Oyuncuya çarpıyorsa ve oyuncu şu an aktif Parry yapıyorsa: ANINDA YANSIT!
                if (!_isDeflected)
                {
                    PlayerParryController parry = hit.collider.GetComponentInParent<PlayerParryController>();
                    if (parry != null && parry.IsParrying && parry.IsFacingTarget(transform.position))
                    {
                        Deflect(parry.GetAimDirection());
                        parry.NotifyProjectileParried();
                        return;
                    }

                    // Kendi atanını vurmasın
                    if (_shooter != null && (hit.transform == _shooter || hit.transform.IsChildOf(_shooter)))
                    {
                        transform.position += stepMove;
                        return;
                    }
                }
                else
                {
                    // Yansıtılmışsa oyuncuyu vurmasın (Düşmanları vursun)
                    if (hit.collider.GetComponentInParent<PlayerHealth>() != null)
                    {
                        transform.position += stepMove;
                        return;
                    }
                }

                Impact(hit.point, hit.normal, hit.collider);
            }
            else
            {
                transform.position += stepMove;
            }
        }

        private void Impact(Vector3 point, Vector3 normal, Collider hitCollider)
        {
            if (_hasImpacted) return;
            _hasImpacted = true;

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
