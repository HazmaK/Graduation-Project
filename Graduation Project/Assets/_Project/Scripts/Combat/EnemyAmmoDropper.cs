using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Düşmanlara eklenen ve öldüklerinde akıllı cephane düşüşünü yöneten modüler bileşen.
    /// - DOOM Eternal kaynak döngüsü: Yakın dövüş (Melee) veya kritik vuruşla öldürülen düşmanlar daha bol cephane saçar.
    /// - IEncounterEnemy olaylarını dinleyerek sıfır bağımlılıkla çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAmmoDropper : MonoBehaviour
    {
        [Header("Düşüş Ayarları")]
        [Tooltip("Düşmanın kesin olarak cephane düşürme olasılığı (0.0 - 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] private float dropChance = 0.65f;

        [Tooltip("Düşüş yüksekliği ofseti")]
        [SerializeField] private float dropHeightOffset = 0.6f;

        [Tooltip("Her zaman cephane bıraksın mı? (Elit düşmanlar için)")]
        [SerializeField] private bool guaranteedDrop = false;

        private IEncounterEnemy _encounterEnemy;
        private bool _lastDamageWasCritical = false;

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

        public void NotifyDamageTaken(bool isCritical)
        {
            _lastDamageWasCritical = isCritical;
        }

        private void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            TriggerDrop();
        }

        /// <summary>
        /// Cephane düşüşünü hesaplar ve sahnede oluşturur.
        /// </summary>
        public void TriggerDrop(bool isCriticalKill = false, bool isMeleeKill = false)
        {
            bool isCrit = isCriticalKill || _lastDamageWasCritical;

            if (!guaranteedDrop && !isCrit && !isMeleeKill)
            {
                if (Random.value > dropChance) return;
            }

            Vector3 spawnPos = transform.position + Vector3.up * dropHeightOffset;

            // Oyuncunun envanterini tara: Hangi cephanesi daha azsa ona öncelik ver
            PlayerAmmoInventory inventory = FindAnyObjectByType<PlayerAmmoInventory>();
            AmmoType chosenType = AmmoType.Shells;

            if (inventory != null)
            {
                float shellRatio = inventory.GetAmmoRatio(AmmoType.Shells);
                float boltRatio = inventory.GetAmmoRatio(AmmoType.Bolts);

                // Hangi cephane daha azsa onu düşür, eşitse rastgele seç
                if (shellRatio < boltRatio)
                {
                    chosenType = AmmoType.Shells;
                }
                else if (boltRatio < shellRatio)
                {
                    chosenType = AmmoType.Bolts;
                }
                else
                {
                    chosenType = (Random.value > 0.5f) ? AmmoType.Shells : AmmoType.Bolts;
                }
            }

            int amount = (chosenType == AmmoType.Shells) ? 4 : 15;
            if (isCrit || isMeleeKill)
            {
                amount = (chosenType == AmmoType.Shells) ? 6 : 25; // Bonus cephane
            }

            AmmoPickup.CreatePickupGameObject(spawnPos, chosenType, amount);
            _lastDamageWasCritical = false;
        }
    }
}
