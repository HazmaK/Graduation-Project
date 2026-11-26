using System.Collections;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Mühür yuvasının tavanından göğe yükselen ışık sütunu. Mühür alındığında sönümlenerek kapanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArafSealLightPillar : MonoBehaviour
    {
        [SerializeField] private Transform beam;
        [SerializeField] private Light glow;
        [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

        private bool _isLit = true;
        private bool _extinguishing;
        private bool _hasExtinguished;
        private Coroutine _fadeRoutine;

        /// <summary>Işık sütununun hâlâ yanıp yanmadığı.</summary>
        public bool IsLit => _isLit && gameObject.activeSelf;

        private void OnEnable()
        {
            _extinguishing = false;
            // Söndürülmüş (mührü alınmış) sütun, söndürme yarıda kesilse bile bir daha yanmaz.
            if (_hasExtinguished)
            {
                _isLit = false;
                ApplyExtinguishedState();
                gameObject.SetActive(false);
                return;
            }
            _isLit = true;
        }

        private void OnDisable()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
            _extinguishing = false;
            _isLit = false;
        }

        /// <summary>Işık sütununu ve parıltısını <see cref="fadeSeconds"/> içinde söndürüp nesneyi kapatır.</summary>
        public void Extinguish()
        {
            if (!gameObject.activeInHierarchy)
            {
                _isLit = false;
                _extinguishing = false;
                _hasExtinguished = true;
                ApplyExtinguishedState();
                gameObject.SetActive(false);
                return;
            }

            if (_extinguishing || !_isLit) return;

            _isLit = false;
            _hasExtinguished = true;

            if (fadeSeconds <= 0f)
            {
                ApplyExtinguishedState();
                gameObject.SetActive(false);
                return;
            }

            _extinguishing = true;
            _fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = beam != null ? beam.localScale : Vector3.zero;
            float startIntensity = glow != null ? glow.intensity : 0f;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                float factor = 1f - t;

                if (beam != null)
                {
                    beam.localScale = new Vector3(startScale.x * factor, startScale.y, startScale.z * factor);
                }

                if (glow != null)
                {
                    glow.intensity = startIntensity * factor;
                }

                yield return null;
            }

            ApplyExtinguishedState();
            _extinguishing = false;
            _fadeRoutine = null;
            gameObject.SetActive(false);
        }

        private void ApplyExtinguishedState()
        {
            if (beam != null)
            {
                Vector3 currentScale = beam.localScale;
                beam.localScale = new Vector3(0f, currentScale.y, 0f);
            }

            if (glow != null)
            {
                glow.intensity = 0f;
            }
        }
    }
}
