using UnityEngine;

namespace RetroFPS.Run
{
    /// <summary>
    /// Araf (Purgatory) sahnesinin kök denetleyicisi.
    /// Fizik ve NavMesh çakışmalarını önlemek için Araf içeriğini kule geometrisinden
    /// çok uzakta ve YUKARIDA (ör. Y = +1000) konumlandırın. Negatif Y kullanmayın:
    /// PlayerMovement Y &lt; 0 olduğunda oyuncuyu Y = 0.05'e geri çeker.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArafLevel : MonoBehaviour
    {
        [Header("Araf Sahne Bağlantıları")]
        [Tooltip("Oyuncunun Araf'a düştüğünde doğacağı nokta ve bakış yönü")]
        [SerializeField] private Transform entryPoint;

        [Tooltip("Araf sahnesi additive yüklendiğinde başlangıçta kapalı tutulacak içerik kökü")]
        [SerializeField] private GameObject contentRoot;

        /// <summary>
        /// Oyuncunun Araf'a giriş doğuş noktası.
        /// </summary>
        public Transform EntryPoint => entryPoint;

        /// <summary>
        /// Araf sahnesinin aktif/pasif yapılan içerik kök nesnesi.
        /// </summary>
        public GameObject ContentRoot => contentRoot;

        private void Awake()
        {
            if (contentRoot != null)
            {
                contentRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Araf içerik kökünü aktif veya pasif duruma getirir.
        /// </summary>
        /// <param name="active">İçerik aktif olsun mu?</param>
        public void SetContentActive(bool active)
        {
            if (contentRoot != null)
            {
                contentRoot.SetActive(active);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform point = entryPoint != null ? entryPoint : transform;
            Gizmos.color = new Color(0.85f, 0.1f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(point.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(point.position + Vector3.up, point.position + Vector3.up + point.forward * 1.5f);
        }
#endif
    }
}
