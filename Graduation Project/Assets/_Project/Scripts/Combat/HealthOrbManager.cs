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
        [Tooltip("Silahla öldürmede temel düşme olasılığı. Can, savaşarak (özellikle infazla) kazanılır.")]
        [Range(0f, 1f)]
        [SerializeField] private float baseDropChance = 0.12f;

        [Tooltip("Bu can oranının altında acil yaşam hattı bonusu eklenir")]
        [Range(0f, 1f)]
        [SerializeField] private float lowHealthThreshold = 0.30f;

        [Tooltip("Kritik can durumunda eklenen acil yaşam hattı bonusu (kurtarmaz, sadece nefes aldırır)")]
        [Range(0f, 0.6f)]
        [SerializeField] private float lowHealthBonusChance = 0.18f;

        [Tooltip("Bu can oranının üstünde düşüş cezası uygulanır")]
        [Range(0f, 1f)]
        [SerializeField] private float highHealthThreshold = 0.80f;

        [Tooltip("Yüksek can durumunda düşüş azaltma cezası")]
        [Range(0f, 0.4f)]
        [SerializeField] private float highHealthPenaltyChance = 0.12f;

        [Tooltip("Kritik vuruşla (Headshot) öldürme ek bonus şansı")]
        [Range(0f, 0.4f)]
        [SerializeField] private float critKillBonusChance = 0.08f;

        [Tooltip("Yakın dövüş / İnfaz kılıcıyla öldürme ek bonus şansı (Glory Kill eşdeğeri; ana can kaynağı)")]
        [Range(0f, 0.8f)]
        [SerializeField] private float meleeKillBonusChance = 0.55f;

        [Header("Kötü Şans Koruması (Pity)")]
        [Tooltip("Kaç ardışık boş ölümden sonra şans artmaya başlar")]
        [SerializeField] private int pityThreshold = 4;

        [Tooltip("Eşik aşıldıktan sonra her boş ölümde eklenen şans")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pityStepChance = 0.08f;

        [Tooltip("Nihai olasılık sınırları")]
        [Range(0f, 1f)]
        [SerializeField] private float minDropChance = 0.0f;
        [Range(0f, 1f)]
        [SerializeField] private float maxDropChance = 0.90f;

        [Header("İyileşme Miktarları")]
        [Tooltip("Standart küçük can küresi iyileşme miktarı")]
        [SerializeField] private float standardOrbHeal = 10f;

        [Tooltip("Ağır düşmandan düşen büyük can küresi miktarı")]
        [SerializeField] private float heavyOrbHeal = 20f;

        [Tooltip("Tier 1 düşmanı infaz edince ikinci küre çıkma olasılığı")]
        [Range(0f, 1f)]
        [SerializeField] private float meleeExtraOrbChance = 0.50f;

        public float StandardOrbHeal => standardOrbHeal;
        public float HeavyOrbHeal => heavyOrbHeal;

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

                // Kritik can durumunda: küçük bir yaşam hattı (garanti değil; asıl çıkış infaz)
                if (healthRatio <= lowHealthThreshold)
                {
                    finalChance += lowHealthBonusChance;
                }
                // Yüksek can durumunda: İsraf önleyici düşüş kısması
                else if (healthRatio >= highHealthThreshold)
                {
                    finalChance -= highHealthPenaltyChance;
                }
            }

            // Beceri ve risk ödülü: İnfaz ana can kaynağıdır, headshot küçük bonus verir
            if (isCritical) finalChance += critKillBonusChance;
            if (isMelee) finalChance += meleeKillBonusChance;

            // Kötü şans koruması: Uzun kuraklık serilerini kırar ama kıtlığı bozmaz
            if (_consecutiveNonDrops >= pityThreshold)
            {
                finalChance += pityStepChance * (_consecutiveNonDrops - pityThreshold + 1);
            }

            finalChance = Mathf.Clamp(finalChance, minDropChance, maxDropChance);

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

            // Ekstra küre yalnızca infazla kazanılır (Glory Kill eşdeğeri)
            if (enemyTier >= 2)
            {
                healPerOrb = heavyOrbHeal;
                if (isMelee)
                {
                    orbCount = 2; // Ağır düşmanı infaz edene 2 büyük küre
                }
            }
            else if (isMelee)
            {
                orbCount = Random.value < meleeExtraOrbChance ? 2 : 1;
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
