using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Oyuncu arena girişine veya koridora adım attığında karşılaşmayı (Encounter)
    /// otomatik olarak başlatan ve ardından kendini devreden çıkaran tetikleyici hacim.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class EncounterTriggerVolume : MonoBehaviour
    {
        [Header("Karşılaşma Direktörü Referansı")]
        [Tooltip("Tetiklenecek WaveSpawnDirector. Boş bırakılırsa sahnede otomatik aranır.")]
        [SerializeField] private WaveSpawnDirector waveDirector;

        [Tooltip("Tetiklendikten sonra bu tetikleyici hacmi tamamen devre dışı bırak")]
        [SerializeField] private bool disableAfterTrigger = true;

        private bool _hasTriggered = false;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (waveDirector == null)
            {
                waveDirector = Object.FindAnyObjectByType<WaveSpawnDirector>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;

            // Oyuncu kontrolü
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
            {
                _hasTriggered = true;

                if (waveDirector != null)
                {
                    waveDirector.StartEncounter();
                }

                if (disableAfterTrigger)
                {
                    gameObject.SetActive(false);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.40f, 0.10f, 0.35f);
            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(1.0f, 0.60f, 0.10f, 0.90f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
#endif
    }
}
