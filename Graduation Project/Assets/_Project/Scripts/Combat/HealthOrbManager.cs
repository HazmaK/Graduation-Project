using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Sektör standardı, dinamik adaptif dengelemeli (Adaptive Pity Director) ve sıfır GC tahsisli Can Küresi Yöneticisi.
    /// - DOOM Eternal ve ULTRAKILL ilhamlı akıllı düşüş algoritması:
    ///   * Oyuncu kritik candayken (<%35 HP) neredeyse garantili (%95+) can küresi düşüşü sağlar.
    ///   * Oyuncunun canı doluyken (>%80 HP) düşüş oranını kısarak arena çöplüğü oluşmasını engeller ve gerilimi korur.
    ///   * Kritik (Headshot) ve İnfaz (Melee) ölümlerinde ekstra can küresi bonusu verir.
    ///   * Kötü şans koruması (Pity Counter): Arka arkaya küre düşmeyen ölümlerde şansı katlar.
    /// - 24'lük nesne havuzu (Object Pool) ile sıfır GC tahsisi garantisi sunar.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthOrbManager : MonoBehaviour
    {
        private static HealthOrbManager _instance;
        public static HealthOrbManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<HealthOrbManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("HealthOrbManager");
                        _instance = go.AddComponent<HealthOrbManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Havuz Ayarları")]
        [Tooltip("Özel Can Küresi Prefabı. Boş bırakılırsa prosedürel zümrüt küre üretilir.")]
        [SerializeField] private GameObject healthOrbPrefab;
        [SerializeField] private int poolCapacity = 24;

        [Header("Dinamik Düşüş Olasılıkları (Adaptive Director)")]
        [Tooltip("Temel düşme olasılığı (0.50 = %50)")]
        [Range(0f, 1f)]
        [SerializeField] private float baseDropChance = 0.50f;

        [Tooltip("Kritik can durumunda (<%35 HP) eklenen acil yaşam hattı bonusu")]
        [Range(0f, 0.6f)]
        [SerializeField] private float lowHealthBonusChance = 0.45f;

        [Tooltip("Yüksek can durumunda (>%80 HP) düşüş azaltma cezası")]
        [Range(0f, 0.4f)]
        [SerializeField] private float highHealthPenaltyChance = 0.25f;

        [Tooltip("Kritik vuruşla (Headshot) öldürme ek bonus şansı")]
        [Range(0f, 0.4f)]
        [SerializeField] private float critKillBonusChance = 0.25f;

        [Tooltip("Yakın dövüş / İnfaz kılıcıyla öldürme ek bonus şansı")]
        [Range(0f, 0.5f)]
        [SerializeField] private float meleeKillBonusChance = 0.35f;

        [Header("İyileşme Miktarları")]
        [Tooltip("Standart küçük can küresi iyileşme miktarı")]
        [SerializeField] private float standardOrbHeal = 15f;

        [Tooltip("Ağır düşman veya infazdan düşen büyük can küresi miktarı")]
        [SerializeField] private float heavyOrbHeal = 30f;

        // Nesne havuzu
        private readonly Queue<HealthOrb> _pool = new Queue<HealthOrb>();
        private readonly List<HealthOrb> _allOrbs = new List<HealthOrb>();

        // Adaptif durum takibi
        private PlayerHealth _playerHealth;
        private int _consecutiveNonDrops = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            PrewarmPool();
            CachePlayerHealth();
        }

        private void CachePlayerHealth()
        {
            if (_playerHealth != null) return;
            _playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        }

        private void PrewarmPool()
        {
            Transform poolRoot = new GameObject("OrbPool_Container").transform;
            poolRoot.SetParent(transform);

            for (int i = 0; i < poolCapacity; i++)
            {
                HealthOrb orb = CreateNewOrbInstance(poolRoot);
                orb.gameObject.SetActive(false);
                _pool.Enqueue(orb);
                _allOrbs.Add(orb);
            }
        }

        private HealthOrb CreateNewOrbInstance(Transform parent)
        {
            GameObject go;
            if (healthOrbPrefab != null)
            {
                go = Instantiate(healthOrbPrefab, parent);
            }
            else
            {
                go = new GameObject("HealthOrb_Instance");
                go.transform.SetParent(parent);
            }

            HealthOrb orb = go.GetComponent<HealthOrb>();
            if (orb == null)
            {
                orb = go.AddComponent<HealthOrb>();
            }

            return orb;
        }

        /// <summary>
        /// Bir düşman öldüğünde sektör standardı dinamik dengeleme ile can küresi düşüşünü simüle eder.
        /// </summary>
        /// <param name="spawnPosition">Düşmanın ölüm konumu</param>
        /// <param name="enemyTier">1: Standart (Ghoul), 2: Uçan/Ağır (Eye), 3: Elit</param>
        /// <param name="isCritical">Ölüm kafa vuruşu/kritik darbeyle mi gerçekleşti?</param>
        /// <param name="isMelee">Ölüm infaz kılıcı/yakın dövüşle mi gerçekleşti?</param>
        public void TryDropOrbs(Vector3 spawnPosition, int enemyTier = 1, bool isCritical = false, bool isMelee = false)
        {
            CachePlayerHealth();

            // 1. Dinamik Olasılık Hesabı (Adaptive Director Math)
            float finalChance = baseDropChance;

            if (_playerHealth != null && !_playerHealth.IsDead)
            {
                float healthRatio = _playerHealth.HealthPercent;

                // Kritik can durumunda (< %35): DOOM Pacing Garantisi
                if (healthRatio <= 0.35f)
                {
                    finalChance += lowHealthBonusChance;
                }
                // Yüksek can durumunda (> %80): İsraf önleyici düşüş kısması
                else if (healthRatio >= 0.80f)
                {
                    finalChance -= highHealthPenaltyChance;
                }
            }

            // Beceri ve risk ödülü: Headshot ve Melee infazı her zaman ödüllendirilir!
            if (isCritical) finalChance += critKillBonusChance;
            if (isMelee) finalChance += meleeKillBonusChance;

            // Kötü şans koruması: Üst üste 2 düşmandan küre düşmediyse şansı artır
            if (_consecutiveNonDrops >= 2)
            {
                finalChance += 0.20f * (_consecutiveNonDrops - 1);
            }

            finalChance = Mathf.Clamp(finalChance, 0.10f, 0.98f);

            // 2. Şans Zarını At
            bool shouldDrop = Random.value <= finalChance;

            if (!shouldDrop)
            {
                _consecutiveNonDrops++;
                return;
            }

            _consecutiveNonDrops = 0;

            // 3. Düşecek Küre Sayısı ve Değerleri
            int orbCount = 1;
            float healPerOrb = standardOrbHeal;

            // Ağır düşman veya kafa kopması/kritik infaz bonusu
            if (enemyTier >= 2)
            {
                healPerOrb = heavyOrbHeal;
                if (isCritical || isMelee)
                {
                    orbCount = 2; // Ağır düşmanı kritik infaz edene 2 büyük küre!
                }
            }
            else if (isCritical || isMelee)
            {
                // Standart düşmanda kritik vuruş fazladan 1 küçük küre sıçratır
                orbCount = Random.value < 0.60f ? 2 : 1;
            }

            for (int i = 0; i < orbCount; i++)
            {
                Vector3 offset = (i == 0) ? Vector3.zero : Random.insideUnitSphere * 0.35f;
                offset.y = Mathf.Abs(offset.y);
                SpawnOrb(spawnPosition + offset, healPerOrb);
            }
        }

        /// <summary>
        /// Doğrudan belirli bir konumda can küresi doğurur (Sandıklar, sırlar veya garantili ödüller için).
        /// </summary>
        public HealthOrb SpawnOrb(Vector3 position, float healAmount)
        {
            HealthOrb orb = GetFromPool();
            orb.Spawn(position, healAmount, this);
            return orb;
        }

        private HealthOrb GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }

            // Havuz tükendiyse en eski aktif olanı geri dönüştür
            for (int i = 0; i < _allOrbs.Count; i++)
            {
                if (_allOrbs[i] != null && _allOrbs[i].IsActive)
                {
                    _allOrbs[i].Despawn();
                    return _allOrbs[i];
                }
            }

            // Acil durum yeni örnekleme
            HealthOrb newOrb = CreateNewOrbInstance(transform);
            _allOrbs.Add(newOrb);
            return newOrb;
        }

        public void ReturnToPool(HealthOrb orb)
        {
            if (orb == null) return;
            if (!_pool.Contains(orb))
            {
                _pool.Enqueue(orb);
            }
        }
    }
}
