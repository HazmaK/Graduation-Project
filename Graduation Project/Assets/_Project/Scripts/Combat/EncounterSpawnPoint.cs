using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Karşılaşma ve Dalga Direktörü (WaveSpawnDirector) için akıllı doğuş noktası düğümü.
    /// Düşman çakışmalarını önlemek için zemin/hava filtreleme, bekleme süresi (cooldown)
    /// ve sıfır GC tahsisli fiziksel alan kontrolü (Clearance Check) sunar.
    /// </summary>
    [DisallowMultipleComponent]
    public class EncounterSpawnPoint : MonoBehaviour
    {
        [Header("Düşman Tipi Uyumluluğu")]
        [Tooltip("Yürüyen / zemin düşmanları bu noktada doğabilir mi?")]
        [SerializeField] private bool allowGroundEnemies = true;

        [Tooltip("Uçan / hava düşmanları bu noktada doğabilir mi?")]
        [SerializeField] private bool allowAirEnemies = true;

        [Header("Bölge / Etiket (Zone Tag)")]
        [Tooltip("Level Designer için doğuş bölgesi etiketi (Örn: 'Balkon', 'Meydan', 'Kuzey'). Boş bırakılırsa tüm gruplar için geçerlidir.")]
        [SerializeField] private string zoneTag = string.Empty;

        [Header("Zamanlama ve Çakışma Önleme")]
        [Tooltip("Bu noktada art arda düşman belirmesini önleyen bekleme süresi (saniye)")]
        [SerializeField] private float cooldownDuration = 2.5f;

        [Tooltip("Doğuş alanında başka bir düşman veya engel olup olmadığını denetleyen yarıçap")]
        [SerializeField] private float clearanceRadius = 0.9f;

        [Header("Editör Görselleştirmesi")]
        [SerializeField] private Color groundGizmoColor = new Color(0.95f, 0.25f, 0.25f, 0.85f);
        [SerializeField] private Color airGizmoColor = new Color(0.65f, 0.20f, 0.95f, 0.85f);
        [SerializeField] private Color cooldownGizmoColor = new Color(0.40f, 0.40f, 0.40f, 0.40f);

        private float _lastSpawnTimestamp = -999f;
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        public bool AllowGroundEnemies => allowGroundEnemies;
        public bool AllowAirEnemies => allowAirEnemies;
        public string ZoneTag => zoneTag;
        public float ClearanceRadius => clearanceRadius;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
        public Vector3 ClearanceCenter => transform.position + Vector3.up * (clearanceRadius + 0.1f);
        public bool IsOnCooldown => (Time.time - _lastSpawnTimestamp) < cooldownDuration;

        /// <summary>
        /// Son doğuştan bu yana <paramref name="window"/> saniye geçmediyse nokta rezervedir: telgrafı süren düşman
        /// henüz fiziksel olarak orada olmadığı için alan "boş" görünür; aynı noktaya ikinci düşman konursa
        /// iki CharacterController üst üste doğar ve birbirini duvarın içinden dışarı itebilir.
        /// Cooldown gevşetilse bile rezervasyon gevşetilmez.
        /// </summary>
        public bool IsReserved(float window) => (Time.time - _lastSpawnTimestamp) < window;

        /// <summary>
        /// Noktanın yalnızca statik dünyaya (duvar, siper, zemin) göre boş olup olmadığı; düşman/oyuncu/pickup yok sayılır.
        /// false ise nokta geometrinin içinde kalmıştır ve oraya asla doğurulmamalıdır.
        /// </summary>
        public bool IsClearOfStaticGeometry(LayerMask obstacleMask) => IsPhysicallyClear(obstacleMask & GameLayers.GroundMask);

        /// <summary>
        /// Doğuş noktasını kod veya editör araçları üzerinden yapılandırır.
        /// </summary>
        public void Configure(bool allowGround, bool allowAir, string tag = "", float cooldown = 2.5f)
        {
            allowGroundEnemies = allowGround;
            allowAirEnemies = allowAir;
            zoneTag = tag ?? string.Empty;
            cooldownDuration = cooldown;
        }

        /// <summary>
        /// Doğuş noktasının fiziksel olarak boş ve engelsiz olup olmadığını sıfır GC tahsisiyle doğrular.
        /// </summary>
        public bool IsPhysicallyClear(LayerMask obstacleMask)
        {
            // Küre, zemine değmeyecek şekilde yarıçap kadar yukarıdan örneklenir. Eski 0.5m ofset, yarıçap 0.9m iken
            // zemin collider'ına her zaman değiyor ve nokta "dolu" sayılıp her doğuşta fallback'e düşülüyordu.
            int hitCount = Physics.OverlapSphereNonAlloc(ClearanceCenter, clearanceRadius, OverlapBuffer, obstacleMask, QueryTriggerInteraction.Ignore);
            return hitCount == 0;
        }

        /// <summary>
        /// Noktada bir düşman doğduğunda bekleme süresini başlatır.
        /// </summary>
        public void MarkAsSpawned()
        {
            _lastSpawnTimestamp = Time.time;
        }

        /// <summary>
        /// Bekleme süresini sıfırlar.
        /// </summary>
        public void ResetCooldown()
        {
            _lastSpawnTimestamp = -999f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color displayColor = IsOnCooldown ? cooldownGizmoColor : (allowAirEnemies && !allowGroundEnemies ? airGizmoColor : groundGizmoColor);

            Gizmos.color = displayColor;
            Gizmos.DrawWireSphere(ClearanceCenter, clearanceRadius);

            Vector3 forwardLine = transform.position + Vector3.up * 0.5f + transform.forward * 1.2f;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, forwardLine);

            UnityEditor.Handles.color = displayColor;
            UnityEditor.Handles.ArrowHandleCap(0, transform.position + Vector3.up * 0.5f, transform.rotation, 1.0f, EventType.Repaint);

            if (!string.IsNullOrEmpty(zoneTag))
            {
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                labelStyle.normal.textColor = displayColor;
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, $"[{zoneTag}]", labelStyle);
            }
        }
#endif
    }
}
