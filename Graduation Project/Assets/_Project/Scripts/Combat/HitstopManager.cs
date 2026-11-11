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
        private float _previousTimeScale;
        private float _previousFixedDeltaTime;

        public bool IsActive => _isFrozen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
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
                RestoreTime();
            }
        }

        /// <summary>
        /// Darbe anında mikro dondurma (Hitstop) tetikler.
        /// </summary>
        /// <param name="duration">Dondurma süresi (sn cinsinden, örn 0.05s)</param>
        /// <param name="timeScale">Dondurma sırasındaki minimum zaman akışı</param>
        public void TriggerHitstop(float duration = 0.05f, float timeScale = 0.03f)
        {
            if (duration <= 0f || !isActiveAndEnabled || Instance != this) return;

            if (!_isFrozen)
            {
                _previousTimeScale = Time.timeScale;
                _previousFixedDeltaTime = Time.fixedDeltaTime;
            }
            _hitstopTimer = Mathf.Max(_hitstopTimer, duration);
            Time.timeScale = Mathf.Min(Time.timeScale, Mathf.Clamp01(timeScale));
            float scaleRatio = _previousTimeScale > 0f ? Time.timeScale / _previousTimeScale : 1f;
            // Unity requires a positive physics step even for a fully paused hitstop.
            Time.fixedDeltaTime = Mathf.Max(0.000001f, _previousFixedDeltaTime * scaleRatio);
            _isFrozen = true;
        }

        private void OnDisable()
        {
            RestoreTime();
        }

        private void OnDestroy()
        {
            RestoreTime();
            if (Instance == this) Instance = null;
        }

        private void RestoreTime()
        {
            if (!_isFrozen) return;
            Time.timeScale = _previousTimeScale;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            _hitstopTimer = 0f;
            _isFrozen = false;
        }
    }
}
