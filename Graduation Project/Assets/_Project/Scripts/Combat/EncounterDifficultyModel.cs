using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Dinamik zorluk ayarları (Inspector'da direktör altında düzenlenir).
    /// </summary>
    [System.Serializable]
    public class DynamicDifficultySettings
    {
        [Tooltip("Dinamik zorluk açık mı?")]
        [SerializeField] private bool enabled = true;

        [Header("Zorlanma (Yavaşlat)")]
        [Tooltip("Oyuncu canı bu oranın altındayken doğuşlar yavaşlar")]
        [Range(0f, 1f)]
        [SerializeField] private float lowHealthThreshold = 0.35f;

        [Tooltip("Düşük canda doğuş aralığı çarpanı (1.5 = %50 daha seyrek)")]
        [SerializeField] private float lowHealthIntervalMultiplier = 1.5f;

        [Tooltip("Bu karşılaşmada oyuncu en az bir kez öldüyse (retry) doğuş aralığı çarpanı")]
        [SerializeField] private float postDeathIntervalMultiplier = 1.2f;

        [Header("Rahatlık (Ek Düşman)")]
        [Tooltip("Dalga sonunda can bu oranın üstündeyse 'rahat' sayılır")]
        [Range(0f, 1f)]
        [SerializeField] private float cruiseHealthThreshold = 0.8f;

        [Tooltip("Dalga boyunca alınan hasar, maksimum canın bu oranını geçmediyse 'rahat' sayılır")]
        [Range(0f, 1f)]
        [SerializeField] private float cruiseMaxDamageRatio = 0.25f;

        [Tooltip("Her rahat dalgadan sonra sonraki dalgalara eklenecek düşman artışı")]
        [SerializeField] private int bonusEnemiesPerCruisingWave = 1;

        [Tooltip("Bir dalgaya eklenebilecek maksimum ek düşman")]
        [SerializeField] private int maxBonusEnemies = 3;

        public bool Enabled => enabled;
        public float LowHealthThreshold => lowHealthThreshold;
        public float LowHealthIntervalMultiplier => Mathf.Max(1f, lowHealthIntervalMultiplier);
        public float PostDeathIntervalMultiplier => Mathf.Max(1f, postDeathIntervalMultiplier);
        public float CruiseHealthThreshold => cruiseHealthThreshold;
        public float CruiseMaxDamageRatio => cruiseMaxDamageRatio;
        public int BonusEnemiesPerCruisingWave => Mathf.Max(0, bonusEnemiesPerCruisingWave);
        public int MaxBonusEnemies => Mathf.Max(0, maxBonusEnemies);
    }

    /// <summary>
    /// Saf C# dinamik zorluk modeli (POCO; MonoBehaviour'dan bağımsız, test edilebilir).
    /// - Oyuncu zorlanıyorsa (düşük can / ölüm sonrası) doğuş aralıklarını uzatır.
    /// - Oyuncu rahatsa (yüksek can + az hasar) sonraki dalgalara kademeli ek düşman ekler.
    /// Tahsissiz çalışır; tüm metotlar O(1).
    /// </summary>
    public sealed class EncounterDifficultyModel
    {
        private readonly DynamicDifficultySettings _settings;
        private float _damageThisWave;
        private int _bonusLevel;
        private int _deaths;

        public EncounterDifficultyModel(DynamicDifficultySettings settings)
        {
            _settings = settings ?? new DynamicDifficultySettings();
        }

        public bool Enabled => _settings.Enabled;
        public int BonusLevel => _bonusLevel;
        public int Deaths => _deaths;
        public float DamageThisWave => _damageThisWave;

        /// <summary>Yeni bir karşılaşma (retry değil) başlarken tüm durumu sıfırlar.</summary>
        public void ResetAll()
        {
            _damageThisWave = 0f;
            _bonusLevel = 0;
            _deaths = 0;
        }

        public void BeginWave()
        {
            _damageThisWave = 0f;
        }

        public void RegisterDamage(float amount)
        {
            if (amount > 0f) _damageThisWave += amount;
        }

        /// <summary>Oyuncu öldü ve karşılaşma yeniden deneniyor: ek düşmanları sıfırla, merhamet çarpanını aç.</summary>
        public void RegisterPlayerDeath()
        {
            _deaths++;
            _bonusLevel = 0;
            _damageThisWave = 0f;
        }

        /// <summary>
        /// Dalga sonunda performansı değerlendirir ve sonraki dalgaların ek düşman seviyesini günceller.
        /// </summary>
        public void EvaluateWaveEnd(float healthPercent, float maxHealth)
        {
            if (!_settings.Enabled)
            {
                _bonusLevel = 0;
                return;
            }

            float damageRatio = maxHealth > 0f ? _damageThisWave / maxHealth : 0f;

            if (healthPercent >= _settings.CruiseHealthThreshold && damageRatio <= _settings.CruiseMaxDamageRatio)
            {
                _bonusLevel = Mathf.Min(_settings.MaxBonusEnemies, _bonusLevel + _settings.BonusEnemiesPerCruisingWave);
            }
            else if (healthPercent < _settings.LowHealthThreshold)
            {
                _bonusLevel = 0;
            }
            else
            {
                _bonusLevel = Mathf.Max(0, _bonusLevel - 1);
            }
        }

        /// <summary>Sonraki dalgaya eklenecek düşman sayısı.</summary>
        public int GetBonusEnemiesForNextWave()
        {
            return _settings.Enabled ? _bonusLevel : 0;
        }

        /// <summary>Doğuş aralığı çarpanı (1 = normal, büyük = daha seyrek).</summary>
        public float GetSpawnIntervalMultiplier(float healthPercent)
        {
            if (!_settings.Enabled) return 1f;

            float m = 1f;
            if (healthPercent < _settings.LowHealthThreshold) m *= _settings.LowHealthIntervalMultiplier;
            if (_deaths > 0) m *= _settings.PostDeathIntervalMultiplier;
            return m;
        }
    }
}
