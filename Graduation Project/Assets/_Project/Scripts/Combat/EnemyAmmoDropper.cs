using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Düşmanlara eklenen ve öldüklerinde cephane düşüşünü yöneten modüler bileşen.
    /// - DOOM Eternal kaynak döngüsü: Silahla öldürmek cephaneyi NET olarak eritir (geri dönüş ~%30-45);
    ///   yakın dövüş infazı (chainsaw eşdeğeri) ise garantili ve bol cephane saçar.
    /// - Kritik vuruş cephane şansını artırmaz; cephane ekonomisini yalnızca infaz besler.
    /// - Düşman sınıfı ölüm anında TriggerDrop'u TEK kez çağırır; OnEnemyDeath'e ayrıca abone olunmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAmmoDropper : MonoBehaviour
    {
        [Header("Düşüş Ayarları")]
        [Tooltip("Silahla öldürmede cephane düşürme olasılığı (0.0 - 1.0). İnfaz öldürmeleri her zaman düşürür.")]
        [Range(0f, 1f)]
        [SerializeField] private float dropChance = 0.35f;

        [Tooltip("Düşüş yüksekliği ofseti")]
        [SerializeField] private float dropHeightOffset = 0.6f;

        [Tooltip("Her zaman cephane bıraksın mı? (Elit düşmanlar için)")]
        [SerializeField] private bool guaranteedDrop = false;

        [Header("Miktarlar (Silahla Öldürme)")]
        [Tooltip("Pompalı atışı 2 fişek harcar → 2 fişek = 1 atış")]
        [SerializeField] private int gunKillShells = 2;
        [Tooltip("Ghoul ~7, Göz ~5 ok ile ölür")]
        [SerializeField] private int gunKillBolts = 6;

        [Header("Miktarlar (İnfaz / Yakın Dövüş Öldürme)")]
        [SerializeField] private int meleeKillShells = 6;
        [SerializeField] private int meleeKillBolts = 20;

        private PlayerAmmoInventory _inventory;

        /// <summary>
        /// Cephane düşüşünü hesaplar ve sahnede oluşturur.
        /// </summary>
        public void TriggerDrop(bool isCriticalKill = false, bool isMeleeKill = false)
        {
            if (!guaranteedDrop && !isMeleeKill)
            {
                if (Random.value > dropChance) return;
            }

            Vector3 spawnPos = transform.position + Vector3.up * dropHeightOffset;

            // Oyuncunun envanterini tara: Hangi cephanesi daha azsa ona öncelik ver
            if (_inventory == null) _inventory = FindAnyObjectByType<PlayerAmmoInventory>();
            AmmoType chosenType = AmmoType.Shells;

            if (_inventory != null)
            {
                float shellRatio = _inventory.GetAmmoRatio(AmmoType.Shells);
                float boltRatio = _inventory.GetAmmoRatio(AmmoType.Bolts);

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

            int amount;
            if (isMeleeKill)
            {
                amount = (chosenType == AmmoType.Shells) ? meleeKillShells : meleeKillBolts;
            }
            else
            {
                amount = (chosenType == AmmoType.Shells) ? gunKillShells : gunKillBolts;
            }

            AmmoPickup.CreatePickupGameObject(spawnPos, chosenType, amount);
        }
    }
}
