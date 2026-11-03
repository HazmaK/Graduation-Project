using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Düşmanlara eklenen ve öldüklerinde akıllı can küresi düşüşünü yöneten modüler bileşen.
    /// - Herhangi bir düşman sınıfına (BasicEnemyAI, FlyingEyeAI, CombatDummy vb.) bağımsız olarak eklenebilir.
    /// - IEncounterEnemy olaylarını ve IDamageable arayüzünü dinleyerek sıfır bağımlılık (Decoupled) ile çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthDropper : MonoBehaviour
    {
        [Header("Düşman Kademesi & Düşüş Ayarları")]
        [Tooltip("1: Normal (Ghoul Stalker), 2: Uçan/Ağır (Flying Eye Watcher), 3: Elit / Mini-Boss")]
        [Range(1, 3)]
        [SerializeField] private int enemyTier = 1;

        [Tooltip("Her zaman kesin can küresi düşürsün mü? (Elit veya özel mini-boss düşmanlar için)")]
        [SerializeField] private bool guaranteedDrop = false;

        [Tooltip("Doğuş yüksekliği ofseti")]
        [SerializeField] private float dropHeightOffset = 0.8f;

        private IEncounterEnemy _encounterEnemy;
        private bool _lastDamageWasCritical = false;

        public int EnemyTier { get => enemyTier; set => enemyTier = value; }
        public bool GuaranteedDrop { get => guaranteedDrop; set => guaranteedDrop = value; }

        private void Awake()
        {
            _encounterEnemy = GetComponent<IEncounterEnemy>();
        }

        private void OnEnable()
        {
            if (_encounterEnemy != null)
            {
                _encounterEnemy.OnEnemyDeath += HandleEnemyDeath;
            }
        }

        private void OnDisable()
        {
            if (_encounterEnemy != null)
            {
                _encounterEnemy.OnEnemyDeath -= HandleEnemyDeath;
            }
        }

        /// <summary>
        /// Son darbenin kritik olup olmadığını kaydetmek için harici olarak çağrılabilir.
        /// </summary>
        public void NotifyDamageTaken(bool isCritical)
        {
            _lastDamageWasCritical = isCritical;
        }

        private void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            TriggerDrop();
        }

        /// <summary>
        /// Düşüşü doğrudan tetikler.
        /// </summary>
        public void TriggerDrop(bool isCriticalKill = false, bool isMeleeKill = false)
        {
            if (HealthOrbManager.Instance == null) return;

            bool isCrit = isCriticalKill || _lastDamageWasCritical;
            Vector3 dropPosition = transform.position + Vector3.up * dropHeightOffset;

            if (guaranteedDrop)
            {
                // Kesin düşüş: En az 1 veya 2 küre garantili doğur
                float heal = (enemyTier >= 2) ? 30f : 15f;
                HealthOrbManager.Instance.SpawnOrb(dropPosition, heal);
                if (isCrit || isMeleeKill)
                {
                    HealthOrbManager.Instance.SpawnOrb(dropPosition + Vector3.up * 0.2f, 15f);
                }
            }
            else
            {
                HealthOrbManager.Instance.TryDropOrbs(dropPosition, enemyTier, isCrit, isMeleeKill);
            }

            _lastDamageWasCritical = false;
        }
    }
}
