using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Tek bir Araf oda şablonunun ağırlık, derinlik sınırı ve iç mekan prefab verilerini tutar.</summary>
    [CreateAssetMenu(fileName = "ArafRoom_New", menuName = "RetroFPS/Araf/Room Definition", order = 30)]
    public class ArafRoomDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private ArafRoomKind kind = ArafRoomKind.Combat;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(0)] private int minDepth = 0;
        [SerializeField, Min(1)] private int maxPerRun = 99;
        [SerializeField] private bool sealDoorsUntilCleared = true;

        /// <summary>Odanın arayüz veya günlüklerde gösterilecek adı.</summary>
        public string DisplayName => displayName;

        /// <summary>Odanın oynanış arketipi.</summary>
        public ArafRoomKind Kind => kind;

        /// <summary>Oda açıldığında örneklenecek iç mekan prefabı.</summary>
        public GameObject Prefab => prefab;

        /// <summary>Rastgele seçimdeki göreli ağırlık değeri.</summary>
        public float Weight => weight;

        /// <summary>Odanın seçilebileceği en düşük başlangıç derinliği.</summary>
        public int MinDepth => minDepth;

        /// <summary>Tek bir koşuda bu odanın en fazla kaç kez seçilebileceği.</summary>
        public int MaxPerRun => maxPerRun;

        /// <summary>Oda temizlenene kadar kapı bariyerlerinin kapanıp kapanmayacağı.</summary>
        public bool SealDoorsUntilCleared => sealDoorsUntilCleared;

        private void OnValidate()
        {
            if (weight < 0f) weight = 0f;
            if (minDepth < 0) minDepth = 0;
            if (maxPerRun < 1) maxPerRun = 1;
        }
    }
}
