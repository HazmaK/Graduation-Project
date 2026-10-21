using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Varlıkların uzuvları, zayıf noktaları (Headshot / Gözbebeği) veya zırhlı kısımları için
    /// tip güvenli, sıfır GC tahsisli evrensel vuruş alanı bileşeni.
    /// Kırılgan string aramaları (name.Contains("head")) yerine çarpan ve kritik bölge bayrakları kullanır.
    /// </summary>
    [DisallowMultipleComponent]
    public class Hitbox : MonoBehaviour, IDamageable
    {
        [Header("Hasar Katsayısı ve Bölge")]
        [Tooltip("Bu bölgeye gelen hasarın çarpılacağı oran (Örn: Gövde = 1.0, Kafa = 2.0, Zırh = 0.5)")]
        [SerializeField] private float damageMultiplier = 1.0f;

        [Tooltip("Bu bölgeye yapılan vuruşlar doğrudan kritik vuruş (Headshot) sayılır mı?")]
        [SerializeField] private bool isCriticalZone = false;

        [Header("Hedef Varlık")]
        [SerializeField] private Component targetEntityComponent;

        private IDamageable _cachedTarget;

        public float DamageMultiplier => damageMultiplier;
        public bool IsCriticalZone => isCriticalZone;
        public IDamageable Target => _cachedTarget;

        private void Awake()
        {
            CacheTarget();
        }

        private void CacheTarget()
        {
            if (_cachedTarget != null) return;

            if (targetEntityComponent is IDamageable explicitTarget)
            {
                _cachedTarget = explicitTarget;
            }
            else
            {
                _cachedTarget = GetComponentInParent<IDamageable>();
            }
        }

        /// <summary>
        /// Çalışma zamanında dinamik olarak oluşturulan hitbox'ları yapılandırmak için kullanılır.
        /// </summary>
        public void Initialize(IDamageable target, float multiplier = 1.0f, bool isCrit = false)
        {
            _cachedTarget = target;
            if (target is Component comp)
            {
                targetEntityComponent = comp;
            }
            damageMultiplier = multiplier;
            isCriticalZone = isCrit;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (_cachedTarget == null)
            {
                CacheTarget();
            }

            if (_cachedTarget != null)
            {
                float finalDamage = amount * damageMultiplier;
                bool finalCrit = isCritical || isCriticalZone;
                _cachedTarget.TakeDamage(finalDamage, hitPoint, hitNormal, finalCrit);
            }
        }
    }
}
