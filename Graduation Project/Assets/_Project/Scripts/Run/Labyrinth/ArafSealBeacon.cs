using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Mühür yuvasında 3B döngüsel ses yayan işaretçi. Mühür alındığında sesi kısarak durur ve nesneyi kapatır.
    /// Ses klibi atanmamışsa hata vermeden sessizce çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ArafSealBeacon : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeSeconds = 1f;

        private AudioSource _source;
        private float _initialVolume;
        private bool _hasInitialVolume;
        private bool _fading;
        private float _fadeElapsed;
        private float _fadeStartVolume;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _initialVolume = _source.volume;
            _hasInitialVolume = true;
        }

        private void OnEnable()
        {
            _fading = false;
            _fadeElapsed = 0f;

            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }

            if (_source == null) return;

            if (!_hasInitialVolume)
            {
                _initialVolume = _source.volume;
                _hasInitialVolume = true;
            }

            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.volume = _initialVolume;

            if (_source.clip != null && !_source.isPlaying)
            {
                _source.Play();
            }
        }

        private void OnDisable()
        {
            _fading = false;
            if (_source != null && _source.isPlaying)
            {
                _source.Stop();
            }
        }

        private void Update()
        {
            if (!_fading) return;

            _fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeSeconds > 0f ? Mathf.Clamp01(_fadeElapsed / fadeSeconds) : 1f;
            if (_source != null)
            {
                _source.volume = Mathf.Lerp(_fadeStartVolume, 0f, t);
            }

            if (t >= 1f)
            {
                _fading = false;
                if (_source != null)
                {
                    _source.volume = 0f;
                    _source.Stop();
                }
                gameObject.SetActive(false);
            }
        }

        /// <summary>Ses düzeyini <see cref="fadeSeconds"/> içinde sıfıra indirir, ardından durdurup nesneyi kapatır.</summary>
        public void Silence()
        {
            if (_fading) return;

            if (_source == null || _source.clip == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (fadeSeconds <= 0f || !gameObject.activeInHierarchy)
            {
                _source.volume = 0f;
                _source.Stop();
                gameObject.SetActive(false);
                return;
            }

            _fadeStartVolume = _source.volume;
            _fadeElapsed = 0f;
            _fading = true;
        }
    }
}
