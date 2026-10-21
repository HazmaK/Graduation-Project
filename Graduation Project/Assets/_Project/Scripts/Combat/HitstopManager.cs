using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Vuruş Hissi (Hitstop / Micro-Freeze) Yöneticisi.
    /// Ağır vuruşlarda zamanı 0.04 - 0.08 saniyeliğine mikro ölçekte durdurarak
    /// ULTRAKILL ve dövüş oyunlarındaki o tok, kemik kırıcı darbe hissini üretir.
    /// Sıfır GC tahsisiyle çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitstopManager : MonoBehaviour
    {
        public static HitstopManager Instance { get; private set; }

        private float _hitstopTimer = 0f;
        private bool _isFrozen = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (!_isFrozen) return;

            // Zaman ölçeğinden etkilenmeyen gerçek dünya süresini kullan
            _hitstopTimer -= Time.unscaledDeltaTime;

            if (_hitstopTimer <= 0f)
            {
                Time.timeScale = 1.0f;
                Time.fixedDeltaTime = 0.02f;
                _isFrozen = false;
            }
        }

        /// <summary>
        /// Darbe anında mikro dondurma (Hitstop) tetikler.
        /// </summary>
        /// <param name="duration">Dondurma süresi (sn cinsinden, örn 0.05s)</param>
        /// <param name="timeScale">Dondurma sırasındaki minimum zaman akışı</param>
        public void TriggerHitstop(float duration = 0.05f, float timeScale = 0.03f)
        {
            if (duration <= 0f) return;

            _hitstopTimer = duration;
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = 0.02f * timeScale;
            _isFrozen = true;
        }

        private void OnDisable()
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            _isFrozen = false;
        }
    }
}
