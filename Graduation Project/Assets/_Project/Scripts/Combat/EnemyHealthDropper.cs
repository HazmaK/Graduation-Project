using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Düşmanlara eklenen ve öldüklerinde akıllı can küresi düşüşünü yöneten modüler bileşen.
    /// - Düşman sınıfı (BasicEnemyAI, FlyingEyeAI vb.) ölüm anında TriggerDrop'u kritik/infaz bilgisiyle TEK kez çağırır.
    /// - Not: IEncounterEnemy.OnEnemyDeath'e ayrıca abone OLMAZ; aksi halde her ölümde zar iki kez atılır.
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

        private bool _lastDamageWasCritical = false;

        public int EnemyTier { get => enemyTier; set => enemyTier = value; }
        public bool GuaranteedDrop { get => guaranteedDrop; set => guaranteedDrop = value; }

        /// <summary>
        /// Son darbenin kritik olup olmadığını kaydetmek için harici olarak çağrılabilir.
        /// </summary>
        public void NotifyDamageTaken(bool isCritical)
        {
            _lastDamageWasCritical = isCritical;
        }

        /// <summary>
        /// Düşüşü doğrudan tetikler.
        /// </summary>
        public void TriggerDrop(bool isCriticalKill = false, bool isMeleeKill = false)
        {
            HealthOrbManager manager = HealthOrbManager.Instance;
            if (manager == null) return;

            bool isCrit = isCriticalKill || _lastDamageWasCritical;
            Vector3 dropPosition = transform.position + Vector3.up * dropHeightOffset;

            if (guaranteedDrop)
            {
                // Kesin düşüş (elit/mini-boss): 1 küre garantili, infaz ile öldürülürse +1 küçük küre
                float heal = (enemyTier >= 2) ? manager.HeavyOrbHeal : manager.StandardOrbHeal;
                manager.SpawnOrb(dropPosition, heal);
                if (isMeleeKill)
                {
                    manager.SpawnOrb(dropPosition + Vector3.up * 0.2f, manager.StandardOrbHeal);
                }
            }
            else
            {
                manager.TryDropOrbs(dropPosition, enemyTier, isCrit, isMeleeKill);
            }

            _lastDamageWasCritical = false;
        }
    }
}
